using System.Collections.Concurrent;

namespace RecursiveAI.OneNlp;

/// <summary>
/// An interning table: each distinct string gets a small integer ID, so token attribute columns hold IDs instead of strings.
/// ID 0 is always the empty string, which is how "not set" is written in every column.
/// </summary>
/// <remarks>
/// A store is shared by all documents of one pipeline, so the same string has the same ID in every document. It only grows:
/// strings are never removed. Lookups of existing strings and reads by ID take no lock; adding a new string takes a short one.
/// All members are thread-safe.
/// </remarks>
public sealed class StringStore
{
    private readonly ConcurrentDictionary<string, int> _ids = new(StringComparer.Ordinal);
    private readonly Lock _writeGate = new();
    private string[] _strings = new string[64];
    private int _count;

    /// <summary>Creates a store that holds only the empty string, at ID 0.</summary>
    public StringStore()
    {
        _strings[0] = string.Empty;
        _ids[string.Empty] = 0;
        _count = 1;
    }

    /// <summary>The number of strings held, including the empty string. Valid IDs are <c>0</c> up to <c>Count - 1</c>.</summary>
    public int Count => Volatile.Read(ref _count);

    /// <summary>Gets the string with the given ID.</summary>
    /// <param name="id">An ID returned by <see cref="Add"/> of this store.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is negative or not less than <see cref="Count"/>.</exception>
    public string this[int id]
    {
        get
        {
            // The count is read before the array: a writer publishes the array that holds an entry before it publishes the count
            // that includes it, so the array read here always contains every entry below the count.
            int count = Volatile.Read(ref _count);
            if ((uint)id >= (uint)count)
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "No string has this ID in this store.");
            }

            return Volatile.Read(ref _strings)[id];
        }
    }

    /// <summary>Gets the ID of a string, adding the string when it is new.</summary>
    /// <param name="value">The string; the empty string is ID 0.</param>
    /// <returns>The same ID for equal strings, for the life of the store.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public int Add(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (_ids.TryGetValue(value, out int existing))
        {
            return existing;
        }

        lock (_writeGate)
        {
            if (_ids.TryGetValue(value, out existing))
            {
                return existing;
            }

            int id = _count;
            string[] strings = _strings;
            if (id == strings.Length)
            {
                Array.Resize(ref strings, strings.Length * 2);
                Volatile.Write(ref _strings, strings);
            }

            strings[id] = value;
            _ids[value] = id;
            Volatile.Write(ref _count, id + 1);
            return id;
        }
    }

    /// <summary>Looks a string up without adding it.</summary>
    /// <param name="value">The string.</param>
    /// <param name="id">Receives the ID when the string is held.</param>
    /// <returns><see langword="true"/> when the string is held.</returns>
    public bool TryGetId(string value, out int id)
    {
        ArgumentNullException.ThrowIfNull(value);
        return _ids.TryGetValue(value, out id);
    }
}
