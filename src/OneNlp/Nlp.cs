using RecursiveAI.OneNlp.Segmentation;

namespace RecursiveAI.OneNlp;

/// <summary>
/// The happy path: static task verbs with no options objects, each one line over the low-level types (<see cref="Pipeline"/>,
/// <see cref="Doc"/>). Nothing here downloads anything.
/// </summary>
/// <remarks>
/// Until language identification lands, these verbs assume English text: English tokenization data (contractions, abbreviations)
/// applies. For another language build a <see cref="Pipeline"/> with <see cref="Pipeline.Blank(Language)"/>.
/// </remarks>
public static class Nlp
{
    // Built once. With only the tokenizer and the sentencizer it never writes to the string table, so sharing it is safe.
    private static readonly Pipeline TokenPipeline = Pipeline.Blank(Language.English);
    private static readonly Pipeline DefaultPipeline = Pipeline.Blank(Language.English).Add(SentencizerComponent.For(Language.English));

    /// <summary>Analyzes a text with the default pipeline: tokens and sentence boundaries for now.</summary>
    /// <param name="text">The text; may be empty. The document keeps this string and every offset is into it.</param>
    /// <returns>A document with its tokens and sentence boundaries; the other annotations stay unset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static Doc Analyze(string text) => DefaultPipeline.Process(text);

    /// <summary>Splits a text into word tokens (UAX #29 word segments, adjusted to UD English tokenization). White space is not a token.</summary>
    /// <param name="text">The text; may be empty.</param>
    /// <returns>The tokens in order; each has character offsets into <paramref name="text"/> and keeps it alive. Empty for an empty or all-white-space text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static Token[] Tokens(string text) => [.. TokenPipeline.Process(text)];

    /// <summary>Splits a text into sentences (UAX #29 sentence boundaries snapped to tokens, with English abbreviations such as <c>Mr.</c> held together).</summary>
    /// <param name="text">The text; may be empty.</param>
    /// <returns>The sentences in order, each a span of whole tokens with character offsets into <paramref name="text"/>. Empty for a text without tokens.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<Span> Sentences(string text) => Analyze(text).Sentences;
}
