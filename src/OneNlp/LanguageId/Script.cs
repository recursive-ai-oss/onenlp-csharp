namespace RecursiveAI.OneNlp.LanguageId;

/// <summary>The writing systems the detector tells apart before it looks at any n-gram.</summary>
/// <remarks>
/// Han and Kana are one group (<see cref="Cjk"/>) because Japanese text mixes them and the only Han-script languages in the
/// list, Chinese and Japanese, are separated by their character statistics. Marks that combine with a letter take the script
/// of the letter before them.
/// </remarks>
internal enum Script : byte
{
    None = 0,
    Latin,
    Cyrillic,
    Greek,
    Armenian,
    Hebrew,
    Arabic,
    Devanagari,
    Bengali,
    Gurmukhi,
    Gujarati,
    Tamil,
    Telugu,
    Thai,
    Georgian,
    Hangul,
    Cjk,
    Other,
}

/// <summary>Maps code points to <see cref="Script"/> by Unicode block (UAX #24 script ranges, reduced to the scripts of the supported languages).</summary>
internal static class Scripts
{
    /// <summary>Number of script values, for sizing tables.</summary>
    public const int Count = (int)Script.Other + 1;

    /// <summary>The script of a letter, or <see cref="Script.Other"/> for a letter of a script the list does not cover.</summary>
    /// <param name="cp">A code point that is a letter.</param>
    public static Script Of(int cp)
    {
        if (cp < 0x250)
        {
            return cp is >= 0x41 and <= 0x5A or >= 0x61 and <= 0x7A or 0xAA or 0xB5 or 0xBA or >= 0xC0 and not 0xD7 and not 0xF7
                ? Script.Latin
                : Script.Other;
        }

        return cp switch
        {
            <= 0x2AF => Script.Latin,
            >= 0x370 and <= 0x3FF => Script.Greek,
            >= 0x400 and <= 0x52F => Script.Cyrillic,
            >= 0x531 and <= 0x58F => Script.Armenian,
            >= 0x590 and <= 0x5FF => Script.Hebrew,
            >= 0x600 and <= 0x6FF or >= 0x750 and <= 0x77F or >= 0x8A0 and <= 0x8FF => Script.Arabic,
            >= 0x900 and <= 0x97F => Script.Devanagari,
            >= 0x980 and <= 0x9FF => Script.Bengali,
            >= 0xA00 and <= 0xA7F => Script.Gurmukhi,
            >= 0xA80 and <= 0xAFF => Script.Gujarati,
            >= 0xB80 and <= 0xBFF => Script.Tamil,
            >= 0xC00 and <= 0xC7F => Script.Telugu,
            >= 0xE00 and <= 0xE7F => Script.Thai,
            >= 0x10A0 and <= 0x10FF or >= 0x1C90 and <= 0x1CBF => Script.Georgian,
            >= 0x1100 and <= 0x11FF or >= 0x3130 and <= 0x318F or >= 0xAC00 and <= 0xD7AF => Script.Hangul,
            >= 0x1C80 and <= 0x1C8F or >= 0x2DE0 and <= 0x2DFF or >= 0xA640 and <= 0xA69F => Script.Cyrillic,
            >= 0x1E00 and <= 0x1EFF or >= 0x2C60 and <= 0x2C7F or >= 0xA720 and <= 0xA7FF or >= 0xFB00 and <= 0xFB06 => Script.Latin,
            >= 0x1F00 and <= 0x1FFF => Script.Greek,
            >= 0x3040 and <= 0x30FF or >= 0x31F0 and <= 0x31FF or >= 0xFF66 and <= 0xFF9F => Script.Cjk,
            >= 0x3400 and <= 0x4DBF or >= 0x4E00 and <= 0x9FFF or >= 0xF900 and <= 0xFAFF or 0x3005 or >= 0x20000 and <= 0x2FA1F => Script.Cjk,
            >= 0xFB50 and <= 0xFDFF or >= 0xFE70 and <= 0xFEFF => Script.Arabic,
            _ => Script.Other,
        };
    }
}
