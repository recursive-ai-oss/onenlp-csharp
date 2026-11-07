namespace RecursiveAI.OneNlp.Tests;

/// <summary>Pipeline.Create, Add&lt;T&gt;() and tier resolution, with fake packages for languages no real package registers.</summary>
public class PipelineCreateTests
{
    private sealed class Marker(string name) : IPipelineComponent, IDisposable
    {
        public bool Disposed { get; private set; }

        public string Name { get; } = name;

        public void Process(Doc doc)
        {
        }

        public void Dispose() => Disposed = true;
    }

    private sealed class Other : IPipelineComponent
    {
        public string Name => "other";

        public void Process(Doc doc)
        {
        }
    }

    private static Func<Pipeline> Package(Language language, Marker marker) =>
        () => Pipeline.Blank(language).Add(marker).Add(new Other());

    [Fact]
    public void AddTakesTheComponentOfThatTypeInTheOrderAsked()
    {
        var language = Language.FromCode("is");
        var marker = new Marker("marker");
        ModelPackages.RegisterQuietly(language, "Test.Models.Icelandic", Package(language, marker));
        using Pipeline pipeline = Pipeline.Create(language, Tier.Fast);
        Assert.Empty(pipeline.Components);
        pipeline.Add<Other>().Add<Marker>();
        Assert.Collection(pipeline.Components, c => Assert.IsType<Other>(c), c => Assert.Same(marker, c));
        Assert.Equal(language, pipeline.Language);
    }

    [Fact]
    public void AddOfAMissingOrTakenComponentNamesWhatIsLeft()
    {
        var language = Language.FromCode("fo");
        ModelPackages.RegisterQuietly(language, "Test.Models.Faroese", Package(language, new Marker("marker")));
        using Pipeline pipeline = Pipeline.Create(language);
        pipeline.Add<Marker>();
        InvalidOperationException again = Assert.Throws<InvalidOperationException>(() => pipeline.Add<Marker>());
        Assert.Contains("Test.Models.Faroese", again.Message, StringComparison.Ordinal);
        Assert.Contains("already added", again.Message, StringComparison.Ordinal);
        Assert.Contains("Other", again.Message, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => Pipeline.Blank().Add<Marker>());
    }

    [Fact]
    public void DisposeReleasesComponentsThatWereNeverAdded()
    {
        var language = Language.FromCode("mt");
        var marker = new Marker("marker");
        ModelPackages.RegisterQuietly(language, "Test.Models.Maltese", Package(language, marker));
        Pipeline.Create(language).Dispose();
        Assert.True(marker.Disposed);
    }

    [Fact]
    public void CreateLoadsANewCopyEachTimeAndNeverTheSharedPipeline()
    {
        var language = Language.FromCode("lb");
        ModelPackages.RegisterQuietly(language, "Test.Models.Luxembourgish", Package(language, new Marker("marker")));
        using Pipeline a = Pipeline.Create(language);
        using Pipeline b = Pipeline.Create(language);
        Assert.NotSame(a, b);
        Assert.NotSame(a.Tokenizer, ModelPackages.Get(language).Tokenizer);
    }

    [Fact]
    public void AccurateTierPrefersTheHigherPriorityAndFastFallsBackToIt()
    {
        var language = Language.FromCode("sq");
        ModelPackages.RegisterQuietly(language, Tier.Accurate, "Test.Multilingual", Package(language, new Marker("m1")));
        ModelPackages.RegisterQuietly(language, Tier.Accurate, "Test.Multilingual.Large", Package(language, new Marker("m2")), priority: 1);
        using Pipeline accurate = Pipeline.Create(language, Tier.Accurate);
        Assert.Equal("m2", accurate.Add<Marker>().Components.OfType<Marker>().Single().Name);
        using Pipeline fast = Pipeline.Create(language, Tier.Fast);
        Assert.Equal("m2", fast.Add<Marker>().Components.OfType<Marker>().Single().Name);
        Assert.True(ModelPackages.IsRegistered(language));
    }

    [Fact]
    public void AccurateTierNeverFallsBackToTheFastModelAndTheErrorNamesThePackage()
    {
        var language = Language.FromCode("eu");
        ModelPackages.RegisterQuietly(language, "Test.Models.Basque", Package(language, new Marker("marker")));
        ModelNotAvailableException e = Assert.Throws<ModelNotAvailableException>(() => Pipeline.Create(language, Tier.Accurate));

        // No published package has accurate-tier heads for Basque or Yoruba, so none is named.
        Assert.Empty(e.PackageIds);
        Assert.Contains("'eu'", e.Message, StringComparison.Ordinal);
        ModelNotAvailableException none = Assert.Throws<ModelNotAvailableException>(() => Pipeline.Create(Language.FromCode("yo"), Tier.Fast));
        Assert.Empty(none.PackageIds);
        ModelNotAvailableException covered = Assert.Throws<ModelNotAvailableException>(() => Pipeline.Create(Language.Turkish, Tier.Accurate));
        Assert.Contains("RecursiveAI.OneNlp.Models.Multilingual", covered.PackageIds);
    }
}
