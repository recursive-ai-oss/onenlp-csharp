using RecursiveAI.OneNlp.Data;

namespace RecursiveAI.OneNlp.Neural.Components;

/// <summary>
/// A trained word tokenizer for text written without spaces (Chinese): it cuts the text into characters, asks a tagger for each character
/// whether it begins a word, and joins the characters between two beginnings into one token.
/// </summary>
/// <remarks>
/// <para>
/// <b>Model.</b> Character-based segmentation as sequence labelling (Xue 2003, "Chinese Word Segmentation as Character Tagging"; Peng,
/// Feng and McCallum 2004 for the sequence view), with the labels reduced to two: <c>B</c> (the character begins a word) and <c>I</c> (it
/// continues the word before it). Every sequence of these two labels is a valid segmentation, so reading the most likely label of each
/// character needs no constrained decoding. The labels come from a <see cref="Tagger"/> over a <see cref="Encoders.FastEncoder"/> whose
/// "tokens" are the characters of the text: its hashed neighbour, word-pair and wider-context tables give each character the characters
/// around it, and its recurrent layer gives it the whole sentence.
/// </para>
/// <para>
/// <b>Units.</b> The characters of the model are <see cref="Units(string)"/>: every character that is not white space is one unit, except that a
/// run of Latin, Greek or Cyrillic letters, and a run of digits (with decimal points inside), is one unit, because the treebanks
/// keep such runs whole. White space is never inside a token, and the first unit after it always begins a word. Long texts are cut after a
/// sentence-ending mark (at most 1,024 units per piece) so the recurrent layer's cost stays linear.
/// </para>
/// <para>
/// Train it like any tagger: make reference documents whose tokens are <see cref="Units(string)"/> and whose <see cref="TokenColumn.Tag"/> is
/// <see cref="BeginLabel"/> or <see cref="InsideLabel"/> (<see cref="Tags"/>), train a <see cref="Tagger"/> on them with the pipeline's
/// <c>Train</c> extension, then wrap the trained tagger in a segmenter. <c>PipelineFile</c> saves and loads a pipeline whose tokenizer
/// is a segmenter. Safe for concurrent use. Offsets are UTF-16 offsets into the original string; nothing is normalized.
/// </para>
/// </remarks>
public sealed class NeuralWordSegmenter : IWordTokenizer, IDisposable
{
    /// <summary>The label of a character that begins a word: <c>B</c>.</summary>
    public const string BeginLabel = "B";

    /// <summary>The label of a character that continues a word: <c>I</c>.</summary>
    public const string InsideLabel = "I";

    /// <summary>The name of the tagger inside a saved pipeline and in the registry of heads: <c>segmenter</c>.</summary>
    public const string HeadName = "segmenter";

    private const int MaxUnitsPerPiece = 1024;

    private readonly StringStore _store = new();
    private readonly int _beginId;

    /// <summary>Wraps a trained tagger.</summary>
    /// <param name="head">A tagger of the <see cref="TokenColumn.Tag"/> column whose labels are exactly <see cref="BeginLabel"/> and <see cref="InsideLabel"/>. The segmenter takes ownership: disposing the segmenter disposes the tagger (and, when the tagger owns it, its encoder).</param>
    /// <exception cref="ArgumentException">The tagger is not initialized or has other labels.</exception>
    public NeuralWordSegmenter(Tagger head)
    {
        ArgumentNullException.ThrowIfNull(head);
        if (!head.IsInitialized || head.Column != TokenColumn.Tag || head.Labels.Count != 2 || head.Labels[0] != BeginLabel || head.Labels[1] != InsideLabel)
        {
            throw new ArgumentException("A segmenter needs an initialized tagger of the Tag column with the labels B and I.", nameof(head));
        }

        Head = head;
        _beginId = _store.Add(BeginLabel);
    }

    /// <summary>The tagger that decides, for each character, whether it begins a word.</summary>
    public Tagger Head { get; }

    /// <summary>Releases the tagger and what it owns.</summary>
    public void Dispose() => Head.Dispose();

    /// <inheritdoc/>
    public Doc Tokenize(string text, StringStore strings)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(strings);
        TokenBoundary[] units = Units(text);
        if (units.Length == 0)
        {
            return new Doc(strings, text, []);
        }

        // Pieces of at most MaxUnitsPerPiece units, cut after a sentence-ending mark where there is one in the last quarter.
        var pieces = new List<Doc>();
        var ranges = new List<(int From, int To)>();
        int from = 0;
        while (from < units.Length)
        {
            int to = Math.Min(units.Length, from + MaxUnitsPerPiece);
            if (to < units.Length)
            {
                for (int k = to; k > from + (MaxUnitsPerPiece * 3 / 4); k--)
                {
                    if (EndsSentence(text, units[k - 1]))
                    {
                        to = k;
                        break;
                    }
                }
            }

            pieces.Add(new Doc(_store, text, units.AsSpan(from, to - from)));
            ranges.Add((from, to));
            from = to;
        }

        Head.ProcessBatch(pieces.ToArray());

        var words = new List<TokenBoundary>(units.Length / 2 + 1);
        int wordStart = -1;
        int wordEnd = 0;
        for (int p = 0; p < pieces.Count; p++)
        {
            ReadOnlySpan<int> tags = pieces[p].GetColumn(TokenColumn.Tag);
            for (int i = 0; i < tags.Length; i++)
            {
                TokenBoundary unit = units[ranges[p].From + i];
                bool begins = tags[i] == _beginId || wordStart < 0 || (unit.Start != wordEnd && HasGap(text, wordEnd, unit.Start));
                if (begins)
                {
                    if (wordStart >= 0)
                    {
                        words.Add(new TokenBoundary(wordStart, wordEnd - wordStart));
                    }

                    wordStart = unit.Start;
                }

                wordEnd = unit.End;
            }
        }

        if (wordStart >= 0)
        {
            words.Add(new TokenBoundary(wordStart, wordEnd - wordStart));
        }

        return new Doc(strings, text, words.ToArray());
    }

    /// <summary>
    /// Cuts a text into the units the model labels: each character that is not white space, except that a run of letters of the Latin, Greek
    /// or Cyrillic scripts and a run of digits (a full stop or comma between two digits stays inside) is one unit. A surrogate pair is one character.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The units in order, as UTF-16 ranges of <paramref name="text"/>; none overlap and each is non-empty.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static TokenBoundary[] Units(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var units = new List<TokenBoundary>(text.Length);
        int i = 0;
        while (i < text.Length)
        {
            char c = text[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            int start = i;
            if (IsLetterRun(c))
            {
                while (i < text.Length && IsLetterRun(text[i]))
                {
                    i++;
                }
            }
            else if (IsDigit(c))
            {
                i++;
                while (i < text.Length && (IsDigit(text[i]) || (text[i] is '.' or ',' && i + 1 < text.Length && IsDigit(text[i + 1]))))
                {
                    i++;
                }
            }
            else
            {
                i += char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]) ? 2 : 1;
            }

            units.Add(new TokenBoundary(start, i - start));
        }

        return [.. units];
    }

    /// <summary>
    /// The training labels of a text: for each of its <see cref="Units(string)"/>, <see cref="BeginLabel"/> when a gold word starts there and
    /// <see cref="InsideLabel"/> otherwise. A gold word boundary inside a unit (the word "3" and the word "G" in "3G" cannot be told apart
    /// by a model that labels runs) is not representable and is counted in <paramref name="lost"/>.
    /// </summary>
    /// <param name="units">The units of the text.</param>
    /// <param name="goldStarts">Start offsets of the gold words, ascending.</param>
    /// <param name="lost">The number of gold word starts that fall inside a unit.</param>
    /// <returns>One label per unit.</returns>
    public static string[] Tags(ReadOnlySpan<TokenBoundary> units, ReadOnlySpan<int> goldStarts, out int lost)
    {
        var tags = new string[units.Length];
        int g = 0;
        int matched = 0;
        for (int u = 0; u < units.Length; u++)
        {
            while (g < goldStarts.Length && goldStarts[g] < units[u].Start)
            {
                g++;
            }

            bool begins = g < goldStarts.Length && goldStarts[g] == units[u].Start;
            tags[u] = begins ? BeginLabel : InsideLabel;
            if (begins)
            {
                matched++;
            }
        }

        lost = goldStarts.Length - matched;
        return tags;
    }

    private static bool HasGap(string text, int from, int to)
    {
        for (int k = from; k < to; k++)
        {
            if (char.IsWhiteSpace(text[k]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool EndsSentence(string text, TokenBoundary unit) =>
        unit.Length == 1 && text[unit.Start] is '\u3002' or '\uFF01' or '\uFF1F' or '!' or '?' or '\uFF1B' or ';' or '\n';

    // Latin (to Latin Extended-B, and the Latin Extended Additional block), Greek and Cyrillic letters.
    private static bool IsLetterRun(char c) =>
        char.IsLetter(c) && (c < '\u0250' || c is >= '\u0370' and < '\u0530' || c is >= '\u1E00' and < '\u2000');

    // ASCII and full-width digits.
    private static bool IsDigit(char c) => c is >= '0' and <= '9' or >= '\uFF10' and <= '\uFF19';
}
