namespace RecursiveAI.OneNlp.Neural.Components;

/// <summary>
/// Decodes a dependency tree from arc scores as a maximum spanning arborescence: the Chu-Liu/Edmonds algorithm
/// (Chu and Liu 1965; Edmonds 1967), used for dependency parsing by McDonald et al. 2005 §3.2. It finds the best tree whether or not
/// it is projective, which is why a graph-based parser can output the crossing arcs that English web text has.
/// </summary>
internal static class MaximumSpanningTree
{
    private const float Forbidden = -1e30f;

    /// <summary>
    /// The best tree over the nodes 0..n with node 0 the artificial root and exactly one token attached to it.
    /// </summary>
    /// <param name="score">score[h, d] for the arc from head h to dependent d, nodes 0..n (n + 1 rows and columns). Not modified.</param>
    /// <returns>heads[d] for d in 0..n; heads[0] is -1.</returns>
    public static int[] Decode(float[,] score)
    {
        int nodes = score.GetLength(0);
        int[] best = Solve(score);
        List<int> rootChildren = [];
        for (int d = 1; d < nodes; d++)
        {
            if (best[d] == 0)
            {
                rootChildren.Add(d);
            }
        }

        if (rootChildren.Count <= 1)
        {
            return best;
        }

        // A UD sentence has one root. Try each token that the free solution attached to the root as the only one, keep the best total.
        // (The best single-root tree has its root among the free solution's root children: McDonald's trees only get worse by forcing.)
        int[]? winner = null;
        double winnerScore = double.NegativeInfinity;
        foreach (int r in rootChildren)
        {
            var constrained = (float[,])score.Clone();
            for (int d = 1; d < nodes; d++)
            {
                if (d != r)
                {
                    constrained[0, d] = Forbidden;
                }
            }

            int[] heads = Solve(constrained);
            double total = 0;
            for (int d = 1; d < nodes; d++)
            {
                total += score[heads[d], d];
            }

            if (total > winnerScore)
            {
                winnerScore = total;
                winner = heads;
            }
        }

        return winner!;
    }

    // Chu-Liu/Edmonds: pick the best head for every node; if that has a cycle, contract it into one node whose arcs are re-scored so
    // that breaking the cycle at the entry point costs exactly the arc it replaces, solve the smaller problem, and expand.
    private static int[] Solve(float[,] score)
    {
        int nodes = score.GetLength(0);
        var best = new int[nodes];
        best[0] = -1;
        for (int d = 1; d < nodes; d++)
        {
            int h = -1;
            float top = float.NegativeInfinity;
            for (int c = 0; c < nodes; c++)
            {
                if (c != d && score[c, d] > top)
                {
                    top = score[c, d];
                    h = c;
                }
            }

            best[d] = h;
        }

        int[] cycle = FindCycle(best);
        if (cycle.Length == 0)
        {
            return best;
        }

        var inCycle = new bool[nodes];
        double cycleScore = 0;
        foreach (int v in cycle)
        {
            inCycle[v] = true;
            cycleScore += score[best[v], v];
        }

        // Surviving nodes keep their order (the root stays 0); the contracted node comes last.
        var original = new List<int>();
        for (int v = 0; v < nodes; v++)
        {
            if (!inCycle[v])
            {
                original.Add(v);
            }
        }

        int m = original.Count;
        int contracted = m;
        var small = new float[m + 1, m + 1];
        var enterAt = new int[m];
        var leaveFrom = new int[m];
        for (int a = 0; a < m; a++)
        {
            for (int b = 0; b < m; b++)
            {
                small[a, b] = a == b ? float.NegativeInfinity : score[original[a], original[b]];
            }

            // Arc from outside into the cycle at v: it replaces v's cycle arc.
            float enter = float.NegativeInfinity;
            int enterNode = cycle[0];
            foreach (int v in cycle)
            {
                float s = (float)(score[original[a], v] - score[best[v], v] + cycleScore);
                if (s > enter)
                {
                    enter = s;
                    enterNode = v;
                }
            }

            small[a, contracted] = enter;
            enterAt[a] = enterNode;

            // Arc out of the cycle from u to the outside node.
            float leave = float.NegativeInfinity;
            int leaveNode = cycle[0];
            foreach (int u in cycle)
            {
                if (score[u, original[a]] > leave)
                {
                    leave = score[u, original[a]];
                    leaveNode = u;
                }
            }

            small[contracted, a] = leave;
            leaveFrom[a] = leaveNode;
        }

        small[contracted, contracted] = float.NegativeInfinity;
        int[] solved = Solve(small);

        var result = new int[nodes];
        result[0] = -1;
        foreach (int v in cycle)
        {
            result[v] = best[v];
        }

        for (int a = 1; a < m; a++)
        {
            int h = solved[a];
            result[original[a]] = h == contracted ? leaveFrom[a] : original[h];
        }

        int entering = solved[contracted];
        int into = enterAt[entering];
        result[into] = original[entering];
        return result;
    }

    private static int[] FindCycle(int[] head)
    {
        int nodes = head.Length;
        var state = new byte[nodes]; // 0 unseen, 1 on the current walk, 2 finished
        for (int start = 1; start < nodes; start++)
        {
            if (state[start] != 0)
            {
                continue;
            }

            int v = start;
            while (v > 0 && state[v] == 0)
            {
                state[v] = 1;
                v = head[v];
            }

            if (v > 0 && state[v] == 1)
            {
                var cycle = new List<int> { v };
                for (int u = head[v]; u != v; u = head[u])
                {
                    cycle.Add(u);
                }

                return [.. cycle];
            }

            for (int u = start; u > 0 && state[u] == 1; u = head[u])
            {
                state[u] = 2;
            }
        }

        return [];
    }
}
