using RecursiveAI.OneNlp;

namespace RecursiveAI.OneNlp.ModelConsumer.Multilingual;

// Uses the happy-path verbs the way an application does, in each of the nine languages. With the multilingual package referenced everything
// must work with no setup; without it the model verbs must fail naming the package to add.
internal static class Program
{
    private static int failures;

    private static int Main()
    {
        (string Language, string Text)[] texts =
        [
            ("en", "Angela Merkel visited the European Parliament in Brussels last Tuesday. She spoke about energy policy and climate."),
            ("de", "Angela Merkel besuchte am Dienstag das Europäische Parlament in Brüssel. Sie sprach über die Energiepolitik und das Klima."),
            ("fr", "Emmanuel Macron a rencontré les journalistes à Paris hier soir. Il a parlé du budget et de la politique européenne."),
            ("es", "Pedro Sánchez visitó la ciudad de Sevilla el martes por la mañana. Habló con los alcaldes sobre el presupuesto del año."),
            ("pt", "O presidente visitou a cidade do Porto na terça-feira de manhã. Ele falou com os jornalistas sobre o orçamento do país."),
            ("it", "Il presidente Mattarella ha visitato la città di Napoli martedì mattina. Ha parlato con i giornalisti del bilancio dello Stato."),
            ("nl", "Mark Rutte bezocht afgelopen dinsdag de stad Amsterdam. Hij sprak met de journalisten over de begroting van het land."),
            ("tr", "Cumhurbaşkanı salı günü İstanbul şehrini ziyaret etti. Gazetecilerle ülkenin bütçesi hakkında konuştu ve soruları yanıtladı."),
            ("zh", "李克强总理星期二访问了上海市。他和记者们谈论了国家的经济预算，并回答了他们的问题。"),
        ];

#if NO_MODEL
        foreach ((string lang, string text) in texts)
        {
            InvalidOperationException? error = Capture(() => Nlp.Lemmas(text));
            Check(lang + " lemmas error names the package", error?.Message.Contains("RecursiveAI.OneNlp.Models.Multilingual", StringComparison.Ordinal) == true);
        }

        InvalidOperationException? open = Capture(() => Nlp.Entities("Angela Merkel visited Brussels.", "politician"));
        Check("open-type entities error names the package", open?.Message.Contains("RecursiveAI.OneNlp.Models.Multilingual", StringComparison.Ordinal) == true);
        Console.WriteLine(open?.Message);
#else
        // Structure, not accuracy: every token is tagged, lemmatized and attached, and every sentence has a root.
        foreach ((string lang, string text) in texts)
        {
            Doc doc = Nlp.Analyze(text);
            Check(lang + " language", Nlp.DetectLanguage(text).Code == lang);
            Check(lang + " sentences", doc.Sentences.Count >= 2);
            Check(lang + " tags", doc.All(t => t.Pos.Length > 0 && t.Lemma.Length > 0 && t.Dep.Length > 0));
            Check(lang + " one root per sentence", doc.Count(t => t.Dep == "root") == doc.Sentences.Count);
            Check(lang + " entities match analyze", doc.Ents.Count == Nlp.Entities(text).Count);
            Console.WriteLine(lang + ": " + string.Join(" | ", doc.Take(8).Select(t => $"{t}/{t.Pos}/{t.Lemma}/{t.Dep}")) + "  ents: " + string.Join(", ", doc.Ents.Select(e => e.Label + ":" + e)));
        }

        Check("entities in English", Nlp.Entities(texts[0].Text).Any(e => e.Label == EntityLabels.Person));
        IReadOnlyList<Span> open = Nlp.Entities(texts[0].Text, "politician", "city");
        Check("open-type entities", open.Count > 0 && open.All(e => e.Label is "politician" or "city" && e.Confidence > 0));
        Console.WriteLine("open: " + string.Join(", ", open.Select(e => e.Label + ":" + e)));
        Check("many", Nlp.AnalyzeMany([texts[1].Text, texts[2].Text]).Count() == 2);
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
