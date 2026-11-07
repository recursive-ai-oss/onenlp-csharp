using System.Globalization;
using System.Text;

namespace RecursiveAI.OneNlp.Scoring;

/// <summary>
/// A treebank prepared for scoring: the words of every sentence with their annotations, the tokens (including multiword tokens) and
/// the sentences as spans over one stream of characters, as the CoNLL 2018 shared task evaluation describes it (Zeman et al. 2018,
/// "CoNLL 2018 Shared Task: Multilingual Parsing from Raw Text to Universal Dependencies", §5; evaluation description at
/// universaldependencies.org/conll18/evaluation.html).
/// </summary>
/// <remarks>
/// The character stream is the concatenation of the token forms with spaces removed, so a gold and a predicted corpus over the same
/// text have the same stream whatever their tokenization. Words that belong to one multiword token share its character span.
/// Empty nodes (IDs such as <c>8.1</c>) are skipped.
/// </remarks>
public sealed class UdCorpus
{
    private readonly List<UdWord> _words = [];
    private readonly List<CharSpan> _tokens = [];
    private readonly List<CharSpan> _sentences = [];
    private readonly StringBuilder _characters = new();

    private UdCorpus()
    {
    }

    /// <summary>The number of sentences.</summary>
    public int SentenceCount => _sentences.Count;

    /// <summary>The number of syntactic words (multiword tokens count their words, not themselves).</summary>
    public int WordCount => _words.Count;

    internal IReadOnlyList<UdWord> Words => _words;

    internal IReadOnlyList<CharSpan> Tokens => _tokens;

    internal IReadOnlyList<CharSpan> Sentences => _sentences;

    internal string Characters => _characters.ToString();

    /// <summary>Reads a CoNLL-U file with <see cref="Data.ConllUReader"/>. Comment lines are ignored; empty nodes are skipped.</summary>
    /// <param name="reader">The source; it is read to the end and not disposed.</param>
    /// <returns>The corpus.</returns>
    /// <exception cref="InvalidDataException">A line does not have ten tab-separated columns, word IDs are not consecutive, a head is out
    /// of range, or a sentence's heads contain a cycle.</exception>
    public static UdCorpus Read(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var corpus = new UdCorpus();
        var builder = new SentenceBuilder(corpus);
        try
        {
            foreach (Data.ConllUSentence sentence in Data.ConllUReader.Read(reader))
            {
                AddSentence(builder, sentence);
                builder.Finish();
            }
        }
        catch (Data.DataFormatException e)
        {
            throw new InvalidDataException(e.Message, e);
        }

        return corpus;
    }

    // Feeds a sentence read by the shared CoNLL-U reader to the builder as the columns of its lines, multiword token lines included.
    private static void AddSentence(SentenceBuilder builder, Data.ConllUSentence sentence)
    {
        Doc doc = sentence.Doc;
        int next = 0;
        for (int i = 0; i < doc.Length; i++)
        {
            if (next < sentence.MultiwordTokens.Count && sentence.MultiwordTokens[next].FirstWord == i)
            {
                Data.MultiwordToken m = sentence.MultiwordTokens[next++];
                builder.Add(
                    [string.Create(CultureInfo.InvariantCulture, $"{m.FirstWord + 1}-{m.LastWord + 1}"), m.Form, "_", "_", "_", "_", "_", "_", "_", "_"],
                    0);
            }

            Token t = doc[i];
            int headId = t.HeadIndex == i ? 0 : t.HeadIndex + 1;
            builder.Add(
                [
                    (i + 1).ToString(CultureInfo.InvariantCulture),
                    sentence.Forms[i],
                    OrUnderscore(t.Lemma),
                    OrUnderscore(t.Pos),
                    OrUnderscore(t.Tag),
                    OrUnderscore(t.Morph),
                    headId.ToString(CultureInfo.InvariantCulture),
                    OrUnderscore(t.Dep),
                    "_",
                    "_",
                ],
                0);
        }
    }

    /// <summary>Reads a CoNLL-U file from a path.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The corpus.</returns>
    /// <exception cref="InvalidDataException">As for <see cref="Read(TextReader)"/>.</exception>
    public static UdCorpus ReadFile(string path)
    {
        using var reader = new StreamReader(path, Encoding.UTF8);
        return Read(reader);
    }

    /// <summary>
    /// Makes a corpus from documents: one sentence per <see cref="Doc.Sentences"/> entry (a document without boundaries is one
    /// sentence), one word per token, and no multiword tokens. <see cref="Token.Pos"/> is UPOS, <see cref="Token.Tag"/> is XPOS,
    /// <see cref="Token.Morph"/> the features and <see cref="Token.Dep"/> the relation; an empty value is the CoNLL-U <c>_</c>.
    /// A token that is its own head, or whose head lies in another sentence, is a root.
    /// </summary>
    /// <param name="docs">The documents, in order.</param>
    /// <returns>The corpus.</returns>
    /// <exception cref="InvalidDataException">The heads of a sentence contain a cycle.</exception>
    public static UdCorpus FromDocs(IEnumerable<Doc> docs)
    {
        ArgumentNullException.ThrowIfNull(docs);
        var corpus = new UdCorpus();
        var builder = new SentenceBuilder(corpus);
        foreach (Doc doc in docs)
        {
            if (doc.Length == 0)
            {
                continue;
            }

            foreach (Span sentence in doc.Sentences)
            {
                int id = 0;
                foreach (Token token in sentence)
                {
                    id++;
                    int head = token.HeadIndex;
                    int headId = head == token.Index || head < sentence.Start || head >= sentence.End ? 0 : head - sentence.Start + 1;
                    string[] columns =
                    [
                        id.ToString(CultureInfo.InvariantCulture),
                        Strip(token.TextSpan.ToString()),
                        OrUnderscore(token.Lemma),
                        OrUnderscore(token.Pos),
                        OrUnderscore(token.Tag),
                        OrUnderscore(token.Morph),
                        headId.ToString(CultureInfo.InvariantCulture),
                        OrUnderscore(token.Dep),
                        "_",
                        "_",
                    ];
                    builder.Add(columns, 0);
                }

                builder.Finish();
            }
        }

        return corpus;
    }

    private static string OrUnderscore(string value) => value.Length == 0 ? "_" : value;

    private static string Strip(string form) => form.Replace(" ", string.Empty, StringComparison.Ordinal);

    /// <summary>Collects the lines of one sentence and appends it to the corpus when it ends.</summary>
    private sealed class SentenceBuilder(UdCorpus corpus)
    {
        private readonly List<UdWord> _words = [];
        private readonly List<int> _heads = [];
        private int _firstToken = -1;
        private int _multiwordEnd;
        private int _multiwordToken;

        public void Add(string[] columns, int lineNumber)
        {
            string id = columns[0];
            if (id.Contains('.', StringComparison.Ordinal))
            {
                return; // empty node (enhanced graph only)
            }

            if (_firstToken < 0)
            {
                _firstToken = corpus._tokens.Count;
            }

            int dash = id.IndexOf('-', StringComparison.Ordinal);
            if (dash >= 0)
            {
                if (!int.TryParse(id.AsSpan(dash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out int end) || end <= _words.Count)
                {
                    throw new InvalidDataException($"Line {lineNumber.ToString(CultureInfo.InvariantCulture)}: bad multiword token range '{id}'.");
                }

                _multiwordToken = AddToken(columns[1], lineNumber);
                _multiwordEnd = end;
                return;
            }

            if (!int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out int wordId) || wordId != _words.Count + 1)
            {
                throw new InvalidDataException($"Line {lineNumber.ToString(CultureInfo.InvariantCulture)}: word ID '{id}' is not the next one.");
            }

            bool inMultiword = wordId <= _multiwordEnd;
            int token = inMultiword ? _multiwordToken : AddToken(columns[1], lineNumber);
            if (!int.TryParse(columns[6], NumberStyles.None, CultureInfo.InvariantCulture, out int head))
            {
                throw new InvalidDataException($"Line {lineNumber.ToString(CultureInfo.InvariantCulture)}: bad head '{columns[6]}'.");
            }

            CharSpan span = corpus._tokens[token];
            string deprel = columns[7];
            int colon = deprel.IndexOf(':', StringComparison.Ordinal);
            if (colon >= 0)
            {
                deprel = deprel[..colon]; // language-specific subtypes are ignored by the evaluation
            }

            _words.Add(new UdWord
            {
                Form = columns[1],
                Lemma = columns[2],
                Upos = columns[3],
                Xpos = columns[4],
                Feats = UniversalFeatures(columns[5]),
                Deprel = deprel,
                Start = span.Start,
                End = span.End,
                IsMultiword = inMultiword,
                Parent = -1,
            });
            _heads.Add(head);
        }

        public void Finish()
        {
            if (_words.Count == 0)
            {
                Reset();
                return;
            }

            int offset = corpus._words.Count;
            for (int i = 0; i < _words.Count; i++)
            {
                int head = _heads[i];
                if (head < 0 || head > _words.Count)
                {
                    throw new InvalidDataException($"Head {head.ToString(CultureInfo.InvariantCulture)} is out of range in a sentence of {_words.Count.ToString(CultureInfo.InvariantCulture)} words.");
                }

                _words[i].Parent = head == 0 ? -1 : offset + head - 1;
            }

            // A cycle in the heads would make the dependency scores meaningless; the reference evaluation rejects it too.
            for (int i = 0; i < _words.Count; i++)
            {
                int steps = 0;
                int current = _words[i].Parent;
                while (current >= 0)
                {
                    if (++steps > _words.Count)
                    {
                        throw new InvalidDataException("The heads of a sentence contain a cycle.");
                    }

                    current = current >= offset ? _words[current - offset].Parent : -1;
                }
            }

            for (int i = 0; i < _words.Count; i++)
            {
                if (_words[i].Parent >= 0 && _words[i].IsFunctional)
                {
                    (_words[_words[i].Parent - offset].FunctionalChildren ??= []).Add(offset + i);
                }
            }

            corpus._words.AddRange(_words);
            corpus._sentences.Add(new CharSpan(corpus._tokens[_firstToken].Start, corpus._tokens[^1].End));
            Reset();
        }

        private int AddToken(string form, int lineNumber)
        {
            string chars = Strip(form);
            if (chars.Length == 0)
            {
                throw new InvalidDataException($"Line {lineNumber.ToString(CultureInfo.InvariantCulture)}: empty form.");
            }

            int start = corpus._characters.Length;
            corpus._characters.Append(chars);
            corpus._tokens.Add(new CharSpan(start, start + chars.Length));
            return corpus._tokens.Count - 1;
        }

        // Observed in the output of the shared-task evaluation script (conll18_ud_eval.py, SHA-256 1072e02a...), run as a program: UFeats
        // compares only the 21 universal features of the UD guidelines, in any order; language-specific and layered features (Typo,
        // NumForm, Style, ExtPos, Number[psor], ...) are ignored, and no features at all is "_".
        private static readonly HashSet<string> UniversalFeatureNames = new(StringComparer.Ordinal)
        {
            "Abbr", "Animacy", "Aspect", "Case", "Definite", "Degree", "Evident", "Foreign", "Gender", "Mood", "NumType", "Number",
            "Person", "Polarity", "Polite", "Poss", "PronType", "Reflex", "Tense", "VerbForm", "Voice",
        };

        private static string UniversalFeatures(string feats)
        {
            if (feats == "_")
            {
                return feats;
            }

            var kept = new List<string>();
            foreach (string pair in feats.Split('|'))
            {
                int eq = pair.IndexOf('=', StringComparison.Ordinal);
                if (eq > 0 && UniversalFeatureNames.Contains(pair[..eq]))
                {
                    kept.Add(pair);
                }
            }

            kept.Sort(StringComparer.Ordinal);
            return kept.Count == 0 ? "_" : string.Join('|', kept);
        }

        private void Reset()
        {
            _words.Clear();
            _heads.Clear();
            _firstToken = -1;
            _multiwordEnd = 0;
        }
    }
}

/// <summary>A half-open range of positions in a corpus's character stream.</summary>
internal readonly record struct CharSpan(int Start, int End);

/// <summary>One syntactic word with the columns the evaluation reads.</summary>
internal sealed class UdWord
{
    // CoNLL 2018 evaluation description: content-word and functional relations (subtypes already removed).
    private static readonly HashSet<string> ContentRelations = new(StringComparer.Ordinal)
    {
        "nsubj", "obj", "iobj", "csubj", "ccomp", "xcomp", "obl", "vocative", "expl", "dislocated", "advcl", "advmod", "discourse",
        "nmod", "appos", "nummod", "acl", "amod", "conj", "fixed", "flat", "compound", "list", "parataxis", "orphan", "goeswith",
        "reparandum", "root", "dep",
    };

    private static readonly HashSet<string> FunctionalRelations = new(StringComparer.Ordinal)
    {
        "aux", "cop", "mark", "det", "clf", "case", "cc",
    };

    public required string Form { get; init; }

    public required string Lemma { get; init; }

    public required string Upos { get; init; }

    public required string Xpos { get; init; }

    public required string Feats { get; init; }

    public required string Deprel { get; init; }

    public required int Start { get; init; }

    public required int End { get; init; }

    public required bool IsMultiword { get; init; }

    /// <summary>The index of the head in the corpus's word list, or -1 for a root.</summary>
    public int Parent { get; set; }

    public List<int>? FunctionalChildren { get; set; }

    public bool IsContent => ContentRelations.Contains(Deprel);

    public bool IsFunctional => FunctionalRelations.Contains(Deprel);
}
