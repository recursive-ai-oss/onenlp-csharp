using System.Text;
using System.Text.Json;
using RecursiveAI.OneNlp.Tokenizers;
using RecursiveAI.OneNlp.Tokenizers.Bpe;
using RecursiveAI.OneNlp.Tokenizers.Hf;

namespace RecursiveAI.OneNlp.Tests;

public class BpeModelTests
{
    private static BpeModel Toy(BpeOptions? options = null, string mergesJson = """["a b", "ab c"]""", string vocabJson = """{"a":0,"b":1,"c":2,"ab":3,"abc":4,"<unk>":5}""")
    {
        string json = $$"""{"type":"BPE","vocab":{{vocabJson}},"merges":{{mergesJson}}{{(options is null ? "" : $",\"unk_token\":\"<unk>\",\"fuse_unk\":{(options.FuseUnknown ? "true" : "false")},\"byte_fallback\":{(options.ByteFallback ? "true" : "false")},\"ignore_merges\":{(options.IgnoreMerges ? "true" : "false")}")}}}""";
        using JsonDocument doc = JsonDocument.Parse(json);
        return BpeModel.FromConfig(new StageConfig(doc.RootElement.Clone(), StageRegistry.Default));
    }

    private static List<ModelToken> Run(BpeModel model, string piece)
    {
        var output = new List<ModelToken>();
        model.Tokenize(piece, output);
        return output;
    }

    [Fact]
    public void MergesByRankLeftmostFirst()
    {
        // a b (rank 0) merges at both places, leftmost first; then ab c (rank 1).
        List<ModelToken> t = Run(Toy(), "abcab");
        Assert.Equal([new ModelToken(4, "abc", 0, 3), new ModelToken(3, "ab", 3, 5)], t);
    }

    [Fact]
    public void ArrayMergeFormIsTheSameAsStringForm()
    {
        BpeModel a = Toy();
        BpeModel b = Toy(mergesJson: """[["a","b"],["ab","c"]]""");
        Assert.Equal(Run(a, "cabcabc"), Run(b, "cabcabc"));
    }

    [Fact]
    public void UnknownCharactersBecomeTheUnknownTokenAndFuseOnRequest()
    {
        Assert.Equal(
            [new ModelToken(3, "ab", 0, 2), new ModelToken(5, "<unk>", 2, 3), new ModelToken(5, "<unk>", 3, 4), new ModelToken(2, "c", 4, 5)],
            Run(Toy(new BpeOptions(UnknownToken: "<unk>")), "abxxc"));
        Assert.Equal(
            [new ModelToken(3, "ab", 0, 2), new ModelToken(5, "<unk>", 2, 4), new ModelToken(2, "c", 4, 5)],
            Run(Toy(new BpeOptions(UnknownToken: "<unk>", FuseUnknown: true)), "abxxc"));
    }

    [Fact]
    public void ByteFallbackSpellsAnUnknownCharacterWithItsBytes()
    {
        const string Vocab = """{"a":0,"<unk>":1,"<0xE6>":2,"<0x97>":3,"<0xA5>":4}""";
        BpeModel m = Toy(new BpeOptions(ByteFallback: true), "[]", Vocab);
        Assert.Equal(
            [new ModelToken(0, "a", 0, 1), new ModelToken(2, "<0xE6>", 1, 2), new ModelToken(3, "<0x97>", 1, 2), new ModelToken(4, "<0xA5>", 1, 2)],
            Run(m, "a日"));

        // A character whose bytes are not all there is unknown.
        Assert.Equal([new ModelToken(1, "<unk>", 0, 1)], Run(m, "é"));
    }

    [Fact]
    public void IgnoreMergesTakesAWholeVocabularyEntry()
    {
        BpeModel withIgnore = Toy(new BpeOptions(IgnoreMerges: true), """["b c"]""", """{"a":0,"b":1,"c":2,"bc":3,"abc":4,"<unk>":5}""");
        Assert.Equal([new ModelToken(4, "abc", 0, 3)], Run(withIgnore, "abc"));
        Assert.Equal(2, Run(Toy(new BpeOptions(), """["b c"]""", """{"a":0,"b":1,"c":2,"bc":3,"abc":4,"<unk>":5}"""), "abc").Count);
    }

    [Fact]
    public void ContinuingPrefixAndEndOfWordSuffixAreAppliedToLookups()
    {
        const string Vocab = """{"l":0,"o":1,"##o":2,"##w":3,"##ow":4,"low":5,"<unk>":6}""";
        string json = $$"""{"type":"BPE","vocab":{{Vocab}},"merges":["##o ##w","l ##ow"],"unk_token":"<unk>","continuing_subword_prefix":"##"}""";
        using JsonDocument doc = JsonDocument.Parse(json);
        BpeModel m = BpeModel.FromConfig(new StageConfig(doc.RootElement.Clone(), StageRegistry.Default));
        Assert.Equal([new ModelToken(5, "low", 0, 3)], Run(m, "low"));
    }

    [Fact]
    public void MergeThatNamesAMissingTokenIsRejected()
    {
        Assert.Throws<InvalidDataException>(() => Toy(mergesJson: """["a z"]"""));
        Assert.Throws<InvalidDataException>(() => Toy(mergesJson: """["a b"]""", vocabJson: """{"a":0,"b":1}"""));
        Assert.Throws<InvalidDataException>(() => Toy(mergesJson: """["a b c"]"""));
    }

    [Fact]
    public void LongPieceMatchesTheQuadraticReference()
    {
        // Merge rules over {a,b}: every pair joins; compare the heap loop with a plain "find the lowest rank, leftmost" reference.
        var vocab = new Dictionary<string, int> { ["a"] = 0, ["b"] = 1, ["aa"] = 2, ["ab"] = 3, ["ba"] = 4, ["bb"] = 5, ["aab"] = 6, ["abb"] = 7, ["aaab"] = 8 };
        (string, string)[] merges = [("a", "b"), ("a", "a"), ("b", "a"), ("aa", "b"), ("a", "bb"), ("b", "b"), ("aa", "ab")];
        BpeModel model = BpeModel.Create(vocab, merges, new BpeOptions());
        var random = new Random(3);
        for (int trial = 0; trial < 300; trial++)
        {
            string piece = new([.. Enumerable.Range(0, random.Next(1, 120)).Select(_ => random.Next(2) == 0 ? 'a' : 'b')]);
            List<string> expected = Reference(piece, merges);
            Assert.Equal(expected, Run(model, piece).Select(t => t.Value).ToList());
        }
    }

    private static List<string> Reference(string piece, (string, string)[] merges)
    {
        List<string> parts = [.. piece.Select(c => c.ToString())];
        while (true)
        {
            int best = int.MaxValue;
            int at = -1;
            for (int i = 0; i + 1 < parts.Count; i++)
            {
                int r = Array.IndexOf(merges, (parts[i], parts[i + 1]));
                if (r >= 0 && r < best)
                {
                    best = r;
                    at = i;
                }
            }

            if (at < 0)
            {
                return parts;
            }

            parts[at] += parts[at + 1];
            parts.RemoveAt(at + 1);
        }
    }

    [Fact]
    public void SentencePieceScoresOrderTheMerges()
    {
        // Pieces by ID: unk, a, b, c, ab (score -1), bc (score -2), abc (score -3).
        string[] pieces = ["<unk>", "a", "b", "c", "ab", "bc", "abc"];
        float[] scores = [0, 0, 0, 0, -1, -2, -3];
        bool[] mergeable = [false, true, true, true, true, true, true];
        BpeModel m = BpeModel.FromSentencePieces(pieces, scores, mergeable, new BpeOptions(UnknownToken: "<unk>"));
        // abc: ab (-1) beats bc (-2); then ab + c needs "abc" (-3) which exists.
        Assert.Equal([new ModelToken(6, "abc", 0, 3)], Run(m, "abc"));
        Assert.Equal([new ModelToken(2, "b", 0, 1), new ModelToken(5, "bc", 1, 3)], Run(m, "bbc"));
    }

    [Fact]
    public void DropoutOneSkipsEveryMergeAndZeroSkipsNone()
    {
        string json = """{"type":"BPE","vocab":{"a":0,"b":1,"ab":2},"merges":["a b"],"dropout":1.0}""";
        using JsonDocument doc = JsonDocument.Parse(json);
        BpeModel all = BpeModel.FromConfig(new StageConfig(doc.RootElement.Clone(), StageRegistry.Default));
        Assert.Equal(2, Run(all, "ab").Count);

        string json2 = """{"type":"BPE","vocab":{"a":0,"b":1,"ab":2},"merges":["a b"],"dropout":0.5}""";
        using JsonDocument doc2 = JsonDocument.Parse(json2);
        BpeModel half = BpeModel.FromConfig(new StageConfig(doc2.RootElement.Clone(), StageRegistry.Default));
        int merged = 0;
        for (int i = 0; i < 2000; i++)
        {
            merged += Run(half, "ab").Count == 1 ? 1 : 0;
        }

        Assert.InRange(merged, 800, 1200);
    }

    [Fact]
    public void InvalidUtf8InAVocabularyKeyIsAnInvalidFileNotACrash()
    {
        // Found by fuzzing: the JSON parser accepts the bad byte and only fails when the name is read.
        byte[] json = [.. "{\"vocab\":{\"a\":0,\"b"u8, 0x9E, 0x9E, .. "\":1},\"merges\":[]}"u8];
        using JsonDocument doc = JsonDocument.Parse(json);
        Assert.Throws<InvalidDataException>(() => BpeModel.FromConfig(new StageConfig(doc.RootElement.Clone(), StageRegistry.Default)));
    }

    [Fact]
    public void RegistryBuildsBpeFromTheDefaultRegistry()
    {
        using JsonDocument doc = JsonDocument.Parse("""{"vocab":{"a":0,"b":1,"ab":2},"merges":["a b"]}""");
        ITokenizerModel model = StageRegistry.Default.CreateModel(new StageConfig(doc.RootElement.Clone(), StageRegistry.Default));
        Assert.IsType<BpeModel>(model);
        Assert.Equal(3, model.VocabularySize);
        Assert.Equal("ab", model.IdToToken(2));
        Assert.Equal(2, model.TokenToId("ab"));
    }

    /// <summary>Model-level reference outputs from the released Python package (see tools/tokenizer-goldens); skipped when they are not around.</summary>
    [Theory]
    [InlineData("bpe-gpt2.jsonl", "openai-community_gpt2")]
    [InlineData("bpe-roberta.jsonl", "FacebookAI_roberta-base")]
    [InlineData("bpe-qwen.jsonl", "Qwen_Qwen2.5-0.5B")]
    [InlineData("bpe-modernbert.jsonl", "answerdotai_ModernBERT-base")]
    [InlineData("bpe-tinyllama.jsonl", "TinyLlama_TinyLlama-1.1B-Chat-v1.0")]
    public void MatchesReferenceModelOutput(string golden, string modelDir)
    {
        string? goldens = Environment.GetEnvironmentVariable("ONENLP_BPE_GOLDENS");
        string? models = Environment.GetEnvironmentVariable("ONENLP_TOKENIZER_MODELS");
        if (goldens is null || models is null)
        {
            Assert.Skip("ONENLP_BPE_GOLDENS and ONENLP_TOKENIZER_MODELS are not set.");
        }

        using JsonDocument file = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(models, modelDir, "tokenizer.json")));
        BpeModel model = BpeModel.FromConfig(new StageConfig(file.RootElement.GetProperty("model"), StageRegistry.Default));
        int pieces = 0;
        int tokens = 0;
        var mismatches = new List<string>();
        foreach (string line in File.ReadLines(Path.Combine(goldens, golden)))
        {
            using JsonDocument rec = JsonDocument.Parse(line);
            string piece = rec.RootElement.GetProperty("p").GetString()!;
            List<ModelToken> expected = [.. rec.RootElement.GetProperty("t").EnumerateArray().Select(t => new ModelToken(t[0].GetInt32(), t[1].GetString()!, t[2].GetInt32(), t[3].GetInt32()))];
            List<ModelToken> actual = Run(model, piece);
            pieces++;
            tokens += expected.Count;
            if (!expected.SequenceEqual(actual) && mismatches.Count < 10)
            {
                mismatches.Add($"{piece.Length} chars '{(piece.Length > 40 ? piece[..40] : piece)}': expected {string.Join(' ', expected.Take(8))} got {string.Join(' ', actual.Take(8))}");
            }
        }

        Assert.True(mismatches.Count == 0, $"{pieces} pieces, {tokens} tokens; mismatches: {string.Join(" | ", mismatches)}");
        Assert.True(pieces > 1000);
    }
}
