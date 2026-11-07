using System.Globalization;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural.Encoders;

namespace RecursiveAI.OneNlp.Recipes.Chinese;

// Chinese fast-tier recipe. One program for the four parts of the Chinese pipeline and for scoring them:
//
//   train-segmenter --train F.conllu --dev F.conllu --out DIR [options]   word segmenter: a B/I tagger over characters (NeuralWordSegmenter)
//   train-tagging   --train F.conllu --dev F.conllu --out DIR [options]   tagger (XPOS), morphologizer (UPOS, UFeats), edit-tree lemmatizer, on gold words
//   train-parser    --train F.conllu --dev F.conllu --out DIR [options]   biaffine dependency parser with sentence starts, on gold words
//   ner-convert IN.iob2 OUT.jsonl                                         UNER Chinese-GSDSIMP to JSON Lines
//   ner-train TRAIN.jsonl DEV.jsonl OUT-DIR [options]                     entity recognizer
//   ner-eval MODEL EVAL.jsonl OUT-DIR [--ref ref.tsv] [--raw SEGMENTER]   entity F1 with bootstrap intervals, paired against a reference
//   predict --models DIR [--parts segmenter,tagging,parser,ner] --gold F.conllu --mode raw|gold --out P.conllu
//                                                                         CoNLL-U predictions of the pipeline of the parts found in DIR
//   segment --models DIR --gold F.conllu --out P.conllu                   words and sentences of the segmenter and sentencizer alone
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
                Console.Error.WriteLine("usage: train-segmenter|train-tagging|train-parser|ner-convert|ner-train|ner-eval|predict|segment|speed ... (see the comment at the top of Program.cs)");
                return 2;
            }

            if (Opt(args, "threads", string.Empty).Length > 0)
            {
                TensorCompute.Threads = Num(args, "threads", 1);
            }

            return args[0] switch
            {
                "train-tagging" => TrainTagging(args),
                "train-parser" => TrainParser(args),
                "train-segmenter" => TrainSegmenter(args),
                "ner-convert" when args.Length >= 3 => NerConvert(args[1], args[2]),
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

    // The fast encoder of the Chinese heads. Without encoder options this is the configuration shared by the English heads; each of the
    // options below replaces one setting, which is how the Chinese settings were chosen on the development set.
    private static FastEncoder MakeEncoder(string[] args, ulong seed)
    {
        string[] options = ["width", "depth", "norm-rows", "prefix-rows", "suffix-rows", "shape-rows", "prefix", "suffix", "cased-rows", "embed-width", "hashes", "recurrent", "recurrent-size", "plain", "relu", "neighbour-rows", "bigram-rows", "context-rows"];
        if (!options.Any(o => args.Contains("--" + o)))
        {
            return FastEncoder.English(seed);
        }

        return new FastEncoder(
            width: Num(args, "width", 256), depth: Num(args, "depth", 2), normRows: Num(args, "norm-rows", 32768), prefixRows: Num(args, "prefix-rows", 32768),
            suffixRows: Num(args, "suffix-rows", 32768), shapeRows: Num(args, "shape-rows", 32768), seed: seed, prefixLength: Num(args, "prefix", 3),
            suffixLength: Num(args, "suffix", 3), casedRows: Num(args, "cased-rows", 0), embedWidth: Num(args, "embed-width", 64), plain: Num(args, "plain", 0) == 1, hashes: Num(args, "hashes", 1), relu: Num(args, "relu", 0) == 1, recurrentLayers: Num(args, "recurrent", 0), recurrentSize: Num(args, "recurrent-size", 0),
            neighbourRows: Num(args, "neighbour-rows", 0), bigramRows: Num(args, "bigram-rows", 0), contextRows: Num(args, "context-rows", 0));
    }

    private sealed class Progress(Action<TrainProgress> handler) : IProgress<TrainProgress>
    {
        public void Report(TrainProgress value) => handler(value);
    }
}
