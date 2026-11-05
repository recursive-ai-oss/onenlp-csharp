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
    private readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> _dollarWords;
    private readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> _enclitics;
    private readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> _elisions;
    private readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> _hyphenClitics;
    private readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> _hyphenParticles;
    private readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> _hyphenParticleStems;
    private readonly FrozenDictionary<string, SegmentationData.ContextualSplit>.AlternateLookup<ReadOnlySpan<char>> _contextual;
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
        _enclitics = _data.Enclitics.GetAlternateLookup<ReadOnlySpan<char>>();
        _dollarWords = _data.DollarWords.GetAlternateLookup<ReadOnlySpan<char>>();
        _hyphenPrefixes = _data.HyphenPrefixes?.GetAlternateLookup<ReadOnlySpan<char>>();
        _elisions = _data.ElisionPrefixes.GetAlternateLookup<ReadOnlySpan<char>>();
        _hyphenClitics = _data.HyphenClitics.GetAlternateLookup<ReadOnlySpan<char>>();
        _hyphenParticles = _data.HyphenParticles.GetAlternateLookup<ReadOnlySpan<char>>();
        _hyphenParticleStems = _data.HyphenParticleStems.GetAlternateLookup<ReadOnlySpan<char>>();
        _contextual = _data.ContextualSplits.GetAlternateLookup<ReadOnlySpan<char>>();
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

        // A minus sign before a number that follows white space belongs to the number ("-0,8").
        if (_data.NegativeNumbers && text[pos] == '-' && end - pos == 1 && end < n && char.IsAsciiDigit(text[end]) && (pos == 0 || char.IsWhiteSpace(text[pos - 1])))
        {
            return NextBoundary(text, end);
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
            // Portuguese US$ and R$: the dollar sign closes a currency word (UD Portuguese keeps the symbol as one token).
            if (c == '$' && _data.DollarWords.Count > 0 && _dollarWords.Contains(text.AsSpan(pos, end - pos)))
            {
                return end + 1;
            }

            if (c == '.' && (IsAbbreviation(text.AsSpan(pos, end - pos)) || IsInitial(text, pos, end) || (_data.OrdinalDot && IsOrdinal(text, pos, end))))
            {
                return end + 1;
            }

            // "n\u00b0 12": the numero sign stays with its letter.
            if (c is '\u00b0' or '\u00ba' && end - pos == 1 && char.ToLowerInvariant(text[pos]) == 'n' && _data.Abbreviations.Contains("n\u00b0"))
            {
                return end + 1;
            }

            // Digit groups of three, separated by a space ("25 785", "2 001"), are one number.
            if (_data.GroupDigits && c is ' ' or '\u00a0' or '\u202f' && IsDigitGroup(text, pos, end))
            {
                end += 4;
                continue;
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

    // A single capital letter and a full stop that start a name ("V. Prebeg") are an initial: the stop stays.
    private bool IsInitial(string text, int pos, int end) =>
        _data.InitialLetters && end - pos == 1 && char.IsUpper(text[pos]) && end + 2 < text.Length
        && char.IsWhiteSpace(text[end + 1]) && char.IsUpper(text[end + 2]);

    // The text from pos to end is a number written in groups ("1", "12", "2 001") and one more group of exactly three digits follows the space at end.
    private static bool IsDigitGroup(string text, int pos, int end)
    {
        int n = text.Length;
        if (end + 3 >= n || !char.IsAsciiDigit(text[end + 1]) || !char.IsAsciiDigit(text[end + 2]) || !char.IsAsciiDigit(text[end + 3])
            || (end + 4 < n && char.IsAsciiDigit(text[end + 4])))
        {
            return false;
        }

        int digits = 0;
        bool firstGroup = true;
        for (int i = pos; i < end; i++)
        {
            char ch = text[i];
            if (char.IsAsciiDigit(ch))
            {
                digits++;
                continue;
            }

            if (ch is not (' ' or '\u00a0' or '\u202f') || (firstGroup ? digits is < 1 or > 3 : digits != 3))
            {
                return false;
            }

            firstGroup = false;
            digits = 0;
        }

        // What came before is a number whose last group is complete (1 to 3 digits when it is the first, 3 after that).
        return end > pos && (firstGroup ? digits is >= 1 and <= 3 : digits == 3) && (pos == 0 || !char.IsLetterOrDigit(text[pos - 1]));
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

    // Splits one adjusted token at the contraction, elision, hyphenated pronoun or fused form it holds, if any.
    private void Split(string text, int pos, int end, List<TokenBoundary> tokens)
    {
        ReadOnlySpan<char> token = text.AsSpan(pos, end - pos);
        if (_data.HourMarker && TrySplitHour(token, out int hourAt, out int minuteAt))
        {
            // "20h30" is "20" "h" "30".
            tokens.Add(new TokenBoundary(pos, hourAt));
            tokens.Add(new TokenBoundary(pos + hourAt, 1));
            if (minuteAt < token.Length)
            {
                tokens.Add(new TokenBoundary(pos + minuteAt, token.Length - minuteAt));
            }

            return;
        }

        // Contractions and verbs with enclitic pronouns (Spanish): one piece for each word of the multiword token.
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

        if (_data.ContextualSplits.Count > 0 && _contextual.TryGetValue(token, out SegmentationData.ContextualSplit? contextual))
        {
            // A contraction after a noun, the plain word after a verb, a preposition or punctuation ("la plupart des" against "avec des").
            if (!IsSingleWordContext(tokens, text, contextual))
            {
                int at = pos;
                foreach (int length in contextual.Pieces)
                {
                    tokens.Add(new TokenBoundary(at, length));
                    at += length;
                }

                return;
            }

            tokens.Add(new TokenBoundary(pos, token.Length));
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

        // Portuguese "trata-se": the pronoun after the verb's hyphen is a word of its own in UD, written with its hyphen ("-se").
        if (_data.Enclitics.Count > 0)
        {
            int hyphen = token.IndexOf('-');
            if (hyphen > 0 && hyphen == token.LastIndexOf('-') && hyphen < token.Length - 1 && char.IsLetter(token[hyphen - 1]) && _enclitics.Contains(token[(hyphen + 1)..]))
            {
                tokens.Add(new TokenBoundary(pos, hyphen));
                tokens.Add(new TokenBoundary(pos + hyphen, token.Length - hyphen));
                return;
            }
        }

        // Elision (French): "l'homme" is "l'" + "homme". The text before the apostrophe must be a listed word, so "aujourd'hui" and
        // "main-d'oeuvre" stay whole. The rest is split again ("qu'est-ce" -> "qu'" + "est" + "-ce").
        if (_data.ElisionPrefixes.Count > 0)
        {
            int mark = token.IndexOfAny('\'', '\u2019');
            if (mark > 0 && mark < token.Length - 1 && _elisions.Contains(token[..mark]) && char.IsLetterOrDigit(token[mark + 1]))
            {
                tokens.Add(new TokenBoundary(pos, mark + 1));
                Split(text, pos + mark + 1, end, tokens);
                return;
            }
        }

        // Hyphenated pronouns (French): "a-t-il" is "a" + "-t-il"; "celui-ci" and "peut-etre" stay whole.
        if (_data.HyphenClitics.Count > 0 && TrySplitHyphenClitic(token, out int hyphenCut))
        {
            Split(text, pos, pos + hyphenCut, tokens);
            tokens.Add(new TokenBoundary(pos + hyphenCut, token.Length - hyphenCut));
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

    // Digits, h or H, then digits or nothing: "23h", "20h30".
    private static bool TrySplitHour(ReadOnlySpan<char> token, out int hourAt, out int minuteAt)
    {
        hourAt = 0;
        minuteAt = 0;
        int i = 0;
        while (i < token.Length && char.IsAsciiDigit(token[i]))
        {
            i++;
        }

        if (i == 0 || i >= token.Length || i > 2 || token[i] is not ('h' or 'H'))
        {
            return false;
        }

        int j = i + 1;
        while (j < token.Length && char.IsAsciiDigit(token[j]))
        {
            j++;
        }

        if (j != token.Length)
        {
            return false;
        }

        hourAt = i;
        minuteAt = i + 1;
        return true;
    }

    private static bool IsSingleWordContext(List<TokenBoundary> tokens, string text, SegmentationData.ContextualSplit contextual)
    {
        if (tokens.Count == 0)
        {
            return true;
        }

        TokenBoundary previous = tokens[^1];
        ReadOnlySpan<char> word = text.AsSpan(previous.Start, previous.Length);
        bool hasLetterOrDigit = false;
        foreach (char c in word)
        {
            if (char.IsLetterOrDigit(c))
            {
                hasLetterOrDigit = true;
                break;
            }
        }

        // Punctuation closes a clause, except a percent sign ("20 % des").
        if (!hasLetterOrDigit)
        {
            return !word.SequenceEqual("%");
        }

        return contextual.SingleWordAfter.Contains(word.ToString());
    }

    // A hyphen and a pronoun at the end of the token, with a non-empty word before the hyphen: "dis-moi", "a-t-il", "est-ce".
    private bool TrySplitHyphenClitic(ReadOnlySpan<char> token, out int cut)
    {
        cut = 0;
        int hyphen = token.LastIndexOf('-');
        if (hyphen < 1 || hyphen >= token.Length - 1)
        {
            return false;
        }

        ReadOnlySpan<char> tail = token[(hyphen + 1)..];
        ReadOnlySpan<char> head = token[..hyphen];
        // "a-t-il": the euphonic t belongs to the pronoun, so the cut is at the hyphen before it.
        if (hyphen >= 3 && (head[^1] is 't' or 'T') && head[^2] == '-' && _hyphenClitics.Contains(token[(hyphen - 1)..]))
        {
            cut = hyphen - 2;
            return true;
        }

        if (_hyphenClitics.Contains(tail))
        {
            cut = hyphen;
            return true;
        }

        if (_hyphenParticles.Contains(tail))
        {
            int previous = head.LastIndexOf('-');
            ReadOnlySpan<char> stem = previous >= 0 ? head[(previous + 1)..] : head;
            if (!_hyphenParticleStems.Contains(stem))
            {
                cut = hyphen;
                return true;
            }
        }

        return false;
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
