using RecursiveAI.OneNlp.Data;

namespace RecursiveAI.OneNlp.Tests;

public class ExampleAndJsonlTests
{
    private static Doc MakeDoc(string text, params (int Start, int End)[] tokens) =>
        new(new StringStore(), text, [.. tokens.Select(t => new TokenBoundary(t.Start, t.End - t.Start))]);

    [Fact]
    public void AlignsDifferentTokenizationsOfTheSameText()
    {
        // "New York's" as [New][York's] against [New][York]['s].
        Doc predicted = MakeDoc("New York's", (0, 3), (4, 10));
        Doc reference = MakeDoc("New York's", (0, 3), (4, 8), (8, 10));
        var alignment = Alignment.Create(predicted, reference);
        Assert.Equal([0], alignment.XToY(0).ToArray());
        Assert.Equal([1, 2], alignment.XToY(1).ToArray());
        Assert.Equal([1], alignment.YToX(2).ToArray());
        Assert.True(alignment.IsOneToOne(0));
        Assert.False(alignment.IsOneToOne(1));
    }

    [Fact]
    public void AlignmentIgnoresWhitespaceAndCase()
    {
        Doc a = MakeDoc("Hello  World", (0, 5), (7, 12));
        Doc b = MakeDoc("hello world", (0, 5), (6, 11));
        Alignment alignment = Alignment.Create(a, b);
        Assert.Equal([0], alignment.XToY(0).ToArray());
        Assert.Equal([1], alignment.XToY(1).ToArray());
        Assert.Throws<ArgumentException>(() => Alignment.Create(a, MakeDoc("hello wurld", (0, 5), (6, 11))));
    }

    [Fact]
    public void CarriesGoldValuesOntoOneToOneTokensOnly()
    {
        Doc predicted = MakeDoc("New York's", (0, 3), (4, 10));
        Doc reference = MakeDoc("New York's", (0, 3), (4, 8), (8, 10));
        reference.SetAnnotation(TokenColumn.Tag, 0, "A");
        reference.SetAnnotation(TokenColumn.Tag, 1, "B");
        reference.SetAnnotation(TokenColumn.Tag, 2, "C");
        string?[] tags = new Example(predicted, reference).GetAligned(TokenColumn.Tag);
        Assert.Equal(["A", null], tags);

        // The other way round, a predicted token inside one reference token takes its value.
        string?[] back = new Example(reference, predicted).GetAligned(TokenColumn.Tag);
        Assert.Equal([null, null, null], back);
        predicted.SetAnnotation(TokenColumn.Tag, 1, "Z");
        Assert.Equal([null, "Z", "Z"], new Example(reference, predicted).GetAligned(TokenColumn.Tag));
    }

    [Fact]
    public void MapsEntitiesOnlyWhenTheyAlignOnTokenBoundaries()
    {
        Doc reference = MakeDoc("Sam Lee's cat", (0, 3), (4, 7), (7, 9), (10, 13));
        reference.SetEntities([new SpanSpec(0, 2, "PER")]);
        Doc wholeTokens = MakeDoc("Sam Lee's cat", (0, 3), (4, 9), (10, 13));
        SpanSpec?[] mapped = new Example(wholeTokens, reference).GetAlignedEntities();
        Assert.Null(mapped[0]); // "Lee's" is one predicted token, so "Sam Lee" ends inside it

        Doc compatible = MakeDoc("Sam Lee's cat", (0, 3), (4, 7), (7, 9), (10, 13));
        Assert.Equal(new SpanSpec(0, 2, "PER"), new Example(compatible, reference).GetAlignedEntities()[0]);
    }

    [Fact]
    public void FromReferenceMakesAnUnannotatedCopy()
    {
        Doc reference = MakeDoc("a b", (0, 1), (2, 3));
        reference.SetAnnotation(TokenColumn.Pos, 0, "X");
        Example e = Example.FromReference(reference);
        Assert.Equal(string.Empty, e.Predicted[0].Pos);
        Assert.Equal(reference.Text, e.Predicted.Text);
        Assert.True(e.Alignment.IsOneToOne(1));
    }

    [Fact]
    public void ReadsJsonLines()
    {
        string json =
            "{\"text\": \"Ada met Bob in Paris.\", \"entities\": [[0, 3, \"PER\"], [8, 11, \"PER\"], [15, 20, \"LOC\"]], "
            + "\"spans\": {\"sc\": [[0, 7, \"X\"], [4, 11, \"Y\"]]}, \"cats\": {\"pos\": 1, \"neg\": 0.25}, \"extra\": [1, 2]}\n"
            + "\n"
            + "{\"text\": \"Hi there\", \"tokens\": [[0, 2], [3, 8]]}\n";
        List<Example> all = JsonlReader.Read(new StringReader(json)).ToList();
        Assert.Equal(2, all.Count);
        Doc doc = all[0].Reference;
        Assert.Equal(["Ada", "Bob", "Paris"], doc.Ents.Select(e => e.Text).ToArray());
        Assert.Equal(["PER", "PER", "LOC"], doc.Ents.Select(e => e.Label).ToArray());
        Assert.Equal(1f, all[0].Cats!["pos"]);
        Assert.Equal(0.25f, all[0].Cats!["neg"]);
        IReadOnlyList<SpanSpec> group = all[0].Spans!["sc"];
        Assert.Equal(2, group.Count);
        Assert.Equal(new SpanSpec(0, 2, "X"), group[0]);
        Assert.Equal(new SpanSpec(1, 3, "Y"), group[1]);
        Assert.Equal(EntIob.Unset, all[1].Reference[0].EntIob);
        Assert.Equal(2, all[1].Reference.Length);
        Assert.Null(all[1].Cats);
    }

    [Fact]
    public void AnEmptyEntityListMeansNoEntitiesRatherThanNotAnnotated()
    {
        Example e = JsonlReader.Read(new StringReader("{\"text\": \"a b\", \"entities\": []}")).Single();
        Assert.Equal(EntIob.Outside, e.Reference[0].EntIob);
    }

    [Fact]
    public void MisalignedSpansAreAnErrorOrSkipped()
    {
        const string Line = "{\"text\": \"Hello world\", \"entities\": [[0, 3, \"X\"], [6, 11, \"Y\"]]}";
        var e = Assert.Throws<DataFormatException>(() => JsonlReader.Read(new StringReader(Line)).ToList());
        Assert.Equal(1, e.LineNumber);
        Assert.Contains("token boundaries", e.Message, StringComparison.Ordinal);

        Example kept = JsonlReader.Read(new StringReader(Line), strings: null, options: new JsonlOptions { Misaligned = MisalignedSpanPolicy.Skip }).Single();
        Assert.Equal("world", kept.Reference.Ents.Single().Text);
    }

    [Theory]
    [InlineData("[1, 2]", "JSON object")]
    [InlineData("{\"txt\": \"x\"}", "'text'")]
    [InlineData("{\"text\": 5}", "'text'")]
    [InlineData("{\"text\": \"a\", \"entities\": 5}", "'entities'")]
    [InlineData("{\"text\": \"a\", \"entities\": [[0, 1]]}", "entities")]
    [InlineData("{\"text\": \"a\", \"entities\": [[0, 9, \"X\"]]}", "offsets")]
    [InlineData("{\"text\": \"a b\", \"entities\": [[0, 3, \"X\"], [2, 3, \"Y\"]]}", "overlap")]
    [InlineData("{\"text\": \"a\", \"cats\": {\"x\": 2}}", "cats.x")]
    [InlineData("{\"text\": \"a\", \"cats\": {\"x\": \"high\"}}", "cats.x")]
    [InlineData("{\"text\": \"a\", \"tokens\": [[0, 1], [0, 1]]}", "tokens")]
    [InlineData("{\"text\": \"a\", \"tokens\": [[0, 5]]}", "token")]
    [InlineData("{\"text\": \"a\"", "invalid JSON")]
    public void RejectsBadLinesWithTheLineNumber(string line, string fragment)
    {
        var e = Assert.Throws<DataFormatException>(() => JsonlReader.Read(new StringReader("{\"text\": \"ok\"}\n" + line + "\n")).ToList());
        Assert.Equal(2, e.LineNumber);
        Assert.Contains(fragment, e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsDeepNestingAndLongLines()
    {
        string deep = "{\"text\": \"a\", \"meta\": " + new string('[', 40) + new string(']', 40) + "}";
        Assert.Throws<DataFormatException>(() => JsonlReader.Read(new StringReader(deep)).ToList());
        Assert.Throws<DataFormatException>(() => JsonlReader.Read(new StringReader("{\"text\": \"" + new string('a', 500) + "\"}"), strings: null, options: new JsonlOptions { MaxLineLength = 100 }).ToList());
    }
}
