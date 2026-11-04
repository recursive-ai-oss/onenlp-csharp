namespace RecursiveAI.OneNlp.Tokenizers.Loaders;

/// <summary>Reads a SentencePiece .model file. Not implemented yet; the file that implements it replaces this one.</summary>
internal static class SentencePieceModelFile
{
    /// <summary>Loads a tokenizer from the stream.</summary>
    internal static Tokenizer Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        throw new NotSupportedException("Reading a SentencePiece .model file is not implemented yet.");
    }
}
