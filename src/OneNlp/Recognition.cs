namespace RecursiveAI.OneNlp;

/// <summary>
/// One thing a recognizer found in a text: a number, a measure, an amount of money, an e-mail address, and so on. This is the base of
/// every recognizer result; the kind-specific data sits in the derived types (<see cref="Quantity"/>, <see cref="SequenceMatch"/>).
/// </summary>
/// <param name="Start">Offset of the first character in the input string, in UTF-16 code units.</param>
/// <param name="End">Offset just past the last character, in UTF-16 code units. Always greater than <paramref name="Start"/>.</param>
/// <param name="Text">The matched characters, exactly as they appear in the input.</param>
/// <param name="Label">The entity label this result takes when written into a <see cref="Doc"/> (<c>CARDINAL</c>, <c>MONEY</c>, <c>EMAIL</c>, ...).</param>
/// <param name="Confidence">From 0 to 1. Rule results are 1 unless the rule is ambiguous by nature (a <c>$</c> sign may be any dollar; a phone-like digit group is only a guess).</param>
public abstract record Recognition(int Start, int End, string Text, string Label, float Confidence)
{
    /// <summary>The number of characters matched.</summary>
    public int Length => End - Start;
}

/// <summary>What kind of value a <see cref="Quantity"/> holds. Each kind has one entity label.</summary>
public enum QuantityKind
{
    /// <summary>A plain number: <c>42</c>, <c>3.5 million</c>, <c>two hundred and five</c>, <c>a dozen</c>, <c>3/4</c>. Label <c>CARDINAL</c>.</summary>
    Cardinal = 0,

    /// <summary>An ordinal: <c>1st</c>, <c>twenty-third</c>. The value is the position. Label <c>ORDINAL</c>.</summary>
    Ordinal = 1,

    /// <summary>A percentage: <c>5%</c>, <c>fifty percent</c>. The value is in percent (5, not 0.05). Label <c>PERCENT</c>.</summary>
    Percent = 2,

    /// <summary>An amount of money: <c>$5m</c>, <c>five bucks</c>, <c>USD 20</c>. <see cref="Quantity.Currency"/> is set. Label <c>MONEY</c>.</summary>
    Money = 3,

    /// <summary>A number with a unit: <c>5 km</c>, <c>20°C</c>, <c>3 years old</c>. <see cref="Quantity.Unit"/> and <see cref="Quantity.Dimension"/> are set. Label <c>QUANTITY</c>.</summary>
    Measure = 4,
}

/// <summary>A resolved number, possibly with a unit or a currency.</summary>
/// <param name="Start">Offset of the first character in the input, in UTF-16 code units.</param>
/// <param name="End">Offset just past the last character.</param>
/// <param name="Text">The matched characters.</param>
/// <param name="Label">The entity label: <c>CARDINAL</c>, <c>ORDINAL</c>, <c>PERCENT</c>, <c>MONEY</c> or <c>QUANTITY</c>.</param>
/// <param name="Confidence">From 0 to 1; see <see cref="Recognition"/>.</param>
/// <param name="Kind">What the value is.</param>
/// <param name="Value">
/// The value as a <see cref="decimal"/>, so <c>19.99</c> is exactly that. The value is in the unit as written, not converted: <c>5 km</c> is 5, and
/// <c>50 cents</c> is 0.5 (in <see cref="Currency"/>). Numbers beyond ±7.9e28 do not fit and are not reported.
/// </param>
/// <param name="Unit">The canonical unit name (<c>kilometer</c>, <c>celsius</c>, <c>year</c>, <c>percent</c>), or <see langword="null"/> for plain numbers, ordinals and money.</param>
/// <param name="Dimension">What the unit measures (<c>length</c>, <c>mass</c>, <c>volume</c>, <c>area</c>, <c>speed</c>, <c>information</c>, <c>temperature</c>, <c>age</c>, <c>percent</c>, <c>currency</c>), or <see langword="null"/> for plain numbers and ordinals.</param>
/// <param name="Currency">The ISO 4217 alphabetic code (<c>USD</c>), or <see langword="null"/> unless <paramref name="Kind"/> is <see cref="QuantityKind.Money"/>.</param>
public sealed record Quantity(
    int Start, int End, string Text, string Label, float Confidence,
    QuantityKind Kind, decimal Value, string? Unit, string? Dimension, string? Currency)
    : Recognition(Start, End, Text, Label, Confidence);

/// <summary>What a <see cref="SequenceMatch"/> is.</summary>
public enum SequenceKind
{
    /// <summary>An e-mail address (a simplified RFC 5322 addr-spec). Label <c>EMAIL</c>.</summary>
    Email = 0,

    /// <summary>A URL (RFC 3986 / WHATWG URL shapes). Label <c>URL</c>.</summary>
    Url = 1,

    /// <summary>An IPv4 (RFC 791) or IPv6 (RFC 4291) address. Label <c>IP_ADDRESS</c>.</summary>
    IpAddress = 2,

    /// <summary>
    /// Digits that look like a telephone number, judged by syntax only and unvalidated. Label <c>PHONE_LIKE</c>. Use a dedicated
    /// contact-parsing library for validated, canonical phone numbers.
    /// </summary>
    PhoneLike = 3,

    /// <summary>A hashtag such as <c>#nlp</c>. Label <c>HASHTAG</c>.</summary>
    Hashtag = 4,

    /// <summary>An at-mention such as <c>@alice</c>. Label <c>MENTION</c>.</summary>
    Mention = 5,

    /// <summary>A UUID in its RFC 9562 text form. Label <c>GUID</c>.</summary>
    Uuid = 6,
}

/// <summary>A syntactic pattern found in a text (an address, a URL, an identifier). Nothing about it is verified: an address is not checked to exist.</summary>
/// <param name="Start">Offset of the first character in the input, in UTF-16 code units.</param>
/// <param name="End">Offset just past the last character.</param>
/// <param name="Text">The matched characters.</param>
/// <param name="Label">The entity label (see <see cref="SequenceKind"/>).</param>
/// <param name="Confidence">From 0 to 1; see <see cref="Recognition"/>.</param>
/// <param name="Kind">What was found.</param>
/// <param name="Normalized">
/// A normal form of the match: e-mail with a lower-case domain; the URL as written; an IP address in its canonical text form (RFC 5952
/// for IPv6); a phone-like sequence reduced to an optional <c>+</c> and its digits; a hashtag or mention without its sigil, lower-cased; a
/// GUID as 36 lower-case characters.
/// </param>
public sealed record SequenceMatch(
    int Start, int End, string Text, string Label, float Confidence,
    SequenceKind Kind, string Normalized)
    : Recognition(Start, End, Text, Label, Confidence);

/// <summary>What a <see cref="TemporalMatch"/> is. The names are the types of DateTimeV2-style resolution.</summary>
public enum TemporalKind
{
    /// <summary>One calendar day: <c>March 5, 2026</c>, <c>tomorrow</c>, <c>Easter 2027</c>. Label <c>DATE</c>.</summary>
    Date = 0,

    /// <summary>A time of day with no date: <c>3pm</c>, <c>15:45</c>. Label <c>TIME</c>.</summary>
    Time = 1,

    /// <summary>A day and a time of day: <c>tomorrow at 3pm</c>, <c>in 45 minutes</c>. Label <c>DATETIME</c>.</summary>
    DateTime = 2,

    /// <summary>A named period or an explicit range of days: <c>next week</c>, <c>spring 2021</c>, <c>from April 6 to April 10</c>. Label <c>DATE</c>.</summary>
    DateRange = 3,

    /// <summary>A range of times of day: <c>from 3pm to 5pm</c>. Label <c>TIME</c>.</summary>
    TimeRange = 4,

    /// <summary>A range between two date-times, or a part of a day: <c>tomorrow morning</c>, <c>Monday from 9am to 11am</c>. Label <c>DATETIME</c>.</summary>
    DateTimeRange = 5,

    /// <summary>An amount of time: <c>two weeks</c>, <c>1 year and 6 months</c>. Label <c>DURATION</c>.</summary>
    Duration = 6,

    /// <summary>A recurrence: <c>every Monday at 9am</c>, <c>daily</c>, <c>every 3 hours</c>. Label <c>SET</c>.</summary>
    Set = 7,
}

/// <summary>The unit a temporal expression is stated in. For an instant it is the precision; for a range, the kind of period; for a set, the unit of recurrence.</summary>
public enum Granularity
{
    /// <summary>Seconds.</summary>
    Second = 0,

    /// <summary>Minutes.</summary>
    Minute = 1,

    /// <summary>Hours.</summary>
    Hour = 2,

    /// <summary>Days.</summary>
    Day = 3,

    /// <summary>Weeks (Monday to Sunday).</summary>
    Week = 4,

    /// <summary>Calendar months.</summary>
    Month = 5,

    /// <summary>Calendar quarters.</summary>
    Quarter = 6,

    /// <summary>Halves of a calendar year.</summary>
    Half = 7,

    /// <summary>Seasons (meteorological, northern hemisphere, by default).</summary>
    Season = 8,

    /// <summary>Calendar years.</summary>
    Year = 9,

    /// <summary>Decades.</summary>
    Decade = 10,
}

/// <summary>
/// A resolved stretch of time: <c>Start</c> inclusive, <c>End</c> exclusive. A day is from its midnight to the next; an instant such as
/// <c>3pm</c> runs for its precision (an hour for <c>3pm</c>, a minute for <c>3:05pm</c>).
/// </summary>
/// <param name="Start">The first moment. Its offset is the zone the expression names, else the offset of the reference date.</param>
/// <param name="End">The first moment after the range.</param>
/// <param name="Granularity">The unit the expression is stated in.</param>
/// <param name="Approximate">True for loose expressions: seasons, early/mid/late, parts of a day, <c>around 3pm</c>.</param>
public readonly record struct DateRange(DateTimeOffset Start, DateTimeOffset End, Granularity Granularity, bool Approximate);

/// <summary>The unit a duration is counted in (see <see cref="TemporalMatch.Unit"/>).</summary>
public enum DurationUnit
{
    /// <summary>Seconds.</summary>
    Second = 0,

    /// <summary>Minutes.</summary>
    Minute = 1,

    /// <summary>Hours.</summary>
    Hour = 2,

    /// <summary>Calendar days of 24 hours (<c>3 days</c>, <c>10 calendar days</c>).</summary>
    Day = 3,

    /// <summary>Weeks of seven days; a fortnight is two.</summary>
    Week = 4,

    /// <summary>Calendar months; a quarter is three.</summary>
    Month = 5,

    /// <summary>Calendar years; a decade is ten.</summary>
    Year = 6,

    /// <summary>Business days (<c>ten business days</c>, <c>5 working days</c>): days that are not Saturdays or Sundays. Public holidays are not considered.</summary>
    BusinessDay = 7,

    /// <summary>Business hours (<c>8 business hours</c>): hours inside working time. The length of working time is not assumed.</summary>
    BusinessHour = 8,
}

/// <summary>A resolved date, time, range, duration or recurrence.</summary>
/// <param name="Start">Offset of the first character in the input, in UTF-16 code units.</param>
/// <param name="End">Offset just past the last character.</param>
/// <param name="Text">The matched characters.</param>
/// <param name="Label">The entity label: <c>DATE</c>, <c>TIME</c>, <c>DATETIME</c>, <c>DURATION</c> or <c>SET</c> (see <see cref="TemporalKind"/>).</param>
/// <param name="Confidence">From 0 to 1; see <see cref="Recognition"/>.</param>
/// <param name="Kind">What was found.</param>
/// <param name="Value">
/// The resolved value as text. Dates are <c>YYYY-MM-DD</c>; times <c>Thh:mm[:ss]</c>; date-times <c>YYYY-MM-DDThh:mm[:ss]</c>, each with a zone
/// (<c>Z</c> or <c>+hh:mm</c>) when the text names one; ranges <c>start/end</c> with the end exclusive for days and longer periods;
/// durations whole seconds, or ISO 8601 <c>PnYnM...</c> when they name years or months; sets TIMEX3-style (<c>P1D</c>, <c>XXXX-WXX-1T09:00</c>).
/// </param>
/// <param name="Granularity">The unit the expression is stated in; <see langword="null"/> for durations.</param>
/// <param name="Approximate">True for loose expressions (seasons, early/mid/late, parts of a day, <c>around</c>).</param>
/// <param name="UtcOffset">The offset the text names (<c>PST</c>, <c>UTC+2</c>, a trailing <c>Z</c>), else <see langword="null"/>.</param>
/// <param name="Range">
/// The stretch of time on the calendar, in the zone named by the text or else the zone of the reference date. <see langword="null"/> for durations,
/// sets, and times of day when no reference date was given (a time of day is placed on the reference date).
/// </param>
/// <param name="Amount">The length of a duration that has a fixed length in seconds; <see langword="null"/> when it names months or years, and for the other kinds.</param>
public sealed record TemporalMatch(
    int Start, int End, string Text, string Label, float Confidence,
    TemporalKind Kind, string Value, Granularity? Granularity, bool Approximate, TimeSpan? UtcOffset, DateRange? Range, TimeSpan? Amount)
    : Recognition(Start, End, Text, Label, Confidence)
{
    /// <summary>
    /// For a <see cref="TemporalKind.Duration"/>, the unit <see cref="Count"/> is in; <see langword="null"/> for the other kinds and for a duration
    /// that mixes units that cannot be added (<c>1 year and 3 days</c>). A duration of one term keeps its unit: <c>60 days</c> is 60 days,
    /// <c>18 months</c> is 18 months, <c>ten business days</c> is 10 business days. A duration of several terms is counted in its smallest
    /// unit when the terms are all clock and day units or all month and year units: <c>2 hours and 30 minutes</c> is 150 minutes,
    /// <c>1 year and 6 months</c> is 18 months.
    /// </summary>
    public DurationUnit? Unit { get; init; }

    /// <summary>For a duration, how many <see cref="Unit"/> it is long, as written (<c>1.5 hours</c> is 1.5); <see langword="null"/> when <see cref="Unit"/> is.</summary>
    public decimal? Count { get; init; }
}

/// <summary>Per-call inputs to a recognizer beyond the text.</summary>
/// <param name="ReferenceDate">
/// The moment relative expressions are resolved against ("tomorrow", "next Friday", "Christmas"). Wall-clock expressions are read in the offset of
/// this value. <see langword="null"/> means none was given: then only expressions that stand alone are reported (<c>March 5, 2026</c>, <c>3pm</c>,
/// <c>two weeks</c>, <c>every Monday</c>) and the relative ones are left out. The number, unit, currency and sequence rules do not use it.
/// </param>
public readonly record struct RecognitionContext(DateTimeOffset? ReferenceDate = null)
{
    /// <summary>
    /// The region whose conventions apply to holidays and numeric dates: <c>us</c> (the default; <c>04/05/2026</c> is April 5) or <c>gb</c>
    /// (<c>04/05/2026</c> is 4 May; Boxing Day, bank holidays). For Spanish, <c>es</c> (default) or <c>mx</c> (Mexico: <c>1,234.56</c> with a decimal point,
    /// and the Mexican holidays); for Portuguese, <c>br</c> (default) or <c>pt</c> (Portugal holidays). Case-insensitive; an unknown value is read as the
    /// language's own default.
    /// </summary>
    public string? Region { get; init; }
}
