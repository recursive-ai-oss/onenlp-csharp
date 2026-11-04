using System.Text;
using RecursiveAI.OneNlp.Tokenizers;
using RecursiveAI.OneNlp.Tokenizers.Hf;
using RecursiveAI.OneNlp.Tokenizers.Loaders;

namespace RecursiveAI.OneNlp.AotSmoke;

/// <summary>Runs a small tokenizer.json through the loader and every kind of stage under NativeAOT, with one token per character standing in for the model.</summary>
internal static class StageSmoke
{
    private const string Json = """
        {"version":"1.0","truncation":{"max_length":12,"strategy":"LongestFirst","direction":"Right","stride":0},"padding":null,
         "added_tokens":[{"id":3,"content":"<s>","single_word":false,"lstrip":false,"rstrip":false,"normalized":false,"special":true},
                         {"id":4,"content":"</s>","single_word":false,"lstrip":false,"rstrip":false,"normalized":false,"special":true}],
         "normalizer":{"type":"Sequence","normalizers":[{"type":"NFD"},{"type":"StripAccents"},{"type":"Lowercase"},{"type":"Replace","pattern":{"Regex":"\\s+"},"content":" "}]},
         "pre_tokenizer":{"type":"Sequence","pretokenizers":[{"type":"Split","pattern":{"Regex":"'s|'t| ?\\p{L}+| ?\\p{N}+| ?[^\\s\\p{L}\\p{N}]+|\\s+"},"behavior":"Isolated","invert":false},{"type":"Digits","individual_digits":true}]},
         "post_processor":{"type":"RobertaProcessing","sep":["</s>",4],"cls":["<s>",3],"trim_offsets":true,"add_prefix_space":false},
         "decoder":{"type":"Fuse"},
         "model":{"type":"AotSmokeModel"}}
        """;

    internal static List<string> Run()
    {
        var failed = new List<string>();
        StageRegistry registry = StageRegistry.Default.Clone();
        registry.AddModel("AotSmokeModel", _ => new CharModel());
        Tokenizer t = HuggingFaceTokenizerJson.Load(new MemoryStream(Encoding.UTF8.GetBytes(Json)), registry);

        TokenEncoding e = t.Encode("H\u00e9llo  W\u00d6rld 42");
        if (!e.Tokens.SequenceEqual(["<s>", "h", "e", "l", "l", "o", " ", "w", "o", "r", "l", "</s>"]))
        {
            failed.Add("tokenizer.json tokens: " + string.Join(",", e.Tokens));
        }

        if (e.Offsets[2].Start != 1 || e.Offsets[2].End != 2)
        {
            failed.Add("tokenizer.json offsets");
        }

        if (e.Ids.Length != 12 || e.SpecialTokensMask[0] != true)
        {
            failed.Add("tokenizer.json truncation and special tokens");
        }

        if (CodePointRegex.Parse(@"\p{Han}+|\w+").Matches("\u65e5\u672c x").Count != 2)
        {
            failed.Add("code point regex");
        }

        var s = new NormalizedString("e\u0301 \r\n");
        new PrecompiledNormalizer(Convert.ToBase64String(new byte[4])).Normalize(s);
        if (s.Text.Length == 0)
        {
            failed.Add("precompiled normalizer");
        }

        return failed;
    }

    private sealed class CharModel : ITokenizerModel
    {
        public int VocabularySize => 1000;

        public void Tokenize(ReadOnlySpan<char> piece, List<ModelToken> output)
        {
            for (int i = 0; i < piece.Length; i++)
            {
                output.Add(new ModelToken(piece[i] % 1000, piece[i].ToString(), i, i + 1));
            }
        }

        public int? TokenToId(string token) => token.Length == 1 ? token[0] % 1000 : null;

        public string? IdToToken(int id) => id is >= 0 and < 1000 ? ((char)id).ToString() : null;
    }
}
