using System.Globalization;
using System.Text;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;
using RecursiveAI.OneNlp.Neural.Training;
using RecursiveAI.OneNlp.Scoring;

namespace RecursiveAI.OneNlp.Recipes.Chinese;

// The word segmentation part of the Chinese recipe: reference documents whose tokens are the characters (units) of a few consecutive treebank
// sentences, each labelled B (a gold word starts here) or I, a tagger over a fast encoder trained on them with Train, and the segmenter saved
// as a pipeline whose tokenizer it is.
internal static partial class Program
{
    private const int SentencesPerDocument = 8;

    // Groups consecutive sentences into documents joined by a space, the way running text arrives; the space makes the first character of each sentence
    // a word start, as it is in the treebank.
    private static List<Example> SegmentationExamples(string path, out int lost, out int goldWords)
    {
        var result = new List<Example>();
        var strings = new StringStore();
        var text = new StringBuilder();
        var starts = new List<int>();
        int lostCount = 0;
        int goldCount = 0;

        void Flush()
        {
            if (text.Length == 0)
            {
                return;
            }

            string t = text.ToString();
            TokenBoundary[] units = NeuralWordSegmenter.Units(t);
            string[] tags = NeuralWordSegmenter.Tags(units, [.. starts], out int l);
            lostCount += l;
            goldCount += starts.Count;
            var doc = new Doc(strings, t, units);
            for (int i = 0; i < tags.Length; i++)
            {
                doc.SetAnnotation(TokenColumn.Tag, i, tags[i]);
            }

            result.Add(Example.FromReference(doc));
            text.Clear();
            starts.Clear();
        }

        int sentences = 0;
        foreach (ConllUSentence s in ConllUReader.Read(path, strings))
        {
            if (text.Length > 0)
            {
                text.Append(' ');
            }

            int offset = text.Length;
            Doc d = s.Doc;
            text.Append(d.Text);
            foreach (int start in d.GetStarts())
            {
                starts.Add(offset + start);
            }

            if (++sentences % SentencesPerDocument == 0)
            {
                Flush();
            }
        }

        Flush();
        lost = lostCount;
        goldWords = goldCount;
        return result;
    }

    // Word spans of a tag sequence as (first unit, one past the last unit) pairs.
    private static HashSet<(int, int)> WordSpans(Doc doc)
    {
        var spans = new HashSet<(int, int)>();
        int start = 0;
        for (int i = 1; i <= doc.Length; i++)
        {
            if (i == doc.Length || doc[i].Tag == NeuralWordSegmenter.BeginLabel)
            {
                spans.Add((start, i));
                start = i;
            }
        }

        return spans;
    }

    // Development score: word F1 over units, the precision and recall of word spans.
    private static double SegmentationDevScore(IReadOnlyList<Example> examples)
    {
        long correct = 0;
        long predicted = 0;
        long gold = 0;
        foreach (Example e in examples)
        {
            if (e.Reference.Length == 0)
            {
                continue;
            }

            HashSet<(int, int)> g = WordSpans(e.Reference);
            HashSet<(int, int)> p = WordSpans(e.Predicted);
            gold += g.Count;
            predicted += p.Count;
            correct += g.Count(p.Contains);
        }

        return predicted == 0 || gold == 0 ? 0 : 200.0 * correct / (predicted + gold);
    }

    private static int TrainSegmenter(string[] args)
    {
        string outDir = Required(args, "out");
        List<Example> train = SegmentationExamples(Required(args, "train"), out int lostTrain, out int goldTrain);
        List<Example> dev = SegmentationExamples(Required(args, "dev"), out int lostDev, out int goldDev);
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"gold words not representable as unit starts: train {lostTrain}/{goldTrain}, dev {lostDev}/{goldDev}"));

        ulong seed = (ulong)Num(args, "seed", 1);
        FastEncoder encoder = MakeEncoder(args, seed);
        var tagger = new Tagger(encoder, TokenColumn.Tag, NeuralWordSegmenter.HeadName) { OwnsEncoder = true };
        using Pipeline pipeline = Pipeline.Blank(Language.Chinese).Add(tagger);
        int batchWords = Num(args, "batch-words", 2000);
        float lr = Flt(args, "lr", 0.002f);
        int cosineEpochs = Num(args, "cosine-epochs", 0);
        long stepsPerEpoch = Math.Max(1, train.Sum(e => (long)e.Predicted.Length) / batchWords);
        RecursiveAI.OneModel.Training.LrSchedule? schedule = cosineEpochs > 0 ? new RecursiveAI.OneModel.Training.WarmupCosineLr(lr, 200, cosineEpochs * stepsPerEpoch) : null;
        return RunTraining(pipeline, train, new TrainOptions
        {
            Epochs = cosineEpochs > 0 ? cosineEpochs : Num(args, "epochs", 40),
            BatchWords = batchWords,
            LearningRate = lr,
            Schedule = schedule,
            WarmupSteps = 200,
            Dropout = Flt(args, "dropout", 0.1f),
            Seed = seed,
            DevExamples = dev,
            Score = SegmentationDevScore,
            Patience = Num(args, "patience", 6),
            CheckpointDirectory = outDir,
            CheckpointEvery = Num(args, "checkpoint-every", 100),
            Progress = Reporter,
        }, args, outDir, SaveSegmenter);
    }

    // The trained tagger becomes the tokenizer of a pipeline with no components, which is what segmenter.onenlp holds. The tagger stays owned by
    // the training pipeline, which disposes it.
    private static void SaveSegmenter(Pipeline trained, string path)
    {
        var holder = new Pipeline(new NeuralWordSegmenter((Tagger)trained.Components[0]), Language.Chinese);
        PipelineFile.Save(holder, path);
    }
}
