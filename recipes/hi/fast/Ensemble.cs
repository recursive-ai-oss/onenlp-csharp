using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using RecursiveAI.OneNlp.Scoring;

namespace RecursiveAI.OneNlp.Recipes.Hindi;

// The ensemble half of the labelling. Reads the raw predictions of every source head (Label.cs) and writes the training files:
//
//   ensemble --in A.jsonl.gz[,B.jsonl.gz...] --out DIR [--use en,de,...] [--pos-agree 0.7] [--uas-agree 0.55] [--dev-mod 20]
//            [--max-sentences N]
//
//   DIR/hi-ud-train.conllu, hi-ud-dev.conllu   UPOS, UFeats, head and relation of the sentences the sources agree on
//   DIR/hi-ner-train.iob2, hi-ner-dev.iob2     PER/ORG/LOC of the sentences without a contested entity
//   DIR/stats.txt                              retention, and the pairwise agreement of the source heads
//
// The rules (Zhou and Li 2005, tri-training, for the use of agreement between independently trained models as the confidence; McDonald,
// Petrov and Hall 2011 for transferring from several source languages instead of one):
//  - UPOS: the plurality label of the sources; a sentence is kept when the mean share of the plurality over its tokens is at least --pos-agree.
//  - UFeats: a feature is written when more than half of the sources give the same value for it.
//  - Parse: the medoid of the sources' trees, the tree with the highest mean unlabelled attachment agreement with the others; a sentence is
//    kept when that mean is at least --uas-agree. A relation is the plurality among the sources that attach the token to the medoid's head.
//  - Entities: a span (type and boundaries) is kept when at least half of the sources produce it. A span that two or more sources produce
//    but fewer than half do is "contested", and a sentence with one is left out of the entity files (it would teach the model that
//    entities are not entities); a span only one source produces is ignored as noise.
// A document whose index is a multiple of --dev-mod goes to the development files: the development files measure how well the student
// reproduces the labeller on unseen documents, nothing more.
internal static partial class Program
{
    private sealed record Labelled(int Doc, string Text, int[] Starts, int[] Lengths, Dictionary<string, SourceLabels> Sources);

    private sealed record SourceLabels(string[] Pos, string[] Morph, int[] Head, string[] Dep, string[] Ner);

    private static IEnumerable<Labelled> ReadLabelled(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read);
        using var gz = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new StreamReader(gz, Encoding.UTF8);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            using JsonDocument doc = JsonDocument.Parse(line);
            JsonElement root = doc.RootElement;
            int[] tok = [.. root.GetProperty("tok").EnumerateArray().Select(e => e.GetInt32())];
            int n = tok.Length / 2;
            var starts = new int[n];
            var lengths = new int[n];
            for (int i = 0; i < n; i++)
            {
                starts[i] = tok[2 * i];
                lengths[i] = tok[(2 * i) + 1];
            }

            var sources = new Dictionary<string, SourceLabels>();
            foreach (JsonProperty p in root.GetProperty("src").EnumerateObject())
            {
                JsonElement s = p.Value;
                sources[p.Name] = new SourceLabels(
                    s.GetProperty("pos").GetString()!.Split(' '),
                    s.GetProperty("morph").GetString()!.Split(' '),
                    [.. s.GetProperty("head").GetString()!.Split(' ').Select(x => int.Parse(x, CultureInfo.InvariantCulture))],
                    s.GetProperty("dep").GetString()!.Split(' '),
                    s.GetProperty("ner").GetString()!.Split(' '));
            }

            yield return new Labelled(root.GetProperty("d").GetInt32(), root.GetProperty("text").GetString()!, starts, lengths, sources);
        }
    }

    private static Dictionary<string, string> Feats(string morph)
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        if (morph != "_")
        {
            foreach (string kv in morph.Split('|'))
            {
                int eq = kv.IndexOf('=');
                if (eq > 0)
                {
                    d[kv[..eq]] = kv[(eq + 1)..];
                }
            }
        }

        return d;
    }

    // One CoNLL-U sentence: forms from the text, UPOS and UFeats as given, no lemma or XPOS. A head equal to the token's own index is the root.
    private static void WriteConllu(StreamWriter w, string id, Labelled s, string[] pos, string[] feats, int[] heads, string[] deps)
    {
        int n = s.Starts.Length;
        w.WriteLine("# sent_id = " + id);
        w.WriteLine("# text = " + s.Text);
        for (int t = 0; t < n; t++)
        {
            string misc = t + 1 < n && s.Starts[t] + s.Lengths[t] == s.Starts[t + 1] ? "SpaceAfter=No" : "_";
            string form = s.Text.Substring(s.Starts[t], s.Lengths[t]);
            string head = heads[t] == t ? "0" : (heads[t] + 1).ToString(CultureInfo.InvariantCulture);
            string dep = heads[t] == t ? "root" : deps[t];
            w.WriteLine(string.Join('\t', (t + 1).ToString(CultureInfo.InvariantCulture), form, "_", pos[t], "_", feats[t], head, dep, "_", misc));
        }

        w.WriteLine();
    }

    private static double Uas(int[] a, int[] b)
    {
        int same = 0;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] == b[i])
            {
                same++;
            }
        }

        return (double)same / a.Length;
    }

    private static int Ensemble(string[] args)
    {
        string outDir = Required(args, "out");
        Directory.CreateDirectory(outDir);
        string[] use = Opt(args, "use", string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries);
        double posAgree = double.Parse(Opt(args, "pos-agree", "0.7"), CultureInfo.InvariantCulture);
        double uasAgree = double.Parse(Opt(args, "uas-agree", "0.55"), CultureInfo.InvariantCulture);
        int devMod = Num(args, "dev-mod", 20);
        int maxSentences = Num(args, "max-sentences", int.MaxValue);

        using var trainUd = new StreamWriter(Path.Combine(outDir, "hi-ud-train.conllu"), false, new UTF8Encoding(false)) { NewLine = "\n" };
        using var devUd = new StreamWriter(Path.Combine(outDir, "hi-ud-dev.conllu"), false, new UTF8Encoding(false)) { NewLine = "\n" };
        using var trainNer = new StreamWriter(Path.Combine(outDir, "hi-ner-train.iob2"), false, new UTF8Encoding(false)) { NewLine = "\n" };
        using var devNer = new StreamWriter(Path.Combine(outDir, "hi-ner-dev.iob2"), false, new UTF8Encoding(false)) { NewLine = "\n" };

        bool perSource = args.Contains("--per-source");
        using StreamWriter? nerAll = args.Contains("--ner-all") ? new StreamWriter(Path.Combine(outDir, "ner-all.iob2"), false, new UTF8Encoding(false)) { NewLine = "\n" } : null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int duplicates = 0;
        var sourceWriters = new Dictionary<string, StreamWriter>();
        int total = 0, keptUd = 0, keptNer = 0, devUdCount = 0, trainUdCount = 0, devNerCount = 0, trainNerCount = 0;
        long tokens = 0;
        double posShareSum = 0, uasSum = 0;
        int keptTokens = 0;
        string[]? names = null;
        double[,] posPair = new double[0, 0];
        double[,] uasPair = new double[0, 0];
        double[,] nerPair = new double[0, 0];
        long pairTokens = 0;
        long[,] nerBoth = new long[0, 0];
        long[] nerCount = [];
        int[] entityKept = new int[3];
        string[] types = ["PER", "ORG", "LOC"];
        int contestedSentences = 0;

        // Adult-content filter (AdultFilter.cs): the documents of the text file that the filter flags, and the sentences with a weaker term.
        var adultDocs = new HashSet<int>();
        bool adultFilter = Opt(args, "text", string.Empty) is { Length: > 0 };
        int adultRemoved = 0;
        int adultDocsFlagged = 0;
        int docsRead = 0;
        if (adultFilter)
        {
            int di = -1;
            foreach (string line in File.ReadLines(Opt(args, "text", string.Empty)))
            {
                di++;
                using JsonDocument rec = JsonDocument.Parse(line);
                if (rec.RootElement.GetProperty("s").GetString() == Opt(args, "source", "fw"))
                {
                    docsRead++;
                    if (AdultFilter.Document(rec.RootElement.GetProperty("t").GetString()!))
                    {
                        adultDocs.Add(di);
                        adultDocsFlagged++;
                    }
                }
            }
        }

        string[] nerUse = Opt(args, "ner-use", string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries);

        foreach (string path in Required(args, "in").Split(','))
        {
            foreach (Labelled s in ReadLabelled(path))
            {
                if (total >= maxSentences)
                {
                    break;
                }

                if (adultFilter && (adultDocs.Contains(s.Doc) || AdultFilter.Sentence(s.Text)))
                {
                    adultRemoved++;
                    continue;
                }

                // Chunks are labelled by separate processes, so the same sentence can come from two of them. (Scoring runs keep every sentence.)
                if (!perSource && !seen.Add(s.Text))
                {
                    duplicates++;
                    continue;
                }

                total++;
                string[] langs = use.Length > 0 ? use : [.. s.Sources.Keys.Order(StringComparer.Ordinal)];
                if (names is null)
                {
                    names = langs;
                    posPair = new double[names.Length, names.Length];
                    uasPair = new double[names.Length, names.Length];
                    nerPair = new double[names.Length, names.Length];
                    nerBoth = new long[names.Length, names.Length];
                    nerCount = new long[names.Length];
                }

                SourceLabels[] src = [.. langs.Select(l => s.Sources[l])];
                if (perSource)
                {
                    // Every source head's own analysis, unfiltered, to be scored like a system.
                    for (int a = 0; a < langs.Length; a++)
                    {
                        if (!sourceWriters.TryGetValue(langs[a], out StreamWriter? sw))
                        {
                            sw = new StreamWriter(Path.Combine(outDir, "src-" + langs[a] + ".conllu"), false, new UTF8Encoding(false)) { NewLine = "\n" };
                            sourceWriters[langs[a]] = sw;
                        }

                        WriteConllu(sw, "hi-" + total.ToString(CultureInfo.InvariantCulture), s, src[a].Pos, [.. src[a].Morph], src[a].Head, src[a].Dep);
                    }
                }

                int n = s.Starts.Length;
                int m = src.Length;
                tokens += n;

                // Pairwise agreement of the heads, for choosing the sources and for the report.
                for (int a = 0; a < m; a++)
                {
                    for (int b = 0; b < m; b++)
                    {
                        for (int t = 0; t < n; t++)
                        {
                            posPair[a, b] += src[a].Pos[t] == src[b].Pos[t] ? 1 : 0;
                        }

                        uasPair[a, b] += Uas(src[a].Head, src[b].Head) * n;
                    }
                }

                pairTokens += n;

                // UPOS by plurality.
                var pos = new string[n];
                double share = 0;
                for (int t = 0; t < n; t++)
                {
                    var counts = new Dictionary<string, int>(StringComparer.Ordinal);
                    foreach (SourceLabels x in src)
                    {
                        counts[x.Pos[t]] = counts.GetValueOrDefault(x.Pos[t]) + 1;
                    }

                    KeyValuePair<string, int> best = counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).First();
                    pos[t] = best.Key;
                    share += (double)best.Value / m;
                }

                share /= n;

                // UFeats: a feature kept when more than half of the sources agree on its value.
                var feats = new string[n];
                for (int t = 0; t < n; t++)
                {
                    var votes = new Dictionary<(string, string), int>();
                    foreach (SourceLabels x in src)
                    {
                        foreach (KeyValuePair<string, string> kv in Feats(x.Morph[t]))
                        {
                            votes[(kv.Key, kv.Value)] = votes.GetValueOrDefault((kv.Key, kv.Value)) + 1;
                        }
                    }

                    string[] kept = [.. votes.Where(v => v.Value * 2 > m).Select(v => v.Key).OrderBy(k => k.Item1, StringComparer.Ordinal).Select(k => k.Item1 + "=" + k.Item2)];
                    feats[t] = kept.Length == 0 ? "_" : string.Join('|', kept);
                }

                // The medoid tree.
                int medoid = 0;
                double bestSum = -1;
                for (int a = 0; a < m; a++)
                {
                    double sum = 0;
                    for (int b = 0; b < m; b++)
                    {
                        if (a != b)
                        {
                            sum += Uas(src[a].Head, src[b].Head);
                        }
                    }

                    if (sum > bestSum)
                    {
                        bestSum = sum;
                        medoid = a;
                    }
                }

                double meanUas = bestSum / (m - 1);
                int[] heads = src[medoid].Head;
                var deps = new string[n];
                for (int t = 0; t < n; t++)
                {
                    var counts = new Dictionary<string, int>(StringComparer.Ordinal);
                    foreach (SourceLabels x in src)
                    {
                        if (x.Head[t] == heads[t])
                        {
                            counts[x.Dep[t]] = counts.GetValueOrDefault(x.Dep[t]) + 1;
                        }
                    }

                    deps[t] = counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key == src[medoid].Dep[t] ? 0 : 1).ThenBy(kv => kv.Key, StringComparer.Ordinal).First().Key;
                }

                bool oneRoot = Enumerable.Range(0, n).Count(t => heads[t] == t) == 1;
                bool dev = devMod > 0 && s.Doc % devMod == 0;
                if (perSource)
                {
                    if (!sourceWriters.TryGetValue("ensemble", out StreamWriter? ew))
                    {
                        ew = new StreamWriter(Path.Combine(outDir, "src-ensemble.conllu"), false, new UTF8Encoding(false)) { NewLine = "\n" };
                        sourceWriters["ensemble"] = ew;
                    }

                    WriteConllu(ew, "hi-" + total.ToString(CultureInfo.InvariantCulture), s, pos, feats, heads, deps);
                }

                if (share >= posAgree && meanUas >= uasAgree && oneRoot)
                {
                    keptUd++;
                    keptTokens += n;
                    posShareSum += share * n;
                    uasSum += meanUas * n;
                    StreamWriter w = dev ? devUd : trainUd;
                    if (dev)
                    {
                        devUdCount++;
                    }
                    else
                    {
                        trainUdCount++;
                    }

                    WriteConllu(w, "hi-" + keptUd.ToString(CultureInfo.InvariantCulture), s, pos, feats, heads, deps);
                }

                // Entities.
                // --ner-use: the sources that vote on entities (the related-language treebanks have no entity data, so they cannot).
                SourceLabels[] voters = nerUse.Length == 0 ? src : [.. nerUse.Select(l => s.Sources[l])];
                int mn = voters.Length;
                var spans = new Dictionary<(int, int, string), int>();
                var perSourceSpans = new HashSet<(int, int, string)>[mn];
                for (int a = 0; a < mn; a++)
                {
                    perSourceSpans[a] = [];
                    foreach (LabeledSpan e in SpanScorer.ExtractIob2(voters[a].Ner))
                    {
                        if (e.Label is "PER" or "ORG" or "LOC")
                        {
                            var key = (e.Start, e.End, e.Label);
                            spans[key] = spans.GetValueOrDefault(key) + 1;
                            perSourceSpans[a].Add(key);
                        }
                    }

                    if (nerUse.Length == 0)
                    {
                        nerCount[a] += perSourceSpans[a].Count;
                    }
                }

                for (int a = 0; a < mn && nerUse.Length == 0; a++)
                {
                    for (int b = 0; b < mn; b++)
                    {
                        nerBoth[a, b] += perSourceSpans[a].Count(x => perSourceSpans[b].Contains(x));
                    }
                }

                bool contested = spans.Any(kv => kv.Value >= 2 && kv.Value * 2 < mn);
                if (contested)
                {
                    contestedSentences++;
                }

                var accepted = spans.Where(kv => kv.Value * 2 >= mn).Select(kv => kv.Key).OrderBy(k => k.Item1).ToList();
                // Overlapping accepted spans cannot both be written; the longer wins, the sentence stays only if none overlap.
                bool overlapping = false;
                for (int i = 1; i < accepted.Count; i++)
                {
                    if (accepted[i].Item1 < accepted[i - 1].Item2)
                    {
                        overlapping = true;
                    }
                }

                if (nerAll is not null)
                {
                    // Every sentence, contested or not: the labeller's own entity output, for scoring it against hand-labelled sentences.
                    var allTags = Enumerable.Repeat("O", n).ToArray();
                    int last = 0;
                    foreach ((int st, int en, string label) in accepted)
                    {
                        if (st < last)
                        {
                            continue;
                        }

                        allTags[st] = "B-" + label;
                        for (int t = st + 1; t < en; t++)
                        {
                            allTags[t] = "I-" + label;
                        }

                        last = en;
                    }

                    nerAll.WriteLine("# text = " + s.Text);
                    for (int t = 0; t < n; t++)
                    {
                        nerAll.WriteLine(string.Join('\t', (t + 1).ToString(CultureInfo.InvariantCulture), s.Text.Substring(s.Starts[t], s.Lengths[t]), allTags[t]));
                    }

                    nerAll.WriteLine();
                }

                if (!contested && !overlapping)
                {
                    keptNer++;
                    StreamWriter w = dev ? devNer : trainNer;
                    if (dev)
                    {
                        devNerCount++;
                    }
                    else
                    {
                        trainNerCount++;
                    }

                    var tags = Enumerable.Repeat("O", n).ToArray();
                    foreach ((int st, int en, string label) in accepted)
                    {
                        tags[st] = "B-" + label;
                        for (int t = st + 1; t < en; t++)
                        {
                            tags[t] = "I-" + label;
                        }

                        entityKept[Array.IndexOf(types, label)]++;
                    }

                    w.WriteLine("# text = " + s.Text);
                    for (int t = 0; t < n; t++)
                    {
                        w.WriteLine(string.Join('\t', (t + 1).ToString(CultureInfo.InvariantCulture), s.Text.Substring(s.Starts[t], s.Lengths[t]), tags[t]));
                    }

                    w.WriteLine();
                }
            }
        }

        foreach (StreamWriter sw in sourceWriters.Values)
        {
            sw.Dispose();
        }

        var sb = new StringBuilder();
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"sentences={total} duplicates_removed={duplicates} tokens={tokens} sources={string.Join(',', names ?? [])}"));
        if (adultFilter)
        {
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"adult filter: documents flagged={adultDocsFlagged} of {docsRead} ({100.0 * adultDocsFlagged / Math.Max(1, docsRead):F2}%); labelled sentences removed={adultRemoved} of {total + adultRemoved} ({100.0 * adultRemoved / Math.Max(1, total + adultRemoved):F2}%)"));
        }

        sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"ud kept={keptUd} ({100.0 * keptUd / Math.Max(1, total):F1}%) train={trainUdCount} dev={devUdCount} tokens={keptTokens} mean_pos_share={posShareSum / Math.Max(1, keptTokens):F3} mean_medoid_uas={uasSum / Math.Max(1, keptTokens):F3}"));
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"ner kept={keptNer} ({100.0 * keptNer / Math.Max(1, total):F1}%) train={trainNerCount} dev={devNerCount} contested_sentences={contestedSentences} entities PER={entityKept[0]} ORG={entityKept[1]} LOC={entityKept[2]}"));
        if (names is not null)
        {
            sb.AppendLine("pairwise UPOS agreement (row/col = " + string.Join(' ', names) + ")");
            for (int a = 0; a < names.Length; a++)
            {
                sb.AppendLine(names[a] + " " + string.Join(' ', Enumerable.Range(0, names.Length).Select(b => (posPair[a, b] / pairTokens).ToString("F3", CultureInfo.InvariantCulture))));
            }

            sb.AppendLine("pairwise UAS agreement");
            for (int a = 0; a < names.Length; a++)
            {
                sb.AppendLine(names[a] + " " + string.Join(' ', Enumerable.Range(0, names.Length).Select(b => (uasPair[a, b] / pairTokens).ToString("F3", CultureInfo.InvariantCulture))));
            }

            sb.AppendLine("entity spans per source and pairwise F1 of spans (PER/ORG/LOC)");
            for (int a = 0; a < names.Length; a++)
            {
                sb.AppendLine(names[a] + " n=" + nerCount[a].ToString(CultureInfo.InvariantCulture) + " " + string.Join(' ', Enumerable.Range(0, names.Length).Select(b => (2.0 * nerBoth[a, b] / Math.Max(1, nerCount[a] + nerCount[b])).ToString("F3", CultureInfo.InvariantCulture))));
            }
        }

        File.WriteAllText(Path.Combine(outDir, "stats.txt"), sb.ToString());
        Console.Write(sb.ToString());
        return 0;
    }
}
