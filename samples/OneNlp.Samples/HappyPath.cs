using RecursiveAI.OneNlp;

namespace RecursiveAI.OneNlp.Samples;

// The happy path: static task verbs, no options objects. Shown next to Apple's NaturalLanguage in docs/reads-like.md.
internal static class HappyPath
{
    internal static void Run()
    {
        Console.WriteLine("happy path");

        // <sample>
        string text = "Angela Merkel met Emmanuel Macron in Paris on 5 March 2026. The deal cost $5m.";

        Language language = Nlp.DetectLanguage(text);                    // Language.English
        IReadOnlyList<Span> sentences = Nlp.Sentences(text);             // sentence spans, offsets into text
        Token[] words = Nlp.Tokens(text);                                // word tokens
        string[] lemmas = Nlp.Lemmas("The cats were running.");          // "the", "cat", "be", "run", "."
        IReadOnlyList<Span> entities = Nlp.Entities(text);               // "Angela Merkel" PER, "Paris" LOC, ...

        DateTimeOffset today = new(2026, 3, 18, 10, 30, 0, TimeSpan.Zero);
        DateRange? when = Nlp.ParseDate("the first Monday after Easter 2027", today);
        IEnumerable<Quantity> money = Nlp.Quantities(text);              // 5000000 USD
        // </sample>

        Program.Check("language is English", language == Language.English);
        Program.Check("two sentences", sentences.Count == 2);
        Program.Check("words are tokens of the input", words.Length > 10 && words[0].ToString() == "Angela");
        Program.Check("lemmas", lemmas is [_, "cat", "be", _, "."]);
        Console.WriteLine("    entities: " + string.Join(", ", entities.Select(e => e.Label + ":" + e)));
        Program.Check("a person is found", entities.Any(e => e.Label == EntityLabels.Person));
        Program.Check("Paris is a location", entities.Any(e => e.Label == EntityLabels.Location && e.ToString() == "Paris"));
        Program.Check("offsets are into the input", entities.All(e => text.Substring(e.StartChar, e.EndChar - e.StartChar) == e.ToString()));
        Program.Check("Easter 2027 is 28 March, so the Monday after is 29 March", when is { } d && d.Start.Date == new DateTime(2027, 3, 29));
        Program.Check("$5m is 5,000,000 USD", money.Any(q => q.Kind == QuantityKind.Money && q.Value == 5_000_000m && q.Currency == "USD"));

        // The whole analysis in one call: tokens, tags, lemmas, the parse and the entities.
        Doc doc = Nlp.Analyze(text);
        Program.Check("analysis carries a parse", doc.Any(t => t.Dep == "root"));
        foreach (Token t in doc.Take(4))
        {
            Console.WriteLine($"    {t} {t.Pos} {t.Lemma} {t.Dep}");
        }
    }
}
