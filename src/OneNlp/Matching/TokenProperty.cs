namespace RecursiveAI.OneNlp.Matching;

/// <summary>A token attribute that a pattern can test, named as in spaCy's token-pattern documentation.</summary>
public enum TokenProperty
{
    /// <summary>The verbatim text (<c>ORTH</c>, also written <c>TEXT</c>).</summary>
    Orth,

    /// <summary>The normalized form (<c>NORM</c>): the document's norm column, or the lower-cased text where none is set.</summary>
    Norm,

    /// <summary>The lower-cased text (<c>LOWER</c>).</summary>
    Lower,

    /// <summary>The number of Unicode code points in the text (<c>LENGTH</c>); an integer attribute.</summary>
    Length,

    /// <summary>The lemma (<c>LEMMA</c>); empty where no component set it.</summary>
    Lemma,

    /// <summary>The coarse part of speech (<c>POS</c>).</summary>
    Pos,

    /// <summary>The fine-grained tag (<c>TAG</c>).</summary>
    Tag,

    /// <summary>The dependency relation (<c>DEP</c>).</summary>
    Dep,

    /// <summary>The morphological feature string, <c>Case=Nom|Number=Sing</c> (<c>MORPH</c>); also tested as a feature set.</summary>
    Morph,

    /// <summary>The entity type of the token, empty outside entities (<c>ENT_TYPE</c>).</summary>
    EntType,

    /// <summary>The entity position: <c>B</c>, <c>I</c>, <c>O</c>, or empty where the document has no entity annotation (<c>ENT_IOB</c>).</summary>
    EntIob,

    /// <summary>The word shape (<c>SHAPE</c>); see <see cref="TokenText.Shape(ReadOnlySpan{char})"/>.</summary>
    Shape,

    /// <summary><c>IS_ALPHA</c>.</summary>
    IsAlpha,

    /// <summary><c>IS_ASCII</c>.</summary>
    IsAscii,

    /// <summary><c>IS_DIGIT</c>.</summary>
    IsDigit,

    /// <summary><c>IS_LOWER</c>.</summary>
    IsLower,

    /// <summary><c>IS_UPPER</c>.</summary>
    IsUpper,

    /// <summary><c>IS_TITLE</c>.</summary>
    IsTitle,

    /// <summary><c>IS_PUNCT</c>.</summary>
    IsPunct,

    /// <summary><c>IS_SPACE</c>.</summary>
    IsSpace,

    /// <summary><c>IS_STOP</c>: the lower-cased text is in <see cref="Matcher.StopWords"/> (empty unless the caller sets it).</summary>
    IsStop,

    /// <summary><c>IS_SENT_START</c> (also <c>SENT_START</c>): the token starts a sentence; the first token always does.</summary>
    IsSentStart,

    /// <summary><c>IS_CURRENCY</c>.</summary>
    IsCurrency,

    /// <summary><c>IS_LEFT_PUNCT</c>.</summary>
    IsLeftPunct,

    /// <summary><c>IS_RIGHT_PUNCT</c>.</summary>
    IsRightPunct,

    /// <summary><c>IS_QUOTE</c>.</summary>
    IsQuote,

    /// <summary><c>IS_BRACKET</c>.</summary>
    IsBracket,

    /// <summary><c>LIKE_NUM</c>.</summary>
    LikeNum,

    /// <summary><c>LIKE_URL</c>.</summary>
    LikeUrl,

    /// <summary><c>LIKE_EMAIL</c>.</summary>
    LikeEmail,

    /// <summary><c>SPACY</c>: the token is followed by white space.</summary>
    HasSpaceAfter,
}

/// <summary>How a numeric attribute is compared (spaCy's <c>==</c>, <c>!=</c>, <c>&gt;=</c>, <c>&lt;=</c>, <c>&gt;</c>, <c>&lt;</c> predicates).</summary>
public enum Comparison
{
    /// <summary>Equal.</summary>
    Equal,

    /// <summary>Not equal.</summary>
    NotEqual,

    /// <summary>Greater than or equal.</summary>
    GreaterOrEqual,

    /// <summary>Less than or equal.</summary>
    LessOrEqual,

    /// <summary>Greater than.</summary>
    Greater,

    /// <summary>Less than.</summary>
    Less,
}

/// <summary>Names of attributes as written in pattern files.</summary>
public static class TokenProperties
{
    private static readonly Dictionary<string, TokenProperty> ByName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ORTH"] = TokenProperty.Orth,
        ["TEXT"] = TokenProperty.Orth,
        ["NORM"] = TokenProperty.Norm,
        ["LOWER"] = TokenProperty.Lower,
        ["LENGTH"] = TokenProperty.Length,
        ["LEMMA"] = TokenProperty.Lemma,
        ["POS"] = TokenProperty.Pos,
        ["TAG"] = TokenProperty.Tag,
        ["DEP"] = TokenProperty.Dep,
        ["MORPH"] = TokenProperty.Morph,
        ["ENT_TYPE"] = TokenProperty.EntType,
        ["ENT_IOB"] = TokenProperty.EntIob,
        ["SHAPE"] = TokenProperty.Shape,
        ["IS_ALPHA"] = TokenProperty.IsAlpha,
        ["IS_ASCII"] = TokenProperty.IsAscii,
        ["IS_DIGIT"] = TokenProperty.IsDigit,
        ["IS_LOWER"] = TokenProperty.IsLower,
        ["IS_UPPER"] = TokenProperty.IsUpper,
        ["IS_TITLE"] = TokenProperty.IsTitle,
        ["IS_PUNCT"] = TokenProperty.IsPunct,
        ["IS_SPACE"] = TokenProperty.IsSpace,
        ["IS_STOP"] = TokenProperty.IsStop,
        ["IS_SENT_START"] = TokenProperty.IsSentStart,
        ["SENT_START"] = TokenProperty.IsSentStart,
        ["IS_CURRENCY"] = TokenProperty.IsCurrency,
        ["IS_LEFT_PUNCT"] = TokenProperty.IsLeftPunct,
        ["IS_RIGHT_PUNCT"] = TokenProperty.IsRightPunct,
        ["IS_QUOTE"] = TokenProperty.IsQuote,
        ["IS_BRACKET"] = TokenProperty.IsBracket,
        ["LIKE_NUM"] = TokenProperty.LikeNum,
        ["LIKE_URL"] = TokenProperty.LikeUrl,
        ["LIKE_EMAIL"] = TokenProperty.LikeEmail,
        ["SPACY"] = TokenProperty.HasSpaceAfter,
    };

    private static readonly string[] Canonical = BuildCanonical();

    /// <summary>Looks an attribute up by its pattern-file name, ignoring case; <c>TEXT</c> is <see cref="TokenProperty.Orth"/>.</summary>
    /// <param name="name">The name, for example <c>LOWER</c> or <c>is_alpha</c>.</param>
    /// <param name="attribute">The attribute when found.</param>
    /// <returns><see langword="true"/> when the name is known.</returns>
    public static bool TryParse(string name, out TokenProperty attribute) => ByName.TryGetValue(name, out attribute);

    /// <summary>The canonical upper-case name of an attribute, as written to pattern files.</summary>
    /// <param name="attribute">The attribute.</param>
    public static string NameOf(TokenProperty attribute) => Canonical[(int)attribute];

    /// <summary>Whether the attribute is tested against text (as opposed to a number or a yes/no flag).</summary>
    /// <param name="attribute">The attribute.</param>
    public static bool IsString(TokenProperty attribute) => attribute <= TokenProperty.Shape && attribute != TokenProperty.Length;

    /// <summary>Whether the attribute is a yes/no flag.</summary>
    /// <param name="attribute">The attribute.</param>
    public static bool IsFlag(TokenProperty attribute) => attribute >= TokenProperty.IsAlpha;

    private static string[] BuildCanonical()
    {
        var names = new string[Enum.GetValues<TokenProperty>().Length];
        foreach (KeyValuePair<string, TokenProperty> pair in ByName)
        {
            // TEXT and SENT_START are aliases of ORTH and IS_SENT_START.
            if (pair.Key is not ("TEXT" or "SENT_START"))
            {
                names[(int)pair.Value] = pair.Key;
            }
        }

        return names;
    }
}
