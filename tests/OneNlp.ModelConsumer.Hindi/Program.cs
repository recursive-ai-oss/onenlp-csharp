using RecursiveAI.OneNlp;

namespace RecursiveAI.OneNlp.ModelConsumer.Hindi;

// Uses the happy-path verbs on Hindi text the way an application does. With the Hindi model package referenced everything must work with
// no setup; without it the model verbs must fail naming the package to add, and Analyze must still give tokens and sentences.
internal static class Program
{
    private static int failures;

    private static int Main()
    {
        const string text = "प्रधानमंत्री नरेंद्र मोदी ने दिल्ली में कल एक बैठक की। उन्होंने कहा कि सरकार तैयार है।";

        Doc doc = Nlp.Analyze(text);
        Check("two sentences", doc.Sentences.Count == 2);
        Check("the danda is a token of its own", doc.Count(t => t.ToString() == "।") == 2);

#if NO_MODEL
        Check("no tags without a model", doc[0].Pos.Length == 0);
        InvalidOperationException? error = Capture(() => Nlp.Entities(text));
        Check("entities error names the package", error is not null && error.Message.Contains("RecursiveAI.OneNlp.Models.Hindi", StringComparison.Ordinal));
        Console.WriteLine(error?.Message);
#else
        // Structure, not accuracy: every token is tagged and attached, each sentence has one root, and entities carry known labels. The model has no
        // lemmatizer or XPOS head, so those stay empty.
        Check("tags", doc.All(t => t.Pos.Length > 0 && t.Dep.Length > 0));
        Check("one root per sentence (counted over the document)", doc.Count(t => t.Dep == "root") == doc.Sentences.Count);
        Check("entities", Nlp.Entities(text).All(e => e.Label is "PER" or "ORG" or "LOC" && e.Confidence > 0));
        Check("entities match analyze", doc.Ents.Count == Nlp.Entities(text).Count);
        Check("many", Nlp.AnalyzeMany([text, "दिल्ली भारत की राजधानी है।"]).Count() == 2);
        Console.WriteLine(string.Join(" | ", doc.Select(t => $"{t}/{t.Pos}/{t.Dep}")));
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
