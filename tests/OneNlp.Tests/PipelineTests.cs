using System.Collections.Concurrent;
using RecursiveAI.OneNlp;

namespace RecursiveAI.OneNlp.Tests;

internal sealed class RecordingComponent(string name, List<string> log) : IPipelineComponent, IDisposable
{
    public string Name => name;

    public int BatchCalls { get; private set; }

    public bool Disposed { get; private set; }

    public void Process(Doc doc) => log.Add($"{name}:{doc.Text}");

    public void ProcessBatch(ReadOnlySpan<Doc> docs)
    {
        BatchCalls++;
        log.Add($"{name}:batch{docs.Length}");
        foreach (Doc doc in docs)
        {
            doc.SetAnnotation(TokenColumn.Tag, 0, name);
        }
    }

    public void Dispose() => Disposed = true;
}

public class PipelineTests
{
    [Fact]
    public void ComponentsRunInTheOrderAdded()
    {
        var log = new List<string>();
        using Pipeline pipeline = Pipeline.Blank(Language.German)
            .Add(new RecordingComponent("a", log))
            .Add(new RecordingComponent("b", log))
            .Add(new RecordingComponent("c", log));
        Doc doc = pipeline.Process("Hallo Welt");
        Assert.Equal(["a:Hallo Welt", "b:Hallo Welt", "c:Hallo Welt"], log);
        Assert.Equal(["a", "b", "c"], pipeline.Components.Select(c => c.Name));
        Assert.Equal(2, doc.Length);
        Assert.Equal(Language.German, pipeline.Language);
        Assert.Same(pipeline.Strings, doc.Strings);
    }

    [Fact]
    public void DuplicateNamesAreRejected()
    {
        var log = new List<string>();
        using Pipeline pipeline = Pipeline.Blank().Add(new RecordingComponent("a", log));
        Assert.Throws<ArgumentException>(() => pipeline.Add(new RecordingComponent("a", log)));
    }

    [Fact]
    public void ProcessManyBatchesInOrderAndStreamsLazily()
    {
        var log = new List<string>();
        var component = new RecordingComponent("a", log);
        using Pipeline pipeline = Pipeline.Blank().Add(component);
        int pulled = 0;

        IEnumerable<string> Texts()
        {
            for (int i = 0; i < 5; i++)
            {
                pulled++;
                yield return $"text {i}";
            }
        }

        using IEnumerator<Doc> docs = pipeline.ProcessMany(Texts(), batchSize: 2).GetEnumerator();
        Assert.Equal(0, pulled);
        Assert.True(docs.MoveNext());
        Assert.Equal(2, pulled);
        Assert.Equal("text 0", docs.Current.Text);
        var rest = new List<string>();
        while (docs.MoveNext())
        {
            rest.Add(docs.Current.Text);
        }

        Assert.Equal(["text 1", "text 2", "text 3", "text 4"], rest);
        Assert.Equal(["a:batch2", "a:batch2", "a:batch1"], log);
        Assert.Equal(3, component.BatchCalls);
    }

    [Fact]
    public async Task ProcessManyAsyncMatchesTheSyncForm()
    {
        var log = new List<string>();
        using Pipeline pipeline = Pipeline.Blank().Add(new RecordingComponent("a", log));

        static async IAsyncEnumerable<string> Texts()
        {
            for (int i = 0; i < 5; i++)
            {
                await Task.Yield();
                yield return $"t{i}";
            }
        }

        var texts = new List<string>();
        await foreach (Doc doc in pipeline.ProcessManyAsync(Texts(), batchSize: 3))
        {
            texts.Add(doc.Text);
            Assert.Equal("a", doc[0].Tag);
        }

        Assert.Equal(["t0", "t1", "t2", "t3", "t4"], texts);
        Assert.Equal(["a:batch3", "a:batch2"], log);
    }

    [Fact]
    public void BatchSizeAndNullsAreChecked()
    {
        using Pipeline pipeline = Pipeline.Blank();
        Assert.Throws<ArgumentOutOfRangeException>(() => pipeline.ProcessMany(["a"], 0));
        Assert.Throws<ArgumentNullException>(() => pipeline.Process((string)null!));
        Assert.Throws<ArgumentException>(() => pipeline.ProcessMany([null!]).ToList());
        Assert.Empty(pipeline.ProcessMany([]));
    }

    [Fact]
    public void DefaultBatchImplementationCallsProcess()
    {
        var log = new List<string>();
        IPipelineComponent component = new DefaultBatchComponent(log);
        component.ProcessBatch([Nlp.Analyze("x"), Nlp.Analyze("y")]);
        Assert.Equal(["x", "y"], log);
    }

    private sealed class DefaultBatchComponent(List<string> log) : IPipelineComponent
    {
        public string Name => "d";

        public void Process(Doc doc) => log.Add(doc.Text);
    }

    [Fact]
    public void DisposingThePipelineDisposesComponents()
    {
        var component = new RecordingComponent("a", []);
        Pipeline.Blank().Add(component).Dispose();
        Assert.True(component.Disposed);
    }

    [Fact]
    public void ProcessIsSafeFromManyThreads()
    {
        using Pipeline pipeline = Pipeline.Blank().Add("norm");
        var results = new ConcurrentBag<string>();
        Parallel.For(0, 200, i => results.Add(pipeline.Process($"Word{i} OTHER").Last().Norm));
        Assert.Equal(200, results.Count);
        Assert.All(results, r => Assert.Equal("other", r));
    }

    [Fact]
    public void NlpAnalyzeTokenizesOnly()
    {
        Doc doc = Nlp.Analyze("Hello there.");
        Assert.Equal(["Hello", "there", "."], doc.Select(t => t.ToString()));
        Assert.All(doc, t => Assert.Equal("", t.Pos));
        Assert.Throws<ArgumentNullException>(() => Nlp.Analyze(null!));
    }
}

public class RegistryTests
{
    [Fact]
    public void NormIsBuiltInAndCreatesComponentsByName()
    {
        Assert.Contains("norm", ComponentRegistry.Names);
        using Pipeline pipeline = Pipeline.Blank().Add("norm");
        Doc doc = pipeline.Process("Hello WORLD");
        Assert.Equal(["hello", "world"], doc.Select(t => t.Norm));
    }

    [Fact]
    public void RegisteredFactoriesReceiveSettings()
    {
        string name = "settings-" + Guid.NewGuid().ToString("N");
        string? seen = null;
        ComponentRegistry.Register(name, settings =>
        {
            seen = settings.TryGetValue("k", out string? v) ? v : "none";
            return new RecordingComponent(name, []);
        });
        Assert.Equal(name, ComponentRegistry.Create(name, new Dictionary<string, string> { ["k"] = "v" }).Name);
        Assert.Equal("v", seen);
        ComponentRegistry.Create(name);
        Assert.Equal("none", seen);
        Assert.Throws<InvalidOperationException>(() => ComponentRegistry.Register(name, _ => new NormComponent()));
    }

    [Fact]
    public void UnknownNamesSayWhatIsRegistered()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => ComponentRegistry.Create("tagger"));
        Assert.Contains("'tagger'", ex.Message, StringComparison.Ordinal);
        Assert.Contains("norm", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Pipeline.Create", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StringStoreInternsAcrossThreads()
    {
        var store = new StringStore();
        Assert.Equal(0, store.Add(""));
        Assert.Equal("", store[0]);
        var ids = new int[1000];
        Parallel.For(0, 4000, i => ids[i % 1000] = store.Add("s" + (i % 1000)));
        Assert.Equal(1001, store.Count);
        for (int i = 0; i < 1000; i++)
        {
            Assert.Equal("s" + i, store[ids[i]]);
            Assert.Equal(ids[i], store.Add("s" + i));
            Assert.True(store.TryGetId("s" + i, out int found));
            Assert.Equal(ids[i], found);
        }

        Assert.False(store.TryGetId("absent", out _));
        Assert.Equal(1001, store.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => store[1001]);
        Assert.Throws<ArgumentOutOfRangeException>(() => store[-1]);
        Assert.Throws<ArgumentNullException>(() => store.Add(null!));
    }

    [Fact]
    public void LanguageCodes()
    {
        Assert.Equal("en", Language.English.Code);
        Assert.Equal(Language.German, Language.FromCode("DE"));
        Assert.Equal("", Language.Unknown.Code);
        Assert.Equal(Language.Unknown, Language.FromCode(""));
        Assert.Equal("fil", Language.FromCode("fil").ToString());
        Assert.Throws<ArgumentException>(() => Language.FromCode("english"));
        Assert.Throws<ArgumentException>(() => Language.FromCode("e1"));
        Assert.True(Language.French != Language.Spanish);
    }
}
