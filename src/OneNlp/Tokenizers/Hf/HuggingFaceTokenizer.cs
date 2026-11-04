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
            while (keepFirst + keepSecond > room)
            {
                if (keepFirst >= keepSecond)
                {
                    keepFirst--;
                }
                else
                {
                    keepSecond--;
                }
            }

            first = Keep(first, keepFirst, truncation.Side);
            second = Keep(second, keepSecond, truncation.Side);
        }

        return _postProcessor is null ? Concatenate(first, second) : _postProcessor.Process(first, second, addSpecialTokens);
    }

    private TokenEncoding EncodeSequence(string text, int typeId)
    {
        var normalized = new NormalizedString(text);
        _normalizer?.Normalize(normalized);
        var pieces = new PreTokenizedString(normalized);
        _preTokenizer?.PreTokenize(pieces);

        var ids = new List<int>();
        var tokens = new List<string>();
        var offsets = new List<TokenOffset>();
        var words = new List<int>();
        var modelTokens = new List<ModelToken>();
        for (int w = 0; w < pieces.Splits.Count; w++)
        {
            NormalizedString piece = pieces.Splits[w];
            modelTokens.Clear();
            _model.Tokenize(piece.Text, modelTokens);
            foreach (ModelToken t in modelTokens)
            {
                OffsetPair o = piece.ToOriginal(t.Start, t.End);
                ids.Add(t.Id);
                tokens.Add(t.Value);
                offsets.Add(new TokenOffset(o.Start, o.End));
                words.Add(w);
            }
        }

        int n = ids.Count;
        var typeIds = new int[n];
        Array.Fill(typeIds, typeId);
        return new TokenEncoding([.. ids], [.. tokens], [.. offsets], new bool[n], typeIds, wordIds: [.. words]);
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
