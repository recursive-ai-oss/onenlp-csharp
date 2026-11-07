using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Embeddings;
using RecursiveAI.OneNlp.Neural.Encoders;

namespace RecursiveAI.OneNlp.Fuzz;

/// <summary>
/// Targets for the fast tier's readers: the edit-tree text format, the fast encoder's settings, and saved pipelines that hold the fast
/// encoder, the tagger, the morphologizer and the lemmatizer. Only <see cref="InvalidDataException"/> (and, for bare settings,
/// <see cref="ArgumentException"/>, which the pipeline loader turns into it) may escape.
/// </summary>
internal static class NeuralTargets
{
    private const int HeaderSize = 32;

    /// <summary>Edit-tree text: what parses must write back to text that parses to the same tree, and apply to any form without throwing.</summary>
    internal static void EditTreeText(ReadOnlySpan<byte> data)
    {
        string text = Encoding.UTF8.GetString(data);
        EditTree tree;
        try
        {
            tree = EditTree.Parse(text);
        }
        catch (InvalidDataException)
        {
            return;
        }

        string written = tree.Serialize();
        Require(EditTree.Parse(written).Serialize() == written, "edit tree round trip");
        foreach (string form in new[] { string.Empty, "a", "walking", "Walking talks" })
        {
            _ = tree.Apply(form);
        }
    }

    /// <summary>The lemma lexicon table inside a saved lemmatizer: what reads must write back to a table that reads to the same entries.</summary>
    internal static void LemmaLexiconJson(ReadOnlySpan<byte> data)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(data.ToArray(), new JsonDocumentOptions { MaxDepth = 8 });
        }
        catch (JsonException)
        {
            return;
        }

        using (document)
        {
            LemmaLexicon lexicon;
            try
            {
                lexicon = LemmaLexicon.Read(document.RootElement);
            }
            catch (InvalidDataException)
            {
                return;
            }

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                lexicon.Write(writer);
            }

            using JsonDocument again = JsonDocument.Parse(stream.ToArray());
            LemmaLexicon second = LemmaLexicon.Read(again.RootElement);
            Require(second.Count == lexicon.Count, "lexicon round trip count");
            _ = lexicon.TryGet("walking", "VERB", out _);
        }
    }

    /// <summary>The three sentence-embedder configuration files, joined by NUL bytes (modules, pooling, sentence-BERT): only <see cref="InvalidDataException"/> and <see cref="NotSupportedException"/> may escape.</summary>
    internal static void EmbeddingConfigFiles(ReadOnlySpan<byte> data)
    {
        string[] parts = Encoding.UTF8.GetString(data).Split('\0');
        string? Part(int i) => i < parts.Length && parts[i].Length > 0 ? parts[i] : null;
        try
        {
            EmbeddingConfig config = EmbeddingConfig.Parse(Part(0), Part(1), Part(2));
            Require(config.MaxSequenceLength is >= 2 and <= EmbeddingConfig.MaxSequenceLengthLimit, "embedding config length range");
        }
        catch (Exception e) when (e is InvalidDataException or NotSupportedException)
        {
        }
    }

    internal static void SeedEmbeddingConfig(string directory)
    {
        File.WriteAllText(Path.Combine(directory, "seed-bge"), "[{\"idx\":0,\"name\":\"0\",\"path\":\"\",\"type\":\"sentence_transformers.models.Transformer\"},{\"idx\":1,\"name\":\"1\",\"path\":\"1_Pooling\",\"type\":\"sentence_transformers.models.Pooling\"}]\0{\"word_embedding_dimension\":384,\"pooling_mode_cls_token\":true,\"pooling_mode_mean_tokens\":false}\0{\"max_seq_length\":512}");
    }

    internal static void SeedLemmaLexicon(string directory)
    {
        File.WriteAllText(Path.Combine(directory, "seed-lexicon"), "[\"walking\",\"VERB\",\"walk\",\"mice\",\"NOUN\",\"mouse\"]");
        File.WriteAllText(Path.Combine(directory, "seed-empty"), "[]");
    }

    /// <summary>The fast encoder's settings object: what builds must report settings that build the same shape.</summary>
    internal static void FastSettings(ReadOnlySpan<byte> data)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(data.ToArray(), new JsonDocumentOptions { MaxDepth = 32 });
        }
        catch (JsonException)
        {
            return;
        }

        using (document)
        {
            IEncoder encoder;
            try
            {
                encoder = FastEncoder.FromSettings(document.RootElement);
            }
            catch (Exception e) when (e is InvalidDataException or ArgumentException)
            {
                return;
            }

            using (encoder)
            {
                using var stream = new MemoryStream();
                using (var writer = new Utf8JsonWriter(stream))
                {
                    writer.WriteStartObject();
                    encoder.WriteSettings(writer);
                    writer.WriteEndObject();
                }

                using JsonDocument again = JsonDocument.Parse(stream.ToArray());
                using IEncoder rebuilt = FastEncoder.FromSettings(again.RootElement);
                var a = (FastEncoder)encoder;
                var b = (FastEncoder)rebuilt;
                Require(a.Width == b.Width && a.Depth == b.Depth && a.Seed == b.Seed && a.TableRows.SequenceEqual(b.TableRows), "settings round trip");
            }
        }
    }

    /// <summary>A pipeline config object inside a valid header with no weights: reaches encoder and head construction, then fails on the weights.</summary>
    internal static void PipelineConfig(ReadOnlySpan<byte> data)
    {
        var file = new byte[HeaderSize + data.Length];
        "ONENLPPL"u8.CopyTo(file);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt64LittleEndian(file.AsSpan(16), (ulong)data.Length);
        data.CopyTo(file.AsSpan(HeaderSize));
        RunPipeline(file);
    }

    /// <summary>A whole saved pipeline. One that loads must predict, and save to bytes that load and save to the same bytes.</summary>
    internal static void PipelineFileBytes(ReadOnlySpan<byte> data) => RunPipeline(data.ToArray());

    internal static void SeedEditTree(string dir)
    {
        int n = 0;
        foreach ((string form, string lemma) in new[] { ("walking", "walk"), ("went", "go"), ("studies", "study"), ("n't", "not"), ("", "ab"), ("The", "the") })
        {
            File.WriteAllText(Path.Combine(dir, "seed-" + n++), EditTree.Build(form, lemma).Serialize());
        }
    }

    internal static void SeedFastSettings(string dir)
    {
        File.WriteAllText(Path.Combine(dir, "seed-default"), """{"width":96,"depth":4,"normRows":20000,"prefixRows":2000,"suffixRows":5000,"shapeRows":2500,"seed":1}""");
        File.WriteAllText(Path.Combine(dir, "seed-small"), """{"width":8,"depth":0,"normRows":1,"prefixRows":1,"suffixRows":1,"shapeRows":1,"seed":0}""");
    }

    /// <summary>An ONNX model file given to the weight writer: only <see cref="InvalidDataException"/> or <see cref="NotSupportedException"/> may escape, and no output file is left behind on failure.</summary>
    internal static void OnnxPatch(ReadOnlySpan<byte> data)
    {
        string directory = Path.Combine(Path.GetTempPath(), "onenlp-fuzz-onnx-" + Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(directory);
        string input = Path.Combine(directory, "in.onnx");
        string output = Path.Combine(directory, "out.onnx");
        File.WriteAllBytes(input, data.ToArray());
        foreach (string name in PatchNames)
        {
            try
            {
                OnnxWeightPatcher.Patch(input, output, [new WeightPatch(name, [16], static () => new float[16])]);
            }
            catch (Exception e) when (e is InvalidDataException or NotSupportedException)
            {
                Require(!File.Exists(output), "a failed patch leaves no output");
                continue;
            }

            Require(File.Exists(output) && new FileInfo(output).Length > 0, "a patch writes its output");
            File.Delete(output);
        }
    }

    private static readonly string[] PatchNames = ["m.embeddings.LayerNorm.bias", "model.embeddings.LayerNorm.bias", "m.encoder.layer.1.output.LayerNorm.bias", "model.embeddings.projection.bias"];

    internal static void SeedOnnxPatch(string dir)
    {
        // The tiny fixtures (random encoders we generated), found by walking up from the working directory.
        for (string? at = Directory.GetCurrentDirectory(); at is not null; at = Path.GetDirectoryName(at))
        {
            string fixtures = Path.Combine(at, "testdata", "accurate");
            if (Directory.Exists(fixtures))
            {
                foreach (string file in Directory.GetFiles(fixtures, "*.onnx"))
                {
                    File.Copy(file, Path.Combine(dir, "seed-" + Path.GetFileName(file)), overwrite: true);
                }

                return;
            }
        }
    }

    internal static void SeedPipelineFile(string dir)
    {
        File.WriteAllBytes(Path.Combine(dir, "seed-pipeline"), TinyPipelineBytes());
        File.WriteAllBytes(Path.Combine(dir, "seed-segmenter"), TinySegmenterBytes());
        File.WriteAllBytes(Path.Combine(dir, "seed-splitter"), TinySplitterBytes());
        File.WriteAllBytes(Path.Combine(dir, "seed-textcat"), TinyTextcatBytes(multiLabel: false));
        File.WriteAllBytes(Path.Combine(dir, "seed-textcat-multilabel"), TinyTextcatBytes(multiLabel: true));
        File.WriteAllBytes(Path.Combine(dir, "seed-spancat"), TinySpancatBytes());
    }

    internal static void SeedPipelineConfig(string dir)
    {
        byte[] file = TinyPipelineBytes();
        ulong configLength = BinaryPrimitives.ReadUInt64LittleEndian(file.AsSpan(16));
        File.WriteAllBytes(Path.Combine(dir, "seed-config"), file.AsSpan(HeaderSize, (int)configLength).ToArray());
        byte[] segmenter = TinySegmenterBytes();
        ulong segmenterConfigLength = BinaryPrimitives.ReadUInt64LittleEndian(segmenter.AsSpan(16));
        File.WriteAllBytes(Path.Combine(dir, "seed-segmenter-config"), segmenter.AsSpan(HeaderSize, (int)segmenterConfigLength).ToArray());
        byte[] splitter = TinySplitterBytes();
        ulong splitterConfigLength = BinaryPrimitives.ReadUInt64LittleEndian(splitter.AsSpan(16));
        File.WriteAllBytes(Path.Combine(dir, "seed-splitter-config"), splitter.AsSpan(HeaderSize, (int)splitterConfigLength).ToArray());
        foreach ((string name, byte[] bytes) in new[] { ("textcat", TinyTextcatBytes(multiLabel: false)), ("textcat-multilabel", TinyTextcatBytes(multiLabel: true)), ("spancat", TinySpancatBytes()) })
        {
            ulong length = BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(16));
            File.WriteAllBytes(Path.Combine(dir, "seed-" + name + "-config"), bytes.AsSpan(HeaderSize, (int)length).ToArray());
        }
    }

    private static void RunPipeline(byte[] file)
    {
        Pipeline pipeline;
        try
        {
            pipeline = PipelineFile.Load(new MemoryStream(file));
        }
        catch (InvalidDataException)
        {
            return;
        }

        using (pipeline)
        {
            Doc doc = pipeline.Process("Walking talks, 42 times.");
            Require(doc.Length > 0, "loaded pipeline tokenizes");
            using var first = new MemoryStream();
            PipelineFile.Save(pipeline, first);
            using Pipeline again = PipelineFile.Load(new MemoryStream(first.ToArray()));
            using var second = new MemoryStream();
            PipelineFile.Save(again, second);
            Require(first.ToArray().AsSpan().SequenceEqual(second.ToArray()), "save, load, save is stable");
        }
    }

    private static byte[] TinyPipelineBytes()
    {
        var conllu = new StringBuilder();
        string[] stems = ["walk", "talk", "jump"];
        for (int s = 0; s < 12; s++)
        {
            for (int i = 0; i < 4; i++)
            {
                int k = (s + i) % 3;
                string stem = stems[(s + i * 2) % 3];
                conllu.Append(i + 1).Append('\t').Append(stem).Append(k == 1 ? "s" : k == 2 ? "ing" : string.Empty).Append('\t').Append(stem)
                    .Append("\tVERB\tV").Append(k).Append('\t').Append(k == 1 ? "Number=Sing" : "_").Append("\t0\troot\t_\t_\n");
            }

            conllu.Append('\n');
        }

        List<Example> examples = [.. ConllUReader.Read(new StringReader(conllu.ToString())).Select(x => Example.FromReference(x.Doc))];
        var encoder = new FastEncoder(width: 8, depth: 1, normRows: 16, prefixRows: 8, suffixRows: 16, shapeRows: 8, seed: 1);
        using Pipeline pipeline = Pipeline.Blank(Language.English)
            .Add(new Tagger(encoder) { OwnsEncoder = true })
            .Add(new Morphologizer(encoder))
            .Add(new EditTreeLemmatizer(encoder));
        _ = pipeline.Train(examples, new TrainOptions { Epochs = 1, BatchWords = 40, DevFraction = 0, Seed = 1 });
        using var stream = new MemoryStream();
        PipelineFile.Save(pipeline, stream);
        return stream.ToArray();
    }

    // A pipeline with a text classifier head (the "textcat" head kind), multi-class or multi-label.
    private static byte[] TinyTextcatBytes(bool multiLabel)
    {
        const string data = """
            {"text":"the cat sat on the mat","cats":{"ANIMAL":1,"FOOD":0}}
            {"text":"we ate bread and cheese","cats":{"ANIMAL":0,"FOOD":1}}
            {"text":"a dog chased the cat","cats":{"ANIMAL":1,"FOOD":0}}
            {"text":"fresh bread with cheese","cats":{"ANIMAL":0,"FOOD":1}}
            """;
        List<Example> examples = [.. JsonlReader.Read(new StringReader(data))];
        var encoder = new FastEncoder(width: 8, depth: 1, normRows: 16, prefixRows: 8, suffixRows: 16, shapeRows: 8, seed: 1);
        using Pipeline pipeline = Pipeline.Blank(Language.English).Add(new TextClassifier(encoder, multiLabel, hiddenSize: 4) { OwnsEncoder = true });
        _ = pipeline.Train(examples, new TrainOptions { Epochs = 1, BatchWords = 40, DevFraction = 0, Seed = 1 });
        using var stream = new MemoryStream();
        PipelineFile.Save(pipeline, stream);
        return stream.ToArray();
    }

    // A pipeline with a span categorizer head (the "spancat" head kind).
    private static byte[] TinySpancatBytes()
    {
        const string data = """
            {"text":"Grand Piano and Widget","spans":{"sc":[[0,11,"PRODUCT"],[6,11,"PART"],[16,22,"PART"]]}}
            {"text":"a Widget in the room","spans":{"sc":[[2,8,"PART"]]}}
            {"text":"nothing here at all","spans":{"sc":[]}}
            """;
        List<Example> examples = [.. JsonlReader.Read(new StringReader(data))];
        var encoder = new FastEncoder(width: 8, depth: 1, normRows: 16, prefixRows: 8, suffixRows: 16, shapeRows: 8, seed: 1);
        using Pipeline pipeline = Pipeline.Blank(Language.English).Add(new SpanCategorizer(encoder, "sc", [1, 2], hiddenSize: 4) { OwnsEncoder = true });
        _ = pipeline.Train(examples, new TrainOptions { Epochs = 1, BatchWords = 40, DevFraction = 0, Seed = 1 });
        using var stream = new MemoryStream();
        PipelineFile.Save(pipeline, stream);
        return stream.ToArray();
    }

    // A pipeline whose tokenizer is a trained word segmenter (the "segmenter" tokenizer kind of the file format).
    private static byte[] TinySegmenterBytes()
    {
        var encoder = new FastEncoder(width: 8, depth: 1, normRows: 16, prefixRows: 8, suffixRows: 16, shapeRows: 8, seed: 1, neighbourRows: 8, recurrentLayers: 1, recurrentSize: 4);
        var strings = new StringStore();
        var examples = new List<Example>();
        foreach (string text in new[] { "我们喜欢学习", "中文和数据", "我们的数据" })
        {
            TokenBoundary[] units = NeuralWordSegmenter.Units(text);
            var doc = new Doc(strings, text, units);
            string[] tags = NeuralWordSegmenter.Tags(units, [0, 2], out _);
            for (int u = 0; u < tags.Length; u++)
            {
                doc.SetAnnotation(TokenColumn.Tag, u, tags[u]);
            }

            examples.Add(Example.FromReference(doc));
        }

        using Pipeline training = Pipeline.Blank(Language.Chinese).Add(new Tagger(encoder, TokenColumn.Tag, NeuralWordSegmenter.HeadName) { OwnsEncoder = true });
        _ = training.Train(examples, new TrainOptions { Epochs = 1, BatchWords = 40, DevFraction = 0, Seed = 1 });
        using var holder = new Pipeline(new NeuralWordSegmenter((Tagger)training.Components[0]), Language.Chinese);
        using var stream = new MemoryStream();
        PipelineFile.Save(holder, stream);
        return stream.ToArray();
    }

    // A pipeline whose tokenizer is a multiword-token splitter (the "mwt" tokenizer kind of the file format).
    private static byte[] TinySplitterBytes()
    {
        var encoder = new FastEncoder(width: 8, depth: 1, normRows: 16, prefixRows: 8, suffixRows: 16, shapeRows: 8, seed: 1, recurrentLayers: 1, recurrentSize: 4);
        var strings = new StringStore();
        var examples = new List<Example>();
        foreach (string[] tokens in new[] { new[] { "vardır", "ok" }, new[] { "güzeldir" }, new[] { "yok", "kitaptır" } })
        {
            Doc doc = NeuralMultiwordSplitter.MakeWindow(strings, tokens, out int[] first);
            var tags = new string[doc.Length];
            Array.Fill(tags, NeuralMultiwordSplitter.BeginLabel);
            for (int k = 0; k < tokens.Length; k++)
            {
                if (NeuralMultiwordSplitter.IsCandidate(tokens[k]) && tokens[k].Length > 5)
                {
                    string[] t = NeuralMultiwordSplitter.Tags(tokens[k].Length, [tokens[k].Length - 3, 3]);
                    Array.Copy(t, 0, tags, first[k], t.Length);
                }
            }

            for (int u = 0; u < tags.Length; u++)
            {
                doc.SetAnnotation(TokenColumn.Tag, u, tags[u]);
            }

            examples.Add(Example.FromReference(doc));
        }

        using Pipeline training = Pipeline.Blank(Language.Turkish).Add(new Tagger(encoder, TokenColumn.Tag, NeuralMultiwordSplitter.HeadName) { OwnsEncoder = true });
        _ = training.Train(examples, new TrainOptions { Epochs = 1, BatchWords = 40, DevFraction = 0, Seed = 1 });
        using var holder = new Pipeline(new NeuralMultiwordSplitter(new RecursiveAI.OneNlp.Segmentation.UaxWordTokenizer(RecursiveAI.OneNlp.Segmentation.SegmentationData.TurkishWhole), (Tagger)training.Components[0]), Language.Turkish);
        using var stream = new MemoryStream();
        PipelineFile.Save(holder, stream);
        return stream.ToArray();
    }

    private static void Require(bool condition, string what)
    {
        if (!condition)
        {
            throw new InvalidOperationException("Invariant failed: " + what);
        }
    }
}
