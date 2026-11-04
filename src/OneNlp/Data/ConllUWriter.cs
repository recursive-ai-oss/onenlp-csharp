using System.Globalization;

namespace RecursiveAI.OneNlp.Data;

/// <summary>Writes sentences as CoNLL-U (universaldependencies.org/format.html), the inverse of <see cref="ConllUReader"/>.</summary>
public static class ConllUWriter
{
    /// <summary>
    /// Writes a sentence read by <see cref="ConllUReader"/> with the columns of its document, so that reading and writing an
    /// unchanged file reproduces it exactly (when the file uses LF line breaks and a blank line after every sentence).
    /// </summary>
    /// <param name="writer">The destination. Lines end with LF.</param>
    /// <param name="sentence">The sentence.</param>
    public static void Write(TextWriter writer, ConllUSentence sentence)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(sentence);
        Doc doc = sentence.Doc;
        foreach (string c in sentence.Comments)
        {
            writer.Write(c);
            writer.Write('\n');
        }

        int mwt = 0;
        int empty = 0;
        for (int i = 0; i < doc.Length; i++)
        {
            for (; empty < sentence.EmptyNodes.Count && sentence.EmptyNodes[empty].WordsBefore <= i; empty++)
            {
                writer.Write(sentence.EmptyNodes[empty].Line);
                writer.Write('\n');
            }

            if (mwt < sentence.MultiwordTokens.Count && sentence.MultiwordTokens[mwt].FirstWord == i)
            {
                MultiwordToken m = sentence.MultiwordTokens[mwt++];
                writer.Write(string.Create(CultureInfo.InvariantCulture, $"{m.FirstWord + 1}-{m.LastWord + 1}\t{m.Form}\t_\t_\t_\t_\t_\t_\t_\t{Field(m.Misc)}\n"));
            }

            WriteWord(writer, doc, i, sentence.Forms[i], sentence.Deps[i], sentence.Misc[i]);
        }

        for (; empty < sentence.EmptyNodes.Count; empty++)
        {
            writer.Write(sentence.EmptyNodes[empty].Line);
            writer.Write('\n');
        }

        writer.Write('\n');
    }

    /// <summary>
    /// Writes a document that did not come from a CoNLL-U file, for example a prediction: the token text as FORM, the annotated
    /// columns, and <c>SpaceAfter=No</c> in MISC where a token is directly followed by the next. DEPS is <c>_</c>.
    /// </summary>
    /// <param name="writer">The destination. Lines end with LF.</param>
    /// <param name="doc">The document; written as one sentence.</param>
    public static void Write(TextWriter writer, Doc doc)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(doc);
        for (int i = 0; i < doc.Length; i++)
        {
            bool joined = i + 1 < doc.Length && !doc[i].HasSpaceAfter;
            WriteWord(writer, doc, i, doc[i].ToString(), string.Empty, joined ? "SpaceAfter=No" : string.Empty);
        }

        writer.Write('\n');
    }

    private static void WriteWord(TextWriter writer, Doc doc, int i, string form, string deps, string misc)
    {
        Token t = doc[i];
        string deprel = t.Dep;
        string head = t.HeadIndex != i ? (t.HeadIndex + 1).ToString(CultureInfo.InvariantCulture) : (deprel.Length > 0 ? "0" : "_");
        writer.Write((i + 1).ToString(CultureInfo.InvariantCulture));
        foreach (string column in new[] { form, Field(t.Lemma), Field(t.Pos), Field(t.Tag), Field(t.Morph), head, Field(deprel), Field(deps), Field(misc) })
        {
            writer.Write('\t');
            writer.Write(column);
        }

        writer.Write('\n');
    }

    private static string Field(string value) => value.Length == 0 ? "_" : value;
}
