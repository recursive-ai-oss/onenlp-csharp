using System.Text;
using RecursiveAI.OneNlp.Tokenizers;
using RecursiveAI.OneNlp.Tokenizers.Hf;
using RecursiveAI.OneNlp.Tokenizers.Loaders;

namespace RecursiveAI.OneNlp.Tests;

/// <summary>Stages that do nothing, so that the loaders can be tested for the vocabulary they read without the real pipeline stages.</summary>
internal sealed class NopStage : INormalizer, IPreTokenizer, IPostProcessor, ITokenDecoder
{
    public void Normalize(NormalizedString text)
    {
    }

    public void PreTokenize(PreTokenizedString text)
    {
    }

    public int AddedTokenCount(bool isPair) => 0;

    public TokenEncoding Process(TokenEncoding encoding, TokenEncoding? pair, bool addSpecialTokens) => encoding;

    public List<string> DecodeChain(List<string> tokens) => tokens;

    internal static StageRegistry Registry()
    {
        var r = new StageRegistry();
        var nop = new NopStage();
        foreach (string t in (string[])["BertNormalizer", "Precompiled"])
        {
            r.AddNormalizer(t, _ => nop);
        }

        foreach (string t in (string[])["BertPreTokenizer", "Sequence", "Metaspace"])
        {
            r.AddPreTokenizer(t, _ => nop);
        }

        r.AddPostProcessor("TemplateProcessing", _ => nop);
        foreach (string t in (string[])["WordPiece", "Metaspace", "Sequence"])
        {
            r.AddDecoder(t, _ => nop);
        }

        return r;
    }
}

/// <summary>The <c>vocab.txt</c> and SentencePiece <c>.model</c> readers.</summary>
public class LoaderTests
{
    private static string Fixture(string name, [System.Runtime.CompilerServices.CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "testdata", "tokenizers", "models", name));

    [Fact]
    public void VocabTxtMatchesTheReferenceReader()
    {
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(Fixture("vocab-txt.json")));
        int checkedCases = 0;
        foreach (System.Text.Json.JsonElement c in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            string name = c.GetProperty("name").GetString()!;
            byte[] file = Convert.FromBase64String(c.GetProperty("file").GetString()!);
            bool reference = !c.TryGetProperty("error", out _);
            var expected = new Dictionary<string, int>();
            if (reference)
            {
                foreach (System.Text.Json.JsonProperty p in c.GetProperty("vocab").EnumerateObject())
                {
                    expected[p.Name] = p.Value.GetInt32();
                }
            }

            using var stream = new MemoryStream(file);
            if (!reference || !expected.ContainsKey("[UNK]"))
            {
                // The reference either rejects the file, or reads a vocabulary that a WordPiece tokenizer cannot use because it lacks [UNK].
                Assert.Throws<InvalidDataException>(() => WordPieceVocabularyFile.Load(stream, null, NopStage.Registry()));
            }
            else
            {
                Tokenizer t = WordPieceVocabularyFile.Load(stream, null, NopStage.Registry());
                Assert.True(expected.Count == t.VocabularySize, $"{name}: vocabulary size");
                foreach ((string token, int id) in expected)
                {
                    Assert.True(t.TokenToId(token) == id, $"{name}: id of '{token}'");
                    Assert.True(t.IdToToken(id) == token, $"{name}: token of {id}");
                }
            }

            checkedCases++;
        }

        Assert.True(checkedCases > 20);
    }

    [Fact]
    public void VocabTxtDecidesCaseFromTheVocabulary()
    {
        Assert.NotNull(WordPieceVocabularyFile.Load(new MemoryStream("[PAD]\n[UNK]\n[CLS]\n[SEP]\nhello\n##s\n"u8.ToArray()), null, NopStage.Registry()));
        Tokenizer t = WordPieceVocabularyFile.Load(new MemoryStream("[PAD]\n[UNK]\n[CLS]\n[SEP]\nHello\n"u8.ToArray()), null, NopStage.Registry());
        Assert.Equal(4, t.TokenToId("Hello"));
    }

    [Fact]
    public void VocabTxtRefusesInvalidUtf8()
    {
        Assert.Throws<InvalidDataException>(() => WordPieceVocabularyFile.Load(new MemoryStream([.. "[UNK]\n"u8.ToArray(), 0xC3]), null, NopStage.Registry()));
    }

    [Fact]
    public void SentencePieceModelLoadsPiecesAndSpecialSettings()
    {
        byte[] model = Proto.Model(
            [("<unk>", 0f, 2), ("<s>", 0f, 3), ("</s>", 0f, 3), ("\u2581a", -1.5f, 1), ("b", -2f, 1), ("<x>", 0f, 4)],
            modelType: 1, unkId: 0, byteFallback: false, charsmap: [1, 2, 3]);
        Tokenizer t = SentencePieceModelFile.Load(new MemoryStream(model), NopStage.Registry());
        Assert.Equal(6, t.VocabularySize);
        Assert.Equal(3, t.TokenToId("\u2581a"));
        Assert.Equal("b", t.IdToToken(4));
        Assert.Equal(5, t.TokenToId("<x>"));
    }

    [Fact]
    public void SentencePieceModelOfOtherTypesIsRefused()
    {
        byte[] bpe = Proto.Model([("<unk>", 0f, 2), ("a", -1f, 1)], modelType: 2, unkId: 0, byteFallback: false, charsmap: []);
        Assert.Throws<NotSupportedException>(() => SentencePieceModelFile.Load(new MemoryStream(bpe), NopStage.Registry()));
    }

    [Fact]
    public void SentencePieceModelRejectsEveryTruncationCleanly()
    {
        byte[] model = Proto.Model(
            [("<unk>", 0f, 2), ("\u2581a", -1.5f, 1), ("b", -2f, 1)], modelType: 1, unkId: 0, byteFallback: false, charsmap: [9, 9]);
        for (int cut = 0; cut < model.Length; cut++)
        {
            byte[] part = model[..cut];
            try
            {
                SentencePieceModelFile.Load(new MemoryStream(part), NopStage.Registry());
            }
            catch (InvalidDataException)
            {
            }
        }

        // A length that runs past the end, an over-long varint and a piece count far beyond the data.
        Assert.Throws<InvalidDataException>(() => SentencePieceProto.Read([0x0A, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F]));
        Assert.Throws<InvalidDataException>(() => SentencePieceProto.Read([0x0A, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x01]));
        Assert.Throws<InvalidDataException>(() => SentencePieceProto.Read([0x1B]));
        Assert.Throws<InvalidDataException>(() => SentencePieceProto.Read([]));
    }

    /// <summary>Writes the parts of a SentencePiece model message that the reader uses.</summary>
    private static class Proto
    {
        internal static byte[] Model((string Piece, float Score, int Type)[] pieces, int modelType, int unkId, bool byteFallback, byte[] charsmap)
        {
            var o = new List<byte>();
            foreach ((string piece, float score, int type) in pieces)
            {
                var body = new List<byte>();
                Bytes(body, 1, Encoding.UTF8.GetBytes(piece));
                body.Add((2 << 3) | 5);
                body.AddRange(BitConverter.GetBytes(score));
                Varint(body, 3, type);
                Bytes(o, 1, [.. body]);
            }

            var trainer = new List<byte>();
            Varint(trainer, 3, modelType);
            Varint(trainer, 35, byteFallback ? 1 : 0);
            Varint(trainer, 40, unkId);
            Bytes(o, 2, [.. trainer]);

            var norm = new List<byte>();
            Bytes(norm, 1, "nmt_nfkc"u8.ToArray());
            Bytes(norm, 2, charsmap);
            Bytes(o, 3, [.. norm]);
            return [.. o];
        }

        private static void Key(List<byte> o, int field, int wire) => Raw(o, (field << 3) | wire);

        private static void Varint(List<byte> o, int field, int value)
        {
            Key(o, field, 0);
            Raw(o, value);
        }

        private static void Bytes(List<byte> o, int field, byte[] value)
        {
            Key(o, field, 2);
            Raw(o, value.Length);
            o.AddRange(value);
        }

        private static void Raw(List<byte> o, int value)
        {
            uint v = (uint)value;
            while (v >= 0x80)
            {
                o.Add((byte)(v | 0x80));
                v >>= 7;
            }

            o.Add((byte)v);
        }
    }
}
