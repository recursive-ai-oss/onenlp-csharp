namespace RecursiveAI.OneNlp.Models.Multilingual.Large.TokenTable1;

/// <summary>One slice of the base-size accurate-tier weights, embedded in this assembly (see RecursiveAI.OneNlp.Models.Multilingual.Large).</summary>
public static class Part
{
    /// <summary>The name of the weights file this part carries.</summary>
    public const string FileName = "token-table-00.bin";

    /// <summary>Opens the slice for reading.</summary>
    /// <returns>A readable, seekable stream over the embedded bytes. The caller disposes it.</returns>
    /// <exception cref="InvalidOperationException">The package was built without its weights.</exception>
    public static Stream Open() =>
        typeof(Part).Assembly.GetManifestResourceStream("weights/" + FileName)
        ?? throw new InvalidOperationException("This build of TokenTable1 carries no weights; use the packed package.");
}
