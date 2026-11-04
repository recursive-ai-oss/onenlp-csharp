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
}
