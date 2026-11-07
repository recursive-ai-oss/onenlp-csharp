using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;
using RecursiveAI.OneNlp.Neural.Training;
using RecursiveAI.OneNlp.Segmentation;

namespace RecursiveAI.OneNlp.Tests;

public class NeuralMultiwordSplitterTests
{
    [Theory]
    [InlineData("var", true)]
    [InlineData("va", false)]
    [InlineData("vardır", true)]
    [InlineData("Ankara'da", false)]
    [InlineData("12ab", false)]
    [InlineData("a-b-c", false)]
    public void OnlyLettersOnlyTokensAreCandidates(string token, bool expected) => Assert.Equal(expected, NeuralMultiwordSplitter.IsCandidate(token));

    [Fact]
    public void LongRunsOfLettersAreNotCandidates() => Assert.False(NeuralMultiwordSplitter.IsCandidate(new string('a', NeuralMultiwordSplitter.MaxCandidateLength + 1)));

    [Fact]
    public void TagsMarkWhereGoldPiecesStart()
    {
        Assert.Equal(["B", "I", "I", "B", "I"], NeuralMultiwordSplitter.Tags(5, [3, 2]));
        Assert.Equal(["B", "I", "I"], NeuralMultiwordSplitter.Tags(3, [3]));
        Assert.Throws<ArgumentException>(() => NeuralMultiwordSplitter.Tags(5, [3, 3]));
        Assert.Throws<ArgumentException>(() => NeuralMultiwordSplitter.Tags(5, [3, 0, 2]));
    }

    [Fact]
    public void WindowsHaveOneUnitPerCandidateCharacterAndOneForTheRest()
    {
        var store = new StringStore();
        Doc window = NeuralMultiwordSplitter.MakeWindow(store, ["vardır", ".", "A'da"], out int[] first);
        // 6 letters, a space, the dot, a space, one unit for the token with an apostrophe.
        Assert.Equal(6 + 1 + 1 + 1 + 1, window.Length);
        Assert.Equal([0, 7, 9], first);
        Assert.Equal("vardır . A'da", window.Text);
        Assert.Equal([1, 2, 3, 4, 5], NeuralMultiwordSplitter.InteriorUnits(window));
    }

    [Fact]
    public void WindowsEndAfterASentenceMarkAndAtTheLimit()
    {
        var tokens = new List<string> { "a", ".", "b", "c" };
        Assert.Equal([(0, 2), (2, 4)], NeuralMultiwordSplitter.Windows(tokens));
        var many = Enumerable.Repeat("x", NeuralMultiwordSplitter.MaxWindowTokens + 1).ToList();
        Assert.Equal([(0, NeuralMultiwordSplitter.MaxWindowTokens), (NeuralMultiwordSplitter.MaxWindowTokens, many.Count)], NeuralMultiwordSplitter.Windows(many));
    }

    // Words of a small lexicon, some of which take the suffix "dır" as a word of its own, written together.
    private static readonly string[] Stems = ["var", "yok", "güzel", "kitap", "eski", "temiz"];

    private static List<Example> Examples(int count, int seed)
    {
        var random = new Random(seed);
        var strings = new StringStore();
        var result = new List<Example>();
        for (int i = 0; i < count; i++)
        {
            var surface = new List<string>();
            var pieces = new List<int[]>();
            int n = random.Next(2, 6);
            for (int k = 0; k < n; k++)
            {
                string stem = Stems[random.Next(Stems.Length)];
                if (random.Next(2) == 0)
                {
                    surface.Add(stem + "dır");
                    pieces.Add([stem.Length, 3]);
                }
                else
                {
                    surface.Add(stem);
                    pieces.Add([stem.Length]);
                }
            }

            Doc doc = NeuralMultiwordSplitter.MakeWindow(strings, surface, out int[] first);
            var tags = new string[doc.Length];
            Array.Fill(tags, NeuralMultiwordSplitter.BeginLabel);
            for (int k = 0; k < surface.Count; k++)
            {
                string[] t = NeuralMultiwordSplitter.Tags(surface[k].Length, pieces[k]);
                Array.Copy(t, 0, tags, first[k], t.Length);
            }

            for (int u = 0; u < tags.Length; u++)
            {
                doc.SetAnnotation(TokenColumn.Tag, u, tags[u]);
            }

            result.Add(Example.FromReference(doc));
        }

        return result;
    }

    [Fact]
    public void TrainedSplitterCutsSavesAndLoadsAsATokenizer()
    {
        List<Example> train = Examples(400, 1);
        var encoder = new FastEncoder(width: 32, depth: 1, normRows: 512, prefixRows: 64, suffixRows: 64, shapeRows: 64, seed: 3, recurrentLayers: 1, recurrentSize: 16);
        using Pipeline pipeline = Pipeline.Blank(Language.Turkish).Add(new Tagger(encoder, TokenColumn.Tag, NeuralMultiwordSplitter.HeadName) { OwnsEncoder = true });
        TrainResult result = pipeline.Train(train, new TrainOptions { Epochs = 15, BatchWords = 300, LearningRate = 0.01f, Dropout = 0f, Seed = 5, DevFraction = 0.1 });
        Assert.True(result.Completed);

        var splitter = new NeuralMultiwordSplitter(new UaxWordTokenizer(SegmentationData.TurkishWhole), (Tagger)pipeline.Components[0]);
        string path = Path.Combine(Path.GetTempPath(), "onenlp-splitter-" + Guid.NewGuid().ToString("N") + ".onenlp");
        try
        {
            using var holder = new Pipeline(splitter, Language.Turkish);
            PipelineFile.Save(holder, path);
            using Pipeline loaded = PipelineFile.Load(path);
            var loadedSplitter = Assert.IsType<NeuralMultiwordSplitter>(loaded.Tokenizer);
            Assert.Equal("turkish-whole", Assert.IsType<UaxWordTokenizer>(loadedSplitter.Inner).Data.Name);

            const string text = "güzeldır, kitap vardır. eski yok!";
            Doc a = holder.Process(text);
            Doc b = loaded.Process(text);
            Assert.Equal(a.GetStarts().ToArray(), b.GetStarts().ToArray());
            Assert.Equal(a.GetLengths().ToArray(), b.GetLengths().ToArray());
            Assert.Equal(["güzel", "dır", ",", "kitap", "var", "dır", ".", "eski", "yok", "!"], a.Select(t => t.ToString()).ToArray());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FindsThePiecesOfAMultiwordTokenAndNotPunctuation()
    {
        var strings = new StringStore();
        var doc = new Doc(strings, "var dır, ok", [new TokenBoundary(0, 3), new TokenBoundary(4, 3), new TokenBoundary(7, 1), new TokenBoundary(9, 2)]);
        Assert.False(NeuralMultiwordSplitter.TryMatch(doc, 0, out _));

        var touching = new Doc(strings, "vardır, ok", [new TokenBoundary(0, 3), new TokenBoundary(3, 3), new TokenBoundary(6, 1), new TokenBoundary(8, 2)]);
        Assert.True(NeuralMultiwordSplitter.TryMatch(touching, 0, out int count));
        Assert.Equal(2, count);
        Assert.False(NeuralMultiwordSplitter.TryMatch(touching, 1, out _));
        Assert.False(NeuralMultiwordSplitter.TryMatch(touching, 2, out _));
    }

    [Fact]
    public void RejectsATaggerWithOtherLabels()
    {
        var encoder = new FastEncoder(width: 16, depth: 1, normRows: 64, prefixRows: 16, suffixRows: 16, shapeRows: 16, seed: 1);
        using var tagger = new Tagger(encoder, TokenColumn.Tag, NeuralMultiwordSplitter.HeadName) { OwnsEncoder = true };
        Assert.Throws<ArgumentException>(() => new NeuralMultiwordSplitter(new UaxWordTokenizer(SegmentationData.TurkishWhole), tagger));
    }

    [Fact]
    public void TheWholeWordDataNeverCutsAWordTheTableWould()
    {
        var uax = new UaxWordTokenizer(SegmentationData.TurkishWhole);
        Doc doc = uax.Tokenize("Burada güzeldir ve vardır.", new StringStore());
        Assert.Equal(["Burada", "güzeldir", "ve", "vardır", "."], doc.Select(t => t.ToString()).ToArray());
        Assert.Equal("turkish-whole", SegmentationData.ByName("turkish-whole").Name);
    }
}
