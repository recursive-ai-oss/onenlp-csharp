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
    // German runs of the same mark are one token, except ! and ?, which UD German writes one by one; `` and '' stand for quotation marks.
    private static readonly char[] GermanMarkRunChars = ['.', '-', '*', '=', '_', '~', '<', '>', '#', '$', '+', '/', '\\', '`', '\''];
    private static readonly string[] CommonEmoticons = [":-)", ":)", ":-(", ":(", ";-)", ";)", ":-D", ":D", ":-P", ":P", ":'(", ":-/", ":/", ":-O", ":O", "<3", "=)", "=D", "^_^"];

    /// <summary>The name of the built-in data set: <c>none</c>, <c>neutral</c>, <c>english</c>, <c>german</c> or <c>portuguese</c>. Saved pipelines record it.</summary>
    public string Name { get; private init; } = string.Empty;

    /// <summary>Finds a built-in data set by its <see cref="Name"/>.</summary>
    /// <param name="name"><c>none</c>, <c>neutral</c>, <c>english</c>, <c>german</c> or <c>portuguese</c>.</param>
    /// <returns>The data set.</returns>
    /// <exception cref="ArgumentException">There is no such data set.</exception>
    public static SegmentationData ByName(string name) => name switch
    {
        "none" => None,
        "neutral" => Neutral,
        "english" => English,
        "german" => German,
        "portuguese" => Portuguese,
        _ => throw new ArgumentException($"Unknown segmentation data '{name}'; expected none, neutral, english, german or portuguese.", nameof(name)),
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
    /// Portuguese: the neutral defaults plus UD Portuguese tokenization. The contractions of a preposition with an article, pronoun or
    /// demonstrative (<c>do</c>, <c>na</c>, <c>pelo</c>, <c>nesse</c>, <c>dele</c>, <c>aos</c>) are split in two pieces, which the treebank annotates as a
    /// multiword token (<c>de</c> + <c>o</c>); hyphenated words stay whole, but an enclitic pronoun after a verb is split off with its hyphen (<c>trata</c> <c>-se</c>, <c>dá</c> <c>-lo</c>);
    /// <c>US$</c> and <c>R$</c> are one token; abbreviations keep their full stop (<c>Dr.</c>, <c>art.</c>, initials).
    /// </summary>
    public static SegmentationData Portuguese { get; } = new(
        // UD Portuguese-Bosque keeps a hyphen between letters inside the word (quinta-feira, norte-americano, dá-lo).
        keepUrlsAndEmails: true, joinHyphenated: true, hyphenPrefixes: null, digitJoiners: DigitJoinerMarks, markRuns: GermanMarkRunChars, emoticons: CommonEmoticons,
        // The surface forms of Bosque's multiword tokens (counted on its training and development files; a form is listed when it is a multiword
        // token far more often than a single word). Pieces are cut as the CoNLL-U reader cuts them (docs/data-formats.md): the words when they
        // spell the form (ao is a|o), otherwise nearly equal lengths, longer first (do is d|o, pelo is pe|lo). The contraction à has one
        // character for two words, so it stays one token.
        specialCases: new Dictionary<string, int[]>
        {
            ["ao"] = [1, 1], ["aos"] = [1, 2], ["da"] = [1, 1], ["dali"] = [2, 2], ["dantes"] = [3, 3], ["daquela"] = [4, 3],
            ["daquelas"] = [4, 4], ["daquele"] = [4, 3], ["daqueles"] = [4, 4], ["daqui"] = [3, 2], ["daquilo"] = [4, 3], ["das"] = [2, 1],
            ["daí"] = [2, 1], ["dela"] = [2, 2], ["delas"] = [3, 2], ["dele"] = [2, 2], ["deles"] = [3, 2], ["dessa"] = [3, 2],
            ["dessas"] = [3, 3], ["desse"] = [3, 2], ["desses"] = [3, 3], ["desta"] = [3, 2], ["destas"] = [3, 3], ["deste"] = [3, 2],
            ["destes"] = [3, 3], ["disso"] = [3, 2], ["disto"] = [3, 2], ["do"] = [1, 1], ["dos"] = [2, 1], ["dum"] = [2, 1],
            ["duma"] = [2, 2], ["dumas"] = [3, 2], ["duns"] = [2, 2], ["na"] = [1, 1], ["nalgum"] = [3, 3], ["nalguma"] = [4, 3],
            ["nalgumas"] = [4, 4], ["nalguns"] = [4, 3], ["naquela"] = [4, 3], ["naquelas"] = [4, 4], ["naquele"] = [4, 3], ["naqueles"] = [4, 4],
            ["naquilo"] = [4, 3], ["nas"] = [2, 1], ["nela"] = [2, 2], ["nelas"] = [3, 2], ["nele"] = [2, 2], ["neles"] = [3, 2],
            ["nessa"] = [3, 2], ["nessas"] = [3, 3], ["nesse"] = [3, 2], ["nesses"] = [3, 3], ["nesta"] = [3, 2], ["nestas"] = [3, 3],
            ["neste"] = [3, 2], ["nestes"] = [3, 3], ["nisso"] = [3, 2], ["nisto"] = [3, 2], ["no"] = [1, 1], ["nos"] = [2, 1],
            ["noutra"] = [3, 3], ["noutras"] = [4, 3], ["noutro"] = [3, 3], ["noutros"] = [4, 3], ["num"] = [2, 1], ["numa"] = [2, 2],
            ["numas"] = [3, 2], ["nuns"] = [2, 2], ["pela"] = [2, 2], ["pelas"] = [3, 2], ["pelo"] = [2, 2], ["pelos"] = [3, 2],
            ["àquela"] = [3, 3], ["àquelas"] = [4, 3], ["àquele"] = [3, 3], ["àqueles"] = [4, 3], ["àquilo"] = [3, 3], ["às"] = [1, 1],
        },
        // Words Bosque keeps joined to their full stop (counted on its training file), and single letters (initials) except a, e and o.
        abbreviations:
        [
            "etc", "dr", "dra", "art", "arts", "tel", "jr", "pág", "págs", "av", "sr", "sra", "ed", "dom", "sáb", "inc", "cia", "fund", "nº", "eng", "mr", "mrs", "st",
            "cf", "al", "min", "bros", "séc", "sécs", "prof", "profa", "ltda", "vol", "fig", "obs", "gen", "cel", "pres", "e.g", "a.c", "d.c",
            "b", "c", "d", "f", "g", "h", "i", "j", "k", "l", "m", "n", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z",
        ],
        clitics: [], nTSuffix: false,
        // The sentencizer never starts a sentence after these: titles and abbreviations followed by a name or a noun.
        nonFinalAbbreviations:
        [
            "dr.", "dra.", "sr.", "sra.", "av.", "art.", "arts.", "tel.", "pág.", "págs.", "jr.", "dom.", "eng.", "cia.", "fund.", "prof.", "profa.", "cf.", "nº.", "mr.", "mrs.", "st.", "cel.", "gen.", "pres.",
        ])
    {
        Name = "portuguese",
        SameMarkRuns = true,
        // The letters that make a currency symbol with the dollar sign: US$, R$, CR$.
        DollarWords = new[] { "US", "R", "CR" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
        // The pronouns UD Portuguese-Bosque writes after a hyphen as a second word of the verb's multiword token (counted on its training and
        // development files: every hyphenated form that ends in one of these is such a token).
        Enclitics = new[] { "se", "me", "te", "lhe", "lhes", "nos", "vos", "o", "a", "os", "as", "lo", "la", "los", "las" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
    };

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

    /// <summary>Words (such as <c>US</c> and <c>R</c>) that take a following dollar sign into the same token.</summary>
    internal FrozenSet<string> DollarWords { get; private init; } = FrozenSet<string>.Empty;

    /// <summary>Pronouns (such as <c>se</c> and <c>lo</c>) that follow a verb after a hyphen (<c>trata-se</c>); the hyphen and the pronoun are split off as their own token (<c>-se</c>).</summary>
    internal FrozenSet<string> Enclitics { get; private init; } = FrozenSet<string>.Empty;

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

    /// <summary>Picks the data for a language: English and German have their own; every other language gets the neutral defaults.</summary>
    /// <param name="language">The language.</param>
    /// <returns>The data; never <see langword="null"/>.</returns>
    public static SegmentationData For(Language language) => language == Language.English ? English : language == Language.German ? German : language == Language.Portuguese ? Portuguese : Neutral;
}
