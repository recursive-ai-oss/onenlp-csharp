using System.Diagnostics;
using System.Globalization;
using System.Text;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;
using RecursiveAI.OneNlp;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;
using RecursiveAI.OneNlp.Neural.Training;
using RecursiveAI.OneNlp.Scoring;

namespace RecursiveAI.OneNlp.Recipes.Accurate;

// Accurate-tier recipe: one fine-tuned mmBERT-base encoder shared by the heads of one or more languages.
//
//   train --langs de,tr --data DIR --onnx mmbert-base.onnx --tokenizer tokenizer.json --out DIR
//         [--steps N] [--lr X] [--warmup N] [--batch-words N] [--sentences-per-doc N] [--seed N] [--threads N]
//         [--budget-seconds S] [--eval-every STEPS] [--checkpoint-every STEPS] [--heads tagger,morphologizer,lemmatizer,parser]
//
// Every language gets its own heads (tagger for XPOS, morphologizer for UPOS and UFeats, edit-tree lemmatizer, biaffine parser) trained
// only on that language's sentences (ScopedHead); the encoder sees every language. With one language this is plain per-language
// fine-tuning, so the same command measures the shared encoder against per-language ones. Adding a treebank means adding a row to
// Treebanks, not code. Run it again with the same arguments to continue (bit-exact with an uninterrupted run).
internal static class Program
{
    // code -> directory under DATA/train/ud (and DATA/eval/ud) and the file prefix of the gating treebank.
    private static readonly Dictionary<string, (string Dir, string Prefix)> Treebanks = new(StringComparer.Ordinal)
    {
        ["en"] = ("ud-en-ewt-r2.18", "en_ewt-ud"),
        ["de"] = ("de", "de_gsd-ud"),
        ["fr"] = ("fr", "fr_gsd-ud"),
        ["es"] = ("es", "es_ancora-ud"),
        ["pt"] = ("pt", "pt_bosque-ud"),
        ["it"] = ("it", "it_gating-ud"),
        ["nl"] = ("nl", "nl_alpino-ud"),
        ["tr"] = ("tr", "tr_boun-ud"),
        ["zh"] = ("zh", "zh_gsdsimp-ud"),
    };

    private static readonly Stopwatch Wall = Stopwatch.StartNew();

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || args[0] != "train")
            {
                Console.Error.WriteLine("usage: train ... (see the comment at the top of Program.cs)");
                return 2;
            }

            if (Opt(args, "threads", string.Empty).Length > 0)
            {
                TensorCompute.Threads = Num(args, "threads", 8);
            }

            return Train(args);
        }
        catch (Exception e) when (e is ArgumentException or IOException or DataFormatException or InvalidDataException or FormatException or KeyNotFoundException)
        {
            Console.Error.WriteLine("error: " + e.Message);
            return 1;
        }
    }

    private static string Opt(string[] args, string name, string fallback)
    {
        int i = Array.IndexOf(args, "--" + name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
    }

    private static string Required(string[] args, string name) =>
        Opt(args, name, string.Empty) is { Length: > 0 } v ? v : throw new ArgumentException($"--{name} is required.");

    private static int Num(string[] args, string name, int fallback) => int.Parse(Opt(args, name, fallback.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);

    private static float Flt(string[] args, string name, float fallback) => float.Parse(Opt(args, name, fallback.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);

    private static int Train(string[] args)
    {
        string[] langs = Required(args, "langs").Split(',');
        string data = Required(args, "data");
        string outDir = Required(args, "out");
        int perDoc = Num(args, "sentences-per-doc", 4);
        ulong seed = (ulong)Num(args, "seed", 1);
        long steps = Num(args, "steps", 500);
        float lr = Flt(args, "lr", 1e-4f);
        int warmup = Num(args, "warmup", 40);
        string[] heads = Opt(args, "heads", "tagger,morphologizer,lemmatizer,parser").Split(',');

        var train = new List<Example>();
        var dev = new List<Example>();
        // Each language owns one string table, shared by its train and dev documents and by the copies the trainer makes: that is how a
        // head knows which documents are its own.
        var stores = new Dictionary<string, StringStore>();
        foreach (string lang in langs)
        {
            (string dir, string prefix) = Treebanks[lang];
            string root = Path.Combine(data, "train", "ud", dir);
            if (!Directory.Exists(root))
            {
                root = Path.Combine(data, "train", dir);
            }

            var store = new StringStore();
            stores[lang] = store;
            int before = train.Count;
            train.AddRange(Documents(Path.Combine(root, prefix + "-train.conllu"), perDoc, store));
            int trainCount = train.Count - before;
            before = dev.Count;
            dev.AddRange(Documents(Path.Combine(root, prefix + "-dev.conllu"), perDoc, store));
            Console.WriteLine($"{lang}: train docs={trainCount} dev docs={dev.Count - before}");
        }

        // The examples of the languages are interleaved by the trainer's seeded shuffle; with one language this is a plain epoch order.
        using var encoder = TrainableTransformerEncoder.LoadOnnx(
            Required(args, "onnx"),
            Required(args, "tokenizer"),
            new TrainableTransformerOptions { Pooling = SubwordPooling.Mean, FreezeEmbeddings = true });
        using Pipeline pipeline = Pipeline.Blank(Language.English);
        var inner = new List<IDisposable>();
        foreach (string lang in langs)
        {
            StringStore mine = stores[lang];
            Func<Doc, bool> scope = d => ReferenceEquals(d.Strings, mine);
            foreach (string head in heads)
            {
                string name = head + "-" + lang;
                ITrainableComponent h = head switch
                {
                    "tagger" => new Tagger(encoder, TokenColumn.Tag, name),
                    "morphologizer" => new Morphologizer(encoder, name),
                    "lemmatizer" => new EditTreeLemmatizer(encoder, name),
                    "parser" => new Parser(encoder, arcSize: 512, labelSize: 256, contextLayers: 0, name: name),
                    _ => throw new ArgumentException("Unknown head " + head),
                };
                pipeline.Add(new ScopedHead(h, scope));
            }
        }

        Func<IReadOnlyList<Example>, double> score = examples =>
        {
            double total = 0;
            foreach (string lang in langs)
            {
                var mine = examples.Where(e => ReferenceEquals(e.Predicted.Strings, stores[lang])).ToList();
                var metrics = new List<string>();
                if (heads.Contains("morphologizer")) { metrics.Add("UPOS"); metrics.Add("UFeats"); }
                if (heads.Contains("tagger")) { metrics.Add("XPOS"); }
                if (heads.Contains("lemmatizer")) { metrics.Add("Lemmas"); }
                if (heads.Contains("parser")) { metrics.Add("LAS"); }
                var parts = new StringBuilder();
                double sum = 0;
                foreach (string m in metrics)
                {
                    double v = UdScorer.Score(mine.Select(e => e.Predicted), mine.Select(e => e.Reference), m).Value;
                    sum += v;
                    parts.Append(CultureInfo.InvariantCulture, $" {m}={v:F2}");
                }

                Console.WriteLine($"dev {lang}:{parts}");
                total += sum / metrics.Count;
            }

            return total / langs.Length;
        };

        double budget = Opt(args, "budget-seconds", string.Empty) is { Length: > 0 } b ? double.Parse(b, CultureInfo.InvariantCulture) : double.PositiveInfinity;
        using var cts = new CancellationTokenSource();
        if (!double.IsPositiveInfinity(budget))
        {
            cts.CancelAfter(TimeSpan.FromSeconds(budget));
        }

        int batchWords = Num(args, "batch-words", 400 * langs.Length);
        int logEvery = Num(args, "log-every", 5);
        var progress = new Progress(p =>
        {
            if (p.DevScore is { } s)
            {
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"step={p.Step} epoch={p.Epoch} loss={p.Loss:F4} dev={s:F3} best={p.IsBest} elapsed_s={Wall.Elapsed.TotalSeconds:F1}"));
            }
            else if (p.Step % logEvery == 0)
            {
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"step={p.Step} epoch={p.Epoch} loss={p.Loss:F4} lr={p.LearningRate:E2} elapsed_s={Wall.Elapsed.TotalSeconds:F1}"));
            }
        });

        TrainResult result = pipeline.Train(
            train,
            new TrainOptions
            {
                Epochs = 1000,
                MaxSteps = steps,
                BatchWords = batchWords,
                LearningRate = lr,
                WarmupSteps = warmup,
                Schedule = new WarmupCosineLr(lr, warmup, steps, 0.1f),
                Dropout = Flt(args, "dropout", 0.1f),
                Seed = seed,
                DevExamples = dev,
                Score = score,
                EvalEvery = Num(args, "eval-every", 100),
                Patience = 1000,
                CheckpointDirectory = outDir,
                CheckpointEvery = Num(args, "checkpoint-every", 10),
                Progress = progress,
            },
            cts.Token);

        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"completed={result.Completed} steps={result.Steps} epochs={result.Epochs} evaluations={result.Evaluations} best_dev={result.BestScore:F3} best_step={result.BestStep}"));
        Console.WriteLine($"weights_sha256={result.WeightsSha256}");
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"chunk_wall_s={Wall.Elapsed.TotalSeconds:F1} threads={TensorCompute.Threads}"));
        return 0;
    }

    // Groups consecutive sentences into documents of perDoc sentences, with every annotation column kept: the heads of one example batch
    // (tags, morphology, lemmas, parse) all read the same documents.
    private static List<Example> Documents(string path, int perDoc, StringStore strings)
    {
        var result = new List<Example>();
        var group = new List<ConllUSentence>();
        foreach (ConllUSentence s in ConllUReader.Read(path, strings))
        {
            group.Add(s);
            if (group.Count == perDoc)
            {
                result.Add(Example.FromReference(Merge(group, strings)));
                group.Clear();
            }
        }

        if (group.Count > 0)
        {
            result.Add(Example.FromReference(Merge(group, strings)));
        }

        return result;
    }

    private static Doc Merge(List<ConllUSentence> sentences, StringStore strings)
    {
        var text = new StringBuilder();
        var tokens = new List<TokenBoundary>();
        var heads = new List<int>();
        var marks = new List<SentenceBoundary>();
        TokenColumn[] columns = [TokenColumn.Lemma, TokenColumn.Pos, TokenColumn.Tag, TokenColumn.Dep, TokenColumn.Morph];
        var values = columns.Select(_ => new List<int>()).ToArray();
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
                marks.Add(t == 0 ? SentenceBoundary.Start : SentenceBoundary.Inside);
                for (int c = 0; c < columns.Length; c++)
                {
                    values[c].Add(d.GetColumn(columns[c])[t]);
                }
            }
        }

        var doc = new Doc(strings, text.ToString(), tokens.ToArray());
        doc.SetHeads(heads.ToArray());
        for (int c = 0; c < columns.Length; c++)
        {
            doc.SetColumn(columns[c], values[c].ToArray());
        }

        doc.SetSentenceBoundaries(marks.ToArray());
        return doc;
    }

    private sealed class Progress(Action<TrainProgress> handler) : IProgress<TrainProgress>
    {
        public void Report(TrainProgress value) => handler(value);
    }
}
