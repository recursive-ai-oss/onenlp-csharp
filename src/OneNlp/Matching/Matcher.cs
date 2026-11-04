namespace RecursiveAI.OneNlp.Matching;

/// <summary>One match: the key it was added under and the token range it covers.</summary>
/// <param name="Key">The key given to <c>Add</c>.</param>
/// <param name="Start">The index of the first token.</param>
/// <param name="End">The index just past the last token; greater than <paramref name="Start"/>.</param>
public readonly record struct PatternMatch(string Key, int Start, int End)
{
    /// <summary>The number of tokens matched.</summary>
    public int Length => End - Start;

    /// <summary>Makes a span over the matched tokens, labelled with the key and with confidence 1.</summary>
    /// <param name="doc">The document that was matched.</param>
    /// <exception cref="ArgumentOutOfRangeException">The range is not inside <paramref name="doc"/>.</exception>
    public Span ToSpan(Doc doc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        return new Span(doc, Start, End, Key);
    }
}

/// <summary>How overlapping matches of one key are thinned (spaCy's <c>greedy</c> argument).</summary>
public enum GreedyMode
{
    /// <summary>Keep every match, overlapping ones included.</summary>
    None,

    /// <summary>Among overlapping matches keep the one that starts first, and of those the longest.</summary>
    First,

    /// <summary>Among overlapping matches keep the longest, and of equally long ones the one that starts first.</summary>
    Longest,
}

/// <summary>Called for each key that matched, as spaCy's <c>on_match</c> is.</summary>
/// <param name="matcher">The matcher that found the matches.</param>
/// <param name="doc">The document.</param>
/// <param name="index">The position in <paramref name="matches"/> of the match the callback is called for.</param>
/// <param name="matches">All matches of this call, in result order.</param>
public delegate void MatchCallback(Matcher matcher, Doc doc, int index, IReadOnlyList<PatternMatch> matches);

/// <summary>
/// Finds token sequences that satisfy token patterns, with spaCy's documented semantics: every pattern is matched at every start,
/// and every token range a pattern can cover is a match, so <c>a+</c> over <c>a a a</c> gives six matches, not one.
/// </summary>
/// <remarks>
/// <para>
/// A pattern is a <see cref="Pattern"/>: positions of attribute constraints with an operator each. Positions are compiled to a
/// small non-deterministic automaton and run without backtracking, so run time is linear in the document for a given pattern.
/// </para>
/// <para>
/// Results are sorted by end token, then start token, then the order keys were added; a key matches a range at most once however many of
/// its patterns cover it; a range is never empty. Adding patterns is not thread-safe with matching; matching is read-only and may run
/// from several threads.
/// </para>
/// <para>
/// Not supported: extension attributes (<c>_</c>), entity-ID attributes, and dependency patterns.
/// </para>
/// </remarks>
public sealed class Matcher
{
    private readonly List<Key> _keys = [];
    private readonly Dictionary<string, int> _index = new(StringComparer.Ordinal);
    private HashSet<string>? _stopWords;

    private sealed class Key(string name)
    {
        internal string Name { get; } = name;

        internal List<Compiled> Patterns { get; } = [];

        internal GreedyMode Greedy { get; set; }

        internal MatchCallback? OnMatch { get; set; }
    }

    private sealed class Compiled
    {
        internal required Constraint[][] Constraints { get; init; }

        internal required bool[] Negated { get; init; }

        // 0 = must match one token, 1 = may be skipped, 2 = may repeat or be skipped.
        internal required byte[] Kinds { get; init; }

        internal int Slots => Kinds.Length;
    }

    /// <summary>The words <c>IS_STOP</c> accepts, compared against the lower-cased token text. Empty by default: the core package ships no stop-word list.</summary>
    public IReadOnlyCollection<string> StopWords
    {
        get => _stopWords ?? (IReadOnlyCollection<string>)[];
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _stopWords = new HashSet<string>(value, StringComparer.Ordinal);
        }
    }

    /// <summary>The number of keys added.</summary>
    public int Count => _keys.Count;

    /// <summary>Whether a key has been added.</summary>
    /// <param name="key">The key.</param>
    public bool Contains(string key) => _index.ContainsKey(key);

    /// <summary>Adds one pattern under a key. Adding again under the same key adds another alternative.</summary>
    /// <param name="key">The match key, returned with each match and used as the span label; not empty.</param>
    /// <param name="pattern">The pattern.</param>
    /// <param name="greedy">How overlapping matches of this key are thinned; a value other than <see cref="GreedyMode.None"/> replaces the key's earlier setting.</param>
    /// <param name="onMatch">A callback for this key's matches, replacing an earlier one when not <see langword="null"/>.</param>
    /// <exception cref="ArgumentException">The key is empty.</exception>
    public void Add(string key, Pattern pattern, GreedyMode greedy = GreedyMode.None, MatchCallback? onMatch = null)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        Add(key, [pattern], greedy, onMatch);
    }

    /// <summary>Adds patterns under a key. A key matches when any of its patterns does.</summary>
    /// <param name="key">The match key, returned with each match and used as the span label; not empty.</param>
    /// <param name="patterns">The patterns; at least one.</param>
    /// <param name="greedy">How overlapping matches of this key are thinned; a value other than <see cref="GreedyMode.None"/> replaces the key's earlier setting.</param>
    /// <param name="onMatch">A callback for this key's matches, replacing an earlier one when not <see langword="null"/>.</param>
    /// <exception cref="ArgumentException">The key is empty or there are no patterns.</exception>
    public void Add(string key, IEnumerable<Pattern> patterns, GreedyMode greedy = GreedyMode.None, MatchCallback? onMatch = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(patterns);
        List<Compiled> compiled = [.. patterns.Select(Compile)];
        if (compiled.Count == 0)
        {
            throw new ArgumentException("Add at least one pattern.", nameof(patterns));
        }

        if (!_index.TryGetValue(key, out int at))
        {
            at = _keys.Count;
            _index[key] = at;
            _keys.Add(new Key(key));
        }

        Key k = _keys[at];
        k.Patterns.AddRange(compiled);
        if (greedy != GreedyMode.None)
        {
            k.Greedy = greedy;
        }

        if (onMatch is not null)
        {
            k.OnMatch = onMatch;
        }
    }

    /// <summary>Adds the token patterns of a JSON document under a key; see <see cref="PatternJson"/> for the format.</summary>
    /// <param name="key">The match key.</param>
    /// <param name="json">One pattern (an array of token objects) or an array of such patterns.</param>
    /// <param name="greedy">As in <see cref="Add(string, IEnumerable{Pattern}, GreedyMode, MatchCallback?)"/>.</param>
    /// <param name="onMatch">As in <see cref="Add(string, IEnumerable{Pattern}, GreedyMode, MatchCallback?)"/>.</param>
    /// <exception cref="PatternFormatException">The JSON is not a valid pattern.</exception>
    public void AddJson(string key, string json, GreedyMode greedy = GreedyMode.None, MatchCallback? onMatch = null) =>
        Add(key, PatternJson.ParsePatterns(json), greedy, onMatch);

    /// <summary>Removes a key and its patterns.</summary>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true"/> when the key existed.</returns>
    public bool Remove(string key)
    {
        if (!_index.TryGetValue(key, out int at))
        {
            return false;
        }

        _keys.RemoveAt(at);
        _index.Clear();
        for (int i = 0; i < _keys.Count; i++)
        {
            _index[_keys[i].Name] = i;
        }

        return true;
    }

    /// <summary>Finds every match in a document.</summary>
    /// <param name="doc">The document.</param>
    /// <returns>The matches sorted by end, then start, then key order. Callbacks have run before this returns.</returns>
    public IReadOnlyList<PatternMatch> Match(Doc doc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        List<PatternMatch> result = FindAll(doc);
        for (int i = 0; i < result.Count; i++)
        {
            if (_index.TryGetValue(result[i].Key, out int at) && _keys[at].OnMatch is { } callback)
            {
                callback(this, doc, i, result);
            }
        }

        return result;
    }

    /// <summary>Finds every match and returns them as spans labelled with their keys.</summary>
    /// <param name="doc">The document.</param>
    /// <returns>The spans in result order, with confidence 1.</returns>
    public IReadOnlyList<Span> MatchSpans(Doc doc)
    {
        IReadOnlyList<PatternMatch> matches = Match(doc);
        var spans = new Span[matches.Count];
        for (int i = 0; i < spans.Length; i++)
        {
            spans[i] = matches[i].ToSpan(doc);
        }

        return spans;
    }

    /// <summary>Matches several documents in order.</summary>
    /// <param name="docs">The documents.</param>
    /// <returns>One result list per document.</returns>
    public IEnumerable<IReadOnlyList<PatternMatch>> MatchMany(IEnumerable<Doc> docs)
    {
        ArgumentNullException.ThrowIfNull(docs);
        return docs.Select(Match);
    }

    private static Compiled Compile(Pattern pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        var constraints = new List<Constraint[]>();
        var negated = new List<bool>();
        var kinds = new List<byte>();
        foreach (TokenSpec spec in pattern)
        {
            Quantifier q = spec.Quantifier;
            Constraint[] cs = [.. spec.Constraints];

            // Unfold {min,max}: min required slots, then either a repeating slot (no upper bound) or max - min optional ones.
            // Optional slots in a row accept "at most k" repeats, which is all the range means.
            for (int r = 0; r < q.Min; r++)
            {
                constraints.Add(cs);
                negated.Add(q.Negated);
                kinds.Add(0);
            }

            if (q.Max == -1)
            {
                constraints.Add(cs);
                negated.Add(false);
                kinds.Add(2);
            }
            else
            {
                for (int r = q.Min; r < q.Max; r++)
                {
                    constraints.Add(cs);
                    negated.Add(false);
                    kinds.Add(1);
                }
            }
        }

        return new Compiled { Constraints = [.. constraints], Negated = [.. negated], Kinds = [.. kinds] };
    }

    private List<PatternMatch> FindAll(Doc doc)
    {
        var facts = new TokenFacts(doc, _stopWords);
        var hits = new List<(int Key, int Start, int End)>();
        int n = doc.Length;
        for (int k = 0; k < _keys.Count; k++)
        {
            foreach (Compiled p in _keys[k].Patterns)
            {
                Run(p, facts, n, k, hits);
            }
        }

        // Sort by end, start, key; then drop repeats of the same (key, start, end) that several patterns of a key produced.
        hits.Sort(static (a, b) =>
        {
            int c = a.End.CompareTo(b.End);
            if (c != 0)
            {
                return c;
            }

            c = a.Start.CompareTo(b.Start);
            return c != 0 ? c : a.Key.CompareTo(b.Key);
        });

        var perKey = new List<(int Start, int End)>[_keys.Count];
        var result = new List<PatternMatch>(hits.Count);
        (int Key, int Start, int End) previous = (-1, -1, -1);
        foreach ((int Key, int Start, int End) h in hits)
        {
            if (h == previous)
            {
                continue;
            }

            previous = h;
            if (_keys[h.Key].Greedy == GreedyMode.None)
            {
                result.Add(new PatternMatch(_keys[h.Key].Name, h.Start, h.End));
            }
            else
            {
                (perKey[h.Key] ??= []).Add((h.Start, h.End));
            }
        }

        bool anyGreedy = false;
        for (int k = 0; k < perKey.Length; k++)
        {
            if (perKey[k] is { } list)
            {
                anyGreedy = true;
                foreach ((int s, int e) in Thin(list, _keys[k].Greedy))
                {
                    result.Add(new PatternMatch(_keys[k].Name, s, e));
                }
            }
        }

        if (anyGreedy)
        {
            // Restore result order across keys: end, start, then key order.
            var order = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int k = 0; k < _keys.Count; k++)
            {
                order[_keys[k].Name] = k;
            }

            result.Sort((a, b) =>
            {
                int c = a.End.CompareTo(b.End);
                if (c != 0)
                {
                    return c;
                }

                c = a.Start.CompareTo(b.Start);
                return c != 0 ? c : order[a.Key].CompareTo(order[b.Key]);
            });
        }

        return result;
    }

    // Keeps a maximal set of non-overlapping matches, choosing by the greedy mode: First prefers the earliest start and then the
    // longer match; Longest prefers the longer match and then the earlier start.
    private static List<(int Start, int End)> Thin(List<(int Start, int End)> matches, GreedyMode mode)
    {
        var sorted = new List<(int Start, int End)>(matches);
        sorted.Sort(mode == GreedyMode.First
            ? static (a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : b.End.CompareTo(a.End)
            : static (a, b) => (a.End - a.Start) != (b.End - b.Start) ? (b.End - b.Start).CompareTo(a.End - a.Start) : a.Start.CompareTo(b.Start));
        var kept = new List<(int Start, int End)>();
        foreach ((int s, int e) in sorted)
        {
            bool overlaps = false;
            foreach ((int ks, int ke) in kept)
            {
                if (s < ke && ks < e)
                {
                    overlaps = true;
                    break;
                }
            }

            if (!overlaps)
            {
                kept.Add((s, e));
            }
        }

        return kept;
    }

    // Runs the pattern's automaton from every start token. The state set holds slot numbers; slot N (past the last) means "complete".
    private static void Run(Compiled p, TokenFacts facts, int n, int key, List<(int Key, int Start, int End)> hits)
    {
        int slots = p.Slots;
        int[] current = new int[slots + 1];
        int[] next = new int[slots + 1];
        int[] mark = new int[slots + 1];
        int stamp = 0;
        for (int start = 0; start < n; start++)
        {
            int count = 0;
            stamp++;
            AddWithClosure(0, p, current, ref count, mark, stamp);
            for (int t = start; t < n && count > 0; t++)
            {
                int nextCount = 0;
                stamp++;
                bool complete = false;
                for (int c = 0; c < count; c++)
                {
                    int s = current[c];
                    if (s >= slots || !SlotMatches(p, s, facts, t))
                    {
                        continue;
                    }

                    // A repeating slot stays where it is; the closure also lets it move on.
                    AddWithClosure(p.Kinds[s] == 2 ? s : s + 1, p, next, ref nextCount, mark, stamp);
                }

                for (int c = 0; c < nextCount; c++)
                {
                    if (next[c] == slots)
                    {
                        complete = true;
                        break;
                    }
                }

                if (complete)
                {
                    hits.Add((key, start, t + 1));
                }

                (current, next) = (next, current);
                count = nextCount;
            }
        }
    }

    private static void AddWithClosure(int slot, Compiled p, int[] list, ref int count, int[] mark, int stamp)
    {
        // From a slot that may be skipped, the next slot is reachable without consuming a token.
        while (mark[slot] != stamp)
        {
            mark[slot] = stamp;
            list[count++] = slot;
            if (slot < p.Slots && p.Kinds[slot] != 0)
            {
                slot++;
            }
            else
            {
                break;
            }
        }
    }

    private static bool SlotMatches(Compiled p, int slot, TokenFacts facts, int token)
    {
        bool all = true;
        foreach (Constraint c in p.Constraints[slot])
        {
            if (!facts.Satisfies(c, token))
            {
                all = false;
                break;
            }
        }

        return all != p.Negated[slot];
    }
}
