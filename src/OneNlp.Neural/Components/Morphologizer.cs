using System.Buffers;
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
    private StringStore? _idStore;
    private (int Pos, int Morph)[]? _ids;
    private Dictionary<string, int> _labelIds = new(StringComparer.Ordinal);
    private (string Pos, string Morph)[] _parts = [];
    private FactoredOutput? _module;
    private readonly int _headClasses;

    /// <summary>Creates an uninitialized morphologizer.</summary>
    /// <param name="encoder">The encoder it reads.</param>
    /// <param name="name">The component name, unique in its pipeline. Default <c>morphologizer</c>.</param>
    /// <param name="headClasses">With a value above 0 the output layer is two-level (see <see cref="FactoredOutput"/>): the most frequent <paramref name="headClasses"/> labels are scored for every token and the rest only for tokens none of them suits, which cuts the work of the layer several-fold when a few labels cover nearly all tokens. 0 (the default) scores every label for every token.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="headClasses"/> is negative or above 4096.</exception>
    public Morphologizer(IEncoder encoder, string name = "morphologizer", int headClasses = 0)
        : base(name, encoder)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(headClasses);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(headClasses, 4096);
        _headClasses = headClasses;
    }

    /// <summary>The number of labels scored for every token (0 when every label is).</summary>
    public int HeadClasses => _headClasses;

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

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Example example in examples)
        {
            Doc reference = example.Reference;
            ReadOnlySpan<int> pos = reference.GetColumn(TokenColumn.Pos);
            ReadOnlySpan<int> morph = reference.GetColumn(TokenColumn.Morph);
            for (int t = 0; t < pos.Length; t++)
            {
                if (pos[t] != 0)
                {
                    string label = Join(reference.Strings[pos[t]], morph[t] == 0 ? string.Empty : reference.Strings[morph[t]]);
                    counts[label] = counts.GetValueOrDefault(label) + 1;
                }
            }
        }

        // Falling frequency when the output layer is two-level (its first classes are the frequent ones), alphabetical otherwise.
        IEnumerable<string> ordered = _headClasses > 0
            ? counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key)
            : counts.Keys.Order(StringComparer.Ordinal);
        string[] seen = [.. ordered];
        if (seen.Length == 0)
        {
            throw new ArgumentException($"The training examples hold no gold part-of-speech values for the component '{Name}'.", nameof(examples));
        }

        if (seen.Length > MaxLabels)
        {
            throw new ArgumentException($"The training examples hold more than {MaxLabels} distinct part-of-speech and feature combinations.", nameof(examples));
        }

        Build(seen, new Generator(seed));
    }

    /// <inheritdoc/>
    public override Tensor Loss(EncodedBatch encoded, IReadOnlyList<Example> examples)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        ArgumentNullException.ThrowIfNull(examples);
        FactoredOutput module = _module ?? throw new InvalidOperationException($"The component '{Name}' is not initialized.");
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

        return module.Loss(encoded.Vectors, targets, IgnoreIndex, counted);
    }

    /// <inheritdoc/>
    public override void Predict(EncodedBatch encoded, ReadOnlySpan<Doc> docs)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        FactoredOutput module = _module ?? throw new InvalidOperationException($"The component '{Name}' is not initialized.");
        int rows = encoded.TokenCount;
        int[] best = ArrayPool<int>.Shared.Rent(rows);
        float[] probability = ArrayPool<float>.Shared.Rent(rows);
        try
        {
            module.Decode(encoded.Vectors, best.AsSpan(0, rows), probability.AsSpan(0, rows));
            for (int d = 0; d < docs.Length; d++)
            {
                Doc doc = docs[d];
                int start = encoded.Start(d);
                var pos = new int[doc.Length];
                var morph = new int[doc.Length];
                var confidence = new float[doc.Length];
                for (int t = 0; t < doc.Length; t++)
                {
                    int label = best[start + t];
                    if (_ids is null || !ReferenceEquals(_idStore, doc.Strings))
                    {
                        // Label to string id: one table per string store, so a token costs an array read instead of two hash lookups.
                        _idStore = doc.Strings;
                        _ids = new (int Pos, int Morph)[_parts.Length];
                        Array.Fill(_ids, (-1, -1));
                    }

                    if (_ids[label].Pos < 0)
                    {
                        (string posValue, string morphValue) = _parts[label];
                        _ids[label] = (doc.Strings.Add(posValue), morphValue.Length == 0 ? 0 : doc.Strings.Add(morphValue));
                    }

                    (pos[t], morph[t]) = _ids[label];
                    confidence[t] = probability[start + t];
                }

                doc.SetColumn(TokenColumn.Pos, pos);
                doc.SetColumn(TokenColumn.Morph, morph);
                doc.SetConfidences(confidence);
            }
        }
        finally
        {
            ArrayPool<int>.Shared.Return(best);
            ArrayPool<float>.Shared.Return(probability);
        }
    }

    /// <inheritdoc/>
    public override void WriteConfig(Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (_module is { IsFactored: true })
        {
            writer.WriteNumber("headClasses", _module.HeadClasses);
        }

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

        var head = new Morphologizer(encoder, name, JsonSettings.OptionalInt(config, "headClasses", 0, 4096, 0)) { OwnsEncoder = true };
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

        _module = new FactoredOutput(Encoder.Width, labels.Length, _headClasses, generator);
    }
}
