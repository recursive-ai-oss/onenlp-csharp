using RecursiveAI.OneNlp;

namespace RecursiveAI.OneNlp.ModelConsumer.Embeddings;

// Uses Nlp.Embed the way an application does. With the embeddings package referenced it must work with no setup; without it the verb
// must fail naming the package to add.
internal static class Program
{
    private static int failures;

    private static int Main()
    {
#if NO_MODEL
        InvalidOperationException? error = Capture(() => Nlp.Embed("hello"));
        Check("embed error names the package", error is not null && error.Message.Contains("RecursiveAI.OneNlp.Models.Embeddings", StringComparison.Ordinal));
        Check("embed many error names the package", Capture(() => Nlp.EmbedMany(["hello"]))?.Message.Contains("RecursiveAI.OneNlp.Models.Embeddings", StringComparison.Ordinal) == true);
        Console.WriteLine(error?.Message);
#elif MULTI
        // The multilingual package is the default for Nlp.Embed, also when the English package is referenced too.
        float[] de = Nlp.Embed("Wie setze ich mein Passwort zurück?");
        float[] en = Nlp.Embed("How do I reset my password?");
        float[] cat = Nlp.Embed("Die Katze sitzt auf dem warmen Fensterbrett.");
        Check("dimension", de.Length == 384);
        Check("unit length", Math.Abs(Dot(de, de) - 1f) < 1e-4f);
        Check("cross-language neighbour closer than unrelated", Dot(de, en) > Dot(de, cat));
        using RecursiveAI.OneNlp.Models.Embeddings.Multilingual.E5Embedder e5 = RecursiveAI.OneNlp.Models.Embeddings.Multilingual.MultilingualEmbeddingsModel.Load();
        Check("Nlp.Embed is the query-prefixed multilingual model", Dot(e5.EmbedQuery("How do I reset my password?"), en) > 0.9999f);
        Check("passage differs from query", Dot(e5.EmbedPassage("How do I reset my password?"), en) < 0.9999f);
#if BOTH
        using var bge = RecursiveAI.OneNlp.Models.Embeddings.EmbeddingsModel.Load();
        Check("English model still loadable by hand", bge.Embed("hello").Length == 384 && Math.Abs(Dot(bge.Embed("How do I reset my password?"), en)) < 0.99f);
#endif
        Console.WriteLine($"sim(de, en) = {Dot(de, en):F3}, sim(de, cat) = {Dot(de, cat):F3}");
#else
        float[] a = Nlp.Embed("how do I reset my password");
        float[] b = Nlp.Embed("I forgot my login credentials");
        float[] c = Nlp.Embed("the cat sat on the warm windowsill");
        Check("dimension", a.Length == 384);
        Check("unit length", Math.Abs(Dot(a, a) - 1f) < 1e-4f);
        Check("similar closer than unrelated", Dot(a, b) > Dot(a, c));
        float[][] many = [.. Nlp.EmbedMany(["how do I reset my password", "I forgot my login credentials"])];
        Check("many", many.Length == 2 && Dot(many[0], a) > 0.999f && Dot(many[1], b) > 0.999f);
        Check("empty text", Nlp.Embed("").Length == 384);
        Console.WriteLine($"sim(reset, forgot) = {Dot(a, b):F3}, sim(reset, cat) = {Dot(a, c):F3}");
#endif
        Console.WriteLine(failures == 0 ? "OK" : $"FAILED {failures}");
        return failures == 0 ? 0 : 1;
    }

    private static float Dot(float[] x, float[] y)
    {
        float s = 0;
        for (int i = 0; i < x.Length; i++)
        {
            s += x[i] * y[i];
        }

        return s;
    }

    private static InvalidOperationException? Capture(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (InvalidOperationException e)
        {
            return e;
        }
    }

    private static void Check(string name, bool ok)
    {
        if (!ok)
        {
            failures++;
            Console.WriteLine("FAIL: " + name);
        }
    }
}
