using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using RecursiveAI.OneNlp;
using RecursiveAI.OneNlp.Serialization;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;
using RecursiveAI.OneNlp.Neural.Training;
using RecursiveAI.OneNlp.Tokenizers;

namespace RecursiveAI.OneNlp.AotSmoke;

/// <summary>
/// Builds a pipeline from registered names, processes text in batches, and uses the tokenizer contract, all under NativeAOT.
/// Registration through a module initializer is the way a package adds components without reflection.
/// </summary>
internal static class Program
{
    private static int failures;

    [ModuleInitializer]
    internal static void RegisterComponents() =>
        ComponentRegistry.Register("shout", settings => new ShoutComponent(settings.TryGetValue("suffix", out string? s) ? s : "!"));

    private static int Main()
    {
        // Doc, Token and Span views.
        Doc doc = Nlp.Analyze("Ada Lovelace wrote notes, 1843.");
        Check("token count", doc.Length == 7);
        Check("offsets", doc[1].Idx == 4 && doc[1].EndIdx == 12 && doc[1].Text.SequenceEqual("Lovelace"));
        doc.SetEntities([new SpanSpec(0, 2, "PER", 0.75f)]);
        Check("entity", doc.Ents.Count == 1 && doc.Ents[0].ToString() == "Ada Lovelace" && doc[1].EntIob == EntIob.Inside && doc.Ents[0].Confidence == 0.75f);
        doc.SetAnnotation(TokenColumn.Pos, 0, "PROPN");
        Check("annotation", doc[0].Pos == "PROPN" && doc[1].Pos.Length == 0);
        Check("char span", doc.TryGetCharSpan(4, 18, out Span span) && span.ToString() == "Lovelace wrote");

        // Word and sentence segmentation, with English data and the registered sentencizer.
        Token[] tokens = Nlp.Tokens("Mr. Smith can't go. He's gone!");
        Check("contraction split", tokens.Length == 10 && tokens[2].ToString() == "ca" && tokens[3].ToString() == "n't");
        Check("sentences", Nlp.Sentences("Mr. Smith can't go. He's gone!").Count == 2);

        // Registration by name, built-in and from this assembly's module initializer.
        using Pipeline pipeline = Pipeline.Blank(Language.English).Add("norm").Add("shout", new Dictionary<string, string> { ["suffix"] = "?" });
        Doc[] docs = [.. pipeline.ProcessMany(["One Two", "THREE"], batchSize: 1)];
        Check("pipeline order", docs.Length == 2 && docs[0][0].Norm == "one" && docs[0][1].Lemma == "Two?" && docs[1][0].Norm == "three");
        Check("unknown component", Throws<InvalidOperationException>(() => pipeline.Add("missing")));

        // The tokenizer contract.
        var tokenizer = new SpaceTokenizer();
        Span<int> ids = stackalloc int[2];
        OperationStatus status = tokenizer.EncodeToIds("héllo wörld again"u8, ids, out int written, out int consumed);
        Check("encode to ids", status == OperationStatus.DestinationTooSmall && written == 2 && consumed == 13);
        TokenEncoding encoding = tokenizer.Encode("héllo wörld"u8);
        Check("utf8 offsets", encoding.Offsets[1].Start == 7 && encoding.Offsets[1].End == 13);
        Check("trim", tokenizer.TrimToTokenBudget("a b c", 2).SequenceEqual("a b"));
        Check("empty vocab.txt rejected", Throws<InvalidDataException>(() => Tokenizer.FromStream(new MemoryStream(), TokenizerFormat.WordPieceVocabulary)));

        // Both serialization formats, and the container, under NativeAOT.
        Doc annotated = Nlp.Analyze("Ada Lovelace wrote notes in London \uD83D\uDE00.");
        annotated.SetAnnotation(TokenColumn.Pos, 0, "PROPN");
        annotated.SetAnnotation(TokenColumn.Norm, 1, "lovelace");
        annotated.SetHeads(new int[annotated.Length]);
        annotated.SetSentenceStart(0, true);
        annotated.SetConfidence(2, 0.5f);
        annotated.SetEntities([new SpanSpec(0, 2, "PER", 0.75f)]);
        Doc fromJson = Doc.FromJson(annotated.ToJson());
        Check("json round trip", fromJson.Length == annotated.Length && fromJson[0].Pos == "PROPN" && fromJson[1].Norm == "lovelace" && fromJson.Ents[0].Confidence == 0.75f && fromJson[2].Confidence == 0.5f);
        Doc fromBytes = Doc.FromBytes(annotated.ToBytes());
        Check("binary round trip", fromBytes.ToJson() == annotated.ToJson() && fromBytes[5].HeadIndex == 0);
        var bin = new DocBin();
        bin.Add(annotated);
        bin.Add(Nlp.Analyze("second"));
        DocBin binBack = DocBin.FromBytes(bin.ToBytes());
        int streamed = 0;
        var binReader = new DocBinReader(new MemoryStream(bin.ToBytes()));
        while (binReader.TryRead(out _))
        {
            streamed++;
        }

        Check("docbin", binBack.Count == 2 && streamed == 2);
        Check("hostile input", Throws<InvalidDataException>(() => Doc.FromBytes(new byte[] { 1, 2, 3 })) && Throws<InvalidDataException>(() => Doc.FromJson("{")));

        TrainSaveLoad();

        Console.WriteLine(failures == 0 ? "OK" : $"{failures} check(s) failed");
        return failures == 0 ? 0 : 1;
    }

    // Reads CoNLL-U, trains a tagger with Train, saves the pipeline, loads it back and compares predictions, all under NativeAOT.
    private static void TrainSaveLoad()
    {
        var conllu = new System.Text.StringBuilder();
        for (int s = 0; s < 40; s++)
        {
            for (int i = 0; i < 6; i++)
            {
                int w = (s * 7 + i * 3) % 12;
                conllu.Append(i + 1).Append("\tw").Append(w).Append("\tw").Append(w).Append("\tX\tT").Append(w % 3).Append("\t_\t0\troot\t_\t_\n");
            }

            conllu.Append('\n');
        }

        List<Example> examples = [.. ConllUReader.Read(new StringReader(conllu.ToString())).Select(x => Example.FromReference(x.Doc))];
        using Pipeline trained = Pipeline.Blank(Language.English).Add(new Tagger(new HashEmbeddingEncoder(16, 256, 1)) { OwnsEncoder = true });
        TrainResult result = trained.Train(examples, new TrainOptions { Epochs = 4, BatchWords = 60, LearningRate = 0.02f, Seed = 3 });
        Check("train", result.Completed && result.Steps > 0);

        using var buffer = new MemoryStream();
        PipelineFile.Save(trained, buffer);
        using Pipeline loaded = PipelineFile.Load(new MemoryStream(buffer.ToArray()));
        Doc a = trained.Process("w1 w2 w3 w4 w5");
        Doc b = loaded.Process("w1 w2 w3 w4 w5");
        bool same = a.Length == b.Length && a.Length == 5;
        for (int i = 0; same && i < a.Length; i++)
        {
            same = a[i].Tag == b[i].Tag && a[i].Tag.Length > 0 && a[i].Confidence == b[i].Confidence;
        }

        Check("saved pipeline reproduces predictions", same);
        Check("hostile pipeline file", Throws<InvalidDataException>(() => PipelineFile.Load(new MemoryStream(new byte[] { 1, 2, 3 }))));
    }

    private static bool Throws<T>(Action action)
        where T : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (T)
        {
            return true;
        }
    }

    private static void Check(string name, bool ok)
    {
        if (!ok)
        {
            failures++;
            Console.Error.WriteLine("FAILED: " + name);
        }
    }

    private sealed class ShoutComponent(string suffix) : IPipelineComponent
    {
        public string Name => "shout";

        public void Process(Doc doc)
        {
            for (int i = 0; i < doc.Length; i++)
            {
                doc.SetAnnotation(TokenColumn.Lemma, i, doc[i].ToString() + suffix);
            }
        }
    }

    /// <summary>One token per space-separated word; the ID is the word's length.</summary>
    private sealed class SpaceTokenizer : Tokenizer
    {
        public override int VocabularySize => 0;

        public override string Decode(ReadOnlySpan<int> ids, bool skipSpecialTokens = true) => string.Join(' ', ids.ToArray());

        public override string? IdToToken(int id) => null;

        public override int? TokenToId(string token) => null;

        protected override TokenEncoding EncodeCore(ReadOnlySpan<char> text, bool addSpecialTokens, TruncationOptions? truncation)
        {
            var ids = new List<int>();
            var offsets = new List<TokenOffset>();
            var tokens = new List<string>();
            int i = 0;
            while (i < text.Length)
            {
                if (text[i] == ' ')
                {
                    i++;
                    continue;
                }

                int start = i;
                while (i < text.Length && text[i] != ' ')
                {
                    i++;
                }

                ids.Add(i - start);
                offsets.Add(new TokenOffset(start, i));
                tokens.Add(text[start..i].ToString());
            }

            return new TokenEncoding([.. ids], [.. tokens], [.. offsets], new bool[ids.Count]);
        }
    }
}
