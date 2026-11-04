using System.Text.Json;
using RecursiveAI.OneNlp.Tokenizers.Hf;
using RecursiveAI.OneNlp.Tokenizers.Unigram;

namespace RecursiveAI.OneNlp.Tokenizers.Loaders;

/// <summary>
/// Reads a SentencePiece <c>.model</c> file (Kudo and Richardson 2018) into a tokenizer. A unigram model becomes the Unigram model
/// behind the stages the converted <c>tokenizer.json</c> files of such models use: the model's own compiled normalization table
/// (<c>Precompiled</c>), whitespace splitting, <c>Metaspace</c> and the matching decoder. Pieces the model marks as user-defined
/// symbols are added tokens, so they are never split.
/// </summary>
/// <remarks>
/// Differences from a converted <c>tokenizer.json</c>, which adds the model's control symbols as added tokens and a template that
/// appends the end-of-sequence token: neither is done here, as the SentencePiece library does neither, so control symbols written in
/// the text are ordinary text and no special token is added. Models that keep runs of spaces (<c>remove_extra_whitespaces</c> off) or
/// write spaces as they are (<c>escape_whitespaces</c> off) are refused.
/// </remarks>
internal static class SentencePieceModelFile
{
    private const string ByteFallbackDecoderHead = """{"type":"Sequence","decoders":[{"type":"Replace","pattern":{"String":"\u2581"},"content":" "},{"type":"ByteFallback"},{"type":"Fuse"}""";

    private const string Metaspace = """{"type":"Metaspace","replacement":"\u2581","str_rep":"\u2581","add_prefix_space":__PREFIX__}""";

    /// <summary>Loads a tokenizer from the stream.</summary>
    /// <param name="stream">A readable stream holding the serialized model.</param>
    /// <exception cref="InvalidDataException">The file is malformed, truncated or over a limit.</exception>
    /// <exception cref="NotSupportedException">The model is not a unigram model, or uses settings that are not supported.</exception>
    internal static Tokenizer Load(Stream stream) => Load(stream, StageRegistry.Default);

    /// <summary>Loads a tokenizer from the stream.</summary>
    /// <param name="stream">A readable stream holding the serialized model.</param>
    /// <param name="registry">The stage types to build the pipeline from.</param>
    internal static Tokenizer Load(Stream stream, StageRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(registry);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (buffer.Length + read > SentencePieceProto.MaxFileBytes)
            {
                throw new InvalidDataException("The model file is larger than the limit.");
            }

            buffer.Write(chunk, 0, read);
        }

        SentencePieceData data = SentencePieceProto.Read(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
        switch (data.ModelType)
        {
            case 1:
                break;
            case 2:
                throw new NotSupportedException("This is a BPE-type SentencePiece model, which is not supported yet.");
            default:
                throw new NotSupportedException($"SentencePiece model type {data.ModelType} (word or character) is not supported.");
        }

        if (!data.EscapeWhitespaces || !data.RemoveExtraWhitespaces)
        {
            throw new NotSupportedException("SentencePiece models that keep extra spaces or do not escape spaces are not supported.");
        }

        if (data.Charsmap.Length == 0 && data.NormalizerName is not ("" or "identity"))
        {
            throw new NotSupportedException($"The model names the normalizer '{data.NormalizerName}' but carries no compiled table for it.");
        }

        var scores = new double[data.Scores.Length];
        var matchable = new bool[data.Pieces.Length];
        var added = new List<AddedToken>();
        for (int i = 0; i < scores.Length; i++)
        {
            if (!float.IsFinite(data.Scores[i]))
            {
                throw new InvalidDataException("A piece has a score that is not a finite number.");
            }

            scores[i] = data.Scores[i];

            // The unigram lattice holds ordinary and user-defined pieces; control, unknown, unused and byte pieces are never matched in text.
            matchable[i] = data.Types[i] is SentencePieceData.Normal or SentencePieceData.UserDefined;
            if (data.Types[i] == SentencePieceData.UserDefined)
            {
                added.Add(new AddedToken(i, data.Pieces[i], Special: false, SingleWord: false, LeftStrip: false, RightStrip: false, Normalized: false));
            }
        }

        // Built first: the stages are only needed once the model has proved sound.
        var model = new UnigramModel([.. data.Pieces], scores, data.UnkId, data.ByteFallback, matchable);
        string prefix = data.AddDummyPrefix ? "true" : "false";
        string metaspace = Metaspace.Replace("__PREFIX__", prefix, StringComparison.Ordinal);

        INormalizer? normalizer = data.Charsmap.Length == 0
            ? null
            : WordPieceVocabularyFile.FromJson(
                $$"""{"type":"Precompiled","precompiled_charsmap":"{{Convert.ToBase64String(data.Charsmap)}}"}""", registry, registry.CreateNormalizer);

        IPreTokenizer preTokenizer = WordPieceVocabularyFile.FromJson(
            $$"""{"type":"Sequence","pretokenizers":[{"type":"WhitespaceSplit"},{{metaspace}}]}""", registry, registry.CreatePreTokenizer);

        // With byte fallback, tokens like <0x41> must turn back into bytes before the text is read; the dummy prefix is then the
        // first space of the joined text.
        string stripPrefix = data.AddDummyPrefix ? """,{"type":"Strip","content":" ","start":1,"stop":0}""" : string.Empty;
        string decoderJson = data.ByteFallback ? ByteFallbackDecoderHead + stripPrefix + "]}" : metaspace;

        return new HuggingFaceTokenizer(
            model,
            normalizer,
            preTokenizer,
            postProcessor: null,
            WordPieceVocabularyFile.FromJson(decoderJson, registry, registry.CreateDecoder),
            new AddedVocabulary(added));
    }
}
