namespace RecursiveAI.OneNlp.Scoring;

/// <summary>Text classification metrics: accuracy, macro and micro F1 for single-label tasks, and F1 and ROC-AUC for multi-label ones.</summary>
public static class ClassificationScorer
{
    /// <summary>The share of documents whose predicted label equals the gold label.</summary>
    /// <param name="gold">The gold labels.</param>
    /// <param name="predicted">The predicted labels, one per gold label.</param>
    /// <returns>A value from 0 to 1; 0 for no documents.</returns>
    /// <exception cref="ArgumentException">The lists differ in length.</exception>
    public static double Accuracy(IReadOnlyList<string> gold, IReadOnlyList<string> predicted)
    {
        ArgumentNullException.ThrowIfNull(gold);
        ArgumentNullException.ThrowIfNull(predicted);
        CheckLengths(gold.Count, predicted.Count);
        int correct = 0;
        for (int i = 0; i < gold.Count; i++)
        {
            if (string.Equals(gold[i], predicted[i], StringComparison.Ordinal))
            {
                correct++;
            }
        }

        return gold.Count == 0 ? 0 : (double)correct / gold.Count;
    }

    /// <summary>Per-label counts for single-label classification: gold = documents with the label, system = predicted with it.</summary>
    /// <param name="gold">The gold labels.</param>
    /// <param name="predicted">The predicted labels.</param>
    /// <returns>Counts per label (ordinal order), with the label in the key.</returns>
    /// <exception cref="ArgumentException">The lists differ in length.</exception>
    public static IReadOnlyDictionary<string, MetricCounts> LabelCounts(IReadOnlyList<string> gold, IReadOnlyList<string> predicted)
    {
        ArgumentNullException.ThrowIfNull(gold);
        ArgumentNullException.ThrowIfNull(predicted);
        CheckLengths(gold.Count, predicted.Count);
        var map = new SortedDictionary<string, MetricCounts>(StringComparer.Ordinal);
        for (int i = 0; i < gold.Count; i++)
        {
            bool hit = string.Equals(gold[i], predicted[i], StringComparison.Ordinal);
            Bump(map, gold[i], new MetricCounts(1, 0, hit ? 1 : 0));
            Bump(map, predicted[i], new MetricCounts(0, 1, 0));
        }

        return map;
    }

    /// <summary>The unweighted mean of the per-label F1 scores over the labels in the gold or the prediction.</summary>
    /// <param name="gold">The gold labels.</param>
    /// <param name="predicted">The predicted labels.</param>
    /// <returns>A value from 0 to 1; 0 for no documents.</returns>
    /// <exception cref="ArgumentException">The lists differ in length.</exception>
    public static double MacroF1(IReadOnlyList<string> gold, IReadOnlyList<string> predicted)
    {
        IReadOnlyDictionary<string, MetricCounts> counts = LabelCounts(gold, predicted);
        return counts.Count == 0 ? 0 : counts.Values.Average(c => c.F1);
    }

    /// <summary>The F1 of the counts summed over labels; for single-label tasks this equals the accuracy.</summary>
    /// <param name="gold">The gold labels.</param>
    /// <param name="predicted">The predicted labels.</param>
    /// <returns>A value from 0 to 1.</returns>
    /// <exception cref="ArgumentException">The lists differ in length.</exception>
    public static double MicroF1(IReadOnlyList<string> gold, IReadOnlyList<string> predicted)
    {
        MetricCounts sum = default;
        foreach (MetricCounts c in LabelCounts(gold, predicted).Values)
        {
            sum += c;
        }

        return sum.F1;
    }

    /// <summary>
    /// Multi-label F1: a label is predicted for a document when its score reaches the threshold. Returns macro (mean over labels
    /// that have a gold or predicted occurrence) and micro (counts summed over labels) F1.
    /// </summary>
    /// <param name="gold">Gold label sets: <c>gold[d][l]</c> is whether document <c>d</c> has label <c>l</c>.</param>
    /// <param name="scores">Scores, the same shape as <paramref name="gold"/>.</param>
    /// <param name="threshold">The decision threshold; 0.5 by default.</param>
    /// <returns>The macro and micro F1, each from 0 to 1.</returns>
    /// <exception cref="ArgumentException">The shapes differ.</exception>
    public static (double Macro, double Micro) MultiLabelF1(IReadOnlyList<bool[]> gold, IReadOnlyList<float[]> scores, float threshold = 0.5f)
    {
        ArgumentNullException.ThrowIfNull(gold);
        ArgumentNullException.ThrowIfNull(scores);
        CheckLengths(gold.Count, scores.Count);
        int labels = gold.Count == 0 ? 0 : gold[0].Length;
        var perLabel = new MetricCounts[labels];
        for (int d = 0; d < gold.Count; d++)
        {
            if (gold[d].Length != labels || scores[d].Length != labels)
            {
                throw new ArgumentException("Every document needs one entry per label.", nameof(scores));
            }

            for (int l = 0; l < labels; l++)
            {
                bool predicted = scores[d][l] >= threshold;
                perLabel[l] += new MetricCounts(gold[d][l] ? 1 : 0, predicted ? 1 : 0, gold[d][l] && predicted ? 1 : 0);
            }
        }

        MetricCounts sum = default;
        double macro = 0;
        int used = 0;
        foreach (MetricCounts c in perLabel)
        {
            sum += c;
            if (c.Gold + c.System > 0)
            {
                macro += c.F1;
                used++;
            }
        }

        return (used == 0 ? 0 : macro / used, sum.F1);
    }

    /// <summary>
    /// The area under the ROC curve for one label: the probability that a random positive scores higher than a random negative,
    /// ties counting half (the Mann-Whitney U statistic, Hanley and McNeil 1982).
    /// </summary>
    /// <param name="scores">The scores.</param>
    /// <param name="positive">Whether each document is a positive example.</param>
    /// <returns>A value from 0 to 1, or <see cref="double.NaN"/> when there are no positives or no negatives.</returns>
    /// <exception cref="ArgumentException">The lists differ in length.</exception>
    public static double RocAuc(IReadOnlyList<float> scores, IReadOnlyList<bool> positive)
    {
        ArgumentNullException.ThrowIfNull(scores);
        ArgumentNullException.ThrowIfNull(positive);
        CheckLengths(scores.Count, positive.Count);
        int n = scores.Count;
        var order = new int[n];
        for (int i = 0; i < n; i++)
        {
            order[i] = i;
        }

        Array.Sort(order, (a, b) => scores[a].CompareTo(scores[b]));
        double rankSumPositive = 0;
        long positives = 0;
        int start = 0;
        while (start < n)
        {
            int end = start;
            while (end + 1 < n && scores[order[end + 1]] == scores[order[start]])
            {
                end++;
            }

            double averageRank = (start + end) / 2.0 + 1; // tied scores share the mean of their ranks
            for (int k = start; k <= end; k++)
            {
                if (positive[order[k]])
                {
                    rankSumPositive += averageRank;
                    positives++;
                }
            }

            start = end + 1;
        }

        long negatives = n - positives;
        if (positives == 0 || negatives == 0)
        {
            return double.NaN;
        }

        return (rankSumPositive - positives * (positives + 1) / 2.0) / ((double)positives * negatives);
    }

    /// <summary>The mean ROC-AUC over labels, skipping labels that have only positive or only negative examples.</summary>
    /// <param name="gold">Gold label sets: <c>gold[d][l]</c> is whether document <c>d</c> has label <c>l</c>.</param>
    /// <param name="scores">Scores, the same shape.</param>
    /// <returns>A value from 0 to 1, or <see cref="double.NaN"/> when no label can be scored.</returns>
    /// <exception cref="ArgumentException">The shapes differ.</exception>
    public static double MacroRocAuc(IReadOnlyList<bool[]> gold, IReadOnlyList<float[]> scores)
    {
        ArgumentNullException.ThrowIfNull(gold);
        ArgumentNullException.ThrowIfNull(scores);
        CheckLengths(gold.Count, scores.Count);
        int labels = gold.Count == 0 ? 0 : gold[0].Length;
        double sum = 0;
        int used = 0;
        for (int l = 0; l < labels; l++)
        {
            var s = new float[gold.Count];
            var p = new bool[gold.Count];
            for (int d = 0; d < gold.Count; d++)
            {
                if (gold[d].Length != labels || scores[d].Length != labels)
                {
                    throw new ArgumentException("Every document needs one entry per label.", nameof(scores));
                }

                s[d] = scores[d][l];
                p[d] = gold[d][l];
            }

            double auc = RocAuc(s, p);
            if (!double.IsNaN(auc))
            {
                sum += auc;
                used++;
            }
        }

        return used == 0 ? double.NaN : sum / used;
    }

    private static void CheckLengths(int a, int b)
    {
        if (a != b)
        {
            throw new ArgumentException("The lists differ in length.");
        }
    }

    private static void Bump(SortedDictionary<string, MetricCounts> map, string label, MetricCounts add) =>
        map[label] = map.TryGetValue(label, out MetricCounts current) ? current + add : add;
}
