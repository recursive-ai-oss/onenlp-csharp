using System.Text;
using RecursiveAI.OneNlp.Tokenizers;
using RecursiveAI.OneNlp.Tokenizers.Hf;
using RecursiveAI.OneNlp.Tokenizers.Loaders;

namespace RecursiveAI.OneNlp.Tests;

/// <summary>Toy stages that exercise the seam: lowercase and drop "-" (a normalizer that changes lengths), split on spaces, one token per character.</summary>
internal sealed class ToyNormalizer : INormalizer
{
    public void Normalize(NormalizedString text)
    {
        var b = new NormalizedStringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            char c = text.Text[i];
            if (c == '-')
            {
                continue;
            }

            if (c == 'ß')
            {
                // One unit becomes two that share the origin.
                b.Append("ss", text.AlignmentAt(i));
                continue;
            }

            b.Append(char.ToLowerInvariant(c), text.AlignmentAt(i));
        }

        b.CopyTo(text);
    }
}

internal sealed class ToySpaceSplit : IPreTokenizer
{
    public void PreTokenize(PreTokenizedString text) => text.SplitEach(piece =>
    {
        var parts = new List<NormalizedString>();
        int start = 0;
        for (int i = 0; i <= piece.Length; i++)
        {
            if (i == piece.Length || piece.Text[i] == ' ')
            {
                if (i > start)
                {
                    parts.Add(piece.Slice(start, i));
                }

                start = i + 1;
            }
        }

        return parts;
    });
}

internal sealed class ToyModel : ITokenizerModel
{
    public int VocabularySize => 128;

    public void Tokenize(ReadOnlySpan<char> piece, List<ModelToken> output)
    {
        for (int i = 0; i < piece.Length; i++)
        {
            output.Add(new ModelToken(piece[i], piece[i].ToString(), i, i + 1));
        }
    }

    public int? TokenToId(string token) => token.Length == 1 && token[0] < 128 ? token[0] : null;

    public string? IdToToken(int id) => id is >= 0 and < 128 ? ((char)id).ToString() : null;
}

internal sealed class ToyTemplate : IPostProcessor
{
    // [CLS]=200 first, [SEP]=201 after each sequence; second sequence gets type id 1 from the encoder already.
    public int AddedTokenCount(bool isPair) => isPair ? 3 : 2;

    public TokenEncoding Process(TokenEncoding encoding, TokenEncoding? pair, bool addSpecialTokens)
    {
        var ids = new List<int>();
        var tokens = new List<string>();
        var offsets = new List<TokenOffset>();
        var special = new List<bool>();
        var types = new List<int>();
        var words = new List<int>();

        void Add(int id, string tok, TokenOffset o, bool sp, int type, int word)
        {
            ids.Add(id);
            tokens.Add(tok);
            offsets.Add(o);
            special.Add(sp);
            types.Add(type);
            words.Add(word);
        }

        void AddSequence(TokenEncoding e, int type)
        {
            for (int i = 0; i < e.Length; i++)
            {
                Add(e.Ids[i], e.Tokens[i], e.Offsets[i], false, type, e.WordIds[i]);
            }
        }

        if (addSpecialTokens)
        {
            Add(200, "[CLS]", default, true, 0, -1);
        }

        AddSequence(encoding, 0);
        if (addSpecialTokens)
        {
            Add(201, "[SEP]", default, true, 0, -1);
        }

        if (pair is not null)
        {
            AddSequence(pair, 1);
            if (addSpecialTokens)
            {
                Add(201, "[SEP]", default, true, 1, -1);
            }
        }

        return new TokenEncoding([.. ids], [.. tokens], [.. offsets], [.. special], [.. types], wordIds: [.. words]);
    }
}

internal sealed class ToyDecoder : ITokenDecoder
{
    public List<string> DecodeChain(List<string> tokens) => [.. tokens.Select(t => t.ToUpperInvariant())];
}

public class TokenizerSeamTests
{
    private static HuggingFaceTokenizer Toy(bool template = false) => new(
        new ToyModel(),
        new ToyNormalizer(),
        new ToySpaceSplit(),
        template ? new ToyTemplate() : null,
        new ToyDecoder(),
        new AddedVocabulary([new AddedToken(200, "[CLS]", true, false, false, false, false), new AddedToken(201, "[SEP]", true, false, false, false, false)]));

    [Fact]
    public void OffsetsPointIntoTheOriginalTextThroughNormalization()
    {
        const string text = "Ab-c  Straße x";
        TokenEncoding e = Toy().Encode(text);
        Assert.Equal("abc", string.Concat(e.Tokens.Take(3)));
        Assert.Equal("abcstrasse" + "x", string.Concat(e.Tokens));
        // "Ab-c": the dash is dropped, so 'c' maps to offset 3..4 and the piece "abc" has words 0.
        Assert.Equal(new TokenOffset(0, 1), e.Offsets[0]);
        Assert.Equal(new TokenOffset(3, 4), e.Offsets[2]);
        // ß becomes "ss": both tokens cover the original ß.
        int s = text.IndexOf('ß', StringComparison.Ordinal);
        TokenOffset[] sharp = [.. e.Offsets.Where(o => o.Start == s)];
        Assert.Equal(2, sharp.Length);
        Assert.All(sharp, o => Assert.Equal(new TokenOffset(s, s + 1), o));
        Assert.Equal([0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 2], e.WordIds);
        Assert.All(e.TypeIds, t => Assert.Equal(0, t));
    }

    [Fact]
    public void EmptyAndBlankTextEncodeToNothing()
    {
        Assert.Equal(0, Toy().Encode("").Length);
        Assert.Equal(0, Toy().Encode("   ").Length);
        Assert.Equal(2, Toy(template: true).Encode("").Length);
    }

    [Fact]
    public void PostProcessorAddsTemplateAndMask()
    {
        TokenEncoding e = Toy(template: true).Encode("ab");
        Assert.Equal([200, 'a', 'b', 201], e.Ids);
        Assert.Equal([true, false, false, true], e.SpecialTokensMask);
        Assert.Equal([default, new TokenOffset(0, 1), new TokenOffset(1, 2), default], e.Offsets);
        Assert.Equal(2, Toy(template: true).Encode("ab", addSpecialTokens: false).Length);
    }

    [Fact]
    public void PairEncodingNumbersSequencesAndTruncatesLongestFirst()
    {
        HuggingFaceTokenizer t = Toy(template: true);
        TokenEncoding e = t.Encode("ab", "cde");
        Assert.Equal([200, 'a', 'b', 201, 'c', 'd', 'e', 201], e.Ids);
        Assert.Equal([0, 0, 0, 0, 1, 1, 1, 1], e.TypeIds);

        t.Truncation = new TruncationOptions(7);
        e = t.Encode("ab", "cde");
        Assert.Equal(7, e.Length);
        Assert.Equal(['a', 'b', 'c', 'd'], e.Ids.Where(i => i < 128));
        t.Truncation = new TruncationOptions(4, TokenizerSide.Left);
        Assert.Equal([200, 'c', 'd', 201], t.Encode("abcd").Ids.Select(i => i == 'c' || i == 'd' || i > 128 ? i : -1));
    }

    [Fact]
    public void TruncationLeavesRoomForSpecialTokensOnlyWhenTheyAreAdded()
    {
        HuggingFaceTokenizer t = Toy(template: true);
        t.Truncation = new TruncationOptions(3);
        Assert.Equal(3, t.Encode("abcdef").Length);
        Assert.Equal(3, t.Encode("abcdef", addSpecialTokens: false).Length);
        Assert.Equal(['a'], t.Encode("abcdef").Ids.Where(i => i < 128));
        Assert.Equal(['a', 'b', 'c'], t.Encode("abcdef", addSpecialTokens: false).Ids);
    }

    [Fact]
    public void DecodeSkipsSpecialAddedTokensAndRunsTheDecoder()
    {
        HuggingFaceTokenizer t = Toy(template: true);
        Assert.Equal("AB", t.Decode([200, 'a', 'b', 201]));
        Assert.Equal("[CLS]AB[SEP]", t.Decode([200, 'a', 'b', 201], skipSpecialTokens: false));
        Assert.Equal(128 + 2, t.VocabularySize);
        Assert.Equal(200, t.TokenToId("[CLS]"));
        Assert.Equal('a', t.TokenToId("a"));
        Assert.Equal("[SEP]", t.IdToToken(201));
    }

    [Fact]
    public void BaseClassHelpersWorkOnTheComposedTokenizer()
    {
        HuggingFaceTokenizer t = Toy();
        Assert.Equal(5, t.CountTokens("ab cde"));
        Assert.Equal("ab cd", t.TrimToTokenBudget("ab cde", 4).ToString());
        Span<int> ids = stackalloc int[3];
        Assert.Equal(System.Buffers.OperationStatus.DestinationTooSmall, t.EncodeToIds("ab cde", ids, out int written, out _));
        Assert.Equal(3, written);
        Assert.Equal(Encoding.UTF8.GetByteCount("Ab-c"), t.Encode("Ab-c"u8).Offsets.Max(o => o.End));
    }

    private static StageRegistry ToyRegistry()
    {
        var r = new StageRegistry();
        r.AddNormalizer("Toy", _ => new ToyNormalizer());
        r.AddPreTokenizer("Toy", _ => new ToySpaceSplit());
        r.AddModel("Toy", _ => new ToyModel());
        r.AddPostProcessor("Toy", _ => new ToyTemplate());
        r.AddDecoder("Toy", c => new ToyDecoder());
        return r;
    }

    private static Tokenizer Load(string json, StageRegistry? registry = null) =>
        HuggingFaceTokenizerJson.Load(new MemoryStream(Encoding.UTF8.GetBytes(json)), registry ?? ToyRegistry());

    [Fact]
    public void LoaderComposesStagesAddedTokensTruncationAndPadding()
    {
        const string json = """
        {
          "version": "1.0",
          "truncation": {"direction": "Left", "max_length": 6, "strategy": "LongestFirst", "stride": 0},
          "padding": {"strategy": {"Fixed": 8}, "direction": "Left", "pad_to_multiple_of": null, "pad_id": 0, "pad_type_id": 0, "pad_token": "[PAD]"},
          "added_tokens": [
            {"id": 200, "content": "[CLS]", "single_word": false, "lstrip": false, "rstrip": false, "normalized": false, "special": true},
            {"id": 201, "content": "[SEP]", "special": true}
          ],
          "normalizer": {"type": "Toy"},
          "pre_tokenizer": {"type": "Toy"},
          "model": {"type": "Toy"},
          "post_processor": {"type": "Toy"},
          "decoder": {"type": "Toy"}
        }
        """;
        Tokenizer t = Load(json);
        Assert.Equal(new TruncationOptions(6, TokenizerSide.Left), t.Truncation);
        Assert.Equal(8, t.Padding!.Length);
        Assert.Equal(TokenizerSide.Left, t.Padding.Side);
        TokenEncoding e = t.Encode("abcdefgh");
        Assert.Equal(8, e.Length);
        Assert.Equal([0, 0, 1, 1, 1, 1, 1, 1], e.AttentionMask);
        Assert.Equal([200, 'e', 'f', 'g', 'h', 201], e.Ids[2..]);
        Assert.Equal(130, t.VocabularySize);
        Assert.Equal("EFGH", t.Decode(e.Ids.Where(i => i != 0).ToArray()));
    }

    [Fact]
    public void ModelTypeDefaultsToBpeAndUnknownTypesAreRefused()
    {
        var r = new StageRegistry();
        r.AddModel("BPE", _ => new ToyModel());
        Assert.NotNull(Load("""{"model": {"merges": [], "vocab": {}}}""", r));

        NotSupportedException ex = Assert.Throws<NotSupportedException>(() => Load("""{"model": {"type": "Nope"}}""", r));
        Assert.Contains("'Nope'", ex.Message, StringComparison.Ordinal);
        Assert.Throws<NotSupportedException>(() => Load("""{"model": {"type": "BPE"}, "normalizer": {"type": "Lowercase"}}""", r));
    }

    [Fact]
    public void TheDefaultRegistryRefusesUndocumentedTypesByName()
    {
        NotSupportedException ex = Assert.Throws<NotSupportedException>(() => Load("""{"model": {"type": "Nope"}}""", StageRegistry.Default));
        Assert.Contains("'Nope'", ex.Message, StringComparison.Ordinal);
        Assert.Contains("not supported", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("{")]
    [InlineData("{}")]
    [InlineData("""{"model": 3}""")]
    [InlineData("""{"model": {"type": "Toy"}, "added_tokens": [{"id": 1}]}""")]
    [InlineData("""{"model": {"type": "Toy"}, "added_tokens": [{"id": 1, "content": "a"}, {"id": 1, "content": "b"}]}""")]
    [InlineData("""{"model": {"type": "Toy"}, "truncation": {"max_length": 0}}""")]
    [InlineData("""{"model": {"type": "Toy"}, "truncation": {"max_length": 4, "direction": "Up"}}""")]
    [InlineData("""{"model": {"type": "Toy"}, "padding": {"pad_id": "x"}}""")]
    public void MalformedFilesThrowInvalidData(string json) => Assert.Throws<InvalidDataException>(() => Load(json));

    [Fact]
    public void DeeplyNestedJsonIsRefused()
    {
        string deep = string.Concat(Enumerable.Repeat("[", 200)) + string.Concat(Enumerable.Repeat("]", 200));
        Assert.Throws<InvalidDataException>(() => Load("{\"model\": {\"type\": \"Toy\"}, \"x\": " + deep + "}"));
    }

    [Fact]
    public void NormalizedStringKeepsAlignment()
    {
        var s = new NormalizedString("héllo");
        Assert.Equal(new OffsetPair(1, 4), s.ToOriginal(1, 4));
        NormalizedString slice = s.Slice(2, 4);
        Assert.Equal("ll", slice.Text);
        Assert.Equal(new OffsetPair(2, 4), slice.ToOriginal(0, 2));
        Assert.Throws<ArgumentException>(() => s.Replace("ab", new OffsetPair[1]));
        Assert.Throws<ArgumentOutOfRangeException>(() => s.Slice(3, 9));
        var pieces = new PreTokenizedString(new NormalizedString(""));
        Assert.Empty(pieces.Splits);
    }
}
