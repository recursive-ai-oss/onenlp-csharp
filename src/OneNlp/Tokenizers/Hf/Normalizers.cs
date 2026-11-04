using System.Globalization;
using System.Text;

namespace RecursiveAI.OneNlp.Tokenizers.Hf;

internal sealed partial class StageRegistry
{
    static partial void RegisterNormalizers(StageRegistry registry)
    {
        registry.AddNormalizer("BertNormalizer", c => new BertNormalizer(
            c.Bool("clean_text", true),
            c.Bool("handle_chinese_chars", true),
            c.Has("strip_accents") ? c.Bool("strip_accents", false) : null,
            c.Bool("lowercase", true)));
        registry.AddNormalizer("NFC", _ => new UnicodeFormNormalizer(compatibility: false, compose: true));
        registry.AddNormalizer("NFD", _ => new UnicodeFormNormalizer(compatibility: false, compose: false));
        registry.AddNormalizer("NFKC", _ => new UnicodeFormNormalizer(compatibility: true, compose: true));
        registry.AddNormalizer("NFKD", _ => new UnicodeFormNormalizer(compatibility: true, compose: false));
        registry.AddNormalizer("Lowercase", _ => new LowercaseNormalizer());
        registry.AddNormalizer("Strip", c => new StripNormalizer(c.Bool("strip_left", true), c.Bool("strip_right", true)));
        registry.AddNormalizer("StripAccents", _ => new StripAccentsNormalizer());
        registry.AddNormalizer("Replace", c => new ReplaceNormalizer(c.Pattern("pattern", out bool isRegex), isRegex, c.String("content")));
        registry.AddNormalizer("Prepend", c => new PrependNormalizer(c.String("prepend")));
        registry.AddNormalizer("Precompiled", c => new PrecompiledNormalizer(c.String("precompiled_charsmap")));
        registry.AddNormalizer("ByteLevel", _ => new ByteLevelNormalizer());
        registry.AddNormalizer("Sequence", c => new SequenceNormalizer([.. c.Objects("normalizers").Select(registry.CreateNormalizer)]));
    }
}

/// <summary>Applies normalizers one after the other.</summary>
internal sealed class SequenceNormalizer(INormalizer[] steps) : INormalizer
{
    public void Normalize(NormalizedString text)
    {
        foreach (INormalizer step in steps)
        {
            step.Normalize(text);
        }
    }
}

/// <summary>Shared helpers for normalizers that rewrite one code point at a time.</summary>
internal static class NormalizerHelpers
{
    /// <summary>Gets the code point at an index of a string, treating an unpaired surrogate as itself.</summary>
    internal static int CodePointAt(string text, int index, out int width)
    {
        char c = text[index];
        if (char.IsHighSurrogate(c) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
        {
            width = 2;
            return char.ConvertToUtf32(c, text[index + 1]);
        }

        width = 1;
        return c;
    }

    /// <summary>Appends a code point to a builder, every unit with the given origin.</summary>
    internal static void AppendCodePoint(NormalizedStringBuilder builder, int cp, OffsetPair origin)
    {
        if (cp < 0x10000)
        {
            builder.Append((char)cp, origin);
        }
        else
        {
            builder.Append(char.ConvertFromUtf32(cp), origin);
        }
    }

    /// <summary>The origin of the code point at an index: its units' joint range.</summary>
    internal static OffsetPair OriginOf(NormalizedString text, int index, int width) => text.ToOriginal(index, index + width);

    /// <summary>Whether a code point is a nonspacing mark.</summary>
    internal static bool IsNonSpacingMark(int cp) => CodePointRegex.CategoryOf(cp) == UnicodeCategory.NonSpacingMark;

    /// <summary>Whether a code point is any kind of mark.</summary>
    internal static bool IsMark(int cp) => CodePointRegex.CategoryOf(cp) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;

    /// <summary>Lowercases one code point with the full (possibly multi-character) mapping Unicode defines for it, without context.</summary>
    internal static string Lower(int cp)
    {
        // U+0130 is the one code point whose full lowercase mapping (SpecialCasing.txt) differs from the simple one the BCL applies.
        if (cp == 0x130)
        {
            return "i\u0307";
        }

        if (!Rune.IsValid(cp))
        {
            return ((char)cp).ToString();
        }

        return Rune.ToLowerInvariant(new Rune(cp)).ToString();
    }
}

/// <summary>
/// The BERT text cleaning of the original BERT code: drop control characters, turn whitespace into spaces, put spaces around CJK
/// ideographs, optionally remove accents, and lowercase.
/// </summary>
internal sealed class BertNormalizer(bool cleanText, bool handleChineseChars, bool? stripAccents, bool lowercase) : INormalizer
{
    public void Normalize(NormalizedString text)
    {
        if (cleanText)
        {
            Clean(text);
        }

        if (handleChineseChars)
        {
            SpaceOutChineseChars(text);
        }

        // strip_accents unset means "follow lowercase", as the published BertNormalizer docs say.
        if (stripAccents ?? lowercase)
        {
            new UnicodeFormNormalizer(compatibility: false, compose: false).Normalize(text);
            new StripAccentsNormalizer(nonSpacingOnly: true).Normalize(text);
        }

        if (lowercase)
        {
            new LowercaseNormalizer().Normalize(text);
        }
    }

    private static void Clean(NormalizedString text)
    {
        var b = new NormalizedStringBuilder();
        string s = text.Text;
        for (int i = 0; i < s.Length;)
        {
            int cp = NormalizerHelpers.CodePointAt(s, i, out int w);
            OffsetPair origin = NormalizerHelpers.OriginOf(text, i, w);
            if (cp == 0 || cp == 0xFFFD || IsControl(cp))
            {
                i += w;
                continue;
            }

            if (CodePointRegex.IsWhiteSpace(cp))
            {
                b.Append(' ', origin);
            }
            else
            {
                NormalizerHelpers.AppendCodePoint(b, cp, origin);
            }

            i += w;
        }

        b.CopyTo(text);
    }

    // Tab, line feed and carriage return are whitespace for the cleaning step; every other control, format, private-use or unassigned code point is dropped.
    private static bool IsControl(int cp) =>
        cp is not (0x09 or 0x0A or 0x0D) && CodePointRegex.CategoryOf(cp) is UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.Surrogate or UnicodeCategory.PrivateUse or UnicodeCategory.OtherNotAssigned;

    private static void SpaceOutChineseChars(NormalizedString text)
    {
        var b = new NormalizedStringBuilder();
        string s = text.Text;
        for (int i = 0; i < s.Length;)
        {
            int cp = NormalizerHelpers.CodePointAt(s, i, out int w);
            OffsetPair origin = NormalizerHelpers.OriginOf(text, i, w);
            if (IsCjk(cp))
            {
                b.Append(' ', origin);
                NormalizerHelpers.AppendCodePoint(b, cp, origin);
                b.Append(' ', origin);
            }
            else
            {
                NormalizerHelpers.AppendCodePoint(b, cp, origin);
            }

            i += w;
        }

        b.CopyTo(text);
    }

    // The CJK Unified Ideograph blocks listed in the original BERT tokenization code.
    private static bool IsCjk(int cp) =>
        cp is (>= 0x4E00 and <= 0x9FFF) or (>= 0x3400 and <= 0x4DBF) or (>= 0x20000 and <= 0x2A6DF) or (>= 0x2A700 and <= 0x2B73F)
            or (>= 0x2B740 and <= 0x2B81F) or (>= 0x2B820 and <= 0x2CEAF) or (>= 0xF900 and <= 0xFAFF) or (>= 0x2F800 and <= 0x2FA1F);
}

/// <summary>NFC, NFD, NFKC or NFKD, keeping alignment: each run of a base character and its combining marks is normalized on its own.</summary>
internal sealed class UnicodeFormNormalizer(bool compatibility, bool compose) : INormalizer
{
    public void Normalize(NormalizedString text)
    {
        string s = text.Text;
        if (s.Length == 0 || IsAscii(s))
        {
            return;
        }

        var b = new NormalizedStringBuilder();
        int i = 0;
        while (i < s.Length)
        {
            int start = i;
            NormalizerHelpers.CodePointAt(s, i, out int w);
            i += w;
            while (i < s.Length)
            {
                int cp = NormalizerHelpers.CodePointAt(s, i, out int mw);
                if (!NormalizerHelpers.IsMark(cp) && !UnicodeNormalization.CombinesWithPrevious(cp))
                {
                    break;
                }

                i += mw;
            }

            string chunk = s.Substring(start, i - start);
            string normalized = IsWellFormed(chunk) ? UnicodeNormalization.Normalize(chunk, compatibility, compose) : chunk;
            if (string.Equals(normalized, chunk, StringComparison.Ordinal))
            {
                for (int k = start; k < i; k++)
                {
                    b.AppendFrom(text, k);
                }
            }
            else
            {
                // Each output unit takes the origin of the input unit at the same position, and the last input unit when the output is
                // longer; a composed character therefore keeps the origin of its first component (observed in output of tokenizers 0.22.1).
                // When the output is shorter, the first unit keeps the first input and the rest line up with the end of the input, so a
                // mark left over after a composition keeps its own origin (also observed in output of tokenizers 0.22.1).
                int shrink = Math.Max(0, chunk.Length - normalized.Length);
                for (int k = 0; k < normalized.Length; k++)
                {
                    int input = k == 0 ? 0 : Math.Min(k + shrink, chunk.Length - 1);
                    b.Append(normalized[k], text.AlignmentAt(start + input));
                }
            }
        }

        b.CopyTo(text);
    }

    private static bool IsAscii(string s)
    {
        foreach (char c in s)
        {
            if (c >= 0x80)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsWellFormed(string s)
    {
        for (int i = 0; i < s.Length; i++)
        {
            if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
            {
                i++;
            }
            else if (char.IsSurrogate(s[i]))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>Lowercases each code point, with the full mapping where it is longer than one character.</summary>
internal sealed class LowercaseNormalizer : INormalizer
{
    public void Normalize(NormalizedString text)
    {
        var b = new NormalizedStringBuilder();
        string s = text.Text;
        for (int i = 0; i < s.Length;)
        {
            int cp = NormalizerHelpers.CodePointAt(s, i, out int w);
            OffsetPair origin = NormalizerHelpers.OriginOf(text, i, w);
            if (cp is >= 0xD800 and <= 0xDFFF)
            {
                b.Append((char)cp, origin);
            }
            else
            {
                b.Append(NormalizerHelpers.Lower(cp), origin);
            }

            i += w;
        }

        b.CopyTo(text);
    }
}

/// <summary>Removes whitespace from the ends of the text.</summary>
internal sealed class StripNormalizer(bool left, bool right) : INormalizer
{
    public void Normalize(NormalizedString text)
    {
        string s = text.Text;
        int start = 0;
        int end = s.Length;
        if (left)
        {
            while (start < end && CodePointRegex.IsWhiteSpace(s[start]))
            {
                start++;
            }
        }

        if (right)
        {
            while (end > start && CodePointRegex.IsWhiteSpace(s[end - 1]))
            {
                end--;
            }
        }

        if (start == 0 && end == s.Length)
        {
            return;
        }

        NormalizedString part = text.Slice(start, end);
        text.Replace(part.Text, CopyAlignment(part));
    }

    private static OffsetPair[] CopyAlignment(NormalizedString part)
    {
        var a = new OffsetPair[part.Length];
        for (int i = 0; i < a.Length; i++)
        {
            a[i] = part.AlignmentAt(i);
        }

        return a;
    }
}

/// <summary>Removes combining marks (accents), usually after NFD. The BERT normalizer removes nonspacing marks only; the StripAccents normalizer removes every mark, as observed in output of tokenizers 0.22.1.</summary>
internal sealed class StripAccentsNormalizer(bool nonSpacingOnly = false) : INormalizer
{
    public void Normalize(NormalizedString text)
    {
        var b = new NormalizedStringBuilder();
        string s = text.Text;
        for (int i = 0; i < s.Length;)
        {
            int cp = NormalizerHelpers.CodePointAt(s, i, out int w);
            if (!(nonSpacingOnly ? NormalizerHelpers.IsNonSpacingMark(cp) : NormalizerHelpers.IsMark(cp)))
            {
                for (int k = 0; k < w; k++)
                {
                    b.AppendFrom(text, i + k);
                }
            }

            i += w;
        }

        b.CopyTo(text);
    }
}

/// <summary>Replaces every match of a literal string or a pattern with fixed text.</summary>
internal sealed class ReplaceNormalizer : INormalizer
{
    private readonly string _pattern;
    private readonly CodePointRegex? _regex;
    private readonly string _content;

    internal ReplaceNormalizer(string pattern, bool isRegex, string content)
    {
        _pattern = pattern;
        _regex = isRegex ? CodePointRegex.Parse(pattern) : null;
        _content = content;
    }

    public void Normalize(NormalizedString text)
    {
        List<OffsetPair> matches = Find(text.Text);
        if (matches.Count == 0)
        {
            return;
        }

        var b = new NormalizedStringBuilder();
        int at = 0;
        foreach (OffsetPair m in matches)
        {
            for (int k = at; k < m.Start; k++)
            {
                b.AppendFrom(text, k);
            }

            // Replacement text takes the origin of the last character it replaces (observed in output of tokenizers 0.22.1).
            b.Append(_content, m.End > m.Start ? text.ToOriginal(m.End - 1, m.End) : text.ToOriginal(m.Start, m.Start));
            at = m.End;
        }

        for (int k = at; k < text.Length; k++)
        {
            b.AppendFrom(text, k);
        }

        b.CopyTo(text);
    }

    private List<OffsetPair> Find(string s)
    {
        if (_regex is not null)
        {
            return _regex.Matches(s);
        }

        var result = new List<OffsetPair>();
        if (_pattern.Length == 0)
        {
            return result;
        }

        int from = 0;
        while (from <= s.Length - _pattern.Length)
        {
            int at = s.IndexOf(_pattern, from, StringComparison.Ordinal);
            if (at < 0)
            {
                break;
            }

            result.Add(new OffsetPair(at, at + _pattern.Length));
            from = at + _pattern.Length;
        }

        return result;
    }
}

/// <summary>Puts a fixed string in front of the text.</summary>
internal sealed class PrependNormalizer(string prefix) : INormalizer
{
    public void Normalize(NormalizedString text)
    {
        if (text.Length == 0 || prefix.Length == 0)
        {
            return;
        }

        var b = new NormalizedStringBuilder();
        NormalizerHelpers.CodePointAt(text.Text, 0, out int firstWidth);
        b.Append(prefix, text.ToOriginal(0, firstWidth));
        for (int k = 0; k < text.Length; k++)
        {
            b.AppendFrom(text, k);
        }

        b.CopyTo(text);
    }
}

/// <summary>Rewrites every UTF-8 byte of the text as the printable character of the GPT-2 byte table.</summary>
internal sealed class ByteLevelNormalizer : INormalizer
{
    public void Normalize(NormalizedString text)
    {
        var b = new NormalizedStringBuilder();
        string s = text.Text;
        for (int i = 0; i < s.Length;)
        {
            int cp = NormalizerHelpers.CodePointAt(s, i, out int w);
            OffsetPair origin = NormalizerHelpers.OriginOf(text, i, w);
            foreach (byte x in Encoding.UTF8.GetBytes(char.ConvertFromUtf32(Rune.IsValid(cp) ? cp : 0xFFFD)))
            {
                b.Append(ByteLevelTable.ToChar(x), origin);
            }

            i += w;
        }

        b.CopyTo(text);
    }
}

/// <summary>The GPT-2 byte-to-character table: printable bytes map to themselves, the others to U+0100 and up in order.</summary>
internal static class ByteLevelTable
{
    private static readonly char[] ByteToChar = BuildByteToChar();
    private static readonly int[] CharToByte = BuildCharToByte();

    internal static char ToChar(byte b) => ByteToChar[b];

    /// <summary>Gets the byte a table character stands for, or -1 when the character is not in the table.</summary>
    internal static int ToByte(char c) => c < CharToByte.Length ? CharToByte[c] : -1;

    private static char[] BuildByteToChar()
    {
        var table = new char[256];
        int extra = 0;
        for (int b = 0; b < 256; b++)
        {
            bool printable = b is (>= 0x21 and <= 0x7E) or (>= 0xA1 and <= 0xAC) or (>= 0xAE and <= 0xFF);
            table[b] = printable ? (char)b : (char)(0x100 + extra++);
        }

        return table;
    }

    private static int[] BuildCharToByte()
    {
        var table = new int[0x144];
        Array.Fill(table, -1);
        for (int b = 0; b < 256; b++)
        {
            table[ByteToChar[b]] = b;
        }

        return table;
    }
}
