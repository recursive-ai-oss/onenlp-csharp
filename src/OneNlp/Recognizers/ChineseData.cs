using System.Globalization;

namespace RecursiveAI.OneNlp.Recognizers;

/// <summary>Forms looked up by the longest match at a position of a text. Chinese has no spaces, so nothing else can say where a word ends.</summary>
internal sealed class LongestForms<T>
{
    private readonly Dictionary<string, T> _forms = new(StringComparer.Ordinal);
    private Dictionary<string, T>.AlternateLookup<ReadOnlySpan<char>> _lookup;
    private bool _ready;

    /// <summary>The length of the longest form.</summary>
    internal int MaxLength { get; private set; }

    internal bool IsEmpty => _forms.Count == 0;

    internal void Add(string form, T value)
    {
        if (form.Length == 0 || form.Length > 32)
        {
            throw new RecognizerRulesException($"The form '{form}' must have 1 to 32 characters.");
        }

        _forms[form] = value;
        MaxLength = Math.Max(MaxLength, form.Length);
        _ready = false;
    }

    /// <summary>The longest form that starts at <paramref name="pos"/> in <paramref name="text"/>.</summary>
    internal bool Match(string text, int pos, out int length, out T value)
    {
        if (!_ready)
        {
            _lookup = _forms.GetAlternateLookup<ReadOnlySpan<char>>();
            _ready = true;
        }

        for (int len = Math.Min(MaxLength, text.Length - pos); len >= 1; len--)
        {
            if (_lookup.TryGetValue(text.AsSpan(pos, len), out value!))
            {
                length = len;
                return true;
            }
        }

        length = 0;
        value = default!;
        return false;
    }

    /// <summary>Every form that starts at <paramref name="pos"/>, longest first.</summary>
    internal List<(int Length, T Value)> MatchAll(string text, int pos)
    {
        var all = new List<(int, T)>();
        if (!_ready)
        {
            _lookup = _forms.GetAlternateLookup<ReadOnlySpan<char>>();
            _ready = true;
        }

        for (int len = Math.Min(MaxLength, text.Length - pos); len >= 1; len--)
        {
            if (_lookup.TryGetValue(text.AsSpan(pos, len), out T? value))
            {
                all.Add((len, value));
            }
        }

        return all;
    }
}

/// <summary>A unit of a duration: its length in seconds or months and the unit a <see cref="TemporalMatch"/> counts it in.</summary>
internal sealed record ZhUnit(string Name, int Seconds, int Months, DurationUnit Unit);

/// <summary>A word meaning a day relative to the reference day (today, tomorrow), with the part of the day it implies (tonight).</summary>
internal sealed record ZhRelDay(int Offset, string? DayPart);

/// <summary>A relative period word (this year, next month, last week).</summary>
internal sealed record ZhRelPeriod(string Unit, int Offset);

/// <summary>A part of the day: its hours and how an hour written with it is read on a 24-hour clock.</summary>
internal sealed record ZhDayPart(string Name, int Start, int End, string Shift);

/// <summary>A season: its first month and its length in months.</summary>
internal sealed record ZhSeason(string Name, int FirstMonth, int Months);

/// <summary>A holiday: a computed rule, or a lunar-calendar date from a pinned table plus or minus some days.</summary>
internal sealed record ZhHoliday(string Name, HolidayRule Rule, string? Lunar, int Plus);

/// <summary>A prefix word that turns a following number into a measure of a unit (<c>摄氏</c> 180 <c>度</c>), possibly negating it (<c>零下</c>).</summary>
internal sealed record ZhUnitPrefix(string Unit, string Dimension, string[] Tails, bool Negate);

/// <summary>
/// The vocabulary of Chinese numbers and dates, read from the <c>zh.*</c> sections of rule data. The grammar that uses it (numerals built
/// from digit, ten/hundred/thousand and ten-thousand/hundred-million characters; dates written year-month-day) is code in
/// <c>Scan.Chinese*.cs</c>; the characters and words are data.
/// </summary>
internal sealed class ChineseData
{
    internal Dictionary<char, int> Digits { get; } = [];

    internal Dictionary<char, int> Small { get; } = [];

    internal Dictionary<char, long> Big { get; } = [];

    /// <summary>Role words by role (point, negative, ordinal, percent, fraction, classifier, ...).</summary>
    internal Dictionary<string, LongestForms<bool>> Words { get; } = new(StringComparer.Ordinal);

    internal LongestForms<ZhRelDay> RelDays { get; } = new();

    internal LongestForms<ZhRelPeriod> RelPeriods { get; } = new();

    internal LongestForms<int> Weekdays { get; } = new();

    internal LongestForms<ZhSeason> Seasons { get; } = new();

    internal LongestForms<ZhDayPart> DayParts { get; } = new();

    internal LongestForms<ZhUnit> Units { get; } = new();

    internal LongestForms<ZhHoliday> Holidays { get; } = new();

    internal LongestForms<(int Minutes, bool Z)> Zones { get; } = new();

    internal LongestForms<List<ZhUnitPrefix>> UnitPrefixes { get; } = new();

    private readonly Dictionary<string, List<ZhUnitPrefix>> _prefixRows = new(StringComparer.Ordinal);

    internal Dictionary<string, ZhDayPart> PartsByName { get; } = new(StringComparer.Ordinal);

    /// <summary>Lunar-calendar holiday dates by name, then by year.</summary>
    internal Dictionary<string, Dictionary<int, DateTime>> Lunar { get; } = new(StringComparer.Ordinal);

    internal bool IsEmpty => Digits.Count == 0;

    internal bool Role(string role, string text, int pos, out int length) =>
        Words.TryGetValue(role, out LongestForms<bool>? forms) ? forms.Match(text, pos, out length, out _) : Fail(out length);

    private static bool Fail(out int length)
    {
        length = 0;
        return false;
    }

    internal void Add(string section, string[] f)
    {
        switch (section)
        {
            case "zh.digit":
                Need(f, 2);
                foreach (string form in Forms(f[0]))
                {
                    Digits[One(form)] = Int(f[1], 0, 9);
                }

                break;
            case "zh.small":
                Need(f, 2);
                foreach (string form in Forms(f[0]))
                {
                    Small[One(form)] = Int(f[1], 10, 1000);
                }

                break;
            case "zh.big":
                Need(f, 2);
                foreach (string form in Forms(f[0]))
                {
                    Big[One(form)] = Int(f[1], 10000, 100_000_000);
                }

                break;
            case "zh.word":
                Need(f, 2);
                if (!Words.TryGetValue(f[0], out LongestForms<bool>? words))
                {
                    Words[f[0]] = words = new LongestForms<bool>();
                }

                foreach (string form in Forms(f[1]))
                {
                    words.Add(form, true);
                }

                break;
            case "zh.relday":
                Need(f, 2, 3);
                foreach (string form in Forms(f[0]))
                {
                    RelDays.Add(form, new ZhRelDay(Int(f[1], -400, 400), f.Length > 2 && f[2].Length > 0 ? f[2] : null));
                }

                break;
            case "zh.relperiod":
                Need(f, 3);
                if (f[1] is not ("year" or "month" or "week" or "quarter" or "weekday"))
                {
                    throw new RecognizerRulesException($"Unknown period unit '{f[1]}' (expected year, month, week, quarter or weekday).");
                }

                foreach (string form in Forms(f[0]))
                {
                    RelPeriods.Add(form, new ZhRelPeriod(f[1], Int(f[2], -10, 10)));
                }

                break;
            case "zh.weekday":
                Need(f, 2);
                foreach (string form in Forms(f[1]))
                {
                    Weekdays.Add(form, Int(f[0], 1, 7));
                }

                break;
            case "zh.season":
                Need(f, 4);
                foreach (string form in Forms(f[1]))
                {
                    Seasons.Add(form, new ZhSeason(f[0], Int(f[2], 1, 12), Int(f[3], 1, 12)));
                }

                break;
            case "zh.daypart":
                Need(f, 5);
                if (f[4] is not ("am" or "pm" or "noon" or "night" or "evening"))
                {
                    throw new RecognizerRulesException($"Unknown shift '{f[4]}' (expected am, pm, noon, night or evening).");
                }

                foreach (string form in Forms(f[1]))
                {
                    var part = new ZhDayPart(f[0], Int(f[2], 0, 23), Int(f[3], 1, 24), f[4]);
                    DayParts.Add(form, part);
                    PartsByName[f[0]] = part;
                }

                break;
            case "zh.unit":
                Need(f, 5);
                if (!Enum.TryParse(f[4], true, out DurationUnit du))
                {
                    throw new RecognizerRulesException($"Unknown duration unit '{f[4]}'.");
                }

                foreach (string form in Forms(f[1]))
                {
                    Units.Add(form, new ZhUnit(f[0], Int(f[2], 0, 100_000_000), Int(f[3], 0, 12000), du));
                }

                break;
            case "zh.holiday":
                Need(f, 2);
                foreach (string form in Forms(f[0]))
                {
                    Holidays.Add(form, HolidayOf(form, f[1]));
                }

                break;
            case "zh.lunar":
                Need(f, 2);
                if (!Lunar.TryGetValue(f[0], out Dictionary<int, DateTime>? years))
                {
                    Lunar[f[0]] = years = [];
                }

                foreach (string item in Forms(f[1]))
                {
                    if (!DateTime.TryParseExact(item, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime d))
                    {
                        throw new RecognizerRulesException($"'{item}' is not a date (yyyy-MM-dd).");
                    }

                    years[d.Year] = d;
                }

                break;
            case "zh.zone":
                Need(f, 2, 3);
                foreach (string form in Forms(f[0]))
                {
                    Zones.Add(form, (Int(f[1], -840, 840), f.Length > 2 && f[2] == "z"));
                }

                break;
            case "zh.unit-prefix":
                Need(f, 4, 5);
                foreach (string form in Forms(f[2]))
                {
                    if (!_prefixRows.TryGetValue(form, out List<ZhUnitPrefix>? rows))
                    {
                        _prefixRows[form] = rows = [];
                        UnitPrefixes.Add(form, rows);
                    }

                    rows.Add(new ZhUnitPrefix(f[0], f[1], [.. Forms(f[3])], f.Length > 4 && f[4] == "negate"));
                }

                break;
            default:
                throw new RecognizerRulesException($"Unknown section '{section}'.");
        }
    }

    private static ZhHoliday HolidayOf(string name, string rule)
    {
        string[] p = rule.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (p.Length >= 2 && p[0] == "lunar")
        {
            int plus = p.Length > 2 ? Int(p[2], -30, 30) : 0;
            return new ZhHoliday(name, default, p[1], plus);
        }

        return new ZhHoliday(name, TemporalData.ParseRule(rule), null, 0);
    }

    /// <summary>The date of a holiday in a year, or null when the year is not covered (a lunar holiday outside the pinned table).</summary>
    internal DateTime? DateOf(ZhHoliday h, int year)
    {
        if (h.Lunar is null)
        {
            return CivilCalendar.Holiday(h.Rule, year);
        }

        return Lunar.TryGetValue(h.Lunar, out Dictionary<int, DateTime>? years) && years.TryGetValue(year, out DateTime d) ? d.AddDays(h.Plus) : null;
    }

    private static string[] Forms(string s)
    {
        string[] forms = s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return forms.Length > 0 ? forms : throw new RecognizerRulesException("A list of forms is empty.");
    }

    private static char One(string s) =>
        s.Length == 1 ? s[0] : throw new RecognizerRulesException($"'{s}' must be one character.");

    private static void Need(string[] f, int min, int max = int.MaxValue)
    {
        if (f.Length < min || f.Length > max)
        {
            throw new RecognizerRulesException($"Expected {min}{(max > min && max < int.MaxValue ? "-" + max : string.Empty)} fields, found {f.Length}.");
        }

        for (int i = 0; i < min; i++)
        {
            if (f[i].Length == 0)
            {
                throw new RecognizerRulesException("An empty field.");
            }
        }
    }

    private static int Int(string s, int min, int max) =>
        int.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int v) && v >= min && v <= max
            ? v
            : throw new RecognizerRulesException($"'{s}' is not a whole number from {min} to {max}.");
}
