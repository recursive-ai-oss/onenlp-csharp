using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace RecursiveAI.OneNlp.Recipes.Hindi;

// Keeps explicit adult content out of the labelled web text, and so out of the training data. The rule is deliberately plain and
// conservative: it prefers to lose a harmless document over keeping an explicit one.
//
//  1. A document is dropped whole when it contains a word from the explicit-term list (Devanagari, romanised Hindi and English), or a
//     host name in its text that carries an adult keyword (the sample has no page addresses, so the address heuristic works on web
//     addresses that appear inside the text).
//  2. A sentence is dropped when it contains a word from the weaker list (words that occur in news about courts or health, but
//     whose sentence is better left out).
//
// Words are whole tokens (runs of letters and combining marks), lower-cased, so that "लंडन" (London) is not hit by the entry for a
// vulgar word that it begins with. docs/provenance in the recipe records the share removed.
internal static partial class AdultFilter
{
    private static readonly HashSet<string> Strong = new(StringComparer.Ordinal)
    {
        "सेक्स", "सेक्सी", "सैक्स", "पोर्न", "पॉर्न", "पोर्नस्टार", "चुदाई", "चुदक्कड़", "चोदना", "चोदो", "चुत", "चूत", "लंड", "लण्ड", "लौड़ा", "लौडा",
        "गांड", "गाण्ड", "रंडी", "रण्डी", "हरामजादी", "नंगी", "एस्कॉर्ट", "एस्कॉर्ट्स", "कॉलगर्ल",
        "sex", "sexy", "porn", "porno", "xxx", "nude", "nudes", "escort", "escorts", "callgirl", "fuck", "fucking", "blowjob", "hentai", "chudai", "randi",
    };

    private static readonly HashSet<string> Weak = new(StringComparer.Ordinal)
    {
        "अश्लील", "अश्लीलता", "कामुक", "कामुकता", "नग्न", "नंगा", "वयस्क", "बलात्कार", "यौन", "हॉट", "adult", "erotic", "hot",
    };

    private static readonly string[] PairStrong = ["कॉल गर्ल", "ब्लू फिल्म", "सेक्स वीडियो", "call girl", "blue film"];

    // A host name with an adult keyword in its first label, e.g. "freeporn.example.com" or "hot-xxx.in".
    [GeneratedRegex(@"[a-z0-9\-]*(porn|xxx|sexy|nude|escort|sex)[a-z0-9\-]*\.(com|net|org|in|xyz|tv|info|co|me|club|site|online|live)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AdultHost();

    private static IEnumerable<string> Words(string text)
    {
        var sb = new StringBuilder();
        foreach (char c in text)
        {
            UnicodeCategory cat = char.GetUnicodeCategory(c);
            if (char.IsLetterOrDigit(c) || cat is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark || c == '_')
            {
                sb.Append(char.ToLowerInvariant(c));
            }
            else if (sb.Length > 0)
            {
                yield return sb.ToString();
                sb.Clear();
            }
        }

        if (sb.Length > 0)
        {
            yield return sb.ToString();
        }
    }

    /// <summary>Whether a whole document should be dropped.</summary>
    public static bool Document(string text)
    {
        foreach (string w in Words(text))
        {
            if (Strong.Contains(w))
            {
                return true;
            }
        }

        foreach (string p in PairStrong)
        {
            if (text.Contains(p, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return AdultHost().IsMatch(text);
    }

    /// <summary>Whether one sentence should be dropped (the weaker list; the strong list is applied to the document).</summary>
    public static bool Sentence(string text)
    {
        foreach (string w in Words(text))
        {
            if (Weak.Contains(w) || Strong.Contains(w))
            {
                return true;
            }
        }

        return false;
    }
}
