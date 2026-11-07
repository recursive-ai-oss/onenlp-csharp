using RecursiveAI.OneNlp;
using RecursiveAI.OneNlp.Matching;

namespace RecursiveAI.OneNlp.Samples;

// The low level: spaCy's documented shape and terms. Shown next to spaCy in docs/reads-like.md.
internal static class PipelineSample
{
    internal static void Run()
    {
        Console.WriteLine("pipeline");

        // <sample>
        // A blank English pipeline: the Unicode word tokenizer, then the components in the order they are added.
        using Pipeline nlp = Pipeline.Blank(Language.English);

        // Rule entities, from token patterns (the vocabulary of spaCy's rule-based matching) or from phrases.
        EntityRuler ruler = new() { OverwriteEnts = true };
        ruler.Add("CASE_NO", Pattern.Shape("dd-dddd"));
        ruler.AddPhrase("ORG", "Recursive AI");
        nlp.Add(ruler);

        Doc doc = nlp.Process("Recursive AI filed case 21-1234 on Monday.");
        foreach (Span e in doc.Ents)
        {
            Console.WriteLine($"{e.Label} {e} {e.StartChar}..{e.EndChar}");
        }

        // The Matcher finds token patterns in any Doc.
        Matcher matcher = new();
        matcher.Add("GREETING", Pattern.Token(new TokenSpec().Lower("hello"), new TokenSpec().Is(TokenProperty.IsPunct, true).Optional(), new TokenSpec().Lower("world")));
        IReadOnlyList<PatternMatch> matches = matcher.Match(nlp.Process("Hello, world! hello world"));

        // Many texts at once, lazily, in batches.
        foreach (Doc d in nlp.ProcessMany(["First text.", "Second text."]))
        {
            Console.WriteLine(d.Sentences.Count);
        }
        // </sample>

        Program.Check("two rule entities", doc.Ents.Count == 2);
        Program.Check("case number", doc.Ents.Any(e => e.Label == "CASE_NO" && e.ToString() == "21-1234"));
        Program.Check("organisation", doc.Ents.Any(e => e.Label == "ORG" && e.ToString() == "Recursive AI"));
        Program.Check("both greetings match", matches.Count == 2);
    }
}
