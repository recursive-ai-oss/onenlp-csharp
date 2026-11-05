namespace RecursiveAI.OneNlp.Tokenizers.Hf;

/// <summary>A growable list of token IDs that a thread reuses between calls, so that encoding to IDs allocates nothing once warm.</summary>
internal sealed class IdBuffer
{
    private int[] _items = new int[256];

    /// <summary>The number of IDs held.</summary>
    internal int Count { get; private set; }

    /// <summary>The IDs held.</summary>
    internal ReadOnlySpan<int> AsSpan() => _items.AsSpan(0, Count);

    /// <summary>The IDs held from an index on.</summary>
    /// <param name="start">The first index.</param>
    internal ReadOnlySpan<int> AsSpan(int start) => _items.AsSpan(start, Count - start);

    /// <summary>Empties the buffer, keeping its storage.</summary>
    internal void Clear() => Count = 0;

    /// <summary>Drops the IDs after the first <paramref name="count"/>.</summary>
    /// <param name="count">The number of IDs to keep.</param>
    internal void Truncate(int count) => Count = count;

    /// <summary>Appends one ID.</summary>
    /// <param name="id">The ID.</param>
    internal void Add(int id)
    {
        if (Count == _items.Length)
        {
            Array.Resize(ref _items, Count * 2);
        }

        _items[Count++] = id;
    }

    /// <summary>Appends several IDs.</summary>
    /// <param name="ids">The IDs.</param>
    internal void AddRange(ReadOnlySpan<int> ids)
    {
        if (Count + ids.Length > _items.Length)
        {
            Array.Resize(ref _items, Math.Max(_items.Length * 2, Count + ids.Length));
        }

        ids.CopyTo(_items.AsSpan(Count));
        Count += ids.Length;
    }
}
