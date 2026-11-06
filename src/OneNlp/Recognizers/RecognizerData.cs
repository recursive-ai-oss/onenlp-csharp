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

    /// <summary>A scale word that may stand alone for one of itself: French <c>cent</c>, <c>mille</c>; German <c>hundert</c>, <c>tausend</c>.</summary>
    BareScale,

    /// <summary>A hundreds word that stands for a whole number of hundreds: Spanish <c>doscientos</c> (200), Portuguese <c>quinhentos</c> (500).</summary>
    Hundreds,
}

/// <summary>How number words combine. English is additive and multiplicative in the short scale; the others need rules of their own.</summary>
internal enum NumberSystem
{
    English,

    /// <summary>French: <c>quatre-vingt-dix-sept</c> (80 + 10 + 7), <c>soixante et onze</c>, invariable <c>mille</c>, hundreds with a plural <c>cents</c>.</summary>
    French,

    /// <summary>German: whole numbers below a million are written as one word, units before tens (<c>dreihundertvierundzwanzig</c>).</summary>
    German,

    /// <summary>Italian: like German, whole numbers below a million are one word, but the tens come before the units (<c>trecentoventiquattro</c>, <c>ventitré</c>).</summary>
    Italian,

    /// <summary>Chinese: numerals are built from digit, ten/hundred/thousand and ten-thousand/hundred-million characters, with no spaces between words (see <c>ChineseData</c>).</summary>
    Chinese,

    /// <summary>
    /// Spanish and Portuguese: tens are joined to the unit by a connector (<c>treinta y cinco</c>, <c>trinta e cinco</c>), hundreds are words of their
    /// own (<c>doscientos</c>), and an ordinal tens word may be followed by an ordinal unit (<c>vigésimo tercero</c>, <c>décimo quinto</c>).
    /// </summary>
    Iberian,
}

internal readonly record struct Cardinal(CardinalKind Kind, decimal Value);

internal readonly record struct OrdinalWord(decimal Value, bool NotAfterNumber);

internal enum MorphemeKind
{
    Unit,
    Teen,
    Tens,
    Hundred,
    Thousand,
    And,
}

/// <summary>A piece of a German number word: <c>drei</c>, <c>hundert</c>, <c>und</c>, an ordinal stem such as <c>zwanzigst</c>.</summary>
internal readonly record struct Morpheme(string Text, MorphemeKind Kind, decimal Value, bool Ordinal);

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

    /// <summary>The characters that may separate groups of three digits (<c>1,234</c>; <c>1 234</c> with a space, no-break space or narrow no-break space; <c>1.234</c>).</summary>
    internal string GroupSeparators { get; private set; } = ",";

    internal NumberSystem System { get; private set; } = NumberSystem.English;

    /// <summary>Decimal separators for regions that write numbers differently from the language's default (<c>decimal.mx</c>).</summary>
    internal Dictionary<string, char> RegionDecimal { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Group separators for regions that differ from the language's default (<c>group.mx</c>).</summary>
    internal Dictionary<string, string> RegionGroups { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Digits a phone-like number written as space-separated groups may start with (<c>phone.space-start</c>), and the least and most digits it has.
    /// Without a rule, only a national trunk prefix 0 and 9 to 11 digits count.
    /// </summary>
    internal string PhoneSpaceStarts { get; private set; } = "0";

    internal int PhoneSpaceMinDigits { get; private set; } = 9;

    internal int PhoneSpaceMaxDigits { get; private set; } = 11;

    /// <summary>A whole number followed by a dot and a word is an ordinal (German <c>3. März</c>).</summary>
    internal bool OrdinalDot { get; private set; }

    /// <summary>Words that follow a number and a dot and show the dot makes an ordinal even when the word has a capital letter (German nouns: <c>5. Stock</c>).</summary>
    internal HashSet<string> OrdinalDotWords { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Words that are never a number on their own (the indefinite articles <c>un</c>, <c>ein</c>) but count inside a longer number.</summary>
    internal HashSet<string> LoneSkip { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Endings a declined ordinal takes after its stem (German <c>dritte</c>, <c>dritten</c>); the bare stem is looked up as written.</summary>
    internal List<string> OrdinalEndings { get; } = [];

    /// <summary>Words before a number that make the following age unit an age (<c>âgé de</c> 5 ans).</summary>
    internal FormTable<bool> AgePrefixes { get; } = new();

    /// <summary>Age units that are an age without more words (<c>5 Jahre alt</c> needs <c>alt</c>; <c>5-jährig</c> does not).</summary>
    internal FormTable<string> AgeUnitsBare { get; } = new();

    internal string NegativeSigns { get; private set; } = "-\u2212";

    internal Dictionary<string, Cardinal> Cardinals { get; } = new(StringComparer.OrdinalIgnoreCase);

    internal Dictionary<string, OrdinalWord> Ordinals { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Ordinal stems that need an ending to be a word (German <c>dritt</c>, written <c>number.ordinal</c> with a trailing <c>~</c>).</summary>
    internal Dictionary<string, OrdinalWord> OrdinalStems { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Pieces of compound number words that are not words themselves (<c>number.part</c>).</summary>
    private List<Morpheme> Parts { get; } = [];

    /// <summary>The parts German number words are cut into, longest first; empty for other languages.</summary>
    internal Morpheme[] Morphemes { get; private set; } = [];

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

    /// <summary>The Chinese numeral and date vocabulary (the <c>zh.*</c> sections); empty for other languages.</summary>
    internal ChineseData Chinese { get; } = new();

    internal bool IsChinese => System == NumberSystem.Chinese;

    /// <summary>Leading digits and length of a plain digit run that is phone-like (a national mobile number), or none.</summary>
    internal string[] PhonePlainStarts { get; private set; } = [];

    internal int PhonePlainLength { get; private set; }

    /// <summary>An area code and a subscriber number joined by a hyphen (<c>010-12345678</c>): the leading digit and the digit counts.</summary>
    internal string PhoneAreaLead { get; private set; } = string.Empty;

    internal int PhoneAreaMin { get; private set; }

    internal int PhoneAreaMax { get; private set; }

    internal int PhoneSubMin { get; private set; }

    internal int PhoneSubMax { get; private set; }

    internal HashSet<string> BareTlds { get; } = new(StringComparer.Ordinal);

    /// <summary>The decimal and group separators for a caller's region: the language's own unless the data names a different convention for the region.</summary>
    internal void NumberFormat(string? region, out char decimalSeparator, out string groupSeparators)
    {
        decimalSeparator = DecimalSeparator;
        groupSeparators = GroupSeparators;
        if (region is null)
        {
            return;
        }

        if (RegionDecimal.TryGetValue(region, out char d))
        {
            decimalSeparator = d;
        }

        if (RegionGroups.TryGetValue(region, out string? g))
        {
            groupSeparators = g;
        }
    }

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
        "number.point", "number.suffix", "number.ordinal-suffix", "percent", "unit", "unit.compound", "number.not-number", "currency.context", "age.unit", "age.suffix", "age.prefix", "age.word", "number.lone-skip", "number.part", "number.ordinal-ending", "number.ordinal-dot-word", "currency.symbol",
        "currency.name", "currency.code", "url.scheme", "url.tld", "url.tld.bare", "time.month", "time.weekday", "time.unit", "time.season",
        "time.daypart", "time.meridiem", "time.word", "time.recurrence", "time.zone", "time.holiday", "time.flag", "time.region", "time.glue", "time.decade", "phone.space-start", "phone.plain", "phone.area", "zh.digit", "zh.small", "zh.big", "zh.word", "zh.relday", "zh.relperiod",
        "zh.weekday", "zh.season", "zh.daypart", "zh.unit", "zh.holiday", "zh.lunar", "zh.zone", "zh.unit-prefix",
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
                    case var k when k.StartsWith("decimal.", StringComparison.Ordinal):
                        RegionDecimal[k[8..]] = OneChar(f[1]);
                        break;
                    case var k when k.StartsWith("group.", StringComparison.Ordinal):
                        RegionGroups[k[6..]] = Groups(f[1]);
                        break;
                    case "group":
                        GroupSeparators = Groups(f[1]);
                        break;
                    case "system":
                        System = f[1] switch
                        {
                            "english" => NumberSystem.English,
                            "french" => NumberSystem.French,
                            "german" or "dutch" => NumberSystem.German,
                            "italian" => NumberSystem.Italian,
                            "iberian" => NumberSystem.Iberian,
                            "chinese" => NumberSystem.Chinese,
                            _ => throw new RecognizerRulesException($"Unknown number system '{f[1]}' (expected english, french, german, dutch, italian, iberian or chinese)."),
                        };
                        break;
                    case "ordinal-dot":
                        OrdinalDot = f[1] switch
                        {
                            "yes" => true,
                            "no" => false,
                            _ => throw new RecognizerRulesException($"Unknown value '{f[1]}' (expected yes or no)."),
                        };
                        break;
                    case "negative":
                        NegativeSigns = f[1];
                        break;
                    default:
                        throw new RecognizerRulesException($"Unknown key '{f[0]}' (expected decimal, group, decimal.<region>, group.<region>, negative, system or ordinal-dot).");
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
                    "bare-scale" => CardinalKind.BareScale,
                    "hundreds" => CardinalKind.Hundreds,
                    _ => throw new RecognizerRulesException($"Unknown cardinal kind '{f[1]}'."),
                }, Number(f[2]));
                break;
            case "number.ordinal":
                Need(f, 2, 3);
                if (f[0].EndsWith('~'))
                {
                    OrdinalStems[f[0][..^1].ToLowerInvariant()] = new OrdinalWord(Number(f[1]), f.Length > 2 && f[2] == "not-after-number");
                }
                else
                {
                    Ordinals[f[0].ToLowerInvariant()] = new OrdinalWord(Number(f[1]), f.Length > 2 && f[2] == "not-after-number");
                }

                break;
            case "number.part":
                // A piece of a compound number word that is no word of its own (Italian elisions: vent- in ventuno, cent- in centottanta).
                Need(f, 3, 3);
                Parts.Add(new Morpheme(f[0].ToLowerInvariant(), f[1] switch
                {
                    "unit" => MorphemeKind.Unit,
                    "teen" => MorphemeKind.Teen,
                    "tens" => MorphemeKind.Tens,
                    "hundred" => MorphemeKind.Hundred,
                    "thousand" => MorphemeKind.Thousand,
                    _ => throw new RecognizerRulesException($"Unknown part kind '{f[1]}' (expected unit, teen, tens, hundred or thousand)."),
                }, Number(f[2]), false));
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
            case "age.prefix":
                Need(f, 1, 1);
                foreach (string form in List(f[0]))
                {
                    AgePrefixes.Add(form, true);
                }

                break;
            case "age.word":
                Need(f, 2, 2);
                foreach (string form in List(f[1]))
                {
                    Suffixes.Add(form, new SuffixEntry(SuffixKind.Age, f[0], "age", 1m, 1f));
                    AgeUnitsBare.Add(form, f[0]);
                }

                break;
            case "number.lone-skip":
                Need(f, 1, 1);
                foreach (string w in List(f[0]))
                {
                    LoneSkip.Add(w);
                }

                break;
            case "number.ordinal-ending":
                Need(f, 1, 1);
                OrdinalEndings.AddRange(List(f[0]).Select(e => e.ToLowerInvariant()));
                break;
            case "number.ordinal-dot-word":
                Need(f, 1, 1);
                foreach (string w in List(f[0]))
                {
                    OrdinalDotWords.Add(w);
                }

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
            case "phone.space-start":
                Need(f, 3, 3);
                PhoneSpaceStarts = f[0];
                PhoneSpaceMinDigits = (int)Math.Clamp(Number(f[1]), 7, 15);
                PhoneSpaceMaxDigits = (int)Math.Clamp(Number(f[2]), PhoneSpaceMinDigits, 15);
                break;
            case "phone.plain":
                // Digits a national plain run starts with, and its length: "13,14,15,16,17,18,19 | 11".
                Need(f, 2, 2);
                PhonePlainStarts = [.. Words(f[0])];
                PhonePlainLength = (int)Math.Clamp(Number(f[1]), 7, 15);
                break;
            case "phone.area":
                // An area code and a subscriber number joined by one hyphen: "0 | 3-4 | 7-8" (leading digit, area code digits, subscriber digits).
                Need(f, 3, 3);
                PhoneAreaLead = f[0];
                (PhoneAreaMin, PhoneAreaMax) = Range(f[1]);
                (PhoneSubMin, PhoneSubMax) = Range(f[2]);
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
                if (section.StartsWith("zh.", StringComparison.Ordinal))
                {
                    Chinese.Add(section, f);
                    break;
                }

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
                AgeUnitsBare.Add(form, canonical);
                foreach (string suffix in _ageSuffixes)
                {
                    var entry = new SuffixEntry(SuffixKind.Age, canonical, "age", 1m, 1f);
                    Suffixes.Add(form + " " + suffix, entry);
                    Suffixes.Add(form + "-" + suffix, entry);
                }
            }
        }

        if (System is NumberSystem.German or NumberSystem.Italian)
        {
            BuildMorphemes();
        }

        foreach (string code in CurrencyCodes)
        {
            // Codes are upper case in text ("USD 5"); a lower-case "usd" is left alone because many codes are English words (ALL, TRY, TOP).
            Prefixes.Add("=" + code, new PrefixEntry(code, 1f));
            Suffixes.Add("=" + code, new SuffixEntry(SuffixKind.Money, code, "currency", 1m, 1f));
        }
    }

    // The parts of a German number word: the units, teens, tens, hundert and tausend of the cardinal table, the connector und, and the
    // ordinal stems (which need an ending, so they only count as the last part).
    private void BuildMorphemes()
    {
        var parts = new List<Morpheme>();
        foreach ((string word, Cardinal c) in Cardinals)
        {
            MorphemeKind? kind = c.Kind switch
            {
                CardinalKind.Unit when c.Value is >= 1 and <= 9 && c.Value == decimal.Truncate(c.Value) => MorphemeKind.Unit,
                CardinalKind.Teen => MorphemeKind.Teen,
                CardinalKind.Tens => MorphemeKind.Tens,
                CardinalKind.Scale or CardinalKind.BareScale when c.Value == 100 => MorphemeKind.Hundred,
                CardinalKind.Scale or CardinalKind.BareScale when c.Value == 1000 => MorphemeKind.Thousand,
                _ => null,
            };
            if (kind is MorphemeKind k)
            {
                parts.Add(new Morpheme(word, k, c.Value, false));
            }
        }

        parts.AddRange(Parts);
        foreach (string word in Connectors)
        {
            parts.Add(new Morpheme(word, MorphemeKind.And, 0, false));
        }

        foreach ((string stem, OrdinalWord o) in OrdinalStems)
        {
            MorphemeKind kind = o.Value switch
            {
                < 10 => MorphemeKind.Unit,
                < 20 => MorphemeKind.Teen,
                < 100 => MorphemeKind.Tens,
                100 => MorphemeKind.Hundred,
                _ => MorphemeKind.Thousand,
            };
            parts.Add(new Morpheme(stem, kind, o.Value, true));
        }

        parts.Sort((a, b) =>
        {
            int c = b.Text.Length.CompareTo(a.Text.Length);
            return c != 0 ? c : string.CompareOrdinal(a.Text, b.Text);
        });
        Morphemes = [.. parts];
    }

    private static (int Min, int Max) Range(string s)
    {
        string[] p = s.Split('-');
        return p.Length == 2 && int.TryParse(p[0], out int a) && int.TryParse(p[1], out int b) && a >= 1 && b >= a && b <= 15
            ? (a, b)
            : throw new RecognizerRulesException($"'{s}' is not a digit-count range such as 3-4.");
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

    // A group separator is one character, or several named or written characters separated by spaces: "space nbsp nnbsp .".
    private static string Groups(string s)
    {
        var chars = new StringBuilder();
        foreach (string part in s.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            chars.Append(part switch
            {
                "space" => ' ',
                "nbsp" => ' ',
                "nnbsp" => ' ',
                "thinsp" => ' ',
                _ => OneChar(part),
            });
        }

        return chars.Length > 0 ? chars.ToString() : throw new RecognizerRulesException("A group separator is needed.");
    }

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
