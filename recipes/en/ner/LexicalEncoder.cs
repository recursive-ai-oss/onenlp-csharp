using System.Text;
using System.Text.Json;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;
using RecursiveAI.OneNlp.Neural.Encoders;

namespace RecursiveAI.OneNlp.Recipes.EnNer;

/// <summary>
/// A stand-in word encoder for this recipe: each token's lower-cased form, 3-character prefix and suffix, and word shape are hashed
/// into one embedding table, joined and projected (Svenstrup et al. 2017 hash embeddings; the same features the fast tier uses). The
/// shape carries capitalization, which named entities depend on. It is replaced by the library's fast encoder when that is used.
/// </summary>
internal sealed class LexicalEncoder : IEncoder
{
    public const string EncoderKind = "lexical-recipe";

    private const int Features = 4;
    private readonly LexicalModule _module;
    private readonly int _dim;

    public LexicalEncoder(int width = 128, int dim = 64, int buckets = 1 << 15, ulong seed = 0)
    {
        Width = width;
        _dim = dim;
        Buckets = buckets;
        Seed = seed;
        _module = new LexicalModule(buckets, dim, width, new Generator(seed));
    }

    public string Kind => EncoderKind;

    public int Width { get; }

    public int Buckets { get; }

    public ulong Seed { get; }

    public NnModule? Weights => _module;

    public static IEncoder FromSettings(JsonElement s) =>
        new LexicalEncoder(s.GetProperty("width").GetInt32(), s.GetProperty("dim").GetInt32(), s.GetProperty("buckets").GetInt32(), s.GetProperty("seed").GetUInt64());

    public void Dispose() => _module.Dispose();

    public EncodedBatch Encode(ReadOnlySpan<Doc> docs)
    {
        var starts = new int[docs.Length + 1];
        for (int d = 0; d < docs.Length; d++)
        {
            starts[d + 1] = starts[d] + docs[d].Length;
        }

        int total = starts[^1];
        if (total == 0)
        {
            Tensor empty = Tensor.Zeros(DType.F32, Shape.Create(0, Width));
            ActivationScope.Track(empty);
            return new EncodedBatch(empty, starts);
        }

        var ids = new int[total * Features];
        int row = 0;
        foreach (Doc doc in docs)
        {
            for (int t = 0; t < doc.Length; t++)
            {
                string text = doc[t].ToString();
                string lower = text.ToLowerInvariant();
                ids[row++] = Bucket(1, lower);
                ids[row++] = Bucket(2, lower.Length > 3 ? lower[..3] : lower);
                ids[row++] = Bucket(3, lower.Length > 3 ? lower[^3..] : lower);
                ids[row++] = Bucket(4, WordShape(text));
            }
        }

        Tensor idTensor = Tensor.FromSpan<int>(ids, Shape.Create(total * Features));
        ActivationScope.Track(idTensor);
        return new EncodedBatch(_module.Forward(idTensor), starts);
    }

    public void WriteSettings(Utf8JsonWriter writer)
    {
        writer.WriteNumber("width", Width);
        writer.WriteNumber("dim", _dim);
        writer.WriteNumber("buckets", Buckets);
        writer.WriteNumber("seed", Seed);
    }

    // Letters become X or x, digits d, runs longer than four are cut to four (the shape convention of spaCy's documented token.shape_).
    private static string WordShape(string text)
    {
        var sb = new StringBuilder(text.Length);
        char previous = '\0';
        int run = 0;
        foreach (char c in text)
        {
            char s = char.IsUpper(c) ? 'X' : char.IsLower(c) ? 'x' : char.IsDigit(c) ? 'd' : c;
            run = s == previous ? run + 1 : 1;
            previous = s;
            if (run <= 4)
            {
                sb.Append(s);
            }
        }

        return sb.ToString();
    }

    private int Bucket(int salt, string key)
    {
        // FNV-1a over UTF-16 units, seeded with the feature number.
        ulong h = 14695981039346656037UL ^ (ulong)salt;
        foreach (char c in key)
        {
            h = (h ^ c) * 1099511628211UL;
        }

        return (int)(h % (ulong)Buckets);
    }

    private sealed class LexicalModule : NnModule
    {
        private readonly Embedding _embed;
        private readonly Linear _project;
        private readonly int _dim;

        public LexicalModule(int buckets, int dim, int width, Generator generator)
        {
            _dim = dim;
            _embed = RegisterModule("embed", new Embedding(buckets, dim, generator));
            _project = RegisterModule("project", new Linear(Features * dim, width, generator));
        }

        // Input: Int32 bucket ids [N * 4]. Output: [N, width].
        public override Tensor Forward(Tensor input) =>
            Ops.Relu(_project.Forward(_embed.Forward(input).Reshape(input.Shape[0] / Features, Features * _dim)));
    }
}
