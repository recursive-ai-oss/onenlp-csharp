using RecursiveAI.OneNlp.Tokenizers;

namespace RecursiveAI.OneNlp.Parity;

/// <summary>Loads the tokenizer file of a family. A <c>.tiktoken</c> file holds ranks only, so it is loaded through the named encoding it belongs to.</summary>
public static class TokenizerFiles
{
    /// <summary>Loads a tokenizer from a file.</summary>
    /// <param name="path">A <c>tokenizer.json</c>, <c>vocab.txt</c>, SentencePiece <c>.model</c>, or <c>.tiktoken</c> file named after its encoding (for example <c>cl100k_base.tiktoken</c>).</param>
    public static Tokenizer Load(string path)
    {
        if (!path.EndsWith(".tiktoken", StringComparison.Ordinal))
        {
            return Tokenizer.FromFile(path);
        }

        return Path.GetFileNameWithoutExtension(path) switch
        {
            "cl100k_base" => TiktokenTokenizer.Cl100kBase(path),
            "o200k_base" => TiktokenTokenizer.O200kBase(path),
            "r50k_base" => TiktokenTokenizer.R50kBase(path),
            "p50k_base" => TiktokenTokenizer.P50kBase(path),
            string other => throw new NotSupportedException($"No named Tiktoken encoding '{other}'."),
        };
    }
}
