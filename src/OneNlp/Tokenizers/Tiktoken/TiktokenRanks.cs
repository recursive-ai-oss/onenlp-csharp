using System.Buffers.Binary;

namespace RecursiveAI.OneNlp.Tokenizers.Tiktoken;

/// <summary>
/// Compares byte sequences by content, and lets a <see cref="ReadOnlySpan{T}"/> of bytes look an entry up in a dictionary keyed by
/// <c>byte[]</c> without copying it. The hash reads eight bytes at a time.
/// </summary>
internal sealed class ByteSequenceComparer : IEqualityComparer<byte[]>, IAlternateEqualityComparer<ReadOnlySpan<byte>, byte[]>
{
    /// <summary>The shared instance.</summary>
    internal static ByteSequenceComparer Instance { get; } = new();

    /// <inheritdoc/>
    public bool Equals(byte[]? x, byte[]? y) => x is not null && y is not null && x.AsSpan().SequenceEqual(y);

    /// <inheritdoc/>
    public int GetHashCode(byte[] obj) => GetHashCode(obj.AsSpan());

    /// <inheritdoc/>
    public bool Equals(ReadOnlySpan<byte> alternate, byte[] other) => alternate.SequenceEqual(other);

    /// <inheritdoc/>
    public int GetHashCode(ReadOnlySpan<byte> alternate)
    {
        const ulong Mul = 0x9E3779B97F4A7C15UL;
        ulong h = (ulong)alternate.Length * Mul;
        ReadOnlySpan<byte> rest = alternate;
        while (rest.Length >= 8)
        {
            h = (h ^ BinaryPrimitives.ReadUInt64LittleEndian(rest)) * Mul;
            h ^= h >> 29;
            rest = rest[8..];
        }

        if (!rest.IsEmpty)
        {
            ulong tail = 0;
            for (int i = 0; i < rest.Length; i++)
            {
                tail |= (ulong)rest[i] << (8 * i);
            }

            h = (h ^ tail) * Mul;
            h ^= h >> 29;
        }

        return (int)(h ^ (h >> 32));
    }

    /// <inheritdoc/>
    public byte[] Create(ReadOnlySpan<byte> alternate) => alternate.ToArray();
}

/// <summary>
/// The mergeable ranks of a Tiktoken encoding: every token's bytes with its rank, and the rank-based byte-pair merge that turns the
/// bytes of one pre-token into token ranks.
/// </summary>
/// <remarks>
/// Byte-pair encoding (Sennrich et al. 2016) over raw bytes, with the rank of a token as its merge priority: starting from single
/// bytes, the adjacent pair whose joined bytes have the lowest rank is merged, leftmost first on ties, until no joined pair is a
/// token. Immutable after construction; safe for concurrent use.
/// </remarks>
internal sealed class TiktokenRanks
{
    private const int ScanLimit = 48;

    private readonly Dictionary<byte[], int> _ranks;
    private readonly Dictionary<byte[], int>.AlternateLookup<ReadOnlySpan<byte>> _bySpan;
    private readonly byte[]?[] _tokenBytes;
    private readonly int[] _singleByte = new int[256];

    [ThreadStatic]
    private static Work? t_work;

    private sealed class Work
    {
        public int[] Starts = new int[ScanLimit + 2];
        public int[] Ranks = new int[ScanLimit + 2];
        public int[] Next = new int[64];
        public int[] Prev = new int[64];
        public int[] End = new int[64];
        public ulong[] Heap = new ulong[128];
    }

    /// <summary>Creates the table.</summary>
    /// <param name="tokens">Token bytes with their ranks.</param>
    /// <exception cref="InvalidDataException">A byte value has no single-byte token, a token or rank repeats, or a rank is out of range.</exception>
    internal TiktokenRanks(IReadOnlyList<(byte[] Bytes, int Rank)> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        _ranks = new Dictionary<byte[], int>(tokens.Count, ByteSequenceComparer.Instance);
        _bySpan = _ranks.GetAlternateLookup<ReadOnlySpan<byte>>();
        int max = -1;
        foreach ((byte[] bytes, int rank) in tokens)
        {
            if (bytes.Length == 0 || rank < 0)
            {
                throw new InvalidDataException("A Tiktoken rank entry has an empty token or a negative rank.");
            }

            if (!_ranks.TryAdd(bytes, rank))
            {
                throw new InvalidDataException("A Tiktoken token occurs twice.");
            }

            max = Math.Max(max, rank);
        }

        // Ranks are normally dense (the file may skip a few); refuse a file that would make the reverse table enormous.
        if (max > (4 * tokens.Count) + 65_536)
        {
            throw new InvalidDataException("The Tiktoken ranks are too sparse to be valid.");
        }

        _tokenBytes = new byte[max + 1][];
        foreach (KeyValuePair<byte[], int> kv in _ranks)
        {
            if (_tokenBytes[kv.Value] is not null)
            {
                throw new InvalidDataException("A Tiktoken rank occurs twice.");
            }

            _tokenBytes[kv.Value] = kv.Key;
        }

        // Every byte needs its own token, or some text could not be encoded at all.
        for (int b = 0; b < 256; b++)
        {
            _singleByte[b] = _bySpan.TryGetValue([(byte)b], out int r) ? r : throw new InvalidDataException($"The Tiktoken ranks have no token for the byte 0x{b:X2}.");
        }
    }

    /// <summary>The number of tokens.</summary>
    internal int Count => _ranks.Count;

    /// <summary>One more than the largest rank.</summary>
    internal int RankLimit => _tokenBytes.Length;

    /// <summary>Gets the bytes of a token.</summary>
    /// <param name="rank">The rank.</param>
    /// <returns>The bytes, or <see langword="null"/> when no token has this rank.</returns>
    internal byte[]? BytesOf(int rank) => (uint)rank < (uint)_tokenBytes.Length ? _tokenBytes[rank] : null;

    /// <summary>Finds the rank of a token by its bytes.</summary>
    /// <param name="bytes">The token's bytes.</param>
    /// <returns>The rank, or <see langword="null"/>.</returns>
    internal int? RankOf(ReadOnlySpan<byte> bytes) => _bySpan.TryGetValue(bytes, out int r) ? r : null;

    /// <summary>Encodes the bytes of one pre-token, appending token ranks and the byte length of each token.</summary>
    /// <param name="piece">The bytes; not empty.</param>
    /// <param name="ids">Receives the ranks.</param>
    /// <param name="lengths">Receives the byte length of each token, or <see langword="null"/> when the caller does not need them.</param>
    internal void EncodePiece(ReadOnlySpan<byte> piece, List<int> ids, List<int>? lengths)
    {
        if (piece.Length == 1)
        {
            ids.Add(_singleByte[piece[0]]);
            lengths?.Add(1);
            return;
        }

        if (_bySpan.TryGetValue(piece, out int whole))
        {
            ids.Add(whole);
            lengths?.Add(piece.Length);
            return;
        }

        Work work = t_work ??= new Work();
        if (piece.Length <= ScanLimit)
        {
            MergeByScan(piece, work, ids, lengths);
        }
        else
        {
            MergeByHeap(piece, work, ids, lengths);
        }
    }

    /// <summary>Encodes a piece with the heap merge whatever its length, so that tests can compare it with the scan.</summary>
    internal void EncodePieceByHeap(ReadOnlySpan<byte> piece, List<int> ids, List<int>? lengths) =>
        MergeByHeap(piece, t_work ??= new Work(), ids, lengths);

    private int RankOfRange(ReadOnlySpan<byte> piece, int start, int end) =>
        _bySpan.TryGetValue(piece[start..end], out int r) ? r : int.MaxValue;

    // Short pieces: keep the part starts and the rank of each adjacent pair, repeatedly merge the lowest rank (leftmost on ties).
    private void MergeByScan(ReadOnlySpan<byte> piece, Work w, List<int> ids, List<int>? lengths)
    {
        int[] starts = w.Starts;
        int[] ranks = w.Ranks; // ranks[i] = rank of parts i and i+1 joined
        int parts = piece.Length;
        for (int i = 0; i <= parts; i++)
        {
            starts[i] = i;
        }

        for (int i = 0; i + 1 < parts; i++)
        {
            ranks[i] = RankOfRange(piece, i, i + 2);
        }

        while (parts > 1)
        {
            int best = int.MaxValue;
            int at = -1;
            for (int i = 0; i + 1 < parts; i++)
            {
                if (ranks[i] < best)
                {
                    best = ranks[i];
                    at = i;
                }
            }

            if (at < 0)
            {
                break;
            }

            // Join parts at and at+1: drop the start of at+1.
            for (int i = at + 1; i < parts; i++)
            {
                starts[i] = starts[i + 1];
            }

            parts--;
            for (int i = at + 1; i + 1 < parts; i++)
            {
                ranks[i] = ranks[i + 1];
            }

            ranks[at] = at + 1 < parts ? RankOfRange(piece, starts[at], starts[at + 2]) : int.MaxValue;
            if (at > 0)
            {
                ranks[at - 1] = RankOfRange(piece, starts[at - 1], starts[at + 1]);
            }
        }

        for (int i = 0; i < parts; i++)
        {
            ReadOnlySpan<byte> part = piece[starts[i]..starts[i + 1]];
            ids.Add(_bySpan.TryGetValue(part, out int r) ? r : _singleByte[part[0]]);
            lengths?.Add(part.Length);
        }
    }

    // Long pieces: the same merge order with a linked list of parts and a min-heap of (rank, position), so a long run of letters
    // costs O(n log n). A heap entry is re-checked against the current parts when popped.
    private void MergeByHeap(ReadOnlySpan<byte> piece, Work w, List<int> ids, List<int>? lengths)
    {
        int n = piece.Length;
        if (w.Next.Length < n + 1)
        {
            int size = Math.Max(n + 1, w.Next.Length * 2);
            w.Next = new int[size];
            w.Prev = new int[size];
            w.End = new int[size];
        }

        int[] next = w.Next;
        int[] end = w.End; // end[i] = end of the part that starts at i; -1 once the part was merged into its left neighbour
        int heapCount = 0;
        ulong[] heap = w.Heap;
        for (int i = 0; i < n; i++)
        {
            next[i] = i + 1 < n ? i + 1 : -1;
            w.Prev[i] = i - 1;
            end[i] = i + 1;
        }

        for (int i = 0; i + 1 < n; i++)
        {
            int r = RankOfRange(piece, i, i + 2);
            if (r != int.MaxValue)
            {
                Push(ref heap, ref heapCount, r, i);
            }
        }

        while (heapCount > 0)
        {
            ulong top = Pop(heap, ref heapCount);
            int pos = (int)(uint)top;
            int rank = (int)(top >> 32);
            if (end[pos] < 0 || next[pos] < 0)
            {
                continue;
            }

            int right = next[pos];
            if (RankOfRange(piece, pos, end[right]) != rank)
            {
                continue;
            }

            end[pos] = end[right];
            end[right] = -1;
            next[pos] = next[right];
            if (next[right] >= 0)
            {
                w.Prev[next[right]] = pos;
            }

            if (w.Prev[pos] >= 0)
            {
                int p = w.Prev[pos];
                int r = RankOfRange(piece, p, end[pos]);
                if (r != int.MaxValue)
                {
                    Push(ref heap, ref heapCount, r, p);
                }
            }

            if (next[pos] >= 0)
            {
                int r = RankOfRange(piece, pos, end[next[pos]]);
                if (r != int.MaxValue)
                {
                    Push(ref heap, ref heapCount, r, pos);
                }
            }
        }

        w.Heap = heap;
        for (int i = 0; i >= 0 && i < n; i = next[i])
        {
            ReadOnlySpan<byte> part = piece[i..end[i]];
            ids.Add(_bySpan.TryGetValue(part, out int r) ? r : _singleByte[part[0]]);
            lengths?.Add(part.Length);
        }
    }

    private static void Push(ref ulong[] heap, ref int count, int rank, int pos)
    {
        if (count == heap.Length)
        {
            Array.Resize(ref heap, count * 2);
        }

        ulong key = ((ulong)(uint)rank << 32) | (uint)pos;
        int i = count++;
        while (i > 0)
        {
            int parent = (i - 1) / 2;
            if (heap[parent] <= key)
            {
                break;
            }

            heap[i] = heap[parent];
            i = parent;
        }

        heap[i] = key;
    }

    private static ulong Pop(ulong[] heap, ref int count)
    {
        ulong top = heap[0];
        ulong last = heap[--count];
        int i = 0;
        int half = count / 2;
        while (i < half)
        {
            int child = (2 * i) + 1;
            if (child + 1 < count && heap[child + 1] < heap[child])
            {
                child++;
            }

            if (last <= heap[child])
            {
                break;
            }

            heap[i] = heap[child];
            i = child;
        }

        if (count > 0)
        {
            heap[i] = last;
        }

        return top;
    }
}
