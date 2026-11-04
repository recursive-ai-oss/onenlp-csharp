namespace RecursiveAI.OneNlp;

/// <summary>Where one token sits in the text: a half-open range of UTF-16 code unit offsets into the original string.</summary>
/// <param name="Start">The offset of the first character of the token.</param>
/// <param name="Length">The number of UTF-16 code units; at least 1.</param>
public readonly record struct TokenBoundary(int Start, int Length)
{
    /// <summary>The offset just past the last character of the token.</summary>
    public int End => Start + Length;
}

/// <summary>A span to attach to a <see cref="Doc"/>: a token range with a label and a confidence.</summary>
/// <param name="Start">The index of the first token.</param>
/// <param name="End">The index just past the last token; greater than <paramref name="Start"/>.</param>
/// <param name="Label">The label, for example <c>PER</c>.</param>
/// <param name="Confidence">The model's confidence from 0 to 1; 1 for rule-based results.</param>
public readonly record struct SpanSpec(int Start, int End, string Label, float Confidence = 1f);

/// <summary>The string-valued token columns a <see cref="Doc"/> stores as interned IDs, one column each.</summary>
public enum TokenColumn
{
    /// <summary>The normalized form of the token.</summary>
    Norm = 0,

    /// <summary>The lemma (base form).</summary>
    Lemma = 1,

    /// <summary>The coarse part of speech, a Universal Dependencies UPOS tag.</summary>
    Pos = 2,

    /// <summary>The fine-grained, language-specific tag (XPOS).</summary>
    Tag = 3,

    /// <summary>The dependency relation of the token to its head.</summary>
    Dep = 4,

    /// <summary>The morphological features as a Universal Dependencies feature string, <c>Case=Nom|Number=Sing</c>.</summary>
    Morph = 5,
}

/// <summary>Whether a token starts a sentence, as written in a document's sentence column.</summary>
public enum SentenceBoundary : byte
{
    /// <summary>Not decided yet.</summary>
    Unset = 0,

    /// <summary>The token starts a sentence.</summary>
    Start = 1,

    /// <summary>The token continues the sentence of the token before it.</summary>
    Inside = 2,
}

/// <summary>The position of a token in an entity, with the numeric values spaCy documents for <c>ent_iob</c>.</summary>
public enum EntIob
{
    /// <summary>No entity annotation has been written to the document.</summary>
    Unset = 0,

    /// <summary>The token continues an entity (I).</summary>
    Inside = 1,

    /// <summary>The token is outside every entity (O).</summary>
    Outside = 2,

    /// <summary>The token begins an entity (B).</summary>
    Begin = 3,
}
