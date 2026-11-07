using System.Diagnostics;
using System.Globalization;
using System.Text;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneNlp;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;

namespace RecursiveAI.OneNlp.Toy;

// A small driver for checking the training spine end to end:
//   gen <dir> [seed]                     writes a synthetic tagging corpus as CoNLL-U (train, dev, test)
//   roundtrip <file.conllu>              reads a CoNLL-U file and writes it back, comparing the bytes
//   train <train> <dev> <out-dir> [epochs] [checkpoint-every] [threads]
//                                        trains a tagger with Train, checkpointing to <out-dir>, prints key=value lines
//   eval <model> <test>                  loads a saved pipeline and prints its tagging accuracy
// The tagging task is deliberately easy (a word's tag is fixed by the word), so convergence and resume are the point, not accuracy.
internal static class Program
{
    private const int Words = 1500;
    private const int Tags = 12;
    private const int Width = 64;
    private const int Buckets = 16384;

    private static int Main(string[] args)
    {
        try
        {
            switch (args.Length > 0 ? args[0] : string.Empty)
            {
                case "gen" when args.Length >= 2:
                    Generate(args[1], args.Length > 2 ? ulong.Parse(args[2], CultureInfo.InvariantCulture) : 1);
                    return 0;
                case "roundtrip" when args.Length == 2:
                    return RoundTrip(args[1]);
                case "train" when args.Length >= 4:
                    return Train(args);
                case "eval" when args.Length == 3:
                    return Eval(args[1], args[2]);
                default:
                    Console.Error.WriteLine("usage: gen <dir> [seed] | roundtrip <file> | train <train> <dev> <out-dir> [epochs] [checkpoint-every] [threads] | eval <model> <test>");
                    return 2;
            }
        }
        catch (DataFormatException e)
        {
            Console.Error.WriteLine("data error: " + e.Message);
            return 1;
        }
    }

    private static void Generate(string dir, ulong seed)
    {
        Directory.CreateDirectory(dir);
        WriteCorpus(Path.Combine(dir, "toy-train.conllu"), 6000, seed);
        WriteCorpus(Path.Combine(dir, "toy-dev.conllu"), 500, seed + 1);
        WriteCorpus(Path.Combine(dir, "toy-test.conllu"), 500, seed + 2);
        Console.WriteLine("wrote " + dir);
    }

    private static void WriteCorpus(string path, int sentences, ulong seed)
    {
        // SplitMix64, so the corpus does not depend on the runtime's Random.
        ulong state = seed * 0x9E3779B97F4A7C15UL;
        ulong Next()
        {
            ulong z = state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        var sb = new StringBuilder();
        for (int s = 0; s < sentences; s++)
        {
            int n = 5 + (int)(Next() % 16);
            sb.Append("# sent_id = toy-").Append(seed).Append('-').Append(s).Append('\n');
            for (int i = 0; i < n; i++)
            {
                int w = (int)(Next() % Words);
                string misc = i == n - 1 ? "SpaceAfter=No" : "_";
                sb.Append(i + 1).Append("\tw").Append(w).Append("\tw").Append(w).Append("\tX\tT").Append(w % Tags)
                  .Append("\t_\t").Append(i == 0 ? 0 : 1).Append('\t').Append(i == 0 ? "root" : "dep").Append("\t_\t").Append(misc).Append('\n');
            }

            sb.Append('\n');
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    private static int RoundTrip(string path)
    {
        string original = File.ReadAllText(path, new UTF8Encoding(false, true));
        var rebuilt = new StringBuilder(original.Length);
        int sentences = 0, words = 0, mwts = 0, empties = 0, textMismatches = 0;
        using (var writer = new StringWriter(rebuilt))
        {
            writer.NewLine = "\n";
            foreach (ConllUSentence s in ConllUReader.Read(path))
            {
                sentences++;
                words += s.Doc.Length;
                mwts += s.MultiwordTokens.Count;
                empties += s.EmptyNodes.Count;
                if (s.GetComment("text") is { } commentText && commentText != s.Doc.Text)
                {
                    textMismatches++;
                }

                ConllUWriter.Write(writer, s);
            }
        }

        string result = rebuilt.ToString();
        Console.WriteLine($"sentences={sentences} words={words} multiword_tokens={mwts} empty_nodes={empties} text_comment_mismatches={textMismatches}");
        if (result == original)
        {
            Console.WriteLine("roundtrip=identical");
            return 0;
        }

        int at = 0;
        while (at < result.Length && at < original.Length && result[at] == original[at])
        {
            at++;
        }

        int line = original.AsSpan(0, at).Count('\n') + 1;
        Console.WriteLine($"roundtrip=DIFFERENT first difference at line {line}");
        return 1;
    }

    private static Pipeline NewPipeline() =>
        Pipeline.Blank(Language.English).Add(new Tagger(new HashEmbeddingEncoder(Width, Buckets, seed: 1)) { OwnsEncoder = true });

    private static int Train(string[] args)
    {
        int epochs = args.Length > 4 ? int.Parse(args[4], CultureInfo.InvariantCulture) : 10;
        int checkpointEvery = args.Length > 5 ? int.Parse(args[5], CultureInfo.InvariantCulture) : 50;
        if (args.Length > 6)
        {
            TensorCompute.Threads = int.Parse(args[6], CultureInfo.InvariantCulture);
        }

        List<Example> train = [.. ConllUReader.ReadExamples(args[1])];
        List<Example> dev = [.. ConllUReader.ReadExamples(args[2])];
        using Pipeline pipeline = NewPipeline();

        var clock = Stopwatch.StartNew();
        long lastReported = 0;
        var progress = new ProgressHandler(p =>
        {
            if (p.DevScore is { } score)
            {
                Console.WriteLine($"step={p.Step} epoch={p.Epoch} loss={p.Loss.ToString("F4", CultureInfo.InvariantCulture)} dev_score={score.ToString("F5", CultureInfo.InvariantCulture)} best={p.IsBest} elapsed_s={clock.Elapsed.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture)}");
            }

            lastReported = p.Step;
        });

        TrainResult result = pipeline.Train(train, new TrainOptions
        {
            Epochs = epochs,
            BatchWords = 1000,
            LearningRate = 0.003f,
            Dropout = 0.1f,
            Seed = 11,
            DevExamples = dev,
            Score = DevScores.Ud("XPOS"),
            Patience = 3,
            CheckpointDirectory = args[3],
            CheckpointEvery = checkpointEvery,
            Progress = progress,
        });

        Console.WriteLine($"steps={result.Steps} epochs={result.Epochs} completed={result.Completed} evaluations={result.Evaluations} best_score={result.BestScore.ToString("F5", CultureInfo.InvariantCulture)} best_step={result.BestStep}");
        Console.WriteLine($"weights_sha256={result.WeightsSha256}");
        Console.WriteLine($"train_wall_s={clock.Elapsed.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture)} threads={TensorCompute.Threads} last_step={lastReported}");
        PipelineFile.Save(pipeline, Path.Combine(args[3], "model.onenlp"));
        return 0;
    }

    private static int Eval(string modelPath, string testPath)
    {
        using Pipeline pipeline = PipelineFile.Load(modelPath);
        int right = 0, total = 0;
        foreach (ConllUSentence s in ConllUReader.Read(testPath))
        {
            Doc predicted = pipeline.Process(s.Doc.Text);
            for (int i = 0; i < predicted.Length; i++)
            {
                total++;
                if (predicted[i].Tag == s.Doc[i].Tag)
                {
                    right++;
                }
            }
        }

        Console.WriteLine($"tag_accuracy={((double)right / total).ToString("F5", CultureInfo.InvariantCulture)} tokens={total}");
        return 0;
    }

    private sealed class ProgressHandler(Action<TrainProgress> handler) : IProgress<TrainProgress>
    {
        public void Report(TrainProgress value) => handler(value);
    }
}
