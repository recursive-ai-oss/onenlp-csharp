using System.Diagnostics;
using System.Globalization;
using System.Text;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneNlp;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;
using RecursiveAI.OneNlp.Neural.Training;
using RecursiveAI.OneNlp.Scoring;

namespace RecursiveAI.OneNlp.Recipes.EnglishJoint;

// English fast tier, one encoder for every head: tagger (XPOS), morphologizer (UPOS, UFeats), edit-tree lemmatizer, biaffine dependency
// parser with sentence starts, and entity recognizer, trained together by one call to Train.
//
// The two datasets are not the same text: Universal Dependencies English-EWT carries tags, lemmas and trees, UNER English-EWT carries
// entities. Every training example is one or the other, and a head skips the examples that hold no gold for it, so each head learns from
// its own data while all of them shape the shared encoder.
//
//   train   --ud-train F.conllu --ud-dev F.conllu --ner-train F.jsonl --ner-dev F.jsonl --out DIR [options]
//           trains (or resumes from DIR/last.ckpt) until done or the time budget is used; writes DIR/model.onenlp when done.
//           Run it again with the same arguments to continue: the result is the same as one uninterrupted run.
//   predict --model M.onenlp --gold F.conllu --mode raw|gold [--mwt] --out P.conllu
//           writes CoNLL-U predictions (lemma, UPOS, XPOS, features, head, relation) for the OneNlp.Eval tool. "raw" gives the model only
//           the text; "gold" gives it the gold tokens and sentences.
//
// The NER JSONL files are written from the UNER .iob2 files by `recipes/en/ner convert`; entity scores come from `recipes/en/ner eval`.
//
// Options (train): --epochs N (40) --seed N (1) --threads N --budget-seconds S --patience N (8) --lr X --dropout X --batch-words N
//   encoder: --width --embed-width --depth --norm-rows --prefix-rows --suffix-rows --shape-rows --prefix --suffix --hashes --relu --neighbour-rows --context-rows --bigram-rows
//   heads:   --heads all|tagging  --min-class-count N  --arc N --label N --context N --lstm N --lstm-layers N --ner-hidden N
internal static class Program
{
    private static readonly string[] Clitics =
        ["n't", "n\u2019t", "'s", "\u2019s", "'re", "\u2019re", "'ve", "\u2019ve", "'ll", "\u2019ll", "'d", "\u2019d", "'m", "\u2019m"];

    private static readonly string[] Fused = ["cannot", "gonna", "gotta", "wanna", "gimme", "lemme"];

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("usage: train|predict ... (see the comment at the top of Program.cs)");
                return 2;
            }

            Dictionary<string, string> o = Options(args.AsSpan(1));
            if (o.TryGetValue("threads", out string? threads))
            {
                TensorCompute.Threads = int.Parse(threads, CultureInfo.InvariantCulture);
            }

            return args[0] switch
            {
                "train" => Train(o),
                "predict" => Predict(o),
                _ => 2,
            };
        }
        catch (Exception e) when (e is ArgumentException or IOException or DataFormatException or InvalidDataException or FormatException or KeyNotFoundException)
        {
            Console.Error.WriteLine("error: " + e.Message);
            return 1;
        }
    }

    private static Dictionary<string, string> Options(ReadOnlySpan<string> args)
    {
        var o = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException("Unexpected argument '" + args[i] + "'.");
            }

            string name = args[i][2..];
            bool flag = i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal);
            o[name] = flag ? "true" : args[++i];
        }

        return o;
    }

    private static int Int(Dictionary<string, string> o, string name, int fallback) =>
        o.TryGetValue(name, out string? v) ? int.Parse(v, CultureInfo.InvariantCulture) : fallback;

    private static float Float(Dictionary<string, string> o, string name, float fallback) =>
        o.TryGetValue(name, out string? v) ? float.Parse(v, CultureInfo.InvariantCulture) : fallback;

    // The shared encoder. Defaults are the shipped configuration; every number can be changed from the command line to try another shape.
    private static FastEncoder NewEncoder(Dictionary<string, string> o, ulong seed) =>
        new(
            width: Int(o, "width", 128),
            depth: Int(o, "depth", 1),
            normRows: Int(o, "norm-rows", 32768),
            prefixRows: Int(o, "prefix-rows", 16384),
            suffixRows: Int(o, "suffix-rows", 16384),
            shapeRows: Int(o, "shape-rows", 4096),
            seed: seed,
            prefixLength: Int(o, "prefix", 3),
            suffixLength: Int(o, "suffix", 3),
            embedWidth: Int(o, "embed-width", 32),
            hashes: Int(o, "hashes", 1),
            relu: !o.ContainsKey("gelu"),
            neighbourRows: Int(o, "neighbour-rows", 16384),
            bigramRows: Int(o, "bigram-rows", 0),
            contextRows: Int(o, "context-rows", 0));

    private static Pipeline NewPipeline(Dictionary<string, string> o, ulong seed)
    {
        FastEncoder encoder = NewEncoder(o, seed);
        Pipeline pipeline = Pipeline.Blank(Language.English);
        pipeline.Add(new Tagger(encoder, TokenColumn.Tag) { OwnsEncoder = true });
        pipeline.Add(new Morphologizer(encoder, headClasses: Int(o, "morph-head", 64)));
        pipeline.Add(new EditTreeLemmatizer(encoder, minClassCount: Int(o, "min-class-count", 2), headClasses: Int(o, "lemma-head", 32)));
        if (o.GetValueOrDefault("heads", "all") == "all")
        {
            pipeline.Add(new Parser(encoder, Int(o, "arc", 192), Int(o, "label", 96), Int(o, "context", 3), Int(o, "lstm", 0), Int(o, "lstm-layers", 1), labelBilinear: !o.ContainsKey("no-bilinear")));
            pipeline.Add(new EntityRecognizer(encoder, NerDecoding.Tagging, Int(o, "ner-context", 0), Int(o, "ner-hidden", 256)));
        }

        return pipeline;
    }

    private static bool HasEntities(Example e) => e.Reference.Length > 0 && e.Reference[0].EntIob != EntIob.Unset;

    private static bool HasTrees(Example e) => e.Reference.GetColumn(TokenColumn.Dep).ContainsAnyExcept(0);

    // The development score: the mean of three numbers, so that no head is given up for another: the mean of UPOS, XPOS, UFeats and
    // Lemmas, LAS, and entity F1. Each is computed on the dev documents that carry its gold, given gold tokens.
    private static double DevScore(IReadOnlyList<Example> examples)
    {
        Example[] ud = [.. examples.Where(HasTrees)];
        Example[] ner = [.. examples.Where(HasEntities)];
        double tagging = 0;
        foreach (string metric in new[] { "UPOS", "XPOS", "UFeats", "Lemmas" })
        {
            tagging += UdScorer.Score(ud.Select(e => e.Predicted), ud.Select(e => e.Reference), metric).Value;
        }

        tagging /= 4;
        double las = ud.Length > 0 && ud[0].Predicted.GetColumn(TokenColumn.Dep).ContainsAnyExcept(0)
            ? UdScorer.Score(ud.Select(e => e.Predicted), ud.Select(e => e.Reference), "LAS").Value
            : 0;
        double f1 = ner.Length > 0 && ner.Any(e => e.Predicted.Ents.Count > 0)
            ? SpanScorer.EntityScore([.. ner.Select(e => e.Predicted)], [.. ner.Select(e => e.Reference)]).Value
            : 0;
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  dev: tagging={tagging:F2} las={las:F2} ner_f1={f1:F2}"));
        return ud.Length > 0 && ner.Length > 0 ? (tagging + las + f1) / 3 : tagging;
    }

    // Tagging-only runs (--heads tagging) are judged on the tagging mean alone.
    private static double TaggingScore(IReadOnlyList<Example> examples)
    {
        Example[] ud = [.. examples.Where(HasTrees)];
        double sum = 0;
        foreach (string metric in new[] { "UPOS", "XPOS", "UFeats", "Lemmas" })
        {
            sum += UdScorer.Score(ud.Select(e => e.Predicted), ud.Select(e => e.Reference), metric).Value;
        }

        return sum / 4;
    }

    private static int Train(Dictionary<string, string> o)
    {
        string outDir = o["out"];
        bool all = o.GetValueOrDefault("heads", "all") == "all";
        int epochs = Int(o, "epochs", 40);
        ulong seed = ulong.Parse(o.GetValueOrDefault("seed", "1"), CultureInfo.InvariantCulture);
        double budget = o.TryGetValue("budget-seconds", out string? b) ? double.Parse(b, CultureInfo.InvariantCulture) : double.PositiveInfinity;
        int perDoc = Int(o, "sentences-per-doc", 10);

        var strings = new StringStore();
        List<Example> train = Documents(o["ud-train"], perDoc, strings);
        List<Example> dev = Documents(o["ud-dev"], perDoc, strings);
        int cap = Int(o, "max-train-docs", int.MaxValue); // screening and smoke runs use a slice of the training data
        train = [.. train.Take(cap)];
        if (all)
        {
            train.AddRange(JsonlReader.Read(o["ner-train"], strings).Take(cap == int.MaxValue ? int.MaxValue : cap * 10));
            dev.AddRange(JsonlReader.Read(o["ner-dev"], strings));
        }

        using Pipeline pipeline = NewPipeline(o, seed);
        using var cts = new CancellationTokenSource();
        if (!double.IsPositiveInfinity(budget))
        {
            cts.CancelAfter(TimeSpan.FromSeconds(budget));
        }

        var clock = Stopwatch.StartNew();
        var progress = new Progress(p =>
        {
            if (p.DevScore is { } score)
            {
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"step={p.Step} epoch={p.Epoch} loss={p.Loss:F4} dev={score:F3} best={p.IsBest} elapsed_s={clock.Elapsed.TotalSeconds:F1}"));
            }
        });

        TrainResult result = pipeline.Train(
            train,
            new TrainOptions
            {
                Epochs = epochs,
                MaxSteps = Int(o, "max-steps", 0),
                BatchWords = Int(o, "batch-words", 1200),
                LearningRate = Float(o, "lr", 0.002f),
                WarmupSteps = 200,
                Dropout = Float(o, "dropout", 0.25f),
                Seed = seed,
                DevExamples = dev,
                Score = all ? DevScore : TaggingScore,
                Patience = Int(o, "patience", 8),
                CheckpointDirectory = outDir,
                CheckpointEvery = Int(o, "checkpoint-every", 100),
                Progress = progress,
            },
            cts.Token);

        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"completed={result.Completed} steps={result.Steps} epochs={result.Epochs} evaluations={result.Evaluations} best_dev={result.BestScore:F3} best_step={result.BestStep} stopped_early={result.StoppedEarly}"));
        Console.WriteLine($"weights_sha256={result.WeightsSha256}");
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"chunk_wall_s={clock.Elapsed.TotalSeconds:F1} threads={TensorCompute.Threads}"));
        if (result.Completed)
        {
            string model = Path.Combine(outDir, "model.onenlp");
            PipelineFile.Save(pipeline, model);
            Console.WriteLine($"model={model} bytes={new FileInfo(model).Length}");
        }

        return 0;
    }

    // Groups consecutive sentences into documents, the way running text arrives: a parser that only ever saw single sentences could not
    // learn where one ends. Tags, features, lemmas, heads and relations are carried over.
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
        TokenColumn[] columns = [TokenColumn.Dep, TokenColumn.Pos, TokenColumn.Tag, TokenColumn.Morph, TokenColumn.Lemma];
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
                for (int c = 0; c < columns.Length; c++)
                {
                    values[c].Add(d.GetColumn(columns[c])[t]);
                }

                marks.Add(t == 0 ? SentenceBoundary.Start : SentenceBoundary.Inside);
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

    private static int Predict(Dictionary<string, string> o)
    {
        bool raw = o["mode"] == "raw";
        bool mwt = o.ContainsKey("mwt");
        using Pipeline pipeline = PipelineFile.Load(o["model"]);
        var sb = new StringBuilder();
        int tokens = 0;
        if (raw)
        {
            // Ten gold sentences at a time, joined by one space as the text of a running document.
            var chunk = new List<string>();
            foreach (ConllUSentence sentence in ConllUReader.Read(o["gold"]))
            {
                chunk.Add(sentence.Doc.Text);
                if (chunk.Count == 10)
                {
                    tokens += WriteRaw(pipeline, string.Join(' ', chunk), mwt, sb);
                    chunk.Clear();
                }
            }

            if (chunk.Count > 0)
            {
                tokens += WriteRaw(pipeline, string.Join(' ', chunk), mwt, sb);
            }
        }
        else
        {
            foreach (ConllUSentence sentence in ConllUReader.Read(o["gold"]))
            {
                Doc predicted = Example.Blank(sentence.Doc);
                predicted.SetSentenceBoundaries(sentence.Doc.GetSentenceBoundaries());
                foreach (IPipelineComponent component in pipeline.Components.Where(c => c is ITrainableComponent))
                {
                    component.Process(predicted);
                }

                tokens += predicted.Length;
                foreach (string comment in sentence.Comments.Where(c => c.StartsWith("# sent_id", StringComparison.Ordinal)))
                {
                    sb.Append(comment).Append('\n');
                }

                int next = 0;
                for (int i = 0; i < predicted.Length; i++)
                {
                    if (next < sentence.MultiwordTokens.Count && sentence.MultiwordTokens[next].FirstWord == i)
                    {
                        MultiwordToken m = sentence.MultiwordTokens[next++];
                        sb.Append(m.FirstWord + 1).Append('-').Append(m.LastWord + 1).Append('\t').Append(m.Form).Append("\t_\t_\t_\t_\t_\t_\t_\t_\n");
                    }

                    WriteWord(sb, predicted, i, 0, i, sentence.Forms[i]);
                }

                sb.Append('\n');
            }
        }

        File.WriteAllText(o["out"], sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"tokens={tokens}");
        return 0;
    }

    private static int WriteRaw(Pipeline pipeline, string text, bool mwt, StringBuilder sb)
    {
        Doc doc = pipeline.Process(text);
        foreach (Span sentence in doc.Sentences)
        {
            var ids = new List<int>();
            foreach (Token token in sentence)
            {
                ids.Add(token.Index);
            }

            for (int k = 0; k < ids.Count; k++)
            {
                int groupEnd = k;
                if (mwt && k + 1 < ids.Count && doc[ids[k]].EndIdx == doc[ids[k + 1]].Idx)
                {
                    string second = doc[ids[k + 1]].ToString();
                    string whole = doc[ids[k]].ToString() + second;
                    if (Array.Exists(Clitics, c => string.Equals(c, second, StringComparison.OrdinalIgnoreCase))
                        || Array.Exists(Fused, f => string.Equals(f, whole, StringComparison.OrdinalIgnoreCase)))
                    {
                        groupEnd = k + 1;
                    }
                }

                if (groupEnd > k)
                {
                    sb.Append(k + 1).Append('-').Append(groupEnd + 1).Append('\t')
                        .Append(text, doc[ids[k]].Idx, doc[ids[groupEnd]].EndIdx - doc[ids[k]].Idx)
                        .Append("\t_\t_\t_\t_\t_\t_\t_\t_\n");
                }

                for (int j = k; j <= groupEnd; j++)
                {
                    WriteWord(sb, doc, ids[j], sentence.Start, j, doc[ids[j]].ToString());
                }

                k = groupEnd;
            }

            sb.Append('\n');
        }

        return doc.Length;
    }

    // HEAD is the position in the sentence (1-based, 0 for the root); sentenceStart is the document index of the sentence's first token.
    private static void WriteWord(StringBuilder sb, Doc doc, int tokenIndex, int sentenceStart, int position, string form)
    {
        Token t = doc[tokenIndex];
        int head = t.HeadIndex == tokenIndex ? 0 : t.HeadIndex - sentenceStart + 1;
        string deprel = head == 0 ? "root" : Cell(t.Dep);
        sb.Append(position + 1).Append('\t').Append(form).Append('\t').Append(Cell(t.Lemma)).Append('\t').Append(Cell(t.Pos)).Append('\t')
            .Append(Cell(t.Tag)).Append('\t').Append(Cell(t.Morph)).Append('\t').Append(head).Append('\t').Append(deprel).Append("\t_\t_\n");
    }

    private static string Cell(string value) => value.Length == 0 ? "_" : value;

    private sealed class Progress(Action<TrainProgress> handler) : IProgress<TrainProgress>
    {
        public void Report(TrainProgress value) => handler(value);
    }
}
