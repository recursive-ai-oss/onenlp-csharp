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
