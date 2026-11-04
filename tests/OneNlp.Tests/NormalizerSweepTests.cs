using System.IO.Compression;
using System.Text.Json;
using RecursiveAI.OneNlp.Tokenizers.Hf;

namespace RecursiveAI.OneNlp.Tests;

/// <summary>
/// Compares the normalizers with reference outputs for every Unicode scalar value, written by tools/stage_goldens/make_sweep.py.
/// Set ONENLP_NORMALIZER_SWEEP to that file and ONENLP_TOKENIZER_MODELS to the model directories; without both the test does nothing.
/// </summary>
public class NormalizerSweepTests
{
    private static StageRegistry Registry => StageRegistry.Default;

    private static INormalizer Make(string json)
    {
        using JsonDocument d = JsonDocument.Parse(json);
        return Registry.CreateNormalizer(new StageConfig(d.RootElement.Clone(), Registry));
    }

    private static INormalizer CharsMap(string root, string model)
    {
        using JsonDocument d = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, model, "tokenizer.json")));
        JsonElement n = d.RootElement.GetProperty("normalizer");
        JsonElement first = n.GetProperty("type").GetString() == "Sequence" ? n.GetProperty("normalizers")[0] : n;
        return Registry.CreateNormalizer(new StageConfig(first.Clone(), Registry));
    }

    [Fact]
    public void EveryScalarValueMatchesTheReference()
    {
        string? sweep = Environment.GetEnvironmentVariable("ONENLP_NORMALIZER_SWEEP");
        string? root = Environment.GetEnvironmentVariable("ONENLP_TOKENIZER_MODELS");
        if (string.IsNullOrEmpty(sweep) || string.IsNullOrEmpty(root))
        {
            return;
        }

        using var gz = new GZipStream(File.OpenRead(sweep), CompressionMode.Decompress);
        using JsonDocument doc = JsonDocument.Parse(gz);
        var subjects = new Dictionary<string, INormalizer>
        {
            ["bert_clean"] = Make("""{"type":"BertNormalizer","clean_text":true,"handle_chinese_chars":false,"strip_accents":false,"lowercase":false}"""),
            ["bert_uncased"] = Make("""{"type":"BertNormalizer","clean_text":true,"handle_chinese_chars":true,"strip_accents":null,"lowercase":true}"""),
            ["nfc"] = Make("""{"type":"NFC"}"""),
            ["nfd"] = Make("""{"type":"NFD"}"""),
            ["nfkc"] = Make("""{"type":"NFKC"}"""),
            ["nfkd"] = Make("""{"type":"NFKD"}"""),
            ["lowercase"] = Make("""{"type":"Lowercase"}"""),
            ["strip_accents"] = Make("""{"type":"StripAccents"}"""),
            ["charsmap_xlmr"] = CharsMap(root, "FacebookAI_xlm-roberta-base"),
            ["charsmap_t5"] = CharsMap(root, "google-t5_t5-small"),
        };

        var report = new List<string>();
        foreach ((string name, INormalizer n) in subjects)
        {
            Dictionary<int, string> changed = [];
            foreach (JsonProperty p in doc.RootElement.GetProperty("changed").GetProperty(name).EnumerateObject())
            {
                changed[int.Parse(p.Name, System.Globalization.CultureInfo.InvariantCulture)] = p.Value.GetString()!;
            }

            var bad = new List<string>();
            int total = 0;
            for (int cp = 0; cp < 0x110000; cp++)
            {
                if (cp is >= 0xD800 and <= 0xDFFF)
                {
                    continue;
                }

                string s = char.ConvertFromUtf32(cp);
                var ns = new NormalizedString(s);
                n.Normalize(ns);
                string expected = changed.TryGetValue(cp, out string? e) ? e : s;
                if (!string.Equals(ns.Text, expected, StringComparison.Ordinal))
                {
                    total++;
                    if (bad.Count < 6)
                    {
                        bad.Add($"U+{cp:X4} -> '{Hex(ns.Text)}', expected '{Hex(expected)}'");
                    }
                }
            }

            if (total > 0)
            {
                report.Add($"{name}: {total} differ, e.g. {string.Join("; ", bad)}");
            }
        }

        Assert.True(report.Count == 0, string.Join("\n", report));
    }

    private static string Hex(string t) => string.Join(" ", t.EnumerateRunes().Select(r => r.Value.ToString("X4", System.Globalization.CultureInfo.InvariantCulture)));
}
