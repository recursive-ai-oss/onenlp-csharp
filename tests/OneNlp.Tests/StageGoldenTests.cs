using System.IO.Compression;
using System.Text;
using System.Text.Json;
using RecursiveAI.OneNlp.Tokenizers;
using RecursiveAI.OneNlp.Tokenizers.Hf;
using RecursiveAI.OneNlp.Tokenizers.Loaders;

namespace RecursiveAI.OneNlp.Tests;

/// <summary>One token per code point, looked up in the model's vocabulary: the model of a BPE with no merges, which exposes every stage's offsets.</summary>
internal sealed class CharModel : ITokenizerModel
{
    private readonly Dictionary<string, int> _vocab = new(StringComparer.Ordinal);
    private readonly Dictionary<int, string> _byId = [];
    private readonly string _unk;

    internal CharModel(StageConfig c)
    {
        foreach (JsonProperty p in c.Element.GetProperty("vocab").EnumerateObject())
        {
            _vocab[p.Name] = p.Value.GetInt32();
            _byId[p.Value.GetInt32()] = p.Name;
        }

        _unk = c.OptionalString("unk_token") ?? "[UNK]";
    }

    public int VocabularySize => _vocab.Count;

    public void Tokenize(ReadOnlySpan<char> piece, List<ModelToken> output)
    {
        for (int i = 0; i < piece.Length;)
        {
            int w = char.IsHighSurrogate(piece[i]) && i + 1 < piece.Length && char.IsLowSurrogate(piece[i + 1]) ? 2 : 1;
            string s = piece.Slice(i, w).ToString();
            output.Add(_vocab.TryGetValue(s, out int id) ? new ModelToken(id, s, i, i + w) : new ModelToken(_vocab[_unk], _unk, i, i + w));
            i += w;
        }
    }

    public int? TokenToId(string token) => _vocab.TryGetValue(token, out int id) ? id : null;

    public string? IdToToken(int id) => _byId.GetValueOrDefault(id);
}

/// <summary>Compares every implemented stage with the reference outputs of the tokenizers package, recorded in testdata.</summary>
public class StageGoldenTests
{
    private static readonly Lazy<JsonDocument> Gold = new(() =>
    {
        string path = Path.Combine(AppContext.BaseDirectory, "testdata", "tokenizers", "stages", "goldens.json.gz");
        using var gz = new GZipStream(File.OpenRead(path), CompressionMode.Decompress);
        return JsonDocument.Parse(gz);
    });

    private static readonly StageRegistry Registry = MakeRegistry();

    private static StageRegistry MakeRegistry()
    {
        StageRegistry r = StageRegistry.Default.Clone();
        r.AddModel("BPE", c => new CharModel(c));
        return r;
    }

    private static string Short(string s) => s.Length > 160 ? s[..160] + "..." : s;

    private static string Show(string s) => Short(string.Concat(s.Select(c => c < 0x20 || c > 0x7e ? $"\\u{(int)c:x4}" : c.ToString())));

    private static void AssertNoFailures(string what, Dictionary<string, List<string>> failures)
    {
        if (failures.Count == 0)
        {
            return;
        }

        var sb = new StringBuilder();
        sb.Append(CultureInfoInvariant(failures.Values.Sum(v => v.Count))).Append(' ').Append(what).Append(" differ from the reference in ").Append(failures.Count).AppendLine(" specs:");
        foreach ((string spec, List<string> list) in failures.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            sb.Append("  ").Append(spec).Append(" (").Append(list.Count).AppendLine(")");
            foreach (string m in list.Take(2))
            {
                sb.Append("      ").AppendLine(m);
            }
        }

        Assert.Fail(sb.ToString());
    }

    private static string CultureInfoInvariant(int n) => n.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static void Fail(Dictionary<string, List<string>> failures, string spec, string message)
    {
        if (!failures.TryGetValue(spec, out List<string>? list))
        {
            failures[spec] = list = [];
        }

        list.Add(message);
    }

    [Fact]
    public void NormalizersMatchTheReference()
    {
        var failures = new Dictionary<string, List<string>>();
        foreach (JsonElement spec in Gold.Value.RootElement.GetProperty("normalizers").EnumerateArray())
        {
            string name = spec.GetProperty("name").GetString()!;
            INormalizer n = Registry.CreateNormalizer(new StageConfig(spec.GetProperty("config"), Registry));
            foreach (JsonElement c in spec.GetProperty("cases").EnumerateArray())
            {
                string input = c.GetProperty("in").GetString()!;
                string expected = c.GetProperty("out").GetString()!;
                var s = new NormalizedString(input);
                n.Normalize(s);
                if (!string.Equals(s.Text, expected, StringComparison.Ordinal))
                {
                    Fail(failures, name, $"'{Show(input)}' -> '{Show(s.Text)}', expected '{Show(expected)}'");
                }
            }
        }

        AssertNoFailures("normalizer outputs", failures);
    }

    [Fact]
    public void PreTokenizersMatchTheReference()
    {
        var failures = new Dictionary<string, List<string>>();
        foreach (JsonElement spec in Gold.Value.RootElement.GetProperty("pre_tokenizers").EnumerateArray())
        {
            string name = spec.GetProperty("name").GetString()!;
            IPreTokenizer p = Registry.CreatePreTokenizer(new StageConfig(spec.GetProperty("config"), Registry));
            foreach (JsonElement c in spec.GetProperty("cases").EnumerateArray())
            {
                string input = c.GetProperty("in").GetString()!;
                var pieces = new PreTokenizedString(new NormalizedString(input));
                p.PreTokenize(pieces);
                string actual = string.Join(" | ", pieces.Splits.Select(x => { OffsetPair o = x.ToOriginal(0, x.Length); return $"{Show(x.Text)}@{o.Start}-{o.End}"; }));
                string expected = string.Join(" | ", c.GetProperty("pieces").EnumerateArray().Select(x => $"{Show(x[0].GetString()!)}@{x[1].GetInt32()}-{x[2].GetInt32()}"));
                if (actual != expected)
                {
                    Fail(failures, name, $"'{Show(input)}': got [{actual}] expected [{expected}]");
                }
            }
        }

        AssertNoFailures("pre-tokenizations", failures);
    }

    [Fact]
    public void DecodersMatchTheReference()
    {
        var failures = new Dictionary<string, List<string>>();
        foreach (JsonElement spec in Gold.Value.RootElement.GetProperty("decoders").EnumerateArray())
        {
            string name = spec.GetProperty("name").GetString()!;
            ITokenDecoder d = Registry.CreateDecoder(new StageConfig(spec.GetProperty("config"), Registry));
            foreach (JsonElement c in spec.GetProperty("cases").EnumerateArray())
            {
                List<string> tokens = [.. c.GetProperty("tokens").EnumerateArray().Select(t => t.GetString()!)];
                string expected = c.GetProperty("out").GetString()!;
                string actual = string.Concat(d.DecodeChain(tokens));
                if (!string.Equals(actual, expected, StringComparison.Ordinal))
                {
                    Fail(failures, name, $"[{string.Join(",", tokens.Select(Show))}] -> '{Show(actual)}', expected '{Show(expected)}'");
                }
            }
        }

        AssertNoFailures("decodings", failures);
    }

    private static string[] Describe(int[] ids, string[] tokens, TokenOffset[] offsets, int[] types, int[] words, bool[] special, int[] attention) =>
    [
        $"ids={string.Join(",", ids)}",
        $"tokens={string.Join(",", tokens.Select(Show))}",
        $"off={string.Join(",", offsets.Select(o => $"{o.Start}-{o.End}"))}",
        $"type={string.Join("", types)}",
        $"word={string.Join(",", words)}",
        $"sp={string.Join("", special.Select(b => b ? 1 : 0))}",
        $"att={string.Join("", attention)}",
    ];

    [Fact]
    public void WholeTokenizersMatchTheReference()
    {
        var failures = new Dictionary<string, List<string>>();
        foreach (JsonElement spec in Gold.Value.RootElement.GetProperty("tokenizers").EnumerateArray())
        {
            string name = spec.GetProperty("name").GetString()!;
            Tokenizer t = HuggingFaceTokenizerJson.Load(new MemoryStream(Encoding.UTF8.GetBytes(spec.GetProperty("json").GetString()!)), Registry);
            bool padded = spec.GetProperty("batch_padding").GetBoolean();
            var batchResults = new Dictionary<(string, bool), TokenEncoding>();
            if (padded)
            {
                foreach (bool add in new[] { true, false })
                {
                    string[] texts = [.. spec.GetProperty("cases").EnumerateArray().Where(c => c.GetProperty("add_special").GetBoolean() == add).Select(c => c.GetProperty("text").GetString()!)];
                    IReadOnlyList<TokenEncoding> encs = t.EncodeBatch(texts, add);
                    for (int i = 0; i < texts.Length; i++)
                    {
                        batchResults[(texts[i], add)] = encs[i];
                    }
                }
            }

            foreach (JsonElement c in spec.GetProperty("cases").EnumerateArray())
            {
                string text = c.GetProperty("text").GetString()!;
                string? pair = c.GetProperty("pair").GetString();
                bool add = c.GetProperty("add_special").GetBoolean();
                TokenEncoding e;
                try
                {
                    e = padded ? batchResults[(text, add)] : pair is null ? t.Encode(text, add) : t.Encode(text, pair, add);
                }
                catch (Exception ex) when (ex is not Xunit.Sdk.XunitException)
                {
                    Fail(failures, name, $"'{Show(text)}'|'{Show(pair ?? "<none>")}' threw {ex.GetType().Name}: {ex.Message}");
                    continue;
                }

                string[] actual = Describe(e.Ids, e.Tokens, e.Offsets, e.TypeIds, e.WordIds, e.SpecialTokensMask, e.AttentionMask);
                string[] expected = Describe(
                    [.. c.GetProperty("ids").EnumerateArray().Select(x => x.GetInt32())],
                    [.. c.GetProperty("tokens").EnumerateArray().Select(x => x.GetString()!)],
                    [.. c.GetProperty("offsets").EnumerateArray().Select(x => new TokenOffset(x[0].GetInt32(), x[1].GetInt32()))],
                    [.. c.GetProperty("type_ids").EnumerateArray().Select(x => x.GetInt32())],
                    [.. c.GetProperty("word_ids").EnumerateArray().Select(x => x.GetInt32())],
                    [.. c.GetProperty("special").EnumerateArray().Select(x => x.GetInt32() == 1)],
                    [.. c.GetProperty("attention").EnumerateArray().Select(x => x.GetInt32())]);
                string diff = string.Concat(Enumerable.Range(0, actual.Length).Where(k => actual[k] != expected[k]).Select(k => $"\n          got      {Short(actual[k])}\n          expected {Short(expected[k])}"));
                if (diff.Length > 0)
                {
                    Fail(failures, name, $"'{Show(text)}'{(pair is null ? "" : $"|'{Show(pair)}'")} add={add}{diff}");
                }
            }

            foreach (JsonElement d in spec.GetProperty("decode").EnumerateArray())
            {
                int[] ids = [.. d.GetProperty("ids").EnumerateArray().Select(x => x.GetInt32())];
                bool skip = d.GetProperty("skip").GetBoolean();
                string expected = d.GetProperty("out").GetString()!;
                string actual = t.Decode(ids, skip);
                if (!string.Equals(actual, expected, StringComparison.Ordinal))
                {
                    Fail(failures, name + " (decode)", $"skip={skip} ids={string.Join(",", ids)} -> '{Show(actual)}', expected '{Show(expected)}'");
                }
            }
        }

        AssertNoFailures("encodings", failures);
    }
}
