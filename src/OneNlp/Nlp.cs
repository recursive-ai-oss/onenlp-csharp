using System.Collections.Concurrent;
using RecursiveAI.OneNlp.LanguageId;
using RecursiveAI.OneNlp.Segmentation;

namespace RecursiveAI.OneNlp;

/// <summary>
/// The happy path: static task verbs with no options objects, each one line over the low-level types (<see cref="Pipeline"/>,
/// <see cref="Doc"/>). Nothing here downloads anything.
/// </summary>
/// <remarks>
/// The language of a text is detected first (<see cref="LanguageDetector"/>), and that language picks the model package and the
/// segmentation data. Without a model package for the language, <see cref="Analyze(string)"/> returns tokens and sentence boundaries
/// from the language's rule-based segmentation (English has contraction and abbreviation exceptions; other languages use plain UAX #29).
/// To fix the language yourself, build a <see cref="Pipeline"/> with <see cref="Pipeline.Blank(Language)"/>.
/// </remarks>
public static class Nlp
{
    // Rule-based pipelines, one per language, built on first use and shared. With only the tokenizer and the sentencizer a pipeline never
    // writes to the string table, so sharing it between threads is safe. The key set is bounded by the languages of the detector.
    private static readonly ConcurrentDictionary<Language, Pipeline> TokenPipelines = new();
    private static readonly ConcurrentDictionary<Language, Pipeline> SentencePipelines = new();

    private static Pipeline Plain(Language language) =>
        SentencePipelines.GetOrAdd(language, static l => Pipeline.Blank(l).Add(SentencizerComponent.For(l)));

    // The one place the language of a text is decided. A text with no letters of a supported script is English; so is every text in a
    // build that carries no language-ID model (a development build without ONENLP_LANGID_WEIGHTS), where nothing can be detected.
    private static Language LanguageOf(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!LanguageDetector.TryGetDefault(out LanguageDetector? detector))
        {
            return Language.English;
        }

        Language language = detector.Detect(text);
        return language == Language.Unknown ? Language.English : language;
    }

    /// <summary>The most likely language of a text, from the model that ships with this library.</summary>
    /// <param name="text">Any text, from one word up. Digits and punctuation are ignored.</param>
    /// <returns>The language, or <see cref="Language.Unknown"/> when the text has no letters of a supported script. Use <see cref="LanguageDetector"/> for confidences, restricted candidate sets and mixed-language text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">This build carries no language-ID model.</exception>
    public static Language DetectLanguage(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return LanguageDetector.Default.Detect(text);
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
        AnalyzeAs(text, LanguageOf(text));

    private static Doc AnalyzeAs(string text, Language language) =>
        ModelPackages.TryGet(language, out Pipeline model) ? model.Process(text) : Plain(language).Process(text);

    /// <summary>Analyzes many texts, a batch at a time, with the same pipeline as <see cref="Analyze"/>; the texts are read lazily, so a long sequence streams.</summary>
    /// <param name="texts">The texts; none may be <see langword="null"/>.</param>
    /// <returns>One document per text, in input order. The language is detected once, from the first text, and applies to all.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="texts"/> or one of its items is <see langword="null"/>.</exception>
    public static IEnumerable<Doc> AnalyzeMany(IEnumerable<string> texts)
    {
        ArgumentNullException.ThrowIfNull(texts);
        return AnalyzeManyCore(texts);
    }

    private static IEnumerable<Doc> AnalyzeManyCore(IEnumerable<string> texts)
    {
        using IEnumerator<string> e = texts.GetEnumerator();
        if (!e.MoveNext())
        {
            yield break;
        }

        Language language = LanguageOf(e.Current);
        Pipeline pipeline = ModelPackages.TryGet(language, out Pipeline model) ? model : Plain(language);
        IEnumerable<string> All()
        {
            yield return e.Current;
            while (e.MoveNext())
            {
                yield return e.Current;
            }
        }

        foreach (Doc doc in pipeline.ProcessMany(All()))
        {
            yield return doc;
        }
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

    /// <summary>Splits a text into word tokens (UAX #29 word segments; for English adjusted to UD English tokenization). White space is not a token.</summary>
    /// <param name="text">The text; may be empty.</param>
    /// <returns>The tokens in order; each has character offsets into <paramref name="text"/> and keeps it alive. Empty for an empty or all-white-space text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static Token[] Tokens(string text) => [.. TokenPipelines.GetOrAdd(LanguageOf(text), static l => Pipeline.Blank(l)).Process(text)];

    /// <summary>Splits a text into sentences (UAX #29 sentence boundaries snapped to tokens; for English abbreviations such as <c>Mr.</c> are held together).</summary>
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
