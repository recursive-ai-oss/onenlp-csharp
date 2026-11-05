using System.Collections.Frozen;
using OneFont;

namespace RecursiveAI.OneNlp.Segmentation;

/// <summary>
/// The word tokenizer: the Unicode word segments of UAX #29 section 4 (through OneFont) that are not white space, adjusted by
/// <see cref="SegmentationData"/> where Universal Dependencies tokenization differs from UAX #29 (contractions, abbreviations,
/// URLs and e-mail addresses, hyphenated words).
/// </summary>
/// <remarks>
/// <para>
/// Offsets are UTF-16 offsets into the original string; nothing is normalized. Every character that is not white space belongs to
/// exactly one token, and white space belongs to none (the document recovers it as <see cref="Token.HasSpaceAfter"/>).
/// </para>
/// <para>
/// Work is linear in the text: the segment walk is OneFont's, and the adjustments look at a bounded neighbourhood (a URL is read to
/// at most 2048 characters, an e-mail address to 256 and 255). There is no regular expression and no backtracking.
/// A lone surrogate is an ordinary one-character token. Safe for concurrent use.
/// </para>
/// </remarks>
public sealed class UaxWordTokenizer : IWordTokenizer
{
    private const int MaxUrlLength = 2048;
    private const int MaxEmailLocalLength = 256;
    private const int MaxEmailDomainLength = 255;

    private readonly SegmentationData _data;
    private readonly FrozenDictionary<string, int[]>.AlternateLookup<ReadOnlySpan<char>> _specialCases;
    private readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> _abbreviations;
    private readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> _clitics;
    private readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>>? _hyphenPrefixes;
    private readonly bool _adjust;

    /// <summary>Creates a tokenizer.</summary>
    /// <param name="data">The adjustments; <see cref="SegmentationData.None"/> gives the plain UAX #29 segments. <see langword="null"/> means <see cref="SegmentationData.Neutral"/>.</param>
    public UaxWordTokenizer(SegmentationData? data = null)
    {
        _data = data ?? SegmentationData.Neutral;
        _adjust = !ReferenceEquals(_data, SegmentationData.None);
        _specialCases = _data.SpecialCases.GetAlternateLookup<ReadOnlySpan<char>>();
        _abbreviations = _data.Abbreviations.GetAlternateLookup<ReadOnlySpan<char>>();
        _clitics = _data.Clitics.GetAlternateLookup<ReadOnlySpan<char>>();
        _hyphenPrefixes = _data.HyphenPrefixes?.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    /// <summary>The adjustments this tokenizer applies.</summary>
    public SegmentationData Data => _data;

    /// <summary>Creates the tokenizer for a language: English data for English, the neutral defaults otherwise.</summary>
    /// <param name="language">The language.</param>
    public static UaxWordTokenizer For(Language language) => new(SegmentationData.For(language));

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> or <paramref name="strings"/> is <see langword="null"/>.</exception>
    public Doc Tokenize(string text, StringStore strings)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(strings);
        var tokens = new List<TokenBoundary>();
        Segment(text, tokens);
        return new Doc(strings, text, tokens.ToArray());
    }

    /// <summary>Finds the tokens of a text.</summary>
    /// <param name="text">The text; may be empty.</param>
    /// <returns>The tokens in text order as (start, length) in UTF-16 units.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<TokenBoundary> Segment(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var tokens = new List<TokenBoundary>();
        Segment(text, tokens);
        return tokens;
    }

    private void Segment(string text, List<TokenBoundary> tokens)
    {
        int n = text.Length;
        int pos = 0;
        while (pos < n)
        {
            // UAX #29 section 4: the next default word boundary after pos.
            int end = TextSegmentation.GetNextWordBoundary(text, pos);
            if (end <= pos)
            {
                end = pos + 1;
            }

            if (IsAllWhiteSpace(text.AsSpan(pos, end - pos)))
            {
                pos = end;
                continue;
            }

            if (!_adjust)
            {
                tokens.Add(new TokenBoundary(pos, end - pos));
                pos = end;
                continue;
            }

            if (_data.KeepUrlsAndEmails && TryUrlOrEmail(text, pos, out int whole))
            {
                tokens.Add(new TokenBoundary(pos, whole - pos));
                pos = whole;
                continue;
            }

            end = Extend(text, pos, end);
            Split(text, pos, end, tokens);
            pos = end;
        }
    }

    // Joins what UAX #29 separates but a UD treebank keeps together. Each step consumes at least one character.
    private int Extend(string text, int pos, int end)
    {
        int n = text.Length;
        if (_data.Emoticons.Length > 0 && Array.IndexOf(_data.EmoticonStarts, text[pos]) >= 0)
        {
            foreach (string emoticon in _data.Emoticons)
            {
                int after = pos + emoticon.Length;
                if (text.AsSpan(pos).StartsWith(emoticon, StringComparison.Ordinal) && (after >= n || !char.IsLetterOrDigit(text[after])))
                {
                    return after;
                }
            }
        }

        if (end - pos == 1 && Array.IndexOf(_data.MarkRuns, text[pos]) >= 0)
        {
            // A run of marks ("...", "--", "?!", "$$$") is one token.
            while (end < n && Array.IndexOf(_data.MarkRuns, text[end]) >= 0 && (!_data.SameMarkRuns || text[end] == text[pos]))
            {
                end++;
            }

            // A full stop that closes a run of other marks ends the sentence, not the run ("$$$.").
            if (text[pos] != '.')
            {
                while (end - 1 > pos && text[end - 1] == '.')
                {
                    end--;
                }
            }

            return end;
        }

        while (end < n)
        {
            char c = text[end];
            if (c == '.' && (IsAbbreviation(text.AsSpan(pos, end - pos)) || (_data.OrdinalDot && IsOrdinal(text, pos, end))))
            {
                return end + 1;
            }

            if (_data.JoinHyphenated && (c is '-' or '\u2010') && end + 1 < n && IsLetterOrDigitBefore(text, end) && IsLetterOrDigitAt(text, end + 1)
                && (HyphenMayJoin(text.AsSpan(pos, end - pos)) || (IsDigitBefore(text, end) && IsDigitAt(text, end + 1))))
            {
                end = NextBoundary(text, end + 1);
                continue;
            }

            if (end + 1 < n && Array.IndexOf(_data.DigitJoiners, c) >= 0 && IsDigitBefore(text, end) && IsDigitAt(text, end + 1))
            {
                end = NextBoundary(text, end + 1);
                continue;
            }

            break;
        }

        return end;
    }

    // Hyphens join only after a listed prefix when the language names some (the word so far is the prefix alone).
    private bool HyphenMayJoin(ReadOnlySpan<char> wordSoFar) => _hyphenPrefixes is null || _hyphenPrefixes.Value.Contains(wordSoFar);

    private static int NextBoundary(string text, int from)
    {
        int next = TextSegmentation.GetNextWordBoundary(text, from);
        return next > from ? next : from + 1;
    }

    // An ordinal such as German "3. Oktober": one to four digits, the full stop, then a lower-case word or one of the language's listed
    // words. A full stop that ends a sentence ("im Jahr 2010. Danach") stays a token of its own.
    private bool IsOrdinal(string text, int pos, int dot)
    {
        int digits = dot - pos;
        if (digits is < 1 or > 4)
        {
            return false;
        }

        for (int i = pos; i < dot; i++)
        {
            if (!char.IsAsciiDigit(text[i]))
            {
                return false;
            }
        }

        int next = dot + 1;
        if (next >= text.Length || text[next] != ' ')
        {
            return false;
        }

        while (next < text.Length && text[next] == ' ')
        {
            next++;
        }

        int wordEnd = next;
        while (wordEnd < text.Length && char.IsLetter(text[wordEnd]))
        {
            wordEnd++;
        }

        return wordEnd > next && (char.IsLower(text[next]) || _data.OrdinalFollowers.GetAlternateLookup<ReadOnlySpan<char>>().Contains(text.AsSpan(next, wordEnd - next)));
    }

    private bool IsAbbreviation(ReadOnlySpan<char> word)
    {
        if (word.Length == 0 || word.Length > 16)
        {
            return false;
        }

        if (_abbreviations.Contains(word))
        {
            return true;
        }

        // Dotted initials such as U.S.A or e.g: single letters separated by full stops.
        if (word.Length < 3 || word.Length % 2 == 0)
        {
            return false;
        }

        for (int i = 0; i < word.Length; i++)
        {
            if (i % 2 == 0 ? !char.IsLetter(word[i]) : word[i] != '.')
            {
                return false;
            }
        }

        return _data.Abbreviations.Count > 0;
    }

    // Splits one adjusted token at the contraction or fused form it holds, if any.
    private void Split(string text, int pos, int end, List<TokenBoundary> tokens)
    {
        ReadOnlySpan<char> token = text.AsSpan(pos, end - pos);
        if (_data.HasMultiwordTokens && _data.TryMultiwordWords(token, out string[] words) && SegmentationData.PieceLengths(token, words) is { } pieceLengths)
        {
            int from = pos;
            foreach (int length in pieceLengths)
            {
                tokens.Add(new TokenBoundary(from, length));
                from += length;
            }

            return;
        }

        if (_data.SpecialCases.Count > 0 && _specialCases.TryGetValue(token, out int[]? pieces))
        {
            int at = pos;
            foreach (int length in pieces)
            {
                tokens.Add(new TokenBoundary(at, length));
                at += length;
            }

            return;
        }

        int apostrophe = token.LastIndexOfAny('\'', '\u2019');
        if (apostrophe > 0 && apostrophe < token.Length - 1
            && token[..apostrophe].IndexOfAny('\'', '\u2019') < 0
            && IsLetterOrDigitRun(token[..apostrophe]) && IsLetterRun(token[(apostrophe + 1)..]))
        {
            ReadOnlySpan<char> suffix = token[(apostrophe + 1)..];
            int cut = -1;
            if (_clitics.Contains(suffix))
            {
                cut = apostrophe;
            }
            else if (_data.NTSuffix && suffix.Equals("t", StringComparison.OrdinalIgnoreCase) && apostrophe >= 2
                && token[apostrophe - 1] is 'n' or 'N')
            {
                // UD English: "don't" is "do" + "n't".
                cut = apostrophe - 1;
            }

            if (cut > 0)
            {
                tokens.Add(new TokenBoundary(pos, cut));
                tokens.Add(new TokenBoundary(pos + cut, token.Length - cut));
                return;
            }
        }

        tokens.Add(new TokenBoundary(pos, token.Length));
    }

    // ---- URLs and e-mail addresses (kept whole, as UD treebanks tokenize them) ----

    private static bool TryUrlOrEmail(string text, int pos, out int end)
    {
        end = pos;
        ReadOnlySpan<char> rest = text.AsSpan(pos);
        int prefix = UrlPrefixLength(rest);
        if (prefix > 0)
        {
            int limit = Math.Min(text.Length, pos + MaxUrlLength);
            int e = pos;
            while (e < limit && !char.IsWhiteSpace(text[e]))
            {
                e++;
            }

            // Trailing punctuation belongs to the sentence, not the address; a closing bracket stays only when the address opened one.
            while (e > pos + prefix)
            {
                char last = text[e - 1];
                if (last is '.' or ',' or ';' or ':' or '!' or '?' or '\'' or '"' or '\u2019' or '\u201D' or '>'
                    || (last == ')' && Count(text, pos, e, '(') < Count(text, pos, e, ')'))
                    || (last == ']' && Count(text, pos, e, '[') < Count(text, pos, e, ']'))
                    || char.IsHighSurrogate(last))
                {
                    e--;
                    continue;
                }

                break;
            }

            if (e > pos + prefix)
            {
                end = e;
                return true;
            }

            return false;
        }

        return IsAsciiLetterOrDigit(text[pos]) && TryEmail(text, pos, out end);
    }

    private static int UrlPrefixLength(ReadOnlySpan<char> rest)
    {
        ReadOnlySpan<string> prefixes = ["https://", "http://", "ftp://", "www."];
        foreach (string p in prefixes)
        {
            if (rest.Length > p.Length && rest.StartsWith(p, StringComparison.OrdinalIgnoreCase) && !char.IsWhiteSpace(rest[p.Length]))
            {
                return p.Length;
            }
        }

        return 0;
    }

    private static int Count(string text, int from, int to, char c)
    {
        int count = 0;
        for (int i = from; i < to; i++)
        {
            if (text[i] == c)
            {
                count++;
            }
        }

        return count;
    }

    private static bool TryEmail(string text, int pos, out int end)
    {
        end = pos;
        int n = text.Length;
        int limit = Math.Min(n, pos + MaxEmailLocalLength);
        int i = pos;
        while (i < limit && (IsAsciiLetterOrDigit(text[i]) || text[i] is '.' or '_' or '%' or '+' or '-'))
        {
            i++;
        }

        if (i >= n || text[i] != '@')
        {
            return false;
        }

        int domainLimit = Math.Min(n, i + 1 + MaxEmailDomainLength);
        int k = i + 1;
        int labels = 0;
        int lastStart = k;
        while (true)
        {
            int start = k;
            while (k < domainLimit && (IsAsciiLetterOrDigit(text[k]) || text[k] == '-'))
            {
                k++;
            }

            if (k == start)
            {
                break;
            }

            labels++;
            lastStart = start;
            if (k + 1 < domainLimit && text[k] == '.' && IsAsciiLetterOrDigit(text[k + 1]))
            {
                k++;
                continue;
            }

            break;
        }

        if (labels < 2 || k - lastStart < 2)
        {
            return false;
        }

        for (int j = lastStart; j < k; j++)
        {
            if (!char.IsAsciiLetter(text[j]))
            {
                return false;
            }
        }

        end = k;
        return true;
    }

    // ---- character helpers ----

    private static bool IsAsciiLetterOrDigit(char c) => char.IsAsciiLetterOrDigit(c);

    private static bool IsAllWhiteSpace(ReadOnlySpan<char> segment)
    {
        foreach (char c in segment)
        {
            if (!char.IsWhiteSpace(c))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsLetterOrDigitRun(ReadOnlySpan<char> s)
    {
        foreach (char c in s)
        {
            if (!char.IsLetterOrDigit(c))
            {
                return false;
            }
        }

        return s.Length > 0;
    }

    private static bool IsLetterRun(ReadOnlySpan<char> s)
    {
        foreach (char c in s)
        {
            if (!char.IsLetter(c))
            {
                return false;
            }
        }

        return s.Length > 0;
    }

    private static System.Text.Rune Before(string text, int end)
    {
        System.Text.Rune.DecodeLastFromUtf16(text.AsSpan(0, end), out System.Text.Rune rune, out _);
        return rune;
    }

    private static System.Text.Rune At(string text, int index)
    {
        System.Text.Rune.DecodeFromUtf16(text.AsSpan(index), out System.Text.Rune rune, out _);
        return rune;
    }

    private static bool IsLetterOrDigitBefore(string text, int end) => System.Text.Rune.IsLetterOrDigit(Before(text, end));

    private static bool IsLetterOrDigitAt(string text, int index) => System.Text.Rune.IsLetterOrDigit(At(text, index));

    private static bool IsDigitBefore(string text, int end) => System.Text.Rune.IsDigit(Before(text, end));

    private static bool IsDigitAt(string text, int index) => System.Text.Rune.IsDigit(At(text, index));
}
