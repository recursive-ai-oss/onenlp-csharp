namespace RecursiveAI.OneNlp.LanguageId;

/// <summary>A language and the detector's confidence in it for one text.</summary>
/// <param name="Language">The language.</param>
/// <param name="Confidence">A probability from 0 to 1. The values of one result sum to 1 (to within rounding).</param>
public readonly record struct LanguageConfidence(Language Language, double Confidence);

/// <summary>A run of text in one language, from <see cref="LanguageDetector.DetectSpans"/>.</summary>
/// <param name="Language">The language of the run.</param>
/// <param name="Start">Index of the first UTF-16 code unit of the run in the input text.</param>
/// <param name="End">Index just after the last code unit of the run. Runs tile the text: each begins where the one before ends.</param>
/// <param name="Confidence">The probability of <paramref name="Language"/> given only the words of this run, from 0 to 1.</param>
public readonly record struct LanguageSpan(Language Language, int Start, int End, double Confidence)
{
    /// <summary>The number of UTF-16 code units in the run.</summary>
    public int Length => End - Start;
}
