using RecursiveAI.OneNlp;

namespace RecursiveAI.OneNlp.Samples;

// Sentence embeddings: Nlp.Embed with RecursiveAI.OneNlp.Models.Embeddings referenced.
internal static class EmbedSample
{
    internal static void Run()
    {
        Console.WriteLine("embeddings");

        // <sample>
        float[] question = Nlp.Embed("how do I reset my password");        // 384 floats, length 1 (normalized)
        float[] related = Nlp.Embed("I forgot my login credentials");
        float[] unrelated = Nlp.Embed("the best way to bake sourdough bread");

        static float Dot(float[] a, float[] b) => a.Zip(b, (x, y) => x * y).Sum();
        // </sample>

        Program.Check("384 dimensions", question.Length == 384);
        Program.Check("related text is closer than unrelated text", Dot(question, related) > Dot(question, unrelated));
    }
}
