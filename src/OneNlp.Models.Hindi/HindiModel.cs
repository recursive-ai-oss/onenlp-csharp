using System.Reflection;
using RecursiveAI.OneNlp.Neural;

namespace RecursiveAI.OneNlp.Models.Hindi;

/// <summary>
/// The Hindi fast-tier model: one pipeline that tokenizes, splits sentences, tags (UPOS and morphology), parses dependencies and finds named
/// entities (PER, LOC, ORG), from weights packed inside this assembly. It has no lemmatizer and no XPOS tagger: there is no allowed Hindi
/// treebank to learn them from, so <c>Token.Lemma</c> is the token's own text and <c>Token.Tag</c> stays empty.
/// </summary>
/// <remarks>
/// The weights were trained on Hindi web text that the accurate tier labelled by transfer from other languages (<c>PROVENANCE.md</c>), so
/// they are weaker than the models of languages with a treebank. Referencing this package is all the <see cref="Nlp"/> verbs need: the package adds a module initializer to the project that references it, which
/// calls <see cref="Register"/>. Nothing is downloaded and nothing is discovered by reflection. The weights are read on first use.
/// </remarks>
public static class HindiModel
{
    /// <summary>The NuGet package id.</summary>
    public const string PackageId = "RecursiveAI.OneNlp.Models.Hindi";

    // The three saved pipelines, in pipeline order: tagging (sentencizer, morphologizer), parser, entities.
    // Each has its own fast encoder; they are separate files because they are separate training parts of one recipe.
    private static readonly string[] Parts = ["tagging.onenlp", "parser.onenlp", "ner.onenlp"];

    /// <summary>
    /// Makes the <see cref="Nlp"/> verbs use this model for Hindi text. Safe to call more than once and from any thread. Needed only when the
    /// project that references the package cannot compile the module initializer the package adds (a non-C# project, for example).
    /// </summary>
    public static void Register() => ModelPackages.Register(Language.Hindi, PackageId, Load);

    /// <summary>Loads a new copy of the Hindi pipeline from the weights in this assembly, for use without <see cref="Nlp"/>.</summary>
    /// <returns>A pipeline the caller owns and disposes. Several loaded copies share nothing.</returns>
    /// <exception cref="InvalidOperationException">This assembly was built without its weights (a development build).</exception>
    /// <exception cref="InvalidDataException">A weights file is damaged.</exception>
    public static Pipeline Load()
    {
        Assembly assembly = typeof(HindiModel).Assembly;
        var loaded = new List<Pipeline>(Parts.Length);
        try
        {
            foreach (string part in Parts)
            {
                using Stream stream = assembly.GetManifestResourceStream("weights/" + part)
                    ?? throw new InvalidOperationException($"This build of {PackageId} has no weights ('{part}'): it was built without the weights directory.");
                loaded.Add(PipelineFile.Load(stream));
            }

            // The parts share one tokenizer and one string table, so token and label IDs mean the same to every component. Their
            // components move into the combined pipeline, which owns and disposes them; the part pipelines are not disposed.
            Pipeline first = loaded[0];
            var combined = new Pipeline(first.Tokenizer, Language.Hindi, first.Strings);
            foreach (Pipeline part in loaded)
            {
                foreach (IPipelineComponent component in part.Components)
                {
                    // The tagging recipe saves a rule sentencizer; the parser predicts sentence starts better on raw text (see the
                    // parser recipe's results), and it would otherwise be confined to the rule's sentences. It sets them itself.
                    if (component.Name == "sentencizer")
                    {
                        continue;
                    }

                    combined.Add(component);
                }
            }

            combined.Add(new FormLemmas());
            return combined;
        }
        catch
        {
            foreach (Pipeline part in loaded)
            {
                part.Dispose();
            }

            throw;
        }
    }
}
