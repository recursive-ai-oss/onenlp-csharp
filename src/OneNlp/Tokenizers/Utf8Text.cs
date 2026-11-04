using System.Text;

namespace RecursiveAI.OneNlp.Tokenizers;

/// <summary>Reading UTF-8 input as UTF-16 for the families, and mapping their offsets back to bytes.</summary>
internal static class Utf8Text
{
    /// <summary>
    /// Decodes UTF-8 to a string. Each invalid sequence becomes U+FFFD, as <see cref="Rune.DecodeFromUtf8"/> reports it.
    /// </summary>
    /// <param name="utf8">The bytes.</param>
    /// <param name="byteOffsetOfChar">
    /// <c>text.Length + 1</c> entries: the byte offset where each UTF-16 code unit's character starts, with the second unit of a
    /// surrogate pair mapped to the same offset as the first, and the last entry the byte length.
    /// </param>
    internal static string ToString(ReadOnlySpan<byte> utf8, out int[] byteOffsetOfChar)
    {
        var chars = new StringBuilder(utf8.Length);
        var offsets = new List<int>(utf8.Length + 1);
        int i = 0;
        while (i < utf8.Length)
        {
            Rune.DecodeFromUtf8(utf8[i..], out Rune rune, out int width);
            chars.Append(rune);
            offsets.Add(i);
            if (!rune.IsBmp)
            {
                offsets.Add(i);
            }

            i += width;
        }

        offsets.Add(utf8.Length);
        byteOffsetOfChar = [.. offsets];
        return chars.ToString();
    }

    /// <summary>Returns an encoding whose offsets are bytes instead of UTF-16 units; the other columns are shared.</summary>
    internal static TokenEncoding WithByteOffsets(TokenEncoding encoding, int[] byteOffsetOfChar)
    {
        var offsets = new TokenOffset[encoding.Length];
        for (int i = 0; i < offsets.Length; i++)
        {
            TokenOffset o = encoding.Offsets[i];
            offsets[i] = new TokenOffset(byteOffsetOfChar[o.Start], byteOffsetOfChar[o.End]);
        }

        return new TokenEncoding(
            encoding.Ids, encoding.Tokens, offsets, encoding.SpecialTokensMask, encoding.TypeIds, encoding.AttentionMask, encoding.WordIds);
    }
}
