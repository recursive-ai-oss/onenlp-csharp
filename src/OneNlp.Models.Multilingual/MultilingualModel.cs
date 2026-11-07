using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Encoders;

namespace RecursiveAI.OneNlp.Models.Multilingual;

/// <summary>
/// The distilled accurate-tier model: one six-layer, 384-wide transformer encoder (a student of mmBERT-base, fine-tuned on the treebanks)
/// shared by the heads of nine languages. Each language's pipeline tokenizes, tags (UPOS, morphology and, where the treebank has it, XPOS),
/// lemmatizes, parses dependencies, sets sentence starts and finds entities where entity data exists. The package also carries an
/// open-type entity recognizer (entity types named at run time) on the frozen student.
/// </summary>
/// <remarks>
/// <para>
/// Referencing this package is all the <see cref="Nlp"/> verbs need: the package adds a module initializer to the project that references it, which
/// calls <see cref="Register"/>. The model is the <em>accurate tier</em> of each language: <see cref="Nlp"/> uses it for a language when no
/// fast-tier package is referenced, and <see cref="Pipeline.Create(Language, Tier)"/> with <see cref="Tier.Accurate"/> selects it. Nothing is
/// downloaded and nothing is discovered by reflection. The weights are read the first time a verb needs one of the languages; the encoder is
/// then shared by all of them.
/// </para>
/// <para>
/// The weights live in embedded resources of this assembly (the heads, the tokenizer, the open-type head) and of two part assemblies (the two
/// encoders), so that every package stays under the size limit of nuget.org.
/// </para>
/// </remarks>
public static class MultilingualModel
{
    /// <summary>The NuGet package id.</summary>
    public const string PackageId = "RecursiveAI.OneNlp.Models.Multilingual";

    // The names the saved head files record for the encoder they read (SharedEncoder).
    private const string SharedName = "student-multilingual";
    private const string FrozenName = "student-frozen";

    private static readonly string[] Tokenized = ["zh", "tr"];

    private static readonly Lock Gate = new();
    private static TransformerEncoder? _encoder;
    private static TransformerEncoder? _frozen;

    /// <summary>The languages this package has heads for (ISO 639-1 codes).</summary>
    public static IReadOnlyList<string> Languages { get; } = ["en", "de", "fr", "es", "pt", "it", "nl", "tr", "zh"];

    /// <summary>
    /// Makes the <see cref="Nlp"/> verbs use this model for its languages (as the accurate tier, behind any fast-tier package) and for entity types
    /// named at run time. Safe to call more than once and from any thread; the registration the package adds to a project calls it. The weights are read on first use.
    /// </summary>
    public static void Register()
    {
        foreach (string code in Languages)
        {
            string language = code;
            ModelPackages.Register(Language.FromCode(language), Tier.Accurate, PackageId, () => Load(language), priority: 0);
        }

        ModelPackages.RegisterOpenEntities(PackageId, LoadOpenEntities);
    }

    /// <summary>Loads a new copy of one language's pipeline, over the shared encoder (loaded on the first call).</summary>
    /// <param name="language">One of <see cref="Languages"/>.</param>
    /// <returns>A new pipeline; the caller disposes it. The encoder it reads lives for the rest of the process.</returns>
    /// <exception cref="ArgumentException">The language has no heads in this package.</exception>
    /// <exception cref="InvalidOperationException">The package was built without its weights.</exception>
    /// <exception cref="InvalidDataException">A weights file is damaged.</exception>
    public static Pipeline Load(string language)
    {
        ArgumentNullException.ThrowIfNull(language);
        if (!Languages.Contains(language))
        {
            throw new ArgumentException($"{PackageId} has no model for '{language}'.", nameof(language));
        }

        EnsureEncoder();
        using Stream headsFile = Resource("heads/" + language + ".onenlp");
        Pipeline heads = PipelineFile.Load(headsFile);
        if (!Tokenized.Contains(language))
        {
            return heads;
        }

        // Chinese words and Turkish multiword tokens come from small trained models of their own, saved as pipelines that hold a tokenizer only.
        // The tokenizer moves to a pipeline with the heads; the heads' own components move with it, and the part pipeline is left undisposed,
        // since it owns the tokenizer (as the fast-tier packages do).
        using Stream tokenizerFile = Segmenters.Part.Open(language + ".onenlp");
        Pipeline tokenizer = PipelineFile.Load(tokenizerFile);
        var combined = new Pipeline(tokenizer.Tokenizer, Language.FromCode(language), tokenizer.Strings);
        foreach (IPipelineComponent component in heads.Components)
        {
            combined.Add(component);
        }

        return combined;
    }

    /// <summary>Loads a new copy of the open-type entity pipeline (entity types named at run time), over the frozen encoder (loaded on the first call).</summary>
    /// <returns>A new pipeline; the caller disposes it.</returns>
    /// <exception cref="InvalidOperationException">The package was built without its weights.</exception>
    /// <exception cref="InvalidDataException">A weights file is damaged.</exception>
    public static Pipeline LoadOpenEntities()
    {
        EnsureFrozenEncoder();
        using Stream head = Resource("open-entities.onenlp");
        return PipelineFile.Load(head);
    }

    private static Stream Resource(string name) =>
        typeof(MultilingualModel).Assembly.GetManifestResourceStream("weights/" + name)
        ?? throw new InvalidOperationException($"This build of {PackageId} carries no weights ('{name}'); use the packed package.");

    // The fine-tuned encoder reads the words of multiword tokens (French du as de le), as it was trained to.
    private static void EnsureEncoder()
    {
        lock (Gate)
        {
            if (_encoder is not null)
            {
                return;
            }

            using Stream onnx = Encoder.Part.Open();
            using Stream tokenizer = Resource("tokenizer.json");
            _encoder = TransformerEncoder.FromStreams(onnx, tokenizer, SubwordPooling.Mean, expandWordForms: true);
            SharedEncoder.Register(SharedName, _encoder);
        }
    }

    // The open-type head was trained on the original student, before fine-tuning, so it reads that one.
    private static void EnsureFrozenEncoder()
    {
        lock (Gate)
        {
            if (_frozen is not null)
            {
                return;
            }

            using Stream onnx = FrozenEncoder.Part.Open();
            using Stream tokenizer = Resource("tokenizer.json");
            _frozen = TransformerEncoder.FromStreams(onnx, tokenizer, SubwordPooling.Mean);
            SharedEncoder.Register(FrozenName, _frozen);
        }
    }
}
