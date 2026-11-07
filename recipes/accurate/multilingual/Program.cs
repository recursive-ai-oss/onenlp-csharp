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
using RecursiveAI.OneNlp.Neural.Training;
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
    private static int nerTestLimit;

    private static readonly Stopwatch Wall = Stopwatch.StartNew();

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length > 0 && args[0] == "verify-export")
            {
                if (Opt(args, "threads", string.Empty).Length > 0)
                {
                    TensorCompute.Threads = Num(args, "threads", 8);
                }

                return VerifyExport(args);
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
        string[] heads = Opt(args, "heads", "tagger,morphologizer,lemmatizer,parser,ner").Split(',');
        string[] nerLangs = heads.Contains("ner") ? [.. langs.Where(NerSets.ContainsKey)] : [];
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
            dev.AddRange(Documents(Path.Combine(root, prefix + "-dev.conllu"), perDoc, store).Take(Num(args, "dev-docs", 1000000)));
            Console.WriteLine($"{lang}: train docs={trainCount} dev docs={dev.Count - before}");
        }

        var nerStores = new Dictionary<string, StringStore>();
        foreach (string lang in nerLangs)
        {
            var store = new StringStore();
            nerStores[lang] = store;
            (string dir, string prefix, _) = NerSets[lang];
            string root = Path.Combine(data, "train", dir);
            int before = train.Count;
            train.AddRange(NerExamples(Path.Combine(root, prefix + "-train.iob2"), lang, Num(args, "ner-train", 6000), store));
            int trainCount = train.Count - before;
            before = dev.Count;
            dev.AddRange(NerExamples(Path.Combine(root, prefix + "-dev.iob2"), lang, Num(args, "ner-dev", 600), store));
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
                pipeline.Add(new ScopedHead(h, scope));
            }
        }

        foreach (string lang in nerLangs)
        {
            StringStore mine = nerStores[lang];
            pipeline.Add(new ScopedHead(new EntityRecognizer(encoder, NerDecoding.Tagging, 1, 256, name: "ner-" + lang), d => ReferenceEquals(d.Strings, mine)));
        }

        var hasTag = langs.ToDictionary(l => l, l => Available("tagger", train, stores[l]));
        Func<IReadOnlyList<Example>, double> score = examples =>
        {
            double total = 0;
            int count = 0;
            foreach (string lang in langs)
            {
                var mine = examples.Where(e => ReferenceEquals(e.Predicted.Strings, stores[lang])).ToList();
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
        if (result.Completed && Opt(args, "export", string.Empty) is { Length: > 0 } exportDir)
        {
            Export(encoder, pipeline, langs, exportDir);
        }

        if (result.Completed && Opt(args, "predict-split", string.Empty) is { Length: > 0 } split)
        {
            // Predictions for tools/OneNlp.Eval with the best dev weights: gold tokens and sentences (gold mode), raw text with the language's
            // tokenizer (--raw 1; the parser sets the sentences), and entities on the entity corpus's tokens.
            bool raw = Num(args, "raw", 0) == 1;
            foreach (string lang in langs.Where(l => predictLangs.Length == 0 || predictLangs.Contains(l)))
            {
                (string dir, string prefix) = Treebanks[lang];
                string sub = split == "test" ? "eval" : "train";
                string root = Path.Combine(data, sub, "ud", dir);
                if (!Directory.Exists(root))
                {
                    root = Path.Combine(data, sub, dir);
                }

                string[] names = split == "test" && lang == "it" ? ["it_parlamint-ud-test", "it_markit-ud-test"] : [$"{prefix}-{split}"];
                ITrainableComponent[] mine = [.. pipeline.Components.OfType<ScopedHead>().Where(h => h.Name.EndsWith("-" + lang, StringComparison.Ordinal)).Select(h => h.Inner)];
                ITrainableComponent[] ud = [.. mine.Where(h => h.Kind != "ner")];
                foreach (string name in names)
                {
                    string gold = Path.Combine(root, name + ".conllu");
                    Predict(encoder, ud, gold, stores[lang], Path.Combine(outDir, $"pred-{name}.conllu"));
                    if (raw)
                    {
                        var tokenizer = new Pipeline(UaxWordTokenizer.For(Language.FromCode(lang)), Language.FromCode(lang));
                        PredictRaw(encoder, ud, tokenizer, SegmentationData.For(Language.FromCode(lang)), gold, Path.Combine(outDir, $"pred-raw-{name}.conllu"));
                    }
                }

                if (NerSets.TryGetValue(lang, out var ns) && nerStores.TryGetValue(lang, out StringStore? nerStore))
                {
                    string nerRoot = split == "test" ? Path.Combine(data, "eval", ns.EvalDir) : Path.Combine(data, "train", ns.Dir);
                    PredictNer(encoder, [.. mine.Where(h => h.Kind == "ner")], Path.Combine(nerRoot, $"{ns.Prefix}-{split}.iob2"), lang, nerStore, outDir, split);
                }
            }
        }

        return 0;
    }

    // The best-dev weights as the model package ships them: the encoder's layers (the token table is frozen and comes from the ONNX file, see
    // tools/accurate/split_token_table.py), the encoder's settings and shape, and for each language a pipeline file that holds its heads only,
    // bound to the shared encoder.
    private static void Export(TrainableTransformerEncoder encoder, Pipeline trained, string[] langs, string dir)
    {
        Directory.CreateDirectory(Path.Combine(dir, "heads"));
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

        var shared = new SharedEncoder(encoder, "multilingual-large");
        foreach (string lang in langs)
        {
            Language language = Language.FromCode(lang);
            using Pipeline package = new(UaxWordTokenizer.For(language), language);
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
            Console.WriteLine($"exported {file} {new FileInfo(file).Length} bytes");
        }
    }

    // verify-export --export DIR --lang pt --data DATA [--predict-limit 100]: the packaged model, rebuilt from its files the way the package does
    // (encoder from parts, heads from the per-language file), analyses the first sentences of the test file as raw text; the output is written
    // in the format of --raw predictions so that it can be compared line by line with them.
    private static int VerifyExport(string[] args)
    {
        string dir = Required(args, "export");
        string lang = Required(args, "lang");
        predictLimit = Num(args, "predict-limit", 100);
        (string td, string prefix) = Treebanks[lang];
        string gold = Path.Combine(Required(args, "data"), "eval", "ud", td, lang == "it" ? "it_markit-ud-test.conllu" : $"{prefix}-test.conllu");
        using JsonDocument description = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(dir, "encoder.json")));
        JsonElement shape = description.RootElement.GetProperty("shape");
        Stream[] table = [.. Directory.GetFiles(dir, "token-table-0?.bin").Order(StringComparer.Ordinal).Select(f => (Stream)File.OpenRead(f))];
        using Stream layers = new ConcatenatedFiles([Path.Combine(dir, "encoder-layers.ckpt.part1"), Path.Combine(dir, "encoder-layers.ckpt.part2")]);
        using Stream tokenizerJson = File.OpenRead(Path.Combine(dir, "tokenizer.json"));
        using TrainableTransformerEncoder encoder = TrainableTransformerEncoder.FromParts(
            description.RootElement.GetProperty("settings"),
            shape.GetProperty("vocabularySize").GetInt32(),
            shape.GetProperty("width").GetInt32(),
            shape.GetProperty("layers").GetInt32(),
            shape.GetProperty("feedForwardWidth").GetInt32(),
            tokenizerJson,
            table,
            layers);
        foreach (Stream t in table)
        {
            t.Dispose();
        }

        SharedEncoder.Register("multilingual-large", encoder);
        using Pipeline package = PipelineFile.Load(Path.Combine(dir, "heads", lang + ".onenlp"));
        var tokenizer = new Pipeline(UaxWordTokenizer.For(Language.FromCode(lang)), Language.FromCode(lang));
        string output = Required(args, "out");
        var clock = Stopwatch.StartNew();
        PredictRaw(encoder, [], tokenizer, SegmentationData.For(Language.FromCode(lang)), gold, output, package);
        double seconds = clock.Elapsed.TotalSeconds;
        int words = File.ReadLines(output).Count(l => l.Length > 0 && l[0] != '#' && !l.AsSpan(0, Math.Min(l.Length, 8)).Contains('-'));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"analysed {words} words in {seconds:F1} s with {TensorCompute.Threads} threads: {words / seconds:F0} words/s"));
        return 0;
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

    // Entity types to the family scheme. WikiNER and UNER already use PER, ORG, LOC (and MISC); GermEval's OTH is MISC and its derived and
    // partial variants are outside; Turkish-WikiNER keeps PERSON, ORG, GPE and LOC (GPE joins LOC) and drops its other types.
    private static string MapTag(string tag)
    {
        if (tag.Length < 3)
        {
            return "O";
        }

        string mapped = tag[2..] switch { "PER" or "PERSON" => "PER", "LOC" or "GPE" => "LOC", "ORG" => "ORG", "MISC" or "OTH" => "MISC", _ => string.Empty };
        return mapped.Length == 0 ? "O" : tag[..2] + mapped;
    }

    // An .iob2 file (a '# text = ...' comment, then one token per line: id, form, BIO tag, ...; a blank line ends a sentence) as examples;
    // at most limit sentences, taken evenly through the file so the sample is the same on every run (0 = all).
    private static List<Example> NerExamples(string path, string lang, int limit, StringStore strings)
    {
        var sentences = new List<(string? Text, List<string> Forms, List<string> Tags)>();
        string? text = null;
        var forms = new List<string>();
        var tags = new List<string>();
        foreach (string line in File.ReadLines(path))
        {
            if (line.StartsWith("# text = ", StringComparison.Ordinal))
            {
                text = line["# text = ".Length..];
            }
            else if (line.StartsWith('#'))
            {
                continue;
            }
            else if (line.Length == 0)
            {
                if (forms.Count > 0)
                {
                    sentences.Add((text, forms, tags));
                }

                text = null;
                forms = [];
                tags = [];
            }
            else
            {
                string[] cols = line.Split('\t');
                forms.Add(cols[1]);
                tags.Add(MapTag(cols[2]));
            }
        }

        if (forms.Count > 0)
        {
            sentences.Add((text, forms, tags));
        }

        var result = new List<Example>();
        double step = limit > 0 && sentences.Count > limit ? (double)sentences.Count / limit : 1;
        for (double at = 0; (int)at < sentences.Count; at += step)
        {
            (string? t, List<string> f, List<string> g) = sentences[(int)at];
            result.Add(NerExample(t, f, g, strings));
        }

        return result;
    }

    // Token offsets come from walking the sentence text; if a form is not found there, the forms are joined with spaces instead.
    private static Example NerExample(string? text, List<string> forms, List<string> tags, StringStore strings)
    {
        var starts = new int[forms.Count];
        bool ok = text is not null;
        int pos = 0;
        for (int i = 0; ok && i < forms.Count; i++)
        {
            int at = text!.IndexOf(forms[i], pos, StringComparison.Ordinal);
            if (at < 0)
            {
                ok = false;
                break;
            }

            starts[i] = at;
            pos = at + forms[i].Length;
        }

        if (!ok)
        {
            text = string.Join(' ', forms);
            pos = 0;
            for (int i = 0; i < forms.Count; i++)
            {
                starts[i] = pos;
                pos += forms[i].Length + 1;
            }
        }

        var tokens = new TokenBoundary[forms.Count];
        for (int i = 0; i < tokens.Length; i++)
        {
            tokens[i] = new TokenBoundary(starts[i], forms[i].Length);
        }

        var doc = new Doc(strings, text!, tokens);
        var entities = new List<SpanSpec>();
        foreach (LabeledSpan sp in SpanScorer.ExtractIob2(tags))
        {
            entities.Add(new SpanSpec(sp.Start, sp.End, sp.Label));
        }

        doc.SetEntities(entities.ToArray());
        var marks = new SentenceBoundary[tokens.Length];
        Array.Fill(marks, SentenceBoundary.Inside);
        marks[0] = SentenceBoundary.Start;
        doc.SetSentenceBoundaries(marks);
        return Example.FromReference(doc);
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

        foreach (ConllUSentence s in ConllUReader.Read(gold).Take(predictLimit > 0 ? predictLimit : int.MaxValue))
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
        List<Example> examples = NerExamples(file, lang, split == "test" ? nerTestLimit : 0, store);
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
        var sentences = ConllUReader.Read(gold, store).Take(predictLimit > 0 ? predictLimit : int.MaxValue).ToList();
        var sb = new StringBuilder();
        const int Chunk = 32;
        for (int start = 0; start < sentences.Count; start += Chunk)
        {
            int n = Math.Min(Chunk, sentences.Count - start);
            var docs = new Doc[n];
            for (int i = 0; i < n; i++)
            {
                docs[i] = Example.Blank(Merge([sentences[start + i]], store));
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

    // The text of one sentence as the encoder reads it, with one token for each word. A multiword token whose words spell its surface form
    // (English "don't" = "do" + "n't") is written as it stands; one whose words do not (French "du" = "de" + "le", German "zum" = "zu" + "dem")
    // is written as its words separated by spaces, so that each word has characters of its own and a vector of its own. Inference does the same
    // from the tokenizer's pieces (MultiwordFormsComponent and the encoder's ExpandWordForms).
    private static void AppendWords(ConllUSentence s, StringBuilder text, List<TokenBoundary> tokens)
    {
        int n = s.Doc.Length;
        int m = 0;
        int word = 0;
        while (word < n)
        {
            bool spaceAfter;
            if (m < s.MultiwordTokens.Count && s.MultiwordTokens[m].FirstWord == word)
            {
                MultiwordToken mwt = s.MultiwordTokens[m++];
                int count = mwt.LastWord - mwt.FirstWord + 1;
                bool spelled = string.Concat(Enumerable.Range(word, count).Select(i => s.Forms[i])) == mwt.Form;
                if (spelled)
                {
                    int start = text.Length;
                    text.Append(mwt.Form);
                    int at = start;
                    for (int k = 0; k < count; k++)
                    {
                        tokens.Add(new TokenBoundary(at, s.Forms[word + k].Length));
                        at += s.Forms[word + k].Length;
                    }
                }
                else
                {
                    for (int k = 0; k < count; k++)
                    {
                        if (k > 0)
                        {
                            text.Append(' ');
                        }

                        tokens.Add(new TokenBoundary(text.Length, s.Forms[word + k].Length));
                        text.Append(s.Forms[word + k]);
                    }
                }

                spaceAfter = !mwt.Misc.Contains("SpaceAfter=No", StringComparison.Ordinal);
                word += count;
            }
            else
            {
                tokens.Add(new TokenBoundary(text.Length, s.Forms[word].Length));
                text.Append(s.Forms[word]);
                spaceAfter = !s.Misc[word].Contains("SpaceAfter=No", StringComparison.Ordinal);
                word++;
            }

            if (word < n && spaceAfter)
            {
                text.Append(' ');
            }
        }
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

            int tokenOffset = tokens.Count;
            Doc d = s.Doc;
            AppendWords(s, text, tokens);
            for (int t = 0; t < d.Length; t++)
            {
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
