using RecursiveAI.OneNlp.Segmentation;

namespace RecursiveAI.OneNlp.Tests;

public class SegmentationTests
{
    // These tests pin the English rules, so they use the English pipeline directly instead of the language-detecting Nlp verbs.
    private static readonly Pipeline English = Pipeline.Blank(Language.English).Add(SentencizerComponent.For(Language.English));

    private static Token[] Tokens(string text) => [.. English.Process(text)];

    private static IReadOnlyList<Span> Sentences(string text) => English.Process(text).Sentences;

    private static string[] Words(string text) => [.. Tokens(text).Select(t => t.ToString())];

    [Theory]
    [InlineData("I don't know.", new[] { "I", "do", "n't", "know", "." })]
    [InlineData("It can't be.", new[] { "It", "ca", "n't", "be", "." })]
    [InlineData("They won\u2019t go", new[] { "They", "wo", "n\u2019t", "go" })]
    [InlineData("DON'T", new[] { "DO", "N'T" })]
    [InlineData("John's dog, we're, I've, they'll, he'd, I'm", new[] { "John", "'s", "dog", ",", "we", "'re", ",", "I", "'ve", ",", "they", "'ll", ",", "he", "'d", ",", "I", "'m" })]
    [InlineData("I cannot, gonna wanna", new[] { "I", "can", "not", ",", "gon", "na", "wan", "na" })]
    [InlineData("the boys' toys", new[] { "the", "boys", "'", "toys" })]
    [InlineData("o'clock rock'n'roll", new[] { "o'clock", "rock'n'roll" })]
    [InlineData("Mr. Smith lives in the U.S. now.", new[] { "Mr.", "Smith", "lives", "in", "the", "U.S.", "now", "." })]
    [InlineData("e.g. this", new[] { "e.g.", "this" })]
    [InlineData("A well-known e-mail, anti-war al-Sadr", new[] { "A", "well", "-", "known", "e-mail", ",", "anti-war", "al", "-", "Sadr" })]
    [InlineData("Nice :) and :-( ok$$$ --> <<x>>", new[] { "Nice", ":)", "and", ":-(", "ok", "$$$", "-->", "<<", "x", ">>" })]
    [InlineData("Wait... what?!", new[] { "Wait", "...", "what", "?!" })]
    [InlineData("At 3:30, 3.14 and 1,000 or 1/2", new[] { "At", "3:30", ",", "3.14", "and", "1,000", "or", "1/2" })]
    [InlineData("Mail me@example.com or see https://example.org/a?b=1.", new[] { "Mail", "me@example.com", "or", "see", "https://example.org/a?b=1", "." })]
    [InlineData("(www.example.com)", new[] { "(", "www.example.com", ")" })]
    [InlineData("Call 303-832-8160 for $$$. 15-year", new[] { "Call", "303-832-8160", "for", "$$$", ".", "15", "-", "year" })]
    [InlineData("  \t\n ", new string[0])]
    [InlineData("", new string[0])]
    public void EnglishRules(string text, string[] expected) => Assert.Equal(expected, Words(text));

    [Fact]
    public void OffsetsPointIntoTheOriginalStringAndSpacesAreRecovered()
    {
        const string text = "Don't  stop";
        Doc doc = English.Process(text);
        Assert.Equal(["Do", "n't", "stop"], doc.Select(t => t.ToString()));
        Assert.Equal([(0, 2), (2, 5), (7, 11)], doc.Select(t => (t.Idx, t.EndIdx)));
        Assert.Equal([false, true, false], doc.Select(t => t.HasSpaceAfter));
    }

    [Fact]
    public void NeutralDataDoesNotSplitContractionsButStillKeepsUrls()
    {
        using Pipeline pipeline = Pipeline.Blank(Language.FromCode("sw"));
        Doc doc = pipeline.Process("don't visit http://a.de/x well-known");
        Assert.Equal(["don't", "visit", "http://a.de/x", "well-known"], doc.Select(t => t.ToString()));
    }

    [Fact]
    public void NoDataGivesPlainUaxSegments()
    {
        var tokenizer = new UaxWordTokenizer(SegmentationData.None);
        Assert.Equal(["don't", "well", "-", "known", "U.S", "."], tokenizer.Segment("don't well-known U.S.").Select(b => "don't well-known U.S.".Substring(b.Start, b.Length)));
    }

    [Fact]
    public void SentencesFollowUaxAndKeepAbbreviationsTogether()
    {
        const string text = "Dr. Who arrived at 3.5 p.m. on Tuesday. Was it late? Yes! \"Quite,\" he said.";
        Assert.Equal(
            ["Dr. Who arrived at 3.5 p.m. on Tuesday.", "Was it late?", "Yes!", "\"Quite,\" he said."],
            Sentences(text).Select(s => s.Text.ToString()));
    }

    [Fact]
    public void SentencesOfEmptyAndSingleSentenceText()
    {
        Assert.Empty(Sentences(""));
        Assert.Single(Sentences("no end"));
        Assert.Equal(2, Sentences("One.  Two.\n").Count);
    }

    [Fact]
    public void SentencizerIsRegisteredAndTakesALanguage()
    {
        using Pipeline pipeline = Pipeline.Blank(Language.English).Add("sentencizer", new Dictionary<string, string> { ["language"] = "en" });
        Assert.Single(pipeline.Process("Hello Mr. Brown.").Sentences);
    }

    [Fact]
    public void LongInputsStayLinearAndNeverThrow()
    {
        string text = new string('a', 200_000) + " " + string.Concat(Enumerable.Repeat("a-", 100_000)) + string.Concat(Enumerable.Repeat("http://", 5000));
        Assert.NotEmpty(Tokens(text));
        Assert.NotNull(Tokens("\uD800 x \uDC00 \uD83D\uDE00"));
    }
}
