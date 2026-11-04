namespace RecursiveAI.OneNlp.Tokenizers.Unigram;

/// <summary>
/// A trie over vocabulary pieces for common-prefix search: the pieces that are prefixes of the text at a position, shortest first.
/// Stored as arrays (edges of a node are contiguous and sorted), so a large vocabulary costs a few bytes per node and no objects.
/// </summary>
internal sealed class PieceTrie
{
    private readonly int[] _edgeStart;
    private readonly char[] _edgeChar;
    private readonly int[] _edgeTarget;
    private readonly int[] _terminal;

    /// <summary>Builds the trie. When a piece repeats, the later ID wins.</summary>
    /// <param name="pieces">The piece strings, indexed by ID. Empty pieces are skipped.</param>
    internal PieceTrie(string[] pieces)
    {
        ArgumentNullException.ThrowIfNull(pieces);
        // Sort IDs by piece (ordinal); for equal pieces the later ID sorts last, so it overwrites the terminal.
        int[] order = [.. Enumerable.Range(0, pieces.Length).Where(i => pieces[i].Length > 0)];
        Array.Sort(order, (a, b) =>
        {
            int c = string.CompareOrdinal(pieces[a], pieces[b]);
            return c != 0 ? c : a.CompareTo(b);
        });

        var edgeStart = new List<int>();
        var edgeChar = new List<char>();
        var edgeTarget = new List<int>();
        var terminal = new List<int> { -1 };

        // Breadth first, so that the children of a node are appended together and nodes are numbered in the order their edges are.
        var queue = new Queue<(int Node, int Lo, int Hi, int Depth)>();
        queue.Enqueue((0, 0, order.Length, 0));
        while (queue.Count > 0)
        {
            (int node, int lo, int hi, int depth) = queue.Dequeue();
            edgeStart.Add(edgeChar.Count);
            while (lo < hi && pieces[order[lo]].Length == depth)
            {
                terminal[node] = order[lo];
                lo++;
            }

            int i = lo;
            while (i < hi)
            {
                char c = pieces[order[i]][depth];
                int j = i;
                while (j < hi && pieces[order[j]][depth] == c)
                {
                    j++;
                }

                int child = terminal.Count;
                terminal.Add(-1);
                edgeChar.Add(c);
                edgeTarget.Add(child);
                queue.Enqueue((child, i, j, depth + 1));
                i = j;
            }
        }

        edgeStart.Add(edgeChar.Count);
        _edgeStart = [.. edgeStart];
        _edgeChar = [.. edgeChar];
        _edgeTarget = [.. edgeTarget];
        _terminal = [.. terminal];
    }

    /// <summary>The root node, where a search starts.</summary>
    internal static int Root => 0;

    /// <summary>The ID of the piece that ends at the node, or -1.</summary>
    /// <param name="node">The node.</param>
    internal int TerminalId(int node) => _terminal[node];

    /// <summary>Follows one character from a node.</summary>
    /// <param name="node">The node.</param>
    /// <param name="c">The character.</param>
    /// <returns>The child node, or -1 when no piece continues with that character.</returns>
    internal int Step(int node, char c)
    {
        int lo = _edgeStart[node];
        int hi = _edgeStart[node + 1] - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >>> 1;
            char m = _edgeChar[mid];
            if (m == c)
            {
                return _edgeTarget[mid];
            }

            if (m < c)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        return -1;
    }
}
