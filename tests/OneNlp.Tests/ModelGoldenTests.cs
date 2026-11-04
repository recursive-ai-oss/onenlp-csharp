using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using RecursiveAI.OneNlp.Tokenizers.Hf;

namespace RecursiveAI.OneNlp.Tests;

/// <summary>
/// Model-level goldens: random vocabularies and texts run through the reference package's own model objects. The committed files are
/// small; set ONENLP_MODEL_GOLDENS to a directory made by tools/modelgolden for a large run.
/// </summary>
public class ModelGoldenTests
{
    private static string FixtureDirectory([CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "testdata", "tokenizers", "models"));

    private static IEnumerable<string> Directories()
    {
        yield return FixtureDirectory();
        if (Environment.GetEnvironmentVariable("ONENLP_MODEL_GOLDENS") is { Length: > 0 } extra)
        {
            yield return extra;
        }
    }

    [Theory]
    [InlineData("unigram")]
    [InlineData("wordpiece")]
    [InlineData("wordlevel")]
    public void ModelsMatchTheReference(string family)
    {
        int compared = 0;
        var mismatches = new List<string>();
        foreach (string dir in Directories())
        {
            string path = Path.Combine(dir, family + ".json");
            Assert.True(File.Exists(path), path);
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllBytes(path));
            foreach (JsonElement c in doc.RootElement.GetProperty("cases").EnumerateArray())
            {
                ITokenizerModel model = StageRegistry.Default.CreateModel(new StageConfig(c.GetProperty("model"), StageRegistry.Default));
                foreach (JsonElement t in c.GetProperty("texts").EnumerateArray())
                {
                    string text = t.GetProperty("text").GetString()!;
                    string expected = t.TryGetProperty("error", out _) ? "error" : Format(t.GetProperty("tokens").EnumerateArray()
                        .Select(x => (x[0].GetInt32(), x[1].GetString()!, x[2].GetInt32(), x[3].GetInt32())));
                    string actual;
                    try
                    {
                        var output = new List<ModelToken>();
                        model.Tokenize(text, output);
                        actual = Format(output.Select(m => (m.Id, m.Value, m.Start, m.End)));
                    }
                    catch (InvalidDataException)
                    {
                        actual = "error";
                    }

                    compared++;
                    if (actual != expected && mismatches.Count < 8)
                    {
                        mismatches.Add($"{family} model={c.GetProperty("model").GetRawText()[..Math.Min(300, c.GetProperty("model").GetRawText().Length)]}\n  text={JsonSerializer.Serialize(text)}\n  want {expected}\n  got  {actual}");
                    }
                    else if (actual != expected)
                    {
                        mismatches.Add("...");
                    }
                }
            }
        }

        Assert.True(compared > 0);
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} of {compared} differ:\n" + string.Join("\n", mismatches.Take(8)));
    }

    /// <summary>Whole words cut with real vocabularies; runs only when ONENLP_REAL_GOLDENS names a directory made by tools/modelgolden.</summary>
    [Fact]
    public void RealVocabulariesMatchTheReference()
    {
        if (Environment.GetEnvironmentVariable("ONENLP_REAL_GOLDENS") is not { Length: > 0 } dir)
        {
            return;
        }

        var report = new List<string>();
        foreach (string file in Directory.GetFiles(dir, "real-*.jsonl").Order())
        {
            int compared = 0, wrong = 0;
            ITokenizerModel? model = null;
            foreach (string line in File.ReadLines(file))
            {
                using JsonDocument doc = JsonDocument.Parse(line);
                JsonElement root = doc.RootElement;
                if (model is null)
                {
                    using JsonDocument tokenizerJson = JsonDocument.Parse(File.ReadAllBytes(root.GetProperty("tokenizer_json").GetString()!));
                    model = StageRegistry.Default.CreateModel(new StageConfig(tokenizerJson.RootElement.GetProperty("model"), StageRegistry.Default));
                    continue;
                }

                string text = root.GetProperty("t").GetString()!;
                string expected = root.TryGetProperty("error", out _) ? "error" : Format(root.GetProperty("r").EnumerateArray()
                    .Select(x => (x[0].GetInt32(), x[1].GetString()!, x[2].GetInt32(), x[3].GetInt32())));
                string actual;
                try
                {
                    var output = new List<ModelToken>();
                    model.Tokenize(text, output);
                    actual = Format(output.Select(m => (m.Id, m.Value, m.Start, m.End)));
                }
                catch (InvalidDataException)
                {
                    actual = "error";
                }

                compared++;
                if (actual != expected && wrong++ < 5)
                {
                    report.Add($"{Path.GetFileName(file)} {JsonSerializer.Serialize(text)}\n  want {expected}\n  got  {actual}");
                }
            }

            Assert.True(compared > 1000, file);
            report.Add($"{Path.GetFileName(file)}: {compared} words compared, {wrong} differ");
            Assert.True(wrong == 0, string.Join("\n", report));
        }

        Assert.NotEmpty(report);
        Console.WriteLine(string.Join("\n", report));
    }

    private static string Format(IEnumerable<(int Id, string Value, int Start, int End)> tokens)
    {
        var sb = new StringBuilder();
        foreach ((int id, string value, int s, int e) in tokens)
        {
            sb.Append('[').Append(id).Append(' ').Append(value).Append(' ').Append(s).Append(',').Append(e).Append(']');
        }

        return sb.ToString();
    }
}
