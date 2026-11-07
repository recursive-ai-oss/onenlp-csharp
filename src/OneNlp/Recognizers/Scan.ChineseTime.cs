using System.Globalization;

namespace RecursiveAI.OneNlp.Recognizers;

/// <summary>
/// Chinese dates and times. The vocabulary (relative days, weekdays, seasons, parts of the day, units, holidays) is rule data; the grammar
/// that joins numbers and the characters 年 月 日 号 点 时 分 半 刻 into dates, times, ranges, durations and recurrences is here.
/// </summary>
internal sealed partial class Scan
{
    private readonly record struct ZCand(int Start, int End, TemporalValue V, float C = 1f);

    /// <summary>A single day read at a position, with the part of the day the words imply (今晚: the evening).</summary>
    private readonly record struct ZDay(int End, TemporalValue V, ZhDayPart? Implied);

    /// <summary>A time of day read at a position.</summary>
    private readonly record struct ZTod(int End, int Hour, int Minute, Granularity Grain, ZhDayPart? Part, int? Zone, bool ZoneZ);

    private const char Nian = '\u5E74';   // 年
    private const char Yue = '\u6708';    // 月
    private const char Ri = '\u65E5';     // 日
    private const char Hao = '\u53F7';    // 号
    private const char Dian = '\u70B9';   // 点
    private const char Shi = '\u65F6';    // 时
    private const char Fen = '\u5206';    // 分
    private const char Ban = '\u534A';    // 半
    private const char Ke = '\u523B';     // 刻
    private const char Zheng = '\u6574';  // 整

    private void FindZhTemporal()
    {
        var cands = new List<ZCand>();
        for (int i = 0; i < _t.Length; i++)
        {
            if (char.IsWhiteSpace(_t[i]))
            {
                continue;
            }

            cands.Clear();
            try
            {
                ZhTemporalAt(i, cands);
            }
            catch (Exception e) when (e is ArgumentOutOfRangeException or OverflowException)
            {
                // A value outside the calendar (year 10000): the expression is left out.
                continue;
            }

            foreach (ZCand c in cands)
            {
                AddZh(c);
            }
        }
    }

    private void AddZh(ZCand c)
    {
        TemporalValue v = c.V;
        if ((v.NeedsRef && _ref is null) || c.End <= c.Start)
        {
            return;
        }

        TimeSpan? length = v.Kind == TemporalKind.Duration && v.Seconds is decimal s ? TimeSpan.FromSeconds((double)s) : null;
        _found.Add(new TemporalMatch(
            c.Start, c.End, _t.Substring(c.Start, c.End - c.Start), v.Label, c.C, v.Kind, v.Format(), v.Kind == TemporalKind.Duration ? null : v.Grain,
            v.Approx, v.Offset is int o ? TimeSpan.FromMinutes(o) : null, v.ToRange(_ref), length)
        {
            Unit = v.DurUnit,
            Count = v.Count,
        });
    }

    private void ZhTemporalAt(int i, List<ZCand> r)
    {
        InitZhOnce();

        // Days, with what can follow a day: a time, a part of the day, a range of times.
        var days = new List<ZDay>();
        DayAt(i, days);
        foreach (ZDay d in days)
        {
            r.Add(new ZCand(i, d.End, d.V));
            DayThen(i, d, r);
        }

        PeriodAt(i, r);
        TimeAt(i, r);
        DateRangeAt(i, r);
        DurationAt(i, r);
        SetAt(i, r);
        OffsetAt(i, r);
    }

    // The default reference for expressions that need none is irrelevant; with a reference the day is the reference's own.
    private void InitZhOnce()
    {
        if (_ref is DateTimeOffset r)
        {
            _today = r.DateTime.Date;
            _now = r.DateTime;
        }
    }

    // ---- reading integers --------------------------------------------------------------------------------------------------------

    /// <summary>Arabic digits (ASCII or full-width), between <paramref name="min"/> and <paramref name="max"/> of them, not part of a longer run.</summary>
    private bool ZArabic(int p, int min, int max, out int value, out int end)
    {
        value = 0;
        end = p;
        int q = p;
        while (q < _t.Length && IsDigitChar(_t[q], out int d))
        {
            value = (value * 10) + d;
            q++;
            if (q - p > max)
            {
                return false;
            }
        }

        if (q - p < min || (p > 0 && (IsDigitChar(_t[p - 1], out _) || char.IsAsciiLetter(_t[p - 1]))))
        {
            return false;
        }

        end = q;
        return true;
    }

    /// <summary>A whole number up to 9999 in digits or Chinese numerals (十二, 二十五, 3).</summary>
    private bool ZInt(int p, out int value, out int end)
    {
        if (ZArabic(p, 1, 4, out value, out end))
        {
            return true;
        }

        value = 0;
        if (p < _t.Length && ParseCn(p, out end, out decimal v, out _) && v is >= 0 and <= 9999 && v == decimal.Truncate(v))
        {
            value = (int)v;
            return true;
        }

        end = p;
        return false;
    }

    /// <summary>A year: four digits (or four Chinese digits, 二〇二六) and 年. <paramref name="end"/> is after 年.</summary>
    private bool ZYear(int p, out int year, out int end)
    {
        year = 0;
        end = p;
        if (ZArabic(p, 4, 4, out year, out int e) && e < _t.Length && _t[e] == Nian)
        {
            end = e + 1;
            return year is >= 1000 and <= 9999;
        }

        int q = p;
        int v = 0;
        while (q < _t.Length && q - p < 4 && Z.Digits.TryGetValue(_t[q], out int d))
        {
            v = (v * 10) + d;
            q++;
        }

        if (q - p == 4 && q < _t.Length && _t[q] == Nian && v >= 1000)
        {
            year = v;
            end = q + 1;
            return true;
        }

        return false;
    }

    private bool ZMonth(int p, out int month, out int end)
    {
        if (ZInt(p, out month, out int e) && month is >= 1 and <= 12 && e < _t.Length && _t[e] == Yue)
        {
            end = e + 1;
            return true;
        }

        end = p;
        return false;
    }

    private bool ZDayNumber(int p, out int day, out int end)
    {
        if (ZInt(p, out day, out int e) && day is >= 1 and <= 31 && e < _t.Length && (_t[e] == Ri || _t[e] == Hao))
        {
            end = e + 1;
            return true;
        }

        end = p;
        return false;
    }

    // ---- days --------------------------------------------------------------------------------------------------------------------

    private static DateTime? Valid(int y, int m, int d) =>
        y is >= 1 and <= 9999 && m is >= 1 and <= 12 && d >= 1 && d <= DateTime.DaysInMonth(y, m) ? new DateTime(y, m, d) : null;

    /// <summary>The first day on or after the reference day with this month and day (the missing year is the nearest coming one).</summary>
    private DateTime? NextMonthDay(int month, int day)
    {
        for (int y = _today.Year; y <= _today.Year + 8; y++)
        {
            if (Valid(y, month, day) is DateTime d && d >= _today)
            {
                return d;
            }
        }

        return null;
    }

    private bool YearAt(int p, out int year, out int end, out bool relative)
    {
        relative = false;
        if (ZYear(p, out year, out end))
        {
            return true;
        }

        foreach ((int len, ZhRelPeriod rp) in Z.RelPeriods.MatchAll(_t, p))
        {
            if (rp.Unit == "year")
            {
                year = _today.Year + rp.Offset;
                end = p + len;
                relative = true;
                return true;
            }
        }

        year = 0;
        end = p;
        return false;
    }

    private void DayAt(int i, List<ZDay> r)
    {
        int n = _t.Length;

        // ISO 8601 and slashed calendar dates: 2026-03-20, 2026/04/01.
        if (ZArabic(i, 4, 4, out int iy, out int ie) && ie < n && _t[ie] is '-' or '/' && ZArabic(ie + 1, 1, 2, out int im, out int ime)
            && ime < n && _t[ime] == _t[ie] && ZArabic(ime + 1, 1, 2, out int id, out int ide) && Valid(iy, im, id) is DateTime iso
            && !(ide < n && (char.IsAsciiDigit(_t[ide]) || _t[ide] == _t[ie])))
        {
            r.Add(new ZDay(ide, TemporalValue.Day(iso, false), null));
        }

        // A date written with 年 月 日, with the year optional or relative (去年12月5日, 今年国庆节).
        if (YearAt(i, out int y, out int ye, out bool rel))
        {
            if (ZMonth(ye, out int m, out int me) && ZDayNumber(me, out int dd, out int de) && Valid(y, m, dd) is DateTime ymd)
            {
                r.Add(new ZDay(de, TemporalValue.Day(ymd, rel), null));
            }

            foreach ((int hl, ZhHoliday h) in Z.Holidays.MatchAll(_t, ye))
            {
                if (Z.DateOf(h, y) is DateTime hd)
                {
                    r.Add(new ZDay(ye + hl, TemporalValue.Day(hd, rel), null));
                }
            }
        }
        else
        {
            if (ZMonth(i, out int m, out int me) && ZDayNumber(me, out int dd, out int de) && NextMonthDay(m, dd) is DateTime md)
            {
                r.Add(new ZDay(de, TemporalValue.Day(md, true), null));
            }
            else if (ZDayNumber(i, out int day, out int bde))
            {
                // 25号: the next such day of a month.
                DateTime? d = NextDayOfMonth(day);
                if (d is DateTime dayDate && !(i > 0 && (_t[i - 1] is '第' || _t[i - 1] == Yue)))
                {
                    r.Add(new ZDay(bde, TemporalValue.Day(dayDate, true), null));
                }
            }
        }

        // 下个月5号: a day of a relative month.
        foreach ((int len, ZhRelPeriod rp) in Z.RelPeriods.MatchAll(_t, i))
        {
            if (rp.Unit == "month" && ZDayNumber(i + len, out int dd, out int de))
            {
                DateTime month = new DateTime(_today.Year, _today.Month, 1).AddMonths(rp.Offset);
                if (Valid(month.Year, month.Month, dd) is DateTime d)
                {
                    r.Add(new ZDay(de, TemporalValue.Day(d, true), null));
                }
            }
        }

        // 今天, 明天, 后天, 昨天, 今晚.
        if (Z.RelDays.Match(_t, i, out int rl, out ZhRelDay rd))
        {
            ZhDayPart? part = rd.DayPart is null ? null : Z.PartsByName[rd.DayPart];
            r.Add(new ZDay(i + rl, TemporalValue.Day(_today.AddDays(rd.Offset), true), part));
        }

        WeekdayAt(i, r);

        // A holiday with no year: the next one.
        foreach ((int hl, ZhHoliday h) in Z.Holidays.MatchAll(_t, i))
        {
            for (int yr = _today.Year; yr <= _today.Year + 3; yr++)
            {
                if (Z.DateOf(h, yr) is DateTime hd && hd >= _today)
                {
                    r.Add(new ZDay(i + hl, TemporalValue.Day(hd, true), null));
                    break;
                }
            }
        }
    }

    private DateTime? NextDayOfMonth(int day)
    {
        for (int k = 0; k < 24; k++)
        {
            DateTime first = new DateTime(_today.Year, _today.Month, 1).AddMonths(k);
            if (Valid(first.Year, first.Month, day) is DateTime d && d >= _today)
            {
                return d;
            }
        }

        return null;
    }

    private bool WeekdayWord(int p, out int weekday, out int end)
    {
        if (Z.Weekdays.Match(_t, p, out int wl, out weekday))
        {
            end = p + wl;
            return true;
        }

        end = p;
        return false;
    }

    /// <summary>A prefix (下, 上个, 这) that puts a weekday in the next, last or this week.</summary>
    private bool WeekPrefix(int p, out int offset, out int end)
    {
        foreach ((int len, ZhRelPeriod rp) in Z.RelPeriods.MatchAll(_t, p))
        {
            if (rp.Unit == "weekday")
            {
                offset = rp.Offset;
                end = p + len;
                return true;
            }
        }

        offset = 0;
        end = p;
        return false;
    }

    private void WeekdayAt(int i, List<ZDay> r)
    {
        bool prefixed = WeekPrefix(i, out int offset, out int p);
        if (!prefixed)
        {
            p = i;
        }

        if (WeekdayWord(p, out int wd, out int e))
        {
            DateTime day = prefixed
                ? CivilCalendar.MondayOf(_today).AddDays((7 * offset) + wd - 1)
                : CivilCalendar.OnOrAfter(_today, wd);
            r.Add(new ZDay(e, TemporalValue.Day(day, true), null));
        }
    }

    // ---- days with times --------------------------------------------------------------------------------------------------------

    private void DayThen(int start, ZDay d, List<ZCand> r)
    {
        DateTime day = d.V.Start;
        bool needs = d.V.NeedsRef;

        if (TimeOfDay(d.End, d.Implied, out ZTod t))
        {
            DateTime at = day + new TimeSpan(t.Hour, t.Minute, 0);
            r.Add(new ZCand(start, t.End, new TemporalValue
            {
                Kind = TemporalKind.DateTime,
                Start = at,
                End = at + TemporalValue.Step(t.Grain),
                Grain = t.Grain,
                NeedsRef = needs,
                Offset = t.Zone,
                ZoneIsZ = t.ZoneZ,
            }));
        }

        if (TimeRangeAt(d.End, d.Implied, out ZTimeRange tr))
        {
            DateTime a = day + new TimeSpan(tr.From.Hour, tr.From.Minute, 0);
            DateTime b = day + new TimeSpan(tr.To.Hour, tr.To.Minute, 0);
            if (b <= a)
            {
                b = b.AddDays(1);
            }

            r.Add(new ZCand(start, tr.End, new TemporalValue
            {
                Kind = TemporalKind.DateTimeRange,
                Start = a,
                End = b,
                Grain = tr.From.Grain == Granularity.Minute || tr.To.Grain == Granularity.Minute ? Granularity.Minute : Granularity.Hour,
                NeedsRef = needs,
                Offset = tr.From.Zone,
                ZoneIsZ = tr.From.ZoneZ,
            }));
        }

        // 明天上午, 周五晚上, 今晚: a part of the day, with no hour.
        ZhDayPart? part = d.Implied;
        int end = d.End;
        if (Z.DayParts.Match(_t, d.End, out int dl, out ZhDayPart dp) && !TimeOfDay(d.End, d.Implied, out _))
        {
            part = dp;
            end = d.End + dl;
        }

        if (part is not null)
        {
            DateTime a = day.AddHours(part.Start);
            DateTime b = day.AddHours(part.End);
            r.Add(new ZCand(start, end, new TemporalValue
            {
                Kind = TemporalKind.DateTimeRange,
                Start = a,
                End = b,
                Grain = Granularity.Hour,
                Approx = true,
                NeedsRef = needs,
            }));
        }
    }

    // ---- times of day ------------------------------------------------------------------------------------------------------------

    private bool HourMark(int p, out int length)
    {
        if (p + 1 < _t.Length && _t[p] == Dian && _t[p + 1] == '\u949F')
        {
            length = 2;
            return true;
        }

        length = 1;
        return p < _t.Length && (_t[p] == Dian || _t[p] == Shi);
    }

    private void TimeAt(int i, List<ZCand> r)
    {
        if (TimeRangeAt(i, null, out ZTimeRange tr))
        {
            DateTime a = TemporalValue.NoDate + new TimeSpan(tr.From.Hour, tr.From.Minute, 0);
            DateTime b = TemporalValue.NoDate + new TimeSpan(tr.To.Hour, tr.To.Minute, 0);
            r.Add(new ZCand(tr.Start, tr.End, new TemporalValue
            {
                Kind = TemporalKind.TimeRange,
                Start = a,
                End = b,
                Grain = tr.From.Grain == Granularity.Minute || tr.To.Grain == Granularity.Minute ? Granularity.Minute : Granularity.Hour,
                Offset = tr.From.Zone,
                ZoneIsZ = tr.From.ZoneZ,
            }));
        }

        if (TimeOfDay(i, null, out ZTod t))
        {
            TemporalValue v = TemporalValue.Clock(t.Hour, t.Minute, 0, t.Grain);
            v.Offset = t.Zone;
            v.ZoneIsZ = t.ZoneZ;
            r.Add(new ZCand(i, t.End, v));
        }
    }

    /// <summary>
    /// A time of day: an optional zone name, an optional part of the day, an hour (9点, 九点, 15时, or 9:30), and a tail
    /// (半, 一刻, 整, or minutes with 分). The part of the day decides morning or afternoon (下午3点 is 15:00).
    /// </summary>
    private bool TimeOfDay(int i, ZhDayPart? implied, out ZTod t)
    {
        t = default;
        int n = _t.Length;
        int p = i;
        int? zone = null;
        bool zoneZ = false;
        if (Z.Zones.Match(_t, p, out int zl, out (int Minutes, bool Z) zv))
        {
            zone = zv.Minutes;
            zoneZ = zv.Z;
            p += zl;
        }

        ZhDayPart? part = implied;
        if (Z.DayParts.Match(_t, p, out int dl, out ZhDayPart dp))
        {
            part = dp;
            p += dl;
        }

        int hour;
        int minute = 0;
        int end;
        Granularity grain = Granularity.Hour;
        bool arabicHour = ZArabic(p, 1, 2, out int hv, out int he);
        if (arabicHour && he + 2 < n + 0 && _t[he] is ':' or '\uFF1A' && ZArabic(he + 1, 2, 2, out int mv, out int me) && mv < 60 && hv <= 24)
        {
            hour = hv;
            minute = mv;
            end = me;
            grain = Granularity.Minute;
            if (me + 2 < n && _t[me] == ':' && ZArabic(me + 1, 2, 2, out int sv, out int se) && sv < 60)
            {
                end = se;
            }
        }
        else
        {
            bool chineseHour = false;
            if (!arabicHour)
            {
                if (!(ParseCn(p, out he, out decimal cv, out _) && cv is >= 0 and <= 24 && cv == decimal.Truncate(cv)))
                {
                    return false;
                }

                hv = (int)cv;
                chineseHour = true;
            }

            if (!HourMark(he, out int hml))
            {
                return false;
            }

            hour = hv;
            end = he + hml;
            bool tail = false;
            bool minuteTail = false;
            if (end < n && _t[end] == Ban)
            {
                minute = 30;
                end++;
                tail = true;
                minuteTail = true;
            }
            else if (end + 1 < n && _t[end + 1] == Ke && (_t[end] is '\u4E00' or '\u4E09' or '1' or '3'))
            {
                minute = _t[end] is '\u4E00' or '1' ? 15 : 45;
                end += 2;
                tail = true;
            }
            else if (end < n && _t[end] == Zheng)
            {
                end++;
                tail = true;
            }
            else if (MinuteTail(end, out int mm, out int mend))
            {
                minute = mm;
                end = mend;
                tail = true;
                minuteTail = true;
            }

            if (minute != 0 || minuteTail)
            {
                grain = Granularity.Minute;
            }

            // 三点 can also be "three points", so a Chinese-numeral hour needs a part of the day, a tail (半, 整, minutes) or 点钟.
            if (chineseHour && part is null && !tail && hml == 1)
            {
                return false;
            }

            if (hour > 24)
            {
                return false;
            }
        }

        if (hour <= 12 && part is not null)
        {
            hour = part.Shift switch
            {
                "pm" => hour < 12 ? hour + 12 : hour,
                "noon" => hour is >= 1 and <= 6 ? hour + 12 : hour,
                "night" => hour == 12 ? 0 : hour,
                "evening" => hour < 12 ? hour + 12 : 0,
                _ => hour,
            };
        }

        if (hour == 24)
        {
            hour = 0;
        }

        t = new ZTod(end, hour, minute, grain, part, zone, zoneZ);
        return true;
    }

    private bool MinuteTail(int p, out int minute, out int end)
    {
        minute = 0;
        end = p;
        if (ZArabic(p, 1, 2, out minute, out end) || (ParseCn(p, out end, out decimal cv, out _) && cv is >= 0 and <= 59 && cv == decimal.Truncate(cv) && (minute = (int)cv) >= 0))
        {
            if (minute < 60 && end < _t.Length && _t[end] == Fen)
            {
                end++;
                if (end < _t.Length && _t[end] == '\u949F')
                {
                    end++;
                }

                return true;
            }
        }

        minute = 0;
        end = p;
        return false;
    }

    private readonly record struct ZTimeRange(int Start, int End, ZTod From, ZTod To);

    /// <summary>Two times joined by 到, 至 or a dash, with an optional 从: 上午9点到11点. The second end takes the part of the day of the first.</summary>
    private bool TimeRangeAt(int i, ZhDayPart? implied, out ZTimeRange range)
    {
        range = default;
        int p = i;
        if (Z.Role("from", _t, p, out int fl))
        {
            p += fl;
        }

        if (!TimeOfDay(p, implied, out ZTod a) || !Z.Role("rangeto", _t, a.End, out int tl))
        {
            return false;
        }

        // The first end takes the zone it names; the second borrows its part of the day when it has none of its own.
        if (!TimeOfDay(a.End + tl, a.Part ?? implied, out ZTod b))
        {
            return false;
        }

        // 9:00-17:00 and 8:30~12:00 are only ranges of times when both ends are on a clock.
        range = new ZTimeRange(i, b.End, a, b);
        return true;
    }

    // ---- periods -----------------------------------------------------------------------------------------------------------------

    private static TemporalValue Per(DateTime start, DateTime end, Granularity g, bool approx, bool needs) =>
        TemporalValue.Period(start, end, g, approx, needs);

    private void PeriodAt(int i, List<ZCand> r)
    {
        // Years and what can follow one: 2026年, 2026年3月, 2026年夏天, 2026年第二季度, 今年上半年, 1990年代.
        if (YearAt(i, out int y, out int ye, out bool rel))
        {
            var jan = new DateTime(y, 1, 1);
            if (ye < _t.Length && _t[ye] == '\u4EE3' && !rel)
            {
                // 1990年代: the decade.
                r.Add(new ZCand(i, ye + 1, Per(new DateTime((y / 10) * 10, 1, 1), new DateTime(((y / 10) * 10) + 10, 1, 1), Granularity.Decade, false, false)));
            }

            r.Add(new ZCand(i, ye, Per(jan, jan.AddYears(1), Granularity.Year, false, rel)));
            if (ZMonth(ye, out int m, out int me))
            {
                DateTime first = new(y, m, 1);
                r.Add(new ZCand(i, me, Per(first, first.AddMonths(1), Granularity.Month, false, rel)));
                MonthPart(i, me, first, rel, r);
            }

            if (Z.Seasons.Match(_t, ye, out int sl, out ZhSeason season))
            {
                DateTime start = new(y, season.FirstMonth, 1);
                r.Add(new ZCand(i, ye + sl, Per(start, start.AddMonths(season.Months), Granularity.Season, true, rel)));
            }

            if (QuarterAt(ye, out int q, out int qe))
            {
                DateTime start = new(y, ((q - 1) * 3) + 1, 1);
                r.Add(new ZCand(i, qe, Per(start, start.AddMonths(3), Granularity.Quarter, false, rel)));
            }

            if (Z.Role("firsthalf", _t, ye, out int h1))
            {
                r.Add(new ZCand(i, ye + h1, Per(jan, jan.AddMonths(6), Granularity.Half, false, rel)));
            }

            if (Z.Role("secondhalf", _t, ye, out int h2))
            {
                r.Add(new ZCand(i, ye + h2, Per(jan.AddMonths(6), jan.AddYears(1), Granularity.Half, false, rel)));
            }
        }

        foreach ((int len, ZhRelPeriod rp) in Z.RelPeriods.MatchAll(_t, i))
        {
            int e = i + len;
            switch (rp.Unit)
            {
                case "month":
                {
                    DateTime first = new DateTime(_today.Year, _today.Month, 1).AddMonths(rp.Offset);
                    r.Add(new ZCand(i, e, Per(first, first.AddMonths(1), Granularity.Month, false, true)));
                    MonthPart(i, e, first, true, r);
                    break;
                }

                case "week":
                {
                    DateTime monday = CivilCalendar.MondayOf(_today).AddDays(7 * rp.Offset);
                    r.Add(new ZCand(i, e, Per(monday, monday.AddDays(7), Granularity.Week, false, true)));
                    break;
                }

                case "quarter":
                {
                    DateTime first = CivilCalendar.QuarterStart(_today).AddMonths(3 * rp.Offset);
                    r.Add(new ZCand(i, e, Per(first, first.AddMonths(3), Granularity.Quarter, false, true)));
                    break;
                }

                case "weekday":
                    // 这个周末, 下周末: the Saturday and Sunday of a week.
                    if (Z.Role("weekend", _t, e, out int wl))
                    {
                        DateTime sat = CivilCalendar.MondayOf(_today).AddDays((7 * rp.Offset) + 5);
                        r.Add(new ZCand(i, e + wl, Per(sat, sat.AddDays(2), Granularity.Day, false, true)));
                    }

                    break;
            }
        }

        if (Z.Role("weekend", _t, i, out int w0))
        {
            DateTime sat = CivilCalendar.MondayOf(_today).AddDays(5);
            r.Add(new ZCand(i, i + w0, Per(sat, sat.AddDays(2), Granularity.Day, false, true)));
        }

        // 去年下半年 and 本季度 need the reference; a bare 上半年 does not name a year.
    }

    // 3月初, 3月上旬, 3月中旬, 3月底: the parts of a month, approximate. The same split as early, mid and late in English.
    private void MonthPart(int start, int e, DateTime first, bool needs, List<ZCand> r)
    {
        int days = DateTime.DaysInMonth(first.Year, first.Month);
        if (Z.Role("monthearly", _t, e, out int l1))
        {
            r.Add(new ZCand(start, e + l1, Per(first, first.AddDays(10), Granularity.Month, true, needs)));
        }

        if (Z.Role("monthmid", _t, e, out int l2))
        {
            r.Add(new ZCand(start, e + l2, Per(first.AddDays(10), first.AddDays(20), Granularity.Month, true, needs)));
        }

        if (Z.Role("monthlate", _t, e, out int l3))
        {
            r.Add(new ZCand(start, e + l3, Per(first.AddDays(20), first.AddDays(days), Granularity.Month, true, needs)));
        }
    }

    /// <summary>第二季度, 二季度, 第2季度: a quarter of a year.</summary>
    private bool QuarterAt(int p, out int quarter, out int end)
    {
        int q = p;
        if (Z.Role("ordinal", _t, q, out int ol))
        {
            q += ol;
        }

        if (ZInt(q, out quarter, out int e) && quarter is >= 1 and <= 4 && Z.Role("quarter", _t, e, out int ql))
        {
            end = e + ql;
            return true;
        }

        quarter = 0;
        end = p;
        return false;
    }

    // ---- ranges of days, months and years ------------------------------------------------------------------------------------------

    /// <summary>Which parts of a date were written.</summary>
    private readonly record struct ZYmd(int? Year, int? Month, int? Day, int End);

    private bool Ymd(int p, out ZYmd d)
    {
        int? year = null;
        int q = p;
        if (ZYear(q, out int y, out int ye))
        {
            year = y;
            q = ye;
        }

        int? month = null;
        if (ZMonth(q, out int m, out int me))
        {
            month = m;
            q = me;
        }

        int? day = null;
        if (ZDayNumber(q, out int dd, out int de) && (month is not null || year is null))
        {
            day = dd;
            q = de;
        }

        d = new ZYmd(year, month, day, q);
        return q > p;
    }

    private void DateRangeAt(int i, List<ZCand> r)
    {
        int p = i;
        if (Z.Role("from", _t, p, out int fl))
        {
            p += fl;
        }

        if (Ymd(p, out ZYmd a) && Z.Role("rangeto", _t, a.End, out int tl) && Ymd(a.End + tl, out ZYmd b))
        {
            int bEnd = b.End;
            if (a.Day is int ad && a.Month is int am)
            {
                // 4月6日到4月10日, 5月1日至5日, 12月28日到1月3日: days.
                int ay;
                if (a.Year is int ya)
                {
                    ay = ya;
                }
                else if (NextMonthDay(am, ad) is DateTime next)
                {
                    ay = next.Year;
                }
                else
                {
                    return;
                }

                if (Valid(ay, am, ad) is not DateTime start)
                {
                    return;
                }

                int bm = b.Month ?? am;
                int by = b.Year ?? ay;
                if (b.Day is int bd)
                {
                    if (b.Month is null)
                    {
                        by = ay;
                    }
                    else if (b.Year is null && Valid(by, bm, bd) is DateTime t1 && t1 < start)
                    {
                        by++;
                    }

                    if (Valid(by, bm, bd) is DateTime end && end >= start)
                    {
                        r.Add(new ZCand(i, bEnd, Per(start, end.AddDays(1), Granularity.Day, false, a.Year is null)));
                    }
                }
            }
            else if (a.Month is int mo && a.Day is null && b.Day is null)
            {
                // 2026年6月至8月: months.
                int ay = a.Year ?? _today.Year;
                int by = b.Year ?? ay;
                int bm = b.Month ?? 0;
                if (bm > 0 && Valid(ay, mo, 1) is DateTime start && Valid(by, bm, 1) is DateTime last && last >= start)
                {
                    r.Add(new ZCand(i, bEnd, Per(start, last.AddMonths(1), Granularity.Month, false, a.Year is null)));
                }
            }
            else if (a.Year is int y1 && a.Month is null && b.Year is int y2 && b.Month is null && y2 >= y1)
            {
                // 2020年到2024年: years.
                r.Add(new ZCand(i, bEnd, Per(new DateTime(y1, 1, 1), new DateTime(y2 + 1, 1, 1), Granularity.Year, false, false)));
            }
        }

        // 周一到周五: weekdays of the coming week.
        bool pre = WeekPrefix(p, out int off, out int wp);
        if (!pre)
        {
            wp = p;
        }

        if (WeekdayWord(wp, out int w1, out int w1e) && Z.Role("rangeto", _t, w1e, out int rl))
        {
            int q = w1e + rl;
            if (WeekPrefix(q, out _, out int q2))
            {
                q = q2;
            }

            if (WeekdayWord(q, out int w2, out int w2e) && w2 > w1)
            {
                DateTime first = pre ? CivilCalendar.MondayOf(_today).AddDays((7 * off) + w1 - 1) : CivilCalendar.OnOrAfter(_today, w1);
                r.Add(new ZCand(i, w2e, Per(first, first.AddDays(w2 - w1 + 1), Granularity.Day, false, true)));
            }
        }
    }

    // ---- amounts of time ---------------------------------------------------------------------------------------------------------

    /// <summary>An amount: digits (with a decimal point), a Chinese numeral, or 半 (a half).</summary>
    private bool ZAmount(int p, out decimal amount, out int end, bool afterGap = false)
    {
        amount = 0;
        end = p;
        if (p < _t.Length && _t[p] == Ban)
        {
            amount = 0.5m;
            end = p + 1;
            return true;
        }

        if (p > 0 && !afterGap && (_t[p - 1] == '\u7B2C' || (IsNumeralChar(_t[p - 1]) && IsNumeralChar(_t[p]))))
        {
            return false;
        }

        if (p < _t.Length && IsDigitChar(_t[p], out _) && ReadNumber(p, false, out ZNum num) && num.Arabic)
        {
            amount = num.Value;
            end = num.End;
            return true;
        }

        if (p < _t.Length && ParseCn(p, out end, out amount, out _))
        {
            return true;
        }

        end = p;
        return false;
    }

    private void DurationAt(int i, List<ZCand> r)
    {
        decimal seconds = 0;
        decimal months = 0;
        decimal years = 0;
        var parts = new List<(decimal Amount, ZhUnit Unit)>();
        int p = i;
        decimal previous = decimal.MaxValue;
        while (true)
        {
            int q = p;
            bool afterGap = false;
            if (parts.Count > 0)
            {
                // 两年零三个月: a gap word between terms.
                if (Z.Role("gap", _t, q, out int gl))
                {
                    q += gl;
                    afterGap = true;
                }
            }

            if (!ZAmount(q, out decimal amount, out int ae, afterGap))
            {
                break;
            }

            // 一个半小时: n and a half, then a unit written without 个.
            bool plusHalf = false;
            int ue = ae;
            if (Z.Role("andahalf", _t, ae, out int hl))
            {
                plusHalf = true;
                ue = ae + hl;
            }

            if (!Z.Units.Match(_t, ue, out int ul, out ZhUnit unit))
            {
                break;
            }

            // The year of a date is a number of four digits: 2026年 is not two thousand and twenty-six years.
            if (unit.Unit == DurationUnit.Year && amount >= 1000)
            {
                break;
            }

            if (plusHalf)
            {
                amount += 0.5m;
            }

            int end = ue + ul;

            // 一年半, 三天半: a half more, after the unit.
            if (!plusHalf && end < _t.Length && _t[end] == Ban && unit.Unit is DurationUnit.Year or DurationUnit.Day or DurationUnit.Hour or DurationUnit.Week)
            {
                amount += 0.5m;
                end++;
            }

            if (unit.Seconds == 0 && unit.Months == 0)
            {
                // Business days have no length, so they stand alone.
                if (parts.Count == 0)
                {
                    string count = Trim(amount).ToString(CultureInfo.InvariantCulture);
                    r.Add(new ZCand(i, end, new TemporalValue
                    {
                        Kind = TemporalKind.Duration,
                        Text = $"P{count}BD",
                        DurUnit = DurationUnit.BusinessDay,
                        Count = Trim(amount),
                    }));
                }

                return;
            }

            decimal size = unit.Seconds > 0 ? unit.Seconds : unit.Months * 2_592_000m;
            if (parts.Count > 0 && size >= previous)
            {
                break;
            }

            previous = size;
            if (unit.Months > 0 && amount != decimal.Truncate(amount) && !(unit.Months == 12 && amount * 2 == decimal.Truncate(amount * 2)))
            {
                return;
            }

            parts.Add((amount, unit));
            seconds += amount * unit.Seconds;
            if (unit.Months == 12)
            {
                years += amount;
            }
            else
            {
                months += amount * unit.Months;
            }
            p = end;

            var v = new TemporalValue
            {
                Kind = TemporalKind.Duration,
                Text = TemporalValue.DurationText(seconds, years, months),
                Seconds = months == 0 && years == 0 ? seconds : null,
            };
            CountOfZh(parts, v);
            r.Add(new ZCand(i, p, v));
        }
    }

    // A duration of one term keeps its unit; several terms are counted in the smallest when they are all clock and day units or all month and year units.
    private static void CountOfZh(List<(decimal Amount, ZhUnit Unit)> parts, TemporalValue v)
    {
        if (parts.Count == 1)
        {
            v.DurUnit = parts[0].Unit.Unit;
            v.Count = Trim(parts[0].Unit.Name == "quarterhour" ? parts[0].Amount * 15 : parts[0].Amount);
            return;
        }

        bool monthFamily = parts.All(x => x.Unit.Months > 0);
        bool clockFamily = parts.All(x => x.Unit.Seconds > 0 && x.Unit.Months == 0);
        if (monthFamily)
        {
            decimal total = parts.Sum(x => x.Amount * x.Unit.Months);
            v.DurUnit = DurationUnit.Month;
            v.Count = Trim(total);
        }
        else if (clockFamily)
        {
            (decimal Amount, ZhUnit Unit) smallest = parts.OrderBy(x => x.Unit.Seconds).First();
            decimal total = parts.Sum(x => x.Amount * x.Unit.Seconds) / smallest.Unit.Seconds;
            v.DurUnit = smallest.Unit.Unit;
            v.Count = Trim(total);
        }
    }

    // 三天后, 两周前, 3个月后, 30分钟后: an amount of time before or after the reference.
    private void OffsetAt(int i, List<ZCand> r)
    {
        if (!ZAmount(i, out decimal amount, out int ae) || !Z.Units.Match(_t, ae, out int ul, out ZhUnit unit))
        {
            return;
        }

        int e = ae + ul;
        int sign;
        if (Z.Role("later", _t, e, out int ll))
        {
            sign = 1;
            e += ll;
        }
        else if (Z.Role("ago", _t, e, out int al))
        {
            sign = -1;
            e += al;
        }
        else
        {
            return;
        }

        if (unit.Unit == DurationUnit.Year && amount >= 1000)
        {
            return;
        }

        decimal signed = sign * amount;
        switch (unit.Unit)
        {
            case DurationUnit.Day or DurationUnit.Week:
            {
                decimal days = signed * (unit.Unit == DurationUnit.Week ? 7 : 1);
                if (days != decimal.Truncate(days))
                {
                    return;
                }

                r.Add(new ZCand(i, e, TemporalValue.Day(_today.AddDays((double)days), true)));
                break;
            }

            case DurationUnit.Month or DurationUnit.Year:
            {
                decimal months = signed * (unit.Unit == DurationUnit.Year ? 12 : 1);
                if (months != decimal.Truncate(months))
                {
                    return;
                }

                r.Add(new ZCand(i, e, TemporalValue.Day(_today.AddMonths((int)months), true)));
                break;
            }

            case DurationUnit.Hour or DurationUnit.Minute or DurationUnit.Second:
            {
                double secs = (double)(signed * unit.Seconds);
                DateTime at = _now.AddSeconds(secs);
                Granularity g = unit.Unit == DurationUnit.Second ? Granularity.Second : Granularity.Minute;
                if (g == Granularity.Minute)
                {
                    at = new DateTime(at.Year, at.Month, at.Day, at.Hour, at.Minute, 0);
                }

                r.Add(new ZCand(i, e, new TemporalValue
                {
                    Kind = TemporalKind.DateTime,
                    Start = at,
                    End = at + TemporalValue.Step(g),
                    Grain = g,
                    NeedsRef = true,
                }));
                break;
            }
        }
    }

    // ---- recurrences -------------------------------------------------------------------------------------------------------------

    private void SetAt(int i, List<ZCand> r)
    {
        if (Z.Role("daily", _t, i, out int dl))
        {
            AddSet(i, i + dl, "P1D", Granularity.Day, r);
            return;
        }

        int p;
        bool everyOther = false;
        if (Z.Role("everyother", _t, i, out int eo))
        {
            p = i + eo;
            everyOther = true;
        }
        else if (Z.Role("every", _t, i, out int el))
        {
            p = i + el;
        }
        else
        {
            return;
        }

        // 每周一, 每个星期三: a weekday.
        if (!everyOther && WeekdayWord(p, out int wd, out int we))
        {
            string code = "XXXX-WXX-" + wd.ToString(CultureInfo.InvariantCulture);
            AddSet(i, we, code, Granularity.Day, r);
            if (TimeOfDay(we, null, out ZTod t))
            {
                AddSet(i, t.End, code + Clock(t), t.Grain, r);
            }

            return;
        }

        decimal count = 1;
        int ue = p;
        bool counted = ZAmount(p, out decimal amount, out int ae);
        if (counted)
        {
            count = amount;
            ue = ae;
        }

        if (!SetUnit(ue, out string letter, out bool time, out Granularity g, out int unitEnd))
        {
            return;
        }

        string n = Trim(count).ToString(CultureInfo.InvariantCulture);
        string period = (time ? "PT" : "P") + n + letter;
        AddSet(i, unitEnd, period, g, r);

        // 每月15号: a day of every month.
        if (letter == "M" && !time && !counted && ZDayNumber(unitEnd, out int day, out int de))
        {
            AddSet(i, de, "XXXX-XX-" + day.ToString("00", CultureInfo.InvariantCulture), Granularity.Month, r);
        }

        // 每天早上8点: the time of day of a daily recurrence.
        if (period == "P1D" && TimeOfDay(unitEnd, null, out ZTod dt))
        {
            AddSet(i, dt.End, Clock(dt), dt.Grain, r);
        }
    }

    private static string Clock(ZTod t) =>
        "T" + t.Hour.ToString("00", CultureInfo.InvariantCulture) + ":" + t.Minute.ToString("00", CultureInfo.InvariantCulture);

    private static void AddSet(int start, int end, string value, Granularity g, List<ZCand> r) =>
        r.Add(new ZCand(start, end, new TemporalValue { Kind = TemporalKind.Set, Text = value, Grain = g }));

    private bool SetUnit(int p, out string letter, out bool time, out Granularity g, out int end)
    {
        letter = string.Empty;
        time = false;
        g = Granularity.Day;
        end = p;
        if (p < _t.Length && _t[p] == Yue)
        {
            (letter, g, end) = ("M", Granularity.Month, p + 1);
            return true;
        }

        if (!Z.Units.Match(_t, p, out int ul, out ZhUnit u))
        {
            return false;
        }

        end = p + ul;
        switch (u.Unit)
        {
            case DurationUnit.Day:
                (letter, g) = ("D", Granularity.Day);
                return true;
            case DurationUnit.Week:
                (letter, g) = ("W", Granularity.Week);
                return true;
            case DurationUnit.Month:
                (letter, g) = ("M", Granularity.Month);
                return true;
            case DurationUnit.Year:
                (letter, g) = ("Y", Granularity.Year);
                return true;
            case DurationUnit.Hour:
                (letter, time, g) = ("H", true, Granularity.Hour);
                return true;
            case DurationUnit.Minute:
                (letter, time, g) = ("M", true, Granularity.Minute);
                return true;
            default:
                return false;
        }
    }
}
