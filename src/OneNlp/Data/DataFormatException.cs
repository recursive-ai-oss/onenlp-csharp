using System.Text;

namespace RecursiveAI.OneNlp.Data;

/// <summary>A training-data file (CoNLL-U or JSONL) is malformed. The message names the line.</summary>
public sealed class DataFormatException : FormatException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="lineNumber">The 1-based number of the offending line.</param>
    /// <param name="message">What is wrong, without the line number.</param>
    public DataFormatException(int lineNumber, string message)
        : base($"line {lineNumber}: {message}") => LineNumber = lineNumber;

    /// <summary>The 1-based number of the offending line.</summary>
    public int LineNumber { get; }
}

/// <summary>
/// Reads lines from a <see cref="TextReader"/> with a hard length limit, so a hostile file without line breaks cannot make the reader
/// allocate without bound. Lines end at LF or CR LF; a lone CR is not a line end (CoNLL-U and JSON Lines both use LF).
/// </summary>
internal sealed class BoundedLineReader
{
    private readonly TextReader _reader;
    private readonly int _maxLength;
    private readonly char[] _buffer = new char[8192];
    private readonly StringBuilder _line = new();
    private int _position;
    private int _count;

    public BoundedLineReader(TextReader reader, int maxLength)
    {
        _reader = reader;
        _maxLength = maxLength;
    }

    /// <summary>The number of the line most recently returned, or the one being read when an error is thrown.</summary>
    public int LineNumber { get; private set; }

    /// <summary>Reads the next line without its terminator, or <see langword="null"/> at the end of the input.</summary>
    /// <exception cref="DataFormatException">The line is longer than the limit, or the input is not valid UTF-8.</exception>
    public string? ReadLine()
    {
        LineNumber++;
        _line.Clear();
        bool any = false;
        while (true)
        {
            if (_position == _count)
            {
                try
                {
                    _count = _reader.Read(_buffer, 0, _buffer.Length);
                }
                catch (DecoderFallbackException)
                {
                    throw new DataFormatException(LineNumber, "the input is not valid UTF-8");
                }

                _position = 0;
                if (_count == 0)
                {
                    if (!any)
                    {
                        LineNumber--;
                        return null;
                    }

                    return Finish();
                }
            }

            int start = _position;
            int newline = Array.IndexOf(_buffer, '\n', start, _count - start);
            int end = newline < 0 ? _count : newline;
            if (_line.Length + (end - start) > _maxLength)
            {
                throw new DataFormatException(LineNumber, $"the line is longer than the limit of {_maxLength} characters");
            }

            _line.Append(_buffer, start, end - start);
            any = true;
            if (newline >= 0)
            {
                _position = newline + 1;
                return Finish();
            }

            _position = _count;
        }
    }

    private string Finish()
    {
        if (_line.Length > 0 && _line[^1] == '\r')
        {
            _line.Length--;
        }

        return _line.ToString();
    }
}
