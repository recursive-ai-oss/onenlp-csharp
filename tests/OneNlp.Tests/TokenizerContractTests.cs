using System.Buffers;
using System.Text;
using RecursiveAI.OneNlp.Tokenizers;

namespace RecursiveAI.OneNlp.Tests;

/// <summary>A stand-in family: one token per whitespace-separated word, ID = 1 + index in a fixed list, 0 = unknown, and [S] as a special token.</summary>
internal sealed class WordListTokenizer : Tokenizer
{
    private static readonly string[] Vocabulary = ["[UNK]", "hello", "world", "héllo", "😀", "[S]", "[PAD]"];

    public override int VocabularySize => Vocabulary.Length;

    protected override TokenEncoding EncodeCore(ReadOnlySpan<char> text, bool addSpecialTokens, TruncationOptions? truncation)
    {
        var ids = new List<int>();
        var tokens = new List<string>();
        var offsets = new List<TokenOffset>();
        var special = new List<bool>();
        int i = 0;
        while (i < text.Length)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                i++;
                continue;
            }

            int start = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i]))
            {
                i++;
            }

            string word = text[start..i].ToString();
            int id = Array.IndexOf(Vocabulary, word);
            ids.Add(id < 0 ? 0 : id);
            tokens.Add(id < 0 ? Vocabulary[0] : word);
            offsets.Add(new TokenOffset(start, i));
            special.Add(false);
        }

        if (truncation is { } t && ids.Count > t.MaxLength)
        {
            int drop = ids.Count - t.MaxLength;
            int from = t.Side == TokenizerSide.Right ? t.MaxLength : 0;
            ids.RemoveRange(from, drop);
            tokens.RemoveRange(from, drop);
            offsets.RemoveRange(from, drop);
            special.RemoveRange(from, drop);
        }

        if (addSpecialTokens)
        {
            ids.Add(5);
            tokens.Add("[S]");
            offsets.Add(default);
            special.Add(true);
        }

        return new TokenEncoding([.. ids], [.. tokens], [.. offsets], [.. special]);
    }

    public override string Decode(ReadOnlySpan<int> ids, bool skipSpecialTokens = true)
    {
        var words = new List<string>();
        foreach (int id in ids)
        {
            if (skipSpecialTokens && id >= 5)
            {
                continue;
            }

            words.Add(IdToToken(id) ?? string.Empty);
        }

        return string.Join(' ', words);
    }

    public override string? IdToToken(int id) => (uint)id < (uint)Vocabulary.Length ? Vocabulary[id] : null;

    public override int? TokenToId(string token) => Array.IndexOf(Vocabulary, token) is >= 0 and int i ? i : null;
}

public class TokenizerContractTests
{
    private readonly WordListTokenizer _tokenizer = new();

    [Fact]
    public void EncodeGivesParallelColumnsAndOffsetsIntoTheInput()
    {
        const string text = "hello  unknown world";
        TokenEncoding e = _tokenizer.Encode(text);
        Assert.Equal([1, 0, 2, 5], e.Ids);
        Assert.Equal(["hello", "[UNK]", "world", "[S]"], e.Tokens);
        Assert.Equal([new TokenOffset(0, 5), new TokenOffset(7, 14), new TokenOffset(15, 20), default], e.Offsets);
        Assert.Equal([false, false, false, true], e.SpecialTokensMask);
        Assert.Equal([0, 0, 0, 0], e.TypeIds);
        Assert.Equal([1, 1, 1, 1], e.AttentionMask);
        Assert.Equal([-1, -1, -1, -1], e.WordIds);
        Assert.Equal(4, e.Length);
        Assert.Equal(2, _tokenizer.Encode(text, addSpecialTokens: false).Ids.Count(id => id != 0));
    }

    [Fact]
    public void EmptyInputEncodesToNothing()
    {
        Assert.Equal(0, _tokenizer.Encode("", addSpecialTokens: false).Length);
        Assert.Equal(OperationStatus.Done, _tokenizer.EncodeToIds("", new int[2], out int written, out int consumed, addSpecialTokens: false));
        Assert.Equal((0, 0), (written, consumed));
    }

    [Fact]
    public void Utf8OffsetsAreBytes()
    {
        string text = "héllo 😀 world";
        byte[] utf8 = Encoding.UTF8.GetBytes(text);
        TokenEncoding chars = _tokenizer.Encode(text, addSpecialTokens: false);
        TokenEncoding bytes = _tokenizer.Encode(utf8, addSpecialTokens: false);
        Assert.Equal(chars.Ids, bytes.Ids);
        Assert.Equal(3, bytes.Length);
        // héllo is 6 bytes, the emoji 4.
        Assert.Equal([new TokenOffset(0, 6), new TokenOffset(7, 11), new TokenOffset(12, 17)], bytes.Offsets);
        for (int i = 0; i < bytes.Length; i++)
        {
            Assert.Equal(
                text.Substring(chars.Offsets[i].Start, chars.Offsets[i].End - chars.Offsets[i].Start),
                Encoding.UTF8.GetString(utf8.AsSpan(bytes.Offsets[i].Start, bytes.Offsets[i].End - bytes.Offsets[i].Start)));
        }
    }

    [Fact]
    public void InvalidUtf8IsReplacedAndOffsetsStayInBytes()
    {
        byte[] utf8 = [(byte)'h', 0xFF, 0xFE, (byte)' ', (byte)'x'];
        TokenEncoding e = _tokenizer.Encode(utf8, addSpecialTokens: false);
        Assert.Equal(2, e.Length);
        Assert.Equal(new TokenOffset(0, 3), e.Offsets[0]);
        Assert.Equal(new TokenOffset(4, 5), e.Offsets[1]);
    }

    [Fact]
    public void EncodeToIdsFillsThePrefixAndReportsTheRest()
    {
        const string text = "hello world hello";
        Span<int> small = stackalloc int[2];
        OperationStatus status = _tokenizer.EncodeToIds(text, small, out int written, out int consumed, addSpecialTokens: false);
        Assert.Equal(OperationStatus.DestinationTooSmall, status);
        Assert.Equal(2, written);
        Assert.Equal(11, consumed);
        Assert.Equal([1, 2], small.ToArray());

        Span<int> big = stackalloc int[8];
        Assert.Equal(OperationStatus.Done, _tokenizer.EncodeToIds(text, big, out written, out consumed, addSpecialTokens: false));
        Assert.Equal((3, text.Length), (written, consumed));

        Assert.Equal(OperationStatus.Done, _tokenizer.EncodeToIds(Encoding.UTF8.GetBytes(text), big, out written, out consumed));
        Assert.Equal((4, text.Length), (written, consumed));
        Assert.Equal(OperationStatus.DestinationTooSmall, _tokenizer.EncodeToIds(Encoding.UTF8.GetBytes("héllo world"), small[..1], out written, out consumed, false));
        Assert.Equal((1, 6), (written, consumed));
    }

    [Fact]
    public void CountingAndTrimmingToABudget()
    {
        const string text = "hello  world hello world";
        Assert.Equal(4, _tokenizer.CountTokens(text, addSpecialTokens: false));
        Assert.Equal(5, _tokenizer.CountTokens(text));
        Assert.Equal(4, _tokenizer.CountTokens(Encoding.UTF8.GetBytes(text), addSpecialTokens: false));
        Assert.Equal("hello  world", _tokenizer.TrimToTokenBudget(text, 2).ToString());
        Assert.Equal("hello world", _tokenizer.TrimToTokenBudget(text, 2, TokenizerSide.Right).ToString());
        Assert.Equal(text, _tokenizer.TrimToTokenBudget(text, 4).ToString());
        Assert.Equal(text, _tokenizer.TrimToTokenBudget(text, 100).ToString());
        Assert.Equal("", _tokenizer.TrimToTokenBudget(text, 0).ToString());
        Assert.Throws<ArgumentOutOfRangeException>(() => _tokenizer.TrimToTokenBudget(text, -1));

        // Counting and trimming ignore the tokenizer's own truncation.
        _tokenizer.Truncation = new TruncationOptions(1);
        Assert.Equal(4, _tokenizer.CountTokens(text, addSpecialTokens: false));
        Assert.Equal("hello  world", _tokenizer.TrimToTokenBudget(text, 2).ToString());
        _tokenizer.Truncation = null;
    }

    [Fact]
    public void TruncationAndPaddingApplyToEncodeOnly()
    {
        var tokenizer = new WordListTokenizer
        {
            Truncation = new TruncationOptions(2),
            Padding = new PaddingOptions(6, "[PAD]", Length: 4),
        };
        TokenEncoding e = tokenizer.Encode("hello world hello", addSpecialTokens: false);
        Assert.Equal([1, 2, 6, 6], e.Ids);
        Assert.Equal([1, 1, 0, 0], e.AttentionMask);
        Assert.Equal([false, false, true, true], e.SpecialTokensMask);
        Assert.Equal(["hello", "world", "[PAD]", "[PAD]"], e.Tokens);
        Assert.Equal(default, e.Offsets[3]);
        Assert.Equal([-1, -1, -1, -1], e.WordIds);

        tokenizer.Padding = new PaddingOptions(6, "[PAD]", Length: 3, Side: TokenizerSide.Left, PadTypeId: 7);
        e = tokenizer.Encode("hello", addSpecialTokens: false);
        Assert.Equal([6, 6, 1], e.Ids);
        Assert.Equal([0, 0, 1], e.AttentionMask);
        Assert.Equal([7, 7, 0], e.TypeIds);
        Assert.Equal(new TokenOffset(0, 5), e.Offsets[2]);

        // A fixed length never truncates; rounding goes up to the multiple.
        tokenizer.Truncation = null;
        tokenizer.Padding = new PaddingOptions(6, "[PAD]", Length: 1, PadToMultipleOf: 4);
        Assert.Equal(4, tokenizer.Encode("hello world hello", addSpecialTokens: false).Length);

        // The span interface does not pad or truncate.
        tokenizer.Truncation = new TruncationOptions(1);
        Span<int> ids = stackalloc int[8];
        tokenizer.EncodeToIds("hello world hello", ids, out int written, out _, addSpecialTokens: false);
        Assert.Equal(3, written);
    }

    [Fact]
    public void BatchPadsToTheLongest()
    {
        var tokenizer = new WordListTokenizer { Padding = new PaddingOptions(6, "[PAD]") };
        IReadOnlyList<TokenEncoding> batch = tokenizer.EncodeBatch(["hello", "hello world hello", ""], addSpecialTokens: false);
        Assert.Equal([3, 3, 3], batch.Select(e => e.Length));
        Assert.Equal([1, 0, 0], batch[0].AttentionMask);
        Assert.Equal([0, 0, 0], batch[2].AttentionMask);
        Assert.Empty(tokenizer.EncodeBatch([]));
    }

    [Fact]
    public void DecodeToStringAndBuffer()
    {
        Assert.Equal("hello world", _tokenizer.Decode([1, 2, 5]));
        Assert.Equal("hello world [S]", _tokenizer.Decode([1, 2, 5], skipSpecialTokens: false));

        Span<char> buffer = stackalloc char[11];
        Assert.Equal(OperationStatus.Done, _tokenizer.Decode([1, 2], buffer, out int consumed, out int chars));
        Assert.Equal((2, 11), (consumed, chars));
        Assert.Equal("hello world", buffer.ToString());
        Assert.Equal(OperationStatus.DestinationTooSmall, _tokenizer.Decode([1, 2], buffer[..5], out consumed, out chars));
        Assert.Equal((0, 0), (consumed, chars));
    }

    [Fact]
    public void PairEncodingIsNotSupportedUnlessTheFamilyDoesIt() =>
        Assert.Throws<NotSupportedException>(() => _tokenizer.Encode("a", "b"));

    [Fact]
    public void VocabularyLookups()
    {
        Assert.Equal(7, _tokenizer.VocabularySize);
        Assert.Equal(2, _tokenizer.TokenToId("world"));
        Assert.Null(_tokenizer.TokenToId("nope"));
        Assert.Null(_tokenizer.IdToToken(99));
        Assert.Null(_tokenizer.IdToToken(-1));
    }

    [Fact]
    public void EncodingRejectsMismatchedColumns() =>
        Assert.Throws<ArgumentException>(() => new TokenEncoding([1], [], [default], [false]));

    [Fact]
    public void FromFileAndStreamFailClearlyForNow()
    {
        Assert.Throws<NotSupportedException>(() => Tokenizer.FromFile("model.bin"));
        Assert.Throws<FileNotFoundException>(() => Tokenizer.FromFile(Path.Combine(Path.GetTempPath(), "onenlp-absent-" + Guid.NewGuid().ToString("N") + ".json")));
        string file = Path.Combine(Path.GetTempPath(), "onenlp-" + Guid.NewGuid().ToString("N") + "-vocab.txt");
        File.WriteAllText(file, "[UNK]\n");
        try
        {
            Assert.Throws<NotSupportedException>(() => Tokenizer.FromFile(file));
        }
        finally
        {
            File.Delete(file);
        }

        using var empty = new MemoryStream();
        foreach (TokenizerFormat format in Enum.GetValues<TokenizerFormat>().Where(f => f != TokenizerFormat.HuggingFaceJson))
        {
            Assert.Throws<NotSupportedException>(() => Tokenizer.FromStream(empty, format));
        }

        Assert.Throws<InvalidDataException>(() => Tokenizer.FromStream(empty, TokenizerFormat.HuggingFaceJson));

        Assert.Throws<ArgumentOutOfRangeException>(() => Tokenizer.FromStream(empty, (TokenizerFormat)99));
        Assert.Throws<ArgumentNullException>(() => Tokenizer.FromStream(null!, TokenizerFormat.Tiktoken));
    }
}
