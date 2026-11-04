using System.Text.Json;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural.Encoders;

namespace RecursiveAI.OneNlp.Neural.Components;

/// <summary>
/// A graph-based dependency parser: for every token it scores every other token of the sentence as its head with a biaffine
/// classifier and labels the chosen arc with a second biaffine classifier (Dozat and Manning 2017, "Deep Biaffine Attention for
/// Neural Dependency Parsing"). The tree is the maximum spanning tree of the arc scores (Chu-Liu/Edmonds), so crossing arcs are allowed.
/// It writes the head and dependency relation of every token into the document.
/// </summary>
/// <remarks>
/// <para>
/// <b>Context.</b> The biaffine scorer wants each token's vector to know its neighbours. The encoder gives some; the parser adds
/// <see cref="ContextLayers"/> residual layers of its own, each a linear map of the previous, current and next token vectors
/// followed by a ReLU (a convolution of width 3), so the parser works on top of an encoder that sees one token at a time as well.
/// </para>
/// <para>
/// <b>Sentences.</b> A tree is built per sentence. When the document already has sentence boundaries (a sentencizer ran, or the
/// caller set them), the parser uses them. Otherwise it predicts them with a two-class classifier on the same vectors, trained
/// from the reference documents' boundaries, and writes them into the document; training documents that hold several
/// consecutive sentences are what teach it. A predicted sentence longer than <see cref="MaxSentenceLength"/> tokens is cut into pieces of that length.
/// </para>
/// <para>
/// <b>Labels.</b> The label set is every dependency relation of the training examples' reference documents, in ordinal order. A
/// token that is its own head in the reference is a root; its relation (normally <c>root</c>) is a label like any other, and the
/// decoder gives <c>root</c> to the one token attached to the artificial root, and never to any other.
/// </para>
/// <para>
/// A training example is used only when its predicted and reference tokens match one to one; others are left out of the loss.
/// </para>
/// </remarks>
public sealed class Parser : TrainableHead
{
    /// <summary>The kind name used by <see cref="HeadRegistry"/>: <c>parser</c>.</summary>
    public const string ComponentKind = "parser";

    /// <summary>The longest sentence parsed in one piece; a longer one is cut. Default 400.</summary>
    public const int MaxSentenceLength = 400;

    private const string RootLabel = "root";
    private const int IgnoreIndex = -100;
    private const int MaxLabels = 4096;
    private const float DiagonalMask = -1e4f;
    private string[] _labels = [];
    private int _rootLabel = -1;
    private Dictionary<string, int> _labelIds = new(StringComparer.Ordinal);
    private ParserModule? _module;

    /// <summary>Creates an uninitialized parser.</summary>
    /// <param name="encoder">The encoder the parser reads.</param>
    /// <param name="arcSize">The width of the vectors the arc scorer compares, 1 to 2048. Default 256.</param>
    /// <param name="labelSize">The width of the vectors the label scorer compares, 1 to 1024. Default 128.</param>
    /// <param name="contextLayers">The number of window layers the parser adds on top of the encoder, 0 to 16. Default 2.</param>
    /// <param name="name">The component name, unique in its pipeline. Default <c>parser</c>.</param>
    /// <exception cref="ArgumentOutOfRangeException">A size is out of range.</exception>
    public Parser(IEncoder encoder, int arcSize = 256, int labelSize = 128, int contextLayers = 2, string name = "parser")
        : base(name, encoder)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(arcSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(arcSize, 2048);
        ArgumentOutOfRangeException.ThrowIfLessThan(labelSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(labelSize, 1024);
        ArgumentOutOfRangeException.ThrowIfLessThan(contextLayers, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(contextLayers, 16);
        ArcSize = arcSize;
        LabelSize = labelSize;
        ContextLayers = contextLayers;
    }

    /// <summary>The width of the arc scorer's vectors.</summary>
    public int ArcSize { get; }

    /// <summary>The width of the label scorer's vectors.</summary>
    public int LabelSize { get; }

    /// <summary>The number of window layers on top of the encoder.</summary>
    public int ContextLayers { get; }

    /// <summary>The dependency relations, in output order; empty until the parser is initialized.</summary>
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
            foreach (int id in reference.GetColumn(TokenColumn.Dep))
            {
                if (id != 0)
                {
                    seen.Add(reference.Strings[id]);
                }
            }
        }

        if (seen.Count == 0)
        {
            throw new ArgumentException($"The training examples hold no gold dependency relations for the component '{Name}'.", nameof(examples));
        }

        if (seen.Count > MaxLabels)
        {
            throw new ArgumentException($"The training examples hold more than {MaxLabels} distinct dependency relations.", nameof(examples));
        }

        Build([.. seen], new Generator(seed));
    }

    /// <inheritdoc/>
    public override Tensor Loss(EncodedBatch encoded, IReadOnlyList<Example> examples)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        ArgumentNullException.ThrowIfNull(examples);
        ParserModule module = _module ?? throw new InvalidOperationException($"The component '{Name}' is not initialized.");
        if (examples.Count != encoded.DocCount)
        {
            throw new ArgumentException("The batch has a different number of documents than examples.", nameof(examples));
        }

        int total = encoded.TokenCount;
        var boundaryTargets = new int[total];
        Array.Fill(boundaryTargets, IgnoreIndex);
        var depRows = new List<int>();
        var headRows = new List<int>();
        var relTargets = new List<int>();
        var sentences = new List<(int Start, int End, int[] Heads)>(); // global rows; heads are local, -1 for the root

        for (int e = 0; e < examples.Count; e++)
        {
            GoldTree? gold = GoldTree.Create(examples[e], _labelIds);
            if (gold is null)
            {
                continue;
            }

            int offset = encoded.Start(e);
            for (int t = 0; t < gold.Length; t++)
            {
                boundaryTargets[offset + t] = gold.SentenceStart[t] ? 1 : 0;
            }

            foreach ((int a, int b) in gold.Sentences)
            {
                var local = new int[b - a];
                for (int t = a; t < b; t++)
                {
                    int head = gold.Heads[t];
                    local[t - a] = head == t ? -1 : head - a;
                    depRows.Add(offset + t);
                    headRows.Add(head == t ? total : offset + head);
                    relTargets.Add(gold.Labels[t] ?? IgnoreIndex);
                }

                sentences.Add((offset + a, offset + b, local));
            }
        }

        Tensor x = module.Context(encoded.Vectors, encoded);
        Tensor boundaryLoss = Losses.CrossEntropy(module.Boundary(x), IntTensor(boundaryTargets), 0f, IgnoreIndex, mean: false);
        if (sentences.Count == 0)
        {
            return boundaryLoss * 0f;
        }

        ParserModule.Projections p = module.Project(x);
        Tensor arcLoss = default;
        bool first = true;
        foreach ((int start, int end, int[] heads) in sentences)
        {
            Tensor scores = module.ArcScores(p, start, end);
            var targets = new int[end - start];
            for (int t = 0; t < targets.Length; t++)
            {
                targets[t] = heads[t] + 1;
            }

            Tensor loss = Losses.CrossEntropy(scores, IntTensor(targets), 0f, IgnoreIndex, mean: false);
            arcLoss = first ? loss : arcLoss + loss;
            first = false;
        }

        Tensor relLogits = module.LabelScores(p, depRows.ToArray(), headRows.ToArray());
        Tensor relLoss = Losses.CrossEntropy(relLogits, IntTensor([.. relTargets]), 0f, IgnoreIndex, mean: false);
        return (arcLoss + relLoss + boundaryLoss) * (1f / total);
    }

    /// <inheritdoc/>
    public override void Predict(EncodedBatch encoded, ReadOnlySpan<Doc> docs)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        ParserModule module = _module ?? throw new InvalidOperationException($"The component '{Name}' is not initialized.");
        int total = encoded.TokenCount;
        Tensor x = module.Context(encoded.Vectors, encoded);
        ReadOnlySpan<float> boundary = module.Boundary(x).Data<float>();
        ParserModule.Projections p = module.Project(x);

        var sentenceRanges = new List<(int Start, int End)>(); // global rows
        for (int d = 0; d < docs.Length; d++)
        {
            Doc doc = docs[d];
            int offset = encoded.Start(d);
            if (!HasBoundaries(doc))
            {
                var predicted = new SentenceBoundary[doc.Length];
                for (int t = 0; t < doc.Length; t++)
                {
                    bool start = t == 0 || boundary[((offset + t) * 2) + 1] > boundary[(offset + t) * 2];
                    predicted[t] = start ? SentenceBoundary.Start : SentenceBoundary.Inside;
                }

                doc.SetSentenceBoundaries(predicted);
            }

            ReadOnlySpan<SentenceBoundary> marks = doc.GetSentenceBoundaries();
            int begin = 0;
            for (int t = 1; t <= doc.Length; t++)
            {
                if (t == doc.Length || marks[t] == SentenceBoundary.Start || t - begin == MaxSentenceLength)
                {
                    sentenceRanges.Add((offset + begin, offset + t));
                    begin = t;
                }
            }
        }

        // Trees first, then all labels in one batch.
        var heads = new int[total]; // global row of the head; the token's own row for a root
        var depRows = new List<int>();
        var headRows = new List<int>();
        var rootRows = new List<int>();
        foreach ((int start, int end) in sentenceRanges)
        {
            int n = end - start;
            ReadOnlySpan<float> s = module.ArcScores(p, start, end).Data<float>();
            var graph = new float[n + 1, n + 1];
            for (int d = 0; d < n; d++)
            {
                for (int h = 0; h <= n; h++)
                {
                    graph[h, d + 1] = s[(d * (n + 1)) + h];
                }
            }

            int[] tree = MaximumSpanningTree.Decode(graph);
            for (int d = 1; d <= n; d++)
            {
                int row = start + d - 1;
                int head = tree[d];
                heads[row] = head == 0 ? row : start + head - 1;
                depRows.Add(row);
                headRows.Add(head == 0 ? total : heads[row]);
                if (head == 0)
                {
                    rootRows.Add(row);
                }
            }
        }

        var labelOf = new int[total];
        if (depRows.Count > 0)
        {
            int classes = _labels.Length;
            ReadOnlySpan<float> logits = module.LabelScores(p, depRows.ToArray(), headRows.ToArray()).Data<float>();
            for (int i = 0; i < depRows.Count; i++)
            {
                bool isRoot = headRows[i] == total;
                int pick = -1;
                for (int c = 0; c < classes; c++)
                {
                    // Only the root takes the label root, and the root takes nothing else (when the label exists).
                    if (_rootLabel >= 0 && (c == _rootLabel) != isRoot)
                    {
                        continue;
                    }

                    if (pick < 0 || logits[(i * classes) + c] > logits[(i * classes) + pick])
                    {
                        pick = c;
                    }
                }

                labelOf[depRows[i]] = pick < 0 ? 0 : pick;
            }
        }

        for (int d = 0; d < docs.Length; d++)
        {
            Doc doc = docs[d];
            int offset = encoded.Start(d);
            var docHeads = new int[doc.Length];
            var docDeps = new int[doc.Length];
            for (int t = 0; t < doc.Length; t++)
            {
                docHeads[t] = heads[offset + t] - offset;
                docDeps[t] = doc.Strings.Add(_labels[labelOf[offset + t]]);
            }

            doc.SetHeads(docHeads);
            doc.SetColumn(TokenColumn.Dep, docDeps);
        }
    }

    /// <inheritdoc/>
    public override void WriteConfig(Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteNumber("arcSize", ArcSize);
        writer.WriteNumber("labelSize", LabelSize);
        writer.WriteNumber("contextLayers", ContextLayers);
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
            throw new InvalidDataException("A parser's 'labels' must be a non-empty list without duplicates.");
        }

        var parser = new Parser(
            encoder,
            JsonSettings.Int(config, "arcSize", 1, 2048),
            JsonSettings.Int(config, "labelSize", 1, 1024),
            JsonSettings.Int(config, "contextLayers", 0, 16),
            name)
        { OwnsEncoder = true };
        parser.Build(labels, new Generator(0));
        return parser;
    }

    private static bool HasBoundaries(Doc doc)
    {
        foreach (SentenceBoundary b in doc.GetSentenceBoundaries())
        {
            if (b != SentenceBoundary.Unset)
            {
                return true;
            }
        }

        return false;
    }

    private static Tensor IntTensor(int[] values)
    {
        Tensor t = Tensor.FromSpan<int>(values, Shape.Create(values.Length));
        ActivationScope.Track(t);
        return t;
    }

    private void Build(string[] labels, Generator generator)
    {
        _labels = labels;
        _labelIds = new Dictionary<string, int>(labels.Length, StringComparer.Ordinal);
        for (int i = 0; i < labels.Length; i++)
        {
            _labelIds[labels[i]] = i;
        }

        _rootLabel = _labelIds.TryGetValue(RootLabel, out int root) ? root : -1;
        _module = new ParserModule(Encoder.Width, ArcSize, LabelSize, ContextLayers, labels.Length, generator);
    }

    /// <summary>The gold tree of an example on the predicted tokens, or null when the tokenizations differ.</summary>
    private sealed class GoldTree
    {
        public int Length { get; private init; }

        public int[] Heads { get; private init; } = [];

        public int?[] Labels { get; private init; } = [];

        public bool[] SentenceStart { get; private init; } = [];

        public List<(int Start, int End)> Sentences { get; private init; } = [];

        public static GoldTree? Create(Example example, Dictionary<string, int> labelIds)
        {
            Doc predicted = example.Predicted;
            Doc reference = example.Reference;
            if (predicted.Length != reference.Length || predicted.Length == 0)
            {
                return null;
            }

            Alignment alignment = example.Alignment;
            int n = predicted.Length;
            for (int t = 0; t < n; t++)
            {
                if (!alignment.IsOneToOne(t) || alignment.XToY(t)[0] != t)
                {
                    return null;
                }
            }

            ReadOnlySpan<int> refHeads = reference.GetHeads();
            ReadOnlySpan<int> refDeps = reference.GetColumn(TokenColumn.Dep);
            ReadOnlySpan<SentenceBoundary> marks = reference.GetSentenceBoundaries();
            var start = new bool[n];
            var sentences = new List<(int, int)>();
            int begin = 0;
            for (int t = 0; t <= n; t++)
            {
                if (t == n || (t > 0 && marks[t] == SentenceBoundary.Start))
                {
                    sentences.Add((begin, t));
                    begin = t;
                }
            }

            foreach ((int a, _) in sentences)
            {
                start[a] = true;
            }

            var labels = new int?[n];
            for (int t = 0; t < n; t++)
            {
                if (refDeps[t] != 0 && labelIds.TryGetValue(reference.Strings[refDeps[t]], out int id))
                {
                    labels[t] = id;
                }
            }

            // Heads must stay inside their sentence; a malformed reference drops the example from the arc loss.
            foreach ((int a, int b) in sentences)
            {
                for (int t = a; t < b; t++)
                {
                    if (refHeads[t] < a || refHeads[t] >= b)
                    {
                        return null;
                    }
                }
            }

            return new GoldTree { Length = n, Heads = refHeads.ToArray(), Labels = labels, SentenceStart = start, Sentences = sentences };
        }
    }

    private sealed class ParserModule : NnModule
    {
        private readonly int _width;
        private readonly int _labels;
        private readonly int _labelSize;
        private readonly Linear[] _window;
        private readonly Linear _arcDep;
        private readonly Linear _arcHead;
        private readonly Linear _arcBilinear;
        private readonly Linear _arcHeadBias;
        private readonly Linear _relDep;
        private readonly Linear _relHead;
        private readonly Linear _relBilinear;
        private readonly Linear _relPair;
        private readonly Linear _boundary;
        private readonly Embedding _rootArc;
        private readonly Embedding _rootRel;

        public ParserModule(int width, int arcSize, int labelSize, int contextLayers, int labels, Generator generator)
        {
            _width = width;
            _labels = labels;
            _labelSize = labelSize;
            _window = new Linear[contextLayers];
            for (int i = 0; i < contextLayers; i++)
            {
                _window[i] = RegisterModule("window" + i, new Linear(3 * width, width, generator));
            }

            _arcDep = RegisterModule("arc_dep", new Linear(width, arcSize, generator));
            _arcHead = RegisterModule("arc_head", new Linear(width, arcSize, generator));
            _arcBilinear = RegisterModule("arc_bilinear", new Linear(arcSize, arcSize, generator, bias: false));
            _arcHeadBias = RegisterModule("arc_head_bias", new Linear(arcSize, 1, generator));
            _relDep = RegisterModule("rel_dep", new Linear(width, labelSize, generator));
            _relHead = RegisterModule("rel_head", new Linear(width, labelSize, generator));
            _relBilinear = RegisterModule("rel_bilinear", new Linear(labelSize, labels * labelSize, generator, bias: false));
            _relPair = RegisterModule("rel_pair", new Linear(2 * labelSize, labels, generator));
            _boundary = RegisterModule("boundary", new Linear(width, 2, generator));
            _rootArc = RegisterModule("root_arc", new Embedding(1, arcSize, generator));
            _rootRel = RegisterModule("root_rel", new Embedding(1, labelSize, generator));
        }

        public override Tensor Forward(Tensor input) => throw new NotSupportedException("A parser's module is driven by the parser.");

        // Window layers: x <- x + ReLU(W [x_{i-1}; x_i; x_{i+1}]). The ends of a document see zeros, so a batch behaves like its documents alone.
        public Tensor Context(Tensor vectors, EncodedBatch batch)
        {
            Tensor x = vectors;
            int n = x.Shape[0];
            if (_window.Length == 0 || n == 0)
            {
                return x;
            }

            var previous = new int[n];
            var next = new int[n];
            for (int d = 0; d < batch.DocCount; d++)
            {
                int a = batch.Start(d);
                int b = a + batch.Length(d);
                for (int r = a; r < b; r++)
                {
                    previous[r] = r > a ? r - 1 : n;
                    next[r] = r + 1 < b ? r + 1 : n;
                }
            }

            Tensor previousIds = IntTensor(previous);
            Tensor nextIds = IntTensor(next);
            Tensor zero = Tensor.Zeros(DType.F32, Shape.Create(1, _width));
            ActivationScope.Track(zero);
            foreach (Linear layer in _window)
            {
                Tensor padded = Ops.Concat([x, zero], 0);
                Tensor window = Ops.Concat([Ops.Embedding(padded, previousIds), x, Ops.Embedding(padded, nextIds)], 1);
                x = x + Ops.Relu(layer.Forward(window));
            }

            return x;
        }

        public Tensor Boundary(Tensor x) => _boundary.Forward(x);

        public Projections Project(Tensor x) => new(
            Ops.Relu(_arcDep.Forward(x)),
            Ops.Relu(_arcHead.Forward(x)),
            Ops.Relu(_relDep.Forward(x)),
            Ops.Relu(_relHead.Forward(x)));

        // Scores [n, n + 1] of rows start..end: column 0 is the artificial root, column j + 1 the token j of the sentence.
        // s[i, j] = d_i' U h_j + b . h_j (Dozat and Manning 2017 §2.2: biaffine with a head-side bias); a token cannot head itself.
        public Tensor ArcScores(Projections p, int start, int end)
        {
            int n = end - start;
            Tensor dep = p.ArcDep.Slice(0, start, end);
            Tensor head = Ops.Concat([_rootArc.Forward(IntTensor([0])), p.ArcHead.Slice(0, start, end)], 0);
            Tensor bilinear = Ops.MatMul(_arcBilinear.Forward(dep), head.Transpose(0, 1));
            Tensor bias = _arcHeadBias.Forward(head).Reshape(1, n + 1);
            Tensor mask = Tensor.Zeros(DType.F32, Shape.Create(n, n + 1));
            Span<float> m = mask.MutableData<float>();
            for (int i = 0; i < n; i++)
            {
                m[(i * (n + 1)) + i + 1] = DiagonalMask;
            }

            ActivationScope.Track(mask);
            return bilinear + bias + mask;
        }

        // Label scores [M, labels] for arcs (dependent row, head row); head row == the batch's token count means the artificial root.
        // l[i, c] = d' U_c h + W [d; h] + b (Dozat and Manning 2017 §2.2: a biaffine classifier with one matrix per label).
        public Tensor LabelScores(Projections p, int[] depRows, int[] headRows)
        {
            int count = depRows.Length;
            Tensor heads = Ops.Concat([p.RelHead, _rootRel.Forward(IntTensor([0]))], 0);
            Tensor d = Ops.Embedding(p.RelDep, IntTensor(depRows));
            Tensor h = Ops.Embedding(heads, IntTensor(headRows));
            Tensor product = _relBilinear.Forward(d).Reshape(count, _labels, _labelSize) * h.Reshape(count, 1, _labelSize);
            return Ops.Sum(product, 2) + _relPair.Forward(Ops.Concat([d, h], 1));
        }

        private static Tensor IntTensor(int[] values)
        {
            Tensor t = Tensor.FromSpan<int>(values, Shape.Create(values.Length));
            ActivationScope.Track(t);
            return t;
        }

        public readonly record struct Projections(Tensor ArcDep, Tensor ArcHead, Tensor RelDep, Tensor RelHead);
    }
}
