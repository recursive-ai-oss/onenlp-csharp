using System.Globalization;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;
using RecursiveAI.OneNlp.Neural.Training;
using RecursiveAI.OneNlp.Segmentation;

namespace RecursiveAI.OneNlp.Recipes.Turkish;

// The multiword-token part of the Turkish recipe. UD Turkish writes a copula or a suffix spelled onto its word as a word of its own
// (vardır = var + dır). Each sentence of the treebank becomes one reference document of units (a unit for each character of a letters-only
// token, see NeuralMultiwordSplitter.MakeWindow), labelled B where a UD word starts and I elsewhere; a tagger over a small fast encoder is trained on them with Train, and the
// result is saved as the tokenizer of a pipeline (NeuralMultiwordSplitter over the whole-word Turkish tokenizer).
internal static partial class Program
{
    // Turkish lower case, to tell whether the words of a multiword token spell its surface form.
    private static string Lower(string s)
    {
        var chars = new char[s.Length];
        for (int i = 0; i < s.Length; i++)
        {
            chars[i] = s[i] == 'I' ? '\u0131' : s[i] == '\u0130' ? 'i' : char.ToLowerInvariant(s[i]);
        }

        return new string(chars);
    }

    // One example per window of surface tokens (a sentence, or 40 tokens of a long one). A multiword token whose words spell its surface form
    // is cut where the words end; one of two words that does not (a letter changes: 'işçiydi' = 'işçi' + 'di') is cut after the letters its first word
    // shares with the surface form; any other is counted in 'lost' and labelled as one word.
    private static List<Example> SplitterExamples(string path, out int lost, out int multiword, out int total)
    {
        var result = new List<Example>();
        var strings = new StringStore();
        int lostCount = 0;
        int multiwordCount = 0;
        int totalCount = 0;
        foreach (ConllUSentence s in ConllUReader.Read(path, strings))
        {
            var surface = new List<string>();
            var pieces = new List<int[]>();
            var groupAt = new Dictionary<int, MultiwordToken>();
            foreach (MultiwordToken m in s.MultiwordTokens)
            {
                groupAt[m.FirstWord] = m;
            }

            for (int w = 0; w < s.Forms.Count; w++)
            {
                if (groupAt.TryGetValue(w, out MultiwordToken m))
                {
                    string[] words = [.. Enumerable.Range(m.FirstWord, m.LastWord - m.FirstWord + 1).Select(k => s.Forms[k])];
                    int[] cut = [m.Form.Length];
                    if (NeuralMultiwordSplitter.IsCandidate(m.Form) && words.Length >= 2)
                    {
                        if (Lower(string.Concat(words)) == Lower(m.Form))
                        {
                            cut = [.. words.Select(x => x.Length)];
                        }
                        else if (words.Length == 2)
                        {
                            string f = Lower(m.Form);
                            string first = Lower(words[0]);
                            int shared = 0;
                            while (shared < first.Length && shared < f.Length && first[shared] == f[shared])
                            {
                                shared++;
                            }

                            cut = shared >= 1 && shared < f.Length ? [shared, f.Length - shared] : cut;
                        }
                    }

                    if (cut.Length == 1)
                    {
                        lostCount++;
                    }

                    multiwordCount++;
                    surface.Add(m.Form);
                    pieces.Add(cut);
                    w = m.LastWord;
                }
                else
                {
                    surface.Add(s.Forms[w]);
                    pieces.Add([s.Forms[w].Length]);
                }
            }

            totalCount += surface.Count;
            foreach ((int from, int to) in NeuralMultiwordSplitter.Windows(surface))
            {
                Doc doc = NeuralMultiwordSplitter.MakeWindow(strings, surface.GetRange(from, to - from), out int[] firstUnit);
                var tags = new string[doc.Length];
                Array.Fill(tags, NeuralMultiwordSplitter.BeginLabel);
                for (int k = from; k < to; k++)
                {
                    if (NeuralMultiwordSplitter.IsCandidate(surface[k]))
                    {
                        string[] t = NeuralMultiwordSplitter.Tags(surface[k].Length, pieces[k]);
                        Array.Copy(t, 0, tags, firstUnit[k - from], t.Length);
                    }
                }

                for (int i = 0; i < tags.Length; i++)
                {
                    doc.SetAnnotation(TokenColumn.Tag, i, tags[i]);
                }

                result.Add(Example.FromReference(doc));
            }
        }

        lost = lostCount;
        multiword = multiwordCount;
        total = totalCount;
        return result;
    }

    // Development score: F1 of the cut positions (units inside a candidate token labelled B), so the many uncut tokens cannot hide the cuts.
    private static double SplitterDevScore(IReadOnlyList<Example> examples)
    {
        long correct = 0;
        long predicted = 0;
        long gold = 0;
        foreach (Example e in examples)
        {
            foreach (int i in NeuralMultiwordSplitter.InteriorUnits(e.Reference))
            {
                bool g = e.Reference[i].Tag == NeuralMultiwordSplitter.BeginLabel;
                bool p = e.Predicted[i].Tag == NeuralMultiwordSplitter.BeginLabel;
                gold += g ? 1 : 0;
                predicted += p ? 1 : 0;
                correct += g && p ? 1 : 0;
            }
        }

        return predicted + gold == 0 ? 100 : 200.0 * correct / (predicted + gold);
    }

    // A small encoder: the units are single characters, so the tables are small and the recurrent layer reads the whole token.
    private static FastEncoder MakeSplitterEncoder(string[] args, ulong seed) => new(
        width: Num(args, "width", 128), depth: Num(args, "depth", 2), normRows: Num(args, "norm-rows", 4096), prefixRows: Num(args, "prefix-rows", 4096),
        suffixRows: Num(args, "suffix-rows", 4096), shapeRows: Num(args, "shape-rows", 1024), seed: seed, prefixLength: 1, suffixLength: 1,
        casedRows: Num(args, "cased-rows", 0), embedWidth: Num(args, "embed-width", 48), plain: false, hashes: 1, turkicCase: true,
        recurrentLayers: Num(args, "recurrent", 1), recurrentSize: Num(args, "recurrent-size", 64),
        neighbourRows: Num(args, "neighbour-rows", 0), bigramRows: Num(args, "bigram-rows", 0), contextRows: Num(args, "context-rows", 0));

    private static int TrainSplitter(string[] args)
    {
        string outDir = Required(args, "out");
        List<Example> train = SplitterExamples(Required(args, "train"), out int lostTrain, out int mwTrain, out int totalTrain);
        List<Example> dev = SplitterExamples(Required(args, "dev"), out int lostDev, out int mwDev, out int totalDev);
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"train: {train.Count} windows of {totalTrain} tokens, {mwTrain} multiword, {lostTrain} not expressible; dev: {dev.Count} of {totalDev}, {mwDev} multiword, {lostDev} not expressible"));

        ulong seed = (ulong)Num(args, "seed", 1);
        var tagger = new Tagger(MakeSplitterEncoder(args, seed), TokenColumn.Tag, NeuralMultiwordSplitter.HeadName) { OwnsEncoder = true };
        using Pipeline pipeline = Pipeline.Blank(Language.Turkish).Add(tagger);
        return RunTraining(pipeline, train, new TrainOptions
        {
            Epochs = Num(args, "epochs", 40),
            BatchWords = Num(args, "batch-words", 2000),
            LearningRate = Flt(args, "lr", 0.002f),
            WarmupSteps = 200,
            Dropout = Flt(args, "dropout", 0.1f),
            Seed = seed,
            DevExamples = dev,
            Score = SplitterDevScore,
            Patience = Num(args, "patience", 6),
            CheckpointDirectory = outDir,
            CheckpointEvery = Num(args, "checkpoint-every", 100),
            Progress = Reporter,
        }, args, outDir, SaveSplitter);
    }

    // The trained tagger becomes the tokenizer of a pipeline with no components, which is what splitter.onenlp holds. The tagger stays owned by
    // the training pipeline, which disposes it.
    private static void SaveSplitter(Pipeline trained, string path)
    {
        var holder = new Pipeline(new NeuralMultiwordSplitter(new UaxWordTokenizer(SegmentationData.TurkishWhole), (Tagger)trained.Components[0]), Language.Turkish);
        PipelineFile.Save(holder, path);
    }
}
