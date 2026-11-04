namespace RecursiveAI.OneNlp.Data;

/// <summary>A multiword token line (<c>1-2</c>) of a CoNLL-U sentence: a surface form that stands for several syntactic words.</summary>
/// <param name="FirstWord">The 0-based index of the first word of the range.</param>
/// <param name="LastWord">The 0-based index of the last word of the range, inclusive.</param>
/// <param name="Form">The surface form.</param>
/// <param name="Misc">The line's MISC column as written (<c>SpaceAfter=No</c> and the like), empty for <c>_</c>.</param>
public readonly record struct MultiwordToken(int FirstWord, int LastWord, string Form, string Misc);

/// <summary>An empty node line (<c>8.1</c>) of a CoNLL-U sentence. Empty nodes belong to the enhanced graph and have no token in the document.</summary>
/// <param name="WordsBefore">How many words precede the node in the sentence.</param>
/// <param name="Line">The line as written, without its line break.</param>
public readonly record struct EmptyNode(int WordsBefore, string Line);

/// <summary>
/// One sentence of a CoNLL-U file: a <see cref="Doc"/> with the gold columns, plus what a document has no place for (comments,
/// multiword tokens, empty nodes, the DEPS and MISC columns) so that writing the sentence back gives the same lines.
/// </summary>
/// <remarks>
/// The document has one token per syntactic word. Its text is the sentence's surface text: the surface forms joined by a space,
/// except after a token marked <c>SpaceAfter=No</c>. The columns map as: LEMMA to <see cref="TokenColumn.Lemma"/>, UPOS to
/// <see cref="TokenColumn.Pos"/>, XPOS to <see cref="TokenColumn.Tag"/>, FEATS to <see cref="TokenColumn.Morph"/> (as written),
/// HEAD to the head index (a root is its own head), DEPREL to <see cref="TokenColumn.Dep"/>. An underscore is "not set". The first
/// token is a sentence start and the others are inside it.
/// </remarks>
public sealed class ConllUSentence
{
    internal ConllUSentence(
        Doc doc,
        string[] comments,
        string[] forms,
        string[] deps,
        string[] misc,
        MultiwordToken[] multiwordTokens,
        EmptyNode[] emptyNodes)
    {
        Doc = doc;
        Comments = comments;
        Forms = forms;
        Deps = deps;
        Misc = misc;
        MultiwordTokens = multiwordTokens;
        EmptyNodes = emptyNodes;
    }

    /// <summary>The sentence with its gold annotation.</summary>
    public Doc Doc { get; }

    /// <summary>The comment lines before the sentence, each as written (starting with <c>#</c>).</summary>
    public IReadOnlyList<string> Comments { get; }

    /// <summary>The FORM column of each word. For a word inside a multiword token this is the word's own form, not the surface text.</summary>
    public IReadOnlyList<string> Forms { get; }

    /// <summary>The DEPS column (enhanced dependencies) of each word as written, empty for <c>_</c>.</summary>
    public IReadOnlyList<string> Deps { get; }

    /// <summary>The MISC column of each word as written, empty for <c>_</c>.</summary>
    public IReadOnlyList<string> Misc { get; }

    /// <summary>The multiword token lines, in order.</summary>
    public IReadOnlyList<MultiwordToken> MultiwordTokens { get; }

    /// <summary>The empty node lines, in order.</summary>
    public IReadOnlyList<EmptyNode> EmptyNodes { get; }

    /// <summary>The value of a <c># key = value</c> comment such as <c>sent_id</c> or <c>text</c>.</summary>
    /// <param name="key">The key, without the hash or the equals sign.</param>
    /// <returns>The value, or <see langword="null"/> when the sentence has no such comment.</returns>
    public string? GetComment(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        foreach (string c in Comments)
        {
            ReadOnlySpan<char> s = c.AsSpan(1).TrimStart();
            if (s.StartsWith(key, StringComparison.Ordinal))
            {
                ReadOnlySpan<char> rest = s[key.Length..].TrimStart();
                if (rest.Length > 0 && rest[0] == '=')
                {
                    return rest[1..].TrimStart().ToString();
                }
            }
        }

        return null;
    }
}
