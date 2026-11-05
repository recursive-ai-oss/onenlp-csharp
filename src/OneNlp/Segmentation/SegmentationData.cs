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
public sealed partial class SegmentationData
{
    private static readonly char[] NoMarks = [];
    private static readonly char[] DigitJoinerMarks = [':', '/'];
    private static readonly char[] MarkRunChars = ['.', '-', '!', '?', '*', '=', '_', '~', '<', '>', '#', '$', '+', '/', '\\'];
    // German runs of the same mark are one token, except ! and ?, which UD German writes one by one; `` and '' stand for quotation marks.
    private static readonly char[] GermanMarkRunChars = ['.', '-', '*', '=', '_', '~', '<', '>', '#', '$', '+', '/', '\\', '`', '\''];
    private static readonly string[] CommonEmoticons = [":-)", ":)", ":-(", ":(", ";-)", ";)", ":-D", ":D", ":-P", ":P", ":'(", ":-/", ":/", ":-O", ":O", "<3", "=)", "=D", "^_^"];

    /// <summary>The name of the built-in data set: <c>none</c>, <c>neutral</c>, <c>english</c>, <c>french</c>, <c>german</c>, <c>portuguese</c>, <c>dutch</c>, <c>turkish</c> or <c>spanish</c> or <c>spanish</c>. Saved pipelines record it.</summary>
    public string Name { get; private init; } = string.Empty;

    /// <summary>Finds a built-in data set by its <see cref="Name"/>.</summary>
    /// <param name="name"><c>none</c>, <c>neutral</c>, <c>english</c>, <c>french</c>, <c>german</c>, <c>portuguese</c>, <c>dutch</c>, <c>turkish</c> or <c>spanish</c> or <c>spanish</c>.</param>
    /// <returns>The data set.</returns>
    /// <exception cref="ArgumentException">There is no such data set.</exception>
    public static SegmentationData ByName(string name) => name switch
    {
        "none" => None,
        "neutral" => Neutral,
        "english" => English,
        "french" => French,
        "german" => German,
        "portuguese" => Portuguese,
        "dutch" => Dutch,
        "turkish" => Turkish,
        "spanish" => Spanish,
        _ => throw new ArgumentException($"Unknown segmentation data '{name}'; expected none, neutral, english, french, german, portuguese, dutch, turkish or spanish.", nameof(name)),
    };

    /// <summary>No adjustments: tokens are the UAX #29 word segments that are not white space.</summary>
    public static SegmentationData None { get; } = new(
        keepUrlsAndEmails: false, joinHyphenated: false, hyphenPrefixes: null, digitJoiners: NoMarks, markRuns: NoMarks, emoticons: [],
        specialCases: [], abbreviations: [], clitics: [], nTSuffix: false, nonFinalAbbreviations: []) { Name = "none" };

    /// <summary>
    /// Language-neutral defaults: URLs and e-mail addresses stay whole, a hyphen between two letters or digits does not split,
    /// <c>:</c> and <c>/</c> between digits do not split (<c>3:30</c>, <c>1/2</c>), and runs of the same mark (<c>...</c>, <c>--</c>)
    /// are one token.
    /// </summary>
    public static SegmentationData Neutral { get; } = new(
        keepUrlsAndEmails: true, joinHyphenated: true, hyphenPrefixes: null, digitJoiners: DigitJoinerMarks, markRuns: MarkRunChars, emoticons: CommonEmoticons,
        specialCases: [], abbreviations: [], clitics: [], nTSuffix: false, nonFinalAbbreviations: []) { Name = "neutral" };

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
        ]) { Name = "english" };


    /// <summary>
    /// German: the neutral defaults plus UD German tokenization. The contractions of a preposition and an article (<c>zum</c>, <c>im</c>,
    /// <c>vom</c>) are split in two pieces, which the treebank annotates as a multiword token (<c>zu</c> + <c>dem</c>); abbreviations
    /// keep their full stop (<c>Dr.</c>, <c>Nr.</c>, <c>z.B.</c>, initials); and a number followed by a full stop is one token when it is an
    /// ordinal (<c>3. Oktober</c>) and two when the full stop ends the sentence.
    /// </summary>
    public static SegmentationData German { get; } = new(
        // UD German-GSD splits a hyphen between letters off (US-Soldaten is US, -, Soldaten) and keeps it between digits (1-2).
        keepUrlsAndEmails: true, joinHyphenated: true, hyphenPrefixes: [], digitJoiners: DigitJoinerMarks, markRuns: GermanMarkRunChars, emoticons: CommonEmoticons,
        // Pieces are cut in nearly equal lengths, longer first, which is how the CoNLL-U reader cuts the surface form of a multiword token
        // whose words do not concatenate to it (docs/data-formats.md): zum is zu|m, beim is be|im.
        specialCases: new Dictionary<string, int[]>
        {
            ["zum"] = [2, 1], ["zur"] = [2, 1], ["im"] = [1, 1], ["am"] = [1, 1], ["vom"] = [2, 1], ["beim"] = [2, 2],
            ["ins"] = [2, 1], ["ans"] = [2, 1], ["ums"] = [2, 1], ["aufs"] = [2, 2], ["übers"] = [3, 2],
            ["durchs"] = [3, 3], ["fürs"] = [2, 2], ["hinterm"] = [4, 3], ["überm"] = [3, 2], ["unterm"] = [3, 3], ["vorm"] = [2, 2],
        },
        // Words that UD German-GSD keeps joined to their full stop more often than not (counted on its training file; usw. and bzw. are
        // split there), a few usual ones that file does not contain, and single letters (z. B., initials).
        abbreviations:
        [
            "dr", "prof", "st", "ca", "etc", "inc", "co", "ltd", "mr", "mrs", "jap", "engl", "lat", "ital", "dt", "gr", "nr", "str", "vgl", "ggf", "inkl",
            "hrsg", "geb", "mio", "mrd", "sog", "jh", "tel", "ph.d",
            "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z",
        ],
        // geht's is geht + 's.
        clitics: ["s"], nTSuffix: false,
        // The sentencizer never starts a sentence after these: titles and abbreviations that are followed by a name or a noun.
        nonFinalAbbreviations:
        [
            "dr.", "prof.", "st.", "nr.", "str.", "ca.", "bzw.", "evtl.", "ggf.", "vgl.", "inkl.", "sog.", "max.", "min.", "jun.", "sen.", "hrsg.", "geb.", "gest.",
            "z.b.", "u.a.", "d.h.", "z.", "u.", "d.", "v.", "mr.", "mrs.", "abs.", "kap.", "bd.",
        ])
    {
        Name = "german",
        OrdinalDot = true,
        SameMarkRuns = true,
        OrdinalFollowers = new[] { "januar", "februar", "märz", "april", "mai", "juni", "juli", "august", "september", "oktober", "november", "dezember",
            "quartal", "jahrhundert", "jahrtausend", "weltkrieg", "platz", "liga", "bundesliga", "stock", "og", "klasse", "mal", "auflage", "runde", "etappe", "spieltag", "tag" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
    };

    /// <summary>
    /// Spanish: the neutral defaults plus UD Spanish tokenization. The contractions <c>del</c> and <c>al</c> and verbs with enclitic
    /// pronouns (<c>dárselo</c>, <c>conseguirlo</c>, <c>situándose</c>) are cut into one piece for each word of the multiword token the treebank
    /// annotates (<c>de</c> + <c>el</c>, <c>dar</c> + <c>se</c> + <c>lo</c>); a few abbreviations keep their full stop (<c>Sr.</c>, <c>fig.</c>, <c>EE.UU.</c>,
    /// single capital letters). <see cref="TryMatchMultiword"/> gives the words of such a token.
    /// </summary>
    public static SegmentationData Spanish { get; } = CreateSpanish();

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

    /// <summary>Whether a number followed by a full stop is one token (an ordinal, as in German <c>3. Oktober</c>) when what follows says so.</summary>
    internal bool OrdinalDot { get; private init; }

    /// <summary>Capitalised words (lower case here) after which a number and a full stop is an ordinal; a lower-case word always is.</summary>
    internal FrozenSet<string> OrdinalFollowers { get; private init; } = FrozenSet<string>.Empty;

    /// <summary>Whether a run of marks is made of one mark repeated (<c>...</c>) rather than any mix of them (<c>.''</c> is then a full stop and a quote).</summary>
    internal bool SameMarkRuns { get; private init; }


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

    /// <summary>Picks the data for a language: English, French, German, Portuguese, Dutch, Turkish and Spanish have their own; every other language gets the neutral defaults.</summary>
    /// <param name="language">The language.</param>
    /// <returns>The data; never <see langword="null"/>.</returns>
    public static SegmentationData For(Language language) =>
        language == Language.English ? English : language == Language.French ? French : language == Language.German ? German
        : language == Language.Portuguese ? Portuguese : language == Language.Dutch ? Dutch : language == Language.Turkish ? Turkish : language == Language.Spanish ? Spanish : Neutral;
}
