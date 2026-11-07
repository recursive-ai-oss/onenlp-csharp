namespace RecursiveAI.OneNlp;

/// <summary>Which kind of trained model <see cref="Pipeline.Create"/> loads.</summary>
public enum Tier
{
    /// <summary>The per-language model of hash embeddings and a small convolutional or recurrent encoder: the fastest, a language package such as <c>RecursiveAI.OneNlp.Models.German</c>. When no such package is referenced for the language, the accurate model is used instead.</summary>
    Fast = 0,

    /// <summary>The multilingual transformer model (<c>RecursiveAI.OneNlp.Models.Multilingual.Large</c> when referenced, otherwise <c>RecursiveAI.OneNlp.Models.Multilingual</c>). Never replaced by a fast model.</summary>
    Accurate = 1,
}
