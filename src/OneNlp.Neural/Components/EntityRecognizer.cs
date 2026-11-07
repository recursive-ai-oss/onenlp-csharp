using System.Text.Json;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural.Encoders;

namespace RecursiveAI.OneNlp.Neural.Components;

/// <summary>How an <see cref="EntityRecognizer"/> turns scores into entities.</summary>
public enum NerDecoding
{
    /// <summary>
    /// One softmax over BILUO tags per token (Lample et al. 2016 §2.1 use the same tag scheme), decoded with a Viterbi search that
    /// allows only well-formed tag sequences. The search needs no learned transition scores: the constraint is the structure.
    /// </summary>
    Tagging = 0,

    /// <summary>
    /// Every span up to <see cref="EntityRecognizer.MaxSpanLength"/> tokens is classified from its two end vectors and its width
    /// (a "none" class stands for no entity); the predicted spans are chosen greedily by probability with no overlap.
    /// </summary>
    Spans = 1,
}

/// <summary>
/// A named entity recognizer: reads each token's encoder vector together with its neighbours, scores entity labels, and writes the
/// entities (a label and a confidence each) into <see cref="Doc.Ents"/>. The label set is whatever the training examples contain, so
/// it serves the UNER scheme (PER, ORG, LOC) and any custom set equally.
/// </summary>
/// <remarks>
/// <para>
/// <b>Context.</b> The head gathers <see cref="Context"/> vectors on each side of a token, joins them, and passes them through one
/// hidden layer (a window classifier, Collobert et al. 2011 §3.3.1). An encoder that already mixes neighbours makes this
/// redundant but harmless; with a per-token encoder it is the only place context enters.
/// </para>
/// <para>
/// <b>Existing entities.</b> Entities already in the document (for example from <c>entity_ruler</c> placed before this component)
/// are kept as they are, and the recognizer predicts only around them. Rules placed after the recognizer follow
/// <c>EntityRuler.OverwriteEnts</c>.
/// </para>
/// <para>
/// <b>Training data.</b> A reference document counts only when its entities are set (even to none); a document with an entity whose
/// edges are not predicted-token boundaries is left out of the loss. The confidence of an entity is the mean probability of its tokens'
/// tags (<see cref="NerDecoding.Tagging"/>) or the probability of its span class (<see cref="NerDecoding.Spans"/>).
/// </para>
/// </remarks>
public sealed class EntityRecognizer : TrainableHead
{
    /// <summary>The kind name used by <see cref="HeadRegistry"/>: <c>ner</c>.</summary>
    public const string ComponentKind = "ner";

    private const int IgnoreIndex = -100;
    private const int MaxLabels = 4096;
    private const int MaxContext = 8;
    private const int MaxHidden = 4096;
    private const int MaxSpan = 64;
    private const int WidthEmbedding = 16;
    private string[] _labels = [];
    private Dictionary<string, int> _labelIds = new(StringComparer.Ordinal);
    private NerModule? _module;
    private float _minConfidence;

    /// <summary>Creates an uninitialized recognizer.</summary>
    /// <param name="encoder">The encoder the recognizer reads.</param>
    /// <param name="decoding">How scores become entities. Default <see cref="NerDecoding.Tagging"/>.</param>
    /// <param name="context">How many neighbouring vectors on each side join a token's own, from 0 to 8. Default 2.</param>
    /// <param name="hidden">Width of the hidden layer, from 1 to 4096. Default 128.</param>
    /// <param name="maxSpanLength">For <see cref="NerDecoding.Spans"/>, the longest entity in tokens, from 1 to 64. Longer gold entities are not learned. Default 8.</param>
    /// <param name="name">The component name, unique in its pipeline. Default <c>ner</c>.</param>
    /// <exception cref="ArgumentOutOfRangeException">A setting is outside its range.</exception>
    public EntityRecognizer(IEncoder encoder, NerDecoding decoding = NerDecoding.Tagging, int context = 2, int hidden = 128, int maxSpanLength = 8, string name = "ner")
        : base(name, encoder)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(context);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(context, MaxContext);
        ArgumentOutOfRangeException.ThrowIfLessThan(hidden, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(hidden, MaxHidden);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxSpanLength, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxSpanLength, MaxSpan);
        if (!Enum.IsDefined(decoding))
        {
            throw new ArgumentOutOfRangeException(nameof(decoding), decoding, "Unknown decoding.");
        }

        Decoding = decoding;
        Context = context;
        Hidden = hidden;
        MaxSpanLength = maxSpanLength;
    }

    /// <summary>
    /// Entities whose confidence is below this value are dropped after decoding, trading recall for precision. From 0 to 1;
    /// default 0 keeps every decoded entity and leaves existing models' output unchanged. It is saved with the model when above 0.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside 0 to 1 or not a number.</exception>
    public float MinConfidence
    {
        get => _minConfidence;
        set
        {
            if (!(value >= 0f && value <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "MinConfidence must be from 0 to 1.");
            }

            _minConfidence = value;
        }
    }

    /// <summary>How scores become entities.</summary>
    public NerDecoding Decoding { get; }

    /// <summary>Neighbouring vectors on each side of a token that the head reads.</summary>
    public int Context { get; }

    /// <summary>Width of the hidden layer.</summary>
    public int Hidden { get; }

    /// <summary>For <see cref="NerDecoding.Spans"/>, the longest entity in tokens.</summary>
    public int MaxSpanLength { get; }

    /// <summary>The entity labels, in output order; empty until the recognizer is initialized.</summary>
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
            foreach (Span entity in example.Reference.Ents)
            {
                seen.Add(entity.Label);
            }
        }

        seen.Remove(string.Empty);
        if (seen.Count == 0)
        {
            throw new ArgumentException($"The training examples hold no gold entities for the component '{Name}'.", nameof(examples));
        }

        if (seen.Count > MaxLabels)
        {
            throw new ArgumentException($"The training examples hold more than {MaxLabels} entity labels.", nameof(examples));
        }

        Build([.. seen], new Generator(seed));
    }

    /// <summary>
    /// Appends the entity labels of <paramref name="examples"/> that this recognizer lacks, keeping every weight it has (spaCy's
    /// "add a label, then train"). New labels follow the existing ones in sorted order, so the old outputs keep their positions; the
    /// new outputs start from fresh random weights drawn from <paramref name="seed"/>. <c>Train</c> calls this for a recognizer that
    /// already has weights.
    /// </summary>
    /// <param name="examples">The training examples.</param>
    /// <param name="seed">Seed of the new outputs' initial weights.</param>
    /// <returns>The labels added, in output order; empty when there were none.</returns>
    /// <exception cref="InvalidOperationException">The recognizer has no weights yet.</exception>
    /// <exception cref="ArgumentException">The label set would exceed 4096 labels.</exception>
    public override IReadOnlyList<string> ExtendLabels(IReadOnlyList<Example> examples, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(examples);
        NerModule old = _module ?? throw new InvalidOperationException($"The component '{Name}' is not initialized.");
        var added = new SortedSet<string>(StringComparer.Ordinal);
        foreach (Example example in examples)
        {
            foreach (Span entity in example.Reference.Ents)
            {
                if (entity.Label.Length > 0 && !_labelIds.ContainsKey(entity.Label))
                {
                    added.Add(entity.Label);
                }
            }
        }

        if (added.Count == 0)
        {
            return [];
        }

        if (_labels.Length + added.Count > MaxLabels)
        {
            throw new ArgumentException($"The recognizer would hold more than {MaxLabels} entity labels.", nameof(examples));
        }

        // New output rows come last (tag layout and span classes both index labels in order), so every old row keeps its place.
        Dictionary<string, Tensor> before = old.NamedParameters().ToDictionary(static p => p.Name, static p => p.Tensor, StringComparer.Ordinal);
        Build([.. _labels, .. added], new Generator(seed));
        foreach ((string name, Tensor target) in _module!.NamedParameters())
        {
            if (before.TryGetValue(name, out Tensor source))
            {
                // Same trailing shape, fewer or equal leading rows: the leading rows are the old weights.
                source.Data<float>().CopyTo(target.MutableData<float>());
            }
        }

        old.Dispose();
        return [.. added];
    }

    /// <inheritdoc/>
    public override Tensor Loss(EncodedBatch encoded, IReadOnlyList<Example> examples)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        ArgumentNullException.ThrowIfNull(examples);
        NerModule module = _module ?? throw new InvalidOperationException($"The component '{Name}' is not initialized.");
        if (examples.Count != encoded.DocCount)
        {
            throw new ArgumentException("The batch has a different number of documents than examples.", nameof(examples));
        }

        // Gold entities per document, or null when the document teaches nothing.
        var gold = new List<SpanSpec>?[examples.Count];
        for (int i = 0; i < examples.Count; i++)
        {
            gold[i] = GoldEntities(examples[i], encoded.Length(i));
        }

        Tensor hidden = module.Hidden(encoded.Vectors, encoded);
        if (Decoding == NerDecoding.Tagging)
        {
            var targets = new int[encoded.TokenCount];
            int counted = 0;
            for (int d = 0; d < gold.Length; d++)
            {
                int start = encoded.Start(d);
                int length = encoded.Length(d);
                if (gold[d] is not { } entities)
                {
                    Array.Fill(targets, IgnoreIndex, start, length);
                    continue;
                }

                Array.Fill(targets, 0, start, length);
                foreach (SpanSpec e in entities)
                {
                    int k = _labelIds[e.Label];
                    if (e.End - e.Start == 1)
                    {
                        targets[start + e.Start] = TagOf(k, Unit);
                        continue;
                    }

                    targets[start + e.Start] = TagOf(k, Begin);
                    for (int t = e.Start + 1; t < e.End - 1; t++)
                    {
                        targets[start + t] = TagOf(k, Inside);
                    }

                    targets[start + e.End - 1] = TagOf(k, Last);
                }

                counted += length;
            }

            return Losses.CrossEntropy(module.TagScores(hidden), Track(targets), 0f, IgnoreIndex, counted > 0);
        }

        // Spans: every span of up to MaxSpanLength tokens is a candidate; gold spans are the positives.
        var spanTargets = new List<int>();
        var begins = new List<int>();
        var ends = new List<int>();
        var widths = new List<int>();
        for (int d = 0; d < gold.Length; d++)
        {
            int start = encoded.Start(d);
            int length = encoded.Length(d);
            Dictionary<(int, int), int>? positives = null;
            if (gold[d] is { } entities)
            {
                positives = [];
                foreach (SpanSpec e in entities)
                {
                    positives[(e.Start, e.End - 1)] = 1 + _labelIds[e.Label];
                }
            }

            for (int i = 0; i < length; i++)
            {
                for (int j = i; j < length && j - i < MaxSpanLength; j++)
                {
                    begins.Add(start + i);
                    ends.Add(start + j);
                    widths.Add(j - i);
                    spanTargets.Add(positives is null ? IgnoreIndex : (positives.TryGetValue((i, j), out int c) ? c : 0));
                }
            }
        }

        int counts = spanTargets.Count(static t => t != IgnoreIndex);
        Tensor logits = module.SpanScores(hidden, Track([.. begins]), Track([.. ends]), Track([.. widths]));
        return Losses.CrossEntropy(logits, Track([.. spanTargets]), 0f, IgnoreIndex, counts > 0);
    }

    /// <inheritdoc/>
    public override void Predict(EncodedBatch encoded, ReadOnlySpan<Doc> docs)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        NerModule module = _module ?? throw new InvalidOperationException($"The component '{Name}' is not initialized.");
        Tensor hidden = module.Hidden(encoded.Vectors, encoded);
        if (Decoding == NerDecoding.Tagging)
        {
            ReadOnlySpan<float> p = Ops.Softmax(module.TagScores(hidden)).Data<float>();
            int tags = TagCount;
            for (int d = 0; d < docs.Length; d++)
            {
                Doc doc = docs[d];
                bool[] blocked = Blocked(doc);
                SetEntities(doc, DecodeTags(p.Slice(encoded.Start(d) * tags, doc.Length * tags), doc.Length, blocked), blocked);
            }

            return;
        }

        var begins = new List<int>();
        var ends = new List<int>();
        var widths = new List<int>();
        for (int d = 0; d < docs.Length; d++)
        {
            int start = encoded.Start(d);
            int length = docs[d].Length;
            for (int i = 0; i < length; i++)
            {
                for (int j = i; j < length && j - i < MaxSpanLength; j++)
                {
                    begins.Add(start + i);
                    ends.Add(start + j);
                    widths.Add(j - i);
                }
            }
        }

        ReadOnlySpan<float> probabilities = Ops.Softmax(module.SpanScores(hidden, Track([.. begins]), Track([.. ends]), Track([.. widths]))).Data<float>();
        int classes = _labels.Length + 1;
        int row = 0;
        for (int d = 0; d < docs.Length; d++)
        {
            Doc doc = docs[d];
            bool[] blocked = Blocked(doc);
            var candidates = new List<(int Start, int End, int Label, float P)>();
            for (int i = 0; i < doc.Length; i++)
            {
                for (int j = i; j < doc.Length && j - i < MaxSpanLength; j++)
                {
                    ReadOnlySpan<float> r = probabilities.Slice(row++ * classes, classes);
                    int best = 0;
                    for (int c = 1; c < classes; c++)
                    {
                        if (r[c] > r[best])
                        {
                            best = c;
                        }
                    }

                    if (best != 0 && !AnyBlocked(blocked, i, j + 1))
                    {
                        candidates.Add((i, j + 1, best - 1, r[best]));
                    }
                }
            }

            // Highest probability first; ties go to the longer, then the earlier span.
            candidates.Sort(static (a, b) =>
            {
                int c = b.P.CompareTo(a.P);
                if (c != 0)
                {
                    return c;
                }

                c = (b.End - b.Start).CompareTo(a.End - a.Start);
                return c != 0 ? c : a.Start.CompareTo(b.Start);
            });
            var taken = new bool[doc.Length];
            var chosen = new List<SpanSpec>();
            foreach ((int s, int e, int label, float p) in candidates)
            {
                if (AnyBlocked(taken, s, e))
                {
                    continue;
                }

                Array.Fill(taken, true, s, e - s);
                chosen.Add(new SpanSpec(s, e, _labels[label], Math.Clamp(p, 0f, 1f)));
            }

            SetEntities(doc, chosen, blocked);
        }
    }

    /// <inheritdoc/>
    public override void WriteConfig(Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteString("decoding", Decoding.ToString());
        writer.WriteNumber("context", Context);
        writer.WriteNumber("hidden", Hidden);
        writer.WriteNumber("max_span_length", MaxSpanLength);
        if (_minConfidence > 0f)
        {
            writer.WriteNumber("min_confidence", _minConfidence);
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
        NerDecoding decoding = JsonSettings.String(config, "decoding", 16) switch
        {
            "Tagging" => NerDecoding.Tagging,
            "Spans" => NerDecoding.Spans,
            _ => throw new InvalidDataException("A ner head's 'decoding' must be Tagging or Spans."),
        };
        string[] labels = JsonSettings.Strings(config, "labels", MaxLabels, 4096);
        if (labels.Length == 0 || labels.Distinct(StringComparer.Ordinal).Count() != labels.Length)
        {
            throw new InvalidDataException("A ner head's 'labels' must be a non-empty list without duplicates.");
        }

        var head = new EntityRecognizer(
            encoder,
            decoding,
            JsonSettings.Int(config, "context", 0, MaxContext),
            JsonSettings.Int(config, "hidden", 1, MaxHidden),
            JsonSettings.Int(config, "max_span_length", 1, MaxSpan),
            name)
        { OwnsEncoder = true };
        if (config.TryGetProperty("min_confidence", out JsonElement minConfidence))
        {
            if (minConfidence.ValueKind != JsonValueKind.Number || !minConfidence.TryGetSingle(out float threshold) || !(threshold >= 0f && threshold <= 1f))
            {
                throw new InvalidDataException("A ner head's 'min_confidence' must be a number from 0 to 1.");
            }

            head.MinConfidence = threshold;
        }

        head.Build(labels, new Generator(0));
        return head;
    }

    // Tag layout: 0 is "outside"; label k owns 1 + 4k .. 4 + 4k as begin, inside, last, unit (BILUO).
    private const int Begin = 0;
    private const int Inside = 1;
    private const int Last = 2;
    private const int Unit = 3;

    private int TagCount => (4 * _labels.Length) + 1;

    private static int TagOf(int label, int part) => 1 + (4 * label) + part;

    private static Tensor Track(int[] values)
    {
        Tensor t = Tensor.FromSpan<int>(values, Shape.Create(values.Length));
        ActivationScope.Track(t);
        return t;
    }

    private static bool AnyBlocked(bool[] flags, int start, int end)
    {
        for (int i = start; i < end; i++)
        {
            if (flags[i])
            {
                return true;
            }
        }

        return false;
    }

    // Tokens inside entities that were set before this component ran: they stay as they are.
    private static bool[] Blocked(Doc doc)
    {
        var blocked = new bool[doc.Length];
        foreach (Span e in doc.Ents)
        {
            Array.Fill(blocked, true, e.Start, e.Length);
        }

        return blocked;
    }

    private void SetEntities(Doc doc, List<SpanSpec> found, bool[] blocked)
    {
        var all = new List<SpanSpec>(found.Count);
        foreach (SpanSpec f in found)
        {
            if (f.Confidence >= _minConfidence)
            {
                all.Add(f);
            }
        }

        if (blocked.Contains(true))
        {
            foreach (Span e in doc.Ents)
            {
                all.Add(new SpanSpec(e.Start, e.End, e.Label, e.Confidence));
            }
        }

        all.Sort(static (a, b) => a.Start.CompareTo(b.Start));
        doc.SetEntities([.. all]);
    }

    // The example's gold entities on the predicted tokens, or null when the example carries no usable entity annotation.
    private List<SpanSpec>? GoldEntities(Example example, int tokens)
    {
        Doc reference = example.Reference;
        if (reference.Length == 0 || reference[0].EntIob == EntIob.Unset || example.Predicted.Length != tokens)
        {
            return null;
        }

        var result = new List<SpanSpec>();
        foreach (SpanSpec? e in example.GetAlignedEntities())
        {
            if (e is not { } spec || !_labelIds.ContainsKey(spec.Label))
            {
                return null;
            }

            result.Add(spec);
        }

        return result;
    }

    // Viterbi over tag log-probabilities, allowing only well-formed BILUO sequences (and "outside" at blocked tokens).
    private List<SpanSpec> DecodeTags(ReadOnlySpan<float> probabilities, int length, bool[] blocked)
    {
        int tags = TagCount;
        const float Impossible = float.NegativeInfinity;
        var score = new float[length * tags];
        var back = new int[length * tags];
        for (int t = 0; t < length; t++)
        {
            // The best "closed" predecessor (outside, last, unit) is shared by every tag that may start something.
            int closedBest = -1;
            if (t > 0)
            {
                float best = Impossible;
                ReadOnlySpan<float> previous = score.AsSpan((t - 1) * tags, tags);
                for (int s = 0; s < tags; s++)
                {
                    if (IsClosed(s) && previous[s] > best)
                    {
                        best = previous[s];
                        closedBest = s;
                    }
                }
            }

            for (int s = 0; s < tags; s++)
            {
                float lp = MathF.Log(MathF.Max(probabilities[(t * tags) + s], 1e-9f));
                float from;
                int via;
                if (blocked[t] && s != 0)
                {
                    from = Impossible;
                    via = -1;
                }
                else if (s == 0 || PartOf(s) is Begin or Unit)
                {
                    via = closedBest;
                    from = t == 0 ? 0f : (closedBest < 0 ? Impossible : score[((t - 1) * tags) + closedBest]);
                }
                else
                {
                    // Inside or last of label k continues a begin or inside of the same label.
                    int k = LabelOf(s);
                    via = -1;
                    from = Impossible;
                    if (t > 0)
                    {
                        for (int part = Begin; part <= Inside; part++)
                        {
                            int p = TagOf(k, part);
                            float v = score[((t - 1) * tags) + p];
                            if (v > from)
                            {
                                from = v;
                                via = p;
                            }
                        }
                    }
                }

                score[(t * tags) + s] = from + lp;
                back[(t * tags) + s] = via;
            }
        }

        // A sequence must end closed.
        int last = -1;
        float lastScore = Impossible;
        for (int s = 0; s < tags; s++)
        {
            float v = score[((length - 1) * tags) + s];
            if (IsClosed(s) && v > lastScore)
            {
                lastScore = v;
                last = s;
            }
        }

        var path = new int[length];
        for (int t = length - 1, s = last; t >= 0; t--)
        {
            path[t] = s;
            s = back[(t * tags) + s];
        }

        var found = new List<SpanSpec>();
        int begin = -1;
        float sum = 0;
        for (int t = 0; t < length; t++)
        {
            int s = path[t];
            if (s == 0)
            {
                continue;
            }

            float p = probabilities[(t * tags) + s];
            int part = PartOf(s);
            if (part is Begin or Unit)
            {
                begin = t;
                sum = 0;
            }

            sum += p;
            if (part is Last or Unit)
            {
                found.Add(new SpanSpec(begin, t + 1, _labels[LabelOf(s)], Math.Clamp(sum / (t + 1 - begin), 0f, 1f)));
            }
        }

        return found;
    }

    private static bool IsClosed(int tag) => tag == 0 || PartOf(tag) is Last or Unit;

    private static int PartOf(int tag) => (tag - 1) & 3;

    private static int LabelOf(int tag) => (tag - 1) >> 2;

    private void Build(string[] labels, Generator generator)
    {
        _labels = labels;
        _labelIds = new Dictionary<string, int>(labels.Length, StringComparer.Ordinal);
        for (int i = 0; i < labels.Length; i++)
        {
            _labelIds[labels[i]] = i;
        }

        _module = new NerModule(Encoder.Width, Context, Hidden, Decoding == NerDecoding.Tagging ? TagCount : labels.Length + 1, Decoding, generator);
    }

    private sealed class NerModule : NnModule
    {
        private readonly int _width;
        private readonly int _context;
        private readonly Linear _window;
        private readonly Linear? _tagOutput;
        private readonly Embedding? _spanWidth;
        private readonly Linear? _spanHidden;
        private readonly Linear? _spanOutput;

        public NerModule(int width, int context, int hidden, int outputs, NerDecoding decoding, Generator generator)
        {
            _width = width;
            _context = context;
            _window = RegisterModule("window", new Linear(((2 * context) + 1) * width, hidden, generator));
            if (decoding == NerDecoding.Tagging)
            {
                _tagOutput = RegisterModule("tags", new Linear(hidden, outputs, generator));
            }
            else
            {
                _spanWidth = RegisterModule("span_width", new Embedding(MaxSpan, WidthEmbedding, generator));
                _spanHidden = RegisterModule("span_hidden", new Linear((2 * hidden) + WidthEmbedding, hidden, generator));
                _spanOutput = RegisterModule("span_classes", new Linear(hidden, outputs, generator));
            }
        }

        public override Tensor Forward(Tensor input) => throw new NotSupportedException("Use Hidden, TagScores or SpanScores.");

        // Joins each token's vector with its neighbours' (zeros beyond the document's edges), then one ReLU layer. [N, hidden].
        public Tensor Hidden(Tensor vectors, EncodedBatch batch)
        {
            int n = batch.TokenCount;
            if (_context == 0 || n == 0)
            {
                return Ops.Relu(_window.Forward(vectors));
            }

            int taps = (2 * _context) + 1;
            var index = new int[n * taps];
            for (int d = 0; d < batch.DocCount; d++)
            {
                int start = batch.Start(d);
                int end = start + batch.Length(d);
                for (int t = start; t < end; t++)
                {
                    for (int o = 0; o < taps; o++)
                    {
                        int source = t + o - _context;
                        index[(t * taps) + o] = source >= start && source < end ? source : n;
                    }
                }
            }

            Tensor zeros = Tensor.Zeros(DType.F32, Shape.Create(1, _width));
            ActivationScope.Track(zeros);
            Tensor padded = Ops.Concat([vectors, zeros], 0);
            Tensor gathered = Ops.Embedding(padded, Track(index));
            return Ops.Relu(_window.Forward(gathered.Reshape(n, taps * _width)));
        }

        public Tensor TagScores(Tensor hidden) => _tagOutput!.Forward(hidden);

        public Tensor SpanScores(Tensor hidden, Tensor begins, Tensor ends, Tensor widths)
        {
            Tensor first = Ops.Embedding(hidden, begins);
            Tensor second = Ops.Embedding(hidden, ends);
            Tensor width = _spanWidth!.Forward(widths);
            Tensor joined = Ops.Concat([first, second, width], 1);
            return _spanOutput!.Forward(Ops.Relu(_spanHidden!.Forward(joined)));
        }
    }
}
