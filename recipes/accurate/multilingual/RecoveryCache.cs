using System.Security.Cryptography;
using System.Text.Json;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;
using RecursiveAI.OneNlp.Neural.Encoders;

namespace RecursiveAI.OneNlp.Recipes.Accurate;

// Private, generated float32 cache. Keys include the graph hash (directory), original text, token boundaries and expanded word forms.
// Consequently shuffled batches and freshly cloned dev documents read the same vectors without conflating raw and gold tokenizations.
internal sealed class RecoveryCache(IEncoder inner, string directory) : IEncoder
{
    public string Kind => inner.Kind;
    public int Width => inner.Width;
    public NnModule? Weights => null;

    public EncodedBatch Encode(ReadOnlySpan<Doc> docs)
    {
        Directory.CreateDirectory(directory);
        var starts = new int[docs.Length + 1];
        for (int i = 0; i < docs.Length; i++)
        {
            starts[i + 1] = checked(starts[i] + docs[i].Length);
        }

        var values = new float[checked(starts[^1] * Width)];
        for (int i = 0; i < docs.Length; i++)
        {
            Doc doc = docs[i];
            using var key = new MemoryStream();
            using (var writer = new BinaryWriter(key, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(doc.Text);
                writer.Write(doc.Length);
                foreach (Token token in doc)
                {
                    writer.Write(token.Idx);
                    writer.Write(token.Length);
                    writer.Write(token.Norm);
                }
            }

            string path = Path.Combine(directory, Convert.ToHexStringLower(SHA256.HashData(key.ToArray())) + ".f32");
            int count = checked(doc.Length * Width);
            if (!File.Exists(path))
            {
                using var scope = new ActivationScope();
                EncodedBatch encoded = inner.Encode([doc]);
                using (var f = File.Create(path + ".new"))
                using (var w = new BinaryWriter(f))
                {
                    foreach (float value in encoded.Vectors.Data<float>())
                    {
                        w.Write(value);
                    }
                }

                File.Move(path + ".new", path, overwrite: true);
            }

            using var reader = new BinaryReader(File.OpenRead(path));
            if (reader.BaseStream.Length != (long)count * sizeof(float))
            {
                throw new InvalidDataException("A generated encoder cache has an inconsistent length; remove it and retry.");
            }

            for (int j = 0; j < count; j++)
            {
                values[starts[i] * Width + j] = reader.ReadSingle();
            }
        }

        Tensor vectors = Tensor.FromSpan<float>(values, Shape.Create(starts[^1], Width));
        ActivationScope.Track(vectors);
        return new EncodedBatch(vectors, starts);
    }

    public void WriteSettings(Utf8JsonWriter writer) => inner.WriteSettings(writer);
    public void Dispose() { }
}
