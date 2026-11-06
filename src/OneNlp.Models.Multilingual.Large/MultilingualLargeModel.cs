using System.Text.Json;
using RecursiveAI.OneModel.Training;
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Encoders;

namespace RecursiveAI.OneNlp.Models.Multilingual.Large;

/// <summary>
/// The base-size accurate-tier model: one fine-tuned mmBERT-base encoder (Marone et al. 2025) shared by the heads of nine languages. Each
/// language's pipeline tokenizes, tags (UPOS, morphology and, where the treebank has it, XPOS), lemmatizes, parses dependencies and sets
/// sentence starts, and finds entities where entity data exists.
/// </summary>
/// <remarks>
/// <para>
/// Referencing this package is all the <see cref="Nlp"/> verbs need: the package adds a module initializer to the project that references it, which
/// calls <see cref="Register"/>. The model is a <em>fallback</em>: a language that also has a fast-tier package uses that package's pipeline.
/// Nothing is downloaded and nothing is discovered by reflection. The 1.3 GB of weights are read the first time a verb needs any of the
/// languages; the encoder is then shared by all of them.
/// </para>
/// <para>
/// The weights live in embedded resources of this assembly (the heads, the settings, the tokenizer) and of the part assemblies (the encoder's layers in two
/// halves and the token table in four row ranges), so that every package stays under the size limit of nuget.org.
/// </para>
/// </remarks>
public static class MultilingualLargeModel
{
    /// <summary>The NuGet package id.</summary>
    public const string PackageId = "RecursiveAI.OneNlp.Models.Multilingual.Large";

    /// <summary>The name under which the shared encoder is registered for the saved pipelines.</summary>
    internal const string SharedName = "multilingual-large";

    private static readonly Lazy<TrainableTransformerEncoder> Encoder = new(BuildEncoder, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The languages this package has heads for (ISO 639-1 codes).</summary>
    public static IReadOnlyList<string> Languages { get; } = ["en", "de", "fr", "es", "pt", "it", "nl", "tr", "zh"];

    /// <summary>
    /// Makes the <see cref="Nlp"/> verbs use this model for its languages when no fast-tier package registered one. Safe to call more than once; the
    /// registration the package adds to a project calls it. The weights are read on first use.
    /// </summary>
    public static void Register()
    {
        foreach (string code in Languages)
        {
            string language = code;
            ModelPackages.RegisterFallback(Language.FromCode(language), PackageId, () => Load(language));
        }
    }

    /// <summary>Loads the pipeline of one language, with the shared encoder (loaded on the first call).</summary>
    /// <param name="language">One of <see cref="Languages"/>.</param>
    /// <returns>A new pipeline; the caller disposes it. The encoder it reads lives for the rest of the process.</returns>
    /// <exception cref="ArgumentException">The language has no heads in this package.</exception>
    /// <exception cref="InvalidOperationException">The package was built without its weights.</exception>
    public static Pipeline Load(string language)
    {
        ArgumentNullException.ThrowIfNull(language);
        if (!Languages.Contains(language))
        {
            throw new ArgumentException($"{PackageId} has no model for '{language}'.", nameof(language));
        }

        _ = Encoder.Value;
        using Stream heads = Resource("heads/" + language + ".onenlp");
        return PipelineFile.Load(heads);
    }

    private static Stream Resource(string name) =>
        typeof(MultilingualLargeModel).Assembly.GetManifestResourceStream("weights/" + name)
        ?? throw new InvalidOperationException($"This build of {PackageId} carries no weights ('{name}'); use the packed package.");

    private static TrainableTransformerEncoder BuildEncoder()
    {
        using Stream encoderJson = Resource("encoder.json");
        using JsonDocument description = JsonDocument.Parse(encoderJson);
        JsonElement shape = description.RootElement.GetProperty("shape");

        byte[] layers = ReadAll([Encoder1.Part.Open(), Encoder2.Part.Open()]);
        using var layerStream = new MemoryStream(layers, writable: false);
        using Stream tokenizer = Resource("tokenizer.json");
        Stream[] table = [TokenTable1.Part.Open(), TokenTable2.Part.Open(), TokenTable3.Part.Open(), TokenTable4.Part.Open()];
        try
        {
            TrainableTransformerEncoder encoder = TrainableTransformerEncoder.FromParts(
                description.RootElement.GetProperty("settings"),
                shape.GetProperty("vocabularySize").GetInt32(),
                shape.GetProperty("width").GetInt32(),
                shape.GetProperty("layers").GetInt32(),
                shape.GetProperty("feedForwardWidth").GetInt32(),
                tokenizer,
                table,
                layerStream);
            SharedEncoder.Register(SharedName, encoder);
            return encoder;
        }
        finally
        {
            foreach (Stream s in table)
            {
                s.Dispose();
            }
        }
    }

    // The halves of one file, joined. Bounds: the two parts are each under 250 MB, so the array is under 500 MB.
    private static byte[] ReadAll(Stream[] parts)
    {
        try
        {
            long total = 0;
            foreach (Stream s in parts)
            {
                total += s.Length;
            }

            byte[] all = new byte[checked((int)total)];
            int at = 0;
            foreach (Stream s in parts)
            {
                s.ReadExactly(all, at, (int)s.Length);
                at += (int)s.Length;
            }

            return all;
        }
        finally
        {
            foreach (Stream s in parts)
            {
                s.Dispose();
            }
        }
    }
}
