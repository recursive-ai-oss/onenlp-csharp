using System.Text.Json;
using RecursiveAI.OneNlp.Tokenizers.Hf;

namespace RecursiveAI.OneNlp.Tests;

/// <summary>
/// Checks the SentencePiece character map normalizer against reference outputs for two real models. The model files are not part of the
/// repository: set ONENLP_TOKENIZER_MODELS to a directory holding one sub-directory per model (named as in the golden file) with its
/// tokenizer.json; without it the test does nothing.
/// </summary>
public class PrecompiledTests
{
    [Fact]
    public void CharsMapNormalizationMatchesTheReference()
    {
        string? root = Environment.GetEnvironmentVariable("ONENLP_TOKENIZER_MODELS");
        if (string.IsNullOrEmpty(root))
        {
            return;
        }

        string gold = Path.Combine(AppContext.BaseDirectory, "testdata", "tokenizers", "stages", "precompiled.json");
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllBytes(gold));
        var failures = new List<string>();
        int checkedCases = 0;
        foreach (JsonElement entry in doc.RootElement.EnumerateArray())
        {
            string model = entry.GetProperty("model").GetString()!;
            string file = Path.Combine(root, model, "tokenizer.json");
            if (!File.Exists(file))
            {
                continue;
            }

            using JsonDocument tok = JsonDocument.Parse(File.ReadAllBytes(file));
            string charsmap = FindCharsMap(tok.RootElement.GetProperty("normalizer"))
                ?? throw new InvalidDataException($"{model} has no Precompiled normalizer.");
            var normalizer = new PrecompiledNormalizer(charsmap);
            foreach (JsonElement c in entry.GetProperty("cases").EnumerateArray())
            {
                string input = c.GetProperty("in").GetString()!;
                string expected = c.GetProperty("out").GetString()!;
                var s = new NormalizedString(input);
                normalizer.Normalize(s);
                checkedCases++;
                if (!string.Equals(s.Text, expected, StringComparison.Ordinal))
                {
                    failures.Add($"{model}: {Hex(input)} -> {Hex(s.Text)}, expected {Hex(expected)}");
                }
            }
        }

        Assert.True(failures.Count == 0, $"{failures.Count} of {checkedCases} differ:\n" + string.Join("\n", failures.Take(10)));
    }

    private static string Hex(string t) => string.Join(" ", t.Select(c => ((int)c).ToString("x4", System.Globalization.CultureInfo.InvariantCulture)));

    private static string? FindCharsMap(JsonElement normalizer)
    {
        if (normalizer.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        string? type = normalizer.GetProperty("type").GetString();
        if (type == "Precompiled")
        {
            return normalizer.GetProperty("precompiled_charsmap").GetString();
        }

        if (type == "Sequence")
        {
            foreach (JsonElement n in normalizer.GetProperty("normalizers").EnumerateArray())
            {
                if (FindCharsMap(n) is { } found)
                {
                    return found;
                }
            }
        }

        return null;
    }
}
