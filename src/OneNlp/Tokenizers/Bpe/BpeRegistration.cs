namespace RecursiveAI.OneNlp.Tokenizers.Hf;

internal sealed partial class StageRegistry
{
    // The BPE model covers byte-level BPE too: that is this model over text the ByteLevel pre-tokenizer has mapped to printable characters.
    static partial void RegisterBpe(StageRegistry registry) => registry.AddModel("BPE", Bpe.BpeModel.FromConfig);
}
