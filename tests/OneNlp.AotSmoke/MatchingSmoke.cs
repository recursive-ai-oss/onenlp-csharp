using System.Text;
using RecursiveAI.OneNlp.Matching;

namespace RecursiveAI.OneNlp.AotSmoke;

/// <summary>Exercises the rule matchers under NativeAOT: patterns from JSON and the builder, both regex engines, rule files and the registered ruler.</summary>
internal static class MatchingSmoke
{
    internal static void Run(Action<string, bool> check)
    {
        Doc doc = Nlp.Analyze("Acme Corp filed case K1234 on 2024 with Paris Hilton, not the other Hilton.");

        var ruler = new EntityRuler { ["CASE_NO"] = Pattern.Shape("Xdddd") };
        ruler.AddPhrase("ORG", "acme corp", "acme");
        ruler.Add("PER", PatternJson.ParsePattern("""[{"TEXT":"Paris"},{"LOWER":{"REGEX":"^hil+ton$"}}]"""));
        var lowerRuler = new EntityRuler(overwriteEnts: false, TokenProperty.Lower);
        foreach (EntityRulerPattern rule in ruler.Patterns)
        {
            lowerRuler.Add(rule);
        }

        check("ruler id", lowerRuler.FindEntities(doc).Any(e => e.Id == "acme"));
        lowerRuler.Process(doc);
        check("ruler entities", string.Join(" ", doc.Ents.Select(e => e.Label + ":" + e.ToString())) == "ORG:Acme Corp CASE_NO:K1234 PER:Paris Hilton");

        // The look-ahead sends the pattern to the backtracking engine; the other expressions run on the linear-time one.
        var matcher = new Matcher();
        matcher.AddJson("YEAR", """[{"SHAPE":"dddd"}]""");
        matcher.AddJson("LOOK", """[{"LOWER":{"REGEX":"^(?!x)hilton$"}}]""", GreedyMode.Longest);
        matcher.Add("RUN", Pattern.Token(new TokenSpec().Is(TokenProperty.IsAlpha, true).OneOrMore(), new TokenSpec().Is(TokenProperty.IsPunct, true)), GreedyMode.First);
        check("matcher keys", string.Join(",", matcher.Match(doc).Select(m => m.Key)) == "YEAR,LOOK,RUN,LOOK,RUN");

        var phrases = new PhraseMatcher(TokenProperty.Lower);
        phrases.Add("PAIR", ["paris hilton", "hilton"]);
        check("phrase matcher", phrases.Match(doc).Count == 3);

        using var stream = new MemoryStream();
        ruler.SavePatterns(stream);
        var loaded = new EntityRuler();
        string saved = Encoding.UTF8.GetString(stream.ToArray());
        stream.Position = 0;
        loaded.LoadPatterns(stream);
        check("rule file round trip", loaded.Patterns.Count == 3 && saved.Contains("\"CASE_NO\"", StringComparison.Ordinal));

        using Pipeline pipeline = Pipeline.Blank(Language.English).Add(EntityRuler.ComponentName, new Dictionary<string, string> { ["overwrite_ents"] = "true" });
        ((EntityRuler)pipeline.Components[0]).AddPhrase("ORG", "Acme Corp");
        check("registered ruler", pipeline.Process("Acme Corp").Ents.Count == 1);
        check("bad pattern", Throws(() => PatternJson.ParsePatterns("[{\"NOPE\":1}]")));
    }

    private static bool Throws(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (PatternFormatException)
        {
            return true;
        }
    }
}
