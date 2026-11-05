// Runs Catalyst's English pipeline as a black box over the raw text of a CoNLL-U / UNER test file.
//
//   CatalystOracle ud  <gold.conllu> <out.conllu>   tokens, sentences, UPOS and lemmas as CoNLL-U (other columns '_')
//   CatalystOracle ner <gold.iob2>   <out.tsv>      entity-bearing tokens per sentence: sentence, begin, end, Catalyst type, tag
//   CatalystOracle speed <gold.conllu> <runs>       single-thread throughput on the raw document texts: default pipeline, then with the entity model
//
// The calls follow the package's published README usage (register the English models, build a pipeline, process a document).
using System.Globalization;
using System.Text;
using Catalyst;
using Catalyst.Models;
using Mosaik.Core;

namespace OneNlp.Tools.CatalystOracle;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length != 3 || (args[0] != "ud" && args[0] != "ner" && args[0] != "speed"))
        {
            Console.Error.WriteLine("usage: CatalystOracle ud|ner <gold file> <output file>  |  speed <gold file> <runs>");
            return 2;
        }

        Catalyst.Models.English.Register();
        Storage.Current = new DiskStorage(Path.Combine(Path.GetTempPath(), "catalyst-models"));
        Pipeline nlp = await Pipeline.ForAsync(Language.English);

        if (args[0] == "speed")
        {
            return await RunSpeed(nlp, args[1], int.Parse(args[2], CultureInfo.InvariantCulture));
        }

        if (args[0] == "ud")
        {
            return RunUd(nlp, args[1], args[2]);
        }

        // The README adds the entity model separately; it is not part of the default English pipeline.
        nlp.Add(await AveragePerceptronEntityRecognizer.FromStoreAsync(language: Language.English, version: Mosaik.Core.Version.Latest, tag: "WikiNER"));
        return RunNer(nlp, args[1], args[2]);
    }

    /// <summary>Documents of a CoNLL-U file as raw text, rebuilt from the surface forms and SpaceAfter, so the characters match the gold file.</summary>
    private static List<string> ReadDocuments(string path)
    {
        var docs = new List<string>();
        var sb = new StringBuilder();
        int skipTo = 0;
        foreach (string line in File.ReadLines(path))
        {
            if (line.StartsWith("# newdoc", StringComparison.Ordinal) && sb.Length > 0)
            {
                docs.Add(sb.ToString().TrimEnd());
                sb.Clear();
            }

            if (line.Length == 0)
            {
                if (sb.Length > 0 && sb[^1] != ' ')
                {
                    sb.Append(' ');
                }

                skipTo = 0;
                continue;
            }

            if (line[0] == '#')
            {
                continue;
            }

            string[] c = line.Split('\t');
            if (c[0].Contains('.', StringComparison.Ordinal))
            {
                continue;
            }

            if (c[0].Contains('-', StringComparison.Ordinal))
            {
                skipTo = int.Parse(c[0].Split('-')[1], CultureInfo.InvariantCulture);
            }
            else if (int.Parse(c[0], CultureInfo.InvariantCulture) <= skipTo)
            {
                continue;
            }

            sb.Append(c[1]);
            if (!c[9].Contains("SpaceAfter=No", StringComparison.Ordinal))
            {
                sb.Append(' ');
            }
        }

        if (sb.Length > 0)
        {
            docs.Add(sb.ToString().TrimEnd());
        }

        return docs;
    }

    /// <summary>Timed passes over the raw document texts on one thread; the first pass is a warm-up and is not counted.</summary>
    private static async Task<int> RunSpeed(Pipeline nlp, string gold, int runs)
    {
        List<string> texts = ReadDocuments(gold);
        long chars = texts.Sum(t => (long)t.Length);
        Console.WriteLine($"{texts.Count} documents, {chars} characters; Catalyst 26.10.6514, English models 26.9.5316");
        Measure("tagging pipeline (sentences, tokens, POS, lemmas)", nlp, texts, chars, runs);
        nlp.Add(await AveragePerceptronEntityRecognizer.FromStoreAsync(language: Language.English, version: Mosaik.Core.Version.Latest, tag: "WikiNER"));
        Measure("tagging pipeline plus WikiNER entities", nlp, texts, chars, runs);
        return 0;
    }

    private static void Measure(string name, Pipeline nlp, List<string> texts, long chars, int runs)
    {
        long tokens = Pass(nlp, texts);
        for (int r = 1; r <= runs; r++)
        {
            long gcBefore = GC.GetAllocatedBytesForCurrentThread();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            tokens = Pass(nlp, texts);
            sw.Stop();
            double s = sw.Elapsed.TotalSeconds;
            Console.WriteLine($"{name}: run {r}: {tokens / s:F0} tokens/s, {chars / s / 1e6:F2} M chars/s, {(GC.GetAllocatedBytesForCurrentThread() - gcBefore) / (double)tokens:F0} B/token ({tokens} tokens, {s:F2} s)");
        }
    }

    private static long Pass(Pipeline nlp, List<string> texts)
    {
        long tokens = 0;
        foreach (string text in texts)
        {
            var doc = new Document(text, Language.English);
            nlp.ProcessSingle(doc);
            foreach (var span in doc.Spans)
            {
                tokens += span.Tokens.Count();
            }
        }

        return tokens;
    }

    private static int RunUd(Pipeline nlp, string gold, string output)
    {
        int sentences = 0, tokens = 0;
        using var w = new StreamWriter(output, false, new UTF8Encoding(false)) { NewLine = "\n" };
        foreach (string text in ReadDocuments(gold))
        {
            var doc = new Document(text, Language.English);
            nlp.ProcessSingle(doc);
            foreach (var span in doc.Spans)
            {
                int id = 0;
                var rows = new List<string>();
                foreach (var tok in span.Tokens)
                {
                    // The slice of the input, not tok.Value: the scorer compares characters, and a token value can be normalized ("'ve" as "have").
                    string form = tok.End >= tok.Begin && tok.End < text.Length ? text.Substring(tok.Begin, tok.End - tok.Begin + 1) : tok.Value;
                    if (string.IsNullOrWhiteSpace(form))
                    {
                        continue;
                    }

                    id++;
                    string spaceAfter = tok.End + 1 < text.Length && text[tok.End + 1] == ' ' ? "_" : "SpaceAfter=No";
                    string lemma = string.IsNullOrEmpty(tok.Lemma) ? "_" : tok.Lemma;
                    rows.Add(string.Join('\t', id.ToString(CultureInfo.InvariantCulture), form, lemma, tok.POS.ToString(), "_", "_", id == 1 ? "0" : "1", id == 1 ? "root" : "dep", "_", spaceAfter));
                }

                if (rows.Count == 0)
                {
                    continue;
                }

                // Catalyst has no dependency parser. The scorer needs a well-formed tree, so every word hangs off the first one;
                // UAS and LAS of this file mean nothing and are not reported.
                foreach (string r in rows)
                {
                    w.Write(r);
                    w.Write('\n');
                }

                w.Write('\n');
                sentences++;
                tokens += id;
            }
        }

        Console.WriteLine($"{output}: {sentences} sentences, {tokens} tokens");
        return 0;
    }

    private static int RunNer(Pipeline nlp, string gold, string output)
    {
        // One document per gold sentence, from its '# text =' line.
        var texts = new List<string>();
        foreach (string line in File.ReadLines(gold))
        {
            if (line.StartsWith("# text = ", StringComparison.Ordinal))
            {
                texts.Add(line["# text = ".Length..]);
            }
        }

        using var w = new StreamWriter(output, false, new UTF8Encoding(false)) { NewLine = "\n" };
        int spans = 0;
        for (int i = 0; i < texts.Count; i++)
        {
            var doc = new Document(texts[i], Language.English);
            nlp.ProcessSingle(doc);
            foreach (var span in doc.Spans)
            {
                foreach (var tok in span.Tokens)
                {
                    // Entities are marked on tokens; each row is one token with one of its entity types. Type names are mapped when scoring.
                    foreach (var et in tok.EntityTypes)
                    {
                        w.Write($"{i}\t{tok.Begin}\t{tok.End + 1}\t{et.Type}\t{et.Tag}\n");
                        spans++;
                    }
                }
            }
        }

        Console.WriteLine($"{output}: {texts.Count} sentences, {spans} entity-token rows");
        return 0;
    }
}
