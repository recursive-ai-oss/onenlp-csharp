namespace RecursiveAI.OneNlp.Models.Embeddings.Multilingual;

/// <summary>
/// A read-only, seekable stream over several streams read one after the other, so the three weight shards are presented to the model
/// loader as the single ONNX file they were cut from, without a temporary file or a joined copy.
/// </summary>
internal sealed class ConcatenatedStream : Stream
{
    private readonly Stream[] _parts;
    private readonly long[] _starts;
    private readonly long _length;
    private long _position;

    /// <param name="parts">Readable, seekable streams, in order. Disposed with this stream.</param>
    public ConcatenatedStream(IReadOnlyList<Stream> parts)
    {
        _parts = [.. parts];
        _starts = new long[_parts.Length];
        long total = 0;
        for (int i = 0; i < _parts.Length; i++)
        {
            _starts[i] = total;
            total += _parts[i].Length;
        }

        _length = total;
    }

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override long Length => _length;

    public override long Position
    {
        get => _position;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _position = value;
        }
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        int done = 0;
        while (done < buffer.Length && _position < _length)
        {
            // The part holding _position: the last one that starts at or before it.
            int p = _parts.Length - 1;
            while (_starts[p] > _position)
            {
                p--;
            }

            Stream part = _parts[p];
            part.Position = _position - _starts[p];
            int n = part.Read(buffer[done..]);
            if (n <= 0)
            {
                break;
            }

            done += n;
            _position += n;
        }

        return done;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        long target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };
        ArgumentOutOfRangeException.ThrowIfNegative(target, nameof(offset));
        _position = target;
        return target;
    }

    public override void Flush()
    {
    }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (Stream s in _parts)
            {
                s.Dispose();
            }
        }

        base.Dispose(disposing);
    }
}
