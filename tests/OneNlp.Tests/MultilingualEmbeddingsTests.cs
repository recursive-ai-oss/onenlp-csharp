using RecursiveAI.OneModel.Graph;
using RecursiveAI.OneNlp.Models.Embeddings.Multilingual;
using RecursiveAI.OneNlp.Neural.Embeddings;
using RecursiveAI.OneNlp.Tokenizers;

namespace RecursiveAI.OneNlp.Tests;

// The shard-joining stream and the e5 prefix wrapper. The embedder is the random 2-layer BERT-style fixture, so these check plumbing, not the model.
public class MultilingualEmbeddingsTests
{
    private static string Dir => Path.Combine(AppContext.BaseDirectory, "testdata", "accurate");

    private static SentenceEmbedder Make(string prefix = "")
    {
        Tokenizer tok = Tokenizer.FromFile(Path.Combine(Dir, "tiny-bert.tokenizer.json"));
        Session session = Session.Load(Path.Combine(Dir, "tiny-bert.onnx"));
        return new SentenceEmbedder(session, tok, new EmbeddingConfig(PoolingMode.Mean, true, 512), padId: 0, prefix);
    }

    private static MemoryStream[] Parts(byte[] data, params int[] cuts)
    {
        var parts = new List<MemoryStream>();
        int at = 0;
        foreach (int c in cuts.Append(data.Length))
        {
            parts.Add(new MemoryStream(data[at..c]));
            at = c;
        }

        return [.. parts];
    }

    [Fact]
    public void ConcatenatedStreamReadsAcrossPartBoundaries()
    {
        byte[] data = [.. Enumerable.Range(0, 1000).Select(i => (byte)(i * 7 + i / 256))];
        using var s = new ConcatenatedStream(Parts(data, 100, 101, 640));
        Assert.Equal(1000, s.Length);
        var all = new byte[1000];
        int n = 0;
        while (n < all.Length)
        {
            int r = s.Read(all, n, Math.Min(97, all.Length - n));
            Assert.True(r > 0);
            n += r;
        }

        Assert.Equal(data, all);
        Assert.Equal(0, s.Read(all, 0, 10));
    }

    [Fact]
    public void ConcatenatedStreamSeeksAndReadsLargeBuffersInOneCall()
    {
        byte[] data = [.. Enumerable.Range(0, 500).Select(i => (byte)(i ^ (i >> 3)))];
        using var s = new ConcatenatedStream(Parts(data, 200, 350));
        var buf = new byte[300];
        Assert.Equal(300, s.Read(buf, 0, 300));
        Assert.Equal(data[..300], buf);
        s.Seek(-50, SeekOrigin.End);
        var tail = new byte[100];
        Assert.Equal(50, s.Read(tail, 0, 100));
        Assert.Equal(data[450..], tail[..50]);
        s.Position = 195;
        var mid = new byte[10];
        Assert.Equal(10, s.Read(mid, 0, 10));
        Assert.Equal(data[195..205], mid);
        s.Seek(10, SeekOrigin.Current);
        Assert.Equal(215, s.Position);
        Assert.Throws<NotSupportedException>(() => s.Write(buf, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => s.Seek(-1, SeekOrigin.Begin));
    }

    [Fact]
    public void ConcatenatedStreamDisposesTheParts()
    {
        MemoryStream[] parts = Parts(new byte[10], 5);
        new ConcatenatedStream(parts).Dispose();
        Assert.All(parts, p => Assert.False(p.CanRead));
    }

    [Fact]
    public void ShardedOnnxLoadsLikeTheWholeFile()
    {
        byte[] onnx = File.ReadAllBytes(Path.Combine(Dir, "tiny-bert.onnx"));
        using var joined = new ConcatenatedStream(Parts(onnx, onnx.Length / 3, onnx.Length / 3 * 2));
        using Stream tok = File.OpenRead(Path.Combine(Dir, "tiny-bert.tokenizer.json"));
        using SentenceEmbedder a = SentenceEmbedder.Load(joined, tok, new EmbeddingConfig(PoolingMode.Mean, true, 512));
        using SentenceEmbedder b = Make();
        Assert.Equal(b.Embed("hello world"), a.Embed("hello world"));
    }

    [Fact]
    public void E5PrefixesMatchAnEmbedderWithThatPrefix()
    {
        using var e5 = new E5Embedder(Make());
        using SentenceEmbedder query = Make("query: ");
        using SentenceEmbedder passage = Make("passage: ");
        string[] texts = ["hello world", "the cat sat", ""];
        Assert.Equal(query.Embed(texts[0]), e5.Embed(texts[0]));
        Assert.Equal(query.Embed(texts[0]), e5.EmbedQuery(texts[0]));
        Assert.Equal(passage.Embed(texts[0]), e5.EmbedPassage(texts[0]));
        Assert.Equal(query.EmbedBatch(texts), e5.EmbedBatch(texts));
        Assert.Equal(query.EmbedBatch(texts), e5.EmbedQueries(texts));
        Assert.Equal(passage.EmbedBatch(texts), e5.EmbedPassages(texts));
        Assert.Equal(query.EmbedBatch(texts), e5.EmbedMany(texts).ToArray());
        Assert.Equal(query.Dimensions, e5.Dimensions);
        // The fixture's tokenizer must see the two prefixes differently, or the checks above could not tell them apart.
        Assert.NotEqual(query.TokenIds("x"), passage.TokenIds("x"));
    }

    [Fact]
    public void E5RejectsNulls()
    {
        using var e5 = new E5Embedder(Make());
        Assert.Throws<ArgumentNullException>(() => e5.EmbedQuery(null!));
        Assert.Throws<ArgumentNullException>(() => e5.EmbedPassages(["a", null!]));
        Assert.Throws<ArgumentNullException>(() => new E5Embedder(null!));
    }
}
