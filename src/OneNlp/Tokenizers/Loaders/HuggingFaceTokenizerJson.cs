using System.Text.Json;
using RecursiveAI.OneNlp.Tokenizers.Hf;

namespace RecursiveAI.OneNlp.Tokenizers.Loaders;

/// <summary>
/// Reads a Hugging Face <c>tokenizer.json</c> into a composed tokenizer. The file is read with a size limit and parsed with a depth
/// limit; each stage is built by the factory registered for its <c>type</c> in <see cref="StageRegistry"/>.
/// </summary>
internal static class HuggingFaceTokenizerJson
{
    /// <summary>The largest file read, in bytes. Real files, with vocabularies of a few hundred thousand entries, are tens of megabytes.</summary>
    internal const long MaxFileBytes = 512L * 1024 * 1024;

    /// <summary>The deepest nesting accepted. Real files nest a few levels, plus one per <c>Sequence</c>.</summary>
    internal const int MaxDepth = 32;

    /// <summary>Loads a tokenizer from the stream with the default registry.</summary>
    /// <param name="stream">A readable stream holding UTF-8 JSON.</param>
    /// <exception cref="InvalidDataException">The file is too large, is not valid JSON, or is missing or has malformed parts.</exception>
    /// <exception cref="NotSupportedException">The file uses a stage type that is not implemented.</exception>
    internal static Tokenizer Load(Stream stream) => Load(stream, StageRegistry.Default);

    /// <summary>Loads a tokenizer from the stream with a given registry.</summary>
    /// <param name="stream">A readable stream holding UTF-8 JSON.</param>
    /// <param name="registry">The stage types to use.</param>
    internal static Tokenizer Load(Stream stream, StageRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(registry);
        byte[] bytes = ReadBounded(stream);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = MaxDepth });
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The tokenizer file is not valid JSON: " + ex.Message, ex);
        }

        using (document)
        {
            var root = new StageConfig(document.RootElement, registry);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("A tokenizer.json holds one JSON object.");
            }

            ITokenizerModel model = registry.CreateModel(root.Object("model") ?? throw root.Missing("model"));
            var tokenizer = new HuggingFaceTokenizer(
                model,
                root.Object("normalizer") is { } n ? registry.CreateNormalizer(n) : null,
                root.Object("pre_tokenizer") is { } p ? registry.CreatePreTokenizer(p) : null,
                root.Object("post_processor") is { } pp ? registry.CreatePostProcessor(pp) : null,
                root.Object("decoder") is { } d ? registry.CreateDecoder(d) : null,
                ReadAddedTokens(root));
            tokenizer.Truncation = ReadTruncation(root);
            tokenizer.Padding = ReadPadding(root);
            return tokenizer;
        }
    }

    private static byte[] ReadBounded(Stream stream)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (buffer.Length + read > MaxFileBytes)
            {
                throw new InvalidDataException($"The tokenizer file is larger than the {MaxFileBytes} byte limit.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static AddedVocabulary ReadAddedTokens(StageConfig root)
    {
        var tokens = new List<AddedToken>();
        foreach (StageConfig t in root.Objects("added_tokens"))
        {
            // normalized defaults to true for ordinary added tokens and false for special ones, as the published format describes.
            bool special = t.Bool("special", false);
            tokens.Add(new AddedToken(
                t.OptionalInt("id") ?? throw t.Missing("id"),
                t.String("content"),
                special,
                t.Bool("single_word", false),
                t.Bool("lstrip", false),
                t.Bool("rstrip", false),
                t.Bool("normalized", !special)));
        }

        return new AddedVocabulary(tokens);
    }

    private static TruncationOptions? ReadTruncation(StageConfig root)
    {
        if (root.Object("truncation") is not { } t)
        {
            return null;
        }

        int max = t.OptionalInt("max_length") ?? throw t.Missing("max_length");
        if (max < 1)
        {
            throw t.Bad("max_length", "at least 1");
        }

        TruncationStrategy strategy = t.OptionalString("strategy") switch
        {
            null or "LongestFirst" => TruncationStrategy.LongestFirst,
            "OnlyFirst" => TruncationStrategy.OnlyFirst,
            "OnlySecond" => TruncationStrategy.OnlySecond,
            _ => throw t.Bad("strategy", "LongestFirst, OnlyFirst or OnlySecond"),
        };
        return new TruncationOptions(max, ReadSide(t, "direction", TokenizerSide.Right), strategy, t.Int("stride", 0));
    }

    private static PaddingOptions? ReadPadding(StageConfig root)
    {
        if (root.Object("padding") is not { } p)
        {
            return null;
        }

        // "strategy" is the string "BatchLongest" or the object {"Fixed": n}.
        int? fixedLength = null;
        if (p.Has("strategy") && p.Element.GetProperty("strategy").ValueKind == JsonValueKind.Object)
        {
            fixedLength = p.Object("strategy")!.Value.OptionalInt("Fixed");
        }

        return new PaddingOptions(
            p.Int("pad_id", 0),
            p.OptionalString("pad_token") ?? "[PAD]",
            fixedLength,
            p.OptionalInt("pad_to_multiple_of"),
            ReadSide(p, "direction", TokenizerSide.Right),
            p.Int("pad_type_id", 0));
    }

    private static TokenizerSide ReadSide(StageConfig config, string name, TokenizerSide defaultSide) =>
        config.OptionalString(name) switch
        {
            null => defaultSide,
            "Left" or "left" => TokenizerSide.Left,
            "Right" or "right" => TokenizerSide.Right,
            _ => throw config.Bad(name, "\"Left\" or \"Right\""),
        };
}
