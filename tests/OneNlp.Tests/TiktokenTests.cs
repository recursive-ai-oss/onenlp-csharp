using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RecursiveAI.OneNlp.Tokenizers;
using RecursiveAI.OneNlp.Tokenizers.Loaders;
using RecursiveAI.OneNlp.Tokenizers.Tiktoken;

namespace RecursiveAI.OneNlp.Tests;

public class TiktokenTests
{
    // Ranks: the 256 bytes at their own value, then "ab" 256, "bc" 257, "abc" 258, "aa" 259, "aab" 260, " a" 261.
    private static readonly string[] Merged = ["ab", "bc", "abc", "aa", "aab", " a"];

    private static MemoryStream RanksFile(bool crlf = false)
    {
        var sb = new StringBuilder();
        string nl = crlf ? "\r\n" : "\n";
        for (int b = 0; b < 256; b++)
        {
            sb.Append(Convert.ToBase64String([(byte)b])).Append(' ').Append(b).Append(nl);
        }

        for (int i = 0; i < Merged.Length; i++)
        {
            sb.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(Merged[i]))).Append(' ').Append(256 + i).Append(nl);
        }

        return new MemoryStream(Encoding.ASCII.GetBytes(sb.ToString()));
    }

    private static TiktokenTokenizer Toy() => TiktokenTokenizer.Create(
        RanksFile(),
        new Regex(@"\w+|\s+|[^\w\s]+", RegexOptions.CultureInvariant),
        new Dictionary<string, int> { ["<|end|>"] = 300 });

    [Fact]
    public void MergesByRankLeftmostFirst()
    {
        TiktokenTokenizer t = Toy();
        // "aabc": ab (256) joins first, then abc (258) beats aab (260).
        Assert.Equal([260], t.Encode("aab").Ids);
        Assert.Equal([258], t.Encode("abc").Ids);
        Assert.Equal([97, 258], t.Encode("aabc").Ids);
        Assert.Equal([259, 259], t.Encode("aaaa").Ids);
    }

    [Fact]
    public void SpecialTokensAreOnlyReadWhenAllowed()
    {
        TiktokenTokenizer t = Toy();
        TokenEncoding withSpecial = t.Encode("ab<|end|>ab", addSpecialTokens: true);
        Assert.Equal([256, 300, 256], withSpecial.Ids);
        Assert.Equal([false, true, false], withSpecial.SpecialTokensMask);
        Assert.Equal([new TokenOffset(0, 2), new TokenOffset(2, 9), new TokenOffset(9, 11)], withSpecial.Offsets);
        Assert.True(t.Encode("ab<|end|>ab", addSpecialTokens: false).Ids.Length > 3);
        Assert.Equal("ab<|end|>ab", t.Decode(withSpecial.Ids, skipSpecialTokens: false));
        Assert.Equal("abab", t.Decode(withSpecial.Ids, skipSpecialTokens: true));
    }

    [Fact]
    public void OffsetsCoverWholeCharactersWhenATokenCutsOne()
    {
        TiktokenTokenizer t = Toy();
        TokenEncoding e = t.Encode("a日b");
        // 日 is three bytes with no merged token: three byte tokens, each reporting the whole character.
        Assert.Equal(5, e.Length);
        Assert.Equal(new TokenOffset(0, 1), e.Offsets[0]);
        Assert.Equal(new TokenOffset(1, 2), e.Offsets[1]);
        Assert.Equal(new TokenOffset(1, 2), e.Offsets[3]);
        Assert.Equal(new TokenOffset(2, 3), e.Offsets[4]);
        Assert.Equal("a日b", t.Decode(e.Ids, false));
        Assert.Equal("<0xE6><0x97><0xA5>", t.IdToToken(e.Ids[1]) + t.IdToToken(e.Ids[2]) + t.IdToToken(e.Ids[3]));
        Assert.Equal(0xE6, t.TokenToId("<0xE6>"));
    }

    [Fact]
    public void HeapAndScanMergesAgree()
    {
        TiktokenRanks ranks = TiktokenRanksFile.Read(RanksFile());
        var random = new Random(11);
        for (int trial = 0; trial < 2000; trial++)
        {
            byte[] piece = [.. Enumerable.Range(0, random.Next(2, 90)).Select(_ => (byte)"abc "[random.Next(4)])];
            var scan = new List<int>();
            var heap = new List<int>();
            var scanLengths = new List<int>();
            var heapLengths = new List<int>();
            ranks.EncodePiece(piece, scan, scanLengths);
            ranks.EncodePieceByHeap(piece, heap, heapLengths);
            Assert.Equal(scan, heap);
            Assert.Equal(scanLengths, heapLengths);
            Assert.Equal(piece.Length, scanLengths.Sum());
            Assert.Equal(piece, scan.SelectMany(id => ranks.BytesOf(id)!).ToArray());
        }
    }

    [Fact]
    public void LongRunOfOneLetterStaysFast()
    {
        TiktokenTokenizer t = Toy();
        string text = new('a', 200_000);
        int[] ids = t.Encode(text).Ids;
        Assert.Equal(100_000, ids.Length);
        Assert.All(ids, id => Assert.Equal(259, id));
    }

    [Fact]
    public void CountingAndIdsMatchEncode()
    {
        TiktokenTokenizer t = Toy();
        const string Text = "aab abc <|end|> 日本 ab";
        TokenEncoding e = t.Encode(Text);
        Assert.Equal(e.Length, t.CountTokens(Text));
        var ids = new int[64];
        Assert.Equal(System.Buffers.OperationStatus.Done, t.EncodeToIds(Text, ids, out int written, out int consumed));
        Assert.Equal(e.Ids, ids[..written]);
        Assert.Equal(Text.Length, consumed);
        Assert.Equal(System.Buffers.OperationStatus.DestinationTooSmall, t.EncodeToIds(Text, ids.AsSpan(0, 3), out written, out _));
        Assert.Equal(3, written);
        Assert.Equal(e.Ids[..3], ids[..3]);
        t.Truncation = new TruncationOptions(2, TokenizerSide.Left);
        Assert.Equal(e.Ids[^2..], t.Encode(Text).Ids);
    }

    [Fact]
    public void FileTokenizerNeedsAKnownEncoding()
    {
        Assert.Throws<InvalidDataException>(() => TiktokenRanksFile.Load(RanksFile()));
        Assert.NotNull(TiktokenTokenizer.Create(RanksFile(), new Regex(@"\w+"), null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("IQ==")]
    [InlineData("IQ== x")]
    [InlineData("IQ== -1")]
    [InlineData("IQ== 1\n\nIg== 2")]
    [InlineData("!!!! 1")]
    [InlineData(" 1")]
    [InlineData("IQ== 99999999999")]
    [InlineData("IQ== 1\nIQ== 2")]
    [InlineData("IQ== 1\nIg== 1")]
    public void MalformedRanksAreRejected(string content)
    {
        using var s = new MemoryStream(Encoding.ASCII.GetBytes(content));
        Assert.Throws<InvalidDataException>(() => TiktokenRanksFile.Read(s));
    }

    [Fact]
    public void CrLfLinesAndMissingFinalNewlineAreAccepted()
    {
        TiktokenRanks a = TiktokenRanksFile.Read(RanksFile(crlf: true));
        Assert.Equal(256 + Merged.Length, a.Count);
    }

    [Fact]
    public void ASparseRankIsRejected()
    {
        var sb = new StringBuilder();
        for (int b = 0; b < 256; b++)
        {
            sb.Append(Convert.ToBase64String([(byte)b])).Append(' ').Append(b).Append('\n');
        }

        sb.Append(Convert.ToBase64String("ab"u8)).Append(" 90000000\n");
        using var s = new MemoryStream(Encoding.ASCII.GetBytes(sb.ToString()));
        Assert.Throws<InvalidDataException>(() => TiktokenRanksFile.Read(s));
    }

    [Fact]
    public void SpecialTokenCollidingWithARankIsRejected()
    {
        Assert.Throws<ArgumentException>(() => TiktokenTokenizer.Create(RanksFile(), new Regex(@"\w+"), new Dictionary<string, int> { ["<x>"] = 5 }));
    }

    // The Python library counts code points; offsets here count UTF-16 units.
    private static int CodePointIndex(string text, int utf16Index)
    {
        int n = utf16Index;
        for (int i = 0; i < utf16Index; i++)
        {
            if (char.IsLowSurrogate(text[i]) && i > 0 && char.IsHighSurrogate(text[i - 1]))
            {
                n--;
            }
        }

        return n;
    }

    private static string? Dir(string variable) => Environment.GetEnvironmentVariable(variable);

    /// <summary>Reference outputs from the released Python package (see tools/tokenizer-goldens); skipped when they are not around.</summary>
    [Theory]
    [InlineData("cl100k_base", TiktokenEncoding.Cl100kBase)]
    [InlineData("o200k_base", TiktokenEncoding.O200kBase)]
    [InlineData("r50k_base", TiktokenEncoding.R50kBase)]
    [InlineData("p50k_base", TiktokenEncoding.P50kBase)]
    public void MatchesReferenceEncodings(string name, TiktokenEncoding encoding)
    {
        string? goldens = Dir("ONENLP_BPE_GOLDENS");
        string? models = Dir("ONENLP_TOKENIZER_MODELS");
        if (goldens is null || models is null)
        {
            Assert.Skip("ONENLP_BPE_GOLDENS and ONENLP_TOKENIZER_MODELS are not set.");
        }

        TiktokenTokenizer t = TiktokenTokenizer.Load(Path.Combine(models, "tiktoken", name + ".tiktoken"), encoding);
        int records = 0;
        int tokens = 0;
        var mismatches = new List<string>();
        foreach (string line in File.ReadLines(Path.Combine(goldens, "tiktoken-" + name + ".jsonl")))
        {
            using JsonDocument rec = JsonDocument.Parse(line);
            JsonElement r = rec.RootElement;
            if (r.TryGetProperty("err", out _))
            {
                continue;
            }

            string text = r.GetProperty("s").GetString()!;
            int[] all = [.. r.GetProperty("all").EnumerateArray().Select(x => x.GetInt32())];
            int[] ord = [.. r.GetProperty("ord").EnumerateArray().Select(x => x.GetInt32())];
            int[] startOffsets = [.. r.GetProperty("off").EnumerateArray().Select(x => x.GetInt32())];
            TokenEncoding e = t.Encode(text, addSpecialTokens: true);
            TokenEncoding o = t.Encode(text, addSpecialTokens: false);
            records++;
            tokens += all.Length;
            string? problem = null;
            if (!e.Ids.AsSpan().SequenceEqual(all))
            {
                int d = 0;
                while (d < Math.Min(e.Ids.Length, all.Length) && e.Ids[d] == all[d])
                {
                    d++;
                }

                problem = $"encode ids differ at token {d}: expected {string.Join(',', all.Skip(d).Take(4))} got {string.Join(',', e.Ids.Skip(d).Take(4))} offsets {string.Join(',', e.Offsets.Skip(d).Take(4))}";
            }
            else if (!o.Ids.AsSpan().SequenceEqual(ord))
            {
                problem = "encode_ordinary ids";
            }
            else if (t.Decode(all, skipSpecialTokens: false) != r.GetProperty("dec").GetString())
            {
                problem = "decode";
            }
            else if (Convert.ToHexString(t.DecodeBytes(all)).Equals(r.GetProperty("decb").GetString(), StringComparison.OrdinalIgnoreCase) is false)
            {
                problem = "decode_bytes";
            }
            else if (!e.Offsets.Select(x => CodePointIndex(text, x.Start)).SequenceEqual(startOffsets))
            {
                problem = "start offsets";
            }

            if (problem is not null && mismatches.Count < 10)
            {
                mismatches.Add($"{problem} for '{(text.Length > 40 ? text[..40] : text).ReplaceLineEndings("\\n")}'");
            }
        }

        Assert.True(mismatches.Count == 0, $"{records} records, {tokens} tokens; mismatches: {string.Join(" | ", mismatches)}");
        Assert.True(records > 500);

        // The same file loaded through the generic entry point is the same tokenizer.
        Tokenizer viaFile = Tokenizer.FromFile(Path.Combine(models, "tiktoken", name + ".tiktoken"));
        Assert.Equal(t.VocabularySize, viaFile.VocabularySize);
    }

    [Theory]
    [InlineData("cl100k_base", TiktokenEncoding.Cl100kBase)]
    [InlineData("o200k_base", TiktokenEncoding.O200kBase)]
    [InlineData("r50k_base", TiktokenEncoding.R50kBase)]
    public void SplitPatternAgreesOnEveryCodePoint(string name, TiktokenEncoding encoding)
    {
        string? goldens = Dir("ONENLP_BPE_GOLDENS");
        string? models = Dir("ONENLP_TOKENIZER_MODELS");
        string? sweep = goldens is null ? null : Path.Combine(goldens, "sweep-" + name + ".txt");
        if (models is null || sweep is null || !File.Exists(sweep))
        {
            Assert.Skip("The code point sweep is not available.");
        }

        TiktokenTokenizer t = TiktokenTokenizer.Load(Path.Combine(models, "tiktoken", name + ".tiktoken"), encoding);
        int checkedCount = 0;
        var mismatches = new List<string>();
        foreach (string line in File.ReadLines(sweep))
        {
            string[] parts = line.Split(' ');
            int cp = Convert.ToInt32(parts[0], 16);
            string c = char.ConvertFromUtf32(cp);
            string a = "a" + c + "b";
            string b = c + "\n";
            int[] ea = parts[1].Length == 0 ? [] : [.. parts[1].Split(',').Select(int.Parse)];
            int[] eb = parts[2].Length == 0 ? [] : [.. parts[2].Split(',').Select(int.Parse)];
            checkedCount++;
            if ((!t.Encode(a, false).Ids.AsSpan().SequenceEqual(ea) || !t.Encode(b, false).Ids.AsSpan().SequenceEqual(eb)) && mismatches.Count < 20)
            {
                mismatches.Add($"U+{cp:X4}");
            }
        }

        Assert.True(mismatches.Count == 0, $"{checkedCount} code points; mismatches: {string.Join(' ', mismatches)}");
    }
}
