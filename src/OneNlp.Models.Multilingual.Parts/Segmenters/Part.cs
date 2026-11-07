namespace RecursiveAI.OneNlp.Models.Multilingual.Segmenters;

/// <summary>The trained tokenizers of the distilled accurate-tier model, embedded in this assembly (see RecursiveAI.OneNlp.Models.Multilingual).</summary>
public static class Part
{
    /// <summary>The names of the files this part carries: a saved pipeline that holds only a tokenizer, for each language that needs one.</summary>
    public static IReadOnlyList<string> FileNames { get; } = ["zh.onenlp", "tr.onenlp"];

    /// <summary>Opens one of the files for reading.</summary>
    /// <param name="fileName">One of <see cref="FileNames"/>.</param>
    /// <returns>A readable, seekable stream over the embedded bytes. The caller disposes it.</returns>
    /// <exception cref="InvalidOperationException">The package was built without its weights.</exception>
    public static Stream Open(string fileName) =>
        typeof(Part).Assembly.GetManifestResourceStream("weights/" + fileName)
        ?? throw new InvalidOperationException("This build of Segmenters carries no weights; use the packed package.");
}
