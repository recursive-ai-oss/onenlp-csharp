using System.Globalization;
using System.Text;

namespace RecursiveAI.OneNlp.Recognizers;

/// <summary>Date and time recognition: infrastructure, times of day, durations, recurrences and the joining of dates with times.</summary>
internal sealed partial class Scan
{
    private readonly record struct VocabHit(int N, List<TWord> Entries);

    /// <summary>A reading of items <c>[S, E)</c>.</summary>
    private readonly record struct Cand(int S, int E, TemporalValue V, float C = 1f);

    private TemporalData? _td;
    private DateTimeOffset? _ref;
    private DateTime _today = new(2000, 1, 1);
    private DateTime _now = new(2000, 1, 1);
    private int _region = TemporalData.RegionUs;
    private VocabHit[]?[]? _vocab;
    private bool[]? _vocabDone;
    private Dictionary<int, NumberCandidate>? _numAt;
    private HashSet<string>.AlternateLookup<ReadOnlySpan<char>> _starts;

    private void InitTemporal(RecognitionContext context)
    {
        _td = _d.Temporal;
        if (_td.IsEmpty)
        {
            return;
        }

        _starts = _td.Starts.GetAlternateLookup<ReadOnlySpan<char>>();
        _ref = context.ReferenceDate;
        if (_ref is DateTimeOffset r)
        {
            _today = r.DateTime.Date;
            _now = r.DateTime;
        }

        int region = TemporalData.RegionOrNone(context.Region);
        _region = region != 0 ? region : _td.DefaultRegion != 0 ? _td.DefaultRegion : TemporalData.RegionUs;
    }

    /// <summary>Finds the dates, times, ranges, durations and recurrences, and adds them to the results.</summary>
    private void FindTemporal()
    {
        if (_td is null || _td.IsEmpty || _items.Count == 0)
        {
            return;
        }

        _vocab = new VocabHit[]?[_items.Count];
        _vocabDone = new bool[_items.Count];
        var cands = new List<Cand>();
        for (int i = 0; i < _items.Count; i++)
        {
            Item it = _items[i];
            if (it.Kind == ItemKind.Other || !FreeLeft(i))
            {
                continue;
            }

            if (it.Kind == ItemKind.Word && Hits(i).Length == 0 && !NumStarts(i))
            {
                continue;
            }

            cands.Clear();
            try
            {
                ExpressionsAt(i, cands);
                if (it.Kind == ItemKind.Word && !_td!.Flags.Contains("determiner-inside-only") && Is(i, "determiner", out int dn) && i + dn < _items.Count && GapOk(_items[i + dn - 1].End, _items[i + dn].Start))
                {
                    // "le 20 mars", "la semaine prochaine", "den Muttertag": a determiner is part of the expression it introduces.
                    var inner = new List<Cand>();
                    ExpressionsAt(i + dn, inner);
                    foreach (Cand c in inner)
                    {
                        cands.Add(c with { S = i });
                    }
                }
            }
            catch (Exception e) when (e is ArgumentOutOfRangeException or OverflowException)
            {
                // A value outside the calendar (year 10000, a million years ago): the expression is left out.
                continue;
            }

            foreach (Cand c in cands)
            {
                Emit(c);
            }
        }
    }

    private void ExpressionsAt(int i, List<Cand> cands)
    {
        TimeRangeAt(i, cands);
        DateTimeRangeAt(i, cands);
        DateRangeAt(i, cands);
        DateAt(i, null, cands);
        DateTimeAt(i, null, cands);
        TimeAt(i, cands, false);
        DurationAt(i, cands);
        SetAt(i, cands);
    }

    private void Emit(Cand c)
    {
        TemporalValue v = c.V;
        if ((v.NeedsRef && _ref is null) || v.PartOfDay || c.E <= c.S || !FreeRight(c.E))
        {
            return;
        }

        int start = _items[c.S].Start;
        int end = _items[c.E - 1].End;
        TimeSpan? length = v.Kind == TemporalKind.Duration && v.Seconds is decimal s ? TimeSpan.FromSeconds((double)s) : null;
        string label = v.Label;
        _found.Add(new TemporalMatch(
            start, end, _t.Substring(start, end - start), label, c.C, v.Kind, v.Format(), v.Kind == TemporalKind.Duration ? null : v.Grain,
            v.Approx, v.Offset is int o ? TimeSpan.FromMinutes(o) : null, v.ToRange(_ref), length)
        {
            Unit = v.DurUnit,
            Count = v.Count,
        });
    }

    // ---- items and vocabulary -------------------------------------------------------------------------------------------------

    private bool Ch(int i, char c) =>
        i >= 0 && i < _items.Count && _items[i].Kind == ItemKind.Other && _items[i].End - _items[i].Start == 1 && _t[_items[i].Start] == c;

    private bool Dig(int i, int len = 0) =>
        i >= 0 && i < _items.Count && _items[i].Kind == ItemKind.Digits && (len == 0 || _items[i].End - _items[i].Start == len);

    private bool Tch(int a, int b) => a >= 0 && b < _items.Count && _items[a].End == _items[b].Start;

    /// <summary>The value of a digit item, or -1 when it has more than nine digits.</summary>
    private int Val(int i)
    {
        Item it = _items[i];
        if (it.End - it.Start > 9)
        {
            return -1;
        }

        int v = 0;
        for (int k = it.Start; k < it.End; k++)
        {
            v = (v * 10) + (_t[k] - '0');
        }

        return v;
    }

    private const string Glue = "-/:+@#$_%&=";

    /// <summary>An expression does not start in the middle of a word, number or address.</summary>
    private bool FreeLeft(int i)
    {
        if (i == 0 || !Tch(i - 1, i))
        {
            return true;
        }

        Item p = _items[i - 1];
        return p.Kind == ItemKind.Other && !Glue.Contains(_t[p.Start]) && _t[p.Start] != '.';
    }

    /// <summary>An expression does not stop in the middle of a word, number or address.</summary>
    private bool FreeRight(int e)
    {
        if (e >= _items.Count || !Tch(e - 1, e))
        {
            return true;
        }

        Item n = _items[e];
        if (n.Kind != ItemKind.Other)
        {
            return false;
        }

        char c = _t[n.Start];
        if (c is '.' or ',')
        {
            return !(e + 1 < _items.Count && Tch(e, e + 1) && _items[e + 1].Kind != ItemKind.Other);
        }

        return !Glue.Contains(c);
    }

    private VocabHit[] Hits(int i)
    {
        if (i < 0 || i >= _items.Count)
        {
            return [];
        }

        if (_vocabDone![i])
        {
            return _vocab![i] ?? [];
        }

        _vocabDone[i] = true;
        Item first = _items[i];
        if (!_starts.Contains(_t.AsSpan(first.Start, first.End - first.Start)))
        {
            return [];
        }

        List<VocabHit>? hits = null;
        int max = Math.Min(_td!.Words.MaxItems, _items.Count - i);
        var sb = new StringBuilder();
        for (int n = 1; n <= max; n++)
        {
            if (n > 1 && !GapOk(_items[i + n - 2].End, _items[i + n - 1].Start))
            {
                break;
            }

            if (n > 1 && _items[i + n - 1].Start > _items[i + n - 2].End)
            {
                sb.Append(' ');
            }

            Item it = _items[i + n - 1];
            for (int k = it.Start; k < it.End; k++)
            {
                sb.Append(_t[k] == '\u2019' ? '\'' : _t[k]);
            }

            if (_td.Words.TryGet(sb.ToString(), out List<TWord>? entries))
            {
                (hits ??= []).Add(new VocabHit(n, entries));
            }
        }

        if (hits is null)
        {
            return [];
        }

        hits.Reverse();
        _vocab![i] = [.. hits];
        return _vocab[i]!;
    }

    /// <summary>The longest vocabulary entry with a role at an item.</summary>
    private bool Find(int i, string role, out int n, out TWord word)
    {
        n = 0;
        word = default;
        if (i < 0 || i >= _items.Count)
        {
            return false;
        }

        foreach (VocabHit h in Hits(i))
        {
            foreach (TWord w in h.Entries)
            {
                if (w.Role == role)
                {
                    n = h.N;
                    word = w;
                    return true;
                }
            }
        }

        return false;
    }

    private bool Is(int i, string role, out int n) => Find(i, role, out n, out _);

    private bool Is(int i, string role) => Find(i, role, out _, out _);

    /// <summary>A number found by the number rules that starts at an item, is not glued to letters, and is not a sign or a money suffix.</summary>
    private bool Num(int i, out int end, out decimal value, out bool ordinal)
    {
        end = i;
        value = 0;
        ordinal = false;
        if (i < 0 || i >= _items.Count)
        {
            return false;
        }

        if (_numAt is null)
        {
            _numAt = [];
            foreach (NumberCandidate c in _numbers)
            {
                if (c.Attached || c.MoneyOnly)
                {
                    continue;
                }

                if (!_numAt.TryGetValue(c.Start, out NumberCandidate old) || old.End < c.End)
                {
                    _numAt[c.Start] = c;
                }
            }
        }

        if (!_numAt.TryGetValue(_items[i].Start, out NumberCandidate n))
        {
            return false;
        }

        end = ItemIndexAt(n.End);
        value = n.Value;
        ordinal = n.Ordinal;
        return end > i;
    }

    private bool NumStarts(int i) => Num(i, out _, out _, out _);

    // ---- times of day ---------------------------------------------------------------------------------------------------------

    /// <summary>A zone after a time: <c>PST</c>, <c>UTC</c>, <c>UTC+2</c>, <c>GMT-05:00</c>.</summary>
    private bool ZoneAt(int j, out int end, out int offset, out bool isZ)
    {
        end = j;
        offset = 0;
        isZ = false;
        if (j >= _items.Count || !GapOk(_items[j - 1].End, _items[j].Start))
        {
            return false;
        }

        if (Find(j, "zonebase", out int n, out _))
        {
            int k = j + n;
            if ((Ch(k, '+') || Ch(k, '-') || Ch(k, '\u2212')) && Tch(k - 1, k) && Dig(k + 1) && Tch(k, k + 1))
            {
                int sign = Ch(k, '+') ? 1 : -1;
                int digits = _items[k + 1].End - _items[k + 1].Start;
                int h;
                int m = 0;
                int e = k + 2;
                if (digits <= 2)
                {
                    h = Val(k + 1);
                    if (Ch(e, ':') && Tch(e - 1, e) && Dig(e + 1, 2) && Tch(e, e + 1))
                    {
                        m = Val(e + 1);
                        e += 2;
                    }
                }
                else if (digits == 4)
                {
                    int v = Val(k + 1);
                    h = v / 100;
                    m = v % 100;
                }
                else
                {
                    return false;
                }

                if (h > 14 || m > 59)
                {
                    return false;
                }

                end = e;
                offset = sign * ((h * 60) + m);
                return true;
            }
        }

        if (Find(j, "zone", out n, out TWord z))
        {
            end = j + n;
            offset = z.A;
            isZ = z.B == 1;
            return true;
        }

        return false;
    }

    /// <summary>Times of day starting at an item (with an optional <c>about</c> before, and a zone after).</summary>
    private void TimeAt(int i, List<Cand> r, bool allowBare)
    {
        int start = i;
        bool approx = false;

        // Spanish and Portuguese "las 15:00", "às dez da manhã": the article (or fused preposition and article) that introduces a clock time is part of it.
        bool articled = false;
        if ((Is(i, "hourarticle", out int arn) || Is(i, "determiner", out arn)) && i + arn < _items.Count && GapOk(_items[i + arn - 1].End, _items[i + arn].Start))
        {
            articled = true;
            i += arn;
        }

        if (Is(i, "approx", out int an) && i + an < _items.Count)
        {
            approx = true;
            i += an;
        }

        var clocks = new List<Cand>();
        ClockAt(i, clocks, allowBare);
        if (articled)
        {
            SpokenClock(i, clocks);
        }

        foreach (Cand c in clocks)
        {
            TemporalValue v = c.V;
            v.Approx = approx;
            r.Add(c with { S = start });
            if (!v.Bare && ZoneAt(c.E, out int ze, out int off, out bool z))
            {
                TemporalValue withZone = v.Clone();
                withZone.Offset = off;
                withZone.ZoneIsZ = z;
                r.Add(new Cand(start, ze, withZone, c.C));
            }
        }
    }

    /// <summary>The word that marks an hour on the 24-hour clock: <c>h</c> and <c>Uhr</c> (strong), <c>heures</c> (weak: also a length of time).</summary>
    private bool HourMark(int k, out int end, out bool weak)
    {
        end = k;
        weak = false;
        if (k <= 0 || k >= _items.Count || !GapOk(_items[k - 1].End, _items[k].Start))
        {
            return false;
        }

        if (Find(k, "hourmark", out int n, out _))
        {
            end = k + n;
            return true;
        }

        if (Find(k, "hourword", out n, out _))
        {
            end = k + n;
            weak = true;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Times written with an hour mark or spoken: <c>15h30</c>, <c>7 h 05</c>, <c>15 Uhr</c>, <c>huit heures du soir</c>, <c>trois heures et quart</c>,
    /// <c>quinze heures trente</c>, <c>halb drei</c>, <c>Viertel nach drei</c>. A weak mark (<c>heures</c>) is a time only after an at-word or with
    /// minutes or a part of the day, because "deux heures" is also a length of time.
    /// </summary>
    private void ClockMarked(int i, List<Cand> r)
    {
        if (_td!.Words.Count == 0)
        {
            return;
        }

        if (Find(i, "halfbefore", out int hn, out _) || Find(i, "plusquarter", out hn, out _) || Find(i, "minusquarter", out hn, out _))
        {
            SpokenHour(i, hn, r);
            return;
        }

        int h;
        int hEnd;
        if (Dig(i) && _items[i].End - _items[i].Start <= 2)
        {
            h = Val(i);
            hEnd = i + 1;
        }
        else if (Num(i, out hEnd, out decimal hv, out bool hOrd) && !hOrd && hv is >= 0 and <= 24 && hv == decimal.Truncate(hv))
        {
            h = (int)hv;
        }
        else
        {
            return;
        }

        if (h > 23 || !HourMark(hEnd, out int e, out bool weak))
        {
            return;
        }

        int m = 0;
        bool clockTail = false;
        Granularity grain = Granularity.Hour;
        if (Dig(e, 2) && Val(e) <= 59 && GapOk(_items[e - 1].End, _items[e].Start) && !(Ch(e + 1, ':') && Tch(e, e + 1)))
        {
            m = Val(e);
            e++;
            grain = Granularity.Minute;
        }
        else if (Num(e, out int me, out decimal mv, out bool mOrd) && !mOrd && mv is >= 1 and <= 59 && mv == decimal.Truncate(mv) && GapOk(_items[e - 1].End, _items[e].Start))
        {
            m = (int)mv;
            e = me;
            grain = Granularity.Minute;
        }
        else if (Is(e, "plus15", out int pn))
        {
            m = 15;
            e += pn;
            clockTail = true;
            grain = Granularity.Minute;
        }
        else if (Is(e, "plus30", out pn))
        {
            m = 30;
            e += pn;
            grain = Granularity.Minute;
        }
        else if (Is(e, "minus15", out pn))
        {
            h = (h + 23) % 24;
            m = 45;
            e += pn;
            clockTail = true;
            grain = Granularity.Minute;
        }
        else if (Is(e, "minus", out pn) && Num(e + pn, out int le, out decimal lv, out bool lOrd) && !lOrd && lv is >= 1 and <= 29 && lv == decimal.Truncate(lv))
        {
            h = (h + 23) % 24;
            m = 60 - (int)lv;
            e = le;
            clockTail = true;
            grain = Granularity.Minute;
        }

        bool part = false;
        if (DaypartPhrase(e, out int de, out TWord daypart) && h is >= 1 and <= 12)
        {
            bool afternoon = daypart.A >= 12;
            h = afternoon ? (h % 12) + 12 : h % 12;
            e = de;
            part = true;
        }

        if (weak && !part && !clockTail && !(i > 0 && (Is(i - 1, "at") || Is(i - 1, "clockcue")) && GapOk(_items[i - 1].End, _items[i].Start)))
        {
            return;
        }

        TemporalValue t = TemporalValue.Clock(h, m, 0, grain);
        t.Clock24 = true;
        t.Meridiem = part;
        r.Add(new Cand(i, e, t));
    }

    /// <summary>
    /// An hour written as a digit or a number word after an article, <c>las diez de la mañana</c>, <c>las tres y media</c>, <c>às oito da noite</c>,
    /// <c>las 15</c>. Without a part of the day or a tail (<c>y media</c>), only an hour above twelve is a time: "las 7" alone is not.
    /// </summary>
    private void SpokenClock(int i, List<Cand> r)
    {
        int h;
        int e;
        if (Dig(i) && _items[i].End - _items[i].Start <= 2)
        {
            h = Val(i);
            e = i + 1;
        }
        else if (Num(i, out e, out decimal hv, out bool hOrd) && !hOrd && hv is >= 1 and <= 23 && hv == decimal.Truncate(hv))
        {
            h = (int)hv;
        }
        else
        {
            return;
        }

        if (h > 23 || (Ch(e, ':') && Tch(e - 1, e)))
        {
            return;
        }

        int m = 0;
        bool tail = false;
        Granularity grain = Granularity.Hour;
        if (Is(e, "plus15", out int pn))
        {
            m = 15;
            e += pn;
            tail = true;
        }
        else if (Is(e, "plus30", out pn))
        {
            m = 30;
            e += pn;
            tail = true;
        }
        else if (Is(e, "minus15", out pn))
        {
            h = (h + 23) % 24;
            m = 45;
            e += pn;
            tail = true;
        }
        else if (Is(e, "minus", out pn) && Num(e + pn, out int le, out decimal lv, out bool lOrd) && !lOrd && lv is >= 1 and <= 29 && lv == decimal.Truncate(lv))
        {
            h = (h + 23) % 24;
            m = 60 - (int)lv;
            e = le;
            tail = true;
        }

        if (tail)
        {
            grain = Granularity.Minute;
        }

        bool part = false;
        if (DaypartPhrase(e, out int de, out TWord daypart) && h is >= 1 and <= 12)
        {
            h = daypart.A >= 12 ? (h % 12) + 12 : h % 12;
            e = de;
            part = true;
        }

        if (!part && !tail && h < 13)
        {
            return;
        }

        TemporalValue t = TemporalValue.Clock(h, m, 0, grain);
        t.Clock24 = !part;
        t.Meridiem = part;
        r.Add(new Cand(i, e, t));
    }

    // "halb drei" is 2:30, "Viertel nach drei" 3:15, "Viertel vor drei" 2:45: the hour that follows is a digit or a number word.
    private void SpokenHour(int i, int wordItems, List<Cand> r)
    {
        int k = i + wordItems;
        int h;
        int e;
        if (Dig(k) && _items[k].End - _items[k].Start <= 2 && GapOk(_items[k - 1].End, _items[k].Start))
        {
            h = Val(k);
            e = k + 1;
        }
        else if (k < _items.Count && GapOk(_items[k - 1].End, _items[k].Start) && Num(k, out e, out decimal v, out bool ord) && !ord && v is >= 1 and <= 12
            && v == decimal.Truncate(v))
        {
            h = (int)v;
        }
        else
        {
            return;
        }

        if (h is < 1 or > 12)
        {
            return;
        }

        bool half = Is(i, "halfbefore");
        bool minusQuarter = !half && Is(i, "minusquarter");
        TemporalValue t;
        if (half)
        {
            // 12:30 for "halb eins"; otherwise the half hour before the stated hour.
            t = TemporalValue.Clock(h == 1 ? 12 : h - 1, 30, 0, Granularity.Minute);
        }
        else if (minusQuarter)
        {
            t = TemporalValue.Clock(h - 1, 45, 0, Granularity.Minute);
        }
        else
        {
            t = TemporalValue.Clock(h % 12, 15, 0, Granularity.Minute);
        }

        t.Meridiem = true;
        r.Add(new Cand(i, e, t));
    }

    private bool Meridiem(int k, out int end, out bool pm)
    {
        end = k;
        pm = false;
        if (k >= _items.Count || !GapOk(_items[k - 1].End, _items[k].Start) || !Find(k, "meridiem", out int n, out TWord w))
        {
            return false;
        }

        end = k + n;
        pm = w.A == 1;
        return true;
    }

    /// <summary>A part of the day after a clock hour: <c>in the afternoon</c>, <c>at night</c>.</summary>
    private bool DaypartPhrase(int k, out int end, out TWord part)
    {
        end = k;
        part = default;
        int j = k;
        if (Is(j, "in", out int n) || Is(j, "at", out n) || Is(j, "daypartprefix", out n))
        {
            j += n;
            if (Is(j, "the", out int t))
            {
                j += t;
            }

            if (Find(j, "daypart", out int dn, out part))
            {
                end = j + dn;
                return true;
            }
        }

        // German "5 Uhr nachmittags": the part of the day follows the hour without a word between.
        if (_td!.Flags.Contains("daypart-direct") && k < _items.Count && GapOk(_items[k - 1].End, _items[k].Start) && Find(k, "daypart", out int direct, out part))
        {
            end = k + direct;
            return true;
        }

        return false;
    }

    private void ClockAt(int i, List<Cand> r, bool allowBare)
    {
        ClockMarked(i, r);
        if (Find(i, "noon", out int n, out _))
        {
            int ne = i + n;
            int half = Is(ne, "plus30", out int pn) ? 30 : Is(ne, "plus15", out pn) ? 15 : 0;
            if (half > 0 && GapOk(_items[ne - 1].End, _items[ne].Start))
            {
                // "midi et demi", "midi et quart"
                r.Add(new Cand(i, ne + pn, Mark(TemporalValue.Clock(12, half, 0, Granularity.Minute), meridiem: true)));
            }

            r.Add(new Cand(i, ne, Mark(TemporalValue.Clock(12, 0, 0, Granularity.Hour), meridiem: true)));
            return;
        }

        // Italian "le sei del mattino", "tre e mezza del pomeriggio": an hour written as a word, then the part of the day. Only where the
        // rule data allows it (flag word-hour): in other languages it would also read English "five in the afternoon" differently.
        if (_td!.Flags.Contains("word-hour") && i < _items.Count && _items[i].Kind == ItemKind.Word && _cardinals.TryGetValue(Span(i), out Cardinal hc)
            && hc.Kind is CardinalKind.Unit or CardinalKind.Teen && hc.Value is >= 1 and <= 12)
        {
            int we = i + 1;
            int wm = 0;
            if (Is(we, "plus30", out int wp) && GapOk(_items[we - 1].End, _items[we].Start))
            {
                wm = 30;
                we += wp;
            }
            else if (Is(we, "plus15", out wp) && GapOk(_items[we - 1].End, _items[we].Start))
            {
                wm = 15;
                we += wp;
            }

            if (DaypartPhrase(we, out int wd, out TWord wpart))
            {
                int hh = (int)hc.Value;
                r.Add(new Cand(i, wd, Mark(TemporalValue.Clock(wpart.A >= 12 ? (hh % 12) + 12 : hh % 12, wm, 0, wm > 0 ? Granularity.Minute : Granularity.Hour), meridiem: true)));
            }
        }

        if (Find(i, "midnight", out n, out _))
        {
            int ne = i + n;
            int half = Is(ne, "plus30", out int pn) ? 30 : Is(ne, "plus15", out pn) ? 15 : 0;
            if (half > 0 && GapOk(_items[ne - 1].End, _items[ne].Start))
            {
                r.Add(new Cand(i, ne + pn, Mark(TemporalValue.Clock(0, half, 0, Granularity.Minute), meridiem: true)));
            }

            r.Add(new Cand(i, ne, Mark(TemporalValue.Clock(0, 0, 0, Granularity.Hour), meridiem: true)));
            return;
        }

        if (!Dig(i) || _items[i].End - _items[i].Start > 2)
        {
            return;
        }

        int h = Val(i);
        int k = i + 1;
        int m = 0;
        int s = 0;
        Granularity grain = Granularity.Hour;
        bool colon = false;
        if (Ch(k, ':') && Tch(i, k) && Dig(k + 1, 2) && Tch(k, k + 1))
        {
            colon = true;
            m = Val(k + 1);
            k += 2;
            grain = Granularity.Minute;
            if (Ch(k, ':') && Tch(k - 1, k) && Dig(k + 1, 2) && Tch(k, k + 1))
            {
                s = Val(k + 1);
                k += 2;
                grain = Granularity.Second;
            }
        }

        if (!colon && Ch(k, '.') && Tch(i, k) && Dig(k + 1, 2) && Tch(k, k + 1) && (Meridiem(k + 2, out _, out _) || HourMark(k + 2, out _, out _)))
        {
            // British "3.30pm": a dot is a time separator only with am/pm after it, because "3.30" alone is a decimal.
            colon = true;
            m = Val(k + 1);
            k += 2;
            grain = Granularity.Minute;
        }

        if (m > 59 || s > 59)
        {
            return;
        }

        bool leadingZero = _t[_items[i].Start] == '0' && _items[i].End - _items[i].Start == 2;
        if (Meridiem(k, out int me, out bool pm))
        {
            if (h is < 1 or > 12)
            {
                return;
            }

            r.Add(new Cand(i, me, Mark(TemporalValue.Clock((h % 12) + (pm ? 12 : 0), m, s, grain), meridiem: true)));
            return;
        }

        if (DaypartPhrase(k, out int de, out TWord part) && h is >= 1 and <= 12)
        {
            bool afternoon = part.A >= 12;
            r.Add(new Cand(i, de, Mark(TemporalValue.Clock(afternoon ? (h % 12) + 12 : h % 12, m, s, grain), meridiem: true)));
            return;
        }

        if (!colon && Is(k, "oclock", out int on) && h is >= 1 and <= 12)
        {
            int e = k + on;
            bool afternoon = false;
            bool hasPart = false;
            if (DaypartPhrase(e, out int pe, out TWord p))
            {
                e = pe;
                afternoon = p.A >= 12;
                hasPart = true;
            }

            if (hasPart)
            {
                r.Add(new Cand(i, e, Mark(TemporalValue.Clock(afternoon ? (h % 12) + 12 : h % 12, 0, 0, Granularity.Hour), meridiem: true)));
            }

            return;
        }

        if (colon && h <= 23)
        {
            // 24-hour evidence: a leading zero, or an hour that 12-hour clocks do not have. Otherwise the hour can still lend an am/pm to a range.
            TemporalValue t = TemporalValue.Clock(h, m, s, grain);
            t.Clock24 = leadingZero || h >= 13 || h == 0;
            if (HourMark(k, out int markEnd, out bool weak) && !weak)
            {
                // "15:30 Uhr"
                t.Clock24 = true;
                k = markEnd;
            }

            r.Add(new Cand(i, k, t));
            return;
        }

        if (!colon && allowBare && (h is >= 1 and <= 12 || (h is >= 13 and <= 23 && _td!.Flags.Contains("bare-hour-24"))) && (k >= _items.Count || !Tch(i, k) || _items[k].Kind == ItemKind.Other))
        {
            TemporalValue t = TemporalValue.Clock(h, 0, 0, Granularity.Hour);
            t.Bare = true;
            r.Add(new Cand(i, k, t));
        }
    }

    private static TemporalValue Mark(TemporalValue v, bool meridiem)
    {
        v.Meridiem = meridiem;
        return v;
    }

    /// <summary>A range of times of day: <c>from 3pm to 5pm</c>, <c>9am-5pm</c>, <c>between 12 and 1 pm</c>.</summary>
    private void TimeRangeAt(int i, List<Cand> r)
    {
        int j = i;
        bool between = false;
        bool framed = false;
        if (Is(j, "from", out int n))
        {
            j += n;
            framed = true;
        }
        else if (Is(j, "between", out n))
        {
            j += n;
            between = true;
            framed = true;
        }

        var firsts = new List<Cand>();
        TimeAt(j, firsts, true);
        foreach (Cand a in firsts)
        {
            int k = a.E;
            if (between ? !Is(k, "and", out n) : !Is(k, "rangeto", out n))
            {
                continue;
            }

            var seconds = new List<Cand>();
            TimeAt(k + n, seconds, framed && _td!.Flags.Contains("bare-range-24"));
            foreach (Cand b in seconds)
            {
                TemporalValue? range = JoinTimes(a.V, b.V, framed, framed && _td!.Flags.Contains("bare-range-24"));
                if (range is not null)
                {
                    // "van 8 tot 12 uur": the hour word after the second end belongs to the range.
                    int rangeEnd = b.E;
                    if (rangeEnd < _items.Count && Is(rangeEnd, "hourword", out int hwn) && GapOk(_items[rangeEnd - 1].End, _items[rangeEnd].Start))
                    {
                        rangeEnd += hwn;
                    }

                    r.Add(new Cand(i, rangeEnd, range));
                }
            }
        }
    }

    /// <summary>Joins the two ends of a range of times. <paramref name="bareRange"/>: both ends may be bare hours on the 24-hour clock (Italian <c>dalle 8 alle 12</c>).</summary>
    private static TemporalValue? JoinTimes(TemporalValue a, TemporalValue b, bool framed, bool bareRange)
    {
        DateTime start = a.Start;
        if (a.Bare || (!a.Meridiem && !a.Clock24 && b.Meridiem))
        {
            if ((b.Clock24 && !b.Meridiem) || (bareRange && b.Bare && !b.Meridiem))
            {
                // "de 9 a 17h", "von 9 bis 17 Uhr": the second end is on the 24-hour clock, so the first hour is read as written.
                start = a.Start;
            }
            else
            {
                if (!b.Meridiem)
                {
                    return null;
                }

                // "between 12 and 1 pm": the first hour takes the second's am or pm, unless that puts it after the end ("between 11 and 1 pm").
                bool pm = b.Start.Hour >= 12;
                int h = a.Start.Hour;
                DateTime moved = TemporalValue.NoDate.AddHours((h % 12) + (pm ? 12 : 0)).AddMinutes(a.Start.Minute).AddSeconds(a.Start.Second);
                if (moved > b.Start && pm)
                {
                    moved = moved.AddHours(-12);
                }

                start = moved;
            }
        }
        else if (!framed && !a.Meridiem && !b.Meridiem)
        {
            // "10:00-11:00" is a range; "5:30 to 6:30" with no frame and no am/pm is too vague.
            if (!a.Clock24 && !b.Clock24)
            {
                return null;
            }
        }

        Granularity grain = a.Grain == Granularity.Second || b.Grain == Granularity.Second ? Granularity.Second
            : a.Grain == Granularity.Minute || b.Grain == Granularity.Minute || start.Minute != 0 || b.Start.Minute != 0 ? Granularity.Minute
            : Granularity.Hour;
        var v = new TemporalValue
        {
            Kind = TemporalKind.TimeRange,
            Start = start,
            End = b.Start,
            Grain = grain,
            Approx = a.Approx || b.Approx,
            Offset = b.Offset ?? a.Offset,
            ZoneIsZ = b.Offset is not null ? b.ZoneIsZ : a.ZoneIsZ,
        };
        return v;
    }

    // ---- durations ------------------------------------------------------------------------------------------------------------

    /// <summary>An amount before a unit: a number, <c>a</c>/<c>an</c>, or <c>half a</c>.</summary>
    private bool Amount(int i, out int end, out decimal value, out bool article)
    {
        article = false;
        if (Num(i, out end, out value, out bool ord) && !ord)
        {
            // "2 and a half": the number rules read the words but not digits before "and a half".
            if (_items[i].Kind == ItemKind.Digits && value == decimal.Truncate(value) && Is(end, "and", out int an) && Num(end + an, out int fe, out decimal frac, out bool fo)
                && !fo && frac is > 0 and < 1)
            {
                value += frac;
                end = fe;
            }

            return value > 0 && value <= 1_000_000;
        }

        // French "une demi-heure", German "eine halbe Stunde": an article may come before the half, and a hyphen after it.
        int h0 = i;
        if (Is(i, "article", out int pa) && Is(i + pa, "half"))
        {
            h0 = i + pa;
        }

        if (Is(h0, "half", out int n))
        {
            end = h0 + n;
            if (Is(end, "article", out int a))
            {
                end += a;
            }

            if (Ch(end, '-') && Tch(end - 1, end))
            {
                end++;
            }

            value = 0.5m;
            return true;
        }

        if (Is(i, "article", out n))
        {
            end = i + n;
            value = 1;
            article = true;
            return true;
        }

        end = i;
        value = 0;
        return false;
    }

    /// <summary>An amount and a unit: <c>3 days</c>, <c>an hour</c>.</summary>
    private bool Term(int i, out int end, out decimal amount, out TWord unit)
    {
        unit = default;
        end = i;
        if (!Amount(i, out int a, out amount, out bool article) || a >= _items.Count || !GapOk(_items[a - 1].End, _items[a].Start)
            || !Find(a, "unit", out int un, out unit))
        {
            return false;
        }

        // "a second" is far more often an ordinal ("a second chance") than a duration; it is a duration when what follows
        // cannot be the thing that is second: the end of the phrase, or a word such as "ago" or "to" ("wait a second, please").
        if (article && unit.S == "second" && !SecondIsUnit(a + un))
        {
            return false;
        }

        end = a + un;
        return true;
    }

    /// <summary>"a second" counts seconds when the item after it is missing, is punctuation, or is a <c>secondfollow</c> word.</summary>
    private bool SecondIsUnit(int next)
    {
        if (next >= _items.Count || _items[next].Kind == ItemKind.Other)
        {
            return true;
        }

        return _items[next].Kind == ItemKind.Word && Is(next, "secondfollow");
    }

    private void DurationAt(int i, List<Cand> r)
    {
        decimal seconds = 0;
        decimal years = 0;
        decimal months = 0;
        decimal previous = decimal.MaxValue;
        var parts = new List<(decimal Amount, TWord Unit)>();
        int j = i;
        for (int terms = 0; terms < 6; terms++)
        {
            int k = j;
            if (terms > 0)
            {
                if (Ch(k, ',') && Tch(k - 1, k))
                {
                    k++;
                }

                if (Is(k, "and", out int an))
                {
                    k += an;
                }
            }

            if (!Term(k, out int e, out decimal amount, out TWord u))
            {
                break;
            }

            if (u.A == 0 && u.B == 0)
            {
                // Business days and hours have no fixed length, so they cannot be added to other terms: they stand alone.
                if (terms == 0)
                {
                    bool day = u.S == "business day";
                    string n = Trim(amount).ToString(CultureInfo.InvariantCulture);
                    r.Add(new Cand(i, e, new TemporalValue
                    {
                        Kind = TemporalKind.Duration,
                        Text = day ? $"P{n}BD" : $"PT{n}BH",
                        DurUnit = day ? DurationUnit.BusinessDay : DurationUnit.BusinessHour,
                        Count = Trim(amount),
                    }));
                }

                break;
            }

            // "2 hours and 30 minutes": each term is smaller than the one before. "seven days a week" is not one duration.
            decimal size = u.A > 0 ? u.A : u.B * 2_592_000m;
            if (terms > 0 && (size >= previous || _items[k].Kind == ItemKind.Word && Is(k, "article") && !Is(j, "and")))
            {
                break;
            }

            previous = size;

            // Years and months keep their own amounts ("18 months" is P18M, "1 year and 6 months" P1Y6M); only a fraction of a year becomes months.
            seconds += amount * u.A;
            switch (u.S)
            {
                case "year":
                    years += amount;
                    break;
                case "decade":
                    years += amount * 10;
                    break;
                case "month":
                    months += amount;
                    break;
                case "quarter":
                    months += amount * 3;
                    break;
            }

            parts.Add((amount, u));
            j = e;
            int hn = 0;
            int ha = 0;
            int hh = 0;
            if (terms == 0 && amount == decimal.Truncate(amount)
                && ((Is(j, "and", out hn) && Is(j + hn, "article", out ha) && Is(j + hn + ha, "half", out hh)) || (Is(j, "andhalf", out hh) && (hn = ha = 0) == 0)))
            {
                // "an hour and a half", "two days and a half"
                seconds += u.A / 2m;
                months += u.B / 2m;
                if (u.S == "decade")
                {
                    years += 5;
                    months -= u.B / 2m;
                }

                parts[^1] = (amount + 0.5m, u);
                j += hn + ha + hh;
                terms = 6;
            }

            var v = new TemporalValue
            {
                Kind = TemporalKind.Duration,
                Text = TemporalValue.DurationText(seconds, years, months),
                Seconds = years == 0 && months == 0 ? seconds : null,
            };
            CountOf(parts, v);
            r.Add(new Cand(i, j, v));
        }
    }

    /// <summary>
    /// Sets the count and unit of a duration (see <see cref="TemporalMatch.Unit"/>): one term keeps its unit, several are counted in the
    /// smallest, and terms from the two families (clock and days, months and years) cannot be added, so they get none.
    /// </summary>
    private static void CountOf(List<(decimal Amount, TWord Unit)> parts, TemporalValue v)
    {
        if (parts.Count == 1)
        {
            (decimal a, TWord u) = parts[0];
            (v.DurUnit, v.Count) = u.S switch
            {
                "second" => (DurationUnit.Second, a),
                "minute" => (DurationUnit.Minute, a),
                "hour" => (DurationUnit.Hour, a),
                "day" => (DurationUnit.Day, a),
                "week" => (DurationUnit.Week, a * u.A / 604800m),
                "month" => (DurationUnit.Month, a),
                "quarter" => (DurationUnit.Month, a * 3),
                "year" => (DurationUnit.Year, a),
                "decade" => (DurationUnit.Year, a * 10),
                _ => ((DurationUnit?)null, (decimal?)null),
            };
            v.Count = v.Count is decimal c ? Trim(c) : null;
            return;
        }

        if (parts.TrueForAll(p => p.Unit.A > 0))
        {
            decimal total = 0;
            int smallest = int.MaxValue;
            foreach ((decimal a, TWord u) in parts)
            {
                total += a * u.A;
                smallest = Math.Min(smallest, u.A);
            }

            (v.DurUnit, int per) = smallest switch
            {
                1 => ((DurationUnit?)DurationUnit.Second, 1),
                60 => (DurationUnit.Minute, 60),
                3600 => (DurationUnit.Hour, 3600),
                86400 => (DurationUnit.Day, 86400),
                _ => (DurationUnit.Week, 604800),
            };
            v.Count = Trim(total / per);
        }
        else if (parts.TrueForAll(p => p.Unit.B > 0 && p.Unit.A == 0))
        {
            decimal total = 0;
            int smallest = int.MaxValue;
            foreach ((decimal a, TWord u) in parts)
            {
                total += a * u.B;
                smallest = Math.Min(smallest, u.B);
            }

            bool inYears = smallest >= 12;
            v.DurUnit = inYears ? DurationUnit.Year : DurationUnit.Month;
            v.Count = Trim(inYears ? total / 12 : total);
        }
    }

    private static decimal Trim(decimal d) => d / 1.0000000000000000000000000000m;

    // ---- sets -----------------------------------------------------------------------------------------------------------------

    private static string Period(decimal n, string unit)
    {
        string c = n.ToString(CultureInfo.InvariantCulture);
        return unit switch
        {
            "second" => $"PT{c}S",
            "minute" => $"PT{c}M",
            "hour" => $"PT{c}H",
            "day" => $"P{c}D",
            "week" => $"P{c}W",
            "month" => $"P{c}M",
            "quarter" => $"P{n * 3}M",
            "year" => $"P{c}Y",
            _ => $"P{c}D",
        };
    }

    private static Granularity GrainOfUnit(string unit) => unit switch
    {
        "second" => Granularity.Second,
        "minute" => Granularity.Minute,
        "hour" => Granularity.Hour,
        "day" => Granularity.Day,
        "week" => Granularity.Week,
        "month" or "quarter" => Granularity.Month,
        _ => Granularity.Year,
    };

    private void SetAt(int i, List<Cand> r)
    {
        int j = i;
        string? timex = null;
        Granularity grain = Granularity.Day;
        bool dayEvery = false;
        string? weekdayCode = null;
        if (Find(i, "recur", out int n, out TWord rec))
        {
            timex = rec.S;
            grain = (Granularity)rec.A;
            j = i + n;
            dayEvery = rec.S == "P1D";
        }
        else if (Is(i, "every", out n) || Is(i, "each", out n))
        {
            j = i + n;
            if (Find(j, "weekday", out int wn, out TWord wd))
            {
                weekdayCode = "XXXX-WXX-" + wd.A.ToString(CultureInfo.InvariantCulture);
                j += wn;
                grain = Granularity.Day;
            }
            else
            {
                decimal count = 1;
                if (Is(j, "other", out int on))
                {
                    count = 2;
                    j += on;
                }
                else if (Num(j, out int ne, out decimal v, out bool ord) && !ord && v is > 0 and <= 1000 && v == decimal.Truncate(v))
                {
                    count = v;
                    j = ne;
                }

                // "every second" is far more often an ordinal ("every second Tuesday") than a recurrence of seconds.
                if (!Find(j, "unit", out int un, out TWord unit) || (unit.S == "second" && count == 1))
                {
                    return;
                }

                j += un;
                timex = Period(count, unit.S);
                grain = GrainOfUnit(unit.S);
                dayEvery = unit.S == "day" && count == 1;
            }
        }
        else if (Find(i, "weekday", out n, out TWord pw) && pw.B == 1)
        {
            // "Mondays" alone is a weekly recurrence on that day.
            r.Add(new Cand(i, i + n, new TemporalValue { Kind = TemporalKind.Set, Text = "XXXX-WXX-" + pw.A.ToString(CultureInfo.InvariantCulture), Grain = Granularity.Day }, 0.8f));
            return;
        }
        else
        {
            return;
        }

        string plain = weekdayCode ?? timex!;
        r.Add(new Cand(i, j, new TemporalValue { Kind = TemporalKind.Set, Text = plain, Grain = grain }));
        if (weekdayCode is not null || dayEvery)
        {
            // "every Monday at 9am", "every day at noon", "daily at 9am"
            int k = j;
            if (Is(k, "at", out int an))
            {
                k += an;
            }

            var times = new List<Cand>();
            TimeAt(k, times, false);
            foreach (Cand t in times)
            {
                if (t.V.Kind != TemporalKind.Time || t.V.Offset is not null)
                {
                    continue;
                }

                string clock = "T" + t.V.Start.ToString(t.V.Grain == Granularity.Second ? "HH:mm:ss" : "HH:mm", CultureInfo.InvariantCulture);
                r.Add(new Cand(i, t.E, new TemporalValue { Kind = TemporalKind.Set, Text = (weekdayCode ?? string.Empty) + clock, Grain = t.V.Grain }));
            }
        }
    }
}
