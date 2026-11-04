namespace RecursiveAI.OneNlp.Tokenizers.WordLevel;

/// <summary>
/// The WordLevel model: each piece is looked up whole in the vocabulary; a piece that is not there becomes the unknown token. It does no
/// subword splitting, so all the cutting is done by the pre-tokenizer.
/// </summary>
/// <remarks>Immutable after construction; safe for concurrent use.</remarks>
internal sealed class WordLevelModel : Hf.ITokenizerModel
{
    private readonly TokenIdTable _vocab;
    private readonly string _unkToken;
    private readonly int? _unkId;

    /// <summary>Creates the model.</summary>
    /// <param name="vocab">The vocabulary.</param>
    /// <param name="unkToken">The token for pieces that are not in the vocabulary.</param>
    internal WordLevelModel(TokenIdTable vocab, string unkToken)
    {
        ArgumentNullException.ThrowIfNull(vocab);
        ArgumentNullException.ThrowIfNull(unkToken);
        _vocab = vocab;
        _unkToken = unkToken;
        _unkId = vocab.GetId(unkToken);
    }

    /// <inheritdoc/>
    public int VocabularySize => _vocab.Count;

    /// <inheritdoc/>
    public int? TokenToId(string token) => _vocab.GetId(token);

    /// <inheritdoc/>
    public string? IdToToken(int id) => _vocab.GetToken(id);

    /// <inheritdoc/>
    /// <exception cref="InvalidDataException">The piece is not in the vocabulary and the vocabulary has no unknown token.</exception>
    public void Tokenize(ReadOnlySpan<char> piece, List<Hf.ModelToken> output)
    {
        if (_vocab.TryGetId(piece, out int id))
        {
            output.Add(new Hf.ModelToken(id, _vocab.GetToken(id)!, 0, piece.Length));
            return;
        }

        if (_unkId is not { } unk)
        {
            throw new InvalidDataException($"The unknown token '{_unkToken}' is not in the vocabulary, and a word needs it.");
        }

        output.Add(new Hf.ModelToken(unk, _unkToken, 0, piece.Length));
    }
}
