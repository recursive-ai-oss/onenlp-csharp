namespace RecursiveAI.OneNlp.Scoring;

/// <summary>A labelled half-open range, for span scoring. The range may be in tokens or characters; the scorer only compares it.</summary>
/// <param name="Start">The first position.</param>
/// <param name="End">The position just past the last.</param>
/// <param name="Label">The label.</param>
public readonly record struct LabeledSpan(int Start, int End, string Label);

/// <summary>The result of exact-match span scoring: micro counts, counts per label and counts per document.</summary>
public sealed class SpanEvaluation
{
    internal SpanEvaluation(IReadOnlyDictionary<string, MetricCounts> perLabel, IReadOnlyList<MetricCounts> perDocument)
    {
        PerLabel = perLabel;
        PerDocument = perDocument;
        MetricCounts sum = default;
        foreach (MetricCounts c in perLabel.Values)
        {
            sum += c;
        }

        Micro = sum;
    }

    /// <summary>Counts summed over every label: the micro-averaged score.</summary>
    public MetricCounts Micro { get; }

    /// <summary>Counts per label, in label order (ordinal).</summary>
    public IReadOnlyDictionary<string, MetricCounts> PerLabel { get; }

    /// <summary>Micro counts of each document or sentence, in input order: the resampling units of a bootstrap.</summary>
    public IReadOnlyList<MetricCounts> PerDocument { get; }

    /// <summary>The unweighted mean of the per-label F1 scores, over the labels that occur in the gold or the prediction.</summary>
    public double MacroF1 => PerLabel.Count == 0 ? 0 : PerLabel.Values.Average(c => c.F1);
}

/// <summary>
/// Entity and span scoring: a predicted span is correct only when its start, end and label all equal a gold span's (the CoNLL-2003
/// span-level definition, Tjong Kim Sang and De Meulder 2003, as the <c>conlleval</c> and <c>seqeval</c> tools compute it).
/// </summary>
public static class SpanScorer
{
    /// <summary>Scores span lists, one list per document or sentence.</summary>
    /// <param name="gold">The gold spans of each document.</param>
    /// <param name="predicted">The predicted spans of each document; the same number of documents.</param>
    /// <returns>The counts.</returns>
    /// <exception cref="ArgumentException">The two lists differ in length.</exception>
    public static SpanEvaluation Score(IReadOnlyList<IReadOnlyCollection<LabeledSpan>> gold, IReadOnlyList<IReadOnlyCollection<LabeledSpan>> predicted)
    {
        ArgumentNullException.ThrowIfNull(gold);
        ArgumentNullException.ThrowIfNull(predicted);
        if (gold.Count != predicted.Count)
        {
            throw new ArgumentException("Gold and predicted lists differ in length.", nameof(predicted));
        }

        var perLabel = new SortedDictionary<string, MetricCounts>(StringComparer.Ordinal);
        var perDocument = new MetricCounts[gold.Count];
        for (int d = 0; d < gold.Count; d++)
        {
            var goldSet = new HashSet<LabeledSpan>(gold[d]);
            var predictedSet = new HashSet<LabeledSpan>(predicted[d]);
            foreach (LabeledSpan g in goldSet)
            {
                bool hit = predictedSet.Contains(g);
                Bump(perLabel, g.Label, new MetricCounts(1, 0, hit ? 1 : 0, hit ? 1 : 0));
                perDocument[d] += new MetricCounts(1, 0, hit ? 1 : 0, hit ? 1 : 0);
            }

            foreach (LabeledSpan p in predictedSet)
            {
                Bump(perLabel, p.Label, new MetricCounts(0, 1, 0));
                perDocument[d] += new MetricCounts(0, 1, 0);
            }
        }

        return new SpanEvaluation(perLabel, perDocument);
    }

    /// <summary>
    /// Scores IOB2 tag sequences with strict span extraction: a span is a <c>B-X</c> followed by any number of <c>I-X</c>; an
    /// <c>I-X</c> that does not continue such a span is ignored, as is any other tag (<c>O</c>).
    /// </summary>
    /// <param name="gold">The gold tags of each sentence.</param>
    /// <param name="predicted">The predicted tags of each sentence.</param>
    /// <returns>The counts.</returns>
    /// <exception cref="ArgumentException">The two lists differ in length.</exception>
    public static SpanEvaluation ScoreIob2(IReadOnlyList<IReadOnlyList<string>> gold, IReadOnlyList<IReadOnlyList<string>> predicted)
    {
        ArgumentNullException.ThrowIfNull(gold);
        ArgumentNullException.ThrowIfNull(predicted);
        return Score(
            gold.Select(t => (IReadOnlyCollection<LabeledSpan>)ExtractIob2(t)).ToList(),
            predicted.Select(t => (IReadOnlyCollection<LabeledSpan>)ExtractIob2(t)).ToList());
    }

    /// <summary>Scores the entities of documents (<see cref="Doc.Ents"/>, token ranges) against gold documents.</summary>
    /// <param name="predicted">The system output, one document per gold document, with the same tokens.</param>
    /// <param name="gold">The gold documents.</param>
    /// <returns>The counts.</returns>
    /// <exception cref="ArgumentException">The two lists differ in length.</exception>
    public static SpanEvaluation Score(IReadOnlyList<Doc> predicted, IReadOnlyList<Doc> gold)
    {
        ArgumentNullException.ThrowIfNull(predicted);
        ArgumentNullException.ThrowIfNull(gold);
        return Score(
            gold.Select(d => (IReadOnlyCollection<LabeledSpan>)Entities(d)).ToList(),
            predicted.Select(d => (IReadOnlyCollection<LabeledSpan>)Entities(d)).ToList());
    }

    /// <summary>The entity F1 of documents as a named score in points (<c>ents_f</c>), for use as a training loop's development score.</summary>
    /// <param name="predicted">The system output.</param>
    /// <param name="gold">The gold documents.</param>
    /// <returns>The micro F1 in points.</returns>
    public static NamedScore EntityScore(IReadOnlyList<Doc> predicted, IReadOnlyList<Doc> gold) =>
        new("ents_f", 100 * Score(predicted, gold).Micro.F1);

    /// <summary>Extracts the spans of an IOB2 tag sequence (see <see cref="ScoreIob2"/>); positions are tag indexes.</summary>
    /// <param name="tags">The tags, such as <c>B-PER</c>, <c>I-PER</c>, <c>O</c>.</param>
    /// <returns>The spans in order.</returns>
    public static List<LabeledSpan> ExtractIob2(IReadOnlyList<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        var spans = new List<LabeledSpan>();
        int i = 0;
        while (i < tags.Count)
        {
            string tag = tags[i];
            if (tag.Length > 2 && tag[0] == 'B' && tag[1] == '-')
            {
                string label = tag[2..];
                int end = i + 1;
                while (end < tags.Count && tags[end].Length == label.Length + 2 && tags[end][0] == 'I' && tags[end][1] == '-'
                    && tags[end].AsSpan(2).SequenceEqual(label))
                {
                    end++;
                }

                spans.Add(new LabeledSpan(i, end, label));
                i = end;
            }
            else
            {
                i++;
            }
        }

        return spans;
    }

    private static List<LabeledSpan> Entities(Doc doc)
    {
        var list = new List<LabeledSpan>();
        foreach (Span e in doc.Ents)
        {
            list.Add(new LabeledSpan(e.Start, e.End, e.Label));
        }

        return list;
    }

    private static void Bump(SortedDictionary<string, MetricCounts> map, string label, MetricCounts add) =>
        map[label] = map.TryGetValue(label, out MetricCounts current) ? current + add : add;
}
