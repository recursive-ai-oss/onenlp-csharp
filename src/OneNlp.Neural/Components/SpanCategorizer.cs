using System.Text.Json;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural.Encoders;

namespace RecursiveAI.OneNlp.Neural.Components;

/// <summary>
/// A span categorizer (spaCy's documented <c>spancat</c>): a suggester proposes every n-gram of the configured sizes, a classifier scores
/// each proposal for every label, and the proposals that clear the threshold go into a named span group of the document
/// (<see cref="Doc.Spans"/>). The spans of a group may overlap and nest, which entities cannot.
/// </summary>
/// <remarks>
/// <para>
/// <b>Suggester.</b> <see cref="Sizes"/> lists the span lengths in tokens (default 1, 2, 3, the documented n-gram suggester default).
/// A gold span of another length cannot be found; it is counted as dropped, not trained on.
/// </para>
/// <para>
/// <b>Classifier.</b> A span is represented as in span-based models (Lee et al. 2017 §3): the mean of its token vectors, the vector of
/// its first token and the vector of its last token, joined; an optional hidden layer with a ReLU follows, then one logit per label.
/// Each label is an independent yes/no (a sigmoid and binary cross entropy), so one span can carry several labels, as in the documented
/// non-exclusive mode. Every proposal that is not a gold span is a negative for every label.
/// </para>
/// <para>
/// <b>Output.</b> A span is written once per label whose score is at least <see cref="Threshold"/> (default 0.5), with that score as its
/// confidence, in text order (start, then length, then label order).
/// </para>
/// </remarks>
public sealed class SpanCategorizer : TrainableHead
{
    /// <summary>The kind name used by <see cref="HeadRegistry"/>: <c>spancat</c>.</summary>
    public const string ComponentKind = "spancat";

    private const int MaxLabels = 1 << 14;
    private const int MaxHidden = 4096;
    private const int MaxSizes = 32;
    private const int MaxSize = 256;
    private const long MaxWeights = 1L << 26;

    // Proposals are scored in tiles of this many, so the pooling matrices stay small whatever the document length.
    private const int TileSize = 256;

    private readonly int[] _sizes;
    private string[] _labels = [];
    private Dictionary<string, int> _labelIds = new(StringComparer.Ordinal);
    private SpanModule? _module;

    /// <summary>Creates an uninitialized span categorizer.</summary>
    /// <param name="encoder">The encoder the categorizer reads.</param>
    /// <param name="spansKey">The name of the span group it reads from the gold documents and writes to predicted ones. Default <c>sc</c>.</param>
    /// <param name="sizes">The span lengths in tokens to propose, each 1 to 256, at most 32 of them; <see langword="null"/> means 1, 2 and 3.</param>
    /// <param name="hiddenSize">Width of the hidden layer, 0 to 4096; 0 (the default) means none.</param>
    /// <param name="threshold">The score a span needs for a label to be written, above 0 and at most 1. Default 0.5.</param>
    /// <param name="name">The component name, unique in its pipeline. Default <c>spancat</c>.</param>
    /// <exception cref="ArgumentException">A size is out of range or repeated, or the key is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The hidden size or the threshold is out of range.</exception>
    public SpanCategorizer(IEncoder encoder, string spansKey = "sc", IReadOnlyList<int>? sizes = null, int hiddenSize = 0, float threshold = 0.5f, string name = "spancat")
        : base(name, encoder)
    {
        ArgumentException.ThrowIfNullOrEmpty(spansKey);
        ArgumentOutOfRangeException.ThrowIfNegative(hiddenSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(hiddenSize, MaxHidden);
        if (!(threshold > 0f && threshold <= 1f))
        {
            throw new ArgumentOutOfRangeException(nameof(threshold), threshold, "The threshold is above 0 and at most 1.");
        }

        int[] list = sizes is null ? [1, 2, 3] : [.. sizes];
        if (list.Length is 0 or > MaxSizes || list.Any(s => s is < 1 or > MaxSize) || list.Distinct().Count() != list.Length)
        {
            throw new ArgumentException($"Sizes are 1 to {MaxSize} tokens, distinct, at most {MaxSizes} of them.", nameof(sizes));
        }

        Array.Sort(list);
        SpansKey = spansKey;
        Sizes = list;
        _sizes = list;
        HiddenSize = hiddenSize;
        Threshold = threshold;
    }

    /// <summary>The span group the categorizer reads and writes.</summary>
    public string SpansKey { get; }

    /// <summary>The proposed span lengths in tokens, ascending.</summary>
    public IReadOnlyList<int> Sizes { get; }

    /// <summary>Width of the hidden layer; 0 for none.</summary>
    public int HiddenSize { get; }

    /// <summary>The score a span needs for a label to be written.</summary>
    public float Threshold { get; }

    /// <summary>The labels, in output order; empty until the categorizer is initialized.</summary>
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
            if (example.Spans is not null && example.Spans.TryGetValue(SpansKey, out IReadOnlyList<SpanSpec>? group))
            {
                foreach (SpanSpec span in group)
                {
                    seen.Add(span.Label);
                }
            }
        }

        if (seen.Count == 0)
        {
            throw new ArgumentException($"The training examples hold no gold spans in the group '{SpansKey}' for the component '{Name}'.", nameof(examples));
        }

        if (seen.Count > MaxLabels)
        {
            throw new ArgumentException($"The training examples hold more than {MaxLabels} span labels.", nameof(examples));
        }

        Build([.. seen], new Generator(seed));
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The examples hold span labels this categorizer's label set lacks. The label set is fixed once the categorizer has weights.</exception>
    public override IReadOnlyList<string> ExtendLabels(IReadOnlyList<Example> examples, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(examples);
        var unknown = new SortedSet<string>(StringComparer.Ordinal);
        foreach (Example example in examples)
        {
            if (example.Spans is not null && example.Spans.TryGetValue(SpansKey, out IReadOnlyList<SpanSpec>? group))
            {
                foreach (SpanSpec span in group)
                {
                    if (!_labelIds.ContainsKey(span.Label))
                    {
                        unknown.Add(span.Label);
                    }
                }
            }
        }

        if (unknown.Count > 0)
        {
            throw new ArgumentException(
                $"The training examples hold span labels that the component '{Name}' does not have: {string.Join(", ", unknown.Take(10))}. "
                + "A span categorizer's label set is fixed once it has weights; train a new one, or remove these values.",
                nameof(examples));
        }

        return [];
    }

    /// <inheritdoc/>
    public override Tensor Loss(EncodedBatch encoded, IReadOnlyList<Example> examples)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        ArgumentNullException.ThrowIfNull(examples);
        SpanModule module = _module ?? throw new InvalidOperationException($"The component '{Name}' is not initialized.");
        if (examples.Count != encoded.DocCount)
        {
            throw new ArgumentException("The batch has a different number of documents than examples.", nameof(examples));
        }

        var parts = new List<Tensor>();
        var targets = new List<float>();
        int labels = _labels.Length;
        for (int d = 0; d < examples.Count; d++)
        {
            int length = encoded.Length(d);
            if (length == 0)
            {
                continue;
            }

            // Gold spans, carried onto the predicted tokens; a gold span the suggester cannot propose, or that is misaligned, is skipped.
            var gold = new Dictionary<(int Start, int End), List<int>>();
            Example example = examples[d];
            if (example.Spans is not null && example.Spans.TryGetValue(SpansKey, out IReadOnlyList<SpanSpec>? group))
            {
                SpanSpec?[] mapped = example.Alignment.MapSpansYToX(group);
                for (int i = 0; i < mapped.Length; i++)
                {
                    if (mapped[i] is { } m && _labelIds.TryGetValue(m.Label, out int id) && Array.IndexOf(_sizes, m.End - m.Start) >= 0)
                    {
                        if (!gold.TryGetValue((m.Start, m.End), out List<int>? ids))
                        {
                            gold[(m.Start, m.End)] = ids = [];
                        }

                        ids.Add(id);
                    }
                }
            }

            List<(int Start, int End)> candidates = Propose(length);
            foreach ((int Start, int End) c in candidates)
            {
                int at = targets.Count;
                for (int k = 0; k < labels; k++)
                {
                    targets.Add(0f);
                }

                if (gold.TryGetValue(c, out List<int>? ids))
                {
                    foreach (int id in ids)
                    {
                        targets[at + id] = 1f;
                    }
                }
            }

            parts.AddRange(Represent(encoded, d, candidates));
        }

        if (parts.Count == 0)
        {
            // No proposals at all (every document empty): a zero that still depends on the weights.
            int width = encoded.Vectors.Shape[1] * 3;
            Tensor blank = Tensor.FromSpan<float>(new float[width], Shape.Create(1, width));
            Tensor zeros = Tensor.FromSpan<float>(new float[labels], Shape.Create(1, labels));
            ActivationScope.Track(blank);
            ActivationScope.Track(zeros);
            return Ops.Mean(Ops.Mul(module.Forward(blank), zeros));
        }

        Tensor reps = parts.Count == 1 ? parts[0] : Ops.Concat([.. parts], 0);
        Tensor logits = module.Forward(reps);
        Tensor targetMatrix = Tensor.FromSpan<float>([.. targets], Shape.Create(logits.Shape[0], labels));
        ActivationScope.Track(targetMatrix);
        return Losses.BceWithLogits(logits, targetMatrix);
    }

    /// <inheritdoc/>
    public override void Predict(EncodedBatch encoded, ReadOnlySpan<Doc> docs)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        SpanModule module = _module ?? throw new InvalidOperationException($"The component '{Name}' is not initialized.");
        int labels = _labels.Length;
        for (int d = 0; d < docs.Length; d++)
        {
            int length = encoded.Length(d);
            var found = new List<SpanSpec>();
            List<(int Start, int End)> candidates = Propose(length);
            int done = 0;
            foreach (Tensor tile in Represent(encoded, d, candidates))
            {
                ReadOnlySpan<float> logits = module.Forward(tile).Data<float>();
                int rows = tile.Shape[0];
                for (int r = 0; r < rows; r++)
                {
                    (int start, int end) = candidates[done + r];
                    for (int k = 0; k < labels; k++)
                    {
                        float p = 1f / (1f + MathF.Exp(-logits[(r * labels) + k]));
                        if (p >= Threshold)
                        {
                            found.Add(new SpanSpec(start, end, _labels[k], Math.Clamp(p, 0f, 1f)));
                        }
                    }
                }

                done += rows;
            }

            docs[d].SetSpans(SpansKey, [.. found]);
        }
    }

    /// <inheritdoc/>
    public override void WriteConfig(Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteString("spans_key", SpansKey);
        writer.WriteNumber("hidden", HiddenSize);
        writer.WriteNumber("threshold", Threshold);
        writer.WriteStartArray("sizes");
        foreach (int size in Sizes)
        {
            writer.WriteNumberValue(size);
        }

        writer.WriteEndArray();
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
        string key = JsonSettings.String(config, "spans_key", 256);
        int hidden = JsonSettings.Int(config, "hidden", 0, MaxHidden);
        JsonElement thresholdElement = JsonSettings.Property(config, "threshold");
        if (thresholdElement.ValueKind != JsonValueKind.Number || !thresholdElement.TryGetSingle(out float threshold) || !(threshold > 0f && threshold <= 1f))
        {
            throw new InvalidDataException("A span categorizer's 'threshold' must be a number above 0 and at most 1.");
        }

        JsonElement sizesElement = JsonSettings.Property(config, "sizes");
        if (sizesElement.ValueKind != JsonValueKind.Array || sizesElement.GetArrayLength() is 0 or > MaxSizes)
        {
            throw new InvalidDataException("A span categorizer's 'sizes' must be a list of 1 to 32 integers.");
        }

        var sizes = new List<int>();
        foreach (JsonElement item in sizesElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Number || !item.TryGetInt32(out int size) || size is < 1 or > MaxSize)
            {
                throw new InvalidDataException("A span categorizer's 'sizes' must be integers from 1 to 256.");
            }

            sizes.Add(size);
        }

        string[] labels = JsonSettings.Strings(config, "labels", MaxLabels, 4096);
        if (labels.Length == 0 || labels.Distinct(StringComparer.Ordinal).Count() != labels.Length || key.Length == 0 || sizes.Distinct().Count() != sizes.Count)
        {
            throw new InvalidDataException("A span categorizer needs a key, distinct sizes, and a non-empty list of distinct labels.");
        }

        var head = new SpanCategorizer(encoder, key, sizes, hidden, threshold, name) { OwnsEncoder = true };
        head.Build(labels, new Generator(0));
        return head;
    }

    // Every n-gram of a size in Sizes, ordered by start, then length.
    private List<(int Start, int End)> Propose(int length)
    {
        var list = new List<(int Start, int End)>();
        for (int start = 0; start < length; start++)
        {
            foreach (int size in Sizes)
            {
                if (start + size <= length)
                {
                    list.Add((start, start + size));
                }
            }
        }

        return list;
    }

    // [mean; first; last] of the tokens of each proposal, one matrix per tile of proposals. The three pieces are matrix products with
    // averaging and selection matrices, which carry the gradient back to the token vectors without a loop over tokens.
    private static IEnumerable<Tensor> Represent(EncodedBatch encoded, int doc, List<(int Start, int End)> candidates)
    {
        int first = encoded.Start(doc);
        for (int tileStart = 0; tileStart < candidates.Count; tileStart += TileSize)
        {
            int count = Math.Min(TileSize, candidates.Count - tileStart);
            int low = candidates[tileStart].Start;
            int high = low;
            for (int i = 0; i < count; i++)
            {
                high = Math.Max(high, candidates[tileStart + i].End);
            }

            int width = high - low;
            var mean = new float[count * width];
            var head = new float[count * width];
            var tail = new float[count * width];
            for (int i = 0; i < count; i++)
            {
                (int s, int e) = candidates[tileStart + i];
                Array.Fill(mean, 1f / (e - s), (i * width) + s - low, e - s);
                head[(i * width) + s - low] = 1f;
                tail[(i * width) + e - 1 - low] = 1f;
            }

            Tensor rows = encoded.Vectors.Slice(0, first + low, first + high);
            Tensor m = Tensor.FromSpan<float>(mean, Shape.Create(count, width));
            Tensor h = Tensor.FromSpan<float>(head, Shape.Create(count, width));
            Tensor t = Tensor.FromSpan<float>(tail, Shape.Create(count, width));
            ActivationScope.Track(m);
            ActivationScope.Track(h);
            ActivationScope.Track(t);
            yield return Ops.Concat([Ops.MatMul(m, rows), Ops.MatMul(h, rows), Ops.MatMul(t, rows)], 1);
        }
    }

    private void Build(string[] labels, Generator generator)
    {
        long inputs = 3L * Encoder.Width;
        long weights = (inputs * Math.Max(HiddenSize, labels.Length)) + ((long)HiddenSize * labels.Length);
        if (weights > MaxWeights)
        {
            throw new ArgumentException($"{labels.Length} labels over vectors of width {Encoder.Width} need more than {MaxWeights} weights.");
        }

        _labels = labels;
        _labelIds = new Dictionary<string, int>(labels.Length, StringComparer.Ordinal);
        for (int i = 0; i < labels.Length; i++)
        {
            _labelIds[labels[i]] = i;
        }

        _module = new SpanModule(Encoder.Width * 3, HiddenSize, labels.Length, generator);
    }

    private sealed class SpanModule : NnModule
    {
        private readonly Linear? _hidden;
        private readonly Linear _output;

        public SpanModule(int inputs, int hidden, int labels, Generator generator)
        {
            if (hidden > 0)
            {
                _hidden = RegisterModule("hidden", new Linear(inputs, hidden, generator));
            }

            _output = RegisterModule("output", new Linear(hidden > 0 ? hidden : inputs, labels, generator));
        }

        // Input: span representations [spans, 3 * width]. Output: one logit per label [spans, labels].
        public override Tensor Forward(Tensor input) => _output.Forward(_hidden is null ? input : Ops.Relu(_hidden.Forward(input)));
    }
}
