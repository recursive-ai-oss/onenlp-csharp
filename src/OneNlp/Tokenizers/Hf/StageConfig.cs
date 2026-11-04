using System.Text.Json;

namespace RecursiveAI.OneNlp.Tokenizers.Hf;

/// <summary>
/// Read-only access to one JSON object of a <c>tokenizer.json</c> (a stage, a model, an added token), for the factories that build
/// stages from it. It wraps a <see cref="JsonElement"/> of a document that was parsed with a size and depth limit, so no reflection
/// is involved. Every accessor that cannot find or read what it is asked for throws <see cref="InvalidDataException"/> naming the
/// object's type and the field.
/// </summary>
internal readonly struct StageConfig
{
    private readonly JsonElement _element;

    internal StageConfig(JsonElement element, StageRegistry registry)
    {
        _element = element;
        Registry = registry;
    }

    /// <summary>The registry that nested stages (for example the members of a <c>Sequence</c>) are created from.</summary>
    internal StageRegistry Registry { get; }

    /// <summary>The raw element, for models that read large arrays or objects (a vocabulary) directly.</summary>
    internal JsonElement Element => _element;

    /// <summary>The object's <c>type</c> string, or the empty string when it has none.</summary>
    internal string Type => _element.ValueKind == JsonValueKind.Object && _element.TryGetProperty("type", out JsonElement t) && t.ValueKind == JsonValueKind.String
        ? t.GetString() ?? string.Empty
        : string.Empty;

    /// <summary>Whether the field is present and not <c>null</c>.</summary>
    /// <param name="name">The field name.</param>
    internal bool Has(string name) => _element.ValueKind == JsonValueKind.Object && _element.TryGetProperty(name, out JsonElement v) && v.ValueKind != JsonValueKind.Null;

    /// <summary>Gets a nested object, or <see langword="null"/> when the field is absent or <c>null</c>.</summary>
    /// <param name="name">The field name.</param>
    internal StageConfig? Object(string name)
    {
        if (!Has(name))
        {
            return null;
        }

        JsonElement v = _element.GetProperty(name);
        return v.ValueKind == JsonValueKind.Object ? new StageConfig(v, Registry) : throw Bad(name, "an object");
    }

    /// <summary>Gets the elements of an array of objects; an absent or <c>null</c> field is an empty array.</summary>
    /// <param name="name">The field name.</param>
    internal List<StageConfig> Objects(string name)
    {
        var result = new List<StageConfig>();
        if (!Has(name))
        {
            return result;
        }

        JsonElement v = _element.GetProperty(name);
        if (v.ValueKind != JsonValueKind.Array)
        {
            throw Bad(name, "an array");
        }

        foreach (JsonElement item in v.EnumerateArray())
        {
            result.Add(item.ValueKind == JsonValueKind.Object ? new StageConfig(item, Registry) : throw Bad(name, "an array of objects"));
        }

        return result;
    }

    /// <summary>Gets a required string field.</summary>
    /// <param name="name">The field name.</param>
    internal string String(string name) => OptionalString(name) ?? throw Missing(name);

    /// <summary>Gets a string field, or <see langword="null"/> when absent or <c>null</c>.</summary>
    /// <param name="name">The field name.</param>
    internal string? OptionalString(string name)
    {
        if (!Has(name))
        {
            return null;
        }

        JsonElement v = _element.GetProperty(name);
        return v.ValueKind == JsonValueKind.String ? v.GetString() : throw Bad(name, "a string");
    }

    /// <summary>Gets an integer field, or the default when absent or <c>null</c>.</summary>
    /// <param name="name">The field name.</param>
    /// <param name="defaultValue">The value when absent.</param>
    internal int Int(string name, int defaultValue) => OptionalInt(name) ?? defaultValue;

    /// <summary>Gets an integer field, or <see langword="null"/> when absent or <c>null</c>.</summary>
    /// <param name="name">The field name.</param>
    internal int? OptionalInt(string name)
    {
        if (!Has(name))
        {
            return null;
        }

        JsonElement v = _element.GetProperty(name);
        return v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int i) ? i : throw Bad(name, "a 32-bit integer");
    }

    /// <summary>Gets a boolean field, or the default when absent or <c>null</c>.</summary>
    /// <param name="name">The field name.</param>
    /// <param name="defaultValue">The value when absent.</param>
    internal bool Bool(string name, bool defaultValue)
    {
        if (!Has(name))
        {
            return defaultValue;
        }

        JsonElement v = _element.GetProperty(name);
        return v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : throw Bad(name, "a boolean");
    }

    /// <summary>Gets a string-or-object field like the <c>pattern</c> of a split, which is <c>{"String": "..."}</c> or <c>{"Regex": "..."}</c>.</summary>
    /// <param name="name">The field name.</param>
    /// <param name="isRegex">Whether it was the <c>Regex</c> form.</param>
    /// <returns>The pattern text.</returns>
    internal string Pattern(string name, out bool isRegex)
    {
        StageConfig p = Object(name) ?? throw Missing(name);
        if (p.OptionalString("Regex") is { } regex)
        {
            isRegex = true;
            return regex;
        }

        isRegex = false;
        return p.OptionalString("String") ?? throw Bad(name, "an object with \"String\" or \"Regex\"");
    }

    /// <summary>Builds the exception for a field that is not there.</summary>
    /// <param name="name">The field name.</param>
    internal InvalidDataException Missing(string name) => new($"The '{Describe()}' entry has no '{name}' field.");

    /// <summary>Builds the exception for a field of the wrong kind or an unusable value.</summary>
    /// <param name="name">The field name.</param>
    /// <param name="expected">What was expected.</param>
    internal InvalidDataException Bad(string name, string expected) => new($"The '{name}' field of the '{Describe()}' entry must be {expected}.");

    private string Describe() => Type is { Length: > 0 } t ? t : "tokenizer.json";
}
