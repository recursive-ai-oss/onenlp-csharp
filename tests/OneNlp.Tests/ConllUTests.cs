using System.Text;
using RecursiveAI.OneNlp.Data;

namespace RecursiveAI.OneNlp.Tests;

public class ConllUTests
{
    // Hand-written sample: comments, a multiword token whose words do not spell the surface form, one that does, an empty node,
    // SpaceAfter=No, a DEPS column, and an unannotated column.
    private const string Sample =
        "# newdoc id = d1\n"
        + "# sent_id = s1\n"
        + "# text = Vámonos al cine, don't.\n"
        + "1-2\tVámonos\t_\t_\t_\t_\t_\t_\t_\t_\n"
        + "1\tVamos\tir\tVERB\tVB\tMood=Imp\t0\troot\t_\t_\n"
        + "2\tnos\tnosotros\tPRON\tPRP\t_\t1\tobj\t_\t_\n"
        + "3-4\tal\t_\t_\t_\t_\t_\t_\t_\t_\n"
        + "3\ta\ta\tADP\tIN\t_\t5\tcase\t_\t_\n"
        + "4\tel\tel\tDET\tDT\t_\t5\tdet\t_\t_\n"
        + "5\tcine\tcine\tNOUN\tNN\tNumber=Sing\t1\tobj\t_\tSpaceAfter=No\n"
        + "5.1\tghost\tghost\tVERB\t_\t_\t_\t_\t1:conj\t_\n"
        + "6\t,\t,\tPUNCT\t,\t_\t1\tpunct\t_\t_\n"
        + "7-8\tdon't\t_\t_\t_\t_\t_\t_\t_\tSpaceAfter=No\n"
        + "7\tdo\tdo\tAUX\tVBP\t_\t1\taux\t1:aux\t_\n"
        + "8\tn't\tnot\tPART\tRB\t_\t7\tadvmod\t_\t_\n"
        + "9\t.\t.\tPUNCT\t.\t_\t1\tpunct\t_\tSpaceAfter=No\n"
        + "\n"
        + "# sent_id = s2\n"
        + "1\tHi\thi\tINTJ\tUH\t_\t0\troot\t_\tSpaceAfter=No\n"
        + "\n";

    private static List<ConllUSentence> Parse(string text, ConllUOptions? options = null) =>
        [.. ConllUReader.Read(new StringReader(text), null, options)];

    [Fact]
    public void ParsesColumnsTextAndSideInformation()
    {
        List<ConllUSentence> sentences = Parse(Sample);
        Assert.Equal(2, sentences.Count);
        ConllUSentence s = sentences[0];
        Doc doc = s.Doc;
        Assert.Equal("Vámonos al cine, don't.", doc.Text);
        Assert.Equal(9, doc.Length);
        Assert.Equal("s1", s.GetComment("sent_id"));
        Assert.Equal("Vámonos al cine, don't.", s.GetComment("text"));
        Assert.Equal("d1", s.GetComment("newdoc id"));

        // "Vámonos" (7 chars) is cut into 4 + 3 because the word forms do not spell it; "don't" is split as "do" + "n't".
        Assert.Equal("Vámo", doc[0].ToString());
        Assert.Equal("nos", doc[1].ToString());
        Assert.Equal("do", doc[6].ToString());
        Assert.Equal("n't", doc[7].ToString());
        Assert.Equal("Vamos", s.Forms[0]);

        Assert.Equal("ir", doc[0].Lemma);
        Assert.Equal("VERB", doc[0].Pos);
        Assert.Equal("VB", doc[0].Tag);
        Assert.Equal("Mood=Imp", doc[0].Morph);
        Assert.Equal(0, doc[0].HeadIndex);
        Assert.Equal("root", doc[0].Dep);
        Assert.Equal(0, doc[1].HeadIndex);
        Assert.Equal(4, doc[2].HeadIndex);
        Assert.Equal(string.Empty, doc[1].Morph);
        Assert.True(doc[0].IsSentStart);
        Assert.False(doc[1].IsSentStart);

        Assert.Equal(3, s.MultiwordTokens.Count);
        Assert.Equal(new MultiwordToken(0, 1, "Vámonos", string.Empty), s.MultiwordTokens[0]);
        Assert.Equal(new MultiwordToken(2, 3, "al", string.Empty), s.MultiwordTokens[1]);
        Assert.Equal(new MultiwordToken(6, 7, "don't", "SpaceAfter=No"), s.MultiwordTokens[2]);
        Assert.Single(s.EmptyNodes);
        Assert.Equal(5, s.EmptyNodes[0].WordsBefore);
        Assert.Equal("1:aux", s.Deps[6]);
        Assert.Equal("SpaceAfter=No", s.Misc[4]);
    }

    [Fact]
    public void WritingTheSentencesGivesTheInputBack()
    {
        var sb = new StringBuilder();
        using (var writer = new StringWriter(sb))
        {
            foreach (ConllUSentence s in Parse(Sample))
            {
                ConllUWriter.Write(writer, s);
            }
        }

        Assert.Equal(Sample, sb.ToString());
    }

    [Fact]
    public void WritesAPredictedDocWithSpaceAfterNo()
    {
        Doc doc = Nlp.Analyze("Hi, you.");
        var sb = new StringBuilder();
        using (var writer = new StringWriter(sb))
        {
            ConllUWriter.Write(writer, doc);
        }

        Assert.Equal(
            "1\tHi\t_\t_\t_\t_\t_\t_\t_\tSpaceAfter=No\n2\t,\t_\t_\t_\t_\t_\t_\t_\t_\n3\tyou\t_\t_\t_\t_\t_\t_\t_\tSpaceAfter=No\n4\t.\t_\t_\t_\t_\t_\t_\t_\t_\n\n",
            sb.ToString());
    }

    [Fact]
    public void TheLastSentenceNeedsNoBlankLineAndCrLfIsAccepted()
    {
        List<ConllUSentence> s = Parse("1\tA\ta\tX\t_\t_\t0\troot\t_\t_\r\n\r\n1\tB\tb\tX\t_\t_\t0\troot\t_\t_");
        Assert.Equal(2, s.Count);
        Assert.Equal("B", s[1].Doc.Text);
    }

    [Fact]
    public void AMultiwordTokenShorterThanItsWordsSpellsTheWordsInTheText()
    {
        // Synthetic: one character standing for two words, then a one-character token for three words (as in Portuguese contractions).
        const string Text =
            "1-2\t\u00e0\t_\t_\t_\t_\t_\t_\t_\t_\n"
            + "1\ta\ta\tADP\t_\t_\t3\tcase\t_\t_\n"
            + "2\ta\ta\tDET\t_\t_\t3\tdet\t_\t_\n"
            + "3\tcasa\tcasa\tNOUN\t_\t_\t0\troot\t_\t_\n\n";
        ConllUSentence s = Parse(Text)[0];
        Doc d = s.Doc;
        Assert.Equal(3, d.Length);
        Assert.Equal("a a casa", d.Text);
        Assert.Equal((0, 1), (d[0].Idx, d[0].EndIdx));
        Assert.Equal((2, 3), (d[1].Idx, d[1].EndIdx));
        Assert.Equal((4, 8), (d[2].Idx, d[2].EndIdx));
        Assert.Equal(new MultiwordToken(0, 1, "\u00e0", string.Empty), s.MultiwordTokens[0]);
        var sb = new StringBuilder();
        using (var w = new StringWriter(sb))
        {
            ConllUWriter.Write(w, s);
        }

        Assert.StartsWith("1-2\t\u00e0\t", sb.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("1\tA\ta\tX\t_\t_\t0\troot\t_\n", 1, "10 tab-separated columns")]
    [InlineData("1\tA\ta\tX\t_\t_\t0\troot\t_\t_\n3\tB\tb\tX\t_\t_\t1\tdep\t_\t_\n", 2, "out of sequence")]
    [InlineData("1\tA\ta\tX\t_\t_\t2\tdep\t_\t_\n", 1, "past the last word")]
    [InlineData("1\tA\ta\tX\t_\t_\t1\tdep\t_\t_\n", 1, "own head")]
    [InlineData("1\tA\ta\tX\t_\t_\tx\tdep\t_\t_\n", 1, "HEAD")]
    [InlineData("1\t\ta\tX\t_\t_\t0\troot\t_\t_\n", 1, "FORM")]
    [InlineData("1-2\tAB\t_\t_\t_\t_\t_\t_\t_\t_\n1\tA\ta\tX\t_\t_\t0\troot\t_\t_\n\n", 2, "extends past")]
    [InlineData("1\tA\ta\tX\t_\t_\t0\troot\t_\t_\n# late\n", 2, "comment line must come before")]
    [InlineData("# only a comment\n\n", 1, "no words")]
    [InlineData("1-1\tA\t_\t_\t_\t_\t_\t_\t_\t_\n", 1, "not a range")]
    [InlineData("1\tA\ta\tX\t_\t_\t0\troot\t_\t_\n1.x\tB\t_\t_\t_\t_\t_\t_\t_\t_\n", 2, "N.M")]
    public void RejectsMalformedInputWithTheLineNumber(string text, int line, string fragment)
    {
        var e = Assert.Throws<DataFormatException>(() => Parse(text));
        Assert.Equal(line, e.LineNumber);
        Assert.Contains(fragment, e.Message, StringComparison.Ordinal);
        Assert.StartsWith($"line {line}:", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadsLazilyOneSentenceAtATime()
    {
        string text = "1\tA\ta\tX\t_\t_\t0\troot\t_\t_\n\nbroken line\n";
        ConllUSentence first = ConllUReader.Read(new StringReader(text)).First();
        Assert.Equal("A", first.Doc.Text);
        Assert.Throws<DataFormatException>(() => ConllUReader.Read(new StringReader(text)).ToList());
    }

    [Fact]
    public void BoundsLineAndSentenceSize()
    {
        string longLine = "1\t" + new string('a', 200) + "\ta\tX\t_\t_\t0\troot\t_\t_\n";
        Assert.Throws<DataFormatException>(() => Parse(longLine, new ConllUOptions { MaxLineLength = 100 }));
        string many = string.Concat(Enumerable.Range(1, 20).Select(i => $"{i}\tA\ta\tX\t_\t_\t0\troot\t_\t_\n"));
        Assert.Throws<DataFormatException>(() => Parse(many, new ConllUOptions { MaxSentenceLines = 10 }));
        string comments = string.Concat(Enumerable.Repeat("# c\n", 20)) + "1\tA\ta\tX\t_\t_\t0\troot\t_\t_\n";
        Assert.Throws<DataFormatException>(() => Parse(comments, new ConllUOptions { MaxCommentLines = 10 }));
    }

    [Fact]
    public void RejectsInvalidUtf8FromAStream()
    {
        byte[] bytes = [.. Encoding.UTF8.GetBytes("1\tA\ta\tX\t_\t_\t0\troot\t_\t_\n\n1\tB"), 0xFF, 0xFE, (byte)'\n'];
        using var stream = new MemoryStream(bytes);
        var e = Assert.Throws<DataFormatException>(() => ConllUReader.Read(stream).ToList());
        Assert.Contains("UTF-8", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExamplesCarryAnUnannotatedPrediction()
    {
        string path = Path.Combine(Path.GetTempPath(), "onenlp-conllu-" + Guid.NewGuid().ToString("N") + ".conllu");
        try
        {
            File.WriteAllText(path, Sample);
            List<Example> examples = ConllUReader.ReadExamples(path).ToList();
            Assert.Equal(2, examples.Count);
            Assert.Equal("VERB", examples[0].Reference[0].Pos);
            Assert.Equal(string.Empty, examples[0].Predicted[0].Pos);
            Assert.Equal(examples[0].Reference.Length, examples[0].Predicted.Length);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
