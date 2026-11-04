namespace RecursiveAI.OneNlp.Scoring;

/// <summary>
/// The counts behind a precision/recall score: how many items the gold standard has, how many the system produced, how many of
/// those are correct, and how many gold items had an aligned system counterpart. Counts add up across documents, which is what lets
/// a bootstrap resample documents without re-scoring.
/// </summary>
/// <param name="Gold">Items in the gold standard.</param>
/// <param name="System">Items the system produced.</param>
/// <param name="Correct">Items counted correct.</param>
/// <param name="Aligned">Gold items with an aligned system item (for exact-match span scores, equal to <paramref name="Correct"/>).</param>
public readonly record struct MetricCounts(long Gold, long System, long Correct, long Aligned = 0)
{
    /// <summary>Correct divided by system items; 0 when the system produced none.</summary>
    public double Precision => System == 0 ? 0 : (double)Correct / System;

    /// <summary>Correct divided by gold items; 0 when there are none.</summary>
    public double Recall => Gold == 0 ? 0 : (double)Correct / Gold;

    /// <summary>The harmonic mean of precision and recall, <c>2 * correct / (gold + system)</c>; 0 when both counts are 0.</summary>
    public double F1 => Gold + System == 0 ? 0 : 2.0 * Correct / (Gold + System);

    /// <summary>Correct divided by aligned gold items; 0 when none is aligned.</summary>
    public double AlignedAccuracy => Aligned == 0 ? 0 : (double)Correct / Aligned;

    /// <summary>Adds two sets of counts.</summary>
    /// <param name="left">The first.</param>
    /// <param name="right">The second.</param>
    /// <returns>The sum.</returns>
    public static MetricCounts operator +(MetricCounts left, MetricCounts right) =>
        new(left.Gold + right.Gold, left.System + right.System, left.Correct + right.Correct, left.Aligned + right.Aligned);
}

/// <summary>A named score in points (0 to 100) that a training loop can select on, such as <c>LAS</c> or <c>ents_f</c>.</summary>
/// <param name="Name">The metric name.</param>
/// <param name="Value">The score in points.</param>
public readonly record struct NamedScore(string Name, double Value);

/// <summary>
/// The result of scoring one predicted treebank against a gold one: the counts of every UD metric, in total and per gold sentence.
/// </summary>
public sealed class UdEvaluation
{
    private readonly Dictionary<string, MetricCounts[]> _perUnit;

    internal UdEvaluation(Dictionary<string, MetricCounts[]> perUnit, int units)
    {
        _perUnit = perUnit;
        Units = units;
    }

    /// <summary>The metric names, in the order of the shared-task table.</summary>
    public static IReadOnlyList<string> MetricNames { get; } =
        ["Tokens", "Sentences", "Words", "UPOS", "XPOS", "UFeats", "AllTags", "Lemmas", "UAS", "LAS", "CLAS", "MLAS", "BLEX"];

    /// <summary>The number of resampling units: the gold sentences.</summary>
    public int Units { get; }

    /// <summary>The summed counts of a metric.</summary>
    /// <param name="metric">One of <see cref="MetricNames"/>.</param>
    /// <returns>The counts.</returns>
    /// <exception cref="KeyNotFoundException">The metric is unknown.</exception>
    public MetricCounts Total(string metric)
    {
        MetricCounts sum = default;
        foreach (MetricCounts c in _perUnit[metric])
        {
            sum += c;
        }

        return sum;
    }

    /// <summary>
    /// The counts of a metric per gold sentence. Every gold item and its aligned system item count for the gold sentence the gold item
    /// starts in; a system item with no gold counterpart counts for the gold sentence containing its first character.
    /// </summary>
    /// <param name="metric">One of <see cref="MetricNames"/>.</param>
    /// <returns>One entry per gold sentence.</returns>
    /// <exception cref="KeyNotFoundException">The metric is unknown.</exception>
    public IReadOnlyList<MetricCounts> PerUnit(string metric) => _perUnit[metric];

    /// <summary>The F1 of a metric in points (0 to 100), the figure the shared task ranks by.</summary>
    /// <param name="metric">One of <see cref="MetricNames"/>.</param>
    /// <returns>The score.</returns>
    public double F1Points(string metric) => 100 * Total(metric).F1;
}

/// <summary>
/// The CoNLL 2018 UD evaluation (Zeman et al. 2018): tokens, sentences and words by character-span alignment, and UPOS, XPOS, UFeats,
/// AllTags, Lemmas, UAS, LAS, CLAS, MLAS and BLEX on the aligned words.
/// </summary>
public static class UdScorer
{
    private static readonly string[] GoldCounted = ["UPOS", "XPOS", "UFeats", "AllTags", "Lemmas", "UAS", "LAS"];

    /// <summary>
    /// Scores a predicted treebank against a gold one.
    /// </summary>
    /// <param name="gold">The gold standard.</param>
    /// <param name="predicted">The system output over the same text.</param>
    /// <returns>The counts of every metric.</returns>
    /// <exception cref="InvalidDataException">The two corpora do not have the same character stream (the tokens of one do not
    /// concatenate to those of the other, spaces aside).</exception>
    public static UdEvaluation Evaluate(UdCorpus gold, UdCorpus predicted)
    {
        ArgumentNullException.ThrowIfNull(gold);
        ArgumentNullException.ThrowIfNull(predicted);
        if (!string.Equals(gold.Characters, predicted.Characters, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The concatenation of tokens in the gold and predicted data differs.");
        }

        int units = gold.SentenceCount;
        var unitStarts = new int[units];
        for (int i = 0; i < units; i++)
        {
            unitStarts[i] = gold.Sentences[i].Start;
        }

        int UnitOf(int position)
        {
            int index = Array.BinarySearch(unitStarts, position);
            if (index < 0)
            {
                index = ~index - 1;
            }

            return Math.Max(index, 0);
        }

        var result = new Dictionary<string, MetricCounts[]>();
        foreach (string name in UdEvaluation.MetricNames)
        {
            result[name] = new MetricCounts[units];
        }

        if (units == 0)
        {
            return new UdEvaluation(result, 0);
        }

        SpanScore(gold.Tokens, predicted.Tokens, result["Tokens"], UnitOf);
        SpanScore(gold.Sentences, predicted.Sentences, result["Sentences"], UnitOf);

        IReadOnlyList<UdWord> gw = gold.Words;
        IReadOnlyList<UdWord> sw = predicted.Words;
        UdAlignment.Align(gw, sw, out int[] goldToSystem, out int[] systemToGold);

        // Head of a word expressed in gold word indexes: for a system word, its head's gold counterpart (-2 when it has none, which
        // equals nothing), so equal keys mean the same attachment.
        int GoldHead(int g) => gw[g].Parent;
        int SystemHead(int s) => sw[s].Parent < 0 ? -1 : systemToGold[sw[s].Parent] >= 0 ? systemToGold[sw[s].Parent] : -2;

        bool LemmaKey(int g, int s) => gw[g].Lemma == "_" || string.Equals(gw[g].Lemma, sw[s].Lemma, StringComparison.Ordinal);

        bool Children(int g, int s)
        {
            List<int>? gc = gw[g].FunctionalChildren;
            List<int>? sc = sw[s].FunctionalChildren;
            int gn = gc?.Count ?? 0;
            int sn = sc?.Count ?? 0;
            if (gn != sn)
            {
                return false;
            }

            for (int k = 0; k < gn; k++)
            {
                int cg = gc![k];
                int cs = sc![k];
                if (systemToGold[cs] != cg
                    || gw[cg].Deprel != sw[cs].Deprel
                    || gw[cg].Upos != sw[cs].Upos
                    || gw[cg].Feats != sw[cs].Feats)
                {
                    return false;
                }
            }

            return true;
        }

        for (int s = 0; s < sw.Count; s++)
        {
            int u = UnitOf(CharStart(sw[s]));
            Add(result, "Words", u, 0, 1, 0, 0);
            Add(result, "UPOS", u, 0, 1, 0, 0);
            Add(result, "XPOS", u, 0, 1, 0, 0);
            Add(result, "UFeats", u, 0, 1, 0, 0);
            Add(result, "AllTags", u, 0, 1, 0, 0);
            Add(result, "Lemmas", u, 0, 1, 0, 0);
            Add(result, "UAS", u, 0, 1, 0, 0);
            Add(result, "LAS", u, 0, 1, 0, 0);
            if (sw[s].IsContent)
            {
                Add(result, "CLAS", u, 0, 1, 0, 0);
                Add(result, "MLAS", u, 0, 1, 0, 0);
                Add(result, "BLEX", u, 0, 1, 0, 0);
            }
        }

        for (int g = 0; g < gw.Count; g++)
        {
            UdWord gold1 = gw[g];
            int u = UnitOf(CharStart(gold1));
            Add(result, "Words", u, 1, 0, 0, 0);
            foreach (string name in GoldCounted)
            {
                Add(result, name, u, 1, 0, 0, 0);
            }

            if (gold1.IsContent)
            {
                Add(result, "CLAS", u, 1, 0, 0, 0);
                Add(result, "MLAS", u, 1, 0, 0, 0);
                Add(result, "BLEX", u, 1, 0, 0, 0);
            }

            int s = goldToSystem[g];
            if (s < 0)
            {
                continue;
            }

            UdWord sys = sw[s];
            Add(result, "Words", u, 0, 0, 1, 1);
            bool upos = gold1.Upos == sys.Upos;
            bool xpos = gold1.Xpos == sys.Xpos;
            bool feats = gold1.Feats == sys.Feats;
            bool head = GoldHead(g) == SystemHead(s);
            bool las = head && gold1.Deprel == sys.Deprel;
            Add(result, "UPOS", u, 0, 0, upos ? 1 : 0, 1);
            Add(result, "XPOS", u, 0, 0, xpos ? 1 : 0, 1);
            Add(result, "UFeats", u, 0, 0, feats ? 1 : 0, 1);
            Add(result, "AllTags", u, 0, 0, upos && xpos && feats ? 1 : 0, 1);
            Add(result, "Lemmas", u, 0, 0, LemmaKey(g, s) ? 1 : 0, 1);
            Add(result, "UAS", u, 0, 0, head ? 1 : 0, 1);
            Add(result, "LAS", u, 0, 0, las ? 1 : 0, 1);
            if (gold1.IsContent)
            {
                Add(result, "CLAS", u, 0, 0, las ? 1 : 0, 1);
                Add(result, "MLAS", u, 0, 0, las && upos && feats && Children(g, s) ? 1 : 0, 1);
                Add(result, "BLEX", u, 0, 0, las && LemmaKey(g, s) ? 1 : 0, 1);
            }
        }

        return new UdEvaluation(result, units);
    }

    /// <summary>
    /// Scores predicted documents against gold documents and returns one metric as a named score in points, for use as a training
    /// loop's development score.
    /// </summary>
    /// <param name="predicted">The system output, one document per gold document, over the same text.</param>
    /// <param name="gold">The gold documents.</param>
    /// <param name="metric">One of <see cref="UdEvaluation.MetricNames"/>; <c>LAS</c> by default.</param>
    /// <returns>The metric's F1 in points, named after the metric.</returns>
    /// <exception cref="ArgumentException">The metric is unknown.</exception>
    /// <exception cref="InvalidDataException">The documents' text differs between the two sets.</exception>
    public static NamedScore Score(IEnumerable<Doc> predicted, IEnumerable<Doc> gold, string metric = "LAS")
    {
        ArgumentNullException.ThrowIfNull(metric);
        if (!UdEvaluation.MetricNames.Contains(metric))
        {
            throw new ArgumentException($"Unknown metric '{metric}'.", nameof(metric));
        }

        UdEvaluation evaluation = Evaluate(UdCorpus.FromDocs(gold), UdCorpus.FromDocs(predicted));
        return new NamedScore(metric, evaluation.F1Points(metric));
    }

    private static int CharStart(UdWord word) => word.Start;

    private static void Add(Dictionary<string, MetricCounts[]> result, string name, int unit, long gold, long system, long correct, long aligned) =>
        result[name][unit] += new MetricCounts(gold, system, correct, aligned);

    /// <summary>
    /// Matches spans that start at the same character, in the manner of the shared-task script: two spans are correct when both their
    /// start and end agree.
    /// </summary>
    private static void SpanScore(IReadOnlyList<CharSpan> gold, IReadOnlyList<CharSpan> system, MetricCounts[] counts, Func<int, int> unitOf)
    {
        foreach (CharSpan g in gold)
        {
            counts[unitOf(g.Start)] += new MetricCounts(1, 0, 0);
        }

        foreach (CharSpan s in system)
        {
            counts[unitOf(s.Start)] += new MetricCounts(0, 1, 0);
        }

        int gi = 0;
        int si = 0;
        while (gi < gold.Count && si < system.Count)
        {
            if (system[si].Start < gold[gi].Start)
            {
                si++;
            }
            else if (gold[gi].Start < system[si].Start)
            {
                gi++;
            }
            else
            {
                if (gold[gi].End == system[si].End)
                {
                    counts[unitOf(gold[gi].Start)] += new MetricCounts(0, 0, 1);
                }

                gi++;
                si++;
            }
        }
    }
}

/// <summary>
/// Word alignment between gold and system words (Zeman et al. 2018, §5): words with identical character spans align; where a multiword
/// token is involved, the words under the overlapping tokens are aligned by the longest common subsequence of their lower-cased forms.
/// </summary>
internal static class UdAlignment
{
    // The longest-common-subsequence table is quadratic in the words under one group of overlapping multiword tokens; real treebanks
    // have a handful of words there, so a larger group means broken input and is refused rather than allocated.
    private const long MaxAlignmentCells = 64L * 1024 * 1024;

    public static void Align(IReadOnlyList<UdWord> gold, IReadOnlyList<UdWord> system, out int[] goldToSystem, out int[] systemToGold)
    {
        goldToSystem = new int[gold.Count];
        systemToGold = new int[system.Count];
        Array.Fill(goldToSystem, -1);
        Array.Fill(systemToGold, -1);

        int gi = 0;
        int si = 0;
        while (gi < gold.Count && si < system.Count)
        {
            UdWord g = gold[gi];
            UdWord s = system[si];
            if (g.IsMultiword || s.IsMultiword)
            {
                // A plain word that lies wholly before the other side's multiword token cannot align with anything.
                if (!g.IsMultiword && g.End <= s.Start)
                {
                    gi++;
                    continue;
                }

                if (!s.IsMultiword && s.End <= g.Start)
                {
                    si++;
                    continue;
                }

                FindMultiwordSpan(gold, system, gi, si, out int ge, out int se);
                if (ge > gi && se > si)
                {
                    Lcs(gold, system, gi, ge, si, se, goldToSystem, systemToGold);
                }

                gi = Math.Max(ge, gi + (ge > gi ? 0 : 1));
                si = Math.Max(se, si + (se > si ? 0 : 1));
            }
            else if (g.Start == s.Start && g.End == s.End)
            {
                goldToSystem[gi] = si;
                systemToGold[si] = gi;
                gi++;
                si++;
            }
            else if (g.Start <= s.Start)
            {
                gi++;
            }
            else
            {
                si++;
            }
        }
    }

    /// <summary>Finds the smallest run of gold and system words, from the current positions, whose tokens overlap one another.</summary>
    private static void FindMultiwordSpan(IReadOnlyList<UdWord> gold, IReadOnlyList<UdWord> system, int gi, int si, out int ge, out int se)
    {
        int end = Math.Max(gold[gi].IsMultiword ? gold[gi].End : 0, system[si].IsMultiword ? system[si].End : 0);
        ge = gi;
        se = si;
        bool grew = true;
        while (grew)
        {
            grew = false;
            while (ge < gold.Count && gold[ge].Start < end)
            {
                end = Math.Max(end, gold[ge].End);
                ge++;
                grew = true;
            }

            while (se < system.Count && system[se].Start < end)
            {
                end = Math.Max(end, system[se].End);
                se++;
                grew = true;
            }
        }
    }

    private static void Lcs(
        IReadOnlyList<UdWord> gold, IReadOnlyList<UdWord> system, int gi, int ge, int si, int se, int[] goldToSystem, int[] systemToGold)
    {
        int gn = ge - gi;
        int sn = se - si;
        if ((long)gn * sn > MaxAlignmentCells)
        {
            throw new InvalidDataException("A group of overlapping multiword tokens is too large to align (more than 64 million word pairs).");
        }

        var table = new int[gn + 1, sn + 1];
        for (int g = gn - 1; g >= 0; g--)
        {
            for (int s = sn - 1; s >= 0; s--)
            {
                table[g, s] = SameForm(gold[gi + g], system[si + s])
                    ? 1 + table[g + 1, s + 1]
                    : Math.Max(table[g + 1, s], table[g, s + 1]);
            }
        }

        int x = 0;
        int y = 0;
        while (x < gn && y < sn)
        {
            if (SameForm(gold[gi + x], system[si + y]))
            {
                goldToSystem[gi + x] = si + y;
                systemToGold[si + y] = gi + x;
                x++;
                y++;
            }
            else if (table[x, y] == table[x + 1, y])
            {
                x++;
            }
            else
            {
                y++;
            }
        }
    }

    private static bool SameForm(UdWord a, UdWord b) =>
        string.Equals(a.Form, b.Form, StringComparison.OrdinalIgnoreCase);
}
