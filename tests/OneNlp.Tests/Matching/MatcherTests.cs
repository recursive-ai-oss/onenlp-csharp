using System.Text;
using RecursiveAI.OneNlp.Matching;

namespace RecursiveAI.OneNlp.Tests.Matching;

public class MatcherTests
{
    private static Doc Tokens(params string[] words)
    {
        var text = new StringBuilder();
        var tokens = new TokenBoundary[words.Length];
        for (int i = 0; i < words.Length; i++)
        {
            tokens[i] = new TokenBoundary(text.Length, words[i].Length);
            text.Append(words[i]).Append(' ');
        }

        return new Doc(new StringStore(), text.ToString(), tokens);
    }

    private static string Spans(IReadOnlyList<PatternMatch> matches) => string.Join(" ", matches.Select(m => $"{m.Start}-{m.End}"));

    private static Matcher One(string json, GreedyMode greedy = GreedyMode.None)
    {
        var m = new Matcher();
        m.AddJson("K", json, greedy);
        return m;
    }

    [Fact]
    public void OneOrMoreReportsEveryRange()
    {
        Assert.Equal("0-1 0-2 1-2 0-3 1-3 2-3", Spans(One("""[{"ORTH":"a","OP":"+"}]""").Match(Tokens("a", "a", "a"))));
    }

    [Fact]
    public void OptionalStarAndNegationFollowTheDocumentedMeaning()
    {
        Doc doc = Tokens("a", "b", "a", "x", "x", "b");
        Assert.Equal("0-1 2-3 2-4", Spans(One("""[{"ORTH":"a"},{"ORTH":"x","OP":"?"}]""").Match(doc)));
        Assert.Equal("0-1 2-3 2-4 2-5", Spans(One("""[{"ORTH":"a"},{"ORTH":"x","OP":"*"}]""").Match(doc)));
        Assert.Equal("0-2", Spans(One("""[{"ORTH":"a"},{"ORTH":"x","OP":"!"}]""").Match(doc)));
        Assert.Empty(One("""[{"ORTH":"x","OP":"*"}]""").Match(Tokens("a", "b")));
    }

    [Theory]
    [InlineData("{2}", "0-2 1-3 2-4 3-5")]
    [InlineData("{2,3}", "0-2 0-3 1-3 1-4 2-4 2-5 3-5")]
    [InlineData("{3,}", "0-3 0-4 1-4 0-5 1-5 2-5")]
    [InlineData("{,2}", "0-1 0-2 1-2 1-3 2-3 2-4 3-4 3-5 4-5")]
    public void RangeOperators(string op, string expected)
    {
        Doc doc = Tokens("a", "a", "a", "a", "a");
        Assert.Equal(expected, Spans(One($$"""[{"ORTH":"a","OP":"{{op}}"}]""").Match(doc)));
    }

    [Fact]
    public void EmptyTokenSpecMatchesAnyToken()
    {
        Assert.Equal("0-2 1-3", Spans(One("""[{},{}]""").Match(Tokens("a", "b", "c"))));
    }

    [Fact]
    public void GreedyModesThinOverlaps()
    {
        var m = new Matcher();
        m.AddJson("K", """[[{"ORTH":"a"},{"ORTH":"b"}],[{"ORTH":"b"},{"ORTH":"c"},{"ORTH":"d"}]]""", GreedyMode.Longest);
        Assert.Equal("1-4", Spans(m.Match(Tokens("a", "b", "c", "d"))));
        var first = new Matcher();
        first.AddJson("K", """[[{"ORTH":"a"},{"ORTH":"b"}],[{"ORTH":"b"},{"ORTH":"c"},{"ORTH":"d"}]]""", GreedyMode.First);
        Assert.Equal("0-2", Spans(first.Match(Tokens("a", "b", "c", "d"))));
    }

    [Fact]
    public void KeysReportSeparatelyAndAlternativesDoNotDuplicate()
    {
        var m = new Matcher();
        m.Add("A", [Pattern.Orth("a"), Pattern.Lower("a")]);
        m.Add("B", Pattern.Orth("a"));
        Doc doc = Tokens("a");
        Assert.Equal(["A:0-1", "B:0-1"], m.Match(doc).Select(x => $"{x.Key}:{x.Start}-{x.End}"));
        Assert.Equal("A", m.MatchSpans(doc)[0].Label);
    }

    [Fact]
    public void CallbackRunsForEachMatch()
    {
        var m = new Matcher();
        var seen = new List<int>();
        m.Add("A", Pattern.Orth("a"), onMatch: (matcher, doc, i, matches) => seen.Add(i * 10 + matches.Count));
        m.Match(Tokens("a", "b", "a"));
        Assert.Equal([2, 12], seen);
    }

    [Fact]
    public void PredicatesOnLength()
    {
        Doc doc = Tokens("a", "bb", "ccc", "\U0001F600\U0001F600");
        Assert.Equal("2-3", Spans(One("""[{"LENGTH":{">=":3}}]""").Match(doc)));
        Assert.Equal("1-2 3-4", Spans(One("""[{"LENGTH":2}]""").Match(doc)));
        Assert.Equal("0-1 2-3", Spans(One("""[{"LENGTH":{"IN":[1,3]}}]""").Match(doc)));
    }

    [Fact]
    public void RegexSearchesAndATimedOutTestIsNoMatch()
    {
        Doc doc = Tokens("abc", "xyz");
        Assert.Equal("0-1", Spans(One("""[{"ORTH":{"REGEX":"b"}}]""").Match(doc)));

        // A look-ahead rules out the linear-time engine, and this alternation backtracks exponentially.
        string evil = new string('a', 40) + "c";
        var m = One("""[{"ORTH":{"REGEX":"^(?!x)(a|aa)+$"}}]""");
        Assert.Empty(m.Match(Tokens(evil)));
    }

    [Fact]
    public void FlagsAndShapes()
    {
        Doc doc = Tokens("Hello", "42", "x-1", "!");
        Assert.Equal("0-1", Spans(One("""[{"IS_TITLE":true}]""").Match(doc)));
        Assert.Equal("1-2", Spans(One("""[{"LIKE_NUM":true}]""").Match(doc)));
        Assert.Equal("3-4", Spans(One("""[{"IS_PUNCT":true}]""").Match(doc)));
        Assert.Equal("2-3", Spans(One("""[{"SHAPE":"x-d"}]""").Match(doc)));
        Assert.Equal("Xxxxx", TokenText.Shape("Hello"));
        Assert.Equal("dddd", TokenText.Shape("123456"));
        Assert.Equal("dd-dddd", TokenText.Shape("12-3456"));
        Assert.Equal("Xx.Xx", TokenText.Shape("Mr.Xu"));
    }

    [Fact]
    public void SentenceStartAndEntityAttributes()
    {
        Doc doc = Tokens("a", "b", "c", "d");
        doc.SetSentenceStart(2, true);
        doc.SetEntities([new SpanSpec(1, 3, "ORG")]);
        Assert.Equal("0-1 2-3", Spans(One("""[{"IS_SENT_START":true}]""").Match(doc)));
        Assert.Equal("1-2 2-3", Spans(One("""[{"ENT_TYPE":"ORG"}]""").Match(doc)));
        Assert.Equal("1-2", Spans(One("""[{"ENT_IOB":"B"}]""").Match(doc)));
        Assert.Equal("1-2", Spans(One("""[{"ENT_IOB":3}]""").Match(doc)));
        Assert.Equal("0-1 3-4", Spans(One("""[{"ENT_TYPE":""}]""").Match(doc)));
    }

    [Fact]
    public void MorphologyPredicatesCompareFeatureSets()
    {
        Doc doc = Tokens("x", "y");
        doc.SetAnnotation(TokenColumn.Morph, 0, "Case=Nom|Number=Sing");
        doc.SetAnnotation(TokenColumn.Morph, 1, "Number=Plur");
        Assert.Equal("0-1", Spans(One("""[{"MORPH":{"IS_SUPERSET":["Number=Sing"]}}]""").Match(doc)));
        Assert.Equal("0-1 1-2", Spans(One("""[{"MORPH":{"INTERSECTS":["Number=Sing","Number=Plur"]}}]""").Match(doc)));
        Assert.Equal("1-2", Spans(One("""[{"MORPH":{"IS_SUBSET":["Number=Plur"]}}]""").Match(doc)));
        Assert.Equal("0-1", Spans(One("""[{"MORPH":"Case=Nom|Number=Sing"}]""").Match(doc)));
    }

    [Fact]
    public void StopWordsAreSuppliedByTheCaller()
    {
        Doc doc = Tokens("The", "cat");
        Assert.Empty(One("""[{"IS_STOP":true}]""").Match(doc));
        var m = new Matcher { StopWords = ["the"] };
        m.AddJson("K", """[{"IS_STOP":true}]""");
        Assert.Equal("0-1", Spans(m.Match(doc)));
    }

    [Fact]
    public void BuilderMatchesJson()
    {
        Pattern built = Pattern.Token(
            new TokenSpec().Lower("hello"),
            new TokenSpec().Is(TokenProperty.IsPunct, true).Optional(),
            new TokenSpec().In(TokenProperty.Lower, "world", "there"));
        var m = new Matcher();
        m.Add("K", built);
        Assert.Equal("0-3 3-5", Spans(m.Match(Tokens("Hello", ",", "WORLD", "hello", "there"))));
        Pattern parsed = PatternJson.ParsePattern(PatternJson.ToJson(built));
        var m2 = new Matcher();
        m2.Add("K", parsed);
        Assert.Equal(PatternJson.ToJson(built), PatternJson.ToJson(parsed));
        Assert.Equal("0-3 3-5", Spans(m2.Match(Tokens("Hello", ",", "WORLD", "hello", "there"))));
    }

    [Fact]
    public void BuilderRejectsWrongKindsOfTests()
    {
        Assert.Throws<ArgumentException>(() => new TokenSpec().Is(TokenProperty.Orth, true));
        Assert.Throws<ArgumentException>(() => new TokenSpec().Is(TokenProperty.IsAlpha, "x"));
        Assert.Throws<ArgumentException>(() => new TokenSpec().Compare(TokenProperty.Orth, Comparison.Less, 3));
        Assert.Throws<ArgumentException>(() => new TokenSpec().Op("{5,2}"));
        Assert.Throws<ArgumentException>(() => new TokenSpec().Op("{5000}"));
        Assert.ThrowsAny<ArgumentException>(() => new TokenSpec().Matches(TokenProperty.Orth, "["));
        Assert.Throws<ArgumentException>(() => Pattern.Token());
        Assert.Throws<ArgumentException>(() => Pattern.Token(new TokenSpec().Repeat(0, 1000), new TokenSpec().Repeat(0, 1000), new TokenSpec().Repeat(0, 1000),
            new TokenSpec().Repeat(0, 1000), new TokenSpec().Repeat(0, 1000), new TokenSpec().Repeat(0, 1000), new TokenSpec().Repeat(0, 1000),
            new TokenSpec().Repeat(0, 1000), new TokenSpec().Repeat(0, 1000), new TokenSpec().Repeat(0, 1000), new TokenSpec().Repeat(0, 1000)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("[")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("[{}] x")]
    [InlineData("""[{"NOPE":1}]""")]
    [InlineData("""[{"_":{"a":1}}]""")]
    [InlineData("""[{"ORTH":"a","OP":""}]""")]
    [InlineData("""[{"ORTH":{"REGEX":"a{5000}"}}]""")]
    [InlineData("""[{"ORTH":[1]}]""")]
    [InlineData("""[{"ORTH":null}]""")]
    public void MalformedPatternsAreRejected(string json)
    {
        Assert.Throws<PatternFormatException>(() => PatternJson.ParsePatterns(json));
    }

    [Fact]
    public void HostileInputsAreRejectedCleanly()
    {
        // Invalid UTF-8 inside a string, a truncated file, too many positions, too many tests on one position, an enormous repeat.
        Assert.Throws<PatternFormatException>(() => PatternJson.ParsePatterns("[{\"ORTH\":\"a\"}]"u8[..8].ToArray()));
        Assert.Throws<PatternFormatException>(() => PatternJson.ParsePatterns([(byte)'[', (byte)'{', (byte)'"', 0xC3, (byte)'"']));
        Assert.Throws<PatternFormatException>(() => PatternJson.ParsePatterns("[" + string.Join(',', Enumerable.Repeat("{}", 1001)) + "]"));
        Assert.Throws<PatternFormatException>(() => PatternJson.ParsePatterns("[{" + string.Join(',', Enumerable.Range(0, 65).Select(i => $"\"IS_ALPHA\":true")) + "}]"));
        Assert.Throws<PatternFormatException>(() => PatternJson.ParsePatterns("""[{"ORTH":"a","OP":"{99999999999}"}]"""));
        Assert.Throws<PatternFormatException>(() => PatternJson.ReadEntityPatterns("{\"label\":\"A\",\"pattern\":\"x\"}\n{\"label\":"u8));
        Assert.Throws<PatternFormatException>(() => PatternJson.ReadEntityPatterns("[{\"label\":\"A\"}]"u8));
    }

    [Fact]
    public void DeepNestingIsRejected()
    {
        string deep = new string('[', 100) + new string(']', 100);
        Assert.Throws<PatternFormatException>(() => PatternJson.ParsePatterns(deep));
    }
}

public class PhraseMatcherTests
{
    private static Doc Doc(string text) => Pipeline.Blank().Process(text);

    [Fact]
    public void FindsOverlappingAndNestedPhrases()
    {
        var m = new PhraseMatcher();
        m.Add("P", ["New York", "York City", "New York City", "City"]);
        IReadOnlyList<PatternMatch> found = m.Match(Doc("I love New York City"));
        Assert.Equal(["2-4", "2-5", "3-5", "4-5"], found.Select(x => $"{x.Start}-{x.End}"));
    }

    [Fact]
    public void LowerAttributeIgnoresCase()
    {
        var m = new PhraseMatcher(TokenProperty.Lower);
        m.Add("P", ["new york"]);
        Assert.Single(m.Match(Doc("NEW York is big")));
        var exact = new PhraseMatcher();
        exact.Add("P", ["new york"]);
        Assert.Empty(exact.Match(Doc("NEW York is big")));
    }

    [Fact]
    public void SamePhraseUnderTwoKeysReportsBoth()
    {
        var m = new PhraseMatcher();
        m.Add("A", ["x y"]);
        m.Add("B", ["x y"]);
        Assert.Equal(["A", "B"], m.Match(Doc("x y")).Select(x => x.Key));
    }

    [Fact]
    public void RejectsEmptyPhrasesAndUnannotatedAttributes()
    {
        Assert.Throws<ArgumentException>(() => new PhraseMatcher().Add("P", [""]));
        Assert.Throws<ArgumentException>(() => new PhraseMatcher(TokenProperty.Pos).Add("P", ["a b"]));
        Assert.Throws<ArgumentException>(() => new PhraseMatcher(TokenProperty.IsAlpha));
    }
}

public class EntityRulerTests
{
    private static Doc Doc(string text) => Pipeline.Blank().Process(text);

    private static string Ents(Doc doc) => string.Join(" ", doc.Ents.Select(e => $"{e.Label}:{e.Start}-{e.End}"));

    [Fact]
    public void PhraseRuleOfOnlyWhiteSpaceIsLeftOutAndOthersStillMatch()
    {
        // Found by fuzzing: {"label":"O","pattern":" "} made the first run throw.
        var ruler = new EntityRuler();
        ruler.AddPhrase("O", " ");
        ruler.AddPhrase("O", "  \t\n", "me");
        ruler.AddPhrase("ORG", "Acme Corp");
        Doc doc = Doc("Hello Acme Corp");
        ruler.Process(doc);
        Assert.Equal("ORG:1-3", Ents(doc));
    }

    [Fact]
    public void IndexerFormMatchesShapes()
    {
        var ruler = new EntityRuler { ["CASE_NO"] = Pattern.Shape("Xdddd") };
        Doc doc = Doc("See case K1234 and Z9876.");
        ruler.Process(doc);
        Assert.Equal("CASE_NO:2-3 CASE_NO:4-5", Ents(doc));
        Assert.Equal(1f, doc.Ents[0].Confidence);
    }

    [Fact]
    public void LongestMatchWinsAndTiesGoToTheEarlierStart()
    {
        var ruler = new EntityRuler();
        ruler.AddPhrase("LOC", "New York");
        ruler.AddPhrase("LOC", "New York City");
        ruler.AddPhrase("ORG", "York City");
        Doc doc = Doc("New York City is big");
        ruler.Process(doc);
        Assert.Equal("LOC:0-3", Ents(doc));

        var tie = new EntityRuler();
        tie.AddPhrase("A", "x y");
        tie.AddPhrase("B", "y z");
        Doc d2 = Doc("x y z");
        tie.Process(d2);
        Assert.Equal("A:0-2", Ents(d2));
    }

    [Fact]
    public void ExistingEntitiesStayUnlessOverwriteIsSet()
    {
        Doc Annotated()
        {
            Doc d = Doc("Apple Inc buys Paris Hilton today");
            d.SetEntities([new SpanSpec(0, 2, "ORG"), new SpanSpec(3, 5, "PER")]);
            return d;
        }

        var keep = new EntityRuler();
        keep.AddPhrase("LOC", "Paris");
        keep.AddPhrase("MISC", "Inc buys");
        keep.AddPhrase("DATE", "today");
        Doc a = Annotated();
        keep.Process(a);
        Assert.Equal("ORG:0-2 PER:3-5 DATE:5-6", Ents(a));

        var overwrite = new EntityRuler(overwriteEnts: true);
        overwrite.AddPhrase("LOC", "Paris");
        overwrite.AddPhrase("MISC", "Inc buys");
        overwrite.AddPhrase("DATE", "today");
        Doc b = Annotated();
        overwrite.Process(b);
        Assert.Equal("MISC:1-3 LOC:3-4 DATE:5-6", Ents(b));
    }

    [Fact]
    public void RuleEntitiesKeepTheirIds()
    {
        var ruler = new EntityRuler();
        ruler.AddPhrase("ORG", "Acme", id: "acme-1");
        ruler.Add("ORG", Pattern.Token(new TokenSpec().Lower("acme"), new TokenSpec().Lower("corp")), "acme-1");
        Doc doc = Doc("Acme and Acme Corp");
        IReadOnlyList<RuleEntity> found = ruler.FindEntities(doc);
        Assert.Equal(["acme-1", "acme-1"], found.Select(f => f.Id));
        Assert.Equal(["ORG:0-1", "ORG:2-4"], found.Select(f => $"{f.Span.Label}:{f.Span.Start}-{f.Span.End}"));
    }

    [Fact]
    public void PatternFilesRoundTrip()
    {
        var ruler = new EntityRuler();
        ruler.AddPhrase("ORG", "Acme Corp", "a");
        ruler.Add("CASE", Pattern.Token(new TokenSpec().Shape("Xddd"), new TokenSpec().Is(TokenProperty.Length, 2).Optional()));
        using var stream = new MemoryStream();
        ruler.SavePatterns(stream);
        string text = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Equal(2, text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);

        stream.Position = 0;
        var loaded = new EntityRuler();
        loaded.LoadPatterns(stream);
        Assert.Equal(2, loaded.Patterns.Count);
        Doc doc = Doc("Acme Corp K123 ab");
        loaded.Process(doc);
        Assert.Equal("ORG:0-2 CASE:2-4", Ents(doc));
    }

    [Fact]
    public void ReadsArraysAndSkipsBlankLines()
    {
        IReadOnlyList<EntityRulerPattern> fromArray = PatternJson.ReadEntityPatterns(Encoding.UTF8.GetBytes("""[{"label":"A","pattern":"x"},{"label":"B","pattern":[{"ORTH":"y"}],"id":"1"}]"""));
        Assert.Equal(2, fromArray.Count);
        IReadOnlyList<EntityRulerPattern> lines = PatternJson.ReadEntityPatterns(Encoding.UTF8.GetBytes("\n{\"label\":\"A\",\"pattern\":\"x\"}\r\n\n{\"label\":\"B\",\"pattern\":\"y\"}\n"));
        Assert.Equal(2, lines.Count);
        PatternFormatException e = Assert.Throws<PatternFormatException>(() => PatternJson.ReadEntityPatterns(Encoding.UTF8.GetBytes("{\"label\":\"A\",\"pattern\":\"x\"}\n{\"label\":\"B\"}\n")));
        Assert.Contains("Line 2", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReaderRefusesInputOverTheLimit()
    {
        using var big = new MemoryStream(new byte[2000]);
        Assert.Throws<PatternFormatException>(() => PatternJson.ReadEntityPatterns(big, maxBytes: 1000));
    }

    [Fact]
    public void WorksAsAPipelineComponentCreatedByName()
    {
        using var pipeline = Pipeline.Blank();
        pipeline.Add(EntityRuler.ComponentName, new Dictionary<string, string> { ["overwrite_ents"] = "true", ["phrase_matcher_attr"] = "LOWER" });
        var ruler = (EntityRuler)pipeline.Components.Single();
        ruler.AddPhrase("ORG", "acme corp");
        Doc doc = pipeline.Process("ACME Corp rules");
        Assert.Equal("ORG:0-2", Ents(doc));
        Assert.Throws<ArgumentException>(() => ComponentRegistry.Create(EntityRuler.ComponentName, new Dictionary<string, string> { ["overwrite_ents"] = "maybe" }));
    }

    [Fact]
    public void EmptyRulerStillMarksTheDocumentAnnotated()
    {
        Doc doc = Doc("nothing here");
        new EntityRuler().Process(doc);
        Assert.Equal(EntIob.Outside, doc[0].EntIob);
    }
}
