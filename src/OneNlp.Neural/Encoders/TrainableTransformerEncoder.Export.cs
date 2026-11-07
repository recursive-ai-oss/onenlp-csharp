using System.Security.Cryptography;
using System.Text.Json;
using RecursiveAI.OneModel.Graph;
using RecursiveAI.OneModel.Onnx;
using RecursiveAI.OneModel.Tensors;

namespace RecursiveAI.OneNlp.Neural.Encoders;

public sealed partial class TrainableTransformerEncoder
{
    /// <summary>
    /// Writes the encoder, with its current (fine-tuned) weights, as an ONNX file of the same graph as the model it was loaded from, so that
    /// <see cref="TransformerEncoder"/> runs it on OneModel's inference runtime, which is about three times faster than the trainable modules.
    /// </summary>
    /// <param name="path">The destination, replaced if it exists. It must not be the file the encoder was loaded from.</param>
    /// <returns>The SHA-256 of the written file as lower-case hex.</returns>
    /// <remarks>
    /// The file is the original export with the values of the layers' weights replaced (and the token table too when it trained); the graph, its
    /// operators and every other byte are unchanged, so it loads wherever the original did. The original must still be at the path it was loaded from. The
    /// output is as large as the original (about 1.2 GB for mmBERT-base). Hidden states of the written graph equal those of this encoder up to the
    /// floating-point differences between OneModel's graph kernels and its autodiff ops (<c>docs/accurate-encoder.md</c>).
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="path"/> is the file the encoder was loaded from.</exception>
    /// <exception cref="InvalidDataException">The original graph no longer matches the encoder's weights.</exception>
    /// <exception cref="NotSupportedException">The original keeps weights in external files, or shares a tensor with an unpatched name, or the result would exceed 2 GiB.</exception>
    public string ExportOnnx(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (string.Equals(Path.GetFullPath(path), Path.GetFullPath(_modelFile), StringComparison.Ordinal))
        {
            throw new ArgumentException("The destination is the file the encoder was loaded from.", nameof(path));
        }

        var state = new Dictionary<string, Tensor>(StringComparer.Ordinal);
        foreach ((string name, Tensor tensor) in _module.StateDict())
        {
            state[name] = tensor;
        }

        var requested = new HashSet<string>(StringComparer.Ordinal);
        List<WeightPatch> patches;
        using (IrModel model = OnnxImporter.Load(_modelFile))
        {
            patches = new OnnxEncoderWeights(model.Graph).Patches(_family, name =>
            {
                if (state.TryGetValue(name, out Tensor t))
                {
                    requested.Add(name);
                    return t;
                }

                return null;
            });
        }

        if (requested.Count != state.Count)
        {
            throw new InvalidDataException($"The encoder has {state.Count} weights but the graph accounts for {requested.Count}.");
        }

        OnnxWeightPatcher.Patch(_modelFile, path, patches);
        using FileStream written = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(written));
    }

    /// <summary>The settings of a <see cref="TransformerEncoder"/> that reads <paramref name="graphFile"/>, the file <see cref="ExportOnnx"/> wrote beside the saved pipeline.</summary>
    internal void WriteGraphSettings(Utf8JsonWriter writer, string graphFile, string sha256)
    {
        writer.WriteString("model", graphFile);
        writer.WriteString("relativeTo", "pipeline");
        writer.WriteString("tokenizer", _tokenizerFile);
        writer.WriteString("modelSha256", sha256);
        writer.WriteString("pooling", Pooling == SubwordPooling.Mean ? "mean" : "first");
        writer.WriteNumber("maxLength", MaxLength);
        if (_options.ExpandWordForms)
        {
            writer.WriteBoolean("expandForms", true);
        }
    }
}
