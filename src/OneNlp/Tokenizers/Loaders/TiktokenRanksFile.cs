using System.Buffers;
using System.Buffers.Text;
using RecursiveAI.OneNlp.Tokenizers.Tiktoken;

namespace RecursiveAI.OneNlp.Tokenizers.Loaders;

/// <summary>
/// Reads a <c>.tiktoken</c> ranks file.
/// </summary>
/// <remarks>
/// The file is text, one token per line: the token's bytes in standard padded base64, one space, and the token's rank in decimal,
/// for example <c>IQ== 0</c> for the byte <c>!</c> at rank 0. Lines end in <c>\n</c> (a <c>\r</c> before it is tolerated) and the last
/// line may lack the newline. Blank lines are not allowed. The rank is the token's ID and its merge priority (lower merges first).
/// The reader refuses files over 64 MiB, lines over 8 KiB, more than 4 million lines, and any malformed line; it never allocates from a
/// length it has not checked.
/// </remarks>
internal static class TiktokenRanksFile
{
    private const long MaxFileBytes = 64L << 20;
    private const int MaxLineBytes = 8192;
    private const int MaxLines = 4_000_000;
    private const int MaxRank = 100_000_000;

    /// <summary>Loads a tokenizer from the stream, choosing the encoding from the file name or, without one, from the largest rank.</summary>
    /// <param name="stream">The file's content.</param>
    /// <param name="fileNameHint">The file name without directory, or <see langword="null"/>.</param>
    /// <exception cref="InvalidDataException">The content is malformed, or the encoding cannot be told: use <see cref="TiktokenTokenizer.Create"/> with a pattern.</exception>
    internal static Tokenizer Load(Stream stream, string? fileNameHint = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        TiktokenRanks ranks = Read(stream);
        string stem = Path.GetFileNameWithoutExtension(fileNameHint ?? string.Empty);
        TiktokenEncoding? encoding = stem.ToUpperInvariant() switch
        {
            "R50K_BASE" or "GPT2" => TiktokenEncoding.R50kBase,
            "P50K_BASE" => TiktokenEncoding.P50kBase,
            "CL100K_BASE" => TiktokenEncoding.Cl100kBase,
            "O200K_BASE" => TiktokenEncoding.O200kBase,
            _ => ranks.RankLimit switch
            {
                50256 => TiktokenEncoding.R50kBase,
                50281 => TiktokenEncoding.P50kBase,
                100256 => TiktokenEncoding.Cl100kBase,
                199998 => TiktokenEncoding.O200kBase,
                _ => null,
            },
        };

        return encoding is { } e
            ? TiktokenTokenizer.FromRanks(ranks, e)
            : throw new InvalidDataException("The encoding of this Tiktoken file cannot be told from its name or size; use TiktokenTokenizer.Create with the encoding's split pattern.");
    }

    /// <summary>Parses the ranks.</summary>
    /// <param name="stream">The file's content.</param>
    /// <exception cref="InvalidDataException">The content is malformed or over a limit.</exception>
    internal static TiktokenRanks Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        byte[] data = ReadAll(stream);
        var tokens = new List<(byte[], int)>();
        byte[] scratch = new byte[(MaxLineBytes / 4 * 3) + 3];
        int pos = 0;
        while (pos < data.Length)
        {
            int eol = data.AsSpan(pos).IndexOf((byte)'\n');
            int lineEnd = eol < 0 ? data.Length : pos + eol;
            ReadOnlySpan<byte> line = data.AsSpan(pos, lineEnd - pos);
            pos = lineEnd + 1;
            if (!line.IsEmpty && line[^1] == (byte)'\r')
            {
                line = line[..^1];
            }

            if (line.Length > MaxLineBytes)
            {
                throw new InvalidDataException("A line of the Tiktoken file is too long.");
            }

            if (tokens.Count >= MaxLines)
            {
                throw new InvalidDataException("The Tiktoken file has too many lines.");
            }

            int space = line.IndexOf((byte)' ');
            if (space <= 0 || space == line.Length - 1)
            {
                throw new InvalidDataException($"Line {tokens.Count + 1} of the Tiktoken file is not '<base64> <rank>'.");
            }

            OperationStatus status = Base64.DecodeFromUtf8(line[..space], scratch, out int consumed, out int written);
            if (status != OperationStatus.Done || consumed != space || written == 0)
            {
                throw new InvalidDataException($"Line {tokens.Count + 1} of the Tiktoken file has invalid base64.");
            }

            tokens.Add((scratch.AsSpan(0, written).ToArray(), ParseRank(line[(space + 1)..], tokens.Count + 1)));
        }

        return new TiktokenRanks(tokens);
    }

    private static int ParseRank(ReadOnlySpan<byte> digits, int lineNumber)
    {
        long value = 0;
        if (digits.Length > 9)
        {
            throw new InvalidDataException($"Line {lineNumber} of the Tiktoken file has a rank that is too large.");
        }

        foreach (byte d in digits)
        {
            if (d is < (byte)'0' or > (byte)'9')
            {
                throw new InvalidDataException($"Line {lineNumber} of the Tiktoken file has a rank that is not a decimal number.");
            }

            value = (value * 10) + (d - '0');
        }

        return value <= MaxRank ? (int)value : throw new InvalidDataException($"Line {lineNumber} of the Tiktoken file has a rank that is too large.");
    }

    private static byte[] ReadAll(Stream stream)
    {
        using var buffer = new MemoryStream();
        byte[] chunk = new byte[81920];
        int read;
        while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (buffer.Length + read > MaxFileBytes)
            {
                throw new InvalidDataException("The Tiktoken file is too large.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
