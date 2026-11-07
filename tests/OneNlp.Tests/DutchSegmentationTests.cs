using RecursiveAI.OneNlp.Segmentation;

namespace RecursiveAI.OneNlp.Tests;

// The Dutch tokenization data (SegmentationData.Dutch): the rules are those of the UD Dutch guidelines and the Dutch-Alpino treebank.
public class DutchSegmentationTests
{
    private static readonly Pipeline Dutch = Pipeline.Blank(Language.Dutch).Add(SentencizerComponent.For(Language.Dutch));

    private static string[] Words(string text) => [.. Dutch.Process(text).Select(t => t.ToString())];

    [Theory]
    // Apostrophe words stay whole; short forms that start with an apostrophe are tokens of their own.
    [InlineData("Dat is zo'n auto's en Jan's fiets, z'n zus.", new[] { "Dat", "is", "zo'n", "auto's", "en", "Jan's", "fiets", ",", "z'n", "zus", "." })]
    [InlineData("Hij zegt 't en 's morgens 'n boek, 'm ook.", new[] { "Hij", "zegt", "'t", "en", "'s", "morgens", "'n", "boek", ",", "'m", "ook", "." })]
    [InlineData("Het was in '69 en 'ns ook.", new[] { "Het", "was", "in", "'69", "en", "'ns", "ook", "." })]
    // A quotation mark before another word is a token of its own.
    [InlineData("Hij zei 'ja' en ging.", new[] { "Hij", "zei", "'", "ja", "'", "en", "ging", "." })]
    // Hyphenated compounds and scores stay whole.
    [InlineData("De 21-jarige uit Zuid-Afrika won met 2-0.", new[] { "De", "21-jarige", "uit", "Zuid-Afrika", "won", "met", "2-0", "." })]
    // Abbreviations keep their full stop; so do dotted abbreviations and initials.
    [InlineData("Mr. Van Dam en drs. J. Bakker, o.a. de N.V. en dr. Smit.", new[] { "Mr.", "Van", "Dam", "en", "drs.", "J.", "Bakker", ",", "o.a.", "de", "N.V.", "en", "dr.", "Smit", "." })]
    // Numbers: thousands point, decimal comma, fractions, and amounts that end in a dash.
    [InlineData("Het kost 130.000,- of 5,- of 2,5 of 1/2.", new[] { "Het", "kost", "130.000,-", "of", "5,-", "of", "2,5", "of", "1/2", "." })]
    // Runs of one mark are one token; ! and ? stay single.
    [InlineData("Wat?! ,,Ja'' ...", new[] { "Wat", "?", "!", ",,", "Ja", "''", "..." })]
    public void DutchRules(string text, string[] expected) => Assert.Equal(expected, Words(text));

    [Fact]
    public void ApostropheWordsKeepOffsetsIntoTheInput()
    {
        const string text = "Dat 's goed";
        Doc doc = Dutch.Process(text);
        Assert.Equal([(0, 3), (4, 6), (7, 11)], doc.Select(t => (t.Idx, t.EndIdx)));
    }

    [Fact]
    public void SentencesDoNotBreakAfterATitleOrAnInitial()
    {
        Assert.Single(Dutch.Process("Gisteren sprak mr. Van Dam met J. Bakker.").Sentences);
        Assert.Equal(2, Dutch.Process("Het was 2010. Daarna kwam Bakker.").Sentences.Count);
    }

    [Fact]
    public void DutchDataIsNamedAndFoundByLanguage()
    {
        Assert.Same(SegmentationData.Dutch, SegmentationData.ByName("dutch"));
        Assert.Equal("dutch", SegmentationData.For(Language.Dutch).Name);
        Assert.Equal("german", SegmentationData.For(Language.German).Name);
    }

    [Fact]
    public void MissingDutchModelNamesTheDutchPackage()
    {
        ModelNotAvailableException e = Assert.Throws<ModelNotAvailableException>(() => ModelPackages.Get(Language.Dutch));
        Assert.Contains("RecursiveAI.OneNlp.Models.Dutch", e.Message);
    }
}
