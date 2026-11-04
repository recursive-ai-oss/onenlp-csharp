using System.Collections.Concurrent;
using System.Text;
using RecursiveAI.OneNlp.Tokenizers.Hf;

namespace RecursiveAI.OneNlp.Tokenizers.Bpe;

/// <summary>The settings of a <see cref="BpeModel"/>, the fields of the <c>BPE</c> model object of a <c>tokenizer.json</c>.</summary>
/// <param name="UnknownToken">The token for characters that are not in the vocabulary, or <see langword="null"/> to drop such characters.</param>
/// <param name="ContinuingSubwordPrefix">A prefix that the vocabulary puts on every symbol that is not at the start of a word (WordPiece-style <c>##</c>), or <see langword="null"/>.</param>
/// <param name="EndOfWordSuffix">A suffix that the vocabulary puts on the last symbol of a word (<c>&lt;/w&gt;</c>), or <see langword="null"/>.</param>
/// <param name="FuseUnknown">Whether a run of unknown characters becomes one unknown token instead of one per character.</param>
/// <param name="ByteFallback">Whether an unknown character is spelled with its UTF-8 bytes as <c>&lt;0xNN&gt;</c> tokens before the unknown token is tried.</param>
/// <param name="IgnoreMerges">Whether a word that is a whole vocabulary entry is one token without any merging.</param>
/// <param name="Dropout">The probability in [0, 1] that a merge is skipped (BPE-dropout, Provilkov et al. 2020); 0 for the deterministic algorithm.</param>
internal sealed record BpeOptions(
    string? UnknownToken = null,
    string? ContinuingSubwordPrefix = null,
    string? EndOfWordSuffix = null,
    bool FuseUnknown = false,
    bool ByteFallback = false,
    bool IgnoreMerges = false,
    double Dropout = 0);

/// <summary>
/// Byte-pair encoding (Sennrich, Haddow and Birch 2016, §3.2): a word starts as one symbol per character, then the adjacent pair with
/// the best (lowest) merge rank is replaced by its merged symbol until no listed pair is left. Byte-level BPE (GPT-2) is this same
/// model over text that the ByteLevel pre-tokenizer has already mapped to printable characters, one per byte.
/// </summary>
/// <remarks>
/// <para>
/// Ties between equal ranks go to the leftmost pair, as the published model description for Hugging Face tokenizers states. Offsets
/// are UTF-16 units of the piece. A token made of byte-fallback or unknown symbols covers the whole characters it stands for.
/// </para>
/// <para>Immutable after construction; safe for concurrent use. Results are cached per piece (without dropout).</para>
/// </remarks>
internal sealed class BpeModel : ITokenizerModel
{
    private const int CacheCapacity = 20_000;
    private const int CacheMaxPieceLength = 128;

    private readonly Dictionary<string, int> _vocab;
    private readonly Dictionary<string, int>.AlternateLookup<ReadOnlySpan<char>> _vocabBySpan;
    private readonly string?[] _idToToken;
    private readonly Dictionary<long, MergeEntry> _merges;
    private readonly int _unknownId;
    private readonly string? _prefix;
    private readonly string? _suffix;
    private readonly bool _fuseUnknown;
    private readonly bool _ignoreMerges;
    private readonly double _dropout;
    private readonly int[]? _byteFallbackIds;
    private readonly ConcurrentDictionary<string, ModelToken[]> _cache = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ModelToken[]>.AlternateLookup<ReadOnlySpan<char>> _cacheBySpan;

    [ThreadStatic]
    private static Workspace? t_workspace;

    private static long s_dropoutCounter;

    private readonly record struct MergeEntry(int Rank, int NewId);

    private struct Symbol
    {
        public int Id; // -1 once merged into its left neighbour
        public int Start;
        public int End;
        public int Prev;
        public int Next;
    }

    private sealed class Workspace
    {
        public Symbol[] Symbols = new Symbol[64];
        public ulong[] Heap = new ulong[128];
        public int HeapCount;
        public List<ulong> Skipped = [];
    }

    private BpeModel(Dictionary<string, int> vocab, Dictionary<long, MergeEntry> merges, BpeOptions options)
    {
        _vocab = vocab;
        _vocabBySpan = vocab.GetAlternateLookup<ReadOnlySpan<char>>();
        _merges = merges;
        _cacheBySpan = _cache.GetAlternateLookup<ReadOnlySpan<char>>();

        int maxId = -1;
        foreach (int id in vocab.Values)
        {
            if (id < 0)
            {
                throw new InvalidDataException("A BPE vocabulary ID is negative.");
            }

            maxId = Math.Max(maxId, id);
        }

        // IDs are normally dense; refuse a hostile file that would make the reverse table enormous.
        if (maxId > (4 * vocab.Count) + 65_536)
        {
            throw new InvalidDataException("The BPE vocabulary IDs are too sparse to be valid.");
        }

        _idToToken = new string?[maxId + 1];
        foreach (KeyValuePair<string, int> kv in vocab)
        {
            _idToToken[kv.Value] = kv.Key;
        }

        _unknownId = -1;
        if (options.UnknownToken is { } unk)
        {
            _unknownId = vocab.TryGetValue(unk, out int u) ? u : throw new InvalidDataException($"The unknown token '{unk}' is not in the BPE vocabulary.");
        }

        _prefix = string.IsNullOrEmpty(options.ContinuingSubwordPrefix) ? null : options.ContinuingSubwordPrefix;
        _suffix = string.IsNullOrEmpty(options.EndOfWordSuffix) ? null : options.EndOfWordSuffix;
        _fuseUnknown = options.FuseUnknown;
        _ignoreMerges = options.IgnoreMerges;
        if (options.Dropout is < 0 or > 1 or double.NaN)
        {
            throw new InvalidDataException("The BPE dropout must be between 0 and 1.");
        }

        _dropout = options.Dropout;
        if (options.ByteFallback)
        {
            _byteFallbackIds = new int[256];
            for (int b = 0; b < 256; b++)
            {
                _byteFallbackIds[b] = vocab.TryGetValue($"<0x{b:X2}>", out int id) ? id : -1;
            }
        }
    }

    /// <inheritdoc/>
    public int VocabularySize => _vocab.Count;

    /// <summary>The number of merge rules.</summary>
    internal int MergeCount => _merges.Count;

    /// <summary>Builds a model from a vocabulary and an ordered merge list.</summary>
    /// <param name="vocab">Token to ID.</param>
    /// <param name="merges">The merges, best first: rank is the index. Each pair's two tokens and their joined token must be in the vocabulary.</param>
    /// <param name="options">The settings.</param>
    /// <exception cref="InvalidDataException">A merge refers to a token that is not in the vocabulary, or the settings are unusable.</exception>
    internal static BpeModel Create(Dictionary<string, int> vocab, IReadOnlyList<(string Left, string Right)> merges, BpeOptions options)
    {
        ArgumentNullException.ThrowIfNull(vocab);
        ArgumentNullException.ThrowIfNull(merges);
        ArgumentNullException.ThrowIfNull(options);
        string? prefix = string.IsNullOrEmpty(options.ContinuingSubwordPrefix) ? null : options.ContinuingSubwordPrefix;
        var table = new Dictionary<long, MergeEntry>(merges.Count);
        for (int rank = 0; rank < merges.Count; rank++)
        {
            (string left, string right) = merges[rank];
            if (!vocab.TryGetValue(left, out int a))
            {
                throw new InvalidDataException($"The merge '{left} {right}' uses '{left}', which is not in the vocabulary.");
            }

            if (!vocab.TryGetValue(right, out int b))
            {
                throw new InvalidDataException($"The merge '{left} {right}' uses '{right}', which is not in the vocabulary.");
            }

            // The merged token drops the continuing-subword prefix of its right half (Sennrich et al.'s joined symbol, as a
            // WordPiece-style vocabulary spells it).
            string joined = prefix is not null && right.StartsWith(prefix, StringComparison.Ordinal)
                ? string.Concat(left, right.AsSpan(prefix.Length))
                : left + right;
            if (!vocab.TryGetValue(joined, out int merged))
            {
                throw new InvalidDataException($"The merge '{left} {right}' makes '{joined}', which is not in the vocabulary.");
            }

            // A pair listed twice keeps its last rank.
            table[PairKey(a, b)] = new MergeEntry(rank, merged);
        }

        return new BpeModel(vocab, table, options);
    }

    /// <summary>
    /// Builds a model from the pieces of a SentencePiece BPE model. SentencePiece merges the adjacent pair whose joined piece has the
    /// highest score; here that is the rank of the joined piece's score (higher score, lower rank, equal scores share a rank so that
    /// the leftmost wins).
    /// </summary>
    /// <param name="pieces">The piece strings; a piece's ID is its index.</param>
    /// <param name="scores">The score of each piece.</param>
    /// <param name="mergeable">Whether each piece is an ordinary piece that merges may produce and consume (not unknown, control, byte or user-defined pieces).</param>
    /// <param name="options">The settings (<c>UnknownToken</c> and <c>ByteFallback</c> are the usual ones).</param>
    /// <exception cref="InvalidDataException">The arrays differ in length, or a piece is repeated.</exception>
    internal static BpeModel FromSentencePieces(IReadOnlyList<string> pieces, IReadOnlyList<float> scores, IReadOnlyList<bool> mergeable, BpeOptions options)
    {
        ArgumentNullException.ThrowIfNull(pieces);
        ArgumentNullException.ThrowIfNull(scores);
        ArgumentNullException.ThrowIfNull(mergeable);
        ArgumentNullException.ThrowIfNull(options);
        if (scores.Count != pieces.Count || mergeable.Count != pieces.Count)
        {
            throw new InvalidDataException("The SentencePiece pieces, scores and kinds differ in length.");
        }

        var vocab = new Dictionary<string, int>(pieces.Count, StringComparer.Ordinal);
        for (int i = 0; i < pieces.Count; i++)
        {
            if (!vocab.TryAdd(pieces[i], i))
            {
                throw new InvalidDataException($"The piece '{pieces[i]}' occurs twice.");
            }
        }

        // Dense rank of each distinct score, highest score first.
        float[] distinct = [.. scores.Distinct().OrderByDescending(s => s)];
        var rankOfScore = new Dictionary<float, int>(distinct.Length);
        for (int i = 0; i < distinct.Length; i++)
        {
            rankOfScore[distinct[i]] = i;
        }

        var table = new Dictionary<long, MergeEntry>();
        for (int id = 0; id < pieces.Count; id++)
        {
            string piece = pieces[id];
            if (!mergeable[id] || piece.Length < 2)
            {
                continue;
            }

            int rank = rankOfScore[scores[id]];
            for (int k = 1; k < piece.Length; k++)
            {
                if (char.IsLowSurrogate(piece[k]))
                {
                    continue;
                }

                if (vocab.TryGetValue(piece[..k], out int a) && mergeable[a] && vocab.TryGetValue(piece[k..], out int b) && mergeable[b])
                {
                    long key = PairKey(a, b);
                    if (!table.TryGetValue(key, out MergeEntry existing) || rank < existing.Rank)
                    {
                        table[key] = new MergeEntry(rank, id);
                    }
                }
            }
        }

        return new BpeModel(vocab, table, options);
    }

    /// <summary>Reads the <c>BPE</c> model object of a <c>tokenizer.json</c>: <c>vocab</c>, <c>merges</c> (<c>"a b"</c> strings or <c>["a","b"]</c> pairs) and the settings.</summary>
    /// <param name="config">The model object.</param>
    /// <exception cref="InvalidDataException">The object is malformed.</exception>
    internal static BpeModel FromConfig(StageConfig config)
    {
        System.Text.Json.JsonElement root = config.Element;
        if (!root.TryGetProperty("vocab", out System.Text.Json.JsonElement vocabElement) || vocabElement.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            throw config.Bad("vocab", "an object mapping tokens to IDs");
        }

        var vocab = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (System.Text.Json.JsonProperty p in vocabElement.EnumerateObject())
        {
            if (p.Value.ValueKind != System.Text.Json.JsonValueKind.Number || !p.Value.TryGetInt32(out int id))
            {
                throw config.Bad("vocab", "an object whose values are 32-bit integers");
            }

            vocab[p.Name] = id;
        }

        var merges = new List<(string, string)>();
        if (root.TryGetProperty("merges", out System.Text.Json.JsonElement mergesElement) && mergesElement.ValueKind != System.Text.Json.JsonValueKind.Null)
        {
            if (mergesElement.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                throw config.Bad("merges", "an array");
            }

            foreach (System.Text.Json.JsonElement m in mergesElement.EnumerateArray())
            {
                merges.Add(ReadMerge(m, config));
            }
        }

        double dropout = 0;
        if (config.Has("dropout"))
        {
            System.Text.Json.JsonElement d = root.GetProperty("dropout");
            dropout = d.ValueKind == System.Text.Json.JsonValueKind.Number ? d.GetDouble() : throw config.Bad("dropout", "a number or null");
        }

        var options = new BpeOptions(
            config.OptionalString("unk_token"),
            config.OptionalString("continuing_subword_prefix"),
            config.OptionalString("end_of_word_suffix"),
            config.Bool("fuse_unk", false),
            config.Bool("byte_fallback", false),
            config.Bool("ignore_merges", false),
            dropout);
        return Create(vocab, merges, options);
    }

    /// <inheritdoc/>
    public int? TokenToId(string token) => _vocab.TryGetValue(token, out int id) ? id : null;

    /// <inheritdoc/>
    public string? IdToToken(int id) => (uint)id < (uint)_idToToken.Length ? _idToToken[id] : null;

    /// <inheritdoc/>
    public void Tokenize(ReadOnlySpan<char> piece, List<ModelToken> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (piece.IsEmpty)
        {
            return;
        }

        if (_ignoreMerges && _vocabBySpan.TryGetValue(piece, out int whole))
        {
            output.Add(new ModelToken(whole, _idToToken[whole]!, 0, piece.Length));
            return;
        }

        if (_dropout > 0)
        {
            output.AddRange(Build(piece));
            return;
        }

        if (_cacheBySpan.TryGetValue(piece, out ModelToken[]? cached))
        {
            output.AddRange(cached);
            return;
        }

        ModelToken[] tokens = Build(piece);
        if (piece.Length <= CacheMaxPieceLength && _cache.Count < CacheCapacity)
        {
            _cacheBySpan.TryAdd(piece, tokens);
        }

        output.AddRange(tokens);
    }

    private static long PairKey(int left, int right) => ((long)left << 32) | (uint)right;

    private ModelToken[] Build(ReadOnlySpan<char> piece)
    {
        Workspace ws = t_workspace ??= new Workspace();
        int count = Split(piece, ws);
        if (count == 0)
        {
            return [];
        }

        Merge(ws, count);

        var tokens = new List<ModelToken>(count);
        for (int i = 0; i != -1; i = ws.Symbols[i].Next)
        {
            ref Symbol s = ref ws.Symbols[i];
            tokens.Add(new ModelToken(s.Id, _idToToken[s.Id]!, s.Start, s.End));
        }

        return [.. tokens];
    }

    // Sennrich et al. 2016 §3.2: start from characters. A character the vocabulary lacks is spelled by its bytes (byte fallback), or
    // becomes the unknown token (fused with neighbours when asked), or is dropped when there is no unknown token.
    private int Split(ReadOnlySpan<char> piece, Workspace ws)
    {
        int n = 0;
        bool tailIsFusableUnknown = false;
        int i = 0;
        while (i < piece.Length)
        {
            int len = char.IsHighSurrogate(piece[i]) && i + 1 < piece.Length && char.IsLowSurrogate(piece[i + 1]) ? 2 : 1;
            bool isFirst = i == 0;
            bool isLast = i + len == piece.Length;
            ReadOnlySpan<char> chars = piece.Slice(i, len);

            int id;
            if (_prefix is null && _suffix is null)
            {
                id = _vocabBySpan.TryGetValue(chars, out int found) ? found : -1;
            }
            else
            {
                string key = string.Concat(isFirst ? default : _prefix.AsSpan(), chars, isLast ? _suffix.AsSpan() : default);
                id = _vocab.TryGetValue(key, out int found) ? found : -1;
            }

            if (id >= 0)
            {
                n = Add(ws, n, id, i, i + len);
                tailIsFusableUnknown = false;
            }
            else if (TryByteFallback(chars, out int[]? byteIds))
            {
                foreach (int b in byteIds)
                {
                    n = Add(ws, n, b, i, i + len);
                }

                tailIsFusableUnknown = false;
            }
            else if (_unknownId >= 0)
            {
                if (_fuseUnknown && tailIsFusableUnknown)
                {
                    ws.Symbols[n - 1].End = i + len;
                }
                else
                {
                    n = Add(ws, n, _unknownId, i, i + len);
                }

                tailIsFusableUnknown = true;
            }

            i += len;
        }

        return n;
    }

    private bool TryByteFallback(ReadOnlySpan<char> chars, out int[] ids)
    {
        ids = [];
        if (_byteFallbackIds is null)
        {
            return false;
        }

        var bytes = new byte[8]; // one scalar is at most 4 UTF-8 bytes; a lone surrogate encodes as 3
        int written = Encoding.UTF8.GetBytes(chars, bytes);
        var result = new int[written];
        for (int k = 0; k < written; k++)
        {
            int id = _byteFallbackIds[bytes[k]];
            if (id < 0)
            {
                return false;
            }

            result[k] = id;
        }

        ids = result;
        return true;
    }

    private static int Add(Workspace ws, int n, int id, int start, int end)
    {
        if (n == ws.Symbols.Length)
        {
            Array.Resize(ref ws.Symbols, n * 2);
        }

        ws.Symbols[n] = new Symbol { Id = id, Start = start, End = end, Prev = n - 1, Next = -1 };
        if (n > 0)
        {
            ws.Symbols[n - 1].Next = n;
        }

        return n + 1;
    }

    // Repeatedly merge the lowest-ranked adjacent pair, leftmost first, keeping the candidates in a binary min-heap keyed by
    // (rank, position). A heap entry goes stale when its pair changed; it is re-checked against the merge table when popped.
    private void Merge(Workspace ws, int count)
    {
        ws.HeapCount = 0;
        for (int i = 0; i + 1 < count; i++)
        {
            Push(ws, i);
        }

        ulong dropoutState = _dropout > 0 ? SplitMix(ref s_dropoutCounter) : 0;
        ws.Skipped.Clear();
        while (ws.HeapCount > 0)
        {
            ulong top = Pop(ws);
            int pos = (int)(uint)top;
            int rank = (int)(top >> 32);
            ref Symbol left = ref ws.Symbols[pos];
            if (left.Id < 0 || left.Next < 0)
            {
                continue;
            }

            int next = left.Next;
            if (!_merges.TryGetValue(PairKey(left.Id, ws.Symbols[next].Id), out MergeEntry entry) || entry.Rank != rank)
            {
                continue;
            }

            if (_dropout > 0 && NextUnit(ref dropoutState) < _dropout)
            {
                ws.Skipped.Add(top);
                continue;
            }

            ref Symbol right = ref ws.Symbols[next];
            left.Id = entry.NewId;
            left.End = right.End;
            left.Next = right.Next;
            right.Id = -1;
            if (right.Next >= 0)
            {
                ws.Symbols[right.Next].Prev = pos;
            }

            foreach (ulong skipped in ws.Skipped)
            {
                PushEntry(ws, skipped);
            }

            ws.Skipped.Clear();
            if (left.Prev >= 0)
            {
                Push(ws, left.Prev);
            }

            if (left.Next >= 0)
            {
                Push(ws, pos);
            }
        }
    }

    // BPE-dropout draws are not meant to be reproducible across runs; a counter-driven SplitMix64 keeps this free of shared Random state.
    private static ulong SplitMix(ref long counter)
    {
        ulong z = (ulong)Interlocked.Add(ref counter, unchecked((long)0x9E3779B97F4A7C15UL));
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    private static double NextUnit(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15UL;
        ulong z = state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        z ^= z >> 31;
        return (z >> 11) * (1.0 / (1UL << 53));
    }

    private void Push(Workspace ws, int pos)
    {
        int next = ws.Symbols[pos].Next;
        if (next >= 0 && _merges.TryGetValue(PairKey(ws.Symbols[pos].Id, ws.Symbols[next].Id), out MergeEntry entry))
        {
            PushEntry(ws, ((ulong)(uint)entry.Rank << 32) | (uint)pos);
        }
    }

    private static void PushEntry(Workspace ws, ulong key)
    {
        if (ws.HeapCount == ws.Heap.Length)
        {
            Array.Resize(ref ws.Heap, ws.HeapCount * 2);
        }

        int i = ws.HeapCount++;
        while (i > 0)
        {
            int parent = (i - 1) / 2;
            if (ws.Heap[parent] <= key)
            {
                break;
            }

            ws.Heap[i] = ws.Heap[parent];
            i = parent;
        }

        ws.Heap[i] = key;
    }

    private static ulong Pop(Workspace ws)
    {
        ulong top = ws.Heap[0];
        ulong last = ws.Heap[--ws.HeapCount];
        int i = 0;
        int half = ws.HeapCount / 2;
        while (i < half)
        {
            int child = (2 * i) + 1;
            if (child + 1 < ws.HeapCount && ws.Heap[child + 1] < ws.Heap[child])
            {
                child++;
            }

            if (last <= ws.Heap[child])
            {
                break;
            }

            ws.Heap[i] = ws.Heap[child];
            i = child;
        }

        if (ws.HeapCount > 0)
        {
            ws.Heap[i] = last;
        }

        return top;
    }

    private static InvalidDataException BadMerge(StageConfig config) => config.Bad("merges", "an array of \"left right\" strings or [\"left\", \"right\"] pairs");

    private static (string, string) ReadMerge(System.Text.Json.JsonElement m, StageConfig config)
    {
        if (m.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            // A merge string holds exactly one space between the two tokens (HF `tokenizer.json` format).
            string text = m.GetString()!;
            int space = text.IndexOf(' ', StringComparison.Ordinal);
            if (space < 0 || text.IndexOf(' ', space + 1) >= 0)
            {
                throw BadMerge(config);
            }

            return (text[..space], text[(space + 1)..]);
        }

        if (m.ValueKind == System.Text.Json.JsonValueKind.Array && m.GetArrayLength() == 2
            && m[0].ValueKind == System.Text.Json.JsonValueKind.String && m[1].ValueKind == System.Text.Json.JsonValueKind.String)
        {
            return (m[0].GetString()!, m[1].GetString()!);
        }

        throw BadMerge(config);
    }
}
