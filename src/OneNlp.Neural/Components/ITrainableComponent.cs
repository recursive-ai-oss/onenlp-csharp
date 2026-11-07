using System.Text.Json;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural.Encoders;

namespace RecursiveAI.OneNlp.Neural.Components;

/// <summary>
/// A pipeline component with weights that <c>Train</c> can fit: a head that reads an encoder's token vectors and writes annotations.
/// The tagger, morphologizer, lemmatizer, parser, entity recognizer, span categorizer and text classifier all implement it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Life cycle.</b> A head is created without a label set, then <see cref="Initialize"/> reads the training examples to fix its
/// labels and build its weights, then it is trained (<see cref="Loss"/>), then it predicts (<see cref="IPipelineComponent.Process"/>,
/// which calls <see cref="Predict"/>). A head rebuilt from a saved pipeline (<see cref="HeadRegistry"/>) is created already
/// initialized, and its weights are loaded into <see cref="Weights"/>.
/// </para>
/// <para>
/// <b>Why the contract is this wide.</b> <see cref="Loss"/> receives the whole encoded batch and the examples, and
/// <see cref="Predict"/> the whole batch and the documents, so a head decides how to use the rows: one softmax over every token
/// (a tagger), a sequence of transition decisions per document with oracle-chosen features (a transition parser or entity
/// recognizer), a biaffine score matrix per document, or a pooled vector per document (a text classifier). A head returns one
/// scalar loss; the trainer adds the heads' losses and runs backward once.
/// </para>
/// </remarks>
public interface ITrainableComponent : IPipelineComponent
{
    /// <summary>The name this kind of head is saved under, for example <c>tagger</c>. See <see cref="HeadRegistry"/>.</summary>
    string Kind { get; }

    /// <summary>The encoder the head reads. Heads of one pipeline may share an encoder.</summary>
    IEncoder Encoder { get; }

    /// <summary>Whether the label set and weights exist.</summary>
    bool IsInitialized { get; }

    /// <summary>The module that owns the head's weights, in a fixed state order.</summary>
    /// <exception cref="InvalidOperationException">The head is not initialized.</exception>
    NnModule Weights { get; }

    /// <summary>Fixes the label set from the gold annotations of the training examples and creates the weights.</summary>
    /// <param name="examples">The training examples. The label set depends only on their reference documents, in a deterministic order.</param>
    /// <param name="seed">Seed of the initial weights.</param>
    /// <exception cref="InvalidOperationException">The head is already initialized.</exception>
    /// <exception cref="ArgumentException">The examples hold no gold annotation for this head.</exception>
    void Initialize(IReadOnlyList<Example> examples, ulong seed);

    /// <summary>
    /// Called by <c>Train</c> on a head that already has weights, before training: makes the head ready for the labels of the training
    /// examples. A head that can grow its label set (the entity recognizer) appends the labels it lacks, keeping every weight it has;
    /// a head that cannot throws for labels it does not know rather than training on data it would silently skip. The default does nothing.
    /// </summary>
    /// <param name="examples">The training examples.</param>
    /// <param name="seed">Seed of the weights created for new labels.</param>
    /// <returns>The labels added, in output order; empty when the head was ready as it was.</returns>
    /// <exception cref="ArgumentException">The examples hold labels the head does not have and cannot add.</exception>
    IReadOnlyList<string> ExtendLabels(IReadOnlyList<Example> examples, ulong seed) => [];

    /// <summary>Computes the training loss of a batch.</summary>
    /// <param name="encoded">The encoder output for <c>examples[i].Predicted</c>, in order; it carries gradients into the encoder when the encoder is being trained.</param>
    /// <param name="examples">The batch.</param>
    /// <returns>A rank-0 float32 tensor that backpropagates into the head's weights, and into the encoder through <paramref name="encoded"/>.</returns>
    Tensor Loss(EncodedBatch encoded, IReadOnlyList<Example> examples);

    /// <summary>Writes predictions into documents.</summary>
    /// <param name="encoded">The encoder output for <paramref name="docs"/>, in order, without gradients.</param>
    /// <param name="docs">The documents to annotate.</param>
    void Predict(EncodedBatch encoded, ReadOnlySpan<Doc> docs);

    /// <summary>Writes what rebuilds the head's shape (labels and settings) as properties of the JSON object the writer is inside.</summary>
    /// <param name="writer">A writer positioned inside an object.</param>
    void WriteConfig(Utf8JsonWriter writer);
}
