using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using RecursiveAI.OneNlp.Tokenizers.Loaders;
using RecursiveAI.OneNlp.Tokenizers.Tiktoken;

namespace RecursiveAI.OneNlp.Tokenizers;

/// <summary>
/// A Tiktoken tokenizer: the text is cut into pre-tokens by a regular expression, each pre-token's UTF-8 bytes are byte-pair merged by
/// rank (see the Tiktoken file format on <see cref="Create(Stream, Regex, IReadOnlyDictionary{string, int}?)"/>), and the ranks are the token IDs.
/// Create one with <see cref="Cl100kBase(string)"/> and its siblings from the encoding file you already have; the library never
/// downloads anything.
/// </summary>
/// <remarks>
/// <para>
/// <b>Special tokens.</b> <c>Encode(text, addSpecialTokens: true)</c> (the default) reads each special token's text in the input as
/// that token, which is <c>allowed_special="all"</c> in the Python library; <c>addSpecialTokens: false</c> encodes the same text as
/// ordinary text (<c>encode_ordinary</c>). Tokens read from the text are marked in the special-token mask.
/// </para>
/// <para>
/// <b>Offsets</b> are UTF-16 units into the input (bytes for UTF-8 input). A token whose bytes start or end inside a multi-byte
/// character reports the whole character, so neighbouring tokens may share one.
/// </para>
/// <para>
/// <b>Token strings.</b> <see cref="IdToToken"/> gives a token's bytes as UTF-8 text when they are valid UTF-8; otherwise each byte is
/// written as <c>&lt;0xNN&gt;</c>. Use <see cref="DecodeBytes"/> for the exact bytes.
/// </para>
/// <para>Truncation is applied after encoding; padding needs a pad ID of your choosing. Instances are immutable apart from the base class settings and safe for concurrent use.</para>
/// </remarks>
public sealed class TiktokenTokenizer : Tokenizer
{
    private readonly TiktokenRanks _ranks;
    private readonly Regex _split;
    private readonly bool _mapAstral;
    private readonly string[] _specialNames;
    private readonly int[] _specialIds;
    private readonly Dictionary<string, int> _specialByName;
    private readonly Dictionary<int, string> _specialById;
    private readonly string?[] _tokenStrings;
    private readonly int _vocabularySize;

    [ThreadStatic]
    private static byte[]? t_bytes;

    private TiktokenTokenizer(TiktokenRanks ranks, Regex split, IEnumerable<KeyValuePair<string, int>> specials, bool mapAstral = false)
    {
        _ranks = ranks;
        _split = split;
        _mapAstral = mapAstral;
        var names = new List<string>();
        var ids = new List<int>();
        _specialByName = new Dictionary<string, int>(StringComparer.Ordinal);
        _specialById = [];
        int max = ranks.RankLimit - 1;
        foreach ((string name, int id) in specials)
        {
            if (string.IsNullOrEmpty(name) || id < 0 || ranks.BytesOf(id) is not null || !_specialByName.TryAdd(name, id) || !_specialById.TryAdd(id, name))
            {
                throw new ArgumentException($"The special token '{name}' (ID {id}) is empty, repeated, negative or collides with a rank.", nameof(specials));
            }

            names.Add(name);
            ids.Add(id);
            max = Math.Max(max, id);
        }

        _specialNames = [.. names];
        _specialIds = [.. ids];
        _vocabularySize = max + 1;
        _tokenStrings = new string?[ranks.RankLimit];
    }

    /// <summary>The special tokens by text, in the order that breaks ties between overlapping matches.</summary>
    public IReadOnlyDictionary<string, int> SpecialTokens => _specialByName;

    /// <summary>One more than the largest token ID, ranks and special tokens together (<c>n_vocab</c> in the Python library).</summary>
    public override int VocabularySize => _vocabularySize;

    /// <summary>
    /// Creates a tokenizer from a ranks file and the pre-tokenizing pattern and special tokens of a named encoding.
    /// </summary>
    /// <param name="ranks">The <c>.tiktoken</c> file's content.</param>
    /// <param name="encoding">The encoding the file belongs to.</param>
    /// <exception cref="InvalidDataException">The file is malformed.</exception>
    public static TiktokenTokenizer Load(Stream ranks, TiktokenEncoding encoding) => FromRanks(TiktokenRanksFile.Read(ranks), encoding);

    /// <summary>Creates a tokenizer from a ranks file on disk and the pattern and special tokens of a named encoding.</summary>
    /// <param name="path">The <c>.tiktoken</c> file.</param>
    /// <param name="encoding">The encoding the file belongs to.</param>
    /// <exception cref="InvalidDataException">The file is malformed.</exception>
    /// <exception cref="IOException">The file cannot be read.</exception>
    public static TiktokenTokenizer Load(string path, TiktokenEncoding encoding)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        using FileStream stream = File.OpenRead(path);
        return Load(stream, encoding);
    }

    /// <summary>Creates the <c>r50k_base</c> (GPT-2) tokenizer from its ranks file.</summary>
    /// <param name="path">The file, for example <c>r50k_base.tiktoken</c>.</param>
    public static TiktokenTokenizer R50kBase(string path) => Load(path, TiktokenEncoding.R50kBase);

    /// <summary>Creates the <c>p50k_base</c> tokenizer from its ranks file.</summary>
    /// <param name="path">The file, for example <c>p50k_base.tiktoken</c>.</param>
    public static TiktokenTokenizer P50kBase(string path) => Load(path, TiktokenEncoding.P50kBase);

    /// <summary>Creates the <c>cl100k_base</c> tokenizer from its ranks file.</summary>
    /// <param name="path">The file, for example <c>cl100k_base.tiktoken</c>.</param>
    public static TiktokenTokenizer Cl100kBase(string path) => Load(path, TiktokenEncoding.Cl100kBase);

    /// <summary>Creates the <c>o200k_base</c> tokenizer from its ranks file.</summary>
    /// <param name="path">The file, for example <c>o200k_base.tiktoken</c>.</param>
    public static TiktokenTokenizer O200kBase(string path) => Load(path, TiktokenEncoding.O200kBase);

    /// <summary>Creates a tokenizer from a ranks file with your own split pattern and special tokens.</summary>
    /// <param name="ranks">
    /// The <c>.tiktoken</c> file's content: one token per line, its bytes in standard padded base64, a space, and its rank in decimal
    /// (<c>IQ== 0</c> is the byte <c>!</c> at rank 0). All 256 single bytes must be present.
    /// </param>
    /// <param name="splitPattern">
    /// The regular expression whose matches are the pre-tokens, in order, matched against UTF-16 units (a character outside the BMP is
    /// two units, which character classes do not treat as one letter). Text no match covers is dropped, as in the Python library, so
    /// the pattern should cover everything. The .NET engine has no possessive quantifiers; write them as atomic groups.
    /// </param>
    /// <param name="specialTokens">Special token texts and their IDs, which must not be rank IDs; <see langword="null"/> for none. Enumeration order breaks ties between special tokens that start at the same place.</param>
    /// <exception cref="InvalidDataException">The file is malformed.</exception>
    /// <exception cref="ArgumentException">A special token is empty, repeated or collides with a rank.</exception>
    public static TiktokenTokenizer Create(Stream ranks, Regex splitPattern, IReadOnlyDictionary<string, int>? specialTokens = null)
    {
        ArgumentNullException.ThrowIfNull(splitPattern);
        return new TiktokenTokenizer(TiktokenRanksFile.Read(ranks), splitPattern, specialTokens ?? new Dictionary<string, int>());
    }

    internal static TiktokenTokenizer FromRanks(TiktokenRanks ranks, TiktokenEncoding encoding) => encoding switch
    {
        // Special tokens and patterns: observed in the Encoding objects of tiktoken 0.14.0 (`_special_tokens`, `_pat_str`).
        TiktokenEncoding.R50kBase or TiktokenEncoding.P50kBase => new TiktokenTokenizer(ranks, TiktokenPatterns.Gpt2(), [new("<|endoftext|>", 50256)], mapAstral: true),
        TiktokenEncoding.Cl100kBase => new TiktokenTokenizer(
            ranks,
            TiktokenPatterns.Cl100k(),
            [new("<|endoftext|>", 100257), new("<|fim_prefix|>", 100258), new("<|fim_middle|>", 100259), new("<|fim_suffix|>", 100260), new("<|endofprompt|>", 100276)],
            mapAstral: true),
        TiktokenEncoding.O200kBase => new TiktokenTokenizer(ranks, TiktokenPatterns.O200k(), [new("<|endoftext|>", 199999), new("<|endofprompt|>", 200018)], mapAstral: true),
        _ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, "Unknown Tiktoken encoding."),
    };

    /// <inheritdoc/>
    public override int? TokenToId(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        if (_specialByName.TryGetValue(token, out int special))
        {
            return special;
        }

        byte[] bytes = new byte[Encoding.UTF8.GetMaxByteCount(token.Length)];
        if (Utf8.FromUtf16(token, bytes, out _, out int written, replaceInvalidSequences: false) == OperationStatus.Done && written > 0
            && _ranks.RankOf(bytes.AsSpan(0, written)) is { } rank)
        {
            return rank;
        }

        // The `<0xNN>` spelling of a token that is not valid UTF-8.
        if (token.Length >= 6 && token.Length % 6 == 0)
        {
            var raw = new byte[token.Length / 6];
            for (int i = 0; i < raw.Length; i++)
            {
                ReadOnlySpan<char> part = token.AsSpan(i * 6, 6);
                if (part[..3] is not "<0x" || part[5] != '>' || !byte.TryParse(part.Slice(3, 2), NumberStyles.AllowHexSpecifier, null, out raw[i]))
                {
                    return null;
                }
            }

            return _ranks.RankOf(raw);
        }

        return null;
    }

    /// <inheritdoc/>
    public override string? IdToToken(int id)
    {
        if (_specialById.TryGetValue(id, out string? special))
        {
            return special;
        }

        byte[]? bytes = _ranks.BytesOf(id);
        if (bytes is null)
        {
            return null;
        }

        return _tokenStrings[id] ??= Utf8.IsValid(bytes) ? Encoding.UTF8.GetString(bytes) : string.Concat(bytes.Select(b => $"<0x{b:X2}>"));
    }

    /// <summary>Gets the exact bytes of the text for token IDs (<c>decode_bytes</c> in the Python library).</summary>
    /// <param name="ids">The token IDs.</param>
    /// <param name="skipSpecialTokens">Whether special tokens are left out; otherwise their text is written.</param>
    /// <exception cref="ArgumentException">An ID is not a token of this encoding.</exception>
    public byte[] DecodeBytes(ReadOnlySpan<int> ids, bool skipSpecialTokens = false)
    {
        byte[] joined = new byte[JoinedLength(ids, skipSpecialTokens)];
        JoinInto(ids, skipSpecialTokens, joined);
        return joined;
    }

    // The length is summed first so the bytes are written once into a buffer of exactly the right size, not through a growing stream.
    private int JoinedLength(ReadOnlySpan<int> ids, bool skipSpecialTokens)
    {
        long total = 0;
        foreach (int id in ids)
        {
            if (_ranks.BytesOf(id) is { } bytes)
            {
                total += bytes.Length;
            }
            else if (_specialById.TryGetValue(id, out string? special))
            {
                if (!skipSpecialTokens)
                {
                    total += Encoding.UTF8.GetByteCount(special);
                }
            }
            else
            {
                throw new ArgumentException($"The token ID {id} is not in this encoding.", nameof(ids));
            }
        }

        return checked((int)total);
    }

    private void JoinInto(ReadOnlySpan<int> ids, bool skipSpecialTokens, Span<byte> destination)
    {
        int at = 0;
        foreach (int id in ids)
        {
            if (_ranks.BytesOf(id) is { } bytes)
            {
                bytes.CopyTo(destination[at..]);
                at += bytes.Length;
            }
            else if (!skipSpecialTokens && _specialById.TryGetValue(id, out string? special))
            {
                at += Encoding.UTF8.GetBytes(special, destination[at..]);
            }
        }
    }

    /// <inheritdoc/>
    /// <remarks>Invalid UTF-8 in the joined bytes becomes U+FFFD, as <c>errors="replace"</c> does in the Python library. Special tokens are written unless skipped, like the Python library when <paramref name="skipSpecialTokens"/> is <see langword="false"/>.</remarks>
    /// <exception cref="ArgumentException">An ID is not a token of this encoding.</exception>
    public override string Decode(ReadOnlySpan<int> ids, bool skipSpecialTokens = true)
    {
        int length = JoinedLength(ids, skipSpecialTokens);
        byte[] rented = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            JoinInto(ids, skipSpecialTokens, rented);
            return Encoding.UTF8.GetString(rented, 0, length);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <inheritdoc/>
    public override OperationStatus EncodeToIds(ReadOnlySpan<char> text, Span<int> ids, out int written, out int consumed, bool addSpecialTokens = true)
    {
        var sink = new Sink(false);
        EncodeInto(text, addSpecialTokens, sink);
        if (sink.Ids.Count > ids.Length)
        {
            return base.EncodeToIds(text, ids, out written, out consumed, addSpecialTokens);
        }

        for (int i = 0; i < sink.Ids.Count; i++)
        {
            ids[i] = sink.Ids[i];
        }

        written = sink.Ids.Count;
        consumed = text.Length;
        return OperationStatus.Done;
    }

    /// <inheritdoc/>
    public override int CountTokens(ReadOnlySpan<char> text, bool addSpecialTokens = true)
    {
        var sink = new Sink(false);
        EncodeInto(text, addSpecialTokens, sink);
        return sink.Ids.Count;
    }

    /// <inheritdoc/>
    protected override TokenEncoding EncodeCore(ReadOnlySpan<char> text, bool addSpecialTokens, TruncationOptions? truncation)
    {
        var sink = new Sink(true);
        EncodeInto(text, addSpecialTokens, sink);
        int n = sink.Ids.Count;
        int from = 0;
        if (truncation is not null && n > truncation.MaxLength)
        {
            n = truncation.MaxLength;
            from = truncation.Side == TokenizerSide.Right ? 0 : sink.Ids.Count - n;
        }

        var ids = new int[n];
        var tokens = new string[n];
        var offsets = new TokenOffset[n];
        var special = new bool[n];
        var words = new int[n];
        for (int i = 0; i < n; i++)
        {
            ids[i] = sink.Ids[from + i];
            tokens[i] = IdToToken(ids[i]) ?? string.Empty;
            offsets[i] = sink.Offsets[from + i];
            special[i] = sink.Special[from + i];
            words[i] = sink.Words[from + i];
        }

        return new TokenEncoding(ids, tokens, offsets, special, wordIds: words);
    }

    private sealed class Sink(bool detailed)
    {
        public bool Detailed { get; } = detailed;

        public List<int> Ids { get; } = [];

        public List<TokenOffset> Offsets { get; } = [];

        public List<bool> Special { get; } = [];

        public List<int> Words { get; } = [];

        public List<int> Lengths { get; } = [];

        public int Word { get; set; }
    }

    private void EncodeInto(ReadOnlySpan<char> text, bool allowSpecial, Sink sink)
    {
        if (!allowSpecial || _specialNames.Length == 0)
        {
            EncodeOrdinary(text, 0, sink);
            return;
        }

        // Special tokens in the text are found left to right; on a tie the one listed first wins, as with an alternation.
        var found = new int[_specialNames.Length];
        Array.Fill(found, -2);
        int pos = 0;
        while (true)
        {
            int bestAt = -1;
            int best = -1;
            for (int s = 0; s < found.Length; s++)
            {
                if (found[s] == -1)
                {
                    continue;
                }

                if (found[s] < pos)
                {
                    int rel = text[pos..].IndexOf(_specialNames[s], StringComparison.Ordinal);
                    found[s] = rel < 0 ? -1 : pos + rel;
                }

                if (found[s] >= 0 && (best < 0 || found[s] < bestAt))
                {
                    best = s;
                    bestAt = found[s];
                }
            }

            if (best < 0)
            {
                EncodeOrdinary(text[pos..], pos, sink);
                return;
            }

            EncodeOrdinary(text[pos..bestAt], pos, sink);
            int end = bestAt + _specialNames[best].Length;
            sink.Ids.Add(_specialIds[best]);
            if (sink.Detailed)
            {
                sink.Offsets.Add(new TokenOffset(bestAt, end));
                sink.Special.Add(true);
                sink.Words.Add(-1);
            }

            pos = end;
        }
    }

    private void EncodeOrdinary(ReadOnlySpan<char> text, int baseOffset, Sink sink)
    {
        if (_mapAstral && text.IndexOfAnyInRange('\uD800', '\uDFFF') >= 0)
        {
            EncodeOrdinaryByScalarClass(text, baseOffset, sink);
            return;
        }

        foreach (ValueMatch m in _split.EnumerateMatches(text))
        {
            if (m.Length == 0)
            {
                continue;
            }

            EncodePiece(text.Slice(m.Index, m.Length), baseOffset + m.Index, sink);
            sink.Word++;
        }
    }

    // The .NET regex engine reads UTF-16 units, so a character outside the BMP is two units that no letter, number or mark class
    // accepts, and that a negated class takes one at a time. The named patterns only look at the general category of a character, so
    // each such character is replaced by one BMP character of the same category for matching, and the matches are mapped back.
    private void EncodeOrdinaryByScalarClass(ReadOnlySpan<char> text, int baseOffset, Sink sink)
    {
        var mapped = new char[text.Length];
        var origin = new int[text.Length + 1];
        int n = 0;
        for (int i = 0; i < text.Length; n++)
        {
            origin[n] = i;
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                mapped[n] = CategoryRepresentative(Rune.GetUnicodeCategory(new Rune(text[i], text[i + 1])));
                i += 2;
            }
            else
            {
                mapped[n] = text[i];
                i++;
            }
        }

        origin[n] = text.Length;
        foreach (ValueMatch m in _split.EnumerateMatches(mapped.AsSpan(0, n)))
        {
            if (m.Length == 0)
            {
                continue;
            }

            int start = origin[m.Index];
            EncodePiece(text[start..origin[m.Index + m.Length]], baseOffset + start, sink);
            sink.Word++;
        }
    }

    // One BMP character for each general category the patterns tell apart: letters by case class, marks and numbers; anything else is
    // "neither letter, number nor space", for which '!' stands.
    internal static char CategoryRepresentative(UnicodeCategory category) => category switch
    {
        UnicodeCategory.UppercaseLetter => 'A',
        UnicodeCategory.LowercaseLetter => 'a',
        UnicodeCategory.TitlecaseLetter => '\u01C5',
        UnicodeCategory.ModifierLetter => '\u02B0',
        UnicodeCategory.OtherLetter => '\u05D0',
        UnicodeCategory.NonSpacingMark => '\u0301',
        UnicodeCategory.SpacingCombiningMark => '\u0903',
        UnicodeCategory.EnclosingMark => '\u20DD',
        UnicodeCategory.DecimalDigitNumber => '0',
        UnicodeCategory.LetterNumber => '\u2160',
        UnicodeCategory.OtherNumber => '\u00B2',
        _ => '!',
    };

    private void EncodePiece(ReadOnlySpan<char> piece, int offset, Sink sink)
    {
        byte[] buffer = t_bytes ??= new byte[512];
        int max = Encoding.UTF8.GetMaxByteCount(piece.Length);
        if (buffer.Length < max)
        {
            buffer = t_bytes = new byte[Math.Max(max, buffer.Length * 2)];
        }

        int n = Encoding.UTF8.GetBytes(piece, buffer);
        int first = sink.Ids.Count;
        sink.Lengths.Clear();
        _ranks.EncodePiece(buffer.AsSpan(0, n), sink.Ids, sink.Detailed ? sink.Lengths : null);
        if (!sink.Detailed)
        {
            return;
        }

        int count = sink.Ids.Count - first;
        if (n == piece.Length)
        {
            // All ASCII: a byte is a character.
            int at = offset;
            for (int i = 0; i < count; i++)
            {
                sink.Offsets.Add(new TokenOffset(at, at + sink.Lengths[i]));
                at += sink.Lengths[i];
            }
        }
        else
        {
            // For every byte, the character it belongs to; a token that cuts a character reports the whole character.
            var charStart = new int[n];
            var charEnd = new int[n];
            int b = 0;
            int c = 0;
            while (c < piece.Length)
            {
                int len = char.IsHighSurrogate(piece[c]) && c + 1 < piece.Length && char.IsLowSurrogate(piece[c + 1]) ? 2 : 1;
                int bytes = Encoding.UTF8.GetByteCount(piece.Slice(c, len));
                for (int k = 0; k < bytes; k++)
                {
                    charStart[b + k] = offset + c;
                    charEnd[b + k] = offset + c + len;
                }

                b += bytes;
                c += len;
            }

            int at = 0;
            for (int i = 0; i < count; i++)
            {
                int len = sink.Lengths[i];
                sink.Offsets.Add(new TokenOffset(charStart[at], charEnd[at + len - 1]));
                at += len;
            }
        }

        for (int i = 0; i < count; i++)
        {
            sink.Special.Add(false);
            sink.Words.Add(sink.Word);
        }
    }
}
