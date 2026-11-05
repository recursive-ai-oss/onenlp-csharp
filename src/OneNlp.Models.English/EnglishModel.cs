using RecursiveAI.OneNlp.Neural;

namespace RecursiveAI.OneNlp.Models.English;

/// <summary>
/// The English fast-tier model: one pipeline that tokenizes, splits sentences, tags (UPOS, XPOS, morphology), lemmatizes, parses
/// dependencies and finds named entities (PER, ORG, LOC), from weights packed inside this assembly.
/// </summary>
/// <remarks>
/// Referencing this package is all the <see cref="Nlp"/> verbs need: the package adds a module initializer to the project that references it, which
/// calls <see cref="Register"/>. Nothing is downloaded and nothing is discovered by reflection. The weights are read on first use.
/// </remarks>
public static class EnglishModel
{
    /// <summary>The NuGet package id.</summary>
    public const string PackageId = "RecursiveAI.OneNlp.Models.English";

    // One saved pipeline holding three separately trained parts, each with its own encoder: tagger, morphologizer and lemmatizer; parser; entity recognizer.
    private const string Weights = "english.onenlp";

    /// <summary>
    /// Makes the <see cref="Nlp"/> verbs use this model for English text. Safe to call more than once and from any thread. Needed only when the
    /// project that references the package cannot compile the module initializer the package adds (a non-C# project, for example).
    /// </summary>
    public static void Register() => ModelPackages.Register(Language.English, PackageId, Load);

    /// <summary>Loads a new copy of the English pipeline from the weights in this assembly, for use without <see cref="Nlp"/>.</summary>
    /// <returns>A pipeline the caller owns and disposes. Several loaded copies share nothing.</returns>
    /// <exception cref="InvalidOperationException">This assembly was built without its weights (a development build).</exception>
    /// <exception cref="InvalidDataException">A weights file is damaged.</exception>
    public static Pipeline Load()
    {
        using Stream stream = typeof(EnglishModel).Assembly.GetManifestResourceStream("weights/" + Weights)
            ?? throw new InvalidOperationException($"This build of {PackageId} has no weights ('{Weights}'): it was built without the weights directory.");
        return PipelineFile.Load(stream);
    }
}
