using System.Globalization;
using System.Text;

namespace RecursiveAI.OneNlp.Matching;

/// <summary>
/// The orthographic properties of a token's text that rule patterns can test: the flags behind <c>IS_ALPHA</c>, <c>IS_LOWER</c>,
/// <c>LIKE_NUM</c> and the others, and the word <see cref="Shape(ReadOnlySpan{char})"/>.
/// </summary>
/// <remarks>
/// The definitions follow spaCy's published description of its token flags and shape, and were settled where the description
/// is silent by running spaCy 3.8.18 on probe strings. Properties are evaluated over Unicode code points, not UTF-16 units, so a
/// character outside the Basic Multilingual Plane counts once. The Unicode data is the runtime's, which may be newer than the
/// data a reference implementation uses; code points assigned only in a newer version can differ.
/// </remarks>
public static class TokenText
{
    // Unicode "Other_Uppercase" and "Other_Lowercase" code points that str.isupper/islower treat as cased (UCD PropList.txt).
    private static readonly (int Lo, int Hi)[] OtherUppercase =
    [
        (0x2160, 0x216F), (0x24B6, 0x24CF), (0x1F130, 0x1F149), (0x1F150, 0x1F169), (0x1F170, 0x1F189),
    ];

    private static readonly (int Lo, int Hi)[] OtherLowercase =
    [
        (0xAA, 0xAA), (0xBA, 0xBA), (0x2B0, 0x2B8), (0x2C0, 0x2C1), (0x2E0, 0x2E4), (0x345, 0x345), (0x37A, 0x37A),
        (0x10FC, 0x10FC), (0x1D2C, 0x1D6A), (0x1D78, 0x1D78), (0x1D9B, 0x1DBF), (0x2071, 0x2071), (0x207F, 0x207F),
        (0x2090, 0x209C), (0x2170, 0x217F), (0x24D0, 0x24E9), (0x2C7C, 0x2C7D), (0xA69C, 0xA69D), (0xA770, 0xA770),
        (0xA7F2, 0xA7F4), (0xA7F8, 0xA7F9), (0xAB5C, 0xAB5F), (0xAB69, 0xAB69), (0x10780, 0x10780), (0x10783, 0x10785), (0x10787, 0x107B0), (0x107B2, 0x107BA),
        (0x1E030, 0x1E06D),
    ];

    private static readonly string[] CardinalWords =
    [
        "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve", "thirteen",
        "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen", "twenty", "thirty", "forty", "fifty", "sixty",
        "seventy", "eighty", "ninety", "hundred", "thousand", "million", "billion", "trillion", "quadrillion", "quintillion",
        "sextillion", "septillion", "octillion", "nonillion", "decillion",
    ];

    private static readonly string[] OrdinalWords =
    [
        "first", "second", "third", "fourth", "fifth", "sixth", "seventh", "eighth", "ninth", "tenth", "eleventh", "twelfth",
        "thirteenth", "fourteenth", "fifteenth", "sixteenth", "seventeenth", "eighteenth", "nineteenth", "twentieth", "thirtieth",
        "fortieth", "fiftieth", "sixtieth", "seventieth", "eightieth", "ninetieth", "hundredth", "thousandth", "millionth",
        "billionth", "trillionth", "quadrillionth", "quintillionth", "sextillionth", "septillionth", "octillionth", "nonillionth", "decillionth",
    ];

    private static readonly HashSet<string> NumberWords = new([.. CardinalWords, .. OrdinalWords], StringComparer.Ordinal);

    private static readonly HashSet<string> LeftPunct = new(["\"", "'", "(", "<", "[", "{", "\u00AB", "\u2018", "\u201A", "\u201B", "\u201C", "\u201E", "\u201F", "\u2039", "\u276E", "``"], StringComparer.Ordinal);
    private static readonly HashSet<string> RightPunct = new(["\"", "'", ")", ">", "]", "}", "\u00BB", "\u2019", "\u201D", "\u203A", "\u276F", "''"], StringComparer.Ordinal);
    private static readonly HashSet<string> Quotes = new(
        [
        "\"", "'", "`", "\u00AB", "\u00BB", "\u2018", "\u2019", "\u201A", "\u201B", "\u201C", "\u201D", "\u201E", "\u201F", "\u2039",
        "\u203A", "\u276E", "\u276F", "``", "''",
    ],
        StringComparer.Ordinal);

    private static readonly HashSet<char> Brackets = ['(', ')', '<', '>', '[', ']', '{', '}'];

    /// <summary>Whether every character is a letter (a Unicode <c>L</c> category) and the text is not empty.</summary>
    /// <param name="text">The token text.</param>
    public static bool IsAlpha(ReadOnlySpan<char> text) => All(text, static r => Rune.IsLetter(r));

    /// <summary>Whether every character is below U+0080. The empty text qualifies.</summary>
    /// <param name="text">The token text.</param>
    public static bool IsAscii(ReadOnlySpan<char> text) => System.Text.Ascii.IsValid(text);

    /// <summary>Whether every character has a Unicode digit value (decimal digits, superscripts, circled digits) and the text is not empty.</summary>
    /// <param name="text">The token text.</param>
    public static bool IsDigit(ReadOnlySpan<char> text) => All(text, static r => HasDigitValue(r));

    /// <summary>Whether the text has at least one cased character and none of the cased characters is upper or title case.</summary>
    /// <param name="text">The token text.</param>
    public static bool IsLower(ReadOnlySpan<char> text)
    {
        bool cased = false;
        foreach (Rune r in text.EnumerateRunes())
        {
            if (IsUpperRune(r) || Rune.GetUnicodeCategory(r) == UnicodeCategory.TitlecaseLetter)
            {
                return false;
            }

            cased |= IsLowerRune(r);
        }

        return cased;
    }

    /// <summary>Whether the text has at least one cased character and none of the cased characters is lower or title case.</summary>
    /// <param name="text">The token text.</param>
    public static bool IsUpper(ReadOnlySpan<char> text)
    {
        bool cased = false;
        foreach (Rune r in text.EnumerateRunes())
        {
            if (IsLowerRune(r) || Rune.GetUnicodeCategory(r) == UnicodeCategory.TitlecaseLetter)
            {
                return false;
            }

            cased |= IsUpperRune(r);
        }

        return cased;
    }

    /// <summary>
    /// Whether the text is in title case: upper- and title-case characters follow only uncased characters, and lower-case characters
    /// follow only cased ones, with at least one cased character.
    /// </summary>
    /// <param name="text">The token text.</param>
    public static bool IsTitle(ReadOnlySpan<char> text)
    {
        bool cased = false;
        bool previousCased = false;
        foreach (Rune r in text.EnumerateRunes())
        {
            if (IsUpperRune(r) || Rune.GetUnicodeCategory(r) == UnicodeCategory.TitlecaseLetter)
            {
                if (previousCased)
                {
                    return false;
                }

                previousCased = true;
                cased = true;
            }
            else if (IsLowerRune(r))
            {
                if (!previousCased)
                {
                    return false;
                }

                previousCased = true;
                cased = true;
            }
            else
            {
                previousCased = false;
            }
        }

        return cased;
    }

    /// <summary>Whether every character is in a Unicode punctuation category (<c>P*</c>) and the text is not empty.</summary>
    /// <param name="text">The token text.</param>
    public static bool IsPunct(ReadOnlySpan<char> text) => All(text, static r => IsPunctuationCategory(Rune.GetUnicodeCategory(r)));

    /// <summary>Whether the text is not empty and every character is white space (including U+001C to U+001F, which Python's rules count).</summary>
    /// <param name="text">The token text.</param>
    public static bool IsSpace(ReadOnlySpan<char> text) => All(text, static r => r.Value is >= 0x1C and <= 0x1F || Rune.IsWhiteSpace(r));

    /// <summary>Whether every character is a currency symbol (category <c>Sc</c>) and the text is not empty.</summary>
    /// <param name="text">The token text.</param>
    public static bool IsCurrency(ReadOnlySpan<char> text) => All(text, static r => Rune.GetUnicodeCategory(r) == UnicodeCategory.CurrencySymbol);

    /// <summary>Whether the text is an opening quote or bracket: <c>" ' ( &lt; [ {</c>, the opening guillemets and curly quotes, or <c>``</c>.</summary>
    /// <param name="text">The token text.</param>
    public static bool IsLeftPunct(ReadOnlySpan<char> text) => LeftPunct.GetAlternateLookup<ReadOnlySpan<char>>().Contains(text);

    /// <summary>Whether the text is a closing quote or bracket: <c>" ' ) &gt; ] }</c>, the closing guillemets and curly quotes, or <c>''</c>.</summary>
    /// <param name="text">The token text.</param>
    public static bool IsRightPunct(ReadOnlySpan<char> text) => RightPunct.GetAlternateLookup<ReadOnlySpan<char>>().Contains(text);

    /// <summary>Whether the text is a quotation mark: straight, curly, angle or grave, or the doubled forms <c>``</c> and <c>''</c>.</summary>
    /// <param name="text">The token text.</param>
    public static bool IsQuote(ReadOnlySpan<char> text) => Quotes.GetAlternateLookup<ReadOnlySpan<char>>().Contains(text);

    /// <summary>Whether the text is one of the ASCII brackets <c>( ) &lt; &gt; [ ] { }</c>.</summary>
    /// <param name="text">The token text.</param>
    public static bool IsBracket(ReadOnlySpan<char> text) => text.Length == 1 && Brackets.Contains(text[0]);

    /// <summary>
    /// Whether the text resembles a number: digits with any <c>,</c> and <c>.</c> separators and an optional leading sign, a fraction
    /// such as <c>3/4</c>, a digit string with an ordinal suffix (<c>1st</c>, <c>22nd</c>, <c>3RD</c>, <c>10th</c>), or an English number word
    /// from <c>zero</c> to <c>decillion</c> or <c>first</c> to <c>decillionth</c>, in any letter case.
    /// </summary>
    /// <param name="text">The token text.</param>
    public static bool LikeNum(ReadOnlySpan<char> text)
    {
        if (text.Length > 0 && text[0] is '+' or '-')
        {
            text = text[1..];
        }

        if (text.Length == 0)
        {
            return false;
        }

        // Strip the group and decimal separators, then ask whether what remains is digits, a fraction, or digits plus an ordinal suffix.
        // The stack buffer is used only for texts of at most 128 units; longer ones get a heap array.
        Span<char> stripped = text.Length <= 128 ? stackalloc char[text.Length] : new char[text.Length];
        int n = 0;
        foreach (char c in text)
        {
            if (c is not ',' and not '.')
            {
                stripped[n++] = c;
            }
        }

        ReadOnlySpan<char> s = stripped[..n];
        if (s.Length > 0)
        {
            if (IsDigit(s))
            {
                return true;
            }

            int slash = s.IndexOf('/');
            if (slash > 0 && slash < s.Length - 1 && s.LastIndexOf('/') == slash && IsDigit(s[..slash]) && IsDigit(s[(slash + 1)..]))
            {
                return true;
            }

            if (s.Length > 2 && IsOrdinalSuffix(s[^2..]) && IsDigit(s[..^2]))
            {
                return true;
            }
        }

        return IsNumberWord(text);
    }

    /// <summary>
    /// Whether the text resembles a URL: it starts with <c>http://</c>, <c>https://</c> or <c>www.</c> plus at least one character, or it contains,
    /// after a character that cannot be part of a host name, a host name (labels of letters, digits and hyphens, ending in a top-level domain of
    /// two or more lower-case letters) or a public IPv4 address, then an optional <c>:port</c>, then nothing or a path, query or fragment.
    /// </summary>
    /// <param name="text">The token text.</param>
    public static bool LikeUrl(ReadOnlySpan<char> text)
    {
        if (text.StartsWith("http://", StringComparison.Ordinal) || text.StartsWith("https://", StringComparison.Ordinal)
            || (text.StartsWith("www.", StringComparison.Ordinal) && text.Length > 4))
        {
            return true;
        }

        // The pattern may start anywhere a host name could begin: at the start, or after a character that is not part of a host name
        // (so "http:a.com" and "a_b.com" qualify, "user@host.com" and "(1.2.3.4.5" do not).
        for (int i = 0; i < text.Length; i++)
        {
            if (!IsHostChar(text[i]) || (i > 0 && (IsHostChar(text[i - 1]) || text[i - 1] is '.' or '@')))
            {
                continue;
            }

            if (UrlAt(text[i..]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether the text starts like an e-mail address: local part of ASCII letters, digits and <c>. _ + -</c>, an <c>@</c>, a domain label of ASCII
    /// letters, digits and hyphens, a dot and at least one more ASCII letter, digit, dot or hyphen. Anything after that is ignored.
    /// </summary>
    /// <param name="text">The token text.</param>
    public static bool LikeEmail(ReadOnlySpan<char> text)
    {
        int at = text.IndexOf('@');
        if (at < 1)
        {
            return false;
        }

        foreach (char c in text[..at])
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '+' or '-'))
            {
                return false;
            }
        }

        ReadOnlySpan<char> rest = text[(at + 1)..];
        int label = 0;
        while (label < rest.Length && (char.IsAsciiLetterOrDigit(rest[label]) || rest[label] == '-'))
        {
            label++;
        }

        return label > 0 && label + 1 < rest.Length && rest[label] == '.' && (char.IsAsciiLetterOrDigit(rest[label + 1]) || rest[label + 1] is '.' or '-');
    }

    /// <summary>
    /// The word shape: each letter becomes <c>X</c> (upper case) or <c>x</c>, each digit becomes <c>d</c>, every other character stays as it
    /// is, and a run of the same output character is cut after four. <c>Hello</c> gives <c>Xxxxx</c>, <c>12345</c> gives <c>dddd</c> and
    /// <c>dd-dddd</c> is the shape of <c>12-3456</c>.
    /// </summary>
    /// <param name="text">The token text.</param>
    /// <returns>The shape string.</returns>
    public static string Shape(ReadOnlySpan<char> text)
    {
        var sb = new StringBuilder(Math.Min(text.Length, 16));
        Rune last = default;
        int run = 0;
        foreach (Rune r in text.EnumerateRunes())
        {
            Rune c = r;
            if (Rune.IsLetter(r))
            {
                c = new Rune(IsUpperRune(r) ? 'X' : 'x');
            }
            else if (HasDigitValue(r))
            {
                c = new Rune('d');
            }

            if (run > 0 && c == last)
            {
                run++;
            }
            else
            {
                last = c;
                run = 1;
            }

            if (run <= 4)
            {
                sb.Append(c.ToString());
            }
        }

        return sb.ToString();
    }

    /// <summary>The number of Unicode code points in the text, which is what <c>LENGTH</c> counts.</summary>
    /// <param name="text">The token text.</param>
    public static int CodePointCount(ReadOnlySpan<char> text)
    {
        int n = text.Length;
        for (int i = 0; i + 1 < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && char.IsLowSurrogate(text[i + 1]))
            {
                n--;
                i++;
            }
        }

        return n;
    }

    private static bool All(ReadOnlySpan<char> text, Func<Rune, bool> test)
    {
        if (text.Length == 0)
        {
            return false;
        }

        foreach (Rune r in text.EnumerateRunes())
        {
            if (!test(r))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsPunctuationCategory(UnicodeCategory c) =>
        c is UnicodeCategory.ConnectorPunctuation or UnicodeCategory.DashPunctuation or UnicodeCategory.OpenPunctuation
            or UnicodeCategory.ClosePunctuation or UnicodeCategory.InitialQuotePunctuation or UnicodeCategory.FinalQuotePunctuation
            or UnicodeCategory.OtherPunctuation;

    private static bool HasDigitValue(Rune r)
    {
        if (r.IsBmp)
        {
            return CharUnicodeInfo.GetDigitValue((char)r.Value) >= 0;
        }

        return CharUnicodeInfo.GetDigitValue(r.ToString(), 0) >= 0;
    }

    private static bool IsUpperRune(Rune r) => Rune.GetUnicodeCategory(r) == UnicodeCategory.UppercaseLetter || InRanges(r.Value, OtherUppercase);

    private static bool IsLowerRune(Rune r) => Rune.GetUnicodeCategory(r) == UnicodeCategory.LowercaseLetter || InRanges(r.Value, OtherLowercase);

    private static bool InRanges(int value, (int Lo, int Hi)[] ranges)
    {
        foreach ((int lo, int hi) in ranges)
        {
            if (value >= lo && value <= hi)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsOrdinalSuffix(ReadOnlySpan<char> s) =>
        s.Equals("st", StringComparison.OrdinalIgnoreCase) || s.Equals("nd", StringComparison.OrdinalIgnoreCase)
        || s.Equals("rd", StringComparison.OrdinalIgnoreCase) || s.Equals("th", StringComparison.OrdinalIgnoreCase);

    private static bool IsNumberWord(ReadOnlySpan<char> text)
    {
        if (text.Length is < 3 or > 16 || !text.ContainsAnyInRange('A', 'z'))
        {
            return false;
        }

        // Bounded: the length check above limits the text to 16 units.
        Span<char> lower = stackalloc char[16];
        int n = text.ToLowerInvariant(lower);
        return NumberWords.GetAlternateLookup<ReadOnlySpan<char>>().Contains(lower[..n]);
    }

    // Diego Perini's public IPv4 rules, as spaCy's description of LIKE_URL leaves addresses open: private and loopback ranges are not URLs.
    private static bool IsPublicIPv4(ReadOnlySpan<char> host)
    {
        // Fixed size: four octets; a fifth is rejected before it is stored.
        Span<int> octets = stackalloc int[4];
        int count = 0;
        foreach (Range part in host.Split('.'))
        {
            ReadOnlySpan<char> p = host[part];
            if (count == 4 || p.Length is < 1 or > 3 || p.ContainsAnyExceptInRange('0', '9') || !int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out int v))
            {
                return false;
            }

            octets[count++] = v;
        }

        if (count != 4 || octets[0] is < 1 or > 223 || octets[3] is < 1 or > 254 || octets[1] > 255 || octets[2] > 255)
        {
            return false;
        }

        int a = octets[0], b = octets[1];
        return !(a is 10 or 127 || (a == 169 && b == 254) || (a == 192 && b == 168) || (a == 172 && b is >= 16 and <= 31));
    }

    private static bool IsHostChar(char c) => char.IsLetterOrDigit(c) || c == '-';

    private static bool UrlAt(ReadOnlySpan<char> s)
    {
        int n = 0;
        while (n < s.Length && (IsHostChar(s[n]) || s[n] == '.'))
        {
            n++;
        }

        ReadOnlySpan<char> host = s[..n];
        ReadOnlySpan<char> rest = s[n..];
        if (!(IsPublicIPv4(host) || IsHostWithTld(host)))
        {
            return false;
        }

        if (rest.Length > 0 && rest[0] == ':')
        {
            int d = 1;
            while (d < rest.Length && char.IsAsciiDigit(rest[d]))
            {
                d++;
            }

            if (d == 1)
            {
                return false;
            }

            rest = rest[d..];
        }

        return rest.IsEmpty || rest[0] is '/' or '?' or '#';
    }

    private static bool IsHostWithTld(ReadOnlySpan<char> host)
    {
        int dot = host.LastIndexOf('.');
        if (dot <= 0)
        {
            return false;
        }

        ReadOnlySpan<char> tld = host[(dot + 1)..];
        if (tld.Length < 2)
        {
            return false;
        }

        foreach (char c in tld)
        {
            if (!char.IsLetter(c) || char.IsUpper(c))
            {
                return false;
            }
        }

        // Every label before the top-level domain must be non-empty.
        return !host[..dot].StartsWith(".", StringComparison.Ordinal) && !host[..dot].EndsWith(".", StringComparison.Ordinal) && !host[..dot].Contains("..", StringComparison.Ordinal);
    }
}
