namespace RecursiveAI.OneNlp.Recognizers;

/// <summary>Proleptic Gregorian calendar arithmetic for the date recognizers. Weekdays are numbered 1 (Monday) to 7 (Sunday), as in ISO 8601.</summary>
internal static class CivilCalendar
{
    internal static int IsoWeekday(DateTime d) => d.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)d.DayOfWeek;

    /// <summary>The Monday of the ISO week containing a day (ISO 8601 §3.2.2: weeks run Monday to Sunday).</summary>
    internal static DateTime MondayOf(DateTime d) => d.Date.AddDays(1 - IsoWeekday(d));

    /// <summary>
    /// The day <paramref name="n"/> business days after <paramref name="from"/> (before it when negative). Business days are Monday to Friday;
    /// public holidays are not considered. Counting from a Saturday or Sunday starts at the weekday on the other side of the weekend from
    /// the direction of travel (Friday going forward, Monday going back), so Saturday plus one business day is Monday.
    /// </summary>
    internal static DateTime AddBusinessDays(DateTime from, int n)
    {
        DateTime d = from.Date;
        if (n == 0)
        {
            return d;
        }

        int wd = IsoWeekday(d);
        if (wd > 5)
        {
            d = d.AddDays(n > 0 ? 5 - wd : 8 - wd);
        }

        int sign = Math.Sign(n);
        int steps = Math.Abs(n);
        d = d.AddDays(7 * sign * (steps / 5));
        for (int k = steps % 5; k > 0; k--)
        {
            d = d.AddDays(sign);
            while (IsoWeekday(d) > 5)
            {
                d = d.AddDays(sign);
            }
        }

        return d;
    }

    /// <summary>The first day on or after <paramref name="from"/> that falls on <paramref name="weekday"/>.</summary>
    internal static DateTime OnOrAfter(DateTime from, int weekday) => from.Date.AddDays((weekday - IsoWeekday(from) + 7) % 7);

    /// <summary>The last day strictly before <paramref name="from"/> that falls on <paramref name="weekday"/>.</summary>
    internal static DateTime Before(DateTime from, int weekday)
    {
        int back = (IsoWeekday(from) - weekday + 7) % 7;
        return from.Date.AddDays(back == 0 ? -7 : -back);
    }

    /// <summary>The <paramref name="n"/>th weekday of a month (1-based), or the last one when <paramref name="n"/> is -1. Null when the month has no such day.</summary>
    internal static DateTime? NthWeekday(int year, int month, int weekday, int n)
    {
        if (n > 0)
        {
            DateTime first = OnOrAfter(new DateTime(year, month, 1), weekday).AddDays(7 * (n - 1));
            return first.Month == month ? first : null;
        }

        var last = new DateTime(year, month, DateTime.DaysInMonth(year, month));
        return last.AddDays(-((IsoWeekday(last) - weekday + 7) % 7));
    }

    /// <summary>
    /// The date of Western Easter Sunday in a Gregorian year: the "Anonymous Gregorian" computus as published by Meeus (Astronomical
    /// Algorithms, ch. 8) and Butcher (1876), valid for every Gregorian year.
    /// </summary>
    internal static DateTime Easter(int year)
    {
        int a = year % 19;
        int b = year / 100;
        int c = year % 100;
        int d = b / 4;
        int e = b % 4;
        int f = (b + 8) / 25;
        int g = (b - f + 1) / 3;
        int h = ((19 * a) + b - d - g + 15) % 30;
        int i = c / 4;
        int k = c % 4;
        int l = (32 + (2 * e) + (2 * i) - h - k) % 7;
        int m = (a + (11 * h) + (22 * l)) / 451;
        int month = (h + l - (7 * m) + 114) / 31;
        int day = ((h + l - (7 * m) + 114) % 31) + 1;
        return new DateTime(year, month, day);
    }

    /// <summary>The date of a holiday in a year.</summary>
    internal static DateTime? Holiday(HolidayRule rule, int year)
    {
        switch (rule.Kind)
        {
            case HolidayKind.Fixed:
                return rule.Month == 2 && rule.Day == 29 && !DateTime.IsLeapYear(year) ? null : new DateTime(year, rule.Month, rule.Day);
            case HolidayKind.Nth:
                return NthWeekday(year, rule.Month, rule.Weekday, rule.N)?.AddDays(rule.Plus);
            case HolidayKind.Easter:
                return Easter(year).AddDays(rule.Plus);
            case HolidayKind.Table:
                foreach (DateTime d in rule.Dates!)
                {
                    if (d.Year == year)
                    {
                        return d;
                    }
                }

                return null;
            default:
            {
                // The weekday nearest a fixed date (Remembrance Sunday is the Sunday nearest 11 November): within three days either way.
                var anchor = new DateTime(year, rule.Month, rule.Day);
                int delta = (rule.Weekday - IsoWeekday(anchor) + 7) % 7;
                return anchor.AddDays(delta > 3 ? delta - 7 : delta);
            }
        }
    }

    /// <summary>The first day of the quarter containing a day.</summary>
    internal static DateTime QuarterStart(DateTime d) => new(d.Year, (((d.Month - 1) / 3) * 3) + 1, 1);
}
