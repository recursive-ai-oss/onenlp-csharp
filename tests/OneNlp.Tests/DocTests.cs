using RecursiveAI.OneNlp;

namespace RecursiveAI.OneNlp.Tests;

public class DocTests
{
    private static Doc Analyze(string text) => Nlp.Analyze(text);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Hello, world!")]
    [InlineData("  leading and   multiple   spaces\tand\nnewlines ")]
    [InlineData("Emoji 😀 and 👨‍👩‍👧 family, café, naïve, e\u0301")]
    [InlineData("It's 3.14 and 1,000 well-known \u2019quotes\u2019 don\u2019t")]
    [InlineData("lone \uD800 surrogate and \uDC00 other")]
    [InlineData("日本語のテキスト、中文文本。")]
    public void TokenTextIsASliceOfTheOriginalAtItsOffsets(string text)
    {
        Doc doc = Analyze(text);
        Assert.Same(text, doc.Text);
        int previousEnd = 0;
        foreach (Token t in doc)
        {
            Assert.True(t.Idx >= previousEnd);
            Assert.True(t.Length > 0);
            Assert.Equal(text.Substring(t.Idx, t.Length), t.ToString());
            Assert.True(t.Text.SequenceEqual(text.AsSpan(t.Idx, t.Length)));
            Assert.Equal(t.Idx + t.Length, t.EndIdx);
            Assert.Equal(t.Index, doc.FindToken(t.Idx));
            Assert.Equal(t.Index, doc.FindToken(t.EndIdx - 1));
            previousEnd = t.EndIdx;
        }

        // Everything outside the tokens is whitespace.
        var covered = new bool[text.Length];
        foreach (Token t in doc)
        {
            for (int i = t.Idx; i < t.EndIdx; i++)
            {
                covered[i] = true;
            }
        }

        for (int i = 0; i < text.Length; i++)
        {
            Assert.True(covered[i] || char.IsWhiteSpace(text[i]), $"character {i} is neither in a token nor whitespace");
        }
    }

    [Fact]
    public void TokenizerSplitsAsDocumented()
    {
        Assert.Equal(["Hello", ",", "world", "!"], Texts("Hello, world!"));
        Assert.Equal(["do", "n't", "well", "-", "known", "3.14", "1,000"], Texts("don't well-known 3.14 1,000"));
        Assert.Equal(["a", "-", "b", "1", ".", "x", "end", "."], Texts("a - b 1 . x end."));
        Assert.Equal(["😀", "😀"], Texts("😀😀"));
        Assert.Equal(["cafe\u0301"], Texts("cafe\u0301"));
        Assert.Empty(Texts(""));
        Assert.Empty(Texts(" \t\n"));
    }

    private static string[] Texts(string s) => [.. Analyze(s).Select(t => t.ToString())];

    [Fact]
    public void SpaceAfterFollowsTheText()
    {
        Doc doc = Analyze("a b,c");
        Assert.Equal([true, false, false, false], doc.Select(t => t.HasSpaceAfter));
    }

    [Fact]
    public void InvalidBoundariesAreRejected()
    {
        var strings = new StringStore();
        Assert.Throws<ArgumentException>(() => new Doc(strings, "abc", [new TokenBoundary(0, 0)]));
        Assert.Throws<ArgumentException>(() => new Doc(strings, "abc", [new TokenBoundary(2, 2)]));
        Assert.Throws<ArgumentException>(() => new Doc(strings, "abc", [new TokenBoundary(0, 2), new TokenBoundary(1, 1)]));
        Assert.Throws<ArgumentException>(() => new Doc(strings, "abc", [new TokenBoundary(2, 1), new TokenBoundary(0, 1)]));
        Assert.Throws<ArgumentException>(() => new Doc(strings, "abc", [new TokenBoundary(-1, 1)]));
        _ = new Doc(strings, "abc", [new TokenBoundary(0, 1), new TokenBoundary(1, 2)]);
    }

    [Fact]
    public void UnsetAnnotationsHaveDocumentedDefaults()
    {
        Doc doc = Pipeline.Blank().Process("one two");
        Token t = doc[1];
        Assert.Equal("", t.Pos);
        Assert.Equal("", t.Lemma);
        Assert.Equal("", t.Norm);
        Assert.Equal(1, t.HeadIndex);
        Assert.Equal(t, t.Head);
        Assert.Null(t.IsSentStart);
        Assert.Equal(EntIob.Unset, t.EntIob);
        Assert.Equal("", t.EntType);
        Assert.Equal(1f, t.Confidence);
        Assert.Empty(doc.Ents);
    }

    [Fact]
    public void AnnotationsAreInternedInTheSharedStore()
    {
        var pipeline = Pipeline.Blank(Language.English);
        Doc a = pipeline.Process("x y");
        Doc b = pipeline.Process("z");
        a.SetAnnotation(TokenColumn.Pos, 0, "NOUN");
        a.SetAnnotation(TokenColumn.Pos, 1, "NOUN");
        b.SetAnnotation(TokenColumn.Pos, 0, "NOUN");
        Assert.Equal(a.GetColumn(TokenColumn.Pos)[0], a.GetColumn(TokenColumn.Pos)[1]);
        Assert.Equal(a.GetColumn(TokenColumn.Pos)[0], b.GetColumn(TokenColumn.Pos)[0]);
        Assert.Equal("NOUN", b[0].Pos);
        Assert.Same(a.Strings, b.Strings);

        a.SetAnnotation(TokenColumn.Pos, 1, "");
        Assert.Equal("", a[1].Pos);
        Assert.Throws<ArgumentOutOfRangeException>(() => a.SetAnnotation(TokenColumn.Pos, 2, "X"));
    }

    [Fact]
    public void ColumnSettersValidate()
    {
        Doc doc = Analyze("a b c");
        int noun = doc.Strings.Add("NOUN");
        doc.SetColumn(TokenColumn.Tag, [noun, noun, 0]);
        Assert.Equal("NOUN", doc[1].Tag);
        Assert.Throws<ArgumentException>(() => doc.SetColumn(TokenColumn.Tag, [noun]));
        Assert.Throws<ArgumentException>(() => doc.SetColumn(TokenColumn.Tag, [0, 0, 999]));

        doc.SetHeads([1, 1, 1]);
        Assert.Equal(doc[1], doc[0].Head);
        Assert.Throws<ArgumentException>(() => doc.SetHeads([0, 0, 3]));
        Assert.Throws<ArgumentException>(() => doc.SetHeads([0, 0]));
        Assert.Throws<ArgumentOutOfRangeException>(() => doc.SetHead(0, -1));

        doc.SetConfidence(2, 0.25f);
        Assert.Equal(0.25f, doc[2].Confidence);
        Assert.Throws<ArgumentOutOfRangeException>(() => doc.SetConfidence(0, 1.5f));
        Assert.Throws<ArgumentOutOfRangeException>(() => doc.SetConfidence(0, float.NaN));
    }

    [Fact]
    public void SentencesFollowBoundaries()
    {
        Doc doc = Analyze("a b . c d . e");
        Assert.Single(doc.Sentences);
        Assert.Equal(0, doc.Sentences[0].Start);
        Assert.Equal(doc.Length, doc.Sentences[0].End);

        doc.SetSentenceStart(3, true);
        doc.SetSentenceStart(6, true);
        Assert.Equal(3, doc.Sentences.Count);
        Assert.Equal(["a b .", "c d .", "e"], doc.Sentences.Select(s => s.ToString()));
        Assert.True(doc[3].IsSentStart);

        doc.SetSentenceStart(3, false);
        Assert.Equal(2, doc.Sentences.Count);
        Assert.False(doc[3].IsSentStart);
        Assert.Empty(Analyze("").Sentences);
    }

    [Fact]
    public void EntitiesSetIobTypesAndSpans()
    {
        Doc doc = Analyze("Ada Lovelace met Charles Babbage in London.");
        doc.SetEntities([new SpanSpec(0, 2, "PER", 0.9f), new SpanSpec(3, 5, "PER"), new SpanSpec(6, 7, "LOC", 0.5f)]);
        Assert.Equal(3, doc.Ents.Count);
        Assert.Equal("Ada Lovelace", doc.Ents[0].ToString());
        Assert.Equal("Charles Babbage", doc.Ents[1].ToString());
        Assert.Equal(0.9f, doc.Ents[0].Confidence);
        Assert.Equal("LOC", doc.Ents[2].Label);
        Assert.Equal(
            [EntIob.Begin, EntIob.Inside, EntIob.Outside, EntIob.Begin, EntIob.Inside, EntIob.Outside, EntIob.Begin, EntIob.Outside],
            doc.Select(t => t.EntIob));
        Assert.Equal("PER", doc[1].EntType);
        Assert.Equal("", doc[2].EntType);

        doc.SetEntities([]);
        Assert.Empty(doc.Ents);
        Assert.All(doc, t => Assert.Equal(EntIob.Outside, t.EntIob));

        Assert.Throws<ArgumentException>(() => doc.SetEntities([new SpanSpec(0, 3, "A"), new SpanSpec(2, 4, "B")]));
        Assert.Throws<ArgumentException>(() => doc.SetEntities([new SpanSpec(2, 3, "A"), new SpanSpec(0, 1, "B")]));
        Assert.Throws<ArgumentException>(() => doc.SetEntities([new SpanSpec(0, 0, "A")]));
        Assert.Throws<ArgumentException>(() => doc.SetEntities([new SpanSpec(0, 99, "A")]));
        Assert.Throws<ArgumentException>(() => doc.SetEntities([new SpanSpec(0, 1, "")]));
    }

    [Fact]
    public void SpanOffsetsAndViews()
    {
        const string text = "The  quick brown fox";
        Doc doc = Analyze(text);
        Span span = doc.Slice(1, 2);
        Assert.Equal(1, span.Start);
        Assert.Equal(3, span.End);
        Assert.Equal(2, span.Length);
        Assert.Equal(5, span.StartChar);
        Assert.Equal(16, span.EndChar);
        Assert.Equal("quick brown", span.ToString());
        Assert.True(span.Text.SequenceEqual("quick brown"));
        Assert.Equal("brown", span[1].ToString());
        Assert.Equal(["quick", "brown"], EnumerateTokens(span));
        Assert.Equal(1f, span.Confidence);
        Assert.Equal("", span.Label);

        Span empty = doc.Slice(2, 0);
        Assert.Equal(empty.StartChar, empty.EndChar);
        Assert.Equal("", empty.ToString());
        Assert.Equal(text.Length, doc.Slice(4, 0).StartChar);
        Assert.Throws<ArgumentOutOfRangeException>(() => doc.Slice(3, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => span[2]);
        Assert.Equal(doc[^1], doc[3]);
        Assert.Equal(doc.Slice(1, 2), doc[1..3]);

        Span labelled = span.With("X", 0.5f);
        Assert.Equal("X", labelled.Label);
        Assert.Equal(0.5f, labelled.Confidence);
        Assert.NotEqual(span, labelled);
    }

    private static string[] EnumerateTokens(Span span)
    {
        var result = new List<string>();
        foreach (Token t in span)
        {
            result.Add(t.ToString());
        }

        return [.. result];
    }

    [Fact]
    public void CharSpansMustFallOnTokenBoundaries()
    {
        Doc d2 = Analyze("Meet Ada Lovelace today");
        Assert.True(d2.TryGetCharSpan(5, 17, out Span span, "PER"));
        Assert.Equal("Ada Lovelace", span.ToString());
        Assert.Equal((5, 17), (span.StartChar, span.EndChar));
        Assert.Equal("PER", span.Label);
        Assert.False(d2.TryGetCharSpan(6, 17, out _));
        Assert.False(d2.TryGetCharSpan(5, 16, out _));
        Assert.False(d2.TryGetCharSpan(5, 5, out _));
        Assert.False(d2.TryGetCharSpan(17, 5, out _));
        Assert.True(d2.TryGetCharSpan(0, 4, out Span first));
        Assert.Equal("Meet", first.ToString());
    }

    [Fact]
    public void FindTokenReturnsMinusOneInGaps()
    {
        Doc doc = Analyze("ab  cd");
        Assert.Equal(-1, doc.FindToken(2));
        Assert.Equal(-1, doc.FindToken(3));
        Assert.Equal(-1, doc.FindToken(6));
        Assert.Equal(-1, doc.FindToken(-1));
        Assert.Equal(1, doc.FindToken(5));
    }
}
