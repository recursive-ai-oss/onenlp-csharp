using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;
using RecursiveAI.OneNlp.Neural.Training;

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

    internal static void SeedPipelineFile(string dir) => File.WriteAllBytes(Path.Combine(dir, "seed-pipeline"), TinyPipelineBytes());

    internal static void SeedPipelineConfig(string dir)
    {
        byte[] file = TinyPipelineBytes();
        ulong configLength = BinaryPrimitives.ReadUInt64LittleEndian(file.AsSpan(16));
        File.WriteAllBytes(Path.Combine(dir, "seed-config"), file.AsSpan(HeaderSize, (int)configLength).ToArray());
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

    private static void Require(bool condition, string what)
    {
        if (!condition)
        {
            throw new InvalidOperationException("Invariant failed: " + what);
        }
    }
}
