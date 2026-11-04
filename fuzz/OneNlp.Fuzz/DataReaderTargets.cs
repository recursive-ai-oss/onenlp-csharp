using System.Text;
using RecursiveAI.OneNlp.Data;

namespace RecursiveAI.OneNlp.Fuzz;

/// <summary>
/// Feeds arbitrary bytes to the CoNLL-U reader as a UTF-8 stream. Only <see cref="DataFormatException"/> may escape. Every sentence
/// that does come out must be a consistent document, and writing it back must not fail.
/// </summary>
internal static class ConllUTarget
{
    private static readonly ConllUOptions Limits = new() { MaxLineLength = 1 << 16, MaxSentenceLines = 2000, MaxCommentLines = 100 };

    internal static void Run(ReadOnlySpan<byte> data)
    {
        using var stream = new MemoryStream(data.ToArray());
        try
        {
            foreach (ConllUSentence s in ConllUReader.Read(stream, null, Limits))
            {
                Doc doc = s.Doc;
                Require(doc.Length > 0 && s.Forms.Count == doc.Length && s.Deps.Count == doc.Length && s.Misc.Count == doc.Length, "column counts");
                int previousEnd = 0;
                for (int i = 0; i < doc.Length; i++)
                {
                    Token t = doc[i];
                    Require(t.Length > 0 && t.Idx >= previousEnd && t.EndIdx <= doc.Text.Length, "token range");
                    Require(t.HeadIndex >= 0 && t.HeadIndex < doc.Length, "head range");
                    previousEnd = t.EndIdx;
                }

                foreach (MultiwordToken m in s.MultiwordTokens)
                {
                    Require(m.FirstWord >= 0 && m.LastWord > m.FirstWord && m.LastWord < doc.Length, "multiword range");
                }

                using var writer = new StringWriter();
                ConllUWriter.Write(writer, s);
                ConllUWriter.Write(writer, doc);
            }
        }
        catch (DataFormatException)
        {
            // The documented way to reject input.
        }
    }

    private static void Require(bool condition, string what)
    {
        if (!condition)
        {
            throw new InvalidOperationException("Invariant failed: " + what);
        }
    }
}

/// <summary>
/// Feeds arbitrary bytes to the JSON Lines reader. Only <see cref="DataFormatException"/> may escape; every example that comes out
/// must align with its own predicted copy and hold entities inside the document.
/// </summary>
internal static class JsonlTarget
{
    private static readonly JsonlOptions Limits = new() { MaxLineLength = 1 << 16 };

    internal static void Run(ReadOnlySpan<byte> data)
    {
        using var stream = new MemoryStream(data.ToArray());
        try
        {
            foreach (Example e in JsonlReader.Read(stream, null, Limits))
            {
                if (e.Reference.Length != e.Predicted.Length || e.Alignment.XLength != e.Predicted.Length)
                {
                    throw new InvalidOperationException("Invariant failed: alignment");
                }

                foreach (Span ent in e.Reference.Ents)
                {
                    if (ent.Start < 0 || ent.End > e.Reference.Length || ent.End <= ent.Start)
                    {
                        throw new InvalidOperationException("Invariant failed: entity range");
                    }
                }

                if (e.Spans is { } groups)
                {
                    foreach (IReadOnlyList<SpanSpec> group in groups.Values)
                    {
                        foreach (SpanSpec s in group)
                        {
                            if (s.Start < 0 || s.End > e.Reference.Length || s.End <= s.Start)
                            {
                                throw new InvalidOperationException("Invariant failed: span range");
                            }
                        }
                    }
                }
            }
        }
        catch (DataFormatException)
        {
            // The documented way to reject input.
        }
    }
}
