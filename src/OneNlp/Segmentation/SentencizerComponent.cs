using System.Collections.Frozen;
using OneFont;

namespace RecursiveAI.OneNlp.Segmentation;

/// <summary>
/// A rule-based sentence segmenter (registered as <c>sentencizer</c>): it writes each token's sentence boundary from the sentence
/// boundaries of UAX #29 section 5, snapped to token boundaries.
/// </summary>
/// <remarks>
/// <para>
/// Snapping: a sentence starts at the first token that begins at or after the Unicode boundary, so a token that straddles a boundary
/// stays in the earlier sentence and white space after a sentence's last token belongs to no token. Token 0 always starts a sentence.
/// </para>
/// <para>
/// UAX #29 has no abbreviation list. The <see cref="SegmentationData"/> may name tokens after which no sentence starts
/// (English <c>Mr.</c>, <c>Dr.</c>); with <see cref="SegmentationData.None"/> the boundaries are exactly Unicode's.
/// A trained sentence segmenter is a separate, later component. Safe for concurrent use.
/// </para>
/// </remarks>
public sealed class SentencizerComponent : IPipelineComponent
{
    /// <summary>The registry name, <c>sentencizer</c>.</summary>
    public const string ComponentName = "sentencizer";

    private readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> _nonFinal;
    private readonly bool _hasNonFinal;

    /// <summary>Creates the component.</summary>
    /// <param name="data">The language data; <see langword="null"/> means <see cref="SegmentationData.Neutral"/>.</param>
    public SentencizerComponent(SegmentationData? data = null)
    {
        data ??= SegmentationData.Neutral;
        _nonFinal = data.NonFinalAbbreviations.GetAlternateLookup<ReadOnlySpan<char>>();
        _hasNonFinal = data.NonFinalAbbreviations.Count > 0;
    }

    /// <inheritdoc/>
    public string Name => ComponentName;

    /// <summary>Creates the component for a language: English data for English, the neutral defaults otherwise.</summary>
    /// <param name="language">The language.</param>
    public static SentencizerComponent For(Language language) => new(SegmentationData.For(language));

    /// <inheritdoc/>
    public void Process(Doc doc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        int n = doc.Length;
        if (n == 0)
        {
            return;
        }

        string text = doc.Text;
        ReadOnlySpan<int> starts = doc.GetStarts();
        var boundaries = new SentenceBoundary[n];
        Array.Fill(boundaries, SentenceBoundary.Inside);
        boundaries[0] = SentenceBoundary.Start;

        int token = 0;
        int pos = 0;
        while (pos < text.Length)
        {
            // UAX #29 section 5: the end of the sentence that starts at pos.
            int next = TextSegmentation.GetNextSentenceBoundary(text, pos);
            if (next <= pos || next >= text.Length)
            {
                break;
            }

            pos = next;
            while (token < n && starts[token] < pos)
            {
                token++;
            }

            if (token >= n)
            {
                break;
            }

            if (token > 0 && !(_hasNonFinal && _nonFinal.Contains(doc[token - 1].Text)))
            {
                boundaries[token] = SentenceBoundary.Start;
            }
        }

        doc.SetSentenceBoundaries(boundaries);
    }
}
