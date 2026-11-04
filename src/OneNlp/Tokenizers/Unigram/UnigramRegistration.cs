using System.Text.Json;
using RecursiveAI.OneNlp.Tokenizers.Unigram;

namespace RecursiveAI.OneNlp.Tokenizers.Hf;

internal sealed partial class StageRegistry
{
    static partial void RegisterUnigram(StageRegistry registry) => registry.AddModel("Unigram", CreateUnigram);

    private static UnigramModel CreateUnigram(StageConfig config)
    {
        if (!config.Has("vocab"))
        {
            throw config.Missing("vocab");
        }

        JsonElement vocab = config.Element.GetProperty("vocab");
        if (vocab.ValueKind != JsonValueKind.Array)
        {
            throw config.Bad("vocab", "an array of [piece, score] pairs");
        }

        var pieces = new List<string>();
        var scores = new List<double>();
        try
        {
            foreach (JsonElement entry in vocab.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Array || entry.GetArrayLength() != 2
                    || entry[0].ValueKind != JsonValueKind.String || entry[1].ValueKind != JsonValueKind.Number)
                {
                    throw config.Bad("vocab", "an array of [piece, score] pairs");
                }

                pieces.Add(entry[0].GetString()!);
                scores.Add(entry[1].GetDouble());
            }
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidDataException("The vocabulary has a piece that is not valid text.", ex);
        }

        return new UnigramModel(
            [.. pieces],
            [.. scores],
            config.OptionalInt("unk_id"),
            config.Bool("byte_fallback", false));
    }
}
