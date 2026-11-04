using System.Text.Json;
using RecursiveAI.OneNlp.Tokenizers.Bpe;
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
                return LoadBpe(data, registry);
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

    // A BPE-type model (Llama-style): the whole text is one piece, with spaces written as the escape character and the dummy prefix
    // put in front, and merges are ranked by the scores of the pieces they make (Sennrich et al. 2016; the SentencePiece model format).
    private static HuggingFaceTokenizer LoadBpe(SentencePieceData data, StageRegistry registry)
    {
        if (!data.EscapeWhitespaces || !data.RemoveExtraWhitespaces)
        {
            throw new NotSupportedException("SentencePiece models that keep extra spaces or do not escape spaces are not supported.");
        }

        if (data.Charsmap.Length == 0 && data.NormalizerName is not ("" or "identity"))
        {
            throw new NotSupportedException($"The model names the normalizer '{data.NormalizerName}' but carries no compiled table for it.");
        }

        (BpeModel model, List<AddedToken> added) = BuildBpe(data);

        string steps = data.AddDummyPrefix
            ? """{"type":"Prepend","prepend":"\u2581"},{"type":"Replace","pattern":{"String":" "},"content":"\u2581"}"""
            : """{"type":"Replace","pattern":{"String":" "},"content":"\u2581"}""";
        if (data.Charsmap.Length != 0)
        {
            steps = $$"""{"type":"Precompiled","precompiled_charsmap":"{{Convert.ToBase64String(data.Charsmap)}}"},""" + steps;
        }

        INormalizer normalizer = WordPieceVocabularyFile.FromJson($$"""{"type":"Sequence","normalizers":[{{steps}}]}""", registry, registry.CreateNormalizer);
        string stripPrefix = data.AddDummyPrefix ? """,{"type":"Strip","content":" ","start":1,"stop":0}""" : string.Empty;
        string decoder = data.ByteFallback ? ByteFallbackDecoderHead + stripPrefix + "]}" : Metaspace.Replace("__PREFIX__", data.AddDummyPrefix ? "true" : "false", StringComparison.Ordinal);
        return new HuggingFaceTokenizer(
            model,
            normalizer,
            preTokenizer: null,
            postProcessor: null,
            WordPieceVocabularyFile.FromJson(decoder, registry, registry.CreateDecoder),
            new AddedVocabulary(added));
    }

    /// <summary>Reads only the BPE model of a BPE-type file, without the stages (for comparing it with a converted <c>tokenizer.json</c>).</summary>
    /// <param name="stream">The model file.</param>
    internal static BpeModel ReadBpeModel(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        SentencePieceData data = SentencePieceProto.Read(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
        return data.ModelType == 2 ? BuildBpe(data).Model : throw new InvalidDataException("The file is not a BPE-type SentencePiece model.");
    }

    private static (BpeModel Model, List<AddedToken> Added) BuildBpe(SentencePieceData data)
    {
        var scores = new float[data.Scores.Length];
        var mergeable = new bool[data.Pieces.Length];
        var added = new List<AddedToken>();
        for (int i = 0; i < scores.Length; i++)
        {
            if (!float.IsFinite(data.Scores[i]))
            {
                throw new InvalidDataException("A piece has a score that is not a finite number.");
            }

            scores[i] = data.Scores[i];
            mergeable[i] = data.Types[i] == SentencePieceData.Normal;
            if (data.Types[i] == SentencePieceData.UserDefined)
            {
                added.Add(new AddedToken(i, data.Pieces[i], Special: false, SingleWord: false, LeftStrip: false, RightStrip: false, Normalized: false));
            }
        }

        string? unk = data.UnkId >= 0 && data.UnkId < data.Pieces.Length ? data.Pieces[data.UnkId] : null;
        var model = BpeModel.FromSentencePieces(data.Pieces, scores, mergeable, new BpeOptions(UnknownToken: unk, FuseUnknown: true, ByteFallback: data.ByteFallback));

        return (model, added);
    }
}
