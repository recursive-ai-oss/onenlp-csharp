using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using RecursiveAI.OneModel;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;

namespace RecursiveAI.OneNlp;

/// <summary>
/// Reads the checkpoint files of <see cref="Checkpoint"/> one tensor at a time from disk, so that a file larger than 2 GB loads (the library's
/// loader reads the whole file into one array). The format is the same (magic ONEMCKPT, a JSON manifest, 64-byte-aligned little-endian
/// tensors); everything is checked against the target before the first tensor is changed.
/// </summary>
internal static class CheckpointFile
{
    private const int HeaderSize = 64;
    private const long MaxManifest = 256L << 20;

    private readonly record struct Entry(string Name, string DType, int[] Dims, long Offset, long Length);

    /// <summary>Loads a checkpoint into a module and optionally an optimizer, as <see cref="Checkpoint.Load(string, NnModule, Optimizer?)"/> does.</summary>
    /// <returns>The extra values of the file.</returns>
    /// <exception cref="InvalidDataException">The file is malformed or does not fit the target; nothing has been changed.</exception>
    public static IReadOnlyDictionary<string, long> Load(string path, NnModule model, Optimizer? optimizer = null)
    {
        using FileStream f = File.OpenRead(path);
        byte[] header = new byte[HeaderSize];
        f.ReadExactly(header);
        if (!header.AsSpan(0, 8).SequenceEqual("ONEMCKPT"u8) || BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8)) != 1 || BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(12)) != 0)
        {
            throw new InvalidDataException("Not a version 1 checkpoint file.");
        }

        long manifestLength = checked((long)BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(24)));
        long dataOffset = checked((long)BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(32)));
        long dataLength = checked((long)BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(40)));
        long fileLength = checked((long)BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(48)));
        if (fileLength != f.Length || BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(16)) != HeaderSize || manifestLength is < 2 or > MaxManifest ||
            HeaderSize + manifestLength > fileLength || dataOffset % 64 != 0 || dataOffset < HeaderSize + manifestLength || dataLength != fileLength - dataOffset)
        {
            throw new InvalidDataException("The checkpoint header is inconsistent with the file length.");
        }

        byte[] manifestBytes = new byte[manifestLength];
        f.ReadExactly(manifestBytes);
        using JsonDocument doc = JsonDocument.Parse(manifestBytes, new JsonDocumentOptions { MaxDepth = 32 });
        JsonElement root = doc.RootElement;
        if (root.GetProperty("format").GetString() != "onemodel-checkpoint" || root.GetProperty("version").GetInt32() != 1)
        {
            throw new InvalidDataException("The manifest is not a version 1 onemodel-checkpoint.");
        }

        List<(string Name, Tensor Tensor)> modelState = model.StateDict();
        List<Entry> modelEntries = Entries(root.GetProperty("model"));
        Check("model", modelEntries, modelState, dataLength);

        List<Entry>? optEntries = null;
        Dictionary<string, float>? hyper = null;
        long step = 0;
        bool hasOptimizer = root.TryGetProperty("optimizer", out JsonElement opt) && opt.ValueKind == JsonValueKind.Object;
        if (optimizer is not null)
        {
            if (!hasOptimizer || opt.GetProperty("kind").GetString() != optimizer.Kind)
            {
                throw new InvalidDataException("The checkpoint holds no optimizer state of kind '" + optimizer.Kind + "'.");
            }

            step = opt.GetProperty("step").GetInt64();
            hyper = new Dictionary<string, float>(StringComparer.Ordinal);
            foreach (JsonElement hp in opt.GetProperty("hyper").EnumerateArray())
            {
                hyper[hp.GetProperty("name").GetString()!] = BitConverter.Int32BitsToSingle(unchecked((int)(uint)hp.GetProperty("bits").GetInt64()));
            }

            foreach ((string name, _) in optimizer.HyperParameters())
            {
                if (!hyper.ContainsKey(name))
                {
                    throw new InvalidDataException("The checkpoint lacks hyper-parameter '" + name + "'.");
                }
            }

            optEntries = Entries(opt.GetProperty("state"));
            Check("optimizer state", optEntries, [.. optimizer.StateTensors], dataLength);
            optimizer.SetHyperParameters(hyper);
        }

        var extra = new Dictionary<string, long>(StringComparer.Ordinal);
        if (root.TryGetProperty("extra", out JsonElement ex) && ex.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement e in ex.EnumerateArray())
            {
                extra[e.GetProperty("key").GetString()!] = e.GetProperty("value").GetInt64();
            }
        }

        Fill(f, dataOffset, modelEntries, modelState);
        if (optEntries is not null && optimizer is not null)
        {
            Fill(f, dataOffset, optEntries, [.. optimizer.StateTensors]);
            optimizer.RestoreStepCount(step);
        }

        return extra;
    }

    private static List<Entry> Entries(JsonElement array)
    {
        var list = new List<Entry>();
        foreach (JsonElement e in array.EnumerateArray())
        {
            list.Add(new Entry(e.GetProperty("name").GetString()!, e.GetProperty("dtype").GetString()!, [.. e.GetProperty("shape").EnumerateArray().Select(d => d.GetInt32())],
                e.GetProperty("offset").GetInt64(), e.GetProperty("length").GetInt64()));
        }

        return list;
    }

    private static string DTypeName(Tensor t) => t.DType switch { DType.F32 => "f32", DType.I32 => "i32", DType.I64 => "i64", _ => "?" };

    private static void Check(string what, List<Entry> entries, List<(string Name, Tensor Tensor)> dest, long dataLength)
    {
        if (entries.Count != dest.Count)
        {
            throw new InvalidDataException($"The checkpoint has {entries.Count} {what} tensors, the target has {dest.Count}.");
        }

        for (int i = 0; i < entries.Count; i++)
        {
            Entry e = entries[i];
            Tensor t = dest[i].Tensor;
            long expected = (long)t.Length * (t.DType == DType.I64 ? 8 : 4);
            if (e.Name != dest[i].Name || e.DType != DTypeName(t) || !e.Dims.AsSpan().SequenceEqual(t.Shape.Dimensions))
            {
                throw new InvalidDataException($"{what} entry {i} is '{e.Name}' {e.DType}[{string.Join(',', e.Dims)}], the target expects '{dest[i].Name}'.");
            }

            if (e.Length != expected || e.Offset < 0 || e.Offset > dataLength || e.Length > dataLength - e.Offset)
            {
                throw new InvalidDataException($"{what} tensor '{e.Name}' lies outside the data region.");
            }
        }
    }

    private static void Fill(FileStream f, long dataOffset, List<Entry> entries, List<(string Name, Tensor Tensor)> dest)
    {
        byte[] buffer = [];
        for (int i = 0; i < entries.Count; i++)
        {
            Entry e = entries[i];
            if (e.Length > int.MaxValue)
            {
                throw new InvalidDataException($"Tensor '{e.Name}' is larger than 2 GB.");
            }

            if (buffer.Length < e.Length)
            {
                buffer = new byte[e.Length];
            }

            f.Seek(dataOffset + e.Offset, SeekOrigin.Begin);
            f.ReadExactly(buffer.AsSpan(0, (int)e.Length));
            ReadOnlySpan<byte> src = buffer.AsSpan(0, (int)e.Length);
            Tensor t = dest[i].Tensor;
            switch (t.DType)
            {
                case DType.F32:
                    Span<float> fl = t.MutableData<float>();
                    for (int k = 0; k < fl.Length; k++)
                    {
                        fl[k] = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(src[(4 * k)..]));
                    }

                    break;
                case DType.I32:
                    Span<int> n = t.MutableData<int>();
                    for (int k = 0; k < n.Length; k++)
                    {
                        n[k] = BinaryPrimitives.ReadInt32LittleEndian(src[(4 * k)..]);
                    }

                    break;
                default:
                    Span<long> l = t.MutableData<long>();
                    for (int k = 0; k < l.Length; k++)
                    {
                        l[k] = BinaryPrimitives.ReadInt64LittleEndian(src[(8 * k)..]);
                    }

                    break;
            }
        }
    }

    /// <summary>SHA-256 of the checkpoint bytes of a module, written to a stream that keeps nothing (the file may be larger than a byte array can be).</summary>
    public static string HashOf(NnModule model)
    {
        using var hash = new HashingStream();
        Checkpoint.Save(hash, model);
        return hash.Finish();
    }

    private sealed class HashingStream : Stream
    {
        private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        public string Finish() => Convert.ToHexStringLower(_hash.GetHashAndReset());

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => _hash.AppendData(buffer, offset, count);

        public override void Write(ReadOnlySpan<byte> buffer) => _hash.AppendData(buffer);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _hash.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
