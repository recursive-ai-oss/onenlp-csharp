using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;
using RecursiveAI.OneNlp.Scoring;

namespace RecursiveAI.OneNlp.Recipes.Accurate;

internal static partial class Program
{
    // Parser-only adaptation: the shipped graph is frozen, and the loaded parser alone enters Train.
    // Disk-cached vectors survive chunk boundaries and also serve the fresh dev documents Train creates.
    private static int RecoverParser(string[] args)
    {
        string lang = Required(args, "lang");
        if (!"en,de,fr,es,pt,it,nl,tr,zh".Split(',').Contains(lang))
        {
            throw new ArgumentException("Parser recovery is limited to the original nine languages.");
        }

        string export = Required(args, "export");
        string output = Required(args, "out");
        Directory.CreateDirectory(output);
        var spec = CorpusSpec.Load(Required(args, "spec"));
        LanguageData data = CorpusReader.ReadUd(spec, spec[lang], Required(args, "data"), Num(args, "sentences-per-doc", 4));
        data = data with { Dev = CorpusReader.ReadUd(spec, spec[lang], Required(args, "data"), 1).Dev };
        if (data.Dev.Count == 0)
        {
            throw new ArgumentException("Full development data is required for selection.");
        }

        int smokeLimit = Num(args, "smoke-docs", 0);
        if (smokeLimit > 0)
        {
            data.Train.RemoveRange(Math.Min(smokeLimit, data.Train.Count), Math.Max(0, data.Train.Count - smokeLimit));
            data.Dev.RemoveRange(Math.Min(smokeLimit, data.Dev.Count), Math.Max(0, data.Dev.Count - smokeLimit));
        }

        Console.WriteLine($"{lang}: train={data.Train.Count} docs/{data.Train.Sum(e => e.Predicted.Length)} words dev={data.Dev.Count} docs/{data.Dev.Sum(e => e.Predicted.Length)} words held_out={data.HeldOut} dropped={data.Dropped}");
        string graphPath = Path.Combine(export, "encoder.onnx");
        using var graph = new TransformerEncoder(graphPath, Path.Combine(export, "tokenizer.json"), expandWordForms: true);
        string graphHash = HashFile(graphPath);
        string tokenizerHash = HashFile(Path.Combine(export, "tokenizer.json"));
        using var cached = new RecoveryCache(graph, Path.Combine(Required(args, "cache"), graphHash + "-" + tokenizerHash));
        SharedEncoder.Register("multilingual-large", cached);
        try
        {
            string source = Path.Combine(export, "heads", lang + ".onenlp");
            using Pipeline original = PipelineFile.Load(source);
            Parser parser = original.Components.OfType<Parser>().Single();
            Dictionary<string, string> untouched = HeadHashes(original, parser);
            MetricCounts[] baseline = RecoveryCounts(parser, data.Dev);
            File.WriteAllText(Path.Combine(output, "baseline.tsv"), CountsText(baseline));
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"baseline full-dev LAS={Bootstrap.F1Points(baseline):F4}"));
            using Pipeline train = Pipeline.Blank(original.Language);
            train.Add(parser);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Num(args, "budget-seconds", 1000)));
            TrainResult result = train.Train(data.Train, new TrainOptions
            {
                Epochs = Num(args, "epochs", 8),
                BatchWords = Num(args, "batch-words", 1500),
                LearningRate = Flt(args, "lr", 0.0002f),
                Dropout = Flt(args, "dropout", 0.1f),
                Seed = (ulong)Num(args, "seed", 66),
                FreezeEncoder = true,
                DevExamples = data.Dev,
                Score = _ => Bootstrap.F1Points(RecoveryCounts(parser, data.Dev)),
                Patience = 3,
                CheckpointDirectory = output,
                CheckpointEvery = 25,
                Progress = new RecoveryProgress(),
            }, cts.Token);
            MetricCounts[] candidate = RecoveryCounts(parser, data.Dev, out string candidatePredictions);
            PairedResult paired = Bootstrap.Paired(candidate, baseline, Bootstrap.F1Points, seed: 66);
            // Keep only an improvement separated from zero by the paired interval; otherwise the previous head wins.
            bool keep = paired.Difference.Low > 0 && smokeLimit == 0;
            string final = Path.Combine(output, "selected.onenlp");
            string candidateFile = Path.Combine(output, "candidate.onenlp");
            PipelineFile.Save(original, candidateFile);
            File.Copy(keep ? candidateFile : source, final, overwrite: true);

            Dictionary<string, string> after = HeadHashes(original, parser);
            if (untouched.Any(p => after[p.Key] != p.Value))
            {
                throw new InvalidOperationException("A non-parser head changed during parser-only training.");
            }

            // Compare actual head/label predictions and the non-parser tensor/configuration bytes after reloading.
            using (Pipeline loaded = PipelineFile.Load(candidateFile))
            {
                Parser loadedParser = loaded.Components.OfType<Parser>().Single();
                MetricCounts[] roundtrip = RecoveryCounts(loadedParser, data.Dev, out string roundtripPredictions);
                if (!roundtrip.SequenceEqual(candidate) || candidatePredictions != roundtripPredictions)
                {
                    throw new InvalidOperationException("Parser export changed development predictions.");
                }

                Dictionary<string, string> loadedHashes = HeadHashes(loaded, loadedParser);
                if (untouched.Any(p => loadedHashes[p.Key] != p.Value))
                {
                    throw new InvalidOperationException("Export changed a non-parser head.");
                }
            }

            File.WriteAllText(Path.Combine(output, "candidate.tsv"), CountsText(candidate));
            using var f = File.Create(Path.Combine(output, "selection.json"));
            using var w = new Utf8JsonWriter(f, new JsonWriterOptions { Indented = true });
            w.WriteStartObject();
            w.WriteString("language", lang);
            w.WriteBoolean("completed", result.Completed);
            w.WriteBoolean("smoke", smokeLimit > 0);
            w.WriteNumber("steps", result.Steps);
            w.WriteNumber("epochs", result.Epochs);
            w.WriteNumber("bestStep", result.BestStep);
            w.WriteBoolean("keepCandidate", keep);
            w.WriteString("encoderSha256", graphHash);
            w.WriteString("tokenizerSha256", tokenizerHash);
            w.WriteString("specSha256", HashFile(Required(args, "spec")));
            w.WriteString("candidatePredictionsSha256", candidatePredictions);
            w.WriteString("sourceSha256", HashFile(source));
            w.WriteString("selectedSha256", HashFile(final));
            WriteInterval(w, "baseline", paired.Second);
            WriteInterval(w, "candidate", paired.First);
            WriteInterval(w, "difference", paired.Difference);
            w.WriteStartObject("unchangedHeads");
            foreach (var p in untouched)
            {
                w.WriteString(p.Key, p.Value);
            }

            w.WriteEndObject();
            w.WriteEndObject();
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"candidate={paired.First.Estimate:F4} baseline={paired.Second.Estimate:F4} delta={paired.Difference.Estimate:F4} [{paired.Difference.Low:F4},{paired.Difference.High:F4}] keep={keep} completed={result.Completed}"));
            return 0;
        }
        finally
        {
            SharedEncoder.Unregister("multilingual-large");
        }
    }

    private static void WriteInterval(Utf8JsonWriter w, string name, ConfidenceInterval ci)
    {
        w.WriteStartObject(name);
        w.WriteNumber("estimate", ci.Estimate);
        w.WriteNumber("low", ci.Low);
        w.WriteNumber("high", ci.High);
        w.WriteNumber("resamples", ci.Resamples);
        w.WriteNumber("seed", ci.Seed);
        w.WriteEndObject();
    }

    private static string HashFile(string path)
    {
        using FileStream f = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(f));
    }

    private static Dictionary<string, string> HeadHashes(Pipeline pipeline, Parser parser)
    {
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (ITrainableComponent h in pipeline.Components.OfType<ITrainableComponent>().Where(h => !ReferenceEquals(h, parser)))
        {
            using var stream = new MemoryStream();
            using (var w = new Utf8JsonWriter(stream))
            {
                w.WriteStartObject();
                h.WriteConfig(w);
                w.WriteEndObject();
            }

            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            hash.AppendData(stream.ToArray());
            using var state = new MemoryStream();
            Checkpoint.Save(state, h.Weights);
            hash.AppendData(state.ToArray());

            hashes.Add(h.Name, Convert.ToHexStringLower(hash.GetHashAndReset()));
        }

        return hashes;
    }

    private static MetricCounts[] RecoveryCounts(Parser parser, IReadOnlyList<Example> examples)
        => RecoveryCounts(parser, examples, out _);

    private static MetricCounts[] RecoveryCounts(Parser parser, IReadOnlyList<Example> examples, out string predictionsHash)
    {
        var predicted = examples.Select(e => Example.Blank(e.Reference)).ToArray();
        // Gold sentence boundaries stay fixed; one resampling unit is one sentence, not a multi-sentence document.
        for (int i = 0; i < predicted.Length; i++)
        {
            predicted[i].SetSentenceBoundaries(examples[i].Reference.GetSentenceBoundaries());
            parser.Process(predicted[i]);
        }

        using var bytes = new MemoryStream();
        using (var w = new BinaryWriter(bytes, Encoding.UTF8, leaveOpen: true))
        {
            foreach (Doc d in predicted)
            {
                w.Write(d.Length);
                for (int t = 0; t < d.Length; t++)
                {
                    w.Write(d.GetHeads()[t]);
                    w.Write(d[t].Dep);
                }
            }
        }

        predictionsHash = Convert.ToHexStringLower(SHA256.HashData(bytes.ToArray()));
        return UdScorer.Evaluate(UdCorpus.FromDocs(examples.Select(e => e.Reference)), UdCorpus.FromDocs(predicted)).PerUnit("LAS").ToArray();
    }

    private static string CountsText(IEnumerable<MetricCounts> counts) => string.Join('\n', counts.Select(c => string.Create(CultureInfo.InvariantCulture, $"{c.Gold}\t{c.System}\t{c.Correct}\t{c.Aligned}"))) + "\n";

    private sealed class RecoveryProgress : IProgress<TrainProgress>
    {
        public void Report(TrainProgress p)
        {
            if (p.Step % 25 == 0 || p.DevScore.HasValue)
            {
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"step={p.Step} epoch={p.Epoch} loss={p.Loss:F5} dev={p.DevScore:F4} best={p.IsBest}"));
            }
        }
    }
}
