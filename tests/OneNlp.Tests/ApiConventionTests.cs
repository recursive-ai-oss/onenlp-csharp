using System.Text.Json;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Matching;
using RecursiveAI.OneNlp.Serialization;

namespace RecursiveAI.OneNlp.Tests;

/// <summary>Conventions a caller meets first: text as strings, one default tokenizer, System.Text.Json, ruler initializers, example factory, I/O verbs.</summary>
public class ApiConventionTests
{
    private const string Sentence = "Ship order 21-1234 to O'Neil's store, e-mail ceo@acme.com, cost $5.99.";

    private static readonly string[] Phrases = ["21-1234", "O'Neil's", "ceo@acme.com", "$5.99"];

    // ---- Text is a string

    [Fact]
    public void TokenAndSpanTextAreStringsThatCompareByContent()
    {
        using Pipeline nlp = Pipeline.Blank(Language.English);
        Doc doc = nlp.Process("Hello big world");
        Assert.True(doc[0].Text == "Hello");
        Assert.Equal(["Hello", "big", "world"], doc.Select(t => t.Text).ToList());
        Assert.Equal("Hello", doc[0].TextSpan.ToString());
        doc.SetEntities([new SpanSpec(1, 3, "X")]);
        Assert.Equal("big world", doc.Ents[0].Text);
        Dictionary<string, Span> bySpan = doc.Ents.ToDictionary(s => s.Text);
        Assert.Contains("big world", bySpan.Keys);
        Assert.True(doc.Ents[0].TextSpan.SequenceEqual("big world"));
    }

    // ---- One default tokenizer

    [Fact]
    public void PhraseMatcherDefaultsToTheTokenizerPipelineBlankUses()
    {
        using Pipeline nlp = Pipeline.Blank(Language.English);
        Doc doc = nlp.Process(Sentence);
        foreach (PhraseMatcher matcher in new[] { new PhraseMatcher(), new PhraseMatcher(nlp) })
        {
            matcher.Add("P", Phrases);
            Assert.Equal(Phrases.OrderBy(p => p, StringComparer.Ordinal), matcher.MatchSpans(doc).Select(s => s.Text).OrderBy(p => p, StringComparer.Ordinal));
        }
    }

    [Fact]
    public void EntityRulerSplitsPhrasesWithTheTokenizerOfThePipelineItJoins()
    {
        var ruler = new EntityRuler();
        foreach (string phrase in Phrases)
        {
            ruler.AddPhrase("P", phrase);
        }

        using Pipeline nlp = Pipeline.Blank(Language.English).Add(ruler);
        Doc doc = nlp.Process(Sentence);
        Assert.Equal(Phrases.OrderBy(p => p, StringComparer.Ordinal), doc.Ents.Select(e => e.Text).OrderBy(p => p, StringComparer.Ordinal));

        // French elision is a tokenizer difference: the ruler must follow the pipeline it was added to, not a global default.
        var french = new EntityRuler();
        french.AddPhrase("P", "l'homme");
        using Pipeline fr = Pipeline.Blank(Language.French).Add(french);
        Doc frDoc = fr.Process("Il a vu l'homme hier.");
        Assert.Equal(["l'homme"], frDoc.Ents.Select(e => e.Text));
    }

    [Fact]
    public void JsonlReaderTakesThePipelineTokenizer()
    {
        using Pipeline nlp = Pipeline.Blank(Language.English);
        string line = "{\"text\": \"" + Sentence + "\"}";
        Example fromPipeline = JsonlReader.Read(new StringReader(line), nlp).Single();
        Assert.Equal(nlp.Process(Sentence).Select(t => t.Text), fromPipeline.Reference.Select(t => t.Text));
        Assert.Same(nlp.Strings, fromPipeline.Reference.Strings);
        Example byDefault = JsonlReader.Read(new StringReader(line)).Single();
        Assert.Equal(fromPipeline.Reference.Length, byDefault.Reference.Length);
    }

    // ---- System.Text.Json

    [Fact]
    public void DocGoesThroughSystemTextJson()
    {
        using Pipeline nlp = Pipeline.Blank(Language.English);
        Doc doc = nlp.Process("Ada Lovelace wrote notes.");
        doc.SetEntities([new SpanSpec(0, 2, "PER", 0.9f)]);

        string direct = JsonSerializer.Serialize(doc);
        Assert.Equal(doc.ToJson(), direct);

        string wrapped = JsonSerializer.Serialize(new Envelope(7, doc));
        using (JsonDocument parsed = JsonDocument.Parse(wrapped))
        {
            Assert.Equal(7, parsed.RootElement.GetProperty("Id").GetInt32());
            Assert.Equal("Ada Lovelace wrote notes.", parsed.RootElement.GetProperty("Analysis").GetProperty("text").GetString());
        }

        Envelope back = JsonSerializer.Deserialize<Envelope>(wrapped)!;
        Assert.Equal(doc.ToJson(), back.Analysis!.ToJson());
        Assert.Null(JsonSerializer.Deserialize<Envelope>("{\"Id\":1,\"Analysis\":null}")!.Analysis);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Doc>("{\"tokens\":[{\"id\":0,\"start\":0,\"end\":99}],\"text\":\"a\"}"));
    }

    [Fact]
    public void SpansAndTokensAreWrittenWithOffsetsLabelAndText()
    {
        using Pipeline nlp = Pipeline.Blank(Language.English);
        Doc doc = nlp.Process("Hi Ada Lovelace");
        doc.SetEntities([new SpanSpec(1, 3, "PER", 0.5f)]);

        using JsonDocument ents = JsonDocument.Parse(JsonSerializer.Serialize(doc.Ents));
        JsonElement e = ents.RootElement[0];
        Assert.Equal(3, e.GetProperty("start").GetInt32());
        Assert.Equal(15, e.GetProperty("end").GetInt32());
        Assert.Equal("PER", e.GetProperty("label").GetString());
        Assert.Equal("Ada Lovelace", e.GetProperty("text").GetString());
        Assert.Equal(0.5, e.GetProperty("confidence").GetDouble());

        using JsonDocument token = JsonDocument.Parse(JsonSerializer.Serialize(doc[1]));
        Assert.Equal("Ada", token.RootElement.GetProperty("text").GetString());
        Assert.Equal(3, token.RootElement.GetProperty("start").GetInt32());
        Assert.Throws<NotSupportedException>(() => JsonSerializer.Deserialize<Span>("{}"));
    }

    private sealed record Envelope(int Id, Doc? Analysis);

    // ---- EntityRuler initializer

    [Fact]
    public void EntityRulerCollectionInitializerAddsRules()
    {
        var ruler = new EntityRuler
        {
            { "ORG", Pattern.Orth("Acme") },
            { "ORG", Pattern.Orth("Bolt") },
        };
        Assert.Equal(2, ruler.Patterns.Count);
        Assert.Equal(2, ruler.Count());
        using Pipeline nlp = Pipeline.Blank(Language.English).Add(ruler);
        Assert.Equal(["Acme", "Bolt"], nlp.Process("Acme and Bolt").Ents.Select(e => e.Text));
    }

    // ---- Example.Create

    [Fact]
    public void ExampleCreateTokenizesWithThePipelineAndAlignsCharacterOffsets()
    {
        using Pipeline nlp = Pipeline.Blank(Language.English);
        const string text = "Ada Lovelace visited Paris.";
        var cats = new Dictionary<string, float> { ["travel"] = 1f };
        Example example = Example.Create(nlp, text, [(21, 26, "LOC"), (0, 12, "PER")], cats);
        Assert.Equal(["Ada Lovelace:PER", "Paris:LOC"], example.Reference.Ents.Select(e => e.Text + ":" + e.Label));
        Assert.Equal(nlp.Process(text).Select(t => t.Text), example.Predicted.Select(t => t.Text));
        Assert.Same(nlp.Strings, example.Reference.Strings);
        Assert.Equal(1f, example.Cats!["travel"]);
        Assert.Empty(Example.Create(nlp, text).Reference.Ents);

        ArgumentException off = Assert.Throws<ArgumentException>(() => Example.Create(nlp, text, [(1, 12, "PER")]));
        Assert.Contains("PER", off.Message);
        Assert.Throws<ArgumentException>(() => Example.Create(nlp, text, [(0, 12, "A"), (4, 12, "B")]));
    }

    // ---- I/O verbs

    [Fact]
    public async Task DocAndDocBinRoundTripThroughAsyncStreams()
    {
        using Pipeline nlp = Pipeline.Blank(Language.English);
        Doc a = nlp.Process("First doc here.");
        Doc b = nlp.Process("Second one follows.");
        a.SetEntities([new SpanSpec(0, 1, "N")]);

        using var ms = new MemoryStream();
        await a.WriteBinaryAsync(ms);
        b.WriteBinary(ms);
        ms.Position = 0;
        Doc a2 = await Doc.FromStreamAsync(ms);
        Doc b2 = Doc.FromStream(ms);
        Assert.Equal(a.ToJson(), a2.ToJson());
        Assert.Equal(b.ToJson(), b2.ToJson());

        using var js = new MemoryStream();
        await a.WriteJsonAsync(js);
        js.Position = 0;
        Assert.Equal(a.ToJson(), (await Doc.FromJsonAsync(js)).ToJson());

        var bin = new DocBin();
        bin.Add(a);
        bin.Add(b);
        using var bs = new MemoryStream();
        await bin.WriteBinaryAsync(bs);
        Assert.Equal(bin.ToBytes(), bs.ToArray());

        bs.Position = 0;
        Assert.Equal([a.ToJson(), b.ToJson()], new DocBinReader(bs).Select(d => d.ToJson()));

        bs.Position = 0;
        var viaAsync = new List<string>();
        await foreach (Doc d in new DocBinReader(bs).ReadAllAsync())
        {
            viaAsync.Add(d.ToJson());
        }

        Assert.Equal([a.ToJson(), b.ToJson()], viaAsync);
        bs.Position = 0;
        Assert.Equal(2, (await DocBin.FromStreamAsync(bs)).Count);

        // A flipped byte still fails the checksum on the asynchronous path.
        byte[] bad = bin.ToBytes();
        bad[^6] ^= 0xFF;
        await Assert.ThrowsAsync<InvalidDataException>(async () => await DocBin.FromStreamAsync(new MemoryStream(bad)));
    }
}
