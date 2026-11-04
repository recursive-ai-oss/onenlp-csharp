using RecursiveAI.OneNlp.Tokenizers;
using RecursiveAI.OneNlp.Tokenizers.Hf;
using RecursiveAI.OneNlp.Tokenizers.Loaders;
using RecursiveAI.OneNlp.Tokenizers.Unigram;

namespace RecursiveAI.OneNlp.Fuzz;

/// <summary>
/// Feeds arbitrary bytes to the <c>vocab.txt</c> reader and to the SentencePiece <c>.model</c> reader. When a file is accepted the
/// model built from it must cut sample text into tokens whose ranges lie inside the text, in order. Only the documented exceptions
/// (<see cref="InvalidDataException"/>, <see cref="NotSupportedException"/>) may escape.
/// </summary>
internal static class VocabularyReaderTargets
{
    private static readonly string[] Samples = ["hello", "h\u00e9llo w\u00f6rld", "\u65e5\u672c\u8a9e", "\U0001F600x", "\u2581a\u2581b", "<unk>", "unaffable"];

    internal static void RunVocabTxt(ReadOnlySpan<byte> data)
    {
        try
        {
            Tokenizer t = WordPieceVocabularyFile.Load(new MemoryStream(data.ToArray()));
            CheckEncoding(t);
        }
        catch (InvalidDataException)
        {
        }
        catch (NotSupportedException)
        {
        }
    }

    internal static void RunSentencePiece(ReadOnlySpan<byte> data)
    {
        byte[] bytes = data.ToArray();
        try
        {
            SentencePieceData parsed = SentencePieceProto.Read(bytes);
            if (parsed.ModelType == 1)
            {
                var scores = new double[parsed.Scores.Length];
                var matchable = new bool[parsed.Pieces.Length];
                for (int i = 0; i < scores.Length; i++)
                {
                    scores[i] = float.IsFinite(parsed.Scores[i]) ? parsed.Scores[i] : 0;
                    matchable[i] = parsed.Types[i] is SentencePieceData.Normal or SentencePieceData.UserDefined;
                }

                var model = new UnigramModel(parsed.Pieces, scores, parsed.UnkId, parsed.ByteFallback, matchable);
                foreach (string text in Samples.Concat(parsed.Pieces.Take(8)))
                {
                    var tokens = new List<ModelToken>();
                    model.Tokenize(text, tokens);
                    Check(tokens, text.Length);
                }
            }
        }
        catch (InvalidDataException)
        {
        }

        try
        {
            CheckEncoding(SentencePieceModelFile.Load(new MemoryStream(bytes)));
        }
        catch (InvalidDataException)
        {
        }
        catch (NotSupportedException)
        {
        }
    }

    private static void CheckEncoding(Tokenizer t)
    {
        foreach (string text in Samples)
        {
            TokenEncoding e = t.Encode(text);
            for (int i = 0; i < e.Length; i++)
            {
                Require(e.Offsets[i].Start >= 0 && e.Offsets[i].Start <= e.Offsets[i].End && e.Offsets[i].End <= text.Length, "offsets inside the text");
            }
        }
    }

    private static void Check(List<ModelToken> tokens, int length)
    {
        int previousStart = 0;
        foreach (ModelToken t in tokens)
        {
            Require(t.Start >= previousStart && t.Start <= t.End && t.End <= length, "token range inside the piece, in order");
            previousStart = t.Start;
        }
    }

    private static void Require(bool condition, string what)
    {
        if (!condition)
        {
            throw new InvalidOperationException("Invariant failed: " + what);
        }
    }
}
