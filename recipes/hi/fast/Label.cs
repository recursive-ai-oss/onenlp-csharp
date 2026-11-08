using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;
using RecursiveAI.OneNlp.Segmentation;

namespace RecursiveAI.OneNlp.Recipes.Hindi;

// The labelling half of the recipe. There is no allowed Hindi treebank, so the student's labels come from the accurate tier read as a
// multi-source cross-lingual transfer system (McDonald, Petrov and Hall 2011, "Multi-source transfer of delexicalized dependency
// parsers"): the encoder is shared by all languages, so heads trained on other languages' treebanks can be applied to Hindi text, and the
// agreement between several of them is the confidence. This file writes what each source head says; Ensemble.cs combines them.
//
//   label --weights DIR --text FILE.jsonl --pud FILE.conllu --out FILE.jsonl.gz [--langs en,de,...] [--first-doc N] [--docs N]
//         [--max-sentences N] [--batch 32] [--threads T] [--source fw]
//
// One output line per kept sentence, with the raw predictions of every source head, so that the ensemble rule can be changed
// without running the encoder again.
internal static partial class Program
{
    private const string EncoderName = "multilingual-large";

    // Devanagari block, U+0900..U+097F, and the Vedic extensions that carry no letters of other scripts.
    private static bool IsDevanagari(char c) => c is >= '\u0900' and <= '\u097F';

    // Whether a sentence is Hindi running text and worth labelling: enough tokens, nearly all letters Devanagari, mostly words.
    private static bool UsableSentence(Doc doc, int minTokens, int maxTokens)
    {
        if (doc.Length < minTokens || doc.Length > maxTokens)
        {
            return false;
        }

        int letters = 0;
        int devanagari = 0;
        int words = 0;
        foreach (Token t in doc)
        {
            bool word = false;
            foreach (char c in t.TextSpan)
            {
                if (char.IsLetter(c) || (c >= '\u0900' && c <= '\u0903') || (c >= '\u093A' && c <= '\u094F'))
                {
                    letters++;
                    word = true;
                    if (IsDevanagari(c))
                    {
                        devanagari++;
                    }
                }
            }

            if (word)
            {
                words++;
            }
        }

        return letters > 0 && devanagari >= 0.95 * letters && words >= 0.6 * doc.Length;
    }

    // The word 8-grams of the evaluation file under our own tokenizer, lower-cased: a candidate sentence that shares one is dropped
    // (docs/provenance.md, "Overlap"). Whole-sentence equality is covered by the same test for sentences of eight words or more and by the
    // exact-text set for shorter ones.
    private const int OverlapN = 8;

    private sealed class EvalOverlap
    {
        private readonly HashSet<string> _grams = new(StringComparer.Ordinal);
        private readonly HashSet<string> _sentences = new(StringComparer.Ordinal);

        public EvalOverlap(string conllu, Pipeline tokenizer)
        {
            foreach (ConllUSentence s in ConllUReader.Read(conllu))
            {
                Doc d = tokenizer.Process(s.Doc.Text);
                string[] words = [.. d.Select(t => t.Text.ToLowerInvariant())];
                _sentences.Add(string.Join(' ', words));
                for (int i = 0; i + OverlapN <= words.Length; i++)
                {
                    _grams.Add(string.Join(' ', words.Skip(i).Take(OverlapN)));
                }
            }
        }

        public bool Hits(Doc d)
        {
            string[] words = [.. d.Select(t => t.Text.ToLowerInvariant())];
            if (_sentences.Contains(string.Join(' ', words)))
            {
                return true;
            }

            for (int i = 0; i + OverlapN <= words.Length; i++)
            {
                if (_grams.Contains(string.Join(' ', words.Skip(i).Take(OverlapN))))
                {
                    return true;
                }
            }

            return false;
        }
    }

    private sealed class Source(string language, Pipeline pipeline)
    {
        public string Language { get; } = language;

        public Morphologizer? Morphologizer { get; } = pipeline.Components.OfType<Morphologizer>().FirstOrDefault();

        public Parser? Parser { get; } = pipeline.Components.OfType<Parser>().FirstOrDefault();

        public EntityRecognizer? Entities { get; } = pipeline.Components.OfType<EntityRecognizer>().FirstOrDefault();

        public Pipeline Pipeline { get; } = pipeline;
    }

    // The accurate tier rebuilt from its exported files, the way the package does: the encoder, the heads from the per-language files.
    // Two layouts: a graph cut in pieces (encoder.onnx.part1, ...; served by the inference runtime) or trainable-module parts. Read-only use.
    private static IEncoder LoadEncoder(string dir)
    {
        if (File.Exists(Path.Combine(dir, "encoder.onnx.part1")))
        {
            using Stream graph = new ConcatenatedFiles([.. Directory.GetFiles(dir, "encoder.onnx.part*").OrderBy(f => int.Parse(f[(f.LastIndexOf("part", StringComparison.Ordinal) + 4)..], CultureInfo.InvariantCulture))]);
            using Stream tokenizer = File.OpenRead(Path.Combine(dir, "tokenizer.json"));
            return TransformerEncoder.FromStreams(graph, tokenizer, SubwordPooling.Mean, expandWordForms: true);
        }

        using JsonDocument description = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(dir, "encoder.json")));
        JsonElement shape = description.RootElement.GetProperty("shape");
        Stream[] table = [.. Directory.GetFiles(dir, "token-table-0?.bin").Order(StringComparer.Ordinal).Select(f => (Stream)File.OpenRead(f))];
        try
        {
            using Stream layers = new ConcatenatedFiles([Path.Combine(dir, "encoder-layers.ckpt.part1"), Path.Combine(dir, "encoder-layers.ckpt.part2")]);
            using Stream tokenizerJson = File.OpenRead(Path.Combine(dir, "tokenizer.json"));
            return TrainableTransformerEncoder.FromParts(
                description.RootElement.GetProperty("settings"),
                shape.GetProperty("vocabularySize").GetInt32(),
                shape.GetProperty("width").GetInt32(),
                shape.GetProperty("layers").GetInt32(),
                shape.GetProperty("feedForwardWidth").GetInt32(),
                tokenizerJson,
                table,
                layers);
        }
        finally
        {
            foreach (Stream t in table)
            {
                t.Dispose();
            }
        }
    }

    private static string HeadsFile(string dir, string lang)
    {
        string file = Path.Combine(dir, "heads", lang + ".onenlp");
        return File.Exists(file) ? file : Path.Combine(dir, lang + ".onenlp");
    }

    // The two halves of the layers' checkpoint, read as one stream.
    private sealed class ConcatenatedFiles(string[] files) : Stream
    {
        private readonly MemoryStream _all = Join(files);

        private static MemoryStream Join(string[] names)
        {
            var ms = new MemoryStream();
            foreach (string f in names)
            {
                using FileStream fs = File.OpenRead(f);
                fs.CopyTo(ms);
            }

            ms.Position = 0;
            return ms;
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => _all.Length;

        public override long Position { get => _all.Position; set => _all.Position = value; }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => _all.Read(buffer, offset, count);

        public override int Read(Span<byte> buffer) => _all.Read(buffer);

        public override long Seek(long offset, SeekOrigin origin) => _all.Seek(offset, origin);

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _all.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private static int Label(string[] args)
    {
        string weights = Required(args, "weights");
        string[] langs = Opt(args, "langs", "en,de,fr,es,pt,it,nl,tr").Split(',');
        int firstDoc = Num(args, "first-doc", 0);
        int docLimit = Num(args, "docs", int.MaxValue);
        int maxSentences = Num(args, "max-sentences", int.MaxValue);
        int batchSize = Num(args, "batch", 32);
        string source = Opt(args, "source", "fw");
        string output = Required(args, "out");
        var clock = Stopwatch.StartNew();

        using var segmenter = Pipeline.Blank(Language.Hindi).Add("sentencizer", new Dictionary<string, string> { ["language"] = "hi" });
        using var tokenizer = Pipeline.Blank(Language.Hindi);
        EvalOverlap? overlap = Opt(args, "pud", string.Empty) is { Length: > 0 } pud ? new EvalOverlap(pud, tokenizer) : null;

        using IEncoder encoder = LoadEncoder(weights);
        SharedEncoder.Register(EncoderName, encoder);
        var sources = new List<Source>();
        foreach (string lang in langs)
        {
            // --extra-heads DIR: head sets trained afterwards on the same encoder (Related.cs), found as DIR/<lang>.onenlp.
            string extra = Path.Combine(Opt(args, "extra-heads", "."), lang + ".onenlp");
            sources.Add(new Source(lang, PipelineFile.Load(Opt(args, "extra-heads", string.Empty).Length > 0 && File.Exists(extra) ? extra : HeadsFile(weights, lang))));
        }

        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"loaded encoder and {sources.Count} head sets in {clock.Elapsed.TotalSeconds:F1} s, threads={TensorCompute.Threads}"));

        int docIndex = -1;
        int sentences = 0;
        int tokens = 0;
        int droppedOverlap = 0;
        int droppedDuplicate = 0;
        int droppedShape = 0;
        int droppedAdultDocs = 0;
        int droppedAdultSentences = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var batch = new List<(int Doc, Doc Tokens)>();

        using var file = new FileStream(output + ".part", FileMode.Create);
        using var gz = new GZipStream(file, CompressionLevel.Fastest);

        void Flush()
        {
            if (batch.Count == 0)
            {
                return;
            }

            WriteBatch(gz, sources, encoder, batch);
            sentences += batch.Count;
            tokens += batch.Sum(b => b.Tokens.Length);
            batch.Clear();
            if (sentences % 1024 < batchSize)
            {
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"sentences={sentences} tokens={tokens} elapsed_s={clock.Elapsed.TotalSeconds:F0} tokens_per_s={tokens / clock.Elapsed.TotalSeconds:F0}"));
            }
        }

        if (Opt(args, "conllu", string.Empty) is { Length: > 0 } goldFile)
        {
            // A CoNLL-U file in place of web text: its tokens are labelled as they are, with no filtering (the hand-annotated development sentences).
            foreach (ConllUSentence gold in ConllUReader.Read(goldFile))
            {
                batch.Add((docIndex = batch.Count + sentences, Example.Blank(gold.Doc)));
                if (batch.Count == batchSize)
                {
                    Flush();
                }
            }

            Flush();
            gz.Dispose();
            file.Dispose();
            File.Move(output + ".part", output, overwrite: true);
            Console.WriteLine($"labelled {sentences} sentences of {goldFile}");
            return 0;
        }

        foreach (string line in File.ReadLines(Required(args, "text")))
        {
            docIndex++;
            if (docIndex < firstDoc)
            {
                continue;
            }

            if (docIndex >= firstDoc + docLimit || sentences + batch.Count >= maxSentences)
            {
                break;
            }

            using JsonDocument rec = JsonDocument.Parse(line);
            if (rec.RootElement.GetProperty("s").GetString() != source)
            {
                continue;
            }

            string docText = rec.RootElement.GetProperty("t").GetString()!;
            if (AdultFilter.Document(docText))
            {
                droppedAdultDocs++;
                continue;
            }

            foreach (string paragraph in docText.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                Doc paragraphDoc = segmenter.Process(paragraph);
                foreach (Span sentence in paragraphDoc.Sentences)
                {
                    Doc d = tokenizer.Process(sentence.Text.Trim());
                    if (!UsableSentence(d, 5, 50))
                    {
                        droppedShape++;
                        continue;
                    }

                    if (AdultFilter.Sentence(d.Text))
                    {
                        droppedAdultSentences++;
                        continue;
                    }

                    if (overlap is not null && overlap.Hits(d))
                    {
                        droppedOverlap++;
                        continue;
                    }

                    if (!seen.Add(d.Text))
                    {
                        droppedDuplicate++;
                        continue;
                    }

                    batch.Add((docIndex, d));
                    if (batch.Count == batchSize)
                    {
                        Flush();
                    }
                }
            }
        }

        Flush();
        gz.Dispose();
        file.Dispose();
        File.Move(output + ".part", output, overwrite: true);
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"done sentences={sentences} tokens={tokens} docs_to={docIndex} dropped_shape={droppedShape} dropped_adult_docs={droppedAdultDocs} dropped_adult_sentences={droppedAdultSentences} dropped_eval_overlap={droppedOverlap} dropped_duplicate={droppedDuplicate} wall_s={clock.Elapsed.TotalSeconds:F1} threads={TensorCompute.Threads}"));
        foreach (Source s in sources)
        {
            s.Pipeline.Dispose();
        }

        return 0;
    }

    // Encodes the batch once and lets every source's heads read the same vectors: the encoder dominates the cost, the heads are small.
    private static void WriteBatch(Stream output, List<Source> sources, IEncoder encoder, List<(int Doc, Doc Tokens)> batch)
    {
        Doc[] docs = [.. batch.Select(b => b.Tokens)];
        EncodedBatch encoded = encoder.Encode(docs);
        var perSource = new Doc[sources.Count][];
        for (int s = 0; s < sources.Count; s++)
        {
            Doc[] copies = [.. docs.Select(Example.Blank)];
            Source src = sources[s];
            src.Morphologizer?.Predict(encoded, copies);
            src.Parser?.Predict(encoded, copies);
            src.Entities?.Predict(encoded, copies);
            perSource[s] = copies;
        }

        using var w = new Utf8JsonWriter(output, new JsonWriterOptions { SkipValidation = true });
        for (int i = 0; i < docs.Length; i++)
        {
            w.WriteStartObject();
            w.WriteNumber("d", batch[i].Doc);
            w.WriteString("text", docs[i].Text);
            w.WriteStartArray("tok");
            for (int t = 0; t < docs[i].Length; t++)
            {
                w.WriteNumberValue(docs[i].GetStarts()[t]);
                w.WriteNumberValue(docs[i].GetLengths()[t]);
            }

            w.WriteEndArray();
            w.WriteStartObject("src");
            for (int s = 0; s < sources.Count; s++)
            {
                Doc p = perSource[s][i];
                w.WriteStartObject(sources[s].Language);
                w.WriteString("pos", string.Join(' ', p.Select(t => t.Pos.Length == 0 ? "_" : t.Pos)));
                w.WriteString("morph", string.Join(' ', p.Select(t => t.Morph.Length == 0 ? "_" : t.Morph.Replace(' ', '+'))));
                w.WriteString("head", string.Join(' ', p.Select(t => t.HeadIndex.ToString(CultureInfo.InvariantCulture))));
                w.WriteString("dep", string.Join(' ', p.Select(t => t.Dep.Length == 0 ? "_" : t.Dep)));
                w.WriteString("ner", string.Join(' ', IobTags(p).Select(x => x)));
                w.WriteEndObject();
            }

            w.WriteEndObject();
            w.WriteEndObject();
            w.Flush();
            output.WriteByte((byte)'\n');
            w.Reset(output);
        }
    }
}
