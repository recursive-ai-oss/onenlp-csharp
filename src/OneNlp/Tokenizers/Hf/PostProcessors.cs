namespace RecursiveAI.OneNlp.Tokenizers.Hf;

internal sealed partial class StageRegistry
{
    static partial void RegisterPostProcessors(StageRegistry registry)
    {
        registry.AddPostProcessor("TemplateProcessing", c => new TemplateProcessor(c));
        registry.AddPostProcessor("BertProcessing", c => new BertProcessor(ReadPair(c, "sep"), ReadPair(c, "cls")));
        registry.AddPostProcessor("RobertaProcessing", c => new RobertaProcessor(ReadPair(c, "sep"), ReadPair(c, "cls"), c.Bool("trim_offsets", true), c.Bool("add_prefix_space", true)));
        registry.AddPostProcessor("ByteLevel", c => new ByteLevelProcessor(c.Bool("trim_offsets", true), c.Bool("add_prefix_space", true)));
        registry.AddPostProcessor("Sequence", c => new SequenceProcessor([.. c.Objects("processors").Select(p => (PostProcessorBase)registry.CreatePostProcessor(p))]));
    }

    // The sep and cls of Bert- and RobertaProcessing are written as [token, id].
    private static (string Token, int Id) ReadPair(StageConfig c, string name)
    {
        if (!c.Has(name))
        {
            throw c.Missing(name);
        }

        System.Text.Json.JsonElement e = c.Element.GetProperty(name);
        if (e.ValueKind != System.Text.Json.JsonValueKind.Array || e.GetArrayLength() != 2
            || e[0].ValueKind != System.Text.Json.JsonValueKind.String || !e[1].TryGetInt32(out int id))
        {
            throw c.Bad(name, "a [token, id] pair");
        }

        return (e[0].GetString()!, id);
    }
}

/// <summary>
/// Base of the post-processors: they work on the list of one or two encodings, as Sequence needs, and the interface method joins what is
/// left into one encoding.
/// </summary>
internal abstract class PostProcessorBase : IPostProcessor
{
    public abstract int AddedTokenCount(bool isPair);

    /// <summary>Processes one encoding (or a pair) into one encoding (template processors) or the same number (offset trimming).</summary>
    internal abstract List<TokenEncoding> ProcessEncodings(List<TokenEncoding> encodings, bool addSpecialTokens);

    public TokenEncoding Process(TokenEncoding encoding, TokenEncoding? pair, bool addSpecialTokens)
    {
        List<TokenEncoding> result = ProcessEncodings(pair is null ? [encoding] : [encoding, pair], addSpecialTokens);
        return result.Count == 1 ? result[0] : EncodingParts.Concatenate(result);
    }
}

/// <summary>Helpers that build encodings from parts.</summary>
internal static class EncodingParts
{
    internal static TokenEncoding Concatenate(IReadOnlyList<TokenEncoding> parts)
    {
        var ids = new List<int>();
        var tokens = new List<string>();
        var offsets = new List<TokenOffset>();
        var special = new List<bool>();
        var types = new List<int>();
        var attention = new List<int>();
        var words = new List<int>();
        foreach (TokenEncoding p in parts)
        {
            ids.AddRange(p.Ids);
            tokens.AddRange(p.Tokens);
            offsets.AddRange(p.Offsets);
            special.AddRange(p.SpecialTokensMask);
            types.AddRange(p.TypeIds);
            attention.AddRange(p.AttentionMask);
            words.AddRange(p.WordIds);
        }

        return new TokenEncoding([.. ids], [.. tokens], [.. offsets], [.. special], [.. types], [.. attention], [.. words]);
    }

    /// <summary>A part made of special tokens: empty offsets, special mask set, no word.</summary>
    internal static TokenEncoding Special(IReadOnlyList<int> ids, IReadOnlyList<string> tokens, int typeId)
    {
        int n = ids.Count;
        var types = new int[n];
        Array.Fill(types, typeId);
        var special = new bool[n];
        Array.Fill(special, true);
        return new TokenEncoding([.. ids], [.. tokens], new TokenOffset[n], special, types);
    }

    internal static TokenEncoding WithTypeId(TokenEncoding e, int typeId)
    {
        var types = new int[e.Length];
        Array.Fill(types, typeId);
        return new TokenEncoding(e.Ids, e.Tokens, e.Offsets, e.SpecialTokensMask, types, e.AttentionMask, e.WordIds);
    }

    /// <summary>
    /// Moves the start of each token's offsets past the leading space marks of its text and the end before the trailing ones, so that a
    /// byte-level token such as "Ġhello" covers "hello", not " hello".
    /// </summary>
    internal static TokenEncoding TrimOffsets(TokenEncoding e, bool addPrefixSpace)
    {
        var offsets = new TokenOffset[e.Length];
        for (int i = 0; i < offsets.Length; i++)
        {
            TokenOffset o = e.Offsets[i];
            string t = e.Tokens[i];
            int leading = 0;
            while (leading < t.Length && IsSpaceMark(t[leading]))
            {
                leading++;
            }

            int trailing = 0;
            while (trailing < t.Length && IsSpaceMark(t[t.Length - 1 - trailing]))
            {
                trailing++;
            }

            // The space the pre-tokenizer put in front of the text is not counted as leading (observed in output of tokenizers 0.22.1:
            // a lone prefix-space token keeps its start and loses its end, giving an empty span at the start of the text).
            if (i == 0 && addPrefixSpace && leading > 0)
            {
                leading--;
            }

            int start = o.Start;
            int end = o.End;
            if (leading > 0)
            {
                start = Math.Min(start + leading, end);
            }

            if (trailing > 0 && i == 0 && addPrefixSpace && t.Length == 1)
            {
                // The prefix space stands for the first character of the text, which may be two UTF-16 units wide: the span is empty.
                end = start;
            }
            else if (trailing > 0 && end >= trailing)
            {
                end = Math.Max(end - trailing, start);
            }

            offsets[i] = new TokenOffset(start, end);
        }

        return new TokenEncoding(e.Ids, e.Tokens, offsets, e.SpecialTokensMask, e.TypeIds, e.AttentionMask, e.WordIds);
    }

    // 'Ġ' is the byte-level mark for a space; real whitespace characters count too.
    private static bool IsSpaceMark(char c) => c == '\u0120' || CodePointRegex.IsWhiteSpace(c);
}

/// <summary>Only trims offsets of byte-level tokens; adds nothing.</summary>
internal sealed class ByteLevelProcessor(bool trimOffsets, bool addPrefixSpace) : PostProcessorBase
{
    public override int AddedTokenCount(bool isPair) => 0;

    internal override List<TokenEncoding> ProcessEncodings(List<TokenEncoding> encodings, bool addSpecialTokens) =>
        trimOffsets ? [.. encodings.Select(e => EncodingParts.TrimOffsets(e, addPrefixSpace))] : encodings;
}

/// <summary>[CLS] A [SEP] and [CLS] A [SEP] B [SEP]; the second sequence and its separator get type 1.</summary>
internal sealed class BertProcessor((string Token, int Id) sep, (string Token, int Id) cls) : PostProcessorBase
{
    public override int AddedTokenCount(bool isPair) => isPair ? 3 : 2;

    internal override List<TokenEncoding> ProcessEncodings(List<TokenEncoding> encodings, bool addSpecialTokens)
    {
        if (!addSpecialTokens)
        {
            return encodings;
        }

        var parts = new List<TokenEncoding>
        {
            EncodingParts.Special([cls.Id], [cls.Token], 0),
            EncodingParts.WithTypeId(encodings[0], 0),
            EncodingParts.Special([sep.Id], [sep.Token], 0),
        };
        if (encodings.Count > 1)
        {
            parts.Add(EncodingParts.WithTypeId(encodings[1], 1));
            parts.Add(EncodingParts.Special([sep.Id], [sep.Token], 1));
        }

        return [EncodingParts.Concatenate(parts)];
    }
}

/// <summary>&lt;s&gt; A &lt;/s&gt; and &lt;s&gt; A &lt;/s&gt;&lt;/s&gt; B &lt;/s&gt;, all with type 0, optionally trimming byte-level offsets first.</summary>
internal sealed class RobertaProcessor((string Token, int Id) sep, (string Token, int Id) cls, bool trimOffsets, bool addPrefixSpace) : PostProcessorBase
{
    public override int AddedTokenCount(bool isPair) => isPair ? 4 : 2;

    internal override List<TokenEncoding> ProcessEncodings(List<TokenEncoding> encodings, bool addSpecialTokens)
    {
        List<TokenEncoding> es = trimOffsets ? [.. encodings.Select(e => EncodingParts.TrimOffsets(e, addPrefixSpace))] : encodings;
        if (!addSpecialTokens)
        {
            // Observed in output of tokenizers 0.22.1: pairs carry type id 0 throughout, special tokens or not.
            return [.. es.Select(e => EncodingParts.WithTypeId(e, 0))];
        }

        var parts = new List<TokenEncoding>
        {
            EncodingParts.Special([cls.Id], [cls.Token], 0),
            EncodingParts.WithTypeId(es[0], 0),
            EncodingParts.Special([sep.Id], [sep.Token], 0),
        };
        if (es.Count > 1)
        {
            parts.Add(EncodingParts.Special([sep.Id], [sep.Token], 0));
            parts.Add(EncodingParts.WithTypeId(es[1], 0));
            parts.Add(EncodingParts.Special([sep.Id], [sep.Token], 0));
        }

        return [EncodingParts.Concatenate(parts)];
    }
}

/// <summary>A template of special tokens and sequences, with a type ID for each piece.</summary>
internal sealed class TemplateProcessor : PostProcessorBase
{
    private readonly List<Piece> _single;
    private readonly List<Piece> _pair;
    private readonly Dictionary<string, (int[] Ids, string[] Tokens)> _specials = new(StringComparer.Ordinal);

    private readonly record struct Piece(bool IsSequence, bool IsSecond, string SpecialName, int TypeId);

    internal TemplateProcessor(StageConfig c)
    {
        if (c.Object("special_tokens") is { } specials)
        {
            foreach (System.Text.Json.JsonProperty p in specials.Element.EnumerateObject())
            {
                var s = new StageConfig(p.Value, c.Registry);
                int[] ids = [.. s.Element.GetProperty("ids").EnumerateArray().Select(e => e.GetInt32())];
                string[] tokens = [.. s.Element.GetProperty("tokens").EnumerateArray().Select(e => e.GetString() ?? string.Empty)];
                if (ids.Length != tokens.Length)
                {
                    throw s.Bad("ids", "as long as tokens");
                }

                _specials[p.Name] = (ids, tokens);
            }
        }

        _single = ReadTemplate(c, "single");
        _pair = ReadTemplate(c, "pair");
    }

    public override int AddedTokenCount(bool isPair) =>
        (isPair ? _pair : _single).Where(p => !p.IsSequence).Sum(p => _specials[p.SpecialName].Ids.Length);

    internal override List<TokenEncoding> ProcessEncodings(List<TokenEncoding> encodings, bool addSpecialTokens)
    {
        List<Piece> template = encodings.Count > 1 ? _pair : _single;
        var parts = new List<TokenEncoding>();
        foreach (Piece p in template)
        {
            if (p.IsSequence)
            {
                int which = p.IsSecond ? 1 : 0;
                if (which < encodings.Count)
                {
                    parts.Add(EncodingParts.WithTypeId(encodings[which], p.TypeId));
                }
            }
            else if (addSpecialTokens)
            {
                (int[] ids, string[] tokens) = _specials[p.SpecialName];
                parts.Add(EncodingParts.Special(ids, tokens, p.TypeId));
            }
        }

        return [EncodingParts.Concatenate(parts)];
    }

    private List<Piece> ReadTemplate(StageConfig c, string name)
    {
        var pieces = new List<Piece>();
        if (!c.Has(name))
        {
            return pieces;
        }

        System.Text.Json.JsonElement array = c.Element.GetProperty(name);
        if (array.ValueKind != System.Text.Json.JsonValueKind.Array)
        {
            throw c.Bad(name, "an array");
        }

        foreach (System.Text.Json.JsonElement item in array.EnumerateArray())
        {
            var node = new StageConfig(item, c.Registry);
            if (node.Object("SpecialToken") is { } st)
            {
                string id = st.String("id");
                if (!_specials.ContainsKey(id))
                {
                    throw c.Bad(name, $"made of known special tokens ('{id}' is not in special_tokens)");
                }

                pieces.Add(new Piece(false, false, id, st.Int("type_id", 0)));
            }
            else if (node.Object("Sequence") is { } sq)
            {
                string id = sq.String("id");
                if (id is not ("A" or "B"))
                {
                    throw c.Bad(name, "using sequence ids A and B");
                }

                pieces.Add(new Piece(true, id == "B", string.Empty, sq.Int("type_id", 0)));
            }
            else
            {
                throw c.Bad(name, "made of SpecialToken and Sequence pieces");
            }
        }

        return pieces;
    }
}

/// <summary>Applies post-processors one after the other to the list of encodings.</summary>
internal sealed class SequenceProcessor(PostProcessorBase[] steps) : PostProcessorBase
{
    public override int AddedTokenCount(bool isPair) => steps.Sum(s => s.AddedTokenCount(isPair));

    internal override List<TokenEncoding> ProcessEncodings(List<TokenEncoding> encodings, bool addSpecialTokens)
    {
        foreach (PostProcessorBase step in steps)
        {
            encodings = step.ProcessEncodings(encodings, addSpecialTokens);
        }

        return encodings;
    }
}
