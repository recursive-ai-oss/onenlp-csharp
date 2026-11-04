using System.Buffers;
using System.Text;

namespace RecursiveAI.OneNlp.Tokenizers.Unigram;

/// <summary>
/// The Unigram language model tokenizer (Kudo 2018, "Subword Regularization", section 2): the segmentation of a piece that maximizes
/// the sum of the pieces' log-probability scores, found by Viterbi search over the lattice of every way to cover the text with
/// vocabulary pieces (Kudo and Richardson 2018, SentencePiece, section 2.2).
/// </summary>
/// <remarks>
/// <para>
/// Details the papers leave open were settled by observing the output of Hugging Face tokenizers 0.22.1 on crafted vocabularies:
/// a position where no one-character piece matches gets a one-character unknown node scored ten below the lowest score of the
/// vocabulary; ties go to the segmentation found first (the earliest start, then the shortest piece); consecutive unknown nodes fuse
/// into one token whose value is the text it covers; with byte fallback a fused unknown becomes one <c>&lt;0xXX&gt;</c> token per
/// UTF-8 byte (each reporting the whole range) when every byte has a piece, and stays unknown otherwise.
/// </para>
/// <para>Immutable after construction; safe for concurrent use.</para>
/// </remarks>
internal sealed class UnigramModel : Hf.ITokenizerModel
{
    /// <summary>How far below the lowest vocabulary score an unknown character is scored.</summary>
    private const double UnknownPenalty = 10.0;

    private const byte EndsMatch = 1;
    private const byte NeedsEnd = 2;

    private readonly string[] _pieces;
    private readonly double[] _scores;
    private readonly TokenIdTable _ids;
    private readonly PieceTrie _trie;
    private readonly int? _unkId;
    private readonly double _unkScore;
    private readonly int[]? _byteIds;

    /// <summary>Creates the model.</summary>
    /// <param name="pieces">The pieces, indexed by ID.</param>
    /// <param name="scores">The score of each piece, same length as <paramref name="pieces"/>.</param>
    /// <param name="unkId">The ID of the unknown token, or <see langword="null"/> when there is none (an unknown character is then an error).</param>
    /// <param name="byteFallback">Whether unknown text is spelled with <c>&lt;0xXX&gt;</c> byte pieces when they all exist.</param>
    /// <param name="matchable">Which pieces can be matched in text, or <see langword="null"/> for all. A SentencePiece model keeps its control and byte pieces out of the lattice.</param>
    internal UnigramModel(string[] pieces, double[] scores, int? unkId, bool byteFallback, bool[]? matchable = null)
    {
        ArgumentNullException.ThrowIfNull(pieces);
        ArgumentNullException.ThrowIfNull(scores);
        if (pieces.Length != scores.Length)
        {
            throw new InvalidDataException("The vocabulary has a different number of pieces and scores.");
        }

        if (unkId is { } u && (u < 0 || u >= pieces.Length))
        {
            throw new InvalidDataException($"unk_id {u} is outside the vocabulary of {pieces.Length} pieces.");
        }

        _pieces = pieces;
        _scores = scores;
        _unkId = unkId;
        var entries = new List<KeyValuePair<string, int>>(pieces.Length);
        double min = double.PositiveInfinity;
        for (int i = 0; i < pieces.Length; i++)
        {
            entries.Add(new KeyValuePair<string, int>(pieces[i], i));
            min = Math.Min(min, scores[i]);
        }

        _ids = new TokenIdTable(entries);
        _unkScore = (pieces.Length == 0 ? 0 : min) - UnknownPenalty;

        string[] inTrie = pieces;
        if (matchable is not null)
        {
            inTrie = new string[pieces.Length];
            for (int i = 0; i < pieces.Length; i++)
            {
                inTrie[i] = matchable[i] ? pieces[i] : string.Empty;
            }
        }

        _trie = new PieceTrie(inTrie);

        if (byteFallback)
        {
            // A missing byte piece only matters for the characters that need it, so the table keeps the gaps (-1).
            var bytes = new int[256];
            for (int b = 0; b < 256; b++)
            {
                bytes[b] = _ids.GetId("<0x" + b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture) + ">") ?? -1;
            }

            _byteIds = bytes;
        }
    }

    /// <inheritdoc/>
    public int VocabularySize => _pieces.Length;

    /// <inheritdoc/>
    public int? TokenToId(string token) => _ids.GetId(token);

    /// <inheritdoc/>
    public string? IdToToken(int id) => (uint)id < (uint)_pieces.Length ? _pieces[id] : null;

    /// <inheritdoc/>
    /// <exception cref="InvalidDataException">The piece has a character no piece covers and the model has no unknown token.</exception>
    public void Tokenize(ReadOnlySpan<char> piece, List<Hf.ModelToken> output)
    {
        int n = piece.Length;
        if (n == 0)
        {
            return;
        }

        double[] best = ArrayPool<double>.Shared.Rent(n + 1);
        int[] prevStart = ArrayPool<int>.Shared.Rent(n + 1);
        int[] prevId = ArrayPool<int>.Shared.Rent(n + 1);
        byte[] flags = ArrayPool<byte>.Shared.Rent(n + 1);
        try
        {
            // Kudo 2018 §3.2 / Viterbi: best[e] is the best score of covering piece[..e]; positions are visited left to right, so
            // every node that ends at e has been tried before e is used as a start.
            Array.Fill(best, double.NegativeInfinity, 0, n + 1);
            Array.Clear(flags, 0, n + 1);
            best[0] = 0;
            for (int start = 0; start < n; start++)
            {
                if (start > 0 && char.IsLowSurrogate(piece[start]) && char.IsHighSurrogate(piece[start - 1]))
                {
                    continue; // inside a surrogate pair
                }

                double here = best[start];
                if (double.IsNegativeInfinity(here) && _unkId is not null)
                {
                    continue; // unreachable
                }

                int charLength = start + 1 < n && char.IsHighSurrogate(piece[start]) && char.IsLowSurrogate(piece[start + 1]) ? 2 : 1;
                bool hasSingle = false;
                int node = PieceTrie.Root;
                for (int i = start; i < n; i++)
                {
                    node = _trie.Step(node, piece[i]);
                    if (node < 0)
                    {
                        break;
                    }

                    int id = _trie.TerminalId(node);
                    if (id < 0)
                    {
                        continue;
                    }

                    int end = i + 1;
                    flags[end] |= EndsMatch;
                    hasSingle |= end - start == charLength;
                    double candidate = here + _scores[id];
                    if (candidate > best[end])
                    {
                        best[end] = candidate;
                        prevStart[end] = start;
                        prevId[end] = id;
                    }
                }

                if (!hasSingle)
                {
                    int end = start + charLength;
                    flags[end] |= NeedsEnd;
                    double candidate = here + _unkScore;
                    if (_unkId is not null && candidate > best[end])
                    {
                        best[end] = candidate;
                        prevStart[end] = start;
                        prevId[end] = -1;
                    }
                }
            }

            if (_unkId is null)
            {
                // Observed in output of tokenizers 0.22.1: without an unk_id, a character that has no one-character piece is an error
                // unless some matched piece ends right after it, even when the best path never needs the missing piece.
                for (int e = 1; e <= n; e++)
                {
                    if ((flags[e] & (NeedsEnd | EndsMatch)) == NeedsEnd)
                    {
                        throw new InvalidDataException("The text has a character that no piece covers, and the model has no unk_id.");
                    }
                }
            }

            Emit(piece, best, prevStart, prevId, output);
        }
        finally
        {
            ArrayPool<double>.Shared.Return(best);
            ArrayPool<int>.Shared.Return(prevStart);
            ArrayPool<int>.Shared.Return(prevId);
            ArrayPool<byte>.Shared.Return(flags);
        }
    }

    private void Emit(ReadOnlySpan<char> piece, double[] best, int[] prevStart, int[] prevId, List<Hf.ModelToken> output)
    {
        int n = piece.Length;
        if (double.IsNegativeInfinity(best[n]))
        {
            throw new InvalidDataException("The text cannot be covered by the vocabulary.");
        }

        // The path is found backwards, from the end.
        int count = 0;
        for (int e = n; e > 0; e = prevStart[e])
        {
            count++;
        }

        int[] ends = ArrayPool<int>.Shared.Rent(count);
        try
        {
            int k = count;
            for (int e = n; e > 0; e = prevStart[e])
            {
                ends[--k] = e;
            }

            int runStart = -1;
            int runEnd = -1;
            int runNodes = 0;
            bool runIsMatch = false;
            for (int j = 0; j < count; j++)
            {
                int end = ends[j];
                int start = prevStart[end];
                int id = prevId[end];
                // A synthesized node (-1) and any vocabulary piece that has the unknown ID both count as unknown text.
                if (id == -1 || id == _unkId)
                {
                    if (_unkId is null)
                    {
                        throw new InvalidDataException("The text has a character that no piece covers, and the model has no unk_id.");
                    }

                    // Consecutive unknown nodes fuse into one token.
                    if (runStart < 0)
                    {
                        runStart = start;
                        runNodes = 0;
                        runIsMatch = id != -1;
                    }

                    runNodes++;
                    runEnd = end;
                    continue;
                }

                FlushUnknown(piece, ref runStart, runEnd, runNodes == 1 && runIsMatch, output);
                output.Add(new Hf.ModelToken(id, _pieces[id], start, end));
            }

            FlushUnknown(piece, ref runStart, runEnd, runNodes == 1 && runIsMatch, output);
        }
        finally
        {
            ArrayPool<int>.Shared.Return(ends);
        }
    }

    private void FlushUnknown(ReadOnlySpan<char> piece, ref int runStart, int runEnd, bool singleMatch, List<Hf.ModelToken> output)
    {
        if (runStart < 0)
        {
            return;
        }

        ReadOnlySpan<char> text = piece[runStart..runEnd];
        int start = runStart;
        runStart = -1;
        // A lone vocabulary piece with the unknown ID is kept as it is (observed); anything else unknown is spelled in bytes.
        if (_byteIds is not null && !singleMatch)
        {
            // The text is at most the piece, which the caller holds, so its UTF-8 bytes are at most three times that.
            byte[] bytes = Encoding.UTF8.GetBytes(text.ToString());
            bool complete = true;
            foreach (byte b in bytes)
            {
                complete &= _byteIds[b] >= 0;
            }

            if (complete)
            {
                foreach (byte b in bytes)
                {
                    int id = _byteIds[b];
                    output.Add(new Hf.ModelToken(id, _pieces[id], start, runEnd));
                }

                return;
            }
        }

        output.Add(new Hf.ModelToken(_unkId!.Value, text.ToString(), start, runEnd));
    }
}
