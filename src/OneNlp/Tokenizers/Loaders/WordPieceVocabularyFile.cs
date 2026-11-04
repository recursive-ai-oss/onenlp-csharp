using System.Text;
using System.Text.Json;
using RecursiveAI.OneNlp.Tokenizers.Hf;
using RecursiveAI.OneNlp.Tokenizers.WordPiece;

namespace RecursiveAI.OneNlp.Tokenizers.Loaders;

/// <summary>
/// Reads a BERT-style <c>vocab.txt</c> (one token per line, the ID is the line number) into a WordPiece tokenizer composed the way the
/// BERT model cards describe it: <c>BertNormalizer</c>, <c>BertPreTokenizer</c>, the WordPiece model, the <c>[CLS] A [SEP]</c> template
/// and the WordPiece decoder.
/// </summary>
/// <remarks>
/// A <c>vocab.txt</c> does not say whether the model lowercases. The loader decides from the vocabulary: a vocabulary with any token that
/// has an uppercase letter (outside the bracketed special tokens) is cased, otherwise it lowercases, which is also the default of the
/// published BERT tokenizer configuration. Files are read as the reference reads them: lines end at <c>\n</c>, trailing white space of
/// a line is dropped, blank lines are empty tokens, and a repeated token keeps its last ID.
/// </remarks>
internal static class WordPieceVocabularyFile
{
    /// <summary>The largest file read, in bytes. The multilingual BERT vocabulary is about 1 MB.</summary>
    internal const long MaxFileBytes = 64L * 1024 * 1024;

    private static readonly string[] SpecialTokens = ["[PAD]", "[UNK]", "[CLS]", "[SEP]", "[MASK]"];

    /// <summary>Loads a tokenizer from the stream, deciding case handling from the vocabulary.</summary>
    /// <param name="stream">A readable stream holding UTF-8 text.</param>
    /// <exception cref="InvalidDataException">The file is too large, is not valid UTF-8, or has no <c>[UNK]</c> token.</exception>
    internal static Tokenizer Load(Stream stream) => Load(stream, lowercase: null, StageRegistry.Default);

    /// <summary>Loads a tokenizer from the stream.</summary>
    /// <param name="stream">A readable stream holding UTF-8 text.</param>
    /// <param name="lowercase">Whether the normalizer lowercases and strips accents; <see langword="null"/> decides from the vocabulary.</param>
    /// <param name="registry">The stage types to build the pipeline from.</param>
    internal static Tokenizer Load(Stream stream, bool? lowercase, StageRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(registry);
        List<string> lines = ReadLines(stream);
        var entries = new List<KeyValuePair<string, int>>(lines.Count);
        for (int i = 0; i < lines.Count; i++)
        {
            entries.Add(new KeyValuePair<string, int>(lines[i], i));
        }

        var vocab = new TokenIdTable(entries);
        if (vocab.GetId("[UNK]") is null)
        {
            throw new InvalidDataException("The vocabulary has no [UNK] token, which a WordPiece tokenizer needs.");
        }

        bool lower = lowercase ?? !lines.Any(HasCasedLetter);
        var added = new List<AddedToken>();
        foreach (string special in SpecialTokens)
        {
            if (vocab.GetId(special) is { } id)
            {
                added.Add(new AddedToken(id, special, Special: true, SingleWord: false, LeftStrip: false, RightStrip: false, Normalized: false));
            }
        }

        IPostProcessor? post = null;
        if (vocab.GetId("[CLS]") is { } cls && vocab.GetId("[SEP]") is { } sep)
        {
            post = FromJson(TemplateJson(cls, sep), registry, registry.CreatePostProcessor);
        }

        return new HuggingFaceTokenizer(
            new WordPieceModel(vocab, "[UNK]", "##", 100),
            FromJson($$"""{"type":"BertNormalizer","lowercase":{{(lower ? "true" : "false")}},"clean_text":true,"handle_chinese_chars":true,"strip_accents":null}""", registry, registry.CreateNormalizer),
            FromJson("""{"type":"BertPreTokenizer"}""", registry, registry.CreatePreTokenizer),
            post,
            FromJson("""{"type":"WordPiece","prefix":"##","cleanup":true}""", registry, registry.CreateDecoder),
            new AddedVocabulary(added));
    }

    /// <summary>Builds a stage from a JSON object in memory.</summary>
    internal static T FromJson<T>(string json, StageRegistry registry, Func<StageConfig, T> create)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        return create(new StageConfig(doc.RootElement, registry));
    }

    // The template the BERT tokenizer files carry: [CLS] A [SEP] for one sequence and [CLS] A [SEP] B:1 [SEP]:1 for two.
    private static string TemplateJson(int cls, int sep) =>
        """
        {"type":"TemplateProcessing",
         "single":[{"SpecialToken":{"id":"[CLS]","type_id":0}},{"Sequence":{"id":"A","type_id":0}},{"SpecialToken":{"id":"[SEP]","type_id":0}}],
         "pair":[{"SpecialToken":{"id":"[CLS]","type_id":0}},{"Sequence":{"id":"A","type_id":0}},{"SpecialToken":{"id":"[SEP]","type_id":0}},{"Sequence":{"id":"B","type_id":1}},{"SpecialToken":{"id":"[SEP]","type_id":1}}],
         "special_tokens":{"[CLS]":{"id":"[CLS]","ids":[__CLS__],"tokens":["[CLS]"]},"[SEP]":{"id":"[SEP]","ids":[__SEP__],"tokens":["[SEP]"]}}}
        """
            .Replace("__CLS__", cls.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("__SEP__", sep.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static bool HasCasedLetter(string token)
    {
        if (token.Length > 2 && token[0] == '[' && token[^1] == ']')
        {
            return false; // [CLS], [SEP], [unused1] and the like are written in capitals in every BERT vocabulary
        }

        foreach (char c in token)
        {
            if (char.IsUpper(c))
            {
                return true;
            }
        }

        return false;
    }

    private static List<string> ReadLines(Stream stream)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (buffer.Length + read > MaxFileBytes)
            {
                throw new InvalidDataException($"The vocabulary file is larger than the {MaxFileBytes} byte limit.");
            }

            buffer.Write(chunk, 0, read);
        }

        string text;
        try
        {
            text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
        }
        catch (DecoderFallbackException ex)
        {
            throw new InvalidDataException("The vocabulary file is not valid UTF-8.", ex);
        }

        var lines = new List<string>();
        int start = 0;
        while (start < text.Length)
        {
            int nl = text.IndexOf('\n', start);
            int end = nl < 0 ? text.Length : nl;
            lines.Add(TrimEnd(text.AsSpan(start, end - start)));
            if (lines.Count > TokenIdTable.MaxId)
            {
                throw new InvalidDataException("The vocabulary has too many lines.");
            }

            start = end + 1;
        }

        return lines;
    }

    // Unicode White_Space, which is what the reference trims. char.IsWhiteSpace also accepts U+001C to U+001F, which are not.
    private static string TrimEnd(ReadOnlySpan<char> line)
    {
        int end = line.Length;
        while (end > 0 && IsWhiteSpace(line[end - 1]))
        {
            end--;
        }

        return line[..end].ToString();
    }

    private static bool IsWhiteSpace(char c) =>
        c is (>= '\t' and <= '\r') or ' ' or '\u0085' or '\u00A0' or '\u1680' or (>= '\u2000' and <= '\u200A')
            or '\u2028' or '\u2029' or '\u202F' or '\u205F' or '\u3000';
}
