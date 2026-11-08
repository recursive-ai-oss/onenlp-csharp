using System.Globalization;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;
using RecursiveAI.OneNlp.Scoring;

namespace RecursiveAI.OneNlp.Recipes.Hindi;

// Heads for languages related to Hindi, trained on top of the frozen encoder of the accurate tier.
//
// Hindi has no allowed treebank, but Marathi and Sanskrit share its script and much of its grammar, and Sindhi and Punjabi are Indo-Aryan too.
// Cross-lingual transfer works better from related languages than from distant ones (a finding repeated across the multi-source transfer
// papers, e.g. McDonald, Petrov and Hall 2011; Rosa and Zabokrtsky 2015, "KLcpos3 - a language similarity measure for delexicalized parser
// transfer"; Lin et al. 2019, "Choosing transfer languages for cross-lingual learning"). A head set trained here reads the same encoder as the
// nine source languages' heads, so it joins them as one more source in the ensemble (Ensemble.cs) at the price of encoding each training
// sentence once: the encoder is frozen, so its output is cached per document and the epochs after the first only train the small heads.
//
//   train-related --weights DIR --name NAME --train FILE[:N][,FILE[:N]...] --dev FILE[:N][,...] --out DIR
//                 [--heads morphologizer,parser] [--epochs 12] [--seconds S] [--per-doc 4] [--lr 0.002] [--seed 1] [--threads T]
//
// FILE:N takes N sentences spread evenly over the file. The result is OUT/model.onenlp, which `label --extra-heads` reads as source NAME.
internal static partial class Program
{
    private static List<Example> SharedDocuments(string spec, int perDoc, StringStore strings)
    {
        string path = spec;
        int limit = int.MaxValue;
        int colon = spec.LastIndexOf(':');
        if (colon > 1 && int.TryParse(spec[(colon + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out int n))
        {
            path = spec[..colon];
            limit = n;
        }

        List<ConllUSentence> all = [.. ConllUReader.Read(path, strings)];
        var picked = new List<ConllUSentence>();
        if (all.Count <= limit)
        {
            picked.AddRange(all);
        }
        else
        {
            for (int i = 0; i < limit; i++)
            {
                picked.Add(all[(int)((long)i * all.Count / limit)]);
            }
        }

        var result = new List<Example>();
        for (int i = 0; i < picked.Count; i += perDoc)
        {
            result.Add(Example.FromReference(Merge(picked.GetRange(i, Math.Min(perDoc, picked.Count - i)), strings)));
        }

        Console.WriteLine($"{path}: {all.Count} sentences, {picked.Count} used, {result.Sum(e => e.Predicted.Length)} tokens");
        return result;
    }

    private static int TrainRelated(string[] args)
    {
        string outDir = Required(args, "out");
        int perDoc = Num(args, "per-doc", 4);
        string[] heads = Opt(args, "heads", "morphologizer,parser").Split(',');
        var strings = new StringStore();
        var train = new List<Example>();
        var dev = new List<Example>();
        foreach (string spec in Required(args, "train").Split(','))
        {
            train.AddRange(SharedDocuments(spec, perDoc, strings));
        }

        foreach (string spec in Required(args, "dev").Split(','))
        {
            dev.AddRange(SharedDocuments(spec, perDoc, strings));
        }

        using IEncoder encoder = LoadEncoder(Required(args, "weights"));
        SharedEncoder.Register(EncoderName, encoder);
        var shared = new SharedEncoder(encoder, EncoderName);
        using Pipeline pipeline = Pipeline.Blank(Language.Hindi);
        foreach (string head in heads)
        {
            switch (head)
            {
                case "morphologizer":
                    pipeline.Add(new Morphologizer(shared));
                    break;
                case "parser":
                    pipeline.Add(new Parser(shared, arcSize: 512, labelSize: 256, contextLayers: 0));
                    break;
                case "lemmatizer":
                    pipeline.Add(new EditTreeLemmatizer(shared));
                    break;
                default:
                    throw new ArgumentException("Unknown head " + head);
            }
        }

        var metricList = new List<string>();
        if (heads.Contains("morphologizer"))
        {
            metricList.Add("UPOS");
            metricList.Add("UFeats");
        }

        if (heads.Contains("parser"))
        {
            metricList.Add("LAS");
        }

        if (heads.Contains("lemmatizer"))
        {
            metricList.Add("Lemmas");
        }

        string[] metrics = [.. metricList];
        Func<IReadOnlyList<Example>, double> score = examples =>
        {
            double sum = 0;
            var parts = new System.Text.StringBuilder();
            foreach (string m in metrics)
            {
                double v = UdScorer.Score(examples.Select(e => e.Predicted), examples.Select(e => e.Reference), m).Value;
                sum += v;
                parts.Append(CultureInfo.InvariantCulture, $" {m}={v:F2}");
            }

            Console.WriteLine("dev" + parts);
            return sum / metrics.Length;
        };

        int batchWords = Num(args, "batch-words", 1500);
        int epochs = Num(args, "epochs", 12);
        float lr = Flt(args, "lr", 0.002f);
        long stepsPerEpoch = Math.Max(1, train.Sum(e => (long)e.Predicted.Length) / batchWords);
        return RunTraining(pipeline, train, new TrainOptions
        {
            Epochs = epochs,
            BatchWords = batchWords,
            LearningRate = lr,
            Schedule = new RecursiveAI.OneModel.Training.WarmupCosineLr(lr, 100, epochs * stepsPerEpoch),
            Dropout = Flt(args, "dropout", 0.2f),
            Seed = (ulong)Num(args, "seed", 1),
            FreezeEncoder = true,
            DevExamples = dev,
            Score = score,
            Patience = 0,
            CheckpointDirectory = outDir,
            CheckpointEvery = Num(args, "checkpoint-every", 50),
            Progress = Reporter,
        }, args, outDir);
    }

    // lemma-probe --weights DIR --heads FILE.onenlp --gold FILE.conllu --lemmas FILE.tsv
    //
    // Whether a lemmatizer trained on a related language gives Hindi lemmas better than copying the form. The gold file is the hand-annotated
    // sentences and the tsv lists the content words whose lemma differs from the form (testdata/hi/selfdev.lemmas.tsv); only the nouns, verbs
    // and adjectives of the sentences the tsv covers are counted, since the convention for function words is not settled.
    private static int LemmaProbe(string[] args)
    {
        var changed = new Dictionary<(int, int), string>();
        foreach (string line in File.ReadLines(Required(args, "lemmas")).Where(l => l.Length > 0 && l[0] != '#'))
        {
            string[] f = line.Split('\t');
            changed[(int.Parse(f[0], CultureInfo.InvariantCulture), int.Parse(f[1], CultureInfo.InvariantCulture))] = f[3];
        }

        int firstCovered = changed.Keys.Min(k => k.Item1);
        using IEncoder encoder = LoadEncoder(Required(args, "weights"));
        SharedEncoder.Register(EncoderName, encoder);
        using Pipeline pipeline = PipelineFile.Load(Required(args, "heads"));
        EditTreeLemmatizer lemmatizer = pipeline.Components.OfType<EditTreeLemmatizer>().Single();
        int total = 0;
        int model = 0;
        int identity = 0;
        int sentence = 0;
        foreach (ConllUSentence gold in ConllUReader.Read(Required(args, "gold")))
        {
            sentence++;
            if (sentence < firstCovered)
            {
                continue;
            }

            Doc predicted = Example.Blank(gold.Doc);
            Doc[] docs = [predicted];
            lemmatizer.Predict(encoder.Encode(docs), docs);
            for (int t = 0; t < predicted.Length; t++)
            {
                if (gold.Doc[t].Pos is not ("NOUN" or "VERB" or "ADJ"))
                {
                    continue;
                }

                string form = gold.Doc[t].Text;
                string expected = changed.GetValueOrDefault((sentence, t + 1), form);
                total++;
                model += predicted[t].Lemma == expected ? 1 : 0;
                identity += form == expected ? 1 : 0;
                if (args.Contains("--show") && predicted[t].Lemma != expected)
                {
                    Console.WriteLine($"{sentence}:{t + 1} {form} gold={expected} model={predicted[t].Lemma}");
                }
            }
        }

        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"content words={total} model lemma accuracy={100.0 * model / total:F2} identity={100.0 * identity / total:F2}"));
        return 0;
    }
}
