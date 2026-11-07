namespace RecursiveAI.OneNlp.Tests;

public class NlpOpenEntitiesTests
{
    // Marks every token that starts with an upper-case letter with the first type given.
    private sealed class Capitals : IOpenEntityRecognizer
    {
        public string Name => "capitals";

        public void Process(Doc doc)
        {
        }

        public void Recognize(Doc doc, IReadOnlyList<string> types, float? threshold = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(types.Count == 0 ? null : types[0]);
            var found = new List<SpanSpec>();
            for (int i = 0; i < doc.Length; i++)
            {
                if (char.IsUpper(doc[i].Text[0]))
                {
                    found.Add(new SpanSpec(i, i + 1, types[0], 0.9f));
                }
            }

            doc.SetEntities(found.ToArray());
        }
    }

    [Fact]
    public void MissingModelNamesThePackageThenRegisteredRecognizerIsUsed()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => Nlp.Entities("Anna met Bob.", "person"));
        Assert.Contains("RecursiveAI.OneNlp.Models.Multilingual", ex.Message, StringComparison.Ordinal);

        ModelPackages.RegisterOpenEntities("RecursiveAI.OneNlp.Models.Multilingual", () => Pipeline.Blank(Language.English).Add(new Capitals()));
        IReadOnlyList<Span> found = Nlp.Entities("Anna met Bob.", "first name");
        Assert.Equal(["Anna", "Bob"], found.Select(s => s.Text.ToString()));
        Assert.All(found, s => Assert.Equal("first name", s.Label));
        Assert.Equal(0, found[0].Start);
        Assert.Empty(Nlp.Entities("", "person"));
        Assert.ThrowsAny<ArgumentException>(() => Nlp.Entities("Anna", Array.Empty<string>()));
    }
}
