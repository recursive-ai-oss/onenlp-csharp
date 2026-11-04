using System.Buffers.Binary;
using System.Text;

namespace RecursiveAI.OneNlp.Tokenizers.Hf;

/// <summary>
/// The normalizer of SentencePiece models, read from the <c>precompiled_charsmap</c> data of a <c>tokenizer.json</c>. The data is a
/// 32-bit little-endian byte count, then that many bytes of a double-array trie (4-byte units) that maps UTF-8 prefixes to offsets, then
/// a pool of NUL-terminated UTF-8 replacement strings. At each position the longest prefix found in the trie is replaced by the pool
/// string at its offset; if there is none, one character is copied unchanged.
/// </summary>
internal sealed class PrecompiledNormalizer : INormalizer
{
    private const int MaxBytes = 64 * 1024 * 1024;

    private readonly uint[] _units;
    private readonly byte[] _pool;

    internal PrecompiledNormalizer(string base64)
    {
        byte[] data;
        try
        {
            data = Convert.FromBase64String(base64);
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException("The precompiled_charsmap is not valid base64.", ex);
        }

        if (data.Length < 4 || data.Length > MaxBytes)
        {
            throw new InvalidDataException("The precompiled_charsmap has an unusable size.");
        }

        uint trieBytes = BinaryPrimitives.ReadUInt32LittleEndian(data);
        if (trieBytes % 4 != 0 || trieBytes > data.Length - 4)
        {
            throw new InvalidDataException("The precompiled_charsmap trie size does not fit the data.");
        }

        _units = new uint[trieBytes / 4];
        for (int i = 0; i < _units.Length; i++)
        {
            _units[i] = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4 + (i * 4)));
        }

        _pool = data[(4 + (int)trieBytes)..];
    }

    public void Normalize(NormalizedString text)
    {
        string s = text.Text;
        if (s.Length == 0)
        {
            return;
        }

        // UTF-8 bytes of the text, and for each code point the UTF-16 index and byte index where it starts.
        var bytes = new List<byte>(s.Length * 2);
        var starts16 = new List<int>(s.Length);
        var startsByte = new List<int>(s.Length);
        var tmp = new byte[4];
        for (int i = 0; i < s.Length;)
        {
            int cp = NormalizerHelpers.CodePointAt(s, i, out int w);
            starts16.Add(i);
            startsByte.Add(bytes.Count);
            int n = new Rune(Rune.IsValid(cp) ? cp : 0xFFFD).EncodeToUtf8(tmp);
            for (int k = 0; k < n; k++)
            {
                bytes.Add(tmp[k]);
            }

            i += w;
        }

        starts16.Add(s.Length);
        startsByte.Add(bytes.Count);
        byte[] input = [.. bytes];

        var b = new NormalizedStringBuilder();
        int index = 0;
        while (index < starts16.Count - 1)
        {
            int at = startsByte[index];
            int matchLength = LongestPrefix(input, at, out int poolOffset);
            if (matchLength == 0)
            {
                for (int k = starts16[index]; k < starts16[index + 1]; k++)
                {
                    b.AppendFrom(text, k);
                }

                index++;
                continue;
            }

            // Consume whole code points covering the matched bytes.
            int last = index;
            while (last < starts16.Count - 1 && startsByte[last + 1] <= at + matchLength)
            {
                last++;
            }

            if (poolOffset >= _pool.Length)
            {
                throw new InvalidDataException("The precompiled_charsmap points outside its replacement pool.");
            }

            int end = poolOffset;
            while (end < _pool.Length && _pool[end] != 0)
            {
                end++;
            }

            string replacement = Encoding.UTF8.GetString(_pool, poolOffset, end - poolOffset);
            b.Append(replacement, text.ToOriginal(starts16[index], starts16[last]));
            index = last;
        }

        b.CopyTo(text);
    }

    // Double-array trie traversal: the unit layout (offset, label, leaf flag, value) is the one the darts-clone format defines.
    private int LongestPrefix(byte[] key, int from, out int poolOffset)
    {
        poolOffset = 0;
        int best = 0;
        uint unit = _units.Length > 0 ? _units[0] : 0;
        long node = Offset(unit);
        for (int i = from; i < key.Length; i++)
        {
            node ^= key[i];
            if ((ulong)node >= (ulong)_units.Length)
            {
                break;
            }

            unit = _units[node];
            if ((unit & 0x800000FFu) != key[i])
            {
                break;
            }

            node ^= Offset(unit);
            if (((unit >> 8) & 1) != 0 && (ulong)node < (ulong)_units.Length)
            {
                best = i + 1 - from;
                poolOffset = (int)(_units[node] & 0x7FFFFFFFu);
            }
        }

        return best;
    }

    private static long Offset(uint unit) => (unit >> 10) << (int)((unit & (1u << 9)) >> 6);
}
