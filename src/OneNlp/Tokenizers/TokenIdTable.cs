namespace RecursiveAI.OneNlp.Tokenizers;

/// <summary>
/// The two-way table of a model vocabulary: token string to ID (with lookups by <see cref="ReadOnlySpan{T}"/> so that a model can probe
/// substrings without allocating) and ID to token. IDs need not be contiguous; a dense table is used when they nearly are.
/// </summary>
internal sealed class TokenIdTable
{
    /// <summary>The largest ID accepted. Real vocabularies are under a million; this bounds the dense table a hostile file can ask for.</summary>
    internal const int MaxId = 1 << 26;

    private readonly Dictionary<string, int> _ids;
    private readonly Dictionary<string, int>.AlternateLookup<ReadOnlySpan<char>> _spanLookup;
    private readonly string?[]? _dense;
    private readonly Dictionary<int, string>? _sparse;

    /// <summary>Builds the table. When a token or an ID repeats, the later entry wins, as when a JSON object repeats a key.</summary>
    /// <param name="entries">The tokens with their IDs.</param>
    /// <exception cref="InvalidDataException">An ID is negative or above <see cref="MaxId"/>.</exception>
    internal TokenIdTable(List<KeyValuePair<string, int>> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _ids = new Dictionary<string, int>(entries.Count, StringComparer.Ordinal);
        int maxId = -1;
        foreach (KeyValuePair<string, int> e in entries)
        {
            if (e.Value is < 0 or > MaxId)
            {
                throw new InvalidDataException($"The vocabulary entry '{e.Key}' has the ID {e.Value}, outside 0 to {MaxId}.");
            }

            _ids[e.Key] = e.Value;
            maxId = Math.Max(maxId, e.Value);
        }

        _spanLookup = _ids.GetAlternateLookup<ReadOnlySpan<char>>();
        if (maxId < (_ids.Count * 4L) + 4096)
        {
            _dense = new string?[maxId + 1];
            foreach (KeyValuePair<string, int> e in entries)
            {
                _dense[e.Value] = e.Key;
            }
        }
        else
        {
            _sparse = new Dictionary<int, string>(entries.Count);
            foreach (KeyValuePair<string, int> e in entries)
            {
                _sparse[e.Value] = e.Key;
            }
        }
    }

    /// <summary>The number of distinct tokens.</summary>
    internal int Count => _ids.Count;

    /// <summary>Looks up a token.</summary>
    /// <param name="token">The token text.</param>
    /// <param name="id">Receives the ID.</param>
    internal bool TryGetId(ReadOnlySpan<char> token, out int id) => _spanLookup.TryGetValue(token, out id);

    /// <summary>Looks up a token string.</summary>
    /// <param name="token">The token text.</param>
    /// <returns>The ID, or <see langword="null"/> when absent.</returns>
    internal int? GetId(string token) => _ids.TryGetValue(token, out int id) ? id : null;

    /// <summary>Gets the token of an ID.</summary>
    /// <param name="id">The ID.</param>
    /// <returns>The token, or <see langword="null"/> when no token has the ID.</returns>
    internal string? GetToken(int id)
    {
        if (_dense is not null)
        {
            return (uint)id < (uint)_dense.Length ? _dense[id] : null;
        }

        return _sparse!.TryGetValue(id, out string? t) ? t : null;
    }
}
