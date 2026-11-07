using System.Buffers;
using RecursiveAI.OneNlp;
using RecursiveAI.OneNlp.Tokenizers;

namespace RecursiveAI.OneNlp.Samples;

// Tokenizers: the BCL span style (OperationStatus, caller buffers). Shown next to Hugging Face tokenizers in docs/reads-like.md.
// Runs on any Hugging Face tokenizer.json; the checks hold for a BERT-style WordPiece file that adds [CLS] and [SEP].
internal static class TokenizerSample
{
    internal static void Run(string tokenizerJson)
    {
        Console.WriteLine("tokenizer " + Path.GetFileName(Path.GetDirectoryName(tokenizerJson)));

        // <sample>
        Tokenizer tok = Tokenizer.FromFile(tokenizerJson);                  // tokenizer.json, SentencePiece .model, vocab.txt, .tiktoken

        // Ids, tokens and character offsets into the input, as the Hugging Face Encoding has them.
        TokenEncoding enc = tok.Encode("Hello, world! Tokenizers count in UTF-16 units.");
        for (int i = 0; i < enc.Length; i++)
        {
            Console.WriteLine($"{enc.Ids[i],6} {enc.Tokens[i],-12} {enc.Offsets[i].Start}..{enc.Offsets[i].End}");
        }

        // The span form: ids go into your buffer, nothing is allocated, and the status says whether it all fit.
        int[] buffer = new int[8];
        OperationStatus status = tok.EncodeToIds("Hello, world! Tokenizers count in UTF-16 units.", buffer, out int written, out int consumed);

        int count = tok.CountTokens("How long is this text, in tokens?");
        string back = tok.Decode(enc.Ids);                                  // special tokens skipped
        ReadOnlySpan<char> budget = tok.TrimToTokenBudget("one two three four five six seven eight", 4);
        // </sample>

        Program.Check("offsets slice the input", enc.Offsets.Where((o, i) => !enc.SpecialTokensMask[i]).All(o => o.End > o.Start));
        Program.Check("special tokens carry the (0, 0) offsets", enc.Offsets.Where((o, i) => enc.SpecialTokensMask[i]).All(o => o.Start == 0 && o.End == 0));
        Program.Check("the short buffer is reported, with an exact prefix", status == OperationStatus.DestinationTooSmall && written == 8 && consumed > 0
            && buffer.AsSpan().SequenceEqual(enc.Ids.AsSpan(0, 8)));
        Program.Check("count agrees with encode", count == tok.Encode("How long is this text, in tokens?").Length);
        Program.Check("decode", back.Length > 0);
        Program.Check("the budget is a prefix of the text", budget.Length > 0 && "one two three four five six seven eight".StartsWith(budget.ToString(), StringComparison.Ordinal));
    }
}
