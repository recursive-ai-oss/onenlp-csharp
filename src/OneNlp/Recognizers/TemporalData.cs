using System.Globalization;

namespace RecursiveAI.OneNlp.Recognizers;

/// <summary>One word or phrase of the date/time vocabulary: what role it plays and its numbers. The meaning of A and B depends on the role.</summary>
/// <remarks>
/// month: A = 1..12. weekday: A = 1 (Monday) .. 7. unit: A = seconds (0 for months and years), B = months, S = canonical name.
/// season: A = first month, B = length in months, S = name. daypart: A and B = start and end hour (end may be 24), S = name.
/// meridiem: A = 0 (am) or 1 (pm). zone: A = offset in minutes, B = 1 when it is written <c>Z</c> (UTC). recur: A = granularity, S = TIMEX3 period.
/// holiday: A = index in the holiday table, B = region mask, S = name. Any other role (today, next, ago, ...): no numbers.
/// </remarks>
internal readonly record struct TWord(string Role, int A, int B, string S);

internal enum HolidayKind
{
    Fixed,
    Nth,
    Easter,
    Nearest,
}

/// <summary>How a holiday's date is computed in a year. See <c>docs/recognizers.md</c> for the grammar in rule data.</summary>
internal readonly record struct HolidayRule(HolidayKind Kind, int N, int Weekday, int Month, int Day, int Plus);

/// <summary>
/// The date/time vocabulary of one language: month, weekday, unit, season, day-part, zone and holiday words, and the role words
/// (today, next, ago, from, to, ...). Read from the <c>time.*</c> sections of rule data. The grammar that uses them is code.
/// </summary>
internal sealed class TemporalData
{
    internal const int RegionUs = 1;
    internal const int RegionGb = 2;
    internal const int RegionFr = 4;
    internal const int RegionDe = 8;
    internal const int RegionAll = RegionUs | RegionGb | RegionFr | RegionDe;

    private static readonly HashSet<string> Roles = new(StringComparer.Ordinal)
    {
        "today", "tomorrow", "yesterday", "tonight", "noon", "midnight", "this", "next", "last", "ago", "later", "in", "at", "on", "of", "and",
        "rangeto", "from", "between", "every", "each", "other", "early", "mid", "late", "after", "before", "half", "quarter", "article", "the",
        "oclock", "approx", "weekend", "zonebase", "quarterprefix", "halfprefix", "past", "to", "plural", "year", "ordinalword", "secondfollow",

        // French and German: a determiner before a date expression, the words "il y a" / "vor" before an amount, "vendredi prochain",
        // 15h / 15 Uhr, "et quart", "halb drei", "1990er Jahre".
        "determiner", "agoprefix", "dayaftertomorrow", "daybeforeyesterday", "nextpost", "lastpost", "andhalf", "hourmark", "hourword",
        "plus15", "plus30", "minus15", "minus", "halfbefore", "plusquarter", "minusquarter", "daypartprefix", "decadeprefix", "decadesuffix", "decadeword",
    };

    private static readonly HashSet<string> FlagNames = new(StringComparer.Ordinal) { "daypart-direct" };

    private static readonly string[] WeekdayCodes = ["mon", "tue", "wed", "thu", "fri", "sat", "sun"];

    internal FormTable<TWord> Words { get; } = new();

    internal List<HolidayRule> Holidays { get; } = [];

    /// <summary>The lower-cased first item of every vocabulary form, to skip positions that cannot start one.</summary>
    internal HashSet<string> Starts { get; } = new(StringComparer.OrdinalIgnoreCase);

    internal bool IsEmpty => Words.Count == 0;

    /// <summary>Behaviours the rule data switches on (<c>time.flag</c>): <c>daypart-direct</c> lets a part of the day follow a clock hour without a preposition (German <c>5 Uhr nachmittags</c>).</summary>
    internal HashSet<string> Flags { get; } = new(StringComparer.Ordinal);

    /// <summary>The region whose conventions apply when the caller names none (<c>time.region</c>): 0 when the data does not say, which means <see cref="RegionUs"/>.</summary>
    internal int DefaultRegion { get; private set; }

    internal void Add(string section, string[] f)
    {
        switch (section)
        {
            case "time.month":
                Need(f, 2, 2);
                AddForms(f[1], new TWord("month", Int(f[0], 1, 12), 0, string.Empty));
                break;
            case "time.weekday":
                Need(f, 2, 3);
                AddForms(f[1], new TWord("weekday", Int(f[0], 1, 7), 0, string.Empty));
                if (f.Length > 2)
                {
                    AddForms(f[2], new TWord("weekday", Int(f[0], 1, 7), 1, string.Empty));
                }

                break;
            case "time.unit":
                Need(f, 4, 4);
                AddForms(f[1], new TWord("unit", Int(f[2], 0, 366 * 86400), Int(f[3], 0, 12000), f[0]));
                break;
            case "time.season":
                Need(f, 4, 4);
                AddForms(f[1], new TWord("season", Int(f[2], 1, 12), Int(f[3], 1, 12), f[0]));
                break;
            case "time.daypart":
                Need(f, 4, 4);
                AddForms(f[1], new TWord("daypart", Int(f[2], 0, 24), Int(f[3], 1, 24), f[0]));
                break;
            case "time.meridiem":
                Need(f, 2, 2);
                AddForms(f[1], new TWord("meridiem", f[0] switch { "am" => 0, "pm" => 1, _ => throw new RecognizerRulesException($"Unknown meridiem '{f[0]}' (expected am or pm).") }, 0, string.Empty));
                break;
            case "time.word":
                Need(f, 2, 2);
                if (!Roles.Contains(f[0]))
                {
                    throw new RecognizerRulesException($"Unknown word role '{f[0]}'.");
                }

                AddForms(f[1], new TWord(f[0], 0, 0, string.Empty));
                break;
            case "time.recurrence":
                Need(f, 3, 3);
                AddForms(f[2], new TWord("recur", GrainOf(f[1]), 0, f[0]));
                break;
            case "time.zone":
                Need(f, 2, 3);
                AddForms(f[0], new TWord("zone", Int(f[1], -14 * 60, 14 * 60), f.Length > 2 && f[2] == "z" ? 1 : 0, string.Empty));
                break;
            case "time.flag":
                Need(f, 1, 1);
                if (!FlagNames.Contains(f[0]))
                {
                    throw new RecognizerRulesException($"Unknown flag '{f[0]}'.");
                }

                Flags.Add(f[0]);
                break;
            case "time.region":
                Need(f, 1, 1);
                DefaultRegion = Region(f[0]);
                break;
            case "time.holiday":
                Need(f, 3, 3);
                int index = Holidays.Count;
                Holidays.Add(ParseRule(f[1]));
                int mask = 0;
                foreach (string region in f[2].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    mask |= region == "all" ? RegionAll : Region(region);
                }

                if (mask == 0)
                {
                    throw new RecognizerRulesException("A holiday needs a region.");
                }

                AddForms(f[0], new TWord("holiday", index, mask, f[0].Split(',')[0].Trim()));
                break;
            default:
                throw new RecognizerRulesException($"Unknown section '{section}'.");
        }
    }

    /// <summary>The region bit for a region code; throws for one the engine has no holiday or date-order rules for.</summary>
    internal static int Region(string code) => code switch
    {
        "us" => RegionUs,
        "gb" => RegionGb,
        "fr" => RegionFr,
        "de" => RegionDe,
        _ => throw new RecognizerRulesException($"Unknown region '{code}' (expected us, gb, fr, de or all)."),
    };

    /// <summary>The region bit for a caller's region code, or 0 for none or an unknown code.</summary>
    internal static int RegionOrNone(string? code) => code?.ToLowerInvariant() switch
    {
        "us" => RegionUs,
        "gb" => RegionGb,
        "fr" => RegionFr,
        "de" => RegionDe,
        _ => 0,
    };

    private void AddForms(string forms, TWord word)
    {
        foreach (string form in forms.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            Words.Add(form, word);
            string bare = form.Length > 1 && form[0] == '=' ? form[1..] : form;
            var items = new List<Item>();
            Lexer.Lex(bare, items);
            Starts.Add(bare.Substring(items[0].Start, items[0].End - items[0].Start));
        }
    }

    private static HolidayRule ParseRule(string rule)
    {
        string[] p = rule.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        try
        {
            switch (p[0])
            {
                case "fixed" when p.Length == 2:
                {
                    (int m, int d) = MonthDay(p[1]);
                    return new HolidayRule(HolidayKind.Fixed, 0, 0, m, d, 0);
                }

                case "nth" when p.Length is 4 or 5:
                    return new HolidayRule(
                        HolidayKind.Nth, p[1] == "last" ? -1 : Int(p[1], 1, 5), Weekday(p[2]), Int(p[3], 1, 12), 0, p.Length == 5 ? Int(p[4], -30, 30) : 0);
                case "easter" when p.Length == 2:
                    return new HolidayRule(HolidayKind.Easter, 0, 0, 0, 0, Int(p[1], -100, 100));
                case "nearest" when p.Length == 3:
                {
                    (int m, int d) = MonthDay(p[2]);
                    return new HolidayRule(HolidayKind.Nearest, 0, Weekday(p[1]), m, d, 0);
                }
            }
        }
        catch (FormatException)
        {
        }

        throw new RecognizerRulesException($"'{rule}' is not a holiday rule (fixed MM-DD, nth N|last WEEKDAY MONTH [+-DAYS], easter +-DAYS, nearest WEEKDAY MM-DD).");
    }

    private static (int Month, int Day) MonthDay(string s)
    {
        string[] p = s.Split('-');
        if (p.Length != 2)
        {
            throw new FormatException();
        }

        int m = Int(p[0], 1, 12);
        int d = Int(p[1], 1, DateTime.DaysInMonth(2000, m));
        return (m, d);
    }

    private static int Weekday(string s)
    {
        int i = Array.IndexOf(WeekdayCodes, s);
        return i >= 0 ? i + 1 : throw new FormatException();
    }

    private static int GrainOf(string s) => s switch
    {
        "second" => (int)Granularity.Second,
        "minute" => (int)Granularity.Minute,
        "hour" => (int)Granularity.Hour,
        "day" => (int)Granularity.Day,
        "week" => (int)Granularity.Week,
        "month" => (int)Granularity.Month,
        "quarter" => (int)Granularity.Quarter,
        "year" => (int)Granularity.Year,
        _ => throw new RecognizerRulesException($"Unknown unit '{s}'."),
    };

    private static int Int(string s, int min, int max)
    {
        if (!int.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int v) || v < min || v > max)
        {
            throw new RecognizerRulesException($"'{s}' is not a whole number from {min} to {max}.");
        }

        return v;
    }

    private static void Need(string[] f, int min, int max)
    {
        if (f.Length < min || f.Length > max)
        {
            throw new RecognizerRulesException($"Expected {min}{(max > min ? "-" + max : string.Empty)} fields, found {f.Length}.");
        }

        for (int i = 0; i < min; i++)
        {
            if (f[i].Length == 0)
            {
                throw new RecognizerRulesException("An empty field.");
            }
        }
    }
}
