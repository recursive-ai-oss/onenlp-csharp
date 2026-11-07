using System.Text.Json;
using System.Text.Json.Serialization;

namespace RecursiveAI.OneNlp.Serialization;

/// <summary>
/// Lets <c>System.Text.Json</c> write and read a <see cref="Doc"/> as the JSON <see cref="Doc.ToJson"/> produces (spaCy's <c>Doc.to_json</c>
/// shape), so a document can be a property of a DTO or the return value of an endpoint. Applied to <see cref="Doc"/> with an attribute,
/// so no options or serializer context are needed, and it uses no reflection.
/// </summary>
/// <remarks>Reading makes a document with a new <see cref="StringStore"/>; use <see cref="Doc.FromJson(string, StringStore?)"/> to share one.</remarks>
public sealed class DocJsonConverter : JsonConverter<Doc>
{
    /// <inheritdoc/>
    public override Doc? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        using JsonDocument json = JsonDocument.ParseValue(ref reader);
        try
        {
            return Doc.FromJson(System.Text.Encoding.UTF8.GetBytes(json.RootElement.GetRawText()));
        }
        catch (InvalidDataException e)
        {
            throw new JsonException(e.Message, e);
        }
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Doc value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteRawValue(value.ToJsonUtf8());
    }
}

/// <summary>
/// Writes a <see cref="Span"/> for <c>System.Text.Json</c> as <c>{"start", "end", "label", "text", "confidence"}</c>; <c>start</c> and <c>end</c> are
/// character offsets (UTF-16 units, end exclusive) into the document's text, and <c>label</c> is left out when the span has none.
/// Writing only: a span belongs to a document, so read the <see cref="Doc"/> and take its spans.
/// </summary>
/// <remarks>Unlike <see cref="Doc.ToJson"/>, which counts code points as spaCy does, these offsets index <see cref="Doc.Text"/> directly.</remarks>
public sealed class SpanJsonConverter : JsonConverter<Span>
{
    /// <inheritdoc/>
    /// <exception cref="NotSupportedException">Always: spans are not read on their own.</exception>
    public override Span Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException("A Span belongs to a Doc and cannot be read on its own; read the Doc.");

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Span value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStartObject();
        writer.WriteNumber("start", value.StartChar);
        writer.WriteNumber("end", value.EndChar);
        if (value.LabelId != 0)
        {
            writer.WriteString("label", value.Label);
        }

        writer.WriteString("text", value.TextSpan);
        writer.WriteNumber("confidence", value.Confidence);
        writer.WriteEndObject();
    }
}

/// <summary>
/// Writes a <see cref="Token"/> for <c>System.Text.Json</c> as <c>{"i", "start", "end", "text"}</c> plus <c>lemma</c>, <c>pos</c>, <c>tag</c>,
/// <c>morph</c> and <c>dep</c> when they are set and <c>head</c> (the head's index); <c>start</c> and <c>end</c> are character offsets (UTF-16 units,
/// end exclusive) into the document's text. Writing only.
/// </summary>
public sealed class TokenJsonConverter : JsonConverter<Token>
{
    /// <inheritdoc/>
    /// <exception cref="NotSupportedException">Always: tokens are not read on their own.</exception>
    public override Token Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException("A Token belongs to a Doc and cannot be read on its own; read the Doc.");

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Token value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStartObject();
        writer.WriteNumber("i", value.Index);
        writer.WriteNumber("start", value.Idx);
        writer.WriteNumber("end", value.EndIdx);
        writer.WriteString("text", value.TextSpan);
        Optional(writer, "lemma", value.Lemma);
        Optional(writer, "pos", value.Pos);
        Optional(writer, "tag", value.Tag);
        Optional(writer, "morph", value.Morph);
        Optional(writer, "dep", value.Dep);
        writer.WriteNumber("head", value.HeadIndex);
        writer.WriteEndObject();
    }

    private static void Optional(Utf8JsonWriter writer, string name, string value)
    {
        if (value.Length > 0)
        {
            writer.WriteString(name, value);
        }
    }
}
