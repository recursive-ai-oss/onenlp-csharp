using System.Text.Json;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural.Encoders;

namespace RecursiveAI.OneNlp.Neural.Components;

/// <summary>
/// An edit-tree lemmatizer: for each token, a softmax over the edit trees (Müller et al. 2015) that turned a training form into its
/// lemma, applied to the token's text to give the lemma. It writes <see cref="TokenColumn.Lemma"/> and leaves the token's confidence alone.
/// </summary>
/// <remarks>
/// <para>
/// <b>Classes.</b> A class is an edit tree plus one bit: whether the form is lower-cased before the tree is applied. The bit is set when the
/// gold lemma has no upper-case letter, so "The" to "the" and "walking" to "walk" share a tree and a sentence-initial capital does not
/// multiply the classes; lemmas with capitals (proper nouns, "Mr.") use the form as written. The class set is every class in the
/// training examples' reference documents, in ordinal order of its text. A training token joins the loss when it lies in exactly one reference token with a lemma
/// and the class of its (form, lemma) pair is in the set.
/// </para>
/// <para>
/// <b>Prediction and backoff.</b> The token takes its most probable tree that fits its text. Looking at the best 16 classes finds one
/// for almost every token; when none fits, the lemma is the token's text itself (the "orth" backoff the spaCy
/// <c>trainable_lemmatizer</c> documents).
/// </para>
/// </remarks>
public sealed class EditTreeLemmatizer : TrainableHead
{
    /// <summary>The kind name used by <see cref="HeadRegistry"/>: <c>lemmatizer</c>.</summary>
    public const string ComponentKind = "lemmatizer";

    private const int IgnoreIndex = -100;
    private const int MaxLabels = 1 << 18;
    private const long MaxOutputValues = 1L << 26;
    private const int Candidates = 16;
    private string[] _labels = [];
    private EditTree[] _trees = [];
    private bool[] _lower = [];
    private Dictionary<string, int> _labelIds = new(StringComparer.Ordinal);
    private LabelModule? _module;

    /// <summary>Creates an uninitialized lemmatizer.</summary>
    /// <param name="encoder">The encoder it reads.</param>
    /// <param name="name">The component name, unique in its pipeline. Default <c>lemmatizer</c>.</param>
    public EditTreeLemmatizer(IEncoder encoder, string name = "lemmatizer")
        : base(name, encoder)
    {
    }

    /// <summary>The classes (a flag character, <c>l</c> or <c>r</c>, then the serialized tree), in output order; empty until initialized.</summary>
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
            ReadOnlySpan<int> lemmas = reference.GetColumn(TokenColumn.Lemma);
            for (int t = 0; t < lemmas.Length; t++)
            {
                if (lemmas[t] != 0)
                {
                    seen.Add(Label(reference[t].ToString(), reference.Strings[lemmas[t]]));
                }
            }
        }

        if (seen.Count == 0)
        {
            throw new ArgumentException($"The training examples hold no gold lemmas for the component '{Name}'.", nameof(examples));
        }

        if (seen.Count > MaxLabels)
        {
            throw new ArgumentException($"The training examples hold more than {MaxLabels} distinct edit trees.", nameof(examples));
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
            string?[] gold = examples[i].GetAligned(TokenColumn.Lemma);
            Doc predicted = examples[i].Predicted;
            if (gold.Length != encoded.Length(i))
            {
                throw new ArgumentException($"Example {i} has {gold.Length} tokens but {encoded.Length(i)} encoder rows.", nameof(examples));
            }

            for (int t = 0; t < gold.Length; t++)
            {
                if (gold[t] is { } lemma && _labelIds.TryGetValue(Label(predicted[t].ToString(), lemma), out int id))
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
        Tensor logitsTensor = module.Forward(encoded.Vectors);
        ReadOnlySpan<float> logits = logitsTensor.Data<float>();
        int classes = _labels.Length;
        Span<int> tried = stackalloc int[Candidates]; // bounded by the constant above
        for (int d = 0; d < docs.Length; d++)
        {
            Doc doc = docs[d];
            int start = encoded.Start(d);
            var lemmas = new int[doc.Length];
            for (int t = 0; t < doc.Length; t++)
            {
                ReadOnlySpan<float> row = logits.Slice((start + t) * classes, classes);
                string form = doc[t].ToString();
                string lowered = form.ToLowerInvariant();
                string? lemma = null;
                for (int k = 0; k < Math.Min(Candidates, classes) && lemma is null; k++)
                {
                    int best = -1;
                    for (int c = 0; c < classes; c++)
                    {
                        if ((best < 0 || row[c] > row[best]) && !tried[..k].Contains(c))
                        {
                            best = c;
                        }
                    }

                    tried[k] = best;
                    lemma = _trees[best].Apply(_lower[best] ? lowered : form);
                }

                lemmas[t] = doc.Strings.Add(lemma ?? form);
            }

            doc.SetColumn(TokenColumn.Lemma, lemmas);
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
        string[] labels = JsonSettings.Strings(config, "labels", MaxLabels, EditTree.MaxSerializedLength + 1);
        if (labels.Length == 0 || labels.Distinct(StringComparer.Ordinal).Count() != labels.Length)
        {
            throw new InvalidDataException("A lemmatizer's 'labels' must be a non-empty list without duplicates.");
        }

        var head = new EditTreeLemmatizer(encoder, name) { OwnsEncoder = true };
        head.Build(labels, new Generator(0));
        return head;
    }

    // The class of a (form, lemma) pair: flag, then the tree from the form (lower-cased when the flag is 'l') to the lemma.
    private static string Label(string form, string lemma)
    {
        string lowerLemma = lemma.ToLowerInvariant();
        bool lower = string.Equals(lemma, lowerLemma, StringComparison.Ordinal);
        EditTree tree = EditTree.Build(lower ? form.ToLowerInvariant() : form, lemma);
        return (lower ? "l" : "r") + tree.Serialize();
    }

    private void Build(string[] labels, Generator generator)
    {
        if ((long)labels.Length * Encoder.Width > MaxOutputValues)
        {
            throw new ArgumentException($"{labels.Length} classes over vectors of width {Encoder.Width} need more than {MaxOutputValues} weights.");
        }

        _labels = labels;
        _trees = new EditTree[labels.Length];
        _lower = new bool[labels.Length];
        _labelIds = new Dictionary<string, int>(labels.Length, StringComparer.Ordinal);
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i].Length < 2 || labels[i][0] is not ('l' or 'r'))
            {
                throw new InvalidDataException("A lemmatizer class must start with 'l' or 'r' and hold a tree.");
            }

            _lower[i] = labels[i][0] == 'l';
            _trees[i] = EditTree.Parse(labels[i][1..]);
            _labelIds[labels[i]] = i;
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
