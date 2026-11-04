using System.Text;
using RecursiveAI.OneNlp;

namespace RecursiveAI.OneNlp.Fuzz;

/// <summary>
/// Feeds arbitrary bytes, read as UTF-8 with invalid sequences replaced, through the word tokenizer and checks the document it builds:
/// every token is a non-empty slice of the text, in order, with only whitespace between tokens, and character spans round-trip.
/// Nothing may throw.
/// </summary>
internal static class WordTokenizerTarget
{
    internal static void Run(ReadOnlySpan<byte> data)
    {
        // Raw UTF-16 units from pairs of bytes reach lone surrogates, which UTF-8 decoding never produces.
        string text = (data.Length > 0 && (data[0] & 1) == 1) ? FromUnits(data[1..]) : Encoding.UTF8.GetString(data);
        Doc doc = Nlp.Analyze(text);

        int previousEnd = 0;
        for (int i = 0; i < doc.Length; i++)
        {
            Token t = doc[i];
            Require(t.Length > 0 && t.Idx >= previousEnd && t.EndIdx <= text.Length, "token range");
            Require(t.Text.SequenceEqual(text.AsSpan(t.Idx, t.Length)), "token text");
            for (int c = previousEnd; c < t.Idx; c++)
            {
                Require(char.IsWhiteSpace(text[c]), "gap holds only whitespace");
            }

            Require(doc.FindToken(t.Idx) == i, "FindToken");
            Require(doc.TryGetCharSpan(t.Idx, t.EndIdx, out Span s) && s.StartChar == t.Idx && s.EndChar == t.EndIdx, "char span");
            previousEnd = t.EndIdx;
        }

        for (int c = previousEnd; c < text.Length; c++)
        {
            Require(char.IsWhiteSpace(text[c]), "tail holds only whitespace");
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
