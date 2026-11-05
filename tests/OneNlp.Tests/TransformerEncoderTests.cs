using System.Text.Json;
using RecursiveAI.OneModel.Graph;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;
using RecursiveAI.OneNlp.Neural.Training;
using RecursiveAI.OneNlp.Tokenizers;

namespace RecursiveAI.OneNlp.Tests;

// The fixture is a random 2-layer BERT-style encoder (width 16) with a small WordPiece vocabulary, made by
// tools/accurate/make_tiny_fixture.py; its reference hidden states come from PyTorch.
public class TransformerEncoderTests
{
    private static string Dir => Path.Combine(AppContext.BaseDirectory, "testdata", "accurate");

    private static string ModelFile => Path.Combine(Dir, "tiny-bert.onnx");

    private static string TokenizerFile => Path.Combine(Dir, "tiny-bert.tokenizer.json");

    private static Doc Words(string text)
    {
        var tokens = new List<TokenBoundary>();
        int i = 0;
        while (i < text.Length)
        {
            if (text[i] == ' ')
            {
                i++;
                continue;
            }

            int j = i;
            while (j < text.Length && text[j] != ' ')
            {
                j++;
            }

            tokens.Add(new TokenBoundary(i, j - i));
            i = j;
        }

        return new Doc(new StringStore(), text, [.. tokens]);
    }

    [Fact]
    public void OneModelReproducesPyTorchOnTheFixture()
    {
        using JsonDocument reference = JsonDocument.Parse(File.ReadAllText(Path.Combine(Dir, "tiny-bert.reference.json")));
        using Session session = Session.Load(ModelFile);
        JsonElement ids = reference.RootElement.GetProperty("input_ids");
        JsonElement mask = reference.RootElement.GetProperty("attention_mask");
        JsonElement hidden = reference.RootElement.GetProperty("hidden");
        int rows = ids.GetArrayLength();
        int len = ids[0].GetArrayLength();
        long[] x = [.. ids.EnumerateArray().SelectMany(r => r.EnumerateArray().Select(v => v.GetInt64()))];
        long[] m = [.. mask.EnumerateArray().SelectMany(r => r.EnumerateArray().Select(v => v.GetInt64()))];
        using Tensor tx = Tensor.FromSpan<long>(x, Shape.Create(rows, len));
        using Tensor tm = Tensor.FromSpan<long>(m, Shape.Create(rows, len));
        using SessionOutputs outputs = session.Run(new Dictionary<string, Tensor> { ["input_ids"] = tx, ["attention_mask"] = tm });
        ReadOnlySpan<float> got = outputs["last_hidden_state"].Data<float>();
        double max = 0;
        for (int r = 0; r < rows; r++)
        {
            for (int t = 0; t < len; t++)
            {
                if (m[(r * len) + t] == 0)
                {
                    continue; // padding rows are not defined
                }

                for (int j = 0; j < 16; j++)
                {
                    max = Math.Max(max, Math.Abs(got[(((r * len) + t) * 16) + j] - hidden[r][t][j].GetSingle()));
                }
            }
        }

        Assert.True(max < 1e-4, $"max abs difference {max}");
    }

    // Runs the model on the tokenizer's own encoding of text, as the encoder should, and returns the piece rows.
    private static (TokenEncoding Pieces, float[] Rows) RunDirect(string text)
    {
        Tokenizer tok = Tokenizer.FromFile(TokenizerFile);
        TokenEncoding with = tok.Encode(text, addSpecialTokens: true);
        using Session session = Session.Load(ModelFile);
        long[] x = [.. with.Ids.Select(v => (long)v)];
        long[] m = [.. x.Select(_ => 1L)];
        using Tensor tx = Tensor.FromSpan<long>(x, Shape.Create(1, x.Length));
        using Tensor tm = Tensor.FromSpan<long>(m, Shape.Create(1, x.Length));
        using SessionOutputs o = session.Run(new Dictionary<string, Tensor> { ["input_ids"] = tx, ["attention_mask"] = tm });
        return (with, o["last_hidden_state"].Data<float>().ToArray());
    }

    [Fact]
    public void FirstPieceAndMeanPoolingFollowThePiecesOfEachWord()
    {
        const string text = "the cat sat on unable";
        (TokenEncoding pieces, float[] rows) = RunDirect(text); // [CLS] the cat sat on un ##able [SEP]
        Assert.Equal(8, pieces.Length);
        Doc doc = Words(text);

        using var first = new TransformerEncoder(ModelFile, TokenizerFile, SubwordPooling.FirstPiece);
        using var mean = new TransformerEncoder(ModelFile, TokenizerFile, SubwordPooling.Mean);
        using var scope = new ActivationScope();
        float[] f = first.Encode([doc]).Vectors.Data<float>().ToArray();
        float[] mn = mean.Encode([doc]).Vectors.Data<float>().ToArray();
        Assert.Equal(5 * 16, f.Length);
        for (int t = 0; t < 5; t++)
        {
            for (int j = 0; j < 16; j++)
            {
                float pieceRow = rows[((t + 1) * 16) + j];
                Assert.Equal(pieceRow, f[(t * 16) + j], 1e-4f);
                float expectedMean = t == 4 ? (rows[(5 * 16) + j] + rows[(6 * 16) + j]) / 2 : pieceRow;
                Assert.Equal(expectedMean, mn[(t * 16) + j], 1e-4f);
            }
        }
    }

    [Fact]
    public void ABatchGivesTheSameRowsAsEachDocumentAloneAndEmptyDocumentsAddNoRows()
    {
        using var encoder = new TransformerEncoder(ModelFile, TokenizerFile) { WindowBatch = 4 };
        using var scope = new ActivationScope();
        Doc a = Words("the cat sat on the mat");
        Doc b = Words("dog ran");
        Doc empty = Words("");
        EncodedBatch batch = encoder.Encode([a, empty, b]);
        Assert.Equal(3, batch.DocCount);
        Assert.Equal(0, batch.Length(1));
        Assert.Equal(8, batch.TokenCount);

        using var alone = new TransformerEncoder(ModelFile, TokenizerFile);
        float[] ra = alone.Encode([Words("the cat sat on the mat")]).Vectors.Data<float>().ToArray();
        float[] rb = alone.Encode([Words("dog ran")]).Vectors.Data<float>().ToArray();
        float[] all = batch.Vectors.Data<float>().ToArray();
        for (int i = 0; i < ra.Length; i++)
        {
            Assert.Equal(ra[i], all[i], 1e-4f);
        }

        for (int i = 0; i < rb.Length; i++)
        {
            Assert.Equal(rb[i], all[ra.Length + i], 1e-4f);
        }
    }

    [Fact]
    public void LongDocumentsAreWindowedAndEveryTokenGetsAVector()
    {
        string text = string.Join(' ', Enumerable.Repeat("the cat sat on a mat and dog ran", 12)); // 108 words, one piece each
        Doc doc = Words(text);
        using var encoder = new TransformerEncoder(ModelFile, TokenizerFile, SubwordPooling.FirstPiece, maxLength: 16);
        using var scope = new ActivationScope();
        float[] v = encoder.Encode([doc]).Vectors.Data<float>().ToArray();
        Assert.Equal(doc.Length * 16, v.Length);
        for (int t = 0; t < doc.Length; t++)
        {
            Assert.Contains(v.AsSpan(t * 16, 16).ToArray(), x => x != 0f);
            Assert.All(v.AsSpan(t * 16, 16).ToArray(), x => Assert.True(float.IsFinite(x)));
        }

        // The first token is read from the first window and the last from the last: compare with direct runs of those windows.
        string[] words = text.Split(' ');
        (TokenEncoding head, float[] headRows) = RunDirect(string.Join(' ', words.Take(14)));
        Assert.Equal(16, head.Length);
        for (int j = 0; j < 16; j++)
        {
            Assert.Equal(headRows[(1 * 16) + j], v[j], 1e-4f);
        }

        (_, float[] tailRows) = RunDirect(string.Join(' ', words.Skip(words.Length - 14)));
        for (int j = 0; j < 16; j++)
        {
            Assert.Equal(tailRows[(14 * 16) + j], v[((doc.Length - 1) * 16) + j], 1e-4f);
        }
    }

    [Fact]
    public void ATokenWithNoPieceGetsAZeroVector()
    {
        using var encoder = new TransformerEncoder(ModelFile, TokenizerFile);
        using var scope = new ActivationScope();
        // The space inside a token's range is skipped by the tokenizer, but a token made only of a pre-tokenizer-dropped character
        // (here a control character) has no piece at all.
        Doc doc = Words("cat \u0001 dog");
        float[] v = encoder.Encode([doc]).Vectors.Data<float>().ToArray();
        Assert.All(v.AsSpan(16, 16).ToArray(), x => Assert.Equal(0f, x));
        Assert.Contains(v.AsSpan(0, 16).ToArray(), x => x != 0f);
    }

    [Fact]
    public void RejectsModelsAndLengthsItCannotUse()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TransformerEncoder(ModelFile, TokenizerFile, maxLength: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TransformerEncoder(ModelFile, TokenizerFile, maxLength: 1 << 20));
        Assert.Throws<FileNotFoundException>(() => new TransformerEncoder(ModelFile + ".missing", TokenizerFile));
    }

    private static List<Example> Toy(int sentences, int seed)
    {
        var random = new Random(seed);
        (string Word, string Pos)[] lexicon = [("the", "DET"), ("a", "DET"), ("cat", "NOUN"), ("dog", "NOUN"), ("mat", "NOUN"), ("sat", "VERB"), ("ran", "VERB"), ("on", "ADP"), ("of", "ADP")];
        var sb = new System.Text.StringBuilder();
        for (int s = 0; s < sentences; s++)
        {
            int n = random.Next(3, 8);
            for (int i = 0; i < n; i++)
            {
                (string w, string pos) = lexicon[random.Next(lexicon.Length)];
                sb.Append(i + 1).Append('\t').Append(w).Append('\t').Append(w).Append('\t').Append(pos).Append("\t_\t_\t")
                  .Append(i == 0 ? 0 : 1).Append('\t').Append(i == 0 ? "root" : "dep").Append("\t_\t_\n");
            }

            sb.Append('\n');
        }

        return [.. ConllUReader.Read(new StringReader(sb.ToString())).Select(x => Example.FromReference(x.Doc))];
    }

    [Fact]
    public void ATaggerTrainsOnTheFrozenEncoderAndTheSavedPipelineReloadsIt()
    {
        List<Example> train = Toy(120, 1);
        List<Example> test = Toy(30, 2);
        using var encoder = new TransformerEncoder(ModelFile, TokenizerFile);
        using Pipeline pipeline = Pipeline.Blank().Add(new Tagger(encoder, TokenColumn.Pos));
        TrainResult result = pipeline.Train(train, new TrainOptions { Epochs = 40, BatchWords = 200, LearningRate = 0.02f, Dropout = 0f, Seed = 5, DevFraction = 0.1 });
        Assert.True(result.Completed);
        Assert.Null(encoder.Weights);
        double accuracy = Accuracy(pipeline, test);
        Assert.True(accuracy > 0.95, $"UPOS {accuracy}");

        string path = Path.Combine(Path.GetTempPath(), "onenlp-transformer-" + Guid.NewGuid().ToString("N") + ".onenlp");
        try
        {
            PipelineFile.Save(pipeline, path);
            TransformerEncoder.ModelDirectory = Dir;
            try
            {
                using Pipeline loaded = PipelineFile.Load(path);
                Assert.Equal(accuracy, Accuracy(loaded, test));
            }
            finally
            {
                TransformerEncoder.ModelDirectory = null;
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static double Accuracy(Pipeline pipeline, List<Example> examples)
    {
        int right = 0, total = 0;
        foreach (Example e in examples)
        {
            Doc d = Example.Blank(e.Reference);
            foreach (IPipelineComponent c in pipeline.Components.Where(c => c is ITrainableComponent))
            {
                c.Process(d);
            }

            for (int i = 0; i < d.Length; i++, total++)
            {
                right += d[i].Pos == e.Reference[i].Pos ? 1 : 0;
            }
        }

        return (double)right / total;
    }

    [Fact]
    public void TheEncoderKindIsRegisteredAndWritesItsFileReferences()
    {
        using var encoder = new TransformerEncoder(ModelFile, TokenizerFile, SubwordPooling.Mean, 64, modelSha256: new string('a', 64));
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            encoder.WriteSettings(w);
            w.WriteEndObject();
        }

        using JsonDocument settings = JsonDocument.Parse(stream.ToArray());
        Assert.Equal("mean", settings.RootElement.GetProperty("pooling").GetString());
        Assert.Equal(64, settings.RootElement.GetProperty("maxLength").GetInt32());
        using IEncoder again = EncoderRegistry.Create("transformer", settings.RootElement);
        Assert.Equal(16, again.Width);
        Assert.Throws<InvalidDataException>(() => ((TransformerEncoder)again).VerifyModelFile());
    }

    // ---- The distilled-student layout: a random 2-layer encoder with a factorised embedding (width 16, rank 8), made by
    // tools/distill/make_tiny_student_fixture.py; its reference hidden states come from PyTorch.

    private static string StudentFile => Path.Combine(Dir, "tiny-student.onnx");

    [Fact]
    public void TheTrainableModulesReproduceThePyTorchStudent()
    {
        using JsonDocument reference = JsonDocument.Parse(File.ReadAllText(Path.Combine(Dir, "tiny-student.reference.json")));
        JsonElement ids = reference.RootElement.GetProperty("input_ids");
        JsonElement mask = reference.RootElement.GetProperty("attention_mask");
        JsonElement hidden = reference.RootElement.GetProperty("hidden");
        int rows = ids.GetArrayLength(), len = ids[0].GetArrayLength();
        long[] x = [.. ids.EnumerateArray().SelectMany(r => r.EnumerateArray().Select(v => v.GetInt64()))];
        long[] m = [.. mask.EnumerateArray().SelectMany(r => r.EnumerateArray().Select(v => v.GetInt64()))];
        int[] lengths = [.. mask.EnumerateArray().Select(r => r.EnumerateArray().Sum(v => v.GetInt32()))];
        using TrainableTransformerEncoder encoder = TrainableTransformerEncoder.LoadOnnx(StudentFile, TokenizerFile);
        Assert.Equal(16, encoder.Width);
        Assert.Equal(TransformerFamily.PostNorm, encoder.Family);
        var module = (PostNormModule)encoder.Weights!;
        Assert.Equal(4, module.Config.Heads);
        Assert.Equal(2, module.Config.Layers);
        Assert.Equal(8, module.Config.EmbeddingWidth);
        using var scope = new ActivationScope();
        using Tensor tx = Tensor.FromSpan<long>(x, Shape.Create(rows, len));
        using (GradMode.NoGrad())
        {
            Tensor h = module.Forward(tx, lengths);
            ReadOnlySpan<float> got = h.Data<float>();
            double max = 0;
            for (int r = 0; r < rows; r++)
            {
                for (int t = 0; t < len; t++)
                {
                    if (m[(r * len) + t] == 0)
                    {
                        continue;
                    }

                    for (int j = 0; j < 16; j++)
                    {
                        max = Math.Max(max, Math.Abs(got[(((r * len) + t) * 16) + j] - hidden[r][t][j].GetSingle()));
                    }
                }
            }

            Assert.True(max < 1e-4, $"max abs difference {max}");
        }
    }

    [Fact]
    public void TheStudentGraphAndItsTrainableCopyGiveTheSameTokenVectors()
    {
        Doc doc = Words("the cat sat on unable mat");
        foreach (SubwordPooling pooling in new[] { SubwordPooling.FirstPiece, SubwordPooling.Mean })
        {
            using var frozen = new TransformerEncoder(StudentFile, TokenizerFile, pooling, maxLength: 8);
            using var tuned = TrainableTransformerEncoder.LoadOnnx(StudentFile, TokenizerFile, new TrainableTransformerOptions { Pooling = pooling, MaxLength = 8 });
            Assert.Null(frozen.Weights);
            Assert.NotNull(tuned.Weights);
            using var scope = new ActivationScope();
            float[] a = frozen.Encode([doc]).Vectors.Data<float>().ToArray();
            float[] b;
            using (GradMode.NoGrad())
            {
                b = tuned.Encode([doc, Words("")]).Vectors.Data<float>().ToArray();
            }

            Assert.Equal(a.Length, b.Length);
            for (int i = 0; i < a.Length; i++)
            {
                Assert.Equal(a[i], b[i], 1e-4f);
            }
        }
    }

    [Fact]
    public void FineTuningTheStudentThroughTrainUpdatesTheEncoderAndTheSavedPipelineReloadsIt()
    {
        List<Example> train = Toy(120, 1);
        List<Example> test = Toy(30, 2);
        using TrainableTransformerEncoder encoder = TrainableTransformerEncoder.LoadOnnx(StudentFile, TokenizerFile);
        NnModule weights = encoder.Weights!;
        float[] before = weights.Parameters().First().Data<float>().ToArray();
        using Pipeline pipeline = Pipeline.Blank().Add(new Tagger(encoder, TokenColumn.Pos));
        TrainResult result = pipeline.Train(train, new TrainOptions { Epochs = 12, BatchWords = 200, LearningRate = 0.01f, Dropout = 0f, Seed = 5, DevFraction = 0.1 });
        Assert.True(result.Completed);
        Assert.False(before.AsSpan().SequenceEqual(weights.Parameters().First().Data<float>()), "the encoder's weights did not change");
        double accuracy = Accuracy(pipeline, test);
        Assert.True(accuracy > 0.9, $"UPOS {accuracy}");

        string path = Path.Combine(Path.GetTempPath(), "onenlp-finetune-" + Guid.NewGuid().ToString("N") + ".onenlp");
        try
        {
            PipelineFile.Save(pipeline, path);
            TransformerEncoder.ModelDirectory = Dir;
            try
            {
                using Pipeline loaded = PipelineFile.Load(path);
                Assert.Equal(accuracy, Accuracy(loaded, test));
            }
            finally
            {
                TransformerEncoder.ModelDirectory = null;
            }
        }
        finally
        {
            File.Delete(path);
        }
    }
}
