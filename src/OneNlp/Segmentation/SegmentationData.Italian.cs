using System.Collections.Frozen;

namespace RecursiveAI.OneNlp.Segmentation;

// Italian tokenization data. Written from the UD Italian guidelines; the multiword-token table and the lists below were counted on the
// training and development files of UD Italian-ParlaMint and Italian-MarkIT only (tools/fetch/it/mwt_counts.py).
public sealed partial class SegmentationData
{
    // The enclitic pronouns that attach to an infinitive or a gerund (fare + lo = farlo, vedere + si = vedersi). Longest first.
    private static readonly string[] ItalianEncliticPronouns = ["gli", "mi", "ti", "ci", "vi", "si", "lo", "la", "li", "le", "ne"];

    private static readonly string[] ItalianFirstPronouns = ["me", "te", "ce", "ve", "se"];
    private static readonly string[] ItalianSecondPronouns = ["lo", "la", "li", "le", "ne"];

    // Words that look like an infinitive with its final -e dropped plus an enclitic (par + la = parla, car + ne = carne) but are single words.
    private static string[] NotItalianEncliticList =>
    [
        "diversi", "rapporti", "porti", "esterne", "esterni", "interne", "interni", "moderne", "moderni", "eterne", "eterni", "certi", "versi", "dispersi",
        "commerci", "arrivederci", "merci", "affermi", "alberti", "parti", "fermi", "esperti", "nervi", "reparti", "persi", "sparsi", "scarsi", "inversi",
        "supporti", "trasporti", "riporti", "apporti", "importi", "esporti", "sorti", "quarti", "scarti", "carti", "marmi", "parmi", "armi", "larvi",
        "parla", "parli", "parlo", "parle", "carne", "carni", "carlo", "carla", "carli", "carle", "perla", "perle", "perli", "perlo", "marne", "marlo",
        "barlo", "barla", "barca", "arte", "orla", "orli", "orlo", "erle", "erli", "erla", "irla", "irli", "irlo", "stirla", "scarlo", "scarla",
    ];

    // An expression, not a stored value: the Italian data is built while the other part of this class is being initialized.
    private static FrozenSet<string> PartitiveContext => Set(
        "sono", "a", "con", "avere", "creare", "accettano", "predisponendo", "state", "ho", "siamo", "piangere", "da", "toccate", "consegnandoci",
        "rivelate", "erano", "crea", "creando", "come", "essere", "otterranno", "assumersi", "diventare", "propose", "abbiamo", "esistere",
        "costruire", "per", "su", "senza", "hanno", "ha", "fare", "fatto", "avuto", "avevano", "aveva", "era", "erano", "fu", "furono");

    // Italian data; see the Italian property. Pieces are cut like the CoNLL-U reader cuts a multiword token (docs/data-formats.md): at the word
    // boundaries when the words spell the surface form (far + lo), otherwise in nearly equal lengths (di + l' over dell' is del|l').
    internal static SegmentationData CreateItalian()
    {
        // (surface, words): the preposition (or its stem) joined with an article. The apostrophe forms get a typographic twin.
        (string, string[])[] table =
        [
            ("del", ["di", "il"]), ("dello", ["di", "lo"]), ("della", ["di", "la"]), ("dell'", ["di", "l'"]), ("dei", ["di", "i"]), ("degli", ["di", "gli"]), ("delle", ["di", "le"]),
            ("al", ["a", "il"]), ("allo", ["a", "lo"]), ("alla", ["a", "la"]), ("all'", ["a", "l'"]), ("ai", ["a", "i"]), ("agli", ["a", "gli"]), ("alle", ["a", "le"]),
            ("dal", ["da", "il"]), ("dallo", ["da", "lo"]), ("dalla", ["da", "la"]), ("dall'", ["da", "l'"]), ("dai", ["da", "i"]), ("dagli", ["da", "gli"]), ("dalle", ["da", "le"]),
            ("nel", ["in", "il"]), ("nello", ["in", "lo"]), ("nella", ["in", "la"]), ("nell'", ["in", "l'"]), ("nei", ["in", "i"]), ("negli", ["in", "gli"]), ("nelle", ["in", "le"]),
            ("sul", ["su", "il"]), ("sullo", ["su", "lo"]), ("sulla", ["su", "la"]), ("sull'", ["su", "l'"]), ("sui", ["su", "i"]), ("sugli", ["su", "gli"]), ("sulle", ["su", "le"]),
            ("col", ["con", "il"]), ("coi", ["con", "i"]), ("collo", ["con", "lo"]), ("colla", ["con", "la"]), ("coll'", ["con", "l'"]),
        ];
        var contractions = new Dictionary<string, string[]>();
        foreach ((string surface, string[] words) in table)
        {
            contractions[surface] = words;
            if (surface.EndsWith('\''))
            {
                contractions[surface[..^1] + "\u2019"] = words;
            }
        }

        return new(
            keepUrlsAndEmails: true, joinHyphenated: true, hyphenPrefixes: null, digitJoiners: DigitJoinerMarks, markRuns: MarkRunChars, emoticons: CommonEmoticons,
            specialCases: [],
            // Single capital letters and the everyday titles and abbreviations keep their full stop in the token (UD guidelines).
            abbreviations:
            [
                "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z",
                "sig", "sigg", "dott", "dr", "prof", "ing", "avv", "on", "sen", "art", "artt", "pag", "pagg", "ecc", "etc", "cfr", "tel", "vs", "fig",
            ],
            clitics: [], nTSuffix: false,
            nonFinalAbbreviations: ["sig.", "sigg.", "dott.", "dr.", "prof.", "ing.", "avv.", "on.", "sen.", "art.", "artt.", "pag.", "pagg.", "cfr.", "vs.", "fig."])
        {
            Name = "italian",
            Contractions = contractions.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase),
            ItalianEncliticVerbs = true,
            ElisionBeforeSpace = true,
            ContextualSplits = new Dictionary<string, ContextualSplit>
            {
                // The partitive article ("ci sono dei bambini") is one word; after a noun the same text is di + i ("l'elenco dei bambini").
                // The words listed are those that precede the article in the training and development files at least once more often
                // than the contraction (tools/fetch/it/partitive_context.py); punctuation and the start of a text count as before an article.
                ["dei"] = new([2, 1], PartitiveContext),
                ["delle"] = new([2, 2], PartitiveContext),
                ["degli"] = new([2, 3], PartitiveContext),
            }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase),
            NotEncliticVerbs = NotItalianEncliticList.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
            // The elided article, preposition or demonstrative stays with its apostrophe and the next word is a token of its own
            // (l'amico, un'altra, c'è, quest'anno); words such as po' stay whole because nothing follows the apostrophe.
            ElisionPrefixes = Set("c", "l", "d", "un", "quest", "quell", "cos", "anch", "tutt", "dov", "com", "quant", "bell", "buon", "sant", "nessun", "senz", "mezz",
                "cinquant", "trent", "quarant", "po", "e", "m", "t", "s", "v", "n", "dell", "all", "dall", "nell", "sull", "coll", "dev"),
        };
    }

    // The words of a verb form with enclitic pronouns, a multiword token in UD Italian: farlo is far + lo, vedersi is veder + si, essendoci is
    // essendo + ci, accorgercene is accorger + ce + ne (the infinitive loses its final -e), affrontiamolo is affrontiamo + lo, sviluppatasi is
    // sviluppata + si. The stem must be an infinitive (-ar, -er, -ir, -por), a gerund (-ando, -endo), a first-person-plural or second-person-plural
    // imperative (-iamo, -ete, -ate, -ite) or a participle before si, and the word must not be one the treebank writes whole.
    private bool TryItalianEncliticVerb(ReadOnlySpan<char> token, out string[] words)
    {
        words = [];
        if (token.Length < 5)
        {
            return false;
        }

        foreach (char c in token)
        {
            if (!char.IsLetter(c))
            {
                return false;
            }
        }

        string lower = token.ToString().ToLowerInvariant();
        if (NotEncliticVerbs.Contains(lower))
        {
            return false;
        }

        // Two pronouns: me/te/ce/ve/se before lo/la/li/le/ne (accorgercene, tenerselo).
        foreach (string second in ItalianSecondPronouns)
        {
            if (!lower.EndsWith(second, StringComparison.Ordinal) || lower.Length < second.Length + 2 + 3)
            {
                continue;
            }

            string first = lower.Substring(lower.Length - second.Length - 2, 2);
            if (Array.IndexOf(ItalianFirstPronouns, first) >= 0 && IsItalianVerbStem(lower[..^(second.Length + 2)], first))
            {
                words = CopyCase([lower[..^(second.Length + 2)], first, second], token);
                return true;
            }
        }

        foreach (string pronoun in ItalianEncliticPronouns)
        {
            if (lower.EndsWith(pronoun, StringComparison.Ordinal) && IsItalianVerbStem(lower[..^pronoun.Length], pronoun))
            {
                words = CopyCase([lower[..^pronoun.Length], pronoun], token);
                return true;
            }
        }

        return false;
    }

    private static bool IsItalianVerbStem(string stem, string pronoun)
    {
        if (stem.Length < 3)
        {
            return false;
        }

        // Plural nouns and adjectives end like an infinitive with a pronoun (diversi, certi, parti, esterne), the more so the shorter the stem, so
        // the pronouns that end such plurals (ti, mi, ci, vi, ne) need a longer stem; a few short irregular infinitives (farci, dirmi) are listed.
        bool plural = pronoun is "ti" or "mi" or "ci" or "vi" or "ne";
        bool infinitive = stem is "far" or "dar" or "dir" or "star" or "trar";
        if (stem.EndsWith("ar", StringComparison.Ordinal))
        {
            infinitive |= stem.Length >= (plural ? 5 : 4);
        }
        else if (stem.EndsWith("er", StringComparison.Ordinal) || stem.EndsWith("ir", StringComparison.Ordinal))
        {
            infinitive |= stem.Length >= (plural ? 6 : 4);
        }
        else if (stem.EndsWith("por", StringComparison.Ordinal))
        {
            infinitive |= !plural;
        }

        bool gerund = stem.Length >= 6 && (stem.EndsWith("ando", StringComparison.Ordinal) || stem.EndsWith("endo", StringComparison.Ordinal));
        bool imperative = stem.Length >= 6 && (stem.EndsWith("iamo", StringComparison.Ordinal) || stem.EndsWith("ete", StringComparison.Ordinal)
            || stem.EndsWith("ate", StringComparison.Ordinal) || stem.EndsWith("ite", StringComparison.Ordinal));
        bool participle = pronoun == "si" && stem.Length >= 6 && (stem.EndsWith("ato", StringComparison.Ordinal) || stem.EndsWith("ata", StringComparison.Ordinal)
            || stem.EndsWith("ati", StringComparison.Ordinal) || stem.EndsWith("ate", StringComparison.Ordinal) || stem.EndsWith("uto", StringComparison.Ordinal)
            || stem.EndsWith("uta", StringComparison.Ordinal) || stem.EndsWith("uti", StringComparison.Ordinal) || stem.EndsWith("ito", StringComparison.Ordinal)
            || stem.EndsWith("ita", StringComparison.Ordinal) || stem.EndsWith("iti", StringComparison.Ordinal));
        return infinitive || gerund || imperative || participle;
    }

    /// <summary>An elision prefix followed by an apostrophe and a space (<c>l' idea</c>) is one token with its apostrophe, as in the treebank text.</summary>
    internal bool ElisionBeforeSpace { get; private init; }

    /// <summary>Whether an infinitive or a gerund with an enclitic pronoun is a multiword token the Italian way (the infinitive loses its final <c>e</c>).</summary>
    internal bool ItalianEncliticVerbs { get; private init; }
}
