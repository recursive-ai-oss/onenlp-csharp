using System.Globalization;
using System.Text;

namespace RecursiveAI.OneNlp.Recognizers;

internal enum ItemKind : byte
{
    /// <summary>A run of letters and combining marks.</summary>
    Word,

    /// <summary>A run of ASCII digits.</summary>
    Digits,

    /// <summary>One other character (a surrogate pair counts as one).</summary>
    Other,
}

/// <summary>A lexical item of a text: <see cref="Start"/> and <see cref="End"/> are UTF-16 offsets. White space is never an item.</summary>
internal readonly record struct Item(int Start, int End, ItemKind Kind);

/// <summary>
/// The tiny lexer every recognizer rule is written against. Rule data and input text go through the same lexer, so a form such as
/// <c>km/h</c> or <c>per cent</c> in the data is the same item sequence as in the text.
/// </summary>
internal static class Lexer
{
    internal static void Lex(string text, List<Item> items)
    {
        int i = 0;
        while (i < text.Length)
        {
            char c = text[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
            }
            else if (c is >= '0' and <= '9')
            {
                int s = i;
                while (i < text.Length && text[i] is >= '0' and <= '9')
                {
                    i++;
                }

                items.Add(new Item(s, i, ItemKind.Digits));
            }
            else if (IsWordChar(c))
            {
                int s = i;
                while (i < text.Length && IsWordChar(text[i]))
                {
                    i++;
                }

                items.Add(new Item(s, i, ItemKind.Word));
            }
            else
            {
                int len = char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]) ? 2 : 1;
                items.Add(new Item(i, i + len, ItemKind.Other));
                i += len;
            }
        }
    }

    internal static bool IsWordChar(char c)
    {
        if (c < 0x80)
        {
            return (uint)((c | 0x20) - 'a') <= 'z' - 'a';
        }

        if (char.IsLetter(c))
        {
            return true;
        }

        UnicodeCategory cat = char.GetUnicodeCategory(c);
        return cat is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;
    }

    /// <summary>
    /// Appends the lookup key of items <c>[first, first + count)</c>: the item texts, with one space where the text had white space
    /// between two items and nothing where they touched. So <c>km/h</c> and <c>km / h</c> are different keys.
    /// </summary>
    internal static void AppendKey(StringBuilder sb, string text, List<Item> items, int first, int count)
    {
        for (int k = first; k < first + count; k++)
        {
            if (k > first && items[k].Start > items[k - 1].End)
            {
                sb.Append(' ');
            }

            sb.Append(text, items[k].Start, items[k].End - items[k].Start);
        }
    }

    /// <summary>The key of a rule-data form, and how many items it has. Returns 0 items for a form with no content.</summary>
    internal static string KeyOf(string form, out int itemCount)
    {
        var items = new List<Item>();
        Lex(form, items);
        itemCount = items.Count;
        var sb = new StringBuilder(form.Length);
        AppendKey(sb, form, items, 0, items.Count);
        return sb.ToString();
    }
}

/// <summary>Forms matched by longest key, some case-sensitive (written <c>=Form</c> in rule data) and the rest compared in lower case.</summary>
internal sealed class FormTable<T>
{
    private const int MaxFormItems = 16;

    private readonly Dictionary<string, List<T>> _exact = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<T>> _folded = new(StringComparer.Ordinal);

    /// <summary>The most items in any form.</summary>
    internal int MaxItems { get; private set; }

    internal int Count => _exact.Count + _folded.Count;

    internal void Add(string form, T value)
    {
        bool caseSensitive = form.Length > 1 && form[0] == '=';
        string key = Lexer.KeyOf(caseSensitive ? form[1..] : form, out int count);
        if (count == 0)
        {
            throw new RecognizerRulesException($"The form '{form}' has no content.");
        }

        if (count > MaxFormItems)
        {
            throw new RecognizerRulesException($"The form '{form}' has more than {MaxFormItems} words and symbols.");
        }

        MaxItems = Math.Max(MaxItems, count);
        Dictionary<string, List<T>> table = caseSensitive ? _exact : _folded;
        string k = caseSensitive ? key : key.ToLowerInvariant();
        if (!table.TryGetValue(k, out List<T>? list))
        {
            table[k] = list = [];
        }

        list.Add(value);
    }

    /// <summary>Finds the entries for a key made from the text: the case-sensitive ones for the exact key, and the others for its lower case.</summary>
    internal bool TryGet(string key, out List<T> entries)
    {
        entries = [];
        bool found = false;
        if (_exact.Count > 0 && _exact.TryGetValue(key, out List<T>? a))
        {
            entries.AddRange(a);
            found = true;
        }

        if (_folded.Count > 0 && _folded.TryGetValue(key.ToLowerInvariant(), out List<T>? b))
        {
            entries.AddRange(b);
            found = true;
        }

        return found;
    }
}
