using System.Text.Json;

namespace RecursiveAI.OneNlp.Neural;

/// <summary>Typed, bounded reads of settings objects in a saved pipeline. Every failure is an <see cref="InvalidDataException"/> naming the field.</summary>
internal static class JsonSettings
{
    public static JsonElement Property(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out JsonElement value))
        {
            throw new InvalidDataException($"The saved pipeline is missing '{name}'.");
        }

        return value;
    }

    public static int Int(JsonElement obj, string name, int min, int max)
    {
        JsonElement e = Property(obj, name);
        if (e.ValueKind != JsonValueKind.Number || !e.TryGetInt32(out int value) || value < min || value > max)
        {
            throw new InvalidDataException($"'{name}' must be an integer from {min} to {max}.");
        }

        return value;
    }

    public static ulong ULong(JsonElement obj, string name)
    {
        JsonElement e = Property(obj, name);
        if (e.ValueKind != JsonValueKind.Number || !e.TryGetUInt64(out ulong value))
        {
            throw new InvalidDataException($"'{name}' must be a non-negative integer.");
        }

        return value;
    }

    public static string String(JsonElement obj, string name, int maxLength = 256)
    {
        JsonElement e = Property(obj, name);
        string? value = e.ValueKind == JsonValueKind.String ? e.GetString() : null;
        if (value is null || value.Length > maxLength)
        {
            throw new InvalidDataException($"'{name}' must be a string of at most {maxLength} characters.");
        }

        return value;
    }

    public static string[] Strings(JsonElement obj, string name, int maxCount, int maxLength = 256)
    {
        JsonElement e = Property(obj, name);
        if (e.ValueKind != JsonValueKind.Array || e.GetArrayLength() > maxCount)
        {
            throw new InvalidDataException($"'{name}' must be an array of at most {maxCount} strings.");
        }

        var result = new string[e.GetArrayLength()];
        int i = 0;
        foreach (JsonElement item in e.EnumerateArray())
        {
            string? s = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
            if (s is null || s.Length > maxLength)
            {
                throw new InvalidDataException($"'{name}' must hold strings of at most {maxLength} characters.");
            }

            result[i++] = s;
        }

        return result;
    }
}
