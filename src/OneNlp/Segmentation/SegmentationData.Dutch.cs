using System.Collections.Frozen;

namespace RecursiveAI.OneNlp.Segmentation;

public sealed partial class SegmentationData
{
    private static SegmentationData? s_dutch;

    // Dutch runs of the same mark are one token (`''` and `,,` stand for quotation marks, `...` is an ellipsis); ! and ? stay single, as in
    // the other UD treebanks of the family. The counts are from the training and development files of UD Dutch-Alpino.
    private static readonly char[] DutchMarkRunChars = ['.', '-', '*', '=', '_', '~', '<', '>', '#', '$', '+', '/', '\\', '`', '\'', ','];

    /// <summary>
    /// Dutch, after the way UD Dutch-Alpino
    /// tokenizes: the neutral defaults plus
    /// apostrophe words kept whole (<c>zo'n</c>, <c>auto's</c>, <c>Jan's</c>, <c>z'n</c>), the short forms that start with an apostrophe
    /// (<c>'t</c>, <c>'s</c>, <c>'n</c>, <c>'m</c>, <c>'ns</c>) and two-digit years (<c>'69</c>) as tokens of their own,
    /// hyphenated compounds and scores whole (<c>Zuid-Afrika</c>, <c>21-jarige</c>, <c>2-0</c>),
    /// abbreviations that keep their full stop (<c>mr.</c>, <c>drs.</c>, <c>o.a.</c>, <c>N.V.</c>, initials such as <c>J.</c>),
    /// and amounts written with a trailing dash (<c>5,-</c>, <c>130.000,-</c>) as one token.
    /// </summary>
    /// <remarks>
    /// Alpino has no contractions the tokenizer would cut (its one multiword token in the training and development files is <c>nogeens</c>), so
    /// every token is one word. The rules were drawn from the counts in the training and development files only
    /// (<c>tools/fetch/nl/token_stats.py</c> prints them).
    /// </remarks>
    public static SegmentationData Dutch => s_dutch ??= BuildDutch();

    /// <summary>Words (lower case, without the apostrophe) that a leading apostrophe joins into one token (<c>'t</c>, <c>'s</c>).</summary>
    internal FrozenSet<string> LeadingApostropheWords { get; private init; } = Set();

    /// <summary>Whether an apostrophe and two digits (<c>'69</c>, a year with its century left out) are one token.</summary>
    internal bool ApostropheYears { get; private init; }

    /// <summary>Whether a number followed by <c>,-</c> or <c>.-</c> (<c>5,-</c>, <c>130.000,-</c>, whole guilders) is one token.</summary>
    internal bool DashAmounts { get; private init; }

    private static SegmentationData BuildDutch() => new(
        keepUrlsAndEmails: true, joinHyphenated: true, hyphenPrefixes: null, digitJoiners: DigitJoinerMarks, markRuns: DutchMarkRunChars, emoticons: CommonEmoticons,
        specialCases: [],
        // The words that Alpino keeps joined to their full stop; dotted single letters (o.a., b.v., t.o.v., N.V.) are recognized by shape.
        // Single letters are the initials of names (J. Bakker).
        abbreviations:
        [
            "mr", "dr", "drs", "ir", "prof", "mevr", "mej", "dhr", "jhr", "ds", "mgr", "st", "kon", "gebr", "burg", "wm", "ch", "th", "fa", "co", "inc", "ltd", "mij",
            "pct", "mln", "mld", "jl", "nl", "zg", "zgn", "ca", "sec", "enz", "etc", "gr", "jr", "sr", "nr", "bijv", "resp", "soc", "dec", "nov", "ver", "ned", "blz",
            "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z",
        ],
        clitics: [], nTSuffix: false,
        // No sentence starts after a title, a dotted abbreviation or an initial that is followed by a capital letter (mr. Van der Meer, J. Bakker).
        nonFinalAbbreviations:
        [
            "mr.", "dr.", "drs.", "ir.", "prof.", "mevr.", "mej.", "dhr.", "jhr.", "ds.", "mgr.", "st.", "kon.", "gebr.", "burg.", "wm.", "ch.", "th.", "ned.", "ver.",
            "v.d.", "v.", "o.a.", "t.o.v.", "a.", "b.", "c.", "d.", "e.", "f.", "g.", "h.", "i.", "j.", "k.", "l.", "m.", "n.", "o.", "p.", "q.", "r.", "s.", "t.",
            "u.", "w.", "x.", "y.", "z.",
        ])
    {
        Name = "dutch",
        SameMarkRuns = true,
        LeadingApostropheWords = Set("t", "s", "n", "m", "ns"),
        ApostropheYears = true,
        DashAmounts = true,
    };
}
