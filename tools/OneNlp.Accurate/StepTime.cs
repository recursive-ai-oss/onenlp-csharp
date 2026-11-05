using System.Diagnostics;
using System.Globalization;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;
using RecursiveAI.OneNlp.Neural.Encoders;

/// <summary>
/// Times the parts of one fine-tuning step on a synthetic batch: forward, backward, gradient clipping and the AdamW update. Information only.
/// </summary>
internal static class StepTime
{
    public static void Run(Dictionary<string, string> o)
    {
        int threads = o.TryGetValue("threads", out string? th) ? int.Parse(th, CultureInfo.InvariantCulture) : 8;
        int rows = o.TryGetValue("rows", out string? r) ? int.Parse(r, CultureInfo.InvariantCulture) : 24;
        int length = o.TryGetValue("length", out string? l) ? int.Parse(l, CultureInfo.InvariantCulture) : 32;
        int steps = o.TryGetValue("steps", out string? s) ? int.Parse(s, CultureInfo.InvariantCulture) : 4;
        TensorCompute.Threads = threads;

        using TrainableTransformerEncoder encoder = TrainableTransformerEncoder.LoadOnnx(o["model"], o["tokenizer"]);
        NnModule module = encoder.Weights!;
        var generator = new Generator(3);
        using var head = new Linear(encoder.Width, 17, generator);
        Tensor[] parameters = [.. module.Parameters(), .. head.Parameters()];
        using var optimizer = new AdamW(parameters, 5e-5f);
        var random = new Random(1);
        long[] x = new long[rows * length];
        for (int i = 0; i < x.Length; i++)
        {
            x[i] = random.Next(10, 5000);
        }

        long[] labels = [.. Enumerable.Range(0, rows * length).Select(i => (long)(i % 17))];
        int[] lengths = [.. Enumerable.Repeat(length, rows)];
        Console.WriteLine($"threads {threads}, batch {rows} x {length} = {rows * length} pieces, parameters {parameters.Sum(p => (long)p.Data<float>().Length):N0}");
        for (int step = 0; step < steps; step++)
        {
            optimizer.ZeroGrad();
            long t0 = Stopwatch.GetTimestamp();
            double forward, backward, clip, update;
            using (var scope = new ActivationScope())
            {
                using Tensor ids = Tensor.FromSpan<long>(x, Shape.Create(rows, length));
                Tensor h = module switch { ModernBertModule m => m.Forward(ids, lengths), PostNormModule m => m.Forward(ids, lengths), _ => throw new InvalidOperationException() };
                Tensor logits = head.Forward(h.Reshape(rows * length, encoder.Width));
                Tensor loss = Losses.CrossEntropy(logits, Tensor.FromSpan<long>(labels, Shape.Create(labels.Length)));
                forward = Stopwatch.GetElapsedTime(t0).TotalSeconds;
                long t1 = Stopwatch.GetTimestamp();
                loss.Backward();
                backward = Stopwatch.GetElapsedTime(t1).TotalSeconds;
            }

            long t2 = Stopwatch.GetTimestamp();
            GradClip.ClipNorm(parameters, 1f);
            clip = Stopwatch.GetElapsedTime(t2).TotalSeconds;
            long t3 = Stopwatch.GetTimestamp();
            optimizer.Step();
            update = Stopwatch.GetElapsedTime(t3).TotalSeconds;
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"step {step}: forward {forward:F2} s, backward {backward:F2} s, clip {clip:F2} s, adamw {update:F2} s, total {forward + backward + clip + update:F2} s"));
        }

        using (var scope = new ActivationScope())
        using (GradMode.NoGrad())
        {
            module.Eval();
            using Tensor ids = Tensor.FromSpan<long>(x, Shape.Create(rows, length));
            for (int rep = 0; rep < 2; rep++)
            {
                long t0 = Stopwatch.GetTimestamp();
                Tensor h = module switch { ModernBertModule m => m.Forward(ids, lengths), PostNormModule m => m.Forward(ids, lengths), _ => throw new InvalidOperationException() };
                _ = h.Data<float>()[0];
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"forward without gradients: {Stopwatch.GetElapsedTime(t0).TotalSeconds:F2} s"));
            }
        }
    }
}
