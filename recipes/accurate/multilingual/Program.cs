using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;
using RecursiveAI.OneNlp;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;
using RecursiveAI.OneNlp.Scoring;
using RecursiveAI.OneNlp.Segmentation;

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
internal static partial class Program
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

    // code -> (train/dev directory under DATA/train, file prefix, test directory under DATA/eval). Entity corpora: UNER where it exists
    // (en, pt, zh), otherwise GermEval 2014 (de), WikiNER (es, fr, it, nl) and Turkish-WikiNER (tr).
    private static readonly Dictionary<string, (string Dir, string Prefix, string EvalDir)> NerSets = new(StringComparer.Ordinal)
    {
        ["en"] = ("uner-en-ewt-8ed072de", "en_ewt-ud", "uner-en-ewt-8ed072de"),
        ["de"] = ("ner/de/germeval-2014", "germeval-2014", "ner/de/germeval-2014"),
        ["es"] = ("ner/es/wikiner-es-wp3", "wikiner-es-wp3", "ner/es/wikiner-es-wp3"),
        ["fr"] = ("ner/fr/wikiner-fr-wp3", "wikiner-fr-wp3", "ner/fr/wikiner-fr-wp3"),
        ["it"] = ("ner/it/wikiner-it-wp3", "wikiner-it-wp3", "ner/it/wikiner-it-wp3"),
        ["nl"] = ("ner/nl/wikiner-nl-wp3", "wikiner-nl-wp3", "ner/nl/wikiner-nl-wp3"),
        ["tr"] = ("ner/tr/turkish-wikiner", "turkish-wikiner", "ner/tr/turkish-wikiner"),
        ["pt"] = ("uner/pt", "pt_bosque-ud", "uner/pt"),
        ["zh"] = ("uner/zh", "zh_gsdsimp-ud", "uner/zh"),
    };

    // For smoke runs: score and write at most this many sentences per file (0 = all).
    private static int predictLimit;
    private static int predictOffset;
    private static int nerTestLimit;

    private static readonly Stopwatch Wall = Stopwatch.StartNew();

    // A trained Chinese word segmenter (--zh-segmenter FILE, a saved pipeline whose tokenizer is a NeuralWordSegmenter): Chinese text has no
    // spaces, so raw-text rows and the packaged Chinese pipeline cut words with it instead of the Unicode word rules.
    private static Pipeline? segmenterSource;

    // --spec FILE: the corpus description every command reads its languages and files from (see recipes/accurate/common).
    private static CorpusSpec? corpus;

    // --segmenter-dir DIR: DIR/<code>/model.onenlp for each language whose text is not separated by spaces (tools/accurate/train_segmenters.sh).
    private static readonly Dictionary<string, Pipeline> Segmenters = new(StringComparer.Ordinal);

    private static IWordTokenizer TokenizerFor(Language language) =>
        Segmenters.TryGetValue(language.Code, out Pipeline? own) ? own.Tokenizer
        : language.Code == "zh" && segmenterSource is not null ? segmenterSource.Tokenizer : UaxWordTokenizer.For(language);

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length > 0 && args[0] == "recover-parser")
            {
                TensorCompute.Threads = Num(args, "threads", 4);
                return RecoverParser(args);
            }

            if (args.Length > 0 && args[0] == "plan")
            {
                return PlanOnly(args);
            }

            if (args.Length > 0 && args[0] == "encoder-parity")
            {
                TensorCompute.Threads = Num(args, "threads", 2);
                LoadSegmenter(args);
                return EncoderParity(args);
            }

            if (args.Length > 0 && args[0] == "verify-export")
            {
                if (Opt(args, "threads", string.Empty).Length > 0)
                {
                    TensorCompute.Threads = Num(args, "threads", 8);
                }

                LoadSegmenter(args);
                return VerifyExport(args);
            }

            if (args.Length > 0 && args[0] == "predict-package")
            {
                if (Opt(args, "threads", string.Empty).Length > 0)
                {
                    TensorCompute.Threads = Num(args, "threads", 8);
                }

                LoadSegmenter(args);
                return PredictPackage(args);
            }

            if (args.Length == 0 || args[0] != "train")
            {
                Console.Error.WriteLine("usage: train ... (see the comment at the top of Program.cs)");
                return 2;
            }

            if (Opt(args, "threads", string.Empty).Length > 0)
            {
                TensorCompute.Threads = Num(args, "threads", 8);
            }

            LoadSegmenter(args);
            return Train(args);
        }
        catch (Exception e) when (e is ArgumentException or IOException or DataFormatException or InvalidDataException or FormatException or KeyNotFoundException or InvalidOperationException)
        {
            Console.Error.WriteLine("error: " + e.Message);
            return 1;
        }
    }

    private static void LoadSegmenter(string[] args)
    {
        predictOffset = Num(args, "predict-offset", 0);
        ArgumentOutOfRangeException.ThrowIfNegative(predictOffset);
        if (Opt(args, "zh-segmenter", string.Empty) is { Length: > 0 } path)
        {
            segmenterSource = PipelineFile.Load(path);
        }

        if (Opt(args, "spec", string.Empty) is { Length: > 0 } specPath)
        {
            corpus = CorpusSpec.Load(specPath);
        }

        if (Opt(args, "segmenter-dir", string.Empty) is { Length: > 0 } dir)
        {
            foreach (string sub in Directory.GetDirectories(dir))
            {
                string file = Path.Combine(sub, "model.onenlp");
                if (File.Exists(file))
                {
                    Segmenters[Path.GetFileName(sub)] = PipelineFile.Load(file);
                }
            }
        }
    }

    // plan --spec FILE --data DIR [--langs a,b | all] [--plan ALPHA] [--sentences-per-doc N]: reads each language's data and prints the sampling plan and
    // the --repeat argument it comes to, without a model. The same numbers the training run prints at its start.
    private static int PlanOnly(string[] args)
    {
        CorpusSpec spec = CorpusSpec.Load(Required(args, "spec"));
        string[] langs = Opt(args, "langs", "all") is "all" ? [.. spec.Languages.Select(l => l.Code)] : Opt(args, "langs", string.Empty).Split(',');
        var words = new Dictionary<string, long>();
        foreach (string lang in langs)
        {
            LanguageData d = CorpusReader.ReadUd(spec, spec[lang], Required(args, "data"), Num(args, "sentences-per-doc", 4));
            words[lang] = d.Train.Sum(e => (long)e.Predicted.Length);
            Console.WriteLine($"{lang}: train docs={d.Train.Count} dev docs={d.Dev.Count} held out={d.HeldOut} dropped={d.Dropped}");
        }

        IReadOnlyList<LanguagePlan> plan = SamplingPlan.Compute(spec, langs, l => words[l], PlanOptions(args, Opt(args, "plan", "0.3")));
        Console.Write(SamplingPlan.Describe(plan));
        Console.WriteLine("--repeat " + SamplingPlan.ToRepeatArgument(plan));
        return 0;
    }

    private static SamplingOptions PlanOptions(string[] args, string alpha)
    {
        var boost = new Dictionary<string, double>();
        foreach (string kv in Opt(args, "boost", string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = kv.Split('=');
            boost[parts[0]] = double.Parse(parts[1], CultureInfo.InvariantCulture);
        }

        return new SamplingOptions(
            Alpha: double.Parse(alpha, CultureInfo.InvariantCulture),
            MaxRepeat: Num(args, "max-repeat", 6),
            SmallMaxRepeat: Num(args, "small-max-repeat", 8),
            ParserWeight: Flt(args, "parser-weight", 1.5f),
            Boost: boost);
    }

    // Replaces the examples that satisfy mine by this many copies of their distinct set; a fraction takes evenly spaced ones of the last copy.
    private static void Repeat(List<Example> train, Func<Example, bool> mine, double times)
    {
        var distinct = new List<Example>(train.Where(mine).Distinct());
        train.RemoveAll(e => mine(e));
        int whole = (int)Math.Floor(times);
        for (int r = 0; r < whole; r++)
        {
            train.AddRange(distinct);
        }

        double fraction = times - whole;
        for (int i = 0; i < distinct.Count; i++)
        {
            if (Math.Floor((i + 1) * fraction) > Math.Floor(i * fraction))
            {
                train.Add(distinct[i]);
            }
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
        string[] heads = Opt(args, "heads", "tagger,morphologizer,lemmatizer,parser,ner").Split(',');
        CorpusSpec? spec = corpus;
        string[] nerLangs = heads.Contains("ner") ? [.. langs.Where(l => spec is not null ? spec.Contains(l) && spec[l].Ner.Count > 0 : NerSets.ContainsKey(l))] : [];
        int balance = Num(args, "balance", 1);
        predictLimit = Num(args, "predict-limit", 0);
        nerTestLimit = Num(args, "ner-test", 3000);
        string[] predictLangs = Opt(args, "predict-langs", string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries);

        var train = new List<Example>();
        var dev = new List<Example>();
        // Each language owns one string table, shared by its train and dev documents and by the copies the trainer makes: that is how a
        // head knows which documents are its own.
        var stores = new Dictionary<string, StringStore>();
        foreach (string lang in langs)
        {
            int before = train.Count;
            if (spec is not null)
            {
                LanguageData data1 = CorpusReader.ReadUd(spec, spec[lang], data, perDoc);
                stores[lang] = data1.Store;
                train.AddRange(data1.Train);
                int trainDocs = train.Count - before;
                before = dev.Count;
                dev.AddRange(data1.Dev.Take(Num(args, "dev-docs", 1000000)));
                Console.WriteLine($"{lang}: train docs={trainDocs} dev docs={dev.Count - before} held out sentences={data1.HeldOut} dropped test repeats={data1.Dropped}");
                continue;
            }

            (string dir, string prefix) = Treebanks[lang];
            string root = Path.Combine(data, "train", "ud", dir);
            if (!Directory.Exists(root))
            {
                root = Path.Combine(data, "train", dir);
            }

            var store = new StringStore();
            stores[lang] = store;
            train.AddRange(CorpusReader.Documents(Path.Combine(root, prefix + "-train.conllu"), perDoc, store));
            int trainCount = train.Count - before;
            before = dev.Count;
            dev.AddRange(CorpusReader.Documents(Path.Combine(root, prefix + "-dev.conllu"), perDoc, store).Take(Num(args, "dev-docs", 1000000)));
            Console.WriteLine($"{lang}: train docs={trainCount} dev docs={dev.Count - before}");
        }

        var nerStores = new Dictionary<string, StringStore>();
        foreach (string lang in nerLangs)
        {
            int before = train.Count;
            if (spec is not null)
            {
                LanguageData n = CorpusReader.ReadNer(spec[lang], data, Num(args, "ner-train", 6000), Num(args, "ner-dev", 600));
                nerStores[lang] = n.Store;
                train.AddRange(n.Train);
                int docs = train.Count - before;
                before = dev.Count;
                dev.AddRange(n.Dev);
                Console.WriteLine($"{lang} ner: train docs={docs} dev docs={dev.Count - before}");
                continue;
            }

            var store = new StringStore();
            nerStores[lang] = store;
            (string dir, string prefix, _) = NerSets[lang];
            string root = Path.Combine(data, "train", dir);
            train.AddRange(CorpusReader.NerExamples(Path.Combine(root, prefix + "-train.iob2"), Num(args, "ner-train", 6000), store));
            int trainCount = train.Count - before;
            before = dev.Count;
            dev.AddRange(CorpusReader.NerExamples(Path.Combine(root, prefix + "-dev.iob2"), Num(args, "ner-dev", 600), store));
            Console.WriteLine($"{lang} ner: train docs={trainCount} dev docs={dev.Count - before}");
        }

        if (balance > 1)
        {
            // Language-balanced sampling: a language with fewer words than the largest is repeated, at most balance times, so small
            // treebanks are not swamped. Counted on UD words.
            var words = langs.ToDictionary(l => l, l => train.Where(e => ReferenceEquals(e.Predicted.Strings, stores[l])).Sum(e => (long)e.Predicted.Length));
            long most = words.Values.Max();
            foreach (string lang in langs)
            {
                int times = (int)Math.Min(balance, Math.Max(1, Math.Round((double)most / words[lang])));
                var mine = train.Where(e => ReferenceEquals(e.Predicted.Strings, stores[lang]) || (nerStores.TryGetValue(lang, out StringStore? ns) && ReferenceEquals(e.Predicted.Strings, ns))).ToList();
                for (int r = 1; r < times; r++)
                {
                    train.AddRange(mine);
                }

                Console.WriteLine($"{lang}: repeated x{times}");
            }
        }

        // The label sets of the heads (classes ordered by frequency, rare ones dropped) come from this list, the one a continued run's first
        // run was built from, so that the tensor shapes and class order of an earlier checkpoint match whatever sampling is used after it.
        List<Example> headInit = [.. train];

        // --ner-use N: only the first N entity documents of each language are trained on (the label sets above still see all of them).
        if (Num(args, "ner-use", 0) is > 0 and int nerUse)
        {
            foreach (string lang in nerLangs)
            {
                var keep = new HashSet<Example>(train.Where(e => ReferenceEquals(e.Predicted.Strings, nerStores[lang])).Take(nerUse));
                train.RemoveAll(e => ReferenceEquals(e.Predicted.Strings, nerStores[lang]) && !keep.Contains(e));
            }
        }

        // --repeat zh=1.8,tr=1.8,fr=0.2: replaces the multiplicity of each listed language's examples (UD and entities) by this many copies
        // of its distinct examples; a fraction takes evenly spaced examples of the last copy. Which languages a continued run sees most is
        // set here, from the development scores of the run before it. --plan ALPHA computes the list by temperature sampling over the languages
        // (SamplingPlan) from the corpus spec; entity examples are repeated by the plan's entity factor.
        var repeats = new List<(string Lang, double Times)>();
        var nerRepeats = new List<(string Lang, double Times)>();
        if (spec is not null && Opt(args, "plan", string.Empty) is { Length: > 0 } alpha)
        {
            IReadOnlyList<LanguagePlan> plan = SamplingPlan.Compute(spec, langs, l => train.Where(e => ReferenceEquals(e.Predicted.Strings, stores[l])).Sum(e => (long)e.Predicted.Length), PlanOptions(args, alpha));
            Console.Write(SamplingPlan.Describe(plan));
            repeats.AddRange(plan.Select(p => (p.Code, p.Repeat)));
            nerRepeats.AddRange(plan.Where(p => p.NerRepeat > 0).Select(p => (p.Code, p.NerRepeat)));
        }

        foreach (string spec1 in Opt(args, "repeat", string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] kv = spec1.Split('=');
            repeats.RemoveAll(r => r.Lang == kv[0]);
            repeats.Add((kv[0], double.Parse(kv[1], CultureInfo.InvariantCulture)));
        }

        foreach ((string lang, double times) in repeats)
        {
            Repeat(train, e => ReferenceEquals(e.Predicted.Strings, stores[lang]), times);
            Console.WriteLine($"{lang}: UD multiplicity {times}");
        }

        foreach ((string lang, double times) in nerRepeats.Where(r => nerStores.ContainsKey(r.Lang)))
        {
            Repeat(train, e => ReferenceEquals(e.Predicted.Strings, nerStores[lang]), times);
            Console.WriteLine($"{lang}: entity multiplicity {times}");
        }

        // The examples of the languages are interleaved by the trainer's seeded shuffle; with one language this is a plain epoch order.
        using var encoder = TrainableTransformerEncoder.LoadOnnx(
            Required(args, "onnx"),
            Required(args, "tokenizer"),
            new TrainableTransformerOptions { Pooling = SubwordPooling.Mean, FreezeEmbeddings = true, ExpandWordForms = true });
        using Pipeline pipeline = Pipeline.Blank(Language.English);
        var inner = new List<IDisposable>();
        foreach (string lang in langs)
        {
            StringStore mine = stores[lang];
            Func<Doc, bool> scope = d => ReferenceEquals(d.Strings, mine);
            if (!Available("tagger", train, mine))
            {
                Console.WriteLine($"{lang}: no XPOS in the treebank, no tagger");
            }

            foreach (string head in heads.Where(h => h != "ner" && Available(h, train, mine)))
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
                pipeline.Add(new ScopedHead(h, scope) { LossWeight = head == "parser" ? Flt(args, "parser-weight", 1f) : 1f });
            }
        }

        foreach (string lang in nerLangs)
        {
            StringStore mine = nerStores[lang];
            pipeline.Add(new ScopedHead(new EntityRecognizer(encoder, NerDecoding.Tagging, 1, 256, name: "ner-" + lang), d => ReferenceEquals(d.Strings, mine)));
        }

        foreach (IPipelineComponent component in pipeline.Components)
        {
            if (component is ScopedHead { IsInitialized: false } head)
            {
                head.Initialize(headInit.Where(e => e.Predicted.Length > 0).ToList(), seed + 1);
            }
        }

        // --warm-from CKPT --warm-langs en,de,...: a fresh run (no last.ckpt in --out yet) that starts from the weights of an earlier run with fewer languages.
        // Tensors are copied by name (the encoder as it is, each head through the name of its language), the optimizer starts new and warms up again.
        bool fullyRestored = false;
        if (Opt(args, "warm-from", string.Empty) is { Length: > 0 } warmPath && !File.Exists(Path.Combine(outDir, "last.ckpt")))
        {
            string[] warmLangs = Required(args, "warm-langs").Split(',');
            List<ITrainableComponent> allHeads = [.. pipeline.Components.OfType<ITrainableComponent>()];
            string[] newNames = [.. allHeads.Select(h => h.Name)];
            string[] oldNames = [.. newNames.Where(n => warmLangs.Contains(n[(n.LastIndexOf('-') + 1)..]))];
            var target = new List<(string, RecursiveAI.OneModel.Tensors.Tensor)>();
            if (encoder.Weights is { } encoderWeights)
            {
                target.AddRange(encoderWeights.StateDict().Select(t => ("encoder0." + t.Name, t.Tensor)));
            }

            for (int i = 0; i < allHeads.Count; i++)
            {
                target.AddRange(allHeads[i].Weights.StateDict().Select(t => ("head" + i.ToString(CultureInfo.InvariantCulture) + "." + t.Name, t.Tensor)));
            }

            WarmStartReport report = WarmStart.Apply(warmPath, target, oldNames, newNames);
            fullyRestored = report.Missing == 0 && report.Mismatched == 0;
            Console.WriteLine($"warm start: copied={report.Copied} missing={report.Missing} mismatched={report.Mismatched} unused={report.Unused}");
            foreach (string note in report.Notes)
            {
                Console.WriteLine("  " + note);
            }
        }

        // A selected weights-only checkpoint needs no optimizer, resume state or new last.ckpt.
        // This keeps finalization independent of the much larger training checkpoint.
        if (Num(args, "export-only", 0) == 1)
        {
            if (!fullyRestored)
            {
                throw new InvalidDataException("--export-only needs a --warm-from checkpoint that restores every tensor, with --warm-langs in the original language order and a fresh --out directory.");
            }

            encoder.Weights?.Eval();
            foreach (ITrainableComponent head in pipeline.Components.OfType<ITrainableComponent>())
            {
                head.Weights.Eval();
            }

            Directory.CreateDirectory(outDir);
            if (Num(args, "tune-lexicons", 0) == 1)
            {
                TuneLexicons(encoder, pipeline, langs, headInit, dev, stores, outDir);
            }

            Export(encoder, pipeline, langs, Required(args, "export"), Required(args, "tokenizer"));
            if (Opt(args, "predict-split", string.Empty) is { Length: > 0 } exportSplit)
            {
                PredictSplit(encoder, lang => [.. pipeline.Components.OfType<ScopedHead>().Where(h => h.Name.EndsWith("-" + lang, StringComparison.Ordinal)).Select(h => h.Inner)],
                    data, exportSplit, Num(args, "raw", 0) == 1, [.. langs.Where(l => predictLangs.Length == 0 || predictLangs.Contains(l))], outDir, stores, nerStores);
            }

            Console.WriteLine("exported selected checkpoint without training");
            return 0;
        }

        var hasTag = langs.ToDictionary(l => l, l => Available("tagger", train, stores[l]));
        Func<IReadOnlyList<Example>, double> score = examples =>
        {
            double total = 0;
            int count = 0;
            foreach (string lang in langs)
            {
                var mine = examples.Where(e => ReferenceEquals(e.Predicted.Strings, stores[lang])).ToList();
                if (mine.Count == 0)
                {
                    continue;  // a language with no dev documents is not seen by selection
                }

                var metrics = new List<string>();
                if (heads.Contains("morphologizer")) { metrics.Add("UPOS"); metrics.Add("UFeats"); }
                // Turkish BOUN leaves XPOS empty ("_") on about half its tokens, so that row is not meaningful there.
                if (heads.Contains("tagger") && lang != "tr" && hasTag.GetValueOrDefault(lang)) { metrics.Add("XPOS"); }
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
                count++;
                if (nerStores.TryGetValue(lang, out StringStore? ns))
                {
                    var ner = examples.Where(e => ReferenceEquals(e.Predicted.Strings, ns)).ToList();
                    double f1 = SpanScorer.EntityScore([.. ner.Select(e => e.Predicted)], [.. ner.Select(e => e.Reference)]).Value;
                    Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"dev {lang} ner: F1={f1:F2}"));
                    total += f1;
                    count++;
                }
            }

            return total / count;
        };

        double budget = Opt(args, "budget-seconds", string.Empty) is { Length: > 0 } b ? double.Parse(b, CultureInfo.InvariantCulture) : double.PositiveInfinity;
        using var cts = new CancellationTokenSource();
        if (!double.IsPositiveInfinity(budget))
        {
            cts.CancelAfter(TimeSpan.FromSeconds(budget));
        }

        string? initFrom = Opt(args, "init", string.Empty) is { Length: > 0 } ip ? ip : null;
        float floorFraction = Flt(args, "floor", 0.1f);
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
                // --init continues an earlier run: the optimizer's step count carries over, so the schedule is shifted by the steps already taken.
                Schedule = initFrom is null ? new WarmupCosineLr(lr, warmup, steps, floorFraction) : new ShiftedLr(new WarmupCosineLr(lr, warmup, steps, floorFraction), Num(args, "init-steps", 0)),
                InitializeFrom = initFrom,
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
        // --lexicon tr=1:0.9,fr=2:0.8: a lemma table from each listed language's training treebank in front of its edit trees (minimum count : minimum share,
        // chosen on development data with tools/accurate/lexicon_dev.py).
        foreach (string setting in Opt(args, "lexicon", string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] kv = setting.Split('=');
            string[] cs = kv[1].Split(':');
            foreach (ScopedHead scoped in pipeline.Components.OfType<ScopedHead>().Where(h => h.Name == "lemmatizer-" + kv[0]))
            {
                var references = train.Where(e => ReferenceEquals(e.Predicted.Strings, stores[kv[0]])).Select(e => e.Reference).Distinct().ToList();
                var table = LemmaLexicon.Build(references, int.Parse(cs[0], CultureInfo.InvariantCulture), double.Parse(cs[1], CultureInfo.InvariantCulture));
                ((EditTreeLemmatizer)scoped.Inner).Lexicon = table;
                Console.WriteLine($"{kv[0]}: lemma lexicon with {table.Count} entries");
            }
        }

        if (result.Completed && Num(args, "tune-lexicons", 0) == 1)
        {
            TuneLexicons(encoder, pipeline, langs, headInit, dev, stores, outDir);
        }

        if (result.Completed && Opt(args, "export", string.Empty) is { Length: > 0 } exportDir)
        {
            Export(encoder, pipeline, langs, exportDir, Required(args, "tokenizer"));
        }

        if (result.Completed && Opt(args, "predict-split", string.Empty) is { Length: > 0 } split)
        {
            PredictSplit(encoder, lang => [.. pipeline.Components.OfType<ScopedHead>().Where(h => h.Name.EndsWith("-" + lang, StringComparison.Ordinal)).Select(h => h.Inner)],
                data, split, Num(args, "raw", 0) == 1, [.. langs.Where(l => predictLangs.Length == 0 || predictLangs.Contains(l))], outDir, stores, nerStores);
        }

        return 0;
    }

    // Choose on the same held-out UD documents as training selection, using the model's UPOS
    // and the exact C# lexicon lookup. This also covers languages whose dev was held out of train.
    private static void TuneLexicons(IEncoder encoder, Pipeline pipeline, string[] langs, List<Example> train, List<Example> dev,
        Dictionary<string, StringStore> stores, string outDir)
    {
        using var log = new StreamWriter(Path.Combine(outDir, "lexicon-dev.tsv"), false, new UTF8Encoding(false));
        log.WriteLine("code\tdev_words\tsetting\tbase_accuracy\tselected_accuracy\tentries");
        foreach (string lang in langs)
        {
            ITrainableComponent[] mine = [.. pipeline.Components.OfType<ScopedHead>()
                .Where(h => h.Name.EndsWith("-" + lang, StringComparison.Ordinal) && h.Kind is "morphologizer" or "lemmatizer").Select(h => h.Inner)];
            EditTreeLemmatizer? head = mine.OfType<EditTreeLemmatizer>().FirstOrDefault();
            if (head is null)
            {
                continue;
            }

            List<Example> examples = [.. dev.Where(e => ReferenceEquals(e.Predicted.Strings, stores[lang]))];
            if (examples.Count == 0)
            {
                log.WriteLine(lang + "\t0\tnone\t\t\t0");
                continue;
            }

            head.Lexicon = null;
            // One document at a time bounds activations even for long development sentences.
            foreach (Example example in examples)
            {
                Annotate(encoder, mine, [example.Predicted]);
            }

            var probes = new List<(string Form, string Pos, string Base, string Gold)>();
            foreach (Example example in examples)
            {
                for (int t = 0; t < example.Reference.Length; t++)
                {
                    Token gold = example.Reference[t];
                    Token pred = example.Predicted[t];
                    if (gold.Lemma.Length > 0)
                    {
                        string form = (pred.Norm.Length > 0 ? pred.Norm : pred.Text.ToString()).ToLowerInvariant();
                        probes.Add((form, pred.Pos, pred.Lemma, gold.Lemma));
                    }
                }
            }

            int baseline = probes.Count(p => p.Base == p.Gold);
            int best = baseline;
            string setting = "none";
            LemmaLexicon? selected = null;
            List<Doc> references = [.. train.Where(e => ReferenceEquals(e.Predicted.Strings, stores[lang])).Select(e => e.Reference).Distinct()];
            foreach (int minCount in new[] { 1, 2 })
            {
                foreach (double minShare in new[] { 0.6, 0.8, 0.9, 1.0 })
                {
                    LemmaLexicon table = LemmaLexicon.Build(references, minCount, minShare);
                    int correct = probes.Count(p => (table.TryGet(p.Form, p.Pos, out string lemma) ? lemma : p.Base) == p.Gold);
                    if (correct > best)
                    {
                        best = correct;
                        selected = table;
                        setting = string.Create(CultureInfo.InvariantCulture, $"{minCount}:{minShare}");
                    }
                }
            }

            head.Lexicon = selected;
            double denominator = Math.Max(1, probes.Count);
            string row = string.Create(CultureInfo.InvariantCulture,
                $"{lang}\t{probes.Count}\t{setting}\t{100.0 * baseline / denominator:F2}\t{100.0 * best / denominator:F2}\t{selected?.Count ?? 0}");
            log.WriteLine(row);
            log.Flush();
            Console.WriteLine("lexicon-dev " + row);
        }
    }

    // The best-dev weights as the model package ships them: the encoder's layers (the token table is frozen and comes from the ONNX file, see
    // tools/accurate/split_token_table.py), the encoder's settings and shape, and for each language a pipeline file that holds its heads only,
    // bound to the shared encoder.
    private static void Export(TrainableTransformerEncoder encoder, Pipeline trained, string[] langs, string dir, string tokenizerPath)
    {
        Directory.CreateDirectory(Path.Combine(dir, "heads"));
        string tokenizerFile = Path.GetFullPath(Path.Combine(dir, "tokenizer.json"));
        if (!string.Equals(Path.GetFullPath(tokenizerPath), tokenizerFile, StringComparison.Ordinal))
        {
            File.Copy(tokenizerPath, tokenizerFile, overwrite: true);
        }

        Checkpoint.Save(Path.Combine(dir, "encoder-layers.ckpt"), encoder.Weights!);
        using (var stream = new MemoryStream())
        {
            using (var w = new Utf8JsonWriter(stream))
            {
                w.WriteStartObject();
                w.WriteStartObject("shape");
                w.WriteNumber("vocabularySize", 256000);
                w.WriteNumber("width", encoder.Width);
                w.WriteNumber("layers", 22);
                w.WriteNumber("feedForwardWidth", 1152);
                w.WriteEndObject();
                w.WriteStartObject("settings");
                using var all = new MemoryStream();
                using (var inner = new Utf8JsonWriter(all))
                {
                    inner.WriteStartObject();
                    encoder.WriteSettings(inner);
                    inner.WriteEndObject();
                }

                using JsonDocument written = JsonDocument.Parse(all.ToArray());
                foreach (JsonProperty prop in written.RootElement.EnumerateObject())
                {
                    // The file names of the training machine mean nothing in the package.
                    if (prop.Name is not ("model" or "tokenizer" or "modelSha256"))
                    {
                        prop.WriteTo(w);
                    }
                }

                w.WriteEndObject();
                w.WriteEndObject();
            }

            File.WriteAllBytes(Path.Combine(dir, "encoder.json"), stream.ToArray());
        }

        // The same weights as an ONNX graph for the inference runtime (the model package serves from it; see docs/accurate-tier.md).
        string graphFile = Path.Combine(dir, "encoder.onnx");
        if (!File.Exists(graphFile))
        {
            Console.WriteLine("encoder.onnx sha256=" + encoder.ExportOnnx(graphFile));
        }

        var shared = new SharedEncoder(encoder, "multilingual-large");
        foreach (string lang in langs)
        {
            Language language = Language.FromCode(lang);
            // The segmenter source owns this tokenizer and is still needed by later
            // dev probes. Dispose only the cloned heads, not the borrowed tokenizer.
            Pipeline package = new(TokenizerFor(language), language);
            package.Add(new MultiwordFormsComponent(language));
            foreach (ScopedHead scoped in trained.Components.OfType<ScopedHead>().Where(h => h.Name.EndsWith("-" + lang, StringComparison.Ordinal)))
            {
                ITrainableComponent from = scoped.Inner;
                using var config = new MemoryStream();
                using (var w = new Utf8JsonWriter(config))
                {
                    w.WriteStartObject();
                    from.WriteConfig(w);
                    w.WriteEndObject();
                }

                using JsonDocument configJson = JsonDocument.Parse(config.ToArray());
                ITrainableComponent copy = HeadRegistry.Create(from.Kind, from.Name, shared, configJson.RootElement);
                using var weights = new MemoryStream();
                Checkpoint.Save(weights, from.Weights);
                weights.Position = 0;
                Checkpoint.Load(weights, copy.Weights);
                package.Add(copy);
            }

            string file = Path.Combine(dir, "heads", lang + ".onenlp");
            PipelineFile.Save(package, file);
            foreach (ITrainableComponent head in package.Components.OfType<ITrainableComponent>())
            {
                (head as IDisposable)?.Dispose();
            }

            Console.WriteLine($"exported {file} {new FileInfo(file).Length} bytes");
        }
    }

    // Predictions for tools/OneNlp.Eval: gold tokens and sentences (gold mode), raw text with the language's tokenizer (raw; the parser sets the
    // sentences), and entities on the entity corpus's tokens. headsOf gives the heads of a language, from the training run or from a package.
    private static void PredictSplit(IEncoder encoder, Func<string, ITrainableComponent[]> headsOf, string data, string split, bool raw, string[] langs, string outDir,
        Dictionary<string, StringStore> stores, Dictionary<string, StringStore> nerStores)
    {
        foreach (string lang in langs)
        {
            string root;
            string[] names;
            if (corpus is not null)
            {
                // The gating treebank's test split, or its dev split when the release has one (a language that holds dev out of train has no dev file).
                string? rel = split == "test" ? corpus[lang].Gating.Test : corpus[lang].Gating.Dev;
                if (rel is null)
                {
                    Console.WriteLine($"{lang}: no {split} file, skipped");
                    continue;
                }

                root = Path.Combine(data, Path.GetDirectoryName(rel)!);
                names = [Path.GetFileNameWithoutExtension(rel)];
            }
            else
            {
                (string dir, string prefix) = Treebanks[lang];
                string sub = split == "test" ? "eval" : "train";
                root = Path.Combine(data, sub, "ud", dir);
                if (!Directory.Exists(root))
                {
                    root = Path.Combine(data, sub, dir);
                }

                names = split == "test" && lang == "it" ? ["it_parlamint-ud-test", "it_markit-ud-test"] : [$"{prefix}-{split}"];
            }

            ITrainableComponent[] mine = headsOf(lang);
            ITrainableComponent[] ud = [.. mine.Where(h => h.Kind != "ner")];
            foreach (string name in names)
            {
                string gold = Path.Combine(root, name + ".conllu");
                Predict(encoder, ud, gold, stores[lang], Path.Combine(outDir, $"pred-{name}.conllu"));
                if (raw)
                {
                    var tokenizer = new Pipeline(TokenizerFor(Language.FromCode(lang)), Language.FromCode(lang));
                    PredictRaw(encoder, ud, tokenizer, SegmentationData.For(Language.FromCode(lang)), gold, Path.Combine(outDir, $"pred-raw-{name}.conllu"));
                }
            }

            if (corpus is not null)
            {
                // With several entity sets (Indonesian, Norwegian) the first is scored; the head trains on all of them.
                CorpusEntitySet? set = corpus[lang].Ner.Count > 0 ? corpus[lang].Ner[0] : null;
                string? file = set is null ? null : split == "test" ? set.Test : set.Dev;
                if (file is not null && nerStores.TryGetValue(lang, out StringStore? corpusStore) && mine.Any(h => h.Kind == "ner"))
                {
                    PredictNer(encoder, [.. mine.Where(h => h.Kind == "ner")], Path.Combine(data, file), lang, corpusStore, outDir, split);
                }
            }
            else if (NerSets.TryGetValue(lang, out var ns) && nerStores.TryGetValue(lang, out StringStore? nerStore))
            {
                string nerRoot = split == "test" ? Path.Combine(data, "eval", ns.EvalDir) : Path.Combine(data, "train", ns.Dir);
                PredictNer(encoder, [.. mine.Where(h => h.Kind == "ner")], Path.Combine(nerRoot, $"{ns.Prefix}-{split}.iob2"), lang, nerStore, outDir, split);
            }
        }
    }

    // predict-package --export DIR --data DATA --split test|dev --out DIR [--raw 1] [--langs de,tr] [--zh-segmenter FILE] [--threads N]: the packaged model
    // (encoder from its ONNX graph, heads from the per-language files with their lemma tables) predicts a split, so that the rows reported are those
    // of the model users get.
    private static int PredictPackage(string[] args)
    {
        string dir = Required(args, "export");
        string outDir = Required(args, "out");
        Directory.CreateDirectory(outDir);
        predictLimit = Num(args, "predict-limit", 0);
        nerTestLimit = Num(args, "ner-test", 3000);
        string[] langs = Opt(args, "langs", corpus is not null ? string.Join(',', corpus.Languages.Select(l => l.Code)) : "en,de,fr,es,pt,it,nl,tr,zh").Split(',');
        using FileStream graph = File.OpenRead(Path.Combine(dir, "encoder.onnx"));
        using Stream tokenizerJson = File.OpenRead(Path.Combine(dir, "tokenizer.json"));
        using TransformerEncoder encoder = TransformerEncoder.FromStreams(graph, tokenizerJson, SubwordPooling.Mean, expandWordForms: true);
        SharedEncoder.Register("multilingual-large", encoder);
        var packages = new Dictionary<string, Pipeline>();
        var stores = new Dictionary<string, StringStore>();
        var nerStores = new Dictionary<string, StringStore>();
        foreach (string lang in langs)
        {
            packages[lang] = PipelineFile.Load(Path.Combine(dir, "heads", lang + ".onenlp"));
            stores[lang] = new StringStore();
            nerStores[lang] = new StringStore();
        }

        PredictSplit(encoder, lang => [.. packages[lang].Components.OfType<ITrainableComponent>()], Required(args, "data"), Opt(args, "split", "test"), Num(args, "raw", 0) == 1, langs, outDir, stores, nerStores);
        foreach (Pipeline p in packages.Values)
        {
            p.Dispose();
        }

        return 0;
    }

    // Compare the same saved heads on module and graph vectors, isolating numerical
    // encoder differences from serialization or label-order differences. Development only.
    private static int EncoderParity(string[] args)
    {
        string dir = Required(args, "export");
        string lang = Required(args, "lang");
        CorpusSpec spec = corpus ?? throw new ArgumentException("--spec is required.");
        string dev = spec[lang].Gating.Dev ?? throw new ArgumentException(lang + " has no dev file.");
        string text = string.Join(' ', ConllUReader.Read(Path.Combine(Required(args, "data"), dev))
            .Take(Num(args, "predict-limit", 4)).Select(s => s.GetComment("text") ?? s.Doc.Text));
        using TrainableTransformerEncoder module = TrainableTransformerEncoder.LoadOnnx(Required(args, "onnx"), Path.Combine(dir, "tokenizer.json"),
            new TrainableTransformerOptions { Pooling = SubwordPooling.Mean, FreezeEmbeddings = true, ExpandWordForms = true });
        Checkpoint.Load(Path.Combine(dir, "encoder-layers.ckpt"), module.Weights!);
        module.Weights!.Eval();
        using Stream graphFile = File.OpenRead(Path.Combine(dir, "encoder.onnx"));
        using Stream tokenizerFile = File.OpenRead(Path.Combine(dir, "tokenizer.json"));
        using TransformerEncoder graph = TransformerEncoder.FromStreams(graphFile, tokenizerFile, SubwordPooling.Mean, expandWordForms: true);
        SharedEncoder.Register("multilingual-large", graph);
        using Pipeline package = PipelineFile.Load(Path.Combine(dir, "heads", lang + ".onenlp"));
        Pipeline tokenizer = new(TokenizerFor(Language.FromCode(lang)), Language.FromCode(lang));
        Doc first = tokenizer.Process(text);
        Doc second = tokenizer.Process(text);
        var forms = new MultiwordFormsComponent(Language.FromCode(lang));
        forms.Process(first);
        forms.Process(second);
        using NoGradScope noGrad = GradMode.NoGrad();
        using var scope = new ActivationScope();
        int graphThreads = TensorCompute.Threads;
        int moduleThreads = Num(args, "module-threads", graphThreads);
        TensorCompute.Threads = moduleThreads;
        EncodedBatch a = module.Encode([first]);
        TensorCompute.Threads = graphThreads;
        EncodedBatch b = graph.Encode([second]);
        ReadOnlySpan<float> x = a.Vectors.Data<float>();
        ReadOnlySpan<float> y = b.Vectors.Data<float>();
        if (x.Length != y.Length)
        {
            throw new InvalidDataException("Encoder shapes differ.");
        }

        double max = 0;
        double squared = 0;
        for (int i = 0; i < x.Length; i++)
        {
            double d = (double)x[i] - y[i];
            max = Math.Max(max, Math.Abs(d));
            squared += d * d;
        }

        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"encoder-parity {lang}: tokens={first.Length} values={x.Length} max_abs={max:E8} rmse={Math.Sqrt(squared / Math.Max(1, x.Length)):E8}"));
        foreach (ITrainableComponent head in package.Components.OfType<ITrainableComponent>().Where(h => h.Kind != "ner"))
        {
            head.Weights.Eval();
            TensorCompute.Threads = moduleThreads;
            head.Predict(a, [first]);
            TensorCompute.Threads = graphThreads;
            head.Predict(b, [second]);
        }

        int different = 0;
        for (int i = 0; i < first.Length; i++)
        {
            Token p = first[i];
            Token q = second[i];
            if (p.Pos != q.Pos || p.Tag != q.Tag || p.Morph != q.Morph || p.Lemma != q.Lemma || p.HeadIndex != q.HeadIndex || p.Dep != q.Dep || p.IsSentStart != q.IsSentStart)
            {
                different++;
                Console.WriteLine($"different token {i} {p}: pos {p.Pos}/{q.Pos}; tag {p.Tag}/{q.Tag}; morph {p.Morph}/{q.Morph}; lemma {p.Lemma}/{q.Lemma}; head {p.HeadIndex}/{q.HeadIndex}; dep {p.Dep}/{q.Dep}; sent {p.IsSentStart}/{q.IsSentStart}");
            }
        }

        Console.WriteLine($"same serialized heads, different encoder vectors: differing words={different}");
        return 0;
    }

    // verify-export --export DIR --lang pt --data DATA [--split dev|test] [--predict-limit 100]: the packaged model, rebuilt from its files the way the package does
    // (encoder from parts, heads from the per-language file), analyses the first sentences of the chosen split as raw text; the output is written
    // in the format of --raw predictions so that it can be compared line by line with them.
    private static int VerifyExport(string[] args)
    {
        string dir = Required(args, "export");
        string lang = Required(args, "lang");
        predictLimit = Num(args, "predict-limit", 100);
        string split = Opt(args, "split", "test");
        if (split is not ("test" or "dev"))
        {
            throw new ArgumentException("--split must be dev or test.");
        }

        string gold;
        if (corpus is not null)
        {
            string? relative = split == "test" ? corpus[lang].Gating.Test : corpus[lang].Gating.Dev;
            gold = Path.Combine(Required(args, "data"), relative ?? throw new ArgumentException(lang + " has no " + split + " file."));
        }
        else
        {
            (string td, string prefix) = Treebanks[lang];
            gold = Path.Combine(Required(args, "data"), split == "test" ? "eval" : "train", "ud", td,
                lang == "it" && split == "test" ? "it_markit-ud-test.conllu" : $"{prefix}-{split}.conllu");
        }

        var load = Stopwatch.StartNew();
        // The packaged model's way: the encoder from its ONNX graph on the inference runtime (see the model package), heads from the per-language file.
        using FileStream graph = File.OpenRead(Path.Combine(dir, "encoder.onnx"));
        using Stream tokenizerJson = File.OpenRead(Path.Combine(dir, "tokenizer.json"));
        using TransformerEncoder encoder = TransformerEncoder.FromStreams(graph, tokenizerJson, SubwordPooling.Mean, expandWordForms: true);
        SharedEncoder.Register("multilingual-large", encoder);
        using Pipeline package = PipelineFile.Load(Path.Combine(dir, "heads", lang + ".onenlp"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"loaded in {load.Elapsed.TotalSeconds:F1} s, working set {Environment.WorkingSet >> 20} MiB"));
        var tokenizer = new Pipeline(TokenizerFor(Language.FromCode(lang)), Language.FromCode(lang));
        string output = Required(args, "out");
        var clock = Stopwatch.StartNew();
        PredictRaw(encoder, [], tokenizer, SegmentationData.For(Language.FromCode(lang)), gold, output, package);
        double seconds = clock.Elapsed.TotalSeconds;
        int words = File.ReadLines(output).Count(l => l.Length > 0 && l[0] != '#' && !l.AsSpan(0, Math.Min(l.Length, 8)).Contains('-'));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"analysed {words} words in {seconds:F1} s with {TensorCompute.Threads} threads: {words / seconds:F0} words/s"));
        return 0;
    }

    // A schedule read at step s - offset: the first steps of a continued run sit at the start of the new schedule.
    private sealed class ShiftedLr(LrSchedule inner, long offset) : LrSchedule
    {
        public override float LrAt(long stepNumber) => inner.LrAt(Math.Max(0, stepNumber - offset));
    }

    // The files joined end to end, read as one stream (the layers' checkpoint is shipped in two halves).
    private sealed class ConcatenatedFiles(string[] files) : Stream
    {
        private readonly MemoryStream _all = Join(files);

        private static MemoryStream Join(string[] names)
        {
            var ms = new MemoryStream();
            foreach (string f in names)
            {
                using FileStream fs = File.OpenRead(f);
                fs.CopyTo(ms);
            }

            ms.Position = 0;
            return ms;
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => _all.Length;

        public override long Position { get => _all.Position; set => _all.Position = value; }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => _all.Read(buffer, offset, count);

        public override int Read(Span<byte> buffer) => _all.Read(buffer);

        public override long Seek(long offset, SeekOrigin origin) => _all.Seek(offset, origin);

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _all.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    // Runs the shared encoder once over the documents and lets every head of the language read the same vectors.
    private static void Annotate(IEncoder encoder, ITrainableComponent[] heads, Doc[] docs)
    {
        encoder.Weights?.Eval();
        foreach (ITrainableComponent h in heads)
        {
            h.Weights.Eval();
        }

        using NoGradScope noGrad = GradMode.NoGrad();
        using var scope = new ActivationScope();
        EncodedBatch encoded = encoder.Encode(docs);
        foreach (ITrainableComponent h in heads)
        {
            h.Predict(encoded, docs);
        }
    }

    // Raw text in, ten gold sentences at a time joined by a space, as a running document. Multiword tokens are found from the pieces.
    // Whether the treebank has the gold column a head learns from (French-GSD has no XPOS, for example).
    private static bool Available(string head, List<Example> train, StringStore store)
    {
        foreach (Example e in train)
        {
            if (!ReferenceEquals(e.Predicted.Strings, store))
            {
                continue;
            }

            Doc d = e.Reference;
            for (int i = 0; i < d.Length; i++)
            {
                if (head switch { "tagger" => d[i].Tag.Length > 0, "lemmatizer" => d[i].Lemma.Length > 0, _ => true })
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void PredictRaw(IEncoder encoder, ITrainableComponent[] heads, Pipeline tokenizer, SegmentationData data, string gold, string output, Pipeline? package = null)
    {
        var forms = new MultiwordFormsComponent(tokenizer.Language);
        var sb = new StringBuilder();
        var chunk = new List<string>();
        void Flush()
        {
            Doc doc;
            if (package is not null)
            {
                doc = package.Process(string.Join(' ', chunk));
            }
            else
            {
                doc = tokenizer.Process(string.Join(' ', chunk));
                forms.Process(doc);
                Annotate(encoder, heads, [doc]);
            }

            foreach (Span sentence in doc.Sentences)
            {
                int first = sentence.Start;
                int end = sentence.End;
                int word = 0;
                var lines = new StringBuilder();
                for (int i = first; i < end; i++)
                {
                    if (data.TryMatchMultiword(doc, i, out int count, out string[]? words) && count > 1 && i + count <= end)
                    {
                        int a = doc[i].Idx;
                        int b = doc[i + count - 1].EndIdx;
                        lines.Append(word + 1).Append('-').Append(word + count).Append('\t').Append(doc.Text, a, b - a).Append("\t_\t_\t_\t_\t_\t_\t_\t_\n");
                        for (int k = 0; k < count; k++)
                        {
                            WriteWord(lines, doc, i + k, first, word + k, words[k]);
                        }

                        i += count - 1;
                        word += count;
                    }
                    else
                    {
                        WriteWord(lines, doc, i, first, word, doc[i].ToString());
                        word++;
                    }
                }

                sb.Append(lines).Append('\n');
            }

            chunk.Clear();
        }

        foreach (ConllUSentence s in ConllUReader.Read(gold).Skip(predictOffset).Take(predictLimit > 0 ? predictLimit : int.MaxValue))
        {
            // The text line of the file is the raw text; Doc.Text spells the words of a one-character multiword token (Portuguese à = a a).
            chunk.Add(s.GetComment("text") ?? s.Doc.Text);
            if (chunk.Count == 10)
            {
                Flush();
            }
        }

        if (chunk.Count > 0)
        {
            Flush();
        }

        File.WriteAllText(output, sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"wrote {output}");
    }

    private static void WriteWord(StringBuilder sb, Doc doc, int tokenIndex, int sentenceStart, int word, string form)
    {
        Token t = doc[tokenIndex];
        string deprel = t.Dep;
        string head = t.HeadIndex == tokenIndex ? (deprel.Length > 0 ? "0" : "_") : (t.HeadIndex - sentenceStart + 1).ToString(CultureInfo.InvariantCulture);
        sb.Append(word + 1).Append('\t').Append(form);
        foreach (string column in new[] { t.Lemma, t.Pos, t.Tag, t.Morph })
        {
            sb.Append('\t').Append(column.Length == 0 ? "_" : column);
        }

        sb.Append('\t').Append(head).Append('\t').Append(deprel.Length == 0 ? "_" : deprel).Append("\t_\t_\n");
    }

    // Entity corpus in, gold and predicted tags out as token-per-line IOB2 files for 'OneNlp.Eval span'.
    private static void PredictNer(IEncoder encoder, ITrainableComponent[] heads, string file, string lang, StringStore store, string outDir, string split)
    {
        List<Example> examples = CorpusReader.NerExamples(file, split == "test" ? nerTestLimit : 0, store);
        if (predictLimit > 0 && examples.Count > predictLimit)
        {
            examples = examples.GetRange(0, predictLimit);
        }

        var gold = new StringBuilder();
        var pred = new StringBuilder();
        const int Chunk = 32;
        for (int start = 0; start < examples.Count; start += Chunk)
        {
            int n = Math.Min(Chunk, examples.Count - start);
            var docs = new Doc[n];
            for (int i = 0; i < n; i++)
            {
                docs[i] = examples[start + i].Predicted;
            }

            Annotate(encoder, heads, docs);

            for (int i = 0; i < n; i++)
            {
                WriteTags(gold, examples[start + i].Reference);
                WriteTags(pred, docs[i]);
            }
        }

        File.WriteAllText(Path.Combine(outDir, $"ner-gold-{lang}-{split}.iob2"), gold.ToString(), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(outDir, $"ner-pred-{lang}-{split}.iob2"), pred.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"wrote ner {lang} {split} sentences={examples.Count}");
    }

    private static void WriteTags(StringBuilder sb, Doc doc)
    {
        var tags = new string[doc.Length];
        Array.Fill(tags, "O");
        foreach (Span e in doc.Ents)
        {
            for (int i = e.Start; i < e.End; i++)
            {
                tags[i] = (i == e.Start ? "B-" : "I-") + e.Label;
            }
        }

        for (int i = 0; i < doc.Length; i++)
        {
            sb.Append(doc[i].ToString()).Append('\t').Append(tags[i]).Append('\n');
        }

        sb.Append('\n');
    }

    private static void Predict(IEncoder encoder, ITrainableComponent[] heads, string gold, StringStore store, string output)
    {
        var sentences = ConllUReader.Read(gold, store).Skip(predictOffset).Take(predictLimit > 0 ? predictLimit : int.MaxValue).ToList();
        var sb = new StringBuilder();
        const int Chunk = 32;
        for (int start = 0; start < sentences.Count; start += Chunk)
        {
            int n = Math.Min(Chunk, sentences.Count - start);
            var docs = new Doc[n];
            for (int i = 0; i < n; i++)
            {
                docs[i] = Example.Blank(CorpusReader.Merge([sentences[start + i]], store));
                docs[i].SetSentenceBoundaries(sentences[start + i].Doc.GetSentenceBoundaries());
            }

            Annotate(encoder, heads, docs);

            for (int i = 0; i < n; i++)
            {
                WriteSentence(sb, sentences[start + i], docs[i]);
            }
        }

        File.WriteAllText(output, sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"wrote {output} sentences={sentences.Count}");
    }

    // The gold file's comments, multiword token lines and forms, with the predicted columns.
    private static void WriteSentence(StringBuilder sb, ConllUSentence gold, Doc doc)
    {
        foreach (string c in gold.Comments.Where(c => c.StartsWith("# sent_id", StringComparison.Ordinal)))
        {
            sb.Append(c).Append('\n');
        }

        int mwt = 0;
        for (int i = 0; i < doc.Length; i++)
        {
            if (mwt < gold.MultiwordTokens.Count && gold.MultiwordTokens[mwt].FirstWord == i)
            {
                MultiwordToken m = gold.MultiwordTokens[mwt++];
                sb.Append(m.FirstWord + 1).Append('-').Append(m.LastWord + 1).Append('\t').Append(m.Form).Append("\t_\t_\t_\t_\t_\t_\t_\t_\n");
            }

            Token t = doc[i];
            string deprel = t.Dep;
            string head = t.HeadIndex != i ? (t.HeadIndex + 1).ToString(CultureInfo.InvariantCulture) : (deprel.Length > 0 ? "0" : "_");
            sb.Append(i + 1).Append('\t').Append(gold.Forms[i]);
            foreach (string column in new[] { t.Lemma, t.Pos, t.Tag, t.Morph })
            {
                sb.Append('\t').Append(column.Length == 0 ? "_" : column);
            }

            sb.Append('\t').Append(head).Append('\t').Append(deprel.Length == 0 ? "_" : deprel).Append("\t_\t_\n");
        }

        sb.Append('\n');
    }

    private sealed class Progress(Action<TrainProgress> handler) : IProgress<TrainProgress>
    {
        public void Report(TrainProgress value) => handler(value);
    }
}
