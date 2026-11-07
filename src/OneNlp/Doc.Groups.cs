using System.Collections.ObjectModel;

namespace RecursiveAI.OneNlp;

public sealed partial class Doc
{
    private string[]? _catLabels;
    private float[] _catScores = [];
    private IReadOnlyDictionary<string, float>? _catsView;
    private List<KeyValuePair<string, EntityRecord[]>>? _groups;
    private IReadOnlyDictionary<string, IReadOnlyList<Span>>? _groupsView;

    /// <summary>
    /// The text category scores a classifier wrote (spaCy's <c>doc.cats</c>): label to a score from 0 to 1, in the order the component
    /// wrote them. Empty until <see cref="SetCats"/> runs. Multi-class scores sum to 1; multi-label scores are independent.
    /// </summary>
    public IReadOnlyDictionary<string, float> Cats => _catsView ??= BuildCats();

    /// <summary>Whether <see cref="SetCats"/> has run, which is how "no categories scored" differs from "not run".</summary>
    public bool HasCats => _catLabels is not null;

    /// <summary>
    /// The span groups (spaCy's <c>doc.spans</c>): group name to spans. Unlike <see cref="Ents"/>, the spans of a group may overlap and
    /// may nest; they are in the order they were set. Empty until <see cref="SetSpans"/> runs.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<Span>> Spans => _groupsView ??= BuildGroups();

    /// <summary>Replaces the category scores.</summary>
    /// <param name="cats">Label and score pairs; the labels are distinct and non-empty and each score is from 0 to 1.</param>
    /// <exception cref="ArgumentException">A label is empty or repeated, or a score is not in [0, 1].</exception>
    public void SetCats(ReadOnlySpan<KeyValuePair<string, float>> cats)
    {
        var labels = new string[cats.Length];
        var scores = new float[cats.Length];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < cats.Length; i++)
        {
            if (string.IsNullOrEmpty(cats[i].Key) || !seen.Add(cats[i].Key) || !(cats[i].Value >= 0f && cats[i].Value <= 1f))
            {
                throw new ArgumentException($"Category {i} has an empty or repeated label, or a score outside [0, 1].", nameof(cats));
            }

            labels[i] = cats[i].Key;
            scores[i] = cats[i].Value;
        }

        _catLabels = labels;
        _catScores = scores;
        _catsView = null;
    }

    /// <summary>Replaces one span group, creating it when it does not exist. Spans may overlap.</summary>
    /// <param name="key">The group name, such as <c>sc</c>, spaCy's default for a span categorizer.</param>
    /// <param name="spans">The spans as token ranges, each labelled, in the order to keep.</param>
    /// <exception cref="ArgumentException">The key is empty, or a span is empty, outside the document, unlabelled or has a confidence outside [0, 1].</exception>
    public void SetSpans(string key, ReadOnlySpan<SpanSpec> spans)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        var records = new EntityRecord[spans.Length];
        for (int i = 0; i < spans.Length; i++)
        {
            SpanSpec s = spans[i];
            if (s.Start < 0 || s.End <= s.Start || s.End > Length || string.IsNullOrEmpty(s.Label) || !(s.Confidence >= 0f && s.Confidence <= 1f))
            {
                throw new ArgumentException($"Span {i} is empty, outside the document, unlabelled or has a confidence outside [0, 1].", nameof(spans));
            }

            records[i] = new EntityRecord(s.Start, s.End, Strings.Add(s.Label), s.Confidence);
        }

        _groups ??= [];
        int at = _groups.FindIndex(g => string.Equals(g.Key, key, StringComparison.Ordinal));
        var entry = new KeyValuePair<string, EntityRecord[]>(key, records);
        if (at >= 0)
        {
            _groups[at] = entry;
        }
        else
        {
            _groups.Add(entry);
        }

        _groupsView = null;
    }

    internal ReadOnlySpan<float> CatScores => _catScores;

    internal string[]? CatLabels => _catLabels;

    internal IReadOnlyList<KeyValuePair<string, SpanSpec[]>> GroupSpecs()
    {
        var result = new List<KeyValuePair<string, SpanSpec[]>>(_groups?.Count ?? 0);
        if (_groups is null)
        {
            return result;
        }

        foreach (KeyValuePair<string, EntityRecord[]> g in _groups)
        {
            var specs = new SpanSpec[g.Value.Length];
            for (int i = 0; i < specs.Length; i++)
            {
                EntityRecord r = g.Value[i];
                specs[i] = new SpanSpec(r.Start, r.End, Strings[r.Label], r.Confidence);
            }

            result.Add(new(g.Key, specs));
        }

        return result;
    }

    private ReadOnlyDictionary<string, float> BuildCats()
    {
        var map = new Dictionary<string, float>(StringComparer.Ordinal);
        if (_catLabels is not null)
        {
            for (int i = 0; i < _catLabels.Length; i++)
            {
                map[_catLabels[i]] = _catScores[i];
            }
        }

        return new ReadOnlyDictionary<string, float>(map);
    }

    private ReadOnlyDictionary<string, IReadOnlyList<Span>> BuildGroups()
    {
        var map = new Dictionary<string, IReadOnlyList<Span>>(StringComparer.Ordinal);
        if (_groups is not null)
        {
            foreach (KeyValuePair<string, EntityRecord[]> g in _groups)
            {
                var spans = new Span[g.Value.Length];
                for (int i = 0; i < spans.Length; i++)
                {
                    EntityRecord r = g.Value[i];
                    spans[i] = new Span(this, r.Start, r.End, r.Label, r.Confidence);
                }

                map[g.Key] = spans;
            }
        }

        return new ReadOnlyDictionary<string, IReadOnlyList<Span>>(map);
    }
}
