namespace RecursiveAI.OneNlp.Tokenizers.Loaders;

/// <summary>Reads a Hugging Face tokenizer.json. Not implemented yet; the file that implements it replaces this one.</summary>
internal static class HuggingFaceTokenizerJson
{
    /// <summary>Loads a tokenizer from the stream.</summary>
    internal static Tokenizer Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        throw new NotSupportedException("Reading a Hugging Face tokenizer.json is not implemented yet.");
    }
}
