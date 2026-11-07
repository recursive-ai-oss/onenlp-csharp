using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;
using RecursiveAI.OneNlp.Scoring;
using RecursiveAI.OneModel.Tensors;

namespace RecursiveAI.OneNlp.Recipes.Portuguese;

// The named entity part of the Portuguese recipe: Universal NER .iob2 (with the treebank) to JSON Lines, training, and scoring against the reference system's tags.
internal static partial class Program
{
    private static string Opt(string[] args, string name, string fallback)
    {
        int i = Array.IndexOf(args, "--" + name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
    }

    private static int Num(string[] args, string name, int fallback) => int.Parse(Opt(args, name, fallback.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);

    private static float Flt(string[] args, string name, float fallback) => float.Parse(Opt(args, name, fallback.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);

    // ner-convert UD.conllu UNER.iob2 OUT.jsonl
    //
    // Universal NER annotates the words of the treebank (the .iob2 file has the same words as the .conllu file, sentence by sentence). The
    // tokenizer makes other tokens for the same text: the pieces of a contraction ("de" + "o" are the tokens "d" and "o"), the verb and the
    // pronoun with its hyphen ("trata", "-se"), and one token for "à". The examples are written in the tokenizer's tokens, so that the
    // recognizer does not meet "d" for the first time on running text; a token for several words takes the first tag that is not O.
    private static int NerConvert(string conllu, string iob2, string output)
    {
        List<ConllUSentence> sentences = [.. ConllUReader.Read(conllu)];
        List<(List<string> Forms, List<string> Tags)> tagged = ReadIob2(iob2);
        if (sentences.Count != tagged.Count)
        {
            throw new InvalidDataException($"{conllu} has {sentences.Count} sentences and {iob2} has {tagged.Count}.");
        }

        using var writerStream = new FileStream(output, FileMode.Create);
        for (int s = 0; s < sentences.Count; s++)
        {
            ConllUSentence sentence = sentences[s];
            (List<string> wordForms, List<string> wordTags) = tagged[s];
            if (sentence.Forms.Count != wordForms.Count || !sentence.Forms.SequenceEqual(wordForms))
            {
                throw new InvalidDataException($"Sentence {s + 1} differs between {conllu} and {iob2}.");
            }

            var forms = new List<string>();
            var tags = new List<string>();
            var counts = new List<int>();
            for (int i = 0; i < wordForms.Count;)
            {
                MultiwordToken? m = sentence.MultiwordTokens.Where(x => x.FirstWord == i).Select(x => (MultiwordToken?)x).FirstOrDefault();
                int count = m is { } group ? group.LastWord - group.FirstWord + 1 : 1;
                Multiword kind = m is { } g2 ? Classify(g2.Form, count) : Multiword.Pieces;
                if (kind == Multiword.Whole)
                {
                    forms.Add(m!.Value.Form);
                    tags.Add(wordTags.Skip(i).Take(count).FirstOrDefault(t => t != "O") ?? "O");
                    counts.Add(count);
                    i += count;
                }
                else if (kind == Multiword.Enclitic)
                {
                    // The verb and the pronoun with its hyphen, as the tokenizer makes them; each takes its word's tag.
                    string surface = m!.Value.Form;
                    int hyphen = surface.IndexOf('-');
                    forms.Add(surface[..hyphen]);
                    forms.Add(surface[hyphen..]);
                    tags.Add(wordTags[i]);
                    tags.Add(wordTags[i + 1]);
                    counts.Add(1);
                    counts.Add(1);
                    i += 2;
                }
                else
                {
                    forms.Add(sentence.Doc[i].ToString());
                    tags.Add(wordTags[i]);
                    counts.Add(1);
                    i++;
                }
            }

            WriteJsonlLine(writerStream, string.Join(' ', forms), forms, tags, [.. counts], wordTags);
        }

        Console.WriteLine($"{conllu}: {sentences.Count} sentences -> {output}");
        return 0;
    }

    // UNER .iob2: comment lines, then one word per line (id, form, tag, ...), a blank line between sentences.
    private static List<(List<string> Forms, List<string> Tags)> ReadIob2(string path)
    {
        var all = new List<(List<string>, List<string>)>();
        var forms = new List<string>();
        var tags = new List<string>();
        foreach (string line in File.ReadLines(path))
        {
            if (line.StartsWith('#'))
            {
                continue;
            }

            if (line.Length == 0)
            {
                if (forms.Count > 0)
                {
                    all.Add((forms, tags));
                    forms = [];
                    tags = [];
                }

                continue;
            }

            string[] cols = line.Split('\t');
            forms.Add(cols[1]);
            tags.Add(cols[2]);
        }

        if (forms.Count > 0)
        {
            all.Add((forms, tags));
        }

        return all;
    }

    // Per example of an evaluation file: how many words each token stands for and the treebank's tag of each word, or null without them.
    private static List<(int[] Words, string[] Tags)?> ReadSidecar(string path)
    {
        var result = new List<(int[], string[])?>();
        foreach (string line in File.ReadLines(path).Where(l => l.Trim().Length > 0))
        {
            using JsonDocument doc = JsonDocument.Parse(line);
            if (doc.RootElement.TryGetProperty("words", out JsonElement w) && doc.RootElement.TryGetProperty("wordTags", out JsonElement t))
            {
                result.Add(([.. w.EnumerateArray().Select(x => x.GetInt32())], [.. t.EnumerateArray().Select(x => x.GetString() ?? "O")]));
            }
            else
            {
                result.Add(null);
            }
        }

        return result;
    }

    // A token that stands for several words gives its later words I- of its label, or O.
    private static List<string> ToWordTags(List<string> tokenTags, int[] words)
    {
        var result = new List<string>();
        for (int t = 0; t < tokenTags.Count; t++)
        {
            string tag = tokenTags[t];
            result.Add(tag);
            for (int k = 1; k < words[t]; k++)
            {
                result.Add(tag.Length > 2 && tag[0] is 'B' or 'I' ? "I-" + tag[2..] : "O");
            }
        }

        return result;
    }

    private static void WriteJsonlLine(Stream stream, string? text, List<string> forms, List<string> tags, int[] words, List<string> wordTags)
    {
        // Token offsets come from walking the sentence text; if a form is not found there, the forms are joined with spaces instead.
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

        using var w = new Utf8JsonWriter(stream, new JsonWriterOptions { SkipValidation = false });
        w.WriteStartObject();
        w.WriteString("text", text);
        w.WriteStartArray("tokens");
        for (int i = 0; i < forms.Count; i++)
        {
            w.WriteStartArray();
            w.WriteNumberValue(starts[i]);
            w.WriteNumberValue(starts[i] + forms[i].Length);
            w.WriteEndArray();
        }

        w.WriteEndArray();
        w.WriteStartArray("entities");
        foreach (LabeledSpan s in SpanScorer.ExtractIob2(tags))
        {
            w.WriteStartArray();
            w.WriteNumberValue(starts[s.Start]);
            w.WriteNumberValue(starts[s.End - 1] + forms[s.End - 1].Length);
            w.WriteStringValue(s.Label);
            w.WriteEndArray();
        }

        w.WriteEndArray();
        // Beside the example: how many treebank words each token stands for, and the treebank's own tag of each word (ner-eval scores words).
        w.WriteStartArray("words");
        foreach (int n in words)
        {
            w.WriteNumberValue(n);
        }

        w.WriteEndArray();
        w.WriteStartArray("wordTags");
        foreach (string tag in wordTags)
        {
            w.WriteStringValue(tag);
        }

        w.WriteEndArray();
        w.WriteEndObject();
        w.Flush();
        stream.WriteByte((byte)'\n');
    }

    private static int NerTrain(string[] args)
    {
        List<Example> train = [.. JsonlReader.Read(args[1])];
        List<Example> dev = [.. JsonlReader.Read(args[2])];
        string outDir = args[3];
        ulong seed = (ulong)Num(args, "seed", 7);
        if (Opt(args, "threads", string.Empty).Length > 0)
        {
            TensorCompute.Threads = Num(args, "threads", 1);
        }

        NerDecoding decoding = Opt(args, "decoding", "tagging") == "spans" ? NerDecoding.Spans : NerDecoding.Tagging;
        FastEncoder encoder = MakeEncoder(args, seed);
        using Pipeline pipeline = Pipeline.Blank(Language.Portuguese)
            .Add(new EntityRecognizer(encoder, decoding, Num(args, "context", 0), Num(args, "hidden", 256)) { OwnsEncoder = true });

        var clock = Stopwatch.StartNew();
        var progress = new Progress(p =>
        {
            if (p.DevScore is { } score)
            {
                Console.WriteLine($"step={p.Step} epoch={p.Epoch} loss={p.Loss.ToString("F4", CultureInfo.InvariantCulture)} dev_f1={score.ToString("F3", CultureInfo.InvariantCulture)} best={p.IsBest} elapsed_s={clock.Elapsed.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture)}");
            }
        });

        TrainResult result = pipeline.Train(train, new TrainOptions
        {
            Epochs = Num(args, "epochs", 40),
            BatchWords = Num(args, "batch-words", 1000),
            LearningRate = Flt(args, "lr", 0.003f),
            Dropout = Flt(args, "dropout", 0.4f),
            Seed = seed,
            DevExamples = dev,
            Score = DevScores.Entities(),
            Patience = Num(args, "patience", 6),
            CheckpointDirectory = outDir,
            CheckpointEvery = Num(args, "checkpoint-every", 0),
            Progress = progress,
        });

        Console.WriteLine($"steps={result.Steps} epochs={result.Epochs} completed={result.Completed} stopped_early={result.StoppedEarly} best_dev_f1={result.BestScore.ToString("F3", CultureInfo.InvariantCulture)} best_step={result.BestStep}");
        Console.WriteLine($"weights_sha256={result.WeightsSha256}");
        Console.WriteLine($"train_wall_s={clock.Elapsed.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture)} threads={TensorCompute.Threads} decoding={decoding}");
        if (result.Completed)
        {
            string model = Path.Combine(outDir, "model.onenlp");
            PipelineFile.Save(pipeline, model);
            Console.WriteLine($"model={model} bytes={new FileInfo(model).Length}");
        }

        return 0;
    }
    private static List<string> IobTags(Doc doc)
    {
        var tags = new List<string>(doc.Length);
        foreach (Span e in doc.Ents)
        {
            while (tags.Count < e.Start)
            {
                tags.Add("O");
            }

            tags.Add("B-" + e.Label);
            for (int t = e.Start + 1; t < e.End; t++)
            {
                tags.Add("I-" + e.Label);
            }
        }

        while (tags.Count < doc.Length)
        {
            tags.Add("O");
        }

        return tags;
    }

    private static int NerEval(string[] args)
    {
        List<Example> examples = [.. JsonlReader.Read(args[2])];
        Directory.CreateDirectory(args[3]);
        string? refPath = Opt(args, "ref", string.Empty) is { Length: > 0 } r ? r : null;
        bool raw = args.Contains("--raw");
        using Pipeline pipeline = PipelineFile.Load(args[1]);

        var gold = new List<IReadOnlyCollection<LabeledSpan>>();
        var predicted = new List<IReadOnlyCollection<LabeledSpan>>();
        var goldTags = new List<IReadOnlyList<string>>();
        var predictedTags = new List<IReadOnlyList<string>>();
        using var goldFile = new StreamWriter(Path.Combine(args[3], raw ? "gold-raw.tsv" : "gold.tsv"), false, new UTF8Encoding(false)) { NewLine = "\n" };
        using var predFile = new StreamWriter(Path.Combine(args[3], raw ? "pred-raw.tsv" : "pred.tsv"), false, new UTF8Encoding(false)) { NewLine = "\n" };
        List<(int[] Words, string[] Tags)?> sidecar = ReadSidecar(args[2]);
        for (int ex = 0; ex < examples.Count; ex++)
        {
            Example e = examples[ex];
            if (raw)
            {
                // Raw text: our own tokenizer, scored on character offsets.
                Doc doc = pipeline.Process(e.Reference.Text);
                gold.Add([.. e.Reference.Ents.Select(s => new LabeledSpan(s.StartChar, s.EndChar, s.Label))]);
                predicted.Add([.. doc.Ents.Select(s => new LabeledSpan(s.StartChar, s.EndChar, s.Label))]);
                continue;
            }

            Doc blank = Example.Blank(e.Reference);
            pipeline.Process(blank);
            // Scored on the treebank's words: a token that stands for several words ("d" + "o" are two tokens, "trata-se" one token for two words)
            // gives each of its words a tag, the later ones inside the entity or outside it.
            List<string> p = IobTags(blank);
            int[] words = sidecar[ex]?.Words ?? [.. Enumerable.Repeat(1, blank.Length)];
            List<string> g = sidecar[ex] is { } sc ? [.. sc.Tags] : IobTags(e.Reference);
            p = ToWordTags(p, words);
            goldTags.Add(g);
            predictedTags.Add(p);
            for (int t = 0; t < p.Count; t++)
            {
                goldFile.WriteLine(t.ToString(CultureInfo.InvariantCulture) + '\t' + g[t]);
                predFile.WriteLine(t.ToString(CultureInfo.InvariantCulture) + '\t' + p[t]);
            }

            goldFile.WriteLine();
            predFile.WriteLine();
            gold.Add([.. SpanScorer.ExtractIob2(g)]);
            predicted.Add([.. SpanScorer.ExtractIob2(p)]);
        }

        SpanEvaluation ours = SpanScorer.Score(gold, predicted);
        SpanEvaluation? reference = null;
        if (refPath is not null)
        {
            // Columns: word, gold tag, predicted tag. The gold tags must equal ours.
            var refTags = new List<IReadOnlyList<string>>();
            var refGold = new List<IReadOnlyList<string>>();
            var cur = new List<string>();
            var curGold = new List<string>();
            foreach (string line in File.ReadLines(refPath))
            {
                if (line.Length == 0)
                {
                    if (cur.Count > 0)
                    {
                        refTags.Add(cur);
                        refGold.Add(curGold);
                        cur = [];
                        curGold = [];
                    }

                    continue;
                }

                string[] cols = line.Split('\t');
                curGold.Add(cols[1]);
                cur.Add(cols[2]);
            }

            if (refTags.Count != examples.Count || !refGold.Zip(goldTags).All(z => z.First.SequenceEqual(z.Second)))
            {
                throw new InvalidDataException("The reference predictions do not line up with the evaluation file.");
            }

            reference = SpanScorer.ScoreIob2(refGold, refTags);
        }

        Console.WriteLine($"mode={(raw ? "raw-text(char spans)" : "gold-tokens")} sentences={examples.Count} gold_entities={ours.Micro.Gold}");
        PrintRow("micro-F1", ours.PerDocument, reference?.PerDocument, Bootstrap.F1Points);
        PrintRow("micro-precision", ours.PerDocument, reference?.PerDocument, u => Sum(u).Precision * 100);
        PrintRow("micro-recall", ours.PerDocument, reference?.PerDocument, u => Sum(u).Recall * 100);
        foreach (string label in ours.PerLabel.Keys)
        {
            // Per label: only that label's spans, still resampled over sentences.
            SpanEvaluation o = SpanScorer.Score([.. gold.Select(g => (IReadOnlyCollection<LabeledSpan>)[.. g.Where(s => s.Label == label)])], [.. predicted.Select(g => (IReadOnlyCollection<LabeledSpan>)[.. g.Where(s => s.Label == label)])]);
            SpanEvaluation? rf = null;
            if (reference is not null)
            {
                rf = SpanScorer.Score(
                    [.. goldTags.Select(t => (IReadOnlyCollection<LabeledSpan>)[.. SpanScorer.ExtractIob2(t).Where(s => s.Label == label)])],
                    [.. ReadRef(refPath!).Select(t => (IReadOnlyCollection<LabeledSpan>)[.. SpanScorer.ExtractIob2(t).Where(s => s.Label == label)])]);
            }

            PrintRow("F1[" + label + "]", o.PerDocument, rf?.PerDocument, Bootstrap.F1Points);
        }

        return 0;
    }

    private static List<List<string>> ReadRef(string path)
    {
        var all = new List<List<string>>();
        var cur = new List<string>();
        foreach (string line in File.ReadLines(path))
        {
            if (line.Length == 0)
            {
                if (cur.Count > 0)
                {
                    all.Add(cur);
                    cur = [];
                }

                continue;
            }

            cur.Add(line.Split('\t')[2]);
        }

        return all;
    }

    private static MetricCounts Sum(ReadOnlySpan<MetricCounts> units)
    {
        MetricCounts sum = default;
        foreach (MetricCounts c in units)
        {
            sum += c;
        }

        return sum;
    }

    private static void PrintRow(string metric, IReadOnlyList<MetricCounts> ours, IReadOnlyList<MetricCounts>? reference, Statistic<MetricCounts> statistic)
    {
        string F(ConfidenceInterval c) => $"{c.Estimate.ToString("F2", CultureInfo.InvariantCulture)} [{c.Low.ToString("F2", CultureInfo.InvariantCulture)}, {c.High.ToString("F2", CultureInfo.InvariantCulture)}]";
        if (reference is null)
        {
            Console.WriteLine($"row {metric}: ours {F(Bootstrap.Interval(ours, statistic))}");
            return;
        }

        PairedResult p = Bootstrap.Paired(ours, reference, statistic);
        Console.WriteLine($"row {metric}: ours {F(p.First)} reference {F(p.Second)} difference {F(p.Difference)} within_noise={NoiseRule.WithinNoise(p.First, p.Second)}");
    }
}
