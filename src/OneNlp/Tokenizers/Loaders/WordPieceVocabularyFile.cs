namespace RecursiveAI.OneNlp.Tokenizers.Loaders;

/// <summary>Reads a WordPiece vocab.txt. Not implemented yet; the file that implements it replaces this one.</summary>
internal static class WordPieceVocabularyFile
{
    /// <summary>Loads a tokenizer from the stream.</summary>
    internal static Tokenizer Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        throw new NotSupportedException("Reading a WordPiece vocab.txt is not implemented yet.");
    }
}
