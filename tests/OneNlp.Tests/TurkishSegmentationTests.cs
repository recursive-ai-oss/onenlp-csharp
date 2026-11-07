using RecursiveAI.OneNlp.Segmentation;

namespace RecursiveAI.OneNlp.Tests;

// The Turkish tokenization data (SegmentationData.Turkish): the rules are those of the UD Turkish-BOUN treebank, chosen from its training and development files.
public class TurkishSegmentationTests
{
    private static readonly Pipeline Turkish = Pipeline.Blank(Language.Turkish).Add(SentencizerComponent.For(Language.Turkish));

    private static string[] Words(string text) => [.. Turkish.Process(text).Select(t => t.ToString())];

    [Theory]
    // The apostrophe that joins a suffix to a name or a number stays inside the token.
    [InlineData("Ankara'da 1882'den beri Ali'nin evinde.", new[] { "Ankara'da", "1882'den", "beri", "Ali'nin", "evinde", "." })]
    // A hyphen is a token of its own, between letters and between digits.
    [InlineData("Türk-Arap ilişkileri 1-2 gün", new[] { "Türk", "-", "Arap", "ilişkileri", "1", "-", "2", "gün" })]
    // A full stop that closes a quotation stays with the closing quote.
    [InlineData("Dedi ki \"gel.\" Sonra gitti.", new[] { "Dedi", "ki", "\"", "gel", ".\"", "Sonra", "gitti", "." })]
    // An ordinal before a month is one token; a year that ends a sentence is two.
    [InlineData("3. Ocak ve 2001. Sonra", new[] { "3.", "Ocak", "ve", "2001", ".", "Sonra" })]
    // A copula or -ken spelled onto its word is a word of its own (ederken is eder + ken), by the table of endings.
    [InlineData("Eve giderken", new[] { "Eve", "gider", "ken" })]
    public void TurkishRules(string text, string[] expected) => Assert.Equal(expected, Words(text));

    [Fact]
    public void CutWordsAreFoundAgainAsMultiwordTokens()
    {
        Doc doc = Turkish.Process("O bunu yaparken gülüyordu, ama.");
        int at = -1;
        for (int i = 0; i + 1 < doc.Length; i++)
        {
            if (doc[i].ToString() == "yapar" && doc[i + 1].ToString() == "ken")
            {
                at = i;
            }
        }

        Assert.True(at >= 0, string.Join(' ', doc.Select(t => t.ToString())));
        Assert.True(SegmentationData.Turkish.TryMatchMultiword(doc, at, out int count, out string[]? words));
        Assert.Equal(2, count);
        Assert.Equal(["yapar", "ken"], words);

        // Two neighbouring tokens that were not cut from one word are not a multiword token (a word and the next word, a word and its comma).
        Assert.False(SegmentationData.Turkish.TryMatchMultiword(doc, 0, out _, out _));
        for (int i = 0; i + 1 < doc.Length; i++)
        {
            if (doc[i].ToString() == "bunu")
            {
                Assert.False(SegmentationData.Turkish.TryMatchMultiword(doc, i, out _, out _));
            }
        }
    }

    [Fact]
    public void CutPiecesCoverTheSurfaceFormWithOffsetsIntoTheInput()
    {
        const string text = "Eve giderken";
        Doc doc = Turkish.Process(text);
        Assert.Equal([(0, 3), (4, 9), (9, 12)], doc.Select(t => (t.Idx, t.EndIdx)));
    }

    [Fact]
    public void TurkishDataIsNamedAndFoundByLanguage()
    {
        Assert.Same(SegmentationData.Turkish, SegmentationData.ByName("turkish"));
        Assert.Equal("turkish", SegmentationData.For(Language.Turkish).Name);
        Assert.Equal("english", SegmentationData.For(Language.English).Name);
    }

    [Fact]
    public void OtherLanguagesDoNotCutWords()
    {
        Assert.Equal(["Eve", "giderken"], [.. Pipeline.Blank(Language.English).Process("Eve giderken").Select(t => t.ToString())]);
    }

    [Fact]
    public void MissingTurkishModelNamesTheTurkishPackage()
    {
        ModelNotAvailableException e = Assert.Throws<ModelNotAvailableException>(() => ModelPackages.Get(Language.Turkish));
        Assert.Contains("RecursiveAI.OneNlp.Models.Turkish", e.Message, StringComparison.Ordinal);
        Assert.Contains("RecursiveAI.OneNlp.Models.Multilingual", e.Message, StringComparison.Ordinal);
    }
}
