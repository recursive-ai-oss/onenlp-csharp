namespace RecursiveAI.OneNlp.Models.Embeddings.Multilingual.Shard3;

/// <summary>Part 3 of 3 of the multilingual-e5-small ONNX file. Used by <c>RecursiveAI.OneNlp.Models.Embeddings.Multilingual</c>; not meant to be called directly.</summary>
public static class Part
{
    /// <summary>Opens the embedded bytes of this part.</summary>
    /// <returns>A readable, seekable stream the caller disposes.</returns>
    /// <exception cref="InvalidOperationException">This assembly was built without its weights (a development build).</exception>
    public static Stream Open() =>
        typeof(Part).Assembly.GetManifestResourceStream("weights/model.onnx.part3")
            ?? throw new InvalidOperationException("This build of RecursiveAI.OneNlp.Models.Embeddings.Multilingual.Shard3 has no weights: it was built without the weights directory.");
}
