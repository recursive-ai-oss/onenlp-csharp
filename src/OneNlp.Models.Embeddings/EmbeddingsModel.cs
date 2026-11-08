using System.Text;
using RecursiveAI.OneNlp.Neural.Embeddings;

namespace RecursiveAI.OneNlp.Models.Embeddings;

/// <summary>
/// The default English sentence embedder: BAAI <c>bge-small-en-v1.5</c> (MIT; 33M parameters, 384 dimensions, CLS pooling, unit-length
/// vectors, 512 tokens), exported to ONNX from the authors' published weights and run on OneModel. Nothing is trained here; see
/// <c>PROVENANCE.md</c> in the package.
/// </summary>
/// <remarks>
/// Referencing this package is all <see cref="Nlp.Embed(string)"/> needs: the package adds a module initializer to the project that
/// references it, which calls <see cref="Register"/>. Nothing is downloaded and nothing is discovered by reflection. The weights are read on first use.
/// The model was trained for retrieval with a query instruction; for symmetric similarity no instruction is used, which is what this embedder does.
/// For the search-query side of retrieval, prefix queries with <see cref="QueryInstruction"/>.
/// </remarks>
public static class EmbeddingsModel
{
    /// <summary>The NuGet package id.</summary>
    public const string PackageId = "RecursiveAI.OneNlp.Models.Embeddings";

    /// <summary>The instruction the model's authors recommend putting in front of a short search query when it is matched against passages.</summary>
    public const string QueryInstruction = "Represent this sentence for searching relevant passages: ";

    /// <summary>Makes <see cref="Nlp.Embed(string)"/> use this model. Safe to call more than once and from any thread. Needed only when the project that references the package cannot compile the module initializer the package adds.</summary>
    public static void Register() => EmbedderPackages.Register(PackageId, Load, priority: 0);

    /// <summary>Loads a new copy of the embedder from the weights in this assembly.</summary>
    /// <returns>An embedder the caller disposes. Several loaded copies share nothing.</returns>
    /// <exception cref="InvalidOperationException">This assembly was built without its weights (a development build).</exception>
    /// <exception cref="InvalidDataException">A weights file is damaged.</exception>
    public static SentenceEmbedder Load()
    {
        System.Reflection.Assembly asm = typeof(EmbeddingsModel).Assembly;
        EmbeddingConfig config = EmbeddingConfig.Parse(Text(asm, "modules.json"), Text(asm, "pooling.json"), Text(asm, "sentence_bert_config.json"));
        using Stream tokenizer = Open(asm, "tokenizer.json");
        Stream onnx = Open(asm, "bge-small-en-v1.5.onnx");
        return SentenceEmbedder.Load(onnx, tokenizer, config, leaveOpen: false);
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
