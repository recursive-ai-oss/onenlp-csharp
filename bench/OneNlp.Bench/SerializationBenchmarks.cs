using BenchmarkDotNet.Attributes;
using RecursiveAI.OneNlp;
using RecursiveAI.OneNlp.Serialization;

namespace RecursiveAI.OneNlp.Bench;

/// <summary>
/// Serialize and deserialize about 100,000 annotated tokens (1,000 documents of 100 tokens) in each format. Every column is set, as a
/// full pipeline leaves them.
/// </summary>
[MemoryDiagnoser]
public class SerializationBenchmarks
{
    private Doc[] _docs = [];
    private string[] _json = [];
    private byte[][] _binary = [];
    private byte[] _bin = [];

    [GlobalSetup]
    public void Setup()
    {
        var store = new StringStore();
        var rng = new Random(1);
        string[] words = ["the", "of", "Lovelace", "wrote", "notes", "in", "London", "and", "analysis", "engine", ",", ".", "café", "1843"];
        string[] tags = ["DT", "IN", "NNP", "VBD", "NNS", "CC", ",", "."];
        string[] deps = ["det", "prep", "nsubj", "ROOT", "dobj", "pobj", "punct", "cc"];
        _docs = new Doc[1000];
        for (int d = 0; d < _docs.Length; d++)
        {
            string text = string.Join(' ', Enumerable.Range(0, 100).Select(_ => words[rng.Next(words.Length)]));
            Doc doc = Nlp.Analyze(text);
            var heads = new int[doc.Length];
            for (int i = 0; i < doc.Length; i++)
            {
                doc.SetAnnotation(TokenColumn.Norm, i, doc[i].ToString().ToLowerInvariant());
                doc.SetAnnotation(TokenColumn.Lemma, i, doc[i].ToString().ToLowerInvariant());
                doc.SetAnnotation(TokenColumn.Tag, i, tags[rng.Next(tags.Length)]);
                doc.SetAnnotation(TokenColumn.Pos, i, "X");
                doc.SetAnnotation(TokenColumn.Dep, i, deps[rng.Next(deps.Length)]);
                doc.SetAnnotation(TokenColumn.Morph, i, "Number=Sing");
                heads[i] = rng.Next(doc.Length);
                doc.SetConfidence(i, (float)rng.NextDouble());
            }

            doc.SetHeads(heads);
            doc.SetSentenceStart(0, true);
            doc.SetEntities([new SpanSpec(2, 4, "PER", 0.9f), new SpanSpec(10, 11, "LOC", 0.8f)]);
            _docs[d] = doc;
        }

        _ = store;
        _json = [.. _docs.Select(d => d.ToJson())];
        _binary = [.. _docs.Select(d => d.ToBytes())];
        var bin = new DocBin();
        foreach (Doc d in _docs)
        {
            bin.Add(d);
        }

        _bin = bin.ToBytes();
        int tokens = _docs.Sum(d => d.Length);
        Console.WriteLine($"tokens {tokens}; bytes/token: json {_json.Sum(s => System.Text.Encoding.UTF8.GetByteCount(s)) / (double)tokens:F1}, binary {_binary.Sum(b => b.Length) / (double)tokens:F1}, docbin {_bin.Length / (double)tokens:F1}");
    }

    [Benchmark]
    public int JsonWrite() => _docs.Sum(d => d.ToJsonUtf8().Length);

    [Benchmark]
    public int JsonRead() => _json.Sum(s => Doc.FromJson(s).Length);

    [Benchmark]
    public int BinaryWrite() => _docs.Sum(d => d.ToBytes().Length);

    [Benchmark]
    public int BinaryRead() => _binary.Sum(b => Doc.FromBytes(b).Length);

    [Benchmark]
    public int DocBinWrite()
    {
        var bin = new DocBin();
        foreach (Doc d in _docs)
        {
            bin.Add(d);
        }

        return bin.ToBytes().Length;
    }

    [Benchmark]
    public int DocBinRead() => DocBin.FromBytes(_bin).Count;
}
