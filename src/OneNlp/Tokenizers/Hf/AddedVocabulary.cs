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

/// <summary>The added tokens of a tokenizer: lookups by ID and by content.</summary>
internal sealed class AddedVocabulary
{
    private readonly Dictionary<int, AddedToken> _byId = [];
    private readonly Dictionary<string, AddedToken> _byContent = new(StringComparer.Ordinal);

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
            if (!_byId.TryAdd(t.Id, t) || !_byContent.TryAdd(t.Content, t))
            {
                throw new InvalidDataException($"The added token '{t.Content}' (ID {t.Id}) repeats an ID or a content.");
            }
        }
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
}
