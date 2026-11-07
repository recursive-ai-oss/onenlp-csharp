using RecursiveAI.OneNlp;

namespace RecursiveAI.OneNlp.Samples;

// Runs the samples shown in docs/reads-like.md and the README. Each prints what it finds and checks a few facts, so the exit code says
// whether the documented calls still do what the documentation says.
internal static class Program
{
    internal static int Failures;

    private static int Main(string[] args)
    {
        string which = args.Length > 0 ? args[0] : "all";
        string? tokenizerFile = args.Length > 1 ? args[1] : Environment.GetEnvironmentVariable("ONENLP_SAMPLE_TOKENIZER");

        if (which is "happy" or "all")
        {
            HappyPath.Run();
        }

        if (which is "pipeline" or "all")
        {
            PipelineSample.Run();
        }

        if (which is "tokenizer" or "all")
        {
            if (tokenizerFile is null)
            {
                Console.WriteLine("tokenizer: skipped (pass a tokenizer.json path, or set ONENLP_SAMPLE_TOKENIZER)");
            }
            else
            {
                TokenizerSample.Run(tokenizerFile);
            }
        }

        if (which is "migration" or "all")
        {
            MigrationSamples.Run(tokenizerFile);
        }

        if (which is "train" or "all")
        {
            TrainSample.Run();
        }

        if (which is "embed" or "all")
        {
            EmbedSample.Run();
        }

        Console.WriteLine(Failures == 0 ? "samples OK" : $"{Failures} check(s) failed");
        return Failures == 0 ? 0 : 1;
    }

    internal static void Check(string what, bool ok)
    {
        if (!ok)
        {
            Failures++;
        }

        Console.WriteLine($"  {(ok ? "ok  " : "FAIL")} {what}");
    }
}
