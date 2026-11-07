namespace RecursiveAI.OneNlp.Models.Multilingual.Encoder;

/// <summary>One file of the distilled accurate-tier weights, embedded in this assembly (see RecursiveAI.OneNlp.Models.Multilingual).</summary>
public static class Part
{
    /// <summary>The name of the weights file this part carries.</summary>
    public const string FileName = "student-multilingual.onnx";

    /// <summary>Opens the file for reading.</summary>
    /// <returns>A readable, seekable stream over the embedded bytes. The caller disposes it.</returns>
    /// <exception cref="InvalidOperationException">The package was built without its weights.</exception>
    public static Stream Open() =>
        typeof(Part).Assembly.GetManifestResourceStream("weights/" + FileName)
        ?? throw new InvalidOperationException("This build of Encoder carries no weights; use the packed package.");
}
