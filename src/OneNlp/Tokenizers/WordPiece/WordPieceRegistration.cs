using System.Text.Json;
using RecursiveAI.OneNlp.Tokenizers.WordLevel;
using RecursiveAI.OneNlp.Tokenizers.WordPiece;

namespace RecursiveAI.OneNlp.Tokenizers.Hf;

internal sealed partial class StageRegistry
{
    /// <summary>The fields of a WordPiece model object are optional except the vocabulary; these are the defaults of the published format.</summary>
    internal const string DefaultUnkToken = "[UNK]";

    static partial void RegisterWordPiece(StageRegistry registry)
    {
        registry.AddModel("WordPiece", CreateWordPiece);
        registry.AddModel("WordLevel", CreateWordLevel);
    }

    private static WordPieceModel CreateWordPiece(StageConfig config) => new(
        ReadVocabularyObject(config),
        config.OptionalString("unk_token") ?? DefaultUnkToken,
        config.OptionalString("continuing_subword_prefix") ?? "##",
        config.Int("max_input_chars_per_word", 100));

    private static WordLevelModel CreateWordLevel(StageConfig config) => new(
        ReadVocabularyObject(config),
        config.OptionalString("unk_token") ?? DefaultUnkToken);

    /// <summary>Reads the <c>vocab</c> object (token to ID) of a WordPiece or WordLevel model.</summary>
    private static TokenIdTable ReadVocabularyObject(StageConfig config)
    {
        if (!config.Has("vocab"))
        {
            throw config.Missing("vocab");
        }

        JsonElement vocab = config.Element.GetProperty("vocab");
        if (vocab.ValueKind != JsonValueKind.Object)
        {
            throw config.Bad("vocab", "an object mapping tokens to IDs");
        }

        var entries = new List<KeyValuePair<string, int>>();
        try
        {
            foreach (JsonProperty p in vocab.EnumerateObject())
            {
                if (p.Value.ValueKind != JsonValueKind.Number || !p.Value.TryGetInt32(out int id))
                {
                    throw config.Bad("vocab", "an object whose values are 32-bit integers");
                }

                entries.Add(new KeyValuePair<string, int>(p.Name, id));
            }
        }
        catch (InvalidOperationException ex)
        {
            // A name with an unpaired surrogate escape cannot be read as a string.
            throw new InvalidDataException("The vocabulary has a token that is not valid text.", ex);
        }

        return new TokenIdTable(entries);
    }
}
