using System.Buffers;
using System.Text;
using System.Text.Json;

namespace RecursiveAI.OneNlp.Matching;

/// <summary>The error for pattern text that is not valid JSON or does not follow the pattern format.</summary>
public sealed class PatternFormatException : FormatException
{
    /// <summary>Creates the exception.</summary>
    public PatternFormatException()
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What is wrong, with the line (for JSON Lines) where known.</param>
    public PatternFormatException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and the error that caused it.</summary>
    /// <param name="message">What is wrong.</param>
    /// <param name="innerException">The cause.</param>
    public PatternFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates the exception with the position of the error.</summary>
    /// <param name="message">What is wrong.</param>
    /// <param name="lineNumber">The line of the input, counted from 1, or <see langword="null"/> when not known.</param>
    /// <param name="bytePositionInLine">The UTF-8 byte offset in that line, counted from 0, or <see langword="null"/> when not known.</param>
    /// <param name="innerException">The cause, or <see langword="null"/>.</param>
    public PatternFormatException(string message, long? lineNumber, long? bytePositionInLine, Exception? innerException)
        : base(message, innerException)
    {
        LineNumber = lineNumber;
        BytePositionInLine = bytePositionInLine;
    }

    /// <summary>The line of the input where the error is, counted from 1 like <c>DataFormatException.LineNumber</c>; <see langword="null"/> when the error is not tied to a line (for example a rule that is well-formed JSON but not a pattern, in a single JSON document).</summary>
    public long? LineNumber { get; }

    /// <summary>The UTF-8 byte offset in <see cref="LineNumber"/> where the JSON reader stopped, counted from 0 as in <see cref="System.Text.Json.JsonException.BytePositionInLine"/>; <see langword="null"/> when not known.</summary>
    public long? BytePositionInLine { get; }

    /// <summary>Wraps an error from reading or validating pattern text, with the position when the cause is a JSON syntax error.</summary>
    /// <param name="prefix">Text before the cause's message.</param>
    /// <param name="cause">What went wrong while reading.</param>
    /// <param name="line">The line, counted from 1, when the input is read line by line and the line is known; otherwise the JSON error's own line is used.</param>
    internal static PatternFormatException From(string prefix, Exception cause, long? line = null)
    {
        long? lineNumber = line;
        long? bytePosition = null;
        if (cause is JsonException json)
        {
            bytePosition = json.BytePositionInLine;
            lineNumber ??= json.LineNumber is { } l ? l + 1 : null;
        }

        return new PatternFormatException(prefix + PlainMessage(cause), lineNumber, bytePosition, cause);
    }

    // ArgumentException appends " (Parameter 'name')" to its message; that names an internal argument of the parser, not something the caller wrote.
    private static string PlainMessage(Exception cause)
    {
        string message = cause.Message;
        if (cause is ArgumentException { ParamName: { Length: > 0 } name })
        {
            int at = message.LastIndexOf(" (", StringComparison.Ordinal);
            if (at > 0 && message.EndsWith($"'{name}')", StringComparison.Ordinal))
            {
                message = message[..at];
            }
        }

        return message;
    }
}

/// <summary>An entity rule: a label, an optional ID, and either a token pattern or a phrase.</summary>
public sealed class EntityRulerPattern
{
    private EntityRulerPattern(string label, string? id, Pattern? tokens, string? phrase)
    {
        Label = label;
        Id = id;
        Tokens = tokens;
        Phrase = phrase;
    }

    /// <summary>The entity label given to matches.</summary>
    public string Label { get; }

    /// <summary>An ID shared by the patterns of one entity, or <see langword="null"/>.</summary>
    public string? Id { get; }

    /// <summary>The token pattern, or <see langword="null"/> for a phrase rule.</summary>
    public Pattern? Tokens { get; }

    /// <summary>The phrase text, or <see langword="null"/> for a token-pattern rule.</summary>
    public string? Phrase { get; }

    /// <summary>Makes a rule that matches a token pattern.</summary>
    /// <param name="label">The entity label; not empty.</param>
    /// <param name="pattern">The pattern.</param>
    /// <param name="id">An optional entity ID.</param>
    /// <exception cref="ArgumentException">The label is empty.</exception>
    /// <returns>The rule.</returns>
    public static EntityRulerPattern ForTokens(string label, Pattern pattern, string? id = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(label);
        ArgumentNullException.ThrowIfNull(pattern);
        return new EntityRulerPattern(label, id, pattern, null);
    }

    /// <summary>Makes a rule that matches a phrase, compared token by token.</summary>
    /// <param name="label">The entity label; not empty.</param>
    /// <param name="phrase">The phrase text; not empty.</param>
    /// <param name="id">An optional entity ID.</param>
    /// <exception cref="ArgumentException">The label or phrase is empty.</exception>
    /// <returns>The rule.</returns>
    public static EntityRulerPattern ForPhrase(string label, string phrase, string? id = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(label);
        ArgumentException.ThrowIfNullOrEmpty(phrase);
        return new EntityRulerPattern(label, id, null, phrase);
    }
}

/// <summary>
/// Reads and writes patterns as JSON, in the layout spaCy documents for token patterns and for entity-ruler pattern files.
/// </summary>
/// <remarks>
/// <para>
/// <b>Token pattern:</b> an array of objects, one per token position. Keys are attribute names (<c>ORTH</c>, <c>TEXT</c>, <c>LOWER</c>,
/// <c>LENGTH</c>, <c>IS_ALPHA</c>, ..., any letter case) and <c>OP</c>. A value is a string, an integer (<c>LENGTH</c>, <c>ENT_IOB</c>), a
/// boolean (the flags), or an object of predicates: <c>REGEX</c> (string), <c>IN</c> and <c>NOT_IN</c> (arrays of strings, or integers for
/// <c>LENGTH</c>), <c>IS_SUBSET</c>, <c>IS_SUPERSET</c> and <c>INTERSECTS</c> (arrays of strings, <c>MORPH</c> only) and <c>==</c>, <c>!=</c>,
/// <c>&gt;=</c>, <c>&lt;=</c>, <c>&gt;</c>, <c>&lt;</c> (integers, <c>LENGTH</c> only). Extension attributes (<c>_</c>) are not supported.
/// </para>
/// <para>
/// <b>Entity rules:</b> a JSON array of objects, or JSON Lines (one object per non-blank line), each <c>{"label": "ORG", "pattern": ..., "id": ...}</c>
/// where <c>pattern</c> is a phrase string or a token pattern and <c>id</c> is optional. Other keys are ignored.
/// </para>
/// <para>
/// Readers reject nesting deeper than 16 levels, patterns longer than <see cref="Pattern.MaxPositions"/> positions, more than 64 tests on
/// one position, regular expressions longer than 4096 characters or with counted repeats above 1000, and input larger than the stated byte limit,
/// before allocating for them. Every problem is a <see cref="PatternFormatException"/>.
/// </para>
/// </remarks>
public static class PatternJson
{
    /// <summary>The default limit on input size for the stream reader: 256 MiB.</summary>
    public const int DefaultMaxBytes = 1 << 28;

    private const int MaxDepth = 16;
    private const int MaxTestsPerPosition = 64;
    private const int MaxRegexLength = 4096;

    private static readonly JsonReaderOptions ReaderOptions = new() { MaxDepth = MaxDepth };

    /// <summary>Parses one token pattern.</summary>
    /// <param name="json">An array of token objects.</param>
    /// <exception cref="PatternFormatException">The text is not a valid pattern.</exception>
    /// <returns>The pattern.</returns>
    public static Pattern ParsePattern(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        IReadOnlyList<Pattern> all = ParsePatterns(json);
        return all.Count == 1 && IsSinglePattern(json) ? all[0] : throw new PatternFormatException("Expected one pattern (an array of token objects).");
    }

    /// <summary>Parses one pattern or a list of patterns.</summary>
    /// <param name="json">An array of token objects, or an array of such arrays.</param>
    /// <exception cref="PatternFormatException">The text is not valid.</exception>
    /// <returns>The patterns; at least one.</returns>
    public static IReadOnlyList<Pattern> ParsePatterns(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Guard(() => ParsePatterns(Encoding.UTF8.GetBytes(json)));
    }

    /// <summary>Parses one pattern or a list of patterns from UTF-8 JSON.</summary>
    /// <param name="utf8">An array of token objects, or an array of such arrays.</param>
    /// <exception cref="PatternFormatException">The text is not valid.</exception>
    /// <returns>The patterns; at least one.</returns>
    public static IReadOnlyList<Pattern> ParsePatterns(ReadOnlySpan<byte> utf8)
    {
        try
        {
            var reader = new Utf8JsonReader(utf8, ReaderOptions);
            Next(ref reader);
            RequireToken(ref reader, JsonTokenType.StartArray, "a pattern must be an array");
            Next(ref reader);
            var result = new List<Pattern>();
            if (reader.TokenType == JsonTokenType.StartObject)
            {
                result.Add(ReadPatternBody(ref reader));
            }
            else if (reader.TokenType == JsonTokenType.StartArray)
            {
                while (reader.TokenType == JsonTokenType.StartArray)
                {
                    result.Add(ReadPatternBody(ref reader, nested: true));
                    Next(ref reader);
                }

                RequireToken(ref reader, JsonTokenType.EndArray, "expected a pattern array or the end of the list");
            }
            else
            {
                throw new PatternFormatException("A pattern needs at least one token object.");
            }

            if (reader.Read())
            {
                throw new PatternFormatException("Unexpected data after the pattern.");
            }

            return result;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or ArgumentException or FormatException and not PatternFormatException)
        {
            throw PatternFormatException.From("Invalid pattern JSON: ", e);
        }
    }

    /// <summary>Reads entity rules from UTF-8 text: a JSON array of rule objects, or JSON Lines.</summary>
    /// <param name="utf8">The text.</param>
    /// <exception cref="PatternFormatException">A rule is invalid; for JSON Lines the message names the line.</exception>
    /// <returns>The rules in order.</returns>
    public static IReadOnlyList<EntityRulerPattern> ReadEntityPatterns(ReadOnlySpan<byte> utf8)
    {
        var result = new List<EntityRulerPattern>();
        int first = 0;
        while (first < utf8.Length && utf8[first] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
        {
            first++;
        }

        if (first == utf8.Length)
        {
            return result;
        }

        if (utf8[first] == (byte)'[')
        {
            try
            {
                var reader = new Utf8JsonReader(utf8, ReaderOptions);
                Next(ref reader);
                Next(ref reader);
                while (reader.TokenType == JsonTokenType.StartObject)
                {
                    result.Add(ReadEntityRule(ref reader));
                    Next(ref reader);
                }

                RequireToken(ref reader, JsonTokenType.EndArray, "expected a rule object or the end of the list");
                if (reader.Read())
                {
                    throw new PatternFormatException("Unexpected data after the list.");
                }
            }
            catch (Exception e) when (e is JsonException or InvalidOperationException or ArgumentException or FormatException and not PatternFormatException)
            {
                throw PatternFormatException.From("Invalid pattern JSON: ", e);
            }

            return result;
        }

        int line = 0;
        int position = 0;
        while (position < utf8.Length)
        {
            int newline = utf8[position..].IndexOf((byte)'\n');
            int end = newline < 0 ? utf8.Length : position + newline;
            ReadOnlySpan<byte> text = utf8[position..end];
            position = end + 1;
            line++;
            if (text.IsEmpty || text.Trim((byte)' ').Trim((byte)'\r').Trim((byte)'\t').IsEmpty)
            {
                continue;
            }

            try
            {
                var reader = new Utf8JsonReader(text, ReaderOptions);
                Next(ref reader);
                RequireToken(ref reader, JsonTokenType.StartObject, "a rule must be an object");
                result.Add(ReadEntityRule(ref reader));
                if (reader.Read())
                {
                    throw new PatternFormatException("Unexpected data after the rule.");
                }
            }
            catch (Exception e) when (e is JsonException or InvalidOperationException or ArgumentException or FormatException)
            {
                throw PatternFormatException.From($"Line {line}: ", e, line);
            }
        }

        return result;
    }

    /// <summary>Reads entity rules from a stream of UTF-8 text.</summary>
    /// <param name="stream">The stream, read to its end.</param>
    /// <param name="maxBytes">The most bytes to accept; <see cref="DefaultMaxBytes"/> by default.</param>
    /// <exception cref="PatternFormatException">The input is longer than <paramref name="maxBytes"/> or a rule is invalid.</exception>
    /// <returns>The rules in order.</returns>
    public static IReadOnlyList<EntityRulerPattern> ReadEntityPatterns(Stream stream, int maxBytes = DefaultMaxBytes)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentOutOfRangeException.ThrowIfNegative(maxBytes);
        using var buffer = new MemoryStream();
        byte[] chunk = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            int read;
            while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
            {
                if (buffer.Length + read > maxBytes)
                {
                    throw new PatternFormatException($"The pattern file is longer than {maxBytes} bytes.");
                }

                buffer.Write(chunk, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(chunk);
        }

        return ReadEntityPatterns(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
    }

    /// <summary>Writes a token pattern as a JSON array.</summary>
    /// <param name="writer">The destination.</param>
    /// <param name="pattern">The pattern.</param>
    public static void Write(Utf8JsonWriter writer, Pattern pattern)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(pattern);
        writer.WriteStartArray();
        foreach (TokenSpec spec in pattern)
        {
            writer.WriteStartObject();
            foreach (Constraint c in spec.Constraints)
            {
                WriteConstraint(writer, c);
            }

            if (spec.Quantifier.ToString() is { Length: > 0 } op)
            {
                writer.WriteString("OP", op);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    /// <summary>Writes a token pattern as a JSON string.</summary>
    /// <param name="pattern">The pattern.</param>
    /// <returns>The JSON text.</returns>
    public static string ToJson(Pattern pattern)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            Write(writer, pattern);
        }

        return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
    }

    /// <summary>Writes entity rules as JSON Lines, one rule per line, UTF-8 without a byte-order mark.</summary>
    /// <param name="stream">The destination.</param>
    /// <param name="rules">The rules.</param>
    public static void WriteEntityPatterns(Stream stream, IEnumerable<EntityRulerPattern> rules)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(rules);
        foreach (EntityRulerPattern rule in rules)
        {
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("label", rule.Label);
                if (rule.Tokens is { } tokens)
                {
                    writer.WritePropertyName("pattern");
                    Write(writer, tokens);
                }
                else
                {
                    writer.WriteString("pattern", rule.Phrase);
                }

                if (rule.Id is not null)
                {
                    writer.WriteString("id", rule.Id);
                }

                writer.WriteEndObject();
            }

            stream.WriteByte((byte)'\n');
        }
    }

    private static bool IsSinglePattern(string json)
    {
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json), ReaderOptions);
        return reader.Read() && reader.Read() && reader.TokenType == JsonTokenType.StartObject;
    }

    private static T Guard<T>(Func<T> parse)
    {
        try
        {
            return parse();
        }
        catch (PatternFormatException)
        {
            throw;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or ArgumentException or FormatException)
        {
            throw PatternFormatException.From("Invalid pattern JSON: ", e);
        }
    }

    private static void Next(ref Utf8JsonReader reader)
    {
        if (!reader.Read())
        {
            throw new PatternFormatException("The JSON ends too soon.");
        }
    }

    private static void RequireToken(ref Utf8JsonReader reader, JsonTokenType type, string message)
    {
        if (reader.TokenType != type)
        {
            throw new PatternFormatException(message + ".");
        }
    }

    // Reads token objects until the pattern's closing bracket. On entry the reader is at the first object (flat form) or at the
    // pattern's own opening bracket (nested form, when called from the list-of-patterns loop).
    private static Pattern ReadPatternBody(ref Utf8JsonReader reader, bool nested = false)
    {
        var specs = new List<TokenSpec>();
        if (nested)
        {
            Next(ref reader);
        }

        while (reader.TokenType == JsonTokenType.StartObject)
        {
            if (specs.Count >= Pattern.MaxPositions)
            {
                throw new PatternFormatException($"A pattern has at most {Pattern.MaxPositions} token positions.");
            }

            specs.Add(ReadTokenSpec(ref reader));
            Next(ref reader);
        }

        RequireToken(ref reader, JsonTokenType.EndArray, "expected a token object or the end of the pattern");
        if (specs.Count == 0)
        {
            throw new PatternFormatException("A pattern needs at least one token object.");
        }

        return Pattern.Token([.. specs]);
    }

    private static TokenSpec ReadTokenSpec(ref Utf8JsonReader reader)
    {
        var spec = new TokenSpec();
        int tests = 0;
        while (true)
        {
            Next(ref reader);
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return spec;
            }

            string name = reader.GetString() ?? string.Empty;
            Next(ref reader);
            if (string.Equals(name, "OP", StringComparison.OrdinalIgnoreCase))
            {
                RequireToken(ref reader, JsonTokenType.String, "OP must be a string");
                string op = reader.GetString() ?? string.Empty;
                if (op.Length == 0)
                {
                    throw new PatternFormatException("OP must not be empty; leave it out for exactly one token.");
                }

                spec.Op(op);
                continue;
            }

            if (!TokenProperties.TryParse(name, out TokenProperty attribute))
            {
                throw new PatternFormatException(name.StartsWith('_')
                    ? "Extension attributes (_) are not supported."
                    : $"Unknown token attribute '{Truncate(name)}'.");
            }

            if (++tests > MaxTestsPerPosition)
            {
                throw new PatternFormatException($"A token position has at most {MaxTestsPerPosition} tests.");
            }

            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    spec.Is(attribute, reader.GetString() ?? string.Empty);
                    break;
                case JsonTokenType.Number:
                    spec.Is(attribute, ReadInt(ref reader));
                    break;
                case JsonTokenType.True:
                case JsonTokenType.False:
                    spec.Is(attribute, reader.TokenType == JsonTokenType.True);
                    break;
                case JsonTokenType.StartObject:
                    ReadPredicates(ref reader, spec, attribute, ref tests);
                    break;
                default:
                    throw new PatternFormatException($"The value of {name} must be a string, number, boolean or predicate object.");
            }
        }
    }

    private static void ReadPredicates(ref Utf8JsonReader reader, TokenSpec spec, TokenProperty attribute, ref int tests)
    {
        while (true)
        {
            Next(ref reader);
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return;
            }

            string predicate = (reader.GetString() ?? string.Empty).ToUpperInvariant();
            Next(ref reader);
            if (++tests > MaxTestsPerPosition)
            {
                throw new PatternFormatException($"A token position has at most {MaxTestsPerPosition} tests.");
            }

            switch (predicate)
            {
                case "REGEX":
                    RequireToken(ref reader, JsonTokenType.String, "REGEX must be a string");
                    string expression = reader.GetString() ?? string.Empty;
                    CheckRegex(expression);
                    spec.Matches(attribute, expression);
                    break;
                case "IN" or "NOT_IN":
                    ReadSet(ref reader, spec, attribute, negate: predicate == "NOT_IN");
                    break;
                case "IS_SUBSET" or "IS_SUPERSET" or "INTERSECTS":
                    if (attribute != TokenProperty.Morph)
                    {
                        throw new PatternFormatException($"{predicate} applies to MORPH only.");
                    }

                    string[] features = ReadStrings(ref reader);
                    _ = predicate switch
                    {
                        "IS_SUBSET" => spec.MorphIsSubset(features),
                        "IS_SUPERSET" => spec.MorphIsSuperset(features),
                        _ => spec.MorphIntersects(features),
                    };
                    break;
                case "==" or "!=" or ">=" or "<=" or ">" or "<":
                    Comparison cmp = predicate switch
                    {
                        "==" => Comparison.Equal,
                        "!=" => Comparison.NotEqual,
                        ">=" => Comparison.GreaterOrEqual,
                        "<=" => Comparison.LessOrEqual,
                        ">" => Comparison.Greater,
                        _ => Comparison.Less,
                    };
                    spec.Compare(attribute, cmp, ReadInt(ref reader));
                    break;
                default:
                    throw new PatternFormatException($"Unknown predicate '{Truncate(predicate)}'.");
            }
        }
    }

    private static void ReadSet(ref Utf8JsonReader reader, TokenSpec spec, TokenProperty attribute, bool negate)
    {
        RequireToken(ref reader, JsonTokenType.StartArray, "IN and NOT_IN take an array");
        if (attribute == TokenProperty.Length)
        {
            var numbers = new List<int>();
            while (true)
            {
                Next(ref reader);
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    break;
                }

                numbers.Add(ReadInt(ref reader));
            }

            _ = negate ? spec.NotIn(attribute, [.. numbers]) : spec.In(attribute, [.. numbers]);
            return;
        }

        string[] values = ReadStringsBody(ref reader);
        _ = negate ? spec.NotIn(attribute, values) : spec.In(attribute, values);
    }

    private static string[] ReadStrings(ref Utf8JsonReader reader)
    {
        RequireToken(ref reader, JsonTokenType.StartArray, "expected an array of strings");
        return ReadStringsBody(ref reader);
    }

    private static string[] ReadStringsBody(ref Utf8JsonReader reader)
    {
        var values = new List<string>();
        while (true)
        {
            Next(ref reader);
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                return [.. values];
            }

            RequireToken(ref reader, JsonTokenType.String, "expected a string in the array");
            values.Add(reader.GetString() ?? string.Empty);
        }
    }

    private static int ReadInt(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out int value))
        {
            throw new PatternFormatException("Expected an integer.");
        }

        return value;
    }

    private static void CheckRegex(string expression)
    {
        if (expression.Length > MaxRegexLength)
        {
            throw new PatternFormatException($"A regular expression is at most {MaxRegexLength} characters.");
        }

        // A counted repeat such as {5000} would make the matcher build a huge automaton; spaCy patterns never need one.
        for (int i = 0; i < expression.Length; i++)
        {
            if (expression[i] != '{')
            {
                continue;
            }

            int digits = 0;
            for (int j = i + 1; j < expression.Length && (char.IsAsciiDigit(expression[j]) || expression[j] == ','); j++)
            {
                digits = expression[j] == ',' ? 0 : digits + 1;
                if (digits > 3)
                {
                    throw new PatternFormatException("A regular expression's counted repeat is at most 999.");
                }
            }
        }
    }

    private static EntityRulerPattern ReadEntityRule(ref Utf8JsonReader reader)
    {
        string? label = null;
        string? id = null;
        string? phrase = null;
        Pattern? tokens = null;
        while (true)
        {
            Next(ref reader);
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            string name = reader.GetString() ?? string.Empty;
            Next(ref reader);
            switch (name)
            {
                case "label":
                    RequireToken(ref reader, JsonTokenType.String, "label must be a string");
                    label = reader.GetString();
                    break;
                case "id":
                    if (reader.TokenType == JsonTokenType.Null)
                    {
                        id = null;
                    }
                    else
                    {
                        RequireToken(ref reader, JsonTokenType.String, "id must be a string");
                        id = reader.GetString();
                    }

                    break;
                case "pattern":
                    if (reader.TokenType == JsonTokenType.String)
                    {
                        phrase = reader.GetString();
                        tokens = null;
                    }
                    else if (reader.TokenType == JsonTokenType.StartArray)
                    {
                        Next(ref reader);
                        tokens = ReadPatternBody(ref reader);
                        phrase = null;
                    }
                    else
                    {
                        throw new PatternFormatException("pattern must be a string or an array of token objects.");
                    }

                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        if (string.IsNullOrEmpty(label))
        {
            throw new PatternFormatException("A rule needs a non-empty label.");
        }

        if (tokens is not null)
        {
            return EntityRulerPattern.ForTokens(label, tokens, id);
        }

        if (string.IsNullOrEmpty(phrase))
        {
            throw new PatternFormatException("A rule needs a pattern.");
        }

        return EntityRulerPattern.ForPhrase(label, phrase, id);
    }

    private static void WriteConstraint(Utf8JsonWriter writer, Constraint c)
    {
        string name = TokenProperties.NameOf(c.Attribute);
        switch (c.Test)
        {
            case Test.Same when TokenProperties.IsFlag(c.Attribute):
                writer.WriteBoolean(name, c.Flag);
                break;
            case Test.Same when c.Attribute == TokenProperty.Length:
                writer.WriteNumber(name, c.Number);
                break;
            case Test.Same:
                writer.WriteString(name, c.Text);
                break;
            default:
                writer.WriteStartObject(name);
                switch (c.Test)
                {
                    case Test.Regex:
                        writer.WriteString("REGEX", c.Text);
                        break;
                    case Test.In or Test.NotIn:
                        writer.WriteStartArray(c.Test == Test.In ? "IN" : "NOT_IN");
                        if (c.Numbers is { } numbers)
                        {
                            foreach (int v in numbers)
                            {
                                writer.WriteNumberValue(v);
                            }
                        }
                        else
                        {
                            foreach (string v in c.Texts!)
                            {
                                writer.WriteStringValue(v);
                            }
                        }

                        writer.WriteEndArray();
                        break;
                    case Test.Compare:
                        writer.WriteNumber(
                            c.Comparison switch
                            {
                                Comparison.Equal => "==",
                                Comparison.NotEqual => "!=",
                                Comparison.GreaterOrEqual => ">=",
                                Comparison.LessOrEqual => "<=",
                                Comparison.Greater => ">",
                                _ => "<",
                            },
                            c.Number);
                        break;
                    default:
                        writer.WriteStartArray(c.Test switch { Test.IsSubset => "IS_SUBSET", Test.IsSuperset => "IS_SUPERSET", _ => "INTERSECTS" });
                        foreach (string v in c.Texts!)
                        {
                            writer.WriteStringValue(v);
                        }

                        writer.WriteEndArray();
                        break;
                }

                writer.WriteEndObject();
                break;
        }
    }

    private static string Truncate(string text) => text.Length <= 40 ? text : text[..40] + "...";
}
