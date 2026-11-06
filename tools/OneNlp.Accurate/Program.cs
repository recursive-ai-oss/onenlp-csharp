using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using RecursiveAI.OneModel.Graph;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneNlp.Tokenizers;

// Commands (arguments are --name value pairs):
//   ids      --tokenizer tokenizer.json --texts texts.jsonl --out ids.json [--max-length 512]
//   parity   --model m.onnx --ids ids.json --ref ref.bin
//   finetune-probe --model m.onnx --ids ids.json [--text 3] [--steps 5]   (BERT/XLM-R style graphs)
//   trainable-parity --model m.onnx --tokenizer tokenizer.json --ids ids.json --ref ref.bin [--texts N] [--batch 8] [--family auto|modernbert|postnorm] [--position-offset 2]
//   step-time --model m.onnx --tokenizer tokenizer.json [--rows 24] [--length 32] [--steps 4] [--threads 8]
//   latency  --model m.onnx --length 64 --batches 1,8 --iters 20 [--threads 8]
Dictionary<string, string> opt = [];
for (int i = 1; i + 1 < args.Length; i += 2)
{
    opt[args[i].TrimStart('-')] = args[i + 1];
}

switch (args.Length > 0 ? args[0] : "")
{
    case "ids": Ids(opt); break;
    case "parity": Parity(opt); break;
    case "latency": Latency(opt); break;
    case "step-time": StepTime.Run(opt); break;
    case "trainable-parity": TrainableParity.Run(opt); break;
    case "finetune-probe": FineTuneProbe.Run(opt); break;
    default: Console.Error.WriteLine("commands: ids, parity, latency"); return 2;
}

return 0;

static void Ids(Dictionary<string, string> o)
{
    Tokenizer tok = Tokenizer.FromFile(o["tokenizer"]);
    int max = o.TryGetValue("max-length", out string? m) ? int.Parse(m, CultureInfo.InvariantCulture) : 512;
    var all = new List<int[]>();
    foreach (string line in File.ReadLines(o["texts"]))
    {
        using JsonDocument d = JsonDocument.Parse(line);
        int[] ids = tok.Encode(d.RootElement.GetProperty("text").GetString()!, addSpecialTokens: true).Ids;
        all.Add(ids.Length > max ? ids[..max] : ids);
    }

    File.WriteAllText(o["out"], JsonSerializer.Serialize(all, IdsContext.Default.ListInt32Array));
    Console.WriteLine($"{all.Count} texts, {all.Sum(a => a.Length)} tokens");
}

static Tensor Ids64(long[] v, int rows, int len) => Tensor.FromSpan<long>(v, Shape.Create(rows, len));

static void Parity(Dictionary<string, string> o)
{
    List<int[]> ids = JsonSerializer.Deserialize(File.ReadAllText(o["ids"]), IdsContext.Default.ListInt32Array)!;
    byte[] raw = File.ReadAllBytes(o["ref"]);
    float[] reference = new float[raw.Length / 4];
    Buffer.BlockCopy(raw, 0, reference, 0, raw.Length);
    using Session session = Session.Load(o["model"]);
    int width = (int)session.Outputs[0].Dims[2].Value;
    Console.WriteLine($"kernel path {session.KernelPath}; width {width}; rows {reference.Length / width}");
    int row = 0;
    double maxRef = 0, maxAbs = 0, sumAbs = 0, minCos = 1, sumCos = 0;
    long count = 0, tokens = 0;
    int worstText = -1;
    double worstText2 = 0;
    for (int t = 0; t < ids.Count; t++)
    {
        int n = ids[t].Length;
        long[] x = [.. ids[t].Select(v => (long)v)];
        long[] mask = [.. Enumerable.Repeat(1L, n)];
        using Tensor a = Ids64(x, 1, n);
        using Tensor b = Ids64(mask, 1, n);
        using SessionOutputs outs = session.Run(new Dictionary<string, Tensor> { ["input_ids"] = a, ["attention_mask"] = b });
        float[] h = outs[session.Outputs[0].Name].Data<float>().ToArray();

        double textMax = 0;
        for (int r = 0; r < n; r++, row++)
        {
            double dot = 0, na = 0, nb = 0;
            for (int j = 0; j < width; j++)
            {
                float u = h[(r * width) + j], v = reference[(row * width) + j];
                double diff = Math.Abs(u - v);
                maxAbs = Math.Max(maxAbs, diff);
                maxRef = Math.Max(maxRef, Math.Abs(v));
                textMax = Math.Max(textMax, diff);
                sumAbs += diff;
                dot += (double)u * v;
                na += (double)u * u;
                nb += (double)v * v;
            }

            double cos = dot / Math.Sqrt(na * nb);
            minCos = Math.Min(minCos, cos);
            sumCos += cos;
            count += width;
            tokens++;
        }

        if (textMax > worstText2)
        {
            worstText2 = textMax;
            worstText = t;
        }
    }

    Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
        $"texts {ids.Count} tokens {tokens} maxAbs {maxAbs:E3} (largest |reference| {maxRef:F1}) meanAbs {sumAbs / count:E3} minCos {minCos:F8} meanCos {sumCos / tokens:F8} worstText {worstText}"));
}

static void Latency(Dictionary<string, string> o)
{
    int len = int.Parse(o["length"], CultureInfo.InvariantCulture);
    int iters = int.Parse(o["iters"], CultureInfo.InvariantCulture);
    var options = new ModelOptions { IntraOpThreads = o.TryGetValue("threads", out string? th) ? int.Parse(th, CultureInfo.InvariantCulture) : 0 };
    using Session session = Session.Load(o["model"], options);
    foreach (int batch in o["batches"].Split(',').Select(s => int.Parse(s, CultureInfo.InvariantCulture)))
    {
        long[] x = new long[batch * len];
        var rng = new Random(1);
        for (int i = 0; i < x.Length; i++)
        {
            x[i] = rng.Next(5, 1000);
        }

        long[] mask = [.. Enumerable.Repeat(1L, x.Length)];
        using Tensor a = Ids64(x, batch, len);
        using Tensor b = Ids64(mask, batch, len);
        var inputs = new Dictionary<string, Tensor> { ["input_ids"] = a, ["attention_mask"] = b };
        for (int w = 0; w < 3; w++)
        {
            using SessionOutputs warm = session.Run(inputs);
        }

        var times = new double[iters];
        for (int i = 0; i < iters; i++)
        {
            long t0 = Stopwatch.GetTimestamp();
            using SessionOutputs r = session.Run(inputs);
            times[i] = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
        }

        Array.Sort(times);
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"onemodel batch {batch} len {len} median {times[iters / 2]:F1} ms min {times[0]:F1} ms"));
    }
}

[System.Text.Json.Serialization.JsonSerializable(typeof(List<int[]>))]
internal sealed partial class IdsContext : System.Text.Json.Serialization.JsonSerializerContext;
