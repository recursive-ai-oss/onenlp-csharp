using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Encoders;

namespace RecursiveAI.OneNlp.Models.Multilingual.Large;

/// <summary>
/// The base-size accurate-tier model: one fine-tuned mmBERT-base encoder (Marone et al. 2025) shared by the heads of nine languages. Each
/// language's pipeline tokenizes, tags (UPOS, morphology and, where the treebank has it, XPOS), lemmatizes, parses dependencies and sets
/// sentence starts, and finds entities where entity data exists.
/// </summary>
/// <remarks>
/// <para>
/// Referencing this package is all the <see cref="Nlp"/> verbs need: the package adds a module initializer to the project that references it, which
/// calls <see cref="Register"/>. The model is accurate-tier: a language that also has a fast-tier package keeps using that package for the plain verbs, and this model serves the accurate-tier request.
/// Nothing is downloaded and nothing is discovered by reflection. The weights are read the first time a verb needs any of the
/// languages; the encoder is then shared by all of them.
/// </para>
/// <para>
/// The weights live in embedded resources of this assembly (the heads, the tokenizer) and of the part assemblies (the encoder's ONNX graph in six
/// consecutive pieces), so that every package stays under the size limit of nuget.org. The encoder runs on OneModel's inference runtime.
/// </para>
/// </remarks>
public static class MultilingualLargeModel
{
    /// <summary>The NuGet package id.</summary>
    public const string PackageId = "RecursiveAI.OneNlp.Models.Multilingual.Large";

    /// <summary>The name under which the shared encoder is registered for the saved pipelines.</summary>
    internal const string SharedName = "multilingual-large";

    private static readonly Lazy<TransformerEncoder> Encoder = new(BuildEncoder, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The languages this package has heads for (ISO 639-1 codes).</summary>
    public static IReadOnlyList<string> Languages { get; } = ["en", "de", "fr", "es", "pt", "it", "nl", "tr", "zh"];

    /// <summary>
    /// Registers this model as the accurate-tier pipeline of its languages, with priority over the base multilingual package. Safe to call more than once; the
    /// registration the package adds to a project calls it. The weights are read on first use.
    /// </summary>
    public static void Register()
    {
        foreach (string code in Languages)
        {
            string language = code;
            ModelPackages.Register(Language.FromCode(language), Tier.Accurate, PackageId, () => Load(language), priority: 1);
        }
    }

    /// <summary>Loads the pipeline of one language, with the shared encoder (loaded on the first call).</summary>
    /// <param name="language">One of <see cref="Languages"/>.</param>
    /// <returns>A new pipeline; the caller disposes it. The encoder it reads lives for the rest of the process.</returns>
    /// <exception cref="ArgumentException">The language has no heads in this package.</exception>
    /// <exception cref="InvalidOperationException">The package was built without its weights.</exception>
    public static Pipeline Load(string language)
    {
        ArgumentNullException.ThrowIfNull(language);
        if (!Languages.Contains(language))
        {
            throw new ArgumentException($"{PackageId} has no model for '{language}'.", nameof(language));
        }

        _ = Encoder.Value;
        using Stream heads = Resource("heads/" + language + ".onenlp");
        return PipelineFile.Load(heads);
    }

    private static Stream Resource(string name) =>
        typeof(MultilingualLargeModel).Assembly.GetManifestResourceStream("weights/" + name)
        ?? throw new InvalidOperationException($"This build of {PackageId} carries no weights ('{name}'); use the packed package.");

    // The fine-tuned encoder is served from its ONNX graph on OneModel's inference runtime (about three times faster than the trainable
    // modules the encoder was fine-tuned in; docs/accurate-tier.md). The graph file is cut into six consecutive pieces to fit the size limit of
    // the package feed; they are read as one stream. The stream is seekable, so the runtime sizes its buffer once and nothing is copied twice.
    private static TransformerEncoder BuildEncoder()
    {
        using Stream tokenizer = Resource("tokenizer.json");
        using var graph = new ConcatenatedStream([Encoder1.Part.Open(), Encoder2.Part.Open(), TokenTable1.Part.Open(), TokenTable2.Part.Open(), TokenTable3.Part.Open(), TokenTable4.Part.Open()]);
        TransformerEncoder encoder = TransformerEncoder.FromStreams(graph, tokenizer, SubwordPooling.Mean, expandWordForms: true);
        SharedEncoder.Register(SharedName, encoder);
        return encoder;
    }

    // Read-only, seekable view of consecutive streams as one. Bounds: every part is seekable with a known length (embedded resources are).
    private sealed class ConcatenatedStream : Stream
    {
        private readonly Stream[] _parts;
        private readonly long[] _starts;
        private long _position;

        public ConcatenatedStream(Stream[] parts)
        {
            _parts = parts;
            _starts = new long[parts.Length + 1];
            for (int i = 0; i < parts.Length; i++)
            {
                _starts[i + 1] = _starts[i] + parts[i].Length;
            }
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => _starts[^1];

        public override long Position
        {
            get => _position;
            set => _position = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            int total = 0;
            while (!buffer.IsEmpty && _position < Length)
            {
                int part = Array.BinarySearch(_starts, _position);
                part = part >= 0 ? part : ~part - 1;
                if (part >= _parts.Length)
                {
                    break;
                }

                _parts[part].Position = _position - _starts[part];
                int want = (int)Math.Min(buffer.Length, _starts[part + 1] - _position);
                int got = _parts[part].Read(buffer[..want]);
                if (got == 0)
                {
                    break;
                }

                _position += got;
                total += got;
                buffer = buffer[got..];
            }

            return total;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            _position = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                _ => Length + offset,
            };
            return _position;
        }

        public override void Flush()
        {
        }

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (Stream s in _parts)
                {
                    s.Dispose();
                }
            }

            base.Dispose(disposing);
        }
    }
}
