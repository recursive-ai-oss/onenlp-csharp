using System.Text;
using System.Text.Json;

namespace RecursiveAI.OneNlp.Data;

/// <summary>What <see cref="JsonlReader"/> does with an entity or span whose edges are not token boundaries.</summary>
public enum MisalignedSpanPolicy
{
    /// <summary>Reject the line with a <see cref="DataFormatException"/>.</summary>
    Error = 0,

    /// <summary>Drop that one entity or span and keep the rest of the line.</summary>
    Skip = 1,
}

/// <summary>Settings for <see cref="JsonlReader"/>.</summary>
public sealed class JsonlOptions
{
    /// <summary>The longest line accepted, in UTF-16 units. Default 16,777,216.</summary>
    public int MaxLineLength { get; init; } = 16 << 20;

    /// <summary>Splits texts that do not give their own <c>tokens</c>. Default: <see cref="RuleWordTokenizer"/>.</summary>
    public IWordTokenizer Tokenizer { get; init; } = new RuleWordTokenizer();

    /// <summary>What to do with entities and spans that do not start and end on token boundaries. Default: <see cref="MisalignedSpanPolicy.Error"/>.</summary>
    public MisalignedSpanPolicy Misaligned { get; init; } = MisalignedSpanPolicy.Error;
}

/// <summary>
/// Reads JSON Lines training data: one JSON object per line with <c>text</c> and optionally <c>tokens</c>, <c>entities</c>,
/// <c>spans</c> and <c>cats</c>, as specified in <c>docs/data-formats.md</c>. Lazy and bounded; a bad line is a
/// <see cref="DataFormatException"/> with its line number. Blank lines are skipped.
/// </summary>
public static class JsonlReader
{
    private static readonly JsonDocumentOptions ParseOptions = new() { MaxDepth = 16, AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow };

    /// <summary>Reads a file as examples. Each is a gold document with an unannotated copy to predict into.</summary>
    /// <param name="path">The file. It is opened when enumeration starts and closed when it ends or is disposed.</param>
    /// <param name="strings">The string table for the documents; a new one when <see langword="null"/>.</param>
    /// <param name="options">Limits and tokenizer; defaults when <see langword="null"/>.</param>
    /// <returns>One example per non-blank line.</returns>
    /// <exception cref="DataFormatException">(While enumerating) a line is malformed.</exception>
    public static IEnumerable<Example> Read(string path, StringStore? strings = null, JsonlOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        return ReadFile(path, strings ?? new StringStore(), options ?? new JsonlOptions());
    }

    /// <summary>Reads a UTF-8 stream as examples.</summary>
    /// <param name="stream">The stream; read to its end and left open.</param>
    /// <param name="strings">The string table for the documents; a new one when <see langword="null"/>.</param>
    /// <param name="options">Limits and tokenizer; defaults when <see langword="null"/>.</param>
    /// <returns>One example per non-blank line.</returns>
    /// <exception cref="DataFormatException">(While enumerating) a line is malformed.</exception>
    public static IEnumerable<Example> Read(Stream stream, StringStore? strings = null, JsonlOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return ReadReader(new StreamReader(stream, new UTF8Encoding(false, true), false, 16384, true), strings ?? new StringStore(), options ?? new JsonlOptions());
    }

    /// <summary>Reads text as examples.</summary>
    /// <param name="reader">The reader; read to its end and left open.</param>
    /// <param name="strings">The string table for the documents; a new one when <see langword="null"/>.</param>
    /// <param name="options">Limits and tokenizer; defaults when <see langword="null"/>.</param>
    /// <returns>One example per non-blank line.</returns>
    /// <exception cref="DataFormatException">(While enumerating) a line is malformed.</exception>
    public static IEnumerable<Example> Read(TextReader reader, StringStore? strings = null, JsonlOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return ReadReader(reader, strings ?? new StringStore(), options ?? new JsonlOptions());
    }

    private static IEnumerable<Example> ReadFile(string path, StringStore strings, JsonlOptions options)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 16384, FileOptions.SequentialScan);
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), false, 16384);
        foreach (Example e in ReadReader(reader, strings, options))
        {
            yield return e;
        }
    }

    private static IEnumerable<Example> ReadReader(TextReader reader, StringStore strings, JsonlOptions options)
    {
        var lines = new BoundedLineReader(reader, options.MaxLineLength);
        string? line;
        while ((line = lines.ReadLine()) is not null)
        {
            if (lines.LineNumber == 1 && line.Length > 0 && line[0] == '\uFEFF')
            {
                line = line[1..];
            }

            if (line.AsSpan().Trim().Length == 0)
            {
                continue;
            }

            yield return ParseLine(line, lines.LineNumber, strings, options);
        }
    }

    private static Example ParseLine(string line, int lineNumber, StringStore strings, JsonlOptions options)
    {
        JsonDocument json;
        try
        {
            json = JsonDocument.Parse(line, ParseOptions);
        }
        catch (JsonException e)
        {
            throw new DataFormatException(lineNumber, "invalid JSON: " + e.Message);
        }

        using (json)
        {
            JsonElement root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new DataFormatException(lineNumber, "a line must be a JSON object");
            }

            if (!root.TryGetProperty("text", out JsonElement textElement) || textElement.ValueKind != JsonValueKind.String)
            {
                throw new DataFormatException(lineNumber, "'text' must be a string");
            }

            string text = textElement.GetString()!;
            Doc doc = MakeDoc(root, text, lineNumber, strings, options);

            if (root.TryGetProperty("entities", out JsonElement entities))
            {
                List<SpanSpec> specs = ParseSpans(entities, "entities", doc, lineNumber, options.Misaligned);
                specs.Sort(static (a, b) => a.Start.CompareTo(b.Start));
                for (int i = 1; i < specs.Count; i++)
                {
                    if (specs[i].Start < specs[i - 1].End)
                    {
                        throw new DataFormatException(lineNumber, "entities overlap");
                    }
                }

                doc.SetEntities(specs.ToArray());
            }

            Dictionary<string, IReadOnlyList<SpanSpec>>? groups = null;
            if (root.TryGetProperty("spans", out JsonElement spans))
            {
                if (spans.ValueKind != JsonValueKind.Object)
                {
                    throw new DataFormatException(lineNumber, "'spans' must be an object of span lists");
                }

                groups = new Dictionary<string, IReadOnlyList<SpanSpec>>(StringComparer.Ordinal);
                foreach (JsonProperty group in spans.EnumerateObject())
                {
                    groups[group.Name] = ParseSpans(group.Value, "spans." + group.Name, doc, lineNumber, options.Misaligned);
                }
            }

            Dictionary<string, float>? cats = null;
            if (root.TryGetProperty("cats", out JsonElement catsElement))
            {
                if (catsElement.ValueKind != JsonValueKind.Object)
                {
                    throw new DataFormatException(lineNumber, "'cats' must be an object of label scores");
                }

                cats = new Dictionary<string, float>(StringComparer.Ordinal);
                foreach (JsonProperty cat in catsElement.EnumerateObject())
                {
                    float value = cat.Value.ValueKind switch
                    {
                        JsonValueKind.True => 1f,
                        JsonValueKind.False => 0f,
                        JsonValueKind.Number => cat.Value.TryGetSingle(out float parsed) ? parsed : float.NaN,
                        _ => throw new DataFormatException(lineNumber, $"cats.{cat.Name} must be a number or a boolean"),
                    };
                    if (!(value >= 0f && value <= 1f))
                    {
                        throw new DataFormatException(lineNumber, $"cats.{cat.Name} must be from 0 to 1");
                    }

                    cats[cat.Name] = value;
                }
            }

            return Example.FromReference(doc, cats, groups);
        }
    }

    private static Doc MakeDoc(JsonElement root, string text, int lineNumber, StringStore strings, JsonlOptions options)
    {
        if (!root.TryGetProperty("tokens", out JsonElement tokens))
        {
            return options.Tokenizer.Tokenize(text, strings);
        }

        if (tokens.ValueKind != JsonValueKind.Array)
        {
            throw new DataFormatException(lineNumber, "'tokens' must be an array of [start, end] pairs");
        }

        var boundaries = new List<TokenBoundary>();
        foreach (JsonElement pair in tokens.EnumerateArray())
        {
            if (!TryPair(pair, out int start, out int end) || start < 0 || end <= start || end > text.Length)
            {
                throw new DataFormatException(lineNumber, "each token must be [start, end] inside the text with start < end");
            }

            boundaries.Add(new TokenBoundary(start, end - start));
        }

        try
        {
            return new Doc(strings, text, boundaries.ToArray());
        }
        catch (ArgumentException e)
        {
            throw new DataFormatException(lineNumber, "tokens must be in order and must not overlap: " + e.Message);
        }
    }

    private static bool TryPair(JsonElement pair, out int start, out int end)
    {
        start = 0;
        end = 0;
        if (pair.ValueKind != JsonValueKind.Array || pair.GetArrayLength() != 2)
        {
            return false;
        }

        JsonElement a = pair[0];
        JsonElement b = pair[1];
        return a.ValueKind == JsonValueKind.Number && b.ValueKind == JsonValueKind.Number && a.TryGetInt32(out start) && b.TryGetInt32(out end);
    }

    private static List<SpanSpec> ParseSpans(JsonElement list, string what, Doc doc, int lineNumber, MisalignedSpanPolicy policy)
    {
        if (list.ValueKind != JsonValueKind.Array)
        {
            throw new DataFormatException(lineNumber, $"'{what}' must be an array of [start, end, label]");
        }

        var result = new List<SpanSpec>();
        foreach (JsonElement item in list.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Array || item.GetArrayLength() != 3
                || item[0].ValueKind != JsonValueKind.Number || item[1].ValueKind != JsonValueKind.Number || item[2].ValueKind != JsonValueKind.String
                || !item[0].TryGetInt32(out int start) || !item[1].TryGetInt32(out int end))
            {
                throw new DataFormatException(lineNumber, $"each entry of '{what}' must be [start, end, \"label\"] with integer offsets");
            }

            string label = item[2].GetString()!;
            if (label.Length == 0 || start < 0 || end <= start || end > doc.Text.Length)
            {
                throw new DataFormatException(lineNumber, $"an entry of '{what}' has an empty label or offsets outside the text");
            }

            if (doc.TryGetCharSpan(start, end, out Span span))
            {
                result.Add(new SpanSpec(span.Start, span.End, label));
            }
            else if (policy == MisalignedSpanPolicy.Error)
            {
                throw new DataFormatException(lineNumber, $"an entry of '{what}' ({start}, {end}, \"{label}\") does not start and end on token boundaries");
            }
        }

        return result;
    }
}
