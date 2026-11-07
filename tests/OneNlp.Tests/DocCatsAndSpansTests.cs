using System.Text.Json;

namespace RecursiveAI.OneNlp.Tests;

public class DocCatsAndSpansTests
{
    private static Doc Make(string text = "the red cat saw Zorp today")
    {
        var tokens = new List<TokenBoundary>();
        int at = 0;
        foreach (string word in text.Split(' '))
        {
            tokens.Add(new TokenBoundary(at, word.Length));
            at += word.Length + 1;
        }

        return new Doc(new StringStore(), text, [.. tokens]);
    }

    [Fact]
    public void ADocumentWithoutThemWritesNeitherFieldAndReadsBackWithout()
    {
        Doc doc = Make();
        Assert.False(doc.HasCats);
        Assert.Empty(doc.Cats);
        Assert.Empty(doc.Spans);
        using JsonDocument json = JsonDocument.Parse(doc.ToJson());
        Assert.False(json.RootElement.TryGetProperty("cats", out _));
        Assert.False(json.RootElement.TryGetProperty("spans", out _));
        Doc again = Doc.FromBytes(doc.ToBytes());
        Assert.False(again.HasCats);
        Assert.Empty(again.Spans);
    }

    [Fact]
    public void OverlappingSpansAndScoresKeepOrderAndConfidenceInBothForms()
    {
        Doc doc = Make();
        doc.SetCats([new("B", 0.25f), new("A", 0.75f)]);
        doc.SetSpans("sc", [new SpanSpec(2, 5, "X", 0.9f), new SpanSpec(3, 5, "Y"), new SpanSpec(2, 5, "Y", 0.5f)]);
        doc.SetSpans("other", [new SpanSpec(0, 1, "Z")]);
        foreach (Doc copy in new[] { Doc.FromBytes(doc.ToBytes()), Doc.FromJson(doc.ToJson()) })
        {
            Assert.Equal(["B", "A"], copy.Cats.Keys);
            Assert.Equal(0.25f, copy.Cats["B"]);
            Assert.Equal(["sc", "other"], copy.Spans.Keys);
            Assert.Equal(
                [(2, 5, "X", 0.9f), (3, 5, "Y", 1f), (2, 5, "Y", 0.5f)],
                copy.Spans["sc"].Select(s => (s.Start, s.End, s.Label, s.Confidence)));
        }

        using JsonDocument json = JsonDocument.Parse(doc.ToJson());
        JsonElement first = json.RootElement.GetProperty("spans").GetProperty("sc")[0];
        Assert.Equal(8, first.GetProperty("start").GetInt32());
        Assert.Equal(20, first.GetProperty("end").GetInt32());
    }

    [Fact]
    public void SpaCyShapedJsonWithExtraFieldsIsRead()
    {
        const string Json = """
            {"text":"a b c","tokens":[{"id":0,"start":0,"end":1},{"id":1,"start":2,"end":3},{"id":2,"start":4,"end":5}],
             "cats":{"POS":1.0,"NEG":0.0},
             "spans":{"sc":[{"start":0,"end":3,"label":"L","kb_id":"","token_start":0,"token_end":2}]}}
            """;
        Doc doc = Doc.FromJson(Json);
        Assert.Equal(1f, doc.Cats["POS"]);
        Span span = Assert.Single(doc.Spans["sc"]);
        Assert.Equal((0, 2, "L"), (span.Start, span.End, span.Label));
    }

    [Fact]
    public void BadGroupsAreRejected()
    {
        Doc doc = Make();
        Assert.Throws<ArgumentException>(() => doc.SetSpans("sc", [new SpanSpec(2, 2, "X")]));
        Assert.Throws<ArgumentException>(() => doc.SetSpans("sc", [new SpanSpec(0, 99, "X")]));
        Assert.Throws<ArgumentException>(() => doc.SetSpans("sc", [new SpanSpec(0, 1, "")]));
        Assert.Throws<ArgumentException>(() => doc.SetCats([new("A", 1.5f)]));
        Assert.Throws<ArgumentException>(() => doc.SetCats([new("A", 0.5f), new("A", 0.5f)]));
        Assert.Throws<InvalidDataException>(() => Doc.FromJson("""{"text":"a","tokens":[{"id":0,"start":0,"end":1}],"spans":{"sc":[{"start":0,"end":5,"label":"L"}]}}"""));
    }
}
