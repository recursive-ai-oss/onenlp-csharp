using System.Text;

namespace RecursiveAI.OneNlp.Tokenizers.Hf;

/// <summary>
/// The Unicode normalization forms NFC, NFD, NFKC and NFKD (UAX #15), implemented over generated tables. The .NET library's own
/// <c>string.Normalize</c> does nothing for non-ASCII text when an application runs in invariant-globalization mode, which AOT
/// and container deployments commonly do, so results would depend on the host; these do not.
/// </summary>
internal static class UnicodeNormalization
{
    private const int SBase = 0xAC00;
    private const int LBase = 0x1100;
    private const int VBase = 0x1161;
    private const int TBase = 0x11A7;
    private const int LCount = 19;
    private const int VCount = 21;
    private const int TCount = 28;
    private const int NCount = VCount * TCount;
    private const int SCount = LCount * NCount;

    private static readonly Lazy<HashSet<int>> SecondsOfPairs = new(() =>
    {
        var set = new HashSet<int>();
        foreach (long k in NormalizationData.PairKeys)
        {
            set.Add((int)(k & 0x1FFFFF));
        }

        return set;
    });

    /// <summary>Normalizes a well-formed string.</summary>
    /// <param name="text">The text; unpaired surrogates are kept as they are.</param>
    /// <param name="compatibility">Use compatibility decompositions (NFKC, NFKD) instead of canonical ones only.</param>
    /// <param name="compose">Recompose afterwards (NFC, NFKC) instead of leaving the text decomposed.</param>
    internal static string Normalize(string text, bool compatibility, bool compose)
    {
        var cps = new List<int>(text.Length + 4);
        for (int i = 0; i < text.Length;)
        {
            int cp = NormalizerHelpers.CodePointAt(text, i, out int w);
            Decompose(cp, compatibility, cps);
            i += w;
        }

        Reorder(cps);
        if (compose)
        {
            cps = Compose(cps);
        }

        var sb = new StringBuilder(cps.Count);
        foreach (int cp in cps)
        {
            if (cp < 0x10000)
            {
                sb.Append((char)cp);
            }
            else
            {
                sb.Append(char.ConvertFromUtf32(cp));
            }
        }

        return sb.ToString();
    }

    /// <summary>Whether a code point can combine with the one before it when composing, so that a run must not be cut in front of it.</summary>
    internal static bool CombinesWithPrevious(int cp) =>
        CombiningClass(cp) != 0 || cp is >= VBase and < VBase + VCount or > TBase and < TBase + TCount || SecondsOfPairs.Value.Contains(cp);

    internal static int CombiningClass(int cp)
    {
        if (cp < 0x300)
        {
            return 0;
        }

        ReadOnlySpan<int> starts = NormalizationData.CccStarts;
        int lo = 0;
        int hi = starts.Length - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >>> 1;
            if (cp < starts[mid])
            {
                hi = mid - 1;
            }
            else if (cp > NormalizationData.CccEnds[mid])
            {
                lo = mid + 1;
            }
            else
            {
                return NormalizationData.CccValues[mid];
            }
        }

        return 0;
    }

    private static void Decompose(int cp, bool compatibility, List<int> output)
    {
        if (cp < 0xA0)
        {
            output.Add(cp);
            return;
        }

        if (cp is >= SBase and < SBase + SCount)
        {
            int s = cp - SBase;
            output.Add(LBase + (s / NCount));
            output.Add(VBase + ((s % NCount) / TCount));
            if (s % TCount != 0)
            {
                output.Add(TBase + (s % TCount));
            }

            return;
        }

        ReadOnlySpan<int> keys = compatibility ? NormalizationData.CompatKeys : NormalizationData.CanonKeys;
        int index = keys.BinarySearch(cp);
        if (index < 0)
        {
            output.Add(cp);
            return;
        }

        ReadOnlySpan<int> offsets = compatibility ? NormalizationData.CompatOffsets : NormalizationData.CanonOffsets;
        ReadOnlySpan<int> pool = compatibility ? NormalizationData.CompatPool : NormalizationData.CanonPool;
        for (int k = offsets[index]; k < offsets[index + 1]; k++)
        {
            int part = pool[k];
            // Hangul syllables never occur inside table entries, so the parts are final.
            output.Add(part);
        }
    }

    // Canonical ordering: within each run of non-starters, a stable sort by combining class.
    private static void Reorder(List<int> cps)
    {
        for (int i = 1; i < cps.Count; i++)
        {
            int cc = CombiningClass(cps[i]);
            if (cc == 0)
            {
                continue;
            }

            int j = i;
            while (j > 0)
            {
                int prev = CombiningClass(cps[j - 1]);
                if (prev <= cc)
                {
                    break;
                }

                (cps[j], cps[j - 1]) = (cps[j - 1], cps[j]);
                j--;
            }
        }
    }

    private static List<int> Compose(List<int> cps)
    {
        var result = new List<int>(cps.Count);
        int starter = -1;
        foreach (int c in cps)
        {
            int cc = CombiningClass(c);
            if (starter >= 0)
            {
                bool intervening = result.Count - 1 > starter;
                bool blocked = intervening && CombiningClass(result[^1]) >= cc;
                if (!blocked)
                {
                    int composite = Pair(result[starter], c);
                    if (composite >= 0)
                    {
                        result[starter] = composite;
                        continue;
                    }
                }
            }

            if (cc == 0)
            {
                starter = result.Count;
            }

            result.Add(c);
        }

        return result;
    }

    private static int Pair(int first, int second)
    {
        if (first is >= LBase and < LBase + LCount && second is >= VBase and < VBase + VCount)
        {
            return SBase + ((((first - LBase) * VCount) + (second - VBase)) * TCount);
        }

        if (first is >= SBase and < SBase + SCount && (first - SBase) % TCount == 0 && second is > TBase and < TBase + TCount)
        {
            return first + (second - TBase);
        }

        int index = NormalizationData.PairKeys.BinarySearch(((long)first << 21) | (uint)second);
        return index >= 0 ? NormalizationData.PairResults[index] : -1;
    }
}
