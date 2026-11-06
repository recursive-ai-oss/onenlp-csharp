using RecursiveAI.OneNlp.Segmentation;

namespace RecursiveAI.OneNlp.Tests;

// The Italian tokenization data (SegmentationData.Italian): the rules follow the UD Italian guidelines; the tables were counted on UD
// Italian-ParlaMint and Italian-MarkIT training and development files.
public class ItalianSegmentationTests
{
    private static readonly Pipeline Italian = Pipeline.Blank(Language.Italian).Add(SentencizerComponent.For(Language.Italian));

    private static string[] Words(string text) => [.. Italian.Process(text).Select(t => t.ToString())];

    [Theory]
    // Articulated prepositions are two pieces; the words (di + il) do not spell the surface form, so the surface is cut in nearly equal parts.
    [InlineData("Vengo dal mercato e vado alla casa.", new[] { "Vengo", "da", "l", "mercato", "e", "vado", "al", "la", "casa", "." })]
    // Elision: the apostrophe stays with the first word; an articulated preposition before an elision is cut too.
    [InlineData("L'amico dell'altro c'è.", new[] { "L'", "amico", "del", "l'", "altro", "c'", "è", "." })]
    [InlineData("Un'altra volta.", new[] { "Un'", "altra", "volta", "." })]
    // An infinitive or gerund with an enclitic pronoun is one piece for the verb and one for the pronoun.
    [InlineData("Vuole farlo e vedersi.", new[] { "Vuole", "far", "lo", "e", "veder", "si", "." })]
    [InlineData("Essendoci tutti.", new[] { "Essendo", "ci", "tutti", "." })]
    // Words that only look like a verb with an enclitic stay whole.
    [InlineData("Lui parla di carne.", new[] { "Lui", "parla", "di", "carne", "." })]
    // The elided article before a space (typeset transcripts) keeps its apostrophe.
    [InlineData("L' idea c' è.", new[] { "L'", "idea", "c'", "è", "." })]
    // The partitive article is one word after a verb, the contraction after a noun.
    [InlineData("Ci sono dei bambini e l'elenco dei bambini.", new[] { "Ci", "sono", "dei", "bambini", "e", "l'", "elenco", "de", "i", "bambini", "." })]
    // Several pronouns.
    [InlineData("Non accorgercene.", new[] { "Non", "accorger", "ce", "ne", "." })]
    // Two sentences.
    [InlineData("Vado a Roma. Torno domani.", new[] { "Vado", "a", "Roma", ".", "Torno", "domani", "." })]
    public void Tokens(string text, string[] expected) => Assert.Equal(expected, Words(text));

    [Theory]
    [InlineData("del", new[] { "di", "il" })]
    [InlineData("Nell'", new[] { "In", "l'" })]
    [InlineData("farsi", new[] { "far", "si" })]
    [InlineData("vederlo", new[] { "veder", "lo" })]
    [InlineData("parla", null)]
    [InlineData("carne", null)]
    [InlineData("mari", null)]
    [InlineData("diversi", null)]
    [InlineData("parti", null)]
    [InlineData("accorgercene", new[] { "accorger", "ce", "ne" })]
    [InlineData("farci", new[] { "far", "ci" })]
    public void MultiwordWords(string token, string[]? expected)
    {
        Doc doc = Italian.Process(token);
        string[]? words = null;
        if (SegmentationData.Italian.TryMatchMultiword(doc, 0, out int count, out words))
        {
            Assert.Equal(doc.Length, count);
        }

        Assert.Equal(expected, words);
    }

    [Fact]
    public void NameRoundTrip() => Assert.Same(SegmentationData.Italian, SegmentationData.ByName("italian"));
}
