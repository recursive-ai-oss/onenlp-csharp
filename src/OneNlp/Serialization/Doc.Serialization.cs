using RecursiveAI.OneNlp.Serialization;

namespace RecursiveAI.OneNlp;

public sealed partial class Doc
{
    /// <summary>Serializes the document as JSON in the shape spaCy documents for <c>Doc.to_json</c>, plus a few fields of our own.</summary>
    /// <returns>
    /// A JSON object with <c>text</c>, <c>ents</c> (only when entities have been set), <c>sents</c> (only when boundaries have been set)
    /// and <c>tokens</c>. Offsets count Unicode code points, as spaCy's do. A token has <c>id</c>, <c>start</c> and <c>end</c>, and
    /// <c>tag</c>, <c>pos</c>, <c>morph</c>, <c>lemma</c>, <c>dep</c> and <c>head</c> for each annotation that is set. Our additions:
    /// token <c>norm</c>, <c>conf</c> (when not 1) and <c>sb</c> (the sentence boundary, 1 start and 2 inside); entity <c>conf</c>
    /// (when not 1). See <c>docs/json-format.md</c>.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// The text or a label holds an unpaired surrogate (JSON text cannot carry one; use <see cref="ToBytes"/>), or a token boundary
    /// splits a surrogate pair.
    /// </exception>
    public string ToJson() => DocJson.Utf8ToString(ToJsonUtf8());

    /// <summary>Serializes the document as UTF-8 JSON; the same content as <see cref="ToJson"/>.</summary>
    /// <exception cref="InvalidOperationException">As for <see cref="ToJson"/>.</exception>
    public byte[] ToJsonUtf8() => DocJson.ToUtf8(this);

    /// <summary>Writes the document as UTF-8 JSON to a stream; the same content as <see cref="ToJson"/>.</summary>
    /// <param name="stream">A writable stream. It is left open.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">As for <see cref="ToJson"/>.</exception>
    public void WriteJson(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        DocJson.WriteTo(this, stream);
    }

    /// <summary>Writes the document as UTF-8 JSON to a stream asynchronously; the same content as <see cref="ToJson"/>.</summary>
    /// <param name="stream">A writable stream. It is left open.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">As for <see cref="ToJson"/>.</exception>
    public ValueTask WriteJsonAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return stream.WriteAsync(ToJsonUtf8(), cancellationToken);
    }

    /// <summary>Reads a document from JSON in spaCy's <c>Doc.to_json</c> shape, with or without our extra fields.</summary>
    /// <param name="json">The JSON text.</param>
    /// <param name="strings">The store the document's strings are added to; a new one when <see langword="null"/>.</param>
    /// <returns>
    /// The document. Tokens come from <c>tokens</c> by <c>start</c> and <c>end</c>; missing annotations stay unset, a missing
    /// <c>head</c> makes the token its own head, and a missing <c>conf</c> is 1. <c>ents</c> and <c>sents</c> are read from their
    /// character offsets, which must fall on token boundaries. <c>cats</c> and <c>spans</c> are read into <see cref="Doc.Cats"/> and <see cref="Doc.Spans"/>; <c>user_data</c> and unknown fields are
    /// ignored, because a <see cref="Doc"/> does not store them.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">The text is not valid JSON of this shape, nests deeper than 32 levels, or describes a document that breaks the invariants (overlapping tokens, a head outside the document, an entity off a token boundary, a confidence outside 0 to 1, an offset beyond the text).</exception>
    public static Doc FromJson(string json, StringStore? strings = null)
    {
        ArgumentNullException.ThrowIfNull(json);
        return DocJson.Read(System.Text.Encoding.UTF8.GetBytes(json), strings ?? new StringStore());
    }

    /// <summary>Reads a document from UTF-8 JSON (a leading byte-order mark is allowed); see <see cref="FromJson(string, StringStore?)"/>.</summary>
    /// <param name="utf8Json">The JSON bytes.</param>
    /// <param name="strings">The store the document's strings are added to; a new one when <see langword="null"/>.</param>
    /// <exception cref="InvalidDataException">As for <see cref="FromJson(string, StringStore?)"/>, and the bytes are not valid UTF-8.</exception>
    public static Doc FromJson(ReadOnlySpan<byte> utf8Json, StringStore? strings = null) => DocJson.Read(utf8Json, strings ?? new StringStore());

    /// <summary>Reads a document from a stream of UTF-8 JSON, reading it to the end; see <see cref="FromJson(string, StringStore?)"/>.</summary>
    /// <param name="stream">A readable stream. It is left open.</param>
    /// <param name="strings">The store the document's strings are added to; a new one when <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">As for <see cref="FromJson(string, StringStore?)"/>, and the stream holds more than 512 MiB.</exception>
    public static Doc FromJson(Stream stream, StringStore? strings = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return DocJson.Read(DocJson.ReadAll(stream), strings ?? new StringStore());
    }

    /// <summary>Reads a document from a stream of UTF-8 JSON asynchronously, reading it to the end; see <see cref="FromJson(string, StringStore?)"/>.</summary>
    /// <param name="stream">A readable stream. It is left open.</param>
    /// <param name="strings">The store the document's strings are added to; a new one when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">As for <see cref="FromJson(Stream, StringStore?)"/>.</exception>
    public static async ValueTask<Doc> FromJsonAsync(Stream stream, StringStore? strings = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return DocJson.Read(await DocJson.ReadAllAsync(stream, cancellationToken).ConfigureAwait(false), strings ?? new StringStore());
    }

    /// <summary>Serializes the document in the compact binary form (<c>docs/binary-format.md</c>): lossless, including unpaired surrogates and every confidence bit.</summary>
    /// <param name="checksum">Append a CRC-32 that <see cref="FromBytes(ReadOnlySpan{byte}, StringStore?)"/> verifies (default <see langword="true"/>).</param>
    /// <returns>The bytes, starting with the magic number <c>ONDC</c>.</returns>
    public byte[] ToBytes(bool checksum = true) => DocBinary.EncodeDoc(this, checksum);

    /// <summary>Writes the binary form into a caller's buffer.</summary>
    /// <param name="destination">The buffer.</param>
    /// <param name="written">The bytes written, or 0 when the buffer is too small.</param>
    /// <param name="checksum">As for <see cref="ToBytes"/>.</param>
    /// <returns><see langword="false"/> when <paramref name="destination"/> is too small; nothing is written then.</returns>
    public bool TryWriteBytes(Span<byte> destination, out int written, bool checksum = true)
    {
        byte[] bytes = ToBytes(checksum);
        if (bytes.Length > destination.Length)
        {
            written = 0;
            return false;
        }

        bytes.CopyTo(destination);
        written = bytes.Length;
        return true;
    }

    /// <summary>Writes the binary form to a stream. Several documents may be written one after another and read back with <see cref="FromStream(Stream, StringStore?)"/>.</summary>
    /// <param name="stream">A writable stream. It is left open.</param>
    /// <param name="checksum">As for <see cref="ToBytes"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    public void WriteBinary(Stream stream, bool checksum = true)
    {
        ArgumentNullException.ThrowIfNull(stream);
        stream.Write(ToBytes(checksum));
    }

    /// <summary>Writes the binary form to a stream asynchronously; see <see cref="WriteBinary"/>.</summary>
    /// <param name="stream">A writable stream. It is left open.</param>
    /// <param name="checksum">As for <see cref="ToBytes"/>.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    public ValueTask WriteBinaryAsync(Stream stream, bool checksum = true, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return stream.WriteAsync(ToBytes(checksum), cancellationToken);
    }

    /// <summary>Reads a document from its binary form.</summary>
    /// <param name="data">Exactly one serialized document.</param>
    /// <param name="strings">The store the document's strings are added to; a new one when <see langword="null"/>.</param>
    /// <exception cref="InvalidDataException">
    /// The data is not a valid document: wrong magic number or version, unknown flags, truncated, a length or count that exceeds the
    /// input or a hard cap (33,554,432 tokens), a section out of order, a string that is not valid UTF-8, a failed checksum, bytes
    /// after the document, or contents that break the document's invariants.
    /// </exception>
    public static Doc FromBytes(ReadOnlySpan<byte> data, StringStore? strings = null)
    {
        Doc doc = DocBinary.DecodeDoc(data, strings ?? new StringStore(), out int consumed);
        if (consumed != data.Length)
        {
            throw new InvalidDataException("There are bytes after the end of the document.");
        }

        return doc;
    }

    /// <summary>Reads one document from a stream and leaves the stream just after it, so documents written back to back can be read in turn.</summary>
    /// <param name="stream">A readable stream positioned at the start of a document.</param>
    /// <param name="strings">The store the document's strings are added to; a new one when <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">As for <see cref="FromBytes(ReadOnlySpan{byte}, StringStore?)"/>, and the stream ends early. Memory use never exceeds what the stream delivers, whatever length the header claims.</exception>
    public static Doc FromStream(Stream stream, StringStore? strings = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        byte[] header = DocBinary.ReadFully(stream, DocBinary.HeaderLength);
        (uint bodyLength, bool checksum) = DocBinary.ReadHeader(header, DocBinary.DocMagicBytes);
        byte[] rest = DocBinary.ReadFully(stream, bodyLength + (checksum ? 4L : 0L));
        return Join(header, rest, strings);
    }

    /// <summary>Reads one document from a stream asynchronously; see <see cref="FromStream(Stream, StringStore?)"/>.</summary>
    /// <param name="stream">A readable stream positioned at the start of a document.</param>
    /// <param name="strings">The store the document's strings are added to; a new one when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">As for <see cref="FromStream(Stream, StringStore?)"/>.</exception>
    public static async ValueTask<Doc> FromStreamAsync(Stream stream, StringStore? strings = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        byte[] header = await DocBinary.ReadFullyAsync(stream, DocBinary.HeaderLength, cancellationToken).ConfigureAwait(false);
        (uint bodyLength, bool checksum) = DocBinary.ReadHeader(header, DocBinary.DocMagicBytes);
        byte[] rest = await DocBinary.ReadFullyAsync(stream, bodyLength + (checksum ? 4L : 0L), cancellationToken).ConfigureAwait(false);
        return Join(header, rest, strings);
    }

    private static Doc Join(byte[] header, byte[] rest, StringStore? strings)
    {
        byte[] all = new byte[header.Length + rest.Length];
        header.CopyTo(all, 0);
        rest.CopyTo(all, header.Length);
        return FromBytes(all, strings);
    }
}
