using System.Globalization;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural.Encoders;
using RecursiveAI.OneNlp.Neural.Training;

namespace RecursiveAI.OneNlp.Recipes.Portuguese;

// Portuguese fast-tier recipe. One program for the three parts of the Portuguese pipeline and for scoring them:
//
//   train-tagging --train F.conllu --dev F.conllu --out DIR [options]     tagger (XPOS), morphologizer (UPOS, UFeats), edit-tree lemmatizer
//   train-parser  --train F.conllu --dev F.conllu --out DIR [options]     biaffine dependency parser with sentence starts
//   prepare IN.conllu OUT.conllu                                          the treebank with enclitic forms and "à" kept as one word (train and dev only)
//   ner-convert IN.conllu IN.iob2 OUT.jsonl                               Universal NER (on the treebank's words) to JSON Lines in the tokenizer's tokens
//   ner-train TRAIN.jsonl DEV.jsonl OUT-DIR [options]                     entity recognizer
//   ner-eval MODEL EVAL.jsonl OUT-DIR [--ref ref.tsv]             entity F1 with bootstrap intervals, paired against a reference
//   predict --models DIR [--parts tagging,parser,ner] --gold F.conllu --lexicon TRAIN.conllu --mode raw|gold --out P.conllu
//                                                                         CoNLL-U predictions of the pipeline of the parts found in DIR
//   segment --gold F.conllu --lexicon TRAIN.conllu --out P.conllu                                tokens and sentences of the tokenizer and sentencizer alone
//   speed --models DIR --gold F.conllu --lexicon TRAIN.conllu [--repeat N]                       indicative throughput of the pipeline on raw text
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
                Console.Error.WriteLine("usage: train-tagging|train-parser|prepare|ner-convert|ner-train|ner-eval|predict|segment|speed ... (see the comment at the top of Program.cs)");
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
                "prepare" when args.Length == 3 => Prepare(args[1], args[2]),
                "ner-convert" when args.Length == 4 => NerConvert(args[1], args[2], args[3]),
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

    // The fast encoder of the Portuguese heads. Without encoder options this is the configuration shared by the English heads; each of the
    // options below replaces one setting, which is how the Portuguese settings were chosen on the development set.
    private static FastEncoder MakeEncoder(string[] args, ulong seed)
    {
        string[] options = ["width", "depth", "norm-rows", "prefix-rows", "suffix-rows", "shape-rows", "prefix", "suffix", "cased-rows", "embed-width", "hashes", "recurrent", "recurrent-size", "plain"];
        if (!options.Any(o => args.Contains("--" + o)))
        {
            return FastEncoder.English(seed);
        }

        return new FastEncoder(
            width: Num(args, "width", 256), depth: Num(args, "depth", 2), normRows: Num(args, "norm-rows", 32768), prefixRows: Num(args, "prefix-rows", 32768),
            suffixRows: Num(args, "suffix-rows", 32768), shapeRows: Num(args, "shape-rows", 32768), seed: seed, prefixLength: Num(args, "prefix", 3),
            suffixLength: Num(args, "suffix", 3), casedRows: Num(args, "cased-rows", 0), embedWidth: Num(args, "embed-width", 64), plain: Num(args, "plain", 0) == 1, hashes: Num(args, "hashes", 1), recurrentLayers: Num(args, "recurrent", 0), recurrentSize: Num(args, "recurrent-size", 0));
    }

    private sealed class Progress(Action<TrainProgress> handler) : IProgress<TrainProgress>
    {
        public void Report(TrainProgress value) => handler(value);
    }
}
