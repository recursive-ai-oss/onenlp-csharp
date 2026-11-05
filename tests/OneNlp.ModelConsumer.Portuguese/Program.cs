using RecursiveAI.OneNlp;

namespace RecursiveAI.OneNlp.ModelConsumer.Portuguese;

// Uses the happy-path verbs on Portuguese text the way an application does. With the Portuguese model package referenced everything must work with
// no setup; without it the model verbs must fail naming the package to add, and Analyze must still give tokens and sentences.
internal static class Program
{
    private static int failures;

    private static int Main()
    {
        const string text = "Maria Silva visitou a cidade de Lisboa em maio. Ela falou do tema da energia!";

        Doc doc = Nlp.Analyze(text);
        Check("two sentences", doc.Sentences.Count == 2);
        Check("do is two pieces", doc.Any(t => t.ToString() == "d") && doc.Any(t => t.ToString() == "o"));

#if NO_MODEL
        Check("no tags without a model", doc[0].Pos.Length == 0);
        InvalidOperationException? error = Capture(() => Nlp.Entities(text));
        Check("entities error names the package", error is not null && error.Message.Contains("RecursiveAI.OneNlp.Models.Portuguese", StringComparison.Ordinal));
        Check("lemmas error names the package", Capture(() => Nlp.Lemmas(text))?.Message.Contains("RecursiveAI.OneNlp.Models.Portuguese", StringComparison.Ordinal) == true);
        Console.WriteLine(error?.Message);
#else
        // Structure, not accuracy: every token has a part of speech, a lemma and a head (Bosque has no XPOS, so the tag column stays empty), each sentence has one root, and entities carry known labels.
        Check("tags", doc.All(t => t.Pos.Length > 0 && t.Lemma.Length > 0 && t.Dep.Length > 0));
        Check("one root per sentence (counted over the document)", doc.Count(t => t.Dep == "root") == doc.Sentences.Count);
        Check("entities", Nlp.Entities(text).Count > 0 && Nlp.Entities(text).All(e => e.Label is "PER" or "ORG" or "LOC" && e.Confidence > 0));
        Check("entities match analyze", doc.Ents.Count == Nlp.Entities(text).Count);
        Check("many", Nlp.AnalyzeMany([text, "Lisboa fica em Portugal."]).Count() == 2);
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
