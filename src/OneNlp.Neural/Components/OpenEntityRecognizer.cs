using System.Text.Json;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural.Encoders;

namespace RecursiveAI.OneNlp.Neural.Components;

/// <summary>
/// Named entity recognition with entity types named at run time: the caller passes the types to look for ("contract party",
/// "governing law") and the head scores every candidate span against each of them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Model.</b> After Zaratiana et al. 2023 (GLiNER, §3): a span is represented from its first and last token vectors and a width
/// embedding (§3.2, "span representation layer"); an entity type is represented by a vector in the same space; the score of a span
/// for a type is the dot product of the two and the probability is its sigmoid (§3.3, "span-type matching"); training is binary
/// cross-entropy over every span and every type of the example (§3.4). Spans are chosen greedily by probability with no overlap
/// (flat NER).
/// </para>
/// <para>
/// <b>What differs from the paper.</b> The paper reads the type names and the text in one input sequence. This head sits behind the
/// encoder seam, which encodes a document without a prompt, so a type is encoded on its own: its name is run through the same
/// encoder as a short document and its token vectors are averaged. The benefit is that type vectors are computed once per call
/// and not once per document. The cost is that types and text do not attend to each other inside the encoder.
/// </para>
/// <para>
/// <b>Training data.</b> The gold for an example is the span groups of <see cref="Example.Spans"/>: the group name is the type's name
/// and its spans are token ranges of the reference document. Entities set on the reference document count as groups too, named by
/// their label. A group with no spans says "this type does not occur here": it is a negative, and the model is trained against up to
/// <see cref="NegativeTypes"/> of an example's negatives, so it learns to say no. A type an example does not list is neither:
/// it teaches nothing, because a data set that labels "person" may not label "actor". Spans may overlap across types: a span can
/// be both "person" and "actor".
/// </para>
/// <para>
/// <b>Existing entities</b> of the document are kept as they are and the head predicts around them, as <see cref="EntityRecognizer"/> does.
/// </para>
/// </remarks>
public sealed class OpenEntityRecognizer : TrainableHead
{
    /// <summary>The kind name used by <see cref="HeadRegistry"/>: <c>open-ner</c>.</summary>
    public const string ComponentKind = "open-ner";

    private const int MaxContext = 8;
    private const int MaxWidthOfSpan = 64;
    private const int MaxHidden = 4096;
    private const int WidthEmbedding = 16;
    private const int MaxTypes = 256;
    private const int MaxTypeChars = 200;
    private readonly Lock _typesGate = new();
    private readonly StringStore _strings = new();
    private readonly Lock _cacheGate = new();
    private readonly Dictionary<string, float[]> _typeCache = new(StringComparer.Ordinal);
    private OpenNerModule? _module;
    private string[] _types = [];

    /// <summary>Creates an uninitialized recognizer.</summary>
    /// <param name="encoder">The encoder the recognizer reads, for the text and for the type names.</param>
    /// <param name="context">How many neighbouring vectors on each side join a token's own, from 0 to 8. Default 1.</param>
    /// <param name="hidden">Width of the hidden layers, from 1 to 4096. Default 256.</param>
    /// <param name="embedding">Width of the space spans and types are matched in, from 1 to 4096. Default 128.</param>
    /// <param name="maxSpanLength">The longest entity in tokens, from 1 to 64. Default 12.</param>
    /// <param name="negativeTypes">How many of an example's span-less types it is trained against at most, from 0 to 256. Default 8.</param>
    /// <param name="threshold">The default probability at or above which a span is reported, above 0 and below 1. Default 0.5.</param>
    /// <param name="name">The component name, unique in its pipeline. Default <c>open_ner</c>.</param>
    /// <exception cref="ArgumentOutOfRangeException">A setting is outside its range.</exception>
    public OpenEntityRecognizer(IEncoder encoder, int context = 1, int hidden = 256, int embedding = 128, int maxSpanLength = 12, int negativeTypes = 8, float threshold = 0.5f, string name = "open_ner")
        : base(name, encoder)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(context);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(context, MaxContext);
        ArgumentOutOfRangeException.ThrowIfLessThan(hidden, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(hidden, MaxHidden);
        ArgumentOutOfRangeException.ThrowIfLessThan(embedding, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(embedding, MaxHidden);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxSpanLength, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxSpanLength, MaxWidthOfSpan);
        ArgumentOutOfRangeException.ThrowIfNegative(negativeTypes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(negativeTypes, MaxTypes);
        if (!(threshold > 0f && threshold < 1f))
        {
            throw new ArgumentOutOfRangeException(nameof(threshold), threshold, "The threshold must be above 0 and below 1.");
        }

        Context = context;
        Hidden = hidden;
        MatchWidth = embedding;
        MaxSpanLength = maxSpanLength;
        NegativeTypes = negativeTypes;
        Threshold = threshold;
    }

    /// <summary>Neighbouring vectors on each side of a token that the head reads.</summary>
    public int Context { get; }

    /// <summary>Width of the hidden layers.</summary>
    public int Hidden { get; }

    /// <summary>Width of the space in which spans and types are matched.</summary>
    public int MatchWidth { get; }

    /// <summary>The longest entity in tokens.</summary>
    public int MaxSpanLength { get; }

    /// <summary>The most span-less (negative) types an example is trained against.</summary>
    public int NegativeTypes { get; }

    /// <summary>The default probability at or above which a span is reported. Saved with the head.</summary>
    public float Threshold { get; }

    /// <summary>
    /// The types <see cref="IPipelineComponent.Process"/> looks for, each one to <see cref="MaxTypeChars"/> characters. Set it before processing; the
    /// default is empty, in which case the head adds nothing. Use <see cref="Recognize"/> when several threads pass different types.
    /// </summary>
    /// <exception cref="ArgumentException">More than 256 types, or a type that is empty, longer than 200 characters or repeated.</exception>
    public IReadOnlyList<string> Types
    {
        get => _types;
        set => _types = CheckTypes(value);
    }

    /// <inheritdoc/>
    public override string Kind => ComponentKind;

    /// <inheritdoc/>
    public override bool IsInitialized => _module is not null;

    /// <inheritdoc/>
    public override NnModule Weights => _module ?? throw new InvalidOperationException($"The component '{Name}' is not initialized.");

    /// <summary>Finds entities of the given types in a document.</summary>
    /// <param name="doc">The document; entities already set on it stay and are not overlapped.</param>
    /// <param name="types">The type names, such as <c>"contract party"</c>: 1 to 256 distinct, non-empty strings of at most 200 characters.</param>
    /// <param name="threshold">The probability at or above which a span is reported, above 0 and below 1; <see langword="null"/> uses <see cref="Threshold"/>.</param>
    /// <remarks>Each entity's label is the type's name as given and its confidence is the span's probability for it. Spans are flat: where two overlap, the more probable wins.</remarks>
    /// <exception cref="ArgumentException">The types are invalid (see <see cref="Types"/>), or are empty.</exception>
    /// <exception cref="InvalidOperationException">The head is not initialized.</exception>
    public void Recognize(Doc doc, IReadOnlyList<string> types, float? threshold = null)
    {
        ArgumentNullException.ThrowIfNull(doc);
        string[] checkedTypes = CheckTypes(types);
        if (threshold is { } t && !(t > 0f && t < 1f))
        {
            throw new ArgumentOutOfRangeException(nameof(threshold), t, "The threshold must be above 0 and below 1.");
        }

        if (checkedTypes.Length == 0)
        {
            throw new ArgumentException("Give at least one type.", nameof(types));
        }

        lock (_typesGate)
        {
            string[] before = _types;
            float? beforeThreshold = _threshold;
            _types = checkedTypes;
            _threshold = threshold;
            try
            {
                Process(doc);
            }
            finally
            {
                _types = before;
                _threshold = beforeThreshold;
            }
        }
    }

    private float? _threshold;

    /// <inheritdoc/>
    public override void Initialize(IReadOnlyList<Example> examples, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(examples);
        if (IsInitialized)
        {
            throw new InvalidOperationException($"The component '{Name}' is already initialized.");
        }

        bool any = false;
        foreach (Example example in examples)
        {
            if (HasGold(example))
            {
                any = true;
                break;
            }
        }

        if (!any)
        {
            throw new ArgumentException($"The training examples hold no gold span groups or entities for the component '{Name}'.", nameof(examples));
        }

        Build(new Generator(seed));
    }

    /// <inheritdoc/>
    public override Tensor Loss(EncodedBatch encoded, IReadOnlyList<Example> examples)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        ArgumentNullException.ThrowIfNull(examples);
        OpenNerModule module = _module ?? throw new InvalidOperationException($"The component '{Name}' is not initialized.");
        lock (_cacheGate)
        {
            // The weights are about to change, so type vectors kept for prediction are stale.
            _typeCache.Clear();
        }

        if (examples.Count != encoded.DocCount)
        {
            throw new ArgumentException("The batch has a different number of documents than examples.", nameof(examples));
        }

        // Gold per document: type -> spans (token ranges of the predicted document), or null when the example teaches nothing.
        var gold = new Dictionary<string, List<(int Start, int End)>>?[examples.Count];
        var chosenTypes = new string[examples.Count][];
        var batchTypes = new SortedSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < examples.Count; i++)
        {
            gold[i] = GoldSpans(examples[i], encoded.Length(i));
            chosenTypes[i] = gold[i] is { } g ? ChooseTypes(g, examples[i].Predicted.Text) : [];
            batchTypes.UnionWith(chosenTypes[i]);
        }

        string[] union = [.. batchTypes];
        if (union.Length == 0)
        {
            return Ops.Mul(Ops.Sum(encoded.Vectors), Tensor.Scalar(0f));
        }

        Tensor hidden = module.Hidden(encoded.Vectors, encoded);
        Tensor typeVectors = module.TypeVectors(Encoder.Encode(TypeEncoding(union)));
        var logits = new List<Tensor>();
        var targets = new List<float>();
        for (int d = 0; d < examples.Count; d++)
        {
            string[] chosen = chosenTypes[d];
            if (gold[d] is not { } g || chosen.Length == 0)
            {
                continue;
            }

            int[] typeIds = [.. chosen.Select(t => Array.BinarySearch(union, t, StringComparer.Ordinal))];
            (List<int> begins, List<int> ends, List<int> widths) = Candidates(encoded.Start(d), encoded.Length(d));
            Tensor spanVectors = module.SpanVectors(hidden, Track([.. begins]), Track([.. ends]), Track([.. widths]));
            Tensor selected = Ops.Embedding(typeVectors, Track(typeIds));
            logits.Add(module.Score(spanVectors, selected).Reshape(begins.Count * chosen.Length));

            int startToken = encoded.Start(d);
            var positives = new HashSet<(int, int, int)>();
            for (int k = 0; k < chosen.Length; k++)
            {
                if (g.TryGetValue(chosen[k], out List<(int Start, int End)>? spans))
                {
                    foreach ((int s, int e) in spans)
                    {
                        positives.Add((s, e - 1, k));
                    }
                }
            }

            // Row-major [span, type], in the order of Candidates.
            for (int r = 0; r < begins.Count; r++)
            {
                int i = begins[r] - startToken;
                int j = ends[r] - startToken;
                for (int k = 0; k < chosen.Length; k++)
                {
                    targets.Add(positives.Contains((i, j, k)) ? 1f : 0f);
                }
            }
        }

        Tensor all = logits.Count == 1 ? logits[0] : Ops.Concat([.. logits], 0);
        Tensor target = Tensor.FromSpan<float>([.. targets], Shape.Create(targets.Count));
        ActivationScope.Track(target);
        // Sum over every span-type pair, per token-type cell of the batch: each cell holds up to MaxSpanLength spans, and a plain mean
        // over pairs would be dominated by the many empty ones.
        long cells = 0;
        for (int d = 0; d < examples.Count; d++)
        {
            cells += (long)encoded.Length(d) * chosenTypes[d].Length;
        }

        Tensor sum = Losses.BceWithLogits(all, target, mean: false);
        Tensor scale = Tensor.Scalar(1f / Math.Max(1L, cells));
        ActivationScope.Track(scale);
        return Ops.Mul(sum, scale);
    }

    /// <inheritdoc/>
    public override void Predict(EncodedBatch encoded, ReadOnlySpan<Doc> docs)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        OpenNerModule module = _module ?? throw new InvalidOperationException($"The component '{Name}' is not initialized.");
        string[] types = _types;
        if (types.Length == 0)
        {
            return;
        }

        float threshold = _threshold ?? Threshold;
        Tensor hidden = module.Hidden(encoded.Vectors, encoded);
        Tensor typeVectors = CachedTypeVectors(module, types);
        for (int d = 0; d < docs.Length; d++)
        {
            Doc doc = docs[d];
            (List<int> begins, List<int> ends, List<int> widths) = Candidates(encoded.Start(d), doc.Length);
            Tensor spanVectors = module.SpanVectors(hidden, Track([.. begins]), Track([.. ends]), Track([.. widths]));
            ReadOnlySpan<float> p = Ops.Sigmoid(module.Score(spanVectors, typeVectors)).Data<float>();
            bool[] blocked = Blocked(doc);
            var candidates = new List<(int Start, int End, int Type, float P)>();
            for (int r = 0; r < begins.Count; r++)
            {
                int i = begins[r] - encoded.Start(d);
                int j = ends[r] - encoded.Start(d);
                for (int k = 0; k < types.Length; k++)
                {
                    float v = p[(r * types.Length) + k];
                    if (v >= threshold && !AnyFlag(blocked, i, j + 1))
                    {
                        candidates.Add((i, j + 1, k, v));
                    }
                }
            }

            // Highest probability first; ties go to the longer, then the earlier span, then the earlier type.
            candidates.Sort(static (a, b) =>
            {
                int c = b.P.CompareTo(a.P);
                if (c != 0)
                {
                    return c;
                }

                c = (b.End - b.Start).CompareTo(a.End - a.Start);
                if (c != 0)
                {
                    return c;
                }

                c = a.Start.CompareTo(b.Start);
                return c != 0 ? c : a.Type.CompareTo(b.Type);
            });
            var taken = new bool[doc.Length];
            var found = new List<SpanSpec>(doc.Ents.Count + 4);
            foreach ((int s, int e, int k, float v) in candidates)
            {
                if (AnyFlag(taken, s, e))
                {
                    continue;
                }

                Array.Fill(taken, true, s, e - s);
                found.Add(new SpanSpec(s, e, types[k], Math.Clamp(v, 0f, 1f)));
            }

            foreach (Span existing in doc.Ents)
            {
                found.Add(new SpanSpec(existing.Start, existing.End, existing.Label, existing.Confidence));
            }

            found.Sort(static (a, b) => a.Start.CompareTo(b.Start));
            doc.SetEntities([.. found]);
        }
    }

    /// <inheritdoc/>
    public override void WriteConfig(Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteNumber("context", Context);
        writer.WriteNumber("hidden", Hidden);
        writer.WriteNumber("embedding", MatchWidth);
        writer.WriteNumber("max_span_length", MaxSpanLength);
        writer.WriteNumber("negative_types", NegativeTypes);
        writer.WriteNumber("threshold_percent", (int)MathF.Round(Threshold * 100f));
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
        var head = new OpenEntityRecognizer(
            encoder,
            JsonSettings.Int(config, "context", 0, MaxContext),
            JsonSettings.Int(config, "hidden", 1, MaxHidden),
            JsonSettings.Int(config, "embedding", 1, MaxHidden),
            JsonSettings.Int(config, "max_span_length", 1, MaxWidthOfSpan),
            JsonSettings.Int(config, "negative_types", 0, MaxTypes),
            JsonSettings.Int(config, "threshold_percent", 1, 99) / 100f,
            name)
        { OwnsEncoder = true };
        head.Build(new Generator(0));
        return head;
    }

    private static string[] CheckTypes(IReadOnlyList<string>? types)
    {
        ArgumentNullException.ThrowIfNull(types);
        if (types.Count > MaxTypes)
        {
            throw new ArgumentException($"At most {MaxTypes} types at once.", nameof(types));
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new string[types.Count];
        for (int i = 0; i < result.Length; i++)
        {
            string t = types[i]?.Trim() ?? string.Empty;
            if (t.Length == 0 || t.Length > MaxTypeChars || !seen.Add(t))
            {
                throw new ArgumentException($"Type {i} is empty, longer than {MaxTypeChars} characters or repeated.", nameof(types));
            }

            result[i] = t;
        }

        return result;
    }

    private static Tensor Track(int[] values)
    {
        Tensor t = Tensor.FromSpan<int>(values, Shape.Create(values.Length));
        ActivationScope.Track(t);
        return t;
    }

    private static bool AnyFlag(bool[] flags, int start, int end)
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

    private static bool[] Blocked(Doc doc)
    {
        var blocked = new bool[doc.Length];
        foreach (Span e in doc.Ents)
        {
            Array.Fill(blocked, true, e.Start, e.Length);
        }

        return blocked;
    }

    private static bool HasGold(Example example) =>
        example.Spans is { Count: > 0 } || (example.Reference.Length > 0 && example.Reference[0].EntIob != EntIob.Unset);

    // Every span of up to MaxSpanLength tokens, as rows of batch positions: first token, last token, width - 1.
    private (List<int> Begins, List<int> Ends, List<int> Widths) Candidates(int start, int length)
    {
        var begins = new List<int>();
        var ends = new List<int>();
        var widths = new List<int>();
        for (int i = 0; i < length; i++)
        {
            for (int j = i; j < length && j - i < MaxSpanLength; j++)
            {
                begins.Add(start + i);
                ends.Add(start + j);
                widths.Add(j - i);
            }
        }

        return (begins, ends, widths);
    }

    // Type vectors for prediction, kept between calls: a type name is encoded once, not once per document. Training clears the cache.
    private Tensor CachedTypeVectors(OpenNerModule module, string[] types)
    {
        int width = MatchWidth;
        var flat = new float[types.Length * width];
        lock (_cacheGate)
        {
            string[] missing = [.. types.Where(t => !_typeCache.ContainsKey(t))];
            if (missing.Length > 0)
            {
                if (_typeCache.Count + missing.Length > 4096)
                {
                    _typeCache.Clear();
                    missing = [.. types];
                }

                ReadOnlySpan<float> computed = module.TypeVectors(Encoder.Encode(TypeEncoding(missing))).Data<float>();
                for (int i = 0; i < missing.Length; i++)
                {
                    _typeCache[missing[i]] = computed.Slice(i * width, width).ToArray();
                }
            }

            for (int i = 0; i < types.Length; i++)
            {
                _typeCache[types[i]].CopyTo(flat, i * width);
            }
        }

        Tensor result = Tensor.FromSpan<float>(flat, Shape.Create(types.Length, width));
        ActivationScope.Track(result);
        return result;
    }

    // The type names as one-off documents for the encoder: a type is a short phrase, split at spaces.
    private Doc[] TypeEncoding(string[] types)
    {
        var docs = new Doc[types.Length];
        for (int i = 0; i < types.Length; i++)
        {
            string text = types[i];
            var tokens = new List<TokenBoundary>();
            int p = 0;
            while (p < text.Length)
            {
                while (p < text.Length && char.IsWhiteSpace(text[p]))
                {
                    p++;
                }

                int s = p;
                while (p < text.Length && !char.IsWhiteSpace(text[p]))
                {
                    p++;
                }

                if (p > s)
                {
                    tokens.Add(new TokenBoundary(s, p - s));
                }
            }

            docs[i] = new Doc(_strings, text, [.. tokens]);
        }

        return docs;
    }

    // Gold spans per type for one example, on the predicted tokens; null when the example has none or a span does not align.
    private static Dictionary<string, List<(int Start, int End)>>? GoldSpans(Example example, int tokens)
    {
        if (!HasGold(example) || example.Predicted.Length != tokens)
        {
            return null;
        }

        var result = new Dictionary<string, List<(int, int)>>(StringComparer.Ordinal);
        if (example.Spans is { } groups)
        {
            foreach ((string type, IReadOnlyList<SpanSpec> specs) in groups)
            {
                if (!result.TryGetValue(type, out List<(int, int)>? list))
                {
                    result[type] = list = [];
                }

                foreach (SpanSpec? mapped in example.Alignment.MapSpansYToX(specs))
                {
                    if (mapped is not { } m)
                    {
                        return null;
                    }

                    list.Add((m.Start, m.End));
                }
            }
        }

        if (example.Reference.Length > 0 && example.Reference[0].EntIob != EntIob.Unset)
        {
            foreach (SpanSpec? mapped in example.GetAlignedEntities())
            {
                if (mapped is not { } m)
                {
                    return null;
                }

                if (!result.TryGetValue(m.Label, out List<(int, int)>? list))
                {
                    result[m.Label] = list = [];
                }

                list.Add((m.Start, m.End));
            }
        }

        return result;
    }

    // The example's types with spans, plus up to NegativeTypes of the types it lists without spans. Which ones depends only on the
    // example's text, so a resumed run draws the same negatives as an uninterrupted one.
    private string[] ChooseTypes(Dictionary<string, List<(int Start, int End)>> gold, string text)
    {
        var chosen = new List<string>(gold.Where(g => g.Value.Count > 0).Select(g => g.Key).Order(StringComparer.Ordinal));
        if (chosen.Count > MaxTypes)
        {
            chosen.RemoveRange(MaxTypes, chosen.Count - MaxTypes);
        }

        chosen.AddRange(gold.Where(g => g.Value.Count == 0).Select(g => g.Key).OrderBy(t => Hash(text, t)).Take(NegativeTypes));
        return [.. chosen];
    }

    private static ulong Hash(string text, string type)
    {
        ulong h = 14695981039346656037UL;
        foreach (char c in text)
        {
            h = (h ^ c) * 1099511628211UL;
        }

        h = (h ^ 0xFF) * 1099511628211UL;
        foreach (char c in type)
        {
            h = (h ^ c) * 1099511628211UL;
        }

        return h;
    }

    private void Build(Generator generator) =>
        _module = new OpenNerModule(Encoder.Width, Context, Hidden, MatchWidth, generator);

    private sealed class OpenNerModule : NnModule
    {
        private readonly int _width;
        private readonly int _context;
        private readonly int _embedding;
        private readonly Linear _window;
        private readonly Embedding _spanWidth;
        private readonly Linear _spanHidden;
        private readonly Linear _spanOut;
        private readonly Linear _typeHidden;
        private readonly Linear _typeOut;
        private readonly Embedding _bias;

        public OpenNerModule(int width, int context, int hidden, int embedding, Generator generator)
        {
            _width = width;
            _context = context;
            _embedding = embedding;
            _window = RegisterModule("window", new Linear(((2 * context) + 1) * width, hidden, generator));
            _spanWidth = RegisterModule("span_width", new Embedding(MaxWidthOfSpan, WidthEmbedding, generator));
            _spanHidden = RegisterModule("span_hidden", new Linear((2 * hidden) + WidthEmbedding, hidden, generator));
            _spanOut = RegisterModule("span_out", new Linear(hidden, embedding, generator));
            _typeHidden = RegisterModule("type_hidden", new Linear(width, hidden, generator));
            _typeOut = RegisterModule("type_out", new Linear(hidden, embedding, generator));
            _bias = RegisterModule("bias", new Embedding(1, 1, generator));
        }

        public override Tensor Forward(Tensor input) => throw new NotSupportedException("Use Hidden, SpanVectors, TypeVectors or Score.");

        // As EntityRecognizer: each token's vector joined with its neighbours' (zeros beyond the document), then one ReLU layer.
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

        // GLiNER §3.2: a span is its first and last token representations and a width embedding through a feed-forward layer.
        public Tensor SpanVectors(Tensor hidden, Tensor begins, Tensor ends, Tensor widths)
        {
            Tensor first = Ops.Embedding(hidden, begins);
            Tensor last = Ops.Embedding(hidden, ends);
            Tensor width = _spanWidth.Forward(widths);
            Tensor joined = Ops.Concat([first, last, width], 1);
            return _spanOut.Forward(Ops.Relu(_spanHidden.Forward(joined)));
        }

        // A type's vector: the mean of its name's token vectors from the encoder, through a feed-forward layer.
        public Tensor TypeVectors(EncodedBatch typeNames)
        {
            var pooled = new Tensor[typeNames.DocCount];
            for (int i = 0; i < pooled.Length; i++)
            {
                pooled[i] = Ops.Mean(typeNames.Rows(i), 0, keepDim: true);
            }

            Tensor means = pooled.Length == 1 ? pooled[0] : Ops.Concat(pooled, 0);
            return _typeOut.Forward(Ops.Relu(_typeHidden.Forward(means)));
        }

        // GLiNER §3.3: the score of a span for a type is their dot product (here scaled by 1/sqrt(width) and with a learned offset). [spans, types]
        public Tensor Score(Tensor spanVectors, Tensor typeVectors)
        {
            Tensor dots = Ops.MatMul(spanVectors, typeVectors.T());
            Tensor scaled = Ops.Mul(dots, ScaleOf(1f / MathF.Sqrt(_embedding)));
            Tensor ids = Tensor.FromSpan<int>([0], Shape.Create(1));
            ActivationScope.Track(ids);
            return Ops.Add(scaled, Ops.Embedding(_bias.Weight, ids));
        }

        private static Tensor ScaleOf(float v)
        {
            Tensor s = Tensor.Scalar(v);
            ActivationScope.Track(s);
            return s;
        }
    }
}
