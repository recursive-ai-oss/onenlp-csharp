using System.Globalization;
using System.Text;

namespace RecursiveAI.OneNlp.Recipes.Accurate;

/// <summary>How often a multilingual run repeats each language's examples, and how much each head counts.</summary>
/// <param name="Alpha">Temperature exponent of the language shares: a language's share of the words seen is proportional to its words raised to this power
/// (1 = natural, 0 = every language alike). 0.3 is the value used for multilingual encoders (Conneau et al. 2020, section 3.1).</param>
/// <param name="MaxRepeat">Most times any language's examples are repeated in one pass.</param>
/// <param name="SmallMaxRepeat">The same for a language flagged small (fewer than 500 sentences): a tiny treebank repeated without limit is memorised.</param>
/// <param name="MinRepeat">Least share of a pass a language keeps (a fraction below 1 takes evenly spaced examples).</param>
/// <param name="ParserWeight">Loss weight of every parser head (1 = as the other heads). The dependency parser is where the accurate tier is furthest from
/// published systems, so it counts more.</param>
/// <param name="Boost">Extra factor on the share of named languages, set between runs from their development scores.</param>
/// <param name="NerMaxRepeat">Most times a language's entity examples are repeated.</param>
public sealed record SamplingOptions(
    double Alpha = 0.3,
    double MaxRepeat = 6,
    double SmallMaxRepeat = 8,
    double MinRepeat = 0.25,
    float ParserWeight = 1.5f,
    IReadOnlyDictionary<string, double>? Boost = null,
    double NerMaxRepeat = 2);

/// <summary>A language's row of the plan.</summary>
/// <param name="Code">Language code.</param>
/// <param name="Words">Training words after hold-out.</param>
/// <param name="Share">Target share of the words seen in one pass.</param>
/// <param name="Repeat">Times the language's UD examples are repeated in one pass (below 1: a fraction of them).</param>
/// <param name="NerRepeat">The same for its entity examples.</param>
public sealed record LanguagePlan(string Code, long Words, double Share, double Repeat, double NerRepeat);

/// <summary>
/// Temperature sampling over languages (Conneau et al. 2020, "Unsupervised Cross-lingual Representation Learning at Scale", section 3.1, after
/// Lample and Conneau 2019): the pass holds as many words as the natural corpus, spread over languages in proportion to words^alpha, then each language's
/// repetition is clipped so a small treebank is neither swamped nor memorised and a large one is not thrown away. The result is what the recipe's
/// <c>--repeat</c> option takes.
/// </summary>
public static class SamplingPlan
{
    /// <summary>Computes the plan for the given languages.</summary>
    /// <param name="spec">The corpus.</param>
    /// <param name="codes">The languages of the run.</param>
    /// <param name="wordsOf">Training words of a language as read (after the dev hold-out).</param>
    /// <param name="options">The knobs.</param>
    public static IReadOnlyList<LanguagePlan> Compute(CorpusSpec spec, IReadOnlyList<string> codes, Func<string, long> wordsOf, SamplingOptions options)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(codes);
        ArgumentNullException.ThrowIfNull(wordsOf);
        ArgumentNullException.ThrowIfNull(options);
        long total = codes.Sum(wordsOf);
        double norm = codes.Sum(c => Math.Pow(Math.Max(1, wordsOf(c)), options.Alpha) * Boost(options, c));
        var plans = new List<LanguagePlan>();
        foreach (string code in codes)
        {
            CorpusLanguage lang = spec[code];
            long words = Math.Max(1, wordsOf(code));
            double share = Math.Pow(words, options.Alpha) * Boost(options, code) / norm;
            double cap = lang.Small ? options.SmallMaxRepeat : options.MaxRepeat;
            double repeat = Math.Clamp(share * total / words, options.MinRepeat, cap);
            plans.Add(new LanguagePlan(code, words, share, Math.Round(repeat, 2), lang.Ner.Count == 0 ? 0 : Math.Min(options.NerMaxRepeat, Math.Max(1, Math.Round(repeat, 2)))));
        }

        return plans;
    }

    /// <summary>The plan as the recipe's <c>--repeat code=times,...</c> argument.</summary>
    /// <param name="plans">A plan from <see cref="Compute"/>.</param>
    public static string ToRepeatArgument(IEnumerable<LanguagePlan> plans)
    {
        ArgumentNullException.ThrowIfNull(plans);
        return string.Join(',', plans.Select(p => p.Code + "=" + p.Repeat.ToString("0.##", CultureInfo.InvariantCulture)));
    }

    /// <summary>A readable table of the plan: words, share, repetition, words seen per pass.</summary>
    /// <param name="plans">A plan from <see cref="Compute"/>.</param>
    public static string Describe(IEnumerable<LanguagePlan> plans)
    {
        ArgumentNullException.ThrowIfNull(plans);
        var sb = new StringBuilder("code      words   share%  repeat  words-per-pass\n");
        foreach (LanguagePlan p in plans)
        {
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{p.Code,-6} {p.Words,8} {100 * p.Share,7:F2} {p.Repeat,7:F2} {p.Words * p.Repeat,10:F0}"));
        }

        return sb.ToString();
    }

    private static double Boost(SamplingOptions options, string code) =>
        options.Boost is not null && options.Boost.TryGetValue(code, out double b) ? b : 1.0;
}
