namespace RecursiveAI.OneNlp.Tokenizers.Hf;

/// <summary>
/// A tokenizer composed the way a Hugging Face <c>tokenizer.json</c> describes it: normalizer, then pre-tokenizer, then a model, then
/// a post-processor, with a decoder for the way back and added tokens beside the model's vocabulary. It is what
/// <see cref="Tokenizer.FromFile(string)"/> returns for that format; the model decides the family.
/// </summary>
/// <remarks>Immutable after construction apart from the base class settings; safe for concurrent encoding and decoding.</remarks>
internal sealed class HuggingFaceTokenizer : Tokenizer
{
    private readonly INormalizer? _normalizer;
    private readonly IPreTokenizer? _preTokenizer;
    private readonly ITokenizerModel _model;
    private readonly IPostProcessor? _postProcessor;
    private readonly ITokenDecoder? _decoder;
    private readonly AddedVocabulary _added;
    private readonly int _addedOutsideModel;

    /// <summary>Composes the stages. Only the model is required.</summary>
    /// <param name="model">The subword model.</param>
    /// <param name="normalizer">The normalizer, or <see langword="null"/> for none.</param>
    /// <param name="preTokenizer">The pre-tokenizer, or <see langword="null"/> to give the model the whole text as one piece.</param>
    /// <param name="postProcessor">The post-processor, or <see langword="null"/> to add no special tokens.</param>
    /// <param name="decoder">The decoder, or <see langword="null"/> to join tokens with a space.</param>
    /// <param name="added">The added tokens, or <see langword="null"/> for none.</param>
    internal HuggingFaceTokenizer(
        ITokenizerModel model,
        INormalizer? normalizer = null,
        IPreTokenizer? preTokenizer = null,
        IPostProcessor? postProcessor = null,
        ITokenDecoder? decoder = null,
        AddedVocabulary? added = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
        _normalizer = normalizer;
        _preTokenizer = preTokenizer;
        _postProcessor = postProcessor;
        _decoder = decoder;
        _added = added ?? AddedVocabulary.Empty;
        if (_added.Count > 0)
        {
            _added.Prepare(_normalizer);
        }

        _addedOutsideModel = _added.Tokens.Count(t => _model.TokenToId(t.Content) is null);
    }

    /// <summary>The model's vocabulary plus the added tokens that the model does not already contain.</summary>
    public override int VocabularySize => _model.VocabularySize + _addedOutsideModel;

    /// <summary>The model.</summary>
    internal ITokenizerModel Model => _model;

    /// <summary>The added tokens.</summary>
    internal AddedVocabulary AddedTokens => _added;

    /// <inheritdoc/>
    public override int? TokenToId(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return _added.TryGetByContent(token, out AddedToken? a) ? a.Id : _model.TokenToId(token);
    }

    /// <inheritdoc/>
    public override string? IdToToken(int id) => _added.TryGetById(id, out AddedToken? a) ? a.Content : _model.IdToToken(id);

    /// <inheritdoc/>
    public override string Decode(ReadOnlySpan<int> ids, bool skipSpecialTokens = true)
    {
        var tokens = new List<string>(ids.Length);
        foreach (int id in ids)
        {
            if (skipSpecialTokens && _added.TryGetById(id, out AddedToken? a) && a.Special)
            {
                continue;
            }

            if (IdToToken(id) is { } token)
            {
                tokens.Add(token);
            }
        }

        if (_decoder is null)
        {
            return string.Join(' ', tokens);
        }

        return string.Concat(_decoder.DecodeChain(tokens));
    }

    /// <inheritdoc/>
    protected override TokenEncoding EncodeCore(ReadOnlySpan<char> text, bool addSpecialTokens, TruncationOptions? truncation)
    {
        TokenEncoding first = EncodeSequence(text.ToString(), typeId: 0);
        int added = _postProcessor is null || !addSpecialTokens ? 0 : _postProcessor.AddedTokenCount(isPair: false);
        first = Truncate(first, truncation, added);
        return _postProcessor is null ? first : _postProcessor.Process(first, null, addSpecialTokens);
    }

    /// <inheritdoc/>
    protected override TokenEncoding EncodePairCore(ReadOnlySpan<char> text, ReadOnlySpan<char> pair, bool addSpecialTokens, TruncationOptions? truncation)
    {
        TokenEncoding first = EncodeSequence(text.ToString(), typeId: 0);
        TokenEncoding second = EncodeSequence(pair.ToString(), typeId: 1);
        if (truncation is not null)
        {
            // Longest-first: shave one token at a time off the longer sequence until both fit with the template's tokens.
            int added = _postProcessor is null || !addSpecialTokens ? 0 : _postProcessor.AddedTokenCount(isPair: true);
            int room = Math.Max(0, truncation.MaxLength - added);
            int keepFirst = first.Length;
            int keepSecond = second.Length;
            switch (truncation.Strategy)
            {
                case TruncationStrategy.OnlyFirst:
                    keepFirst = Math.Min(keepFirst, Math.Max(0, room - keepSecond));
                    break;
                case TruncationStrategy.OnlySecond:
                    keepSecond = Math.Min(keepSecond, Math.Max(0, room - keepFirst));
                    break;
                default:
                    // Longest-first removes one token at a time from the longer sequence. When both are equally long, it takes the one it did not
                    // take from last, starting with the first (observed in output of tokenizers 0.22.1). Done in closed form: cut the longer
                    // down to the shorter, then alternate.
                    int excess = keepFirst + keepSecond - room;
                    if (excess > 0)
                    {
                        int cut = Math.Min(Math.Abs(keepFirst - keepSecond), excess);
                        bool lastWasFirst = keepFirst > keepSecond && cut > 0;
                        if (keepFirst > keepSecond)
                        {
                            keepFirst -= cut;
                        }
                        else
                        {
                            keepSecond -= cut;
                        }

                        excess -= cut;
                        int takeFromTieFirst = lastWasFirst ? excess / 2 : (excess + 1) / 2;
                        keepFirst = Math.Max(0, keepFirst - takeFromTieFirst);
                        keepSecond = Math.Max(0, keepSecond - (excess - takeFromTieFirst));
                    }

                    break;
            }

            first = Keep(first, keepFirst, truncation.Side);
            second = Keep(second, keepSecond, truncation.Side);
        }

        return _postProcessor is null ? Concatenate(first, second) : _postProcessor.Process(first, second, addSpecialTokens);
    }

    private TokenEncoding EncodeSequence(string text, int typeId)
    {
        var ids = new List<int>();
        var tokens = new List<string>();
        var offsets = new List<TokenOffset>();
        var words = new List<int>();
        var special = new List<bool>();
        var modelTokens = new List<ModelToken>();
        int word = 0;
        foreach (InputPiece input in SplitInput(text))
        {
            if (input.Token is { } added)
            {
                ids.Add(added.Id);
                tokens.Add(input.Matched ?? added.Content);  // the matched text, stripped whitespace included (observed in output of tokenizers 0.22.1)
                offsets.Add(new TokenOffset(input.Origin.Start, input.Origin.End));
                words.Add(word++);
                special.Add(false);  // the mask marks tokens a post-processor adds, not added tokens found in the input (observed in output of tokenizers 0.22.1)
                continue;
            }

            var pieces = new PreTokenizedString(input.Text!);
            _preTokenizer?.PreTokenize(pieces);
            foreach (NormalizedString piece in pieces.Splits)
            {
                modelTokens.Clear();
                _model.Tokenize(piece.Text, modelTokens);
                foreach (ModelToken t in modelTokens)
                {
                    OffsetPair o = piece.ToOriginal(t.Start, t.End);
                    ids.Add(t.Id);
                    tokens.Add(t.Value);
                    offsets.Add(new TokenOffset(o.Start, o.End));
                    words.Add(word);
                    special.Add(false);
                }

                word++;
            }
        }

        int n = ids.Count;
        var typeIds = new int[n];
        Array.Fill(typeIds, typeId);
        return new TokenEncoding([.. ids], [.. tokens], [.. offsets], [.. special], typeIds, wordIds: [.. words]);
    }

    // Added tokens are cut out first; the text between them is then normalized piece by piece.
    private List<InputPiece> SplitInput(string text)
    {
        if (_added.Count > 0)
        {
            return _added.Split(text, _normalizer);
        }

        var whole = new NormalizedString(text);
        _normalizer?.Normalize(whole);
        return whole.Length == 0 ? [] : [new InputPiece(whole, null, default)];
    }

    private static TokenEncoding Truncate(TokenEncoding encoding, TruncationOptions? truncation, int addedTokens) =>
        truncation is null ? encoding : Keep(encoding, Math.Max(0, truncation.MaxLength - addedTokens), truncation.Side);

    private static TokenEncoding Keep(TokenEncoding e, int keep, TokenizerSide side)
    {
        if (e.Length <= keep)
        {
            return e;
        }

        int from = side == TokenizerSide.Right ? 0 : e.Length - keep;
        return new TokenEncoding(
            e.Ids[from..(from + keep)],
            e.Tokens[from..(from + keep)],
            e.Offsets[from..(from + keep)],
            e.SpecialTokensMask[from..(from + keep)],
            e.TypeIds[from..(from + keep)],
            e.AttentionMask[from..(from + keep)],
            e.WordIds[from..(from + keep)]);
    }

    private static TokenEncoding Concatenate(TokenEncoding a, TokenEncoding b) => new(
        [.. a.Ids, .. b.Ids],
        [.. a.Tokens, .. b.Tokens],
        [.. a.Offsets, .. b.Offsets],
        [.. a.SpecialTokensMask, .. b.SpecialTokensMask],
        [.. a.TypeIds, .. b.TypeIds],
        [.. a.AttentionMask, .. b.AttentionMask],
        [.. a.WordIds, .. b.WordIds]);
}
