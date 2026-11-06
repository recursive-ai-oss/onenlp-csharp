using RecursiveAI.OneModel.Graph;
using RecursiveAI.OneModel.Tensors;

namespace RecursiveAI.OneNlp.Neural.Encoders;

// The inverse of the loaders in OnnxEncoderWeights.cs: trained weights back to the graph tensors they were read from.
internal sealed partial class OnnxEncoderWeights
{
    /// <summary>
    /// For every weight in <paramref name="state"/> (a module's state by name; <see langword="null"/> for a name it lacks), the patch that gives the graph tensor
    /// the weight was read from its trained values, in the graph's layout (linear weights back to [in, out]).
    /// </summary>
    /// <remarks>
    /// A BERT-family graph adds one token-type vector to every position row; the loader folds it into the position table, so here the position table is written
    /// with it folded in and the token-type table is zeroed, which gives the graph the same sums exactly.
    /// </remarks>
    public List<WeightPatch> Patches(TransformerFamily family, Func<string, Tensor?> state)
    {
        var patches = new List<WeightPatch>();

        void Add(string stateName, string lookup, bool transpose)
        {
            Tensor t = state(stateName) ?? throw new InvalidDataException($"The encoder has no weight '{stateName}'.");
            TensorData target = Initializer(lookup);
            if (target.DataType != 1)
            {
                throw new InvalidDataException($"The graph tensor '{lookup}' is not float32.");
            }

            long[] dims = transpose ? [t.Shape[1], t.Shape[0]] : [.. Enumerable.Range(0, t.Shape.Rank).Select(i => (long)t.Shape[i])];
            if (!dims.AsSpan().SequenceEqual(target.Dims.AsSpan()))
            {
                throw new InvalidDataException($"The weight '{stateName}' does not have the shape of the graph tensor '{lookup}'.");
            }

            patches.Add(new WeightPatch(lookup, dims, () => FloatsOf(t, transpose)));
        }

        if (family == TransformerFamily.ModernBert)
        {
            string p = PrefixOf("embeddings.tok_embeddings.weight");
            if (state("tokens.weight") is not null)
            {
                Add("tokens.weight", p + "embeddings.tok_embeddings.weight", false);
            }

            Add("embedding_norm.weight", p + "embeddings.norm.weight", false);
            for (int i = 0; _matmul.ContainsKey($"{p}layers.{i}.attn.Wqkv"); i++)
            {
                string l = $"{p}layers.{i}.";
                if (i > 0)
                {
                    Add($"layers{i}.attn_norm.weight", l + "attn_norm.weight", false);
                }

                Add($"layers{i}.wqkv.weight", _matmul[l + "attn.Wqkv"], true);
                Add($"layers{i}.wo.weight", _matmul[l + "attn.Wo"], true);
                Add($"layers{i}.mlp_norm.weight", l + "mlp_norm.weight", false);
                Add($"layers{i}.wi.weight", _matmul[l + "mlp.Wi"], true);
                Add($"layers{i}.mlp_wo.weight", _matmul[l + "mlp.Wo"], true);
            }

            Add("final_norm.weight", p + "final_norm.weight", false);
            return patches;
        }

        string q = PrefixOf("embeddings.word_embeddings.weight");
        bool student = _data.ContainsKey(q + "embeddings.projection.weight");
        if (state("tokens.weight") is not null)
        {
            Add("tokens.weight", q + "embeddings.word_embeddings.weight", false);
        }

        if (student)
        {
            Add("projection.weight", q + "embeddings.projection.weight", true);
            Add("projection.bias", q + "embeddings.projection.bias", false);
        }
        else
        {
            string typeName = q + "embeddings.token_type_embeddings.weight";
            TensorData type = Initializer(typeName);
            if (type.DataType != 1)
            {
                throw new InvalidDataException("The token-type table is not float32.");
            }

            long count = 1;
            foreach (long d in type.Dims)
            {
                count *= d;
            }

            patches.Add(new WeightPatch(typeName, [.. type.Dims], () => new float[count]));
        }

        Add("positions.weight", q + "embeddings.position_embeddings.weight", false);
        Add("embedding_norm.weight", q + "embeddings.LayerNorm.weight", false);
        Add("embedding_norm.bias", q + "embeddings.LayerNorm.bias", false);
        for (int i = 0; student ? _data.ContainsKey($"{q}encoder.layer.{i}.attention.query.weight") : _matmul.ContainsKey($"{q}encoder.layer.{i}.attention.self.query"); i++)
        {
            string l = $"{q}encoder.layer.{i}.";
            int layer = i;
            void Linear(string mine, string theirs)
            {
                Add($"layers{layer}.{mine}.weight", student ? l + theirs + ".weight" : _matmul[l + theirs], true);
                Add($"layers{layer}.{mine}.bias", l + theirs + ".bias", false);
            }

            void Norm(string mine, string theirs)
            {
                Add($"layers{layer}.{mine}.weight", l + theirs + ".weight", false);
                Add($"layers{layer}.{mine}.bias", l + theirs + ".bias", false);
            }

            if (student)
            {
                Linear("attn.q", "attention.query");
                Linear("attn.k", "attention.key");
                Linear("attn.v", "attention.value");
                Linear("attn.o", "attention.output");
                Norm("norm1", "attn_norm");
                Linear("ff1", "intermediate");
                Linear("ff2", "output");
                Norm("norm2", "output_norm");
            }
            else
            {
                Linear("attn.q", "attention.self.query");
                Linear("attn.k", "attention.self.key");
                Linear("attn.v", "attention.self.value");
                Linear("attn.o", "attention.output.dense");
                Norm("norm1", "attention.output.LayerNorm");
                Linear("ff1", "intermediate.dense");
                Linear("ff2", "output.dense");
                Norm("norm2", "output.LayerNorm");
            }
        }

        return patches;
    }

    private static float[] FloatsOf(Tensor t, bool transpose)
    {
        ReadOnlySpan<float> s = t.Detach().Data<float>();
        if (!transpose)
        {
            return s.ToArray();
        }

        // [out, in] back to the graph's [in, out].
        int rows = t.Shape[0], cols = t.Shape[1];
        var r = new float[s.Length];
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                r[(j * rows) + i] = s[(i * cols) + j];
            }
        }

        return r;
    }
}
