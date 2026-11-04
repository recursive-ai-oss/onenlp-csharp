using RecursiveAI.OneNlp.Scoring;

namespace RecursiveAI.OneNlp.Tests;

public class ScoringTests
{
    /// <summary>Builds CoNLL-U from rows "id form lemma upos xpos feats head deprel" (feats written without spaces; '_' for none).</summary>
    private static UdCorpus Corpus(params string[] sentences)
    {
        var lines = new List<string>();
        foreach (string sentence in sentences)
        {
            foreach (string row in sentence.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string[] f = row.Split(' ');
                lines.Add(string.Join('\t', f[0], f[1], f[2], f[3], f[4], f[5], f[6], f[7], "_", "_"));
            }

            lines.Add(string.Empty);
        }

        return UdCorpus.Read(new StringReader(string.Join('\n', lines)));
    }

    private static double F1(UdEvaluation e, string metric) => e.Total(metric).F1;

    private const string Simple = """
        1 The the DET DT _ 2 det
        2 dog dog NOUN NN Number=Sing 3 nsubj
        3 barks bark VERB VBZ Number=Sing 0 root
        4 loudly loudly ADV RB _ 3 advmod
        """;

    [Fact]
    public void IdenticalCorporaScoreOneOnEveryMetric()
    {
        UdCorpus c = Corpus(Simple);
        UdEvaluation e = UdScorer.Evaluate(c, Corpus(Simple));
        foreach (string m in UdEvaluation.MetricNames)
        {
            Assert.Equal(1.0, F1(e, m));
        }
    }

    [Fact]
    public void WrongTagHeadAndRelationAreCountedPerMetric()
    {
        const string Predicted = """
            1 The the DET DT _ 2 det
            2 dog dog VERB NN Number=Sing 3 nsubj
            3 barks barked VERB VBZ Number=Sing 0 root
            4 loudly loudly ADV RB _ 2 advmod
            """;
        UdEvaluation e = UdScorer.Evaluate(Corpus(Simple), Corpus(Predicted));
        Assert.Equal(0.75, F1(e, "UPOS"));
        Assert.Equal(1.0, F1(e, "XPOS"));
        Assert.Equal(0.75, F1(e, "Lemmas"));
        Assert.Equal(0.75, F1(e, "UAS"));
        Assert.Equal(0.75, F1(e, "LAS"));
        // Content words are dog, barks, loudly (det is functional): 2 of 3 attach correctly.
        Assert.Equal(2.0 / 3, F1(e, "CLAS"), 12);
        Assert.Equal(1, e.Total("Sentences").Correct);
    }

    [Fact]
    public void DeprelSubtypesAreIgnored()
    {
        const string Gold = """
            1 his he PRON PRP$ _ 2 nmod:poss
            2 dog dog NOUN NN _ 0 root
            """;
        const string Predicted = """
            1 his he PRON PRP$ _ 2 nmod
            2 dog dog NOUN NN _ 0 root
            """;
        Assert.Equal(1.0, F1(UdScorer.Evaluate(Corpus(Gold), Corpus(Predicted)), "LAS"));
    }

    [Fact]
    public void OnlyUniversalFeaturesCountAndOrderDoesNot()
    {
        const string Gold = """
            1 dogs dog NOUN NN Number=Plur|Typo=Yes|Case=Nom 0 root
            """;
        const string Predicted = """
            1 dogs dog NOUN NN Case=Nom|Number=Plur|NumForm=Word 0 root
            """;
        const string Wrong = """
            1 dogs dog NOUN NN Number=Sing|Case=Nom 0 root
            """;
        Assert.Equal(1.0, F1(UdScorer.Evaluate(Corpus(Gold), Corpus(Predicted)), "UFeats"));
        Assert.Equal(0.0, F1(UdScorer.Evaluate(Corpus(Gold), Corpus(Wrong)), "UFeats"));
    }

    [Fact]
    public void GoldLemmaUnderscoreAcceptsAnything()
    {
        const string Gold = """
            1 x _ X X _ 0 root
            """;
        const string Predicted = """
            1 x whatever X X _ 0 root
            """;
        Assert.Equal(1.0, F1(UdScorer.Evaluate(Corpus(Gold), Corpus(Predicted)), "Lemmas"));
    }

    [Fact]
    public void SplitAndMergedTokensAlignByCharacterSpan()
    {
        const string Gold = """
            1 New New PROPN NNP _ 2 compound
            2 York York PROPN NNP _ 0 root
            3 city city NOUN NN _ 2 appos
            """;
        const string Merged = """
            1 NewYork NewYork PROPN NNP _ 0 root
            2 city city NOUN NN _ 1 appos
            """;
        UdEvaluation e = UdScorer.Evaluate(Corpus(Gold), Corpus(Merged));
        Assert.Equal(1, e.Total("Words").Correct); // only "city" has the same span
        Assert.Equal(3, e.Total("Words").Gold);
        Assert.Equal(2, e.Total("Words").System);
        Assert.Equal(1, e.Total("UPOS").Correct);
        Assert.Equal(0, e.Total("UAS").Correct); // "city" attaches to a head that has no aligned counterpart
    }

    [Fact]
    public void ModifiedTokensDoNotChangeSentenceSpans()
    {
        const string Gold = """
            1 a a X X _ 0 root
            2 b b X X _ 1 dep
            """;
        const string Predicted = """
            1 ab ab X X _ 0 root
            """;
        UdEvaluation e = UdScorer.Evaluate(Corpus(Gold), Corpus(Predicted));
        Assert.Equal(1, e.Total("Sentences").Correct);
        Assert.Equal(0, e.Total("Tokens").Correct);
        Assert.Equal(2, e.Total("Tokens").Gold);
    }

    [Fact]
    public void DifferentTextIsRejected()
    {
        const string Other = """
            1 cat cat NOUN NN _ 0 root
            """;
        Assert.Throws<InvalidDataException>(() => UdScorer.Evaluate(Corpus(Simple), Corpus(Other)));
    }

    private static string Row(string id, string form, string upos = "X", string head = "0", string deprel = "root") =>
        string.Join('\t', id, form, form, upos, "_", "_", head, deprel, "_", "_");

    [Fact]
    public void MultiwordTokenWordsAlignThroughTheirForms()
    {
        // gold: "don't" = do + n't ; system tokenizes the same string as do + n't without the multiword token
        string gold = string.Join('\n', Row("1-2", "don't"), Row("1", "do"), Row("2", "n't", "PART", "1", "advmod"), string.Empty);
        string system = string.Join('\n', Row("1", "do"), Row("2", "n't", "PART", "1", "advmod"), string.Empty);
        UdEvaluation e = UdScorer.Evaluate(UdCorpus.Read(new StringReader(gold)), UdCorpus.Read(new StringReader(system)));
        Assert.Equal(2, e.Total("Words").Correct);
        Assert.Equal(2, e.Total("LAS").Correct);
        Assert.Equal(1, e.Total("Tokens").Gold);
        Assert.Equal(2, e.Total("Tokens").System);
        Assert.Equal(0, e.Total("Tokens").Correct); // the system's two tokens do not match the gold's one
    }

    [Fact]
    public void MultiwordTokenWithDifferentWordFormsAlignsByLongestCommonSubsequence()
    {
        // gold splits "cannot" as can + not; the system as can + n + ot: only "can" matches by form
        string gold = string.Join('\n', Row("1-2", "cannot"), Row("1", "can", "AUX", "0", "root"), Row("2", "not", "PART", "1", "advmod"), string.Empty);
        string system = string.Join('\n', Row("1-3", "cannot"), Row("1", "can", "AUX", "0", "root"), Row("2", "n", "X", "1", "dep"), Row("3", "ot", "X", "1", "dep"), string.Empty);
        UdEvaluation e = UdScorer.Evaluate(UdCorpus.Read(new StringReader(gold)), UdCorpus.Read(new StringReader(system)));
        Assert.Equal(1, e.Total("Words").Correct);
        Assert.Equal(2, e.Total("Words").Gold);
        Assert.Equal(3, e.Total("Words").System);
        Assert.Equal(1, e.Total("Tokens").Correct);
        Assert.Equal(1, e.Total("UPOS").Correct);
    }

    [Fact]
    public void FormsInTheLongestCommonSubsequenceAreComparedWithoutCase()
    {
        string gold = string.Join('\n', Row("1-2", "Cannot"), Row("1", "Can", "AUX"), Row("2", "not", "PART", "1", "advmod"), string.Empty);
        string system = string.Join('\n', Row("1-2", "Cannot"), Row("1", "can", "AUX"), Row("2", "not", "PART", "1", "advmod"), string.Empty);
        Assert.Equal(2, UdScorer.Evaluate(UdCorpus.Read(new StringReader(gold)), UdCorpus.Read(new StringReader(system))).Total("Words").Correct);
    }

    [Fact]
    public void PlainWordsBeforeAMultiwordTokenDoNotAlignAcrossIt()
    {
        string gold = string.Join('\n', Row("1", "the"), Row("2-3", "don't"), Row("2", "do", "AUX", "0"), Row("3", "n't", "PART", "2", "advmod"), string.Empty);
        string system = string.Join('\n', Row("1", "the"), Row("2", "do", "AUX", "0"), Row("3", "n't", "PART", "2", "advmod"), string.Empty);
        UdEvaluation e = UdScorer.Evaluate(UdCorpus.Read(new StringReader(gold)), UdCorpus.Read(new StringReader(system)));
        Assert.Equal(3, e.Total("Words").Correct);
    }

    [Fact]
    public void EmptyNodesAreSkipped()
    {
        string text = string.Join('\n', Row("1", "a"), Row("1.1", "ghost"), Row("2", "b", "X", "1", "dep"), string.Empty);
        UdCorpus c = UdCorpus.Read(new StringReader(text));
        Assert.Equal(2, c.WordCount);
    }

    [Fact]
    public void CyclesAndBadHeadsAreRejected()
    {
        string cycle = string.Join('\n', Row("1", "a", "X", "2", "dep"), Row("2", "b", "X", "1", "dep"), string.Empty);
        Assert.Throws<InvalidDataException>(() => UdCorpus.Read(new StringReader(cycle)));
        string range = string.Join('\n', Row("1", "a", "X", "5", "dep"), string.Empty);
        Assert.Throws<InvalidDataException>(() => UdCorpus.Read(new StringReader(range)));
        Assert.Throws<InvalidDataException>(() => UdCorpus.Read(new StringReader("1\ta\tb")));
    }

    [Fact]
    public void MlasRequiresFunctionalChildrenToMatch()
    {
        const string Gold = """
            1 the the DET DT _ 2 det
            2 dog dog NOUN NN Number=Sing 0 root
            """;
        const string NoDeterminerRelation = """
            1 the the DET DT _ 2 amod
            2 dog dog NOUN NN Number=Sing 0 root
            """;
        UdEvaluation e = UdScorer.Evaluate(Corpus(Gold), Corpus(NoDeterminerRelation));
        Assert.Equal(1, e.Total("CLAS").Correct);
        Assert.Equal(0, e.Total("MLAS").Correct);
        Assert.Equal(1, e.Total("BLEX").Correct);
    }

    [Fact]
    public void PerUnitCountsSumToTheTotal()
    {
        UdCorpus gold = Corpus(Simple, Simple);
        UdEvaluation e = UdScorer.Evaluate(gold, Corpus(Simple, Simple));
        Assert.Equal(2, e.Units);
        foreach (string m in UdEvaluation.MetricNames)
        {
            Assert.Equal(e.Total(m).Gold, e.PerUnit(m).Sum(c => c.Gold));
            Assert.Equal(e.Total(m).Correct, e.PerUnit(m).Sum(c => c.Correct));
        }
    }

    [Fact]
    public void DocsScoreLikeConllu()
    {
        string text = "The dog barks";
        Doc Make(string upos2)
        {
            var doc = new Doc(new StringStore(), text, [new TokenBoundary(0, 3), new TokenBoundary(4, 3), new TokenBoundary(8, 5)]);
            doc.SetAnnotation(TokenColumn.Pos, 0, "DET");
            doc.SetAnnotation(TokenColumn.Pos, 1, upos2);
            doc.SetAnnotation(TokenColumn.Pos, 2, "VERB");
            return doc;
        }

        NamedScore score = UdScorer.Score([Make("VERB")], [Make("NOUN")], "UPOS");
        Assert.Equal("UPOS", score.Name);
        Assert.Equal(100 * 2.0 / 3, score.Value, 9);
        Assert.Equal(100.0, UdScorer.Score([Make("NOUN")], [Make("NOUN")], "UPOS").Value);
    }

    [Fact]
    public void SpanScoringIsStrictIob2()
    {
        string[] g = ["B-PER", "I-PER", "O", "B-LOC", "O"];
        string[] p = ["B-PER", "I-PER", "I-LOC", "B-LOC", "I-LOC"]; // stray I-LOC after PER is ignored; B-LOC I-LOC is a 2-token span
        SpanEvaluation e = SpanScorer.ScoreIob2([g], [p]);
        Assert.Equal(2, e.Micro.Gold);
        Assert.Equal(2, e.Micro.System);
        Assert.Equal(1, e.Micro.Correct);
        Assert.Equal(1.0, e.PerLabel["PER"].F1);
        Assert.Equal(0.0, e.PerLabel["LOC"].F1);
        Assert.Equal(0.5, e.MacroF1);
    }

    [Fact]
    public void IobSpanTypeChangeEndsTheSpan()
    {
        List<LabeledSpan> spans = SpanScorer.ExtractIob2(["B-A", "I-B", "I-A"]);
        Assert.Equal([new LabeledSpan(0, 1, "A")], spans);
    }

    [Fact]
    public void ClassificationMetrics()
    {
        string[] gold = ["a", "a", "b", "b", "c"];
        string[] pred = ["a", "b", "b", "b", "a"];
        Assert.Equal(0.6, ClassificationScorer.Accuracy(gold, pred), 12);
        Assert.Equal(0.6, ClassificationScorer.MicroF1(gold, pred), 12);
        // a: P=1/2 R=1/2 F=.5 ; b: P=2/3 R=1 F=.8 ; c: F=0
        Assert.Equal((0.5 + 0.8 + 0.0) / 3, ClassificationScorer.MacroF1(gold, pred), 12);
    }

    [Fact]
    public void RocAucHandlesTiesAndDegenerateLabels()
    {
        Assert.Equal(1.0, ClassificationScorer.RocAuc([0.1f, 0.2f, 0.8f, 0.9f], [false, false, true, true]));
        Assert.Equal(0.5, ClassificationScorer.RocAuc([0.5f, 0.5f, 0.5f, 0.5f], [false, true, false, true]));
        Assert.Equal(0.75, ClassificationScorer.RocAuc([0.1f, 0.4f, 0.35f, 0.8f], [false, false, true, true]));
        Assert.True(double.IsNaN(ClassificationScorer.RocAuc([0.1f, 0.2f], [true, true])));
        double macro = ClassificationScorer.MacroRocAuc(
            [[true, false], [false, false], [true, false]],
            [[0.9f, 0.1f], [0.2f, 0.3f], [0.8f, 0.2f]]);
        Assert.Equal(1.0, macro); // the second label has no positives and is skipped
    }

    [Fact]
    public void BootstrapIsDeterministicAndSeedSensitive()
    {
        MetricCounts[] units = Enumerable.Range(0, 200).Select(i => new MetricCounts(10, 10, 5 + i % 5)).ToArray();
        ConfidenceInterval a = Bootstrap.Interval(units, Bootstrap.F1Points, 1000, 7);
        ConfidenceInterval b = Bootstrap.Interval(units, Bootstrap.F1Points, 1000, 7);
        ConfidenceInterval c = Bootstrap.Interval(units, Bootstrap.F1Points, 1000, 8);
        Assert.Equal(a, b);
        Assert.NotEqual(a.Low, c.Low);
        Assert.True(a.Low < a.Estimate && a.Estimate < a.High);
        Assert.Equal(70.0, a.Estimate, 9);
    }

    [Fact]
    public void PairedBootstrapSeparatesDifferentSystemsAndNotIdenticalOnes()
    {
        MetricCounts[] first = Enumerable.Range(0, 300).Select(i => new MetricCounts(10, 10, 9)).ToArray();
        MetricCounts[] second = Enumerable.Range(0, 300).Select(i => new MetricCounts(10, 10, i % 2 == 0 ? 8 : 9)).ToArray();
        Assert.True(Bootstrap.Paired(first, first, Bootstrap.F1Points).DifferenceContainsZero);
        PairedResult r = Bootstrap.Paired(first, second, Bootstrap.F1Points);
        Assert.False(r.DifferenceContainsZero);
        Assert.Equal(5.0, r.Difference.Estimate, 9);
        Assert.Equal(r, Bootstrap.Paired(first, second, Bootstrap.F1Points));
    }

    [Fact]
    public void WithinNoiseRule()
    {
        var ours = new ConfidenceInterval(90, 89, 91, 1000, 1);
        Assert.True(NoiseRule.WithinNoise(ours, new ConfidenceInterval(91.5, 90.5, 92.5, 1000, 1)));
        Assert.False(NoiseRule.WithinNoise(ours, new ConfidenceInterval(93, 92, 94, 1000, 1)));
        Assert.True(NoiseRule.WithinNoise(ours, 90.8));
        Assert.True(NoiseRule.WithinNoise(new ConfidenceInterval(90, 89.9, 90.1, 1000, 1), 90.4));
        Assert.False(NoiseRule.WithinNoise(new ConfidenceInterval(90, 89.9, 90.1, 1000, 1), 90.6));
    }
}
