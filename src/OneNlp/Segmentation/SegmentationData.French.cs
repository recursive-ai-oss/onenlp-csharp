using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace RecursiveAI.OneNlp.Segmentation;

public sealed partial class SegmentationData
{
    private static SegmentationData? s_french;

    /// <summary>
    /// French, after the UD French guidelines (universaldependencies.org/fr/overview/tokenization.html) and the way UD French-GSD
    /// tokenizes: the neutral defaults plus
    /// elision (<c>l'</c> <c>homme</c>, <c>qu'</c> <c>on</c>, <c>jusqu'</c> <c>au</c>, with <c>aujourd'hui</c> and <c>quelqu'un</c> whole),
    /// subject and object pronouns after a verb split off at their hyphen (<c>a</c> <c>-t-il</c>, <c>dis</c> <c>-moi</c>; <c>peut-être</c> and
    /// <c>lui-même</c> stay whole),
    /// and the contractions <c>du</c>, <c>des</c>, <c>au</c>, <c>aux</c> (and <c>auquel</c>, <c>duquel</c>, …) cut into the pieces of the
    /// multiword token they are, which the treebank expands to <c>de le</c>, <c>de les</c>, <c>à le</c>, <c>à les</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A contraction is a multiword token, so its words need characters of their own: the surface form is cut into pieces of nearly equal
    /// length, longer first (<c>du</c> is <c>d</c> + <c>u</c>, <c>des</c> is <c>de</c> + <c>s</c>), the same cut that <c>ConllUReader</c> makes of a
    /// multiword token. A model trained on a treebank read that way tags the pieces as the words they stand for.
    /// </para>
    /// <para>
    /// <c>des</c> is a contraction of <c>de les</c> after a noun (<c>la plupart des</c>) and the plural indefinite article, one word, after a
    /// verb, preposition or punctuation (<c>avec des</c>, <c>, des</c>). The data lists the words after which it is the article;
    /// that list is drawn from the training split of UD French-GSD only (`tools/fetch/fr/des_context.py` prints it: words that precede the article at least twice and 1.5 times as often as the contraction), plus a few function words (<c>et</c> was added on the development split). The other contractions split always.
    /// </para>
    /// </remarks>
    public static SegmentationData French => s_french ??= BuildFrench();

    /// <summary>Digits in groups of three separated by a space (<c>25 785</c>) are one token.</summary>
    internal bool GroupDigits { get; private init; }

    /// <summary>A minus sign before a number, after white space, belongs to the number.</summary>
    internal bool NegativeNumbers { get; private init; }

    /// <summary>A capital letter and its full stop before another capitalized word (<c>V. Prebeg</c>) are one token.</summary>
    internal bool InitialLetters { get; private init; }

    /// <summary>Hours written with an <c>h</c> (<c>20h30</c>) split into the number, the <c>h</c> and the minutes.</summary>
    internal bool HourMarker { get; private init; }

    /// <summary>Words (without the apostrophe) that elide before a vowel: the text after the apostrophe is a token of its own.</summary>
    internal FrozenSet<string> ElisionPrefixes { get; private init; } = Set();

    /// <summary>Pronouns that a hyphen attaches after a verb and that split off with the hyphen (<c>-il</c>, <c>-t-il</c>).</summary>
    internal FrozenSet<string> HyphenClitics { get; private init; } = Set();

    /// <summary>Demonstrative particles (<c>-ci</c>, <c>-là</c>) that split off unless the word before them is one of <see cref="HyphenParticleStems"/>.</summary>
    internal FrozenSet<string> HyphenParticles { get; private init; } = Set();

    /// <summary>Words that keep a following <c>-ci</c> or <c>-là</c> (<c>celui-ci</c>, <c>jusque-là</c>).</summary>
    internal FrozenSet<string> HyphenParticleStems { get; private init; } = Set();

    /// <summary>Whole-token contractions that split in some contexts only, keyed by text: the pieces, and the words after which the token is one word.</summary>
    internal FrozenDictionary<string, ContextualSplit> ContextualSplits { get; private init; } =
        new Dictionary<string, ContextualSplit>().ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>The words of each multiword token the tokenizer cuts into pieces, keyed by its surface text in lower case (French <c>du</c>: <c>de</c>, <c>le</c>).</summary>
    internal FrozenDictionary<string, string[]> MultiwordForms { get; private init; } =
        new Dictionary<string, string[]>().ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Finds out whether the tokens starting at <paramref name="start"/> are the pieces of one multiword token (French <c>d</c> + <c>u</c> for <c>du</c>)
    /// and gives the words they stand for.
    /// </summary>
    /// <param name="doc">The document the tokenizer made.</param>
    /// <param name="start">The index of the first candidate token.</param>
    /// <param name="count">The number of tokens (pieces) in the group.</param>
    /// <param name="words">The words of the multiword token, spelled as the surface is cased: <c>De</c>, <c>le</c> for <c>Du</c>.</param>
    /// <returns><see langword="true"/> when the pieces form a multiword token of this language's data; always <see langword="false"/> where the data has none.</returns>
    public bool TryMatchMultiword(Doc doc, int start, out int count, [NotNullWhen(true)] out string[]? words)
    {
        ArgumentNullException.ThrowIfNull(doc);
        count = 0;
        words = null;
        if (MultiwordForms.Count == 0 || start < 0 || start + 1 >= doc.Length)
        {
            return false;
        }

        // Every multiword token of the data has two words, and the pieces are consecutive slices of the surface.
        Token first = doc[start];
        Token second = doc[start + 1];
        int length = second.EndIdx - first.Idx;
        if (first.EndIdx != second.Idx || length > 12
            || !MultiwordForms.TryGetValue(doc.Text.Substring(first.Idx, length), out string[]? forms))
        {
            return false;
        }

        count = 2;
        words = (string[])forms.Clone();
        if (char.IsUpper(doc.Text[first.Idx]))
        {
            words[0] = char.ToUpperInvariant(words[0][0]) + words[0][1..];
        }

        return true;
    }

    /// <summary>A token that is a contraction after most words and a single word after the listed ones.</summary>
    internal sealed record ContextualSplit(int[] Pieces, FrozenSet<string> SingleWordAfter);

    private static FrozenSet<string> Set(params string[] values) => values.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static SegmentationData BuildFrench() => new(
        keepUrlsAndEmails: true, joinHyphenated: true, hyphenPrefixes: null, digitJoiners: DigitJoinerMarks, markRuns: MarkRunChars, emoticons: CommonEmoticons,
        // Multiword-token contractions (UD French: au = à + le, du = de + le, aux = à + les, des = de + les, auquel = à + lequel, …).
        // The numbers are the lengths of the pieces in UTF-16 units. "l'on" is one word in the treebank.
        specialCases: new Dictionary<string, int[]>
        {
            ["du"] = [1, 1],
            ["au"] = [1, 1],
            ["aux"] = [2, 1],
            ["duquel"] = [3, 3],
            ["auquel"] = [3, 3],
            ["desquels"] = [4, 4],
            ["desquelles"] = [5, 5],
            ["auxquels"] = [4, 4],
            ["auxquelles"] = [5, 5],
            ["l'on"] = [4],
            ["l\u2019on"] = [4],
            ["rendez-vous"] = [11],
            ["c'est-à-dire"] = [12],
            ["c’est-à-dire"] = [12],
        },
        // A listed word keeps its full stop (UD French keeps "M." and "etc." as one token).
        abbreviations:
        [
            "m", "mm", "mme", "mmes", "mlle", "mlles", "dr", "pr", "st", "ste", "sts", "stes", "cf", "env", "etc", "av", "apr", "vol", "fig", "art",
            "boul", "bd", "p", "pp", "janv", "f\u00e9vr", "avr", "juil", "sept", "oct", "nov", "d\u00e9c", "n\u00b0",
        ],
        clitics: [], nTSuffix: false,
        nonFinalAbbreviations: ["m.", "mm.", "mme.", "mmes.", "mlle.", "dr.", "pr.", "st.", "ste.", "cf.", "env.", "av.", "apr.", "boul.", "bd.", "n\u00b0.", "vol.", "fig.", "art."])
    {
        Name = "french",
        GroupDigits = true,
        NegativeNumbers = true,
        InitialLetters = true,
        HourMarker = true,
        ElisionPrefixes = Set("c", "d", "j", "l", "m", "n", "s", "t", "qu", "jusqu", "lorsqu", "puisqu", "quoiqu"),
        HyphenClitics = Set(
            "je", "tu", "il", "elle", "on", "nous", "vous", "ils", "elles", "ce", "moi", "toi", "lui", "leur", "eux",
            "le", "la", "les", "en", "y", "t-il", "t-elle", "t-on", "t-ils", "t-elles"),
        MultiwordForms = new Dictionary<string, string[]>
        {
            ["du"] = ["de", "le"],
            ["des"] = ["de", "les"],
            ["au"] = ["à", "le"],
            ["aux"] = ["à", "les"],
            ["duquel"] = ["de", "lequel"],
            ["auquel"] = ["à", "lequel"],
            ["desquels"] = ["de", "lesquels"],
            ["desquelles"] = ["de", "lesquelles"],
            ["auxquels"] = ["à", "lesquels"],
            ["auxquelles"] = ["à", "lesquelles"],
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase),
        HyphenParticles = Set("ci", "l\u00e0"),
        HyphenParticleStems = Set("celui", "celle", "celles", "ceux", "jusque", "par", "l\u00e0", "ci"),
        ContextualSplits = new Dictionary<string, ContextualSplit>
        {
            ["des"] = new([2, 1], Set(
                "a", "acheter", "ainsi", "ajouter", "alors", "appelant", "appuyer", "après", "arbore", "atteignent", "aussi", "avait", "avec",
                "avoir", "calculer", "car", "chez", "comme", "connaître", "contenant", "contient", "contre", "dans", "demanda", "depuis", "devant",
                "deviennent", "donc", "donnant", "donner", "donné", "dont", "durant", "découvrir", "développer", "développé", "effectuait",
                "effectuer", "encore", "entendu", "entraîne", "entraîner", "entre", "envoyer", "envoyé", "essentiellement", "est", "et", "eu",
                "exemple", "fabriquer", "faire", "fait", "fixe", "fixer", "fois", "font", "forme", "fournir", "fournissant", "furent",
                "généralement", "lancer", "mais", "moyennant", "mène", "ni", "obtenir", "obtenu", "offre", "offrent", "offrir", "ont", "organise",
                "organiser", "ou", "outre", "où", "par", "parfois", "pas", "permet", "porte", "poser", "pour", "prendre", "produit", "proférer",
                "propose", "proposer", "provoquer", "prélever", "présentent", "publie", "puis", "que", "regroupant", "regroupe", "représentant",
                "retrouve", "reçoit", "reçu", "réaliser", "réalisé", "récolter", "régulièrement", "sans", "selon", "soit", "sont", "sous", "souvent",
                "suit", "suivi", "sur", "toujours", "traversant", "trouve", "trouver", "utilise", "utilisent", "utiliser", "vendre", "vers", "voir",
                "à", "également", "élever", "établir", "étaient", "être")),
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase),
    };
}
