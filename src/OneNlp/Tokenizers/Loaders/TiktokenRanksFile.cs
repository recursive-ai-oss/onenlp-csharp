namespace RecursiveAI.OneNlp.Tokenizers.Loaders;

/// <summary>Reads a Tiktoken ranks file. Not implemented yet; the file that implements it replaces this one.</summary>
internal static class TiktokenRanksFile
{
    /// <summary>Loads a tokenizer from the stream.</summary>
    internal static Tokenizer Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        throw new NotSupportedException("Reading a Tiktoken ranks file is not implemented yet.");
    }
}
