using System.Collections.Frozen;

namespace RecursiveAI.OneNlp.Segmentation;

// Spanish tokenization data. Written from the UD Spanish guidelines and the AnCora treebank; counts were taken on its training and
// development files only.
public sealed partial class SegmentationData
{
    private const int MaxMultiwordLength = 40;

    // The enclitic pronouns that attach to an infinitive or a gerund (dar + me + lo = dármelo). Longest first, so that "nos" is tried
    // before "os" would be; "os" itself is left out, because words in -ros and -tros (primeros, nuestros) are far more common than
    // second-person-plural enclitics.
    private static class SpanishEnclitics
    {
        internal static readonly string[] Pronouns = ["nos", "les", "las", "los", "me", "te", "se", "lo", "la", "le"];
    }

    // Words that look like a verb plus an enclitic pronoun (parte = par + te) but that AnCora's training file writes as one word more often
    // than as a multiword token.
    // A property, not a field: the Spanish data is built while the other part of this class is being initialized.
    private static string[] NotEncliticVerbList =>
    [
        "abandonos", "advierte", "apabullarnos", "aparte", "carla", "carles", "carlo", "carlos", "carme", "carmelo", "carteles", "charla", "charlas",
        "charles", "comparte", "confirme", "convierte", "cuadernos", "cuarteles", "cuernos", "desarme", "descarte", "despierte", "duarte", "duerme",
        "estandarte", "eternos", "externos", "firme", "fuerte", "giancarlo", "gobiernos", "guilherme", "imparte", "internos", "marte", "modernos",
        "montecarlo", "muerte", "olarte", "parte", "perla", "perlas", "suerte", "ugarte", "uriarte", "vierte",
    ];

    // Spanish data; see the Spanish property. Pieces are cut the way the CoNLL-U reader cuts the surface form of a multiword token
    // (docs/data-formats.md): at the word boundaries when the words spell the surface form (conseguir + lo), otherwise in nearly equal
    // lengths, longer first (de + el over del is de|l).
    internal static SegmentationData CreateSpanish() => new(
        keepUrlsAndEmails: true, joinHyphenated: true, hyphenPrefixes: null, digitJoiners: DigitJoinerMarks, markRuns: MarkRunChars, emoticons: CommonEmoticons,
        specialCases: [],
        // AnCora keeps single capital letters, "fig.", "EE.UU." and "Co." joined to their stop and splits most others; the titles and
        // everyday abbreviations below are not in the corpus and follow the UD guidelines (the full stop is part of the token).
        abbreviations:
        [
            "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z",
            "fig", "co", "kg", "ee.uu", "sr", "sra", "srta", "sres", "dr", "dra", "prof", "lic", "ing", "gral", "avda", "núm", "pág", "págs", "aprox", "vs", "tel",
        ],
        clitics: [], nTSuffix: false,
        nonFinalAbbreviations: ["sr.", "sra.", "srta.", "sres.", "dr.", "dra.", "prof.", "lic.", "ing.", "gral.", "avda.", "núm.", "pág.", "págs.", "vs."])
    {
        Name = "spanish",
        Contractions = new Dictionary<string, string[]> { ["del"] = ["de", "el"], ["al"] = ["a", "el"] }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase),
        EncliticVerbs = true,
        NotEncliticVerbs = NotEncliticVerbList.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
    };

    /// <summary>Contractions that are one multiword token of several words: the text, ignoring case, maps to the words (lower case).</summary>
    internal FrozenDictionary<string, string[]> Contractions { get; private init; } = FrozenDictionary<string, string[]>.Empty;

    /// <summary>Whether an infinitive or a gerund with enclitic pronouns is a multiword token of the verb and the pronouns.</summary>
    internal bool EncliticVerbs { get; private init; }

    /// <summary>Words that look like a verb with enclitics and are not (<c>parte</c>), lower case.</summary>
    internal FrozenSet<string> NotEncliticVerbs { get; private init; } = FrozenSet<string>.Empty;

    /// <summary>Whether this data set has multiword tokens at all.</summary>
    internal bool HasMultiwordTokens => Contractions.Count > 0 || EncliticVerbs || ItalianEncliticVerbs;

    // The words of a surface form that UD annotates as one multiword token: a contraction (del is de + el) or a verb with enclitic pronouns
    // (dármelo is dar + me + lo; the stress accent goes). The rule for verbs: the word ends in one to three enclitic pronouns
    // (me te se lo la le nos les las los), what is left is an infinitive (-ar -er -ir) or a gerund (-ando -iendo -yendo) once an accent
    // written for the stress is removed (situándose is situando + se; reírse keeps its accent), and the word is not one the treebank writes
    // whole (parte, fuerte, Carlos). Imperative and subjunctive forms (dámelo) are not recognized.

    /// <summary>The pieces of a multiword token as the tokenizer cut them (TryMatchMultiword): two to four neighbouring tokens without a gap.</summary>
    private bool TryMatchTokenGroup(Doc doc, int start, out int count, out string[]? words)
    {
        count = 0;
        words = null;
        if (start < 0 || start + 1 >= doc.Length)
        {
            return false;
        }

        for (int pieces = Math.Min(4, doc.Length - start); pieces >= 2; pieces--)
        {
            bool joined = true;
            for (int k = 0; joined && k + 1 < pieces; k++)
            {
                joined = doc[start + k].EndIdx == doc[start + k + 1].Idx;
            }

            int length = doc[start + pieces - 1].EndIdx - doc[start].Idx;
            if (joined && length <= MaxMultiwordLength && TryMultiwordWords(doc.Text.AsSpan(doc[start].Idx, length), out string[] found) && found.Length == pieces)
            {
                count = pieces;
                words = found;
                return true;
            }
        }

        return false;
    }

    internal bool TryMultiwordWords(ReadOnlySpan<char> token, out string[] words)
    {
        words = [];
        if (token.Length < 2 || token.Length > MaxMultiwordLength || !HasMultiwordTokens)
        {
            return false;
        }

        if (Contractions.Count > 0 && Contractions.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(token, out string[]? parts))
        {
            words = CopyCase(parts, token);
            return true;
        }

        if (ItalianEncliticVerbs)
        {
            return TryItalianEncliticVerb(token, out words);
        }

        if (!EncliticVerbs || token.Length < 4)
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

        // Peel up to three enclitics from the end; each leaves at least two characters of stem.
        var enclitics = new List<string>(3);
        int stemEnd = lower.Length;
        while (enclitics.Count < 3)
        {
            string? found = null;
            foreach (string pronoun in SpanishEnclitics.Pronouns)
            {
                if (stemEnd - pronoun.Length >= 2 && lower.AsSpan(0, stemEnd).EndsWith(pronoun, StringComparison.Ordinal))
                {
                    found = pronoun;
                    break;
                }
            }

            if (found is null)
            {
                break;
            }

            enclitics.Insert(0, found);
            stemEnd -= found.Length;
        }

        if (enclitics.Count == 0)
        {
            return false;
        }

        string stem = lower[..stemEnd];
        string plain = RemoveStressAccent(stem);
        bool infinitive = plain.EndsWith("ar", StringComparison.Ordinal) || plain.EndsWith("er", StringComparison.Ordinal) || plain.EndsWith("ir", StringComparison.Ordinal);
        bool gerund = plain.EndsWith("ando", StringComparison.Ordinal) || plain.EndsWith("iendo", StringComparison.Ordinal) || plain.EndsWith("yendo", StringComparison.Ordinal);
        if (!(gerund || (infinitive && (plain.Length >= 3 || plain == "ir"))))
        {
            return false;
        }

        // The infinitives of reír, oír and their family keep the accent of the stem itself.
        string verb = stem.EndsWith("eír", StringComparison.Ordinal) || stem.EndsWith("oír", StringComparison.Ordinal) ? stem : plain;
        var result = new string[enclitics.Count + 1];
        result[0] = verb;
        for (int i = 0; i < enclitics.Count; i++)
        {
            result[i + 1] = enclitics[i];
        }

        words = CopyCase(result, token);
        return true;
    }

    // The accent that Spanish spelling writes on the stressed vowel when a pronoun is added (encontrár-nos-las, situándo-se, véa-se).
    private static string RemoveStressAccent(string word) => word.Length == 0 ? word : string.Create(word.Length, word, static (span, w) =>
    {
        for (int i = 0; i < w.Length; i++)
        {
            span[i] = w[i] switch { '\u00e1' => 'a', '\u00e9' => 'e', '\u00ed' => 'i', '\u00f3' => 'o', '\u00fa' => 'u', _ => w[i] };
        }
    });

    // "Del" gives "De" and "el"; "DEL" gives "DE" and "EL"; the first word takes the capital of a capitalised surface form.
    private static string[] CopyCase(string[] words, ReadOnlySpan<char> surface)
    {
        bool allUpper = surface.Length > 1;
        foreach (char c in surface)
        {
            if (!char.IsUpper(c))
            {
                allUpper = false;
                break;
            }
        }

        var result = new string[words.Length];
        for (int i = 0; i < words.Length; i++)
        {
            result[i] = allUpper ? words[i].ToUpperInvariant()
                : i == 0 && char.IsUpper(surface[0]) ? char.ToUpperInvariant(words[i][0]) + words[i][1..]
                : words[i];
        }

        return result;
    }

    /// <summary>The lengths of the pieces the tokenizer cuts a multiword token into, in UTF-16 units, or <see langword="null"/> when it cannot be cut.</summary>
    internal static int[]? PieceLengths(ReadOnlySpan<char> surface, string[] words)
    {
        if (surface.Length < words.Length)
        {
            return null;
        }

        var lengths = new int[words.Length];
        int total = 0;
        foreach (string w in words)
        {
            total += w.Length;
        }

        // The CoNLL-U reader: words that spell the surface form keep their own lengths, otherwise the surface is cut in nearly equal parts.
        bool spelled = total == surface.Length;
        int at = 0;
        for (int i = 0; spelled && i < words.Length; i++)
        {
            spelled = surface.Slice(at, words[i].Length).SequenceEqual(words[i]);
            at += words[i].Length;
        }

        for (int i = 0; i < words.Length; i++)
        {
            lengths[i] = spelled ? words[i].Length : (surface.Length / words.Length) + (i < surface.Length % words.Length ? 1 : 0);
        }

        return lengths;
    }
}
