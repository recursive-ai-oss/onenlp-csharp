using System.Globalization;
using System.Text;

namespace RecursiveAI.OneNlp.Recognizers;

/// <summary>The rule data is malformed. The message names the line.</summary>
public sealed class RecognizerRulesException : FormatException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">What is wrong, with the line number when there is one.</param>
    public RecognizerRulesException(string message)
        : base(message)
    {
    }
}

internal enum CardinalKind
{
    Unit,
    Teen,
    Tens,
    Scale,
    Multiplier,
}

internal readonly record struct Cardinal(CardinalKind Kind, decimal Value);

internal readonly record struct OrdinalWord(decimal Value, bool NotAfterNumber);

internal readonly record struct ScaleSuffix(decimal Value, bool MoneyOnly);

internal readonly record struct OrdinalSuffix(string Suffix, string Digits, int Teen);

internal enum SuffixKind
{
    Percent,
    Unit,
    Money,
    Age,
}

/// <summary>What follows a number: a percent sign or word, a unit, a currency name or code, or an age word.</summary>
internal sealed record SuffixEntry(SuffixKind Kind, string Name, string? Dimension, decimal Factor, float Confidence);

/// <summary>Words that, next to a word-number, show it is not a number: "no one", "one of them", "the first time".</summary>
internal sealed record NotNumberRule(HashSet<string> Before, HashSet<string> After);

/// <summary>A unit name that is also a currency name ("pounds"): money when the words around it talk about money, else the unit.</summary>
internal sealed record AmbiguousMoney(string Iso, decimal Factor, HashSet<string> MoneyWords, HashSet<string> OtherWords);

/// <summary>What precedes a number: a currency symbol or code.</summary>
internal sealed record PrefixEntry(string Iso, float Confidence);

/// <summary>
/// The compiled rules of one language, read from rule data (see <c>docs/recognizers.md</c> for the format). Immutable once loaded, so
/// recognizers share it between threads.
/// </summary>
internal sealed class RecognizerData
{
    private const int MaxBytes = 8 << 20;
    private const int MaxLines = 200_000;
    private const int MaxLineChars = 8192;
    private const int MaxFields = 8;

    internal char DecimalSeparator { get; private set; } = '.';

    internal char GroupSeparator { get; private set; } = ',';

    internal string NegativeSigns { get; private set; } = "-\u2212";

    internal Dictionary<string, Cardinal> Cardinals { get; } = new(StringComparer.OrdinalIgnoreCase);

    internal Dictionary<string, OrdinalWord> Ordinals { get; } = new(StringComparer.OrdinalIgnoreCase);

    internal Dictionary<string, int> Fractions { get; } = new(StringComparer.OrdinalIgnoreCase);

    internal HashSet<string> Articles { get; } = new(StringComparer.OrdinalIgnoreCase);

    internal HashSet<string> Connectors { get; } = new(StringComparer.OrdinalIgnoreCase);

    internal HashSet<string> NegativeWords { get; } = new(StringComparer.OrdinalIgnoreCase);

    internal HashSet<string> PointWords { get; } = new(StringComparer.OrdinalIgnoreCase);

    internal Dictionary<string, NotNumberRule> NotNumbers { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Pairs of units written one after the other as one length ("5 feet 3 inches"): key "larger|smaller", value smaller units in one larger.</summary>
    internal Dictionary<string, decimal> Compounds { get; } = new(StringComparer.Ordinal);

    internal Dictionary<string, AmbiguousMoney> AmbiguousMoney { get; } = new(StringComparer.OrdinalIgnoreCase);

    internal FormTable<ScaleSuffix> ScaleSuffixes { get; } = new();

    internal List<OrdinalSuffix> OrdinalSuffixes { get; } = [];

    internal FormTable<SuffixEntry> Suffixes { get; } = new();

    internal FormTable<PrefixEntry> Prefixes { get; } = new();

    internal HashSet<string> CurrencyCodes { get; } = new(StringComparer.Ordinal);

    internal HashSet<string> Schemes { get; } = new(StringComparer.Ordinal);

    internal HashSet<string> Tlds { get; } = new(StringComparer.Ordinal);

    internal TemporalData Temporal { get; } = new();

    internal HashSet<string> BareTlds { get; } = new(StringComparer.Ordinal);

    private readonly List<(string Canonical, string[] Forms)> _ageUnits = [];
    private readonly List<string> _ageSuffixes = [];

    /// <summary>Reads and compiles rule data. Several files may be given; a section may appear in more than one.</summary>
    internal static RecognizerData Parse(IEnumerable<ReadOnlyMemory<byte>> files)
    {
        var data = new RecognizerData();
        foreach (ReadOnlyMemory<byte> file in files)
        {
            data.ParseFile(file.Span);
        }

        data.Finish();
        return data;
    }

    private void ParseFile(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > MaxBytes)
        {
            throw new RecognizerRulesException($"Rule data is larger than {MaxBytes} bytes.");
        }

        string text;
        try
        {
            text = new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (ArgumentException)
        {
            throw new RecognizerRulesException("Rule data is not valid UTF-8.");
        }

        string? section = null;
        int lineNo = 0;
        int pos = 0;
        while (pos < text.Length)
        {
            int eol = text.IndexOf('\n', pos);
            if (eol < 0)
            {
                eol = text.Length;
            }

            lineNo++;
            if (lineNo > MaxLines)
            {
                throw new RecognizerRulesException($"Rule data has more than {MaxLines} lines.");
            }

            if (eol - pos > MaxLineChars)
            {
                throw new RecognizerRulesException($"Line {lineNo} is longer than {MaxLineChars} characters.");
            }

            string line = text.Substring(pos, eol - pos).Trim();
            pos = eol + 1;
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            try
            {
                if (line[0] == '[')
                {
                    if (line[^1] != ']')
                    {
                        throw new RecognizerRulesException("A section header must end with ']'.");
                    }

                    section = line[1..^1].Trim();
                    if (!Sections.Contains(section))
                    {
                        throw new RecognizerRulesException($"Unknown section '{section}'.");
                    }

                    continue;
                }

                if (section is null)
                {
                    throw new RecognizerRulesException("A rule appears before any [section].");
                }

                string[] fields = line.Split('|', StringSplitOptions.TrimEntries);
                if (fields.Length > MaxFields)
                {
                    throw new RecognizerRulesException($"More than {MaxFields} fields.");
                }

                Apply(section, fields);
            }
            catch (RecognizerRulesException e)
            {
                throw new RecognizerRulesException($"Line {lineNo}: {e.Message}");
            }
        }
    }

    private static readonly HashSet<string> Sections = new(StringComparer.Ordinal)
    {
        "number.digits", "number.cardinal", "number.ordinal", "number.fraction", "number.article", "number.connector", "number.negative",
        "number.point", "number.suffix", "number.ordinal-suffix", "percent", "unit", "unit.compound", "number.not-number", "currency.context", "age.unit", "age.suffix", "currency.symbol",
        "currency.name", "currency.code", "url.scheme", "url.tld", "url.tld.bare", "time.month", "time.weekday", "time.unit", "time.season",
        "time.daypart", "time.meridiem", "time.word", "time.recurrence", "time.zone", "time.holiday",
    };

    private void Apply(string section, string[] f)
    {
        switch (section)
        {
            case "number.digits":
                Need(f, 2, 2);
                switch (f[0])
                {
                    case "decimal":
                        DecimalSeparator = OneChar(f[1]);
                        break;
                    case "group":
                        GroupSeparator = OneChar(f[1]);
                        break;
                    case "negative":
                        NegativeSigns = f[1];
                        break;
                    default:
                        throw new RecognizerRulesException($"Unknown key '{f[0]}' (expected decimal, group or negative).");
                }

                break;
            case "number.cardinal":
                Need(f, 3, 3);
                Cardinals[f[0].ToLowerInvariant()] = new Cardinal(f[1] switch
                {
                    "unit" => CardinalKind.Unit,
                    "teen" => CardinalKind.Teen,
                    "tens" => CardinalKind.Tens,
                    "scale" => CardinalKind.Scale,
                    "multiplier" => CardinalKind.Multiplier,
                    _ => throw new RecognizerRulesException($"Unknown cardinal kind '{f[1]}'."),
                }, Number(f[2]));
                break;
            case "number.ordinal":
                Need(f, 2, 3);
                Ordinals[f[0].ToLowerInvariant()] = new OrdinalWord(Number(f[1]), f.Length > 2 && f[2] == "not-after-number");
                break;
            case "number.fraction":
                Need(f, 2, 2);
                Fractions[f[0].ToLowerInvariant()] = (int)Math.Clamp(Number(f[1]), 2, 1_000_000);
                break;
            case "number.article":
                Need(f, 1, 1);
                Articles.Add(f[0].ToLowerInvariant());
                break;
            case "number.connector":
                Need(f, 1, 1);
                Connectors.Add(f[0].ToLowerInvariant());
                break;
            case "number.negative":
                Need(f, 1, 1);
                NegativeWords.Add(f[0].ToLowerInvariant());
                break;
            case "number.point":
                Need(f, 1, 1);
                PointWords.Add(f[0].ToLowerInvariant());
                break;
            case "number.suffix":
                Need(f, 3, 3);
                ScaleSuffixes.Add(f[0], new ScaleSuffix(Number(f[1]), f[2] switch
                {
                    "any" => false,
                    "money" => true,
                    _ => throw new RecognizerRulesException($"Unknown context '{f[2]}' (expected any or money)."),
                }));
                break;
            case "number.ordinal-suffix":
                Need(f, 3, 3);
                OrdinalSuffixes.Add(new OrdinalSuffix(f[0].ToLowerInvariant(), f[1], f[2] switch
                {
                    "any" => 0,
                    "teen" => 1,
                    "not-teen" => 2,
                    _ => throw new RecognizerRulesException($"Unknown teen rule '{f[2]}' (expected any, teen or not-teen)."),
                }));
                break;
            case "percent":
                Need(f, 1, 1);
                foreach (string form in List(f[0]))
                {
                    Suffixes.Add(form, new SuffixEntry(SuffixKind.Percent, "percent", "percent", 1m, 1f));
                }

                break;
            case "unit":
                Need(f, 3, 4);
                foreach (string form in List(f[2]))
                {
                    Suffixes.Add(form, new SuffixEntry(SuffixKind.Unit, f[0], f[1], 1m, Conf(f, 3)));
                }

                break;
            case "unit.compound":
                Need(f, 3, 3);
                Compounds[f[0] + "|" + f[1]] = Number(f[2]);
                break;
            case "number.not-number":
                Need(f, 3, 3);
                if (!NotNumbers.TryGetValue(f[0], out NotNumberRule? rule))
                {
                    NotNumbers[f[0]] = rule = new NotNumberRule(new HashSet<string>(StringComparer.OrdinalIgnoreCase), new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                }

                HashSet<string> side = f[1] switch
                {
                    "before" => rule.Before,
                    "after" => rule.After,
                    _ => throw new RecognizerRulesException($"Unknown side '{f[1]}' (expected before or after)."),
                };
                foreach (string w in List(f[2]))
                {
                    side.Add(w);
                }

                break;
            case "currency.context":
                Need(f, 5, 5);
                var ambiguous = new AmbiguousMoney(f[1], Number(f[2]), new HashSet<string>(List(f[3]), StringComparer.OrdinalIgnoreCase), new HashSet<string>(List(f[4]), StringComparer.OrdinalIgnoreCase));
                foreach (string form in List(f[0]))
                {
                    AmbiguousMoney[form.ToLowerInvariant()] = ambiguous;
                }

                break;
            case "age.unit":
                Need(f, 2, 2);
                _ageUnits.Add((f[0], [.. List(f[1])]));
                break;
            case "age.suffix":
                Need(f, 1, 1);
                _ageSuffixes.AddRange(List(f[0]));
                break;
            case "currency.symbol":
                Need(f, 3, 4);
                float conf = Conf(f, 3);
                if (f[2] is not ("prefix" or "suffix" or "both"))
                {
                    throw new RecognizerRulesException($"Unknown position '{f[2]}' (expected prefix, suffix or both).");
                }

                if (f[2] is "prefix" or "both")
                {
                    Prefixes.Add(f[0], new PrefixEntry(f[1], conf));
                }

                if (f[2] is "suffix" or "both")
                {
                    Suffixes.Add(f[0], new SuffixEntry(SuffixKind.Money, f[1], "currency", 1m, conf));
                }

                break;
            case "currency.name":
                Need(f, 2, 4);
                decimal factor = f.Length > 2 && f[2].Length > 0 ? Number(f[2]) : 1m;
                foreach (string form in List(f[0]))
                {
                    Suffixes.Add(form, new SuffixEntry(SuffixKind.Money, f[1], "currency", factor, Conf(f, 3)));
                }

                break;
            case "currency.code":
                Need(f, 1, 1);
                foreach (string code in Words(f[0]))
                {
                    if (code.Length != 3 || !code.All(char.IsAsciiLetterUpper))
                    {
                        throw new RecognizerRulesException($"'{code}' is not a three-letter upper-case currency code.");
                    }

                    CurrencyCodes.Add(code);
                }

                break;
            case "url.scheme":
                Need(f, 1, 1);
                foreach (string s in Words(f[0]))
                {
                    Schemes.Add(s.ToLowerInvariant());
                }

                break;
            case "url.tld":
                Need(f, 1, 1);
                foreach (string s in Words(f[0]))
                {
                    Tlds.Add(s.ToLowerInvariant());
                }

                break;
            case "url.tld.bare":
                Need(f, 1, 1);
                foreach (string s in Words(f[0]))
                {
                    BareTlds.Add(s.ToLowerInvariant());
                }

                break;
            default:
                if (section.StartsWith("time.", StringComparison.Ordinal))
                {
                    Temporal.Add(section, f);
                    break;
                }

                throw new RecognizerRulesException($"Unknown section '{section}'.");
        }
    }

    private void Finish()
    {
        // Ages: "5 years old", "5-year-old". Each age unit form is combined with each age suffix, with a space and with a hyphen.
        foreach ((string canonical, string[] forms) in _ageUnits)
        {
            foreach (string form in forms)
            {
                foreach (string suffix in _ageSuffixes)
                {
                    var entry = new SuffixEntry(SuffixKind.Age, canonical, "age", 1m, 1f);
                    Suffixes.Add(form + " " + suffix, entry);
                    Suffixes.Add(form + "-" + suffix, entry);
                }
            }
        }

        foreach (string code in CurrencyCodes)
        {
            // Codes are upper case in text ("USD 5"); a lower-case "usd" is left alone because many codes are English words (ALL, TRY, TOP).
            Prefixes.Add("=" + code, new PrefixEntry(code, 1f));
            Suffixes.Add("=" + code, new SuffixEntry(SuffixKind.Money, code, "currency", 1m, 1f));
        }
    }

    private static void Need(string[] f, int min, int max)
    {
        if (f.Length < min || f.Length > max)
        {
            throw new RecognizerRulesException($"Expected {min}{(max > min ? "-" + max : string.Empty)} fields, found {f.Length}.");
        }

        for (int i = 0; i < min; i++)
        {
            if (f[i].Length == 0)
            {
                throw new RecognizerRulesException("An empty field.");
            }
        }
    }

    private static char OneChar(string s) =>
        s.Length == 1 ? s[0] : throw new RecognizerRulesException($"'{s}' must be one character.");

    private static decimal Number(string s) =>
        decimal.TryParse(s, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal v)
            ? v
            : throw new RecognizerRulesException($"'{s}' is not a number.");

    private static float Conf(string[] f, int index)
    {
        if (f.Length <= index || f[index].Length == 0)
        {
            return 1f;
        }

        return float.TryParse(f[index], NumberStyles.Float, CultureInfo.InvariantCulture, out float c) && c is >= 0f and <= 1f
            ? c
            : throw new RecognizerRulesException($"'{f[index]}' is not a confidence from 0 to 1.");
    }

    // Forms may contain spaces ("miles per hour"), so a list of forms is split on commas only.
    private static string[] List(string s)
    {
        string[] forms = s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return forms.Length > 0 ? forms : throw new RecognizerRulesException("A list of forms is empty.");
    }

    // Codes, schemes and domain endings never contain spaces, so they may also be separated by white space.
    private static string[] Words(string s) =>
        s.Split([',', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
