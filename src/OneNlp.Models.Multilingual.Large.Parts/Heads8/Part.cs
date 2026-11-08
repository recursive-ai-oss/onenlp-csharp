namespace RecursiveAI.OneNlp.Models.Multilingual.Large.Heads8;

/// <summary>Language heads embedded in this part of the base-size accurate-tier model.</summary>
public static class Part
{
    /// <summary>Opens the embedded heads for an ISO language code. The caller disposes the returned seekable stream.</summary>
    /// <param name="language">The language code assigned to this part.</param>
    /// <exception cref="InvalidOperationException">No embedded weights exist for this code.</exception>
    public static Stream Open(string language) =>
        typeof(Part).Assembly.GetManifestResourceStream("weights/heads/" + language + ".onenlp")
        ?? throw new InvalidOperationException("No heads for " + language + "; use the packed model package.");
}
