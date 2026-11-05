using System.Text.Json;
using RecursiveAI.OneNlp.Tokenizers;

namespace RecursiveAI.OneNlp.Tests;

/// <summary>
/// Compares real tokenizer.json files with reference encodings written by tools/stage_goldens/make_real_goldens.py. The files are not
/// in the repository: set ONENLP_TOKENIZER_MODELS to the directory of model sub-directories and ONENLP_TOKENIZER_JSON_GOLDENS to the reference
/// JSON; without both the test does nothing.
/// </summary>
public class RealTokenizerGoldenTests
{
    private static string Describe(int[] ids, string[] tokens, TokenOffset[] offsets, int[] types, int[] words, bool[] special, int[] attention) =>
        $"ids={string.Join(",", ids)} tokens={string.Join("|", tokens)} off={string.Join(",", offsets.Select(o => $"{o.Start}-{o.End}"))} " +
        $"type={string.Join("", types)} word={string.Join(",", words)} special={string.Join("", special.Select(s => s ? 1 : 0))} att={string.Join("", attention)}";

    [Fact]
    public void RealFilesMatchTheReference()
    {
        string? root = Environment.GetEnvironmentVariable("ONENLP_TOKENIZER_MODELS");
        string? goldPath = Environment.GetEnvironmentVariable("ONENLP_TOKENIZER_JSON_GOLDENS");
        if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(goldPath))
        {
            return;
        }

        using JsonDocument gold = JsonDocument.Parse(File.ReadAllBytes(goldPath));
        var failures = new List<string>();
        int compared = 0;
        int tokensCompared = 0;
        foreach (JsonProperty model in gold.RootElement.GetProperty("models").EnumerateObject())
        {
            Tokenizer t = Tokenizer.FromFile(Path.Combine(root, model.Name, "tokenizer.json"));
            int bad = 0;
            foreach (JsonElement c in model.Value.GetProperty("cases").EnumerateArray())
            {
                string text = c.GetProperty("text").GetString()!;
                string? pair = c.GetProperty("pair").GetString();
                bool add = c.GetProperty("add_special").GetBoolean();
                TokenEncoding e = pair is null ? t.Encode(text, add) : t.Encode(text, pair, add);
                string actual = Describe(
                    e.Ids, e.Tokens, e.Offsets, e.TypeIds, e.WordIds, e.SpecialTokensMask, e.AttentionMask);
                string expected = Describe(
                    [.. c.GetProperty("ids").EnumerateArray().Select(x => x.GetInt32())],
                    [.. c.GetProperty("tokens").EnumerateArray().Select(x => x.GetString()!)],
                    [.. c.GetProperty("offsets").EnumerateArray().Select(x => new TokenOffset(x[0].GetInt32(), x[1].GetInt32()))],
                    [.. c.GetProperty("type_ids").EnumerateArray().Select(x => x.GetInt32())],
                    [.. c.GetProperty("word_ids").EnumerateArray().Select(x => x.GetInt32())],
                    [.. c.GetProperty("special").EnumerateArray().Select(x => x.GetInt32() == 1)],
                    [.. c.GetProperty("attention").EnumerateArray().Select(x => x.GetInt32())]);
                compared++;
                tokensCompared += e.Ids.Length;
                if (actual != expected && bad++ < 2)
                {
                    failures.Add($"{model.Name} '{Escape(text)}'{(pair is null ? "" : $"|'{Escape(pair)}'")} add={add}\n   got      {Cut(actual)}\n   expected {Cut(expected)}");
                }
                else if (actual != expected)
                {
                    failures.Add(string.Empty);
                }
            }

            foreach (JsonElement d in model.Value.GetProperty("decode").EnumerateArray())
            {
                int[] ids = [.. d.GetProperty("ids").EnumerateArray().Select(x => x.GetInt32())];
                bool skip = d.GetProperty("skip").GetBoolean();
                string expected = d.GetProperty("out").GetString()!;
                string actual = t.Decode(ids, skip);
                compared++;
                if (!string.Equals(actual, expected, StringComparison.Ordinal))
                {
                    failures.Add($"{model.Name} decode skip={skip} ids={string.Join(",", ids)}\n   got      '{Escape(actual)}'\n   expected '{Escape(expected)}'");
                }
            }
        }

        Console.WriteLine($"real tokenizer goldens: {gold.RootElement.GetProperty("oracle").GetString()}, {compared} comparisons, {tokensCompared} tokens");
        Assert.True(failures.Count == 0, $"{failures.Count(f => f.Length > 0 || true)} mismatches of {compared}:\n" + string.Join("\n", failures.Where(f => f.Length > 0).Take(12)));
    }

    private static string Cut(string s) => s.Length > 600 ? s[..600] + "..." : s;

    private static string Escape(string s) => Cut(string.Concat(s.Select(c => c < 0x20 || c > 0x7e ? $"\\u{(int)c:x4}" : c.ToString())));
}
