using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Scoring;

namespace RecursiveAI.OneNlp.Neural.Training;

/// <summary>
/// Ready-made development scores for <see cref="TrainOptions.Score"/>, built on the scorers in <c>RecursiveAI.OneNlp.Scoring</c>.
/// Each returns the score in points (0 to 100), higher being better.
/// </summary>
public static class DevScores
{
    /// <summary>A Universal Dependencies metric (<see cref="UdEvaluation.MetricNames"/>, such as <c>UPOS</c>, <c>XPOS</c>, <c>UFeats</c>, <c>Lemmas</c>, <c>UAS</c> or <c>LAS</c>) of the predicted documents against the references.</summary>
    /// <param name="metric">The metric name. Default <c>LAS</c>.</param>
    /// <returns>A function to give to <see cref="TrainOptions.Score"/>.</returns>
    /// <exception cref="ArgumentException">The metric is unknown.</exception>
    public static Func<IReadOnlyList<Example>, double> Ud(string metric = "LAS")
    {
        ArgumentNullException.ThrowIfNull(metric);
        if (!UdEvaluation.MetricNames.Contains(metric))
        {
            throw new ArgumentException($"Unknown metric '{metric}'.", nameof(metric));
        }

        return examples => UdScorer.Score(examples.Select(e => e.Predicted), examples.Select(e => e.Reference), metric).Value;
    }

    /// <summary>The micro F1 of predicted entities against the reference entities (<see cref="SpanScorer.EntityScore"/>).</summary>
    /// <returns>A function to give to <see cref="TrainOptions.Score"/>.</returns>
    public static Func<IReadOnlyList<Example>, double> Entities() =>
        examples => SpanScorer.EntityScore([.. examples.Select(e => e.Predicted)], [.. examples.Select(e => e.Reference)]).Value;

    /// <summary>
    /// The share of documents whose highest-scoring predicted category is the gold category (the one with the highest gold score), in
    /// points. Documents without gold cats are left out.
    /// </summary>
    /// <returns>A function to give to <see cref="TrainOptions.Score"/>.</returns>
    public static Func<IReadOnlyList<Example>, double> Accuracy() => examples =>
    {
        int right = 0;
        int total = 0;
        foreach (Example e in examples)
        {
            string? gold = TopLabel(e.Cats);
            if (gold is null)
            {
                continue;
            }

            total++;
            if (string.Equals(TopLabel(e.Predicted.Cats), gold, StringComparison.Ordinal))
            {
                right++;
            }
        }

        return total == 0 ? 0 : 100.0 * right / total;
    };

    /// <summary>
    /// The micro F1, in points, of the categories scored at least <paramref name="threshold"/> against the gold categories scored at
    /// least 0.5, over all documents and labels.
    /// </summary>
    /// <param name="threshold">The score from which a predicted category counts. Default 0.5.</param>
    /// <returns>A function to give to <see cref="TrainOptions.Score"/>.</returns>
    public static Func<IReadOnlyList<Example>, double> CatsMicroF1(float threshold = 0.5f) => examples =>
    {
        long tp = 0;
        long fp = 0;
        long fn = 0;
        foreach (Example e in examples)
        {
            IReadOnlyDictionary<string, float> predicted = e.Predicted.Cats;
            foreach (KeyValuePair<string, float> p in predicted)
            {
                bool gold = e.Cats is not null && e.Cats.TryGetValue(p.Key, out float g) && g >= 0.5f;
                if (p.Value >= threshold)
                {
                    if (gold)
                    {
                        tp++;
                    }
                    else
                    {
                        fp++;
                    }
                }
                else if (gold)
                {
                    fn++;
                }
            }
        }

        return tp == 0 ? 0 : 100.0 * 2 * tp / ((2 * tp) + fp + fn);
    };

    /// <summary>The micro F1, in points, of the predicted spans of a group against the gold spans: a span counts when its start, end and label all match.</summary>
    /// <param name="spansKey">The span group.</param>
    /// <returns>A function to give to <see cref="TrainOptions.Score"/>.</returns>
    public static Func<IReadOnlyList<Example>, double> SpanGroup(string spansKey = "sc")
    {
        ArgumentException.ThrowIfNullOrEmpty(spansKey);
        return examples =>
        {
            long tp = 0;
            long fp = 0;
            long fn = 0;
            foreach (Example e in examples)
            {
                var gold = new HashSet<(int, int, string)>();
                if (e.Spans is not null && e.Spans.TryGetValue(spansKey, out IReadOnlyList<SpanSpec>? group))
                {
                    foreach (SpanSpec s in group)
                    {
                        gold.Add((s.Start, s.End, s.Label));
                    }
                }

                var predicted = new HashSet<(int, int, string)>();
                if (e.Predicted.Spans.TryGetValue(spansKey, out IReadOnlyList<Span>? spans))
                {
                    foreach (Span s in spans)
                    {
                        predicted.Add((s.Start, s.End, s.Label));
                    }
                }

                int hit = predicted.Count(gold.Contains);
                tp += hit;
                fp += predicted.Count - hit;
                fn += gold.Count - hit;
            }

            return tp == 0 ? 0 : 100.0 * 2 * tp / ((2 * tp) + fp + fn);
        };
    }

    private static string? TopLabel(IReadOnlyDictionary<string, float>? cats)
    {
        string? best = null;
        float top = 0f;
        if (cats is null)
        {
            return null;
        }

        foreach (KeyValuePair<string, float> c in cats.OrderBy(c => c.Key, StringComparer.Ordinal))
        {
            if (c.Value > top)
            {
                top = c.Value;
                best = c.Key;
            }
        }

        return best;
    }
}
