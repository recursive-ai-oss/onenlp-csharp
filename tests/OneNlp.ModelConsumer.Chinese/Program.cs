using RecursiveAI.OneNlp;

namespace RecursiveAI.OneNlp.ModelConsumer.Chinese;

// Uses the happy-path verbs on Chinese text the way an application does. With the Chinese model package referenced everything must work with
// no setup; without it the model verbs must fail naming the package to add, and Analyze must still give tokens and sentences.
internal static class Program
{
    private static int failures;

    private static int Main()
    {
        const string text = "李明在北京大学学习计算机科学。他明天去上海。";

        Doc doc = Nlp.Analyze(text);
        Check("two sentences", doc.Sentences.Count == 2);

#if NO_MODEL
        Check("no tags without a model", doc[0].Pos.Length == 0);
        InvalidOperationException? error = Capture(() => Nlp.Entities(text));
        Check("entities error names the package", error is not null && error.Message.Contains("RecursiveAI.OneNlp.Models.Chinese", StringComparison.Ordinal));
        Check("lemmas error names the package", Capture(() => Nlp.Lemmas(text))?.Message.Contains("RecursiveAI.OneNlp.Models.Chinese", StringComparison.Ordinal) == true);
        Console.WriteLine(error?.Message);
#else
        // Structure, not accuracy: the tokens are words (fewer than the characters), every token is tagged, lemmatized and attached, each
        // sentence has one root, and entities carry known labels and offsets into the text.
        Check("words, not characters", doc.Length < text.Length - 4 && doc.Any(t => t.ToString().Length > 1));
        Check("offsets", doc.All(t => text.Substring(t.Idx, t.EndIdx - t.Idx) == t.ToString()));
        Check("tags", doc.All(t => t.Pos.Length > 0 && t.Tag.Length > 0 && t.Lemma.Length > 0 && t.Dep.Length > 0));
        Check("one root per sentence (counted over the document)", doc.Count(t => t.Dep == "root") == doc.Sentences.Count);
        Check("entities", Nlp.Entities(text).Count > 0 && Nlp.Entities(text).All(e => e.Label is "PER" or "ORG" or "LOC" && e.Confidence > 0));
        Check("entities match analyze", doc.Ents.Count == Nlp.Entities(text).Count);
        Check("many", Nlp.AnalyzeMany([text, "我爱北京。"]).Count() == 2);
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
