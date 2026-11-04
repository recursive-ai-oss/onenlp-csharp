namespace RecursiveAI.OneNlp.Tokenizers;

/// <summary>The file formats <see cref="Tokenizer.FromStream"/> reads.</summary>
public enum TokenizerFormat
{
    /// <summary>A Hugging Face <c>tokenizer.json</c>.</summary>
    HuggingFaceJson,

    /// <summary>A SentencePiece <c>.model</c> file (the serialized model proto).</summary>
    SentencePieceModel,

    /// <summary>A WordPiece <c>vocab.txt</c>: one token per line, the line number is the ID.</summary>
    WordPieceVocabulary,

    /// <summary>A Tiktoken ranks file: one base64 token and its rank per line.</summary>
    Tiktoken,
}
