using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using RecursiveAI.OneNlp;

namespace RecursiveAI.OneNlp.Bench;

/// <summary>Placeholder suite so the harness builds; real rows are added with the components they measure.</summary>
[MemoryDiagnoser]
public class AnalyzeBenchmarks
{
    private const string Text = "The quick brown fox jumps over the lazy dog, then rests. It's 3.14 o'clock.";

    [Benchmark]
    public Doc Analyze() => Nlp.Analyze(Text);
}

internal static class Program
{
    private static void Main(string[] args) => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
