using System.Text;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;

namespace RecursiveAI.OneNlp.Tests;

internal sealed class SyncProgress<T>(Action<T> handler) : IProgress<T>
{
    public void Report(T value) => handler(value);
}

public class TrainingTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "onenlp-train-" + Guid.NewGuid().ToString("N"));

    public TrainingTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
        GC.SuppressFinalize(this);
    }

    // A toy language: 60 word types, each word has one fixed tag out of 7. The tag is a function of the word alone.
    internal static List<Example> ToyExamples(int sentences, int seed)
    {
        var random = new Random(seed);
        var sb = new StringBuilder();
        for (int s = 0; s < sentences; s++)
        {
            int n = random.Next(4, 11);
            for (int i = 0; i < n; i++)
            {
                int w = random.Next(60);
                sb.Append(i + 1).Append("\tw").Append(w).Append("\tw").Append(w).Append("\tX\tT").Append(w % 7)
                  .Append("\t_\t").Append(i == 0 ? 0 : 1).Append('\t').Append(i == 0 ? "root" : "dep").Append("\t_\t_\n");
            }

            sb.Append('\n');
        }

        return [.. ConllUReader.Read(new StringReader(sb.ToString())).Select(x => Example.FromReference(x.Doc))];
    }

    private static Pipeline NewPipeline(ulong encoderSeed = 1)
    {
        var encoder = new HashEmbeddingEncoder(width: 32, buckets: 1024, seed: encoderSeed);
        return Pipeline.Blank().Add(new Tagger(encoder) { OwnsEncoder = true });
    }

    private static double TagAccuracy(Pipeline pipeline, List<Example> gold)
    {
        int right = 0, total = 0;
        foreach (Example e in gold)
        {
            Doc d = pipeline.Process(e.Reference.Text);
            for (int i = 0; i < d.Length; i++)
            {
                total++;
                if (d[i].Tag == e.Reference[i].Tag)
                {
                    right++;
                }
            }
        }

        return (double)right / total;
    }

    private static TrainOptions Options(string? dir = null, int checkpointEvery = 0, IProgress<TrainProgress>? progress = null) =>
        new()
        {
            Epochs = 8,
            BatchWords = 120,
            LearningRate = 0.02f,
            Dropout = 0.1f,
            Seed = 7,
            CheckpointDirectory = dir,
            CheckpointEvery = checkpointEvery,
            Progress = progress,
        };

    [Fact]
    public void TheTaggerLearnsTheToyLanguage()
    {
        List<Example> train = ToyExamples(150, 1);
        List<Example> test = ToyExamples(40, 2);
        using Pipeline pipeline = NewPipeline();
        var scores = new List<double>();
        TrainResult result = pipeline.Train(train, Options(progress: new SyncProgress<TrainProgress>(p =>
        {
            if (p.DevScore is { } s)
            {
                scores.Add(s);
            }
        })));
        Assert.True(result.Completed);
        Assert.Equal(8, result.Epochs);
        Assert.Equal(8, result.Evaluations);
        Assert.Equal(8, scores.Count);
        Assert.True(scores[^1] > scores[0], "dev score (negative loss) improves");
        Assert.True(TagAccuracy(pipeline, test) > 0.97);
        Assert.Equal(7, ((Tagger)pipeline.Components[0]).Labels.Count);
    }

    [Fact]
    public void ATrainedPipelineOnlyNeedsTheSameSeedToTrainTheSameWay()
    {
        List<Example> train = ToyExamples(60, 1);
        using Pipeline a = NewPipeline();
        using Pipeline b = NewPipeline();
        Assert.Equal(a.Train(train, Options()).WeightsSha256, b.Train(train, Options()).WeightsSha256);
        using Pipeline c = NewPipeline();
        Assert.NotEqual(a.Train(train, Options()).WeightsSha256, c.Train(train, new TrainOptions { Epochs = 8, BatchWords = 120, LearningRate = 0.02f, Seed = 8 }).WeightsSha256);
    }

    [Fact]
    public void ResumingAKilledRunGivesTheUninterruptedWeights()
    {
        List<Example> train = ToyExamples(80, 3);
        string whole = Path.Combine(_dir, "whole");
        string killed = Path.Combine(_dir, "killed");

        using Pipeline reference = NewPipeline();
        TrainResult uninterrupted = reference.Train(train, Options(whole, checkpointEvery: 4));

        // "Kill" the run after step 13: the token is cancelled from the progress callback, so the run stops without writing a
        // checkpoint for steps 13 and 14 (the last one was written at step 12).
        using var cts = new CancellationTokenSource();
        using Pipeline first = NewPipeline();
        TrainResult partial = first.Train(train, Options(killed, 4, new SyncProgress<TrainProgress>(p =>
        {
            if (p.Step == 13)
            {
                cts.Cancel();
            }
        })), cts.Token);
        Assert.False(partial.Completed);
        Assert.Equal(13, partial.Steps);

        using Pipeline second = NewPipeline();
        TrainResult resumed = second.Train(train, Options(killed, 4));
        Assert.True(resumed.Completed);
        Assert.Equal(uninterrupted.Steps, resumed.Steps);
        Assert.Equal(uninterrupted.WeightsSha256, resumed.WeightsSha256);
        Assert.Equal(uninterrupted.BestScore, resumed.BestScore);
        Assert.Equal(uninterrupted.BestStep, resumed.BestStep);
        Assert.Equal(File.ReadAllBytes(Path.Combine(whole, "last.ckpt")), File.ReadAllBytes(Path.Combine(killed, "last.ckpt")));
        Assert.Equal(File.ReadAllBytes(Path.Combine(whole, "best.ckpt")), File.ReadAllBytes(Path.Combine(killed, "best.ckpt")));

        // Running again on a finished checkpoint changes nothing.
        using Pipeline third = NewPipeline();
        Assert.Equal(uninterrupted.WeightsSha256, third.Train(train, Options(killed, 4)).WeightsSha256);
    }

    [Fact]
    public void ACheckpointForOtherDataIsRefused()
    {
        string dir = Path.Combine(_dir, "ckpt");
        using Pipeline a = NewPipeline();
        a.Train(ToyExamples(40, 1), Options(dir));
        using Pipeline b = NewPipeline();
        Assert.Throws<InvalidOperationException>(() => b.Train(ToyExamples(41, 1), Options(dir)));
    }

    [Fact]
    public void EarlyStoppingEndsTheRunAndKeepsTheBestWeights()
    {
        List<Example> train = ToyExamples(60, 1);
        var scores = new Queue<double>([1, 2, 1.5, 1.4, 1.3, 9, 9, 9, 9, 9]);
        using Pipeline pipeline = NewPipeline();
        TrainResult result = pipeline.Train(train, new TrainOptions
        {
            Epochs = 10,
            BatchWords = 120,
            Seed = 1,
            Patience = 3,
            Score = _ => scores.Dequeue(),
        });
        Assert.True(result.StoppedEarly);
        Assert.Equal(5, result.Evaluations);
        Assert.Equal(2, result.BestScore);
        Assert.InRange(result.BestStep, 1, result.Steps - 3);
    }

    [Fact]
    public void ScoringReceivesPredictionsMadeByThePipeline()
    {
        List<Example> train = ToyExamples(60, 1);
        double last = 0;
        using Pipeline pipeline = NewPipeline();
        pipeline.Train(train, new TrainOptions
        {
            Epochs = 6,
            BatchWords = 120,
            LearningRate = 0.02f,
            Seed = 1,
            Score = dev =>
            {
                int right = 0, total = 0;
                foreach (Example e in dev)
                {
                    for (int i = 0; i < e.Predicted.Length; i++)
                    {
                        total++;
                        right += e.Predicted[i].Tag == e.Reference[i].Tag ? 1 : 0;
                    }
                }

                last = (double)right / total;
                return last;
            },
        });
        Assert.True(last > 0.9);
    }

    [Fact]
    public void TheUdScorerPlugsInAsTheDevScore()
    {
        using Pipeline pipeline = NewPipeline();
        var scores = new List<double>();
        pipeline.Train(ToyExamples(80, 1), new TrainOptions
        {
            Epochs = 6,
            BatchWords = 120,
            LearningRate = 0.02f,
            Seed = 1,
            Score = DevScores.Ud("XPOS"),
            Progress = new SyncProgress<TrainProgress>(p =>
            {
                if (p.DevScore is { } s)
                {
                    scores.Add(s);
                }
            }),
        });
        Assert.Equal(6, scores.Count);
        Assert.True(scores[^1] > 90 && scores[^1] <= 100, "XPOS accuracy is reported in points");
        Assert.Throws<ArgumentException>(() => DevScores.Ud("nope"));
    }

    [Fact]
    public void FrozenEncoderWeightsDoNotMove()
    {
        List<Example> train = ToyExamples(40, 1);
        using Pipeline pipeline = NewPipeline();
        var tagger = (Tagger)pipeline.Components[0];
        byte[] Snapshot()
        {
            using var ms = new MemoryStream();
            RecursiveAI.OneModel.Training.Checkpoint.Save(ms, tagger.Encoder.Weights!);
            return ms.ToArray();
        }

        byte[] before = Snapshot();
        pipeline.Train(train, new TrainOptions { Epochs = 2, BatchWords = 100, Seed = 1, FreezeEncoder = true });
        Assert.Equal(before, Snapshot());
    }

    [Fact]
    public void SavedPipelinesLoadAndPredictTheSame()
    {
        List<Example> train = ToyExamples(80, 1);
        using Pipeline trained = NewPipeline();
        trained.Train(train, Options());
        string path = Path.Combine(_dir, "model.onenlp");
        PipelineFile.Save(trained, path);

        using Pipeline loaded = PipelineFile.Load(path);
        Assert.Equal(trained.Language, loaded.Language);
        Assert.Equal(["tagger"], loaded.Components.Select(c => c.Name).ToArray());
        foreach (Example e in ToyExamples(20, 5))
        {
            Doc a = trained.Process(e.Reference.Text);
            Doc b = loaded.Process(e.Reference.Text);
            Assert.Equal(a.Select(t => t.Tag).ToArray(), b.Select(t => t.Tag).ToArray());
            Assert.Equal(a.GetConfidences().ToArray(), b.GetConfidences().ToArray());
        }

        // Saving what was loaded gives the same bytes.
        string again = Path.Combine(_dir, "again.onenlp");
        PipelineFile.Save(loaded, again);
        Assert.Equal(File.ReadAllBytes(path), File.ReadAllBytes(again));
    }

    [Fact]
    public void HostileSavedPipelinesAreRejectedWithInvalidData()
    {
        using Pipeline trained = NewPipeline();
        trained.Train(ToyExamples(30, 1), new TrainOptions { Epochs = 1, Seed = 1 });
        using var ms = new MemoryStream();
        PipelineFile.Save(trained, ms);
        byte[] good = ms.ToArray();

        void Rejects(byte[] data) => Assert.Throws<InvalidDataException>(() => PipelineFile.Load(new MemoryStream(data)).Dispose());

        Rejects([]);
        Rejects(good[..20]);
        Rejects(good[..^1]);
        Rejects([.. good, 0]);
        byte[] badMagic = (byte[])good.Clone();
        badMagic[0] ^= 1;
        Rejects(badMagic);
        byte[] badVersion = (byte[])good.Clone();
        badVersion[8] = 9;
        Rejects(badVersion);
        byte[] hugeConfig = (byte[])good.Clone();
        hugeConfig[23] = 0x7F;
        Rejects(hugeConfig);
        byte[] hugeWeights = (byte[])good.Clone();
        hugeWeights[31] = 0x7F;
        Rejects(hugeWeights);

        // Every single-byte change in the config region either loads or fails as invalid data, never another exception.
        int configLength = (int)BitConverter.ToUInt64(good, 16);
        for (int i = 32; i < 32 + configLength; i += 3)
        {
            byte[] mutated = (byte[])good.Clone();
            mutated[i] ^= 0x55;
            try
            {
                PipelineFile.Load(new MemoryStream(mutated)).Dispose();
            }
            catch (InvalidDataException)
            {
            }
        }
    }

    [Fact]
    public void SaveRefusesAPipelineWithoutWeights()
    {
        using Pipeline pipeline = NewPipeline();
        Assert.Throws<InvalidOperationException>(() => PipelineFile.Save(pipeline, new MemoryStream()));
        Assert.Throws<InvalidOperationException>(() => pipeline.Process("no weights"));
    }

    [Fact]
    public void AnEnglishPipelineKeepsItsTokenizerDataAndSentencizerThroughSaveAndLoad()
    {
        var encoder = new HashEmbeddingEncoder(width: 32, buckets: 1024, seed: 1);
        using Pipeline trained = Pipeline.Blank(Language.English).Add("sentencizer").Add(new Tagger(encoder) { OwnsEncoder = true });
        trained.Train(ToyExamples(30, 1), new TrainOptions { Epochs = 1, Seed = 1 });
        using var ms = new MemoryStream();
        PipelineFile.Save(trained, ms);
        using Pipeline loaded = PipelineFile.Load(new MemoryStream(ms.ToArray()));

        const string Text = "Mr. Smith doesn't know e-mail. He cannot go to the U.S. today!";
        Doc a = trained.Process(Text);
        Doc b = loaded.Process(Text);
        Assert.Equal(a.Select(t => t.ToString()).ToArray(), b.Select(t => t.ToString()).ToArray());
        Assert.Contains("n't", a.Select(t => t.ToString()));
        Assert.Equal(a.Sentences.Select(x => x.Text).ToArray(), b.Sentences.Select(x => x.Text).ToArray());
        Assert.True(b.Sentences.Count >= 2, string.Join(" | ", b.Sentences.Select(x => x.Text)));
        Assert.Equal(Pipeline.Blank(Language.English).Add("sentencizer").Process(Text).Sentences.Count, b.Sentences.Count);
        Assert.Equal(["sentencizer", "tagger"], loaded.Components.Select(c => c.Name).ToArray());

        // A pipeline with other data keeps that data.
        using Pipeline neutral = Pipeline.Blank(Language.German).Add("sentencizer");
        using var ms2 = new MemoryStream();
        PipelineFile.Save(neutral, ms2);
        using Pipeline neutralLoaded = PipelineFile.Load(new MemoryStream(ms2.ToArray()));
        Assert.Equal(neutral.Process(Text).Select(t => t.ToString()).ToArray(), neutralLoaded.Process(Text).Select(t => t.ToString()).ToArray());
        Assert.Equal(Language.German, neutralLoaded.Language);
    }

    [Fact]
    public void ComponentsFollowTheRegistryWhenSaved()
    {
        using Pipeline pipeline = NewPipeline();
        pipeline.Train(ToyExamples(20, 1), new TrainOptions { Epochs = 1, Seed = 1 });
        pipeline.Add("norm");
        using var ms = new MemoryStream();
        PipelineFile.Save(pipeline, ms);
        using Pipeline loaded = PipelineFile.Load(new MemoryStream(ms.ToArray()));
        Assert.Equal(["tagger", "norm"], loaded.Components.Select(c => c.Name).ToArray());
    }
}
