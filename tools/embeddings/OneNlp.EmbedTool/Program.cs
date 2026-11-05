using System.Diagnostics;
using System.Text.Json;
using RecursiveAI.OneNlp.Neural.Embeddings;

// Usage:
//   EmbedTool run   MODEL_DIR TEXTS.jsonl OUT_PREFIX [--model FILE] [--prefix TEXT] [--batch N] [--time]
//       TEXTS.jsonl has one JSON string per line. Writes OUT_PREFIX.ids.json (token ids per text) and OUT_PREFIX.f32
//       (little-endian float32, texts x dimensions).
//   EmbedTool serve MODEL_DIR [--model FILE] [--prefix TEXT] [--batch N]
//       Reads one JSON array of strings per line from stdin; for each writes an int32 count, an int32 dimension and the
//       float32 vectors to stdout. Ends at end of input.
if (args.Length < 2)
{
    Console.Error.WriteLine("usage: EmbedTool run|serve MODEL_DIR ...");
    return 2;
}

string command = args[0];
string dir = args[1];
string modelFile = Option("--model") ?? "model.onnx";
string prefix = Option("--prefix") ?? "";
int batch = int.TryParse(Option("--batch"), out int b) ? b : 32;
using SentenceEmbedder embedder = SentenceEmbedder.Load(dir, modelFile, prefix);
embedder.BatchSize = batch;

if (command == "tokens")
{
    // Debug aid: prints the pieces, character offsets and special-token flags the bare tokenizer gives for a text.
    var tokenizer = RecursiveAI.OneNlp.Tokenizers.Tokenizer.FromFile(Path.Combine(dir, "tokenizer.json"));
    RecursiveAI.OneNlp.Tokenizers.TokenEncoding e = tokenizer.Encode(args[2].AsSpan());
    for (int i = 0; i < e.Length; i++)
    {
        Console.WriteLine($"{e.Ids[i]}\t{e.Tokens[i]}\t{e.Offsets[i].Start}-{e.Offsets[i].End}\t{e.SpecialTokensMask[i]}");
    }

    Console.WriteLine("marker id: " + tokenizer.TokenToId("\u2581"));
    return 0;
}

if (command == "run")
{
    string[] lines = File.ReadAllLines(args[2]);
    var texts = new string[lines.Length];
    for (int i = 0; i < lines.Length; i++)
    {
        texts[i] = JsonSerializer.Deserialize(lines[i], ToolJson.Default.String) ?? "";
    }

    string outPrefix = args[3];
    var ids = new int[texts.Length][];
    for (int i = 0; i < texts.Length; i++)
    {
        ids[i] = embedder.TokenIds(texts[i]);
    }

    File.WriteAllText(outPrefix + ".ids.json", JsonSerializer.Serialize(ids, ToolJson.Default.Int32ArrayArray));
    var sw = Stopwatch.StartNew();
    float[][] vectors = embedder.EmbedBatch(texts);
    sw.Stop();
    using (var f = new BinaryWriter(File.Create(outPrefix + ".f32")))
    {
        foreach (float[] v in vectors)
        {
            foreach (float x in v)
            {
                f.Write(x);
            }
        }
    }

    if (args.Contains("--time"))
    {
        Console.WriteLine($"{texts.Length} texts in {sw.Elapsed.TotalSeconds:F2} s = {texts.Length / sw.Elapsed.TotalSeconds:F1} texts/s (batch {batch})");
    }

    return 0;
}

if (command == "serve")
{
    using Stream stdout = Console.OpenStandardOutput();
    using var writer = new BinaryWriter(stdout);
    string? line;
    while ((line = Console.In.ReadLine()) is not null)
    {
        string[] texts = JsonSerializer.Deserialize(line, ToolJson.Default.StringArray) ?? [];
        float[][] vectors = embedder.EmbedBatch(texts);
        writer.Write(vectors.Length);
        writer.Write(embedder.Dimensions);
        foreach (float[] v in vectors)
        {
            foreach (float x in v)
            {
                writer.Write(x);
            }
        }

        writer.Flush();
    }

    return 0;
}

Console.Error.WriteLine("unknown command " + command);
return 2;

string? Option(string name)
{
    int i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

[System.Text.Json.Serialization.JsonSerializable(typeof(string))]
[System.Text.Json.Serialization.JsonSerializable(typeof(string[]))]
[System.Text.Json.Serialization.JsonSerializable(typeof(int[][]))]
internal sealed partial class ToolJson : System.Text.Json.Serialization.JsonSerializerContext;
