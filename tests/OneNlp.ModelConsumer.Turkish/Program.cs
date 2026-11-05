using RecursiveAI.OneNlp;

namespace RecursiveAI.OneNlp.ModelConsumer.Turkish;

// Uses the happy-path verbs on Turkish text the way an application does. With the Turkish model package referenced everything must work with
// no setup; without it the model verbs must fail naming the package to add, and Analyze must still give tokens and sentences.
internal static class Program
{
    private static int failures;

    private static int Main()
    {
        const string text = "Mustafa Kemal Atatürk 1881 yılında Selanik'te doğdu. Ankara'da çok sayıda insan toplanıyordu!";

        Doc doc = Nlp.Analyze(text);
        Check("two sentences", doc.Sentences.Count == 2);
        Check("a suffix after an apostrophe stays in the token", doc.Any(t => t.ToString() == "Selanik'te") && doc.Any(t => t.ToString() == "Ankara'da"));

#if NO_MODEL
        Check("no tags without a model", doc[0].Pos.Length == 0);
        InvalidOperationException? error = Capture(() => Nlp.Entities(text));
        Check("entities error names the package", error is not null && error.Message.Contains("RecursiveAI.OneNlp.Models.Turkish", StringComparison.Ordinal));
        Check("lemmas error names the package", Capture(() => Nlp.Lemmas(text))?.Message.Contains("RecursiveAI.OneNlp.Models.Turkish", StringComparison.Ordinal) == true);
        Console.WriteLine(error?.Message);
#else
        // Structure, not accuracy: every token is tagged, lemmatized and attached, each sentence has one root, and entities carry known labels.
        Check("tags", doc.All(t => t.Pos.Length > 0 && t.Tag.Length > 0 && t.Lemma.Length > 0 && t.Dep.Length > 0));
        Check("one root per sentence (counted over the document)", doc.Count(t => t.Dep == "root") == doc.Sentences.Count);
        Check("entities", Nlp.Entities(text).Count > 0 && Nlp.Entities(text).All(e => e.Label is "PER" or "ORG" or "LOC" && e.Confidence > 0));
        Check("entities match analyze", doc.Ents.Count == Nlp.Entities(text).Count);
        Check("many", Nlp.AnalyzeMany([text, "Ankara Türkiye'nin başkentidir."]).Count() == 2);
        Console.WriteLine(string.Join(" | ", doc.Select(t => $"{t}/{t.Pos}/{t.Lemma}/{t.Dep}")));
        Console.WriteLine(string.Join(", ", Nlp.Entities(text).Select(e => e.Label + ":" + e)));
#endif
        Console.WriteLine(failures == 0 ? "OK" : $"FAILED {failures}");
        return failures == 0 ? 0 : 1;
    }

    private static InvalidOperationException? Capture(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (InvalidOperationException e)
        {
            return e;
        }
    }

    private static void Check(string name, bool ok)
    {
        if (!ok)
        {
            failures++;
            Console.WriteLine("FAIL " + name);
        }
    }
}
