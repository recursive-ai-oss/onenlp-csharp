using System.Text.RegularExpressions;

namespace RecursiveAI.OneNlp.Matching;

/// <summary>How many tokens one <see cref="TokenSpec"/> matches (spaCy's <c>OP</c> values).</summary>
/// <param name="Min">The fewest tokens; 0 makes the spec optional.</param>
/// <param name="Max">The most tokens, or -1 for no limit.</param>
/// <param name="Negated">Whether the spec is spaCy's <c>!</c>: it matches one token that does <em>not</em> satisfy the constraints.</param>
public readonly record struct Quantifier(int Min, int Max, bool Negated = false)
{
    /// <summary>Exactly one token (the default, <c>OP</c> absent or empty).</summary>
    public static Quantifier One => new(1, 1);

    /// <summary>The largest <c>{n,m}</c> bound a pattern may use.</summary>
    public const int MaxBound = 1000;

    /// <summary>The operator as written in a pattern file: <c>!</c>, <c>?</c>, <c>+</c>, <c>*</c>, <c>{n}</c>, <c>{n,m}</c>, <c>{n,}</c> or <c>{,m}</c>; empty for exactly one.</summary>
    public override string ToString() => this switch
    {
        { Negated: true } => "!",
        { Min: 1, Max: 1 } => string.Empty,
        { Min: 0, Max: 1 } => "?",
        { Min: 1, Max: -1 } => "+",
        { Min: 0, Max: -1 } => "*",
        { Min: var lo, Max: var hi } when lo == hi => "{" + lo + "}",
        { Min: 0, Max: var hi } => "{," + hi + "}",
        { Min: var lo, Max: -1 } => "{" + lo + ",}",
        { Min: var lo, Max: var hi } => "{" + lo + "," + hi + "}",
    };

    /// <summary>Parses an <c>OP</c> value.</summary>
    /// <param name="op">The operator text; see <see cref="ToString"/>.</param>
    /// <param name="quantifier">The result.</param>
    /// <returns><see langword="false"/> when the text is not an operator or a bound exceeds <see cref="MaxBound"/> or is reversed.</returns>
    public static bool TryParse(string op, out Quantifier quantifier)
    {
        quantifier = One;
        switch (op)
        {
            case "":
                return true;
            case "!":
                quantifier = new Quantifier(1, 1, true);
                return true;
            case "?":
                quantifier = new Quantifier(0, 1);
                return true;
            case "+":
                quantifier = new Quantifier(1, -1);
                return true;
            case "*":
                quantifier = new Quantifier(0, -1);
                return true;
        }

        if (op.Length < 3 || op[0] != '{' || op[^1] != '}')
        {
            return false;
        }

        string body = op[1..^1];
        int comma = body.IndexOf(',', StringComparison.Ordinal);
        string loText = comma < 0 ? body : body[..comma];
        string hiText = comma < 0 ? body : body[(comma + 1)..];
        int lo = 0;
        int hi = -1;
        if (loText.Length > 0 && !TryBound(loText, out lo))
        {
            return false;
        }

        if (hiText.Length > 0 && !TryBound(hiText, out hi))
        {
            return false;
        }

        if (comma >= 0 && hiText.Length == 0 && loText.Length == 0)
        {
            return false;
        }

        if (hi != -1 && (hi < lo || hi < 1))
        {
            return false;
        }

        quantifier = new Quantifier(lo, hi);
        return true;
    }

    private static bool TryBound(string text, out int value)
    {
        value = 0;
        foreach (char c in text)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }
        }

        return text.Length <= 4 && int.TryParse(text, out value) && value <= MaxBound;
    }
}

internal enum Test
{
    Same,
    In,
    NotIn,
    Regex,
    IsSubset,
    IsSuperset,
    Intersects,
    Compare,
}

/// <summary>One attribute test inside a <see cref="TokenSpec"/>. Immutable once built.</summary>
internal sealed class Constraint
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    internal Constraint(TokenProperty attribute, Test test)
    {
        Attribute = attribute;
        Test = test;
    }

    internal TokenProperty Attribute { get; }

    internal Test Test { get; }

    internal string? Text { get; init; }

    internal string[]? Texts { get; init; }

    internal int Number { get; init; }

    internal int[]? Numbers { get; init; }

    internal bool Flag { get; init; }

    internal Comparison Comparison { get; init; }

    internal Regex? Expression { get; init; }

    internal HashSet<string>? TextSet { get; init; }

    internal static Regex CreateRegex(string pattern)
    {
        // The linear-time engine cannot run look-around or back-references; those patterns fall back to the backtracking engine,
        // whose run time the timeout bounds. A timed-out test counts as no match.
        try
        {
            return new Regex(pattern, RegexOptions.NonBacktracking | RegexOptions.CultureInvariant, RegexTimeout);
        }
        catch (NotSupportedException)
        {
            return new Regex(pattern, RegexOptions.CultureInvariant, RegexTimeout);
        }
    }
}

/// <summary>
/// The test one pattern position applies to a token: a set of attribute constraints that must all hold, and how many tokens the
/// position covers. Build one with the fluent methods, then pass it to <see cref="Pattern"/>.
/// </summary>
/// <remarks>
/// An empty spec matches any token. A spec is a builder: <see cref="Pattern"/> copies it, so changing it afterwards does not
/// change patterns already built. A spec is not thread-safe while being built.
/// </remarks>
public sealed class TokenSpec
{
    private readonly List<Constraint> _constraints = [];

    /// <summary>How many tokens the position matches; exactly one unless set.</summary>
    public Quantifier Quantifier { get; private set; } = Quantifier.One;

    internal IReadOnlyList<Constraint> Constraints => _constraints;

    /// <summary>Requires a string attribute to equal a value.</summary>
    /// <param name="attribute">A string attribute: ORTH, NORM, LOWER, LEMMA, POS, TAG, DEP, MORPH, ENT_TYPE, ENT_IOB or SHAPE.</param>
    /// <param name="value">The value, compared as written (case-sensitive). For <see cref="TokenProperty.Morph"/> it is the whole feature string.</param>
    /// <exception cref="ArgumentException">The attribute is not a string attribute, or an ENT_IOB value is not B, I, O or empty.</exception>
    /// <returns>This spec.</returns>
    public TokenSpec Is(TokenProperty attribute, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireString(attribute);
        CheckIob(attribute, value);
        _constraints.Add(new Constraint(attribute, Test.Same) { Text = value });
        return this;
    }

    /// <summary>Requires a yes/no attribute (IS_ALPHA, LIKE_NUM, SENT_START and the others) to be true or false.</summary>
    /// <param name="attribute">A flag attribute.</param>
    /// <param name="value">The required value.</param>
    /// <exception cref="ArgumentException">The attribute is not a flag.</exception>
    /// <returns>This spec.</returns>
    public TokenSpec Is(TokenProperty attribute, bool value)
    {
        if (!TokenProperties.IsFlag(attribute))
        {
            throw new ArgumentException($"{TokenProperties.NameOf(attribute)} is not a yes/no attribute.", nameof(attribute));
        }

        _constraints.Add(new Constraint(attribute, Test.Same) { Flag = value });
        return this;
    }

    /// <summary>Requires the length to equal a number, or an ENT_IOB to equal its numeric code (1 I, 2 O, 3 B, 0 unset).</summary>
    /// <param name="attribute"><see cref="TokenProperty.Length"/> or <see cref="TokenProperty.EntIob"/>.</param>
    /// <param name="value">The number.</param>
    /// <exception cref="ArgumentException">The attribute takes no number, or an ENT_IOB code is outside 0 to 3.</exception>
    /// <returns>This spec.</returns>
    public TokenSpec Is(TokenProperty attribute, int value)
    {
        if (attribute == TokenProperty.EntIob)
        {
            return Is(attribute, IobText(value));
        }

        return Compare(attribute, Comparison.Equal, value);
    }

    /// <summary>Requires the token's length (in code points) to compare to a number.</summary>
    /// <param name="attribute">Only <see cref="TokenProperty.Length"/> takes comparisons.</param>
    /// <param name="comparison">The comparison.</param>
    /// <param name="value">The number to compare with.</param>
    /// <exception cref="ArgumentException">The attribute is not <see cref="TokenProperty.Length"/>.</exception>
    /// <returns>This spec.</returns>
    public TokenSpec Compare(TokenProperty attribute, Comparison comparison, int value)
    {
        RequireLength(attribute);
        _constraints.Add(new Constraint(attribute, Test.Compare) { Comparison = comparison, Number = value });
        return this;
    }

    /// <summary>Requires a string attribute to equal one of the values.</summary>
    /// <param name="attribute">A string attribute.</param>
    /// <param name="values">The accepted values; an empty list accepts nothing.</param>
    /// <exception cref="ArgumentException">The attribute is not a string attribute.</exception>
    /// <returns>This spec.</returns>
    public TokenSpec In(TokenProperty attribute, params string[] values) => AddSet(attribute, Test.In, values);

    /// <summary>Requires a string attribute to equal none of the values.</summary>
    /// <param name="attribute">A string attribute.</param>
    /// <param name="values">The rejected values.</param>
    /// <exception cref="ArgumentException">The attribute is not a string attribute.</exception>
    /// <returns>This spec.</returns>
    public TokenSpec NotIn(TokenProperty attribute, params string[] values) => AddSet(attribute, Test.NotIn, values);

    /// <summary>Requires the length to equal one of the numbers.</summary>
    /// <param name="attribute">Only <see cref="TokenProperty.Length"/>.</param>
    /// <param name="values">The accepted numbers.</param>
    /// <exception cref="ArgumentException">The attribute is not <see cref="TokenProperty.Length"/>.</exception>
    /// <returns>This spec.</returns>
    public TokenSpec In(TokenProperty attribute, params int[] values)
    {
        RequireLength(attribute);
        ArgumentNullException.ThrowIfNull(values);
        _constraints.Add(new Constraint(attribute, Test.In) { Numbers = [.. values] });
        return this;
    }

    /// <summary>Requires the length to equal none of the numbers.</summary>
    /// <param name="attribute">Only <see cref="TokenProperty.Length"/>.</param>
    /// <param name="values">The rejected numbers.</param>
    /// <exception cref="ArgumentException">The attribute is not <see cref="TokenProperty.Length"/>.</exception>
    /// <returns>This spec.</returns>
    public TokenSpec NotIn(TokenProperty attribute, params int[] values)
    {
        RequireLength(attribute);
        ArgumentNullException.ThrowIfNull(values);
        _constraints.Add(new Constraint(attribute, Test.NotIn) { Numbers = [.. values] });
        return this;
    }

    /// <summary>Requires a regular expression to match somewhere in a string attribute (a search, not a full match).</summary>
    /// <param name="attribute">A string attribute.</param>
    /// <param name="pattern">A .NET regular expression. Where the pattern allows it the linear-time engine runs it; otherwise a one-second timeout applies and a timed-out test counts as no match.</param>
    /// <exception cref="ArgumentException">The attribute is not a string attribute, or the expression is invalid.</exception>
    /// <returns>This spec.</returns>
    public TokenSpec Matches(TokenProperty attribute, string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        RequireString(attribute);
        _constraints.Add(new Constraint(attribute, Test.Regex) { Text = pattern, Expression = Constraint.CreateRegex(pattern) });
        return this;
    }

    /// <summary>Requires the token's morphological features to be a subset of the given features.</summary>
    /// <param name="features">Features such as <c>Number=Sing</c>.</param>
    /// <returns>This spec.</returns>
    public TokenSpec MorphIsSubset(params string[] features) => AddSet(TokenProperty.Morph, Test.IsSubset, features);

    /// <summary>Requires the token's morphological features to include all the given features.</summary>
    /// <param name="features">Features such as <c>Number=Sing</c>.</param>
    /// <returns>This spec.</returns>
    public TokenSpec MorphIsSuperset(params string[] features) => AddSet(TokenProperty.Morph, Test.IsSuperset, features);

    /// <summary>Requires the token's morphological features to share at least one feature with the given ones.</summary>
    /// <param name="features">Features such as <c>Number=Sing</c>.</param>
    /// <returns>This spec.</returns>
    public TokenSpec MorphIntersects(params string[] features) => AddSet(TokenProperty.Morph, Test.Intersects, features);

    /// <summary>Requires the verbatim text to equal a value.</summary>
    /// <param name="value">The text.</param>
    /// <returns>This spec.</returns>
    public TokenSpec Orth(string value) => Is(TokenProperty.Orth, value);

    /// <summary>Requires the lower-cased text to equal a value.</summary>
    /// <param name="value">The lower-case text.</param>
    /// <returns>This spec.</returns>
    public TokenSpec Lower(string value) => Is(TokenProperty.Lower, value);

    /// <summary>Requires the word shape to equal a value.</summary>
    /// <param name="value">A shape such as <c>dd-dddd</c>.</param>
    /// <returns>This spec.</returns>
    public TokenSpec Shape(string value) => Is(TokenProperty.Shape, value);

    /// <summary>Sets how many tokens the position covers, by spaCy's <c>OP</c> text.</summary>
    /// <param name="op"><c>!</c>, <c>?</c>, <c>+</c>, <c>*</c>, <c>{n}</c>, <c>{n,m}</c>, <c>{n,}</c>, <c>{,m}</c>, or empty for exactly one. Bounds are at most 1000.</param>
    /// <exception cref="ArgumentException">The text is not an operator.</exception>
    /// <returns>This spec.</returns>
    public TokenSpec Op(string op)
    {
        ArgumentNullException.ThrowIfNull(op);
        if (!Quantifier.TryParse(op, out Quantifier q))
        {
            throw new ArgumentException($"'{op}' is not a pattern operator (use !, ?, +, *, {{n}}, {{n,m}}, {{n,}} or {{,m}}, with bounds up to {Quantifier.MaxBound}).", nameof(op));
        }

        Quantifier = q;
        return this;
    }

    /// <summary>Makes the position optional (<c>?</c>).</summary>
    /// <returns>This spec.</returns>
    public TokenSpec Optional() => Op("?");

    /// <summary>Lets the position repeat one or more times (<c>+</c>).</summary>
    /// <returns>This spec.</returns>
    public TokenSpec OneOrMore() => Op("+");

    /// <summary>Lets the position repeat zero or more times (<c>*</c>).</summary>
    /// <returns>This spec.</returns>
    public TokenSpec ZeroOrMore() => Op("*");

    /// <summary>Negates the position (<c>!</c>): it matches one token that fails the constraints.</summary>
    /// <returns>This spec.</returns>
    public TokenSpec Not() => Op("!");

    /// <summary>Sets a repeat range, <c>{min,max}</c>.</summary>
    /// <param name="min">The fewest tokens, from 0.</param>
    /// <param name="max">The most tokens, at least 1 and at least <paramref name="min"/>, or -1 for no limit. Bounds are at most 1000.</param>
    /// <exception cref="ArgumentException">The bounds are out of range.</exception>
    /// <returns>This spec.</returns>
    public TokenSpec Repeat(int min, int max)
    {
        if (min < 0 || min > Quantifier.MaxBound || max > Quantifier.MaxBound || max == 0 || max < -1 || (max != -1 && max < min))
        {
            throw new ArgumentException("Repeat bounds must satisfy 0 <= min <= max <= 1000 with max >= 1 (max -1 means no limit).");
        }

        Quantifier = new Quantifier(min, max);
        return this;
    }

    internal TokenSpec Clone()
    {
        var copy = new TokenSpec { Quantifier = Quantifier };
        copy._constraints.AddRange(_constraints);
        return copy;
    }

    private TokenSpec AddSet(TokenProperty attribute, Test test, string[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        RequireString(attribute);
        if (attribute == TokenProperty.EntIob)
        {
            throw new ArgumentException("ENT_IOB takes a single value, not a set.", nameof(attribute));
        }

        string[] copy = [.. values];
        foreach (string v in copy)
        {
            ArgumentNullException.ThrowIfNull(v);
            CheckIob(attribute, v);
        }

        _constraints.Add(new Constraint(attribute, test) { Texts = copy, TextSet = new HashSet<string>(copy, StringComparer.Ordinal) });
        return this;
    }

    private static void RequireString(TokenProperty attribute)
    {
        if (!TokenProperties.IsString(attribute))
        {
            throw new ArgumentException($"{TokenProperties.NameOf(attribute)} is not a text attribute.", nameof(attribute));
        }
    }

    private static void RequireLength(TokenProperty attribute)
    {
        if (attribute != TokenProperty.Length)
        {
            throw new ArgumentException($"{TokenProperties.NameOf(attribute)} is not a numeric attribute; only LENGTH takes numbers.", nameof(attribute));
        }
    }

    private static void CheckIob(TokenProperty attribute, string value)
    {
        if (attribute == TokenProperty.EntIob && value is not ("" or "B" or "I" or "O"))
        {
            throw new ArgumentException("ENT_IOB values are B, I, O or empty.");
        }
    }

    internal static string IobText(int code) => code switch
    {
        0 => string.Empty,
        1 => "I",
        2 => "O",
        3 => "B",
        _ => throw new ArgumentException("ENT_IOB codes are 0 (unset), 1 (I), 2 (O) and 3 (B)."),
    };
}
