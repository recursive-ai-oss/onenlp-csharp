using System.Diagnostics;
using System.Text.Json;
using RecursiveAI.OneNlp.Tokenizers;
using RecursiveAI.OneNlp.Tokenizers.Bpe;
using RecursiveAI.OneNlp.Tokenizers.Hf;

namespace RecursiveAI.OneNlp.Tests;

/// <summary>
/// Indicative encode throughput of the byte-pair families on the reference texts; only runs when ONENLP_THROUGHPUT is set (it is a
/// measurement, not a test). Prints best-of-5 times through diagnostic messages.
/// </summary>
public class BpeThroughput
{
    private static void Report(string row, int repetitions, long bytes, long tokens, Action work)
    {
        work(); // warm up: JIT and caches
        double best = double.MaxValue;
        long allocated = 0;
        for (int r = 0; r < repetitions; r++)
        {
            long a0 = GC.GetAllocatedBytesForCurrentThread();
            long t0 = Stopwatch.GetTimestamp();
            work();
            double s = Stopwatch.GetElapsedTime(t0).TotalSeconds;
            if (s < best)
            {
                best = s;
                allocated = GC.GetAllocatedBytesForCurrentThread() - a0;
            }
        }

        TestContext.Current.SendDiagnosticMessage($"{row}: {bytes / 1e6 / best:F1} MB/s, {tokens / 1e6 / best:F2} Mtokens/s, {best * 1000:F1} ms per pass, {allocated / 1e6:F1} MB allocated per pass");
    }

    [Theory]
    [InlineData("cl100k_base", TiktokenEncoding.Cl100kBase)]
    [InlineData("o200k_base", TiktokenEncoding.O200kBase)]
    public void Tiktoken(string name, TiktokenEncoding encoding)
    {
        string? goldens = Environment.GetEnvironmentVariable("ONENLP_BPE_GOLDENS");
        string? models = Environment.GetEnvironmentVariable("ONENLP_TOKENIZER_MODELS");
        if (Environment.GetEnvironmentVariable("ONENLP_THROUGHPUT") is null || goldens is null || models is null)
        {
            Assert.Skip("ONENLP_THROUGHPUT, ONENLP_BPE_GOLDENS and ONENLP_TOKENIZER_MODELS are not all set.");
        }

        TiktokenTokenizer t = TiktokenTokenizer.Load(Path.Combine(models, "tiktoken", name + ".tiktoken"), encoding);
        // The texts of the reference file except the synthetic stress strings (very long runs).
        string[] texts = [.. File.ReadLines(Path.Combine(goldens, "tiktoken-" + name + ".jsonl"))
            .Select(l => JsonDocument.Parse(l).RootElement.GetProperty("s").GetString()!)
            .Where(s => s.Length is > 0 and < 2000 && !s.Contains('\ud800', StringComparison.Ordinal))];
        long bytes = texts.Sum(s => (long)System.Text.Encoding.UTF8.GetByteCount(s));
        long tokens = texts.Sum(s => (long)t.CountTokens(s, false));
        Report($"{name} CountTokens ({texts.Length} texts)", 5, bytes, tokens, () =>
        {
            foreach (string s in texts)
            {
                _ = t.CountTokens(s, false);
            }
        });
        var ids = new int[4096];
        Report($"{name} EncodeToIds", 5, bytes, tokens, () =>
        {
            foreach (string s in texts)
            {
                _ = t.EncodeToIds(s, ids, out _, out _, false);
            }
        });
        Report($"{name} Encode (full)", 5, bytes, tokens, () =>
        {
            foreach (string s in texts)
            {
                _ = t.Encode(s, false);
            }
        });
    }

    [Fact]
    public void Gpt2ModelOverPreSplitPieces()
    {
        string? goldens = Environment.GetEnvironmentVariable("ONENLP_BPE_GOLDENS");
        string? models = Environment.GetEnvironmentVariable("ONENLP_TOKENIZER_MODELS");
        if (Environment.GetEnvironmentVariable("ONENLP_THROUGHPUT") is null || goldens is null || models is null)
        {
            Assert.Skip("ONENLP_THROUGHPUT, ONENLP_BPE_GOLDENS and ONENLP_TOKENIZER_MODELS are not all set.");
        }

        using JsonDocument file = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(models, "openai-community_gpt2", "tokenizer.json")));
        BpeModel model = BpeModel.FromConfig(new StageConfig(file.RootElement.GetProperty("model"), StageRegistry.Default));
        string[] pieces = [.. File.ReadLines(Path.Combine(goldens, "bpe-gpt2.jsonl")).Select(l => JsonDocument.Parse(l).RootElement.GetProperty("p").GetString()!).Where(p => p.Length < 200)];
        long chars = pieces.Sum(p => (long)p.Length);
        var output = new List<ModelToken>();
        long tokens = 0;
        foreach (string p in pieces)
        {
            output.Clear();
            model.Tokenize(p, output);
            tokens += output.Count;
        }

        // Pieces are byte-mapped characters, so a character is a byte of the original text. The cache is warm after the first pass.
        Report($"gpt2 model, {pieces.Length} distinct pieces (cache warm)", 5, chars, tokens, () =>
        {
            foreach (string p in pieces)
            {
                output.Clear();
                model.Tokenize(p, output);
            }
        });
    }
}
