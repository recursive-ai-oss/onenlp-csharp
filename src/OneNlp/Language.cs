namespace RecursiveAI.OneNlp;

/// <summary>A language, named by its lowercase ISO 639 code (<c>en</c>, <c>de</c>, <c>zh</c>, or a three-letter code where no two-letter one exists).</summary>
public readonly struct Language : IEquatable<Language>
{
    private readonly string? _code;

    private Language(string code) => _code = code;

    /// <summary>The language is not specified. Its code is the empty string, and it is the default value.</summary>
    public static Language Unknown => default;

    /// <summary>English (<c>en</c>).</summary>
    public static Language English { get; } = new("en");

    /// <summary>German (<c>de</c>).</summary>
    public static Language German { get; } = new("de");

    /// <summary>French (<c>fr</c>).</summary>
    public static Language French { get; } = new("fr");

    /// <summary>Spanish (<c>es</c>).</summary>
    public static Language Spanish { get; } = new("es");

    /// <summary>Portuguese (<c>pt</c>).</summary>
    public static Language Portuguese { get; } = new("pt");

    /// <summary>Italian (<c>it</c>).</summary>
    public static Language Italian { get; } = new("it");

    /// <summary>Dutch (<c>nl</c>).</summary>
    public static Language Dutch { get; } = new("nl");

    /// <summary>Turkish (<c>tr</c>).</summary>
    public static Language Turkish { get; } = new("tr");

    /// <summary>Hindi (<c>hi</c>).</summary>
    public static Language Hindi { get; } = new("hi");

    /// <summary>Chinese (<c>zh</c>).</summary>
    public static Language Chinese { get; } = new("zh");

    /// <summary>The lowercase language code; the empty string for <see cref="Unknown"/>.</summary>
    public string Code => _code ?? string.Empty;

    /// <summary>Creates a language from its code.</summary>
    /// <param name="code">An ISO 639 code of two or three ASCII letters, in any case; the empty string means <see cref="Unknown"/>.</param>
    /// <exception cref="ArgumentException"><paramref name="code"/> is neither empty nor two or three ASCII letters.</exception>
    public static Language FromCode(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        if (code.Length == 0)
        {
            return default;
        }

        if (code.Length is < 2 or > 3 || !code.All(char.IsAsciiLetter))
        {
            throw new ArgumentException("A language code is two or three ASCII letters.", nameof(code));
        }

        return new Language(code.ToLowerInvariant());
    }

    /// <inheritdoc/>
    public bool Equals(Language other) => string.Equals(Code, other.Code, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Language other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Code.GetHashCode(StringComparison.Ordinal);

    /// <summary>Returns <see cref="Code"/>.</summary>
    public override string ToString() => Code;

    /// <summary>Whether two languages have the same code.</summary>
    public static bool operator ==(Language left, Language right) => left.Equals(right);

    /// <summary>Whether two languages have different codes.</summary>
    public static bool operator !=(Language left, Language right) => !left.Equals(right);
}
