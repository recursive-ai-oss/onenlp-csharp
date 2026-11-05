using System.Text;

namespace RecursiveAI.OneNlp.Tokenizers.Hf;

internal sealed partial class StageRegistry
{
    static partial void RegisterDecoders(StageRegistry registry)
    {
        registry.AddDecoder("WordPiece", c => new WordPieceDecoder(c.OptionalString("prefix") ?? "##", c.Bool("cleanup", true)));
        registry.AddDecoder("Metaspace", c => new MetaspaceDecoder(c));
        registry.AddDecoder("ByteLevel", _ => new ByteLevelDecoder());
        registry.AddDecoder("ByteFallback", _ => new ByteFallbackDecoder());
        registry.AddDecoder("Fuse", _ => new FuseDecoder());
        registry.AddDecoder("Strip", c => new StripDecoder(OneChar(c, "content"), c.Int("start", 0), c.Int("stop", 0)));
        registry.AddDecoder("Replace", c => new ReplaceDecoder(c.Pattern("pattern", out bool _), c.String("content")));
        registry.AddDecoder("BPEDecoder", c => new BpeDecoder(c.OptionalString("suffix") ?? "</w>"));
        registry.AddDecoder("CTC", c => new CtcDecoder(c.OptionalString("pad_token") ?? "<pad>", c.OptionalString("word_delimiter_token") ?? "|", c.Bool("cleanup", true)));
        registry.AddDecoder("Sequence", c => new SequenceDecoder([.. c.Objects("decoders").Select(registry.CreateDecoder)]));
    }

    private static char OneChar(StageConfig c, string name)
    {
        string s = c.String(name);
        return s.Length == 1 ? s[0] : throw c.Bad(name, "a single character");
    }
}

/// <summary>Applies decoders one after the other.</summary>
internal sealed class SequenceDecoder(ITokenDecoder[] steps) : ITokenDecoder
{
    /// <summary>The steps, in order.</summary>
    internal IReadOnlyList<ITokenDecoder> Steps => steps;

    public List<string> DecodeChain(List<string> tokens)
    {
        foreach (ITokenDecoder step in steps)
        {
            tokens = step.DecodeChain(tokens);
        }

        return tokens;
    }
}

/// <summary>Joins WordPiece tokens: a continuation prefix is removed, any other token after the first gets a space in front.</summary>
internal sealed class WordPieceDecoder(string prefix, bool cleanup) : ITokenDecoder
{
    /// <summary>The continuation prefix that is removed.</summary>
    internal string Prefix => prefix;

    /// <summary>Whether <see cref="Cleanup"/> is applied to every token.</summary>
    internal bool CleanupEnabled => cleanup;

    public List<string> DecodeChain(List<string> tokens)
    {
        var result = new List<string>(tokens.Count);
        for (int i = 0; i < tokens.Count; i++)
        {
            string t = tokens[i];
            if (i > 0)
            {
                t = t.StartsWith(prefix, StringComparison.Ordinal) ? t[prefix.Length..] : " " + t;
            }

            result.Add(cleanup ? Cleanup(t) : t);
        }

        return result;
    }

    /// <summary>
    /// Whether <see cref="Cleanup"/> could change the text: every pattern it replaces starts with a space followed by one of these
    /// characters, so a text without such a pair is unchanged and need not be rewritten.
    /// </summary>
    internal static bool CleanupMayChange(ReadOnlySpan<char> s)
    {
        for (int i = 0; i + 1 < s.Length; i++)
        {
            if (s[i] == ' ' && s[i + 1] is '.' or '?' or '!' or ',' or '\'' or 'n' or 'm' or 'd' or 's' or 'v' or 'r')
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Undoes the spaces a tokenizer put before punctuation and contractions.</summary>
    internal static string Cleanup(string s) => s
        .Replace(" .", ".", StringComparison.Ordinal)
        .Replace(" ?", "?", StringComparison.Ordinal)
        .Replace(" !", "!", StringComparison.Ordinal)
        .Replace(" ,", ",", StringComparison.Ordinal)
        .Replace(" ' ", "'", StringComparison.Ordinal)
        .Replace(" n't", "n't", StringComparison.Ordinal)
        .Replace(" 'm", "'m", StringComparison.Ordinal)
        .Replace(" do not", " don't", StringComparison.Ordinal)
        .Replace(" 's", "'s", StringComparison.Ordinal)
        .Replace(" 've", "'ve", StringComparison.Ordinal)
        .Replace(" 're", "'re", StringComparison.Ordinal);
}

/// <summary>Turns the visible space mark back into a space and drops the space the pre-tokenizer added in front of the first token.</summary>
internal sealed class MetaspaceDecoder : ITokenDecoder
{
    private readonly char _mark;
    private readonly bool _stripFirst;

    /// <summary>The character that stands for a space.</summary>
    internal char Mark => _mark;

    /// <summary>Whether the spaces at the start of the first token are removed.</summary>
    internal bool StripFirst => _stripFirst;

    internal MetaspaceDecoder(StageConfig c)
    {
        string r = c.OptionalString("replacement") ?? "\u2581";
        _mark = r.Length == 1 ? r[0] : throw c.Bad("replacement", "a single character");
        string scheme = c.OptionalString("prepend_scheme") ?? (c.Bool("add_prefix_space", true) ? "always" : "never");
        _stripFirst = scheme != "never";
    }

    public List<string> DecodeChain(List<string> tokens)
    {
        var result = new List<string>(tokens.Count);
        for (int i = 0; i < tokens.Count; i++)
        {
            string t = tokens[i].Replace(_mark, ' ');
            if (i == 0 && _stripFirst)
            {
                // Every leading space goes, not just one (observed in output of tokenizers 0.22.1).
                t = t.TrimStart(' ');
            }

            result.Add(t);
        }

        return result;
    }
}

/// <summary>Maps the characters of the byte table back to bytes and reads them as UTF-8; invalid sequences become U+FFFD.</summary>
internal sealed class ByteLevelDecoder : ITokenDecoder
{
    public List<string> DecodeChain(List<string> tokens)
    {
        var bytes = new List<byte>();
        foreach (string t in tokens)
        {
            foreach (char c in t)
            {
                int b = ByteLevelTable.ToByte(c);
                if (b >= 0)
                {
                    bytes.Add((byte)b);
                }
                else
                {
                    bytes.AddRange(Encoding.UTF8.GetBytes(c.ToString()));
                }
            }
        }

        return [Encoding.UTF8.GetString([.. bytes])];
    }
}

/// <summary>Reads runs of <c>&lt;0xHH&gt;</c> tokens as UTF-8; a run that is not valid UTF-8 becomes one U+FFFD per byte.</summary>
internal sealed class ByteFallbackDecoder : ITokenDecoder
{
    private static readonly UTF8Encoding Strict = new(false, true);

    public List<string> DecodeChain(List<string> tokens)
    {
        var result = new List<string>(tokens.Count);
        var run = new List<byte>();
        foreach (string t in tokens)
        {
            if (t.Length == 6 && t.StartsWith("<0x", StringComparison.Ordinal) && t[5] == '>'
                && byte.TryParse(t.AsSpan(3, 2), System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture, out byte b))
            {
                run.Add(b);
                continue;
            }

            Flush(run, result);
            result.Add(t);
        }

        Flush(run, result);
        return result;
    }

    private static void Flush(List<byte> run, List<string> result)
    {
        if (run.Count == 0)
        {
            return;
        }

        try
        {
            result.Add(Strict.GetString([.. run]));
        }
        catch (DecoderFallbackException)
        {
            result.Add(new string('\uFFFD', run.Count));
        }

        run.Clear();
    }
}

/// <summary>Joins all tokens into one.</summary>
internal sealed class FuseDecoder : ITokenDecoder
{
    public List<string> DecodeChain(List<string> tokens) => [string.Concat(tokens)];
}

/// <summary>Removes up to <c>start</c> leading and <c>stop</c> trailing copies of a character from every token.</summary>
internal sealed class StripDecoder(char content, int start, int stop) : ITokenDecoder
{
    /// <summary>The character removed.</summary>
    internal char Content => content;

    /// <summary>The most copies removed from the start of a token.</summary>
    internal int Start => start;

    /// <summary>The most copies removed from the end of a token.</summary>
    internal int Stop => stop;

    public List<string> DecodeChain(List<string> tokens)
    {
        var result = new List<string>(tokens.Count);
        foreach (string t in tokens)
        {
            int from = 0;
            while (from < t.Length && from < start && t[from] == content)
            {
                from++;
            }

            int to = t.Length;
            int removed = 0;
            while (to > from && removed < stop && t[to - 1] == content)
            {
                to--;
                removed++;
            }

            result.Add(t[from..to]);
        }

        return result;
    }
}

/// <summary>Replaces a literal string in every token.</summary>
internal sealed class ReplaceDecoder(string pattern, string content) : ITokenDecoder
{
    /// <summary>The literal string replaced.</summary>
    internal string Pattern => pattern;

    /// <summary>The replacement.</summary>
    internal string Content => content;

    public List<string> DecodeChain(List<string> tokens) =>
        [.. tokens.Select(t => pattern.Length == 0 ? t : t.Replace(pattern, content, StringComparison.Ordinal))];
}

/// <summary>Turns the end-of-word suffix of BPE tokens into a space, except on the last token.</summary>
internal sealed class BpeDecoder(string suffix) : ITokenDecoder
{
    public List<string> DecodeChain(List<string> tokens)
    {
        var result = new List<string>(tokens.Count);
        for (int i = 0; i < tokens.Count; i++)
        {
            result.Add(tokens[i].Replace(suffix, i == tokens.Count - 1 ? string.Empty : " ", StringComparison.Ordinal));
        }

        return result;
    }
}

/// <summary>
/// Collapses repeated frames and removes blanks, as connectionist temporal classification output needs. With cleanup the word delimiter
/// becomes a space and the ends are trimmed; without it the delimiter is left in place (observed in output of
/// tokenizers 0.22.1).
/// </summary>
internal sealed class CtcDecoder(string pad, string delimiter, bool cleanup) : ITokenDecoder
{
    public List<string> DecodeChain(List<string> tokens)
    {
        var kept = new List<string>();
        string? previous = null;
        foreach (string t in tokens)
        {
            if (string.Equals(t, previous, StringComparison.Ordinal))
            {
                continue;
            }

            previous = t;
            if (!string.Equals(t, pad, StringComparison.Ordinal))
            {
                kept.Add(t);
            }
        }

        if (!cleanup)
        {
            return kept;
        }

        return [string.Concat(kept).Replace(delimiter, " ", StringComparison.Ordinal).Trim()];
    }
}
