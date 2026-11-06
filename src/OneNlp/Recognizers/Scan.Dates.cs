using System.Globalization;

namespace RecursiveAI.OneNlp.Recognizers;

/// <summary>Date recognition: calendar dates, relative days, weekdays, holidays, named periods, ranges, and dates joined with times.</summary>
internal sealed partial class Scan
{
    private Dictionary<int, List<Cand>>? _dateCache;

    private static Cand Day(int s, int e, DateTime d, bool needsRef, float c = 1f) => new(s, e, TemporalValue.Day(d, needsRef), c);

    private static Cand Per(int s, int e, DateTime start, DateTime end, Granularity g, bool approx, bool needsRef, float c = 1f) =>
        new(s, e, TemporalValue.Period(start, end, g, approx, needsRef), c);

    /// <summary>Everything that names a day or a period of days, read from an item. <paramref name="anchor"/> replaces the reference date for expressions that count from "now" (the end of "from Monday to Friday" counts from Monday).</summary>
    private void DateAt(int i, DateTime? anchor, List<Cand> r)
    {
        if (anchor is null)
        {
            _dateCache ??= [];
            if (!_dateCache.TryGetValue(i, out List<Cand>? cached))
            {
                cached = [];
                DateAtCore(i, null, cached);
                _dateCache[i] = cached;
            }

            r.AddRange(cached);
            return;
        }

        DateAtCore(i, anchor, r);
    }

    private void DateAtCore(int i, DateTime? anchor, List<Cand> r)
    {
        bool nr = anchor is null;
        DateTime a = anchor ?? _today;
        NumericDateAt(i, r);
        RelativeDay(i, r);
        OffsetDate(i, r);
        WeekdayDate(i, a, nr, r);
        MonthDate(i, a, nr, r);
        HolidayDate(i, a, nr, r);
        RelativePeriod(i, a, nr, r);
        NamedPeriod(i, a, nr, r);
        OffsetFrom(i, a, nr, r);
        NthWeekday(i, a, nr, r);
    }

    // ---- numeric dates ---------------------------------------------------------------------------------------------------------

    private bool IsoDate(int i, out int end, out DateTime d)
    {
        end = i;
        d = default;
        if (i + 4 >= _items.Count || !Dig(i, 4))
        {
            return false;
        }

        char sep = _t[_items[i + 1].Start];
        if (sep is not ('-' or '/' or '.') || !Ch(i + 1, sep) || !Tch(i, i + 1) || !Dig(i + 2, 2) || !Tch(i + 1, i + 2) || !Ch(i + 3, sep)
            || !Tch(i + 2, i + 3) || !Dig(i + 4, 2) || !Tch(i + 3, i + 4))
        {
            return false;
        }

        int y = Val(i);
        int m = Val(i + 2);
        int day = Val(i + 4);
        if (y < 1000 || m is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(y, m))
        {
            return false;
        }

        end = i + 5;
        d = new DateTime(y, m, day);
        return true;
    }

    private void NumericDateAt(int i, List<Cand> r)
    {
        if (!Dig(i))
        {
            return;
        }

        if (IsoDate(i, out int e, out DateTime d))
        {
            r.Add(Day(i, e, d, false));
            IsoDateTimeAt(i, e, d, r);
            return;
        }

        // 04/15/2026, 15/04/2026, 4-15-2026, 15.4.2026
        if (_items[i].End - _items[i].Start > 2 || i + 4 >= _items.Count)
        {
            return;
        }

        char sep = _t[_items[i + 1].Start];
        if (sep is not ('/' or '-' or '.') || !Ch(i + 1, sep) || !Tch(i, i + 1) || !Dig(i + 2) || _items[i + 2].End - _items[i + 2].Start > 2 || !Tch(i + 1, i + 2)
            || !Ch(i + 3, sep) || !Tch(i + 2, i + 3) || !Dig(i + 4, 4) || !Tch(i + 3, i + 4))
        {
            return;
        }

        int a = Val(i);
        int b = Val(i + 2);
        int y = Val(i + 4);
        if (y < 1000)
        {
            return;
        }

        // Month first in the US, day first in Britain and wherever the first number cannot be a month; a dotted date is day first.
        bool dayFirst = sep == '.' || a > 12 || (_region != TemporalData.RegionUs && b <= 12);
        (int month, int day) = dayFirst ? (b, a) : (a, b);
        if (month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(y, month))
        {
            return;
        }

        r.Add(Day(i, i + 5, new DateTime(y, month, day), false));
    }

    /// <summary>ISO 8601 §5.4.1: <c>2026-06-30T09:15:00Z</c>, with an optional zone designator.</summary>
    private void IsoDateTimeAt(int i, int dateEnd, DateTime day, List<Cand> r)
    {
        int k = dateEnd;
        if (k >= _items.Count || _items[k].Kind != ItemKind.Word || _t[_items[k].Start] != 'T' || _items[k].End - _items[k].Start != 1 || !Tch(k - 1, k)
            || !Dig(k + 1, 2) || !Tch(k, k + 1) || !Ch(k + 2, ':') || !Tch(k + 1, k + 2) || !Dig(k + 3, 2) || !Tch(k + 2, k + 3))
        {
            return;
        }

        int h = Val(k + 1);
        int m = Val(k + 3);
        int s = 0;
        Granularity grain = Granularity.Minute;
        int e = k + 4;
        if (Ch(e, ':') && Tch(e - 1, e) && Dig(e + 1, 2) && Tch(e, e + 1))
        {
            s = Val(e + 1);
            e += 2;
            grain = Granularity.Second;
        }

        if (h > 23 || m > 59 || s > 59)
        {
            return;
        }

        TemporalValue v = TemporalValue.Clock(h, m, s, grain);
        v = AtTime(day, v, false);
        if (e < _items.Count && _items[e].Kind == ItemKind.Word && _t[_items[e].Start] == 'Z' && _items[e].End - _items[e].Start == 1 && Tch(e - 1, e))
        {
            v.Offset = 0;
            v.ZoneIsZ = true;
            e++;
        }
        else if ((Ch(e, '+') || Ch(e, '-')) && Tch(e - 1, e) && Dig(e + 1) && Tch(e, e + 1))
        {
            int digits = _items[e + 1].End - _items[e + 1].Start;
            int oh;
            int om = 0;
            int ne = e + 2;
            if (digits == 2)
            {
                oh = Val(e + 1);
                if (Ch(ne, ':') && Tch(ne - 1, ne) && Dig(ne + 1, 2) && Tch(ne, ne + 1))
                {
                    om = Val(ne + 1);
                    ne += 2;
                }
            }
            else if (digits == 4)
            {
                oh = Val(e + 1) / 100;
                om = Val(e + 1) % 100;
            }
            else
            {
                oh = 99;
                ne = e;
            }

            if (oh <= 14 && om <= 59 && ne > e)
            {
                v.Offset = (Ch(e, '+') ? 1 : -1) * ((oh * 60) + om);
                e = ne;
            }
        }

        r.Add(new Cand(i, e, v));
    }

    // ---- relative days, offsets, weekdays -----------------------------------------------------------------------------------

    private void RelativeDay(int i, List<Cand> r)
    {
        if (Is(i, "today", out int n))
        {
            r.Add(Day(i, i + n, _today, true));
        }
        else if (Is(i, "tomorrow", out n))
        {
            r.Add(Day(i, i + n, _today.AddDays(1), true));
        }
        else if (Is(i, "yesterday", out n))
        {
            r.Add(Day(i, i + n, _today.AddDays(-1), true));
        }
        else if (Is(i, "dayaftertomorrow", out n))
        {
            r.Add(Day(i, i + n, _today.AddDays(2), true));
        }
        else if (Is(i, "daybeforeyesterday", out n))
        {
            r.Add(Day(i, i + n, _today.AddDays(-2), true));
        }
    }

    /// <summary><c>in 3 days</c>, <c>a week ago</c>, <c>30 minutes ago</c>, <c>in an hour</c>.</summary>
    private void OffsetDate(int i, List<Cand> r)
    {
        int j = i;
        bool inForm = false;
        bool agoForm = false;
        if (Is(i, "in", out int n))
        {
            inForm = true;
            j += n;
        }
        else if (Is(i, "agoprefix", out n))
        {
            // "il y a 3 jours", "vor 3 Tagen": the word that means "ago" comes first.
            agoForm = true;
            j += n;
        }

        if (!Term(j, out int e, out decimal amount, out TWord unit))
        {
            return;
        }

        int sign = agoForm ? -1 : 1;
        if (!inForm && !agoForm)
        {
            if (Is(e, "ago", out int an))
            {
                sign = -1;
                e += an;
            }
            else if (Is(e, "later", out an))
            {
                e += an;
            }
            else
            {
                return;
            }
        }

        DateTime day;
        switch (unit.S)
        {
            case "second":
            case "minute":
            case "hour":
            {
                Granularity g = unit.S == "second" ? Granularity.Second : unit.S == "minute" ? Granularity.Minute : Granularity.Hour;
                DateTime t = _now.AddSeconds(sign * (double)(amount * unit.A));
                t = g == Granularity.Second ? new DateTime(t.Ticks - (t.Ticks % TimeSpan.TicksPerSecond)) : new DateTime(t.Ticks - (t.Ticks % TimeSpan.TicksPerMinute));
                r.Add(new Cand(i, e, new TemporalValue { Kind = TemporalKind.DateTime, Start = t, End = t + TemporalValue.Step(g), Grain = g, NeedsRef = true }));
                return;
            }

            case "day":
            case "week":
                day = _today.AddDays(sign * (double)(amount * unit.A / 86400m)).Date;
                break;
            case "business day" when amount == decimal.Truncate(amount) && amount <= 10_000:
                day = CivilCalendar.AddBusinessDays(_today, sign * (int)amount);
                break;
            case "month" when amount == decimal.Truncate(amount):
                day = _today.AddMonths(sign * (int)amount);
                break;
            case "year" when amount == decimal.Truncate(amount):
                day = _today.AddYears(sign * (int)amount);
                break;
            default:
                return;
        }

        r.Add(Day(i, e, day, true));
    }

    private void WeekdayDate(int i, DateTime a, bool nr, List<Cand> r)
    {
        int j = i;
        int mode = 0;
        if (Is(i, "this", out int n))
        {
            mode = 1;
            j += n;
        }
        else if (Is(i, "next", out n))
        {
            mode = 2;
            j += n;
        }
        else if (Is(i, "last", out n))
        {
            mode = 3;
            j += n;
        }

        if (!Find(j, "weekday", out int wn, out TWord wd) || wd.B == 1)
        {
            return;
        }

        int e = j + wn;
        if (mode == 0 && Is(e, "nextpost", out int post))
        {
            // "vendredi prochain": the word follows the weekday.
            mode = 2;
            e += post;
        }
        else if (mode == 0 && Is(e, "lastpost", out post))
        {
            mode = 3;
            e += post;
        }

        // "next Friday" is the Friday of next week (Monday to Sunday weeks); "last Friday" the latest one before today; a bare weekday the
        // first one on or after today.
        DateTime d = mode switch
        {
            2 => CivilCalendar.MondayOf(a).AddDays(7 + wd.A - 1),
            3 => CivilCalendar.Before(a, wd.A),
            _ => CivilCalendar.OnOrAfter(a, wd.A),
        };
        r.Add(Day(i, e, d, nr));
        if (mode == 0 && (Is(e, "of", out int of) || Is(e, "in", out of)))
        {
            // "Wednesday of next week"
            int w = e + of;
            int off = Is(w, "this", out int wn2) ? 0 : Is(w, "next", out wn2) ? 1 : Is(w, "last", out wn2) ? -1 : 9;
            if (off != 9 && Find(w + wn2, "unit", out int un, out TWord uw) && uw.S == "week")
            {
                r.Add(Day(i, w + wn2 + un, CivilCalendar.MondayOf(a).AddDays((7 * off) + wd.A - 1), nr));
            }
        }

        if (mode == 0)
        {
            // "Monday, March 23": the weekday only confirms the date.
            int k = e;
            if (Ch(k, ',') && Tch(k - 1, k))
            {
                k++;
                if (Is(k, "determiner", out int dtn))
                {
                    // "Freitag, den 27. März"
                    k += dtn;
                }
            }

            var dates = new List<Cand>();
            NumericDateAt(k, dates);
            MonthDate(k, a, nr, dates);
            foreach (Cand c in dates)
            {
                if (c.V.Kind == TemporalKind.Date)
                {
                    r.Add(new Cand(i, c.E, c.V, c.C));
                }
            }
        }
    }

    // ---- months and days ------------------------------------------------------------------------------------------------------

    /// <summary>A day of the month: digits (with or without st/nd/rd/th) or an ordinal word.</summary>
    private bool DayNum(int k, out int end, out int day, out bool ordinal)
    {
        day = 0;
        ordinal = false;
        if (!Num(k, out end, out decimal v, out ordinal) || v is < 1 or > 31 || v != decimal.Truncate(v))
        {
            return false;
        }

        if (_items[k].Kind == ItemKind.Digits && _items[k].End - _items[k].Start > 2)
        {
            return false;
        }

        if (_items[k].Kind == ItemKind.Word && !ordinal)
        {
            return false;
        }

        day = (int)v;
        return true;
    }

    private bool YearAt(int k, out int end, out int year)
    {
        end = k;
        year = 0;
        if (!Dig(k, 4) || k > 0 && !GapOk(_items[k - 1].End, _items[k].Start))
        {
            return false;
        }

        year = Val(k);
        end = k + 1;
        return year is >= 1000 and <= 2999 && !(Ch(k + 1, ':') && Tch(k, k + 1));
    }

    private static DateTime NextOccurrence(int month, int day, DateTime a)
    {
        for (int y = a.Year; y < a.Year + 9; y++)
        {
            if (day <= DateTime.DaysInMonth(y, month) && new DateTime(y, month, day) >= a.Date)
            {
                return new DateTime(y, month, day);
            }
        }

        return new DateTime(a.Year, month, 1);
    }

    private void MonthDate(int i, DateTime a, bool nr, List<Cand> r)
    {
        foreach (VocabHit hit in Hits(i))
        {
            TWord? monthWord = null;
            foreach (TWord candidate in hit.Entries)
            {
                if (candidate.Role == "month")
                {
                    monthWord = candidate;
                }
            }

            if (monthWord is not TWord mw)
            {
                continue;
            }

            int n = hit.N;
            int m = mw.A;
            int k = i + n;
            if (DayNum(k, out int de, out int d, out _) && GapOk(_items[k - 1].End, _items[k].Start) && d <= 31)
            {
                int yk = de;
                if (Ch(yk, ',') && Tch(yk - 1, yk))
                {
                    yk++;
                }

                bool yearGiven = YearAt(yk, out int ye, out int y);
                if (yearGiven && d <= DateTime.DaysInMonth(y, m))
                {
                    r.Add(Day(i, ye, new DateTime(y, m, d), false));
                }

                // Without a year: the next one on or after the reference day. A year that does not fit the day (February 29, 2027) is not skipped.
                if (!(yearGiven && d > DateTime.DaysInMonth(y, m)) && d <= DateTime.DaysInMonth(2000, m))
                {
                    r.Add(Day(i, de, NextOccurrence(m, d, a), nr));
                }
            }

            int pk = k;
            if (Is(pk, "of", out int on))
            {
                pk += on;
            }
            else if (Ch(pk, ',') && Tch(pk - 1, pk))
            {
                pk++;
            }

            if (YearAt(pk, out int ye2, out int y2))
            {
                r.Add(Per(i, ye2, new DateTime(y2, m, 1), new DateTime(y2, m, 1).AddMonths(1), Granularity.Month, false, false));
            }
        }

        // 5 May, 5th of May 2026, the 4th of July
        int s0 = i;
        if (Is(i, "the", out int tn))
        {
            s0 += tn;
        }

        if (DayNum(s0, out int de2, out int d2, out bool ord))
        {
            int k = de2;
            bool of = false;
            if (Is(k, "of", out int on2))
            {
                k += on2;
                of = true;
            }

            if (Find(k, "month", out int mn, out TWord mw2) && GapOk(_items[k - 1].End, _items[k].Start) && (of || ord || _items[s0].Kind == ItemKind.Digits))
            {
                int m = mw2.A;
                int me = k + mn;
                int yk = me;
                if (Ch(yk, ',') && Tch(yk - 1, yk))
                {
                    yk++;
                }

                bool yearGiven = YearAt(yk, out int ye, out int y);
                if (yearGiven && d2 <= DateTime.DaysInMonth(y, m))
                {
                    r.Add(Day(i, ye, new DateTime(y, m, d2), false));
                }

                if (!(yearGiven && d2 > DateTime.DaysInMonth(y, m)) && d2 <= DateTime.DaysInMonth(2000, m))
                {
                    r.Add(Day(i, me, NextOccurrence(m, d2, a), nr));
                }
            }
            else if (s0 > i && ord && _items[s0].Kind == ItemKind.Digits && !_td!.Flags.Contains("no-lone-day"))
            {
                // "the 25th": the next 25th.
                DateTime cur = new(a.Year, a.Month, 1);
                for (int step = 0; step < 14; step++)
                {
                    DateTime month = cur.AddMonths(step);
                    if (d2 <= DateTime.DaysInMonth(month.Year, month.Month) && new DateTime(month.Year, month.Month, d2) >= a.Date)
                    {
                        r.Add(Day(i, de2, new DateTime(month.Year, month.Month, d2), nr));
                        break;
                    }
                }
            }
        }
    }

    // ---- holidays ---------------------------------------------------------------------------------------------------------------

    private void HolidayDate(int i, DateTime a, bool nr, List<Cand> r)
    {
        int j = i;
        int mode = 0;
        if (Is(i, "this", out int n) || Is(i, "next", out n))
        {
            j += n;
        }
        else if (Is(i, "last", out n))
        {
            mode = 3;
            j += n;
        }

        foreach (VocabHit h in Hits(j))
        {
            foreach (TWord w in h.Entries)
            {
                if (w.Role != "holiday" || (w.B & _region) == 0)
                {
                    continue;
                }

                HolidayRule rule = _td!.Holidays[w.A];
                int e = j + h.N;
                if (mode == 0 && j == i && YearAt(e, out int ye, out int y) && CivilCalendar.Holiday(rule, y) is DateTime dated)
                {
                    r.Add(Day(i, ye, dated, false));
                }

                for (int step = 0; step < 3; step++)
                {
                    int yy = mode == 3 ? a.Year - step : a.Year + step;
                    if (CivilCalendar.Holiday(rule, yy) is DateTime cand && (mode == 3 ? cand < a.Date : cand >= a.Date))
                    {
                        r.Add(Day(i, e, cand, nr));
                        break;
                    }
                }
            }
        }
    }

    // ---- periods -----------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The start of the occurrence of a period (a month, a season: first month and length in months) that a word like "this", "next" or
    /// "last" picks: <paramref name="mode"/> 0 and 1 the one that contains the reference day or comes next, 2 the next one that starts
    /// after it, -1 the latest one that ended before it.
    /// </summary>
    private static DateTime Instance(int mode, int firstMonth, int length, DateTime a)
    {
        DateTime best = new(a.Year, firstMonth, 1);
        if (mode == -1)
        {
            for (int y = a.Year + 1; y >= a.Year - 2; y--)
            {
                var s = new DateTime(y, firstMonth, 1);
                if (s.AddMonths(length) <= a.Date)
                {
                    return s;
                }
            }

            return best;
        }

        for (int y = a.Year - 1; y <= a.Year + 2; y++)
        {
            var s = new DateTime(y, firstMonth, 1);
            if (mode == 2 ? s > a.Date : s.AddMonths(length) > a.Date)
            {
                return s;
            }
        }

        return best;
    }

    /// <summary><c>the past three days</c>, <c>next 2 weeks</c>: the stretch of that length ending or starting today.</summary>
    private void SpanFromToday(int i, List<Cand> r)
    {
        int j = i;
        if (Is(i, "the", out int tn))
        {
            j += tn;
        }

        bool back;
        if (Is(j, "past", out int n) || Is(j, "last", out n))
        {
            back = true;
        }
        else if (Is(j, "next", out n))
        {
            back = false;
        }
        else
        {
            return;
        }

        if (!Num(j + n, out int ne, out decimal amount, out bool ord) || ord || amount != decimal.Truncate(amount) || amount is < 2 or > 1000
            || !Find(ne, "unit", out int un, out TWord u) || u.S is "second" or "minute" or "hour" or "business day" or "business hour" || !GapOk(_items[ne - 1].End, _items[ne].Start))
        {
            return;
        }

        int count = (int)amount;
        DateTime from;
        DateTime to;
        Granularity g = u.S switch { "week" => Granularity.Week, "month" => Granularity.Month, "quarter" => Granularity.Quarter, "year" => Granularity.Year, "decade" => Granularity.Decade, _ => Granularity.Day };
        DateTime Add(DateTime d, int sign) => u.S switch
        {
            "day" => d.AddDays(sign * count),
            "week" => d.AddDays(sign * 7 * count),
            "month" => d.AddMonths(sign * count),
            "quarter" => d.AddMonths(sign * 3 * count),
            "year" => d.AddYears(sign * count),
            _ => d.AddYears(sign * 10 * count),
        };
        if (back)
        {
            to = _today;
            from = Add(_today, -1);
        }
        else
        {
            from = _today;
            to = Add(_today, 1);
        }

        r.Add(Per(i, ne + un, from, to, g, false, true));
    }

    private void RelativePeriod(int i, DateTime a, bool nr, List<Cand> r)
    {
        SpanFromToday(i, r);
        int j = i;
        int offset;
        int post = 0;
        if (Is(i, "this", out int n))
        {
            offset = 0;
        }
        else if (Is(i, "next", out n))
        {
            offset = 1;
        }
        else if (Is(i, "last", out n))
        {
            offset = -1;
        }
        else if ((Find(i, "unit", out int pw, out _) || Is(i, "weekend", out pw) || Find(i, "season", out pw, out _) || Find(i, "month", out pw, out _))
            && (Is(i + pw, "nextpost", out post) || Is(i + pw, "lastpost", out post)))
        {
            // "la semaine prochaine", "l'hiver dernier": the word follows the period.
            offset = Is(i + pw, "nextpost") ? 1 : -1;
            n = 0;
        }
        else
        {
            return;
        }

        j += n;
        int mode = offset == 0 ? 0 : offset == 1 ? 2 : -1;
        if (Find(j, "unit", out int un, out TWord u))
        {
            int e = j + un + post;
            switch (u.S)
            {
                case "week":
                {
                    DateTime s = CivilCalendar.MondayOf(a).AddDays(7 * offset);
                    r.Add(Per(i, e, s, s.AddDays(7), Granularity.Week, false, nr));
                    break;
                }

                case "month":
                {
                    var s = new DateTime(a.Year, a.Month, 1).AddMonths(offset);
                    r.Add(Per(i, e, s, s.AddMonths(1), Granularity.Month, false, nr));
                    break;
                }

                case "quarter":
                {
                    DateTime s = CivilCalendar.QuarterStart(a).AddMonths(3 * offset);
                    r.Add(Per(i, e, s, s.AddMonths(3), Granularity.Quarter, false, nr));
                    break;
                }

                case "year":
                {
                    var s = new DateTime(a.Year + offset, 1, 1);
                    r.Add(Per(i, e, s, s.AddYears(1), Granularity.Year, false, nr));
                    break;
                }

                case "decade":
                {
                    var s = new DateTime((a.Year / 10 * 10) + (10 * offset), 1, 1);
                    r.Add(Per(i, e, s, s.AddYears(10), Granularity.Decade, false, nr));
                    break;
                }
            }

            return;
        }

        if (Is(j, "weekend", out int wn))
        {
            DateTime sat = CivilCalendar.MondayOf(a).AddDays(5 + (7 * offset));
            r.Add(Per(i, j + wn + post, sat, sat.AddDays(2), Granularity.Day, false, nr));
            return;
        }

        if (Find(j, "season", out int sn, out TWord sw))
        {
            DateTime s = Instance(mode, sw.A, sw.B, a);
            r.Add(Per(i, j + sn + post, s, s.AddMonths(sw.B), Granularity.Season, true, nr));
            return;
        }

        if (Find(j, "month", out int mn, out TWord mw))
        {
            DateTime s = Instance(mode, mw.A, 1, a);
            r.Add(Per(i, j + mn + post, s, s.AddMonths(1), Granularity.Month, false, nr));
        }
    }

    /// <summary>Year of a period: a four-digit year, or this, next or last year.</summary>
    private bool YearExpr(int k, DateTime a, out int end, out int year, out bool needsRef)
    {
        needsRef = false;
        if (YearAt(k, out end, out year))
        {
            return true;
        }

        int offset;
        if (Is(k, "this", out int n))
        {
            offset = 0;
        }
        else if (Is(k, "next", out n))
        {
            offset = 1;
        }
        else if (Is(k, "last", out n))
        {
            offset = -1;
        }
        else
        {
            return false;
        }

        if (Find(k + n, "unit", out int un, out TWord u) && u.S == "year")
        {
            end = k + n + un;
            year = a.Year + offset;
            needsRef = true;
            return true;
        }

        return false;
    }

    private void NamedPeriod(int i, DateTime a, bool nr, List<Cand> r)
    {
        // Seasons: "spring 2021", "summer of 2024", and bare ones after "in", "during", ...
        if (Find(i, "season", out int n, out TWord sw))
        {
            int k = i + n;
            if (Is(k, "of", out int on) || Is(k, "in", out on))
            {
                k += on;
            }
            else if (Ch(k, ',') && Tch(k - 1, k))
            {
                k++;
            }

            if (YearAt(k, out int ye, out int y))
            {
                var s = new DateTime(y, sw.A, 1);
                r.Add(Per(i, ye, s, s.AddMonths(sw.B), Granularity.Season, true, false));
            }
        }

        QuarterOrHalf(i, a, nr, r);
        PartOfPeriod(i, a, nr, r);

        // The 1990s
        int d = i;
        if (Is(i, "the", out int tn))
        {
            d += tn;
        }

        if (Dig(d, 4) && d + 1 < _items.Count && Tch(d, d + 1) && _items[d + 1].Kind == ItemKind.Word && _t[_items[d + 1].Start] == 's' && _items[d + 1].End - _items[d + 1].Start == 1
            && Val(d) % 10 == 0 && Val(d) >= 1000 && Val(d) <= 2990)
        {
            var s = new DateTime(Val(d), 1, 1);
            r.Add(Per(i, d + 2, s, s.AddYears(10), Granularity.Decade, false, false));
        }

        // French "les années 1990" and German "1990er Jahre": the same decade as "the 1990s".
        if (Is(i, "decadeprefix", out int dpn) && Dig(i + dpn, 4) && GapOk(_items[i + dpn - 1].End, _items[i + dpn].Start) && Val(i + dpn) % 10 == 0
            && Val(i + dpn) is >= 1000 and <= 2990 && FreeRight(i + dpn + 1))
        {
            var s = new DateTime(Val(i + dpn), 1, 1);
            r.Add(Per(i, i + dpn + 1, s, s.AddYears(10), Granularity.Decade, false, false));
        }
        else if (Is(i, "decadeprefix", out int dq) && DecadeName(i + dq, out int decadeEnd, out int decadeYear))
        {
            var s = new DateTime(decadeYear, 1, 1);
            r.Add(Per(i, decadeEnd, s, s.AddYears(10), Granularity.Decade, false, false));
        }
        else if (Dig(i, 4) && i + 1 < _items.Count && Tch(i, i + 1) && Val(i) % 10 == 0 && Val(i) is >= 1000 and <= 2990 && Is(i + 1, "decadesuffix", out int dsn))
        {
            int end = i + 1 + dsn;
            if (end < _items.Count && Is(end, "decadeword", out int dwn) && GapOk(_items[end - 1].End, _items[end].Start))
            {
                end += dwn;
            }

            var s = new DateTime(Val(i), 1, 1);
            r.Add(Per(i, end, s, s.AddYears(10), Granularity.Decade, false, false));
        }

        // A bare year
        if (YearAt(i, out int e, out int year) && !(Dig(i, 4) && i + 1 < _items.Count && Tch(i, i + 1) && _items[i + 1].Kind == ItemKind.Word))
        {
            var s = new DateTime(year, 1, 1);
            r.Add(Per(i, e, s, s.AddYears(1), Granularity.Year, false, false, 0.8f));
        }
    }

    /// <summary>A decade written as a name ("Novanta") or as two digits with an optional apostrophe ("'90", "90") after "anni" / "jaren".</summary>
    private bool DecadeName(int k, out int end, out int year)
    {
        end = k;
        year = 0;
        int yy;
        if (Find(k, "decade", out int n, out TWord w))
        {
            yy = w.A;
            end = k + n;
        }
        else
        {
            int d = k;
            if (d < _items.Count && _items[d].Kind == ItemKind.Other && _items[d].End - _items[d].Start == 1 && _t[_items[d].Start] is '\'' or '\u2019')
            {
                d++;
            }

            if (!Dig(d, 2) || Val(d) % 10 != 0 || !FreeRight(d + 1))
            {
                return false;
            }

            yy = Val(d);
            end = d + 1;
        }

        year = yy >= 30 ? 1900 + yy : 2000 + yy;
        return true;
    }

    private void QuarterOrHalf(int i, DateTime a, bool nr, List<Cand> r)
    {
        int k = i;
        if (Is(i, "the", out int tn))
        {
            k += tn;
        }

        int number;
        int e;
        bool isQuarter;
        if (Num(k, out int ne, out decimal v, out bool ord) && ord && v is >= 1 and <= 4 && v == decimal.Truncate(v) && Is(ne, "quarter", out int qn))
        {
            number = (int)v;
            e = ne + qn;
            isQuarter = true;
        }
        else if (ord && v is >= 1 and <= 2 && Is(ne, "half", out int hn))
        {
            number = (int)v;
            e = ne + hn;
            isQuarter = false;
        }
        else if ((Is(i, "quarterprefix", out int qp) || Is(i, "halfprefix", out qp)) && Dig(i + qp, 1) && Tch(i + qp - 1, i + qp))
        {
            isQuarter = Is(i, "quarterprefix");
            number = Val(i + qp);
            if (number < 1 || number > (isQuarter ? 4 : 2))
            {
                return;
            }

            e = i + qp + 1;
            if (!FreeRight(e))
            {
                return;
            }
        }
        else
        {
            return;
        }

        int year = a.Year;
        bool needs = true;
        int yk = e;
        if (Is(yk, "of", out int on) || Is(yk, "in", out on))
        {
            yk += on;
        }
        else if (Ch(yk, ',') && Tch(yk - 1, yk))
        {
            yk++;
        }

        if (YearExpr(yk, a, out int ye, out int y, out bool yn))
        {
            year = y;
            needs = yn;
            e = ye;
        }

        int months = isQuarter ? 3 : 6;
        var s = new DateTime(year, ((number - 1) * months) + 1, 1);
        r.Add(Per(i, e, s, s.AddMonths(months), isQuarter ? Granularity.Quarter : Granularity.Half, false, needs && nr));
    }

    /// <summary>early, mid and late parts of a year or month: <c>late 2023</c>, <c>mid-March</c>, <c>early next year</c>.</summary>
    private void PartOfPeriod(int i, DateTime a, bool nr, List<Cand> r)
    {
        int part;
        if (Is(i, "early", out int n))
        {
            part = 0;
        }
        else if (Is(i, "mid", out n))
        {
            part = 1;
        }
        else if (Is(i, "late", out n))
        {
            part = 2;
        }
        else
        {
            return;
        }

        int k = i + n;
        if (Ch(k, '-') && Tch(k - 1, k))
        {
            k++;
        }

        if (YearExpr(k, a, out int ye, out int y, out bool needs))
        {
            var s = new DateTime(y, (part * 4) + 1, 1);
            r.Add(Per(i, ye, s, part == 2 ? new DateTime(y + 1, 1, 1) : s.AddMonths(4), Granularity.Year, true, needs && nr));
            return;
        }

        if (Find(k, "month", out int mn, out TWord mw))
        {
            int e = k + mn;
            int pk = e;
            if (Is(pk, "of", out int on))
            {
                pk += on;
            }
            else if (Ch(pk, ',') && Tch(pk - 1, pk))
            {
                pk++;
            }

            DateTime first;
            bool needsRef = false;
            if (YearAt(pk, out int ye2, out int y2))
            {
                first = new DateTime(y2, mw.A, 1);
                e = ye2;
            }
            else
            {
                first = Instance(0, mw.A, 1, a);
                needsRef = nr;
            }

            DateTime s = first.AddDays(part * 10);
            DateTime end = part == 2 ? first.AddMonths(1) : s.AddDays(10);
            r.Add(Per(i, e, s, end, Granularity.Month, true, needsRef));
        }
    }

    // ---- computed dates ----------------------------------------------------------------------------------------------------

    /// <summary><c>the day after tomorrow</c>, <c>two days before Christmas</c>, <c>a week after Easter 2027</c>.</summary>
    private void OffsetFrom(int i, DateTime a, bool nr, List<Cand> r)
    {
        int e;
        decimal amount;
        TWord unit;
        if (Is(i, "the", out int tn) && Find(i + tn, "unit", out int un, out unit))
        {
            e = i + tn + un;
            amount = 1;
        }
        else if (!Term(i, out e, out amount, out unit))
        {
            return;
        }

        if (unit.S is not ("day" or "week" or "month" or "year") || amount != decimal.Truncate(amount))
        {
            return;
        }

        int sign;
        if (Is(e, "after", out int an))
        {
            sign = 1;
        }
        else if (Is(e, "before", out an))
        {
            sign = -1;
        }
        else
        {
            return;
        }

        var bases = new List<Cand>();
        DateAt(e + an, nr ? null : a, bases);
        foreach (Cand b in bases)
        {
            if (b.V.Kind != TemporalKind.Date)
            {
                continue;
            }

            int n = sign * (int)amount;
            DateTime d = unit.S switch
            {
                "day" => b.V.Start.AddDays(n),
                "week" => b.V.Start.AddDays(7 * n),
                "month" => b.V.Start.AddMonths(n),
                _ => b.V.Start.AddYears(n),
            };
            r.Add(new Cand(i, b.E, TemporalValue.Day(d, b.V.NeedsRef)));
        }
    }

    /// <summary><c>the third Thursday of April</c>, <c>the last Friday of May 2026</c>, <c>the first Monday after Easter 2027</c>.</summary>
    private void NthWeekday(int i, DateTime a, bool nr, List<Cand> r)
    {
        int k = i;
        if (Is(i, "the", out int tn))
        {
            k += tn;
        }

        int nth;
        if (Is(k, "last", out int ln))
        {
            nth = -1;
            k += ln;
        }
        else if (Num(k, out int ne, out decimal v, out bool ord) && ord && v is >= 1 and <= 5)
        {
            nth = (int)v;
            k = ne;
        }
        else
        {
            return;
        }

        if (!Find(k, "weekday", out int wn, out TWord wd) || wd.B == 1)
        {
            return;
        }

        k += wn;
        if (Is(k, "of", out int on))
        {
            k += on;
            if (Is(k, "the", out tn) && Is(k + tn, "this", out _))
            {
                return;
            }

            if (!MonthRef(k, a, out int me, out int year, out int month, out bool needs))
            {
                return;
            }

            if (CivilCalendar.NthWeekday(year, month, wd.A, nth) is DateTime d)
            {
                r.Add(Day(i, me, d, needs && nr));
            }

            return;
        }

        bool after = Is(k, "after", out int an);
        if (!after && !Is(k, "before", out an))
        {
            return;
        }

        var bases = new List<Cand>();
        DateAt(k + an, nr ? null : a, bases);
        foreach (Cand b in bases)
        {
            if (b.V.Kind != TemporalKind.Date)
            {
                continue;
            }

            DateTime d = after
                ? CivilCalendar.OnOrAfter(b.V.Start.AddDays(1), wd.A).AddDays(7 * (Math.Max(nth, 1) - 1))
                : CivilCalendar.Before(b.V.Start, wd.A).AddDays(-7 * (Math.Max(nth, 1) - 1));
            r.Add(new Cand(i, b.E, TemporalValue.Day(d, b.V.NeedsRef)));
        }
    }

    /// <summary>A month for "the third Thursday of ...": April, April 2026, this month, next month.</summary>
    private bool MonthRef(int k, DateTime a, out int end, out int year, out int month, out bool needsRef)
    {
        end = k;
        year = 0;
        month = 0;
        needsRef = true;
        if (Find(k, "month", out int mn, out TWord mw))
        {
            end = k + mn;
            month = mw.A;
            int pk = end;
            if (Is(pk, "of", out int on))
            {
                pk += on;
            }
            else if (Ch(pk, ',') && Tch(pk - 1, pk))
            {
                pk++;
            }

            if (YearAt(pk, out int ye, out int y))
            {
                end = ye;
                year = y;
                needsRef = false;
            }
            else
            {
                year = Instance(0, month, 1, a).Year;
            }

            return true;
        }

        int offset;
        if (Is(k, "this", out int n))
        {
            offset = 0;
        }
        else if (Is(k, "next", out n))
        {
            offset = 1;
        }
        else if (Is(k, "last", out n))
        {
            offset = -1;
        }
        else
        {
            return false;
        }

        if (Find(k + n, "unit", out int un, out TWord u) && u.S == "month")
        {
            DateTime s = new DateTime(a.Year, a.Month, 1).AddMonths(offset);
            year = s.Year;
            month = s.Month;
            end = k + n + un;
            return true;
        }

        return false;
    }

    // ---- ranges of days ----------------------------------------------------------------------------------------------------

    /// <summary><c>from April 6 to April 10</c>, <c>between Monday and Friday</c>, <c>May 4 - May 8</c>, <c>May 4-8</c>.</summary>
    private void DateRangeAt(int i, List<Cand> r)
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

        MonthDayRange(i, j, r);
        DayMonthRange(i, j, between, r);
        var firsts = new List<Cand>();
        DateAt(j, null, firsts);
        foreach (Cand a in firsts)
        {
            if (a.V.Kind is not (TemporalKind.Date or TemporalKind.DateRange))
            {
                continue;
            }

            int k = a.E;
            if (between ? !Is(k, "and", out n) : !Is(k, "rangeto", out n))
            {
                continue;
            }

            var seconds = new List<Cand>();
            DateAt(k + n, a.V.Start, seconds);
            foreach (Cand b in seconds)
            {
                // "du 3 mars au 7 avril 2026": the year at the end belongs to both ends, so the first end is not the next 3 March but the one before 7 April 2026.
                if (a.V.NeedsRef && a.V.Kind == TemporalKind.Date && b.V.Kind == TemporalKind.Date && !b.V.NeedsRef)
                {
                    DateTime earlier = a.V.Start;
                    while (earlier >= b.V.End)
                    {
                        earlier = earlier.AddYears(-1);
                    }

                    if (earlier != a.V.Start)
                    {
                        r.Add(new Cand(i, b.E, TemporalValue.Period(earlier, b.V.End, Granularity.Day, a.V.Approx || b.V.Approx, false)));
                        continue;
                    }
                }

                if (b.V.Kind is not (TemporalKind.Date or TemporalKind.DateRange) || b.V.End <= a.V.Start || (!framed && !(a.V.Kind == TemporalKind.Date && b.V.Kind == TemporalKind.Date) && !BothYears(a.V, b.V)))
                {
                    continue;
                }

                Granularity g = a.V.Kind == b.V.Kind && a.V.Grain == b.V.Grain && a.V.Kind == TemporalKind.DateRange ? a.V.Grain!.Value : Granularity.Day;
                TemporalValue v = TemporalValue.Period(a.V.Start, b.V.End, g, a.V.Approx || b.V.Approx, a.V.NeedsRef || b.V.NeedsRef);
                r.Add(new Cand(i, b.E, v));
            }
        }
    }

    private static bool BothYears(TemporalValue a, TemporalValue b) =>
        a.Kind == TemporalKind.DateRange && b.Kind == TemporalKind.DateRange && a.Grain == Granularity.Year && b.Grain == Granularity.Year && !a.NeedsRef && !b.NeedsRef;

    /// <summary>Days of one month with the day first: <c>du 6 au 10 avril</c>, <c>vom 6. bis 10. April</c>, <c>zwischen dem 1. und 15. Juni</c>.</summary>
    private void DayMonthRange(int s, int j, bool between, List<Cand> r)
    {
        int k = j;
        if (Is(k, "determiner", out int dn))
        {
            k += dn;
        }

        if (!DayNum(k, out int e1, out int d1, out _))
        {
            return;
        }

        int cn;
        if (between ? !Is(e1, "and", out cn) : !Is(e1, "rangeto", out cn))
        {
            return;
        }

        int m = e1 + cn;
        if (Is(m, "determiner", out dn))
        {
            m += dn;
        }

        if (!DayNum(m, out int e2, out int d2, out _) || d2 < d1 || e2 >= _items.Count || !GapOk(_items[e2 - 1].End, _items[e2].Start)
            || !Find(e2, "month", out int mn, out TWord mw))
        {
            return;
        }

        int me = e2 + mn;
        int yk = me;
        if (Ch(yk, ',') && Tch(yk - 1, yk))
        {
            yk++;
        }

        bool hasYear = YearAt(yk, out int ye, out int y);
        DateTime start = hasYear ? new DateTime(y, mw.A, 1) : NextOccurrence(mw.A, 1, _today);
        if (d2 > DateTime.DaysInMonth(start.Year, mw.A))
        {
            return;
        }

        if (!hasYear)
        {
            start = NextOccurrence(mw.A, d1, _today);
        }
        else
        {
            start = new DateTime(y, mw.A, d1);
        }

        r.Add(Per(s, hasYear ? ye : me, start, new DateTime(start.Year, mw.A, d2).AddDays(1), Granularity.Day, false, !hasYear));
    }

    /// <summary>Days of one month: <c>May 4-8</c>, <c>April 6 to 10, 2026</c>.</summary>
    private void MonthDayRange(int s, int j, List<Cand> r)
    {
        if (!Find(j, "month", out int mn, out TWord mw) || !DayNum(j + mn, out int de, out int d1, out _) || !Is(de, "rangeto", out int cn))
        {
            return;
        }

        int k = de + cn;
        if (!Dig(k) || !DayNum(k, out int e2, out int d2, out _) || d2 < d1 || (Ch(e2, ':') && Tch(e2 - 1, e2)) || Find(e2, "month", out _, out _))
        {
            return;
        }

        int m = mw.A;
        int yk = e2;
        if (Ch(yk, ',') && Tch(yk - 1, yk))
        {
            yk++;
        }

        bool hasYear = YearAt(yk, out int ye, out int y);
        DateTime start = hasYear ? new DateTime(y, m, d1) : NextOccurrence(m, d1, _today);
        if (d2 > DateTime.DaysInMonth(start.Year, m))
        {
            return;
        }

        r.Add(Per(s, hasYear ? ye : e2, start, new DateTime(start.Year, m, d2).AddDays(1), Granularity.Day, false, !hasYear));
    }

    // ---- dates with times ------------------------------------------------------------------------------------------------------

    private static TemporalValue AtTime(DateTime day, TemporalValue t, bool needsRef)
    {
        DateTime s = day.Date + (t.Start - TemporalValue.NoDate);
        return new TemporalValue
        {
            Kind = TemporalKind.DateTime,
            Start = s,
            End = s + TemporalValue.Step(t.Grain ?? Granularity.Hour),
            Grain = t.Grain,
            Approx = t.Approx,
            Offset = t.Offset,
            ZoneIsZ = t.ZoneIsZ,
            NeedsRef = needsRef,
        };
    }

    private static TemporalValue PartOf(DateTime day, int from, int to, bool needsRef) =>
        new()
        {
            Kind = TemporalKind.DateTimeRange,
            Start = day.Date.AddHours(from),
            End = day.Date.AddHours(to),
            Grain = Granularity.Hour,
            Approx = true,
            NeedsRef = needsRef,
        };

    /// <summary><c>this morning</c>, <c>last night</c>, <c>tonight</c>: a part of today or yesterday.</summary>
    private void PartPrefixDate(int i, List<Cand> dates, List<Cand> r)
    {
        int j = i;
        DateTime day;
        if (Is(i, "tonight", out int n))
        {
            day = _today;
            r.Add(new Cand(i, i + n, PartOf(day, 20, 24, true)));
            TemporalValue flag = TemporalValue.Day(day, true);
            flag.PartOfDay = true;
            dates.Add(new Cand(i, i + n, flag));
            return;
        }

        if (Is(i, "this", out n))
        {
            day = _today;
        }
        else if (Is(i, "last", out n))
        {
            day = _today.AddDays(-1);
        }
        else
        {
            return;
        }

        j += n;
        if (Find(j, "daypart", out int dn, out TWord part))
        {
            r.Add(new Cand(i, j + dn, PartOf(day, part.A, part.B, true)));
            TemporalValue flag = TemporalValue.Day(day, true);
            flag.PartOfDay = true;
            dates.Add(new Cand(i, j + dn, flag));
        }
    }

    private void DateTimeAt(int i, DateTime? anchor, List<Cand> r)
    {
        var dates = new List<Cand>();
        DateAt(i, anchor, dates);
        if (anchor is null)
        {
            PartPrefixDate(i, dates, r);
        }

        foreach (Cand d in dates)
        {
            if (d.V.Kind != TemporalKind.Date)
            {
                continue;
            }

            int j = d.E;
            bool nr = d.V.NeedsRef;
            if (!d.V.PartOfDay)
            {
                int pk = j;
                if (Is(pk, "in", out int inn) && Is(pk + inn, "the", out int th))
                {
                    pk += inn + th;
                }

                if (Find(pk, "daypart", out int dn, out TWord part) && GapOk(_items[pk - 1].End, _items[pk].Start))
                {
                    r.Add(new Cand(i, pk + dn, PartOf(d.V.Start, part.A, part.B, nr)));
                }
            }

            int k = j;
            if (Is(k, "at", out int an))
            {
                k += an;
            }
            else if (Ch(k, ',') && Tch(k - 1, k))
            {
                k++;
            }

            var times = new List<Cand>();
            TimeAt(k, times, false);
            foreach (Cand t in times)
            {
                if (t.V.Kind == TemporalKind.Time)
                {
                    r.Add(new Cand(i, t.E, AtTime(d.V.Start, t.V, nr)));
                }
            }

            var ranges = new List<Cand>();
            TimeRangeAt(k, ranges);
            foreach (Cand t in ranges)
            {
                DateTime s = d.V.Start + (t.V.Start - TemporalValue.NoDate);
                DateTime e = d.V.Start + (t.V.End - TemporalValue.NoDate);
                if (e < s)
                {
                    e = e.AddDays(1);
                }

                r.Add(new Cand(i, t.E, new TemporalValue
                {
                    Kind = TemporalKind.DateTimeRange, Start = s, End = e, Grain = t.V.Grain, Approx = t.V.Approx, Offset = t.V.Offset, ZoneIsZ = t.V.ZoneIsZ, NeedsRef = nr,
                }));
            }
        }

        // A range of times first: "between 1 and 3 pm tomorrow"
        var firstRanges = new List<Cand>();
        TimeRangeAt(i, firstRanges);
        foreach (Cand t in firstRanges)
        {
            int k = t.E;
            if (Is(k, "on", out int on))
            {
                k += on;
            }

            var later = new List<Cand>();
            DateAt(k, anchor, later);
            foreach (Cand d in later)
            {
                if (d.V.Kind != TemporalKind.Date)
                {
                    continue;
                }

                DateTime s = d.V.Start + (t.V.Start - TemporalValue.NoDate);
                DateTime e = d.V.Start + (t.V.End - TemporalValue.NoDate);
                r.Add(new Cand(i, d.E, new TemporalValue
                {
                    Kind = TemporalKind.DateTimeRange, Start = s, End = e < s ? e.AddDays(1) : e, Grain = t.V.Grain, Approx = t.V.Approx, Offset = t.V.Offset,
                    ZoneIsZ = t.V.ZoneIsZ, NeedsRef = d.V.NeedsRef,
                }));
            }
        }

        // A time first: "3pm on March 22", "at 3pm tomorrow"
        var firstTimes = new List<Cand>();
        TimeAt(i, firstTimes, false);
        foreach (Cand t in firstTimes)
        {
            if (t.V.Kind != TemporalKind.Time)
            {
                continue;
            }

            int k = t.E;
            if (Is(k, "on", out int on))
            {
                k += on;
            }
            else if (Ch(k, ',') && Tch(k - 1, k))
            {
                k++;
            }

            var later = new List<Cand>();
            DateAt(k, anchor, later);
            foreach (Cand d in later)
            {
                if (d.V.Kind == TemporalKind.Date)
                {
                    r.Add(new Cand(i, d.E, AtTime(d.V.Start, t.V, d.V.NeedsRef)));
                }
            }
        }
    }

    /// <summary><c>from Monday 8am to Monday 6pm</c>, <c>between May 5 at 9am and May 6 at noon</c>.</summary>
    private void DateTimeRangeAt(int i, List<Cand> r)
    {
        int j = i;
        bool between = false;
        if (Is(j, "from", out int n))
        {
            j += n;
        }
        else if (Is(j, "between", out n))
        {
            j += n;
            between = true;
        }

        var firsts = new List<Cand>();
        DateTimeAt(j, null, firsts);
        foreach (Cand a in firsts)
        {
            if (a.V.Kind != TemporalKind.DateTime)
            {
                continue;
            }

            int k = a.E;
            if (between ? !Is(k, "and", out n) : !Is(k, "rangeto", out n))
            {
                continue;
            }

            k += n;
            var seconds = new List<Cand>();
            DateTimeAt(k, a.V.Start.Date, seconds);
            var times = new List<Cand>();
            TimeAt(k, times, false);
            foreach (Cand t in times)
            {
                if (t.V.Kind == TemporalKind.Time)
                {
                    seconds.Add(new Cand(k, t.E, AtTime(a.V.Start.Date, t.V, false)));
                }
            }

            foreach (Cand b in seconds)
            {
                if (b.V.Kind != TemporalKind.DateTime || b.V.Start <= a.V.Start)
                {
                    continue;
                }

                var v = new TemporalValue
                {
                    Kind = TemporalKind.DateTimeRange,
                    Start = a.V.Start,
                    End = b.V.Start,
                    Grain = (Granularity)Math.Min((int)a.V.Grain!.Value, (int)b.V.Grain!.Value),
                    Approx = a.V.Approx || b.V.Approx,
                    Offset = b.V.Offset ?? a.V.Offset,
                    ZoneIsZ = b.V.Offset is not null ? b.V.ZoneIsZ : a.V.ZoneIsZ,
                    NeedsRef = a.V.NeedsRef || b.V.NeedsRef,
                };
                r.Add(new Cand(i, b.E, v));
            }
        }
    }
}
