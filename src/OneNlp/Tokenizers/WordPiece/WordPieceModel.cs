using System.Buffers;

namespace RecursiveAI.OneNlp.Tokenizers.WordPiece;

/// <summary>
/// The WordPiece model of BERT (Schuster and Nakajima 2012; Devlin et al. 2019, section 4.1 and appendix A.3): a word is cut into the
/// longest vocabulary pieces from the left, greedily, with every piece after the first carrying a continuing-subword prefix
/// (<c>##</c>). A word that cannot be covered completely, or is longer than the limit, becomes one unknown token.
/// </summary>
/// <remarks>Immutable after construction; safe for concurrent use.</remarks>
internal sealed class WordPieceModel : Hf.ITokenizerModel
{
    private readonly TokenIdTable _vocab;
    private readonly string _unkToken;
    private readonly string _prefix;
    private readonly int _maxInputChars;
    private readonly int? _unkId;

    /// <summary>Creates the model.</summary>
    /// <param name="vocab">The vocabulary.</param>
    /// <param name="unkToken">The unknown token, used when a word has no complete segmentation.</param>
    /// <param name="continuingSubwordPrefix">The prefix of every piece that does not start its word.</param>
    /// <param name="maxInputCharsPerWord">The most characters (Unicode scalar values, not UTF-16 units) in a word that is segmented; a longer word is unknown.</param>
    internal WordPieceModel(TokenIdTable vocab, string unkToken, string continuingSubwordPrefix, int maxInputCharsPerWord)
    {
        ArgumentNullException.ThrowIfNull(vocab);
        ArgumentNullException.ThrowIfNull(unkToken);
        ArgumentNullException.ThrowIfNull(continuingSubwordPrefix);
        _vocab = vocab;
        _unkToken = unkToken;
        _prefix = continuingSubwordPrefix;
        _maxInputChars = maxInputCharsPerWord;
        _unkId = vocab.GetId(unkToken);
    }

    /// <inheritdoc/>
    public int VocabularySize => _vocab.Count;

    /// <inheritdoc/>
    public int? TokenToId(string token) => _vocab.GetId(token);

    /// <inheritdoc/>
    public string? IdToToken(int id) => _vocab.GetToken(id);

    /// <inheritdoc/>
    /// <exception cref="InvalidDataException">The word needs the unknown token and the vocabulary has none.</exception>
    public void Tokenize(ReadOnlySpan<char> piece, List<Hf.ModelToken> output)
    {
        if (CountScalars(piece) > _maxInputChars)
        {
            AddUnknown(piece.Length, output);
            return;
        }

        int first = output.Count;
        char[]? rented = null;
        try
        {
            int start = 0;
            while (start < piece.Length)
            {
                // Probe text for a non-initial piece is prefix + the rest of the word, so every probe is a prefix of one buffer.
                ReadOnlySpan<char> rest = piece[start..];
                ReadOnlySpan<char> probe = rest;
                int offset = 0;
                if (start > 0 && _prefix.Length > 0)
                {
                    int needed = _prefix.Length + rest.Length;
                    if (rented is null || rented.Length < needed)
                    {
                        if (rented is not null)
                        {
                            ArrayPool<char>.Shared.Return(rented);
                        }

                        // Bounded by the word: at most prefix length plus the piece length, which the caller already holds in memory.
                        rented = ArrayPool<char>.Shared.Rent(needed);
                    }

                    _prefix.CopyTo(rented);
                    rest.CopyTo(rented.AsSpan(_prefix.Length));
                    probe = rented.AsSpan(0, needed);
                    offset = _prefix.Length;
                }

                // Longest match first: shorten from the right one scalar value at a time.
                int end = rest.Length;
                int id = -1;
                while (end > 0)
                {
                    if (_vocab.TryGetId(probe[..(offset + end)], out id))
                    {
                        break;
                    }

                    end -= end >= 2 && char.IsLowSurrogate(rest[end - 1]) && char.IsHighSurrogate(rest[end - 2]) ? 2 : 1;
                    id = -1;
                }

                if (end == 0)
                {
                    // One piece of the word has no match, so the whole word is unknown (BERT paper, appendix A.3).
                    output.RemoveRange(first, output.Count - first);
                    AddUnknown(piece.Length, output);
                    return;
                }

                output.Add(new Hf.ModelToken(id, _vocab.GetToken(id)!, start, start + end));
                start += end;
            }
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidDataException">The word needs the unknown token and the vocabulary has none.</exception>
    public void TokenizeIds(ReadOnlySpan<char> piece, Hf.IdBuffer output)
    {
        if (CountScalars(piece) > _maxInputChars)
        {
            AddUnknownId(output);
            return;
        }

        int first = output.Count;
        char[]? rented = null;
        try
        {
            int start = 0;
            while (start < piece.Length)
            {
                ReadOnlySpan<char> rest = piece[start..];
                ReadOnlySpan<char> probe = rest;
                int offset = 0;
                if (start > 0 && _prefix.Length > 0)
                {
                    int needed = _prefix.Length + rest.Length;
                    if (rented is null || rented.Length < needed)
                    {
                        if (rented is not null)
                        {
                            ArrayPool<char>.Shared.Return(rented);
                        }

                        // Bounded by the word: at most prefix length plus the piece length, which the caller already holds in memory.
                        rented = ArrayPool<char>.Shared.Rent(needed);
                    }

                    _prefix.CopyTo(rented);
                    rest.CopyTo(rented.AsSpan(_prefix.Length));
                    probe = rented.AsSpan(0, needed);
                    offset = _prefix.Length;
                }

                int end = rest.Length;
                int id = -1;
                while (end > 0)
                {
                    if (_vocab.TryGetId(probe[..(offset + end)], out id))
                    {
                        break;
                    }

                    end -= end >= 2 && char.IsLowSurrogate(rest[end - 1]) && char.IsHighSurrogate(rest[end - 2]) ? 2 : 1;
                    id = -1;
                }

                if (end == 0)
                {
                    output.Truncate(first);
                    AddUnknownId(output);
                    return;
                }

                output.Add(id);
                start += end;
            }
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }

    private void AddUnknownId(Hf.IdBuffer output)
    {
        if (_unkId is not { } id)
        {
            throw new InvalidDataException($"The unknown token '{_unkToken}' is not in the vocabulary, and a word needs it.");
        }

        output.Add(id);
    }

    private static int CountScalars(ReadOnlySpan<char> text)
    {
        int n = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (!char.IsLowSurrogate(text[i]) || i == 0 || !char.IsHighSurrogate(text[i - 1]))
            {
                n++;
            }
        }

        return n;
    }

    private void AddUnknown(int length, List<Hf.ModelToken> output)
    {
        if (_unkId is not { } id)
        {
            throw new InvalidDataException($"The unknown token '{_unkToken}' is not in the vocabulary, and a word needs it.");
        }

        output.Add(new Hf.ModelToken(id, _unkToken, 0, length));
    }
}
