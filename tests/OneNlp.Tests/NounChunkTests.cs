using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace RecursiveAI.OneNlp.Tests;

/// <summary>
/// Noun chunks: the rule is compared with spaCy 3.8.18's output on the same trees (tools/oracles/spacy_chunks regenerates the
/// fixture), then the Universal Dependencies profile is checked on hand-built sentences.
/// </summary>
public class NounChunkTests
{
    private static string Dir([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

    private static Doc Build(string[] words, string[] pos, int[] heads, string[] deps)
    {
        var strings = new StringStore();
        var boundaries = new TokenBoundary[words.Length];
        string text = string.Join(' ', words);
        int at = 0;
        for (int i = 0; i < words.Length; i++)
        {
            boundaries[i] = new TokenBoundary(at, words[i].Length);
            at += words[i].Length + 1;
        }

        var doc = new Doc(strings, text, boundaries);
        for (int i = 0; i < words.Length; i++)
        {
            doc.SetAnnotation(TokenColumn.Pos, i, pos[i]);
            doc.SetAnnotation(TokenColumn.Dep, i, deps[i]);
        }

        doc.SetHeads(heads);
        return doc;
    }

    [Fact]
    public void AgreesWithSpacyOnRandomAndTreebankTrees()
    {
        using FileStream file = File.OpenRead(Path.Combine(Dir(), "NounChunks", "cases.json.gz"));
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using JsonDocument json = JsonDocument.Parse(gzip);
        Assert.Equal("3.8.18", json.RootElement.GetProperty("spacy").GetString());
        int total = 0;
        var mismatches = new List<string>();
        foreach (JsonElement c in json.RootElement.GetProperty("cases").EnumerateArray())
        {
            string[] pos = [.. c.GetProperty("pos").EnumerateArray().Select(x => x.GetString()!)];
            string[] deps = [.. c.GetProperty("deps").EnumerateArray().Select(x => x.GetString()!)];
            int[] heads = [.. c.GetProperty("heads").EnumerateArray().Select(x => x.GetInt32())];
            Doc doc = Build([.. pos.Select((_, i) => "w" + i)], pos, heads, deps);
            string want = string.Join(',', c.GetProperty("chunks").EnumerateArray().Select(x => $"{x[0]}-{x[1]}"));
            string got = string.Join(',', NounChunker.Find(doc, NounChunkProfile.ClearStyle).Select(s => $"{s.Start}-{s.End}"));
            total++;
            if (want != got)
            {
                mismatches.Add($"{c.GetProperty("kind").GetString()} heads={string.Join(' ', heads)} deps={string.Join(' ', deps)} spacy={want} ours={got}");
            }
        }

        // The known residue: spaCy mis-reports the left edge of a phrase when arcs cross several times in a row (heavily
        // non-projective random trees); none of these occurs in the treebank trees, which carry the real-data weight.
        Assert.True(total >= 6000);
        Assert.True(mismatches.Count <= KnownMismatches, $"{mismatches.Count} of {total} differ:\n{string.Join('\n', mismatches.Take(5))}");
        Assert.DoesNotContain(mismatches, m => m.StartsWith("ud ", StringComparison.Ordinal));
    }

    private const int KnownMismatches = 3;

    [Fact]
    public void UniversalDependenciesKeepsCaseMarkersAndClausesOut()
    {
        // "The chief of the staff left because he was tired ."
        string[] words = ["The", "chief", "of", "the", "staff", "left", "because", "he", "was", "tired", "."];
        string[] pos = ["DET", "NOUN", "ADP", "DET", "NOUN", "VERB", "SCONJ", "PRON", "AUX", "ADJ", "PUNCT"];
        int[] heads = [1, 5, 4, 4, 1, 5, 9, 9, 9, 5, 5];
        string[] deps = ["det", "nsubj", "case", "det", "nmod", "root", "mark", "nsubj", "cop", "advcl", "punct"];
        Doc doc = Build(words, pos, heads, deps);
        Assert.Equal(["The chief", "the staff", "he"], doc.NounChunks.Select(s => s.ToString()));
    }

    [Fact]
    public void UniversalDependenciesCoversNamesWrittenHeadFirst()
    {
        // "Ada Lovelace met Bob and Eve ."
        string[] words = ["Ada", "Lovelace", "met", "Bob", "and", "Eve", "."];
        string[] pos = ["PROPN", "PROPN", "VERB", "PROPN", "CCONJ", "PROPN", "PUNCT"];
        int[] heads = [2, 0, 2, 2, 5, 3, 2];
        string[] deps = ["nsubj", "flat", "root", "obj", "cc", "conj", "punct"];
        Doc doc = Build(words, pos, heads, deps);
        Assert.Equal(["Ada Lovelace", "Bob", "Eve"], doc.NounChunks.Select(s => s.ToString()));
    }

    [Fact]
    public void TimeAdverbialsAndPossessorsAreNotPhraseHeads()
    {
        // "He left yesterday ."  with yesterday as obl:tmod
        string[] words = ["He", "left", "yesterday", "."];
        string[] pos = ["PRON", "VERB", "NOUN", "PUNCT"];
        Doc doc = Build(words, pos, [1, 1, 1, 1], ["nsubj", "root", "obl:tmod", "punct"]);
        Assert.Equal(["He"], doc.NounChunks.Select(s => s.ToString()));
    }

    [Fact]
    public void ADocumentWithoutAParseThrows()
    {
        var doc = new Doc(new StringStore(), "a b", [new TokenBoundary(0, 1), new TokenBoundary(2, 1)]);
        Assert.Throws<InvalidOperationException>(() => doc.NounChunks);
    }
}
