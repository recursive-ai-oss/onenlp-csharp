using System.Globalization;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural.Encoders;
using RecursiveAI.OneNlp.Neural.Training;

namespace RecursiveAI.OneNlp.Recipes.Turkish;

// Turkish fast-tier recipe. One program for the three parts of the Turkish pipeline and for scoring them:
//
//   train-splitter --train F.conllu --dev F.conllu --out DIR [options]   multiword-token splitter: a B/I tagger over the characters of a token
//   train-tagging --train F.conllu --dev F.conllu --out DIR [options]     tagger (XPOS), morphologizer (UPOS, UFeats), edit-tree lemmatizer
//   train-parser  --train F.conllu --dev F.conllu --out DIR [options]     biaffine dependency parser with sentence starts
//   ner-convert IN.iob2 OUT.jsonl                                         Turkish-WikiNER to JSON Lines (PERSON, ORG, GPE and LOC kept)
//   ner-train TRAIN.jsonl DEV.jsonl OUT-DIR [options]                     entity recognizer
//   ner-eval MODEL EVAL.jsonl OUT-DIR [--ref ref.tsv] [--raw]             entity F1 with bootstrap intervals, paired against a reference
//   predict --models DIR [--parts tagging,parser,ner] --gold F.conllu --mode raw|gold --out P.conllu
//                                                                         CoNLL-U predictions of the pipeline of the parts found in DIR
//   segment --gold F.conllu --out P.conllu                                tokens and sentences of the tokenizer and sentencizer alone
//   speed --models DIR --gold F.conllu [--repeat N]                       indicative throughput of the pipeline on raw text
//
// Every train command checkpoints and resumes: run it again with the same arguments to continue (bit-exact with an uninterrupted run);
// --seconds S stops cleanly after about S seconds. Score predictions with tools/OneNlp.Eval.
internal static partial class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("usage: train-splitter|train-tagging|train-parser|ner-convert|ner-train|ner-eval|predict|segment|speed ... (see the comment at the top of Program.cs)");
                return 2;
            }

            if (Opt(args, "threads", string.Empty).Length > 0)
            {
                TensorCompute.Threads = Num(args, "threads", 1);
            }

            return args[0] switch
            {
                "train-splitter" => TrainSplitter(args),
                "train-tagging" => TrainTagging(args),
                "train-parser" => TrainParser(args),
                "ner-convert" when args.Length == 3 => NerConvert(args[1], args[2]),
                "ner-train" when args.Length >= 4 => NerTrain(args),
                "ner-eval" when args.Length >= 4 => NerEval(args),
                "predict" => Predict(args),
                "segment" => Segment(args),
                "speed" => Speed(args),
                _ => 2,
            };
        }
        catch (Exception e) when (e is ArgumentException or IOException or DataFormatException or InvalidDataException or FormatException or KeyNotFoundException)
        {
            Console.Error.WriteLine("error: " + e.Message);
            return 1;
        }
    }

    private static string Required(string[] args, string name) =>
        Opt(args, name, string.Empty) is { Length: > 0 } v ? v : throw new ArgumentException($"--{name} is required.");

    // The fast encoder of the Turkish heads: the configuration shared by the English heads with the Turkish lower-casing of the norm
    // attribute (I is ı, İ is i), so "KIRMIZI" and "kırmızı" share a row. Each option replaces one setting, which is how the
    // settings were chosen on the development set; --turkic 0 gives the invariant lower-casing for the comparison.
    private static FastEncoder MakeEncoder(string[] args, ulong seed) => new(
        width: Num(args, "width", 256), depth: Num(args, "depth", 2), normRows: Num(args, "norm-rows", 32768), prefixRows: Num(args, "prefix-rows", 32768),
        suffixRows: Num(args, "suffix-rows", 32768), shapeRows: Num(args, "shape-rows", 32768), seed: seed, prefixLength: Num(args, "prefix", 3),
        suffixLength: Num(args, "suffix", 3), casedRows: Num(args, "cased-rows", 0), embedWidth: Num(args, "embed-width", 64), plain: Num(args, "plain", 0) == 1,
        hashes: Num(args, "hashes", 1), turkicCase: Num(args, "turkic", 1) == 1, relu: Num(args, "relu", 0) == 1,
        recurrentLayers: Num(args, "recurrent", 0), recurrentSize: Num(args, "recurrent-size", 0));

    private sealed class Progress(Action<TrainProgress> handler) : IProgress<TrainProgress>
    {
        public void Report(TrainProgress value) => handler(value);
    }
}
