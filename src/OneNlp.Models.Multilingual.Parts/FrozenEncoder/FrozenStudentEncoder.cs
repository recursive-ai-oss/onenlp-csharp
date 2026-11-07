using RecursiveAI.OneNlp.Neural.Encoders;

namespace RecursiveAI.OneNlp.Models.Multilingual.FrozenEncoder;

/// <summary>
/// The frozen distilled multilingual encoder (a six-layer, width-384 student of mmBERT-base) with its tokenizer, as an <see cref="IEncoder"/>.
/// The weights are embedded in this package; nothing is downloaded.
/// </summary>
public static class FrozenStudentEncoder
{
    /// <summary>The number of values per token that the encoder returns.</summary>
    public const int Width = 384;

    /// <summary>Loads the encoder. Each call reads the embedded weights again and returns an independent encoder; keep one and share it between threads.</summary>
    /// <param name="pooling">How subword pieces become tokens. Default <see cref="SubwordPooling.Mean"/>.</param>
    /// <param name="maxLength">The longest sequence given to the model, special tokens included (2 to 65,536). Default 512; longer documents are read in windows by the encoder.</param>
    /// <param name="options">OneModel session options; the default runs in fast mode on every core.</param>
    /// <returns>An encoder to dispose when done. Its output rows follow the tokens of the <see cref="Doc"/> you give it.</returns>
    /// <exception cref="InvalidOperationException">The package was built without its weights.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLength"/> is out of range.</exception>
    public static TransformerEncoder Create(SubwordPooling pooling = SubwordPooling.Mean, int maxLength = 512, RecursiveAI.OneModel.Graph.ModelOptions? options = null)
    {
        using Stream model = Part.Open();
        using Stream tokenizer = Part.OpenTokenizer();
        return TransformerEncoder.FromStreams(model, tokenizer, pooling, maxLength, expandWordForms: false, options);
    }
}
