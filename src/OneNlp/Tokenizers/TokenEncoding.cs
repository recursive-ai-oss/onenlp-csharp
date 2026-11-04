namespace RecursiveAI.OneNlp.Tokenizers;

/// <summary>A half-open range <c>[Start, End)</c> of offsets into the input a tokenizer was given.</summary>
/// <remarks>
/// For text passed as UTF-16 the offsets count UTF-16 code units of that string; for text passed as UTF-8 they count bytes of that
/// span. They are never offsets into a normalized copy. Tokens that stand for no input text (added special tokens, padding) have
/// the empty range <c>(0, 0)</c>.
/// </remarks>
/// <param name="Start">The offset of the first unit.</param>
/// <param name="End">The offset just past the last unit.</param>
public readonly record struct TokenOffset(int Start, int End);

/// <summary>Which end of a sequence an operation works on.</summary>
public enum TokenizerSide
{
    /// <summary>The start of the sequence.</summary>
    Left,

    /// <summary>The end of the sequence.</summary>
    Right,
}

/// <summary>Truncation of an encoding to a token budget, applied by the tokenizer family while it encodes.</summary>
/// <param name="MaxLength">The most tokens an encoding may hold, special tokens included; at least 1.</param>
/// <param name="Side">The side tokens are removed from.</param>
public sealed record TruncationOptions(int MaxLength, TokenizerSide Side = TokenizerSide.Right);

/// <summary>Padding of an encoding, applied by <see cref="Tokenizer"/> after the family has encoded.</summary>
/// <param name="PadId">The ID of the padding token.</param>
/// <param name="PadToken">The padding token's text.</param>
/// <param name="Length">Pad every encoding up to this many tokens, or <see langword="null"/> to pad to the longest of a batch.</param>
/// <param name="PadToMultipleOf">Round the padded length up to a multiple of this, or <see langword="null"/> for no rounding.</param>
/// <param name="Side">The side the padding is added on.</param>
/// <param name="PadTypeId">The type ID given to padding tokens.</param>
public sealed record PaddingOptions(
    int PadId,
    string PadToken,
    int? Length = null,
    int? PadToMultipleOf = null,
    TokenizerSide Side = TokenizerSide.Right,
    int PadTypeId = 0);

/// <summary>
/// The result of encoding one text (or text pair): parallel arrays with one entry per token, the fields Hugging Face's <c>Encoding</c>
/// documents. Instances do not change after they are created.
/// </summary>
public sealed class TokenEncoding
{
    /// <summary>An encoding with no tokens.</summary>
    public static TokenEncoding Empty { get; } = new([], [], [], []);

    /// <summary>Creates an encoding. The arrays are kept, not copied, and must not be changed afterwards.</summary>
    /// <param name="ids">The token IDs.</param>
    /// <param name="tokens">The token strings as they appear in the vocabulary.</param>
    /// <param name="offsets">Where each token came from in the input; see <see cref="TokenOffset"/>.</param>
    /// <param name="specialTokensMask">Whether each token is a special token (added by the post-processor, padding, or matched as special in the input).</param>
    /// <param name="typeIds">The segment ID of each token; <see langword="null"/> for all 0.</param>
    /// <param name="attentionMask">1 for real tokens and 0 for padding; <see langword="null"/> for all 1.</param>
    /// <param name="wordIds">The index of the pre-tokenized word each token came from, or -1 for tokens that belong to none; <see langword="null"/> for all -1.</param>
    /// <exception cref="ArgumentException">The arrays differ in length.</exception>
    public TokenEncoding(
        int[] ids,
        string[] tokens,
        TokenOffset[] offsets,
        bool[] specialTokensMask,
        int[]? typeIds = null,
        int[]? attentionMask = null,
        int[]? wordIds = null)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(offsets);
        ArgumentNullException.ThrowIfNull(specialTokensMask);
        int n = ids.Length;
        if (tokens.Length != n || offsets.Length != n || specialTokensMask.Length != n
            || (typeIds is not null && typeIds.Length != n) || (attentionMask is not null && attentionMask.Length != n)
            || (wordIds is not null && wordIds.Length != n))
        {
            throw new ArgumentException("Every array of an encoding has one entry per token.");
        }

        Ids = ids;
        Tokens = tokens;
        Offsets = offsets;
        SpecialTokensMask = specialTokensMask;
        TypeIds = typeIds ?? new int[n];
        AttentionMask = attentionMask ?? CreateFilled(n, 1);
        WordIds = wordIds ?? CreateFilled(n, -1);
    }

    /// <summary>The number of tokens.</summary>
    public int Length => Ids.Length;

    /// <summary>The token IDs.</summary>
    public int[] Ids { get; }

    /// <summary>The token strings.</summary>
    public string[] Tokens { get; }

    /// <summary>Where each token came from in the input.</summary>
    public TokenOffset[] Offsets { get; }

    /// <summary>Whether each token is a special token.</summary>
    public bool[] SpecialTokensMask { get; }

    /// <summary>The segment ID of each token: 0 for the first text, 1 for the second of a pair.</summary>
    public int[] TypeIds { get; }

    /// <summary>1 for real tokens and 0 for padding.</summary>
    public int[] AttentionMask { get; }

    /// <summary>The pre-tokenized word each token came from, or -1.</summary>
    public int[] WordIds { get; }

    /// <summary>Returns this encoding padded to <paramref name="length"/> tokens (it is returned as is when already that long or longer).</summary>
    /// <param name="padding">The padding token and side.</param>
    /// <param name="length">The target length.</param>
    internal TokenEncoding PaddedTo(PaddingOptions padding, int length)
    {
        int extra = length - Length;
        if (extra <= 0)
        {
            return this;
        }

        int at = padding.Side == TokenizerSide.Left ? 0 : Length;
        return new TokenEncoding(
            Insert(Ids, at, extra, padding.PadId),
            Insert(Tokens, at, extra, padding.PadToken),
            Insert(Offsets, at, extra, default),
            Insert(SpecialTokensMask, at, extra, true),
            Insert(TypeIds, at, extra, padding.PadTypeId),
            Insert(AttentionMask, at, extra, 0),
            Insert(WordIds, at, extra, -1));
    }

    private static int[] CreateFilled(int n, int value)
    {
        var a = new int[n];
        Array.Fill(a, value);
        return a;
    }

    private static T[] Insert<T>(T[] source, int at, int count, T value)
    {
        var result = new T[source.Length + count];
        Array.Copy(source, 0, result, at == 0 ? count : 0, source.Length);
        Array.Fill(result, value, at, count);
        return result;
    }
}
