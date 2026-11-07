using System.Globalization;
using System.Text;

namespace RecursiveAI.OneNlp;

/// <summary>
/// A simple rule-based word tokenizer, kept for pipeline files that name it and for code that wants fixed, data-free rules. It is not
/// the default anywhere: <see cref="Segmentation.UaxWordTokenizer"/> (Unicode word boundaries, UAX #29, plus Universal Dependencies
/// adjustments) is. Every character belongs to exactly one of these rules:
/// whitespace separates tokens and belongs to none; a run of letters, digits and combining marks is one token, and may contain an
/// apostrophe or hyphen between two such characters (<c>don't</c>, <c>well-known</c>) or a period or comma between two digits
/// (<c>3.14</c>, <c>1,000</c>); any other character (punctuation, symbols, emoji) is a token by itself.
/// </summary>
/// <remarks>It is deliberately unambitious: no abbreviations, URLs, contractions or CJK word segmentation. Safe for concurrent use.</remarks>
public sealed class RuleWordTokenizer : IWordTokenizer
{
    /// <inheritdoc/>
    public Doc Tokenize(string text, StringStore strings)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(strings);

        var tokens = new List<TokenBoundary>();
        int i = 0;
        while (i < text.Length)
        {
            Rune rune = Next(text, i, out int width);
            if (Rune.IsWhiteSpace(rune))
            {
                i += width;
            }
            else if (IsWordPart(rune))
            {
                int end = i + width;
                while (end < text.Length)
                {
                    Rune next = Next(text, end, out int nextWidth);
                    if (IsWordPart(next))
                    {
                        end += nextWidth;
                    }
                    else if (IsJoiner(next, text, end, nextWidth, Previous(text, end)))
                    {
                        end += nextWidth;
                    }
                    else
                    {
                        break;
                    }
                }

                tokens.Add(new TokenBoundary(i, end - i));
                i = end;
            }
            else
            {
                tokens.Add(new TokenBoundary(i, width));
                i += width;
            }
        }

        return new Doc(strings, text, tokens.ToArray());
    }

    private static Rune Next(string text, int index, out int width)
    {
        // An unpaired surrogate decodes to U+FFFD with width 1, so it becomes a one-character token and the loop always advances.
        Rune.DecodeFromUtf16(text.AsSpan(index), out Rune rune, out width);
        return rune;
    }

    private static Rune Previous(string text, int end)
    {
        Rune.DecodeLastFromUtf16(text.AsSpan(0, end), out Rune rune, out _);
        return rune;
    }

    private static bool IsWordPart(Rune rune)
    {
        if (Rune.IsLetterOrDigit(rune))
        {
            return true;
        }

        UnicodeCategory c = Rune.GetUnicodeCategory(rune);
        return c is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;
    }

    // A joiner stays inside a token only when a word character follows it and a suitable one came before it.
    private static bool IsJoiner(Rune rune, string text, int index, int width, Rune before)
    {
        int after = index + width;
        if (after >= text.Length)
        {
            return false;
        }

        Rune following = Next(text, after, out _);
        if (rune.Value is '\'' or 0x2019 or '-')
        {
            return Rune.IsLetter(before) && Rune.IsLetter(following);
        }

        if (rune.Value is '.' or ',')
        {
            return Rune.IsDigit(before) && Rune.IsDigit(following);
        }

        return false;
    }
}
