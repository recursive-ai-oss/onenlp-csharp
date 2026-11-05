using RecursiveAI.OneNlp.Segmentation;

namespace RecursiveAI.OneNlp.Tests;

// The German tokenization data (SegmentationData.German): the rules are those of the UD German guidelines and the German-GSD treebank.
public class GermanSegmentationTests
{
    private static readonly Pipeline German = Pipeline.Blank(Language.German).Add(SentencizerComponent.For(Language.German));

    private static string[] Words(string text) => [.. German.Process(text).Select(t => t.ToString())];

    [Theory]
    // A preposition and article that have grown together are two pieces (the words are "zu" + "dem"; docs/segmentation.md).
    [InlineData("Wir gehen zum Arzt, zur Bank und ins Kino.", new[] { "Wir", "gehen", "zu", "m", "Arzt", ",", "zu", "r", "Bank", "und", "in", "s", "Kino", "." })]
    [InlineData("Im Haus am See", new[] { "I", "m", "Haus", "a", "m", "See" })]
    [InlineData("vom Berg beim Essen", new[] { "vo", "m", "Berg", "be", "im", "Essen" })]
    // Abbreviations keep their full stop; initials and z. B. too.
    [InlineData("Dr. Meier und Prof. Schulz, z.B. in St. Gallen.", new[] { "Dr.", "Meier", "und", "Prof.", "Schulz", ",", "z.B.", "in", "St.", "Gallen", "." })]
    [InlineData("Johann S. Bach", new[] { "Johann", "S.", "Bach" })]
    // An ordinal number is one token; a number that ends a sentence is two.
    [InlineData("Am 3. Oktober war es so.", new[] { "A", "m", "3.", "Oktober", "war", "es", "so", "." })]
    [InlineData("Er wurde 1990 geboren. Danach zog er um.", new[] { "Er", "wurde", "1990", "geboren", ".", "Danach", "zog", "er", "um", "." })]
    [InlineData("Im Jahr 2010. Dann kam es anders.", new[] { "I", "m", "Jahr", "2010", ".", "Dann", "kam", "es", "anders", "." })]
    // A hyphen between letters is a token of its own; between digits it stays.
    [InlineData("US-Soldaten und E-Mail, 1-2 Tage", new[] { "US", "-", "Soldaten", "und", "E", "-", "Mail", ",", "1-2", "Tage" })]
    // Marks: ! and ? are written one by one, `` and '' are quotation marks, and 's is split off.
    [InlineData("Nein!! Was?? ``Ja'' geht's", new[] { "Nein", "!", "!", "Was", "?", "?", "``", "Ja", "''", "geht", "'s" })]
    // Decimal comma and thousands point stay inside the number; so do dates.
    [InlineData("Es kostet 1.234,56 Euro am 24.12.2020.", new[] { "Es", "kostet", "1.234,56", "Euro", "a", "m", "24.12.2020", "." })]
    public void GermanRules(string text, string[] expected) => Assert.Equal(expected, Words(text));

    [Fact]
    public void ContractionPiecesCoverTheSurfaceFormWithOffsetsIntoTheInput()
    {
        const string text = "Zum Glück";
        Doc doc = German.Process(text);
        Assert.Equal(["Zu", "m", "Glück"], doc.Select(t => t.ToString()));
        Assert.Equal([(0, 2), (2, 3), (4, 9)], doc.Select(t => (t.Idx, t.EndIdx)));
    }

    [Fact]
    public void SentencesDoNotBreakAfterAnOrdinalOrATitle()
    {
        Assert.Single(German.Process("Am 3. Oktober kam Dr. Meier nach St. Gallen.").Sentences);
        Assert.Equal(2, German.Process("Das war 2010. Danach kam Dr. Meier.").Sentences.Count);
    }

    [Fact]
    public void GermanDataIsNamedAndFoundByLanguage()
    {
        Assert.Same(SegmentationData.German, SegmentationData.ByName("german"));
        Assert.Equal("german", SegmentationData.For(Language.German).Name);
        Assert.Equal("english", SegmentationData.For(Language.English).Name);
        Assert.Equal("neutral", SegmentationData.For(Language.French).Name);
    }

    [Fact]
    public void MissingGermanModelNamesTheGermanPackage()
    {
        InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => ModelPackages.Get(Language.German));
        Assert.Contains("RecursiveAI.OneNlp.Models.German", e.Message, StringComparison.Ordinal);
        Assert.Contains("RecursiveAI.OneNlp.Models.Multilingual", e.Message, StringComparison.Ordinal);
    }
}
