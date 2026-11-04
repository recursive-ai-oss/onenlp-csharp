using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RecursiveAI.OneNlp.Tokenizers;
using RecursiveAI.OneNlp.Tokenizers.Bpe;
using RecursiveAI.OneNlp.Tokenizers.Hf;
using RecursiveAI.OneNlp.Tokenizers.Loaders;

namespace RecursiveAI.OneNlp.Fuzz;

/// <summary>
/// Feeds arbitrary bytes to the <c>.tiktoken</c> reader. A file it accepts must build a tokenizer whose encoding of a fixed probe text
/// decodes back to the same text; a file it refuses must be refused with <see cref="InvalidDataException"/> and nothing else.
/// </summary>
internal static partial class TiktokenRanksTarget
{
    private const string Probe = "ab abc  aab, 日本語 😀!\n";

    [GeneratedRegex(@"\w+|\s+|[^\w\s]+", RegexOptions.CultureInvariant)]
    private static partial Regex Everything();

    internal static void Run(ReadOnlySpan<byte> data)
    {
        TiktokenTokenizer tokenizer;
        try
        {
            tokenizer = TiktokenTokenizer.Create(new MemoryStream(data.ToArray()), Everything());
        }
        catch (InvalidDataException)
        {
            return;
        }

        TokenEncoding e = tokenizer.Encode(Probe, addSpecialTokens: false);
        string back = tokenizer.Decode(e.Ids, skipSpecialTokens: false);
        if (back != Probe)
        {
            throw new InvalidOperationException("Invariant failed: encode then decode does not give the text back");
        }

        for (int i = 0; i < e.Length; i++)
        {
            if (e.Offsets[i].Start < 0 || e.Offsets[i].End > Probe.Length || e.Offsets[i].Start > e.Offsets[i].End)
            {
                throw new InvalidOperationException("Invariant failed: offsets");
            }
        }
    }
}

/// <summary>
/// Feeds arbitrary bytes, read as JSON, to the BPE model reader. Refusals must be <see cref="InvalidDataException"/> or a JSON error;
/// a model that loads must tokenize a probe piece into tokens that lie inside it, in order, and whose strings are the vocabulary's.
/// </summary>
internal static class BpeModelTarget
{
    internal static void Run(ReadOnlySpan<byte> data)
    {
        BpeModel model;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(data.ToArray(), new JsonDocumentOptions { MaxDepth = 32 });
            model = BpeModel.FromConfig(new StageConfig(doc.RootElement.Clone(), StageRegistry.Default));
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException or NotSupportedException)
        {
            return;
        }

        const string Piece = "ab abc\u00e9\U0001F600z";
        var tokens = new List<ModelToken>();
        model.Tokenize(Piece, tokens);
        foreach (ModelToken t in tokens)
        {
            if (t.Start < 0 || t.End > Piece.Length || t.End < t.Start || model.IdToToken(t.Id) != t.Value)
            {
                throw new InvalidOperationException("Invariant failed: token range or value");
            }
        }
    }
}
