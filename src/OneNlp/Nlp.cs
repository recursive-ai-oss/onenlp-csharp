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
/// To fix the language yourself, pass it: <see cref="Analyze(string, Language)"/>, <see cref="Entities(string, Language)"/> and
/// <see cref="Lemmas(string, Language)"/> take a <see cref="Language"/> like the recognizer verbs do.
/// </remarks>
/// <remarks>
/// <para>
/// <b>Short texts and names.</b> A word or a name cannot be told apart reliably ("Paris" is French, but an English model reads it fine). When the
/// detector is unsure (confidence below 0.8) and has no model for its answer, the language is chosen among the languages that have a model. A
/// confident language without a model is an error (<see cref="Entities(string)"/>, <see cref="Lemmas(string)"/>) or leaves the document
/// without annotations (<see cref="Analyze(string)"/>); name the language to skip detection.
/// </para>
/// <para>
/// <b>Size.</b> Every verb that takes a text refuses one longer than <see cref="MaxLength"/> characters with an <see cref="ArgumentOutOfRangeException"/>.
/// Split longer input yourself (by paragraph or line), or raise the limit knowing that memory grows with the text: about 100 bytes per character
/// for <see cref="Analyze(string)"/> with a model, 10 for the recognizers.
/// </para>
/// </remarks>
public static class Nlp
{
    // Rule-based pipelines, one per language, built on first use and shared. With only the tokenizer and the sentencizer a pipeline never
    // writes to the string table, so sharing it between threads is safe. The key set is bounded by the languages of the detector.
    private static readonly ConcurrentDictionary<Language, Pipeline> TokenPipelines = new();
    private static readonly ConcurrentDictionary<Language, Pipeline> SentencePipelines = new();

    /// <summary>The longest text, in UTF-16 characters, that the verbs of this class and a <see cref="Pipeline"/> that has no limit of its own accept. 1,000,000 unless changed.</summary>
    /// <value>At least 1. Set it once at start-up; it is read on every call.</value>
    /// <exception cref="ArgumentOutOfRangeException">Set to less than 1.</exception>
    public static int MaxLength
    {
        get => Volatile.Read(ref _maxLength);
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            Volatile.Write(ref _maxLength, value);
        }
    }

    private static int _maxLength = DefaultMaxLength;

    /// <summary>The value of <see cref="MaxLength"/> until it is changed.</summary>
    internal const int DefaultMaxLength = 1_000_000;

    /// <summary>The detector's confidence from which its answer is trusted even when no model exists for the language.</summary>
    internal const double TrustedConfidence = 0.8;

    /// <summary>Throws when a text is longer than <paramref name="limit"/>.</summary>
    internal static void CheckLength(string text, int limit, string paramName, string limitName)
    {
        ArgumentNullException.ThrowIfNull(text, paramName);
        if (text.Length > limit)
        {
            throw new ArgumentOutOfRangeException(paramName, text.Length,
                $"The text has {text.Length} characters; {limitName} is {limit}. Split the text, or raise the limit.");
        }
    }

    private static void CheckLength(string text) => CheckLength(text, MaxLength, nameof(text), "Nlp.MaxLength");

    private static Pipeline Plain(Language language) =>
        SentencePipelines.GetOrAdd(language, static l => Pipeline.Blank(l).Add(SentencizerComponent.For(l)));

    // The one place the language of a text is decided when the caller did not say. A text with no letters of a supported script is English;
    // so is every text in a build that carries no language-ID model (a development build without ONENLP_LANGID_WEIGHTS), where nothing can be detected.
    private static Language LanguageOf(string text)
    {
        CheckLength(text);
        if (!LanguageDetector.TryGetDefault(out LanguageDetector? detector))
        {
            return Language.English;
        }

        return Choose(text, detector.Detect(text, out double confidence), confidence, RegisteredDetector(detector), ModelPackages.IsRegistered);
    }

    // A detector is unsure about a word or a name ("Paris" is French, "Ok" is Japanese, "Angela Merkel" is Basque), and the caller who referenced one
    // model package wants that model: when the detected language has no model and the detector's confidence is below TrustedConfidence, the text is
    // read in the most likely of the languages that do have one (Lingua's README advises restricting the candidates the same way). A confident answer
    // stays, so French text without a French model is an error that names the package, not English output.
    internal static Language Choose(string text, Language detected, double confidence, LanguageDetector? among, Func<Language, bool> hasModel)
    {
        if (detected == Language.Unknown)
        {
            detected = Language.English;
            confidence = 0;
        }

        if (hasModel(detected) || confidence >= TrustedConfidence || among is null)
        {
            return detected;
        }

        Language found = among.Detect(text);
        return found == Language.Unknown ? detected : found;
    }

    // The detector restricted to the languages that have a model, rebuilt when a package registers.
    private sealed record RestrictedDetector(int Version, LanguageDetector? Detector);

    private static RestrictedDetector _registeredDetector = new(-1, null);

    private static LanguageDetector? RegisteredDetector(LanguageDetector all)
    {
        RestrictedDetector cached = Volatile.Read(ref _registeredDetector);
        int now = ModelPackages.Version;
        if (cached.Version == now)
        {
            return cached.Detector;
        }

        Language[] languages = ModelPackages.RegisteredLanguages();
        LanguageDetector? built = null;
        if (languages.Length > 0)
        {
            try
            {
                built = all.Restrict(languages);
            }
            catch (ArgumentException)
            {
                // None of the registered languages is in the detector's model: nothing to restrict to.
            }
        }

        Volatile.Write(ref _registeredDetector, new RestrictedDetector(now, built));
        return built;
    }

    /// <summary>The most likely language of a text, from the model that ships with this library.</summary>
    /// <param name="text">Any text, from one word up. Digits and punctuation are ignored.</param>
    /// <returns>The language, or <see cref="Language.Unknown"/> when the text has no letters of a supported script. Use <see cref="LanguageDetector"/> for confidences, restricted candidate sets and mixed-language text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="text"/> is longer than <see cref="MaxLength"/>.</exception>
    /// <exception cref="InvalidOperationException">This build carries no language-ID model.</exception>
    public static Language DetectLanguage(string text)
    {
        CheckLength(text);
        return LanguageDetector.Default.Detect(text);
    }

    /// <summary>
    /// Analyzes a text. With a model package for the text's language referenced (<c>RecursiveAI.OneNlp.Models.English</c> for English)
    /// the document carries sentences, tags, morphology, lemmas, the dependency parse and entities; without one it carries tokens and
    /// sentence boundaries only. The language is detected as described in the class remarks, the same way as for <see cref="Entities(string)"/>.
    /// </summary>
    /// <param name="text">The text; may be empty. The document keeps this string and every offset is into it.</param>
    /// <returns>A document with its tokens and sentence boundaries, and the model annotations when a model package is available; otherwise those stay unset. Unlike <see cref="Entities(string)"/>, a missing model is not an error here: this is the verb that works without any model package.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="text"/> is longer than <see cref="MaxLength"/>.</exception>
    public static Doc Analyze(string text) =>
        AnalyzeAs(text, LanguageOf(text));

    /// <summary>Analyzes a text in a language you name, without detecting it (see <see cref="Analyze(string)"/>).</summary>
    /// <param name="text">The text; may be empty.</param>
    /// <param name="language">The language of the text. <see cref="Language.Unknown"/> is read as English.</param>
    /// <returns>A document as for <see cref="Analyze(string)"/>; with no model package for <paramref name="language"/> it carries tokens and sentence boundaries only.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="text"/> is longer than <see cref="MaxLength"/>.</exception>
    public static Doc Analyze(string text, Language language)
    {
        CheckLength(text);
        return AnalyzeAs(text, language == Language.Unknown ? Language.English : language);
    }

    private static Doc AnalyzeAs(string text, Language language) =>
        ModelPackages.TryGet(language, out Pipeline model) ? model.Process(text) : Plain(language).Process(text);

    /// <summary>Analyzes many texts, a batch at a time, with the same pipeline as <see cref="Analyze(string)"/>; the texts are read lazily, so a long sequence streams.</summary>
    /// <param name="texts">The texts; none may be <see langword="null"/>.</param>
    /// <returns>One document per text, in input order. The language is detected once, from the first text, and applies to all.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="texts"/> or one of its items is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A text is longer than <see cref="MaxLength"/>; raised when that text is reached.</exception>
    public static IEnumerable<Doc> AnalyzeMany(IEnumerable<string> texts)
    {
        ArgumentNullException.ThrowIfNull(texts);
        return AnalyzeManyCore(texts, null);
    }

    /// <summary>Analyzes many texts in a language you name (see <see cref="AnalyzeMany(IEnumerable{string})"/>).</summary>
    /// <param name="texts">The texts; none may be <see langword="null"/>.</param>
    /// <param name="language">The language of all the texts. <see cref="Language.Unknown"/> is read as English.</param>
    /// <returns>One document per text, in input order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="texts"/> or one of its items is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A text is longer than <see cref="MaxLength"/>; raised when that text is reached.</exception>
    public static IEnumerable<Doc> AnalyzeMany(IEnumerable<string> texts, Language language)
    {
        ArgumentNullException.ThrowIfNull(texts);
        return AnalyzeManyCore(texts, language == Language.Unknown ? Language.English : language);
    }

    private static IEnumerable<Doc> AnalyzeManyCore(IEnumerable<string> texts, Language? given)
    {
        using IEnumerator<string> e = texts.GetEnumerator();
        if (!e.MoveNext())
        {
            yield break;
        }

        Language language = given ?? LanguageOf(e.Current);
        Pipeline pipeline = ModelPackages.TryGet(language, out Pipeline model) ? model : Plain(language);
        IEnumerable<string> All()
        {
            yield return e.Current;
            while (e.MoveNext())
            {
                yield return e.Current;
            }
        }

        // The shared pipelines follow MaxLength, so ProcessMany refuses an over-long text the same way as the other verbs.
        foreach (Doc doc in pipeline.ProcessMany(All()))
        {
            yield return doc;
        }
    }

    /// <summary>Finds the named entities of a text (person, organization, location).</summary>
    /// <param name="text">The text; may be empty.</param>
    /// <returns>The entities in text order, without overlaps; each has its <see cref="Span.Label"/> (<see cref="EntityLabels.Person"/>, <see cref="EntityLabels.Organization"/> or <see cref="EntityLabels.Location"/>), a confidence, and character offsets into <paramref name="text"/>. They stay valid as long as the text's <see cref="Doc"/> does. Empty when there are none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="text"/> is longer than <see cref="MaxLength"/>.</exception>
    /// <exception cref="ModelNotAvailableException">No model package is available for the text's language (detected as described in the class remarks); the message names the package to add. <see cref="Analyze(string)"/> does not throw for this: it returns tokens without entities.</exception>
    public static IReadOnlyList<Span> Entities(string text) => ModelPackages.Get(LanguageOf(text)).Process(text).Ents;

    /// <summary>Finds the named entities of a text in a language you name, without detecting it.</summary>
    /// <param name="text">The text; may be empty.</param>
    /// <param name="language">The language of the text; its model package must be referenced.</param>
    /// <returns>The entities as for <see cref="Entities(string)"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="text"/> is longer than <see cref="MaxLength"/>.</exception>
    /// <exception cref="ModelNotAvailableException">No model package is available for <paramref name="language"/>.</exception>
    public static IReadOnlyList<Span> Entities(string text, Language language)
    {
        CheckLength(text);
        return ModelPackages.Get(language).Process(text).Ents;
    }

    /// <summary>Finds entities of types you name at run time, such as <c>"contract party"</c> or <c>"governing law"</c>.</summary>
    /// <param name="text">The text; may be empty.</param>
    /// <param name="types">The type names: 1 to 256 distinct, non-empty strings of at most 200 characters, in any language the model reads.</param>
    /// <returns>The entities in text order, without overlaps; each <see cref="Span.Label"/> is the type name as given, with a confidence and character offsets into <paramref name="text"/>. Empty when there are none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> or <paramref name="types"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">No type is given, or a type is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="text"/> is longer than <see cref="MaxLength"/>.</exception>
    /// <exception cref="ModelNotAvailableException">No open-type model is available; the message names the package to add.</exception>
    public static IReadOnlyList<Span> Entities(string text, params string[] types) => OpenEntities(text, types, null);

    /// <summary>
    /// Finds entities of types you name at run time, reporting every span whose probability is at least <paramref name="threshold"/>.
    /// The default of <see cref="Entities(string, string[])"/> is 0.5, which suits common types such as <c>"person"</c> or
    /// <c>"organization"</c>; an unusual type name (<c>"contract party"</c>) often scores lower, so lower the threshold for it and
    /// check the results.
    /// </summary>
    /// <param name="text">The text; may be empty.</param>
    /// <param name="types">The type names: 1 to 256 distinct, non-empty strings of at most 200 characters.</param>
    /// <param name="threshold">The probability at or above which a span is reported, above 0 and below 1.</param>
    /// <returns>The entities in text order, without overlaps, each with a confidence of at least <paramref name="threshold"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> or <paramref name="types"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">No type is given, or a type is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="text"/> is longer than <see cref="MaxLength"/>, or <paramref name="threshold"/> is outside (0, 1).</exception>
    /// <exception cref="ModelNotAvailableException">No open-type model is available; the message names the package to add.</exception>
    public static IReadOnlyList<Span> Entities(string text, IReadOnlyList<string> types, float threshold) => OpenEntities(text, types, threshold);

    private static IReadOnlyList<Span> OpenEntities(string text, IReadOnlyList<string> types, float? threshold)
    {
        CheckLength(text);
        return ModelPackages.RecognizeOpen(text, types, threshold);
    }

    /// <summary>Finds the lemma (dictionary form) of each token of a text.</summary>
    /// <param name="text">The text; may be empty.</param>
    /// <returns>One lemma per token, in token order; empty for a text without tokens.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="text"/> is longer than <see cref="MaxLength"/>.</exception>
    /// <exception cref="ModelNotAvailableException">No model package is available for the text's language (detected as described in the class remarks); the message names the package to add.</exception>
    public static string[] Lemmas(string text) => LemmasOf(ModelPackages.Get(LanguageOf(text)), text);

    /// <summary>Finds the lemma of each token of a text in a language you name, without detecting it.</summary>
    /// <param name="text">The text; may be empty.</param>
    /// <param name="language">The language of the text; its model package must be referenced.</param>
    /// <returns>One lemma per token, in token order; empty for a text without tokens.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="text"/> is longer than <see cref="MaxLength"/>.</exception>
    /// <exception cref="ModelNotAvailableException">No model package is available for <paramref name="language"/>.</exception>
    public static string[] Lemmas(string text, Language language)
    {
        CheckLength(text);
        return LemmasOf(ModelPackages.Get(language), text);
    }

    private static string[] LemmasOf(Pipeline model, string text) => [.. model.Process(text).Select(t => t.Lemma)];

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
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="text"/> is longer than <see cref="MaxLength"/>.</exception>
    public static IEnumerable<Quantity> Quantities(string text) => Recognizers.Recognizer.English.Recognize(Checked(text)).OfType<Quantity>();

    /// <summary>Finds numbers, ordinals, percentages, measures and amounts of money in a text of the given language (see <see cref="Quantities(string)"/>).</summary>
    /// <param name="text">The text; may be empty.</param>
    /// <param name="language">English, French, German, Spanish, Portuguese, Italian, Dutch, Turkish, Hindi or Chinese: the languages with built-in rules.</param>
    /// <returns>The results in text order without overlaps, with UTF-16 offsets into <paramref name="text"/>. French and German read <c>1 234,56 €</c> and <c>18.000 EUR</c> with the decimal comma and grouped thousands of their country.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="text"/> is longer than <see cref="MaxLength"/>.</exception>
    /// <exception cref="NotSupportedException">No rules for <paramref name="language"/> are built in.</exception>
    public static IEnumerable<Quantity> Quantities(string text, Language language) => Recognizers.Recognizer.For(language).Recognize(Checked(text)).OfType<Quantity>();

    /// <summary>Finds plain numbers and ordinals in English text (<see cref="QuantityKind.Cardinal"/> and <see cref="QuantityKind.Ordinal"/> only).</summary>
    /// <param name="text">The text; may be empty.</param>
    /// <returns>The results in text order, with UTF-16 offsets into <paramref name="text"/>. A number that belongs to a unit, a percent sign or a currency is part of that measure and is not listed here.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="text"/> is longer than <see cref="MaxLength"/>.</exception>
    public static IEnumerable<Quantity> Numbers(string text) =>
        Recognizers.Recognizer.English.Recognize(Checked(text)).OfType<Quantity>().Where(q => q.Kind is QuantityKind.Cardinal or QuantityKind.Ordinal);

    /// <summary>Finds plain numbers and ordinals in a text of the given language (see <see cref="Numbers(string)"/>).</summary>
    /// <param name="text">The text; may be empty.</param>
    /// <param name="language">English, French, German, Spanish, Portuguese, Italian, Dutch, Turkish, Hindi or Chinese.</param>
    /// <returns>The results in text order, with UTF-16 offsets into <paramref name="text"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="text"/> is longer than <see cref="MaxLength"/>.</exception>
    /// <exception cref="NotSupportedException">No rules for <paramref name="language"/> are built in.</exception>
    public static IEnumerable<Quantity> Numbers(string text, Language language) =>
        Recognizers.Recognizer.For(language).Recognize(Checked(text)).OfType<Quantity>().Where(q => q.Kind is QuantityKind.Cardinal or QuantityKind.Ordinal);

    /// <summary>
    /// Finds e-mail addresses, URLs, IP addresses, hashtags, mentions, GUIDs and phone-like sequences in a text. Phone-like sequences are
    /// unvalidated; use a dedicated contact-parsing library for validated, canonical phones.
    /// </summary>
    /// <param name="text">The text; may be empty.</param>
    /// <returns>The results in text order without overlaps, with UTF-16 offsets into <paramref name="text"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="text"/> is longer than <see cref="MaxLength"/>.</exception>
    public static IEnumerable<SequenceMatch> Sequences(string text) => Recognizers.Recognizer.English.Recognize(Checked(text)).OfType<SequenceMatch>();

    /// <summary>Finds the address-like sequences in a text of the given language (see <see cref="Sequences(string)"/>).</summary>
    /// <param name="text">The text; may be empty.</param>
    /// <param name="language">English, French, German, Spanish, Portuguese, Italian, Dutch, Turkish, Hindi or Chinese.</param>
    /// <returns>The results in text order without overlaps, with UTF-16 offsets into <paramref name="text"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="text"/> is longer than <see cref="MaxLength"/>.</exception>
    /// <exception cref="NotSupportedException">No rules for <paramref name="language"/> are built in.</exception>
    public static IEnumerable<SequenceMatch> Sequences(string text, Language language) => Recognizers.Recognizer.For(language).Recognize(Checked(text)).OfType<SequenceMatch>();

    /// <summary>
    /// Finds dates, times, date and time ranges, durations and recurrences in English text and resolves each to a value:
    /// <c>next Friday</c>, <c>spring 2021</c>, <c>from 3pm to 5pm</c>, <c>two weeks</c>, <c>every Monday at 9am</c>.
    /// </summary>
    /// <param name="text">The text; may be empty.</param>
    /// <param name="reference">The moment relative expressions ("tomorrow", "last quarter") are resolved against; wall-clock expressions are read in its offset.</param>
    /// <returns>The results in text order without overlaps, with UTF-16 offsets into <paramref name="text"/>. Empty when there are none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="text"/> is longer than <see cref="MaxLength"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is <c>default(DateTimeOffset)</c>, which is year 1 and almost certainly an unset value.</exception>
    public static IEnumerable<TemporalMatch> Dates(string text, DateTimeOffset reference) =>
        Recognizers.Recognizer.English.Recognize(Checked(text), ContextAt(reference)).OfType<TemporalMatch>();

    /// <summary>Finds dates, times, ranges, durations and recurrences in a text of the given language (see <see cref="Dates(string, DateTimeOffset)"/>).</summary>
    /// <param name="text">The text; may be empty.</param>
    /// <param name="reference">The moment relative expressions are resolved against; wall-clock expressions are read in its offset.</param>
    /// <param name="language">English, French, German, Spanish, Portuguese, Italian, Dutch, Turkish, Hindi or Chinese. French and German read <c>04/05/2026</c> as 4 May and use the holidays of France and Germany.</param>
    /// <returns>The results in text order without overlaps, with UTF-16 offsets into <paramref name="text"/>. Empty when there are none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="text"/> is longer than <see cref="MaxLength"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is <c>default(DateTimeOffset)</c>, which is year 1 and almost certainly an unset value.</exception>
    /// <exception cref="NotSupportedException">No rules for <paramref name="language"/> are built in.</exception>
    public static IEnumerable<TemporalMatch> Dates(string text, DateTimeOffset reference, Language language) =>
        Recognizers.Recognizer.For(language).Recognize(Checked(text), ContextAt(reference)).OfType<TemporalMatch>();

    /// <summary>
    /// Resolves the first date, time or range in an English text to a stretch of time: <c>the first Monday after Easter 2027</c> is
    /// that day, <c>spring 2021</c> is March to May 2021 (approximate), <c>3pm</c> is that hour on the reference day.
    /// </summary>
    /// <param name="text">The text; may be empty.</param>
    /// <param name="reference">The moment relative expressions are resolved against; wall-clock expressions are read in its offset.</param>
    /// <returns>The range (start inclusive, end exclusive), or <see langword="null"/> when the text holds no date or time. Durations and recurrences are not dates and are skipped; use <see cref="Dates(string, DateTimeOffset)"/> for them.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="text"/> is longer than <see cref="MaxLength"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is <c>default(DateTimeOffset)</c>, which is year 1 and almost certainly an unset value.</exception>
    public static DateRange? ParseDate(string text, DateTimeOffset reference) =>
        Dates(text, reference).FirstOrDefault(d => d.Range is not null)?.Range;

    /// <summary>Resolves the first date, time or range in a text of the given language to a stretch of time (see <see cref="ParseDate(string, DateTimeOffset)"/>).</summary>
    /// <param name="text">The text; may be empty.</param>
    /// <param name="reference">The moment relative expressions are resolved against; wall-clock expressions are read in its offset.</param>
    /// <param name="language">English, French, German, Spanish, Portuguese, Italian, Dutch, Turkish, Hindi or Chinese.</param>
    /// <returns>The range (start inclusive, end exclusive), or <see langword="null"/> when the text holds no date or time.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="text"/> is longer than <see cref="MaxLength"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is <c>default(DateTimeOffset)</c>, which is year 1 and almost certainly an unset value.</exception>
    /// <exception cref="NotSupportedException">No rules for <paramref name="language"/> are built in.</exception>
    public static DateRange? ParseDate(string text, DateTimeOffset reference, Language language) =>
        Dates(text, reference, language).FirstOrDefault(d => d.Range is not null)?.Range;

    // A reference date of default(DateTimeOffset) is 1 January of year 1: always a mistake (a forgotten value), so it is refused.
    private static RecognitionContext ContextAt(DateTimeOffset reference)
    {
        if (reference == default)
        {
            throw new ArgumentException("The reference date is default(DateTimeOffset), which is year 1 and almost certainly unset. Pass the moment relative expressions such as \"tomorrow\" are resolved against, for example DateTimeOffset.UtcNow.", nameof(reference));
        }

        return new RecognitionContext(reference);
    }

    // Every recognizer verb refuses a text over Nlp.MaxLength; the low-level Recognizer has no limit of its own.
    private static string Checked(string text)
    {
        CheckLength(text);
        return text;
    }

    /// <summary>Embeds a text as a sentence vector with the default embedding model.</summary>
    /// <param name="text">The text; may be empty. Text beyond the model's maximum sequence length is cut off.</param>
    /// <returns>A new array; the default model's vectors have unit length, so the dot product of two is their cosine similarity.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No embedding package is referenced; the message names <c>RecursiveAI.OneNlp.Models.Embeddings</c>.</exception>
    public static float[] Embed(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return EmbedderPackages.Get().Embed(text);
    }

    /// <summary>Embeds many texts with the default embedding model, lazily and in batches.</summary>
    /// <param name="texts">The texts; enumerated once.</param>
    /// <returns>One vector per text, in order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="texts"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No embedding package is referenced; the message names <c>RecursiveAI.OneNlp.Models.Embeddings</c>.</exception>
    public static IEnumerable<float[]> EmbedMany(IEnumerable<string> texts)
    {
        ArgumentNullException.ThrowIfNull(texts);
        return EmbedderPackages.Get().EmbedMany(texts);
    }
}
