using System.Reflection;

namespace RecursiveAI.OneNlp.Recognizers;

/// <summary>
/// Finds numbers, ordinals, percentages, measures, amounts of money and address-like sequences (e-mail, URL, IP, phone-like, hashtag,
/// mention, GUID) in text, and resolves each to a value. One engine reads the rules of a language as data, so a language is added by
/// writing a rule file, not code.
/// </summary>
/// <remarks>
/// <para>
/// <b>Overlaps.</b> Rules propose results independently, and overlapping proposals are settled the same way every time: the longest
/// wins; of equal length, the type listed first in this order: URL, EMAIL, MENTION, HASHTAG, GUID, IP_ADDRESS, PHONE_LIKE, MONEY,
/// PERCENT, QUANTITY, ORDINAL, CARDINAL (the dates and times, DATE, TIME, DATETIME, DURATION and SET, rank after IP_ADDRESS and before PHONE_LIKE); then the earlier start. What is left does not overlap.
/// </para>
/// <para>Instances are immutable and safe to use from several threads. The rule format is in <c>docs/recognizers.md</c>.</para>
/// </remarks>
public sealed class Recognizer
{
    private static readonly Lazy<Recognizer> EnglishRules = new(() => LoadEmbedded(Language.English));

    private readonly RecognizerData _data;

    private Recognizer(RecognizerData data, Language language)
    {
        _data = data;
        Language = language;
    }

    /// <summary>The recognizer for English, from the rules built into this package.</summary>
    public static Recognizer English => EnglishRules.Value;

    /// <summary>The language the rules are for; <see cref="Language.Unknown"/> for rules loaded without one.</summary>
    public Language Language { get; }

    /// <summary>Returns the recognizer for a language from the rules built into this package.</summary>
    /// <param name="language">The language.</param>
    /// <exception cref="NotSupportedException">No rules for the language are built in. English is the only one so far; <see cref="FromRules"/> loads rules from a file.</exception>
    public static Recognizer For(Language language) =>
        language == Language.English
            ? English
            : throw new NotSupportedException(
                $"No built-in recognizer rules for language '{language.Code}'. English ('en') is the only one so far; load rules with Recognizer.FromRules.");

    /// <summary>Compiles recognizer rules from rule data.</summary>
    /// <param name="rules">One or more rule files as UTF-8 text, in the format described in <c>docs/recognizers.md</c>. Sections may repeat across files.</param>
    /// <param name="language">The language the rules are for, or <see cref="Language.Unknown"/>.</param>
    /// <returns>A recognizer over the rules.</returns>
    /// <exception cref="RecognizerRulesException">The data is malformed: invalid UTF-8, an unknown section, a field of the wrong shape, or a limit exceeded (8 MB per file, 200,000 lines, 8192 characters per line). The message names the line.</exception>
    public static Recognizer FromRules(IEnumerable<ReadOnlyMemory<byte>> rules, Language language = default)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return new Recognizer(RecognizerData.Parse(rules), language);
    }

    /// <summary>Compiles recognizer rules from files.</summary>
    /// <param name="paths">Paths of rule files; see <see cref="FromRules"/>.</param>
    /// <returns>A recognizer over the rules; its language is <see cref="Language.Unknown"/>.</returns>
    /// <exception cref="RecognizerRulesException">A file is malformed; see <see cref="FromRules"/>.</exception>
    /// <exception cref="IOException">A file cannot be read.</exception>
    public static Recognizer FromFiles(params string[] paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return FromRules(paths.Select(p => (ReadOnlyMemory<byte>)File.ReadAllBytes(p)).ToList());
    }

    /// <summary>Finds everything the rules recognize in a text.</summary>
    /// <param name="text">The text; may be empty.</param>
    /// <param name="context">Inputs beyond the text: the reference date and region for dates and times. Without a reference date, relative expressions ("tomorrow", "next Friday") are left out.</param>
    /// <returns>
    /// The results in text order, without overlaps (see the remarks of <see cref="Recognizer"/>). Each is a <see cref="Quantity"/> or a
    /// <see cref="SequenceMatch"/> with UTF-16 offsets into <paramref name="text"/>. Empty when nothing is found.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<Recognition> Recognize(string text, RecognitionContext context = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
        {
            return [];
        }

        var scan = new Scan(_data, text, context);
        return scan.Run();
    }

    private static Recognizer LoadEmbedded(Language language)
    {
        Assembly assembly = typeof(Recognizer).Assembly;
        string prefix = "Recognizers/Data/" + language.Code + "/";
        string[] names =
        [
            .. assembly.GetManifestResourceNames()
                .Where(n => n.Replace('\\', '/').StartsWith(prefix, StringComparison.Ordinal))
                .Order(StringComparer.Ordinal),
        ];
        var files = new List<ReadOnlyMemory<byte>>(names.Length);
        foreach (string name in names)
        {
            using Stream stream = assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException("Missing resource " + name);
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            files.Add(copy.ToArray());
        }

        return new Recognizer(RecognizerData.Parse(files), language);
    }
}

internal sealed partial class Scan
{
    /// <summary>Runs every rule family and settles overlaps.</summary>
    internal List<Recognition> Run()
    {
        FindNumbers();
        EmitQuantities();
        FindSequences();
        FindTemporal();
        return Resolve();
    }

    private static int Rank(string label) => label switch
    {
        Labels.Url => 0,
        Labels.Email => 1,
        Labels.Mention => 2,
        Labels.Hashtag => 3,
        Labels.Guid => 4,
        Labels.IpAddress => 5,
        Labels.Date or Labels.Time or Labels.DateTime or Labels.Duration or Labels.Set => 6,
        Labels.PhoneLike => 7,
        Labels.Money => 8,
        Labels.Percent => 9,
        Labels.Quantity => 10,
        Labels.Ordinal => 11,
        _ => 12,
    };

    // Longest first, then by type order, then earlier start; a result is dropped when it overlaps one already taken.
    private List<Recognition> Resolve()
    {
        List<Recognition> all = _found;
        var order = new int[all.Count];
        for (int i = 0; i < order.Length; i++)
        {
            order[i] = i;
        }

        Array.Sort(order, (a, b) =>
        {
            Recognition x = all[a];
            Recognition y = all[b];
            int c = y.Length.CompareTo(x.Length);
            if (c != 0)
            {
                return c;
            }

            c = Rank(x.Label).CompareTo(Rank(y.Label));
            if (c != 0)
            {
                return c;
            }

            c = x.Start.CompareTo(y.Start);
            if (c != 0)
            {
                return c;
            }

            c = y.Confidence.CompareTo(x.Confidence);
            return c != 0 ? c : a.CompareTo(b);
        });

        var taken = new ulong[(_t.Length + 63) / 64];
        var kept = new List<Recognition>();
        foreach (int index in order)
        {
            Recognition r = all[index];
            if (AnyTaken(taken, r.Start, r.End))
            {
                continue;
            }

            for (int i = r.Start; i < r.End; i++)
            {
                taken[i >> 6] |= 1UL << (i & 63);
            }

            kept.Add(r);
        }

        kept.Sort((a, b) => a.Start.CompareTo(b.Start));
        return kept;
    }

    private static bool AnyTaken(ulong[] taken, int start, int end)
    {
        for (int i = start; i < end; i++)
        {
            if ((taken[i >> 6] & (1UL << (i & 63))) != 0)
            {
                return true;
            }
        }

        return false;
    }
}
