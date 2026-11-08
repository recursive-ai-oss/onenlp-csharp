using RecursiveAI.OneNlp;

namespace RecursiveAI.OneNlp.Models.Hindi;

/// <summary>
/// Writes each token's own text as its lemma. There is no Hindi lemmatizer: no Hindi treebank may be used for training, and an edit-tree
/// lemmatizer trained on Marathi, the closest language with allowed data, was not clearly better than copying the form on our own
/// annotated sentences (62.5% against 61.0% of nouns, verbs and adjectives), so the form is the lemma. The lemma of an inflected word is
/// therefore wrong (the form of लड़कों is लड़कों, not लड़का); the column is filled so that code which reads <c>Token.Lemma</c> gets a usable key for uninflected words and function words.
/// </summary>
internal sealed class FormLemmas : IPipelineComponent
{
    /// <inheritdoc/>
    public string Name => "form_lemmas";

    /// <inheritdoc/>
    public void Process(Doc doc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        for (int t = 0; t < doc.Length; t++)
        {
            doc.SetAnnotation(TokenColumn.Lemma, t, doc[t].Text);
        }
    }
}
