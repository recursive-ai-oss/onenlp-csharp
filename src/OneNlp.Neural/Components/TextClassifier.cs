using System.Text.Json;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural.Encoders;

namespace RecursiveAI.OneNlp.Neural.Components;

/// <summary>
/// A text classifier (spaCy's documented <c>textcat</c> and <c>textcat_multilabel</c>): it averages the encoder's token vectors over a
/// document and scores the categories from that one pooled vector. The scores go to <see cref="Doc.Cats"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Design.</b> This is the pooled bag-of-vectors classifier of Joulin et al. 2016 (§2.1: average the word representations, then a
/// linear classifier), with the encoder's context-aware token vectors in place of word and n-gram embeddings, so any
/// <see cref="IEncoder"/> works: the fast encoder (hash embeddings and a window CNN, as in Kim 2014 §2 with mean pooling) or a transformer.
/// An optional hidden layer with a ReLU sits between the pooled vector and the output.
/// </para>
/// <para>
/// <b>Exclusive and multi-label.</b> With <c>multiLabel</c> off, exactly one category is true: a softmax over the labels, trained with
/// cross entropy on the gold category (the label with the highest gold score), and the scores sum to 1. With it on, each category is its
/// own yes/no: a sigmoid per label, trained with binary cross entropy, where a label missing from an example's gold <see cref="Example.Cats"/>
/// counts as 0 (spaCy's convention). The confidence of a prediction is the score of the category.
/// </para>
/// <para>
/// The label set is every category named in the training examples' gold cats, in ordinal order. An example with no gold cats is left out
/// of the loss when the classifier is exclusive.
/// </para>
/// </remarks>
public sealed class TextClassifier : TrainableHead
{
    /// <summary>The kind name used by <see cref="HeadRegistry"/>: <c>textcat</c>.</summary>
    public const string ComponentKind = "textcat";

    private const int IgnoreIndex = -100;
    private const int MaxLabels = 1 << 16;
    private const int MaxHidden = 4096;
    private const int PredictGroup = 64;
    private const long MaxWeights = 1L << 26;

    private string[] _labels = [];
    private Dictionary<string, int> _labelIds = new(StringComparer.Ordinal);
    private ClassifierModule? _module;

    /// <summary>Creates an uninitialized classifier.</summary>
    /// <param name="encoder">The encoder the classifier reads.</param>
    /// <param name="multiLabel">Whether several categories can be true at once. Default false (exclusive classes).</param>
    /// <param name="hiddenSize">Width of the hidden layer between the pooled vector and the output, 0 to 4096; 0 (the default) means none.</param>
    /// <param name="name">The component name, unique in its pipeline. Default <c>textcat</c>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="hiddenSize"/> is out of range.</exception>
    public TextClassifier(IEncoder encoder, bool multiLabel = false, int hiddenSize = 0, string name = "textcat")
        : base(name, encoder)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(hiddenSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(hiddenSize, MaxHidden);
        MultiLabel = multiLabel;
        HiddenSize = hiddenSize;
    }

    /// <summary>Whether several categories can be true at once.</summary>
    public bool MultiLabel { get; }

    /// <summary>Width of the hidden layer; 0 for none.</summary>
    public int HiddenSize { get; }

    /// <summary>The categories, in output order; empty until the classifier is initialized.</summary>
    public IReadOnlyList<string> Labels => _labels;

    /// <inheritdoc/>
    public override string Kind => ComponentKind;

    /// <inheritdoc/>
    public override bool IsInitialized => _module is not null;

    /// <inheritdoc/>
    public override NnModule Weights => _module ?? throw new InvalidOperationException($"The component '{Name}' is not initialized.");

    /// <inheritdoc/>
    public override void Initialize(IReadOnlyList<Example> examples, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(examples);
        if (IsInitialized)
        {
            throw new InvalidOperationException($"The component '{Name}' is already initialized.");
        }

        var seen = new SortedSet<string>(StringComparer.Ordinal);
        foreach (Example example in examples)
        {
            if (example.Cats is null)
            {
                continue;
            }

            foreach (string label in example.Cats.Keys)
            {
                seen.Add(label);
            }
        }

        if (seen.Count == 0)
        {
            throw new ArgumentException($"The training examples hold no gold categories for the component '{Name}'.", nameof(examples));
        }

        if (seen.Count > MaxLabels)
        {
            throw new ArgumentException($"The training examples hold more than {MaxLabels} categories.", nameof(examples));
        }

        Build([.. seen], new Generator(seed));
    }

    /// <inheritdoc/>
    public override Tensor Loss(EncodedBatch encoded, IReadOnlyList<Example> examples)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        ArgumentNullException.ThrowIfNull(examples);
        ClassifierModule module = _module ?? throw new InvalidOperationException($"The component '{Name}' is not initialized.");
        if (examples.Count != encoded.DocCount)
        {
            throw new ArgumentException("The batch has a different number of documents than examples.", nameof(examples));
        }

        Tensor logits = module.Forward(Pool(encoded, 0, encoded.DocCount));
        if (MultiLabel)
        {
            var targets = new float[examples.Count * _labels.Length];
            for (int i = 0; i < examples.Count; i++)
            {
                if (examples[i].Cats is { } cats)
                {
                    foreach (KeyValuePair<string, float> cat in cats)
                    {
                        if (_labelIds.TryGetValue(cat.Key, out int id))
                        {
                            targets[(i * _labels.Length) + id] = cat.Value;
                        }
                    }
                }
            }

            Tensor targetMatrix = Tensor.FromSpan<float>(targets, Shape.Create(examples.Count, _labels.Length));
            ActivationScope.Track(targetMatrix);
            return Losses.BceWithLogits(logits, targetMatrix);
        }

        var classes = new int[examples.Count];
        int counted = 0;
        for (int i = 0; i < examples.Count; i++)
        {
            classes[i] = IgnoreIndex;
            if (examples[i].Cats is not { } cats)
            {
                continue;
            }

            float best = 0f;
            foreach (KeyValuePair<string, float> cat in cats)
            {
                if (cat.Value > best && _labelIds.TryGetValue(cat.Key, out int id))
                {
                    best = cat.Value;
                    classes[i] = id;
                }
            }

            if (classes[i] != IgnoreIndex)
            {
                counted++;
            }
        }

        Tensor targetTensor = Tensor.FromSpan<int>(classes, Shape.Create(classes.Length));
        ActivationScope.Track(targetTensor);

        // With no counted example the mean is NaN; the sum is a zero that still depends on the weights.
        return Losses.CrossEntropy(logits, targetTensor, 0f, IgnoreIndex, counted > 0);
    }

    /// <inheritdoc/>
    public override void Predict(EncodedBatch encoded, ReadOnlySpan<Doc> docs)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        ClassifierModule module = _module ?? throw new InvalidOperationException($"The component '{Name}' is not initialized.");
        int labels = _labels.Length;
        var scores = new float[labels];
        var pairs = new KeyValuePair<string, float>[labels];
        ReadOnlySpan<float> values = default;
        for (int d = 0; d < docs.Length; d++)
        {
            // Pooled in groups so the pooling matrix stays small however many documents are scored at once.
            if (d % PredictGroup == 0)
            {
                values = module.Forward(Pool(encoded, d, Math.Min(PredictGroup, docs.Length - d))).Data<float>();
            }

            ReadOnlySpan<float> row = values.Slice((d % PredictGroup) * labels, labels);
            if (MultiLabel)
            {
                for (int k = 0; k < labels; k++)
                {
                    scores[k] = Sigmoid(row[k]);
                }
            }
            else
            {
                float max = float.NegativeInfinity;
                for (int k = 0; k < labels; k++)
                {
                    max = Math.Max(max, row[k]);
                }

                float sum = 0f;
                for (int k = 0; k < labels; k++)
                {
                    scores[k] = MathF.Exp(row[k] - max);
                    sum += scores[k];
                }

                for (int k = 0; k < labels; k++)
                {
                    scores[k] /= sum;
                }
            }

            for (int k = 0; k < labels; k++)
            {
                float s = scores[k];
                pairs[k] = new KeyValuePair<string, float>(_labels[k], float.IsNaN(s) ? 0f : Math.Clamp(s, 0f, 1f));
            }

            docs[d].SetCats(pairs);
        }
    }

    /// <inheritdoc/>
    public override void WriteConfig(Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteBoolean("multi_label", MultiLabel);
        writer.WriteNumber("hidden", HiddenSize);
        writer.WriteStartArray("labels");
        foreach (string label in _labels)
        {
            writer.WriteStringValue(label);
        }

        writer.WriteEndArray();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _module?.Dispose();
        }

        base.Dispose(disposing);
    }

    internal static ITrainableComponent FromConfig(string name, IEncoder encoder, JsonElement config)
    {
        JsonElement multi = JsonSettings.Property(config, "multi_label");
        if (multi.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new InvalidDataException("A text classifier's 'multi_label' must be true or false.");
        }

        int hidden = JsonSettings.Int(config, "hidden", 0, MaxHidden);
        string[] labels = JsonSettings.Strings(config, "labels", MaxLabels, 4096);
        if (labels.Length == 0 || labels.Distinct(StringComparer.Ordinal).Count() != labels.Length)
        {
            throw new InvalidDataException("A text classifier's 'labels' must be a non-empty list without duplicates.");
        }

        var head = new TextClassifier(encoder, multi.GetBoolean(), hidden, name) { OwnsEncoder = true };
        head.Build(labels, new Generator(0));
        return head;
    }

    private static float Sigmoid(float x) => 1f / (1f + MathF.Exp(-x));

    // The mean of each document's token vectors as one matrix product: row d of the pooling matrix holds 1/length over document d's rows.
    // A matrix product carries the gradient back to every token vector with no per-document loop.
    private static Tensor Pool(EncodedBatch encoded, int first, int docs)
    {
        int from = encoded.Start(first);
        int tokens = encoded.Start(first + docs) - from;
        var weights = new float[(long)docs * tokens];
        for (int d = 0; d < docs; d++)
        {
            int n = encoded.Length(first + d);
            if (n == 0)
            {
                continue;
            }

            Array.Fill(weights, 1f / n, (d * tokens) + encoded.Start(first + d) - from, n);
        }

        Tensor matrix = Tensor.FromSpan<float>(weights, Shape.Create(docs, tokens));
        ActivationScope.Track(matrix);
        Tensor rows = docs == encoded.DocCount ? encoded.Vectors : encoded.Vectors.Slice(0, from, from + tokens);
        return Ops.MatMul(matrix, rows);
    }

    private void Build(string[] labels, Generator generator)
    {
        long weights = ((long)Encoder.Width * Math.Max(HiddenSize, labels.Length)) + ((long)HiddenSize * labels.Length);
        if (weights > MaxWeights)
        {
            throw new ArgumentException($"{labels.Length} categories over vectors of width {Encoder.Width} need more than {MaxWeights} weights.");
        }

        _labels = labels;
        _labelIds = new Dictionary<string, int>(labels.Length, StringComparer.Ordinal);
        for (int i = 0; i < labels.Length; i++)
        {
            _labelIds[labels[i]] = i;
        }

        _module = new ClassifierModule(Encoder.Width, HiddenSize, labels.Length, generator);
    }

    private sealed class ClassifierModule : NnModule
    {
        private readonly Linear? _hidden;
        private readonly Linear _output;

        public ClassifierModule(int width, int hidden, int labels, Generator generator)
        {
            if (hidden > 0)
            {
                _hidden = RegisterModule("hidden", new Linear(width, hidden, generator));
            }

            _output = RegisterModule("output", new Linear(hidden > 0 ? hidden : width, labels, generator));
        }

        // Input: pooled vectors [docs, width]. Output: category scores [docs, labels].
        public override Tensor Forward(Tensor input) => _output.Forward(_hidden is null ? input : Ops.Relu(_hidden.Forward(input)));
    }
}
