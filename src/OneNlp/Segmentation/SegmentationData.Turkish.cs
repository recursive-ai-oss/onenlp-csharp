using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace RecursiveAI.OneNlp.Segmentation;

public sealed partial class SegmentationData
{
    private const int MaxSuffixKey = 12;
    private static SegmentationData? s_turkish;
    private static SegmentationData? s_turkishWhole;

    // Month names, which an ordinal number may precede with a capital letter ("1. Ocak"); lower-case words always follow an ordinal.
    private static readonly FrozenSet<string> TurkishMonths = new[] { "ocak", "\u015fubat", "mart", "nisan", "may\u0131s", "haziran", "temmuz", "a\u011fustos", "eyl\u00fcl", "ekim", "kas\u0131m", "aral\u0131k" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Turkish, after the way UD Turkish-BOUN tokenizes: the neutral defaults, with the apostrophe that joins a suffix to a proper noun
    /// (<c>Ankara'da</c>, <c>Ali'nin</c>) kept inside the token, as the treebank does, and the productive split of multiword tokens. UD Turkish
    /// writes a copula or a derivational suffix that is spelled together with its word as a separate syntactic word (<c>vardır</c> is
    /// <c>var</c> + <c>dır</c>, <c>arasındaki</c> is <c>arasında</c> + <c>ki</c>, <c>önemliydi</c> is <c>önemli</c> + <c>ydi</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Whether such a suffix is a word of its own depends on the stem, so no word list can tell. The tokenizer reads a table of word endings
    /// (<c>Segmentation/Data/tr.mwt.tsv</c>, written by <c>tools/fetch/tr/mwt_table.py</c> from the training split of UD Turkish-BOUN only): the
    /// last letters of a token, lower-cased with the Turkish rules, map to the length of the final piece the treebank cuts off. The longest
    /// matching ending decides. About half of the cuts it makes are the treebank's; <see cref="TryMatchMultiword"/> tells a cut from a
    /// punctuation split, so the output keeps the surface form as one token with two words.
    /// </para>
    /// </remarks>
    public static SegmentationData Turkish => s_turkish ??= BuildTurkish();

    /// <summary>An apostrophe between a digit and a letter stays inside the token (<c>1882'den</c>); UAX #29 cuts there, as it has no rule for digit, apostrophe, letter.</summary>
    internal bool NumberApostrophe { get; private init; }

    /// <summary>Ending (lower case, 2 to 12 letters) to the length of the piece a multiword token cuts off at the end; empty where the language has none.</summary>
    internal FrozenDictionary<string, int> SuffixSplits { get; private init; } = new Dictionary<string, int>().ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The longest ending in <see cref="SuffixSplits"/>.</summary>
    internal int SuffixSplitKeyLength { get; private init; }

    /// <summary>
    /// Length of the final piece that the multiword-token table cuts off a word, or 0 when the word is not cut.
    /// </summary>
    /// <param name="word">A word of letters; anything else is never cut.</param>
    internal int SuffixSplitLength(ReadOnlySpan<char> word)
    {
        if (SuffixSplits.Count == 0 || word.Length < 3)
        {
            return 0;
        }

        foreach (char c in word)
        {
            if (!char.IsLetter(c))
            {
                return 0;
            }
        }

        // Bounded buffer: at most MaxSuffixKey (12) characters are lower-cased and looked up.
        int keep = Math.Min(word.Length, SuffixSplitKeyLength);
        Span<char> tail = stackalloc char[MaxSuffixKey];
        for (int i = 0; i < keep; i++)
        {
            tail[i] = TurkicLower(word[word.Length - keep + i]);
        }

        var lookup = SuffixSplits.GetAlternateLookup<ReadOnlySpan<char>>();
        for (int k = keep; k >= 2; k--)
        {
            if (lookup.TryGetValue(tail.Slice(keep - k, k), out int cut))
            {
                // The final piece must leave a stem of at least two letters.
                return cut > 0 && cut <= word.Length - 2 ? cut : 0;
            }
        }

        return 0;
    }

    /// <summary>Lower case with the Turkish rules: <c>I</c> is <c>ı</c> and <c>İ</c> is <c>i</c>.</summary>
    internal static char TurkicLower(char c) => c == 'I' ? '\u0131' : c == '\u0130' ? 'i' : char.ToLowerInvariant(c);

    private static bool TryMatchSuffixSplit(SegmentationData data, Doc doc, int start, out int count, [NotNullWhen(true)] out string[]? words)
    {
        count = 0;
        words = null;
        if (data.SuffixSplits.Count == 0 || start < 0 || start + 1 >= doc.Length)
        {
            return false;
        }

        // The tokenizer cut a word in two pieces without a space between them; punctuation after a word is not a letter run.
        Token first = doc[start];
        Token second = doc[start + 1];
        if (first.EndIdx != second.Idx || second.EndIdx - first.Idx > 64)
        {
            return false;
        }

        ReadOnlySpan<char> surface = doc.Text.AsSpan(first.Idx, second.EndIdx - first.Idx);
        if (data.SuffixSplitLength(surface) != second.EndIdx - second.Idx)
        {
            return false;
        }

        count = 2;
        words = [doc.Text.Substring(first.Idx, first.EndIdx - first.Idx), doc.Text.Substring(second.Idx, second.EndIdx - second.Idx)];
        return true;
    }

    /// <summary>
    /// Turkish as <see cref="Turkish"/> tokenizes it, except that no word is cut by the ending table: a copula or suffix spelled onto its word stays
    /// part of one token. It is the inner tokenizer of a trained multiword-token splitter, which decides the cuts itself.
    /// </summary>
    public static SegmentationData TurkishWhole => s_turkishWhole ??= BuildTurkish(withSuffixSplits: false);

    private static SegmentationData BuildTurkish(bool withSuffixSplits = true)
    {
        int longest = 0;
        FrozenDictionary<string, int> splits = withSuffixSplits ? LoadSuffixSplits(out longest) : new Dictionary<string, int>().ToFrozenDictionary(StringComparer.Ordinal);
        return new(
            // UD Turkish-BOUN cuts a hyphen off both sides (Fransızca - Türkçe, 1 - 2), so hyphens never join.
            keepUrlsAndEmails: true, joinHyphenated: false, hyphenPrefixes: null, digitJoiners: DigitJoinerMarks, markRuns: MarkRunChars, emoticons: [.. CommonEmoticons, ".\""],
            specialCases: [], abbreviations: [], clitics: [], nTSuffix: false, nonFinalAbbreviations: [])
        {
            Name = withSuffixSplits ? "turkish" : "turkish-whole",
            NumberApostrophe = true,
            OrdinalDot = true,
            OrdinalFollowers = TurkishMonths,
            SuffixSplits = splits,
            SuffixSplitKeyLength = longest,
        };
    }

    private static FrozenDictionary<string, int> LoadSuffixSplits(out int longest)
    {
        longest = 0;
        using Stream? stream = typeof(SegmentationData).Assembly.GetManifestResourceStream("Segmentation/Data/tr.mwt.tsv");
        if (stream is null)
        {
            throw new InvalidOperationException("The Turkish multiword-token table is missing from the assembly.");
        }

        var table = new Dictionary<string, int>(StringComparer.Ordinal);
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line)
        {
            int tab = line.IndexOf('\t');
            if (tab < 2 || tab > MaxSuffixKey || !int.TryParse(line.AsSpan(tab + 1), out int cut) || cut < 0 || cut > tab)
            {
                throw new InvalidDataException("A line of the Turkish multiword-token table is not 'ending<TAB>length'.");
            }

            table[line[..tab]] = cut;
            longest = Math.Max(longest, tab);
        }

        return table.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
