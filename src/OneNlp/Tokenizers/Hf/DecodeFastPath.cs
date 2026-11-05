using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Unicode;

namespace RecursiveAI.OneNlp.Tokenizers.Hf;

/// <summary>
/// Decodes IDs straight to text for the decoder shapes that dominate in practice, without a list of strings per step. It gives exactly
/// the text the general chain gives; the parity run compares both with the reference library.
/// </summary>
internal abstract class DecodeFastPath
{
    /// <summary>Decodes the tokens, which are already filtered of skipped special tokens and of IDs outside the vocabulary.</summary>
    /// <param name="ids">The IDs.</param>
    /// <param name="skipSpecialTokens">Whether special tokens are left out.</param>
    /// <param name="tokens">Gives the vocabulary string of an ID, or <see langword="null"/> for an ID to leave out.</param>
    internal abstract string Decode(ReadOnlySpan<int> ids, bool skipSpecialTokens, TokenTable tokens);

    /// <summary>Recognises the decoder shape, or returns <see langword="null"/>.</summary>
    internal static DecodeFastPath? TryCreate(ITokenDecoder? decoder)
    {
        switch (decoder)
        {
            case ByteLevelDecoder:
                return new ByteLevelPath();
            case MetaspaceDecoder m:
                return new MetaspacePath(m.Mark, m.StripFirst);
            case WordPieceDecoder w:
                return new WordPiecePath(w.Prefix, w.CleanupEnabled);
            case SequenceDecoder { Steps: [ReplaceDecoder { Pattern.Length: 1 } replace, ByteFallbackDecoder, FuseDecoder, StripDecoder { Content: ' ', Start: 1, Stop: 0 }] } when replace.Content == " ":
                return new SentencePieceLikePath(replace.Pattern[0]);
            default:
                return null;
        }
    }
}

/// <summary>The vocabulary string of every ID, as arrays, with the special flag of the added tokens.</summary>
internal sealed class TokenTable
{
    private readonly string?[] _tokens;
    private readonly bool[] _special;

    /// <summary>Builds the table, or returns <see langword="null"/> when the IDs are too sparse for arrays.</summary>
    internal static TokenTable? TryCreate(ITokenizerModel model, AddedVocabulary added)
    {
        int max = model.VocabularySize - 1;
        foreach (AddedToken t in added.Tokens)
        {
            max = Math.Max(max, t.Id);
        }

        // Models normally number their tokens densely; a hostile file must not make this table enormous.
        if (max < 0 || max > (4 * (model.VocabularySize + added.Count)) + 65_536)
        {
            return null;
        }

        var table = new TokenTable(max + 1);
        int known = 0;
        for (int id = 0; id <= max; id++)
        {
            if ((table._tokens[id] = model.IdToToken(id)) is not null)
            {
                known++;
            }
        }

        if (known != model.VocabularySize)
        {
            return null; // IDs with gaps: the model knows tokens this table did not see, so leave decoding to the general path
        }

        foreach (AddedToken t in added.Tokens)
        {
            if ((uint)t.Id < (uint)table._tokens.Length)
            {
                table._tokens[t.Id] = t.Content;
                table._special[t.Id] = t.Special;
            }
        }

        return table;
    }

    private TokenTable(int size)
    {
        _tokens = new string?[size];
        _special = new bool[size];
    }

    /// <summary>The token of an ID, or <see langword="null"/> when it is not in the vocabulary or is a special token that is skipped.</summary>
    internal string? Get(int id, bool skipSpecialTokens) =>
        (uint)id < (uint)_tokens.Length && !(skipSpecialTokens && _special[id]) ? _tokens[id] : null;
}

/// <summary>Byte-level decoding: table characters back to bytes, bytes read as UTF-8.</summary>
internal sealed class ByteLevelPath : DecodeFastPath
{
    private const int StackBytes = 1024;

    internal override string Decode(ReadOnlySpan<int> ids, bool skipSpecialTokens, TokenTable tokens)
    {
        // A character is at most three bytes. Short inputs use a fixed stack buffer; a token that would not fit moves the work to a rented one.
        byte[]? rented = null;
        Span<byte> bytes = stackalloc byte[StackBytes];
        int n = 0;
        try
        {
            foreach (int id in ids)
            {
                if (tokens.Get(id, skipSpecialTokens) is not { } token)
                {
                    continue;
                }

                if (n + (token.Length * 3) > bytes.Length)
                {
                    byte[] bigger = ArrayPool<byte>.Shared.Rent(Math.Max(bytes.Length * 2, n + (token.Length * 3)));
                    bytes[..n].CopyTo(bigger);
                    if (rented is not null)
                    {
                        ArrayPool<byte>.Shared.Return(rented);
                    }

                    rented = bigger;
                    bytes = bigger;
                }

                foreach (char c in token)
                {
                    int b = ByteLevelTable.ToByte(c);
                    if (b >= 0)
                    {
                        bytes[n++] = (byte)b;
                    }
                    else if (char.IsSurrogate(c))
                    {
                        // A character alone is read as text: an unpaired surrogate becomes U+FFFD.
                        bytes[n++] = 0xEF;
                        bytes[n++] = 0xBF;
                        bytes[n++] = 0xBD;
                    }
                    else
                    {
                        n += Encoding.UTF8.GetBytes(new ReadOnlySpan<char>(in c), bytes[n..]);
                    }
                }
            }

            return Encoding.UTF8.GetString(bytes[..n]);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }
}

/// <summary>Output space for the decoders that build text: a stack buffer for short text, a rented one otherwise.</summary>
internal static class DecodeBuffer
{
    internal const int StackChars = 512;

    /// <summary>The number of characters the tokens hold, plus <paramref name="extraPerToken"/> for each.</summary>
    internal static int Bound(ReadOnlySpan<int> ids, bool skipSpecialTokens, TokenTable tokens, int extraPerToken)
    {
        long total = 0;
        foreach (int id in ids)
        {
            if (tokens.Get(id, skipSpecialTokens) is { } t)
            {
                total += t.Length + extraPerToken;
            }
        }

        return checked((int)total);
    }
}

/// <summary>The marker back to a space in every token, and the leading spaces of the first token removed.</summary>
internal sealed class MetaspacePath(char marker, bool stripFirst) : DecodeFastPath
{
    internal override string Decode(ReadOnlySpan<int> ids, bool skipSpecialTokens, TokenTable tokens)
    {
        int bound = DecodeBuffer.Bound(ids, skipSpecialTokens, tokens, 0);
        char[]? rented = null;
        Span<char> buffer = bound <= DecodeBuffer.StackChars ? stackalloc char[DecodeBuffer.StackChars] : (rented = ArrayPool<char>.Shared.Rent(bound));
        try
        {
            int n = 0;
            bool first = true;
            foreach (int id in ids)
            {
                if (tokens.Get(id, skipSpecialTokens) is not { } token)
                {
                    continue;
                }

                bool stripping = first && stripFirst; // every leading space goes, not just one
                first = false;
                foreach (char c in token)
                {
                    char d = c == marker ? ' ' : c;
                    if (stripping && d == ' ')
                    {
                        continue;
                    }

                    stripping = false;
                    buffer[n++] = d;
                }
            }

            return new string(buffer[..n]);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }
}

/// <summary>WordPiece decoding: continuation prefixes removed, other tokens joined by a space, optional punctuation cleanup.</summary>
internal sealed class WordPiecePath(string prefix, bool cleanup) : DecodeFastPath
{
    internal override string Decode(ReadOnlySpan<int> ids, bool skipSpecialTokens, TokenTable tokens)
    {
        int bound = DecodeBuffer.Bound(ids, skipSpecialTokens, tokens, 1);
        char[]? rented = null;
        Span<char> buffer = bound <= DecodeBuffer.StackChars ? stackalloc char[DecodeBuffer.StackChars] : (rented = ArrayPool<char>.Shared.Rent(bound));
        try
        {
            int n = 0;
            bool first = true;
            foreach (int id in ids)
            {
                if (tokens.Get(id, skipSpecialTokens) is not { } token)
                {
                    continue;
                }

                ReadOnlySpan<char> piece = token;
                bool space = false;
                if (!first)
                {
                    if (token.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        piece = token.AsSpan(prefix.Length);
                    }
                    else
                    {
                        space = true;
                    }
                }

                first = false;
                // The cleanup only rewrites a space followed by punctuation or a contraction, so most tokens are copied as they are.
                bool rewrite = cleanup && (WordPieceDecoder.CleanupMayChange(piece) || (space && piece.Length > 0 && WordPieceDecoder.CleanupMayChange([' ', piece[0]])));
                if (rewrite)
                {
                    string cleaned = WordPieceDecoder.Cleanup(space ? " " + piece.ToString() : piece.ToString());
                    cleaned.CopyTo(buffer[n..]);
                    n += cleaned.Length;
                }
                else
                {
                    if (space)
                    {
                        buffer[n++] = ' ';
                    }

                    piece.CopyTo(buffer[n..]);
                    n += piece.Length;
                }
            }

            return new string(buffer[..n]);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }
}

/// <summary>
/// The decoder chain of SentencePiece-style tokenizers: the space marker back to a space, runs of <c>&lt;0xHH&gt;</c> tokens read as UTF-8
/// (a run that is not valid UTF-8 becomes one U+FFFD per byte), everything joined, and one leading space removed.
/// </summary>
internal sealed class SentencePieceLikePath(char marker) : DecodeFastPath
{
    internal override string Decode(ReadOnlySpan<int> ids, bool skipSpecialTokens, TokenTable tokens)
    {
        // A byte token is six characters and stands for one byte, which is at most one character, so the tokens' length bounds the text.
        int bound = DecodeBuffer.Bound(ids, skipSpecialTokens, tokens, 0);
        char[]? rentedChars = null;
        Span<char> buffer = bound <= DecodeBuffer.StackChars ? stackalloc char[DecodeBuffer.StackChars] : (rentedChars = ArrayPool<char>.Shared.Rent(bound));
        byte[]? rentedBytes = null;
        Span<byte> run = stackalloc byte[64];
        int runLength = 0;
        int n = 0;
        try
        {
            foreach (int id in ids)
            {
                if (tokens.Get(id, skipSpecialTokens) is not { } token)
                {
                    continue;
                }

                if (token.Length == 6 && token.StartsWith("<0x", StringComparison.Ordinal) && token[5] == '>'
                    && byte.TryParse(token.AsSpan(3, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out byte b))
                {
                    if (runLength == run.Length)
                    {
                        byte[] bigger = ArrayPool<byte>.Shared.Rent(run.Length * 2);
                        run[..runLength].CopyTo(bigger);
                        if (rentedBytes is not null)
                        {
                            ArrayPool<byte>.Shared.Return(rentedBytes);
                        }

                        rentedBytes = bigger;
                        run = bigger;
                    }

                    run[runLength++] = b;
                    continue;
                }

                Flush(buffer, ref n, run, ref runLength);
                foreach (char c in token)
                {
                    buffer[n++] = c == marker ? ' ' : c;
                }
            }

            Flush(buffer, ref n, run, ref runLength);
            // The strip step: up to one leading space.
            return n > 0 && buffer[0] == ' ' ? new string(buffer[1..n]) : new string(buffer[..n]);
        }
        finally
        {
            if (rentedChars is not null)
            {
                ArrayPool<char>.Shared.Return(rentedChars);
            }

            if (rentedBytes is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedBytes);
            }
        }
    }

    private static void Flush(Span<char> buffer, ref int n, Span<byte> run, ref int runLength)
    {
        if (runLength == 0)
        {
            return;
        }

        ReadOnlySpan<byte> bytes = run[..runLength];
        if (Utf8.IsValid(bytes))
        {
            n += Encoding.UTF8.GetChars(bytes, buffer[n..]);
        }
        else
        {
            buffer.Slice(n, runLength).Fill('\uFFFD');
            n += runLength;
        }

        runLength = 0;
    }
}
