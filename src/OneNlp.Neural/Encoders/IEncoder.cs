using System.Text.Json;
using RecursiveAI.OneModel.Training;

namespace RecursiveAI.OneNlp.Neural.Encoders;

/// <summary>
/// The seam between encoders and heads: maps the tokens of documents to one vector each. Every head reads only this, so the fast
/// (hash-embedding CNN) and accurate (transformer) tiers, and encoders a user supplies, are interchangeable.
/// </summary>
/// <remarks>
/// <para>
/// <b>Inference and training use the same call.</b> <see cref="Encode"/> returns an autodiff tensor whenever recording is on and
/// the encoder has trainable parameters; inside <c>GradMode.NoGrad()</c> (which inference and a frozen encoder use) it returns plain
/// values. Dropout and other training-only behaviour follow <see cref="NnModule.IsTraining"/> of <see cref="Weights"/>.
/// </para>
/// <para>
/// <see cref="IDisposable.Dispose"/> releases the weights and may be called more than once; heads that share an encoder may each dispose it.
/// </para>
/// <para>
/// An encoder that has no parameters of its own, such as one wrapping an imported ONNX model, returns <see langword="null"/> from
/// <see cref="Weights"/> and is never trained.
/// </para>
/// </remarks>
public interface IEncoder : IDisposable
{
    /// <summary>The name this kind of encoder is saved under, for example <c>hash-embedding</c>. See <see cref="EncoderRegistry"/>.</summary>
    string Kind { get; }

    /// <summary>The length of each token's vector.</summary>
    int Width { get; }

    /// <summary>The module that owns the encoder's weights, in a fixed state order, or <see langword="null"/> when it has none to train or save.</summary>
    NnModule? Weights { get; }

    /// <summary>Encodes the tokens of a batch of documents. Empty documents contribute no rows.</summary>
    /// <param name="docs">The documents.</param>
    /// <returns>A batch with one row per token, documents stacked in order.</returns>
    EncodedBatch Encode(ReadOnlySpan<Doc> docs);

    /// <summary>Writes the settings that rebuild an encoder of the same shape (sizes, seeds) as properties of the JSON object the writer is inside.</summary>
    /// <param name="writer">A writer positioned inside an object.</param>
    void WriteSettings(Utf8JsonWriter writer);
}
