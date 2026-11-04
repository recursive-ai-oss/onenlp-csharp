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

/// <summary>Per-call inputs to a recognizer beyond the text.</summary>
/// <param name="ReferenceDate">
/// The moment relative expressions are resolved against ("tomorrow"). The number, unit, currency and sequence rules do not use it; it is
/// part of the context so every recognizer takes the same call. <see langword="null"/> means none was given.
/// </param>
public readonly record struct RecognitionContext(DateTimeOffset? ReferenceDate = null);
