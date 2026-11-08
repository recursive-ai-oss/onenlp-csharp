using System.Diagnostics;
using RecursiveAI.OneNlp;

namespace RecursiveAI.OneNlp.ModelConsumer.Large;

// Uses the happy-path verbs the way an application does, in languages that have no fast-tier package here (the project references only the
// base-size accurate-tier package). With the package referenced everything must work with no setup; without it the model verbs must fail
// naming a package to add, and Analyze must still give tokens and sentences.
internal static class Program
{
    private static int failures;

    private static int Main(string[] args)
    {
        string[] texts =
        [
            "Angela Merkel besuchte gestern die Universität in Heidelberg.",
            "Ahmet dün İstanbul'dan Ankara'ya gitti.",
            "Jan woont in Amsterdam en werkt bij Philips.",
            "Ada Lovelace worked in London. She wrote the first program.",
            "Barack Obama a visité Paris, et il est venu au musée du Louvre.",
            "李明昨天在北京的清华大学见到了王教授。",
        ];

#if NO_MODEL
        Doc probe = Nlp.Analyze(texts[0]);
        Check("one sentence", probe.Sentences.Count == 1);
        InvalidOperationException? error = Capture(() => Nlp.Entities(texts[0]));
        Check("entities error names a package", error is not null && error.Message.Contains("RecursiveAI.OneNlp.Models", StringComparison.Ordinal));
        Console.WriteLine(error?.Message);
#else
        var clock = Stopwatch.StartNew();
        Doc first = Nlp.Analyze(texts[0]);
        double loadMs = clock.Elapsed.TotalMilliseconds;
        Check("one sentence", first.Sentences.Count == 1);
        Console.WriteLine($"first Analyze (loads the encoder and the heads): {loadMs:F0} ms");
        // A separate process lets /usr/bin/time measure one language without materializing every head.
        if (args is ["--single-language"])
        {
            Check("German tags and parse", first.Length > 0 && first.All(t => t.Pos.Length > 0 && t.Dep.Length > 0));
            Console.WriteLine(failures == 0 ? "OK" : $"FAILED {failures}");
            return failures == 0 ? 0 : 1;
        }
        foreach (string text in texts)
        {
            Doc doc = Nlp.Analyze(text);
            Check("offsets: " + text, doc.All(t => text.Substring(t.Idx, t.EndIdx - t.Idx) == t.ToString()));
            Check("tags: " + text, doc.All(t => t.Pos.Length > 0 && t.Lemma.Length > 0 && t.Dep.Length > 0));
            Check("one root per sentence: " + text, doc.Count(t => t.Dep == "root") == doc.Sentences.Count);
            Check("entities match: " + text, doc.Ents.Count == Nlp.Entities(text).Count);
            Console.WriteLine(string.Join(" ", doc.Select(t => $"{t}/{t.Pos}/{t.Lemma}")) + "  ||  " + string.Join(", ", doc.Ents.Select(e => e.Label + ":" + e)));
        }

        Check("84 registered languages", RecursiveAI.OneNlp.Models.Multilingual.Large.MultilingualLargeModel.Languages.Count == 84);
        foreach (string code in RecursiveAI.OneNlp.Models.Multilingual.Large.MultilingualLargeModel.Languages)
        {
            using Pipeline loaded = RecursiveAI.OneNlp.Models.Multilingual.Large.MultilingualLargeModel.Load(code);
            Check("load heads: " + code, loaded.Language.Code == code);
        }

        foreach ((string code, string text) in new[]
        {
            ("cs", "Praha je hlavní město České republiky."),
            ("ja", "東京は日本の首都です。"),
            ("fi", "Helsinki on Suomen pääkaupunki."),
        })
        {
            using Pipeline accurate = Pipeline.Create(Language.FromCode(code), Tier.Accurate)
                .Add<RecursiveAI.OneNlp.Neural.Components.Morphologizer>()
                .Add<RecursiveAI.OneNlp.Neural.Components.Lemmatizer>()
                .Add<RecursiveAI.OneNlp.Neural.Components.DependencyParser>();
            Doc doc = accurate.Process(text);
            Check("expanded language offsets: " + code, doc.All(t => text.Substring(t.Idx, t.EndIdx - t.Idx) == t.ToString()));
            Check("expanded language heads: " + code, doc.Length > 0 && doc.All(t => t.Pos.Length > 0 && t.Dep.Length > 0));
        }

        Check("a German person", Nlp.Entities(texts[0]).Any(e => e.Label == "PER" && e.ToString().Contains("Merkel", StringComparison.Ordinal)));
        Check("a Turkish place", Nlp.Entities(texts[1]).Any(e => e.Label == "LOC"));
        Check("many", Nlp.AnalyzeMany(texts).Count() == texts.Length);
        Check("first call", first.Length > 0);
        Console.WriteLine($"peak working set: {Process.GetCurrentProcess().PeakWorkingSet64 / (1024 * 1024)} MB");
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
