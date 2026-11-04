using System.Diagnostics;
using System.Text;
using RecursiveAI.OneNlp.Tokenizers;
using RecursiveAI.OneNlp.Tokenizers.Hf;
using RecursiveAI.OneNlp.Tokenizers.Loaders;

namespace RecursiveAI.OneNlp.Tests;

/// <summary>Edge cases of the tokenizer.json stages that the reference-output tests do not cover: the regex engine's limits and loader errors.</summary>
public class StageEdgeTests
{
    private static string[] Split(string pattern, string text)
    {
        CodePointRegex re = CodePointRegex.Parse(pattern);
        return [.. re.Matches(text).Select(m => text[m.Start..m.End])];
    }

    [Fact]
    public void QwenSplitPatternSeparatesDigitsContractionsAndNewlines()
    {
        const string qwen = @"(?i:'s|'t|'re|'ve|'m|'ll|'d)|[^\r\n\p{L}\p{N}]?\p{L}+|\p{N}| ?[^\s\p{L}\p{N}]+[\r\n]*|\s*[\r\n]+|\s+(?!\S)|\s+";
        Assert.Equal(["I", "'LL", " go", " ", "1", "2", "3", "!\n\n", "ok"], Split(qwen, "I'LL go 123!\n\nok"));
    }

    [Fact]
    public void AstralCharactersCountAsOneLetter()
    {
        Assert.Equal(["\U0001D49C\U0001D539", "x"], Split(@"\p{L}+|\S", "\U0001D49C\U0001D539 x"));
        Assert.Equal(["\U0001F600"], Split(@"\p{So}", "a\U0001F600b"));
    }

    [Theory]
    [InlineData("(")]
    [InlineData("a)")]
    [InlineData("[a")]
    [InlineData(@"\q")]
    [InlineData(@"\p{NoSuchProperty}")]
    [InlineData("*a")]
    [InlineData("[z-a]")]
    [InlineData(@"\")]
    [InlineData("(?<n")]
    [InlineData("(?z)")]
    [InlineData(@"[a-\")]
    [InlineData(@"[\")]
    public void MalformedPatternsAreRefused(string pattern) =>
        Assert.Throws<InvalidDataException>(() => CodePointRegex.Parse(pattern));

    [Fact]
    public void DeepNestingAndHugePatternsAreRefused()
    {
        Assert.Throws<InvalidDataException>(() => CodePointRegex.Parse(new string('(', 500) + "a" + new string(')', 500)));
        Assert.Throws<InvalidDataException>(() => CodePointRegex.Parse(new string('a', 100_000)));
    }

    [Theory]
    [InlineData("(a+)+b")]
    [InlineData("(a|a)*b")]
    [InlineData("(?:a*)*b")]
    [InlineData("(a?){40}a{40}b")]
    public void HostilePatternsEndQuicklyWithAnError(string pattern)
    {
        CodePointRegex re = CodePointRegex.Parse(pattern);
        string text = new('a', 3_000);
        var watch = Stopwatch.StartNew();
        Assert.Throws<InvalidOperationException>(() => re.Matches(text));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), $"took {watch.Elapsed}");
    }

    [Fact]
    public void LongRealisticInputStaysWithinTheBudget()
    {
        const string gpt2 = @"'s|'t|'re|'ve|'m|'ll|'d| ?\p{L}+| ?\p{N}+| ?[^\s\p{L}\p{N}]+|\s+(?!\S)|\s+";
        string text = string.Concat(Enumerable.Repeat("Hello, world! 12345 \t\t\n  \U0001F600 ", 40_000));
        CodePointRegex re = CodePointRegex.Parse(gpt2);
        var watch = Stopwatch.StartNew();
        int count = re.Matches(text).Count;
        Assert.True(count > 100_000);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), $"took {watch.Elapsed}");
    }

    private static StageRegistry CharRegistry()
    {
        StageRegistry r = StageRegistry.Default.Clone();
        r.AddModel("BPE", c => new CharModel(c));
        return r;
    }

    private static Tokenizer Load(string json) =>
        HuggingFaceTokenizerJson.Load(new MemoryStream(Encoding.UTF8.GetBytes(json)), CharRegistry());

    private const string Head = """
        {"version":"1.0","truncation":null,"padding":null,
         "added_tokens":[{"id":3,"content":"[CLS]","single_word":false,"lstrip":false,"rstrip":false,"normalized":false,"special":true},
                         {"id":4,"content":"[SEP]","single_word":false,"lstrip":false,"rstrip":false,"normalized":false,"special":true}],
        """;

    private const string Model = """
        "model":{"type":"BPE","vocab":{"a":0,"b":1,"[UNK]":2,"[CLS]":3,"[SEP]":4,"▁":5,"▁a":6},"merges":[],"unk_token":"[UNK]"}
        """;

    [Fact]
    public void TemplateWithoutSpecialTokensKeepsPairTypeIds()
    {
        string json = Head + """
            "normalizer":null,"pre_tokenizer":null,"decoder":null,
            "post_processor":{"type":"TemplateProcessing","single":[{"SpecialToken":{"id":"[CLS]","type_id":0}},{"Sequence":{"id":"A","type_id":0}},{"SpecialToken":{"id":"[SEP]","type_id":0}}],
              "pair":[{"SpecialToken":{"id":"[CLS]","type_id":0}},{"Sequence":{"id":"A","type_id":0}},{"SpecialToken":{"id":"[SEP]","type_id":0}},{"Sequence":{"id":"B","type_id":1}},{"SpecialToken":{"id":"[SEP]","type_id":1}}],
              "special_tokens":{"[CLS]":{"id":"[CLS]","ids":[3],"tokens":["[CLS]"]},"[SEP]":{"id":"[SEP]","ids":[4],"tokens":["[SEP]"]}}},
            """ + Model + "}";
        Tokenizer t = Load(json);
        TokenEncoding bare = t.Encode("ab", "ba", addSpecialTokens: false);
        Assert.Equal([0, 1, 1, 0], bare.Ids.ToArray());
        Assert.Equal([0, 0, 1, 1], bare.TypeIds.ToArray());
        TokenEncoding full = t.Encode("ab", "ba");
        Assert.Equal([3, 0, 1, 4, 1, 0, 4], full.Ids.ToArray());
        Assert.Equal([0, 0, 0, 0, 1, 1, 1], full.TypeIds.ToArray());
        Assert.Equal([true, false, false, true, false, false, true], full.SpecialTokensMask.ToArray());
    }

    [Fact]
    public void AddedTokensWinOverlapsLongestFirstAndEmptyInputGivesNoTokens()
    {
        string json = """
            {"version":"1.0","truncation":null,"padding":null,
             "added_tokens":[{"id":10,"content":"ab","single_word":false,"lstrip":false,"rstrip":false,"normalized":false,"special":false},
                             {"id":11,"content":"abab","single_word":false,"lstrip":false,"rstrip":false,"normalized":false,"special":false}],
             "normalizer":null,"pre_tokenizer":null,"post_processor":null,"decoder":null,
            """ + Model + "}";
        Tokenizer t = Load(json);
        Assert.Equal([11, 10, 0], t.Encode("abababa", addSpecialTokens: false).Ids.ToArray());
        Assert.Empty(t.Encode("", addSpecialTokens: true).Ids.ToArray());
    }

    [Theory]
    [InlineData(@"{""type"":""Metaspace"",""replacement"":""▁"",""add_prefix_space"":true,""prepend_scheme"":""always"",""split"":true}")]
    [InlineData(@"{""type"":""Metaspace"",""replacement"":""▁"",""add_prefix_space"":true,""split"":true}")]
    public void MetaspaceMarksEachWordStart(string preTokenizer)
    {
        string json = Head + $$"""
            "normalizer":null,"pre_tokenizer":{{preTokenizer}},"post_processor":null,"decoder":null,
            """ + Model + "}";
        TokenEncoding e = Load(json).Encode("a a", addSpecialTokens: false);
        // The test model has one token per character, so the marker and the letter come out separately.
        Assert.Equal(["▁", "a", "▁", "a"], e.Tokens.ToArray());
    }

    [Fact]
    public void InvalidUtf8InsideAStringIsADataError()
    {
        // Found by fuzzing: a stray 0xEE byte in a stage type name used to escape as InvalidOperationException.
        byte[] json = [.. "{\"model\":{\"type\":\"B"u8, 0xEE, .. "PE\",\"vocab\":{},\"merges\":[]}}"u8];
        Assert.Throws<InvalidDataException>(() => HuggingFaceTokenizerJson.Load(new MemoryStream(json), CharRegistry()));
    }

    [Theory]
    [InlineData(@"{""model"":{""type"":""BPE"",""vocab"":{},""merges"":[]},""post_processor"":{""type"":""TemplateProcessing"",""single"":[],""pair"":[],""special_tokens"":{""x"":{""id"":""x""}}}}")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData(@"{""model"":{""type"":""NoSuchModel""}}")]
    [InlineData(@"{""model"":{""type"":""BPE"",""vocab"":{},""merges"":[]},""normalizer"":{""type"":""NoSuchNormalizer""}}")]
    [InlineData(@"{""model"":{""type"":""BPE"",""vocab"":{},""merges"":[]},""pre_tokenizer"":{""type"":""Split"",""pattern"":{""Regex"":""(""},""behavior"":""Isolated"",""invert"":false}}")]
    [InlineData(@"{""model"":{""type"":""BPE"",""vocab"":{},""merges"":[]},""added_tokens"":[{""id"":1,""content"":""x""},{""id"":1,""content"":""y""}]}")]
    public void BrokenFilesAreRefusedWithDataErrors(string json)
    {
        Exception ex = Assert.ThrowsAny<Exception>(() => Load(json));
        Assert.True(ex is InvalidDataException or NotSupportedException, ex.ToString());
    }
}
