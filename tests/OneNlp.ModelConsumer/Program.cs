using RecursiveAI.OneNlp;

namespace RecursiveAI.OneNlp.ModelConsumer;

// Uses the happy-path verbs the way an application does. With the English model package referenced everything must work with no
// setup; without it the model verbs must fail naming the package to add, and Analyze must still give tokens and sentences.
internal static class Program
{
    private static int failures;

    private static int Main()
    {
#if LANG_GERMAN
        const string text = "Angela Merkel besuchte im Mai die Stadt Hamburg. Sie sprach zum Thema Energie!";
        const string package = "RecursiveAI.OneNlp.Models.German";
#elif LANG_PORTUGUESE
        const string text = "Ana Maria visitou a cidade de Lisboa em maio. Ela falou sobre o clima!";
        const string package = "RecursiveAI.OneNlp.Models.Portuguese";
#elif LANG_DUTCH
        const string text = "Willem Alexander bezocht in mei de stad Amsterdam. Hij sprak over het klimaat!";
        const string package = "RecursiveAI.OneNlp.Models.Dutch";
#else
        const string text = "Ada Lovelace worked with Charles Babbage in London. She wrote the first program!";
        const string package = "RecursiveAI.OneNlp.Models.English";
#endif

        Console.WriteLine(package);
        Doc doc = Nlp.Analyze(text);
        Check("sentences", doc.Sentences.Count == 2);

#if NO_MODEL
        Check("no tags without a model", doc[0].Pos.Length == 0);
        InvalidOperationException? error = Capture(() => Nlp.Entities(text));
        Check("entities error names the package", error is not null && error.Message.Contains(package, StringComparison.Ordinal));
        Check("lemmas error names the package", Capture(() => Nlp.Lemmas(text))?.Message.Contains(package, StringComparison.Ordinal) == true);
        Console.WriteLine(error?.Message);
#else
        // Structure, not accuracy: every token is tagged, lemmatized and attached, each sentence has one root, and entities carry known labels.
#if LANG_PORTUGUESE
        // UD Portuguese-Bosque has no XPOS column, so the package carries no tagger and Token.Tag stays empty.
        Check("tags", doc.All(t => t.Pos.Length > 0 && t.Lemma.Length > 0 && t.Dep.Length > 0));
#else
        Check("tags", doc.All(t => t.Pos.Length > 0 && t.Tag.Length > 0 && t.Lemma.Length > 0 && t.Dep.Length > 0));
#endif
        Check("one root per sentence (counted over the document)", doc.Count(t => t.Dep == "root") == doc.Sentences.Count);
#if LANG_ENGLISH_CHECKS_English
        string[] lemmas = Nlp.Lemmas("The cats were running.");
        Check("lemma " + string.Join(" ", lemmas), lemmas is [_, "cat", "be", _, "."]);
#endif
        Check("entities", Nlp.Entities(text).Count > 0 && Nlp.Entities(text).All(e => e.Label is "PER" or "ORG" or "LOC" or "MISC" && e.Confidence > 0));
        Check("entities match analyze", doc.Ents.Count == Nlp.Entities(text).Count);
        Check("many", Nlp.AnalyzeMany([text, "Paris is in France."]).Count() == 2);
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
