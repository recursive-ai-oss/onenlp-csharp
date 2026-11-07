using System.Text;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Scoring;

namespace RecursiveAI.OneNlp.Recipes.Accurate;

/// <summary>What a language contributes to a training run: documents of its training and development sentences, all sharing one string table
/// (that is how a scoped head knows which documents are its own).</summary>
public sealed record LanguageData(StringStore Store, List<Example> Train, List<Example> Dev, int HeldOut, int Dropped);

/// <summary>
/// Reads a <see cref="CorpusSpec"/> language into examples. Training sentences are the gating treebank's and the extra treebanks', without the extra
/// sentences that repeat a gating test sentence; the dev split is the treebanks' dev files, or every n-th training sentence when there are none.
/// </summary>
public static class CorpusReader
{
    /// <summary>Reads the universal-dependencies part of a language.</summary>
    /// <param name="spec">The corpus.</param>
    /// <param name="language">The language to read.</param>
    /// <param name="dataRoot">The directory the spec's relative paths start from.</param>
    /// <param name="sentencesPerDoc">Consecutive sentences joined into one document.</param>
    /// <exception cref="IOException">A file is missing.</exception>
    public static LanguageData ReadUd(CorpusSpec spec, CorpusLanguage language, string dataRoot, int sentencesPerDoc)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(language);
        var store = new StringStore();
        HashSet<string> testTexts = [];
        if (language.Gating.Test is { } gatingTest && language.Treebanks.Count > 1)
        {
            foreach (ConllUSentence s in ConllUReader.Read(Path.Combine(dataRoot, gatingTest)))
            {
                testTexts.Add(SentenceText(s));
            }
        }

        var train = new List<ConllUSentence>();
        var dev = new List<ConllUSentence>();
        int dropped = 0;
        int heldOut = 0;
        int index = 0;
        foreach (CorpusTreebank tb in language.Treebanks)
        {
            foreach (ConllUSentence s in ConllUReader.Read(Path.Combine(dataRoot, tb.Train), store))
            {
                if (tb.Role != "gating" && testTexts.Contains(SentenceText(s)))
                {
                    dropped++;
                    continue;
                }

                if (language.DevFromTrain && index++ % spec.HeldOutEvery == spec.HeldOutEvery - 1)
                {
                    dev.Add(s);
                    heldOut++;
                }
                else
                {
                    train.Add(s);
                }
            }

            if (tb.Dev is { } devPath)
            {
                dev.AddRange(ConllUReader.Read(Path.Combine(dataRoot, devPath), store));
            }
        }

        return new LanguageData(store, Documents(train, sentencesPerDoc, store), Documents(dev, sentencesPerDoc, store), heldOut, dropped);
    }

    /// <summary>Reads the entity part of a language: the train and dev files of all its entity sets in one string table.</summary>
    /// <param name="language">The language.</param>
    /// <param name="dataRoot">The directory the spec's relative paths start from.</param>
    /// <param name="trainLimit">At most this many training sentences per set, taken evenly (0 = all).</param>
    /// <param name="devLimit">The same for dev.</param>
    /// <param name="excludeTexts">Sentences to leave out (for example the language's UD test sentences).</param>
    public static LanguageData ReadNer(CorpusLanguage language, string dataRoot, int trainLimit, int devLimit, ISet<string>? excludeTexts = null)
    {
        ArgumentNullException.ThrowIfNull(language);
        var store = new StringStore();
        var train = new List<Example>();
        var dev = new List<Example>();
        foreach (CorpusEntitySet set in language.Ner)
        {
            train.AddRange(NerExamples(Path.Combine(dataRoot, set.Train), trainLimit, store, excludeTexts));
            if (set.Dev is { } d)
            {
                dev.AddRange(NerExamples(Path.Combine(dataRoot, d), devLimit, store, excludeTexts));
            }
        }

        return new LanguageData(store, train, dev, 0, 0);
    }

    private static string SentenceText(ConllUSentence s) => s.GetComment("text") ?? s.Doc.Text;

    // Entity types to the family scheme. WikiNER and UNER already use PER, ORG, LOC (and MISC); GermEval's OTH is MISC and its derived and
    // partial variants are outside; Turkish-WikiNER keeps PERSON, ORG, GPE and LOC (GPE joins LOC) and drops its other types.
    internal static string MapTag(string tag)
    {
        if (tag.Length < 3)
        {
            return "O";
        }

        string mapped = tag[2..] switch { "PER" or "PERSON" => "PER", "LOC" or "GPE" => "LOC", "ORG" => "ORG", "MISC" or "OTH" => "MISC", _ => string.Empty };
        return mapped.Length == 0 ? "O" : tag[..2] + mapped;
    }

    // An .iob2 file (a '# text = ...' comment, then one token per line: id, form, BIO tag, ...; a blank line ends a sentence) as examples;
    // at most limit sentences, taken evenly through the file so the sample is the same on every run (0 = all).
    public static List<Example> NerExamples(string path, int limit, StringStore strings, ISet<string>? excludeTexts = null)
    {
        var sentences = new List<(string? Text, List<string> Forms, List<string> Tags)>();
        string? text = null;
        var forms = new List<string>();
        var tags = new List<string>();
        foreach (string line in File.ReadLines(path))
        {
            if (line.StartsWith("# text = ", StringComparison.Ordinal))
            {
                text = line["# text = ".Length..];
            }
            else if (line.StartsWith('#'))
            {
                continue;
            }
            else if (line.Length == 0)
            {
                if (forms.Count > 0)
                {
                    sentences.Add((text, forms, tags));
                }

                text = null;
                forms = [];
                tags = [];
            }
            else
            {
                string[] cols = line.Split('\t');
                forms.Add(cols[1]);
                tags.Add(MapTag(cols[2]));
            }
        }

        if (forms.Count > 0)
        {
            sentences.Add((text, forms, tags));
        }

        if (excludeTexts is not null)
        {
            sentences.RemoveAll(s => excludeTexts.Contains(s.Text ?? string.Join(' ', s.Forms)));
        }

        var result = new List<Example>();
        double step = limit > 0 && sentences.Count > limit ? (double)sentences.Count / limit : 1;
        for (double at = 0; (int)at < sentences.Count; at += step)
        {
            (string? t, List<string> f, List<string> g) = sentences[(int)at];
            result.Add(NerExample(t, f, g, strings));
        }

        return result;
    }

    // Token offsets come from walking the sentence text; if a form is not found there, the forms are joined with spaces instead.
    private static Example NerExample(string? text, List<string> forms, List<string> tags, StringStore strings)
    {
        var starts = new int[forms.Count];
        bool ok = text is not null;
        int pos = 0;
        for (int i = 0; ok && i < forms.Count; i++)
        {
            int at = text!.IndexOf(forms[i], pos, StringComparison.Ordinal);
            if (at < 0)
            {
                ok = false;
                break;
            }

            starts[i] = at;
            pos = at + forms[i].Length;
        }

        if (!ok)
        {
            text = string.Join(' ', forms);
            pos = 0;
            for (int i = 0; i < forms.Count; i++)
            {
                starts[i] = pos;
                pos += forms[i].Length + 1;
            }
        }

        var tokens = new TokenBoundary[forms.Count];
        for (int i = 0; i < tokens.Length; i++)
        {
            tokens[i] = new TokenBoundary(starts[i], forms[i].Length);
        }

        var doc = new Doc(strings, text!, tokens);
        var entities = new List<SpanSpec>();
        foreach (LabeledSpan sp in SpanScorer.ExtractIob2(tags))
        {
            entities.Add(new SpanSpec(sp.Start, sp.End, sp.Label));
        }

        doc.SetEntities(entities.ToArray());
        var marks = new SentenceBoundary[tokens.Length];
        Array.Fill(marks, SentenceBoundary.Inside);
        marks[0] = SentenceBoundary.Start;
        doc.SetSentenceBoundaries(marks);
        return Example.FromReference(doc);
    }


    // Groups consecutive sentences into documents of perDoc sentences, with every annotation column kept: the heads of one example batch
    // (tags, morphology, lemmas, parse) all read the same documents.
    public static List<Example> Documents(string path, int perDoc, StringStore strings) => Documents(ConllUReader.Read(path, strings), perDoc, strings);

    /// <summary>The same for sentences already read (and perhaps filtered).</summary>
    public static List<Example> Documents(IEnumerable<ConllUSentence> sentences, int perDoc, StringStore strings)
    {
        var result = new List<Example>();
        var group = new List<ConllUSentence>();
        foreach (ConllUSentence s in sentences)
        {
            group.Add(s);
            if (group.Count == perDoc)
            {
                result.Add(Example.FromReference(Merge(group, strings)));
                group.Clear();
            }
        }

        if (group.Count > 0)
        {
            result.Add(Example.FromReference(Merge(group, strings)));
        }

        return result;
    }

    // The text of one sentence as the encoder reads it, with one token for each word. A multiword token whose words spell its surface form
    // (English "don't" = "do" + "n't") is written as it stands; one whose words do not (French "du" = "de" + "le", German "zum" = "zu" + "dem")
    // is written as its words separated by spaces, so that each word has characters of its own and a vector of its own. Inference does the same
    // from the tokenizer's pieces (MultiwordFormsComponent and the encoder's ExpandWordForms).
    private static void AppendWords(ConllUSentence s, StringBuilder text, List<TokenBoundary> tokens)
    {
        int n = s.Doc.Length;
        int m = 0;
        int word = 0;
        while (word < n)
        {
            bool spaceAfter;
            if (m < s.MultiwordTokens.Count && s.MultiwordTokens[m].FirstWord == word)
            {
                MultiwordToken mwt = s.MultiwordTokens[m++];
                int count = mwt.LastWord - mwt.FirstWord + 1;
                bool spelled = string.Concat(Enumerable.Range(word, count).Select(i => s.Forms[i])) == mwt.Form;
                if (spelled)
                {
                    int start = text.Length;
                    text.Append(mwt.Form);
                    int at = start;
                    for (int k = 0; k < count; k++)
                    {
                        tokens.Add(new TokenBoundary(at, s.Forms[word + k].Length));
                        at += s.Forms[word + k].Length;
                    }
                }
                else
                {
                    for (int k = 0; k < count; k++)
                    {
                        if (k > 0)
                        {
                            text.Append(' ');
                        }

                        tokens.Add(new TokenBoundary(text.Length, s.Forms[word + k].Length));
                        text.Append(s.Forms[word + k]);
                    }
                }

                spaceAfter = !mwt.Misc.Contains("SpaceAfter=No", StringComparison.Ordinal);
                word += count;
            }
            else
            {
                tokens.Add(new TokenBoundary(text.Length, s.Forms[word].Length));
                text.Append(s.Forms[word]);
                spaceAfter = !s.Misc[word].Contains("SpaceAfter=No", StringComparison.Ordinal);
                word++;
            }

            if (word < n && spaceAfter)
            {
                text.Append(' ');
            }
        }
    }

    /// <summary>One document from several sentences, with every annotation column kept.</summary>
    public static Doc Merge(List<ConllUSentence> sentences, StringStore strings)
    {
        var text = new StringBuilder();
        var tokens = new List<TokenBoundary>();
        var heads = new List<int>();
        var marks = new List<SentenceBoundary>();
        TokenColumn[] columns = [TokenColumn.Lemma, TokenColumn.Pos, TokenColumn.Tag, TokenColumn.Dep, TokenColumn.Morph];
        var values = columns.Select(_ => new List<int>()).ToArray();
        foreach (ConllUSentence s in sentences)
        {
            if (text.Length > 0)
            {
                text.Append(' ');
            }

            int tokenOffset = tokens.Count;
            Doc d = s.Doc;
            AppendWords(s, text, tokens);
            for (int t = 0; t < d.Length; t++)
            {
                heads.Add(tokenOffset + d.GetHeads()[t]);
                marks.Add(t == 0 ? SentenceBoundary.Start : SentenceBoundary.Inside);
                for (int c = 0; c < columns.Length; c++)
                {
                    values[c].Add(d.GetColumn(columns[c])[t]);
                }
            }
        }

        var doc = new Doc(strings, text.ToString(), tokens.ToArray());
        doc.SetHeads(heads.ToArray());
        for (int c = 0; c < columns.Length; c++)
        {
            doc.SetColumn(columns[c], values[c].ToArray());
        }

        doc.SetSentenceBoundaries(marks.ToArray());
        return doc;
    }

}
