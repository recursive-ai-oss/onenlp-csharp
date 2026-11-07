namespace RecursiveAI.OneNlp.Tests;

public class SentencizerLanguageTests
{
    private const string Text = "Mr. Smith doesn't know e-mail. He cannot go to the U.S. today!";

    [Fact]
    public void ASentencizerAddedByNameUsesThePipelinesLanguage()
    {
        using Pipeline english = Pipeline.Blank(Language.English).Add("sentencizer");
        Assert.Equal(["Mr. Smith doesn't know e-mail.", "He cannot go to the U.S. today!"], english.Process(Text).Sentences.Select(s => s.Text));

        // Settings that name the data win over the pipeline's language.
        using Pipeline none = Pipeline.Blank(Language.English).Add("sentencizer", new Dictionary<string, string> { ["data"] = "none" });
        Assert.Equal(3, none.Process(Text).Sentences.Count);

        // No language, neutral data: Unicode's boundaries.
        using Pipeline unknown = Pipeline.Blank().Add("sentencizer");
        Assert.Equal(3, unknown.Process(Text).Sentences.Count);
    }
}
