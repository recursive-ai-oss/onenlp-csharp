using System.Collections;

namespace RecursiveAI.OneNlp.Matching;

/// <summary>
/// A token pattern: a sequence of <see cref="TokenSpec"/> positions that a run of tokens must satisfy in order, as in spaCy's
/// token-pattern lists. Immutable; build one with <see cref="Token"/> or the one-token shortcuts, extend with <see cref="Then(TokenSpec)"/>.
/// </summary>
/// <example>
/// <code>
/// Pattern.Shape("dd-dddd")
/// Pattern.Token(new TokenSpec().Lower("hello"), new TokenSpec().Is(TokenProperty.IsPunct, true).Optional(), new TokenSpec().Lower("world"))
/// </code>
/// </example>
public sealed class Pattern : IReadOnlyList<TokenSpec>
{
    /// <summary>The most positions one pattern may have.</summary>
    public const int MaxPositions = 1000;

    /// <summary>The most token slots one pattern may expand to once <c>{n,m}</c> repeats are counted.</summary>
    public const int MaxSlots = 10_000;

    private readonly TokenSpec[] _specs;

    private Pattern(TokenSpec[] specs)
    {
        _specs = specs;
    }

    /// <inheritdoc/>
    public int Count => _specs.Length;

    /// <inheritdoc/>
    public TokenSpec this[int index] => _specs[index];

    /// <summary>Makes a pattern from positions, copying each.</summary>
    /// <param name="tokens">The positions in order; at least one, at most <see cref="MaxPositions"/>.</param>
    /// <exception cref="ArgumentException">There are no positions, too many, or the repeats expand beyond <see cref="MaxSlots"/> slots.</exception>
    /// <returns>The pattern.</returns>
    public static Pattern Token(params TokenSpec[] tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        if (tokens.Length == 0)
        {
            throw new ArgumentException("A pattern needs at least one token position.", nameof(tokens));
        }

        if (tokens.Length > MaxPositions)
        {
            throw new ArgumentException($"A pattern has at most {MaxPositions} token positions.", nameof(tokens));
        }

        long slots = 0;
        var copy = new TokenSpec[tokens.Length];
        for (int i = 0; i < tokens.Length; i++)
        {
            ArgumentNullException.ThrowIfNull(tokens[i]);
            copy[i] = tokens[i].Clone();
            Quantifier q = copy[i].Quantifier;
            slots += q.Max == -1 ? q.Min + 1 : q.Max;
        }

        if (slots > MaxSlots)
        {
            throw new ArgumentException($"The repeats expand to {slots} token slots; the limit is {MaxSlots}.", nameof(tokens));
        }

        return new Pattern(copy);
    }

    /// <summary>A one-token pattern: the token's shape equals <paramref name="shape"/>.</summary>
    /// <param name="shape">A shape such as <c>dd-dddd</c> (see <see cref="TokenText.Shape(ReadOnlySpan{char})"/>).</param>
    /// <returns>The pattern.</returns>
    public static Pattern Shape(string shape) => Token(new TokenSpec().Shape(shape));

    /// <summary>A one-token pattern: the token's text equals <paramref name="text"/>.</summary>
    /// <param name="text">The verbatim text.</param>
    /// <returns>The pattern.</returns>
    public static Pattern Orth(string text) => Token(new TokenSpec().Orth(text));

    /// <summary>A one-token pattern: the token's lower-cased text equals <paramref name="text"/>.</summary>
    /// <param name="text">The lower-case text.</param>
    /// <returns>The pattern.</returns>
    public static Pattern Lower(string text) => Token(new TokenSpec().Lower(text));

    /// <summary>A new pattern with one more position at the end.</summary>
    /// <param name="next">The position, copied.</param>
    /// <exception cref="ArgumentException">The result would exceed the pattern limits.</exception>
    /// <returns>The longer pattern.</returns>
    public Pattern Then(TokenSpec next) => Token([.. _specs, next]);

    /// <summary>A new pattern: this one followed by another.</summary>
    /// <param name="next">The pattern to append.</param>
    /// <exception cref="ArgumentException">The result would exceed the pattern limits.</exception>
    /// <returns>The longer pattern.</returns>
    public Pattern Then(Pattern next)
    {
        ArgumentNullException.ThrowIfNull(next);
        return Token([.. _specs, .. next._specs]);
    }

    /// <inheritdoc/>
    public IEnumerator<TokenSpec> GetEnumerator() => ((IEnumerable<TokenSpec>)_specs).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _specs.GetEnumerator();
}
