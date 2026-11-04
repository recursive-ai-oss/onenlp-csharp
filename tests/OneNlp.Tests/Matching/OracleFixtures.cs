using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace RecursiveAI.OneNlp.Tests.Matching;

/// <summary>Loads the committed oracle inputs and outputs and builds documents from them.</summary>
internal static class OracleFixtures
{
    private static readonly Lazy<JsonDocument> CasesDoc = new(() => Load("cases.json.gz"));
    private static readonly Lazy<JsonDocument> ExpectedDoc = new(() => Load("expected.json.gz"));

    internal static JsonElement Cases => CasesDoc.Value.RootElement;

    internal static JsonElement Expected => ExpectedDoc.Value.RootElement;

    private static string Dir([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

    private static JsonDocument Load(string name)
    {
        using FileStream file = File.OpenRead(Path.Combine(Dir(), name));
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        return JsonDocument.Parse(gzip);
    }

    internal static string[] Strings(JsonElement array) => [.. array.EnumerateArray().Select(e => e.GetString()!)];

    /// <summary>Builds a document from a case: words and spacing set the tokens, the rest fills annotation columns.</summary>
    internal static Doc MakeDoc(StringStore strings, JsonElement d)
    {
        string[] words = Strings(d.GetProperty("words"));
        bool[] spaces = [.. d.GetProperty("spaces").EnumerateArray().Select(e => e.GetBoolean())];
        var text = new System.Text.StringBuilder();
        var tokens = new TokenBoundary[words.Length];
        for (int i = 0; i < words.Length; i++)
        {
            tokens[i] = new TokenBoundary(text.Length, words[i].Length);
            text.Append(words[i]);
            if (spaces[i])
            {
                text.Append(' ');
            }
        }

        var doc = new Doc(strings, text.ToString(), tokens);
        Fill(doc, d, "lemmas", TokenColumn.Lemma);
        Fill(doc, d, "pos", TokenColumn.Pos);
        Fill(doc, d, "tags", TokenColumn.Tag);
        Fill(doc, d, "morphs", TokenColumn.Morph);
        Fill(doc, d, "deps", TokenColumn.Dep);
        if (d.TryGetProperty("norms", out JsonElement norms))
        {
            int i = 0;
            foreach (JsonElement n in norms.EnumerateArray())
            {
                if (n.ValueKind == JsonValueKind.String)
                {
                    doc.SetAnnotation(TokenColumn.Norm, i, n.GetString()!);
                }

                i++;
            }
        }

        if (d.TryGetProperty("sent_starts", out JsonElement starts) && starts.ValueKind == JsonValueKind.Array)
        {
            doc.SetSentenceBoundaries([.. starts.EnumerateArray().Select(e => e.GetBoolean() ? SentenceBoundary.Start : SentenceBoundary.Inside)]);
        }

        if (d.TryGetProperty("ents", out JsonElement ents) && ents.ValueKind == JsonValueKind.Array)
        {
            string[] tags = Strings(ents);
            var specs = new List<SpanSpec>();
            for (int i = 0; i < tags.Length; i++)
            {
                if (tags[i].StartsWith("B-", StringComparison.Ordinal))
                {
                    int end = i + 1;
                    while (end < tags.Length && tags[end].StartsWith("I-", StringComparison.Ordinal))
                    {
                        end++;
                    }

                    specs.Add(new SpanSpec(i, end, tags[i][2..]));
                }
            }

            doc.SetEntities([.. specs]);
        }

        return doc;
    }

    private static void Fill(Doc doc, JsonElement d, string property, TokenColumn column)
    {
        int i = 0;
        foreach (JsonElement e in d.GetProperty(property).EnumerateArray())
        {
            doc.SetAnnotation(column, i++, e.GetString()!);
        }
    }

    /// <summary>A phrase case as a document; the annotation columns are set, no entities.</summary>
    internal static Doc MakePhraseDoc(StringStore strings, JsonElement p)
    {
        string[] words = Strings(p.GetProperty("words"));
        var tokens = new TokenBoundary[words.Length];
        var text = new System.Text.StringBuilder();
        for (int i = 0; i < words.Length; i++)
        {
            tokens[i] = new TokenBoundary(text.Length, words[i].Length);
            text.Append(words[i]).Append(' ');
        }

        var doc = new Doc(strings, text.ToString(), tokens);
        Fill(doc, p, "lemmas", TokenColumn.Lemma);
        Fill(doc, p, "pos", TokenColumn.Pos);
        Fill(doc, p, "tags", TokenColumn.Tag);
        Fill(doc, p, "morphs", TokenColumn.Morph);
        Fill(doc, p, "deps", TokenColumn.Dep);
        return doc;
    }
}
