using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RecursiveAI.OneNlp.Parity;

/// <summary>
/// Digests used for records too long to store element by element. The Python driver computes the same values (see docs/parity.md):
/// SHA-256 over int32 little-endian values, or over each string as an int32 little-endian UTF-8 length plus its bytes.
/// </summary>
public static class Digests
{
    /// <summary>The digest of an int sequence.</summary>
    public static (int N, string Sha256) OfInts(ReadOnlySpan<int> values)
    {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> buf = stackalloc byte[4];
        foreach (int v in values)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(buf, v);
            h.AppendData(buf);
        }

        return (values.Length, Convert.ToHexStringLower(h.GetHashAndReset()));
    }

    /// <summary>The digest of a string sequence.</summary>
    public static (int N, string Sha256) OfStrings(IReadOnlyList<string> values)
    {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> len = stackalloc byte[4];
        foreach (string s in values)
        {
            byte[] b = Encoding.UTF8.GetBytes(s);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(len, b.Length);
            h.AppendData(len);
            h.AppendData(b);
        }

        return (values.Count, Convert.ToHexStringLower(h.GetHashAndReset()));
    }

    /// <summary>The digest of a text: its length in code points and the SHA-256 of its UTF-8 bytes.</summary>
    public static (int N, string Sha256) OfText(string s)
    {
        int n = 0;
        foreach (Rune _ in s.EnumerateRunes())
        {
            n++;
        }

        return (n, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(s))));
    }

    /// <summary>True when the golden stored this field as a digest object rather than an array or string.</summary>
    public static bool IsDigest(JsonElement e) => e.ValueKind == JsonValueKind.Object;

    /// <summary>Reads a digest object.</summary>
    public static (int N, string Sha256) Read(JsonElement e) => (e.GetProperty("n").GetInt32(), e.GetProperty("sha256").GetString()!);
}
