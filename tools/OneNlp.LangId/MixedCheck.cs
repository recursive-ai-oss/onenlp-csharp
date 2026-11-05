using System.Globalization;
using System.Text;
using RecursiveAI.OneNlp.LanguageId;

namespace RecursiveAI.OneNlp.LangIdTool;

/// <summary>
/// A synthetic check of <see cref="LanguageDetector.DetectSpans"/>: documents made of two or three held-out sentences in different
/// languages. Reports the share of characters assigned to the right language and the share of documents whose run count and
/// languages are exactly right, for several switch penalties.
/// </summary>
internal static class MixedCheck
{
    public static void Run(Dictionary<string, string> opt)
    {
        using FileStream fs = File.OpenRead(opt["model"]);
        LanguageDetector d = LanguageDetector.Load(fs);
        string[] lines = File.ReadAllLines(Path.Combine(opt["eval"], opt.GetValueOrDefault("set", "main"), "sentences.tsv"), Encoding.UTF8);
        (string lang, string text)[] sents = [.. lines.Select(l => l.Split('\t')).Select(p => (p[0], p[1]))];
        var rnd = new Random(99);
        int docs = int.Parse(opt.GetValueOrDefault("docs", "2000"), CultureInfo.InvariantCulture);
        var sb = new StringBuilder();
        foreach (double penalty in new[] { 2.0, 4.0, 6.0, 9.0 })
        {
            var r = new Random(99);
            long chars = 0, right = 0;
            int exact = 0;
            for (int n = 0; n < docs; n++)
            {
                int parts = r.Next(2, 4);
                var picked = new List<(string lang, string text)>();
                while (picked.Count < parts)
                {
                    (string lang, string text) s = sents[r.Next(sents.Length)];
                    if (s.text.Length > 25 && (picked.Count == 0 || picked[^1].lang != s.lang))
                    {
                        picked.Add(s);
                    }
                }

                var doc = new StringBuilder();
                var gold = new List<string>();
                foreach ((string lang, string text) in picked)
                {
                    if (doc.Length > 0)
                    {
                        doc.Append(' ');
                    }

                    for (int k = 0; k < text.Length; k++)
                    {
                        gold.Add(lang);
                    }

                    doc.Append(text);
                }

                // gold has one entry per character of the sentences; the separating blanks are not scored.
                IReadOnlyList<LanguageSpan> spans = d.DetectSpans(doc.ToString(), penalty);
                int pos = 0;
                string s2 = doc.ToString();
                var assigned = new string[s2.Length];
                foreach (LanguageSpan sp in spans)
                {
                    for (int k = sp.Start; k < sp.End; k++)
                    {
                        assigned[k] = sp.Language.Code;
                    }
                }

                foreach ((string lang, string text) in picked)
                {
                    for (int k = 0; k < text.Length; k++)
                    {
                        chars++;
                        if (assigned[pos + k] == lang)
                        {
                            right++;
                        }
                    }

                    pos += text.Length + 1;
                }

                if (spans.Count == picked.Count && spans.Select(x => x.Language.Code).SequenceEqual(picked.Select(x => x.lang)))
                {
                    exact++;
                }
            }

            sb.AppendLine(CultureInfo.InvariantCulture, $"penalty {penalty}: characters right {100.0 * right / chars:F2}%, documents with exactly the right runs {100.0 * exact / docs:F2}% ({docs} documents)");
        }

        Console.Write(sb);
        if (opt.TryGetValue("out", out string? o))
        {
            File.WriteAllText(o, sb.ToString());
        }

        _ = rnd;
    }
}
