namespace RecursiveAI.OneNlp.Tokenizers.Hf;

/// <summary>
/// First stage of a Hugging Face tokenizer pipeline: rewrites the text (case, accents, Unicode forms, replacements) while keeping the
/// alignment to the original input. Created from the <c>normalizer</c> object of a <c>tokenizer.json</c>, chosen by its <c>type</c>.
/// Implementations are immutable and safe for concurrent use.
/// </summary>
internal interface INormalizer
{
    /// <summary>Rewrites the string in place.</summary>
    /// <param name="text">The text to normalize; its alignment must stay valid.</param>
    void Normalize(NormalizedString text);
}

/// <summary>
/// Second stage: cuts the normalized text into pieces (words, punctuation, byte-mapped chunks). Created from the
/// <c>pre_tokenizer</c> object. Implementations are immutable and safe for concurrent use.
/// </summary>
internal interface IPreTokenizer
{
    /// <summary>Splits and may rewrite the pieces (the byte-level one maps bytes to printable characters).</summary>
    /// <param name="text">The pieces so far.</param>
    void PreTokenize(PreTokenizedString text);
}

/// <summary>One token a model produced for a piece.</summary>
/// <param name="Id">The token ID in the model's vocabulary.</param>
/// <param name="Value">The vocabulary string of the token.</param>
/// <param name="Start">The first UTF-16 unit of the piece the token covers.</param>
/// <param name="End">Just past the last unit of the piece the token covers. A model that cannot say (byte fallback, an unknown token) reports the range it consumed.</param>
internal readonly record struct ModelToken(int Id, string Value, int Start, int End);

/// <summary>
/// Third stage, the subword model of a tokenizer family (WordPiece, BPE, Unigram, Tiktoken-style ranks): turns one piece into tokens.
/// Created from the <c>model</c> object. This is what a family implements. Implementations are immutable after construction and
/// safe for concurrent use.
/// </summary>
internal interface ITokenizerModel
{
    /// <summary>The number of entries in the model's own vocabulary, without added tokens.</summary>
    int VocabularySize { get; }

    /// <summary>
    /// Tokenizes one piece. The piece is never empty. Offsets are UTF-16 units of <paramref name="piece"/>, in text order, and each
    /// token's range lies inside the piece; the composed tokenizer maps them back to the original input through the piece's alignment.
    /// </summary>
    /// <param name="piece">The text of the piece, after normalization and pre-tokenization.</param>
    /// <param name="output">Receives the tokens; the model appends and never clears it.</param>
    void Tokenize(ReadOnlySpan<char> piece, List<ModelToken> output);

    /// <summary>
    /// Like <see cref="Tokenize"/> but appends only the IDs, so a model can skip building the vocabulary strings and ranges. The default
    /// calls <see cref="Tokenize"/>; a model overrides it when that is measurably cheaper.
    /// </summary>
    /// <param name="piece">The text of the piece; never empty.</param>
    /// <param name="output">Receives the IDs.</param>
    void TokenizeIds(ReadOnlySpan<char> piece, IdBuffer output)
    {
        var tokens = new List<ModelToken>();
        Tokenize(piece, tokens);
        foreach (ModelToken t in tokens)
        {
            output.Add(t.Id);
        }
    }

    /// <summary>Looks a token string up in the model vocabulary.</summary>
    /// <param name="token">The token exactly as it appears in the vocabulary.</param>
    /// <returns>The ID, or <see langword="null"/> when it is not in the vocabulary.</returns>
    int? TokenToId(string token);

    /// <summary>Gets the vocabulary string of an ID.</summary>
    /// <param name="id">The ID.</param>
    /// <returns>The token, or <see langword="null"/> when the ID is not in the vocabulary.</returns>
    string? IdToToken(int id);
}

/// <summary>
/// Fourth stage: adds the special tokens of a template (<c>[CLS]</c>, <c>[SEP]</c>, <c>&lt;s&gt;</c>) around one or two encodings and sets
/// their type IDs. Created from the <c>post_processor</c> object. Implementations are immutable and safe for concurrent use.
/// </summary>
internal interface IPostProcessor
{
    /// <summary>The number of special tokens the template adds, so that truncation can leave room for them.</summary>
    /// <param name="isPair">Whether two sequences are being joined.</param>
    int AddedTokenCount(bool isPair);

    /// <summary>Applies the template.</summary>
    /// <param name="encoding">The first sequence, already truncated.</param>
    /// <param name="pair">The second sequence, or <see langword="null"/>.</param>
    /// <param name="addSpecialTokens">Whether the template's special tokens are added. A processor may still change offsets or type IDs when it is <see langword="false"/>.</param>
    /// <returns>The final encoding, one entry per token.</returns>
    TokenEncoding Process(TokenEncoding encoding, TokenEncoding? pair, bool addSpecialTokens);
}

/// <summary>
/// Last stage, used by <c>Decode</c>: turns vocabulary strings back into text, as a chain of steps each mapping the list of
/// strings to another list. Created from the <c>decoder</c> object. Implementations are immutable and safe for concurrent use.
/// </summary>
internal interface ITokenDecoder
{
    /// <summary>Maps the token strings to the strings after this step (a step may fuse, strip or rewrite them).</summary>
    /// <param name="tokens">The strings from the step before.</param>
    /// <returns>The strings for the step after; the final strings are concatenated.</returns>
    List<string> DecodeChain(List<string> tokens);
}
