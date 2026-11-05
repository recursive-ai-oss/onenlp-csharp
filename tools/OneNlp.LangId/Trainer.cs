using System.Numerics;
using System.Text;
using RecursiveAI.OneNlp.LanguageId;

namespace RecursiveAI.OneNlp.LangIdTool;

/// <summary>Settings of one training run; every field is recorded in the run's log.</summary>
internal sealed class TrainOptions
{
    public string TrainDir = "";
    public string Languages = "";
    public string Out = "";
    public int Epochs = 4;
    public int SamplesPerLanguage = 60000;
    public int DevPerLanguage = 400;
    public double LearningRate = 0.4;
    public float Exponent = 0.5f;
    public int Seed = 1;
    public double ClipPercentile = 0.9995;
    public int DefaultBits = 14;
    public Dictionary<string, int> Bits = new(StringComparer.OrdinalIgnoreCase) { ["Latin"] = 16 };

    /// <summary>A script's share of a language's letters below which the language is not a member of the script's group.</summary>
    public double MinScriptShare = 0.03;
}

/// <summary>
/// Trains the linear softmax models, one per script group, with sparse AdaGrad (Duchi, Hazan, Singer 2011). A training sample is
/// a random view of a training sentence: one word, two words, a short phrase or the whole sentence, so that the model sees the
/// lengths it is judged on.
/// </summary>
internal static class Trainer
{
    private sealed class Group
    {
        public Script Script;
        public int Bits;
        public List<int> Languages = [];
    }

    public static void Train(TrainOptions o)
    {
        string[] codes = [.. File.ReadLines(o.Languages).Where(l => l.Length > 0 && l[0] != '#').Select(l => l.Split('\t')[0])];
        var data = new List<string>[codes.Length];
        var dev = new List<string>[codes.Length];
        for (int i = 0; i < codes.Length; i++)
        {
            string path = Path.Combine(o.TrainDir, codes[i] + ".txt");
            var all = File.ReadAllLines(path, Encoding.UTF8).Where(l => l.Length > 0).ToList();
            int d = Math.Min(o.DevPerLanguage, all.Count / 10);
            dev[i] = all[^d..];
            data[i] = all[..^d];
            Console.WriteLine($"{codes[i]}: {data[i].Count} train, {dev[i].Count} dev");
        }

        // Which scripts does each language use? A language joins the group of every script that has at least MinScriptShare of its letters.
        var groups = new Dictionary<Script, Group>();
        var scratch = new LangIdText();
        for (int i = 0; i < codes.Length; i++)
        {
            var letters = new long[Scripts.Count];
            foreach (string s in data[i].Take(5000))
            {
                scratch.Run(s);
                for (int w = 0; w < scratch.WordCount; w++)
                {
                    letters[(int)scratch.WordScript[w]] += scratch.CharEnd[w] - scratch.CharStart[w];
                }
            }

            long total = letters.Sum();
            for (int s = (int)Script.Latin; s < (int)Script.Other; s++)
            {
                if (total > 0 && letters[s] >= o.MinScriptShare * total)
                {
                    if (!groups.TryGetValue((Script)s, out Group? g))
                    {
                        groups[(Script)s] = g = new Group { Script = (Script)s, Bits = o.Bits.GetValueOrDefault(((Script)s).ToString(), o.DefaultBits) };
                    }

                    g.Languages.Add(i);
                }
            }

            Console.WriteLine($"{codes[i]}: " + string.Join(", ", Enumerable.Range((int)Script.Latin, Scripts.Count - (int)Script.Latin).Where(s => total > 0 && letters[s] > 0).Select(s => $"{(Script)s} {100.0 * letters[s] / total:F1}%")));
        }

        Group[] ordered = [.. groups.Values.OrderBy(g => g.Script)];
        var built = new LanguageModel.Group[ordered.Length];
        var floatWeights = new float[ordered.Length][];
        Parallel.For(0, ordered.Length, new ParallelOptions { MaxDegreeOfParallelism = 4 }, gi =>
        {
            built[gi] = TrainGroup(o, ordered[gi], data, dev, codes, out floatWeights[gi]);
        });

        // Temperature: the multiplier on scores that minimizes the log loss of the quantized model on the held-out sentences.
        LanguageModel model = LanguageModel.Create(codes, built, o.Exponent, 1f);
        float temperature = Calibrate(model, dev, codes);
        model = LanguageModel.Create(codes, built, o.Exponent, temperature);
        using (var fs = File.Create(o.Out))
        {
            model.Write(fs);
        }

        Console.WriteLine($"wrote {o.Out}: {new FileInfo(o.Out).Length} bytes, temperature {temperature:F3}");
    }

    private static LanguageModel.Group TrainGroup(TrainOptions o, Group g, List<string>[] data, List<string>[] dev, string[] codes, out float[] weights)
    {
        int nc = g.Languages.Count;
        uint mask = 1u << (int)g.Script;
        if (nc == 1)
        {
            weights = [];
            Console.WriteLine($"[{g.Script}] single language {codes[g.Languages[0]]}: no weights");
            return new LanguageModel.Group { ScriptMask = mask, Bits = 0, Classes = [.. g.Languages], Scale = 1f, Bias = [0f] };
        }

        int V = Vector<float>.Count;
        int stride = (nc + V - 1) / V * V;
        int rows = 1 << g.Bits;
        int shift = 32 - g.Bits;
        float[] w = new float[(long)rows * stride];
        float[] gsum = new float[rows];
        float[] bias = new float[nc];
        var rnd = new Random(o.Seed + (int)g.Script);
        var text = new LangIdText();
        int[] wordsOfScript = new int[4096];
        int[] feat = new int[1 << 16];
        float[] z = new float[stride];
        float[] grad = new float[stride];
        for (int epoch = 0; epoch < o.Epochs; epoch++)
        {
            var order = new List<(int cls, int idx)>();
            for (int c = 0; c < nc; c++)
            {
                List<string> d = data[g.Languages[c]];
                int take = Math.Min(o.SamplesPerLanguage, d.Count);
                int offset = (int)((long)epoch * take % d.Count);
                for (int k = 0; k < take; k++)
                {
                    order.Add((c, (offset + k) % d.Count));
                }
            }

            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            double loss = 0;
            long used = 0;
            foreach ((int cls, int idx) in order)
            {
                int n = Sample(text, data[g.Languages[cls]][idx], g.Script, rnd, ref wordsOfScript, ref feat, shift);
                if (n == 0)
                {
                    continue;
                }

                float scale = MathF.Pow(n, -o.Exponent);
                Array.Clear(z);
                for (int f = 0; f < n; f++)
                {
                    Add(z, w.AsSpan(feat[f] * stride, stride));
                }

                float max = float.NegativeInfinity;
                for (int c = 0; c < nc; c++)
                {
                    z[c] = bias[c] + (z[c] * scale);
                    max = MathF.Max(max, z[c]);
                }

                float sum = 0;
                for (int c = 0; c < nc; c++)
                {
                    z[c] = MathF.Exp(z[c] - max);
                    sum += z[c];
                }

                float g2 = 0;
                for (int c = 0; c < nc; c++)
                {
                    float p = z[c] / sum;
                    grad[c] = p - (c == cls ? 1f : 0f);
                    g2 += grad[c] * grad[c];
                    if (c == cls)
                    {
                        loss -= Math.Log(Math.Max(p, 1e-9));
                    }
                }

                used++;
                float gs = scale * scale * g2 / nc;
                for (int f = 0; f < n; f++)
                {
                    int r = feat[f];
                    float lr = (float)(o.LearningRate / Math.Sqrt(1.0 + gsum[r])) * scale;
                    gsum[r] += gs;
                    var row = w.AsSpan(r * stride, stride);
                    for (int c = 0; c < stride; c += V)
                    {
                        (new Vector<float>(row[c..]) - (new Vector<float>(grad.AsSpan(c)) * lr)).CopyTo(row[c..]);
                    }
                }

                for (int c = 0; c < nc; c++)
                {
                    bias[c] -= 0.02f * grad[c];
                }
            }

            double acc = DevAccuracy(o, g, dev, w, bias, stride, shift, nc);
            Console.WriteLine($"[{g.Script}] epoch {epoch + 1}: train loss {loss / Math.Max(1, used):F4}, dev accuracy {acc:P2}");
        }

        // Quantize to int8 with a clip so a few large weights do not use up the resolution.
        var mags = new List<float>(rows * nc);
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < nc; c++)
            {
                float a = MathF.Abs(w[(r * stride) + c]);
                if (a > 0)
                {
                    mags.Add(a);
                }
            }
        }

        mags.Sort();
        float clip = mags.Count == 0 ? 1f : mags[(int)Math.Min(mags.Count - 1, mags.Count * o.ClipPercentile)];
        float qs = clip / 127f;
        var q = new sbyte[(long)rows * nc];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < nc; c++)
            {
                q[((long)r * nc) + c] = (sbyte)Math.Clamp((int)MathF.Round(w[(r * stride) + c] / qs), -127, 127);
            }
        }

        weights = w;
        return new LanguageModel.Group { ScriptMask = mask, Bits = g.Bits, Classes = [.. g.Languages], Scale = qs, Bias = bias, Weights = q };
    }

    private static void Add(float[] z, Span<float> row)
    {
        int V = Vector<float>.Count;
        for (int c = 0; c < row.Length; c += V)
        {
            (new Vector<float>(z.AsSpan(c)) + new Vector<float>(row[c..])).CopyTo(z.AsSpan(c));
        }
    }

    /// <summary>Picks a random view of the sentence (1 word, 2 words, 3 to 5, or all) among the words of the group's script and returns the bucket ids.</summary>
    private static int Sample(LangIdText t, string sentence, Script script, Random rnd, ref int[] wordsOfScript, ref int[] feat, int shift)
    {
        t.Run(sentence);
        int count = 0;
        if (wordsOfScript.Length < t.WordCount)
        {
            wordsOfScript = new int[t.WordCount * 2];
        }

        for (int w = 0; w < t.WordCount; w++)
        {
            if (t.WordScript[w] == script)
            {
                wordsOfScript[count++] = w;
            }
        }

        if (count == 0)
        {
            return 0;
        }

        double r = rnd.NextDouble();
        int k = r < 0.22 ? 1 : r < 0.5 ? 2 : r < 0.65 ? rnd.Next(3, 6) : count;
        k = Math.Min(k, count);
        int start = rnd.Next(count - k + 1);
        int n = 0;
        for (int j = start; j < start + k; j++)
        {
            int w = wordsOfScript[j];
            int len = t.HashStart[w + 1] - t.HashStart[w];
            if (n + len > feat.Length)
            {
                Array.Resize(ref feat, Math.Max(feat.Length * 2, n + len));
            }

            for (int h = t.HashStart[w]; h < t.HashStart[w + 1]; h++)
            {
                feat[n++] = (int)((uint)t.Hashes[h] >> shift);
            }
        }

        return n;
    }

    private static double DevAccuracy(TrainOptions o, Group g, List<string>[] dev, float[] w, float[] bias, int stride, int shift, int nc)
    {
        var rnd = new Random(12345);
        var t = new LangIdText();
        int[] words = new int[4096];
        int[] feat = new int[1 << 16];
        var z = new float[stride];
        long ok = 0, tot = 0;
        for (int c = 0; c < nc; c++)
        {
            foreach (string s in dev[g.Languages[c]])
            {
                int n = Sample(t, s, g.Script, rnd, ref words, ref feat, shift);
                if (n == 0)
                {
                    continue;
                }

                Array.Clear(z);
                for (int f = 0; f < n; f++)
                {
                    Add(z, w.AsSpan(feat[f] * stride, stride));
                }

                float scale = MathF.Pow(n, -o.Exponent);
                int best = 0;
                for (int k = 0; k < nc; k++)
                {
                    z[k] = bias[k] + (z[k] * scale);
                    if (z[k] > z[best])
                    {
                        best = k;
                    }
                }

                tot++;
                if (best == c)
                {
                    ok++;
                }
            }
        }

        return tot == 0 ? 0 : (double)ok / tot;
    }

    private static float Calibrate(LanguageModel model, List<string>[] dev, string[] codes)
    {
        var t = new LangIdText();
        var rnd = new Random(777);
        var samples = new List<(float[] z, int cls)>();
        for (int l = 0; l < codes.Length; l++)
        {
            foreach (string s in dev[l])
            {
                // A random view: first k words, as in training.
                t.Run(s);
                if (t.WordCount == 0)
                {
                    continue;
                }

                double r = rnd.NextDouble();
                int k = r < 0.22 ? 1 : r < 0.5 ? 2 : r < 0.65 ? rnd.Next(3, 6) : t.WordCount;
                k = Math.Min(k, t.WordCount);
                int from = rnd.Next(t.WordCount - k + 1);
                int gi = -1;
                for (int w = from; w < from + k && gi < 0; w++)
                {
                    gi = model.GroupOf(t.WordScript[w]);
                }

                if (gi < 0)
                {
                    continue;
                }

                LanguageModel.Group g = model.Groups[gi];
                int cls = Array.IndexOf(g.Classes, l);
                if (cls < 0 || g.Classes.Length == 1)
                {
                    continue;
                }

                var z = new float[g.Classes.Length];
                _ = model.Score(t, from, from + k, gi, z);
                samples.Add((z, cls));
            }
        }

        float bestT = 1;
        double bestLoss = double.MaxValue;
        for (float temp = 0.2f; temp <= 6f; temp *= 1.06f)
        {
            double loss = 0;
            foreach ((float[] z, int cls) in samples)
            {
                float max = z.Max();
                double sum = 0;
                foreach (float v in z)
                {
                    sum += Math.Exp((v - max) * temp);
                }

                loss -= ((z[cls] - max) * temp) - Math.Log(sum);
            }

            if (loss < bestLoss)
            {
                bestLoss = loss;
                bestT = temp;
            }
        }

        Console.WriteLine($"calibration: {samples.Count} dev samples, log loss {bestLoss / samples.Count:F4} at temperature {bestT:F3}");
        return bestT;
    }
}
