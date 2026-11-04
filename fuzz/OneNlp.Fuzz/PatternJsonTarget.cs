using RecursiveAI.OneNlp.Matching;

namespace RecursiveAI.OneNlp.Fuzz;

/// <summary>
/// Feeds arbitrary bytes to the pattern readers (a token-pattern list, and an entity-rule file as a JSON array or JSON Lines). Only
/// <see cref="PatternFormatException"/> may escape. What parses must write back to JSON that parses to the same pattern, match on a
/// small document with in-range, ordered results, and survive being saved and loaded as an entity-rule file.
/// </summary>
internal static class PatternJsonTarget
{
    private static readonly Doc Sample = Pipeline.Blank().Process("Hello, World 42 x-y test@example.com The cat sat on the mat. Acme Corp");

    internal static void Run(ReadOnlySpan<byte> data)
    {
        IReadOnlyList<Pattern>? patterns = null;
        try
        {
            patterns = PatternJson.ParsePatterns(data);
        }
        catch (PatternFormatException)
        {
        }

        if (patterns is not null)
        {
            var matcher = new Matcher();
            foreach (Pattern p in patterns)
            {
                string json = PatternJson.ToJson(p);
                Require(PatternJson.ToJson(PatternJson.ParsePattern(json)) == json, "pattern JSON round trip");
                matcher.Add("k", p);
            }

            CheckMatches(matcher.Match(Sample));
        }

        IReadOnlyList<EntityRulerPattern>? rules = null;
        try
        {
            rules = PatternJson.ReadEntityPatterns(data);
        }
        catch (PatternFormatException)
        {
        }

        if (rules is not null)
        {
            var ruler = new EntityRuler();
            foreach (EntityRulerPattern rule in rules)
            {
                ruler.Add(rule);
            }

            int end = 0;
            foreach (RuleEntity e in ruler.FindEntities(Sample))
            {
                Require(e.Span.Start >= end && e.Span.End > e.Span.Start && e.Span.End <= Sample.Length, "entity range");
                end = e.Span.End;
            }

            using var stream = new MemoryStream();
            ruler.SavePatterns(stream);
            stream.Position = 0;
            Require(PatternJson.ReadEntityPatterns(stream).Count == rules.Count, "rule file round trip");
        }
    }

    private static void CheckMatches(IReadOnlyList<PatternMatch> matches)
    {
        (int End, int Start) previous = (0, 0);
        foreach (PatternMatch m in matches)
        {
            Require(m.Start >= 0 && m.End > m.Start && m.End <= Sample.Length, "match range");
            Require((m.End, m.Start).CompareTo(previous) >= 0, "match order");
            previous = (m.End, m.Start);
        }
    }

    private static void Require(bool condition, string what)
    {
        if (!condition)
        {
            throw new InvalidOperationException("Invariant failed: " + what);
        }
    }
}
