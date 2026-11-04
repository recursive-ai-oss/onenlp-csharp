using System.Runtime.CompilerServices;
using System.Text.Json;
using RecursiveAI.OneNlp.Parity;
using RecursiveAI.OneNlp.Tokenizers;

namespace RecursiveAI.OneNlp.Tests.Parity;

/// <summary>
/// Proves the parity harness on a toy tokenizer: it passes when the tokenizer matches the golden file the reference library wrote
/// for <c>toy/tokenizer.json</c>, and it reports each kind of injected defect.
/// </summary>
public class ToyParityTests
{
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Parity", "toy", name);

    private static FamilyResult Check(ToyFault fault) =>
        ParityChecker.Run("toy", new ToyCharTokenizer(fault), Fixture("toy.golden.jsonl.gz"), Fixture("corpus"), new ParityOptions { MaxExamples = 5, MaxTestCases = 5 });

    [Fact]
    public void ReferenceTokenizerMatchesTheGoldenFile()
    {
        FamilyResult r = Check(ToyFault.None);
        Assert.True(r.Passed, string.Join("\n", r.Examples.Select(m => $"{m.Check}.{m.Field} record {m.RecordId} ({m.Class}): {m.Detail}")));
        Assert.Equal(0, r.MismatchCount);
        Assert.True(r.Records > 500);
        Assert.True(r.Pairs > 0);
        Assert.True(r.Comparisons > 20000);
    }

    [Theory]
    [InlineData(ToyFault.WrongId, "encode.special.ids")]
    [InlineData(ToyFault.WrongOffset, "encode.special.offsets")]
    [InlineData(ToyFault.WrongOffset, "encode.utf8.special.offsets")]
    [InlineData(ToyFault.WrongSpecialMask, "encode.special.special_mask")]
    [InlineData(ToyFault.WrongDecode, "decode.skip.text")]
    [InlineData(ToyFault.WrongConsumed, "ids.char.special.small.consumed")]
    [InlineData(ToyFault.WrongTruncationSide, "encode.trunc.ids")]
    [InlineData(ToyFault.WrongPairTieBreak, "pair.trunc.ids")]
    public void InjectedDefectIsReported(ToyFault fault, string expectedField)
    {
        FamilyResult r = Check(fault);
        Assert.False(r.Passed);
        Assert.Contains(expectedField, r.ByField.Keys);
    }

    [Fact]
    public void FailuresComeWithReadyToCommitTestCases()
    {
        FamilyResult r = Check(ToyFault.WrongId);
        Assert.NotEmpty(r.TestCases);
        Assert.All(r.TestCases, m => Assert.Contains("Assert.Equal(", m.TestBody));
        // The shortest failing inputs come first.
        Assert.True(r.TestCases[0].InputLength <= 5);
    }

    [Fact]
    public void ReaderSkipsNothingAndRejectsAMisalignedCorpus()
    {
        string tmp = Path.Combine(Path.GetTempPath(), "parity-misaligned-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        try
        {
            // Dropping the first corpus record shifts every id, which must be reported rather than compared.
            string[] lines = File.ReadAllLines(Path.Combine(Fixture("corpus"), "records.jsonl"));
            File.WriteAllLines(Path.Combine(tmp, "records.jsonl"), lines.Skip(1));
            Assert.Throws<InvalidDataException>(() => ParityChecker.Run("toy", new ToyCharTokenizer(), Fixture("toy.golden.jsonl.gz"), tmp));
        }
        finally
        {
            Directory.Delete(tmp, true);
        }
    }

    [Fact]
    public void DigestsMatchThePythonDefinition()
    {
        // sha256 of int32 little-endian 1, 2, 3 and of the strings "a", "bc" with int32 length prefixes, as computed by
        // tools/parity/common.py with hashlib.
        Assert.Equal((3, "4636993d3e1da4e9d6b8f87b79e8f7c6d018580d52661950eabc3845c5897a4d"), Digests.OfInts([1, 2, 3]));
        Assert.Equal((2, "47ea6f805c5b663e33012cd34184e1398ad81a0dcea50071346f05d26692e843"), Digests.OfStrings(["a", "bc"]));
    }
}

/// <summary>Loads a family's tokenizer for the tests the harness writes; skips when the model files are not available.</summary>
public static class ParityTokenizers
{
    /// <summary>Loads <paramref name="familyId"/> from the directory named by <c>ONENLP_TOKENIZER_MODELS</c>.</summary>
    public static Tokenizer Load(string familyId, [CallerFilePath] string thisFile = "")
    {
        string? models = Environment.GetEnvironmentVariable("ONENLP_TOKENIZER_MODELS");
        if (string.IsNullOrEmpty(models))
        {
            Assert.Skip("Set ONENLP_TOKENIZER_MODELS to the directory made by tools/fetch/fetch_tokenizers.py.");
        }

        string families = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", "..", "tools", "parity", "families.json"));
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(families));
        foreach (JsonElement f in doc.RootElement.GetProperty("families").EnumerateArray())
        {
            if (f.GetProperty("id").GetString() == familyId)
            {
                string path = Path.Combine(models!, f.GetProperty("dir").GetString()!, f.GetProperty("files").GetProperty("primary").GetString()!);
                return TokenizerFiles.Load(path);
            }
        }

        throw new ArgumentException($"No family '{familyId}' in {families}.");
    }
}
