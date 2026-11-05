using System.Buffers;
using System.Text;
using RecursiveAI.OneNlp.Tokenizers.Bpe;
using RecursiveAI.OneNlp.Tokenizers.Tiktoken;

namespace RecursiveAI.OneNlp.Tokenizers.Hf;

/// <summary>
/// Encodes straight to token IDs for the pipeline shapes that dominate in practice, without the alignment, string and offset
/// bookkeeping that a full encoding needs. It must give exactly the IDs the general path gives; the parity run compares both with
/// the reference library, and a shape it does not recognise (or a text it cannot take) goes to the general path.
/// </summary>
internal abstract class IdsFastPath
{
    /// <summary>Appends the IDs of a text that holds no added token and no unpaired surrogate.</summary>
    /// <param name="text">The text.</param>
    /// <param name="ids">Receives the IDs.</param>
    /// <param name="general">The tokenizer, for the stretches the fast path leaves to the general path.</param>
    internal abstract void Encode(ReadOnlySpan<char> text, IdBuffer ids, HuggingFaceTokenizer general);

    // The Unigram path relies on printable ASCII passing through the normalizer unchanged; check that on the real normalizer.
    private static bool AsciiPassesThrough(PrecompiledNormalizer normalizer)
    {
        for (char c = ' '; c <= '~'; c++)
        {
            var one = new NormalizedString(c.ToString());
            normalizer.Normalize(one);
            if (one.Text.Length != 1 || one.Text[0] != c)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Recognises the pipeline shape, or returns <see langword="null"/>.</summary>
    internal static IdsFastPath? TryCreate(ITokenizerModel model, INormalizer? normalizer, IPreTokenizer? preTokenizer)
    {
        if (model is BpeModel bpe)
        {
            if (normalizer is null && preTokenizer is ByteLevelPreTokenizer { AddPrefixSpace: false } byteLevel)
            {
                return new ByteLevelBpePath(bpe, byteLevel.UseRegex);
            }

            if (preTokenizer is null && normalizer is SequenceNormalizer { Steps: [PrependNormalizer prepend, ReplaceNormalizer { IsRegex: false, Pattern: " " } replace] })
            {
                return new MetaspaceBpePath(bpe, prepend.Prefix, replace.Content);
            }
        }

        if (model is Unigram.UnigramModel
            && normalizer is PrecompiledNormalizer precompiled
            && preTokenizer is SequencePreTokenizer { Steps: [PredicateSplitPreTokenizer { Behavior: SplitBehavior.Removed } whitespace, MetaspacePreTokenizer { Scheme: "always", Splits: true } metaspace] }
            && whitespace.Predicate == CodePointRegex.IsWhiteSpace
            && AsciiPassesThrough(precompiled))
        {
            return new MetaspaceUnigramPath(model, metaspace.Mark);
        }

        if (normalizer is BertNormalizer bert && preTokenizer is BertPreTokenizer)
        {
            return new BertPath(model, bert.Lowercase);
        }

        return null;
    }
}

/// <summary>
/// SentencePiece Unigram as XLM-RoBERTa's <c>tokenizer.json</c> writes it: the character map (which leaves printable ASCII alone),
/// a split at whitespace, a marker in front of every word, and the Unigram model per word. Text with anything but printable ASCII
/// goes to the general path.
/// </summary>
internal sealed class MetaspaceUnigramPath(ITokenizerModel model, char marker) : IdsFastPath
{
    internal override void Encode(ReadOnlySpan<char> text, IdBuffer ids, HuggingFaceTokenizer general)
    {
        if (text.IsEmpty)
        {
            return;
        }

        if (text.IndexOfAnyExceptInRange(' ', '~') >= 0)
        {
            general.AppendGeneralIds(text.ToString(), ids);
            return;
        }

        char[] piece = ArrayPool<char>.Shared.Rent(text.Length + 2);
        try
        {
            piece[0] = marker;
            int i = 0;
            while (i < text.Length)
            {
                while (i < text.Length && text[i] == ' ')
                {
                    i++;
                }

                int end = i;
                while (end < text.Length && text[end] != ' ')
                {
                    end++;
                }

                if (end > i)
                {
                    text[i..end].CopyTo(piece.AsSpan(1));
                    model.TokenizeIds(piece.AsSpan(0, 1 + end - i), ids);
                }

                i = end;
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(piece);
        }
    }
}

/// <summary>Byte-level BPE (GPT-2, RoBERTa): GPT-2 pattern split, bytes mapped to printable characters, BPE per piece.</summary>
internal sealed class ByteLevelBpePath(BpeModel model, bool useRegex) : IdsFastPath
{
    [ThreadStatic]
    private static byte[]? t_bytes;

    [ThreadStatic]
    private static char[]? t_chars;

    internal override void Encode(ReadOnlySpan<char> text, IdBuffer ids, HuggingFaceTokenizer general)
    {
        if (text.IsEmpty)
        {
            return;
        }

        if (!useRegex)
        {
            EncodePiece(text, ids);
            return;
        }

        if (text.IndexOfAnyInRange('\uD800', '\uDFFF') >= 0)
        {
            EncodeByScalarClass(text, ids);
            return;
        }

        foreach (System.Text.RegularExpressions.ValueMatch m in TiktokenPatterns.Gpt2().EnumerateMatches(text))
        {
            if (m.Length > 0)
            {
                EncodePiece(text.Slice(m.Index, m.Length), ids);
            }
        }
    }

    // The .NET regex engine reads UTF-16 units, so a character outside the BMP is matched by one BMP character of its category (the
    // same device the Tiktoken tokenizer uses) and the matches are mapped back to the text.
    private void EncodeByScalarClass(ReadOnlySpan<char> text, IdBuffer ids)
    {
        char[] mapped = ArrayPool<char>.Shared.Rent(text.Length);
        int[] origin = ArrayPool<int>.Shared.Rent(text.Length + 1);
        try
        {
            int n = 0;
            for (int i = 0; i < text.Length; n++)
            {
                origin[n] = i;
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    mapped[n] = TiktokenTokenizer.CategoryRepresentative(Rune.GetUnicodeCategory(new Rune(text[i], text[i + 1])));
                    i += 2;
                }
                else
                {
                    mapped[n] = text[i];
                    i++;
                }
            }

            origin[n] = text.Length;
            foreach (System.Text.RegularExpressions.ValueMatch m in TiktokenPatterns.Gpt2().EnumerateMatches(mapped.AsSpan(0, n)))
            {
                if (m.Length > 0)
                {
                    EncodePiece(text[origin[m.Index]..origin[m.Index + m.Length]], ids);
                }
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(mapped);
            ArrayPool<int>.Shared.Return(origin);
        }
    }

    private void EncodePiece(ReadOnlySpan<char> piece, IdBuffer ids)
    {
        int max = Encoding.UTF8.GetMaxByteCount(piece.Length);
        byte[] bytes = t_bytes is { } b && b.Length >= max ? b : t_bytes = new byte[Math.Max(max, 1024)];
        char[] chars = t_chars is { } c && c.Length >= max ? c : t_chars = new char[Math.Max(max, 1024)];
        int n = Encoding.UTF8.GetBytes(piece, bytes);
        for (int i = 0; i < n; i++)
        {
            chars[i] = ByteLevelTable.ToChar(bytes[i]);
        }

        model.TokenizeIds(chars.AsSpan(0, n), ids);
    }
}

/// <summary>
/// SentencePiece-style BPE as Llama's <c>tokenizer.json</c> writes it: a prefix in front of the text, every space replaced by the
/// marker, and the whole text given to the model as one piece.
/// </summary>
internal sealed class MetaspaceBpePath(BpeModel model, string prefix, string marker) : IdsFastPath
{
    internal override void Encode(ReadOnlySpan<char> text, IdBuffer ids, HuggingFaceTokenizer general)
    {
        if (text.IsEmpty)
        {
            return; // the prefix is only put in front of text that exists
        }

        int length = prefix.Length + (text.Length * Math.Max(1, marker.Length));
        char[] buffer = ArrayPool<char>.Shared.Rent(length);
        try
        {
            prefix.CopyTo(buffer);
            int n = prefix.Length;
            foreach (char c in text)
            {
                if (c == ' ')
                {
                    marker.CopyTo(0, buffer, n, marker.Length);
                    n += marker.Length;
                }
                else
                {
                    buffer[n++] = c;
                }
            }

            model.TokenizeIds(buffer.AsSpan(0, n), ids);
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }
}

/// <summary>
/// BERT: cleaning, accent stripping, lowercasing, whitespace and punctuation splitting, then WordPiece. The text is cut at ASCII
/// whitespace, which no stage of the pipeline looks across; a stretch of printable ASCII is handled directly, anything else (controls,
/// accents, other scripts) goes through the general path on its own.
/// </summary>
internal sealed class BertPath(ITokenizerModel model, bool lowercase) : IdsFastPath
{
    internal override void Encode(ReadOnlySpan<char> text, IdBuffer ids, HuggingFaceTokenizer general)
    {
        int start = 0;
        for (int i = 0; i <= text.Length; i++)
        {
            if (i < text.Length && text[i] is not (' ' or '\t' or '\n' or '\r'))
            {
                continue;
            }

            if (i > start)
            {
                Chunk(text[start..i], ids, general);
            }

            start = i + 1;
        }
    }

    private void Chunk(ReadOnlySpan<char> chunk, IdBuffer ids, HuggingFaceTokenizer general)
    {
        if (chunk.IndexOfAnyExceptInRange('!', '~') >= 0)
        {
            general.AppendGeneralIds(chunk.ToString(), ids);
            return;
        }

        Span<char> word = stackalloc char[64]; // a word longer than this is handled by the model from a rented buffer
        char[]? rented = null;
        try
        {
            int i = 0;
            while (i < chunk.Length)
            {
                char c = chunk[i];
                if (!char.IsAsciiLetterOrDigit(c))
                {
                    // ASCII punctuation and symbols are words of their own.
                    word[0] = c;
                    model.TokenizeIds(word[..1], ids);
                    i++;
                    continue;
                }

                int end = i + 1;
                while (end < chunk.Length && char.IsAsciiLetterOrDigit(chunk[end]))
                {
                    end++;
                }

                int length = end - i;
                Span<char> buffer = word;
                if (length > word.Length)
                {
                    rented = ArrayPool<char>.Shared.Rent(length);
                    buffer = rented;
                }

                ReadOnlySpan<char> source = chunk[i..end];
                if (lowercase)
                {
                    for (int k = 0; k < length; k++)
                    {
                        buffer[k] = char.IsAsciiLetterUpper(source[k]) ? (char)(source[k] | 0x20) : source[k];
                    }
                }
                else
                {
                    source.CopyTo(buffer);
                }

                model.TokenizeIds(buffer[..length], ids);
                if (rented is not null)
                {
                    ArrayPool<char>.Shared.Return(rented);
                    rented = null;
                }

                i = end;
            }
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }
}
