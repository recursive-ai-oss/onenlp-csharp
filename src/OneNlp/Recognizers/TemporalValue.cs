using System.Globalization;
using System.Text;

namespace RecursiveAI.OneNlp.Recognizers;

/// <summary>
/// A temporal reading of some words, before it becomes a <see cref="TemporalMatch"/>. Times and dates are wall-clock values with no
/// zone; <see cref="Offset"/> holds the zone the text names. A time of day with no date sits on <see cref="NoDate"/>.
/// </summary>
internal sealed class TemporalValue
{
    /// <summary>The date a time of day without a date is stored on.</summary>
    internal static readonly DateTime NoDate = DateTime.MinValue.Date;

    internal TemporalKind Kind;

    /// <summary>The start of the day, instant or range.</summary>
    internal DateTime Start;

    /// <summary>The end: exclusive for days and longer periods; for an instant, the start plus its precision.</summary>
    internal DateTime End;

    internal Granularity? Grain;
    internal bool Approx;

    /// <summary>The zone offset in minutes the text names, if any; <see cref="ZoneIsZ"/> when it is written as UTC.</summary>
    internal int? Offset;

    internal bool ZoneIsZ;

    /// <summary>The reading depends on the reference date, so it is left out when none was given.</summary>
    internal bool NeedsRef;

    /// <summary>Durations and sets keep their value text; <see cref="Seconds"/> is the length of a duration that has one.</summary>
    internal string? Text;

    internal decimal? Seconds;

    /// <summary>A bare hour such as the 12 of "between 12 and 1 pm": usable only as an end of a time range.</summary>
    internal bool Bare;

    /// <summary>The time was written on a 24-hour clock (<c>15:45</c>), so a range end cannot lend it a meridiem.</summary>
    internal bool Clock24;

    /// <summary>The time carried an am/pm marker or a part of the day.</summary>
    internal bool Meridiem;

    /// <summary>The value is a part of a day taken from a date (<c>last night</c>), not yet joined to a time.</summary>
    internal bool PartOfDay;

    internal TemporalValue Clone() => (TemporalValue)MemberwiseClone();

    internal static TemporalValue Day(DateTime d, bool needsRef) =>
        new() { Kind = TemporalKind.Date, Start = d.Date, End = d.Date.AddDays(1), Grain = Granularity.Day, NeedsRef = needsRef };

    internal static TemporalValue Period(DateTime start, DateTime end, Granularity grain, bool approx, bool needsRef) =>
        new() { Kind = TemporalKind.DateRange, Start = start, End = end, Grain = grain, Approx = approx, NeedsRef = needsRef };

    internal static TemporalValue Clock(int h, int m, int s, Granularity grain) =>
        new()
        {
            Kind = TemporalKind.Time,
            Start = NoDate.AddHours(h).AddMinutes(m).AddSeconds(s),
            End = NoDate.AddHours(h).AddMinutes(m).AddSeconds(s).Add(Step(grain)),
            Grain = grain,
        };

    internal static TimeSpan Step(Granularity g) => g switch
    {
        Granularity.Second => TimeSpan.FromSeconds(1),
        Granularity.Minute => TimeSpan.FromMinutes(1),
        _ => TimeSpan.FromHours(1),
    };

    internal string Label => Kind switch
    {
        TemporalKind.Date or TemporalKind.DateRange => "DATE",
        TemporalKind.Time or TemporalKind.TimeRange => "TIME",
        TemporalKind.DateTime or TemporalKind.DateTimeRange => "DATETIME",
        TemporalKind.Duration => "DURATION",
        _ => "SET",
    };

    /// <summary>The value text (see <see cref="TemporalMatch.Value"/>).</summary>
    internal string Format()
    {
        if (Text is not null)
        {
            return Text;
        }

        CultureInfo c = CultureInfo.InvariantCulture;
        string zone = Zone();
        switch (Kind)
        {
            case TemporalKind.Date:
                return Start.ToString("yyyy-MM-dd", c);
            case TemporalKind.DateRange:
                return Start.ToString("yyyy-MM-dd", c) + "/" + End.ToString("yyyy-MM-dd", c);
            case TemporalKind.Time:
                return Clk(Start) + zone;
            case TemporalKind.TimeRange:
                return Clk(Start) + zone + "/" + Clk(End) + zone;
            case TemporalKind.DateTime:
                return Full(Start) + zone;
            default:
                return Full(Start) + zone + "/" + Full(End) + zone;
        }
    }

    internal string Zone()
    {
        if (Offset is not int o)
        {
            return string.Empty;
        }

        if (ZoneIsZ)
        {
            return "Z";
        }

        int a = Math.Abs(o);
        return string.Create(CultureInfo.InvariantCulture, $"{(o < 0 ? '-' : '+')}{a / 60:00}:{a % 60:00}");
    }

    private string Clk(DateTime t) => "T" + t.ToString(Grain == Granularity.Second ? "HH:mm:ss" : "HH:mm", CultureInfo.InvariantCulture);

    private string Full(DateTime t) => t.ToString(Grain == Granularity.Second ? "yyyy-MM-dd'T'HH:mm:ss" : "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Places the value on the calendar. Null for durations and sets, and for a time of day when there is no reference date.</summary>
    internal DateRange? ToRange(DateTimeOffset? reference)
    {
        if (Kind is TemporalKind.Duration or TemporalKind.Set)
        {
            return null;
        }

        DateTime start = Start;
        DateTime end = End;
        if (Kind is TemporalKind.Time or TemporalKind.TimeRange)
        {
            if (reference is null)
            {
                return null;
            }

            DateTime day = reference.Value.DateTime.Date;
            start = day + (Start - NoDate);
            end = day + (End - NoDate);
            if (end < start)
            {
                end = end.AddDays(1);
            }
        }

        TimeSpan offset = Offset is int o ? TimeSpan.FromMinutes(o) : reference?.Offset ?? TimeSpan.Zero;
        return new DateRange(new DateTimeOffset(start, offset), new DateTimeOffset(end, offset), Grain ?? Granularity.Day, Approx);
    }

    /// <summary>The value text of a duration (see <see cref="TemporalMatch.Value"/>): whole seconds, or ISO 8601 when it names months or years.</summary>
    internal static string DurationText(decimal seconds, decimal years, decimal months)
    {
        CultureInfo c = CultureInfo.InvariantCulture;
        if (years == 0 && months == 0)
        {
            return Trim(seconds).ToString(c);
        }

        // A fraction of a year is stated in months (2.5 years is P2Y6M); the other units keep their amounts.
        decimal wholeYears = decimal.Truncate(years);
        months += (years - wholeYears) * 12;
        var sb = new StringBuilder("P");
        if (wholeYears != 0)
        {
            sb.Append(Trim(wholeYears).ToString(c)).Append('Y');
        }

        if (months != 0)
        {
            sb.Append(Trim(months).ToString(c)).Append('M');
        }

        decimal days = decimal.Truncate(seconds / 86400);
        decimal rest = seconds - (days * 86400);
        if (days != 0)
        {
            sb.Append(Trim(days).ToString(c)).Append('D');
        }

        if (rest != 0)
        {
            sb.Append('T');
            decimal hours = decimal.Truncate(rest / 3600);
            rest -= hours * 3600;
            decimal minutes = decimal.Truncate(rest / 60);
            rest -= minutes * 60;
            if (hours != 0)
            {
                sb.Append(Trim(hours).ToString(c)).Append('H');
            }

            if (minutes != 0)
            {
                sb.Append(Trim(minutes).ToString(c)).Append('M');
            }

            if (rest != 0)
            {
                sb.Append(Trim(rest).ToString(c)).Append('S');
            }
        }

        return sb.ToString();
    }

    private static decimal Trim(decimal d) => d / 1.0000000000000000000000000000m;
}
