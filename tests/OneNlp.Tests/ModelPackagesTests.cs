namespace RecursiveAI.OneNlp.Tests;

public class ModelPackagesTests
{
    // These tests use languages no real model package registers for, because registration is process-wide.

    [Fact]
    public void RegisteredFactoryRunsOnceAndOnDemand()
    {
        var irish = Language.FromCode("ga");
        int calls = 0;
        ModelPackages.Register(irish, "Test.Models.Irish", () =>
        {
            Interlocked.Increment(ref calls);
            return Pipeline.Blank(irish);
        });
        Assert.True(ModelPackages.IsRegistered(irish));
        Assert.Equal(0, calls);

        Pipeline first = ModelPackages.Get(irish);
        Pipeline second = ModelPackages.Get(irish);
        Assert.Same(first, second);
        Assert.Equal(1, calls);

        // The same package may register again (two projects can both add the registration file); a different one may not.
        ModelPackages.Register(irish, "Test.Models.Irish", () => throw new InvalidOperationException("never called"));
        Assert.Same(first, ModelPackages.Get(irish));
        Assert.Throws<InvalidOperationException>(() => ModelPackages.Register(irish, "Other.Package", () => Pipeline.Blank(irish)));
    }

    [Fact]
    public void ConcurrentFirstUseLoadsOnce()
    {
        var welsh = Language.FromCode("cy");
        int calls = 0;
        ModelPackages.Register(welsh, "Test.Models.Welsh", () =>
        {
            Interlocked.Increment(ref calls);
            Thread.Sleep(20);
            return Pipeline.Blank(welsh);
        });
        Pipeline[] got = new Pipeline[8];
        Parallel.For(0, got.Length, i => got[i] = ModelPackages.Get(welsh));
        Assert.Equal(1, calls);
        Assert.All(got, p => Assert.Same(got[0], p));
    }

    [Fact]
    public void MissingModelNamesThePackageToAdd()
    {
        InvalidOperationException english = Assert.Throws<InvalidOperationException>(() => ModelPackages.Get(Language.English));
        Assert.Contains("RecursiveAI.OneNlp.Models.English", english.Message, StringComparison.Ordinal);
        Assert.Contains("RecursiveAI.OneNlp.Models.Multilingual", english.Message, StringComparison.Ordinal);
        InvalidOperationException other = Assert.Throws<InvalidOperationException>(() => ModelPackages.Get(Language.FromCode("sw")));
        Assert.Contains("RecursiveAI.OneNlp.Models.Multilingual", other.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Models.English", other.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ModelVerbsFailWithoutAPackageButAnalyzeStillTokenizes()
    {
        // The test project references no model package, so English has none.
        Assert.Throws<InvalidOperationException>(() => Nlp.Entities("Ada lives in London."));
        Assert.Throws<InvalidOperationException>(() => Nlp.Lemmas("cats"));
        Assert.Throws<ArgumentNullException>(() => Nlp.Entities(null!));
        Doc doc = Nlp.Analyze("Ada lives in London. She is happy.");
        Assert.Equal(2, doc.Sentences.Count);
        Assert.Equal(2, Nlp.AnalyzeMany(["One two.", "Three."]).Count());
    }

    [Fact]
    public void UnknownLanguageCannotBeRegistered() =>
        Assert.Throws<ArgumentException>(() => ModelPackages.Register(Language.Unknown, "Test.Package", () => Pipeline.Blank()));

    [Fact]
    public void AFallbackServesALanguageOnlyWhenNoRegularPackageDoes()
    {
        var latvian = Language.FromCode("lv");
        int fallbackCalls = 0;
        ModelPackages.RegisterFallback(latvian, "Test.Models.Large", () =>
        {
            Interlocked.Increment(ref fallbackCalls);
            return Pipeline.Blank(latvian);
        });
        Assert.True(ModelPackages.IsRegistered(latvian));
        Pipeline viaFallback = ModelPackages.Get(latvian);
        Assert.Equal(1, fallbackCalls);

        // A fast-tier package registered later wins, and the fallback is not asked again.
        var lithuanian = Language.FromCode("lt");
        ModelPackages.RegisterFallback(lithuanian, "Test.Models.Large", () => throw new InvalidOperationException("the fallback must not load"));
        Pipeline fast = Pipeline.Blank(lithuanian);
        ModelPackages.Register(lithuanian, "Test.Models.Lithuanian", () => fast);
        Assert.Same(fast, ModelPackages.Get(lithuanian));
        Assert.Same(viaFallback, ModelPackages.Get(latvian));
        Assert.Throws<ArgumentException>(() => ModelPackages.RegisterFallback(Language.Unknown, "Test.Models.Large", () => Pipeline.Blank()));
    }
}
