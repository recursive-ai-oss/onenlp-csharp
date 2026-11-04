using BenchmarkDotNet.Attributes;
using RecursiveAI.OneNlp.Matching;

namespace RecursiveAI.OneNlp.Bench;

/// <summary>
/// Phrase matching at list scale: 100,000 phrases of one to three words against about 10 MB of text, already split into tokens, so
/// the row measures the matcher and not the tokenizer. The text and phrases are synthetic, drawn from a Zipf-like vocabulary with a fixed seed.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class PhraseMatcherBenchmarks
{
    private const int VocabularySize = 40_000;
    private const int PhraseCount = 100_000;
    private const int DocumentCount = 800;
    private const int TokensPerDocument = 2_000;

    private Doc[] _docs = [];
    private PhraseMatcher _orth = new();
    private PhraseMatcher _lower = new(TokenProperty.Lower);

    /// <summary>The size of the matched text in bytes, for turning times into throughput.</summary>
    public long TextBytes { get; private set; }

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(20261104);
        string[] vocabulary = new string[VocabularySize];
        for (int i = 0; i < vocabulary.Length; i++)
        {
            vocabulary[i] = (i % 3 == 0 ? "W" : "w") + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        // Zipf-like draw: the rank is the square of a uniform number, so low ranks are frequent.
        string Word()
        {
            double u = random.NextDouble();
            return vocabulary[(int)(u * u * (VocabularySize - 1))];
        }

        var phrases = new List<string>(PhraseCount);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (phrases.Count < PhraseCount)
        {
            string phrase = string.Join(' ', Enumerable.Range(0, random.Next(1, 4)).Select(_ => Word()));
            if (seen.Add(phrase))
            {
                phrases.Add(phrase);
            }
        }

        _orth = new PhraseMatcher();
        _orth.Add("P", phrases);
        _lower = new PhraseMatcher(TokenProperty.Lower);
        _lower.Add("P", phrases);

        var tokenizer = new RuleWordTokenizer();
        var strings = new StringStore();
        _docs = new Doc[DocumentCount];
        for (int d = 0; d < _docs.Length; d++)
        {
            string text = string.Join(' ', Enumerable.Range(0, TokensPerDocument).Select(_ => Word()));
            TextBytes += System.Text.Encoding.UTF8.GetByteCount(text);
            _docs[d] = tokenizer.Tokenize(text, strings);
        }

        Console.WriteLine($"phrase-matcher input: {PhraseCount} phrases, {_docs.Sum(d => d.Length)} tokens, {TextBytes} bytes of text");
    }

    [Benchmark]
    public int MatchOrth()
    {
        int n = 0;
        foreach (Doc doc in _docs)
        {
            n += _orth.Match(doc).Count;
        }

        return n;
    }

    [Benchmark]
    public int MatchLower()
    {
        int n = 0;
        foreach (Doc doc in _docs)
        {
            n += _lower.Match(doc).Count;
        }

        return n;
    }
}
