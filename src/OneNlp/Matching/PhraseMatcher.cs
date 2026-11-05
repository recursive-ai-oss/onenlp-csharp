namespace RecursiveAI.OneNlp.Matching;

/// <summary>
/// Finds occurrences of many phrases at once, comparing one token attribute (the verbatim text by default), as spaCy's
/// <c>PhraseMatcher</c> does. Phrases are kept in a trie keyed by token values, so the cost of matching depends on the document and the
/// longest phrase, not on how many phrases were added.
/// </summary>
/// <remarks>
/// Every occurrence is reported, overlapping and nested ones included, in order of end token, then start token, then key order. A phrase
/// added under two keys is reported once for each. Adding is not thread-safe with matching; matching is read-only and may run from
/// several threads.
/// </remarks>
public sealed class PhraseMatcher
{
    private readonly TokenProperty _attribute;
    private readonly IWordTokenizer _tokenizer;
    private readonly StringStore _strings = new();
    private readonly List<string> _keys = [];
    private readonly Dictionary<string, int> _keyIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _vocabulary = new(StringComparer.Ordinal);
    private readonly Dictionary<long, int> _children = [];
    private readonly Dictionary<int, int[]> _terminals = [];
    private int _nodeCount = 1;
    private int _maxDepth;

    /// <summary>Creates an empty matcher.</summary>
    /// <param name="attribute">The token attribute that phrases and documents are compared on: Orth, Lower, Norm, Shape, Lemma, Pos, Tag, Dep or Morph. Lemma, Pos, Tag, Dep and Morph need annotated phrase documents.</param>
    /// <param name="tokenizer">Splits phrases added as strings; the rule word tokenizer when <see langword="null"/>. Use the tokenizer that produced the documents you will match so both sides agree on token boundaries.</param>
    /// <exception cref="ArgumentException">The attribute cannot be compared as text (for example a flag).</exception>
    public PhraseMatcher(TokenProperty attribute = TokenProperty.Orth, IWordTokenizer? tokenizer = null)
    {
        if (!TokenProperties.IsString(attribute) || attribute is TokenProperty.EntType or TokenProperty.EntIob)
        {
            throw new ArgumentException($"A phrase matcher compares text attributes; {TokenProperties.NameOf(attribute)} is not supported.", nameof(attribute));
        }

        _attribute = attribute;
        _tokenizer = tokenizer ?? new RuleWordTokenizer();
    }

    /// <summary>The attribute phrases are compared on.</summary>
    public TokenProperty Attribute => _attribute;

    /// <summary>The number of keys added.</summary>
    public int Count => _keys.Count;

    /// <summary>Adds phrases, given as text, under a key. Each is split into tokens by the matcher's tokenizer.</summary>
    /// <param name="key">The match key, returned with each match and used as the span label; not empty.</param>
    /// <param name="phrases">The phrases. A phrase that has no tokens is rejected. Attributes other than Orth, Lower, Norm and Shape need documents: use the <see cref="Doc"/> overload.</param>
    /// <exception cref="ArgumentException">The key is empty, a phrase has no tokens, or the attribute needs annotated documents.</exception>
    public void Add(string key, IEnumerable<string> phrases)
    {
        ArgumentNullException.ThrowIfNull(phrases);
        Add(key, phrases.Select(p => _tokenizer.Tokenize(p ?? throw new ArgumentException("A phrase is null.", nameof(phrases)), _strings)));
    }

    // True when the matcher's tokenizer finds at least one token in the text. Rules read from files may hold text that is only
    // white space; the entity ruler leaves those out (they could never match) instead of failing when it first runs.
    internal bool HasTokens(string phrase) => _tokenizer.Tokenize(phrase, _strings).Length > 0;

    /// <summary>Adds phrase documents under a key.</summary>
    /// <param name="key">The match key, returned with each match and used as the span label; not empty.</param>
    /// <param name="phrases">The phrases as documents; for Lemma, Pos, Tag, Dep and Morph they must carry that annotation.</param>
    /// <exception cref="ArgumentException">The key is empty, a phrase has no tokens, or a token lacks the attribute value.</exception>
    public void Add(string key, IEnumerable<Doc> phrases)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(phrases);
        List<Doc> docs = [.. phrases];
        List<int[]> prepared = [];
        foreach (Doc phrase in docs)
        {
            ArgumentNullException.ThrowIfNull(phrase);
            if (phrase.Length == 0)
            {
                throw new ArgumentException("A phrase needs at least one token.", nameof(phrases));
            }

            var facts = new TokenFacts(phrase, null);
            var ids = new int[phrase.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                string value = facts.Value(_attribute, i).ToString();
                if (value.Length == 0 && _attribute is not (TokenProperty.Orth or TokenProperty.Lower or TokenProperty.Norm or TokenProperty.Shape))
                {
                    throw new ArgumentException($"Phrase token {i} has no {TokenProperties.NameOf(_attribute)} value; annotate the phrase document first.", nameof(phrases));
                }

                ids[i] = Intern(value);
            }

            prepared.Add(ids);
        }

        if (!_keyIds.TryGetValue(key, out int keyId))
        {
            keyId = _keys.Count;
            _keyIds[key] = keyId;
            _keys.Add(key);
        }

        foreach (int[] ids in prepared)
        {
            int node = 0;
            foreach (int id in ids)
            {
                long edge = ((long)node << 32) | (uint)id;
                if (!_children.TryGetValue(edge, out int child))
                {
                    child = _nodeCount++;
                    _children[edge] = child;
                }

                node = child;
            }

            _maxDepth = Math.Max(_maxDepth, ids.Length);
            if (!_terminals.TryGetValue(node, out int[]? keys))
            {
                _terminals[node] = [keyId];
            }
            else if (Array.IndexOf(keys, keyId) < 0)
            {
                _terminals[node] = [.. keys, keyId];
            }
        }
    }

    /// <summary>Finds every occurrence of every phrase in a document.</summary>
    /// <param name="doc">The document. For Lemma, Pos, Tag, Dep and Morph it must be annotated.</param>
    /// <returns>The matches sorted by end, then start, then key order.</returns>
    public IReadOnlyList<PatternMatch> Match(Doc doc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        int n = doc.Length;
        TokenFacts? facts = _attribute is TokenProperty.Orth or TokenProperty.Lower ? null : new TokenFacts(doc, null);
        int[] tokenIds = new int[n];
        for (int i = 0; i < n; i++)
        {
            tokenIds[i] = Lookup(doc, facts, i);
        }

        var hits = new List<(int Key, int Start, int End)>();
        for (int start = 0; start < n; start++)
        {
            int node = 0;
            int last = Math.Min(n, start + _maxDepth);
            for (int t = start; t < last; t++)
            {
                int id = tokenIds[t];
                if (id < 0 || !_children.TryGetValue(((long)node << 32) | (uint)id, out node))
                {
                    break;
                }

                if (_terminals.TryGetValue(node, out int[]? keys))
                {
                    foreach (int k in keys)
                    {
                        hits.Add((k, start, t + 1));
                    }
                }
            }
        }

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
        var result = new PatternMatch[hits.Count];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = new PatternMatch(_keys[hits[i].Key], hits[i].Start, hits[i].End);
        }

        return result;
    }

    /// <summary>Finds every occurrence and returns them as spans labelled with their keys.</summary>
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

    private int Intern(string value)
    {
        if (!_vocabulary.TryGetValue(value, out int id))
        {
            id = _vocabulary.Count;
            _vocabulary[value] = id;
        }

        return id;
    }

    private int Lookup(Doc doc, TokenFacts? facts, int i)
    {
        var lookup = _vocabulary.GetAlternateLookup<ReadOnlySpan<char>>();
        ReadOnlySpan<char> text = doc[i].Text;
        if (_attribute == TokenProperty.Orth)
        {
            return lookup.TryGetValue(text, out int id) ? id : -1;
        }

        if (_attribute == TokenProperty.Lower)
        {
            return LookupLower(lookup, text);
        }

        return lookup.TryGetValue(facts!.Value(_attribute, i), out int other) ? other : -1;
    }

    private static int LookupLower(Dictionary<string, int>.AlternateLookup<ReadOnlySpan<char>> lookup, ReadOnlySpan<char> text)
    {
        // Lower-casing keeps the length (simple case mapping), so a token of up to 128 units is folded into a fixed stack buffer.
        if (text.Length <= 128)
        {
            Span<char> buffer = stackalloc char[128];
            int n = text.ToLowerInvariant(buffer);
            return lookup.TryGetValue(buffer[..n], out int id) ? id : -1;
        }

        return lookup.TryGetValue(text.ToString().ToLowerInvariant(), out int folded) ? folded : -1;
    }
}
