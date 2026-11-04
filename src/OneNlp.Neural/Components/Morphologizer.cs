using System.Text;
using System.Text.Json;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural.Encoders;

namespace RecursiveAI.OneNlp.Neural.Components;

/// <summary>
/// A morphologizer: one softmax over joint labels, each the coarse part of speech together with the whole morphological feature
/// string (<c>POS=NOUN|Number=Sing</c>), the scheme the spaCy morphologizer documents. It writes both
/// <see cref="TokenColumn.Pos"/> and <see cref="TokenColumn.Morph"/>, and the label's probability as the token's confidence.
/// </summary>
/// <remarks>
/// <para>
/// Treating the pair as one label means the two always agree and one classifier serves both, at the price of a larger label set (a few
/// hundred on English); the factored alternative (one head per feature) was not needed to reach the reference scores.
/// </para>
/// <para>
/// The label set is every joint value in the training examples' reference documents, in ordinal order. A token takes part in the
/// loss when it lies in exactly one reference token that has a part of speech; a missing feature string there means "no features".
/// </para>
/// </remarks>
public sealed class Morphologizer : TrainableHead
{
    /// <summary>The kind name used by <see cref="HeadRegistry"/>: <c>morphologizer</c>.</summary>
    public const string ComponentKind = "morphologizer";

    private const int IgnoreIndex = -100;
    private const int MaxLabels = 1 << 16;
    private const string PosPrefix = "POS=";
    private string[] _labels = [];
    private Dictionary<string, int> _labelIds = new(StringComparer.Ordinal);
    private (string Pos, string Morph)[] _parts = [];
    private LabelModule? _module;

    /// <summary>Creates an uninitialized morphologizer.</summary>
    /// <param name="encoder">The encoder it reads.</param>
    /// <param name="name">The component name, unique in its pipeline. Default <c>morphologizer</c>.</param>
    public Morphologizer(IEncoder encoder, string name = "morphologizer")
        : base(name, encoder)
    {
    }

    /// <summary>The joint labels, in output order; empty until initialized.</summary>
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
            Doc reference = example.Reference;
            ReadOnlySpan<int> pos = reference.GetColumn(TokenColumn.Pos);
            ReadOnlySpan<int> morph = reference.GetColumn(TokenColumn.Morph);
            for (int t = 0; t < pos.Length; t++)
            {
                if (pos[t] != 0)
                {
                    seen.Add(Join(reference.Strings[pos[t]], morph[t] == 0 ? string.Empty : reference.Strings[morph[t]]));
                }
            }
        }

        if (seen.Count == 0)
        {
            throw new ArgumentException($"The training examples hold no gold part-of-speech values for the component '{Name}'.", nameof(examples));
        }

        if (seen.Count > MaxLabels)
        {
            throw new ArgumentException($"The training examples hold more than {MaxLabels} distinct part-of-speech and feature combinations.", nameof(examples));
        }

        Build([.. seen], new Generator(seed));
    }

    /// <inheritdoc/>
    public override Tensor Loss(EncodedBatch encoded, IReadOnlyList<Example> examples)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        ArgumentNullException.ThrowIfNull(examples);
        LabelModule module = _module ?? throw new InvalidOperationException($"The component '{Name}' is not initialized.");
        if (examples.Count != encoded.DocCount)
        {
            throw new ArgumentException("The batch has a different number of documents than examples.", nameof(examples));
        }

        var targets = new int[encoded.TokenCount];
        int counted = 0;
        int row = 0;
        for (int i = 0; i < examples.Count; i++)
        {
            string?[] pos = examples[i].GetAligned(TokenColumn.Pos);
            string?[] morph = examples[i].GetAligned(TokenColumn.Morph);
            if (pos.Length != encoded.Length(i))
            {
                throw new ArgumentException($"Example {i} has {pos.Length} tokens but {encoded.Length(i)} encoder rows.", nameof(examples));
            }

            for (int t = 0; t < pos.Length; t++)
            {
                if (pos[t] is { } p && _labelIds.TryGetValue(Join(p, morph[t] ?? string.Empty), out int id))
                {
                    targets[row++] = id;
                    counted++;
                }
                else
                {
                    targets[row++] = IgnoreIndex;
                }
            }
        }

        Tensor targetTensor = Tensor.FromSpan<int>(targets, Shape.Create(targets.Length));
        ActivationScope.Track(targetTensor);
        return Losses.CrossEntropy(module.Forward(encoded.Vectors), targetTensor, 0f, IgnoreIndex, counted > 0);
    }

    /// <inheritdoc/>
    public override void Predict(EncodedBatch encoded, ReadOnlySpan<Doc> docs)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        LabelModule module = _module ?? throw new InvalidOperationException($"The component '{Name}' is not initialized.");
        Tensor probabilities = Ops.Softmax(module.Forward(encoded.Vectors));
        ReadOnlySpan<float> p = probabilities.Data<float>();
        int classes = _labels.Length;
        for (int d = 0; d < docs.Length; d++)
        {
            Doc doc = docs[d];
            int start = encoded.Start(d);
            var pos = new int[doc.Length];
            var morph = new int[doc.Length];
            var confidence = new float[doc.Length];
            for (int t = 0; t < doc.Length; t++)
            {
                ReadOnlySpan<float> row = p.Slice((start + t) * classes, classes);
                int best = 0;
                for (int c = 1; c < classes; c++)
                {
                    if (row[c] > row[best])
                    {
                        best = c;
                    }
                }

                (string posValue, string morphValue) = _parts[best];
                pos[t] = doc.Strings.Add(posValue);
                morph[t] = morphValue.Length == 0 ? 0 : doc.Strings.Add(morphValue);
                confidence[t] = Math.Clamp(row[best], 0f, 1f);
            }

            doc.SetColumn(TokenColumn.Pos, pos);
            doc.SetColumn(TokenColumn.Morph, morph);
            doc.SetConfidences(confidence);
        }
    }

    /// <inheritdoc/>
    public override void WriteConfig(Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
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
        string[] labels = JsonSettings.Strings(config, "labels", MaxLabels, 4096);
        if (labels.Length == 0 || labels.Distinct(StringComparer.Ordinal).Count() != labels.Length)
        {
            throw new InvalidDataException("A morphologizer's 'labels' must be a non-empty list without duplicates.");
        }

        foreach (string label in labels)
        {
            if (!label.StartsWith(PosPrefix, StringComparison.Ordinal))
            {
                throw new InvalidDataException("A morphologizer's labels must start with 'POS='.");
            }
        }

        var head = new Morphologizer(encoder, name) { OwnsEncoder = true };
        head.Build(labels, new Generator(0));
        return head;
    }

    private static string Join(string pos, string morph)
    {
        var sb = new StringBuilder(PosPrefix, 32).Append(pos);
        if (morph.Length > 0)
        {
            sb.Append('|').Append(morph);
        }

        return sb.ToString();
    }

    private void Build(string[] labels, Generator generator)
    {
        _labels = labels;
        _labelIds = new Dictionary<string, int>(labels.Length, StringComparer.Ordinal);
        _parts = new (string, string)[labels.Length];
        for (int i = 0; i < labels.Length; i++)
        {
            _labelIds[labels[i]] = i;
            string rest = labels[i][PosPrefix.Length..];
            int bar = rest.IndexOf('|', StringComparison.Ordinal);
            _parts[i] = bar < 0 ? (rest, string.Empty) : (rest[..bar], rest[(bar + 1)..]);
        }

        _module = new LabelModule(Encoder.Width, labels.Length, generator);
    }

    private sealed class LabelModule : NnModule
    {
        private readonly Linear _output;

        public LabelModule(int width, int labels, Generator generator) =>
            _output = RegisterModule("output", new Linear(width, labels, generator));

        public override Tensor Forward(Tensor input) => _output.Forward(input);
    }
}
