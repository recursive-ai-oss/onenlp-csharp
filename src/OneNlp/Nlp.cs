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

    // The one place the language of a text is decided. Language identification plugs in here; until then every text is English.
    private static Language LanguageOf(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Language.English;
    }

    /// <summary>
    /// Analyzes a text. With a model package for the text's language referenced (<c>RecursiveAI.OneNlp.Models.English</c> for English)
    /// the document carries sentences, tags, morphology, lemmas, the dependency parse and entities; without one it carries tokens and
    /// sentence boundaries only.
    /// </summary>
    /// <param name="text">The text; may be empty. The document keeps this string and every offset is into it.</param>
    /// <returns>A document with its tokens and sentence boundaries, and the model annotations when a model package is available; otherwise those stay unset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static Doc Analyze(string text) =>
        ModelPackages.TryGet(LanguageOf(text), out Pipeline model) ? model.Process(text) : DefaultPipeline.Process(text);

    /// <summary>Analyzes many texts, a batch at a time, with the same pipeline as <see cref="Analyze"/>; the texts are read lazily, so a long sequence streams.</summary>
    /// <param name="texts">The texts; none may be <see langword="null"/>.</param>
    /// <returns>One document per text, in input order. The language is decided once, from the first text (English for now), and applies to all.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="texts"/> or one of its items is <see langword="null"/>.</exception>
    public static IEnumerable<Doc> AnalyzeMany(IEnumerable<string> texts)
    {
        ArgumentNullException.ThrowIfNull(texts);
        Pipeline pipeline = ModelPackages.TryGet(Language.English, out Pipeline model) ? model : DefaultPipeline;
        return pipeline.ProcessMany(texts);
    }

    /// <summary>Finds the named entities of a text (person, organization, location).</summary>
    /// <param name="text">The text; may be empty.</param>
    /// <returns>The entities in text order, without overlaps; each has its <see cref="Span.Label"/> (<c>PER</c>, <c>ORG</c> or <c>LOC</c>), a confidence, and character offsets into <paramref name="text"/>. They stay valid as long as the text's <see cref="Doc"/> does. Empty when there are none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No model package is available for the text's language; the message names the package to add.</exception>
    public static IReadOnlyList<Span> Entities(string text) => ModelPackages.Get(LanguageOf(text)).Process(text).Ents;

    /// <summary>Finds the lemma (dictionary form) of each token of a text.</summary>
    /// <param name="text">The text; may be empty.</param>
    /// <returns>One lemma per token, in token order; empty for a text without tokens.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No model package is available for the text's language; the message names the package to add.</exception>
    public static string[] Lemmas(string text) => [.. ModelPackages.Get(LanguageOf(text)).Process(text).Select(t => t.Lemma)];

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

    /// <summary>
    /// Finds numbers, ordinals, percentages, measures and amounts of money in English text and resolves each to a value:
    /// <c>$5m</c> is 5,000,000 USD, <c>twenty-first</c> is the ordinal 21, <c>3.5 km</c> is 3.5 kilometers.
    /// </summary>
    /// <param name="text">The text; may be empty.</param>
    /// <returns>The results in text order without overlaps, with UTF-16 offsets into <paramref name="text"/>. Empty when there are none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static IEnumerable<Quantity> Quantities(string text) => Recognizers.Recognizer.English.Recognize(text).OfType<Quantity>();

    /// <summary>Finds plain numbers and ordinals in English text (<see cref="QuantityKind.Cardinal"/> and <see cref="QuantityKind.Ordinal"/> only).</summary>
    /// <param name="text">The text; may be empty.</param>
    /// <returns>The results in text order, with UTF-16 offsets into <paramref name="text"/>. A number that belongs to a unit, a percent sign or a currency is part of that measure and is not listed here.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static IEnumerable<Quantity> Numbers(string text) =>
        Recognizers.Recognizer.English.Recognize(text).OfType<Quantity>().Where(q => q.Kind is QuantityKind.Cardinal or QuantityKind.Ordinal);

    /// <summary>
    /// Finds e-mail addresses, URLs, IP addresses, hashtags, mentions, GUIDs and phone-like sequences in a text. Phone-like sequences are
    /// unvalidated; use a dedicated contact-parsing library for validated, canonical phones.
    /// </summary>
    /// <param name="text">The text; may be empty.</param>
    /// <returns>The results in text order without overlaps, with UTF-16 offsets into <paramref name="text"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static IEnumerable<SequenceMatch> Sequences(string text) => Recognizers.Recognizer.English.Recognize(text).OfType<SequenceMatch>();

    /// <summary>
    /// Finds dates, times, date and time ranges, durations and recurrences in English text and resolves each to a value:
    /// <c>next Friday</c>, <c>spring 2021</c>, <c>from 3pm to 5pm</c>, <c>two weeks</c>, <c>every Monday at 9am</c>.
    /// </summary>
    /// <param name="text">The text; may be empty.</param>
    /// <param name="reference">The moment relative expressions ("tomorrow", "last quarter") are resolved against; wall-clock expressions are read in its offset.</param>
    /// <returns>The results in text order without overlaps, with UTF-16 offsets into <paramref name="text"/>. Empty when there are none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static IEnumerable<TemporalMatch> Dates(string text, DateTimeOffset reference) =>
        Recognizers.Recognizer.English.Recognize(text, new RecognitionContext(reference)).OfType<TemporalMatch>();

    /// <summary>
    /// Resolves the first date, time or range in an English text to a stretch of time: <c>the first Monday after Easter 2027</c> is
    /// that day, <c>spring 2021</c> is March to May 2021 (approximate), <c>3pm</c> is that hour on the reference day.
    /// </summary>
    /// <param name="text">The text; may be empty.</param>
    /// <param name="reference">The moment relative expressions are resolved against; wall-clock expressions are read in its offset.</param>
    /// <returns>The range (start inclusive, end exclusive), or <see langword="null"/> when the text holds no date or time. Durations and recurrences are not dates and are skipped; use <see cref="Dates"/> for them.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static DateRange? ParseDate(string text, DateTimeOffset reference) =>
        Dates(text, reference).FirstOrDefault(d => d.Range is not null)?.Range;
}
