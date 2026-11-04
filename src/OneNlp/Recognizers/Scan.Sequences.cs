using System.Globalization;
using System.Text;

namespace RecursiveAI.OneNlp.Recognizers;

// The Sequence recognizers: e-mail addresses, URLs, IP addresses, phone-like digit groups, hashtags, mentions and GUIDs. Their shape is
// fixed by the standards cited on each method, not by language, so they are code; what varies (URL schemes, domain endings) is rule data.
internal sealed partial class Scan
{
    private const int MaxLocalPart = 64;
    private const int MaxDomain = 253;
    private const int MaxLabel = 63;
    private const int MaxUrl = 2048;

    private void FindSequences()
    {
        FindAtSigns();
        FindHashtags();
        FindUrls();
        FindGuids();
        FindIPv4();
        FindIPv6();
        FindPhones();
    }

    private void AddSequence(int start, int end, SequenceKind kind, string label, float confidence, string normalized) =>
        _found.Add(new SequenceMatch(start, end, _t.Substring(start, end - start), label, confidence, kind, normalized));

    // ---- e-mail and mentions ------------------------------------------------------------------------------------------------

    private void FindAtSigns()
    {
        for (int at = _t.IndexOf('@'); at >= 0; at = _t.IndexOf('@', at + 1))
        {
            TryEmail(at);
            TryMention(at);
        }
    }

    // RFC 5322 §3.4.1 addr-spec, simplified: a dot-atom local part (no quoted strings, comments or folding) of at most 64 characters
    // (RFC 5321 §4.5.3.1.1), then '@', then a domain of dot-separated labels whose last label is a known domain ending. Non-ASCII letters
    // and digits are allowed in both parts (RFC 6531).
    private void TryEmail(int at)
    {
        string t = _t;
        int l = at;
        while (l > 0 && IsAtext(t[l - 1]) && at - l <= MaxLocalPart + 8)
        {
            l--;
        }

        while (l < at && !(char.IsLetterOrDigit(t[l]) || t[l] == '_'))
        {
            l++;
        }

        if (l == at || at - l > MaxLocalPart || t[at - 1] == '.' || t.AsSpan(l, at - l).Contains("..", StringComparison.Ordinal))
        {
            return;
        }

        if (!TryDomain(at + 1, true, out int end))
        {
            return;
        }

        string normalized = string.Concat(t.AsSpan(l, at - l), "@", t.Substring(at + 1, end - at - 1).ToLowerInvariant());
        AddSequence(l, end, SequenceKind.Email, Labels.Email, 1f, normalized);
    }

    /// <summary>Reads a domain name at <paramref name="start"/>: labels of host characters joined by dots, ending in a known domain ending.</summary>
    private bool TryDomain(int start, bool requireTld, out int end)
    {
        string t = _t;
        int n = t.Length;
        int r = start;
        while (r < n && r - start < MaxDomain + 1 && (IsHostChar(t[r]) || t[r] == '.'))
        {
            r++;
        }

        while (r > start && (t[r - 1] == '.' || t[r - 1] == '-'))
        {
            r--;
        }

        end = r;
        if (r - start is 0 or > MaxDomain)
        {
            return false;
        }

        int labels = 0;
        int ls = start;
        for (int i = start; i <= r; i++)
        {
            if (i == r || t[i] == '.')
            {
                int len = i - ls;
                if (len is 0 or > MaxLabel || t[ls] == '-' || t[i - 1] == '-')
                {
                    return false;
                }

                labels++;
                if (i == r && requireTld)
                {
                    string tld = t.Substring(ls, len).ToLowerInvariant();
                    if (labels < 2 || !(_d.Tlds.Contains(tld) || tld.StartsWith("xn--", StringComparison.Ordinal)))
                    {
                        return false;
                    }
                }

                ls = i + 1;
            }
        }

        return true;
    }

    private void TryMention(int at)
    {
        string t = _t;
        int n = t.Length;
        if (at > 0 && (char.IsLetterOrDigit(t[at - 1]) || "_@+.".Contains(t[at - 1], StringComparison.Ordinal)))
        {
            return;
        }

        int p = at + 1;
        while (p < n && p - at <= MaxLocalPart + 1)
        {
            char c = t[p];
            if (char.IsLetterOrDigit(c) || c == '_' || Lexer.IsWordChar(c))
            {
                p++;
            }
            else if ((c is '.' or '-') && p + 1 < n && (char.IsLetterOrDigit(t[p + 1]) || t[p + 1] == '_'))
            {
                p++;
            }
            else
            {
                break;
            }
        }

        if (p == at + 1)
        {
            return;
        }

        // A fediverse handle: @user@host.example
        if (p < n && t[p] == '@' && TryDomain(p + 1, true, out int domainEnd))
        {
            p = domainEnd;
        }

        AddSequence(at, p, SequenceKind.Mention, Labels.Mention, 1f, t.Substring(at + 1, p - at - 1).ToLowerInvariant());
    }

    private static bool IsAtext(char c) =>
        c < 0x80 ? (char.IsAsciiLetterOrDigit(c) || "!#$%&'*+-/=?^_`{|}~.".Contains(c, StringComparison.Ordinal)) : char.IsLetterOrDigit(c);

    private static bool IsHostChar(char c) => c < 0x80 ? (char.IsAsciiLetterOrDigit(c) || c == '-') : Lexer.IsWordChar(c) || char.IsDigit(c);

    // ---- hashtags -----------------------------------------------------------------------------------------------------------

    private void FindHashtags()
    {
        string t = _t;
        int n = t.Length;
        for (int at = t.IndexOf('#'); at >= 0; at = t.IndexOf('#', at + 1))
        {
            if (at > 0 && (char.IsLetterOrDigit(t[at - 1]) || "_&#/".Contains(t[at - 1], StringComparison.Ordinal)))
            {
                continue;
            }

            int p = at + 1;
            bool nonDigit = false;
            while (p < n && p - at <= 100 && (char.IsLetterOrDigit(t[p]) || t[p] == '_' || Lexer.IsWordChar(t[p]) || t[p] == '\u200C'))
            {
                nonDigit |= !char.IsDigit(t[p]);
                p++;
            }

            // "#1" and "#2024" are numbers, not tags.
            if (p > at + 1 && nonDigit)
            {
                AddSequence(at, p, SequenceKind.Hashtag, Labels.Hashtag, 1f, t.Substring(at + 1, p - at - 1).ToLowerInvariant());
            }
        }
    }

    // ---- URLs ---------------------------------------------------------------------------------------------------------------

    private void FindUrls()
    {
        string t = _t;
        int n = t.Length;
        int i = 0;
        while (i < n)
        {
            if (char.IsWhiteSpace(t[i]) || t[i] is '<' or '>' or '"')
            {
                i++;
                continue;
            }

            int e = i;
            while (e < n && !char.IsWhiteSpace(t[e]) && t[e] is not ('<' or '>' or '"'))
            {
                e++;
            }

            UrlsInChunk(i, e);
            i = e;
        }
    }

    private void UrlsInChunk(int chunkStart, int chunkEnd)
    {
        string t = _t;
        int s = chunkStart;
        while (s < chunkEnd)
        {
            bool wordStart = char.IsLetterOrDigit(t[s]) && (s == chunkStart || !(IsHostChar(t[s - 1]) || t[s - 1] is '.' or '@'));
            if (wordStart && TryUrl(s, chunkEnd, out int end))
            {
                AddSequence(s, end, SequenceKind.Url, Labels.Url, 1f, t.Substring(s, end - s));
                s = end;
                continue;
            }

            s++;
        }
    }

    // RFC 3986 §3: scheme ":" "//" authority [path] ["?" query] ["#" fragment], with the scheme from the rule data (an unknown scheme
    // is not reported, since "note://this" is more likely prose). Without a scheme, WHATWG-style addresses are taken when they start
    // with "www." or when a known domain ending is followed by a port or a path. A trailing "." "," ")" and the like are not part of it
    // (RFC 3986 Appendix C).
    private bool TryUrl(int s, int limit, out int end)
    {
        string t = _t;
        end = s;
        int p = s;
        while (p < limit && (char.IsAsciiLetterOrDigit(t[p]) || t[p] is '+' or '-' or '.'))
        {
            p++;
        }

        int hostStart;
        int floor;
        bool schemeForm = false;
        if (p + 2 < limit && p > s && t[p] == ':' && t[p + 1] == '/' && t[p + 2] == '/' && char.IsAsciiLetter(t[s])
            && _d.Schemes.Contains(t.Substring(s, p - s).ToLowerInvariant()))
        {
            schemeForm = true;
            int a = p + 3;
            int authEnd = a;
            while (authEnd < limit && t[authEnd] is not ('/' or '?' or '#'))
            {
                authEnd++;
            }

            int at = authEnd - 1;
            while (at >= a && t[at] != '@')
            {
                at--;
            }

            hostStart = at >= a ? at + 1 : a;
        }
        else
        {
            hostStart = s;
        }

        int hostEnd;
        if (schemeForm && hostStart < limit && t[hostStart] == '/' && hostStart == p + 3)
        {
            // "file:///tmp/x": an empty authority (RFC 3986 section 3.2).
            hostEnd = hostStart;
        }
        else if (hostStart < limit && t[hostStart] == '[')
        {
            // "[2001:db8::1]"
            int close = t.IndexOf(']', hostStart, Math.Min(limit - hostStart, 47));
            if (!schemeForm || close < 0 || !TryParseIPv6(t.AsSpan(hostStart + 1, close - hostStart - 1), out _))
            {
                return false;
            }

            hostEnd = close + 1;
        }
        else
        {
            if (!TryHostName(hostStart, limit, out hostEnd, out int labels, out int lastLabel))
            {
                return false;
            }

            string last = t.Substring(lastLabel, hostEnd - lastLabel).ToLowerInvariant();
            if (!schemeForm)
            {
                bool www = hostEnd - hostStart > 4 && string.Compare(t, hostStart, "www.", 0, 4, StringComparison.OrdinalIgnoreCase) == 0;
                if (www)
                {
                    if (labels < 3 || !_d.Tlds.Contains(last))
                    {
                        return false;
                    }
                }
                else
                {
                    if (labels < 2 || !_d.BareTlds.Contains(last))
                    {
                        return false;
                    }
                }
            }
        }

        floor = hostEnd;
        int q = hostEnd;
        if (q + 1 < limit && t[q] == ':' && char.IsAsciiDigit(t[q + 1]))
        {
            int digits = q + 1;
            while (digits < limit && char.IsAsciiDigit(t[digits]) && digits - q <= 5)
            {
                digits++;
            }

            if (digits >= limit || !char.IsAsciiDigit(t[digits]))
            {
                q = digits;
                floor = q;
            }
        }

        if (schemeForm && floor < limit && t[floor] is not ('/' or '?' or '#') && IsHostChar(t[floor]))
        {
            return false;
        }

        while (q < limit && q - s < MaxUrl && IsUrlChar(t[q]) && (q == floor ? t[q] is '/' or '?' or '#' : true))
        {
            q++;
        }

        // Trailing punctuation belongs to the sentence, not the address; a closing bracket stays when its opening one is inside.
        while (q > floor)
        {
            char c = t[q - 1];
            if (c is '!' or '\'' or '*' or ',' or '.' or ':' or ';' or '?')
            {
                q--;
            }
            else if (c == ')' && Count(s, q, '(') < Count(s, q, ')'))
            {
                q--;
            }
            else if (c == ']' && Count(s, q, '[') < Count(s, q, ']'))
            {
                q--;
            }
            else
            {
                break;
            }
        }

        end = q;
        return end > s;
    }

    private int Count(int from, int to, char c)
    {
        int k = 0;
        for (int i = from; i < to; i++)
        {
            if (_t[i] == c)
            {
                k++;
            }
        }

        return k;
    }

    /// <summary>Reads dot-separated host labels (RFC 1035 §2.3.1 sizes: 63 per label, 253 in all).</summary>
    private bool TryHostName(int start, int limit, out int end, out int labels, out int lastLabelStart)
    {
        string t = _t;
        end = start;
        labels = 0;
        lastLabelStart = start;
        int p = start;
        while (true)
        {
            int ls = p;
            while (p < limit && p - ls <= MaxLabel && IsHostChar(t[p]))
            {
                p++;
            }

            int len = p - ls;
            if (len is 0 or > MaxLabel || t[ls] == '-' || t[p - 1] == '-' || p - start > MaxDomain)
            {
                return false;
            }

            labels++;
            lastLabelStart = ls;
            end = p;
            if (p + 1 < limit && t[p] == '.' && IsHostChar(t[p + 1]))
            {
                p++;
                continue;
            }

            return true;
        }
    }

    private static bool IsUrlChar(char c)
    {
        if (c < 0x80)
        {
            return char.IsAsciiLetterOrDigit(c) || "-._~:/?#[]@!$&'()*+,;=%".Contains(c, StringComparison.Ordinal);
        }

        if (char.IsLetterOrDigit(c) || Lexer.IsWordChar(c) || char.IsSurrogate(c))
        {
            return true;
        }

        UnicodeCategory cat = char.GetUnicodeCategory(c);
        return cat is UnicodeCategory.MathSymbol or UnicodeCategory.CurrencySymbol or UnicodeCategory.ModifierSymbol or UnicodeCategory.OtherSymbol;
    }

    // ---- GUID ---------------------------------------------------------------------------------------------------------------

    // RFC 9562 §4: 8-4-4-4-12 hexadecimal digits; version and variant bits are not checked (the nil and max UUIDs are UUIDs).
    // The braced form "{...}" and "urn:uuid:..." (RFC 9562 §4 / RFC 4122 §3) are included in the match.
    private void FindGuids()
    {
        string t = _t;
        int n = t.Length;
        for (int h = t.IndexOf('-'); h >= 0; h = t.IndexOf('-', h + 1))
        {
            int s = h - 8;
            if (s < 0 || s + 36 > n || t[s + 13] != '-' || t[s + 18] != '-' || t[s + 23] != '-')
            {
                continue;
            }

            bool ok = true;
            for (int k = 0; k < 36 && ok; k++)
            {
                ok = k is 8 or 13 or 18 or 23 ? t[s + k] == '-' : char.IsAsciiHexDigit(t[s + k]);
            }

            int e = s + 36;
            if (!ok || (s > 0 && (char.IsLetterOrDigit(t[s - 1]) || t[s - 1] == '-')) || (e < n && (char.IsLetterOrDigit(t[e]) || t[e] == '-')))
            {
                continue;
            }

            int start = s;
            int end = e;
            if (s > 0 && t[s - 1] == '{' && e < n && t[e] == '}')
            {
                start--;
                end++;
            }
            else if (s >= 9 && string.Compare(t, s - 9, "urn:uuid:", 0, 9, StringComparison.OrdinalIgnoreCase) == 0)
            {
                start -= 9;
            }

            AddSequence(start, end, SequenceKind.Uuid, Labels.Guid, 1f, t.Substring(s, 36).ToLowerInvariant());
        }
    }

    // ---- IP addresses -------------------------------------------------------------------------------------------------------

    // RFC 791 / dotted decimal: four decimal octets 0-255 without leading zeros (so "010.1.1.1" is not read as an address).
    private void FindIPv4()
    {
        string t = _t;
        int n = t.Length;
        int i = 0;
        while (i < n)
        {
            if (!char.IsAsciiDigit(t[i]) || (i > 0 && (char.IsLetterOrDigit(t[i - 1]) || (t[i - 1] == '.' && i > 1 && char.IsAsciiDigit(t[i - 2])))))
            {
                i++;
                continue;
            }

            int p = i;
            bool ok = true;
            for (int part = 0; part < 4 && ok; part++)
            {
                if (part > 0)
                {
                    if (p < n && t[p] == '.')
                    {
                        p++;
                    }
                    else
                    {
                        ok = false;
                        break;
                    }
                }

                int ps = p;
                while (p < n && char.IsAsciiDigit(t[p]) && p - ps < 4)
                {
                    p++;
                }

                int len = p - ps;
                ok = len is >= 1 and <= 3 && !(len > 1 && t[ps] == '0') && int.Parse(t.AsSpan(ps, len), CultureInfo.InvariantCulture) <= 255;
            }

            if (ok && (p >= n || !(char.IsLetterOrDigit(t[p]) || (t[p] == '.' && p + 1 < n && char.IsAsciiDigit(t[p + 1])))))
            {
                AddSequence(i, p, SequenceKind.IpAddress, Labels.IpAddress, 1f, t.Substring(i, p - i));
                i = p;
                continue;
            }

            // Skip the whole run of digits and dots so a long dotted string costs one pass.
            while (i < n && (char.IsAsciiDigit(t[i]) || t[i] == '.'))
            {
                i++;
            }
        }
    }

    // RFC 4291 §2.2: eight 16-bit groups in hex, one "::" for a run of zero groups, an embedded dotted IPv4 as the last 32 bits.
    // The match is written in the canonical form of RFC 5952 §4.
    private void FindIPv6()
    {
        string t = _t;
        int n = t.Length;
        int i = 0;
        while (i < n)
        {
            if (!(char.IsAsciiHexDigit(t[i]) || t[i] == ':'))
            {
                i++;
                continue;
            }

            int s = i;
            while (i < n && (char.IsAsciiHexDigit(t[i]) || t[i] == ':' || t[i] == '.'))
            {
                i++;
            }

            if (s > 0 && (char.IsLetterOrDigit(t[s - 1]) || t[s - 1] == ':'))
            {
                continue;
            }

            int e = i;
            if (e < n && (char.IsLetterOrDigit(t[e]) || t[e] == '%'))
            {
                continue;
            }

            for (int attempt = 0; attempt < 3 && e - s >= 2; attempt++)
            {
                if (e - s <= 45 && TryParseIPv6(t.AsSpan(s, e - s), out ushort[]? groups) && t.AsSpan(s, e - s).Contains(':'))
                {
                    AddSequence(s, e, SequenceKind.IpAddress, Labels.IpAddress, 1f, FormatIPv6(groups));
                    break;
                }

                // "Server at ::1." and "see 2001:db8::1:" end in sentence punctuation.
                if (t[e - 1] is '.' or ':' && !(t[e - 1] == ':' && e - 2 >= s && t[e - 2] == ':'))
                {
                    e--;
                }
                else
                {
                    break;
                }
            }
        }
    }

    internal static bool TryParseIPv6(ReadOnlySpan<char> s, out ushort[] groups)
    {
        groups = [];
        if (s.Length is < 2 or > 45)
        {
            return false;
        }

        int dbl = s.IndexOf("::", StringComparison.Ordinal);
        if (dbl >= 0 && s[(dbl + 1)..].IndexOf("::", StringComparison.Ordinal) >= 0)
        {
            return false;
        }

        var head = new List<ushort>(8);
        var tail = new List<ushort>(8);
        if (dbl < 0)
        {
            if (!ParseSide(s, head, true))
            {
                return false;
            }

            if (head.Count != 8)
            {
                return false;
            }

            groups = [.. head];
            return true;
        }

        if (!ParseSide(s[..dbl], head, false) || !ParseSide(s[(dbl + 2)..], tail, true) || head.Count + tail.Count > 7)
        {
            return false;
        }

        var all = new ushort[8];
        head.CopyTo(all);
        tail.CopyTo(all, 8 - tail.Count);
        groups = all;
        return true;
    }

    private static bool ParseSide(ReadOnlySpan<char> side, List<ushort> into, bool lastMayBeIPv4)
    {
        if (side.Length == 0)
        {
            return true;
        }

        int p = 0;
        while (true)
        {
            int colon = side[p..].IndexOf(':');
            ReadOnlySpan<char> part = colon < 0 ? side[p..] : side.Slice(p, colon);
            bool last = colon < 0;
            if (last && lastMayBeIPv4 && part.Contains('.'))
            {
                if (!TryParseIPv4(part, out byte[]? quad))
                {
                    return false;
                }

                into.Add((ushort)((quad[0] << 8) | quad[1]));
                into.Add((ushort)((quad[2] << 8) | quad[3]));
                return true;
            }

            if (part.Length is < 1 or > 4 || !ushort.TryParse(part, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ushort g))
            {
                return false;
            }

            into.Add(g);
            if (last)
            {
                return true;
            }

            p += colon + 1;
        }
    }

    internal static bool TryParseIPv4(ReadOnlySpan<char> s, out byte[] quad)
    {
        quad = new byte[4];
        int p = 0;
        for (int part = 0; part < 4; part++)
        {
            int dot = s[p..].IndexOf('.');
            ReadOnlySpan<char> o = part < 3 ? (dot < 0 ? default : s.Slice(p, dot)) : s[p..];
            if (o.Length is < 1 or > 3 || (o.Length > 1 && o[0] == '0') || !byte.TryParse(o, NumberStyles.None, CultureInfo.InvariantCulture, out quad[part]))
            {
                return false;
            }

            p += o.Length + 1;
        }

        return true;
    }

    /// <summary>RFC 5952 §4: lower case, no leading zeros, the longest run of two or more zero groups (the first on a tie) written as "::".</summary>
    internal static string FormatIPv6(ushort[] g)
    {
        int bestStart = -1;
        int bestLen = 0;
        for (int i = 0; i < 8;)
        {
            if (g[i] != 0)
            {
                i++;
                continue;
            }

            int j = i;
            while (j < 8 && g[j] == 0)
            {
                j++;
            }

            if (j - i > bestLen)
            {
                bestStart = i;
                bestLen = j - i;
            }

            i = j;
        }

        if (bestLen < 2)
        {
            bestStart = -1;
        }

        var sb = new StringBuilder(40);
        for (int i = 0; i < 8; i++)
        {
            if (i == bestStart)
            {
                sb.Append("::");
                i += bestLen - 1;
                continue;
            }

            if (sb.Length > 0 && sb[^1] != ':')
            {
                sb.Append(':');
            }

            sb.Append(g[i].ToString("x", CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }

    // ---- phone-like digit groups ----------------------------------------------------------------------------------------------

    // Syntax only, nothing validated: an optional "+", groups of digits (one group may be in parentheses) joined by a single space,
    // hyphen or dot, 7 to 15 digits in all (E.164 caps a number at 15). The shapes are limited so that dates, ranges, decimals and
    // addresses are not taken: see PhoneAccepted.
    private void FindPhones()
    {
        string t = _t;
        int n = t.Length;
        var starts = new List<int>(8);
        var ends = new List<int>(8);
        var seps = new List<char>(8);
        var paren = new List<bool>(8);
        int i = 0;
        while (i < n)
        {
            char c = t[i];
            if (!(char.IsAsciiDigit(c) || c is '+' or '('))
            {
                i++;
                continue;
            }

            if (i > 0 && (char.IsLetterOrDigit(t[i - 1]) || "-./_@#$%+".Contains(t[i - 1], StringComparison.Ordinal)))
            {
                i++;
                continue;
            }

            starts.Clear();
            ends.Clear();
            seps.Clear();
            paren.Clear();
            bool plus = c == '+';
            int p = plus ? i + 1 : i;
            while (starts.Count < 8 && p < n)
            {
                bool isParen = false;
                int gs;
                int ge;
                if (t[p] == '(')
                {
                    gs = p + 1;
                    ge = gs;
                    while (ge < n && char.IsAsciiDigit(t[ge]) && ge - gs < 5)
                    {
                        ge++;
                    }

                    if (ge == gs || ge >= n || t[ge] != ')')
                    {
                        break;
                    }

                    isParen = true;
                }
                else if (char.IsAsciiDigit(t[p]))
                {
                    gs = p;
                    ge = p;
                    while (ge < n && char.IsAsciiDigit(t[ge]) && ge - gs < 16)
                    {
                        ge++;
                    }
                }
                else
                {
                    break;
                }

                starts.Add(gs);
                paren.Add(isParen);
                int after = isParen ? ge + 1 : ge;
                ends.Add(after);
                int q = after;
                char sep = '\0';
                if (q < n && t[q] is ' ' or '-' or '.' or '\u00A0')
                {
                    sep = t[q];
                    q++;
                }

                if (q < n && (char.IsAsciiDigit(t[q]) || t[q] == '(') && (sep != '\0' || isParen))
                {
                    seps.Add(sep);
                    p = q;
                    continue;
                }

                break;
            }

            bool emitted = false;
            for (int count = starts.Count; count >= 1 && !emitted; count--)
            {
                int end = ends[count - 1];
                if (end < n && char.IsLetterOrDigit(t[end]))
                {
                    continue;
                }

                if (PhoneAccepted(starts, ends, seps, paren, count, plus))
                {
                    var digits = new StringBuilder(16);
                    if (plus)
                    {
                        digits.Append('+');
                    }

                    for (int k = i; k < end; k++)
                    {
                        if (char.IsAsciiDigit(t[k]))
                        {
                            digits.Append(t[k]);
                        }
                    }

                    AddSequence(i, end, SequenceKind.PhoneLike, Labels.PhoneLike, 0.7f, digits.ToString());
                    i = end;
                    emitted = true;
                }
            }

            if (!emitted)
            {
                i++;
            }
        }
    }

    private bool PhoneAccepted(List<int> starts, List<int> ends, List<char> seps, List<bool> paren, int count, bool plus)
    {
        var sizes = new int[count];
        int digits = 0;
        bool anyParen = false;
        for (int k = 0; k < count; k++)
        {
            sizes[k] = (paren[k] ? ends[k] - 1 : ends[k]) - starts[k];
            digits += sizes[k];
            anyParen |= paren[k];
        }

        if (digits is < 7 or > 15)
        {
            return false;
        }

        if (plus)
        {
            // "+44 20 7183 8750", "+1 (555) 123-4567", "+442071838750": a country code of one to three digits, then more.
            return (count == 1 && sizes[0] >= 8) || (count >= 2 && sizes[0] <= 3);
        }

        if (count < 2)
        {
            return false;
        }

        if (anyParen)
        {
            return digits >= 9;
        }

        char sep = seps[0];
        for (int k = 1; k < count - 1; k++)
        {
            if (seps[k] != sep)
            {
                return false;
            }
        }

        if (sep == ' ' || sep == '\u00A0')
        {
            // Space-separated digits are only phone-like with a national trunk prefix: "020 7183 8750".
            return _t[starts[0]] == '0' && count >= 2 && digits is >= 9 and <= 11 && sizes.All(s => s is >= 2 and <= 5);
        }

        if (count == 2)
        {
            return sep == '-' && sizes[0] == 3 && sizes[1] == 4;
        }

        // "1-800-555-0199": a one-digit country code may start the group.
        bool firstOk = sizes[0] is >= 2 and <= 4 || (sizes[0] == 1 && count >= 4);
        if (count > 5 || !firstOk || !sizes.Skip(1).All(s => s is >= 2 and <= 4))
        {
            return false;
        }

        // 2020-01-15 and 15.01.2020 are dates, not telephone numbers.
        if (count == 3)
        {
            int a = int.Parse(_t.AsSpan(starts[0], sizes[0]), CultureInfo.InvariantCulture);
            int b = int.Parse(_t.AsSpan(starts[1], sizes[1]), CultureInfo.InvariantCulture);
            int c = int.Parse(_t.AsSpan(starts[2], sizes[2]), CultureInfo.InvariantCulture);
            if (sizes[0] == 4 && sizes[1] == 2 && sizes[2] == 2 && a is >= 1000 and <= 2999 && b is >= 1 and <= 12 && c is >= 1 and <= 31)
            {
                return false;
            }

            if (sizes[0] == 2 && sizes[1] == 2 && sizes[2] is 2 or 4 && a is >= 1 and <= 31 && b is >= 1 and <= 31 && (a <= 12 || b <= 12))
            {
                return false;
            }
        }

        return true;
    }
}
