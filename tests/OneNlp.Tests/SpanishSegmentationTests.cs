using RecursiveAI.OneNlp.Segmentation;

namespace RecursiveAI.OneNlp.Tests;

// The Spanish tokenization data (SegmentationData.Spanish): the rules follow the UD Spanish guidelines and the AnCora treebank.
public class SpanishSegmentationTests
{
    private static readonly Pipeline Spanish = Pipeline.Blank(Language.Spanish).Add(SentencizerComponent.For(Language.Spanish));

    private static string[] Words(string text) => [.. Spanish.Process(text).Select(t => t.ToString())];

    [Theory]
    // del and al are two pieces (the words are de + el and a + el), cut like the CoNLL-U reader cuts a surface form its words do not spell.
    [InlineData("Vengo del mercado y voy al cine.", new[] { "Vengo", "de", "l", "mercado", "y", "voy", "a", "l", "cine", "." })]
    [InlineData("Del Al DEL", new[] { "De", "l", "A", "l", "DE", "L" })]
    // A verb with enclitic pronouns is one piece for the verb and one for each pronoun.
    [InlineData("Quiero verlo y dárselo.", new[] { "Quiero", "ver", "lo", "y", "dár", "se", "lo", "." })]
    [InlineData("Va a conseguirlo para acercarse.", new[] { "Va", "a", "conseguir", "lo", "para", "acercar", "se", "." })]
    // With the accent of the stress the words do not spell the surface form, so the pieces are cut in equal parts (situándose is situando + se).
    [InlineData("situándose", new[] { "situá", "ndose" })]
    // Words that only look like a verb and a pronoun stay whole.
    [InlineData("La parte fuerte de Carlos es la muerte.", new[] { "La", "parte", "fuerte", "de", "Carlos", "es", "la", "muerte", "." })]
    [InlineData("los primeros números", new[] { "los", "primeros", "números" })]
    // Inverted marks are tokens of their own.
    [InlineData("¿Qué dices? ¡Nada!", new[] { "¿", "Qué", "dices", "?", "¡", "Nada", "!" })]
    // Initials, titles and fig. keep their full stop; thousands points and decimal commas stay in the number; hyphenated names stay whole.
    [InlineData("W. Smith y el Sr. Ruiz-Mateos, fig. 3, 2.000 euros y 3,5 euros.", new[] { "W.", "Smith", "y", "el", "Sr.", "Ruiz-Mateos", ",", "fig.", "3", ",", "2.000", "euros", "y", "3,5", "euros", "." })]
    public void SpanishRules(string text, string[] expected) => Assert.Equal(expected, Words(text));

    [Fact]
    public void PiecesCoverTheSurfaceFormWithOffsetsIntoTheInput()
    {
        const string text = "Voy al cine";
        Doc doc = Spanish.Process(text);
        Assert.Equal(["Voy", "a", "l", "cine"], doc.Select(t => t.ToString()));
        Assert.Equal([(0, 3), (4, 5), (5, 6), (7, 11)], doc.Select(t => (t.Idx, t.EndIdx)));
    }

    [Theory]
    [InlineData("del", new[] { "de", "el" })]
    [InlineData("Al", new[] { "A", "el" })]
    [InlineData("dándoselo", new[] { "dando", "se", "lo" })]
    [InlineData("verse", new[] { "ver", "se" })]
    [InlineData("encontrárnoslas", new[] { "encontrar", "nos", "las" })]
    [InlineData("señala", null)]
    [InlineData("reírse", new[] { "reír", "se" })]
    [InlineData("irse", new[] { "ir", "se" })]
    [InlineData("arte", null)]
    [InlineData("CONSEGUIRLO", new[] { "CONSEGUIR", "LO" })]
    public void MultiwordWords(string token, string[]? expected)
    {
        Doc doc = Spanish.Process(token);
        string[]? words = null;
        if (SegmentationData.Spanish.TryMatchMultiword(doc, 0, out int count, out words))
        {
            Assert.Equal(doc.Length, count);
        }

        Assert.Equal(expected, words);
    }

    [Fact]
    public void OtherDataSetsHaveNoSpanishMultiwordTokens()
    {
        Doc doc = Spanish.Process("del verlo");
        Assert.False(SegmentationData.English.TryMatchMultiword(doc, 0, out _, out _));
        Assert.False(SegmentationData.Neutral.TryMatchMultiword(doc, 0, out _, out _));
    }

    [Fact]
    public void SentencesDoNotBreakAfterATitle()
    {
        Assert.Single(Spanish.Process("El Sr. Ruiz llamó al Dr. Pérez.").Sentences);
        Assert.Equal(2, Spanish.Process("Llegó tarde. El Sr. Ruiz se fue.").Sentences.Count);
    }

    [Fact]
    public void SpanishDataIsNamedAndFoundByLanguage()
    {
        Assert.Same(SegmentationData.Spanish, SegmentationData.ByName("spanish"));
        Assert.Equal("spanish", SegmentationData.For(Language.Spanish).Name);
    }

    [Fact]
    public void MissingSpanishModelNamesTheSpanishPackage()
    {
        InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => ModelPackages.Get(Language.Spanish));
        Assert.Contains("RecursiveAI.OneNlp.Models.Spanish", e.Message, StringComparison.Ordinal);
        Assert.Contains("RecursiveAI.OneNlp.Models.Multilingual", e.Message, StringComparison.Ordinal);
    }
}
