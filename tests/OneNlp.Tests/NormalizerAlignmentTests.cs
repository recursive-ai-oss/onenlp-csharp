using RecursiveAI.OneNlp.Tokenizers.Hf;
using Xunit;

namespace RecursiveAI.OneNlp.Tests;

/// <summary>
/// Origins of normalized characters after NFC and NFD. The expected values were read from the reference library's public
/// NormalizedString API (tokenizers 0.22.1) on the same strings; they are small, minimized cases of what the parity corpus found.
/// </summary>
public sealed class NormalizerAlignmentTests
{
    // The origin of every normalized character as "start-end", in order.
    private static string Describe(string input, bool compose)
    {
        var text = new NormalizedString(input);
        new UnicodeFormNormalizer(compatibility: false, compose).Normalize(text);
        var starts = new List<string>();
        for (int i = 0; i < text.Length; i++)
        {
            starts.Add(text.AlignmentAt(i).Start + "-" + text.AlignmentAt(i).End);
        }

        return string.Join(',', starts);
    }

    [Theory]
    // Both pieces of a decomposed vowel sign cover that sign; the mark after it keeps its own origin.
    [InlineData("a\u09cb\u0981b", false, "0-1,1-2,1-2,2-3,3-4")]
    // A mark pair that composes into one vowel sign: the composite covers the first component, the second vanishes.
    [InlineData("\u0998\u09c7\u09be", true, "0-1,1-2")]
    [InlineData("x\u09c7\u09bey", true, "0-1,1-2,3-4")]
    // Composition followed by a leftover mark; reordering moves characters but not origins.
    [InlineData("a\u0301\u0302", true, "0-1,2-3")]
    [InlineData("e\u0301\u0323", true, "0-1,2-3")]
    // A precomposed letter that decomposes and composes again around reordered marks keeps one origin per character.
    [InlineData("\u00fc\u0303\u0331", true, "0-1,1-2,2-3")]
    [InlineData("\u00e3\u0331x", true, "0-1,1-2,2-3")]
    // A composition-excluded letter decomposes, both parts covering it.
    [InlineData("\u0958\u0301", true, "0-1,0-1,1-2")]
    public void OriginsMatchTheReference(string input, bool compose, string expected)
    {
        Assert.Equal(expected, Describe(input, compose));
    }

    [Theory]
    [InlineData(0x4E00, true)]
    [InlineData(0x2B81F, true)]
    [InlineData(0x2B820, false)] // observed in output of tokenizers 0.22.1: the reference stops short of this block
    [InlineData(0x2B91F, false)]
    [InlineData(0x2B920, true)]
    [InlineData(0x2CEAF, true)]
    [InlineData(0x2CEB0, false)]
    public void ChineseCharactersAreSpacedOutAsInTheReference(int codePoint, bool spaced)
    {
        string c = char.ConvertFromUtf32(codePoint);
        var text = new NormalizedString(c);
        new BertNormalizer(cleanText: false, handleChineseChars: true, stripAccents: false, lowercase: false).Normalize(text);
        Assert.Equal(spaced ? " " + c + " " : c, text.Text);
    }
}
