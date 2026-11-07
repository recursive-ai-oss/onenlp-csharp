using System.Buffers;
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
public sealed class EditTreeLemmatizer : Lemmatizer
{
    /// <summary>The kind name used by <see cref="HeadRegistry"/>: <c>lemmatizer</c>.</summary>
    public const string ComponentKind = "lemmatizer";

    private const int IgnoreIndex = -100;
    private const int MaxLabels = 1 << 18;
    private const long MaxOutputValues = 1L << 26;
    private const int Candidates = 16;
    private const int LowerBuffer = 128;
    private string[] _labels = [];
    private EditTree[] _trees = [];
    private bool[] _lower = [];
    private Dictionary<string, int> _labelIds = new(StringComparer.Ordinal);
    private FactoredOutput? _module;
    private readonly int _minClassCount;
    private readonly int _headClasses;

    /// <summary>Creates an uninitialized lemmatizer.</summary>
    /// <param name="encoder">The encoder it reads.</param>
    /// <param name="name">The component name, unique in its pipeline. Default <c>lemmatizer</c>.</param>
    /// <param name="minClassCount">Classes seen fewer times than this in the training examples are left out of the label set (their tokens leave the loss), 1 to 1000. Most classes of a treebank are substitutions that fit one word form and were seen once; the output layer's size, and so the time of every prediction, grows with the number of classes, and a class seen once says little about words not seen. Default 1 (keep every class).</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minClassCount"/> is out of range.</exception>
    /// <param name="headClasses">With a value above 0 the output layer is two-level (see <see cref="FactoredOutput"/>): the most frequent <paramref name="headClasses"/> classes (copying the form and the few endings that cover nearly every token) are scored for every token and the rest only for tokens none of them suits. Default 0 (every class is scored for every token).</param>
    public EditTreeLemmatizer(IEncoder encoder, string name = "lemmatizer", int minClassCount = 1, int headClasses = 0)
        : base(name, encoder)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(headClasses);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(headClasses, 4096);
        _headClasses = headClasses;
        ArgumentOutOfRangeException.ThrowIfLessThan(minClassCount, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minClassCount, 1000);
        _minClassCount = minClassCount;
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

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Example example in examples)
        {
            Doc reference = example.Reference;
            ReadOnlySpan<int> lemmas = reference.GetColumn(TokenColumn.Lemma);
            for (int t = 0; t < lemmas.Length; t++)
            {
                if (lemmas[t] != 0)
                {
                    string label = Label(reference[t].ToString(), reference.Strings[lemmas[t]]);
                    counts[label] = counts.GetValueOrDefault(label) + 1;
                }
            }
        }

        // Falling frequency when the output layer is two-level (its first classes are the frequent ones), alphabetical otherwise.
        IEnumerable<KeyValuePair<string, int>> kept = counts.Where(kv => kv.Value >= _minClassCount);
        string[] seen = _headClasses > 0
            ? [.. kept.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key)]
            : [.. kept.Select(kv => kv.Key).Order(StringComparer.Ordinal)];

        if (seen.Length == 0)
        {
            throw new ArgumentException($"The training examples hold no gold lemmas for the component '{Name}'.", nameof(examples));
        }

        if (seen.Length > MaxLabels)
        {
            throw new ArgumentException($"The training examples hold more than {MaxLabels} distinct edit trees.", nameof(examples));
        }

        Build(seen, new Generator(seed));
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The examples hold edit trees this lemmatizer's class set lacks, seen at least <c>minClassCount</c> times. The class set is fixed once the lemmatizer has weights.</exception>
    public override IReadOnlyList<string> ExtendLabels(IReadOnlyList<Example> examples, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(examples);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Example example in examples)
        {
            Doc reference = example.Reference;
            ReadOnlySpan<int> lemmas = reference.GetColumn(TokenColumn.Lemma);
            for (int t = 0; t < lemmas.Length; t++)
            {
                if (lemmas[t] != 0)
                {
                    string label = Label(reference[t].ToString(), reference.Strings[lemmas[t]]);
                    if (!_labelIds.ContainsKey(label))
                    {
                        counts[label] = counts.GetValueOrDefault(label) + 1;
                    }
                }
            }
        }

        // Trees below the minimum count are left out exactly as at initialization, so resuming a run does not trip on them.
        string[] unknown = [.. counts.Where(kv => kv.Value >= _minClassCount).Select(kv => kv.Key).Order(StringComparer.Ordinal)];
        if (unknown.Length > 0)
        {
            throw new ArgumentException(
                $"The training examples hold {unknown.Length} edit trees that the component '{Name}' does not have, for example: {string.Join(", ", unknown.Take(5))}. "
                + "A lemmatizer's classes are fixed once it has weights; train a new lemmatizer on this data.",
                nameof(examples));
        }

        return [];
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

        return module.Loss(encoded.Vectors, targets, IgnoreIndex, counted);
    }

    /// <inheritdoc/>
    public override void Predict(EncodedBatch encoded, ReadOnlySpan<Doc> docs)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        FactoredOutput module = _module ?? throw new InvalidOperationException($"The component '{Name}' is not initialized.");
        int classes = _labels.Length;
        int rows = encoded.TokenCount;
        int k = module.HeadClasses;
        bool factored = module.IsFactored;

        // Scores of every row from the first level (the whole layer when it is not two-level); a two-level layer's second level is
        // scored only for the rows where "other" wins, and tailAt maps a row to its position among them. All without tensor operations.
        RowClassifier firstLevel = factored ? module.HeadFast : module.FlatFast;
        RowClassifier? tailLevel = factored ? module.TailFast : null;
        int firstWidth = firstLevel.Stride;
        int tailStride = tailLevel?.Stride ?? 0;
        int tailWidth = classes - k;
        ReadOnlySpan<float> vectors = encoded.Vectors.Data<float>();
        float[] first = ArrayPool<float>.Shared.Rent(rows * firstWidth);
        int[]? tailAt = factored ? ArrayPool<int>.Shared.Rent(rows) : null;
        float[]? second = null;
        int tailCount = 0;
        try
        {
            int inputs = firstLevel.Inputs;
            for (int r = 0; r < rows; r++)
            {
                ReadOnlySpan<float> x = vectors.Slice(r * inputs, inputs);
                Span<float> row = first.AsSpan(r * firstWidth, firstWidth);
                firstLevel.Logits(x, row);
                if (tailLevel is null)
                {
                    continue;
                }

                (int best, _) = RowKernels.ArgMax(row, firstLevel.Classes);
                if (best != k)
                {
                    tailAt![r] = -1;
                    continue;
                }

                tailAt![r] = tailCount;
                if (second is null || (tailCount + 1) * tailStride > second.Length)
                {
                    float[] grown = ArrayPool<float>.Shared.Rent(Math.Max(16, tailCount * 2) * tailStride);
                    second?.AsSpan(0, tailCount * tailStride).CopyTo(grown);
                    if (second is not null)
                    {
                        ArrayPool<float>.Shared.Return(second);
                    }

                    second = grown;
                }

                tailLevel.Logits(x, second.AsSpan(tailCount * tailStride, tailStride));
                tailCount++;
            }

            // An encoder that reads the words of multiword tokens (French du = de + le) has the lemmatizer read them too: the edit tree of the
            // word "le" is applied to "le", not to the piece "u".
            bool wordForms = Encoder is IWordFormEncoder { ReadsWordForms: true };
            Span<int> tried = stackalloc int[2 * Candidates]; // bounded by the constant above
            Span<char> lowered = stackalloc char[LowerBuffer]; // forms up to LowerBuffer characters are lower-cased here; longer ones get a string
            for (int d = 0; d < docs.Length; d++)
            {
                Doc doc = docs[d];
                int start = encoded.Start(d);
                var lemmas = new int[doc.Length];
                for (int t = 0; t < doc.Length; t++)
                {
                    int r = start + t;
                    string norm = wordForms ? doc[t].Norm : string.Empty;
                    ReadOnlySpan<char> form = norm.Length > 0 ? norm : doc[t].TextSpan;
                    ReadOnlySpan<char> lower = lowered[..0];
                    if (form.Length <= LowerBuffer)
                    {
                        form.ToLowerInvariant(lowered);
                        lower = lowered[..form.Length];
                    }
                    else
                    {
                        lower = form.ToString().ToLowerInvariant();
                    }

                    ReadOnlySpan<float> firstRow = first.AsSpan(r * firstWidth, firstWidth);
                    string? lemma = null;
                    int used = 0;

                    // Candidates in order of score: the best classes of the level that won, then (for "other") the first level's own.
                    // The best class alone fits almost every token, so the loops usually end at once.
                    if (factored && tailAt![r] >= 0)
                    {
                        lemma = TryRanked(second.AsSpan(tailAt[r] * tailStride, tailStride), tailWidth, k, tried, ref used, form, lower);
                    }

                    if (lemma is null)
                    {
                        lemma = TryRanked(firstRow, factored ? k : classes, 0, tried, ref used, form, lower);
                    }

                    lemmas[t] = doc.Strings.Add(lemma ?? form.ToString());
                }

                doc.SetColumn(TokenColumn.Lemma, lemmas);
            }
        }
        finally
        {
            ArrayPool<float>.Shared.Return(first);
            if (second is not null)
            {
                ArrayPool<float>.Shared.Return(second);
            }

            if (tailAt is not null)
            {
                ArrayPool<int>.Shared.Return(tailAt);
            }
        }
    }

    // Tries the classes of one level from the highest score down, at most Candidates of them, skipping ones already tried; the first
    // tree that fits the form gives the lemma.
    private string? TryRanked(ReadOnlySpan<float> scores, int count, int offset, Span<int> tried, ref int used, ReadOnlySpan<char> form, ReadOnlySpan<char> lower)
    {
        for (int attempt = 0; attempt < Math.Min(Candidates, count); attempt++)
        {
            int best = -1;
            float top = float.NegativeInfinity;
            ReadOnlySpan<int> skip = tried[..used];
            for (int c = 0; c < count; c++)
            {
                float v = scores[c];
                if (v > top && (used == 0 || !skip.Contains(offset + c)))
                {
                    top = v;
                    best = c;
                }
            }

            if (best < 0)
            {
                return null;
            }

            int cls = offset + best;
            tried[used++] = cls;
            string? lemma = _trees[cls].Apply(_lower[cls] ? lower : form);
            if (lemma is not null)
            {
                return lemma;
            }
        }

        return null;
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
        string[] labels = JsonSettings.Strings(config, "labels", MaxLabels, EditTree.MaxSerializedLength + 1);
        if (labels.Length == 0 || labels.Distinct(StringComparer.Ordinal).Count() != labels.Length)
        {
            throw new InvalidDataException("A lemmatizer's 'labels' must be a non-empty list without duplicates.");
        }

        var head = new EditTreeLemmatizer(encoder, name, 1, JsonSettings.OptionalInt(config, "headClasses", 0, 4096, 0)) { OwnsEncoder = true };
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

        _module = new FactoredOutput(Encoder.Width, labels.Length, _headClasses, generator);
    }
}
