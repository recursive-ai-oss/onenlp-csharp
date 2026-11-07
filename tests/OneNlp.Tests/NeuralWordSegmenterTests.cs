using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;

namespace RecursiveAI.OneNlp.Tests;

public class NeuralWordSegmenterTests
{
    private static string Cut(string text, TokenBoundary[] units) => string.Join("|", units.Select(u => text.Substring(u.Start, u.Length)));

    [Theory]
    [InlineData("我爱北京", "我|爱|北|京")]
    [InlineData("abc 12.5%，ok", "abc|12.5|%|，|ok")]
    [InlineData("3G手机", "3|G|手|机")]
    [InlineData("1,2 x,y", "1,2|x|,|y")]
    [InlineData("  \t\n", "")]
    [InlineData("", "")]
    [InlineData("𠀀a", "𠀀|a")]
    public void UnitsAreCharactersExceptLetterAndDigitRuns(string text, string expected) =>
        Assert.Equal(expected, Cut(text, NeuralWordSegmenter.Units(text)));

    [Fact]
    public void ALoneSurrogateIsOneUnit() => Assert.Equal(3, NeuralWordSegmenter.Units("a" + '\uD800' + "b").Length);

    [Fact]
    public void TagsMarkGoldWordStartsAndCountTheOnesInsideAUnit()
    {
        string text = "3G手机";
        TokenBoundary[] units = NeuralWordSegmenter.Units(text);
        string[] tags = NeuralWordSegmenter.Tags(units, [0, 1, 2], out int lost);
        Assert.Equal(["B", "B", "B", "I"], tags);
        Assert.Equal(0, lost);

        units = NeuralWordSegmenter.Units("ab手");
        tags = NeuralWordSegmenter.Tags(units, [0, 1, 2], out lost);
        Assert.Equal(["B", "B"], tags);
        Assert.Equal(1, lost);
    }

    // Sentences made of words of a small lexicon, written without spaces.
    private static (string Text, int[] Starts) Sentence(Random random, string[] lexicon)
    {
        var sb = new System.Text.StringBuilder();
        var starts = new List<int>();
        int n = random.Next(3, 9);
        for (int i = 0; i < n; i++)
        {
            starts.Add(sb.Length);
            sb.Append(lexicon[random.Next(lexicon.Length)]);
        }

        return (sb.ToString(), [.. starts]);
    }

    private static List<Example> Examples(int count, int seed, string[] lexicon)
    {
        var random = new Random(seed);
        var strings = new StringStore();
        var result = new List<Example>();
        for (int i = 0; i < count; i++)
        {
            (string text, int[] starts) = Sentence(random, lexicon);
            TokenBoundary[] units = NeuralWordSegmenter.Units(text);
            var doc = new Doc(strings, text, units);
            string[] tags = NeuralWordSegmenter.Tags(units, starts, out _);
            for (int u = 0; u < tags.Length; u++)
            {
                doc.SetAnnotation(TokenColumn.Tag, u, tags[u]);
            }

            result.Add(Example.FromReference(doc));
        }

        return result;
    }

    [Fact]
    public void TrainedSegmenterCutsWordsSavesAndLoadsAsATokenizer()
    {
        string[] lexicon = ["我们", "喜欢", "学习", "中文", "和", "数据", "的", "人", "工智能", "北京大学"];
        List<Example> train = Examples(300, 1, lexicon);
        var encoder = new FastEncoder(width: 32, depth: 1, normRows: 512, prefixRows: 64, suffixRows: 64, shapeRows: 64, seed: 3, neighbourRows: 256, bigramRows: 256, recurrentLayers: 1, recurrentSize: 16);
        using Pipeline pipeline = Pipeline.Blank(Language.Chinese).Add(new Tagger(encoder, TokenColumn.Tag, NeuralWordSegmenter.HeadName) { OwnsEncoder = true });
        TrainResult result = pipeline.Train(train, new TrainOptions { Epochs = 12, BatchWords = 300, LearningRate = 0.01f, Dropout = 0f, Seed = 5, DevFraction = 0.1 });
        Assert.True(result.Completed);

        var segmenter = new NeuralWordSegmenter((Tagger)pipeline.Components[0]);
        string path = Path.Combine(Path.GetTempPath(), "onenlp-segmenter-" + Guid.NewGuid().ToString("N") + ".onenlp");
        try
        {
            using var holder = new Pipeline(segmenter, Language.Chinese);
            PipelineFile.Save(holder, path);
            using Pipeline loaded = PipelineFile.Load(path);
            Assert.IsType<NeuralWordSegmenter>(loaded.Tokenizer);

            var random = new Random(99);
            int wrong = 0;
            for (int i = 0; i < 40; i++)
            {
                (string text, int[] starts) = Sentence(random, lexicon);
                Doc a = holder.Process(text);
                Doc b = loaded.Process(text);
                Assert.Equal(a.GetStarts().ToArray(), b.GetStarts().ToArray());
                Assert.Equal(a.GetLengths().ToArray(), b.GetLengths().ToArray());
                if (!a.GetStarts().ToArray().SequenceEqual(starts))
                {
                    wrong++;
                }
            }

            Assert.True(wrong <= 2, $"{wrong} of 40 sentences were cut differently from the gold words");
        }
        finally
        {
            File.Delete(path);
        }

    }

    [Fact]
    public void SegmenterNeverCrossesWhiteSpaceAndKeepsOffsets()
    {
        string[] lexicon = ["我们", "喜欢", "学习"];
        List<Example> train = Examples(60, 2, lexicon);
        var encoder = new FastEncoder(width: 16, depth: 1, normRows: 128, prefixRows: 32, suffixRows: 32, shapeRows: 32, seed: 3);
        using Pipeline pipeline = Pipeline.Blank(Language.Chinese).Add(new Tagger(encoder, TokenColumn.Tag, NeuralWordSegmenter.HeadName) { OwnsEncoder = true });
        pipeline.Train(train, new TrainOptions { Epochs = 2, BatchWords = 200, LearningRate = 0.01f, Seed = 5 });
        var segmenter = new NeuralWordSegmenter((Tagger)pipeline.Components[0]);
        const string text = "我们 喜欢\n学习abc 123。";
        Doc doc = segmenter.Tokenize(text, new StringStore());
        Assert.True(doc.Length > 0);
        for (int t = 0; t < doc.Length; t++)
        {
            string word = text.Substring(doc[t].Idx, doc[t].EndIdx - doc[t].Idx);
            Assert.DoesNotContain(word, c => char.IsWhiteSpace(c));
        }

        Assert.Empty(segmenter.Tokenize("", new StringStore()));
        Assert.Empty(segmenter.Tokenize("  \n ", new StringStore()));
    }
}
