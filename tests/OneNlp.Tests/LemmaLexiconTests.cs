using System.Text.Json;
using RecursiveAI.OneNlp.Neural.Components;

namespace RecursiveAI.OneNlp.Tests;

public class LemmaLexiconTests
{
    [Fact]
    public void ReadsATableItWrote()
    {
        using JsonDocument document = JsonDocument.Parse("[\"walking\",\"VERB\",\"walk\",\"mice\",\"NOUN\",\"mouse\"]");
        LemmaLexicon lexicon = LemmaLexicon.Read(document.RootElement);
        Assert.Equal(2, lexicon.Count);
        Assert.True(lexicon.TryGet("mice", "NOUN", out string? lemma));
        Assert.Equal("mouse", lemma);
    }

    // Found by fuzzing: a string whose bytes are not valid UTF-8 made GetString throw InvalidOperationException out of Read,
    // which documents only InvalidDataException.
    [Fact]
    public void InvalidUtf8InAStringIsInvalidData()
    {
        byte[] json = [0x5b, 0x22, 0x77, 0x9f, 0x8b, 0x6b, 0x69, 0x6e, 0x67, 0x22, 0x2c, 0x22, 0x56, 0x45, 0x52, 0x42, 0x22, 0x2c, 0x22, 0x77, 0x61, 0x6c, 0x6b, 0x22, 0x5d];
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Throws<InvalidDataException>(() => LemmaLexicon.Read(document.RootElement));
    }
}
