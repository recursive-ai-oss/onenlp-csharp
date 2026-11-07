using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using RecursiveAI.OneNlp.Serialization;

namespace RecursiveAI.OneNlp.Tests;

public class SerializationTests
{
    // ---- random documents with every column set ----

    private static readonly string[] Words =
    [
        "the", "Ada", "wrote", "café", "日本語", "😀", "e\u0301", "3.14", "don't", "—", "Zürich", "👨‍👩‍👧", "x", "A", "\u00A0",
    ];

    internal static Doc RandomDoc(Random rng, bool allowLoneSurrogates, StringStore? store = null)
    {
        store ??= new StringStore();
        int n = rng.Next(0, 40);
        var text = new StringBuilder();
        var bounds = new List<TokenBoundary>();
        if (rng.Next(4) == 0)
        {
            text.Append(' ', rng.Next(3));
        }

        for (int i = 0; i < n; i++)
        {
            string w = Words[rng.Next(Words.Length)];
            if (allowLoneSurrogates && rng.Next(10) == 0)
            {
                w = rng.Next(2) == 0 ? "\uD800x" : "y\uDC00";
            }

            if (w == "\u00A0")
            {
                w = "z";
            }

            bounds.Add(new TokenBoundary(text.Length, w.Length));
            text.Append(w);
            text.Append(' ', rng.Next(3));
        }

        var doc = new Doc(store, text.ToString(), bounds.ToArray());
        string Label(string prefix) => rng.Next(5) == 0 ? string.Empty : prefix + rng.Next(6) + (rng.Next(8) == 0 ? "é😀" : string.Empty);
        foreach (TokenColumn c in Enum.GetValues<TokenColumn>())
        {
            if (rng.Next(6) == 0)
            {
                continue;
            }

            for (int i = 0; i < n; i++)
            {
                doc.SetAnnotation(c, i, Label(c.ToString()));
            }
        }

        if (n > 0 && rng.Next(4) != 0)
        {
            var heads = new int[n];
            for (int i = 0; i < n; i++)
            {
                heads[i] = rng.Next(3) == 0 ? i : rng.Next(n);
            }

            doc.SetHeads(heads);
        }

        if (rng.Next(3) != 0)
        {
            var b = new SentenceBoundary[n];
            bool partial = rng.Next(3) == 0;
            for (int i = 0; i < n; i++)
            {
                b[i] = partial && rng.Next(2) == 0 ? SentenceBoundary.Unset : (rng.Next(4) == 0 ? SentenceBoundary.Start : SentenceBoundary.Inside);
            }

            doc.SetSentenceBoundaries(b);
        }

        if (rng.Next(3) != 0)
        {
            var conf = new float[n];
            for (int i = 0; i < n; i++)
            {
                conf[i] = rng.Next(4) switch
                {
                    0 => 1f,
                    1 => 0f,
                    2 => (float)rng.NextDouble(),
                    _ => BitConverter.UInt32BitsToSingle(0x3F800000u - (uint)rng.Next(1, 1 << 20)),
                };
            }

            doc.SetConfidences(conf);
        }

        if (n > 0 && rng.Next(4) != 0)
        {
            var specs = new List<SpanSpec>();
            for (int i = 0; i < n;)
            {
                if (rng.Next(3) == 0)
                {
                    int len = rng.Next(1, Math.Min(4, n - i + 1));
                    specs.Add(new SpanSpec(i, i + len, "L" + rng.Next(4) + (rng.Next(9) == 0 ? "ß" : string.Empty), (float)rng.NextDouble()));
                    i += len;
                }
                else
                {
                    i++;
                }
            }

            doc.SetEntities(specs.ToArray());
        }

        return doc;
    }

    internal static void AssertSame(Doc a, Doc b)
    {
        Assert.Equal(a.Text, b.Text);
        Assert.Equal(a.Length, b.Length);
        for (int i = 0; i < a.Length; i++)
        {
            Token x = a[i], y = b[i];
            Assert.Equal(x.Idx, y.Idx);
            Assert.Equal(x.Length, y.Length);
            Assert.Equal(x.HasSpaceAfter, y.HasSpaceAfter);
            Assert.Equal(x.Norm, y.Norm);
            Assert.Equal(x.Lemma, y.Lemma);
            Assert.Equal(x.Pos, y.Pos);
            Assert.Equal(x.Tag, y.Tag);
            Assert.Equal(x.Dep, y.Dep);
            Assert.Equal(x.Morph, y.Morph);
            Assert.Equal(x.HeadIndex, y.HeadIndex);
            Assert.Equal(x.IsSentStart, y.IsSentStart);
            Assert.Equal(x.EntIob, y.EntIob);
            Assert.Equal(x.EntType, y.EntType);
            Assert.Equal(BitConverter.SingleToUInt32Bits(x.Confidence), BitConverter.SingleToUInt32Bits(y.Confidence));
        }

        Assert.Equal(a.Ents.Count, b.Ents.Count);
        for (int i = 0; i < a.Ents.Count; i++)
        {
            Assert.Equal(a.Ents[i].Start, b.Ents[i].Start);
            Assert.Equal(a.Ents[i].End, b.Ents[i].End);
            Assert.Equal(a.Ents[i].Label, b.Ents[i].Label);
            Assert.Equal(BitConverter.SingleToUInt32Bits(a.Ents[i].Confidence), BitConverter.SingleToUInt32Bits(b.Ents[i].Confidence));
        }

        Assert.Equal(a.Sentences.Count, b.Sentences.Count);
    }

    [Fact]
    public void RandomDocsSurviveJson()
    {
        var rng = new Random(7);
        for (int k = 0; k < 600; k++)
        {
            Doc doc = RandomDoc(rng, allowLoneSurrogates: false);
            AssertSame(doc, Doc.FromJson(doc.ToJson()));
            AssertSame(doc, Doc.FromJson(doc.ToJsonUtf8()));
            using var ms = new MemoryStream();
            doc.WriteJson(ms);
            ms.Position = 0;
            AssertSame(doc, Doc.FromJson(ms));
        }
    }

    [Fact]
    public void RandomDocsSurviveBinaryBitForBit()
    {
        var rng = new Random(8);
        for (int k = 0; k < 600; k++)
        {
            Doc doc = RandomDoc(rng, allowLoneSurrogates: true);
            bool checksum = k % 2 == 0;
            byte[] bytes = doc.ToBytes(checksum);
            AssertSame(doc, Doc.FromBytes(bytes));
            Assert.True(doc.TryWriteBytes(new byte[bytes.Length], out int w, checksum) && w == bytes.Length);
            Assert.False(doc.TryWriteBytes(new byte[bytes.Length - 1], out _, checksum));

            // Same document, same bytes.
            Assert.Equal(bytes, Doc.FromBytes(bytes).ToBytes(checksum));
        }
    }

    [Fact]
    public void DocsWrittenBackToBackReadFromAStreamInTurn()
    {
        var rng = new Random(9);
        Doc[] docs = [.. Enumerable.Range(0, 20).Select(_ => RandomDoc(rng, true))];
        using var ms = new MemoryStream();
        foreach (Doc d in docs)
        {
            d.WriteBinary(ms);
        }

        ms.Position = 0;
        foreach (Doc d in docs)
        {
            AssertSame(d, Doc.FromStream(ms));
        }

        Assert.Equal(ms.Length, ms.Position);
    }

    [Fact]
    public void DocBinSharesStringsAndStreams()
    {
        var rng = new Random(10);
        var bin = new DocBin();
        for (int i = 0; i < 50; i++)
        {
            bin.Add(RandomDoc(rng, true));
        }

        byte[] bytes = bin.ToBytes();
        DocBin back = DocBin.FromBytes(bytes);
        Assert.Equal(bin.Count, back.Count);
        for (int i = 0; i < bin.Count; i++)
        {
            AssertSame(bin.Docs[i], back.Docs[i]);
        }

        // The bin is smaller than the same documents written one by one.
        Assert.True(bytes.Length < bin.Docs.Sum(d => d.ToBytes().Length));

        using var ms = new MemoryStream(bytes);
        var reader = new DocBinReader(ms, new StringStore());
        int count = 0;
        while (reader.TryRead(out Doc? d))
        {
            AssertSame(bin.Docs[count++], d);
        }

        Assert.Equal(bin.Count, count);

        Assert.Empty(DocBin.FromBytes(new DocBin().ToBytes()).Docs);
    }

    [Fact]
    public void ReadersShareTheCallersStringStore()
    {
        var store = new StringStore();
        Doc doc = Nlp.Analyze("one two");
        doc.SetAnnotation(TokenColumn.Pos, 0, "NUM");
        Doc back = Doc.FromBytes(doc.ToBytes(), store);
        Assert.Same(store, back.Strings);
        Assert.True(store.TryGetId("NUM", out _));
    }

    [Fact]
    public void JsonCannotCarryUnpairedSurrogatesButBinaryCan()
    {
        Doc doc = Nlp.Analyze("a \uD800 b");
        Assert.Throws<InvalidOperationException>(() => doc.ToJson());
        AssertSame(doc, Doc.FromBytes(doc.ToBytes()));
    }

    [Fact]
    public void JsonOffsetsCountCodePoints()
    {
        Doc doc = Nlp.Analyze("a 😀 b");
        string json = doc.ToJson();
        Assert.Contains("\"start\":2,\"end\":3", json);
        AssertSame(doc, Doc.FromJson(json));
    }

    // ---- hostile input ----

    private static byte[] SampleBytes(bool checksum)
    {
        Doc doc = RandomDoc(new Random(11), true);
        while (doc.Length < 5 || doc.Ents.Count == 0)
        {
            doc = RandomDoc(new Random(Random.Shared.Next()), true);
        }

        return doc.ToBytes(checksum);
    }

    private static void MustBeRejectedOrRead(Action read)
    {
        try
        {
            read();
        }
        catch (InvalidDataException)
        {
        }
    }

    [Fact]
    public void EveryTruncationOfADocumentIsRejected()
    {
        byte[] bytes = SampleBytes(true);
        for (int len = 0; len < bytes.Length; len++)
        {
            byte[] cut = bytes[..len];
            Assert.Throws<InvalidDataException>(() => Doc.FromBytes(cut));
            Assert.Throws<InvalidDataException>(() => Doc.FromStream(new MemoryStream(cut)));
        }
    }

    [Fact]
    public void EveryTruncationOfABinIsRejected()
    {
        var bin = new DocBin();
        var rng = new Random(12);
        for (int i = 0; i < 4; i++)
        {
            bin.Add(RandomDoc(rng, true));
        }

        byte[] bytes = bin.ToBytes();
        for (int len = 0; len < bytes.Length; len++)
        {
            byte[] cut = bytes[..len];
            Assert.Throws<InvalidDataException>(() => DocBin.FromBytes(cut));
        }
    }

    [Fact]
    public void ChecksumCatchesEveryFlippedByte()
    {
        byte[] bytes = SampleBytes(true);
        for (int i = 0; i < bytes.Length; i++)
        {
            byte[] bad = (byte[])bytes.Clone();
            bad[i] ^= 0x10;
            Assert.Throws<InvalidDataException>(() => Doc.FromBytes(bad));
        }
    }

    [Fact]
    public void FlippedBytesWithoutChecksumGiveADocOrADocumentedException()
    {
        byte[] bytes = SampleBytes(false);
        var rng = new Random(13);
        for (int k = 0; k < 20000; k++)
        {
            byte[] bad = (byte[])bytes.Clone();
            for (int f = rng.Next(1, 4); f > 0; f--)
            {
                bad[rng.Next(bad.Length)] = (byte)rng.Next(256);
            }

            MustBeRejectedOrRead(() => Doc.FromBytes(bad));
            MustBeRejectedOrRead(() => DocBin.FromBytes(bad));
        }
    }

    [Fact]
    public void HugeDeclaredLengthsAreRejectedWithoutAllocating()
    {
        byte[] header = new byte[12];
        "ONDC"u8.CopyTo(header);
        header[4] = 1;

        // A header that claims 4 GiB of body, then ends.
        header[8] = header[9] = header[10] = header[11] = 0xFF;
        Assert.Throws<InvalidDataException>(() => Doc.FromBytes(header));
        Assert.Throws<InvalidDataException>(() => Doc.FromStream(new MemoryStream(header)));

        // A string table that claims 2^31 strings in a few bytes.
        byte[] body = [1, 6, 0, 0, 0, 0xFF, 0xFF, 0xFF, 0xFF, 0x07];
        var doc = new byte[12 + body.Length];
        "ONDC"u8.CopyTo(doc);
        doc[4] = 1;
        doc[8] = (byte)body.Length;
        body.CopyTo(doc, 12);
        Assert.Throws<InvalidDataException>(() => Doc.FromBytes(doc));

        byte[] binHeader = new byte[12];
        "ONDB"u8.CopyTo(binHeader);
        binHeader[4] = 1;
        binHeader[8] = binHeader[9] = binHeader[10] = binHeader[11] = 0xFF;
        Assert.Throws<InvalidDataException>(() => DocBin.FromBytes(binHeader));
    }

    [Fact]
    public void WrongMagicVersionAndFlagsAreRejected()
    {
        byte[] bytes = SampleBytes(true);
        byte[] magic = (byte[])bytes.Clone();
        magic[0] = (byte)'X';
        byte[] version = (byte[])bytes.Clone();
        version[4] = 9;
        byte[] flags = (byte[])bytes.Clone();
        flags[6] = 0x80;
        Assert.Throws<InvalidDataException>(() => Doc.FromBytes(magic));
        Assert.Throws<InvalidDataException>(() => Doc.FromBytes(version));
        Assert.Throws<InvalidDataException>(() => Doc.FromBytes(flags));
        Assert.Throws<InvalidDataException>(() => Doc.FromBytes([.. bytes, 0]));
        Assert.Throws<InvalidDataException>(() => DocBin.FromBytes(bytes));
    }

    [Theory]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("{")]
    [InlineData("{\"text\":\"a\"}")]
    [InlineData("{\"text\":\"a\",\"tokens\":[{\"start\":0,\"end\":2}]}")]
    [InlineData("{\"text\":\"a\",\"tokens\":[{\"start\":0,\"end\":1,\"head\":5}]}")]
    [InlineData("{\"text\":\"a\",\"tokens\":[{\"start\":0,\"end\":1,\"id\":3}]}")]
    [InlineData("{\"text\":\"a\",\"tokens\":[{\"start\":0,\"end\":1,\"conf\":2}]}")]
    [InlineData("{\"text\":\"a b\",\"tokens\":[{\"start\":0,\"end\":1},{\"start\":0,\"end\":3}]}")]
    [InlineData("{\"text\":\"ab\",\"ents\":[{\"start\":0,\"end\":1,\"label\":\"X\"}],\"tokens\":[{\"start\":0,\"end\":2}]}")]
    [InlineData("{\"text\":\"a\",\"tokens\":[]} trailing")]
    [InlineData("{\"text\":1,\"tokens\":[]}")]
    [InlineData("{\"text\":\"a\",\"tokens\":[{\"start\":1.5,\"end\":2}]}")]
    public void MalformedJsonIsRejected(string json) => Assert.Throws<InvalidDataException>(() => Doc.FromJson(json));

    [Fact]
    public void DeeplyNestedJsonIsRejected()
    {
        string json = "{\"text\":\"\",\"tokens\":[],\"user_data\":" + new string('[', 5000) + new string(']', 5000) + "}";
        Assert.Throws<InvalidDataException>(() => Doc.FromJson(json));
    }

    [Fact]
    public void InvalidUtf8JsonIsRejected() =>
        Assert.Throws<InvalidDataException>(() => Doc.FromJson([.. "{\"text\":\""u8, 0xFF, 0xFE, .. "\",\"tokens\":[]}"u8]));

    [Fact]
    public void UnknownFieldsAreIgnored()
    {
        Doc doc = Doc.FromJson("{\"text\":\"a b\",\"cats\":{\"x\":0.5},\"spans\":{\"sc\":[{\"start\":0,\"end\":1,\"label\":\"Q\"}]},\"tokens\":[{\"id\":0,\"start\":0,\"end\":1,\"whitespace\":\" \"},{\"id\":1,\"start\":2,\"end\":3}]}");
        Assert.Equal(2, doc.Length);
    }

    [Fact]
    public void RandomJsonMutationsGiveADocOrADocumentedException()
    {
        var rng = new Random(14);
        byte[] seed = RandomDocJson(rng);
        for (int k = 0; k < 20000; k++)
        {
            byte[] bad = (byte[])seed.Clone();
            for (int f = rng.Next(1, 4); f > 0; f--)
            {
                bad[rng.Next(bad.Length)] = (byte)rng.Next(256);
            }

            MustBeRejectedOrRead(() => Doc.FromJson(bad));
            MustBeRejectedOrRead(() => Doc.FromJson(bad.AsSpan(0, rng.Next(bad.Length))));
        }
    }

    private static byte[] RandomDocJson(Random rng)
    {
        Doc doc = RandomDoc(rng, false);
        while (doc.Length < 5 || doc.Ents.Count == 0)
        {
            doc = RandomDoc(rng, false);
        }

        return doc.ToJsonUtf8();
    }

    // ---- interop with spaCy's Doc.to_json (black-box fixtures, see tools/oracles/spacy_json) ----

    private static string FixtureDir([CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "testdata", "serialization", "spacy"));

    private static JsonObject Normalize(string json)
    {
        JsonObject o = JsonNode.Parse(json)!.AsObject();
        foreach (string key in new[] { "ents", "sents" })
        {
            if (o[key] is JsonArray { Count: 0 })
            {
                o.Remove(key);
            }
        }

        foreach (JsonNode? t in o["tokens"]!.AsArray())
        {
            JsonObject to = t!.AsObject();
            foreach (string extra in new[] { "norm", "conf", "sb" })
            {
                to.Remove(extra);
            }
        }

        foreach (JsonNode? e in o["ents"]?.AsArray() ?? [])
        {
            e!.AsObject().Remove("conf");
        }

        return o;
    }

    [Fact]
    public void SpacyToJsonOutputReadsAndWritesBackTheSameShape()
    {
        string[] files = Directory.GetFiles(FixtureDir(), "*.spacy.json");
        Assert.NotEmpty(files);
        foreach (string file in files)
        {
            string original = File.ReadAllText(file);
            Doc doc = Doc.FromJson(original);
            Assert.True(JsonNode.DeepEquals(Normalize(original), Normalize(doc.ToJson())), Path.GetFileName(file));
        }
    }

    [Fact]
    public void OurJsonFixturesMatchWhatTheWriterProduces()
    {
        foreach (string file in Directory.GetFiles(FixtureDir(), "*.spacy.json"))
        {
            Doc doc = Doc.FromJson(File.ReadAllText(file));
            for (int i = 0; i < doc.Length; i++)
            {
                doc.SetAnnotation(TokenColumn.Norm, i, doc[i].ToString().ToLowerInvariant());
                if (i % 2 == 1)
                {
                    doc.SetConfidence(i, 0.75f);
                }
            }

            string ours = doc.ToJson() + "\n";
            string path = file.Replace(".spacy.json", ".ours.json", StringComparison.Ordinal);
            if (Environment.GetEnvironmentVariable("ONENLP_WRITE_FIXTURES") == "1")
            {
                File.WriteAllText(path, ours);
            }

            Assert.Equal(File.ReadAllText(path).ReplaceLineEndings("\n"), ours);
            AssertSame(doc, Doc.FromJson(ours));
        }
    }
}
