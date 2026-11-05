using System.Globalization;
using System.Text;
using RecursiveAI.OneNlp.Scoring;

namespace RecursiveAI.OneNlp.LangIdTool;

/// <summary>
/// Scores language-ID predictions per bucket. The unit of resampling is one test text; the macro statistic is the mean of the
/// per-language accuracies, so every language weighs the same (as in the Lingua accuracy tables).
/// </summary>
internal static class Scorer
{
    private readonly record struct Item(byte Language, bool Correct);

    public static void Run(Dictionary<string, string> opt)
    {
        string evalDir = Path.Combine(opt["eval"], opt["set"]);
        string[] systems = opt["systems"].Split(',');
        string[] refs = opt["ref"].Split(',');
        bool perLanguage = opt.ContainsKey("per-language");
        var sb = new StringBuilder();
        var csv = new StringBuilder("set,bucket,language,n,system,accuracy,reference,paired_diff,low,high,loses\n");
        foreach (string bucket in new[] { "words", "pairs", "sentences" })
        {
            string[] gold = [.. File.ReadAllLines(Path.Combine(evalDir, bucket + ".tsv"), Encoding.UTF8).Select(l => l[..l.IndexOf('\t', StringComparison.Ordinal)])];
            string[] langs = [.. gold.Distinct().Order(StringComparer.Ordinal)];
            var index = langs.Select((l, i) => (l, i)).ToDictionary(x => x.l, x => (byte)x.i);
            var items = new Dictionary<string, Item[]>();
            foreach (string s in systems)
            {
                string[] pred = File.ReadAllLines(Path.Combine(opt["pred"], $"{s}.{opt["set"]}.{bucket}.txt"), Encoding.UTF8);
                if (pred.Length != gold.Length)
                {
                    throw new InvalidDataException($"{s} {bucket}: {pred.Length} predictions for {gold.Length} texts");
                }

                items[s] = [.. gold.Select((g, i) => new Item(index[g], pred[i] == g))];
            }

            sb.AppendLine(CultureInfo.InvariantCulture, $"### {opt["set"]} / {bucket} ({gold.Length} texts, {langs.Length} languages)");
            sb.AppendLine();
            sb.AppendLine("| system | macro accuracy % [95% CI] | micro % | " + string.Join(" | ", refs.Select(r => $"Δ vs {r} [95% CI]")) + " |");
            sb.AppendLine("|---|---|---|" + string.Concat(refs.Select(_ => "---|")));
            Statistic<Item> macro = u => Macro(u, langs.Length);
            foreach (string s in systems)
            {
                ConfidenceInterval ci = Bootstrap.Interval(items[s], macro);
                double micro = 100.0 * items[s].Count(i => i.Correct) / items[s].Length;
                sb.Append(CultureInfo.InvariantCulture, $"| {s} | {ci.Estimate:F2} [{ci.Low:F2}, {ci.High:F2}] | {micro:F2} |");
                foreach (string r in refs)
                {
                    if (r == s)
                    {
                        sb.Append(" — |");
                        continue;
                    }

                    PairedResult p = Bootstrap.Paired(items[s], items[r], macro);
                    string verdict = p.Difference.Low > 0 ? "better" : p.Difference.High < 0 ? "WORSE" : "within noise";
                    sb.Append(CultureInfo.InvariantCulture, $" {p.Difference.Estimate:+0.00;-0.00} [{p.Difference.Low:+0.00;-0.00}, {p.Difference.High:+0.00;-0.00}] {verdict} |");
                }

                sb.AppendLine();
            }

            sb.AppendLine();
            if (opt.ContainsKey("confusions"))
            {
                // Where the first system's errors go, for languages below 90%.
                string first = systems[0];
                string[] pred = File.ReadAllLines(Path.Combine(opt["pred"], $"{first}.{opt["set"]}.{bucket}.txt"), Encoding.UTF8);
                foreach (string lang in langs)
                {
                    int[] pos = [.. Enumerable.Range(0, gold.Length).Where(i => gold[i] == lang)];
                    double acc = 100.0 * pos.Count(i => pred[i] == lang) / pos.Length;
                    if (acc < 90)
                    {
                        string top = string.Join(", ", pos.Where(i => pred[i] != lang).GroupBy(i => pred[i]).OrderByDescending(g => g.Count()).Take(4).Select(g => $"{(g.Key.Length == 0 ? "?" : g.Key)} {100.0 * g.Count() / pos.Length:F1}%"));
                        sb.AppendLine(CultureInfo.InvariantCulture, $"confusions {first} {bucket} {lang} ({acc:F1}%): {top}");
                    }
                }

                sb.AppendLine();
            }

            if (!perLanguage)
            {
                continue;
            }

            for (int l = 0; l < langs.Length; l++)
            {
                int[] pos = [.. Enumerable.Range(0, gold.Length).Where(i => gold[i] == langs[l])];
                foreach (string s in systems)
                {
                    Item[] mine = [.. pos.Select(i => items[s][i])];
                    double acc = 100.0 * mine.Count(i => i.Correct) / mine.Length;
                    foreach (string r in refs)
                    {
                        if (r == s)
                        {
                            continue;
                        }

                        Item[] theirs = [.. pos.Select(i => items[r][i])];
                        PairedResult p = Bootstrap.Paired(mine, theirs, Accuracy, 400);
                        csv.AppendLine(CultureInfo.InvariantCulture,
                            $"{opt["set"]},{bucket},{langs[l]},{mine.Length},{s},{acc:F2},{r},{p.Difference.Estimate:F2},{p.Difference.Low:F2},{p.Difference.High:F2},{(p.Difference.High < 0 ? "yes" : "no")}");
                    }
                }
            }
        }

        Console.Write(sb);
        if (perLanguage && opt.TryGetValue("csv", out string? path))
        {
            File.WriteAllText(path, csv.ToString());
        }
    }

    private static double Accuracy(ReadOnlySpan<Item> units)
    {
        int ok = 0;
        foreach (Item i in units)
        {
            if (i.Correct)
            {
                ok++;
            }
        }

        return 100.0 * ok / units.Length;
    }

    private static double Macro(ReadOnlySpan<Item> units, int languages)
    {
        var ok = new int[languages];
        var n = new int[languages];
        foreach (Item i in units)
        {
            n[i.Language]++;
            if (i.Correct)
            {
                ok[i.Language]++;
            }
        }

        double sum = 0;
        int present = 0;
        for (int l = 0; l < languages; l++)
        {
            if (n[l] > 0)
            {
                sum += (double)ok[l] / n[l];
                present++;
            }
        }

        return 100.0 * sum / present;
    }
}
