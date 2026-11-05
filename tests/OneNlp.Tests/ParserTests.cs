using System.Text;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;
using RecursiveAI.OneNlp.Neural.Training;

namespace RecursiveAI.OneNlp.Tests;

public class ParserTests
{
    // Brute force: the best tree over nodes 0..n with exactly one arc leaving node 0, by trying every head vector.
    private static double BestScoreByEnumeration(float[,] s)
    {
        int n = s.GetLength(0) - 1;
        var heads = new int[n + 1];
        double best = double.NegativeInfinity;

        void Walk(int d)
        {
            if (d > n)
            {
                if (IsSingleRootTree(heads))
                {
                    double total = 0;
                    for (int i = 1; i <= n; i++)
                    {
                        total += s[heads[i], i];
                    }

                    best = Math.Max(best, total);
                }

                return;
            }

            for (int h = 0; h <= n; h++)
            {
                if (h != d)
                {
                    heads[d] = h;
                    Walk(d + 1);
                }
            }
        }

        Walk(1);
        return best;
    }

    private static bool IsSingleRootTree(int[] heads)
    {
        int roots = 0;
        for (int d = 1; d < heads.Length; d++)
        {
            roots += heads[d] == 0 ? 1 : 0;
            int v = d;
            for (int steps = 0; v != 0; steps++)
            {
                if (steps > heads.Length)
                {
                    return false;
                }

                v = heads[v];
            }
        }

        return roots == 1;
    }

    [Fact]
    public void SpanningTreeMatchesEnumerationOnRandomScores()
    {
        var random = new Random(5);
        for (int trial = 0; trial < 300; trial++)
        {
            int n = random.Next(1, 7);
            var s = new float[n + 1, n + 1];
            for (int h = 0; h <= n; h++)
            {
                for (int d = 0; d <= n; d++)
                {
                    s[h, d] = (float)((random.NextDouble() * 10) - 5);
                }
            }

            int[] tree = MaximumSpanningTree.Decode(s);
            Assert.True(IsSingleRootTree(tree), $"trial {trial}: not a single-root tree");
            double got = 0;
            for (int d = 1; d <= n; d++)
            {
                got += s[tree[d], d];
            }

            Assert.Equal(BestScoreByEnumeration(s), got, 3);
        }
    }

    // Toy treebank: each word's head is the word before it (the first is the root); the word type decides the relation.
    private static List<Example> Toy(int sentences, int seed)
    {
        var random = new Random(seed);
        var all = new List<ConllUSentence>();
        for (int s = 0; s < sentences; s++)
        {
            var sb = new StringBuilder();
            int n = random.Next(4, 10);
            for (int i = 0; i < n; i++)
            {
                int w = random.Next(30);
                sb.Append(i + 1).Append("\tw").Append(w).Append("\tw").Append(w).Append("\tX\tX\t_\t").Append(i).Append('\t')
                  .Append(i == 0 ? "root" : "r" + (w % 3)).Append("\t_\t_\n");
            }

            sb.Append('\n');
            all.AddRange(ConllUReader.Read(new StringReader(sb.ToString())));
        }

        return [.. all.Select(x => Example.FromReference(x.Doc))];
    }

    [Fact]
    public void LearnsAToyTreebankAndSurvivesSaveAndLoad()
    {
        List<Example> train = Toy(300, 1);
        List<Example> dev = Toy(60, 2);
        using Pipeline pipeline = Pipeline.Blank().Add(new Parser(new HashEmbeddingEncoder(32, 512, seed: 1), arcSize: 32, labelSize: 16, contextLayers: 2) { OwnsEncoder = true });
        TrainResult result = pipeline.Train(train, new TrainOptions
        {
            Epochs = 12,
            BatchWords = 200,
            LearningRate = 0.01f,
            Dropout = 0.0f,
            Seed = 3,
            DevExamples = dev,
            Score = DevScores.Ud("LAS"),
        });
        Assert.True(result.BestScore > 90, $"dev LAS {result.BestScore}");

        string path = Path.Combine(Path.GetTempPath(), "onenlp-parser-" + Guid.NewGuid().ToString("N") + ".onenlp");
        try
        {
            PipelineFile.Save(pipeline, path);
            using Pipeline loaded = PipelineFile.Load(path);
            foreach (Example e in dev.Take(20))
            {
                Doc a = pipeline.Process(e.Reference.Text);
                Doc b = loaded.Process(e.Reference.Text);
                Assert.Equal(a.GetHeads().ToArray(), b.GetHeads().ToArray());
                Assert.Equal(
                    a.GetColumn(TokenColumn.Dep).ToArray().Select(x => a.Strings[x]),
                    b.GetColumn(TokenColumn.Dep).ToArray().Select(x => b.Strings[x]));
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RecurrentContextLearnsAndDoesNotDependOnTheBatch()
    {
        List<Example> train = Toy(300, 1);
        List<Example> dev = Toy(60, 2);
        using Pipeline pipeline = Pipeline.Blank().Add(new Parser(new HashEmbeddingEncoder(32, 512, seed: 1), arcSize: 32, labelSize: 16, contextLayers: 0, recurrentSize: 16) { OwnsEncoder = true });
        TrainResult result = pipeline.Train(train, new TrainOptions
        {
            Epochs = 12,
            BatchWords = 200,
            LearningRate = 0.01f,
            Dropout = 0.0f,
            Seed = 3,
            DevExamples = dev,
            Score = DevScores.Ud("LAS"),
        });
        Assert.True(result.BestScore > 90, $"dev LAS {result.BestScore}");

        string[] texts = [.. dev.Take(12).Select(e => e.Reference.Text)];
        Doc[] together = [.. pipeline.ProcessMany(texts, batchSize: 12)];
        for (int i = 0; i < texts.Length; i++)
        {
            Doc alone = pipeline.Process(texts[i]);
            Assert.Equal(alone.GetHeads().ToArray(), together[i].GetHeads().ToArray());
        }
    }

    [Fact]
    public void PredictsSentenceBoundariesInRunningText()
    {
        // Six sentences per document. In this toy language a sentence always begins with word type 0 and no other position holds one.
        var random = new Random(9);
        var docs = new List<Example>();
        for (int d = 0; d < 60; d++)
        {
            var sb = new StringBuilder();
            for (int s = 0; s < 6; s++)
            {
                int n = random.Next(3, 8);
                for (int i = 0; i < n; i++)
                {
                    int w = i == 0 ? 0 : 1 + random.Next(20);
                    sb.Append(i + 1).Append("\tw").Append(w).Append("\tw").Append(w).Append("\tX\tX\t_\t").Append(i).Append('\t')
                      .Append(i == 0 ? "root" : "dep").Append("\t_\t_\n");
                }

                sb.Append('\n');
            }

            docs.Add(Example.FromReference(Merge([.. ConllUReader.Read(new StringReader(sb.ToString()))])));
        }

        using Pipeline pipeline = Pipeline.Blank().Add(new Parser(new HashEmbeddingEncoder(32, 512, seed: 1), arcSize: 16, labelSize: 8, contextLayers: 1) { OwnsEncoder = true });
        pipeline.Train(docs, new TrainOptions { Epochs = 10, BatchWords = 150, LearningRate = 0.01f, Dropout = 0f, Seed = 1, DevFraction = 0 });
        int right = 0;
        int total = 0;
        foreach (Example e in docs.Take(10))
        {
            Doc predicted = Example.Blank(e.Reference);
            pipeline.Components[0].Process(predicted);
            ReadOnlySpan<SentenceBoundary> want = e.Reference.GetSentenceBoundaries();
            ReadOnlySpan<SentenceBoundary> got = predicted.GetSentenceBoundaries();
            for (int i = 0; i < want.Length; i++)
            {
                total++;
                right += want[i] == got[i] ? 1 : 0;
            }
        }

        Assert.True((double)right / total > 0.97, $"boundary accuracy {(double)right / total}");
    }

    private static Doc Merge(List<ConllUSentence> sentences)
    {
        var text = new StringBuilder();
        var tokens = new List<TokenBoundary>();
        var heads = new List<int>();
        var deps = new List<int>();
        var marks = new List<SentenceBoundary>();
        StringStore strings = sentences[0].Doc.Strings;
        foreach (ConllUSentence s in sentences)
        {
            if (text.Length > 0)
            {
                text.Append(' ');
            }

            int charOffset = text.Length;
            int tokenOffset = tokens.Count;
            Doc d = s.Doc;
            text.Append(d.Text);
            for (int t = 0; t < d.Length; t++)
            {
                tokens.Add(new TokenBoundary(charOffset + d.GetStarts()[t], d.GetLengths()[t]));
                heads.Add(tokenOffset + d.GetHeads()[t]);
                deps.Add(d.GetColumn(TokenColumn.Dep)[t]);
                marks.Add(t == 0 ? SentenceBoundary.Start : SentenceBoundary.Inside);
            }
        }

        var doc = new Doc(strings, text.ToString(), [.. tokens]);
        doc.SetHeads([.. heads]);
        doc.SetColumn(TokenColumn.Dep, [.. deps]);
        doc.SetSentenceBoundaries([.. marks]);
        return doc;
    }
}
