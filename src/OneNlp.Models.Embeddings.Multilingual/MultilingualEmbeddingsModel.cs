using System.Text;
using RecursiveAI.OneNlp.Neural.Embeddings;

namespace RecursiveAI.OneNlp.Models.Embeddings.Multilingual;

/// <summary>
/// The multilingual sentence embedder: <c>intfloat/multilingual-e5-small</c> (MIT; 118M parameters, 384 dimensions, mean pooling, unit-length
/// vectors, 512 tokens, about 100 languages), exported to ONNX from the authors' published weights and run on OneModel. Nothing is trained
/// here; see <c>PROVENANCE.md</c> in the package.
/// </summary>
/// <remarks>
/// Referencing this package makes <see cref="Nlp.Embed(string)"/> use this model in place of the English package, when both are referenced
/// (this package registers with a higher priority). To use the English model anyway, call <c>EmbeddingsModel.Load()</c> from
/// <c>RecursiveAI.OneNlp.Models.Embeddings</c>. The package adds a module initializer to the project that references it, which calls
/// <see cref="Register"/>. Nothing is downloaded and nothing is discovered by reflection. The weights are read on first use.
/// </remarks>
public static class MultilingualEmbeddingsModel
{
    /// <summary>The NuGet package id.</summary>
    public const string PackageId = "RecursiveAI.OneNlp.Models.Embeddings.Multilingual";

    /// <summary>Makes <see cref="Nlp.Embed(string)"/> use this model, ahead of the English package. Safe to call more than once and from any thread. Needed only when the project that references the package cannot compile the module initializer the package adds.</summary>
    public static void Register() => EmbedderPackages.Register(PackageId, () => Load(), priority: 1);

    /// <summary>Loads a new copy of the embedder from the weights in this package and its three shard packages.</summary>
    /// <returns>An embedder the caller disposes. Several loaded copies share nothing.</returns>
    /// <exception cref="InvalidOperationException">An assembly was built without its weights (a development build).</exception>
    /// <exception cref="InvalidDataException">A weights file is damaged.</exception>
    public static E5Embedder Load()
    {
        System.Reflection.Assembly asm = typeof(MultilingualEmbeddingsModel).Assembly;
        EmbeddingConfig config = EmbeddingConfig.Parse(Text(asm, "modules.json"), Text(asm, "pooling.json"), Text(asm, "sentence_bert_config.json"));
        using Stream onnx = new ConcatenatedStream([Shard1.Part.Open(), Shard2.Part.Open(), Shard3.Part.Open()]);
        using Stream tokenizer = Open(asm, "tokenizer.json");
        return new E5Embedder(SentenceEmbedder.Load(onnx, tokenizer, config));
    }

    private static Stream Open(System.Reflection.Assembly asm, string name) =>
        asm.GetManifestResourceStream("weights/" + name)
            ?? throw new InvalidOperationException($"This build of {PackageId} has no weights ('{name}'): it was built without the weights directory.");

    private static string Text(System.Reflection.Assembly asm, string name)
    {
        using Stream s = Open(asm, name);
        using var r = new StreamReader(s, Encoding.UTF8);
        return r.ReadToEnd();
    }
}
