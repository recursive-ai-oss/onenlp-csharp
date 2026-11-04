using System.IO.Compression;
using System.Text;
using System.Text.Json;
using RecursiveAI.OneNlp.Tokenizers;
using RecursiveAI.OneNlp.Tokenizers.Hf;
using RecursiveAI.OneNlp.Tokenizers.Loaders;

namespace RecursiveAI.OneNlp.Fuzz;

/// <summary>One token per code point with an ID derived from it: a model that lets every stage in front of it be exercised.</summary>
internal sealed class FuzzModel : ITokenizerModel
{
    public int VocabularySize => 1000;

    public void Tokenize(ReadOnlySpan<char> piece, List<ModelToken> output)
    {
        for (int i = 0; i < piece.Length;)
        {
            int w = char.IsHighSurrogate(piece[i]) && i + 1 < piece.Length && char.IsLowSurrogate(piece[i + 1]) ? 2 : 1;
            output.Add(new ModelToken(piece[i] % 1000, piece.Slice(i, w).ToString(), i, i + w));
            i += w;
        }
    }

    public int? TokenToId(string token) => token.Length == 1 ? token[0] % 1000 : null;

    public string? IdToToken(int id) => id is >= 0 and < 1000 ? ((char)id).ToString() : null;
}

/// <summary>A broken invariant: a finding, never one of the documented exceptions.</summary>
internal sealed class FuzzInvariantException(string message) : Exception(message);

internal static class Fuzz
{
    internal static void Require(bool condition, string what)
    {
        if (!condition)
        {
            throw new FuzzInvariantException("Invariant failed: " + what);
        }
    }
}

/// <summary>
/// Feeds arbitrary bytes to the tokenizer.json loader with a toy model standing in for the subword model, then encodes and decodes
/// short texts when loading works. Only the documented exceptions may escape: a bad file is InvalidDataException or NotSupportedException,
/// and a split pattern that costs too much on a text is InvalidOperationException. Encodings must keep their invariants.
/// </summary>
internal static class TokenizerJsonTarget
{
    private static readonly StageRegistry Registry = MakeRegistry();

    // Set ONENLP_FUZZ_TRACE=1 when replaying an input to see why a file was refused.
    private static readonly bool Trace = Environment.GetEnvironmentVariable("ONENLP_FUZZ_TRACE") == "1";

    private static readonly string[] Texts =
    [
        "Hello, world! It's 12 o'clock.",
        "  leading\tand\r\ntrailing  ",
        "na\u00efve caf\u00e9 e\u0301 \U0001F600\u200d\U0001F600 \u65e5\u672c\u8a9e [CLS] <s> \ud800",
        "",
    ];

    private static StageRegistry MakeRegistry()
    {
        StageRegistry r = StageRegistry.Default.Clone();
        r.AddModel("BPE", _ => new FuzzModel());
        r.AddModel("WordPiece", _ => new FuzzModel());
        r.AddModel("Unigram", _ => new FuzzModel());
        r.AddModel("WordLevel", _ => new FuzzModel());
        return r;
    }

    /// <summary>The loader with the toy model standing in for every model type.</summary>
    internal static void Run(ReadOnlySpan<byte> data) => Run(data, Registry);

    /// <summary>The loader with the real models (WordPiece, BPE, Unigram, WordLevel) behind the stages.</summary>
    internal static void RunWithModels(ReadOnlySpan<byte> data) => Run(data, StageRegistry.Default);

    private static void Run(ReadOnlySpan<byte> data, StageRegistry registry)
    {
        Tokenizer tokenizer;
        try
        {
            tokenizer = HuggingFaceTokenizerJson.Load(new MemoryStream(data.ToArray()), registry);
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException)
        {
            if (Trace)
            {
                Console.Error.WriteLine(ex.Message);
            }

            return;
        }

        try
        {
            foreach (string text in Texts)
            {
                TokenEncoding e = tokenizer.Encode(text);
                Fuzz.Require(
                    e.Ids.Length == e.Tokens.Length && e.Ids.Length == e.Offsets.Length && e.Ids.Length == e.TypeIds.Length
                    && e.Ids.Length == e.AttentionMask.Length && e.Ids.Length == e.SpecialTokensMask.Length && e.Ids.Length == e.WordIds.Length,
                    "parallel arrays");
                for (int i = 0; i < e.Ids.Length; i++)
                {
                    TokenOffset o = e.Offsets[i];
                    Fuzz.Require(o.Start >= 0 && o.Start <= o.End && o.End <= text.Length, "offset range");
                }

                tokenizer.Decode(e.Ids);
                tokenizer.Encode(text, "pair " + text);
            }
        }
        catch (InvalidOperationException)
        {
            // A hostile split pattern ran out of its step budget.
        }
    }

    /// <summary>Writes the tokenizer.json documents of the golden file, and each stage config on its own, as seeds.</summary>
    internal static void Seeds(string directory, string testdata)
    {
        string path = Path.Combine(testdata, "tokenizers", "stages", "goldens.json.gz");
        using var gz = new GZipStream(File.OpenRead(path), CompressionMode.Decompress);
        using JsonDocument doc = JsonDocument.Parse(gz);
        int n = 0;
        foreach (JsonElement spec in doc.RootElement.GetProperty("tokenizers").EnumerateArray())
        {
            File.WriteAllText(Path.Combine(directory, $"seed-{n++:D2}"), spec.GetProperty("json").GetString()!, new UTF8Encoding(false));
        }

        foreach ((string list, string slot) in new[] { ("pre_tokenizers", "pre_tokenizer"), ("normalizers", "normalizer") })
        {
            foreach (JsonElement spec in doc.RootElement.GetProperty(list).EnumerateArray())
            {
                string json = "{\"model\":{\"type\":\"BPE\",\"vocab\":{},\"merges\":[]},\"" + slot + "\":" + spec.GetProperty("config").GetRawText() + "}";
                File.WriteAllText(Path.Combine(directory, $"seed-{n++:D2}"), json, new UTF8Encoding(false));
            }
        }
    }
}

/// <summary>
/// The code point regex engine on its own: the bytes are a pattern, a NUL, then a text (both UTF-8). A bad pattern is InvalidDataException,
/// a pattern that costs too much is InvalidOperationException; matches must be ordered, non-empty ranges inside the text.
/// </summary>
internal static class CodePointRegexTarget
{
    internal static void Run(ReadOnlySpan<byte> data)
    {
        int split = data.IndexOf((byte)0);
        ReadOnlySpan<byte> patternBytes = split < 0 ? data : data[..split];
        string text = split < 0 ? string.Empty : Encoding.UTF8.GetString(data[(split + 1)..]);
        CodePointRegex re;
        try
        {
            re = CodePointRegex.Parse(Encoding.UTF8.GetString(patternBytes), rustWordClass: (data.Length & 1) == 1);
        }
        catch (InvalidDataException)
        {
            return;
        }

        try
        {
            int previous = 0;
            foreach (OffsetPair m in re.Matches(text))
            {
                Fuzz.Require(m.Start >= previous && m.End > m.Start && m.End <= text.Length, "match range");
                previous = m.End;
            }
        }
        catch (InvalidOperationException)
        {
            // Step budget or depth bound.
        }
    }

    internal static void Seeds(string directory)
    {
        string[] seeds =
        [
            @"'s|'t|'re|'ve|'m|'ll|'d| ?\p{L}+| ?\p{N}+| ?[^\s\p{L}\p{N}]+|\s+(?!\S)|\s+" + "\0Hello, world! 123  \U0001F600",
            @"(?i:'s|'t|'re|'ve|'m|'ll|'d)|[^\r\n\p{L}\p{N}]?\p{L}+|\p{N}{1,3}| ?[^\s\p{L}\p{N}]+[\r\n]*|\s*[\r\n]+|\s+(?!\S)|\s+" + "\0It's 2024\n\n ok",
            @"(a+)+b" + "\0aaaaaaaaaaaaaaaaaaaaaaaa",
            @"\w+(?=\.)|(?>x+)y|[\p{Han}\p{Hiragana}]+|\bfoo\B" + "\0foo. xxy \u65e5\u672c",
        ];
        for (int i = 0; i < seeds.Length; i++)
        {
            File.WriteAllText(Path.Combine(directory, $"seed-{i}"), seeds[i], new UTF8Encoding(false));
        }
    }
}

/// <summary>The SentencePiece character map normalizer fed arbitrary bytes as its data, then asked to normalize short texts.</summary>
internal static class CharsMapTarget
{
    internal static void Run(ReadOnlySpan<byte> data)
    {
        try
        {
            var n = new PrecompiledNormalizer(Convert.ToBase64String(data));
            foreach (string text in (string[])["Hello \r\n e\u0301 \U0001F600 \ud800 x", "\u1100\u1161\u11a8 \ufb01"])
            {
                var s = new NormalizedString(text);
                n.Normalize(s);
            }
        }
        catch (InvalidDataException)
        {
            // A malformed character map.
        }
    }
}
