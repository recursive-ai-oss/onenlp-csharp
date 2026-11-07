using System.Text.Json;

namespace RecursiveAI.OneNlp.Neural.Components;

/// <summary>
/// A table of lemmas for (word form, part of speech) pairs seen in training, which an <see cref="EditTreeLemmatizer"/> consults before its
/// edit trees. A treebank lemmatizes irregular and rare words in ways a handful of edit trees cannot reach; where the same form and part of
/// speech had the same lemma nearly every time in the training data, the table gives that lemma. Pairs the table does not hold fall back to the trees.
/// </summary>
/// <remarks>
/// Keys are the lower-cased form (invariant culture) and the part-of-speech tag; a form that was followed by two different lemmas keeps the more
/// frequent one only if its share is at least <c>minShare</c>. The lookup is exact: no stemming, no guessing.
/// </remarks>
public sealed class LemmaLexicon
{
    private const int MaxEntries = 1 << 21;
    private const int MaxLength = 256;
    private readonly Dictionary<(string Form, string Pos), string> _entries;

    private LemmaLexicon(Dictionary<(string Form, string Pos), string> entries) => _entries = entries;

    /// <summary>The number of entries.</summary>
    public int Count => _entries.Count;

    /// <summary>Builds the table from reference documents (tokens with a lemma and a part of speech).</summary>
    /// <param name="references">The training documents' reference annotations.</param>
    /// <param name="minCount">Pairs seen fewer times than this are left out. Default 1.</param>
    /// <param name="minShare">The share of a pair's occurrences its most frequent lemma needs, from just over 0 up to 1. Default 0.9.</param>
    /// <returns>The table.</returns>
    public static LemmaLexicon Build(IEnumerable<Doc> references, int minCount = 1, double minShare = 0.9)
    {
        ArgumentNullException.ThrowIfNull(references);
        ArgumentOutOfRangeException.ThrowIfLessThan(minCount, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minShare, 1.0);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minShare);
        var counts = new Dictionary<(string, string), Dictionary<string, int>>();
        foreach (Doc doc in references)
        {
            for (int t = 0; t < doc.Length; t++)
            {
                Token token = doc[t];
                string lemma = token.Lemma;
                string pos = token.Pos;
                if (lemma.Length == 0 || pos.Length == 0 || lemma.Length > MaxLength)
                {
                    continue;
                }

                string form = (token.Norm.Length > 0 ? token.Norm : token.Text.ToString()).ToLowerInvariant();
                if (form.Length == 0 || form.Length > MaxLength)
                {
                    continue;
                }

                if (!counts.TryGetValue((form, pos), out Dictionary<string, int>? byLemma))
                {
                    byLemma = new Dictionary<string, int>(StringComparer.Ordinal);
                    counts[(form, pos)] = byLemma;
                }

                byLemma[lemma] = byLemma.GetValueOrDefault(lemma) + 1;
            }
        }

        var entries = new Dictionary<(string, string), string>();
        foreach (((string form, string pos), Dictionary<string, int> byLemma) in counts)
        {
            int total = 0;
            int best = 0;
            string bestLemma = string.Empty;
            foreach ((string lemma, int n) in byLemma)
            {
                total += n;
                if (n > best || (n == best && string.CompareOrdinal(lemma, bestLemma) < 0))
                {
                    best = n;
                    bestLemma = lemma;
                }
            }

            if (total >= minCount && best >= minShare * total)
            {
                entries[(form, pos)] = bestLemma;
            }
        }

        return new LemmaLexicon(entries);
    }

    /// <summary>Looks a pair up.</summary>
    /// <param name="lowerForm">The word form, already lower-cased with the invariant culture.</param>
    /// <param name="pos">The part-of-speech tag.</param>
    /// <param name="lemma">The lemma when found.</param>
    /// <returns>Whether the pair is in the table.</returns>
    public bool TryGet(string lowerForm, string pos, out string lemma)
    {
        if (_entries.TryGetValue((lowerForm, pos), out string? found))
        {
            lemma = found;
            return true;
        }

        lemma = string.Empty;
        return false;
    }

    /// <summary>Writes the table as a JSON array of strings, three for each entry (form, part of speech, lemma), in a fixed order.</summary>
    /// <param name="writer">The writer; the array is written as the value of the current property.</param>
    public void Write(Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStartArray();
        foreach (((string form, string pos), string lemma) in _entries.OrderBy(e => e.Key.Form, StringComparer.Ordinal).ThenBy(e => e.Key.Pos, StringComparer.Ordinal))
        {
            writer.WriteStringValue(form);
            writer.WriteStringValue(pos);
            writer.WriteStringValue(lemma);
        }

        writer.WriteEndArray();
    }

    // GetString throws InvalidOperationException on a string whose bytes are not valid UTF-8; the table reports that as bad data.
    private static bool TryGetText(JsonElement element, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? text)
    {
        try
        {
            text = element.GetString();
            return text is not null;
        }
        catch (InvalidOperationException)
        {
            text = null;
            return false;
        }
    }

    /// <summary>Reads a table written by <see cref="Write"/>.</summary>
    /// <param name="array">A JSON array of strings whose length is a multiple of three, at most 6,291,456.</param>
    /// <returns>The table.</returns>
    /// <exception cref="InvalidDataException">The array is malformed or too large.</exception>
    public static LemmaLexicon Read(JsonElement array)
    {
        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() % 3 != 0 || array.GetArrayLength() > 3 * MaxEntries)
        {
            throw new InvalidDataException("A lemma lexicon must be an array of strings in threes (form, part of speech, lemma).");
        }

        var entries = new Dictionary<(string, string), string>();
        string?[] triple = new string?[3];
        int at = 0;
        foreach (JsonElement e in array.EnumerateArray())
        {
            if (e.ValueKind != JsonValueKind.String || !TryGetText(e, out string? text) || text.Length > MaxLength)
            {
                throw new InvalidDataException("A lemma lexicon holds only strings of at most 256 characters.");
            }

            triple[at++] = text;
            if (at == 3)
            {
                entries[(triple[0]!, triple[1]!)] = triple[2]!;
                at = 0;
            }
        }

        return new LemmaLexicon(entries);
    }
}
