using System.Globalization;
using System.Text;

namespace RecursiveAI.OneNlp.Tokenizers.Hf;

/// <summary>
/// A small backtracking regular-expression engine that works on Unicode code points, not UTF-16 units. The split patterns of byte-level
/// BPE tokenizers (<c>\p{L}</c> classes, optional prefixes, <c>\s+(?!\S)</c>, <c>(?i:'s|'t)</c>, possessive quantifiers) depend on a
/// character outside the Basic Multilingual Plane counting as one character and keeping its real category, which .NET's own
/// <c>Regex</c> does not do (it sees two surrogate units). Matching is leftmost, with alternatives and greedy or lazy quantifiers tried
/// in order, as in Perl-style engines such as the one the Hugging Face pre-tokenizers use for <c>Split</c>.
/// </summary>
/// <remarks>
/// Supported: literals; <c>.</c>; classes with ranges, negation, escapes and <c>\p{..}</c>; groups (capturing groups are treated as
/// plain groups); alternation; <c>? * + {n} {n,} {n,m}</c> with lazy (<c>?</c>) and possessive (<c>+</c>) forms; lookahead
/// <c>(?= )</c> and <c>(?! )</c>; atomic groups <c>(?&gt; )</c>; <c>\b \B \A \z \Z ^ $</c>; scoped flags <c>i s m</c>. Properties: general
/// categories (short and long names), scripts, <c>Alphabetic</c>, <c>White_Space</c>, <c>Any</c>. Anything else is refused at parse time.
/// Nesting is limited, and matching stops with an <see cref="InvalidOperationException"/> when it would recurse deeper than a fixed bound
/// or spend more steps than a budget that grows with the text length, so a hostile pattern cannot hang or crash the caller.
/// </remarks>
internal sealed class CodePointRegex
{
    private const int MaxPatternLength = 4096;
    private const int MaxParseDepth = 64;
    private const int MaxMatchDepth = 4000;

    private readonly Node _root;

    private CodePointRegex(Node root) => _root = root;

    /// <summary>Compiles a pattern.</summary>
    /// <param name="pattern">The pattern text.</param>
    /// <exception cref="InvalidDataException">The pattern is malformed or uses an unsupported construct.</exception>
    /// <param name="rustWordClass">Use the word class of the other regex engine tokenizers uses (it counts the joiners U+200C and U+200D as word characters).</param>
    internal static CodePointRegex Parse(string pattern, bool rustWordClass = false)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        if (pattern.Length > MaxPatternLength)
        {
            throw new InvalidDataException("A regular expression pattern is too long.");
        }

        var parser = new Parser(pattern, rustWordClass);
        return new CodePointRegex(parser.ParseAll());
    }

    /// <summary>Finds the non-empty matches, left to right, without overlap.</summary>
    /// <param name="text">The text.</param>
    /// <returns>UTF-16 ranges of the matches. Empty matches are skipped.</returns>
    internal List<OffsetPair> Matches(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = new List<OffsetPair>();
        var ctx = new Context(text);
        int pos = 0;
        while (pos <= text.Length)
        {
            int end = -1;
            ctx.Depth = 0;
            bool matched;
            try
            {
                matched = _root.Match(ctx, pos, e => { end = e; return true; });
            }
            catch (InsufficientExecutionStackException ex)
            {
                throw new InvalidOperationException("The pattern recursed too deeply on this text.", ex);
            }

            if (matched && end > pos)
            {
                result.Add(new OffsetPair(pos, end));
                pos = end;
            }
            else
            {
                if (pos >= text.Length)
                {
                    break;
                }

                pos += Width(text, pos);
            }
        }

        return result;
    }

    private static int Width(string text, int pos) => char.IsHighSurrogate(text[pos]) && pos + 1 < text.Length && char.IsLowSurrogate(text[pos + 1]) ? 2 : 1;

    private static int RuneAt(string text, int pos, out int width)
    {
        char c = text[pos];
        if (char.IsHighSurrogate(c) && pos + 1 < text.Length && char.IsLowSurrogate(text[pos + 1]))
        {
            width = 2;
            return char.ConvertToUtf32(c, text[pos + 1]);
        }

        width = 1;
        return c;
    }

    private static int RuneBefore(string text, int pos)
    {
        char c = text[pos - 1];
        if (char.IsLowSurrogate(c) && pos >= 2 && char.IsHighSurrogate(text[pos - 2]))
        {
            return char.ConvertToUtf32(text[pos - 2], c);
        }

        return c;
    }

    internal static UnicodeCategory CategoryOf(int cp) =>
        cp is >= 0xD800 and <= 0xDFFF ? UnicodeCategory.Surrogate : CharUnicodeInfo.GetUnicodeCategory(cp);

    /// <summary>The White_Space property.</summary>
    internal static bool IsWhiteSpace(int cp) =>
        cp is (>= 9 and <= 13) or 32 or 0x85 or 0xA0 or 0x1680 or (>= 0x2000 and <= 0x200A) or 0x2028 or 0x2029 or 0x202F or 0x205F or 0x3000;

    /// <summary>
    /// The word class <c>\w</c> of the engine behind the <c>Split</c> pre-tokenizer: alphabetic characters, marks, decimal digits and
    /// connector punctuation, plus the Latin-1 vulgar fractions and superscript digits, which that engine counts as word characters (observed
    /// in output of tokenizers 0.22.1). The joiners U+200C and U+200D are not word characters.
    /// </summary>
    internal static bool IsWord(int cp) => IsAlphabeticOrMark(cp) || cp is 0xB2 or 0xB3 or 0xB9 or 0xBC or 0xBD or 0xBE;

    /// <summary>The word class of the engine behind the <c>Whitespace</c> pre-tokenizer: <see cref="IsWord"/> without the Latin-1 numbers, with the joiners U+200C and U+200D.</summary>
    internal static bool IsWordRust(int cp) => IsAlphabeticOrMark(cp) || cp is 0x200C or 0x200D;

    private static bool IsAlphabeticOrMark(int cp)
    {
        switch (CategoryOf(cp))
        {
            case UnicodeCategory.UppercaseLetter:
            case UnicodeCategory.LowercaseLetter:
            case UnicodeCategory.TitlecaseLetter:
            case UnicodeCategory.ModifierLetter:
            case UnicodeCategory.OtherLetter:
            case UnicodeCategory.NonSpacingMark:
            case UnicodeCategory.SpacingCombiningMark:
            case UnicodeCategory.EnclosingMark:
            case UnicodeCategory.DecimalDigitNumber:
            case UnicodeCategory.LetterNumber:
            case UnicodeCategory.ConnectorPunctuation:
                return true;
            default:
                // Circled letters are alphabetic (Other_Alphabetic) although they are symbols.
                return cp is >= 0x24B6 and <= 0x24E9;
        }
    }

    private sealed class Context(string text)
    {
        // Work allowed for one Matches call: patterns come from files we do not trust, so a pattern such as (a+)+b on a long run of 'a'
        // must end with an error instead of running for years. Real split patterns use a few dozen steps per character.
        private long _stepsLeft = 2_000_000 + (500L * text.Length);

        internal string Text { get; } = text;

        internal int Depth { get; set; }

        internal void Tick()
        {
            if (--_stepsLeft < 0)
            {
                throw new InvalidOperationException("The pattern took too many steps on this text.");
            }
        }
    }

    private abstract class Node
    {
        internal abstract bool Match(Context c, int pos, Func<int, bool> k);
    }

    // A node that consumes exactly one code point, which lets repeats of it run in a loop instead of recursing per character.
    private abstract class Single : Node
    {
        internal abstract bool Test(int cp);

        internal sealed override bool Match(Context c, int pos, Func<int, bool> k)
        {
            if (pos >= c.Text.Length)
            {
                return false;
            }

            int cp = RuneAt(c.Text, pos, out int w);
            return Test(cp) && k(pos + w);
        }
    }

    private sealed class Pred(Func<int, bool> test) : Single
    {
        internal override bool Test(int cp) => test(cp);
    }

    private sealed class Seq(Node[] items) : Node
    {
        internal override bool Match(Context c, int pos, Func<int, bool> k) => From(c, 0, pos, k);

        private bool From(Context c, int i, int pos, Func<int, bool> k)
        {
            if (i == items.Length)
            {
                return k(pos);
            }

            return items[i].Match(c, pos, p => From(c, i + 1, p, k));
        }
    }

    private sealed class Alt(Node[] options) : Node
    {
        internal override bool Match(Context c, int pos, Func<int, bool> k)
        {
            foreach (Node o in options)
            {
                c.Tick();
                if (o.Match(c, pos, k))
                {
                    return true;
                }
            }

            return false;
        }
    }

    private sealed class Repeat(Node body, int min, int max, bool greedy, bool possessive) : Node
    {
        internal override bool Match(Context c, int pos, Func<int, bool> k)
        {
            if (body is Single s)
            {
                return MatchSingle(c, s, pos, k);
            }

            if (possessive)
            {
                int end = -1;
                if (!Run(c, pos, 0, e => { end = e; return true; }))
                {
                    return false;
                }

                return k(end);
            }

            return Run(c, pos, 0, k);
        }

        private bool MatchSingle(Context c, Single s, int pos, Func<int, bool> k)
        {
            if (greedy || possessive)
            {
                var ends = new List<int>();
                int p = pos;
                while (ends.Count < max && p < c.Text.Length)
                {
                    int cp = RuneAt(c.Text, p, out int w);
                    if (!s.Test(cp))
                    {
                        break;
                    }

                    p += w;
                    ends.Add(p);
                }

                if (ends.Count < min)
                {
                    return false;
                }

                if (possessive)
                {
                    return k(ends.Count == 0 ? pos : ends[^1]);
                }

                for (int n = ends.Count; n >= min; n--)
                {
                    c.Tick();
                    if (k(n == 0 ? pos : ends[n - 1]))
                    {
                        return true;
                    }
                }

                return false;
            }

            // Lazy: extend one character at a time.
            int q = pos;
            int count = 0;
            while (true)
            {
                c.Tick();
                if (count >= min && k(q))
                {
                    return true;
                }

                if (count >= max || q >= c.Text.Length)
                {
                    return false;
                }

                int cp = RuneAt(c.Text, q, out int w);
                if (!s.Test(cp))
                {
                    return false;
                }

                q += w;
                count++;
            }
        }

        private bool Run(Context c, int pos, int count, Func<int, bool> k)
        {
            c.Tick();
            if (++c.Depth > MaxMatchDepth)
            {
                throw new InvalidOperationException("The pattern recursed too deeply on this text.");
            }

            try
            {
                if (greedy || possessive)
                {
                    System.Runtime.CompilerServices.RuntimeHelpers.EnsureSufficientExecutionStack();
                    if (count < max && body.Match(c, pos, p => (p != pos || count < min) && Run(c, p, count + 1, k)))
                    {
                        return true;
                    }

                    return count >= min && k(pos);
                }

                if (count >= min && k(pos))
                {
                    return true;
                }

                return count < max && body.Match(c, pos, p => (p != pos || count < min) && Run(c, p, count + 1, k));
            }
            finally
            {
                c.Depth--;
            }
        }
    }

    private sealed class Atomic(Node body) : Node
    {
        internal override bool Match(Context c, int pos, Func<int, bool> k)
        {
            int end = -1;
            return body.Match(c, pos, e => { end = e; return true; }) && k(end);
        }
    }

    private sealed class Look(Node body, bool negative) : Node
    {
        internal override bool Match(Context c, int pos, Func<int, bool> k)
        {
            bool found = body.Match(c, pos, _ => true);
            return found != negative && k(pos);
        }
    }

    private sealed class Anchor(char kind, bool multiline) : Node
    {
        internal override bool Match(Context c, int pos, Func<int, bool> k)
        {
            string t = c.Text;
            bool ok = kind switch
            {
                '^' => pos == 0 || (multiline && t[pos - 1] == '\n'),
                '$' => pos == t.Length || (multiline && t[pos] == '\n'),
                'A' => pos == 0,
                'z' => pos == t.Length,
                'Z' => pos == t.Length || (pos == t.Length - 1 && t[pos] == '\n'),
                'b' => IsBoundary(t, pos),
                'B' => !IsBoundary(t, pos),
                _ => false,
            };
            return ok && k(pos);
        }

        private static bool IsBoundary(string t, int pos)
        {
            bool before = pos > 0 && IsWord(RuneBefore(t, pos));
            bool after = pos < t.Length && IsWord(RuneAt(t, pos, out _));
            return before != after;
        }
    }

    private sealed class Empty : Node
    {
        internal override bool Match(Context c, int pos, Func<int, bool> k) => k(pos);
    }

    private sealed class Parser(string pattern, bool rustWordClass)
    {
        private int _i;
        private int _depth;

        internal Node ParseAll()
        {
            Node n = ParseAlt(new Flags(false, false, false));
            if (_i < pattern.Length)
            {
                throw Fail("unbalanced ')'");
            }

            return n;
        }

        private InvalidDataException Fail(string why) => new($"Unsupported or malformed regular expression '{pattern}': {why}.");

        private Node ParseAlt(Flags flags)
        {
            if (++_depth > MaxParseDepth)
            {
                throw Fail("nesting is too deep");
            }

            var options = new List<Node>();
            var current = new List<Node>();
            while (_i < pattern.Length && pattern[_i] != ')')
            {
                if (pattern[_i] == '|')
                {
                    _i++;
                    options.Add(MakeSeq(current));
                    current = [];
                    continue;
                }

                // An inline flag group like (?i) changes the flags for the rest of the enclosing group.
                if (TryInlineFlags(ref flags))
                {
                    continue;
                }

                Node atom = ParseAtom(flags);
                current.Add(ParseQuantifier(atom));
            }

            _depth--;
            if (options.Count == 0)
            {
                return MakeSeq(current);
            }

            options.Add(MakeSeq(current));
            return new Alt([.. options]);
        }

        private static Node MakeSeq(List<Node> items) => items.Count switch
        {
            0 => new Empty(),
            1 => items[0],
            _ => new Seq([.. items]),
        };

        private bool TryInlineFlags(ref Flags flags)
        {
            if (_i + 2 >= pattern.Length || pattern[_i] != '(' || pattern[_i + 1] != '?')
            {
                return false;
            }

            int j = _i + 2;
            Flags f = flags;
            bool on = true;
            while (j < pattern.Length && pattern[j] != ')' && pattern[j] != ':')
            {
                switch (pattern[j])
                {
                    case '-': on = false; break;
                    case 'i': f = f with { IgnoreCase = on }; break;
                    case 's': f = f with { DotAll = on }; break;
                    case 'm': f = f with { Multiline = on }; break;
                    default: return false;
                }

                j++;
            }

            if (j < pattern.Length && pattern[j] == ')' && j > _i + 2)
            {
                flags = f;
                _i = j + 1;
                return true;
            }

            return false;
        }

        private Node ParseQuantifier(Node atom)
        {
            while (_i < pattern.Length)
            {
                int min;
                int max;
                char c = pattern[_i];
                if (c == '*')
                {
                    (min, max) = (0, int.MaxValue);
                    _i++;
                }
                else if (c == '+')
                {
                    (min, max) = (1, int.MaxValue);
                    _i++;
                }
                else if (c == '?')
                {
                    (min, max) = (0, 1);
                    _i++;
                }
                else if (c == '{' && TryBraces(out min, out max))
                {
                }
                else
                {
                    break;
                }

                bool greedy = true;
                bool possessive = false;
                if (_i < pattern.Length && pattern[_i] == '?')
                {
                    greedy = false;
                    _i++;
                }
                else if (_i < pattern.Length && pattern[_i] == '+')
                {
                    possessive = true;
                    _i++;
                }

                atom = new Repeat(atom, min, max, greedy, possessive);
            }

            return atom;
        }

        private bool TryBraces(out int min, out int max)
        {
            min = max = 0;
            int j = _i + 1;
            int start = j;
            while (j < pattern.Length && char.IsAsciiDigit(pattern[j]))
            {
                j++;
            }

            if (j == start || j - start > 4)
            {
                return false;
            }

            min = int.Parse(pattern.AsSpan(start, j - start), CultureInfo.InvariantCulture);
            max = min;
            if (j < pattern.Length && pattern[j] == ',')
            {
                j++;
                int s2 = j;
                while (j < pattern.Length && char.IsAsciiDigit(pattern[j]))
                {
                    j++;
                }

                if (j - s2 > 4)
                {
                    return false;
                }

                max = j == s2 ? int.MaxValue : int.Parse(pattern.AsSpan(s2, j - s2), CultureInfo.InvariantCulture);
            }

            if (j >= pattern.Length || pattern[j] != '}' || max < min)
            {
                return false;
            }

            _i = j + 1;
            return true;
        }

        private Node ParseAtom(Flags flags)
        {
            char c = pattern[_i];
            switch (c)
            {
                case '(':
                    return ParseGroup(flags);
                case '[':
                    return ParseClass(flags);
                case '.':
                    _i++;
                    return new Pred(x => flags.DotAll || x != '\n');
                case '^':
                case '$':
                    _i++;
                    return new Anchor(c, flags.Multiline);
                case '\\':
                    return ParseEscapeAtom(flags);
                case '*':
                case '+':
                case '?':
                    throw Fail("nothing to repeat");
                default:
                    int cp = ReadLiteral();
                    return Literal(cp, flags);
            }
        }

        private int ReadLiteral()
        {
            int cp = RuneAt(pattern, _i, out int w);
            _i += w;
            return cp;
        }

        private static Pred Literal(int cp, Flags flags) => flags.IgnoreCase
            ? new Pred(x => x == cp || Fold(x) == Fold(cp))
            : new Pred(x => x == cp);

        private static int Fold(int cp) => cp < 0x10000 && !(cp is >= 0xD800 and <= 0xDFFF) ? char.ToLowerInvariant((char)cp) : Rune.IsValid(cp) ? Rune.ToLowerInvariant(new Rune(cp)).Value : cp;

        private Node ParseGroup(Flags flags)
        {
            _i++;
            Node inner;
            if (_i < pattern.Length && pattern[_i] == '?')
            {
                _i++;
                if (_i >= pattern.Length)
                {
                    throw Fail("incomplete group");
                }

                char k = pattern[_i];
                if (k == ':')
                {
                    _i++;
                    inner = ParseAlt(flags);
                }
                else if (k == '=' || k == '!')
                {
                    _i++;
                    inner = new Look(ParseAlt(flags), k == '!');
                }
                else if (k == '>')
                {
                    _i++;
                    inner = new Atomic(ParseAlt(flags));
                }
                else if (k == '<' && _i + 1 < pattern.Length && pattern[_i + 1] is not ('=' or '!'))
                {
                    int close = pattern.IndexOf('>', _i);
                    if (close < 0)
                    {
                        throw Fail("unterminated group name");
                    }

                    _i = close + 1;
                    inner = ParseAlt(flags);
                }
                else
                {
                    // (?i:...) and friends.
                    Flags f = flags;
                    bool on = true;
                    while (_i < pattern.Length && pattern[_i] != ':')
                    {
                        switch (pattern[_i])
                        {
                            case '-': on = false; break;
                            case 'i': f = f with { IgnoreCase = on }; break;
                            case 's': f = f with { DotAll = on }; break;
                            case 'm': f = f with { Multiline = on }; break;
                            default: throw Fail($"unsupported group construct '(?{pattern[_i]}'");
                        }

                        _i++;
                    }

                    if (_i >= pattern.Length)
                    {
                        throw Fail("incomplete group");
                    }

                    _i++;
                    inner = ParseAlt(f);
                }
            }
            else
            {
                inner = ParseAlt(flags);
            }

            if (_i >= pattern.Length || pattern[_i] != ')')
            {
                throw Fail("missing ')'");
            }

            _i++;
            return inner;
        }

        private Node ParseEscapeAtom(Flags flags)
        {
            _i++;
            if (_i >= pattern.Length)
            {
                throw Fail("trailing backslash");
            }

            char e = pattern[_i];
            if (e is 'A' or 'z' or 'Z' or 'b' or 'B')
            {
                _i++;
                return new Anchor(e, false);
            }

            Func<int, bool>? set = TryClassEscape();
            if (set is not null)
            {
                return new Pred(set);
            }

            int cp = ReadEscapedLiteral();
            return Literal(cp, flags);
        }

        // \d \D \s \S \w \W \p{..} \P{..}; returns null (without consuming) for anything else.
        private Func<int, bool>? TryClassEscape()
        {
            if (_i >= pattern.Length)
            {
                throw Fail("trailing backslash");
            }

            char e = pattern[_i];
            switch (e)
            {
                case 'd': _i++; return cp => CategoryOf(cp) == UnicodeCategory.DecimalDigitNumber;
                case 'D': _i++; return cp => CategoryOf(cp) != UnicodeCategory.DecimalDigitNumber;
                case 's': _i++; return IsWhiteSpace;
                case 'S': _i++; return cp => !IsWhiteSpace(cp);
                case 'w': _i++; return rustWordClass ? IsWordRust : IsWord;
                case 'W': _i++; return rustWordClass ? (cp => !IsWordRust(cp)) : (cp => !IsWord(cp));
                case 'p':
                case 'P':
                    {
                        _i++;
                        bool negate = e == 'P';
                        string name;
                        if (_i < pattern.Length && pattern[_i] == '{')
                        {
                            int close = pattern.IndexOf('}', _i);
                            if (close < 0)
                            {
                                throw Fail("unterminated \\p{");
                            }

                            name = pattern.Substring(_i + 1, close - _i - 1);
                            _i = close + 1;
                        }
                        else if (_i < pattern.Length)
                        {
                            name = pattern[_i++].ToString();
                        }
                        else
                        {
                            throw Fail("incomplete \\p");
                        }

                        if (name.StartsWith('^'))
                        {
                            negate = !negate;
                            name = name[1..];
                        }

                        Func<int, bool> prop = Property(name);
                        return negate ? cp => !prop(cp) : prop;
                    }

                default:
                    return null;
            }
        }

        private int ReadEscapedLiteral()
        {
            if (_i >= pattern.Length)
            {
                throw Fail("trailing backslash");
            }

            char e = pattern[_i++];
            switch (e)
            {
                case 'n': return '\n';
                case 'r': return '\r';
                case 't': return '\t';
                case 'f': return '\f';
                case 'v': return '\v';
                case 'a': return 7;
                case 'e': return 27;
                case '0': return 0;
                case 'x': return ReadHex(2, allowBraces: true);
                case 'u': return ReadHex(4, allowBraces: true);
                default:
                    if (char.IsAsciiLetterOrDigit(e))
                    {
                        throw Fail($"unsupported escape '\\{e}'");
                    }

                    _i--;
                    return ReadLiteral();
            }
        }

        private int ReadHex(int digits, bool allowBraces)
        {
            int start = _i;
            if (allowBraces && _i < pattern.Length && pattern[_i] == '{')
            {
                int close = pattern.IndexOf('}', _i);
                if (close < 0)
                {
                    throw Fail("unterminated hex escape");
                }

                string h = pattern.Substring(_i + 1, close - _i - 1);
                _i = close + 1;
                return ParseHex(h);
            }

            if (_i + digits > pattern.Length)
            {
                throw Fail("short hex escape");
            }

            _i += digits;
            return ParseHex(pattern.Substring(start, digits));
        }

        private int ParseHex(string h)
        {
            if (h.Length is 0 or > 8 || !int.TryParse(h, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int v) || v is < 0 or > 0x10FFFF)
            {
                throw Fail("bad hex escape");
            }

            return v;
        }

        private Pred ParseClass(Flags flags)
        {
            _i++;
            bool negated = false;
            if (_i < pattern.Length && pattern[_i] == '^')
            {
                negated = true;
                _i++;
            }

            var tests = new List<Func<int, bool>>();
            bool first = true;
            while (true)
            {
                if (_i >= pattern.Length)
                {
                    throw Fail("unterminated class");
                }

                char c = pattern[_i];
                if (c == ']' && !first)
                {
                    _i++;
                    break;
                }

                first = false;
                if (c == '[')
                {
                    // A nested class such as [a-z[0-9]] is a union.
                    Pred nested = ParseClass(flags);
                    tests.Add(nested.Test);
                    continue;
                }

                int lo;
                if (c == '\\')
                {
                    _i++;
                    if (_i >= pattern.Length)
                    {
                        throw Fail("trailing backslash");
                    }

                    Func<int, bool>? set = TryClassEscape();
                    if (set is not null)
                    {
                        tests.Add(set);
                        continue;
                    }

                    lo = pattern[_i] == 'b' ? ReadBackspace() : ReadEscapedLiteral();
                }
                else
                {
                    lo = ReadLiteral();
                }

                int hi = lo;
                if (_i + 1 < pattern.Length && pattern[_i] == '-' && pattern[_i + 1] != ']')
                {
                    _i++;
                    if (pattern[_i] == '\\')
                    {
                        _i++;
                        if (TryClassEscape() is not null)
                        {
                            throw Fail("a class escape cannot end a range");
                        }

                        hi = ReadEscapedLiteral();
                    }
                    else
                    {
                        hi = ReadLiteral();
                    }

                    if (hi < lo)
                    {
                        throw Fail("reversed range");
                    }
                }

                int l = lo;
                int h = hi;
                if (flags.IgnoreCase)
                {
                    tests.Add(x => (x >= l && x <= h) || (Fold(x) >= l && Fold(x) <= h) || (Upper(x) >= l && Upper(x) <= h));
                }
                else
                {
                    tests.Add(x => x >= l && x <= h);
                }
            }

            Func<int, bool>[] all = [.. tests];
            return new Pred(cp =>
            {
                bool any = false;
                foreach (Func<int, bool> t in all)
                {
                    if (t(cp))
                    {
                        any = true;
                        break;
                    }
                }

                return any != negated;
            });
        }

        private int ReadBackspace()
        {
            _i++;
            return 8;
        }

        private static int Upper(int cp) => Rune.IsValid(cp) ? Rune.ToUpperInvariant(new Rune(cp)).Value : cp;

        private Func<int, bool> Property(string name)
        {
            string key = name.Replace("_", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
            if (key.StartsWith("SCRIPT=", StringComparison.Ordinal))
            {
                key = key[7..];
            }
            else if (key.StartsWith("GENERALCATEGORY=", StringComparison.Ordinal) || key.StartsWith("GC=", StringComparison.Ordinal))
            {
                key = key[(key.IndexOf('=', StringComparison.Ordinal) + 1)..];
            }

            switch (key)
            {
                case "ANY": return _ => true;
                case "ALPHABETIC": return cp => CategoryOf(cp) is UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter or UnicodeCategory.LetterNumber;
                case "WHITESPACE": return IsWhiteSpace;
                case "LC": case "CASEDLETTER": return cp => CategoryOf(cp) is UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter;
            }

            UnicodeCategory[]? cats = key switch
            {
                "L" or "LETTER" => [UnicodeCategory.UppercaseLetter, UnicodeCategory.LowercaseLetter, UnicodeCategory.TitlecaseLetter, UnicodeCategory.ModifierLetter, UnicodeCategory.OtherLetter],
                "LU" or "UPPERCASELETTER" => [UnicodeCategory.UppercaseLetter],
                "LL" or "LOWERCASELETTER" => [UnicodeCategory.LowercaseLetter],
                "LT" or "TITLECASELETTER" => [UnicodeCategory.TitlecaseLetter],
                "LM" or "MODIFIERLETTER" => [UnicodeCategory.ModifierLetter],
                "LO" or "OTHERLETTER" => [UnicodeCategory.OtherLetter],
                "M" or "MARK" or "COMBININGMARK" => [UnicodeCategory.NonSpacingMark, UnicodeCategory.SpacingCombiningMark, UnicodeCategory.EnclosingMark],
                "MN" or "NONSPACINGMARK" => [UnicodeCategory.NonSpacingMark],
                "MC" or "SPACINGMARK" => [UnicodeCategory.SpacingCombiningMark],
                "ME" or "ENCLOSINGMARK" => [UnicodeCategory.EnclosingMark],
                "N" or "NUMBER" => [UnicodeCategory.DecimalDigitNumber, UnicodeCategory.LetterNumber, UnicodeCategory.OtherNumber],
                "ND" or "DECIMALNUMBER" => [UnicodeCategory.DecimalDigitNumber],
                "NL" or "LETTERNUMBER" => [UnicodeCategory.LetterNumber],
                "NO" or "OTHERNUMBER" => [UnicodeCategory.OtherNumber],
                "P" or "PUNCTUATION" => [UnicodeCategory.ConnectorPunctuation, UnicodeCategory.DashPunctuation, UnicodeCategory.OpenPunctuation, UnicodeCategory.ClosePunctuation, UnicodeCategory.InitialQuotePunctuation, UnicodeCategory.FinalQuotePunctuation, UnicodeCategory.OtherPunctuation],
                "PC" or "CONNECTORPUNCTUATION" => [UnicodeCategory.ConnectorPunctuation],
                "PD" or "DASHPUNCTUATION" => [UnicodeCategory.DashPunctuation],
                "PS" or "OPENPUNCTUATION" => [UnicodeCategory.OpenPunctuation],
                "PE" or "CLOSEPUNCTUATION" => [UnicodeCategory.ClosePunctuation],
                "PI" or "INITIALPUNCTUATION" => [UnicodeCategory.InitialQuotePunctuation],
                "PF" or "FINALPUNCTUATION" => [UnicodeCategory.FinalQuotePunctuation],
                "PO" or "OTHERPUNCTUATION" => [UnicodeCategory.OtherPunctuation],
                "S" or "SYMBOL" => [UnicodeCategory.MathSymbol, UnicodeCategory.CurrencySymbol, UnicodeCategory.ModifierSymbol, UnicodeCategory.OtherSymbol],
                "SM" or "MATHSYMBOL" => [UnicodeCategory.MathSymbol],
                "SC" or "CURRENCYSYMBOL" => [UnicodeCategory.CurrencySymbol],
                "SK" or "MODIFIERSYMBOL" => [UnicodeCategory.ModifierSymbol],
                "SO" or "OTHERSYMBOL" => [UnicodeCategory.OtherSymbol],
                "Z" or "SEPARATOR" => [UnicodeCategory.SpaceSeparator, UnicodeCategory.LineSeparator, UnicodeCategory.ParagraphSeparator],
                "ZS" or "SPACESEPARATOR" => [UnicodeCategory.SpaceSeparator],
                "ZL" or "LINESEPARATOR" => [UnicodeCategory.LineSeparator],
                "ZP" or "PARAGRAPHSEPARATOR" => [UnicodeCategory.ParagraphSeparator],
                "C" or "OTHER" => [UnicodeCategory.Control, UnicodeCategory.Format, UnicodeCategory.Surrogate, UnicodeCategory.PrivateUse, UnicodeCategory.OtherNotAssigned],
                "CC" or "CONTROL" => [UnicodeCategory.Control],
                "CF" or "FORMAT" => [UnicodeCategory.Format],
                "CS" or "SURROGATE" => [UnicodeCategory.Surrogate],
                "CO" or "PRIVATEUSE" => [UnicodeCategory.PrivateUse],
                "CN" or "UNASSIGNED" => [UnicodeCategory.OtherNotAssigned],
                _ => null,
            };
            if (cats is not null)
            {
                return cp => Array.IndexOf(cats, CategoryOf(cp)) >= 0;
            }

            for (int s = 0; s < ScriptData.Names.Length; s++)
            {
                if (string.Equals(ScriptData.Names[s].Replace("_", string.Empty, StringComparison.Ordinal), key, StringComparison.OrdinalIgnoreCase))
                {
                    int id = s;
                    return cp => ScriptData.ScriptOf(cp) == id;
                }
            }

            throw Fail($"unknown property '{name}'");
        }
    }

    private readonly record struct Flags(bool IgnoreCase, bool DotAll, bool Multiline);
}

/// <summary>Lookup in the generated script table.</summary>
internal static partial class ScriptData
{
    /// <summary>Gets the script of a code point as an index into <see cref="Names"/> (0 is Unknown).</summary>
    /// <param name="cp">The code point.</param>
    internal static int ScriptOf(int cp)
    {
        ReadOnlySpan<int> starts = Starts;
        int lo = 0;
        int hi = starts.Length - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >>> 1;
            if (cp < starts[mid])
            {
                hi = mid - 1;
            }
            else if (cp > Ends[mid])
            {
                lo = mid + 1;
            }
            else
            {
                return ScriptIds[mid];
            }
        }

        return 0;
    }
}
