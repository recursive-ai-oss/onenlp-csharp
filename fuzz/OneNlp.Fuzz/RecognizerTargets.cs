using System.Reflection;
using System.Text;
using RecursiveAI.OneNlp.Recognizers;

namespace RecursiveAI.OneNlp.Fuzz;

/// <summary>
/// Feeds arbitrary text (UTF-8, or raw UTF-16 units when the first byte is odd) to the English recognizers and checks the results:
/// ordered, in range, without overlap, with the text they claim, a confidence from 0 to 1. The results also go through the pipeline
/// component, which must accept them. Nothing may throw.
/// </summary>
internal static class RecognizerTargets
{
    private static readonly Pipeline Pipe = Pipeline.Blank(Language.English).Add(new RecognizerComponent(Recognizer.English));

    internal static void Text(ReadOnlySpan<byte> data)
    {
        string text = (data.Length > 0 && (data[0] & 1) == 1) ? FromUnits(data[1..]) : Encoding.UTF8.GetString(data);
        Check(Recognizer.English, text);
        Doc doc = Pipe.Process(text);
        int previous = -1;
        foreach (Span e in doc.Ents)
        {
            Require(e.Start > previous && e.End > e.Start && e.End <= doc.Length, "entity range");
            previous = e.End - 1;
        }
    }

    /// <summary>Arbitrary bytes as rule data: only <see cref="RecognizerRulesException"/> may escape, and rules that load must run.</summary>
    internal static void Rules(ReadOnlySpan<byte> data)
    {
        Recognizer recognizer;
        try
        {
            recognizer = Recognizer.FromRules([data.ToArray()]);
        }
        catch (RecognizerRulesException)
        {
            return;
        }

        Check(recognizer, "twenty-one dollars, $5m, 3.5 km, 50%, 1st, a@b.com http://x.org 1,234.5 USD 20 two hundred and five");
    }

    internal static void SeedText(string dir)
    {
        File.WriteAllText(Path.Combine(dir, "seed-mixed"),
            "Pay $2.5 million or twenty-one euros by the 3rd; 12% of 3.5 km at 98.6\u00b0F, 10 GB, 5 years old. Mail Bob@Example.com, "
            + "see https://www.example.com/a_(b)?q=1#x or www.x.org, ping 10.0.0.1 or 2001:db8::1, call +1 (555) 123-4567, "
            + "#tag @user 123e4567-e89b-12d3-a456-426614174000 and one and a half, three quarters, a dozen, minus 5.");
    }

    internal static void SeedRules(string dir)
    {
        Assembly assembly = typeof(Recognizer).Assembly;
        foreach (string name in assembly.GetManifestResourceNames().Where(n => n.Replace('\\', '/').StartsWith("Recognizers/Data/en/", StringComparison.Ordinal)))
        {
            using Stream s = assembly.GetManifestResourceStream(name)!;
            using FileStream f = File.Create(Path.Combine(dir, "seed-" + Path.GetFileName(name.Replace('\\', '/'))));
            s.CopyTo(f);
        }
    }

    private static void Check(Recognizer recognizer, string text)
    {
        IReadOnlyList<Recognition> results = recognizer.Recognize(text);
        int previousEnd = 0;
        foreach (Recognition r in results)
        {
            Require(r.Start >= previousEnd && r.End > r.Start && r.End <= text.Length, "result range and order");
            Require(r.Text == text.Substring(r.Start, r.End - r.Start), "result text");
            Require(r.Confidence is >= 0f and <= 1f && r.Label.Length > 0, "confidence and label");
            previousEnd = r.End;
        }
    }

    private static string FromUnits(ReadOnlySpan<byte> data)
    {
        var chars = new char[data.Length / 2];
        for (int i = 0; i < chars.Length; i++)
        {
            chars[i] = (char)(data[2 * i] | (data[(2 * i) + 1] << 8));
        }

        return new string(chars);
    }

    private static void Require(bool condition, string what)
    {
        if (!condition)
        {
            throw new InvalidOperationException("Invariant failed: " + what);
        }
    }
}
