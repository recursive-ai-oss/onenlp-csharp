using System.Buffers.Binary;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace RecursiveAI.OneNlp.Neural.Encoders;

/// <summary>New values for one float32 tensor of an ONNX graph: an initializer, or a name an <c>Identity</c> node gives to one.</summary>
/// <param name="Name">The tensor's name in the graph.</param>
/// <param name="Dims">The dimensions, which must equal those of the tensor being replaced.</param>
/// <param name="Values">Produces the values in row-major order; called once, while the file is written, so only one tensor's copy is alive at a time.</param>
internal sealed record WeightPatch(string Name, long[] Dims, Func<float[]> Values);

/// <summary>
/// Writes a copy of an ONNX model file in which named float32 tensors carry new values and everything else is byte for byte what it was.
/// </summary>
/// <remarks>
/// <para>
/// The file is protobuf (<c>onnx.proto3</c>, ModelProto field 7 = graph, GraphProto field 1 = node, field 5 = initializer, TensorProto fields
/// dims 1, data_type 2, name 8, raw_data 9; protobuf encoding specification for keys, varints and lengths). The writer indexes the file's top-level
/// fields and the graph's fields with reads at offsets, never loading a tensor, then writes the output field by field: untouched fields are copied
/// as byte ranges; a replaced initializer is re-encoded with <c>raw_data</c> holding the new values.
/// </para>
/// <para>
/// <b>Shared and aliased tensors.</b> A PyTorch export stores equal tensors once and names the others with <c>Identity</c> nodes (the zero biases of
/// a freshly made model). A patch for a name produced by such a node adds an initializer of that name and drops the node. A patch for an initializer
/// that other, unpatched names alias is refused, because replacing it would change them too.
/// </para>
/// <para>
/// <b>Bounds.</b> Every length is compared with the bytes that remain in its parent before anything is read or allocated; names are at most 4 KiB;
/// a node larger than 64 KiB is never read (an <c>Identity</c> node is a few dozen bytes); at most 4 million fields per message; the file is at
/// most 2 GiB − 1 (protobuf's own limit). Tensors stored in external data files are refused, since the copy may live elsewhere.
/// </para>
/// </remarks>
internal static class OnnxWeightPatcher
{
    private const int FloatType = 1;
    private const int MaxFields = 4_000_000;
    private const int MaxNameBytes = 4096;
    private const int MaxNodeBytes = 1 << 16;
    private const int ModelGraphField = 7;
    private const int GraphNodeField = 1;
    private const int GraphInitializerField = 5;

    /// <summary>Writes <paramref name="destinationPath"/>, replacing the file if it exists.</summary>
    /// <exception cref="InvalidDataException">The source is not a well-formed model, or lacks a patched tensor, or a patched tensor is not float32 of the given dimensions.</exception>
    /// <exception cref="NotSupportedException">The model keeps tensors in external files, a patched tensor is aliased by an unpatched name, or the result would exceed 2 GiB.</exception>
    public static void Patch(string sourcePath, string destinationPath, IReadOnlyList<WeightPatch> patches)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourcePath);
        ArgumentException.ThrowIfNullOrEmpty(destinationPath);
        ArgumentNullException.ThrowIfNull(patches);
        var byName = new Dictionary<string, WeightPatch>(StringComparer.Ordinal);
        foreach (WeightPatch p in patches)
        {
            if (!byName.TryAdd(p.Name, p))
            {
                throw new ArgumentException($"Two patches name '{p.Name}'.", nameof(patches));
            }
        }

        using SafeFileHandle handle = File.OpenHandle(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.RandomAccess);
        long length = RandomAccess.GetLength(handle);
        if (length > int.MaxValue)
        {
            throw new InvalidDataException("An ONNX file is at most 2 GiB − 1 bytes.");
        }

        var src = new Source(handle, length);
        List<Field> top = src.Fields(0, length);
        int graphIndex = -1;
        for (int i = 0; i < top.Count; i++)
        {
            if (top[i].Number == ModelGraphField && top[i].Wire == 2)
            {
                if (graphIndex >= 0)
                {
                    throw new InvalidDataException("The model has two graphs.");
                }

                graphIndex = i;
            }
        }

        if (graphIndex < 0)
        {
            throw new InvalidDataException("The model has no graph.");
        }

        Field graph = top[graphIndex];
        List<Field> fields = src.Fields(graph.BodyStart, graph.BodyStart + graph.BodyLength);

        // First pass: what is each initializer and node?
        var action = new Step[fields.Count];
        var tensorNames = new string[fields.Count];
        var initializerNames = new HashSet<string>(StringComparer.Ordinal);
        var inPlace = new HashSet<string>(StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < fields.Count; i++)
        {
            Field f = fields[i];
            if (f.Number != GraphInitializerField || f.Wire != 2)
            {
                continue;
            }

            (string name, int dataType, bool external, long[] dims) = src.Tensor(f.BodyStart, f.BodyStart + f.BodyLength);
            initializerNames.Add(name);
            tensorNames[i] = name;
            if (byName.TryGetValue(name, out WeightPatch? patch))
            {
                if (dataType != FloatType || !dims.AsSpan().SequenceEqual(patch.Dims))
                {
                    throw new InvalidDataException($"The tensor '{name}' is not float32 with the dimensions of its replacement.");
                }

                action[i] = Step.Replace;
                inPlace.Add(name);
                used.Add(name);
            }
            else if (external)
            {
                throw new NotSupportedException($"The tensor '{name}' is stored in an external file; write the model with its weights inside the file first.");
            }
        }

        for (int i = 0; i < fields.Count; i++)
        {
            Field f = fields[i];
            if (f.Number != GraphNodeField || f.Wire != 2 || f.BodyLength > MaxNodeBytes)
            {
                continue;
            }

            (string opType, List<string> inputs, List<string> outputs) = src.Node(f.BodyStart, f.BodyStart + f.BodyLength);
            if (opType != "Identity" || inputs.Count != 1 || outputs.Count != 1)
            {
                continue;
            }

            if (byName.ContainsKey(outputs[0]))
            {
                if (initializerNames.Contains(outputs[0]))
                {
                    throw new InvalidDataException($"The name '{outputs[0]}' is both an initializer and an Identity output.");
                }

                action[i] = Step.Drop;
                used.Add(outputs[0]);
            }
            else if (inPlace.Contains(inputs[0]))
            {
                throw new NotSupportedException($"The tensor '{inputs[0]}' is shared with '{outputs[0]}', which is not being replaced; replacing it would change both.");
            }
        }

        foreach (WeightPatch p in patches)
        {
            if (!used.Contains(p.Name))
            {
                throw new InvalidDataException($"The graph has no initializer or Identity output '{p.Name}'.");
            }
        }

        // Sizes.
        long graphLength = 0;
        for (int i = 0; i < fields.Count; i++)
        {
            graphLength += action[i] switch
            {
                Step.Drop => 0,
                Step.Replace => EncodedTensorField(byName[tensorNames[i]]),
                _ => fields[i].End - fields[i].Start,
            };
        }

        var added = patches.Where(p => !inPlace.Contains(p.Name)).ToList();
        foreach (WeightPatch p in added)
        {
            graphLength += EncodedTensorField(p);
        }

        long total = length - (graph.End - graph.Start) + KeySize(ModelGraphField) + VarintSize((ulong)graphLength) + graphLength;
        if (total > int.MaxValue)
        {
            throw new NotSupportedException("The patched model would exceed 2 GiB, the most an ONNX file can hold.");
        }

        string temp = destinationPath + ".tmp";
        try
        {
            using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16))
            {
                foreach (Field t in top)
                {
                    if (t.Start == graph.Start)
                    {
                        WriteKey(output, ModelGraphField, 2);
                        WriteVarint(output, (ulong)graphLength);
                        for (int i = 0; i < fields.Count; i++)
                        {
                            switch (action[i])
                            {
                                case Step.Drop:
                                    break;
                                case Step.Replace:
                                    WriteTensorField(output, byName[tensorNames[i]]);
                                    break;
                                default:
                                    src.CopyTo(output, fields[i].Start, fields[i].End);
                                    break;
                            }
                        }

                        foreach (WeightPatch p in added)
                        {
                            WriteTensorField(output, p);
                        }
                    }
                    else
                    {
                        src.CopyTo(output, t.Start, t.End);
                    }
                }

                if (output.Length != total)
                {
                    throw new InvalidOperationException("The patched model has an unexpected length.");
                }
            }

            File.Move(temp, destinationPath, overwrite: true);
        }
        catch
        {
            File.Delete(temp);
            throw;
        }
    }

    private enum Step : byte
    {
        Copy,
        Replace,
        Drop,
    }

    // TensorProto: dims (1, unpacked), data_type (2), name (8), raw_data (9).
    private static byte[] TensorHeader(WeightPatch p)
    {
        using var m = new MemoryStream();
        foreach (long d in p.Dims)
        {
            WriteKey(m, 1, 0);
            WriteVarint(m, (ulong)d);
        }

        WriteKey(m, 2, 0);
        WriteVarint(m, FloatType);
        byte[] name = Encoding.UTF8.GetBytes(p.Name);
        WriteKey(m, 8, 2);
        WriteVarint(m, (ulong)name.Length);
        m.Write(name);
        return m.ToArray();
    }

    private static long ElementCount(WeightPatch p)
    {
        long n = 1;
        foreach (long d in p.Dims)
        {
            if (d < 0 || (d != 0 && n > int.MaxValue / 4 / d))
            {
                throw new InvalidDataException($"The tensor '{p.Name}' is too large for an ONNX file.");
            }

            n *= d;
        }

        return n;
    }

    private static long TensorBodyLength(WeightPatch p)
    {
        long raw = ElementCount(p) * 4;
        return TensorHeader(p).Length + KeySize(9) + VarintSize((ulong)raw) + raw;
    }

    private static long EncodedTensorField(WeightPatch p)
    {
        long body = TensorBodyLength(p);
        return KeySize(GraphInitializerField) + VarintSize((ulong)body) + body;
    }

    private static void WriteTensorField(Stream output, WeightPatch p)
    {
        float[] values = p.Values();
        if (values.Length != ElementCount(p))
        {
            throw new InvalidOperationException($"The replacement for '{p.Name}' has {values.Length} values, not {ElementCount(p)}.");
        }

        byte[] header = TensorHeader(p);
        long raw = values.LongLength * 4;
        WriteKey(output, GraphInitializerField, 2);
        WriteVarint(output, (ulong)TensorBodyLength(p));
        output.Write(header);
        WriteKey(output, 9, 2);
        WriteVarint(output, (ulong)raw);
        var chunk = new byte[1 << 16];
        for (int at = 0; at < values.Length;)
        {
            int n = Math.Min(chunk.Length / 4, values.Length - at);
            for (int i = 0; i < n; i++)
            {
                BinaryPrimitives.WriteSingleLittleEndian(chunk.AsSpan(i * 4), values[at + i]);
            }

            output.Write(chunk, 0, n * 4);
            at += n;
        }
    }

    private static int VarintSize(ulong v)
    {
        int n = 1;
        while (v >= 0x80)
        {
            v >>= 7;
            n++;
        }

        return n;
    }

    private static int KeySize(int field) => VarintSize((ulong)(field << 3));

    private static void WriteKey(Stream s, int field, int wire) => WriteVarint(s, (ulong)((field << 3) | wire));

    private static void WriteVarint(Stream s, ulong v)
    {
        while (v >= 0x80)
        {
            s.WriteByte((byte)(v | 0x80));
            v >>= 7;
        }

        s.WriteByte((byte)v);
    }

    private readonly record struct Field(int Number, int Wire, long Start, long BodyStart, long BodyLength, long End, ulong Value);

    /// <summary>Reads protobuf messages from a file at offsets, checking every length against the end of the enclosing message.</summary>
    private sealed class Source(SafeFileHandle handle, long length)
    {
        private readonly byte[] _scratch = new byte[10];

        public List<Field> Fields(long from, long to)
        {
            var list = new List<Field>();
            long pos = from;
            while (pos < to)
            {
                long start = pos;
                ulong key = Varint(ref pos, to);
                int wire = (int)(key & 7);
                ulong number = key >> 3;
                if (number == 0 || number > int.MaxValue)
                {
                    throw new InvalidDataException("A field number is out of range.");
                }

                long bodyStart = pos;
                long bodyLength = 0;
                ulong value = 0;
                switch (wire)
                {
                    case 0:
                        value = Varint(ref pos, to);
                        break;
                    case 1:
                        bodyLength = 8;
                        pos += 8;
                        break;
                    case 2:
                        ulong n = Varint(ref pos, to);
                        if (n > (ulong)(to - pos))
                        {
                            throw new InvalidDataException("A length runs past the end of its message.");
                        }

                        bodyStart = pos;
                        bodyLength = (long)n;
                        pos += bodyLength;
                        break;
                    case 5:
                        bodyLength = 4;
                        pos += 4;
                        break;
                    default:
                        throw new InvalidDataException("The file uses a protobuf wire type ONNX does not.");
                }

                if (pos > to)
                {
                    throw new InvalidDataException("A field runs past the end of its message.");
                }

                if (list.Count >= MaxFields)
                {
                    throw new InvalidDataException("A message has too many fields.");
                }

                list.Add(new Field((int)number, wire, start, bodyStart, bodyLength, pos, value));
            }

            return list;
        }

        public (string Name, int DataType, bool External, long[] Dims) Tensor(long from, long to)
        {
            string name = "";
            int type = 0;
            bool external = false;
            var dims = new List<long>();
            foreach (Field f in Fields(from, to))
            {
                switch (f.Number, f.Wire)
                {
                    case (1, 0):
                        dims.Add((long)f.Value);
                        break;
                    case (1, 2):
                        long p = f.BodyStart;
                        long e = f.BodyStart + f.BodyLength;
                        while (p < e)
                        {
                            dims.Add((long)Varint(ref p, e));
                        }

                        break;
                    case (2, 0):
                        type = (int)f.Value;
                        break;
                    case (8, 2):
                        name = Text(f);
                        break;
                    case (13, 2):
                    case (14, 0) when f.Value == 1:
                        external = true;
                        break;
                }
            }

            return (name, type, external, [.. dims]);
        }

        public (string OpType, List<string> Inputs, List<string> Outputs) Node(long from, long to)
        {
            string op = "";
            var inputs = new List<string>();
            var outputs = new List<string>();
            foreach (Field f in Fields(from, to))
            {
                if (f.Wire != 2 || f.BodyLength > MaxNameBytes)
                {
                    continue;
                }

                switch (f.Number)
                {
                    case 1:
                        inputs.Add(Text(f));
                        break;
                    case 2:
                        outputs.Add(Text(f));
                        break;
                    case 4:
                        op = Text(f);
                        break;
                }
            }

            return (op, inputs, outputs);
        }

        public void CopyTo(Stream output, long from, long to)
        {
            var buffer = new byte[(int)Math.Min(1 << 20, Math.Max(1, to - from))];
            for (long pos = from; pos < to;)
            {
                int n = (int)Math.Min(buffer.Length, to - pos);
                int read = RandomAccess.Read(handle, buffer.AsSpan(0, n), pos);
                if (read <= 0)
                {
                    throw new InvalidDataException("The file ended while it was being copied.");
                }

                output.Write(buffer, 0, read);
                pos += read;
            }
        }

        private string Text(Field f)
        {
            if (f.BodyLength > MaxNameBytes)
            {
                throw new InvalidDataException("A name is longer than 4 KiB.");
            }

            var bytes = new byte[(int)f.BodyLength];
            ReadExactly(bytes, f.BodyStart);
            try
            {
                return new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (ArgumentException e)
            {
                throw new InvalidDataException("A name is not valid UTF-8.", e);
            }
        }

        private ulong Varint(ref long pos, long end)
        {
            int n = (int)Math.Min(10, end - pos);
            if (n <= 0)
            {
                throw new InvalidDataException("A varint is cut off by the end of its message.");
            }

            ReadExactly(_scratch.AsSpan(0, n), pos);
            ulong v = 0;
            for (int i = 0; i < n; i++)
            {
                if (i == 9 && _scratch[i] > 1)
                {
                    throw new InvalidDataException("A varint overflows 64 bits.");
                }

                v |= (ulong)(_scratch[i] & 0x7F) << (7 * i);
                if ((_scratch[i] & 0x80) == 0)
                {
                    pos += i + 1;
                    return v;
                }
            }

            throw new InvalidDataException("A varint is cut off by the end of its message.");
        }

        private void ReadExactly(Span<byte> buffer, long offset)
        {
            if (offset < 0 || offset + buffer.Length > length)
            {
                throw new InvalidDataException("A read lies outside the file.");
            }

            for (int done = 0; done < buffer.Length;)
            {
                int n = RandomAccess.Read(handle, buffer[done..], offset + done);
                if (n <= 0)
                {
                    throw new InvalidDataException("The file ended early.");
                }

                done += n;
            }
        }
    }
}
