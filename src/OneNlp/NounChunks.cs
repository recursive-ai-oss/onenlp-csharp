namespace RecursiveAI.OneNlp;

/// <summary>
/// Base noun phrases found from a dependency parse: a noun, proper noun or pronoun that serves as subject, object, oblique,
/// apposition, predicate nominal or root, together with the words that lie before it in its own phrase. A base noun phrase
/// does not contain another one, so there is no coordination at phrase level, no prepositional phrase and no relative clause.
/// </summary>
/// <remarks>
/// The rule follows the behaviour of spaCy's published <c>Doc.noun_chunks</c> as observed in its output on crafted and random trees
/// (spaCy 3.8.18): tokens are visited in order; a token qualifies when its part of speech is NOUN, PROPN or PRON and its relation is
/// one of the head relations, or is <c>conj</c> in a chain of <c>conj</c> that ends in a head relation; the chunk runs from the left
/// edge of the token's subtree to the token; a token whose left edge falls inside the previous chunk is skipped. Where UD
/// differs from the scheme spaCy's English rule was written for, the <see cref="NounChunkProfile.UniversalDependencies"/> profile
/// adapts it: it keeps case markers, conjunctions and clauses out of a phrase, and extends the phrase over names written head-first.
/// </remarks>
internal static class NounChunker
{
    public static List<Span> Find(Doc doc, NounChunkProfile profile)
    {
        int n = doc.Length;
        var chunks = new List<Span>();
        if (n == 0)
        {
            return chunks;
        }

        ReadOnlySpan<int> heads = doc.GetHeads();
        int[] dep = doc.GetColumn(TokenColumn.Dep).ToArray();
        int[] pos = doc.GetColumn(TokenColumn.Pos).ToArray();
        var kind = new byte[n]; // 0 other, 1 head relation, 2 conj
        var cut = new bool[n]; // the edge from this token to its head does not extend the head's phrase
        var extends = new bool[n]; // the token is written after the phrase head and belongs to it
        bool anyDep = false;
        for (int i = 0; i < n; i++)
        {
            if (dep[i] == 0)
            {
                continue;
            }

            anyDep = true;
            string label = doc.Strings[dep[i]];
            kind[i] = profile.IsHeadRelation(label) ? (byte)1 : label == "conj" ? (byte)2 : (byte)0;
            cut[i] = profile.IsCut(label);
            extends[i] = profile.ExtendsRight(label);
        }

        if (!anyDep)
        {
            throw new InvalidOperationException("The document has no dependency relations; run a parser before asking for noun chunks.");
        }

        // Left edge: the smallest index in the token's subtree, found by sweeping until nothing changes.
        // (Observed in the output of spaCy 3.8.18: a head's edge is the minimum over all its descendants.)
        var left = new int[n];
        var right = new int[n];
        for (int i = 0; i < n; i++)
        {
            left[i] = i;
            right[i] = i;
        }

        for (bool changed = true; changed;)
        {
            changed = false;
            for (int i = 0; i < n; i++)
            {
                int h = heads[i];
                if (h == i || cut[i])
                {
                    continue;
                }

                if (left[i] < left[h])
                {
                    left[h] = left[i];
                    changed = true;
                }
            }
        }

        // The right end of a phrase head: its own position, or the end of a name or word group written after it.
        for (bool changed = true; changed;)
        {
            changed = false;
            for (int i = 0; i < n; i++)
            {
                int h = heads[i];
                if (h != i && extends[i] && right[i] > right[h])
                {
                    right[h] = right[i];
                    changed = true;
                }
            }
        }

        int previousEnd = -1;
        for (int i = 0; i < n; i++)
        {
            string p = pos[i] == 0 ? string.Empty : doc.Strings[pos[i]];
            if (p is not ("NOUN" or "PROPN" or "PRON"))
            {
                continue;
            }

            bool qualifies = kind[i] == 1;
            if (kind[i] == 2)
            {
                int h = heads[i];
                for (int guard = 0; kind[h] == 2 && heads[h] < h && guard < n; guard++)
                {
                    h = heads[h];
                }

                qualifies = kind[h] == 1;
            }

            if (!qualifies || left[i] <= previousEnd)
            {
                continue;
            }

            chunks.Add(new Span(doc, left[i], right[i] + 1));
            previousEnd = right[i];
        }

        return chunks;
    }
}

/// <summary>Which dependency relations the noun-chunk rule treats as phrase heads, as cuts, and as right extensions.</summary>
internal sealed class NounChunkProfile
{
    private readonly HashSet<string> _headRelations;
    private readonly HashSet<string> _cuts;
    private readonly HashSet<string> _rightExtensions;
    private readonly bool _subtypes;

    private NounChunkProfile(IEnumerable<string> heads, IEnumerable<string> cuts, IEnumerable<string> right, bool subtypes)
    {
        _headRelations = new HashSet<string>(heads, StringComparer.Ordinal);
        _cuts = new HashSet<string>(cuts, StringComparer.Ordinal);
        _rightExtensions = new HashSet<string>(right, StringComparer.Ordinal);
        _subtypes = subtypes;
    }

    /// <summary>The relation names of the scheme spaCy's English rule was written for, with no cuts: used to compare against spaCy.</summary>
    public static NounChunkProfile ClearStyle { get; } = new(
        ["ROOT", "nsubj", "nsubjpass", "dobj", "dative", "pobj", "appos", "attr", "pcomp", "oprd"], [], [], subtypes: false);

    /// <summary>Universal Dependencies relations.</summary>
    public static NounChunkProfile UniversalDependencies { get; } = new(
        ["root", "ROOT", "nsubj", "obj", "iobj", "obl", "nmod", "appos", "xcomp"],
        ["case", "cc", "mark", "punct", "cop", "aux", "advcl", "acl", "ccomp", "csubj", "parataxis", "vocative", "discourse", "dislocated", "expl", "list", "orphan", "reparandum", "dep"],
        ["flat", "goeswith", "fixed"],
        subtypes: true);

    public bool IsHeadRelation(string label)
    {
        if (!_subtypes)
        {
            return _headRelations.Contains(label);
        }

        (string baseName, string subtype) = Split(label);
        // Temporal and adverbial-noun obliques and possessors are modifiers, not arguments.
        return _headRelations.Contains(baseName) && subtype is not ("tmod" or "npmod" or "poss");
    }

    public bool IsCut(string label) => _cuts.Contains(_subtypes ? Split(label).Base : label);

    public bool ExtendsRight(string label) => _rightExtensions.Contains(_subtypes ? Split(label).Base : label);

    private static (string Base, string Subtype) Split(string label)
    {
        int colon = label.IndexOf(':', StringComparison.Ordinal);
        return colon < 0 ? (label, string.Empty) : (label[..colon], label[(colon + 1)..]);
    }
}

public sealed partial class Doc
{
    /// <summary>
    /// The base noun phrases of the document, in text order. They never overlap and each is a contiguous span (no label, confidence 1).
    /// A phrase is a noun, proper noun or pronoun serving as subject, object, oblique, apposition, predicate nominal or root, or
    /// conjoined with one, plus the words before it that belong to it (determiners, adjectives, numbers, compounds, possessors) and
    /// the rest of a name written head-first (<c>flat</c>).
    /// </summary>
    /// <remarks>Computed on each access from the heads, relations and parts of speech; the result is not cached.</remarks>
    /// <exception cref="InvalidOperationException">No token has a dependency relation: the document has not been parsed.</exception>
    public IReadOnlyList<Span> NounChunks => NounChunker.Find(this, NounChunkProfile.UniversalDependencies);
}
