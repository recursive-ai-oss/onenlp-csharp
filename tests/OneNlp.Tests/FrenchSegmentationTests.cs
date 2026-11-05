using RecursiveAI.OneNlp.Segmentation;

namespace RecursiveAI.OneNlp.Tests;

public class FrenchSegmentationTests
{
    private static readonly Pipeline French = Pipeline.Blank(Language.French).Add(SentencizerComponent.For(Language.French));

    private static string[] Words(string text) => [.. French.Process(text).Select(t => t.ToString())];

    [Theory]
    // Elision: the apostrophe stays with the elided word.
    [InlineData("L'homme qu'on voit d'ici n'est pas là", new[] { "L'", "homme", "qu'", "on", "voit", "d'", "ici", "n'", "est", "pas", "là" })]
    [InlineData("jusqu'au bout, lorsqu\u2019il pleut", new[] { "jusqu'", "a", "u", "bout", ",", "lorsqu\u2019", "il", "pleut" })]
    // Words with an apostrophe that UD French keeps whole.
    [InlineData("aujourd'hui quelqu'un l'on c'est-à-dire", new[] { "aujourd'hui", "quelqu'un", "l'on", "c'est-à-dire" })]
    [InlineData("qu'est-ce que c'est", new[] { "qu'", "est", "-ce", "que", "c'", "est" })]
    // Inversion clitics split at their hyphen; compounds and demonstratives stay whole.
    [InlineData("A-t-il vu? Dis-moi, peut-être celui-ci lui-même", new[] { "A", "-t-il", "vu", "?", "Dis", "-moi", ",", "peut-être", "celui-ci", "lui-même" })]
    [InlineData("Viendront-ils? Donne-le-lui", new[] { "Viendront", "-ils", "?", "Donne", "-le", "-lui" })]
    [InlineData("ce garçon-là et deux hommes-ci", new[] { "ce", "garçon", "-là", "et", "deux", "hommes", "-ci" })]
    [InlineData("un rendez-vous au-delà, week-end", new[] { "un", "rendez-vous", "au-delà", ",", "week-end" })]
    // Numbers: digit groups, negative numbers, hours.
    [InlineData("25 785 habitants, 2 001 mètres, 1 234 567 euros", new[] { "25 785", "habitants", ",", "2 001", "mètres", ",", "1 234 567", "euros" })]
    [InlineData("en 1999 100 fois", new[] { "en", "1999", "100", "fois" })]
    [InlineData("de -1 à -0,8 ; à 20h30 ou 23h", new[] { "de", "-1", "à", "-0,8", ";", "à", "20", "h", "30", "ou", "23", "h" })]
    [InlineData("le n° 12 de M. Dupont et Mme Martin", new[] { "le", "n°", "12", "de", "M.", "Dupont", "et", "Mme", "Martin" })]
    [InlineData("de V. Prebeg", new[] { "de", "V.", "Prebeg" })]
    public void FrenchRules(string text, string[] expected) => Assert.Equal(expected, Words(text));

    [Theory]
    // The contractions are cut into the pieces of the multiword token ("d" + "u" for "du"), which the treebank expands.
    [InlineData("Il parle du chat", new[] { "Il", "parle", "d", "u", "chat" })]
    [InlineData("Il va au marché et aux champs", new[] { "Il", "va", "a", "u", "marché", "et", "au", "x", "champs" })]
    [InlineData("auquel et desquelles", new[] { "auq", "uel", "et", "desqu", "elles" })]
    public void ContractionsAreCutIntoMultiwordPieces(string text, string[] expected) => Assert.Equal(expected, Words(text));

    [Theory]
    // "des" is a contraction of "de les" after a noun and the plural article after a verb, a preposition or punctuation.
    [InlineData("la plupart des gens", new[] { "la", "plupart", "de", "s", "gens" })]
    [InlineData("il a des idées", new[] { "il", "a", "des", "idées" })]
    [InlineData("avec des amis", new[] { "avec", "des", "amis" })]
    [InlineData("Des amis, des enfants", new[] { "Des", "amis", ",", "des", "enfants" })]
    public void DesDependsOnTheWordBeforeIt(string text, string[] expected) => Assert.Equal(expected, Words(text));

    [Fact]
    public void MultiwordTokensGiveTheirWords()
    {
        Doc doc = French.Process("Du pain au lait, des gens");
        var found = new List<string>();
        for (int i = 0; i < doc.Length; i++)
        {
            if (SegmentationData.French.TryMatchMultiword(doc, i, out int count, out string[]? words))
            {
                found.Add(string.Join('+', words));
                i += count - 1;
            }
        }

        Assert.Equal(["De+le", "à+le"], found);
    }

    [Fact]
    public void OtherDataNeverHasMultiwordTokens()
    {
        Doc doc = Pipeline.Blank(Language.English).Process("du au");
        Assert.False(SegmentationData.English.TryMatchMultiword(doc, 0, out _, out _));
    }

    [Fact]
    public void OffsetsPointIntoTheOriginalString()
    {
        const string text = "Qu'il parte du 25 785";
        Doc doc = French.Process(text);
        Assert.Equal(["Qu'", "il", "parte", "d", "u", "25 785"], doc.Select(t => t.ToString()));
        Assert.All(doc, t => Assert.Equal(t.ToString(), text.Substring(t.Idx, t.Length)));
        Assert.Equal(SegmentationData.French, SegmentationData.ByName("french"));
    }

    [Fact]
    public void NoSentenceStartsAfterAFrenchAbbreviation()
    {
        Doc doc = French.Process("Rencontrez M. Dupont demain. Il viendra.");
        Assert.Equal(2, doc.Sentences.Count);
    }
}
