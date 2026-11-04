using System.Buffers;

namespace RecursiveAI.OneNlp.Tokenizers;

/// <summary>
/// A subword tokenizer: turns text into token IDs with offsets back into the input, and IDs back into text. The WordPiece, BPE,
/// byte-level BPE, Unigram and Tiktoken families derive from this class; callers hold a <see cref="Tokenizer"/> and need not know
/// which family it is. Create one with <see cref="FromFile(string)"/> or <see cref="FromStream"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Offsets.</b> Text passed as <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> gets UTF-16 offsets into that span; text
/// passed as UTF-8 gets byte offsets into that span (see <see cref="TokenOffset"/>). A family must give the offsets of the original
/// input, not of its normalized form. Invalid UTF-8 is read as the replacement character, one per invalid sequence, and the offsets
/// still count the original bytes.
/// </para>
/// <para>
/// <b>Settings.</b> <see cref="Truncation"/> and <see cref="Padding"/> apply to <c>Encode</c> and <c>EncodeBatch</c> only, never to
/// counting, trimming or <c>EncodeToIds</c>.
/// </para>
/// <para>
/// <b>Threads.</b> Encoding and decoding are safe from several threads at once as long as the settings are not changed meanwhile.
/// </para>
/// <para>
/// <b>For family authors.</b> Implement <see cref="EncodeCore"/> (honouring the truncation it is given), <see cref="Decode(ReadOnlySpan{int}, bool)"/>,
/// <see cref="IdToToken"/>, <see cref="TokenToId"/> and <see cref="VocabularySize"/>. Override <see cref="EncodeToIds(ReadOnlySpan{char}, Span{int}, out int, out int, bool)"/>
/// or <see cref="CountTokens(ReadOnlySpan{char}, bool)"/> only to avoid the allocation of building a full <see cref="TokenEncoding"/>.
/// </para>
/// </remarks>
public abstract class Tokenizer
{
    /// <summary>Truncation applied by <see cref="Encode(ReadOnlySpan{char}, bool)"/> and <see cref="EncodeBatch"/>; <see langword="null"/> for none.</summary>
    public TruncationOptions? Truncation { get; set; }

    /// <summary>Padding applied by <see cref="Encode(ReadOnlySpan{char}, bool)"/> and <see cref="EncodeBatch"/>; <see langword="null"/> for none.</summary>
    public PaddingOptions? Padding { get; set; }

    /// <summary>The number of tokens in the vocabulary, added tokens included.</summary>
    public abstract int VocabularySize { get; }

    /// <summary>Loads a tokenizer from a file, choosing the format from the file name.</summary>
    /// <param name="path">
    /// <c>tokenizer.json</c> or another <c>.json</c> (Hugging Face), <c>.model</c> (SentencePiece), <c>vocab.txt</c> or another <c>.txt</c>
    /// (WordPiece vocabulary), or <c>.tiktoken</c> (Tiktoken ranks).
    /// </param>
    /// <exception cref="NotSupportedException">The extension is not one of these, or the format has no loader yet.</exception>
    /// <exception cref="IOException">The file cannot be read.</exception>
    public static Tokenizer FromFile(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        TokenizerFormat format = Path.GetExtension(path).ToUpperInvariant() switch
        {
            ".JSON" => TokenizerFormat.HuggingFaceJson,
            ".MODEL" => TokenizerFormat.SentencePieceModel,
            ".TXT" => TokenizerFormat.WordPieceVocabulary,
            ".TIKTOKEN" => TokenizerFormat.Tiktoken,
            _ => throw new NotSupportedException($"Cannot tell the tokenizer format from the file name '{Path.GetFileName(path)}'; open a stream and name the format."),
        };

        using FileStream stream = File.OpenRead(path);
        return format == TokenizerFormat.Tiktoken
            ? Loaders.TiktokenRanksFile.Load(stream, Path.GetFileName(path)) // the file name can name the encoding
            : FromStream(stream, format);
    }

    /// <summary>Loads a tokenizer from a stream in a stated format.</summary>
    /// <param name="stream">A readable stream at the start of the file's content. It is read to the end and not closed.</param>
    /// <param name="format">The format.</param>
    /// <exception cref="NotSupportedException">The format has no loader yet.</exception>
    /// <exception cref="InvalidDataException">The content is not valid for the format.</exception>
    public static Tokenizer FromStream(Stream stream, TokenizerFormat format)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return format switch
        {
            TokenizerFormat.HuggingFaceJson => Loaders.HuggingFaceTokenizerJson.Load(stream),
            TokenizerFormat.SentencePieceModel => Loaders.SentencePieceModelFile.Load(stream),
            TokenizerFormat.WordPieceVocabulary => Loaders.WordPieceVocabularyFile.Load(stream),
            TokenizerFormat.Tiktoken => Loaders.TiktokenRanksFile.Load(stream),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown tokenizer format."),
        };
    }

    /// <summary>Encodes a text, applying <see cref="Truncation"/> and <see cref="Padding"/>.</summary>
    /// <param name="text">The text; offsets in the result are UTF-16 offsets into it.</param>
    /// <param name="addSpecialTokens">Whether the post-processor adds its special tokens (for example <c>[CLS]</c> and <c>[SEP]</c>).</param>
    public TokenEncoding Encode(ReadOnlySpan<char> text, bool addSpecialTokens = true) =>
        PadSingle(EncodeCore(text, addSpecialTokens, Truncation));

    /// <summary>Encodes UTF-8 text like <see cref="Encode(ReadOnlySpan{char}, bool)"/>, with offsets counted in bytes of <paramref name="utf8"/>.</summary>
    /// <param name="utf8">The text as UTF-8.</param>
    /// <param name="addSpecialTokens">Whether the post-processor adds its special tokens.</param>
    public TokenEncoding Encode(ReadOnlySpan<byte> utf8, bool addSpecialTokens = true)
    {
        string text = Utf8Text.ToString(utf8, out int[] byteOffsetOfChar);
        return Utf8Text.WithByteOffsets(PadSingle(EncodeCore(text, addSpecialTokens, Truncation)), byteOffsetOfChar);
    }

    /// <summary>Encodes a text pair as one sequence (for example a question and a passage), applying <see cref="Truncation"/> and <see cref="Padding"/>.</summary>
    /// <param name="text">The first text.</param>
    /// <param name="pair">The second text. Offsets of its tokens are into this span; its tokens have type ID 1.</param>
    /// <param name="addSpecialTokens">Whether the post-processor adds its special tokens.</param>
    /// <exception cref="NotSupportedException">The family does not encode pairs.</exception>
    public TokenEncoding Encode(ReadOnlySpan<char> text, ReadOnlySpan<char> pair, bool addSpecialTokens = true) =>
        PadSingle(EncodePairCore(text, pair, addSpecialTokens, Truncation));

    /// <summary>Encodes several texts. When <see cref="Padding"/> has no fixed length, every encoding is padded to the longest.</summary>
    /// <param name="texts">The texts.</param>
    /// <param name="addSpecialTokens">Whether the post-processor adds its special tokens.</param>
    public IReadOnlyList<TokenEncoding> EncodeBatch(IReadOnlyList<string> texts, bool addSpecialTokens = true)
    {
        ArgumentNullException.ThrowIfNull(texts);
        var result = new TokenEncoding[texts.Count];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = EncodeCore(texts[i], addSpecialTokens, Truncation);
        }

        if (Padding is { } padding)
        {
            int longest = result.Length == 0 ? 0 : result.Max(e => e.Length);
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = result[i].PaddedTo(padding, TargetLength(padding, longest));
            }
        }

        return result;
    }

    /// <summary>
    /// Encodes into a caller's buffer. Ignores <see cref="Truncation"/> and <see cref="Padding"/>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="ids">Receives the token IDs.</param>
    /// <param name="written">The number of IDs written.</param>
    /// <param name="consumed">
    /// The offset in <paramref name="text"/> up to which the written tokens reach (the end of the last written token's offsets):
    /// <c>text.Length</c> when done. After <see cref="OperationStatus.DestinationTooSmall"/> the written IDs are an exact prefix of the
    /// full encoding. Encoding the rest from <paramref name="consumed"/> as a new text is not guaranteed to give the rest of the
    /// IDs, because normalization and pre-tokenization can look across that point; to be exact, encode with a larger buffer.
    /// </param>
    /// <param name="addSpecialTokens">Whether the post-processor adds its special tokens.</param>
    /// <returns><see cref="OperationStatus.Done"/>, or <see cref="OperationStatus.DestinationTooSmall"/> when <paramref name="ids"/> cannot hold every token.</returns>
    public virtual OperationStatus EncodeToIds(ReadOnlySpan<char> text, Span<int> ids, out int written, out int consumed, bool addSpecialTokens = true) =>
        CopyIds(EncodeCore(text, addSpecialTokens, null), text.Length, ids, out written, out consumed);

    /// <summary>Encodes UTF-8 text into a caller's buffer like <see cref="EncodeToIds(ReadOnlySpan{char}, Span{int}, out int, out int, bool)"/>, with <paramref name="consumed"/> counted in bytes.</summary>
    /// <param name="utf8">The text as UTF-8.</param>
    /// <param name="ids">Receives the token IDs.</param>
    /// <param name="written">The number of IDs written.</param>
    /// <param name="consumed">The byte offset in <paramref name="utf8"/> up to which the written tokens reach.</param>
    /// <param name="addSpecialTokens">Whether the post-processor adds its special tokens.</param>
    /// <returns><see cref="OperationStatus.Done"/> or <see cref="OperationStatus.DestinationTooSmall"/>.</returns>
    public virtual OperationStatus EncodeToIds(ReadOnlySpan<byte> utf8, Span<int> ids, out int written, out int consumed, bool addSpecialTokens = true)
    {
        string text = Utf8Text.ToString(utf8, out int[] byteOffsetOfChar);
        return CopyIds(Utf8Text.WithByteOffsets(EncodeCore(text, addSpecialTokens, null), byteOffsetOfChar), utf8.Length, ids, out written, out consumed);
    }

    /// <summary>Counts the tokens a text encodes to. Ignores <see cref="Truncation"/> and <see cref="Padding"/>.</summary>
    /// <param name="text">The text.</param>
    /// <param name="addSpecialTokens">Whether the special tokens the post-processor adds are counted.</param>
    public virtual int CountTokens(ReadOnlySpan<char> text, bool addSpecialTokens = true) => EncodeCore(text, addSpecialTokens, null).Length;

    /// <summary>Counts the tokens UTF-8 text encodes to. Ignores <see cref="Truncation"/> and <see cref="Padding"/>.</summary>
    /// <param name="utf8">The text as UTF-8.</param>
    /// <param name="addSpecialTokens">Whether the special tokens the post-processor adds are counted.</param>
    public virtual int CountTokens(ReadOnlySpan<byte> utf8, bool addSpecialTokens = true) =>
        CountTokens(Utf8Text.ToString(utf8, out _), addSpecialTokens);

    /// <summary>
    /// Cuts a text so that it encodes to at most a number of tokens, cutting between tokens. The result is the text covered by the first
    /// (or last) <paramref name="maxTokens"/> tokens of the full encoding without special tokens, so its encoding is that prefix (or
    /// suffix) of the full one. Ignores <see cref="Truncation"/> and <see cref="Padding"/>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="maxTokens">The budget, at least 0.</param>
    /// <param name="keep">Which end of the text to keep: <see cref="TokenizerSide.Left"/> keeps the start, <see cref="TokenizerSide.Right"/> keeps the end.</param>
    /// <returns>A slice of <paramref name="text"/>: all of it when it fits, empty when the budget is 0.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxTokens"/> is negative.</exception>
    public virtual ReadOnlySpan<char> TrimToTokenBudget(ReadOnlySpan<char> text, int maxTokens, TokenizerSide keep = TokenizerSide.Left)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxTokens);
        TokenEncoding encoding = EncodeCore(text, false, null);
        if (encoding.Length <= maxTokens)
        {
            return text;
        }

        if (maxTokens == 0)
        {
            return text[..0];
        }

        return keep == TokenizerSide.Left
            ? text[..encoding.Offsets[maxTokens - 1].End]
            : text[encoding.Offsets[encoding.Length - maxTokens].Start..];
    }

    /// <summary>Turns token IDs back into text.</summary>
    /// <param name="ids">The IDs.</param>
    /// <param name="skipSpecialTokens">Whether special tokens are left out of the text.</param>
    /// <returns>The decoded text. How a family treats an ID outside the vocabulary is part of its documented behaviour.</returns>
    public abstract string Decode(ReadOnlySpan<int> ids, bool skipSpecialTokens = true);

    /// <summary>Turns token IDs back into text in a caller's buffer.</summary>
    /// <param name="ids">The IDs.</param>
    /// <param name="destination">Receives the text.</param>
    /// <param name="idsConsumed">The number of IDs decoded: all of them, or none when the text does not fit.</param>
    /// <param name="charsWritten">The number of characters written; 0 when the text does not fit.</param>
    /// <param name="skipSpecialTokens">Whether special tokens are left out of the text.</param>
    /// <returns><see cref="OperationStatus.Done"/> or <see cref="OperationStatus.DestinationTooSmall"/>. A family that can decode a prefix may override this to write one.</returns>
    public virtual OperationStatus Decode(ReadOnlySpan<int> ids, Span<char> destination, out int idsConsumed, out int charsWritten, bool skipSpecialTokens = true)
    {
        string text = Decode(ids, skipSpecialTokens);
        if (text.Length > destination.Length)
        {
            idsConsumed = 0;
            charsWritten = 0;
            return OperationStatus.DestinationTooSmall;
        }

        text.CopyTo(destination);
        idsConsumed = ids.Length;
        charsWritten = text.Length;
        return OperationStatus.Done;
    }

    /// <summary>Gets the vocabulary string of an ID.</summary>
    /// <param name="id">The token ID.</param>
    /// <returns>The token, or <see langword="null"/> when the ID is not in the vocabulary.</returns>
    public abstract string? IdToToken(int id);

    /// <summary>Gets the ID of a vocabulary string.</summary>
    /// <param name="token">The token exactly as it appears in the vocabulary.</param>
    /// <returns>The ID, or <see langword="null"/> when the token is not in the vocabulary.</returns>
    public abstract int? TokenToId(string token);

    /// <summary>
    /// Encodes one text, as a family's core. Called by every public method; <paramref name="truncation"/> is <see cref="Truncation"/> for
    /// <c>Encode</c> and <see langword="null"/> for counting, trimming and <c>EncodeToIds</c>. Padding is not the family's job.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="addSpecialTokens">Whether to run the post-processor's special-token template.</param>
    /// <param name="truncation">The truncation to apply, counting special tokens against the limit as Hugging Face does, or <see langword="null"/> for none.</param>
    /// <returns>The encoding, offsets as in <see cref="TokenOffset"/>.</returns>
    protected abstract TokenEncoding EncodeCore(ReadOnlySpan<char> text, bool addSpecialTokens, TruncationOptions? truncation);

    /// <summary>Encodes a pair, as a family's core. The default throws; families with a pair template override it.</summary>
    /// <param name="text">The first text.</param>
    /// <param name="pair">The second text.</param>
    /// <param name="addSpecialTokens">Whether to run the post-processor's pair template.</param>
    /// <param name="truncation">The truncation to apply, or <see langword="null"/>.</param>
    /// <exception cref="NotSupportedException">The family does not encode pairs.</exception>
    protected virtual TokenEncoding EncodePairCore(ReadOnlySpan<char> text, ReadOnlySpan<char> pair, bool addSpecialTokens, TruncationOptions? truncation) =>
        throw new NotSupportedException("This tokenizer does not encode text pairs.");

    private static OperationStatus CopyIds(TokenEncoding encoding, int inputLength, Span<int> ids, out int written, out int consumed)
    {
        written = Math.Min(ids.Length, encoding.Length);
        encoding.Ids.AsSpan(0, written).CopyTo(ids);
        if (written == encoding.Length)
        {
            consumed = inputLength;
            return OperationStatus.Done;
        }

        consumed = 0;
        for (int i = 0; i < written; i++)
        {
            consumed = Math.Max(consumed, encoding.Offsets[i].End);
        }

        return OperationStatus.DestinationTooSmall;
    }

    private TokenEncoding PadSingle(TokenEncoding encoding) =>
        Padding is { } padding ? encoding.PaddedTo(padding, TargetLength(padding, encoding.Length)) : encoding;

    private static int TargetLength(PaddingOptions padding, int longest)
    {
        int target = padding.Length ?? longest;
        if (padding.PadToMultipleOf is { } m && m > 0)
        {
            target = ((target + m - 1) / m) * m;
        }

        return target;
    }
}
