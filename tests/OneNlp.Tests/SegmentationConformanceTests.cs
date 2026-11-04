using System.Runtime.CompilerServices;
using System.Text;
using RecursiveAI.OneNlp.Segmentation;

namespace RecursiveAI.OneNlp.Tests;

/// <summary>
/// Runs every line of the Unicode WordBreakTest and SentenceBreakTest files through our segmentation layer with no language data.
/// That shows the layer keeps UAX #29 exactly: tokens are the non-white-space word segments, and sentence starts are the sentence
/// boundaries snapped to tokens.
/// </summary>
public class SegmentationConformanceTests
{
    private static string DataPath(string name, [CallerFilePath] string here = "")
        => Path.Combine(Path.GetDirectoryName(here)!, "..", "testdata", "unicode", name);

    // A line is "÷ 0041 × 0042 ÷ ...  # comment"; returns the text and the break positions (UTF-16 offsets) including 0 and the end.
    private static (string Text, List<int> Breaks) ParseLine(string line)
    {
        string body = line[..line.IndexOf('#', StringComparison.Ordinal)];
        var sb = new StringBuilder();
        var breaks = new List<int>();
        foreach (string part in body.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == "\u00F7")
            {
                breaks.Add(sb.Length);
            }
            else if (part != "\u00D7")
            {
                sb.Append(char.ConvertFromUtf32(Convert.ToInt32(part, 16)));
            }
        }

        return (sb.ToString(), breaks);
    }

    private static IEnumerable<(string Text, List<int> Breaks)> Lines(string file)
        => File.ReadLines(DataPath(file)).Where(l => l.Length > 0 && l[0] != '#').Select(ParseLine);

    [Fact]
    public void WordBreakTestPassesThroughOurLayer()
    {
        var tokenizer = new UaxWordTokenizer(SegmentationData.None);
        int lines = 0;
        var failures = new List<string>();
        foreach ((string text, List<int> breaks) in Lines("WordBreakTest.txt"))
        {
            lines++;
            var expected = new List<TokenBoundary>();
            for (int i = 0; i + 1 < breaks.Count; i++)
            {
                string segment = text[breaks[i]..breaks[i + 1]];
                if (!segment.All(char.IsWhiteSpace))
                {
                    expected.Add(new TokenBoundary(breaks[i], breaks[i + 1] - breaks[i]));
                }
            }

            if (!expected.SequenceEqual(tokenizer.Segment(text)))
            {
                failures.Add(string.Join(' ', text.Select(c => ((int)c).ToString("X4", System.Globalization.CultureInfo.InvariantCulture))));
            }
        }

        Assert.True(lines >= 1800, $"only {lines} lines read");
        Assert.True(failures.Count == 0, $"{failures.Count} of {lines} lines differ, first: {failures.FirstOrDefault()}");
    }

    [Fact]
    public void SentenceBreakTestPassesThroughOurLayer()
    {
        using Pipeline pipeline = new(new UaxWordTokenizer(SegmentationData.None));
        pipeline.Add(new SentencizerComponent(SegmentationData.None));
        int lines = 0;
        var failures = new List<string>();
        foreach ((string text, List<int> breaks) in Lines("SentenceBreakTest.txt"))
        {
            lines++;
            Doc doc = pipeline.Process(text);

            // A sentence starts at the first token that begins at or after each interior Unicode boundary.
            var expected = new SortedSet<int>();
            foreach (int b in breaks.Where(b => b > 0 && b < text.Length))
            {
                for (int t = 0; t < doc.Length; t++)
                {
                    if (doc[t].Idx >= b)
                    {
                        if (t > 0)
                        {
                            expected.Add(t);
                        }

                        break;
                    }
                }
            }

            var actual = new SortedSet<int>();
            for (int t = 1; t < doc.Length; t++)
            {
                if (doc[t].IsSentStart == true)
                {
                    actual.Add(t);
                }
            }

            if (!expected.SetEquals(actual))
            {
                failures.Add(string.Join(' ', text.Select(c => ((int)c).ToString("X4", System.Globalization.CultureInfo.InvariantCulture))));
            }
        }

        Assert.True(lines >= 400, $"only {lines} lines read");
        Assert.True(failures.Count == 0, $"{failures.Count} of {lines} lines differ, first: {failures.FirstOrDefault()}");
    }
}
