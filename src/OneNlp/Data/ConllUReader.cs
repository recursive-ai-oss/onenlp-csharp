using System.Globalization;
using System.Text;

namespace RecursiveAI.OneNlp.Data;

/// <summary>Limits for reading CoNLL-U files. Anything over a limit is rejected with a <see cref="DataFormatException"/> before it is allocated.</summary>
public sealed class ConllUOptions
{
    /// <summary>The longest line accepted, in UTF-16 units. Default 1,048,576.</summary>
    public int MaxLineLength { get; init; } = 1 << 20;

    /// <summary>The most words (and multiword token lines) in one sentence. Default 100,000.</summary>
    public int MaxSentenceLines { get; init; } = 100_000;

    /// <summary>The most comment lines before one sentence. Default 10,000.</summary>
    public int MaxCommentLines { get; init; } = 10_000;
}

/// <summary>
/// Reads CoNLL-U (Universal Dependencies) files, lazily, one sentence at a time, as described by the CoNLL-U format specification
/// (universaldependencies.org/format.html): ten tab-separated columns, comment lines before a sentence, a blank line after it,
/// multiword token ranges (<c>1-2</c>) and empty nodes (<c>8.1</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reader is strict: a line that does not have ten columns, a word ID out of sequence, a head outside the sentence or a word that
/// is its own head is a <see cref="DataFormatException"/> carrying the line number. Input must be UTF-8 (a leading byte-order mark is
/// skipped); invalid bytes are an error, not replaced.
/// </para>
/// <para>
/// Reading never loads more than one sentence, so a file of any size streams in constant memory. The enumerables are single-use per
/// call: each call to <c>Read</c> starts again from the beginning of the path, but a stream or reader is consumed.
/// </para>
/// </remarks>
public static class ConllUReader
{
    /// <summary>Reads the sentences of a file.</summary>
    /// <param name="path">The file. It is opened when enumeration starts and closed when it ends or is disposed.</param>
    /// <param name="strings">The string table for the documents; a new one when <see langword="null"/>. Sharing one table across sentences keeps tag sets compact.</param>
    /// <param name="options">The limits; defaults when <see langword="null"/>.</param>
    /// <returns>The sentences in file order.</returns>
    /// <exception cref="DataFormatException">(While enumerating) the file is malformed.</exception>
    public static IEnumerable<ConllUSentence> Read(string path, StringStore? strings = null, ConllUOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        return ReadFile(path, strings ?? new StringStore(), options ?? new ConllUOptions());
    }

    /// <summary>Reads the sentences of a UTF-8 stream.</summary>
    /// <param name="stream">The stream; it is read to its end and left open.</param>
    /// <param name="strings">The string table for the documents; a new one when <see langword="null"/>.</param>
    /// <param name="options">The limits; defaults when <see langword="null"/>.</param>
    /// <returns>The sentences in stream order.</returns>
    /// <exception cref="DataFormatException">(While enumerating) the input is malformed.</exception>
    public static IEnumerable<ConllUSentence> Read(Stream stream, StringStore? strings = null, ConllUOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return ReadReader(new StreamReader(stream, new UTF8Encoding(false, true), false, 16384, true), strings ?? new StringStore(), options ?? new ConllUOptions());
    }

    /// <summary>Reads the sentences of a text.</summary>
    /// <param name="reader">The reader; it is read to its end and left open.</param>
    /// <param name="strings">The string table for the documents; a new one when <see langword="null"/>.</param>
    /// <param name="options">The limits; defaults when <see langword="null"/>.</param>
    /// <returns>The sentences in order.</returns>
    /// <exception cref="DataFormatException">(While enumerating) the input is malformed.</exception>
    public static IEnumerable<ConllUSentence> Read(TextReader reader, StringStore? strings = null, ConllUOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return ReadReader(reader, strings ?? new StringStore(), options ?? new ConllUOptions());
    }

    /// <summary>Reads a file as training examples: each sentence's document as the reference, with an unannotated copy to predict into.</summary>
    /// <param name="path">The file.</param>
    /// <param name="strings">The string table for the documents; a new one when <see langword="null"/>.</param>
    /// <param name="options">The limits; defaults when <see langword="null"/>.</param>
    /// <returns>One example per sentence, in file order.</returns>
    /// <exception cref="DataFormatException">(While enumerating) the file is malformed.</exception>
    public static IEnumerable<Example> ReadExamples(string path, StringStore? strings = null, ConllUOptions? options = null) =>
        Read(path, strings, options).Select(s => Example.FromReference(s.Doc));

    private static IEnumerable<ConllUSentence> ReadFile(string path, StringStore strings, ConllUOptions options)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 16384, FileOptions.SequentialScan);
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), false, 16384);
        foreach (ConllUSentence s in ReadReader(reader, strings, options))
        {
            yield return s;
        }
    }

    private static IEnumerable<ConllUSentence> ReadReader(TextReader reader, StringStore strings, ConllUOptions options)
    {
        var lines = new BoundedLineReader(reader, options.MaxLineLength);
        var sentence = new SentenceParser(strings, options);
        bool first = true;
        string? line;
        while ((line = lines.ReadLine()) is not null)
        {
            if (first)
            {
                first = false;
                if (line.Length > 0 && line[0] == '\uFEFF')
                {
                    line = line[1..];
                }
            }

            if (line.Length == 0)
            {
                if (sentence.HasLines)
                {
                    yield return sentence.Build(lines.LineNumber - 1);
                }

                continue;
            }

            sentence.Add(line, lines.LineNumber);
        }

        if (sentence.HasLines)
        {
            yield return sentence.Build(lines.LineNumber);
        }
    }

    private sealed class SentenceParser
    {
        private readonly StringStore _strings;
        private readonly ConllUOptions _options;
        private readonly List<string> _comments = [];
        private readonly List<string> _forms = [];
        private readonly List<string> _lemmas = [];
        private readonly List<string> _upos = [];
        private readonly List<string> _xpos = [];
        private readonly List<string> _feats = [];
        private readonly List<int> _heads = [];
        private readonly List<string> _deprels = [];
        private readonly List<string> _deps = [];
        private readonly List<string> _misc = [];
        private readonly List<int> _wordLines = [];
        private readonly List<MultiwordToken> _mwts = [];
        private readonly List<int> _mwtLines = [];
        private readonly List<EmptyNode> _empties = [];
        private int _firstLine;
        private int _openRangeEnd; // 1-based id of the last word of the range still being read, or 0

        public SentenceParser(StringStore strings, ConllUOptions options)
        {
            _strings = strings;
            _options = options;
        }

        public bool HasLines => _comments.Count > 0 || _forms.Count > 0 || _mwts.Count > 0 || _empties.Count > 0;

        public void Add(string line, int lineNumber)
        {
            if (!HasLines)
            {
                _firstLine = lineNumber;
            }

            if (line[0] == '#')
            {
                if (_forms.Count > 0 || _mwts.Count > 0 || _empties.Count > 0)
                {
                    throw new DataFormatException(lineNumber, "a comment line must come before the first word of a sentence");
                }

                if (_comments.Count >= _options.MaxCommentLines)
                {
                    throw new DataFormatException(lineNumber, $"more than {_options.MaxCommentLines} comment lines before one sentence");
                }

                _comments.Add(line);
                return;
            }

            if (_forms.Count + _mwts.Count >= _options.MaxSentenceLines)
            {
                throw new DataFormatException(lineNumber, $"the sentence has more than {_options.MaxSentenceLines} lines");
            }

            string[] f = SplitColumns(line, lineNumber);
            string id = f[0];
            int dash = id.IndexOf('-', StringComparison.Ordinal);
            if (dash >= 0)
            {
                AddRange(f, dash, lineNumber);
            }
            else if (id.Contains('.', StringComparison.Ordinal))
            {
                AddEmptyNode(line, id, lineNumber);
            }
            else
            {
                AddWord(f, lineNumber);
            }
        }

        public ConllUSentence Build(int lastLine)
        {
            int n = _forms.Count;
            if (n == 0)
            {
                throw new DataFormatException(_firstLine, "a sentence has comments or empty nodes but no words");
            }

            if (_openRangeEnd > n)
            {
                throw new DataFormatException(lastLine, "a multiword token range extends past the last word of the sentence");
            }

            int[] heads = new int[n];
            for (int i = 0; i < n; i++)
            {
                int h = _heads[i];
                if (h > n)
                {
                    throw new DataFormatException(_wordLines[i], $"head {h} is past the last word ({n}) of the sentence");
                }

                if (h == i + 1)
                {
                    throw new DataFormatException(_wordLines[i], "a word cannot be its own head");
                }

                heads[i] = h <= 0 ? i : h - 1;
            }

            var text = new StringBuilder();
            var boundaries = new TokenBoundary[n];
            int m = 0;
            int word = 0;
            while (word < n)
            {
                string surface;
                string misc;
                int count = 1;
                if (m < _mwts.Count && _mwts[m].FirstWord == word)
                {
                    surface = _mwts[m].Form;
                    misc = _mwts[m].Misc;
                    count = _mwts[m].LastWord - word + 1;
                    m++;
                }
                else
                {
                    surface = _forms[word];
                    misc = _misc[word];
                }

                int start = text.Length;
                if (count > 1 && surface.Length < count)
                {
                    // The surface has fewer characters than the multiword token has words (Portuguese "à" = "a" + "a"), so the
                    // words cannot each own a character of it. The text then spells the words, separated by a space, in its place.
                    surface = SpellWords(word, count, start, boundaries);
                }
                else
                {
                    SplitSurface(surface, word, count, start, boundaries);
                }

                text.Append(surface);
                word += count;
                if (word < n && !HasSpaceAfterNo(misc))
                {
                    text.Append(' ');
                }
            }

            var doc = new Doc(_strings, text.ToString(), boundaries);
            doc.SetColumn(TokenColumn.Lemma, Intern(_lemmas));
            doc.SetColumn(TokenColumn.Pos, Intern(_upos));
            doc.SetColumn(TokenColumn.Tag, Intern(_xpos));
            doc.SetColumn(TokenColumn.Morph, Intern(_feats));
            doc.SetColumn(TokenColumn.Dep, Intern(_deprels));
            doc.SetHeads(heads);
            var sentenceStarts = new SentenceBoundary[n];
            Array.Fill(sentenceStarts, SentenceBoundary.Inside);
            sentenceStarts[0] = SentenceBoundary.Start;
            doc.SetSentenceBoundaries(sentenceStarts);

            var result = new ConllUSentence(doc, [.. _comments], [.. _forms], [.. _deps], [.. _misc], [.. _mwts], [.. _empties]);
            Reset();
            return result;
        }

        private static string[] SplitColumns(string line, int lineNumber)
        {
            int tabs = 0;
            foreach (char c in line)
            {
                if (c == '\t')
                {
                    tabs++;
                }
            }

            if (tabs != 9)
            {
                throw new DataFormatException(lineNumber, $"expected 10 tab-separated columns, found {tabs + 1}");
            }

            return line.Split('\t');
        }

        private static bool TryParseId(string s, out int value)
        {
            value = 0;
            return s.Length is > 0 and <= 7 && int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value > 0;
        }

        private void AddWord(string[] f, int lineNumber)
        {
            if (!TryParseId(f[0], out int id) || id != _forms.Count + 1)
            {
                throw new DataFormatException(lineNumber, $"word ID '{f[0]}' is out of sequence (expected {_forms.Count + 1})");
            }

            if (f[1].Length == 0)
            {
                throw new DataFormatException(lineNumber, "the FORM column is empty");
            }

            int head = -1;
            if (f[6] != "_" && !(f[6].Length is > 0 and <= 7 && int.TryParse(f[6], NumberStyles.None, CultureInfo.InvariantCulture, out head)))
            {
                throw new DataFormatException(lineNumber, $"the HEAD column '{f[6]}' is neither an integer nor '_'");
            }

            _forms.Add(f[1]);
            _lemmas.Add(Unset(f[2]));
            _upos.Add(Unset(f[3]));
            _xpos.Add(Unset(f[4]));
            _feats.Add(Unset(f[5]));
            _heads.Add(head);
            _deprels.Add(Unset(f[7]));
            _deps.Add(Unset(f[8]));
            _misc.Add(Unset(f[9]));
            _wordLines.Add(lineNumber);
        }

        private void AddRange(string[] f, int dash, int lineNumber)
        {
            if (_forms.Count < _openRangeEnd)
            {
                throw new DataFormatException(lineNumber, "a multiword token starts inside another one");
            }

            string id = f[0];
            if (!TryParseId(id[..dash], out int first) || !TryParseId(id[(dash + 1)..], out int last) || first != _forms.Count + 1 || last <= first)
            {
                throw new DataFormatException(lineNumber, $"multiword token ID '{id}' is not a range of the next words");
            }

            if (last - first + 1 > _options.MaxSentenceLines)
            {
                throw new DataFormatException(lineNumber, "the multiword token range is too long");
            }

            if (f[1].Length == 0)
            {
                throw new DataFormatException(lineNumber, "the FORM column is empty");
            }

            _mwts.Add(new MultiwordToken(first - 1, last - 1, f[1], Unset(f[9])));
            _mwtLines.Add(lineNumber);
            _openRangeEnd = last;
        }

        private void AddEmptyNode(string line, string id, int lineNumber)
        {
            int dot = id.IndexOf('.', StringComparison.Ordinal);
            if (!(dot > 0 && IsDigits(id.AsSpan(0, dot)) && IsDigits(id.AsSpan(dot + 1))))
            {
                throw new DataFormatException(lineNumber, $"empty node ID '{id}' is not of the form N.M");
            }

            _empties.Add(new EmptyNode(_forms.Count, line));
        }

        private static bool IsDigits(ReadOnlySpan<char> s)
        {
            if (s.Length is 0 or > 7)
            {
                return false;
            }

            foreach (char c in s)
            {
                if (c is < '0' or > '9')
                {
                    return false;
                }
            }

            return true;
        }

        private static string Unset(string value) => value == "_" ? string.Empty : value;

        private static bool HasSpaceAfterNo(string misc)
        {
            foreach (string part in misc.Split('|'))
            {
                if (part == "SpaceAfter=No")
                {
                    return true;
                }
            }

            return false;
        }

        // Lays the word forms out one after another, a space between, and returns that text.
        private string SpellWords(int firstWord, int count, int start, TokenBoundary[] boundaries)
        {
            var spelled = new StringBuilder();
            for (int k = 0; k < count; k++)
            {
                if (k > 0)
                {
                    spelled.Append(' ');
                }

                string w = _forms[firstWord + k];
                boundaries[firstWord + k] = new TokenBoundary(start + spelled.Length, w.Length);
                spelled.Append(w);
            }

            return spelled.ToString();
        }

        // Places the words of a multiword token inside its surface form. When the word forms spell the surface form (English "don't"
        // as "do" + "n't" in a range, Arabic clitics) they get their own lengths; otherwise (Spanish "del" = "de" + "el") the
        // surface is cut into pieces of nearly equal length so each word still has characters of its own.
        private void SplitSurface(string surface, int firstWord, int count, int start, TokenBoundary[] boundaries)
        {
            if (count == 1)
            {
                boundaries[firstWord] = new TokenBoundary(start, surface.Length);
                return;
            }

            int total = 0;
            for (int k = 0; k < count; k++)
            {
                total += _forms[firstWord + k].Length;
            }

            bool spelled = total == surface.Length;
            if (spelled)
            {
                int at = 0;
                for (int k = 0; k < count && spelled; k++)
                {
                    string w = _forms[firstWord + k];
                    spelled = string.CompareOrdinal(surface, at, w, 0, w.Length) == 0;
                    at += w.Length;
                }
            }

            int offset = 0;
            for (int k = 0; k < count; k++)
            {
                int length = spelled ? _forms[firstWord + k].Length : (surface.Length / count) + (k < surface.Length % count ? 1 : 0);
                boundaries[firstWord + k] = new TokenBoundary(start + offset, length);
                offset += length;
            }
        }

        private int[] Intern(List<string> values)
        {
            int[] ids = new int[values.Count];
            for (int i = 0; i < ids.Length; i++)
            {
                ids[i] = _strings.Add(values[i]);
            }

            return ids;
        }

        private void Reset()
        {
            _comments.Clear();
            _forms.Clear();
            _lemmas.Clear();
            _upos.Clear();
            _xpos.Clear();
            _feats.Clear();
            _heads.Clear();
            _deprels.Clear();
            _deps.Clear();
            _misc.Clear();
            _wordLines.Clear();
            _mwts.Clear();
            _mwtLines.Clear();
            _empties.Clear();
            _openRangeEnd = 0;
        }
    }
}
