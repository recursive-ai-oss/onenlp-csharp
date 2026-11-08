namespace RecursiveAI.OneNlp.Models.Multilingual.Large.Heads1;

/// <summary>Language heads supplied as copied assets by this part of the base-size accurate-tier model.</summary>
public static class Part
{
    /// <summary>Opens the copied heads for an assigned language code. The caller disposes the returned seekable stream.</summary>
    /// <param name="language">The language code assigned to this part.</param>
    /// <exception cref="ArgumentException">The language is not assigned to this part.</exception>
    /// <exception cref="InvalidOperationException">The model assets were not copied with the application.</exception>
    /// <exception cref="IOException">The asset cannot be read.</exception>
    public static Stream Open(string language)
    {
        string file = language switch
        {
            "af" => "af.onenlp",
            "as" => "as.onenlp",
            "bor" => "bor.onenlp",
            "bxr" => "bxr.onenlp",
            "ca" => "ca.onenlp",
            "ckb" => "ckb.onenlp",
            "cop" => "cop.onenlp",
            "cs" => "cs.onenlp",
            "cy" => "cy.onenlp",
            "da" => "da.onenlp",
            _ => throw new ArgumentException("The language is not assigned to this part.", nameof(language)),
        };
        string path = Path.Combine(AppContext.BaseDirectory, "weights", "multilingual-large", "heads", file);
        return File.Exists(path) ? File.OpenRead(path)
            : throw new InvalidOperationException("Model assets are missing; copy the complete publish directory, including weights/multilingual-large.");
    }
}
