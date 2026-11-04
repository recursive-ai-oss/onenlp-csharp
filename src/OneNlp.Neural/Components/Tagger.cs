using System.Text.Json;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural.Encoders;

namespace RecursiveAI.OneNlp.Neural.Components;

/// <summary>
/// A softmax tagger: one linear layer over each token's encoder vector, a softmax over the label set, trained with cross entropy.
/// It writes the most likely label of every token into one string column, with the label's probability as the token's confidence.
/// </summary>
/// <remarks>
/// <para>
/// This is the smallest complete head and the template for the others. The context a real tagger needs comes from the encoder
/// (a window or transformer encoder gives each vector its neighbours); the head itself is the classifier of Collobert et al. 2011
/// §3, applied token by token. Which column it fills is a setting: <see cref="TokenColumn.Tag"/> (the default), <see cref="TokenColumn.Pos"/>
/// or <see cref="TokenColumn.Morph"/> (the whole feature string is one label).
/// </para>
/// <para>
/// The label set is every value of that column in the training examples' reference documents, in ordinal order. Training tokens
/// with no gold value, or whose gold value comes from several aligned reference tokens, are left out of the loss
/// (<see cref="Example.GetAligned"/>).
/// </para>
/// </remarks>
public sealed class Tagger : TrainableHead
{
    /// <summary>The kind name used by <see cref="HeadRegistry"/>: <c>tagger</c>.</summary>
    public const string ComponentKind = "tagger";

    private const int IgnoreIndex = -100;
    private const int MaxLabels = 1 << 20;
    private const long MaxOutputValues = 1L << 26;
    private string[] _labels = [];
    private Dictionary<string, int> _labelIds = new(StringComparer.Ordinal);
    private TaggerModule? _module;

    /// <summary>Creates an uninitialized tagger.</summary>
    /// <param name="encoder">The encoder the tagger reads.</param>
    /// <param name="column">The column to fill: <see cref="TokenColumn.Tag"/>, <see cref="TokenColumn.Pos"/> or <see cref="TokenColumn.Morph"/>.</param>
    /// <param name="name">The component name, unique in its pipeline. Default <c>tagger</c>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="column"/> is not one of the three.</exception>
    public Tagger(IEncoder encoder, TokenColumn column = TokenColumn.Tag, string name = "tagger")
        : base(name, encoder)
    {
        if (column is not (TokenColumn.Tag or TokenColumn.Pos or TokenColumn.Morph))
        {
            throw new ArgumentOutOfRangeException(nameof(column), column, "A tagger fills Tag, Pos or Morph.");
        }

        Column = column;
    }

    /// <summary>The column the tagger fills.</summary>
    public TokenColumn Column { get; }

    /// <summary>The label set, in output order; empty until the tagger is initialized.</summary>
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
            foreach (int id in reference.GetColumn(Column))
            {
                if (id != 0)
                {
                    seen.Add(reference.Strings[id]);
                }
            }
        }

        if (seen.Count == 0)
        {
            throw new ArgumentException($"The training examples hold no gold {Column} values for the component '{Name}'.", nameof(examples));
        }

        if (seen.Count > MaxLabels)
        {
            throw new ArgumentException($"The training examples hold more than {MaxLabels} distinct {Column} values.", nameof(examples));
        }

        Build([.. seen], new Generator(seed));
    }

    /// <inheritdoc/>
    public override Tensor Loss(EncodedBatch encoded, IReadOnlyList<Example> examples)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        ArgumentNullException.ThrowIfNull(examples);
        TaggerModule module = _module ?? throw new InvalidOperationException($"The component '{Name}' is not initialized.");
        if (examples.Count != encoded.DocCount)
        {
            throw new ArgumentException("The batch has a different number of documents than examples.", nameof(examples));
        }

        var targets = new int[encoded.TokenCount];
        int counted = 0;
        int row = 0;
        for (int i = 0; i < examples.Count; i++)
        {
            string?[] gold = examples[i].GetAligned(Column);
            if (gold.Length != encoded.Length(i))
            {
                throw new ArgumentException($"Example {i} has {gold.Length} tokens but {encoded.Length(i)} encoder rows.", nameof(examples));
            }

            foreach (string? label in gold)
            {
                if (label is not null && _labelIds.TryGetValue(label, out int id))
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
        Tensor logits = module.Forward(encoded.Vectors);

        // With no counted token the mean is NaN; the sum is a zero that still depends on the weights.
        return Losses.CrossEntropy(logits, targetTensor, 0f, IgnoreIndex, counted > 0);
    }

    /// <inheritdoc/>
    public override void Predict(EncodedBatch encoded, ReadOnlySpan<Doc> docs)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        TaggerModule module = _module ?? throw new InvalidOperationException($"The component '{Name}' is not initialized.");
        Tensor probabilities = Ops.Softmax(module.Forward(encoded.Vectors));
        ReadOnlySpan<float> p = probabilities.Data<float>();
        int classes = _labels.Length;
        for (int d = 0; d < docs.Length; d++)
        {
            Doc doc = docs[d];
            int start = encoded.Start(d);
            var ids = new int[doc.Length];
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

                ids[t] = doc.Strings.Add(_labels[best]);
                confidence[t] = float.IsNaN(row[best]) ? 0f : Math.Clamp(row[best], 0f, 1f);
            }

            doc.SetColumn(Column, ids);
            doc.SetConfidences(confidence);
        }
    }

    /// <inheritdoc/>
    public override void WriteConfig(Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteString("column", Column.ToString());
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
        TokenColumn column = JsonSettings.String(config, "column", 16) switch
        {
            "Tag" => TokenColumn.Tag,
            "Pos" => TokenColumn.Pos,
            "Morph" => TokenColumn.Morph,
            _ => throw new InvalidDataException("A tagger's 'column' must be Tag, Pos or Morph."),
        };
        string[] labels = JsonSettings.Strings(config, "labels", MaxLabels, 4096);
        if (labels.Length == 0 || labels.Distinct(StringComparer.Ordinal).Count() != labels.Length)
        {
            throw new InvalidDataException("A tagger's 'labels' must be a non-empty list without duplicates.");
        }

        var tagger = new Tagger(encoder, column, name) { OwnsEncoder = true };
        tagger.Build(labels, new Generator(0));
        return tagger;
    }

    private void Build(string[] labels, Generator generator)
    {
        if ((long)labels.Length * Encoder.Width > MaxOutputValues)
        {
            throw new ArgumentException($"{labels.Length} labels over vectors of width {Encoder.Width} need more than {MaxOutputValues} weights.");
        }

        _labels = labels;
        _labelIds = new Dictionary<string, int>(labels.Length, StringComparer.Ordinal);
        for (int i = 0; i < labels.Length; i++)
        {
            _labelIds[labels[i]] = i;
        }

        _module = new TaggerModule(Encoder.Width, labels.Length, generator);
    }

    private sealed class TaggerModule : NnModule
    {
        private readonly Linear _output;

        public TaggerModule(int width, int labels, Generator generator) =>
            _output = RegisterModule("output", new Linear(width, labels, generator));

        // Input: token vectors [N, width]. Output: label scores [N, labels].
        public override Tensor Forward(Tensor input) => _output.Forward(input);
    }
}
