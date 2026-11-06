using RecursiveAI.OneNlp.Data;

namespace RecursiveAI.OneNlp.Neural.Components;

/// <summary>
/// A word tokenizer that cuts a written token into the several syntactic words Universal Dependencies counts in it (a multiword token:
/// Turkish <c>vardır</c> is <c>var</c> + <c>dır</c>), by asking a trained character tagger where a new word begins.
/// </summary>
/// <remarks>
/// <para>
/// <b>Model.</b> Universal Dependencies tokenization has two steps (Zeman et al. 2018, CoNLL 2018 shared task overview, section 2): cut the
/// text into surface tokens, then expand the tokens that are multiword. The first step is an <see cref="IWordTokenizer"/> given to the
/// constructor (the rule tokenizer of the language). The second is character-based labelling, as in the shared-task systems that predict, for
/// every character of a token, whether a word starts there: each character of a candidate token is labelled <see cref="BeginLabel"/> (a word
/// starts here) or <see cref="InsideLabel"/>. The first character always begins a word; any other <see cref="BeginLabel"/> is a cut. Every
/// label sequence is a valid expansion, so reading the most likely label of each character needs no constrained decoding.
/// </para>
/// <para>
/// <b>Context.</b> Whether a suffix is a word of its own often depends on the sentence (Turkish <c>vardı</c> is <c>var</c> + <c>dı</c> as
/// "there was" and one word as "arrived"). The tagger therefore reads a <i>window</i> (<see cref="MakeWindow"/>): the surface tokens of one sentence
/// (at most <see cref="MaxWindowTokens"/>), as units separated by a space unit. A candidate token contributes one unit for each character; any other
/// token is one unit. A recurrent layer in the encoder (<see cref="Encoders.FastEncoder"/>) then reads the characters of the token and the words around it.
/// </para>
/// <para>
/// <b>Candidates.</b> Only a token of <see cref="MinCandidateLength"/> to <see cref="MaxCandidateLength"/> UTF-16 units that consists of
/// letters only (<see cref="IsCandidate"/>) is cut; a number, a URL, punctuation or a token with an apostrophe or hyphen never is. The
/// pieces are slices of the surface token, so offsets stay into the original string and nothing is normalized. The pieces of a token stay
/// adjacent (no gap), which is how <see cref="TryMatch"/> finds them again.
/// </para>
/// <para>
/// Train it like any tagger: make reference documents with <see cref="MakeWindow"/> whose <see cref="TokenColumn.Tag"/> is
/// <see cref="BeginLabel"/> or <see cref="InsideLabel"/> (<see cref="Tags"/>), train a <see cref="Tagger"/> on them with the pipeline's
/// <c>Train</c> extension, then wrap the trained tagger and the surface tokenizer in a splitter. <c>PipelineFile</c> saves and loads a pipeline
/// whose tokenizer is a splitter over the rule or the UAX #29 tokenizer. The splitter is language-neutral: a language whose treebank writes
/// contractions or clitics as multiword tokens trains its own. Safe for concurrent use.
/// </para>
/// </remarks>
public sealed class NeuralMultiwordSplitter : IWordTokenizer, IDisposable
{
    /// <summary>The label of a character that begins a word: <c>B</c>.</summary>
    public const string BeginLabel = "B";

    /// <summary>The label of a character that continues a word: <c>I</c>.</summary>
    public const string InsideLabel = "I";

    /// <summary>The name of the tagger inside a saved pipeline and in the registry of heads: <c>splitter</c>.</summary>
    public const string HeadName = "splitter";

    /// <summary>The shortest token that is looked at: three units (a cut leaves at least one unit on each side).</summary>
    public const int MinCandidateLength = 3;

    /// <summary>The longest token that is looked at: 48 units. Longer letter runs are never cut.</summary>
    public const int MaxCandidateLength = 48;

    private readonly StringStore _store = new();
    private readonly int _beginId;
    private int _minCutPercent = 50;

    /// <summary>Wraps a surface tokenizer and a trained tagger.</summary>
    /// <param name="inner">The tokenizer that cuts the text into surface tokens.</param>
    /// <param name="head">A tagger of the <see cref="TokenColumn.Tag"/> column whose labels are exactly <see cref="BeginLabel"/> and <see cref="InsideLabel"/>. The splitter takes ownership: disposing the splitter disposes the tagger (and, when the tagger owns it, its encoder) and the inner tokenizer if it is disposable.</param>
    /// <exception cref="ArgumentException">The tagger is not initialized or has other labels.</exception>
    public NeuralMultiwordSplitter(IWordTokenizer inner, Tagger head)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(head);
        if (!head.IsInitialized || head.Column != TokenColumn.Tag || head.Labels.Count != 2 || head.Labels[0] != BeginLabel || head.Labels[1] != InsideLabel)
        {
            throw new ArgumentException("A splitter needs an initialized tagger of the Tag column with the labels B and I.", nameof(head));
        }

        Inner = inner;
        Head = head;
        _beginId = _store.Add(BeginLabel);
    }

    /// <summary>The tokenizer that cuts the text into surface tokens.</summary>
    public IWordTokenizer Inner { get; }

    /// <summary>The tagger that decides, for each character of a candidate token, whether a word begins there.</summary>
    public Tagger Head { get; }

    /// <summary>
    /// How sure the tagger must be, in percent (50 to 99), before a character is made the start of a word. The default, 50, is the plain most
    /// likely label. A wrong cut costs the whole word and two wrong ones appear in its place, a missed cut costs the two words, so a higher value
    /// trades missed cuts for fewer wrong ones; the treebank decides which is better (choose it on development data). Saved with the model. Set it
    /// before the splitter is shared between threads.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside 50 to 99.</exception>
    public int MinCutPercent
    {
        get => _minCutPercent;
        set => _minCutPercent = value is >= 50 and <= 99 ? value : throw new ArgumentOutOfRangeException(nameof(value), "MinCutPercent must be 50 to 99.");
    }

    /// <summary>Releases the tagger and what it owns, and the inner tokenizer when it is disposable.</summary>
    public void Dispose()
    {
        Head.Dispose();
        (Inner as IDisposable)?.Dispose();
    }

    /// <summary>Whether a token is looked at: <see cref="MinCandidateLength"/> to <see cref="MaxCandidateLength"/> units, all letters.</summary>
    /// <param name="token">The surface token.</param>
    public static bool IsCandidate(ReadOnlySpan<char> token)
    {
        if (token.Length is < MinCandidateLength or > MaxCandidateLength)
        {
            return false;
        }

        foreach (char c in token)
        {
            if (!char.IsLetter(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The most surface tokens in one window: 40.</summary>
    public const int MaxWindowTokens = 40;

    /// <summary>
    /// Cuts a token sequence into windows: a window ends after a token that is a lone <c>.</c>, <c>!</c>, <c>?</c> or <c>…</c> and after
    /// <see cref="MaxWindowTokens"/> tokens.
    /// </summary>
    /// <param name="tokens">The surface tokens of a text, or of a sentence.</param>
    /// <returns>Half-open ranges <c>(from, to)</c> of token indices that cover all tokens in order.</returns>
    public static List<(int From, int To)> Windows(IReadOnlyList<string> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        var windows = new List<(int, int)>();
        int from = 0;
        for (int i = 0; i < tokens.Count; i++)
        {
            bool ends = tokens[i] is "." or "!" or "?" or "…";
            if (ends || i + 1 - from == MaxWindowTokens || i + 1 == tokens.Count)
            {
                windows.Add((from, i + 1));
                from = i + 1;
            }
        }

        return windows;
    }

    /// <summary>
    /// Builds the document the tagger reads for a window of surface tokens: the tokens joined by single spaces, a unit for each character of a
    /// candidate token (<see cref="IsCandidate"/>), one unit for any other token and one for each space.
    /// </summary>
    /// <param name="store">The string table of the new document.</param>
    /// <param name="tokens">The surface tokens of the window, none empty.</param>
    /// <param name="firstUnit">For each token, the index of its first unit.</param>
    /// <returns>The document, without tags.</returns>
    public static Doc MakeWindow(StringStore store, IReadOnlyList<string> tokens, out int[] firstUnit)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(tokens);
        var text = new System.Text.StringBuilder();
        var units = new List<TokenBoundary>();
        firstUnit = new int[tokens.Count];
        for (int k = 0; k < tokens.Count; k++)
        {
            string token = tokens[k];
            if (token.Length == 0)
            {
                throw new ArgumentException("A token must not be empty.", nameof(tokens));
            }

            if (k > 0)
            {
                units.Add(new TokenBoundary(text.Length, 1));
                text.Append(' ');
            }

            firstUnit[k] = units.Count;
            if (IsCandidate(token))
            {
                for (int i = 0; i < token.Length; i++)
                {
                    units.Add(new TokenBoundary(text.Length + i, 1));
                }
            }
            else
            {
                units.Add(new TokenBoundary(text.Length, token.Length));
            }

            text.Append(token);
        }

        return new Doc(store, text.ToString(), units.ToArray());
    }

    /// <summary>
    /// The label of a unit of a window, for training: <see cref="BeginLabel"/> where a gold word starts (the first unit of every token, the
    /// space units, and each unit where a gold piece of a candidate token starts), else <see cref="InsideLabel"/>.
    /// </summary>
    /// <param name="length">The length of a candidate token.</param>
    /// <param name="pieceLengths">The lengths of the gold pieces in order; they must add up to <paramref name="length"/>.</param>
    /// <returns>One label per character of the token.</returns>
    /// <exception cref="ArgumentException">The lengths do not add up to <paramref name="length"/> or one is not positive.</exception>
    public static string[] Tags(int length, ReadOnlySpan<int> pieceLengths)
    {
        var tags = new string[length];
        Array.Fill(tags, InsideLabel);
        int at = 0;
        foreach (int piece in pieceLengths)
        {
            if (piece <= 0 || at + piece > length)
            {
                throw new ArgumentException("Piece lengths must be positive and add up to the token length.", nameof(pieceLengths));
            }

            tags[at] = BeginLabel;
            at += piece;
        }

        if (at != length)
        {
            throw new ArgumentException("Piece lengths must be positive and add up to the token length.", nameof(pieceLengths));
        }

        return tags;
    }

    /// <summary>The unit indices of a window document that lie inside a candidate token after its first character: the places a cut may be.</summary>
    /// <param name="window">A document from <see cref="MakeWindow"/>.</param>
    /// <returns>Ascending unit indices.</returns>
    public static List<int> InteriorUnits(Doc window)
    {
        ArgumentNullException.ThrowIfNull(window);
        var interior = new List<int>();
        int i = 0;
        while (i < window.Length)
        {
            // A run of one-character letter units between space units is a candidate token (a one-character token is shorter than the minimum).
            int j = i;
            while (j < window.Length && window[j].EndIdx - window[j].Idx == 1 && char.IsLetter(window.Text[window[j].Idx]))
            {
                j++;
            }

            if (j - i is >= MinCandidateLength and <= MaxCandidateLength)
            {
                for (int k = i + 1; k < j; k++)
                {
                    interior.Add(k);
                }
            }

            i = Math.Max(j, i + 1);
        }

        return interior;
    }

    /// <inheritdoc/>
    public Doc Tokenize(string text, StringStore strings)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(strings);
        Doc surface = Inner.Tokenize(text, strings);
        int n = surface.Length;
        bool any = false;
        var tokens = new string[n];
        for (int t = 0; t < n; t++)
        {
            Token token = surface[t];
            tokens[t] = text.Substring(token.Idx, token.EndIdx - token.Idx);
            any |= IsCandidate(tokens[t]);
        }

        if (!any)
        {
            return surface;
        }

        List<(int From, int To)> windows = Windows(tokens);
        var docs = new Doc[windows.Count];
        var firstUnits = new int[windows.Count][];
        for (int w = 0; w < docs.Length; w++)
        {
            string[] part = tokens[windows[w].From..windows[w].To];
            docs[w] = MakeWindow(_store, part, out firstUnits[w]);
        }

        Head.ProcessBatch(docs);

        var boundaries = new List<TokenBoundary>(n + 8);
        for (int w = 0; w < docs.Length; w++)
        {
            ReadOnlySpan<int> tags = docs[w].GetColumn(TokenColumn.Tag);
            ReadOnlySpan<float> confidence = docs[w].GetConfidences();
            float minCut = _minCutPercent / 100f;
            for (int t = windows[w].From; t < windows[w].To; t++)
            {
                Token token = surface[t];
                int length = token.EndIdx - token.Idx;
                int from = 0;
                if (IsCandidate(tokens[t]))
                {
                    int first = firstUnits[w][t - windows[w].From];
                    for (int i = 1; i < length; i++)
                    {
                        if (tags[first + i] == _beginId && confidence[first + i] >= minCut)
                        {
                            boundaries.Add(new TokenBoundary(token.Idx + from, i - from));
                            from = i;
                        }
                    }
                }

                boundaries.Add(new TokenBoundary(token.Idx + from, length - from));
            }
        }

        return new Doc(strings, text, boundaries.ToArray());
    }

    /// <summary>
    /// Finds the pieces of one multiword token in a document this splitter tokenized: the token at <paramref name="start"/> and the tokens
    /// after it that touch each other (no gap) and consist of letters only.
    /// </summary>
    /// <param name="doc">The tokenized document.</param>
    /// <param name="start">Index of the first piece.</param>
    /// <param name="count">The number of pieces, at least 2, when found.</param>
    /// <returns><see langword="true"/> when the token at <paramref name="start"/> starts a run of at least two touching letter-only tokens whose joint length is at most <see cref="MaxCandidateLength"/>.</returns>
    public static bool TryMatch(Doc doc, int start, out int count)
    {
        ArgumentNullException.ThrowIfNull(doc);
        count = 0;
        if (start < 0 || start + 1 >= doc.Length)
        {
            return false;
        }

        int end = start;
        while (end < doc.Length && IsLetters(doc, end) && (end == start || doc[end - 1].EndIdx == doc[end].Idx) && doc[end].EndIdx - doc[start].Idx <= MaxCandidateLength)
        {
            end++;
        }

        count = end - start;
        return count >= 2;
    }

    private static bool IsLetters(Doc doc, int i)
    {
        Token t = doc[i];
        foreach (char c in doc.Text.AsSpan(t.Idx, t.EndIdx - t.Idx))
        {
            if (!char.IsLetter(c))
            {
                return false;
            }
        }

        return true;
    }
}
