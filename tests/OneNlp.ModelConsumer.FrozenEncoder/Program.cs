using RecursiveAI.OneNlp;
using RecursiveAI.OneNlp.Models.Multilingual.FrozenEncoder;
using RecursiveAI.OneNlp.Neural.Encoders;

namespace RecursiveAI.OneNlp.ModelConsumer.FrozenEncoder;

// Loads the frozen encoder from the packed package and encodes sentences in three languages: one finite 384-wide row per token, and the same
// sentence encodes to the same rows twice.
internal static class Program
{
    private static int Main()
    {
        int failures = 0;
        using TransformerEncoder encoder = FrozenStudentEncoder.Create();
        (Language, string)[] texts =
        [
            (Language.English, "The quick brown fox jumps over the lazy dog."),
            (Language.German, "Der Hund läuft schnell durch den Park."),
            (Language.Chinese, "他和记者们谈论了国家的经济预算。"),
        ];
        foreach ((Language language, string text) in texts)
        {
            using Pipeline tokenizer = Pipeline.Blank(language);
            Doc doc = tokenizer.Process(text);
            EncodedBatch a = encoder.Encode([doc]);
            EncodedBatch b = encoder.Encode([doc]);
            float[] va = a.Vectors.Data<float>().ToArray();
            float[] vb = b.Vectors.Data<float>().ToArray();
            bool shape = encoder.Width == FrozenStudentEncoder.Width && a.TokenCount == doc.Length && va.Length == doc.Length * FrozenStudentEncoder.Width;
            bool finite = va.All(float.IsFinite) && va.Any(x => x != 0f);
            bool same = va.AsSpan().SequenceEqual(vb);
            Console.WriteLine($"{language}: tokens={doc.Length} shape={shape} finite={finite} repeatable={same}");
            if (!shape || !finite || !same)
            {
                failures++;
            }
        }

        Console.WriteLine(failures == 0 ? "OK" : "FAILED");
        return failures == 0 ? 0 : 1;
    }
}
