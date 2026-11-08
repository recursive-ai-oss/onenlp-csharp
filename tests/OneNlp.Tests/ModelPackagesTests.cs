namespace RecursiveAI.OneNlp.Tests;

public class ModelPackagesTests
{
    // These tests use languages no real model package registers for, because registration is process-wide.

    [Fact]
    public void RegisteredFactoryRunsOnceAndOnDemand()
    {
        var irish = Language.FromCode("ga");
        int calls = 0;
        ModelPackages.RegisterQuietly(irish, "Test.Models.Irish", () =>
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
        ModelPackages.RegisterQuietly(irish, "Test.Models.Irish", () => throw new InvalidOperationException("never called"));
        Assert.Same(first, ModelPackages.Get(irish));
        Assert.Throws<InvalidOperationException>(() => ModelPackages.RegisterQuietly(irish, "Other.Package", () => Pipeline.Blank(irish)));
    }

    [Fact]
    public void ConcurrentFirstUseLoadsOnce()
    {
        var welsh = Language.FromCode("cy");
        int calls = 0;
        ModelPackages.RegisterQuietly(welsh, "Test.Models.Welsh", () =>
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
        ModelNotAvailableException english = Assert.Throws<ModelNotAvailableException>(() => ModelPackages.Get(Language.English));
        Assert.Contains("RecursiveAI.OneNlp.Models.English", english.Message, StringComparison.Ordinal);
        Assert.Contains("RecursiveAI.OneNlp.Models.Multilingual", english.Message, StringComparison.Ordinal);
        ModelNotAvailableException other = Assert.Throws<ModelNotAvailableException>(() => ModelPackages.Get(Language.FromCode("sw")));
        Assert.DoesNotContain("Models.English", other.Message, StringComparison.Ordinal);
        Assert.Empty(other.PackageIds);
    }

    [Theory]
    [InlineData("cs", false)]
    [InlineData("de", true)]
    public void AccurateCoverageNamesOnlyPackagesWithHeads(string code, bool distilledCovers)
    {
        Language language = Language.FromCode(code);
        ModelNotAvailableException missing = Assert.Throws<ModelNotAvailableException>(() => Pipeline.Create(language, Tier.Accurate));
        string[] expected = distilledCovers
            ? ["RecursiveAI.OneNlp.Models.Multilingual.Large", "RecursiveAI.OneNlp.Models.Multilingual"]
            : ["RecursiveAI.OneNlp.Models.Multilingual.Large"];
        Assert.Equal(expected, missing.PackageIds);
        if (!distilledCovers)
        {
            ModelNotAvailableException fallback = Assert.Throws<ModelNotAvailableException>(() => ModelPackages.Get(language));
            Assert.Equal(expected, fallback.PackageIds);
        }
    }

    [Fact]
    public void ModelVerbsFailWithoutAPackageButAnalyzeStillTokenizes()
    {
        // The test project references no model package, so English has none.
        Assert.Throws<ModelNotAvailableException>(() => Nlp.Entities("Ada lives in London."));
        Assert.Throws<ModelNotAvailableException>(() => Nlp.Lemmas("cats"));
        Assert.Throws<ArgumentNullException>(() => Nlp.Entities(null!));
        Doc doc = Nlp.Analyze("Ada lives in London. She is happy.");
        Assert.Equal(2, doc.Sentences.Count);
        Assert.Equal(2, Nlp.AnalyzeMany(["One two.", "Three."]).Count());
    }

    [Fact]
    public void UnknownLanguageCannotBeRegistered() =>
        Assert.Throws<ArgumentException>(() => ModelPackages.RegisterQuietly(Language.Unknown, "Test.Package", () => Pipeline.Blank()));
}
