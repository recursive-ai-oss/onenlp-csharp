using System.Globalization;
using System.Text;

namespace RecursiveAI.OneNlp.Tokenizers.Hf;

internal sealed partial class StageRegistry
{
    static partial void RegisterPreTokenizers(StageRegistry registry)
    {
        registry.AddPreTokenizer("BertPreTokenizer", _ => new BertPreTokenizer());
        registry.AddPreTokenizer("Whitespace", _ => new WhitespacePreTokenizer());
        registry.AddPreTokenizer("WhitespaceSplit", _ => new PredicateSplitPreTokenizer(CodePointRegex.IsWhiteSpace, SplitBehavior.Removed));
        registry.AddPreTokenizer("Punctuation", c => new PredicateSplitPreTokenizer(PreTokenizerChars.IsPunctuation, ParseBehavior(c, "behavior", SplitBehavior.Isolated)));
        registry.AddPreTokenizer("Metaspace", c => new MetaspacePreTokenizer(c));
        registry.AddPreTokenizer("ByteLevel", c => new ByteLevelPreTokenizer(c.Bool("add_prefix_space", true), c.Bool("use_regex", true)));
        registry.AddPreTokenizer("Split", c => new SplitPreTokenizer(c));
        registry.AddPreTokenizer("Digits", c => new PredicateSplitPreTokenizer(
            PreTokenizerChars.IsNumeric, c.Bool("individual_digits", false) ? SplitBehavior.Isolated : SplitBehavior.Contiguous));
        registry.AddPreTokenizer("UnicodeScripts", _ => new UnicodeScriptsPreTokenizer());
        registry.AddPreTokenizer("CharDelimiterSplit", c => new CharDelimiterPreTokenizer(c.String("delimiter")));
        registry.AddPreTokenizer("Sequence", c => new SequencePreTokenizer([.. c.Objects("pretokenizers").Select(registry.CreatePreTokenizer)]));
    }

    internal static SplitBehavior ParseBehavior(StageConfig c, string name, SplitBehavior defaultBehavior) => c.OptionalString(name) switch
    {
        null => defaultBehavior,
        "Removed" => SplitBehavior.Removed,
        "Isolated" => SplitBehavior.Isolated,
        "MergedWithPrevious" => SplitBehavior.MergedWithPrevious,
        "MergedWithNext" => SplitBehavior.MergedWithNext,
        "Contiguous" => SplitBehavior.Contiguous,
        _ => throw c.Bad(name, "Removed, Isolated, MergedWithPrevious, MergedWithNext or Contiguous"),
    };
}

/// <summary>What happens to the text a delimiter pattern matches, as the pre-tokenizer documentation lists the behaviours.</summary>
internal enum SplitBehavior
{
    /// <summary>The delimiters are dropped.</summary>
    Removed,

    /// <summary>Every delimiter and every stretch between them is its own piece.</summary>
    Isolated,

    /// <summary>A delimiter joins the piece before it.</summary>
    MergedWithPrevious,

    /// <summary>A delimiter joins the piece after it.</summary>
    MergedWithNext,

    /// <summary>Neighbouring delimiters form one piece.</summary>
    Contiguous,
}

/// <summary>Character classes the pre-tokenizers use.</summary>
internal static class PreTokenizerChars
{
    /// <summary>ASCII punctuation and symbols, or any Unicode punctuation: the BERT notion of punctuation.</summary>
    internal static bool IsPunctuation(int cp) =>
        cp is (>= 33 and <= 47) or (>= 58 and <= 64) or (>= 91 and <= 96) or (>= 123 and <= 126)
        || CodePointRegex.CategoryOf(cp) is UnicodeCategory.ConnectorPunctuation or UnicodeCategory.DashPunctuation or UnicodeCategory.OpenPunctuation
            or UnicodeCategory.ClosePunctuation or UnicodeCategory.InitialQuotePunctuation or UnicodeCategory.FinalQuotePunctuation or UnicodeCategory.OtherPunctuation;

    /// <summary>Any numeric character: decimal digits, letter numbers and other numbers.</summary>
    internal static bool IsNumeric(int cp) =>
        CodePointRegex.CategoryOf(cp) is UnicodeCategory.DecimalDigitNumber or UnicodeCategory.LetterNumber or UnicodeCategory.OtherNumber;
}

/// <summary>Cuts a normalized string at delimiter ranges according to a <see cref="SplitBehavior"/>.</summary>
internal static class Splitter
{
    /// <summary>Finds the ranges of single code points that satisfy a predicate.</summary>
    internal static List<OffsetPair> CharMatches(string text, Func<int, bool> predicate)
    {
        var matches = new List<OffsetPair>();
        for (int i = 0; i < text.Length;)
        {
            int cp = NormalizerHelpers.CodePointAt(text, i, out int w);
            if (predicate(cp))
            {
                matches.Add(new OffsetPair(i, i + w));
            }

            i += w;
        }

        return matches;
    }

    /// <summary>Splits a piece around delimiter matches.</summary>
    /// <param name="piece">The piece.</param>
    /// <param name="matches">Ascending, non-overlapping delimiter ranges inside the piece.</param>
    /// <param name="behavior">What to do with the delimiters.</param>
    /// <param name="invert">Treat the stretches between matches as the delimiters instead.</param>
    internal static List<NormalizedString> Split(NormalizedString piece, List<OffsetPair> matches, SplitBehavior behavior, bool invert = false)
    {
        // Alternate between the text before each match and the match itself, so the segments cover the whole piece.
        var segments = new List<(int Start, int End, bool IsMatch)>();
        int at = 0;
        foreach (OffsetPair m in matches)
        {
            if (m.Start > at)
            {
                segments.Add((at, m.Start, invert));
            }

            segments.Add((m.Start, m.End, !invert));
            at = m.End;
        }

        if (at < piece.Length)
        {
            segments.Add((at, piece.Length, invert));
        }

        var merged = new List<(int Start, int End)>();
        switch (behavior)
        {
            case SplitBehavior.Removed:
                foreach ((int s, int e, bool isMatch) in segments)
                {
                    if (!isMatch)
                    {
                        merged.Add((s, e));
                    }
                }

                break;
            case SplitBehavior.Isolated:
                foreach ((int s, int e, _) in segments)
                {
                    merged.Add((s, e));
                }

                break;
            case SplitBehavior.MergedWithPrevious:
                {
                    bool previousMatch = false;
                    foreach ((int s, int e, bool isMatch) in segments)
                    {
                        if (isMatch && !previousMatch && merged.Count > 0)
                        {
                            merged[^1] = (merged[^1].Start, e);
                        }
                        else
                        {
                            merged.Add((s, e));
                        }

                        previousMatch = isMatch;
                    }

                    break;
                }

            case SplitBehavior.MergedWithNext:
                {
                    bool previousMatch = false;
                    for (int i = segments.Count - 1; i >= 0; i--)
                    {
                        (int s, int e, bool isMatch) = segments[i];
                        if (isMatch && !previousMatch && merged.Count > 0)
                        {
                            merged[^1] = (s, merged[^1].End);
                        }
                        else
                        {
                            merged.Add((s, e));
                        }

                        previousMatch = isMatch;
                    }

                    merged.Reverse();
                    break;
                }

            case SplitBehavior.Contiguous:
                {
                    bool previousMatch = false;
                    foreach ((int s, int e, bool isMatch) in segments)
                    {
                        if (isMatch == previousMatch && merged.Count > 0)
                        {
                            merged[^1] = (merged[^1].Start, e);
                        }
                        else
                        {
                            merged.Add((s, e));
                        }

                        previousMatch = isMatch;
                    }

                    break;
                }
        }

        var parts = new List<NormalizedString>(merged.Count);
        foreach ((int s, int e) in merged)
        {
            parts.Add(piece.Slice(s, e));
        }

        return parts;
    }
}

/// <summary>Splits at every character a predicate accepts, with a chosen behaviour.</summary>
internal sealed class PredicateSplitPreTokenizer(Func<int, bool> predicate, SplitBehavior behavior) : IPreTokenizer
{
    public void PreTokenize(PreTokenizedString text) =>
        text.SplitEach(piece => Splitter.Split(piece, Splitter.CharMatches(piece.Text, predicate), behavior));
}

/// <summary>Splits on whitespace and puts each punctuation character in a piece of its own.</summary>
internal sealed class BertPreTokenizer : IPreTokenizer
{
    public void PreTokenize(PreTokenizedString text) => text.SplitEach(piece =>
    {
        var words = new List<NormalizedString>();
        foreach (NormalizedString word in Splitter.Split(piece, Splitter.CharMatches(piece.Text, CodePointRegex.IsWhiteSpace), SplitBehavior.Removed))
        {
            words.AddRange(Splitter.Split(word, Splitter.CharMatches(word.Text, PreTokenizerChars.IsPunctuation), SplitBehavior.Isolated));
        }

        return words;
    });
}

/// <summary>Keeps runs of word characters and runs of other non-space characters as pieces, the pattern <c>\w+|[^\w\s]+</c>.</summary>
internal sealed class WhitespacePreTokenizer : IPreTokenizer
{
    private static readonly CodePointRegex Pattern = CodePointRegex.Parse(@"\w+|[^\w\s]+", rustWordClass: true);

    public void PreTokenize(PreTokenizedString text) =>
        text.SplitEach(piece => Splitter.Split(piece, Pattern.Matches(piece.Text), SplitBehavior.Removed, invert: true));
}

/// <summary>Splits at a one-character delimiter, which is dropped.</summary>
internal sealed class CharDelimiterPreTokenizer : IPreTokenizer
{
    private readonly int _delimiter;

    internal CharDelimiterPreTokenizer(string delimiter)
    {
        if (delimiter.Length == 0 || NormalizerHelpers.CodePointAt(delimiter, 0, out int w) < 0 || w != delimiter.Length)
        {
            throw new InvalidDataException("The delimiter of a CharDelimiterSplit must be a single character.");
        }

        _delimiter = NormalizerHelpers.CodePointAt(delimiter, 0, out _);
    }

    public void PreTokenize(PreTokenizedString text) =>
        text.SplitEach(piece => Splitter.Split(piece, Splitter.CharMatches(piece.Text, cp => cp == _delimiter), SplitBehavior.Removed));
}

/// <summary>Applies pre-tokenizers one after the other, each to the pieces the one before produced.</summary>
internal sealed class SequencePreTokenizer(IPreTokenizer[] steps) : IPreTokenizer
{
    public void PreTokenize(PreTokenizedString text)
    {
        foreach (IPreTokenizer step in steps)
        {
            step.PreTokenize(text);
        }
    }
}

/// <summary>Splits at a literal string or a pattern, with a chosen behaviour and optional inversion.</summary>
internal sealed class SplitPreTokenizer : IPreTokenizer
{
    private readonly string _literal;
    private readonly CodePointRegex? _regex;
    private readonly SplitBehavior _behavior;
    private readonly bool _invert;

    internal SplitPreTokenizer(StageConfig c)
    {
        string pattern = c.Pattern("pattern", out bool isRegex);
        _regex = isRegex ? CodePointRegex.Parse(pattern) : null;
        _literal = pattern;
        _behavior = StageRegistry.ParseBehavior(c, "behavior", SplitBehavior.Removed);
        _invert = c.Bool("invert", false);
        if (!isRegex && pattern.Length == 0)
        {
            throw c.Bad("pattern", "not empty");
        }
    }

    public void PreTokenize(PreTokenizedString text) => text.SplitEach(piece => Splitter.Split(piece, Find(piece.Text), _behavior, _invert));

    private List<OffsetPair> Find(string s)
    {
        if (_regex is not null)
        {
            return _regex.Matches(s);
        }

        var result = new List<OffsetPair>();
        int from = 0;
        while (from <= s.Length - _literal.Length)
        {
            int at = s.IndexOf(_literal, from, StringComparison.Ordinal);
            if (at < 0)
            {
                break;
            }

            result.Add(new OffsetPair(at, at + _literal.Length));
            from = at + _literal.Length;
        }

        return result;
    }
}

/// <summary>
/// The pre-tokenizer of byte-level BPE: optionally a space in front, a split with the GPT-2 pattern, and every byte of the text
/// rewritten as the printable character of the byte table. Each byte keeps the origin of the character it came from.
/// </summary>
internal sealed class ByteLevelPreTokenizer(bool addPrefixSpace, bool useRegex) : IPreTokenizer
{
    // The GPT-2 pattern as the published BPE papers' code lists it: contractions, letters, numbers, other symbols, then whitespace.
    private static readonly CodePointRegex Gpt2 = CodePointRegex.Parse(@"'s|'t|'re|'ve|'m|'ll|'d| ?\p{L}+| ?\p{N}+| ?[^\s\p{L}\p{N}]+|\s+(?!\S)|\s+");

    public void PreTokenize(PreTokenizedString text) => text.SplitEach(piece =>
    {
        if (addPrefixSpace && !piece.Text.StartsWith(' '))
        {
            new PrependNormalizer(" ").Normalize(piece);
        }

        IEnumerable<NormalizedString> parts = useRegex ? Splitter.Split(piece, Gpt2.Matches(piece.Text), SplitBehavior.Isolated) : [piece];
        var result = new List<NormalizedString>();
        foreach (NormalizedString part in parts)
        {
            new ByteLevelNormalizer().Normalize(part);
            result.Add(part);
        }

        return result;
    });
}

/// <summary>Replaces spaces with a visible mark and splits in front of every mark, as SentencePiece-style tokenizers do.</summary>
internal sealed class MetaspacePreTokenizer : IPreTokenizer
{
    private readonly string _replacement;
    private readonly char _mark;
    private readonly string _scheme;
    private readonly bool _split;

    internal MetaspacePreTokenizer(StageConfig c)
    {
        _replacement = c.OptionalString("replacement") ?? "\u2581";
        if (_replacement.Length != 1)
        {
            throw c.Bad("replacement", "a single character");
        }

        _mark = _replacement[0];
        _scheme = c.OptionalString("prepend_scheme") ?? (c.Bool("add_prefix_space", true) ? "always" : "never");
        if (_scheme is not ("first" or "always" or "never"))
        {
            throw c.Bad("prepend_scheme", "first, always or never");
        }

        _split = c.Bool("split", true);
    }

    public void PreTokenize(PreTokenizedString text) => text.SplitEach((index, piece) =>
    {
        var b = new NormalizedStringBuilder();
        for (int k = 0; k < piece.Length; k++)
        {
            b.Append(piece.Text[k] == ' ' ? _mark : piece.Text[k], piece.AlignmentAt(k));
        }

        b.CopyTo(piece);
        if ((_scheme == "always" || (_scheme == "first" && piece.ToOriginal(0, 1).Start == 0)) && !piece.Text.StartsWith(_mark))
        {
            new PrependNormalizer(_replacement).Normalize(piece);
        }

        return _split
            ? Splitter.Split(piece, Splitter.CharMatches(piece.Text, cp => cp == _mark), SplitBehavior.MergedWithNext)
            : [piece];
    });
}

/// <summary>Splits where the Unicode script changes. Spaces never start a new piece, and Hiragana, Katakana and the prolonged sound mark count as Han.</summary>
internal sealed class UnicodeScriptsPreTokenizer : IPreTokenizer
{
    private const int AnyScript = -1;

    public void PreTokenize(PreTokenizedString text) => text.SplitEach(piece => Cut(piece));

    private static List<NormalizedString> Cut(NormalizedString piece)
    {
        // A space, and a character without a script, never starts a new piece; they stay with the piece before them. A piece made of
        // spaces alone is dropped. All of this is as observed in output of tokenizers 0.22.1.
        var parts = new List<NormalizedString>();
        string s = piece.Text;
        int start = 0;
        int last = int.MinValue;
        for (int i = 0; i < s.Length;)
        {
            int cp = NormalizerHelpers.CodePointAt(s, i, out int w);
            int script = FixedScript(cp);
            if (script != AnyScript)
            {
                if (last != script)
                {
                    Add(parts, piece, start, i);
                    start = i;
                }

                last = script;
            }

            i += w;
        }

        Add(parts, piece, start, s.Length);
        return parts;
    }

    private static void Add(List<NormalizedString> parts, NormalizedString piece, int start, int end)
    {
        if (end > start && piece.Text.AsSpan(start, end - start).ContainsAnyExcept(' '))
        {
            parts.Add(piece.Slice(start, end));
        }
    }

    private static int FixedScript(int cp)
    {
        if (cp == 0x30FC)
        {
            return ScriptIndex("Han");
        }

        int script = ScriptData.ScriptOf(cp);
        if (cp == ' ' || script == 0)
        {
            return AnyScript;
        }

        string name = ScriptData.Names[script];
        return name is "Hiragana" or "Katakana" ? ScriptIndex("Han") : script;
    }

    private static int ScriptIndex(string name) => Array.IndexOf(ScriptData.Names, name);
}
