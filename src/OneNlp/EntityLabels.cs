namespace RecursiveAI.OneNlp;

/// <summary>
/// The entity labels the library writes, as constants, for comparing with <see cref="Span.Label"/> without typing the strings.
/// Labels are plain strings, so a model trained on another label set, or a rule, can use any others.
/// </summary>
/// <example>
/// <code>
/// var people = Nlp.Entities(text).Where(e => e.Label == EntityLabels.Person);
/// </code>
/// </example>
public static class EntityLabels
{
    /// <summary>A person (<c>PER</c>), written by the named-entity models.</summary>
    public const string Person = "PER";

    /// <summary>An organization (<c>ORG</c>), written by the named-entity models.</summary>
    public const string Organization = "ORG";

    /// <summary>A location (<c>LOC</c>), written by the named-entity models.</summary>
    public const string Location = "LOC";

    /// <summary>A named entity of another kind (<c>MISC</c>), for models trained on data that has the class.</summary>
    public const string Misc = "MISC";

    /// <summary>A plain number (<c>CARDINAL</c>), from the recognizers.</summary>
    public const string Cardinal = "CARDINAL";

    /// <summary>An ordinal number (<c>ORDINAL</c>), from the recognizers.</summary>
    public const string Ordinal = "ORDINAL";

    /// <summary>A percentage (<c>PERCENT</c>), from the recognizers.</summary>
    public const string Percent = "PERCENT";

    /// <summary>An amount of money (<c>MONEY</c>), from the recognizers.</summary>
    public const string Money = "MONEY";

    /// <summary>A number with a unit (<c>QUANTITY</c>), from the recognizers.</summary>
    public const string Quantity = "QUANTITY";

    /// <summary>An e-mail address (<c>EMAIL</c>), from the recognizers.</summary>
    public const string Email = "EMAIL";

    /// <summary>A URL (<c>URL</c>), from the recognizers.</summary>
    public const string Url = "URL";

    /// <summary>An IP address (<c>IP_ADDRESS</c>), from the recognizers.</summary>
    public const string IpAddress = "IP_ADDRESS";

    /// <summary>Digits that look like a telephone number, unvalidated (<c>PHONE_LIKE</c>), from the recognizers.</summary>
    public const string PhoneLike = "PHONE_LIKE";

    /// <summary>A hashtag (<c>HASHTAG</c>), from the recognizers.</summary>
    public const string Hashtag = "HASHTAG";

    /// <summary>An at-mention (<c>MENTION</c>), from the recognizers.</summary>
    public const string Mention = "MENTION";

    /// <summary>A UUID (label <c>GUID</c>), from the recognizers.</summary>
    public const string Uuid = "GUID";

    /// <summary>A date or a range of days (<c>DATE</c>), from the recognizers.</summary>
    public const string Date = "DATE";

    /// <summary>A time of day or a range of times (<c>TIME</c>), from the recognizers.</summary>
    public const string Time = "TIME";

    /// <summary>A day with a time of day, or a range between date-times (<c>DATETIME</c>), from the recognizers.</summary>
    public const string DateTime = "DATETIME";

    /// <summary>An amount of time (<c>DURATION</c>), from the recognizers.</summary>
    public const string Duration = "DURATION";

    /// <summary>A recurrence (<c>SET</c>), from the recognizers.</summary>
    public const string Set = "SET";
}
