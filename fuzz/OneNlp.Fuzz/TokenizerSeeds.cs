using System.Text;

namespace RecursiveAI.OneNlp.Fuzz;

/// <summary>Small valid files for the model-reader targets, so mutations start from inputs that load.</summary>
internal static class TokenizerSeeds
{
    internal static void BpeModel(string dir)
    {
        File.WriteAllText(Path.Combine(dir, "bpe-small"), """
            {"type":"BPE","dropout":null,"unk_token":"[UNK]","continuing_subword_prefix":null,"end_of_word_suffix":null,"fuse_unk":false,"byte_fallback":false,
             "vocab":{"[UNK]":0,"a":1,"b":2,"c":3," ":4,"z":5,"ab":6,"abc":7},"merges":["a b","ab c"]}
            """);
        File.WriteAllText(Path.Combine(dir, "bpe-prefixed"), """
            {"type":"BPE","dropout":0.1,"unk_token":null,"continuing_subword_prefix":"##","end_of_word_suffix":"</w>","fuse_unk":true,"byte_fallback":true,
             "vocab":{"a":0,"##b":1,"##c":2,"a##b":3},"merges":[["a","##b"]]}
            """);
    }

    internal static void TiktokenRanks(string dir)
    {
        var sb = new StringBuilder();
        for (int b = 0; b < 256; b++)
        {
            sb.Append(Convert.ToBase64String([(byte)b])).Append(' ').Append(b).Append('\n');
        }

        sb.Append(Convert.ToBase64String("ab"u8)).Append(" 256\n").Append(Convert.ToBase64String("abc"u8)).Append(" 257\n");
        File.WriteAllText(Path.Combine(dir, "ranks-small"), sb.ToString());
    }

    internal static void VocabTxt(string dir)
    {
        File.WriteAllText(Path.Combine(dir, "vocab-small"), "[PAD]\n[UNK]\n[CLS]\n[SEP]\n[MASK]\na\nb\nab\n##b\n##c\nabc\nz\n,\n!\n");
    }
}
