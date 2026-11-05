using System.Globalization;
using System.Text;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Segmentation;

namespace RecursiveAI.OneNlp.Recipes.Portuguese;

// Syntactic words and tokens in the Portuguese recipe.
//
// UD Portuguese-Bosque writes some surface tokens as several words. A contraction of a preposition with an article ("do" = "de" + "o") is
// two pieces for the tokenizer and for the heads, as in the German recipe. Two kinds of multiword token are different:
//   - "à" is "a" + "a" but has one character, so a token (a slice of the text) cannot hold two words: the tokenizer keeps it whole;
//   - an enclitic verb form ("trata-se" = "trata" + "se") is two tokens, "trata" and "-se" (the pronoun with its hyphen). The CoNLL-U reader
//     would cut the surface form into pieces that are not words ("trat", "a-se"), so `prepare` writes the two tokens itself.
// `prepare` rewrites the treebank accordingly (training sees exactly the tokens the tokenizer makes); `predict` writes the words of the
// treebank back: for "à" the two words come from the most frequent analysis in the training file (`--lexicon`), and for "-se" the word is the
// pronoun without its hyphen (as the training file spells it: "-lo" is the word "lo" or "o").
internal static partial class Program
{
    private static Expansions Lex = new();

    private sealed record WordRow(string Form, string Lemma, string Pos, string Tag, string Morph, int Head, string Deprel);

    // The text the gold sentence was written from: the "# text" comment when there is one (Doc.Text spells "à" as "a a").
    private static string RawText(ConllUSentence sentence) => sentence.GetComment("text") ?? sentence.Doc.Text;

    // The pronouns the tokenizer splits off a verb after a hyphen (SegmentationData.Portuguese, Enclitics); keep the two lists equal.
    private static readonly HashSet<string> Enclitics = new(
        ["se", "me", "te", "lhe", "lhes", "nos", "vos", "o", "a", "os", "as", "lo", "la", "los", "las"], StringComparer.OrdinalIgnoreCase);

    private enum Multiword { Pieces, Whole, Enclitic }

    // What the tokenizer makes of a multiword token of the treebank: two or more pieces cut from the surface form (contractions), one token
    // (fewer characters than words, or a hyphenated form that is not verb + one pronoun), or the verb and the pronoun with its hyphen.
    private static Multiword Classify(string surface, int words)
    {
        if (words < 2)
        {
            return Multiword.Pieces;
        }

        if (surface.Length < words)
        {
            return Multiword.Whole;
        }

        if (surface.Length > 2 && surface.Contains('-') && surface[0] != '-' && surface[^1] != '-')
        {
            int h = surface.IndexOf('-');
            bool split = words == 2 && h == surface.LastIndexOf('-') && char.IsLetter(surface[h - 1]) && Enclitics.Contains(surface[(h + 1)..]);
            return split ? Multiword.Enclitic : Multiword.Whole;
        }

        return Multiword.Pieces;
    }

    private static Expansions LoadLexicon(string[] args) =>
        Opt(args, "lexicon", string.Empty) is { Length: > 0 } path ? Expansions.Build(path) : new Expansions();

    /// <summary>What the training file says about the tokens that are not words: the words behind "à", and how the word of an enclitic is spelled.</summary>
    private sealed class Expansions
    {
        private readonly Dictionary<string, string[][]> _whole = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _enclitic = new(StringComparer.Ordinal);

        public static Expansions Build(string path)
        {
            var result = new Expansions();
            var whole = new Dictionary<string, Dictionary<string, int>>();
            var enclitic = new Dictionary<string, Dictionary<string, int>>();
            var rows = new Dictionary<int, string[]>();
            var ranges = new List<(int First, int Last, string Form)>();

            void Flush()
            {
                foreach ((int first, int last, string form) in ranges)
                {
                    int n = last - first + 1;
                    if (!rows.ContainsKey(first) || !rows.ContainsKey(last))
                    {
                        continue;
                    }

                    switch (Classify(form, n))
                    {
                        case Multiword.Whole when form.Length < n:
                            Count(whole, form.ToLowerInvariant(), string.Join('\u0001', Enumerable.Range(first, n).Select(id => Describe(rows[id]))));
                            break;
                        case Multiword.Enclitic:
                            Count(enclitic, form[(form.IndexOf('-') + 1)..].ToLowerInvariant(), rows[last][1].ToLowerInvariant());
                            break;
                    }
                }

                rows.Clear();
                ranges.Clear();
            }

            foreach (string line in File.ReadLines(path))
            {
                if (line.Length == 0)
                {
                    Flush();
                    continue;
                }

                if (line[0] == '#')
                {
                    continue;
                }

                string[] cols = line.Split('\t');
                if (cols.Length < 10)
                {
                    continue;
                }

                if (cols[0].Contains('-'))
                {
                    string[] ends = cols[0].Split('-');
                    if (ends.Length == 2 && int.TryParse(ends[0], CultureInfo.InvariantCulture, out int a) && int.TryParse(ends[1], CultureInfo.InvariantCulture, out int b) && b > a)
                    {
                        ranges.Add((a, b, cols[1]));
                    }
                }
                else if (int.TryParse(cols[0], CultureInfo.InvariantCulture, out int id))
                {
                    rows[id] = cols;
                }
            }

            Flush();
            foreach ((string key, Dictionary<string, int> counts) in whole)
            {
                result._whole[key] = Best(counts).Split('\u0001').Select(d => d.Split('\u0002')).ToArray();
            }

            foreach ((string key, Dictionary<string, int> counts) in enclitic)
            {
                result._enclitic[key] = Best(counts);
            }

            return result;
        }

        // form, lemma, upos, xpos, feats, deprel
        private static string Describe(string[] cols) => string.Join('\u0002', cols[1].ToLowerInvariant(), cols[2], cols[3], cols[4], cols[5], cols[7]);

        private static void Count(Dictionary<string, Dictionary<string, int>> table, string key, string value)
        {
            if (!table.TryGetValue(key, out Dictionary<string, int>? counts))
            {
                table[key] = counts = [];
            }

            counts[value] = counts.GetValueOrDefault(value) + 1;
        }

        private static string Best(Dictionary<string, int> counts) => counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).First().Key;

        // The words of a whole-form token ("à"), or null for any other token. Both words hang from the token's head (-2 is filled in by the caller).
        public WordRow[]? Whole(string text) =>
            _whole.TryGetValue(text.ToLowerInvariant(), out string[][]? list)
                ? [.. list.Select((d, k) => new WordRow(CopyCase(d[0], text, k == 0), d[1], d[2], d[3], d[4], -2, d[5]))]
                : null;

        // The word of an enclitic token "-lo": the spelling the training file gives it ("o"), else the pronoun as written.
        public string EncliticWord(string pronoun) => _enclitic.TryGetValue(pronoun.ToLowerInvariant(), out string? word) ? CopyCase(word, pronoun, false) : pronoun;
    }

    // "Do" gives "De o" and "DO" gives "DE O"; a lower-case surface form gives the lower-case words.
    private static string CopyCase(string word, string surface, bool first)
    {
        if (surface.Length > 1 && surface.All(char.IsUpper))
        {
            return word.ToUpperInvariant();
        }

        return first && char.IsUpper(surface[0]) ? char.ToUpperInvariant(word[0]) + word[1..] : word;
    }

    // Raw text: contractions and enclitics are found from the pieces (neighbouring tokens that together spell one); "à" is expanded.
    private static void WriteRawSentence(StringBuilder sb, Doc doc, int start, int end)
    {
        int n = end - start;
        var rows = new List<WordRow>();
        var groups = new List<(int First, int Last, string Form)>();
        var firstWord = new int[n];
        var headToken = new List<int>(); // per row: the head token (relative to start), -1 for the root
        bool parsed = false;
        for (int k = start; k < end && !parsed; k++)
        {
            parsed = doc[k].Dep.Length > 0;
        }

        for (int i = 0; i < n;)
        {
            Token t = doc[start + i];
            // Contractions ("d" + "o") and enclitics ("trata" + "-se") are found from the pieces by the segmentation data.
            if (i + 1 < n && SegmentationData.Portuguese.TryMatchMultiword(doc, start + i, out _, out string[]? words) && words is not null)
            {
                Token u = doc[start + i + 1];
                bool enclitic = doc.Text[u.Idx] == '-';
                groups.Add((rows.Count, rows.Count + 1, doc.Text.Substring(t.Idx, u.EndIdx - t.Idx)));
                for (int w = 0; w < 2; w++)
                {
                    Token piece = doc[start + i + w];
                    firstWord[i + w] = rows.Count;
                    string form = enclitic && w == 1 ? Lex.EncliticWord(words[1]) : words[w];
                    rows.Add(new WordRow(form, piece.Lemma, piece.Pos, piece.Tag, piece.Morph, 0, piece.Dep));
                    headToken.Add(Relative(piece, start, i + w));
                }

                i += 2;
                continue;
            }

            string text = t.ToString();
            if (Lex.Whole(text) is { } expanded)
            {
                groups.Add((rows.Count, rows.Count + expanded.Length - 1, text));
                firstWord[i] = rows.Count;
                int token = Relative(t, start, i);
                foreach (WordRow x in expanded)
                {
                    rows.Add(x);
                    headToken.Add(token);
                }
            }
            else
            {
                firstWord[i] = rows.Count;
                rows.Add(new WordRow(text, t.Lemma, t.Pos, t.Tag, t.Morph, 0, t.Dep));
                headToken.Add(Relative(t, start, i));
            }

            i++;
        }

        for (int r = 0; r < rows.Count; r++)
        {
            int h = headToken[r];
            rows[r] = rows[r] with { Head = h == -1 ? -1 : firstWord[h] };
        }

        WriteRows(sb, rows, groups, parsed);
    }

    // The head of a token relative to the sentence start; -1 for a root.
    private static int Relative(Token t, int start, int index) => t.HeadIndex == start + index ? -1 : t.HeadIndex - start;

    // Writes one CoNLL-U sentence. HEAD and DEPREL are placeholders when no parser ran (the first word is the root, the rest hang off it) so
    // that the scorer accepts the file.
    private static void WriteRows(StringBuilder sb, List<WordRow> rows, List<(int First, int Last, string Form)> groups, bool parsed)
    {
        int next = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            if (next < groups.Count && groups[next].First == i)
            {
                sb.Append(groups[next].First + 1).Append('-').Append(groups[next].Last + 1).Append('\t').Append(groups[next].Form).Append("\t_\t_\t_\t_\t_\t_\t_\t_\n");
                next++;
            }

            WordRow w = rows[i];
            string head;
            string deprel;
            if (!parsed)
            {
                (head, deprel) = i == 0 ? ("0", "root") : ("1", "dep");
            }
            else if (w.Head < 0)
            {
                (head, deprel) = ("0", "root");
            }
            else
            {
                head = (w.Head + 1).ToString(CultureInfo.InvariantCulture);
                deprel = Cell(w.Deprel);
            }

            sb.Append(i + 1).Append('\t').Append(w.Form).Append('\t').Append(Cell(w.Lemma)).Append('\t').Append(Cell(w.Pos)).Append('\t')
                .Append(Cell(w.Tag)).Append('\t').Append(Cell(w.Morph)).Append('\t').Append(head).Append('\t').Append(deprel).Append("\t_\t_\n");
        }

        sb.Append('\n');
    }

    // ---- prepare: the treebank with collapsed multiword tokens, for training and development ----

    // prepare IN.conllu OUT.conllu
    private static int Prepare(string input, string output)
    {
        var sb = new StringBuilder();
        var block = new List<string>();
        int sentences = 0;
        int collapsed = 0;

        void Flush()
        {
            if (block.Count > 0)
            {
                collapsed += PrepareSentence(block, sb);
                sentences++;
                block.Clear();
            }
        }

        foreach (string line in File.ReadLines(input))
        {
            if (line.Length == 0)
            {
                Flush();
            }
            else
            {
                block.Add(line);
            }
        }

        Flush();
        File.WriteAllText(output, sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"{input}: {sentences} sentences, {collapsed} multiword tokens rewritten -> {output}");
        return 0;
    }

    private static int PrepareSentence(List<string> lines, StringBuilder sb)
    {
        var words = new List<string[]>();
        var ranges = new List<(int First, int Last, string[] Cols)>();
        foreach (string line in lines)
        {
            if (line[0] == '#')
            {
                continue;
            }

            string[] cols = line.Split('\t');
            if (cols.Length < 10 || cols[0].Contains('.'))
            {
                continue;
            }

            if (cols[0].Contains('-'))
            {
                string[] ends = cols[0].Split('-');
                ranges.Add((int.Parse(ends[0], CultureInfo.InvariantCulture), int.Parse(ends[1], CultureInfo.InvariantCulture), cols));
            }
            else
            {
                words.Add(cols);
            }
        }

        // Whole: the words of the group become one word. Enclitic: the group stays two words, with the forms the tokenizer makes. Pieces: kept.
        List<(int First, int Last, string[] Cols)> whole = [.. ranges.Where(r => Classify(r.Cols[1], r.Last - r.First + 1) == Multiword.Whole)];
        List<(int First, int Last, string[] Cols)> enclitic = [.. ranges.Where(r => Classify(r.Cols[1], r.Last - r.First + 1) == Multiword.Enclitic)];
        List<(int First, int Last, string[] Cols)> pieces = [.. ranges.Where(r => Classify(r.Cols[1], r.Last - r.First + 1) == Multiword.Pieces)];

        // For every old word id: the id of the word that stands for it after merging (the first word of its group, else itself).
        var representative = new int[words.Count + 1];
        for (int id = 1; id <= words.Count; id++)
        {
            representative[id] = id;
        }

        foreach ((int first, int last, _) in whole)
        {
            for (int id = first; id <= last; id++)
            {
                representative[id] = first;
            }
        }

        // The new numbering: one id per representative, in order.
        var newId = new int[words.Count + 1];
        int count = 0;
        for (int id = 1; id <= words.Count; id++)
        {
            if (representative[id] == id)
            {
                newId[id] = ++count;
            }
        }

        foreach (string line in lines.Where(l => l[0] == '#'))
        {
            sb.Append(line).Append('\n');
        }

        for (int id = 1; id <= words.Count; id++)
        {
            foreach ((int first, int last, string[] cols) in pieces)
            {
                if (first == id)
                {
                    sb.Append(newId[first]).Append('-').Append(newId[representative[last]]).Append('\t').Append(cols[1]).Append("\t_\t_\t_\t_\t_\t_\t_\t").Append(cols[9]).Append('\n');
                }
            }

            if (representative[id] != id)
            {
                continue;
            }

            string[] w = words[id - 1];
            string[] source = w;
            string form = w[1];
            string misc = w[9];
            (int First, int Last, string[] Cols) group = whole.Find(m => m.First == id);
            if (group.Cols is not null)
            {
                form = group.Cols[1];
                misc = group.Cols[9];
                // The head of the merged word is the head of the first member whose head lies outside the group.
                for (int m = group.First; m <= group.Last; m++)
                {
                    int h = int.Parse(words[m - 1][6], CultureInfo.InvariantCulture);
                    if (h == 0 || h < group.First || h > group.Last)
                    {
                        source = words[m - 1];
                        break;
                    }
                }
            }

            foreach ((int first, int last, string[] cols) in enclitic)
            {
                int hyphen = cols[1].IndexOf('-');
                if (id == first)
                {
                    form = cols[1][..hyphen];
                    misc = misc == "_" ? "SpaceAfter=No" : misc.Contains("SpaceAfter=No", StringComparison.Ordinal) ? misc : misc + "|SpaceAfter=No";
                }
                else if (id == last)
                {
                    form = cols[1][hyphen..];
                    misc = cols[9];
                }
            }

            int head = int.Parse(source[6], CultureInfo.InvariantCulture);
            sb.Append(newId[id]).Append('\t').Append(form).Append('\t').Append(w[2]).Append('\t').Append(w[3]).Append('\t').Append(w[4]).Append('\t').Append(w[5]).Append('\t')
                .Append(head == 0 ? 0 : newId[representative[head]]).Append('\t').Append(source[7]).Append("\t_\t").Append(misc).Append('\n');
        }

        sb.Append('\n');
        return whole.Count + enclitic.Count;
    }
}
