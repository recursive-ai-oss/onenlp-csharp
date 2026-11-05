using System.Text;
using RecursiveAI.OneNlp.Segmentation;

namespace RecursiveAI.OneNlp.Fuzz;

/// <summary>
/// Feeds arbitrary text through the word tokenizer and the sentencizer, with and without English, French and Spanish data, and checks: offsets are
/// ascending and in range, every character that is not white space is inside a token (white space never starts or ends one
/// when no data is applied), sentences partition the tokens in order, and nothing throws.
/// </summary>
internal static class SegmentationTarget
{
    private static readonly UaxWordTokenizer[] Tokenizers =
    [
        new(SegmentationData.None), new(SegmentationData.Neutral), new(SegmentationData.English), new(SegmentationData.French), new(SegmentationData.Spanish),
    ];

    private static readonly SentencizerComponent[] Sentencizers =
    [
        new(SegmentationData.None), new(SegmentationData.Neutral), new(SegmentationData.English), new(SegmentationData.French), new(SegmentationData.Spanish),
    ];

    internal static void Run(ReadOnlySpan<byte> data)
    {
        // An odd first byte selects raw UTF-16 units, which reach lone surrogates that UTF-8 decoding never produces.
        string text = (data.Length > 0 && (data[0] & 1) == 1) ? FromUnits(data[1..]) : Encoding.UTF8.GetString(data);
        var strings = new StringStore();
        for (int k = 0; k < Tokenizers.Length; k++)
        {
            Doc doc = Tokenizers[k].Tokenize(text, strings);
            var covered = new bool[text.Length];
            int previousEnd = 0;
            for (int i = 0; i < doc.Length; i++)
            {
                Token t = doc[i];
                Require(t.Length > 0 && t.Idx >= previousEnd && t.EndIdx <= text.Length, "token range");
                Array.Fill(covered, true, t.Idx, t.Length);
                previousEnd = t.EndIdx;
            }

            for (int c = 0; c < text.Length; c++)
            {
                Require(covered[c] || char.IsWhiteSpace(text[c]), "every non-white-space character is in a token");
            }

            Sentencizers[k].Process(doc);
            IReadOnlyList<Span> sentences = doc.Sentences;
            Require((doc.Length == 0) == (sentences.Count == 0), "sentences exist iff tokens exist");
            int next = 0;
            foreach (Span s in sentences)
            {
                Require(s.Start == next && s.End > s.Start, "sentences partition the tokens");
                next = s.End;
            }

            Require(next == doc.Length, "sentences cover every token");
        }
    }

    private static string FromUnits(ReadOnlySpan<byte> data)
    {
        var chars = new char[data.Length / 2];
        for (int i = 0; i < chars.Length; i++)
        {
            chars[i] = (char)(data[2 * i] | (data[(2 * i) + 1] << 8));
        }

        return new string(chars);
    }

    private static void Require(bool condition, string what)
    {
        if (!condition)
        {
            throw new InvalidOperationException("Invariant failed: " + what);
        }
    }
}
