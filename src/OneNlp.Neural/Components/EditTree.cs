using System.Text;

namespace RecursiveAI.OneNlp.Neural.Components;

/// <summary>
/// An edit tree (Müller, Cotterell, Fraser and Schütze 2015, "Joint Lemmatization and Morphological Tagging with Lemming", §3.1):
/// the rule that turns a word form into its lemma, as a recursive description. Two words with the same inflection pattern share a tree,
/// so a classifier can choose among a few thousand trees instead of generating lemmas.
/// </summary>
/// <remarks>
/// A <b>match</b> node finds the longest substring the form and the lemma share. It keeps that substring, and describes the part of
/// the form before it (<c>PrefixLength</c> characters) and after it (<c>SuffixLength</c> characters) with two sub-trees, which turn those parts
/// of the form into the parts of the lemma before and after the shared substring. A <b>substitution</b> node (the leaf) replaces one
/// whole string by another, and applies only to a form equal to its original. Applying a tree to a form it does not fit returns
/// <see langword="null"/>: the form is too short for a match node, or a substitution's original differs.
/// </remarks>
internal sealed class EditTree
{
    /// <summary>Forms longer than this are not analysed for common substrings; their tree is a single substitution.</summary>
    internal const int MaxAnalysedLength = 48;

    /// <summary>Longest serialized tree accepted on load.</summary>
    internal const int MaxSerializedLength = 4096;

    private const int MaxDepth = 64;

    private EditTree(int prefixLength, int suffixLength, EditTree? left, EditTree? right, string original, string replacement)
    {
        PrefixLength = prefixLength;
        SuffixLength = suffixLength;
        Left = left;
        Right = right;
        Original = original;
        Replacement = replacement;
    }

    private bool IsMatch => Left is not null;

    private int PrefixLength { get; }

    private int SuffixLength { get; }

    private EditTree? Left { get; }

    private EditTree? Right { get; }

    private string Original { get; }

    private string Replacement { get; }

    /// <summary>The tree that turns <paramref name="form"/> into <paramref name="lemma"/>.</summary>
    public static EditTree Build(ReadOnlySpan<char> form, ReadOnlySpan<char> lemma)
    {
        if (form.Length == 0 || lemma.Length == 0 || form.Length > MaxAnalysedLength || lemma.Length > MaxAnalysedLength)
        {
            return Substitution(form, lemma);
        }

        // Longest common substring by dynamic programming; the earliest one in the form wins a tie, so the tree is a function of the pair.
        int bestLength = 0, bestForm = 0, bestLemma = 0;
        // Both rows are at most MaxAnalysedLength + 1 ints: longer pairs returned above.
        Span<int> previous = stackalloc int[lemma.Length + 1];
        Span<int> current = stackalloc int[lemma.Length + 1];
        previous.Clear();
        for (int i = 1; i <= form.Length; i++)
        {
            current[0] = 0;
            for (int j = 1; j <= lemma.Length; j++)
            {
                if (form[i - 1] == lemma[j - 1])
                {
                    int run = previous[j - 1] + 1;
                    current[j] = run;
                    if (run > bestLength)
                    {
                        bestLength = run;
                        bestForm = i - run;
                        bestLemma = j - run;
                    }
                }
                else
                {
                    current[j] = 0;
                }
            }

            Span<int> swap = previous;
            previous = current;
            current = swap;
        }

        if (bestLength == 0)
        {
            return Substitution(form, lemma);
        }

        int suffixForm = form.Length - bestForm - bestLength;
        EditTree left = Build(form[..bestForm], lemma[..bestLemma]);
        EditTree right = Build(form[(bestForm + bestLength)..], lemma[(bestLemma + bestLength)..]);
        return new EditTree(bestForm, suffixForm, left, right, string.Empty, string.Empty);
    }

    /// <summary>Applies the tree to a form; <see langword="null"/> when the tree does not fit it.</summary>
    public string? Apply(string form) => Apply(form.AsSpan());

    /// <summary>The tree written as text: <c>M&lt;prefix&gt;,&lt;suffix&gt;(&lt;left&gt;)(&lt;right&gt;)</c> or <c>S&lt;n&gt;:&lt;original&gt;&lt;m&gt;:&lt;replacement&gt;</c>.</summary>
    public string Serialize()
    {
        var sb = new StringBuilder();
        Write(sb);
        return sb.ToString();
    }

    /// <summary>Reads a serialized tree.</summary>
    /// <exception cref="InvalidDataException">The text is not a tree, is too long or too deep.</exception>
    public static EditTree Parse(string text)
    {
        if (text.Length > MaxSerializedLength)
        {
            throw new InvalidDataException("An edit tree is too long.");
        }

        int at = 0;
        EditTree tree = Read(text, ref at, 0);
        if (at != text.Length)
        {
            throw new InvalidDataException("An edit tree has trailing text.");
        }

        return tree;
    }

    private static EditTree Substitution(ReadOnlySpan<char> original, ReadOnlySpan<char> replacement) =>
        new(0, 0, null, null, original.ToString(), replacement.ToString());

    private string? Apply(ReadOnlySpan<char> form)
    {
        if (!IsMatch)
        {
            return form.SequenceEqual(Original) ? Replacement : null;
        }

        if (form.Length < PrefixLength + SuffixLength)
        {
            return null;
        }

        string? head = Left!.Apply(form[..PrefixLength]);
        string? tail = Right!.Apply(form[^SuffixLength..]);
        if (head is null || tail is null)
        {
            return null;
        }

        return string.Concat(head, form.Slice(PrefixLength, form.Length - PrefixLength - SuffixLength), tail);
    }

    private void Write(StringBuilder sb)
    {
        if (IsMatch)
        {
            sb.Append('M').Append(PrefixLength).Append(',').Append(SuffixLength).Append('(');
            Left!.Write(sb);
            sb.Append(")(");
            Right!.Write(sb);
            sb.Append(')');
        }
        else
        {
            sb.Append('S').Append(Original.Length).Append(':').Append(Original).Append(Replacement.Length).Append(':').Append(Replacement);
        }
    }

    private static EditTree Read(string s, ref int at, int depth)
    {
        if (depth > MaxDepth || at >= s.Length)
        {
            throw new InvalidDataException("An edit tree is malformed or too deep.");
        }

        char kind = s[at++];
        if (kind == 'S')
        {
            string original = ReadCounted(s, ref at);
            string replacement = ReadCounted(s, ref at);
            return new EditTree(0, 0, null, null, original, replacement);
        }

        if (kind != 'M')
        {
            throw new InvalidDataException("An edit tree node must start with 'M' or 'S'.");
        }

        int prefix = ReadNumber(s, ref at, ',');
        int suffix = ReadNumber(s, ref at, '(');
        EditTree left = Read(s, ref at, depth + 1);
        Expect(s, ref at, ')');
        Expect(s, ref at, '(');
        EditTree right = Read(s, ref at, depth + 1);
        Expect(s, ref at, ')');
        return new EditTree(prefix, suffix, left, right, string.Empty, string.Empty);
    }

    private static void Expect(string s, ref int at, char c)
    {
        if (at >= s.Length || s[at] != c)
        {
            throw new InvalidDataException($"An edit tree is malformed: expected '{c}'.");
        }

        at++;
    }

    private static int ReadNumber(string s, ref int at, char terminator)
    {
        int value = 0;
        int digits = 0;
        while (at < s.Length && s[at] is >= '0' and <= '9')
        {
            value = (value * 10) + (s[at++] - '0');
            if (++digits > 6)
            {
                throw new InvalidDataException("An edit tree number is too long.");
            }
        }

        if (digits == 0)
        {
            throw new InvalidDataException("An edit tree is malformed: expected a number.");
        }

        Expect(s, ref at, terminator);
        return value;
    }

    private static string ReadCounted(string s, ref int at)
    {
        int length = ReadNumber(s, ref at, ':');
        if (length > s.Length - at)
        {
            throw new InvalidDataException("An edit tree string runs past the end.");
        }

        string value = s.Substring(at, length);
        at += length;
        return value;
    }
}
