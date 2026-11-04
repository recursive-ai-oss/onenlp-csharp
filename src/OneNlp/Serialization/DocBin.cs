using System.Buffers.Binary;

namespace RecursiveAI.OneNlp.Serialization;

/// <summary>
/// A container for many documents that share one string table, so a label or lemma that appears in a thousand documents is stored
/// once. Write it with <see cref="ToBytes"/> or <see cref="WriteTo(Stream, bool)"/>; read it whole with <see cref="FromBytes"/> or
/// document by document with <see cref="DocBinReader"/>. The layout is in <c>docs/binary-format.md</c>.
/// </summary>
/// <remarks>Not thread-safe. Documents added to a bin may come from different <see cref="StringStore"/> instances.</remarks>
public sealed class DocBin
{
    private readonly List<Doc> _docs = [];

    /// <summary>The number of documents held.</summary>
    public int Count => _docs.Count;

    /// <summary>The documents in the order they were added (or read).</summary>
    public IReadOnlyList<Doc> Docs => _docs;

    /// <summary>Adds a document. The bin keeps a reference; it does not copy the document.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="doc"/> is <see langword="null"/>.</exception>
    public void Add(Doc doc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        _docs.Add(doc);
    }

    /// <summary>Serializes the bin.</summary>
    /// <param name="checksum">Append a CRC-32 over everything before it (default <see langword="true"/>); readers verify it.</param>
    /// <returns>The bytes, starting with the magic number <c>ONDB</c>.</returns>
    /// <exception cref="InvalidOperationException">The bin is larger than the format's 4 GiB section limit.</exception>
    public byte[] ToBytes(bool checksum = true)
    {
        var table = new StringTableBuilder();
        var records = new ByteBuilder();
        foreach (Doc doc in _docs)
        {
            // Each record is length-prefixed, so a reader can take one document at a time.
            int before = records.Length;
            records.U32(0);
            var body = new ByteBuilder();
            DocBinary.EncodeBody(doc, table, body);
            long size = body.Length;
            if (size > uint.MaxValue)
            {
                throw new InvalidOperationException("A document is too large to serialize.");
            }

            records.Bytes(body.AsSpan());
            PatchU32(records, before, (uint)size);
        }

        var strings = new ByteBuilder();
        table.WriteSection(strings);

        var docsSection = new ByteBuilder();
        docsSection.Byte(DocBinary.SectionDocs);
        long docsLength = 4L + records.Length;
        long bodyLength = strings.Length + 1 + 4 + docsLength;
        if (docsLength > uint.MaxValue || bodyLength > uint.MaxValue)
        {
            throw new InvalidOperationException("The bin is too large to serialize.");
        }

        docsSection.U32((uint)docsLength);
        docsSection.U32((uint)_docs.Count);

        var b = new ByteBuilder();
        DocBinary.WriteHeader(b, DocBinary.BinMagic, checksum, (uint)bodyLength);
        b.Bytes(strings.AsSpan());
        b.Bytes(docsSection.AsSpan());
        b.Bytes(records.AsSpan());
        if (checksum)
        {
            b.U32(Crc32.Update(0, b.AsSpan()));
        }

        return b.Buffer.AsSpan(0, b.Length).ToArray();
    }

    private static void PatchU32(ByteBuilder b, int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(b.Buffer.AsSpan(at), value);

    /// <summary>Serializes the bin to a stream. Same bytes as <see cref="ToBytes"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    public void WriteTo(Stream stream, bool checksum = true)
    {
        ArgumentNullException.ThrowIfNull(stream);
        stream.Write(ToBytes(checksum));
    }

    /// <summary>Reads a whole bin from memory.</summary>
    /// <param name="data">The bytes of one bin; bytes after it are an error.</param>
    /// <param name="strings">The store the documents share; a new one when <see langword="null"/>.</param>
    /// <exception cref="InvalidDataException">The data is not a valid bin: bad magic or version, truncated, a length or count that exceeds the input or a cap, a failed checksum, or contents that fail the document's invariants.</exception>
    public static DocBin FromBytes(ReadOnlySpan<byte> data, StringStore? strings = null)
    {
        using var stream = new MemoryStream(data.ToArray(), writable: false);
        var bin = new DocBin();
        var reader = new DocBinReader(stream, strings);
        while (reader.TryRead(out Doc? doc))
        {
            bin._docs.Add(doc);
        }

        if (stream.Position != stream.Length)
        {
            throw new InvalidDataException("There are bytes after the end of the bin.");
        }

        return bin;
    }

    /// <summary>Reads a whole bin from a stream, which is left positioned just after it.</summary>
    /// <param name="stream">A readable stream positioned at the start of a bin.</param>
    /// <param name="strings">The store the documents share; a new one when <see langword="null"/>.</param>
    /// <exception cref="InvalidDataException">As for <see cref="FromBytes"/>, and the stream ends early.</exception>
    public static DocBin FromStream(Stream stream, StringStore? strings = null)
    {
        var bin = new DocBin();
        var reader = new DocBinReader(stream, strings);
        while (reader.TryRead(out Doc? doc))
        {
            bin._docs.Add(doc);
        }

        return bin;
    }
}

/// <summary>
/// Reads a <see cref="DocBin"/> from a stream one document at a time, so a corpus larger than memory can be walked. The header and
/// string table are read when the reader is created; each <see cref="TryRead"/> then reads one record. The checksum, when present,
/// is verified once the last document has been read.
/// </summary>
/// <remarks>Reading never allocates more than the stream actually delivers, whatever the lengths in the data claim.</remarks>
public sealed class DocBinReader
{
    private readonly Stream _stream;
    private readonly StringStore _store;
    private readonly int[] _map;
    private readonly bool _checksum;
    private long _remainingDocsBytes;
    private long _remainingBody;
    private uint _remainingDocs;
    private uint _crc;
    private bool _finished;

    /// <summary>Starts reading a bin: reads and checks the header and the string table.</summary>
    /// <param name="stream">A readable stream positioned at the start of a bin.</param>
    /// <param name="strings">The store the documents are read into; a new one when <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">The header or string table is invalid or the stream ends inside them.</exception>
    public DocBinReader(Stream stream, StringStore? strings = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _stream = stream;
        _store = strings ?? new StringStore();

        byte[] header = DocBinary.ReadFully(_stream, DocBinary.HeaderLength);
        (uint bodyLength, _checksum) = DocBinary.ReadHeader(header, DocBinary.BinMagic);
        _crc = Crc32.Update(0, header);
        _remainingBody = bodyLength;

        byte[] sectionHead = ReadBody(5);
        if (sectionHead[0] != DocBinary.SectionStrings)
        {
            throw new InvalidDataException("The first section is not the string table.");
        }

        uint length = BinaryPrimitives.ReadUInt32LittleEndian(sectionHead.AsSpan(1));
        string[] table = DocBinary.ReadStringTable(ReadBody(length));
        _map = DocBinary.MapToStore(table, _store);

        sectionHead = ReadBody(5);
        if (sectionHead[0] != DocBinary.SectionDocs)
        {
            throw new InvalidDataException("The second section is not the document list.");
        }

        _remainingDocsBytes = BinaryPrimitives.ReadUInt32LittleEndian(sectionHead.AsSpan(1));
        if (_remainingDocsBytes != _remainingBody || _remainingDocsBytes < 4)
        {
            throw new InvalidDataException("The document list does not fill the rest of the body.");
        }

        _remainingDocs = BinaryPrimitives.ReadUInt32LittleEndian(ReadBody(4));
        _remainingDocsBytes -= 4;

        // A record takes at least 4 bytes of length prefix, so the count is bounded by the bytes that remain.
        if (_remainingDocs > _remainingDocsBytes / 4)
        {
            throw new InvalidDataException("The document count is larger than the document list can hold.");
        }

        if (_remainingDocs == 0)
        {
            Finish();
        }
    }

    /// <summary>The number of documents not yet read.</summary>
    public long Remaining => _remainingDocs;

    /// <summary>Reads the next document.</summary>
    /// <param name="doc">The document, or <see langword="null"/> at the end.</param>
    /// <returns><see langword="false"/> at the end of the bin, after the checksum has been verified.</returns>
    /// <exception cref="InvalidDataException">The record is invalid, the stream ends early, or the checksum fails.</exception>
    public bool TryRead([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Doc? doc)
    {
        if (_remainingDocs == 0)
        {
            doc = null;
            return false;
        }

        uint length = BinaryPrimitives.ReadUInt32LittleEndian(ReadBody(4));
        _remainingDocsBytes -= 4;
        byte[] record = ReadBody(length);
        _remainingDocsBytes -= length;
        _remainingDocs--;
        doc = DocBinary.DecodeBody(record, _map, _store);
        if (_remainingDocs == 0)
        {
            Finish();
        }

        return true;
    }

    private void Finish()
    {
        if (_finished)
        {
            return;
        }

        _finished = true;
        if (_remainingDocsBytes != 0 || _remainingBody != 0)
        {
            throw new InvalidDataException("There are bytes in the document list after the last record.");
        }

        if (_checksum)
        {
            byte[] stored = DocBinary.ReadFully(_stream, 4);
            if (BinaryPrimitives.ReadUInt32LittleEndian(stored) != _crc)
            {
                throw new InvalidDataException("The checksum does not match: the data is corrupt.");
            }
        }
    }

    /// <summary>Reads <paramref name="count"/> bytes of the body, charging them against its declared length and the checksum.</summary>
    private byte[] ReadBody(long count)
    {
        if (count > _remainingBody || count > DocBinary.MaxBodyBytes)
        {
            throw new InvalidDataException("A length in the data exceeds what remains of the body.");
        }

        byte[] bytes = DocBinary.ReadFully(_stream, count);
        _remainingBody -= count;
        _crc = Crc32.Update(_crc, bytes);
        return bytes;
    }
}
