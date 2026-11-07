namespace RecursiveAI.OneNlp.Tokenizers;

/// <summary>
/// What a BERT-style <c>vocab.txt</c> leaves unsaid: how the model normalized text before it was split into word pieces. Pass it to
/// <see cref="Tokenizer.FromWordPieceVocabulary(string, WordPieceVocabularyOptions)"/>.
/// </summary>
/// <remarks>
/// The loader behind <see cref="Tokenizer.FromFile(string)"/> guesses instead: a vocabulary with a letter that has a lower-case form
/// in an upper-case token (outside <c>[bracketed]</c> special tokens) is cased; letters that have no case pair, such as <c>ℝ</c>, do not count.
/// The guess is right for the usual BERT vocabularies, but only the model's own configuration knows, so state it when it matters.
/// </remarks>
public sealed class WordPieceVocabularyOptions
{
    /// <summary>
    /// Whether text is lower-cased before it is split (<c>do_lower_case</c> of the BERT configuration). <see langword="true"/> for
    /// <c>bert-base-uncased</c>, <see langword="false"/> for <c>bert-base-cased</c> and <c>bert-base-multilingual-cased</c>.
    /// </summary>
    public bool LowerCase { get; init; }

    /// <summary>
    /// Whether accents are stripped (<c>strip_accents</c>). <see langword="null"/>, the default, follows <see cref="LowerCase"/>, as the
    /// BERT tokenizer does when the setting is absent.
    /// </summary>
    public bool? StripAccents { get; init; }
}
