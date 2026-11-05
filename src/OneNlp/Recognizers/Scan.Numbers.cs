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
    private bool _doubt;

    /// <summary>How much a number's confidence is multiplied by when a parenthesised numeral next to it disagrees.</summary>
    private const float DoubtFactor = 0.6f;

    private readonly Dictionary<string, Cardinal>.AlternateLookup<ReadOnlySpan<char>> _cardinals;
    private readonly Dictionary<string, OrdinalWord>.AlternateLookup<ReadOnlySpan<char>> _ordinals;
    private readonly Dictionary<string, int>.AlternateLookup<ReadOnlySpan<char>> _fractions;
    private readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> _articles;
    private readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> _connectors;
    private readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> _negatives;
    private readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> _points;
    private readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> _loneSkip;
    private readonly Dictionary<string, OrdinalWord>.AlternateLookup<ReadOnlySpan<char>> _ordinalStems;

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
        _loneSkip = data.LoneSkip.GetAlternateLookup<ReadOnlySpan<char>>();
        _ordinalStems = data.OrdinalStems.GetAlternateLookup<ReadOnlySpan<char>>();
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

        /// <summary>The number is followed by a parenthesised numeral that disagrees with it ("ten (12)").</summary>
        public bool Doubt;
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

        MergeParentheticals();
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
            if (r.EndItem == i && _d.NotNumbers.Count > 0 && IsNotNumber(i, word))
            {
                return it.End;
            }

            // "un", "une", "ein": an article far more often than a number, but part of "un million", "einhundert".
            if (r.EndItem == i && _d.LoneSkip.Count > 0 && _loneSkip.Contains(word))
            {
                return it.End;
            }

            int end = _items[r.EndItem].End;
            _numbers.Add(new NumberCandidate { Start = it.Start, End = end, Value = r.Value, Ordinal = r.Ordinal });
            return end;
        }

        return it.End;
    }

    /// <summary>
    /// A lone word such as "one" or "first" is not a number when the word before or after it says so ("no one", "one of them", "for the
    /// first time"): the rules are data (<c>number.not-number</c>).
    /// </summary>
    private bool IsNotNumber(int i, ReadOnlySpan<char> word)
    {
        if (!_d.NotNumbers.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(word, out NotNumberRule? rule))
        {
            return false;
        }

        if (i > 0 && _items[i - 1].Kind == ItemKind.Word && GapOk(_items[i - 1].End, _items[i].Start)
            && rule.Before.GetAlternateLookup<ReadOnlySpan<char>>().Contains(Span(i - 1)))
        {
            return true;
        }

        if (i + 1 >= _items.Count)
        {
            return false;
        }

        Item nx = _items[i + 1];
        if (nx.Kind == ItemKind.Word && GapOk(_items[i].End, nx.Start))
        {
            return rule.After.GetAlternateLookup<ReadOnlySpan<char>>().Contains(Span(i + 1));
        }

        // "one's": an apostrophe and an s touching the word.
        return nx.Kind == ItemKind.Other && nx.Start == _items[i].End && nx.End - nx.Start == 1 && _t[nx.Start] is '\'' or '\u2019'
            && i + 2 < _items.Count && _items[i + 2].Start == nx.End && _items[i + 2].End - _items[i + 2].Start == 1
            && _t[_items[i + 2].Start] is 's' or 'S' && rule.After.Contains("'s");
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
                // "un" is an article and also the number one: "un deux" starts with a plain word.
                if (!_cardinals.ContainsKey(w0))
                {
                    return false;
                }
            }
            else
            {
                article = true;
                i = j;
            }
        }

        while (_items[i].Kind == ItemKind.Word)
        {
            ReadOnlySpan<char> w = Span(i);
            bool terminal = false;
            if (_cardinals.TryGetValue(w, out Cardinal c))
            {
                if (!ApplyCardinal(c, _d.System, ref cur, ref total, ref lastScale, ref last, any, article, ref terminal))
                {
                    break;
                }
            }
            else if (TryOrdinal(w, out OrdinalWord o))
            {
                if (!any && o.NotAfterNumber && PrevIsNumber(i))
                {
                    break;
                }

                if (!ApplyOrdinal(o.Value, _d.System, ref cur, ref total, lastScale, last, any, article))
                {
                    break;
                }

                ordinal = true;
                terminal = true;
            }
            else if (_d.System == NumberSystem.German && cur == 0 && TryGermanCompound(w, out decimal compound, out bool compoundOrdinal)
                && compound < lastScale)
            {
                // "dreihundertvierundzwanzig": a whole number below a million written as one word is one group.
                cur = compound;
                last = Piece.Hundred;
                if (compoundOrdinal)
                {
                    ordinal = true;
                    terminal = true;
                }
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
                if (_d.System == NumberSystem.French)
                {
                    // "vingt et un", "soixante et onze", "vingt-et-unieme": "et" links the tens 20 to 60 to a one (or 60 to eleven) only.
                    int fk = Next(n, true);
                    if (last == Piece.Tens && cur % 100 is 20 or 30 or 40 or 50 or 60 && fk >= 0 && _items[fk].Kind == ItemKind.Word
                        && FrenchOneAfterEt(Span(fk), cur % 100 == 60))
                    {
                        i = fk;
                        continue;
                    }

                    break;
                }

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
        || (TryOrdinal(w, out OrdinalWord o) && o.Value < 100);

    /// <summary>An ordinal word as written, or a stem with one of the endings of the language (German <c>dritte</c>, <c>dritten</c>, <c>dritter</c>).</summary>
    private bool TryOrdinal(ReadOnlySpan<char> w, out OrdinalWord ordinal)
    {
        if (_ordinals.TryGetValue(w, out ordinal))
        {
            return true;
        }

        if (_d.OrdinalStems.Count > 0)
        {
            foreach (string ending in _d.OrdinalEndings)
            {
                if (w.Length > ending.Length + 1 && w.EndsWith(ending, StringComparison.OrdinalIgnoreCase) && _ordinalStems.TryGetValue(w[..^ending.Length], out ordinal))
                {
                    return true;
                }
            }
        }

        ordinal = default;
        return false;
    }

    /// <summary>After French "et": a one (un, une, unieme) or, after sixty only, an eleven (onze, onzieme).</summary>
    private bool FrenchOneAfterEt(ReadOnlySpan<char> w, bool afterSixty)
    {
        if (_cardinals.TryGetValue(w, out Cardinal c))
        {
            return (c.Kind == CardinalKind.Unit && c.Value == 1) || (afterSixty && c.Kind == CardinalKind.Teen && c.Value == 11);
        }

        return TryOrdinal(w, out OrdinalWord o) && (o.Value == 1 || (afterSixty && o.Value == 11));
    }

    // ---- German number words ------------------------------------------------------------------------------------------------

    /// <summary>
    /// Reads a German number written as one word below a million: <c>dreihundertvierundzwanzig</c> is 324, <c>zweitausend</c> is 2000,
    /// <c>einundzwanzigste</c> the 21st. The word is cut into the morphemes of the number words in the rule data (units, teens, tens,
    /// <c>hundert</c>, <c>tausend</c>, <c>und</c>, ordinal stems), trying longer morphemes first and backing out of dead ends, and the
    /// morphemes must fit the grammar: [unit hundert | hundert] [unit tausend | tausend] ... with a unit before its tens joined by und.
    /// </summary>
    private bool TryGermanCompound(ReadOnlySpan<char> word, out decimal value, out bool ordinal)
    {
        value = 0;
        ordinal = false;
        if (word.Length is < 8 or > 64 || _d.Morphemes.Length == 0)
        {
            return false;
        }

        string w = word.ToString().ToLowerInvariant();
        var path = new List<Morpheme>(8);
        if (Cut(w, w.Length, 0, false, path, out value))
        {
            return true;
        }

        foreach (string ending in _d.OrdinalEndings)
        {
            if (w.Length > ending.Length + 4 && w.EndsWith(ending, StringComparison.Ordinal))
            {
                path.Clear();
                if (Cut(w, w.Length - ending.Length, 0, true, path, out value))
                {
                    ordinal = true;
                    return true;
                }
            }
        }

        return false;
    }

    private bool Cut(string w, int length, int pos, bool ordinal, List<Morpheme> path, out decimal value)
    {
        if (pos == length)
        {
            return Evaluate(path, ordinal, out value);
        }

        foreach (Morpheme m in _d.Morphemes)
        {
            if (m.Ordinal && !ordinal || pos + m.Text.Length > length || string.CompareOrdinal(w, pos, m.Text, 0, m.Text.Length) != 0)
            {
                continue;
            }

            path.Add(m);
            if (Cut(w, length, pos + m.Text.Length, ordinal, path, out value))
            {
                return true;
            }

            path.RemoveAt(path.Count - 1);
        }

        value = 0;
        return false;
    }

    /// <summary>Checks a morpheme sequence against the grammar of German numbers below a million and returns its value.</summary>
    private static bool Evaluate(List<Morpheme> m, bool ordinal, out decimal value)
    {
        value = 0;
        if (m.Count < 2 || (ordinal && !m[^1].Ordinal))
        {
            return false;
        }

        for (int k = 0; k < m.Count - 1; k++)
        {
            if (m[k].Ordinal)
            {
                return false;
            }
        }

        int pos = 0;
        decimal total = 0;
        if (!Group(m, ref pos, out decimal first))
        {
            first = 0;
        }

        if (pos < m.Count && m[pos].Kind == MorphemeKind.Thousand)
        {
            pos++;
            total = (first == 0 ? 1 : first) * 1000;
            if (!Group(m, ref pos, out decimal rest))
            {
                rest = 0;
            }

            total += rest;
        }
        else
        {
            total = first;
        }

        value = total;
        return pos == m.Count && total > 0;
    }

    // group := [unit] hundert [rest] | rest ; rest := teen | tens | unit und tens | unit
    private static bool Group(List<Morpheme> m, ref int pos, out decimal value)
    {
        value = 0;
        int start = pos;
        if (pos + 1 < m.Count && m[pos].Kind == MorphemeKind.Unit && m[pos + 1].Kind == MorphemeKind.Hundred)
        {
            value = m[pos].Value * 100;
            pos += 2;
        }
        else if (pos < m.Count && m[pos].Kind == MorphemeKind.Hundred)
        {
            value = 100;
            pos++;
        }

        if (pos < m.Count && m[pos].Kind is MorphemeKind.Teen or MorphemeKind.Tens)
        {
            value += m[pos].Value;
            pos++;
        }
        else if (pos < m.Count && m[pos].Kind == MorphemeKind.Unit)
        {
            if (pos + 2 < m.Count && m[pos + 1].Kind == MorphemeKind.And && m[pos + 2].Kind == MorphemeKind.Tens)
            {
                value += m[pos + 2].Value + m[pos].Value;
                pos += 3;
            }
            else
            {
                value += m[pos].Value;
                pos++;
            }
        }

        return pos > start;
    }

    private static bool ApplyCardinal(
        Cardinal c, NumberSystem sys, ref decimal cur, ref decimal total, ref decimal lastScale, ref Piece last, bool any, bool article, ref bool terminal)
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
                    // French dix-sept, dix-huit, dix-neuf (also soixante-dix-sept): a unit from seven to nine follows "dix".
                    if (!(sys == NumberSystem.French && last == Piece.Teen && cur % 10 == 0 && v >= 7))
                    {
                        return false;
                    }
                }

                cur += v;
                last = Piece.Unit;
                return true;
            case CardinalKind.Teen:
                // French soixante-dix, soixante-treize, quatre-vingt-dix-neuf: the teens follow 60 and 80 (twenty-based counting: four twenties and ten).
                if (cur % 100 != 0 && !(sys == NumberSystem.French && last == Piece.Tens && cur % 100 is 60 or 80))
                {
                    return false;
                }

                cur += v;
                last = Piece.Teen;
                return true;
            case CardinalKind.Tens:
                if (cur % 100 != 0)
                {
                    // French quatre-vingt(s): four twenties.
                    if (sys == NumberSystem.French && v == 20 && last == Piece.Unit && cur % 100 == 4)
                    {
                        cur = cur - 4 + 80;
                        last = Piece.Tens;
                        return true;
                    }

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
                bool bare = c.Kind == CardinalKind.BareScale;
                if (v == 100)
                {
                    if ((article && !any) || (bare && cur == 0))
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

                return ApplyScale(v, bare, ref cur, ref total, ref lastScale, ref last, any, article);
        }
    }

    private static bool ApplyScale(decimal s, bool bare, ref decimal cur, ref decimal total, ref decimal lastScale, ref Piece last, bool any, bool article)
    {
        decimal g = cur;
        if (g == 0)
        {
            if ((article && !any) || bare)
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

    private static bool ApplyOrdinal(decimal v, NumberSystem sys, ref decimal cur, ref decimal total, decimal lastScale, Piece last, bool any, bool article)
    {
        if (v < 20)
        {
            // "first" .. "nineteenth": a unit may also follow tens ("twenty-first").
            bool ok = cur % 100 == 0 || (v < 10 && cur % 100 >= 20 && cur % 10 == 0);
            if (!ok && sys == NumberSystem.French)
            {
                // soixante-dixieme, quatre-vingt-onzieme, dix-septieme
                ok = (last == Piece.Tens && cur % 100 is 60 or 80 && v >= 10) || (last == Piece.Teen && cur % 10 == 0 && v is >= 7 and < 10);
            }

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
                if (sys == NumberSystem.French && v == 20 && last == Piece.Unit && cur % 100 == 4)
                {
                    cur = cur - 4 + 80;
                    return true;
                }

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
        string groupChars = _d.GroupSeparators;
        char grp = '\0';
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
            while (firstRun <= 3 && p + 3 < n && groupChars.Contains(t[p]) && (grp == '\0' || t[p] == grp) && IsDigit(t[p + 1]) && IsDigit(t[p + 2])
                && IsDigit(t[p + 3]) && (p + 4 >= n || !IsDigit(t[p + 4])))
            {
                grp = t[p];
                p += 4;
                grouped = true;
            }
        }

        if (!grouped && !leadingSeparator && p + 3 < n && groupChars == "," && t[p] == ',')
        {
            // Indian digit grouping ("1,00,000", "12,34,567"): the last group has three digits and the ones before it two.
            int run = p - pos;
            int g = p;
            int pairs = 0;
            while (run <= 2 && g + 2 < n && t[g] == ',' && IsDigit(t[g + 1]) && IsDigit(t[g + 2]))
            {
                if (g + 3 < n && IsDigit(t[g + 3]))
                {
                    if (pairs >= 1 && (g + 4 >= n || !IsDigit(t[g + 4])))
                    {
                        p = g + 4;
                        grp = ',';
                        grouped = true;
                    }

                    break;
                }

                pairs++;
                g += 3;
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

        int mantissaEnd = p;
        bool hasExponent = TryExponent(p, out int exponentEnd, out int exponent);
        if (hasExponent)
        {
            p = exponentEnd;
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

        string literal = t.Substring(pos, mantissaEnd - pos);
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

        if (hasExponent && !TryScale(value, exponent, out value))
        {
            return Math.Max(literalEnd, q);
        }

        if (negative)
        {
            value = -value;
        }

        bool plainInteger = !grouped && !hasDecimal && !leadingSeparator && !hasExponent;

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

        // German "3. März", "der 5. Stock": a whole number and a dot is an ordinal when a word follows (see DotOrdinal).
        if (_d.OrdinalDot && plainInteger && !negative && q == literalEnd && literalEnd < n && t[literalEnd] == '.' && literalEnd - pos <= 4 && DotOrdinal(literalEnd + 1))
        {
            _numbers.Add(new NumberCandidate { Start = start, End = literalEnd + 1, Value = value, Ordinal = true });
            return literalEnd + 1;
        }

        if (q > literalEnd)
        {
            // Letters touch the digits: an ordinal ending ("21st"), a multiplier ("3.5k"), or the start of a unit ("5km").
            ReadOnlySpan<char> letters = t.AsSpan(literalEnd, q - literalEnd);
            bool boundary = q >= n || !IsDigit(t[q]);
            if (boundary && !hasDecimal && !leadingSeparator && !hasExponent && !negative && OrdinalSuffixOk(literal, letters))
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

    /// <summary>
    /// Whether the dot after a whole number makes it an ordinal: white space and then a word that starts with a lower-case letter, or a
    /// month name or one of the nouns in the rule data (<c>5. Stock</c>). "Er kaufte 3. Dann ging er." ends a sentence instead.
    /// </summary>
    private bool DotOrdinal(int pos)
    {
        string t = _t;
        int q = pos;
        while (q < t.Length && q - pos < 2 && (t[q] == ' ' || t[q] == ' '))
        {
            q++;
        }

        if (q == pos || q >= t.Length || !char.IsLetter(t[q]))
        {
            return false;
        }

        if (char.IsLower(t[q]))
        {
            return true;
        }

        int e = q;
        while (e < t.Length && Lexer.IsWordChar(t[e]))
        {
            e++;
        }

        string word = t.Substring(q, e - q);
        if (_d.OrdinalDotWords.Contains(word))
        {
            return true;
        }

        if (_d.Temporal.Words.TryGet(word, out List<TWord>? entries))
        {
            foreach (TWord w in entries)
            {
                if (w.Role == "month")
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static readonly string Superscripts = "\u2070\u00B9\u00B2\u00B3\u2074\u2075\u2076\u2077\u2078\u2079";

    /// <summary>
    /// Scientific notation after a mantissa that ends at <paramref name="pos"/>: "e23", "E-5", "×10^3", "x 10^-2", "×10³". The exponent
    /// has at most three digits and must not run into a letter or a digit.
    /// </summary>
    private bool TryExponent(int pos, out int end, out int exponent)
    {
        end = pos;
        exponent = 0;
        string t = _t;
        int n = t.Length;
        int p = pos;
        if (p >= n)
        {
            return false;
        }

        if (t[p] is 'e' or 'E')
        {
            p++;
            return ExponentDigits(ref p, out exponent) && Boundary(p, out end);
        }

        int q = p;
        while (q < n && t[q] == ' ' && q - p < 2)
        {
            q++;
        }

        if (q >= n || t[q] is not ('\u00D7' or 'x' or 'X' or '*'))
        {
            return false;
        }

        q++;
        while (q < n && t[q] == ' ' && q < n)
        {
            q++;
            if (q - p > 4)
            {
                return false;
            }
        }

        if (q + 1 >= n || t[q] != '1' || t[q + 1] != '0')
        {
            return false;
        }

        q += 2;
        if (q < n && t[q] == '^')
        {
            q++;
        }
        else if (q + 1 < n && t[q] == '*' && t[q + 1] == '*')
        {
            q += 2;
        }
        else if (q < n && (Superscripts.Contains(t[q], StringComparison.Ordinal) || t[q] == '\u207B'))
        {
            bool neg = t[q] == '\u207B';
            if (neg)
            {
                q++;
            }

            int value = 0;
            int count = 0;
            while (q < n && Superscripts.IndexOf(t[q]) is int d and >= 0 && count < 3)
            {
                value = (value * 10) + d;
                q++;
                count++;
            }

            if (count == 0)
            {
                return false;
            }

            exponent = neg ? -value : value;
            return Boundary(q, out end);
        }
        else
        {
            return false;
        }

        return ExponentDigits(ref q, out exponent) && Boundary(q, out end);

        bool Boundary(int at, out int e)
        {
            e = at;
            return at >= n || !(IsDigit(t[at]) || Lexer.IsWordChar(t[at]));
        }
    }

    private bool ExponentDigits(ref int p, out int exponent)
    {
        string t = _t;
        exponent = 0;
        bool negative = false;
        if (p < t.Length && t[p] is '+' or '-' or '\u2212')
        {
            negative = t[p] != '+';
            p++;
        }

        int start = p;
        while (p < t.Length && IsDigit(t[p]) && p - start < 4)
        {
            exponent = (exponent * 10) + (t[p] - '0');
            p++;
        }

        if (p == start || exponent > 60 || (p < t.Length && IsDigit(t[p])))
        {
            return false;
        }

        if (negative)
        {
            exponent = -exponent;
        }

        return true;
    }

    /// <summary>Multiplies by a power of ten; false when the result does not fit a decimal or a non-zero mantissa underflows to zero.</summary>
    private static bool TryScale(decimal mantissa, int exponent, out decimal result)
    {
        result = mantissa;
        if (mantissa == 0)
        {
            return true;
        }

        for (int k = 0; k < Math.Abs(exponent); k++)
        {
            if (exponent > 0)
            {
                if (!TryMultiply(result, 10m, out result))
                {
                    return false;
                }
            }
            else
            {
                result /= 10m;
            }
        }

        return result != 0;
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
            _doubt = c.Doubt;
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

        _doubt = false;
        MergeCompounds();
        MergeParentheticalResults();
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
            string suffixKey = _sb.ToString();
            if (!_d.Suffixes.TryGet(suffixKey, out List<SuffixEntry>? entries))
            {
                continue;
            }

            int end = _items[after - 1].End;
            if (_d.AmbiguousMoney.Count > 0 && _d.AmbiguousMoney.TryGetValue(suffixKey, out AmbiguousMoney? money)
                && IsMoneyContext(c, after, money) && TryMultiply(c.Value, money.Factor, out decimal amount))
            {
                _found.Add(MakeQuantity(c.Start, end, QuantityKind.Money, amount, null, null, money.Iso, 0.8f));
                return;
            }

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
                        if (TryMultiply(c.Value, e.Factor, out decimal amountOfMoney))
                        {
                            _found.Add(MakeQuantity(c.Start, end, QuantityKind.Money, amountOfMoney, null, null, e.Name, e.Confidence));
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
        return new Quantity(start, end, _t.Substring(start, end - start), label, _doubt ? confidence * DoubtFactor : confidence, kind, value, unit, dimension, currency);
    }

    // ---- legal-drafting numerals, compound measures, ambiguous currency names --------------------------------------------------

    /// <summary>A numeral in brackets right after a number written in words: "sixty (60) days".</summary>
    private bool ParenNumber(int pos, out int openAt, out int inner)
    {
        openAt = -1;
        inner = -1;
        int j = ItemIndexAt(pos);
        if (j >= _items.Count || !Ch(j, '(') || !GapOk(pos, _items[j].Start))
        {
            return false;
        }

        openAt = _items[j].Start;
        for (int k = 0; k < _numbers.Count; k++)
        {
            NumberCandidate d = _numbers[k];
            if (d.Start == openAt + 1 && !d.Ordinal && !d.Attached && !d.MoneyOnly && d.End < _t.Length && _t[d.End] == ')' && !char.IsLetter(_t[d.Start]))
            {
                if (inner < 0 || _numbers[inner].End < d.End)
                {
                    inner = k;
                }
            }
        }

        return inner >= 0;
    }

    /// <summary>
    /// "sixty (60)": the numeral repeats the words. When the values agree the number grows to include the brackets; when they differ the
    /// words' value stands, with less confidence, and nothing is merged.
    /// </summary>
    private void MergeParentheticals()
    {
        int count = _numbers.Count;
        for (int k = 0; k < count; k++)
        {
            NumberCandidate c = _numbers[k];
            if (c.Ordinal || c.Attached || c.MoneyOnly || !char.IsLetter(_t[c.Start]) || !ParenNumber(c.End, out _, out int inner))
            {
                continue;
            }

            NumberCandidate d = _numbers[inner];
            if (d.Value == c.Value)
            {
                c.End = d.End + 1;
            }
            else
            {
                c.Doubt = true;
            }

            _numbers[k] = c;
        }
    }

    /// <summary>"five thousand dollars ($5,000)", "fifty percent (50%)": the same as <see cref="MergeParentheticals"/> for amounts with a unit.</summary>
    private void MergeParentheticalResults()
    {
        int count = _found.Count;
        for (int k = 0; k < count; k++)
        {
            if (_found[k] is not Quantity q || q.Kind == QuantityKind.Ordinal || q.Kind == QuantityKind.Cardinal || !char.IsLetter(q.Text[0])
                || !OpenBracketAfter(q.End))
            {
                continue;
            }

            int j = ItemIndexAt(q.End);
            if (j >= _items.Count || !Ch(j, '(') || !GapOk(q.End, _items[j].Start))
            {
                continue;
            }

            for (int m = 0; m < count; m++)
            {
                if (_found[m] is not Quantity p || p.Start != _items[j].End)
                {
                    continue;
                }

                bool closed = p.End < _t.Length && _t[p.End] == ')';
                bool same = p.Kind == q.Kind && p.Currency == q.Currency && p.Unit == q.Unit;
                bool plain = p.Kind == QuantityKind.Cardinal && q.Kind is QuantityKind.Money or QuantityKind.Measure;
                if (!closed || !(same || plain))
                {
                    continue;
                }

                if (p.Value == q.Value)
                {
                    _found.Add(q with { End = p.End + 1, Text = _t.Substring(q.Start, p.End + 1 - q.Start) });
                }
                else
                {
                    _found[k] = q with { Confidence = q.Confidence * DoubtFactor };
                }

                break;
            }
        }
    }

    /// <summary>A cheap test before the item search: an opening bracket follows after at most two white-space characters.</summary>
    private bool OpenBracketAfter(int pos)
    {
        int p = pos;
        while (p < _t.Length && p - pos < 2 && _t[p] == ' ')
        {
            p++;
        }

        return p < _t.Length && _t[p] == '(';
    }

    /// <summary>"5 feet 3 inches" is one length: two measures of one dimension that the compound rules pair, joined by white space, a comma or "and".</summary>
    private void MergeCompounds()
    {
        if (_d.Compounds.Count == 0)
        {
            return;
        }

        var connectors = _connectors;
        int count = _found.Count;
        for (int k = 0; k < count; k++)
        {
            if (_found[k] is not Quantity a || a.Kind != QuantityKind.Measure || a.Value != decimal.Truncate(a.Value) || a.Value < 0)
            {
                continue;
            }

            int next = ItemIndexAt(a.End);
            if (next < _items.Count && Ch(next, ',') && _items[next].Start == a.End)
            {
                next++;
            }

            if (next < _items.Count && _items[next].Kind == ItemKind.Word && connectors.Contains(Span(next)))
            {
                next++;
            }

            if (next >= _items.Count || _items[next].Start - a.End > 8 || _t.AsSpan(a.End, _items[next].Start - a.End).ContainsAny('\n', '\r'))
            {
                continue;
            }

            for (int m = 0; m < count; m++)
            {
                if (_found[m] is Quantity b && b.Start == _items[next].Start && b.Kind == QuantityKind.Measure && a.Unit is not null && b.Unit is not null
                    && b.Value >= 0 && _d.Compounds.TryGetValue(a.Unit + "|" + b.Unit, out decimal factor) && b.Value < factor)
                {
                    _found.Add(MakeQuantity(a.Start, b.End, QuantityKind.Measure, (a.Value * factor) + b.Value, b.Unit, b.Dimension, null, Math.Min(a.Confidence, b.Confidence)));
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Whether the words around "1,000 pounds" talk about money rather than weight: some word of the clause within five words before the
    /// number or four after the unit is a money word and none is a weight word.
    /// </summary>
    private bool IsMoneyContext(NumberCandidate c, int unitAfter, AmbiguousMoney rule)
    {
        bool money = false;
        int first = ItemIndexAt(c.Start);
        int checkedWords = 0;
        for (int k = first - 1; k >= 0 && checkedWords < 5; k--)
        {
            if (!ClauseWord(k, ref checkedWords, rule, ref money, out bool stop) || stop)
            {
                if (stop)
                {
                    break;
                }

                return false;
            }
        }

        checkedWords = 0;
        for (int k = unitAfter; k < _items.Count && checkedWords < 4; k++)
        {
            if (!ClauseWord(k, ref checkedWords, rule, ref money, out bool stop) || stop)
            {
                if (stop)
                {
                    break;
                }

                return false;
            }
        }

        return money;
    }

    // Looks at one item of the context window: false when it is a weight word (the answer is "not money"), stop at the end of a clause.
    private bool ClauseWord(int k, ref int words, AmbiguousMoney rule, ref bool money, out bool stop)
    {
        stop = false;
        Item it = _items[k];
        if (it.Kind == ItemKind.Digits)
        {
            // Another number starts another phrase ("1,000 pounds he bought 3 pounds of tea").
            stop = true;
            return true;
        }

        if (it.Kind == ItemKind.Other)
        {
            if (_t[it.Start] is '.' or ';' or '!' or '?' or ':')
            {
                stop = true;
            }

            return true;
        }

        if (it.Kind == ItemKind.Word)
        {
            words++;
            string w = _t.Substring(it.Start, it.End - it.Start);
            if (rule.OtherWords.Contains(w))
            {
                return false;
            }

            money |= rule.MoneyWords.Contains(w);
        }

        return true;
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
