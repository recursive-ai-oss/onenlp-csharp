namespace RecursiveAI.OneNlp.Tokenizers.Hf;

/// <summary>
/// A token added on top of a model's vocabulary, with the flags the <c>added_tokens</c> list of a <c>tokenizer.json</c> documents.
/// </summary>
/// <param name="Id">The token's ID.</param>
/// <param name="Content">The token's text, matched in the input and used as its vocabulary string.</param>
/// <param name="Special">Whether it is a special token: reported in the special-token mask and left out when decoding with special tokens skipped.</param>
/// <param name="SingleWord">Whether it only matches as a whole word, not inside a longer word.</param>
/// <param name="LeftStrip">Whether whitespace to its left in the input is consumed with it.</param>
/// <param name="RightStrip">Whether whitespace to its right in the input is consumed with it.</param>
/// <param name="Normalized">Whether it is matched against the normalized text instead of the original text.</param>
internal sealed record AddedToken(int Id, string Content, bool Special, bool SingleWord, bool LeftStrip, bool RightStrip, bool Normalized);

/// <summary>One piece of the input after added tokens were cut out: either an added token or text still to be pre-tokenized.</summary>
/// <param name="Text">The normalized text, or <see langword="null"/> for an added token.</param>
/// <param name="Token">The added token, or <see langword="null"/> for text.</param>
/// <param name="Origin">For an added token, the range of the original input it covers (whitespace its flags stripped included).</param>
internal readonly record struct InputPiece(NormalizedString? Text, AddedToken? Token, OffsetPair Origin);

/// <summary>The added tokens of a tokenizer: lookups by ID and by content, and the pass that cuts them out of the input.</summary>
internal sealed class AddedVocabulary
{
    private readonly Dictionary<int, AddedToken> _byId = [];
    private readonly Dictionary<string, AddedToken> _byContent = new(StringComparer.Ordinal);
    private Matcher _original = new([]);
    private Matcher _normalized = new([]);

    /// <summary>No added tokens.</summary>
    internal static AddedVocabulary Empty { get; } = new([]);

    /// <summary>Creates the vocabulary.</summary>
    /// <param name="tokens">The tokens; IDs and contents must each be unique.</param>
    /// <exception cref="InvalidDataException">Two tokens share an ID or a content.</exception>
    internal AddedVocabulary(IEnumerable<AddedToken> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        foreach (AddedToken t in tokens)
        {
            if (t.Content.Length == 0 || !_byId.TryAdd(t.Id, t) || !_byContent.TryAdd(t.Content, t))
            {
                throw new InvalidDataException($"The added token '{t.Content}' (ID {t.Id}) is empty or repeats an ID or a content.");
            }
        }

        Prepare(null);
    }

    /// <summary>The number of added tokens.</summary>
    internal int Count => _byId.Count;

    /// <summary>All added tokens, in no particular order.</summary>
    internal IEnumerable<AddedToken> Tokens => _byId.Values;

    /// <summary>Finds an added token by ID.</summary>
    /// <param name="id">The ID.</param>
    /// <param name="token">Receives the token.</param>
    internal bool TryGetById(int id, out AddedToken token) => _byId.TryGetValue(id, out token!);

    /// <summary>Finds an added token by content.</summary>
    /// <param name="content">The exact content.</param>
    /// <param name="token">Receives the token.</param>
    internal bool TryGetByContent(string content, out AddedToken token) => _byContent.TryGetValue(content, out token!);

    /// <summary>
    /// Builds the two matchers: tokens flagged as not normalized are looked for in the original text, the others in the normalized text,
    /// where their content is normalized the same way first so that, for example, a lowercasing normalizer still finds them.
    /// </summary>
    /// <param name="normalizer">The tokenizer's normalizer, or <see langword="null"/>.</param>
    internal void Prepare(INormalizer? normalizer)
    {
        _original = new Matcher([.. _byId.Values.Where(t => !t.Normalized).Select(t => (t.Content, t))]);
        var normalized = new List<(string, AddedToken)>();
        foreach (AddedToken t in _byId.Values.Where(t => t.Normalized))
        {
            var s = new NormalizedString(t.Content);
            normalizer?.Normalize(s);
            if (s.Length > 0)
            {
                normalized.Add((s.Text, t));
            }
        }

        _normalized = new Matcher(normalized);
    }

    /// <summary>Cuts added tokens out of an input and normalizes the text between them.</summary>
    /// <param name="input">The caller's input.</param>
    /// <param name="normalizer">The normalizer, applied to each piece of text that is not an added token.</param>
    internal List<InputPiece> Split(string input, INormalizer? normalizer)
    {
        var result = new List<InputPiece>();
        foreach ((int start, int end, AddedToken? token) in _original.Cut(input))
        {
            if (token is not null)
            {
                result.Add(new InputPiece(null, token, new OffsetPair(start, end)));
                continue;
            }

            var piece = new NormalizedString(input[start..end], start);
            normalizer?.Normalize(piece);
            foreach ((int s, int e, AddedToken? t) in _normalized.Cut(piece.Text))
            {
                if (t is not null)
                {
                    result.Add(new InputPiece(null, t, piece.ToOriginal(s, e)));
                }
                else if (e > s)
                {
                    result.Add(new InputPiece(piece.Slice(s, e), null, default));
                }
            }
        }

        return result;
    }

    private sealed class Matcher
    {
        private readonly Dictionary<char, List<(string Content, AddedToken Token)>> _byFirst = [];

        internal Matcher(IEnumerable<(string Content, AddedToken Token)> tokens)
        {
            foreach ((string content, AddedToken token) in tokens)
            {
                if (!_byFirst.TryGetValue(content[0], out List<(string, AddedToken)>? list))
                {
                    _byFirst[content[0]] = list = [];
                }

                list.Add((content, token));
            }

            foreach (List<(string Content, AddedToken Token)> list in _byFirst.Values)
            {
                list.Sort((a, b) => b.Content.Length.CompareTo(a.Content.Length));
            }
        }

        /// <summary>Splits a text into stretches without added tokens (<c>Token</c> null) and the added tokens, leftmost and longest first.</summary>
        internal List<(int Start, int End, AddedToken? Token)> Cut(string text)
        {
            var parts = new List<(int, int, AddedToken?)>();
            if (_byFirst.Count == 0)
            {
                parts.Add((0, text.Length, null));
                return parts;
            }

            int textStart = 0;
            int i = 0;
            while (i < text.Length)
            {
                (int length, AddedToken? token) = MatchAt(text, i);
                if (token is null)
                {
                    i++;
                    continue;
                }

                int start = i;
                int end = i + length;
                if (token.LeftStrip)
                {
                    while (start > textStart && CodePointRegex.IsWhiteSpace(text[start - 1]))
                    {
                        start--;
                    }
                }

                if (token.RightStrip)
                {
                    while (end < text.Length && CodePointRegex.IsWhiteSpace(text[end]))
                    {
                        end++;
                    }
                }

                if (start > textStart)
                {
                    parts.Add((textStart, start, null));
                }

                parts.Add((start, end, token));
                textStart = end;
                i = end;
            }

            if (textStart < text.Length)
            {
                parts.Add((textStart, text.Length, null));
            }

            return parts;
        }

        private (int Length, AddedToken? Token) MatchAt(string text, int i)
        {
            if (!_byFirst.TryGetValue(text[i], out List<(string Content, AddedToken Token)>? candidates))
            {
                return (0, null);
            }

            foreach ((string content, AddedToken token) in candidates)
            {
                if (string.CompareOrdinal(text, i, content, 0, content.Length) != 0)
                {
                    continue;
                }

                if (token.SingleWord)
                {
                    // A single-word token must not touch a word character on either side.
                    bool startOk = i == 0 || !IsWordUnit(text, i - 1);
                    bool endOk = i + content.Length == text.Length || !IsWordUnit(text, i + content.Length);
                    if (!startOk || !endOk)
                    {
                        continue;
                    }
                }

                return (content.Length, token);
            }

            return (0, null);
        }

        private static bool IsWordUnit(string text, int index)
        {
            int cp = NormalizerHelpers.CodePointAt(text, index, out _);
            if (char.IsLowSurrogate(text[index]) && index > 0)
            {
                cp = NormalizerHelpers.CodePointAt(text, index - 1, out _);
            }

            return CodePointRegex.IsWord(cp);
        }
    }
}
