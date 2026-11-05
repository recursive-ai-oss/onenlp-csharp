using System.Collections.Frozen;

namespace RecursiveAI.OneNlp.Segmentation;

public sealed partial class SegmentationData
{
    private static SegmentationData? s_portuguese;

    /// <summary>
    /// Portuguese: the neutral defaults plus UD Portuguese tokenization. The contractions of a preposition with an article, pronoun or
    /// demonstrative (<c>do</c>, <c>na</c>, <c>pelo</c>, <c>nesse</c>, <c>dele</c>, <c>aos</c>) are split in two pieces, which the treebank annotates as a
    /// multiword token (<c>de</c> + <c>o</c>); hyphenated words stay whole, but an enclitic pronoun after a verb is split off with its hyphen (<c>trata</c> <c>-se</c>, <c>dá</c> <c>-lo</c>);
    /// <c>US$</c> and <c>R$</c> are one token; abbreviations keep their full stop (<c>Dr.</c>, <c>art.</c>, initials).
    /// </summary>
    public static SegmentationData Portuguese => s_portuguese ??= BuildPortuguese();

    /// <summary>Words (such as <c>US</c> and <c>R</c>) that take a following dollar sign into the same token.</summary>
    internal FrozenSet<string> DollarWords { get; private init; } = FrozenSet<string>.Empty;

    /// <summary>Pronouns (such as <c>se</c> and <c>lo</c>) that follow a verb after a hyphen (<c>trata-se</c>); the hyphen and the pronoun are split off as their own token (<c>-se</c>).</summary>
    internal FrozenSet<string> Enclitics { get; private init; } = FrozenSet<string>.Empty;

    private static SegmentationData BuildPortuguese() => new(
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
        DollarWords = Set("US", "R", "CR"),
        // The pronouns UD Portuguese-Bosque writes after a hyphen as a second word of the verb's multiword token (counted on its training and
        // development files: every hyphenated form that ends in one of these is such a token).
        Enclitics = Set("se", "me", "te", "lhe", "lhes", "nos", "vos", "o", "a", "os", "as", "lo", "la", "los", "las"),
        // The words behind each contraction piece pair (TryMatchMultiword); the keys are the forms of the special cases above.
        MultiwordForms = new Dictionary<string, string[]>
        {
            ["ao"] = ["a", "o"], ["aos"] = ["a", "os"], ["da"] = ["de", "a"], ["dali"] = ["de", "ali"],
            ["dantes"] = ["de", "antes"], ["daquela"] = ["de", "aquela"], ["daquelas"] = ["de", "aquelas"], ["daquele"] = ["de", "aquele"],
            ["daqueles"] = ["de", "aqueles"], ["daqui"] = ["de", "aqui"], ["daquilo"] = ["de", "aquilo"], ["das"] = ["de", "as"],
            ["daí"] = ["de", "aí"], ["dela"] = ["de", "ela"], ["delas"] = ["de", "elas"], ["dele"] = ["de", "ele"],
            ["deles"] = ["de", "eles"], ["dessa"] = ["de", "essa"], ["dessas"] = ["de", "essas"], ["desse"] = ["de", "esse"],
            ["desses"] = ["de", "esses"], ["desta"] = ["de", "esta"], ["destas"] = ["de", "estas"], ["deste"] = ["de", "este"],
            ["destes"] = ["de", "estes"], ["disso"] = ["de", "isso"], ["disto"] = ["de", "isto"], ["do"] = ["de", "o"],
            ["dos"] = ["de", "os"], ["dum"] = ["de", "um"], ["duma"] = ["de", "uma"], ["dumas"] = ["de", "umas"],
            ["duns"] = ["de", "uns"], ["na"] = ["em", "a"], ["nalgum"] = ["em", "algum"], ["nalguma"] = ["em", "alguma"],
            ["nalgumas"] = ["em", "algumas"], ["nalguns"] = ["em", "alguns"], ["naquela"] = ["em", "aquela"], ["naquelas"] = ["em", "aquelas"],
            ["naquele"] = ["em", "aquele"], ["naqueles"] = ["em", "aqueles"], ["naquilo"] = ["em", "aquilo"], ["nas"] = ["em", "as"],
            ["nela"] = ["em", "ela"], ["nelas"] = ["em", "elas"], ["nele"] = ["em", "ele"], ["neles"] = ["em", "eles"],
            ["nessa"] = ["em", "essa"], ["nessas"] = ["em", "essas"], ["nesse"] = ["em", "esse"], ["nesses"] = ["em", "esses"],
            ["nesta"] = ["em", "esta"], ["nestas"] = ["em", "estas"], ["neste"] = ["em", "este"], ["nestes"] = ["em", "estes"],
            ["nisso"] = ["em", "isso"], ["nisto"] = ["em", "isto"], ["no"] = ["em", "o"], ["nos"] = ["em", "os"],
            ["noutra"] = ["em", "outra"], ["noutras"] = ["em", "outras"], ["noutro"] = ["em", "outro"], ["noutros"] = ["em", "outros"],
            ["num"] = ["em", "um"], ["numa"] = ["em", "uma"], ["numas"] = ["em", "umas"], ["nuns"] = ["em", "uns"],
            ["pela"] = ["por", "a"], ["pelas"] = ["por", "as"], ["pelo"] = ["por", "o"], ["pelos"] = ["por", "os"],
            ["àquela"] = ["a", "aquela"], ["àquelas"] = ["a", "aquelas"], ["àquele"] = ["a", "aquele"], ["àqueles"] = ["a", "aqueles"],
            ["àquilo"] = ["a", "aquilo"], ["às"] = ["a", "as"],
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase),
    };
}
