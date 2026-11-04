using System.Globalization;
using System.Text;

namespace RecursiveAI.OneNlp.Recognizers;

/// <summary>
/// One recognition pass over one text: the lexer items, the number candidates, and the results of every rule family. A pass is used
/// by one thread; the rule data it reads is shared and immutable.
/// </summary>
internal sealed partial class Scan
{
    private readonly RecognizerData _d;
    private readonly string _t;
    private readonly List<Item> _items = [];
    private readonly StringBuilder _sb = new();
    private readonly List<NumberCandidate> _numbers = [];
    private readonly List<Recognition> _found = [];

    private readonly Dictionary<string, Cardinal>.AlternateLookup<ReadOnlySpan<char>> _cardinals;
    private readonly Dictionary<string, OrdinalWord>.AlternateLookup<ReadOnlySpan<char>> _ordinals;
    private readonly Dictionary<string, int>.AlternateLookup<ReadOnlySpan<char>> _fractions;
    private readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> _articles;
    private readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> _connectors;
    private readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> _negatives;
    private readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> _points;

    internal Scan(RecognizerData data, string text, RecognitionContext context = default)
    {
        _d = data;
        _t = text;
        _cardinals = data.Cardinals.GetAlternateLookup<ReadOnlySpan<char>>();
        _ordinals = data.Ordinals.GetAlternateLookup<ReadOnlySpan<char>>();
        _fractions = data.Fractions.GetAlternateLookup<ReadOnlySpan<char>>();
        _articles = data.Articles.GetAlternateLookup<ReadOnlySpan<char>>();
        _connectors = data.Connectors.GetAlternateLookup<ReadOnlySpan<char>>();
        _negatives = data.NegativeWords.GetAlternateLookup<ReadOnlySpan<char>>();
        _points = data.PointWords.GetAlternateLookup<ReadOnlySpan<char>>();
        Lexer.Lex(text, _items);
        InitTemporal(context);
    }

    /// <summary>A number found in the text, before it is paired with a unit, a currency or a percent sign.</summary>
    /// <remarks>
    /// <c>Attached</c>: letters touch the digits (<c>5km</c>), so the number alone is not a result, but a unit may follow it.
    /// <c>MoneyOnly</c>: the number carries a suffix that only means a multiplier next to a currency (<c>5m</c> in <c>$5m</c>).
    /// </remarks>
    private struct NumberCandidate
    {
        public int Start;
        public int End;
        public decimal Value;
        public bool Ordinal;
        public bool Attached;
        public bool MoneyOnly;
    }

    private struct WordNumber
    {
        public int EndItem;
        public decimal Value;
        public bool Ordinal;
    }

    private enum Piece
    {
        None,
        Unit,
        Teen,
        Tens,
        Hundred,
        Scale,
    }

    // ---- finding numbers ----------------------------------------------------------------------------------------------------

    private void FindNumbers()
    {
        int i = 0;
        while (i < _items.Count)
        {
            Item it = _items[i];
            int end;
            if (it.Kind == ItemKind.Digits)
            {
                end = ScanDigits(it.Start, -1);
            }
            else if (it.Kind == ItemKind.Other && _t[it.Start] == _d.DecimalSeparator && it.End - it.Start == 1
                && i + 1 < _items.Count && _items[i + 1].Kind == ItemKind.Digits && _items[i + 1].Start == it.End)
            {
                end = ScanDigits(it.Start, -1);
            }
            else if (it.Kind == ItemKind.Word)
            {
                end = ScanWords(i);
            }
            else
            {
                i++;
                continue;
            }

            i = Math.Max(i + 1, ItemIndexAt(end));
        }
    }

    private int ScanWords(int i)
    {
        Item it = _items[i];
        ReadOnlySpan<char> word = _t.AsSpan(it.Start, it.End - it.Start);
        if (_negatives.Contains(word))
        {
            int j = Next(i, false);
            if (j >= 0 && _items[j].Kind == ItemKind.Digits)
            {
                return ScanDigits(_items[j].Start, it.Start);
            }

            if (j >= 0 && _items[j].Kind == ItemKind.Word && TryWordNumber(j, true, out WordNumber neg) && !neg.Ordinal)
            {
                _numbers.Add(new NumberCandidate { Start = it.Start, End = _items[neg.EndItem].End, Value = -neg.Value });
                return _items[neg.EndItem].End;
            }

            return it.End;
        }

        if (TryWordNumber(i, true, out WordNumber r))
        {
            int end = _items[r.EndItem].End;
            _numbers.Add(new NumberCandidate { Start = it.Start, End = end, Value = r.Value, Ordinal = r.Ordinal });
            return end;
        }

        return it.End;
    }

    /// <summary>
    /// Reads a number written in words starting at word item <paramref name="first"/>, as far as it stays well formed: units, teens and
    /// tens add up inside a group of hundreds, and scale words (thousand, million, ...) close a group and must descend. The longest
    /// well-formed prefix wins. For example "one hundred and five thousand two hundred" is 105,200 and "one two" is just "one".
    /// </summary>
    private bool TryWordNumber(int first, bool allowTail, out WordNumber result)
    {
        result = default;
        decimal total = 0;
        decimal cur = 0;
        decimal lastScale = decimal.MaxValue;
        bool any = false;
        bool article = false;
        bool ordinal = false;
        Piece last = Piece.None;
        int end = -1;
        decimal endValue = 0;
        bool endOrdinal = false;
        int i = first;

        if (Word(i, out ReadOnlySpan<char> w0) && _articles.Contains(w0))
        {
            int j = Next(i, false);
            if (j < 0 || _items[j].Kind != ItemKind.Word)
            {
                return false;
            }

            ReadOnlySpan<char> next = Span(j);
            if (allowTail && _fractions.TryGetValue(next, out int den))
            {
                result = new WordNumber { EndItem = j, Value = 1m / den };
                return true;
            }

            if (!(_cardinals.TryGetValue(next, out Cardinal c0) && c0.Kind is CardinalKind.Scale or CardinalKind.Multiplier))
            {
                return false;
            }

            article = true;
            i = j;
        }

        while (_items[i].Kind == ItemKind.Word)
        {
            ReadOnlySpan<char> w = Span(i);
            bool terminal = false;
            if (_cardinals.TryGetValue(w, out Cardinal c))
            {
                if (!ApplyCardinal(c, ref cur, ref total, ref lastScale, ref last, any, article, ref terminal))
                {
                    break;
                }
            }
            else if (_ordinals.TryGetValue(w, out OrdinalWord o))
            {
                if (!any && o.NotAfterNumber && PrevIsNumber(i))
                {
                    break;
                }

                if (!ApplyOrdinal(o.Value, ref cur, ref total, lastScale, last, any, article))
                {
                    break;
                }

                ordinal = true;
                terminal = true;
            }
            else
            {
                break;
            }

            any = true;
            end = i;
            endValue = total + cur;
            endOrdinal = ordinal;
            if (terminal)
            {
                break;
            }

            int n = Next(i, true);
            if (n < 0 || _items[n].Kind != ItemKind.Word)
            {
                break;
            }

            if (_connectors.Contains(Span(n)))
            {
                // "and" only links the hundreds or a scale word to what follows: "one hundred and five", "two thousand and ten".
                int k = Next(n, false);
                if (last is (Piece.Hundred or Piece.Scale) && cur % 100 == 0 && k >= 0 && _items[k].Kind == ItemKind.Word && StartsLowerPart(Span(k)))
                {
                    i = k;
                    continue;
                }

                break;
            }

            i = n;
        }

        if (end < 0)
        {
            return false;
        }

        result = new WordNumber { EndItem = end, Value = endValue, Ordinal = endOrdinal };
        if (allowTail && !endOrdinal)
        {
            ApplyTail(ref result);
        }

        return true;
    }

    private bool StartsLowerPart(ReadOnlySpan<char> w) =>
        (_cardinals.TryGetValue(w, out Cardinal c) && c.Kind is CardinalKind.Unit or CardinalKind.Teen or CardinalKind.Tens)
        || (_ordinals.TryGetValue(w, out OrdinalWord o) && o.Value < 100);

    private static bool ApplyCardinal(
        Cardinal c, ref decimal cur, ref decimal total, ref decimal lastScale, ref Piece last, bool any, bool article, ref bool terminal)
    {
        decimal v = c.Value;
        switch (c.Kind)
        {
            case CardinalKind.Unit:
                if (v == 0)
                {
                    if (any)
                    {
                        return false;
                    }

                    terminal = true;
                }
                else if (!(cur % 100 == 0 || (cur % 100 >= 20 && cur % 10 == 0)))
                {
                    return false;
                }

                cur += v;
                last = Piece.Unit;
                return true;
            case CardinalKind.Teen:
                if (cur % 100 != 0)
                {
                    return false;
                }

                cur += v;
                last = Piece.Teen;
                return true;
            case CardinalKind.Tens:
                if (cur % 100 != 0)
                {
                    return false;
                }

                cur += v;
                last = Piece.Tens;
                return true;
            case CardinalKind.Multiplier:
            {
                decimal g = cur > 0 ? cur : (article && !any ? 1 : 0);
                if (g == 0 || total != 0 || last == Piece.Hundred)
                {
                    return false;
                }

                if (!TryMultiply(g, v, out decimal m))
                {
                    return false;
                }

                total = m;
                cur = 0;
                terminal = true;
                last = Piece.Scale;
                return true;
            }

            default:
                if (v == 100)
                {
                    if (article && !any)
                    {
                        cur = 100;
                    }
                    else if (last is Piece.Unit or Piece.Teen or Piece.Tens && cur > 0 && cur < 100)
                    {
                        cur *= 100;
                    }
                    else
                    {
                        return false;
                    }

                    last = Piece.Hundred;
                    return true;
                }

                return ApplyScale(v, ref cur, ref total, ref lastScale, ref last, any, article);
        }
    }

    private static bool ApplyScale(decimal s, ref decimal cur, ref decimal total, ref decimal lastScale, ref Piece last, bool any, bool article)
    {
        decimal g = cur;
        if (g == 0)
        {
            if (article && !any)
            {
                g = 1;
            }
            else
            {
                return false;
            }
        }

        if (s >= lastScale || !TryMultiply(g, s, out decimal m) || total > decimal.MaxValue - m)
        {
            return false;
        }

        total += m;
        cur = 0;
        lastScale = s;
        last = Piece.Scale;
        return true;
    }

    private static bool ApplyOrdinal(decimal v, ref decimal cur, ref decimal total, decimal lastScale, Piece last, bool any, bool article)
    {
        if (v < 20)
        {
            // "first" .. "nineteenth": a unit may also follow tens ("twenty-first").
            bool ok = cur % 100 == 0 || (v < 10 && cur % 100 >= 20 && cur % 10 == 0);
            if (!ok)
            {
                return false;
            }

            cur += v;
            return true;
        }

        if (v < 100)
        {
            if (cur % 100 != 0)
            {
                return false;
            }

            cur += v;
            return true;
        }

        if (v == 100)
        {
            if (cur > 0 && cur < 100 && last is Piece.Unit or Piece.Teen or Piece.Tens)
            {
                cur *= 100;
                return true;
            }

            if (cur == 0 && !any && !article)
            {
                cur = 100;
                return true;
            }

            return false;
        }

        // "thousandth", "millionth": a group count may come first, and a bare one stands for one.
        decimal g = cur == 0 ? 1 : cur;
        if (v >= lastScale || (cur == 0 && any && last != Piece.Scale) || !TryMultiply(g, v, out decimal m))
        {
            return false;
        }

        total += m;
        cur = 0;
        return true;
    }

    /// <summary>Fractions, decimals and mixed numbers after a cardinal in words: "three quarters", "two and a half", "three point one four".</summary>
    private void ApplyTail(ref WordNumber r)
    {
        decimal n = r.Value;
        int end = r.EndItem;
        int j = Next(end, false);
        if (j < 0 || _items[j].Kind != ItemKind.Word)
        {
            return;
        }

        ReadOnlySpan<char> w = Span(j);
        bool changed = false;
        if (_fractions.TryGetValue(w, out int den))
        {
            // "three quarters" (the numerator is the number just read)
            n /= den;
            end = j;
            changed = true;
        }
        else if (_connectors.Contains(w))
        {
            int k = Next(j, false);
            if (k >= 0 && _items[k].Kind == ItemKind.Word)
            {
                if (_articles.Contains(Span(k)))
                {
                    int m = Next(k, false);
                    if (m >= 0 && _items[m].Kind == ItemKind.Word && _fractions.TryGetValue(Span(m), out int d1))
                    {
                        n += 1m / d1;
                        end = m;
                        changed = true;
                    }
                }
                else if (TryWordNumber(k, false, out WordNumber num) && !num.Ordinal)
                {
                    int m = Next(num.EndItem, false);
                    if (m >= 0 && _items[m].Kind == ItemKind.Word && _fractions.TryGetValue(Span(m), out int d2))
                    {
                        n += num.Value / d2;
                        end = m;
                        changed = true;
                    }
                }
            }
        }
        else if (_points.Contains(w))
        {
            // "three point one four" is 3.14: every word after "point" is one digit.
            decimal place = 0.1m;
            decimal frac = 0;
            int k = Next(j, false);
            int lastDigit = -1;
            while (k >= 0 && _items[k].Kind == ItemKind.Word && _cardinals.TryGetValue(Span(k), out Cardinal d) && d.Kind == CardinalKind.Unit)
            {
                frac += d.Value * place;
                place /= 10;
                lastDigit = k;
                k = Next(k, false);
                if (place < 1e-27m)
                {
                    break;
                }
            }

            if (lastDigit >= 0)
            {
                n += frac;
                end = lastDigit;
                changed = true;
            }
        }

        if (!changed)
        {
            return;
        }

        // "two and a half million", "three point five billion"
        int s = Next(end, false);
        if (s >= 0 && _items[s].Kind == ItemKind.Word && _cardinals.TryGetValue(Span(s), out Cardinal sc) && sc.Kind == CardinalKind.Scale
            && sc.Value >= 1000 && TryMultiply(n, sc.Value, out decimal scaled))
        {
            n = scaled;
            end = s;
        }

        r.Value = n;
        r.EndItem = end;
    }

    private bool PrevIsNumber(int item)
    {
        if (item == 0 || !GapOk(_items[item - 1].End, _items[item].Start))
        {
            return false;
        }

        Item p = _items[item - 1];
        return p.Kind == ItemKind.Digits || (p.Kind == ItemKind.Word && _cardinals.ContainsKey(Span(item - 1)));
    }

    // ---- digits -------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Reads a number written in digits at <paramref name="pos"/> (with its group and decimal separators, a sign before it, and letters
    /// touching it), adds its candidates, and returns the offset to continue scanning from. <paramref name="negativeFrom"/> is the start
    /// of a negative word ("minus") already read, or -1.
    /// </summary>
    private int ScanDigits(int pos, int negativeFrom)
    {
        string t = _t;
        int n = t.Length;
        char dec = _d.DecimalSeparator;
        char grp = _d.GroupSeparator;
        int p = pos;
        bool grouped = false;
        bool hasDecimal = false;
        bool leadingSeparator = t[p] == dec && !IsDigit(t[p]);
        if (!leadingSeparator)
        {
            int firstRun = 0;
            while (p < n && IsDigit(t[p]))
            {
                p++;
                firstRun++;
            }

            // Groups of exactly three digits ("1,234,567"); "1,2345" is two numbers.
            while (firstRun <= 3 && p + 3 < n && t[p] == grp && IsDigit(t[p + 1]) && IsDigit(t[p + 2]) && IsDigit(t[p + 3])
                && (p + 4 >= n || !IsDigit(t[p + 4])))
            {
                p += 4;
                grouped = true;
            }
        }

        if (p + 1 < n && t[p] == dec && IsDigit(t[p + 1]))
        {
            hasDecimal = true;
            p++;
            while (p < n && IsDigit(t[p]))
            {
                p++;
            }

            // "1.2.3" is a version number or an address, not a number.
            if (p + 1 < n && t[p] == dec && IsDigit(t[p + 1]))
            {
                while (p < n && (IsDigit(t[p]) || (t[p] == dec && p + 1 < n && IsDigit(t[p + 1]))))
                {
                    p++;
                }

                return p;
            }
        }

        int literalEnd = p;
        int q = p;
        while (q < n && Lexer.IsWordChar(t[q]))
        {
            q++;
        }

        // The sign: "-5" is negative after white space or an opening bracket, a range ("3-5") after a digit, and part of a
        // name ("COVID-19", "B-52") after a letter.
        int start = pos;
        bool negative = false;
        bool valid = true;
        if (negativeFrom >= 0)
        {
            start = negativeFrom;
            negative = true;
        }
        else if (pos > 0 && _d.NegativeSigns.Contains(t[pos - 1], StringComparison.Ordinal))
        {
            if (pos == 1 || SignPrevOk(t[pos - 2]))
            {
                start = pos - 1;
                negative = true;
            }
            else if (char.IsLetter(t[pos - 2]))
            {
                valid = false;
            }
        }

        if (valid && !negative && !PrevOk(pos))
        {
            valid = false;
        }

        if (!valid)
        {
            return Math.Max(literalEnd, q);
        }

        string literal = t.Substring(pos, literalEnd - pos);
        string clean = grouped ? literal.Replace(grp.ToString(), string.Empty, StringComparison.Ordinal) : literal;
        if (dec != '.')
        {
            clean = clean.Replace(dec, '.');
        }

        if (clean.Length > 0 && clean[0] == '.')
        {
            clean = "0" + clean;
        }

        if (!decimal.TryParse(clean, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal value))
        {
            return Math.Max(literalEnd, q);
        }

        if (negative)
        {
            value = -value;
        }

        bool plainInteger = !grouped && !hasDecimal && !leadingSeparator;

        // A fraction in digits: "1/2", "3/4". Only a proper fraction, and not the start of a date ("3/4/2020").
        if (plainInteger && !negative && literalEnd + 1 < n && t[literalEnd] == '/' && IsDigit(t[literalEnd + 1]) && (pos == 0 || t[pos - 1] != '/'))
        {
            int r = literalEnd + 1;
            while (r < n && IsDigit(t[r]))
            {
                r++;
            }

            bool dateLike = r + 1 < n && t[r] == '/' && IsDigit(t[r + 1]);
            if (!dateLike && (r >= n || !Lexer.IsWordChar(t[r]))
                && decimal.TryParse(t.AsSpan(literalEnd + 1, r - literalEnd - 1), NumberStyles.None, CultureInfo.InvariantCulture, out decimal denominator)
                && denominator is > 0 and <= 1000 && value < denominator)
            {
                _numbers.Add(new NumberCandidate { Start = start, End = r, Value = value / denominator });
                return r;
            }
        }

        if (q > literalEnd)
        {
            // Letters touch the digits: an ordinal ending ("21st"), a multiplier ("3.5k"), or the start of a unit ("5km").
            ReadOnlySpan<char> letters = t.AsSpan(literalEnd, q - literalEnd);
            bool boundary = q >= n || !IsDigit(t[q]);
            if (boundary && plainInteger && !negative && OrdinalSuffixOk(literal, letters))
            {
                _numbers.Add(new NumberCandidate { Start = start, End = q, Value = value, Ordinal = true });
                return q;
            }

            if (boundary && _d.ScaleSuffixes.Count > 0 && _d.ScaleSuffixes.TryGet(letters.ToString(), out List<ScaleSuffix>? suffixes))
            {
                bool plain = false;
                foreach (ScaleSuffix s in suffixes)
                {
                    if (TryMultiply(value, s.Value, out decimal scaled))
                    {
                        _numbers.Add(new NumberCandidate { Start = start, End = q, Value = scaled, MoneyOnly = s.MoneyOnly });
                        plain |= !s.MoneyOnly;
                    }
                }

                if (!plain)
                {
                    _numbers.Add(new NumberCandidate { Start = start, End = literalEnd, Value = value, Attached = true });
                }

                return q;
            }

            _numbers.Add(new NumberCandidate { Start = start, End = literalEnd, Value = value, Attached = true });
            return q;
        }

        // "3.5 million", "2 dozen": a scale word after digits multiplies them.
        int last = ItemIndexAt(literalEnd) - 1;
        int nx = last >= 0 ? Next(last, false) : -1;
        if (nx >= 0 && _items[nx].Kind == ItemKind.Word && _cardinals.TryGetValue(Span(nx), out Cardinal sc)
            && sc.Kind is CardinalKind.Scale or CardinalKind.Multiplier && TryMultiply(value, sc.Value, out decimal product))
        {
            int e = _items[nx].End;
            _numbers.Add(new NumberCandidate { Start = start, End = e, Value = product });
            return e;
        }

        _numbers.Add(new NumberCandidate { Start = start, End = literalEnd, Value = value });
        return literalEnd;
    }

    private bool OrdinalSuffixOk(string literal, ReadOnlySpan<char> letters)
    {
        char lastDigit = literal[^1];
        bool teen = literal.Length >= 2 && literal[^2] == '1';
        foreach (OrdinalSuffix rule in _d.OrdinalSuffixes)
        {
            if (letters.Equals(rule.Suffix, StringComparison.OrdinalIgnoreCase) && rule.Digits.Contains(lastDigit, StringComparison.Ordinal)
                && (rule.Teen == 0 || (rule.Teen == 1) == teen))
            {
                return true;
            }
        }

        return false;
    }

    private bool PrevOk(int pos)
    {
        if (pos == 0)
        {
            return true;
        }

        char c = _t[pos - 1];
        if (char.IsLetterOrDigit(c))
        {
            return false;
        }

        // "v1.5", "a.5": a dot inside a name is not a decimal point.
        return !(c == _d.DecimalSeparator && pos >= 2 && char.IsLetterOrDigit(_t[pos - 2]));
    }

    private static bool SignPrevOk(char c) =>
        char.IsWhiteSpace(c) || "([{\"'\u201C\u2018:;=<>~,".Contains(c, StringComparison.Ordinal) || char.GetUnicodeCategory(c) == UnicodeCategory.CurrencySymbol;

    // ---- numbers, then what goes with them ----------------------------------------------------------------------------------

    /// <summary>
    /// Turns the number candidates into results: the bare numbers, and each number with a currency before it, or a percent sign, a unit
    /// or a currency name after it.
    /// </summary>
    private void EmitQuantities()
    {
        foreach (NumberCandidate c in _numbers)
        {
            if (!c.Attached && !c.MoneyOnly)
            {
                _found.Add(MakeQuantity(c.Start, c.End, c.Ordinal ? QuantityKind.Ordinal : QuantityKind.Cardinal, c.Value, null, null, null, 1f));
            }

            if (c.Ordinal)
            {
                continue;
            }

            EmitPrefixMoney(c);
            EmitSuffixed(c);
        }
    }

    private void EmitPrefixMoney(NumberCandidate c)
    {
        if (c.Attached || _d.Prefixes.Count == 0)
        {
            return;
        }

        int k = ItemIndexAt(c.Start);
        for (int n = Math.Min(_d.Prefixes.MaxItems, k); n >= 1; n--)
        {
            int first = k - n;
            if (!GapOk(_items[k - 1].End, c.Start))
            {
                return;
            }

            _sb.Clear();
            Lexer.AppendKey(_sb, _t, _items, first, n);
            if (!_d.Prefixes.TryGet(_sb.ToString(), out List<PrefixEntry>? entries))
            {
                continue;
            }

            int start = _items[first].Start;
            decimal value = c.Value;
            if (start > 0 && _d.NegativeSigns.Contains(_t[start - 1], StringComparison.Ordinal) && (start == 1 || SignPrevOk(_t[start - 2])))
            {
                start--;
                value = -value;
            }

            PrefixEntry e = entries[0];
            _found.Add(MakeQuantity(start, c.End, QuantityKind.Money, value, null, null, e.Iso, e.Confidence));
            return;
        }
    }

    private void EmitSuffixed(NumberCandidate c)
    {
        int j = ItemIndexAt(c.End);
        if (j >= _items.Count || _d.Suffixes.Count == 0)
        {
            return;
        }

        Item h = _items[j];
        if (h.Kind == ItemKind.Other && h.End - h.Start == 1 && _t[h.Start] == '-' && h.Start == c.End && j + 1 < _items.Count
            && _items[j + 1].Start == h.End)
        {
            // "5-km", "3-year-old": one hyphen touching both sides.
            j++;
        }
        else if (!GapOk(c.End, h.Start))
        {
            return;
        }

        for (int n = Math.Min(_d.Suffixes.MaxItems, _items.Count - j); n >= 1; n--)
        {
            int after = j + n;
            if (after < _items.Count && _items[after].Start == _items[after - 1].End && _items[after].Kind != ItemKind.Other)
            {
                continue;
            }

            _sb.Clear();
            Lexer.AppendKey(_sb, _t, _items, j, n);
            if (!_d.Suffixes.TryGet(_sb.ToString(), out List<SuffixEntry>? entries))
            {
                continue;
            }

            int end = _items[after - 1].End;
            foreach (SuffixEntry e in entries)
            {
                if (c.MoneyOnly && e.Kind != SuffixKind.Money)
                {
                    continue;
                }

                switch (e.Kind)
                {
                    case SuffixKind.Percent:
                        _found.Add(MakeQuantity(c.Start, end, QuantityKind.Percent, c.Value, "percent", "percent", null, e.Confidence));
                        break;
                    case SuffixKind.Money:
                        if (TryMultiply(c.Value, e.Factor, out decimal money))
                        {
                            _found.Add(MakeQuantity(c.Start, end, QuantityKind.Money, money, null, null, e.Name, e.Confidence));
                        }

                        break;
                    default:
                        _found.Add(MakeQuantity(c.Start, end, QuantityKind.Measure, c.Value, e.Name, e.Dimension, null, e.Confidence));
                        break;
                }
            }

            return;
        }
    }

    private Quantity MakeQuantity(int start, int end, QuantityKind kind, decimal value, string? unit, string? dimension, string? currency, float confidence)
    {
        string label = kind switch
        {
            QuantityKind.Cardinal => Labels.Cardinal,
            QuantityKind.Ordinal => Labels.Ordinal,
            QuantityKind.Percent => Labels.Percent,
            QuantityKind.Money => Labels.Money,
            _ => Labels.Quantity,
        };
        // 2.50 * 1000000 keeps its two decimals; dividing by 1.000... drops trailing zeros so equal numbers print and compare alike.
        value /= 1.0000000000000000000000000000m;
        return new Quantity(start, end, _t.Substring(start, end - start), label, confidence, kind, value, unit, dimension, currency);
    }

    // ---- helpers --------------------------------------------------------------------------------------------------------------

    private static bool IsDigit(char c) => (uint)(c - '0') <= 9;

    private static bool TryMultiply(decimal a, decimal b, out decimal product)
    {
        try
        {
            product = a * b;
            return true;
        }
        catch (OverflowException)
        {
            product = 0;
            return false;
        }
    }

    private ReadOnlySpan<char> Span(int item) => _t.AsSpan(_items[item].Start, _items[item].End - _items[item].Start);

    private bool Word(int item, out ReadOnlySpan<char> word)
    {
        word = Span(item);
        return _items[item].Kind == ItemKind.Word;
    }

    /// <summary>The first item that starts at or after a character offset.</summary>
    private int ItemIndexAt(int pos)
    {
        int lo = 0;
        int hi = _items.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) >>> 1;
            if (_items[mid].Start < pos)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }

    /// <summary>Two items belong together when nothing, or at most two white-space characters on one line, lie between them.</summary>
    private bool GapOk(int from, int to)
    {
        if (to == from)
        {
            return true;
        }

        if (to - from > 2)
        {
            return false;
        }

        for (int i = from; i < to; i++)
        {
            char c = _t[i];
            if (!char.IsWhiteSpace(c) || c is '\n' or '\r')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The item after <paramref name="i"/> when it belongs to the same phrase: separated by a little white space, or (with
    /// <paramref name="hyphen"/>) by one hyphen touching both words, as in "twenty-one". -1 when there is none.
    /// </summary>
    private int Next(int i, bool hyphen)
    {
        if (i + 1 >= _items.Count)
        {
            return -1;
        }

        Item a = _items[i];
        Item b = _items[i + 1];
        if (hyphen && i + 2 < _items.Count && b.Kind == ItemKind.Other && b.End - b.Start == 1 && _t[b.Start] == '-' && b.Start == a.End
            && _items[i + 2].Start == b.End)
        {
            return i + 2;
        }

        if (GapOk(a.End, b.Start))
        {
            return i + 1;
        }

        return -1;
    }
}

/// <summary>The entity labels the recognizers write.</summary>
internal static class Labels
{
    internal const string Cardinal = "CARDINAL";
    internal const string Ordinal = "ORDINAL";
    internal const string Percent = "PERCENT";
    internal const string Money = "MONEY";
    internal const string Quantity = "QUANTITY";
    internal const string Email = "EMAIL";
    internal const string Url = "URL";
    internal const string IpAddress = "IP_ADDRESS";
    internal const string PhoneLike = "PHONE_LIKE";
    internal const string Hashtag = "HASHTAG";
    internal const string Mention = "MENTION";
    internal const string Guid = "GUID";
    internal const string Date = "DATE";
    internal const string Time = "TIME";
    internal const string DateTime = "DATETIME";
    internal const string Duration = "DURATION";
    internal const string Set = "SET";
}
