using RecursiveAI.OneNlp.Segmentation;

namespace RecursiveAI.OneNlp.Tests;

// The Portuguese tokenization data (SegmentationData.Portuguese): the rules are those of UD Portuguese-Bosque, counted on its training and development files.
public class PortugueseSegmentationTests
{
    private static readonly Pipeline Portuguese = Pipeline.Blank(Language.Portuguese).Add(SentencizerComponent.For(Language.Portuguese));

    private static string[] Words(string text) => [.. Portuguese.Process(text).Select(t => t.ToString())];

    [Theory]
    // A preposition and what follows it that have grown together are two pieces (the words are "de" + "o"; docs/segmentation.md).
    [InlineData("Ele foi ao mercado do Pedro.", new[] { "Ele", "foi", "a", "o", "mercado", "d", "o", "Pedro", "." })]
    [InlineData("pelo país pelas ruas", new[] { "pe", "lo", "país", "pel", "as", "ruas" })]
    [InlineData("Nesse dia, nele e dela", new[] { "Nes", "se", "dia", ",", "ne", "le", "e", "de", "la" })]
    // "à" is one character for two words, so it is one token.
    [InlineData("À noite, às cinco.", new[] { "À", "noite", ",", "à", "s", "cinco", "." })]
    // Hyphenated words stay whole, but the pronoun after a verb is split off with its hyphen; the currency symbols US$ and R$ are one token.
    [InlineData("Trata-se de quinta-feira e norte-americano.", new[] { "Trata", "-se", "de", "quinta-feira", "e", "norte-americano", "." })]
    [InlineData("Quero dá-lo, dar-lhe-á e fazê-lo.", new[] { "Quero", "dá", "-lo", ",", "dar-lhe-á", "e", "fazê", "-lo", "." })]
    [InlineData("Pagou US$ 100 e R$ 5,50.", new[] { "Pagou", "US$", "100", "e", "R$", "5,50", "." })]
    // Abbreviations keep their full stop; so do initials.
    [InlineData("O Dr. Silva e J. Costa chegaram.", new[] { "O", "Dr.", "Silva", "e", "J.", "Costa", "chegaram", "." })]
    // A run of one mark is one token.
    [InlineData("Bom... talvez!!", new[] { "Bom", "...", "talvez", "!", "!" })]
    public void PortugueseRules(string text, string[] expected) => Assert.Equal(expected, Words(text));

    [Fact]
    public void ContractionPiecesCoverTheSurfaceFormWithOffsetsIntoTheInput()
    {
        const string text = "Do Brasil";
        Doc doc = Portuguese.Process(text);
        Assert.Equal(["D", "o", "Brasil"], doc.Select(t => t.ToString()));
        Assert.Equal([(0, 1), (1, 2), (3, 9)], doc.Select(t => (t.Idx, t.EndIdx)));
    }

    [Fact]
    public void SentencesDoNotBreakAfterATitle()
    {
        Assert.Single(Portuguese.Process("O Dr. Silva chegou ontem.").Sentences);
        Assert.Equal(2, Portuguese.Process("O Dr. Silva chegou. Depois saiu.").Sentences.Count);
    }

    [Fact]
    public void PortugueseDataIsNamedAndFoundByLanguage()
    {
        Assert.Same(SegmentationData.Portuguese, SegmentationData.ByName("portuguese"));
        Assert.Equal("portuguese", SegmentationData.For(Language.Portuguese).Name);
        Assert.Equal("german", SegmentationData.For(Language.German).Name);
    }

    [Fact]
    public void MissingPortugueseModelNamesThePortuguesePackage()
    {
        ModelNotAvailableException e = Assert.Throws<ModelNotAvailableException>(() => ModelPackages.Get(Language.Portuguese));
        Assert.Contains("RecursiveAI.OneNlp.Models.Portuguese", e.Message, StringComparison.Ordinal);
        Assert.Contains("RecursiveAI.OneNlp.Models.Multilingual", e.Message, StringComparison.Ordinal);
    }
}
