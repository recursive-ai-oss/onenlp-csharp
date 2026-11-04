using System.Text;

namespace RecursiveAI.OneNlp.Serialization;

/// <summary>Growable little-endian output buffer for the binary format (docs/binary-format.md).</summary>
internal sealed class ByteBuilder
{
    private byte[] _buffer = new byte[256];
    private int _length;

    public int Length => _length;

    public byte[] Buffer => _buffer;

    public ReadOnlySpan<byte> AsSpan() => _buffer.AsSpan(0, _length);

    private void Ensure(int extra)
    {
        if (_buffer.Length - _length >= extra)
        {
            return;
        }

        long needed = (long)_length + extra;
        if (needed > Array.MaxLength)
        {
            throw new InvalidOperationException("The document is too large to serialize.");
        }

        Array.Resize(ref _buffer, (int)Math.Min(Array.MaxLength, Math.Max(needed, (long)_buffer.Length * 2)));
    }

    public void Byte(byte value)
    {
        Ensure(1);
        _buffer[_length++] = value;
    }

    public void U16(ushort value)
    {
        Ensure(2);
        _buffer[_length++] = (byte)value;
        _buffer[_length++] = (byte)(value >> 8);
    }

    public void U32(uint value)
    {
        Ensure(4);
        _buffer[_length++] = (byte)value;
        _buffer[_length++] = (byte)(value >> 8);
        _buffer[_length++] = (byte)(value >> 16);
        _buffer[_length++] = (byte)(value >> 24);
    }

    public void Float(float value) => U32(BitConverter.SingleToUInt32Bits(value));

    /// <summary>LEB128: seven bits per byte, low bits first, high bit set while more follow. At most 5 bytes for a 32-bit value.</summary>
    public void Varint(uint value)
    {
        Ensure(5);
        while (value >= 0x80)
        {
            _buffer[_length++] = (byte)(value | 0x80);
            value >>= 7;
        }

        _buffer[_length++] = (byte)value;
    }

    /// <summary>Zig-zag then LEB128, so small negative numbers stay small.</summary>
    public void SignedVarint(int value) => Varint((uint)((value << 1) ^ (value >> 31)));

    public void Bytes(ReadOnlySpan<byte> data)
    {
        Ensure(data.Length);
        data.CopyTo(_buffer.AsSpan(_length));
        _length += data.Length;
    }

    /// <summary>Starts a section: its id and a length placeholder. Returns the position to give <see cref="EndSection"/>.</summary>
    public int BeginSection(byte id)
    {
        Byte(id);
        int at = _length;
        U32(0);
        return at;
    }

    public void EndSection(int at)
    {
        uint size = (uint)(_length - at - 4);
        _buffer[at] = (byte)size;
        _buffer[at + 1] = (byte)(size >> 8);
        _buffer[at + 2] = (byte)(size >> 16);
        _buffer[at + 3] = (byte)(size >> 24);
    }

    /// <summary>A string as a varint of <c>byteCount * 2 + isUtf16</c> followed by its bytes: UTF-8 when well-formed, else raw UTF-16LE units.</summary>
    public void String(string value)
    {
        if (TextUtil.IsWellFormed(value))
        {
            int bytes = Encoding.UTF8.GetByteCount(value);
            Varint(((uint)bytes << 1) | 0u);
            Ensure(bytes);
            _length += Encoding.UTF8.GetBytes(value, _buffer.AsSpan(_length));
        }
        else
        {
            Varint(((uint)value.Length * 2 << 1) | 1u);
            Ensure(value.Length * 2);
            foreach (char c in value)
            {
                _buffer[_length++] = (byte)c;
                _buffer[_length++] = (byte)(c >> 8);
            }
        }
    }
}

/// <summary>Reads the binary format from memory. Every read checks the bytes that remain and throws <see cref="InvalidDataException"/>.</summary>
internal ref struct ByteCursor
{
    private readonly ReadOnlySpan<byte> _data;
    private int _position;

    public ByteCursor(ReadOnlySpan<byte> data)
    {
        _data = data;
        _position = 0;
    }

    public readonly int Remaining => _data.Length - _position;

    public byte Byte()
    {
        if (_position >= _data.Length)
        {
            throw Truncated();
        }

        return _data[_position++];
    }

    public ushort U16() => (ushort)(Byte() | (Byte() << 8));

    public uint U32()
    {
        uint value = Byte();
        value |= (uint)Byte() << 8;
        value |= (uint)Byte() << 16;
        value |= (uint)Byte() << 24;
        return value;
    }

    public float Float() => BitConverter.UInt32BitsToSingle(U32());

    /// <summary>Reads a LEB128 value that must fit in 32 bits and use at most 5 bytes.</summary>
    public uint Varint()
    {
        uint value = 0;
        for (int shift = 0; shift < 35; shift += 7)
        {
            byte b = Byte();
            if (shift == 28 && b > 0x0F)
            {
                throw new InvalidDataException("A variable-length integer does not fit in 32 bits.");
            }

            value |= (uint)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
            {
                return value;
            }
        }

        throw new InvalidDataException("A variable-length integer is longer than 5 bytes.");
    }

    public long SignedVarint()
    {
        uint v = Varint();
        return (long)(v >> 1) ^ -(long)(v & 1);
    }

    public ReadOnlySpan<byte> Take(long count)
    {
        if (count < 0 || count > Remaining)
        {
            throw Truncated();
        }

        ReadOnlySpan<byte> slice = _data.Slice(_position, (int)count);
        _position += (int)count;
        return slice;
    }

    /// <summary>Reads a string written by <see cref="ByteBuilder.String"/>.</summary>
    public string String()
    {
        uint head = Varint();
        bool utf16 = (head & 1) != 0;
        ReadOnlySpan<byte> bytes = Take(head >> 1);
        return TextUtil.Decode(bytes, utf16);
    }

    private static InvalidDataException Truncated() => new("The data ends before a length or count it declares.");
}

internal static class TextUtil
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>True when the string has no unpaired surrogate, so UTF-8 can carry it unchanged.</summary>
    public static bool IsWellFormed(string value)
    {
        ReadOnlySpan<char> s = value;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (!char.IsSurrogate(c))
            {
                continue;
            }

            if (char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
            {
                i++;
                continue;
            }

            return false;
        }

        return true;
    }

    public static string Decode(ReadOnlySpan<byte> bytes, bool utf16)
    {
        if (utf16)
        {
            if ((bytes.Length & 1) != 0)
            {
                throw new InvalidDataException("A UTF-16 string has an odd byte count.");
            }

            return string.Create(bytes.Length / 2, bytes.ToArray(), static (chars, b) =>
            {
                for (int i = 0; i < chars.Length; i++)
                {
                    chars[i] = (char)(b[2 * i] | (b[(2 * i) + 1] << 8));
                }
            });
        }

        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException e)
        {
            throw new InvalidDataException("A string is not valid UTF-8.", e);
        }
    }
}

/// <summary>CRC-32 (IEEE 802.3, reflected, polynomial 0xEDB88320), as used by zip and PNG.</summary>
internal static class Crc32
{
    private static readonly uint[] Table = Build();

    private static uint[] Build()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }

    /// <summary>Continues a running checksum; start from 0.</summary>
    public static uint Update(uint crc, ReadOnlySpan<byte> data)
    {
        uint c = ~crc;
        foreach (byte b in data)
        {
            c = Table[(c ^ b) & 0xFF] ^ (c >> 8);
        }

        return ~c;
    }
}
