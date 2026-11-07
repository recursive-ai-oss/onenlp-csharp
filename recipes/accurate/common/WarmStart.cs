using System.Buffers.Binary;
using System.Globalization;
using System.Text.Json;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;

namespace RecursiveAI.OneNlp.Recipes.Accurate;

/// <summary>What <see cref="WarmStart.Apply"/> did.</summary>
/// <param name="Copied">Tensors copied.</param>
/// <param name="Missing">Tensors of the target with no counterpart in the checkpoint (they keep their initial values).</param>
/// <param name="Mismatched">Tensors with a counterpart of another type or shape (not copied).</param>
/// <param name="Unused">Tensors of the checkpoint that no target tensor took.</param>
/// <param name="Notes">The first few mismatches, for the log.</param>
public sealed record WarmStartReport(int Copied, int Missing, int Mismatched, int Unused, IReadOnlyList<string> Notes);

/// <summary>
/// Starts a run that has more heads than an earlier one from the earlier run's weights. A checkpoint names a head by its position
/// ("head3.…"), so a pipeline with other heads cannot load it as a whole; this copies tensor by tensor, matching the encoder by name and
/// each head by the name the caller gives it, and leaves the optimizer fresh. It reads the checkpoint format of
/// <see cref="Checkpoint"/> (magic ONEMCKPT, a JSON manifest of tensors, 64-byte-aligned little-endian data) and touches only the model part.
/// </summary>
public static class WarmStart
{
    private const int HeaderSize = 64;
    private const long MaxManifest = 256L << 20;

    /// <summary>Copies matching tensors from a checkpoint into a target state.</summary>
    /// <param name="checkpointPath">A <c>last.ckpt</c> or <c>best.ckpt</c> file.</param>
    /// <param name="target">The target's state (<c>StateModule.StateDict()</c>), whose names are <c>encoder0.…</c> and <c>head{i}.…</c>.</param>
    /// <param name="oldHeadNames">The head names of the checkpoint's pipeline in order: element i is what the checkpoint calls head i.</param>
    /// <param name="newHeadNames">The head names of the target's pipeline in order.</param>
    /// <exception cref="InvalidDataException">The file is not a checkpoint or its offsets are inconsistent.</exception>
    public static WarmStartReport Apply(string checkpointPath, IReadOnlyList<(string Name, Tensor Tensor)> target, IReadOnlyList<string> oldHeadNames, IReadOnlyList<string> newHeadNames)
    {
        ArgumentNullException.ThrowIfNull(target);
        using FileStream f = File.OpenRead(checkpointPath);
        byte[] header = new byte[HeaderSize];
        f.ReadExactly(header);
        if (!header.AsSpan(0, 8).SequenceEqual("ONEMCKPT"u8))
        {
            throw new InvalidDataException("Not a checkpoint file.");
        }

        long manifestOffset = (long)BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(16));
        long manifestLength = (long)BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(24));
        long dataOffset = (long)BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(32));
        long fileLength = (long)BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(48));
        if (manifestOffset != HeaderSize || manifestLength is < 2 or > MaxManifest || dataOffset < 0 || fileLength != f.Length || manifestOffset + manifestLength > fileLength || dataOffset > fileLength)
        {
            throw new InvalidDataException("The checkpoint header is inconsistent.");
        }

        byte[] manifestBytes = new byte[manifestLength];
        f.Seek(manifestOffset, SeekOrigin.Begin);
        f.ReadExactly(manifestBytes);
        using JsonDocument manifest = JsonDocument.Parse(manifestBytes, new JsonDocumentOptions { MaxDepth = 32 });
        var entries = new Dictionary<string, (string DType, int[] Dims, long Offset, long Length)>(StringComparer.Ordinal);
        foreach (JsonElement e in manifest.RootElement.GetProperty("model").EnumerateArray())
        {
            entries[e.GetProperty("name").GetString()!] = (e.GetProperty("dtype").GetString()!, [.. e.GetProperty("shape").EnumerateArray().Select(d => d.GetInt32())],
                e.GetProperty("offset").GetInt64(), e.GetProperty("length").GetInt64());
        }

        // The checkpoint's name for a target tensor: heads are renamed through their names, the encoder is kept.
        var newIndex = newHeadNames.Select((n, i) => (n, i)).ToDictionary(p => p.i, p => p.n);
        var oldIndexOfName = oldHeadNames.Select((n, i) => (n, i)).ToDictionary(p => p.n, p => p.i, StringComparer.Ordinal);
        int copied = 0, missing = 0, mismatched = 0;
        var used = new HashSet<string>(StringComparer.Ordinal);
        var notes = new List<string>();
        foreach ((string name, Tensor tensor) in target)
        {
            string source = name;
            if (name.StartsWith("head", StringComparison.Ordinal))
            {
                int dot = name.IndexOf('.', StringComparison.Ordinal);
                int index = int.Parse(name.AsSpan(4, (dot < 0 ? name.Length : dot) - 4), CultureInfo.InvariantCulture);
                if (!oldIndexOfName.TryGetValue(newIndex[index], out int oldIndex))
                {
                    missing++;
                    continue;
                }

                source = "head" + oldIndex.ToString(CultureInfo.InvariantCulture) + (dot < 0 ? string.Empty : name[dot..]);
            }

            if (!entries.TryGetValue(source, out var entry))
            {
                missing++;
                continue;
            }

            long expected = (long)tensor.Length * (tensor.DType == DType.I64 ? 8 : 4);
            string dtype = tensor.DType switch { DType.F32 => "f32", DType.I32 => "i32", DType.I64 => "i64", _ => "?" };
            if (entry.DType != dtype || !entry.Dims.AsSpan().SequenceEqual(tensor.Shape.Dimensions) || entry.Length != expected || entry.Offset < 0 || dataOffset + entry.Offset + entry.Length > fileLength)
            {
                mismatched++;
                if (notes.Count < 8)
                {
                    notes.Add($"{name} <- {source}: checkpoint {entry.DType}[{string.Join(',', entry.Dims)}], target {dtype}{tensor.Shape}");
                }

                continue;
            }

            byte[] bytes = new byte[entry.Length];
            f.Seek(dataOffset + entry.Offset, SeekOrigin.Begin);
            f.ReadExactly(bytes);
            Decode(bytes, tensor);
            used.Add(source);
            copied++;
        }

        return new WarmStartReport(copied, missing, mismatched, entries.Count - used.Count, notes);
    }

    private static void Decode(ReadOnlySpan<byte> src, in Tensor t)
    {
        switch (t.DType)
        {
            case DType.F32:
                Span<float> f = t.MutableData<float>();
                for (int i = 0; i < f.Length; i++)
                {
                    f[i] = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(src[(4 * i)..]));
                }

                break;
            case DType.I32:
                Span<int> n = t.MutableData<int>();
                for (int i = 0; i < n.Length; i++)
                {
                    n[i] = BinaryPrimitives.ReadInt32LittleEndian(src[(4 * i)..]);
                }

                break;
            default:
                Span<long> l = t.MutableData<long>();
                for (int i = 0; i < l.Length; i++)
                {
                    l[i] = BinaryPrimitives.ReadInt64LittleEndian(src[(8 * i)..]);
                }

                break;
        }
    }
}
