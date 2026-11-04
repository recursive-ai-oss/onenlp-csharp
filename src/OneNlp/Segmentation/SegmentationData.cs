using System.Collections.Frozen;

namespace RecursiveAI.OneNlp.Segmentation;

/// <summary>
/// The per-language data that adjusts Unicode word and sentence segmentation (UAX #29) to the token boundaries of Universal
/// Dependencies treebanks. It follows the model spaCy documents for its tokenizer (special cases, prefixes, suffixes and infixes):
/// the code is language-neutral and a language adds data only.
/// </summary>
/// <remarks>
/// <see cref="None"/> switches every adjustment off, which leaves exactly the UAX #29 segments (minus white space).
/// Instances are immutable and safe to share between threads.
/// </remarks>
public sealed class SegmentationData
{
    private static readonly char[] NoMarks = [];
    private static readonly char[] DigitJoinerMarks = [':', '/'];
    private static readonly char[] MarkRunChars = ['.', '-', '!', '?', '*', '=', '_', '~', '<', '>', '#', '$', '+', '/', '\\'];
    private static readonly string[] CommonEmoticons = [":-)", ":)", ":-(", ":(", ";-)", ";)", ":-D", ":D", ":-P", ":P", ":'(", ":-/", ":/", ":-O", ":O", "<3", "=)", "=D", "^_^"];

    /// <summary>No adjustments: tokens are the UAX #29 word segments that are not white space.</summary>
    public static SegmentationData None { get; } = new(
        keepUrlsAndEmails: false, joinHyphenated: false, hyphenPrefixes: null, digitJoiners: NoMarks, markRuns: NoMarks, emoticons: [],
        specialCases: [], abbreviations: [], clitics: [], nTSuffix: false, nonFinalAbbreviations: []);

    /// <summary>
    /// Language-neutral defaults: URLs and e-mail addresses stay whole, a hyphen between two letters or digits does not split,
    /// <c>:</c> and <c>/</c> between digits do not split (<c>3:30</c>, <c>1/2</c>), and runs of the same mark (<c>...</c>, <c>--</c>)
    /// are one token.
    /// </summary>
    public static SegmentationData Neutral { get; } = new(
        keepUrlsAndEmails: true, joinHyphenated: true, hyphenPrefixes: null, digitJoiners: DigitJoinerMarks, markRuns: MarkRunChars, emoticons: CommonEmoticons,
        specialCases: [], abbreviations: [], clitics: [], nTSuffix: false, nonFinalAbbreviations: []);

    /// <summary>
    /// English: the neutral defaults plus UD English tokenization: contractions split at the apostrophe (<c>do</c> <c>n't</c>,
    /// <c>it</c> <c>'s</c>, <c>we</c> <c>'ll</c>), a few fused forms (<c>cannot</c>, <c>gonna</c>), and abbreviations that keep
    /// their full stop (<c>Mr.</c>, <c>U.S.</c>, <c>e.g.</c>).
    /// </summary>
    public static SegmentationData English { get; } = new(
        keepUrlsAndEmails: true, joinHyphenated: true,
        // UD English splits most hyphenated compounds (al-Sadr, 15-year, West-Coast) but keeps these prefixes attached (e-mail, anti-smoker).
        hyphenPrefixes: ["e", "re", "non", "anti", "vice", "semi", "over", "mis", "counter", "co", "ex", "pre", "post", "mid", "ultra", "multi", "sub", "inter", "pro"], digitJoiners: DigitJoinerMarks, markRuns: MarkRunChars, emoticons: CommonEmoticons,
        // UD English splits these fused forms into two words; the numbers are the lengths of the pieces in UTF-16 units.
        specialCases: new Dictionary<string, int[]>
        {
            ["cannot"] = [3, 3],
            ["gonna"] = [3, 2],
            ["gotta"] = [3, 2],
            ["wanna"] = [3, 2],
            ["gimme"] = [3, 2],
            ["lemme"] = [3, 2],
        },
        abbreviations:
        [
            "mr", "mrs", "ms", "dr", "prof", "sr", "jr", "st", "mt", "messrs", "gen", "col", "capt", "lt", "sgt", "rev", "hon", "gov", "sen", "rep",
            "inc", "ltd", "co", "corp", "vs", "etc", "approx", "dept", "fig", "vol", "jan", "feb", "aug", "sept", "oct", "nov", "dec",
            "a.m", "p.m", "e.g", "i.e", "u.s", "u.k", "ph.d", "d.c",
        ],
        // The suffixes after an apostrophe that UD splits off: 's (is, has or possessive), 're, 've, 'll, 'd, 'm.
        clitics: ["s", "re", "ve", "ll", "d", "m"],
        nTSuffix: true,
        // The sentencizer never starts a sentence after these, whatever the next word looks like.
        nonFinalAbbreviations:
        [
            "mr.", "mrs.", "ms.", "dr.", "prof.", "sr.", "jr.", "st.", "mt.", "messrs.", "gen.", "col.", "capt.", "lt.", "sgt.", "rev.", "hon.",
            "gov.", "sen.", "rep.", "vs.",
        ]);

    private SegmentationData(
        bool keepUrlsAndEmails, bool joinHyphenated, string[]? hyphenPrefixes, char[] digitJoiners, char[] markRuns, string[] emoticons, Dictionary<string, int[]> specialCases,
        string[] abbreviations, string[] clitics, bool nTSuffix, string[] nonFinalAbbreviations)
    {
        KeepUrlsAndEmails = keepUrlsAndEmails;
        JoinHyphenated = joinHyphenated;
        HyphenPrefixes = hyphenPrefixes?.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        DigitJoiners = digitJoiners;
        MarkRuns = markRuns;
        Emoticons = emoticons;
        EmoticonStarts = [.. emoticons.Select(e => e[0]).Distinct()];
        SpecialCases = specialCases.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        Abbreviations = abbreviations.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        Clitics = clitics.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        NTSuffix = nTSuffix;
        NonFinalAbbreviations = nonFinalAbbreviations.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    internal bool KeepUrlsAndEmails { get; }

    internal bool JoinHyphenated { get; }

    /// <summary>When not null, a hyphen joins only after one of these words; null joins after any word.</summary>
    internal FrozenSet<string>? HyphenPrefixes { get; }

    internal char[] DigitJoiners { get; }

    /// <summary>Characters whose runs (any mix, such as <c>...</c>, <c>--</c>, <c>?!</c>, <c>$$$</c>) are one token.</summary>
    internal char[] MarkRuns { get; }

    /// <summary>Emoticons kept whole, longest first.</summary>
    internal string[] Emoticons { get; }

    internal char[] EmoticonStarts { get; }

    /// <summary>Whole-token exceptions: the text, ignoring case, maps to the lengths of its pieces.</summary>
    internal FrozenDictionary<string, int[]> SpecialCases { get; }

    /// <summary>Words (without the full stop) that keep their full stop as part of the token.</summary>
    internal FrozenSet<string> Abbreviations { get; }

    /// <summary>Suffixes (without the apostrophe) split off a word as their own token.</summary>
    internal FrozenSet<string> Clitics { get; }

    /// <summary>Whether <c>n't</c> splits off, as in <c>do</c> <c>n't</c>.</summary>
    internal bool NTSuffix { get; }

    /// <summary>Tokens (with the full stop) after which the sentencizer never starts a sentence.</summary>
    internal FrozenSet<string> NonFinalAbbreviations { get; }

    /// <summary>Picks the data for a language: English has its own; every other language gets the neutral defaults.</summary>
    /// <param name="language">The language.</param>
    /// <returns>The data; never <see langword="null"/>.</returns>
    public static SegmentationData For(Language language) => language == Language.English ? English : Neutral;
}
