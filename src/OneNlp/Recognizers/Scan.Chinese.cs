using System.Globalization;

namespace RecursiveAI.OneNlp.Recognizers;

/// <summary>
/// Chinese numbers, ordinals, percentages, measures and money, and the sequence set. Chinese has no spaces, so nothing here relies on
/// word boundaries: a number is read character by character, and what follows it is looked up by its longest match in the rule data.
/// </summary>
internal sealed partial class Scan
{
    /// <summary>A number read from the text. <c>Lone</c>: one numeral character (一, 十, 两), which is a number only with a counting word after it.</summary>
    private readonly record struct ZNum(int Start, int End, decimal Value, bool Lone, bool Arabic);

    private ChineseData Z => _d.Chinese;

    private void FindChinese()
    {
        FindChineseSequences();
        FindZhNumbers();
        FindZhTemporal();
    }

    // ---- sequences -------------------------------------------------------------------------------------------------------------

    // The sequence syntaxes (RFC 5322, RFC 3986, ...) treat every letter as part of a word, and Chinese characters are letters, so
    // "邮箱a@b.com联系" would read the characters around the address into it. The sequences are found in a copy of the text where the
    // CJK characters and punctuation are blanks (same offsets); mentions and hashtags, whose names may be Chinese, are read from the text.
    private void FindChineseSequences()
    {
        string masked = MaskCjk(_t);
        var inner = new Scan(_d, masked);
        inner.FindSequences();
        _found.AddRange(inner._found);
        for (int at = _t.IndexOf('@'); at >= 0; at = _t.IndexOf('@', at + 1))
        {
            ZhMention(at);
        }

        ZhHashtags();
    }

    private static string MaskCjk(string text)
    {
        char[]? chars = null;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            bool cjk = (c >= '\u2E80' && c <= '\u9FFF') || (c >= '\uFE30' && c <= '\uFE4F') || (c >= '\uFF00' && c <= '\uFFEF') || char.IsSurrogate(c);
            if (cjk)
            {
                chars ??= text.ToCharArray();
                chars[i] = ' ';
            }
        }

        return chars is null ? text : new string(chars);
    }

    private void ZhMention(int at)
    {
        string t = _t;
        if (at > 0 && (char.IsAsciiLetterOrDigit(t[at - 1]) || "_@+.".Contains(t[at - 1], StringComparison.Ordinal)))
        {
            return;
        }

        int p = at + 1;
        while (p < t.Length && p - at <= MaxLocalPart + 1)
        {
            char c = t[p];
            if (char.IsLetterOrDigit(c) || c == '_' || Lexer.IsWordChar(c))
            {
                p++;
            }
            else if ((c is '.' or '-') && p + 1 < t.Length && (char.IsLetterOrDigit(t[p + 1]) || t[p + 1] == '_'))
            {
                p++;
            }
            else
            {
                break;
            }
        }

        if (p > at + 1)
        {
            AddSequence(at, p, SequenceKind.Mention, Labels.Mention, 1f, t.Substring(at + 1, p - at - 1).ToLowerInvariant());
        }
    }

    // "#春节快乐#" (the paired form of Chinese microblogs, which is the only way to end a tag before more Chinese text), "#nlp" (an ASCII
    // tag ends at the first character that is not an ASCII letter, digit or underscore) and "#春节 " (a Chinese tag ends at a blank or punctuation).
    private void ZhHashtags()
    {
        string t = _t;
        int n = t.Length;
        for (int at = t.IndexOf('#'); at >= 0; at = at + 1 < n ? t.IndexOf('#', at + 1) : -1)
        {
            if (at > 0 && (char.IsAsciiLetterOrDigit(t[at - 1]) || "_&#/".Contains(t[at - 1], StringComparison.Ordinal)))
            {
                continue;
            }

            int close = at + 1;
            while (close < n && close - at <= 40 && IsTagChar(t[close]))
            {
                close++;
            }

            if (close < n && t[close] == '#' && close > at + 1 && HasNonDigit(at + 1, close))
            {
                AddSequence(at, close + 1, SequenceKind.Hashtag, Labels.Hashtag, 1f, t.Substring(at + 1, close - at - 1).ToLowerInvariant());
                at = close;
                continue;
            }

            int p = at + 1;
            bool ascii = p < n && t[p] < 0x80;
            while (p < n && p - at <= 100 && IsTagChar(t[p]) && (!ascii || t[p] < 0x80))
            {
                p++;
            }

            if (p > at + 1 && HasNonDigit(at + 1, p))
            {
                AddSequence(at, p, SequenceKind.Hashtag, Labels.Hashtag, 1f, t.Substring(at + 1, p - at - 1).ToLowerInvariant());
            }
        }
    }

    private static bool IsTagChar(char c) => char.IsLetterOrDigit(c) || c == '_' || Lexer.IsWordChar(c);

    private bool HasNonDigit(int from, int to)
    {
        for (int k = from; k < to; k++)
        {
            if (!char.IsDigit(_t[k]))
            {
                return true;
            }
        }

        return false;
    }

    // ---- reading numbers ---------------------------------------------------------------------------------------------------------

    private static bool IsDigitChar(char c, out int value)
    {
        if (c is >= '0' and <= '9')
        {
            value = c - '0';
            return true;
        }

        if (c is >= '\uFF10' and <= '\uFF19')
        {
            value = c - '\uFF10';
            return true;
        }

        value = 0;
        return false;
    }

    private bool IsNumeralChar(char c) => Z.Digits.ContainsKey(c) || Z.Small.ContainsKey(c) || Z.Big.ContainsKey(c);

    /// <summary>A unit or currency name that starts at a position (千克, 千米): a scale character there belongs to it, not to the number before.</summary>
    private bool UnitStartsAt(int p)
    {
        for (int len = Math.Min(4, _t.Length - p); len >= 2; len--)
        {
            if (FindSuffixAt(p, len, out _))
            {
                return true;
            }
        }

        return false;
    }

    private bool FindSuffixAt(int p, int len, out List<SuffixEntry> entries)
    {
        entries = [];
        string key = Lexer.KeyOf(_t.Substring(p, len), out int count);
        return count > 0 && _d.Suffixes.TryGet(key, out entries) && AsciiEndOk(p + len);
    }

    // A unit written in ASCII ends where the ASCII letters and digits end: "5MB的" is megabytes, "5 kg2" is no unit.
    private bool AsciiEndOk(int end) =>
        end >= _t.Length || end == 0 || !(char.IsAsciiLetterOrDigit(_t[end - 1]) && char.IsAsciiLetterOrDigit(_t[end]));

    /// <summary>The entries of the longest unit, currency name, percent sign or age word at a position (after at most one blank).</summary>
    private bool FindSuffix(int pos, out int end, out List<SuffixEntry> entries)
    {
        int p = pos;
        if (p < _t.Length && (_t[p] is ' ' or '\u00A0'))
        {
            p++;
        }

        for (int len = Math.Min(8, _t.Length - p); len >= 1; len--)
        {
            if (FindSuffixAt(p, len, out entries))
            {
                end = p + len;
                return true;
            }
        }

        end = pos;
        entries = [];
        return false;
    }

    private bool FindPrefix(int pos, out int end, out PrefixEntry prefix)
    {
        for (int len = Math.Min(6, _t.Length - pos); len >= 1; len--)
        {
            string key = Lexer.KeyOf(_t.Substring(pos, len), out int count);
            if (count > 0 && _d.Prefixes.TryGet(key, out List<PrefixEntry> found) && AsciiEndOk(pos + len))
            {
                end = pos + len;
                prefix = found[0];
                return true;
            }
        }

        end = pos;
        prefix = null!;
        return false;
    }

    private bool SignOk(int p) =>
        p == 0 || !(char.IsAsciiLetterOrDigit(_t[p - 1]) || IsNumeralChar(_t[p - 1]) || IsDigitChar(_t[p - 1], out _));

    /// <summary>Reads a number at a position: Arabic digits (with grouping, decimals and a 万/亿 scale) or a Chinese numeral, with an optional sign.</summary>
    private bool ReadNumber(int i, bool allowSign, out ZNum num)
    {
        num = default;
        int n = _t.Length;
        int p = i;
        bool negative = false;
        if (p >= n)
        {
            return false;
        }

        if (allowSign)
        {
            char c = _t[p];
            if (c is '-' or '\u2212' or '\uFF0D')
            {
                if (!SignOk(p) || p + 1 >= n)
                {
                    return false;
                }

                negative = true;
                p++;
            }
            else if (Z.Role("negative", _t, p, out int nl))
            {
                negative = true;
                p += nl;
            }
        }

        if (p >= n)
        {
            return false;
        }

        decimal value;
        bool lone = false;
        bool arabic = false;
        int end;
        if (IsDigitChar(_t[p], out _))
        {
            if (p > 0 && (char.IsAsciiLetterOrDigit(_t[p - 1]) || _t[p - 1] == '.' && p > 1 && char.IsAsciiDigit(_t[p - 2])))
            {
                return false;
            }

            if (!ReadArabic(p, out value, out end))
            {
                return false;
            }

            arabic = true;
        }
        else
        {
            if (!ParseCn(p, out end, out value, out lone))
            {
                return false;
            }

            // 三点一四: a decimal fraction, unless what follows is a clock (三点一刻, 三点十分).
            if (end < n && Z.Role("point", _t, end, out int pl))
            {
                int q = end + pl;
                var fraction = new System.Text.StringBuilder();
                while (q < n && Z.Digits.TryGetValue(_t[q], out int d) && fraction.Length < 20)
                {
                    fraction.Append((char)('0' + d));
                    q++;
                }

                bool clock = q < n && (_t[q] is '刻' or '分' or '半' or '整' or '钟' or '时');
                if (fraction.Length > 0 && !clock)
                {
                    value += decimal.Parse("0." + fraction, CultureInfo.InvariantCulture);
                    end = q;
                    lone = false;
                }
            }
        }

        // 分之: 四分之三 is three quarters (the numerator comes after the word).
        if (Z.Role("fraction", _t, end, out int fl) && ReadNumber(end + fl, false, out ZNum numerator) && value != 0)
        {
            value = numerator.Value / value;
            end = numerator.End;
            lone = false;
        }

        num = new ZNum(i, end, negative ? -value : value, lone && !negative, arabic);
        return true;
    }

    private bool ReadArabic(int p, out decimal value, out int end)
    {
        int n = _t.Length;
        var sb = new System.Text.StringBuilder();
        int q = p;
        int firstRun = 0;
        while (q < n && IsDigitChar(_t[q], out int d))
        {
            sb.Append((char)('0' + d));
            q++;
            firstRun++;
        }

        // Group separators only between groups of exactly three digits: 12,345 is one number; 1,2345 is two.
        while (firstRun <= 3 && q + 3 < n && _groupChars.Length > 0 && _groupChars.Contains(_t[q]) && _t[q] != ' '
            && IsDigitChar(_t[q + 1], out _) && IsDigitChar(_t[q + 2], out _) && IsDigitChar(_t[q + 3], out _)
            && !(q + 4 < n && IsDigitChar(_t[q + 4], out _)))
        {
            for (int k = 1; k <= 3; k++)
            {
                IsDigitChar(_t[q + k], out int d);
                sb.Append((char)('0' + d));
            }

            q += 4;
            firstRun = 3;
        }

        if (q + 1 < n && (_t[q] is '.' or '\uFF0E') && IsDigitChar(_t[q + 1], out _))
        {
            sb.Append('.');
            q++;
            while (q < n && IsDigitChar(_t[q], out int d))
            {
                sb.Append((char)('0' + d));
                q++;
            }

            // 1.2.3 and 192.168.1.1 are versions and addresses.
            if (q + 1 < n && _t[q] == '.' && char.IsAsciiDigit(_t[q + 1]))
            {
                value = 0;
                end = q;
                return false;
            }
        }

        if (sb.Length > 27 || !decimal.TryParse(sb.ToString(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value))
        {
            value = 0;
            end = q;
            return false;
        }

        // 3万, 1.5亿, 3千万, 5百: scale characters after the digits, unless they begin a unit (3千克, 5千米).
        try
        {
            decimal mult = 1;
            int e = q;
            if (e < n && Z.Small.TryGetValue(_t[e], out int sm) && sm >= 100 && !UnitStartsAt(e))
            {
                mult *= sm;
                e++;
            }

            if (e < n && Z.Big.TryGetValue(_t[e], out long bg))
            {
                mult *= bg;
                e++;
                if (e < n && Z.Big.TryGetValue(_t[e], out long bg2) && bg2 > bg)
                {
                    mult *= bg2;
                    e++;
                }
            }

            value = checked(value * mult);
            q = e;
        }
        catch (OverflowException)
        {
            end = q;
            return false;
        }

        end = q;
        return true;
    }

    /// <summary>
    /// Reads a Chinese numeral: digits and ten/hundred/thousand units grouped by ten-thousand and hundred-million (三百二十五, 一千零五, 三万五千),
    /// with the usual short forms (三万五 is 35000, 一千二 is 1200, 十二 is 12) and 零 filling a gap.
    /// </summary>
    private bool ParseCn(int i, out int end, out decimal value, out bool lone)
    {
        int n = _t.Length;
        int p = i;
        decimal total = 0;
        decimal section = 0;
        int pending = -1;
        int tokens = 0;
        bool zeroSinceUnit = false;
        bool lastWasBig = false;
        bool lastWasDigit = false;
        decimal lastUnit = 0;
        bool sawUnit = false;
        try
        {
            while (p < n && p - i < 40)
            {
                char c = _t[p];
                if (Z.Digits.TryGetValue(c, out int d))
                {
                    if (d == 0)
                    {
                        if (pending >= 0)
                        {
                            break;
                        }

                        zeroSinceUnit = true;
                    }
                    else
                    {
                        if (pending >= 0)
                        {
                            break;
                        }

                        pending = d;
                    }

                    lastWasDigit = true;
                    lastWasBig = false;
                    p++;
                    tokens++;
                    continue;
                }

                if (Z.Small.TryGetValue(c, out int sm))
                {
                    if (sm >= 100 && pending >= 0 && UnitStartsAt(p))
                    {
                        break;
                    }

                    if (pending < 0)
                    {
                        // 十二, 一百十: a bare 十 counts as 一十 at the start of a number or after a big unit.
                        if (sm == 10 && (tokens == 0 || lastWasBig))
                        {
                            pending = 1;
                        }
                        else
                        {
                            break;
                        }
                    }

                    section += pending * sm;
                    pending = -1;
                    lastUnit = sm;
                    zeroSinceUnit = false;
                    lastWasDigit = false;
                    lastWasBig = false;
                    sawUnit = true;
                    p++;
                    tokens++;
                    continue;
                }

                if (Z.Big.TryGetValue(c, out long bg))
                {
                    decimal group = section + (pending >= 0 ? pending : 0);
                    if (group == 0)
                    {
                        break;
                    }

                    total += group * bg;
                    section = 0;
                    pending = -1;
                    lastUnit = bg;
                    zeroSinceUnit = false;
                    lastWasDigit = false;
                    lastWasBig = true;
                    sawUnit = true;
                    p++;
                    tokens++;
                    continue;
                }

                break;
            }

            if (tokens == 0)
            {
                end = i;
                value = 0;
                lone = false;
                return false;
            }

            decimal rest = section;
            if (pending >= 0)
            {
                // 三万五 is 35000 and 一百五 is 150: a last digit after a hundred or more stands for the next unit down.
                rest += sawUnit && lastUnit >= 100 && !zeroSinceUnit && lastWasDigit ? pending * lastUnit / 10 : pending;
            }

            value = total + rest;
            end = p;
            lone = tokens == 1;
            return true;
        }
        catch (OverflowException)
        {
            end = i;
            value = 0;
            lone = false;
            return false;
        }
    }

    // ---- numbers, ordinals, percentages, measures, money ------------------------------------------------------------------------

    private void FindZhNumbers()
    {
        int n = _t.Length;
        int i = 0;
        while (i < n)
        {
            char c = _t[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            int next = TryZhNumberAt(i);
            i = next > i ? next : i + 1;
        }
    }

    private int TryZhNumberAt(int i)
    {
        int n = _t.Length;

        // 第三, 第12: an ordinal.
        if (Z.Role("ordinal", _t, i, out int ol) && ReadNumber(i + ol, false, out ZNum ord) && ord.Value == decimal.Truncate(ord.Value) && ord.Value >= 0)
        {
            _found.Add(MakeQuantity(i, ord.End, QuantityKind.Ordinal, ord.Value, null, null, null, 1f));
            return ord.End;
        }

        // 百分之五十, 百分之三点五: a percentage with the number after the word.
        if (Z.Role("percent", _t, i, out int pl))
        {
            int q = i + pl;
            if (Z.Role("hundred", _t, q, out int hl))
            {
                _found.Add(MakeQuantity(i, q + hl, QuantityKind.Percent, 100, "percent", "percent", null, 1f));
                return q + hl;
            }

            if (ReadNumber(q, false, out ZNum pct))
            {
                _found.Add(MakeQuantity(i, pct.End, QuantityKind.Percent, pct.Value, "percent", "percent", null, 1f));
                return pct.End;
            }
        }

        // 摄氏180度, 零下5度: a word before the number says which unit (and the sign).
        foreach ((int plen, List<ZhUnitPrefix> rows) in Z.UnitPrefixes.MatchAll(_t, i))
        {
            int q = i + plen;
            if (!ReadNumber(q, false, out ZNum value))
            {
                continue;
            }

            int bestEnd = -1;
            ZhUnitPrefix? best = null;
            foreach (ZhUnitPrefix up in rows)
            {
                foreach (string tail in up.Tails)
                {
                    int e = value.End + tail.Length;
                    if (string.CompareOrdinal(_t, value.End, tail, 0, tail.Length) == 0 && e > bestEnd)
                    {
                        bestEnd = e;
                        best = up;
                    }
                }
            }

            if (best is not null)
            {
                decimal v = best.Negate ? -value.Value : value.Value;
                _found.Add(MakeQuantity(i, bestEnd, QuantityKind.Measure, v, best.Unit, best.Dimension, null, 1f));
                return bestEnd;
            }
        }

        // ¥100, $5, 人民币100万元, RMB 500: a currency sign or name before the amount.
        if (FindPrefix(i, out int pe, out PrefixEntry prefix))
        {
            int q = pe;
            while (q < n && (_t[q] is ' ' or '\u00A0'))
            {
                q++;
            }

            if (ReadNumber(q, true, out ZNum amount))
            {
                decimal money = amount.Value;
                int end = amount.End;
                string iso = prefix.Iso;
                float conf = prefix.Confidence;
                if (FindSuffix(end, out int se, out List<SuffixEntry> suffix))
                {
                    SuffixEntry? m = suffix.FirstOrDefault(s => s.Kind == SuffixKind.Money);
                    if (m is not null)
                    {
                        money *= m.Factor;
                        end = se;
                        iso = m.Name;
                        conf = Math.Max(conf, m.Confidence);
                        end = CompoundMoney(end, m, ref money);
                    }
                }

                _found.Add(MakeQuantity(i, end, QuantityKind.Money, money, null, null, iso, conf));
                return end;
            }
        }

        if (!ReadNumber(i, true, out ZNum num))
        {
            return i;
        }

        bool emitted = false;
        if (FindSuffix(num.End, out int sufEnd, out List<SuffixEntry> entries))
        {
            foreach (SuffixEntry e in entries)
            {
                switch (e.Kind)
                {
                    case SuffixKind.Percent:
                        _found.Add(MakeQuantity(num.Start, sufEnd, QuantityKind.Percent, num.Value, "percent", "percent", null, e.Confidence));
                        emitted = true;
                        break;
                    case SuffixKind.Money:
                    {
                        // 三角形 is a triangle, not three jiao: a small unit of money is not money before these characters.
                        if (e.Factor < 1 && Z.Role("notmoney", _t, sufEnd, out _))
                        {
                            break;
                        }

                        decimal money = num.Value * e.Factor;
                        int end = CompoundMoney(sufEnd, e, ref money);
                        _found.Add(MakeQuantity(num.Start, end, QuantityKind.Money, money, null, null, e.Name, e.Confidence));
                        emitted = true;
                        break;
                    }

                    default:
                        _found.Add(MakeQuantity(num.Start, sufEnd, QuantityKind.Measure, num.Value, e.Name, e.Dimension, null, e.Confidence));
                        emitted = true;
                        break;
                }
            }
        }

        // A single numeral character (一, 三, 十, 两) is a number only before a counting word: 三个人, but not 统一 or 一些.
        if (!num.Lone || Z.Role("classifier", _t, num.End, out _))
        {
            _found.Add(MakeQuantity(num.Start, num.End, QuantityKind.Cardinal, num.Value, null, null, null, 1f));
            emitted = true;
        }

        _ = emitted;
        return num.End;
    }

    // 5元3角, 2元5角: smaller units of the same currency written after the amount are added to it.
    private int CompoundMoney(int end, SuffixEntry first, ref decimal money)
    {
        decimal factor = first.Factor;
        for (int round = 0; round < 2; round++)
        {
            if (!ReadNumber(end, false, out ZNum more) || !FindSuffix(more.End, out int se, out List<SuffixEntry> next))
            {
                break;
            }

            SuffixEntry? small = next.FirstOrDefault(s => s.Kind == SuffixKind.Money && s.Name == first.Name && s.Factor < factor);
            if (small is null)
            {
                break;
            }

            money += more.Value * small.Factor;
            factor = small.Factor;
            end = se;
        }

        return end;
    }
}
