using System.Buffers.Binary;

namespace RecursiveAI.OneNlp.Serialization;

/// <summary>The shared string table of a binary stream while it is written: strings in first-use order, index 0 reserved for the empty string.</summary>
internal sealed class StringTableBuilder
{
    private readonly Dictionary<string, int> _index = new(StringComparer.Ordinal);
    private readonly List<string> _strings = [];

    public IReadOnlyList<string> Strings => _strings;

    public int IndexOf(string value)
    {
        if (value.Length == 0)
        {
            return 0;
        }

        if (!_index.TryGetValue(value, out int i))
        {
            i = _strings.Count + 1;
            _strings.Add(value);
            _index[value] = i;
        }

        return i;
    }

    public void WriteSection(ByteBuilder b)
    {
        int at = b.BeginSection(DocBinary.SectionStrings);
        b.Varint((uint)_strings.Count);
        foreach (string s in _strings)
        {
            b.String(s);
        }

        b.EndSection(at);
    }
}

/// <summary>The binary encoding of one document and of the container; see docs/binary-format.md.</summary>
internal static class DocBinary
{
    internal const byte SectionStrings = 1;
    internal const byte SectionText = 2;
    internal const byte SectionTokens = 3;
    internal const byte SectionEntities = 4;
    internal const byte SectionDocs = 5;

    internal const ushort Version = 1;
    internal const ushort FlagChecksum = 1;
    internal const int HeaderLength = 12;
    internal const int MaxTokens = 1 << 25;
    internal const long MaxBodyBytes = 1L << 30;

    internal static readonly byte[] DocMagicBytes = "ONDC"u8.ToArray();
    internal static readonly byte[] BinMagic = "ONDB"u8.ToArray();

    // ---- writing ----

    /// <summary>Appends the document's sections (text, tokens, entities); strings are written as indexes into <paramref name="table"/>.</summary>
    internal static void EncodeBody(Doc doc, StringTableBuilder table, ByteBuilder b)
    {
        int n = doc.Length;
        int at = b.BeginSection(SectionText);
        if (TextUtil.IsWellFormed(doc.Text))
        {
            b.Byte(0);
            b.Bytes(System.Text.Encoding.UTF8.GetBytes(doc.Text));
        }
        else
        {
            b.Byte(1);
            foreach (char c in doc.Text)
            {
                b.Byte((byte)c);
                b.Byte((byte)(c >> 8));
            }
        }

        b.EndSection(at);

        at = b.BeginSection(SectionTokens);
        b.Varint((uint)n);
        ReadOnlySpan<int> starts = doc.GetStarts();
        ReadOnlySpan<int> lengths = doc.GetLengths();
        int previousEnd = 0;
        for (int i = 0; i < n; i++)
        {
            b.Varint((uint)(starts[i] - previousEnd));
            b.Varint((uint)lengths[i]);
            previousEnd = starts[i] + lengths[i];
        }

        var local = new Dictionary<int, int>();
        for (int a = 0; a < 6; a++)
        {
            foreach (int id in doc.GetColumn((TokenColumn)a))
            {
                if (id == 0)
                {
                    b.Varint(0);
                    continue;
                }

                if (!local.TryGetValue(id, out int index))
                {
                    index = table.IndexOf(doc.Strings[id]);
                    local[id] = index;
                }

                b.Varint((uint)index);
            }
        }

        ReadOnlySpan<int> heads = doc.GetHeads();
        for (int i = 0; i < n; i++)
        {
            b.SignedVarint(heads[i] - i);
        }

        ReadOnlySpan<SentenceBoundary> sentences = doc.GetSentenceBoundaries();
        for (int i = 0; i < n; i += 4)
        {
            int packed = 0;
            for (int k = 0; k < 4 && i + k < n; k++)
            {
                packed |= (int)sentences[i + k] << (2 * k);
            }

            b.Byte((byte)packed);
        }

        ReadOnlySpan<float> conf = doc.GetConfidences();
        bool allOne = true;
        foreach (float c in conf)
        {
            if (BitConverter.SingleToUInt32Bits(c) != BitConverter.SingleToUInt32Bits(1f))
            {
                allOne = false;
                break;
            }
        }

        if (allOne)
        {
            b.Byte(0);
        }
        else
        {
            b.Byte(1);
            foreach (float c in conf)
            {
                b.Float(c);
            }
        }

        b.EndSection(at);

        if (n > 0 && doc[0].EntIob != EntIob.Unset)
        {
            at = b.BeginSection(SectionEntities);
            IReadOnlyList<Span> ents = doc.Ents;
            b.Varint((uint)ents.Count);
            int previous = 0;
            foreach (Span e in ents)
            {
                b.Varint((uint)(e.Start - previous));
                b.Varint((uint)e.Length);
                b.Varint((uint)table.IndexOf(e.Label));
                b.Float(e.Confidence);
                previous = e.End;
            }

            b.EndSection(at);
        }
    }

    /// <summary>Writes the 12-byte header: magic, version, flags and the byte count of everything between header and checksum.</summary>
    internal static void WriteHeader(ByteBuilder b, byte[] magic, bool checksum, uint bodyLength)
    {
        b.Bytes(magic);
        b.U16(Version);
        b.U16(checksum ? FlagChecksum : (ushort)0);
        b.U32(bodyLength);
    }

    internal static byte[] EncodeDoc(Doc doc, bool checksum)
    {
        var table = new StringTableBuilder();
        var body = new ByteBuilder();
        EncodeBody(doc, table, body);

        var b = new ByteBuilder();
        var strings = new ByteBuilder();
        table.WriteSection(strings);
        long length = (long)strings.Length + body.Length;
        if (length > uint.MaxValue)
        {
            throw new InvalidOperationException("The document is too large to serialize.");
        }

        WriteHeader(b, DocMagicBytes, checksum, (uint)length);
        b.Bytes(strings.AsSpan());
        b.Bytes(body.AsSpan());
        if (checksum)
        {
            b.U32(Crc32.Update(0, b.AsSpan()));
        }

        return b.Buffer.AsSpan(0, b.Length).ToArray();
    }

    // ---- reading ----

    /// <summary>Reads exactly <paramref name="count"/> bytes, growing the buffer as data arrives so a false length cannot force a large allocation.</summary>
    internal static byte[] ReadFully(Stream stream, long count)
    {
        const int Chunk = 1 << 20;
        if (count > MaxBodyBytes + 4)
        {
            throw new InvalidDataException("A declared length is over the 1 GiB cap.");
        }

        var builder = new ByteBuilder();
        byte[] chunk = new byte[(int)Math.Min(count, Chunk)];
        long left = count;
        while (left > 0)
        {
            int take = (int)Math.Min(left, chunk.Length);
            int total = 0;
            while (total < take)
            {
                int read = stream.Read(chunk, total, take - total);
                if (read == 0)
                {
                    throw new InvalidDataException("The stream ends before the length its header declares.");
                }

                total += read;
            }

            builder.Bytes(chunk.AsSpan(0, take));
            left -= take;
        }

        return builder.Buffer.AsSpan(0, builder.Length).ToArray();
    }

    /// <summary>Validates a 12-byte header and returns the body length and whether a checksum follows the body.</summary>
    internal static (uint BodyLength, bool Checksum) ReadHeader(ReadOnlySpan<byte> header, byte[] magic)
    {
        if (header.Length < HeaderLength)
        {
            throw new InvalidDataException("The data is shorter than the 12-byte header.");
        }

        if (!header[..4].SequenceEqual(magic))
        {
            throw new InvalidDataException("The data does not start with the expected magic number.");
        }

        ushort version = BinaryPrimitives.ReadUInt16LittleEndian(header[4..]);
        if (version != Version)
        {
            throw new InvalidDataException($"Unsupported format version {version}; this reader supports {Version}.");
        }

        ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(header[6..]);
        if ((flags & ~FlagChecksum) != 0)
        {
            throw new InvalidDataException("The header sets flags this reader does not know.");
        }

        return (BinaryPrimitives.ReadUInt32LittleEndian(header[8..]), (flags & FlagChecksum) != 0);
    }

    internal static Doc DecodeDoc(ReadOnlySpan<byte> data, StringStore store, out int consumed)
    {
        (uint bodyLength, bool checksum) = ReadHeader(data, DocMagicBytes);
        long total = HeaderLength + (long)bodyLength + (checksum ? 4 : 0);
        if (total > data.Length)
        {
            throw new InvalidDataException("The data ends before the length its header declares.");
        }

        ReadOnlySpan<byte> record = data[..(int)total];
        if (checksum)
        {
            VerifyChecksum(record);
        }

        var c = new ByteCursor(record[HeaderLength..(HeaderLength + (int)bodyLength)]);
        if (c.Byte() != SectionStrings)
        {
            throw new InvalidDataException("The first section is not the string table.");
        }

        string[] strings = ReadStringTable(c.Take(c.U32()));
        int[] map = MapToStore(strings, store);
        Doc doc = DecodeBody(record[(HeaderLength + (int)bodyLength - c.Remaining)..(HeaderLength + (int)bodyLength)], map, store);
        consumed = (int)total;
        return doc;
    }

    internal static void VerifyChecksum(ReadOnlySpan<byte> recordWithChecksum)
    {
        ReadOnlySpan<byte> covered = recordWithChecksum[..^4];
        uint stored = BinaryPrimitives.ReadUInt32LittleEndian(recordWithChecksum[^4..]);
        if (Crc32.Update(0, covered) != stored)
        {
            throw new InvalidDataException("The checksum does not match: the data is corrupt.");
        }
    }

    internal static string[] ReadStringTable(ReadOnlySpan<byte> payload)
    {
        var c = new ByteCursor(payload);
        uint count = c.Varint();

        // Every string takes at least one byte, so a count beyond the bytes left is a lie; this bounds the allocation.
        if (count > c.Remaining)
        {
            throw new InvalidDataException("The string table declares more strings than it has bytes.");
        }

        var strings = new string[count];
        for (int i = 0; i < strings.Length; i++)
        {
            strings[i] = c.String();
        }

        if (c.Remaining != 0)
        {
            throw new InvalidDataException("The string table has bytes after its last string.");
        }

        return strings;
    }

    /// <summary>Maps table index (0 = empty, i = <c>strings[i-1]</c>) to an ID in the reader's store.</summary>
    internal static int[] MapToStore(string[] strings, StringStore store)
    {
        var map = new int[strings.Length + 1];
        for (int i = 0; i < strings.Length; i++)
        {
            map[i + 1] = store.Add(strings[i]);
        }

        return map;
    }

    internal static Doc DecodeBody(ReadOnlySpan<byte> body, int[] map, StringStore store)
    {
        var c = new ByteCursor(body);
        string? text = null;
        ReadOnlySpan<byte> tokens = default;
        ReadOnlySpan<byte> ents = default;
        bool hasTokens = false;
        bool hasEnts = false;
        int previousId = 0;
        while (c.Remaining > 0)
        {
            byte id = c.Byte();
            ReadOnlySpan<byte> payload = c.Take(c.U32());
            if (id <= previousId)
            {
                throw new InvalidDataException("Sections are out of order or repeated.");
            }

            previousId = id;
            switch (id)
            {
                case SectionText:
                    if (payload.Length < 1 || payload[0] > 1)
                    {
                        throw new InvalidDataException("The text section has no valid encoding byte.");
                    }

                    text = TextUtil.Decode(payload[1..], payload[0] == 1);
                    break;
                case SectionTokens:
                    tokens = payload;
                    hasTokens = true;
                    break;
                case SectionEntities:
                    ents = payload;
                    hasEnts = true;
                    break;
                default:
                    // Sections a later version adds are skipped; their length says how.
                    break;
            }
        }

        if (text is null || !hasTokens)
        {
            throw new InvalidDataException("A document needs a text section and a token section.");
        }

        try
        {
            Doc doc = DecodeTokens(tokens, text, map, store);
            if (hasEnts)
            {
                DecodeEntities(ents, doc, map);
            }

            return doc;
        }
        catch (ArgumentException e)
        {
            // Doc's own validation caught something the section checks did not.
            throw new InvalidDataException("The document's contents are inconsistent: " + e.Message, e);
        }
    }

    private static Doc DecodeTokens(ReadOnlySpan<byte> payload, string text, int[] map, StringStore store)
    {
        var c = new ByteCursor(payload);
        uint count = c.Varint();

        // A token costs at least 9 bytes (two offset values, six attribute indexes and a head), so the count is bounded by the input.
        if (count > MaxTokens || (long)count * 9 > c.Remaining)
        {
            throw new InvalidDataException("The token count is larger than the section can hold.");
        }

        int n = (int)count;
        var bounds = new TokenBoundary[n];
        long previousEnd = 0;
        for (int i = 0; i < n; i++)
        {
            uint gap = c.Varint();
            uint length = c.Varint();
            long start = previousEnd + gap;
            if (length < 1 || start + length > text.Length)
            {
                throw new InvalidDataException($"Token {i} lies outside the text or is empty.");
            }

            bounds[i] = new TokenBoundary((int)start, (int)length);
            previousEnd = start + length;
        }

        var doc = new Doc(store, text, bounds);
        var ids = new int[n];
        for (int a = 0; a < 6; a++)
        {
            for (int i = 0; i < n; i++)
            {
                uint index = c.Varint();
                if (index >= map.Length)
                {
                    throw new InvalidDataException("An attribute refers to a string the table does not have.");
                }

                ids[i] = map[index];
            }

            doc.SetColumn((TokenColumn)a, ids);
        }

        for (int i = 0; i < n; i++)
        {
            long head = i + c.SignedVarint();
            if (head < 0 || head >= n)
            {
                throw new InvalidDataException($"The head of token {i} is outside the document.");
            }

            ids[i] = (int)head;
        }

        doc.SetHeads(ids);

        var boundaries = new SentenceBoundary[n];
        ReadOnlySpan<byte> packed = c.Take((n + 3L) / 4);
        for (int i = 0; i < n; i++)
        {
            int value = (packed[i / 4] >> (2 * (i % 4))) & 3;
            if (value > 2)
            {
                throw new InvalidDataException("A sentence boundary has an unknown value.");
            }

            boundaries[i] = (SentenceBoundary)value;
        }

        doc.SetSentenceBoundaries(boundaries);

        byte mode = c.Byte();
        if (mode == 1)
        {
            var conf = new float[n];
            for (int i = 0; i < n; i++)
            {
                conf[i] = c.Float();
            }

            doc.SetConfidences(conf);
        }
        else if (mode != 0)
        {
            throw new InvalidDataException("The confidence section has an unknown mode.");
        }

        if (c.Remaining != 0)
        {
            throw new InvalidDataException("The token section has bytes after its last column.");
        }

        return doc;
    }

    private static void DecodeEntities(ReadOnlySpan<byte> payload, Doc doc, int[] map)
    {
        var c = new ByteCursor(payload);
        uint count = c.Varint();

        // An entity takes at least 7 bytes.
        if (count > doc.Length || (long)count * 7 > c.Remaining)
        {
            throw new InvalidDataException("The entity count is larger than the section can hold.");
        }

        var specs = new SpanSpec[count];
        long previousEnd = 0;
        for (int i = 0; i < specs.Length; i++)
        {
            long start = previousEnd + c.Varint();
            long length = c.Varint();
            uint label = c.Varint();
            float confidence = c.Float();
            if (length < 1 || start + length > doc.Length || label == 0 || label >= map.Length)
            {
                throw new InvalidDataException($"Entity {i} is empty, outside the document or unlabelled.");
            }

            specs[i] = new SpanSpec((int)start, (int)(start + length), doc.Strings[map[label]], confidence);
            previousEnd = start + length;
        }

        if (c.Remaining != 0)
        {
            throw new InvalidDataException("The entity section has bytes after its last entity.");
        }

        doc.SetEntities(specs);
    }
}
