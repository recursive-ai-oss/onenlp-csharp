using RecursiveAI.OneNlp.LanguageId;

namespace RecursiveAI.OneNlp.Tests;

public class LanguageIdTests
{
    // The model is embedded only in builds made with ONENLP_LANGID_WEIGHTS; without it these tests skip.
    private const string SkipReason = "This build carries no language-ID model; set ONENLP_LANGID_WEIGHTS to the directory with langid.bin and rebuild.";

    private static LanguageDetector D
    {
        get
        {
            if (!LanguageDetector.TryGetDefault(out LanguageDetector? d))
            {
                Assert.Skip(SkipReason);
            }

            return d;
        }
    }

    private static Stream ModelResource()
    {
        _ = D;
        return typeof(LanguageDetector).Assembly.GetManifestResourceStream(LanguageDetector.ResourceName)!;
    }

    [Theory]
    [InlineData("The quick brown fox jumps over the lazy dog and runs away from the farmer.", "en")]
    [InlineData("Der schnelle braune Fuchs springt über den faulen Hund und läuft vor dem Bauern davon.", "de")]
    [InlineData("Le renard brun rapide saute par-dessus le chien paresseux et s'enfuit loin du fermier.", "fr")]
    [InlineData("El rápido zorro marrón salta sobre el perro perezoso y huye del granjero.", "es")]
    [InlineData("A rápida raposa marrom pula sobre o cão preguiçoso e foge do fazendeiro.", "pt")]
    [InlineData("Быстрая коричневая лиса перепрыгивает через ленивую собаку и убегает от фермера.", "ru")]
    [InlineData("Швидка руда лисиця стрибає через ледачого пса і тікає від фермера.", "uk")]
    [InlineData("素早い茶色の狐は怠け者の犬を飛び越えて農夫から逃げていきます。", "ja")]
    [InlineData("敏捷的棕色狐狸跳过了懒惰的狗，然后从农夫身边逃走了。", "zh")]
    [InlineData("빠른 갈색 여우가 게으른 개를 뛰어넘어 농부에게서 도망칩니다.", "ko")]
    [InlineData("Ο γρήγορος καφέ αλεπού πηδά πάνω από το τεμπέλικο σκυλί και ξεφεύγει από τον αγρότη.", "el")]
    [InlineData("השועל החום המהיר קופץ מעל הכלב העצלן ובורח מהחקלאי.", "he")]
    [InlineData("เจ้าจิ้งจอกสีน้ำตาลตัวเร็วกระโดดข้ามสุนัขขี้เกียจและวิ่งหนีจากชาวนา", "th")]
    [InlineData("Hızlı kahverengi tilki tembel köpeğin üzerinden atlar ve çiftçiden kaçar.", "tr")]
    [InlineData("तेज़ भूरी लोमड़ी आलसी कुत्ते के ऊपर से कूदती है और किसान से भाग जाती है।", "hi")]
    public void DetectsSentences(string text, string code) => Assert.Equal(code, D.Detect(text).Code);

    [Fact]
    public void NoLettersIsUnknown()
    {
        Assert.Equal(Language.Unknown, D.Detect(""));
        Assert.Equal(Language.Unknown, D.Detect("12345 -- ... !!!"));
        Assert.Empty(D.Confidences("   "));
        Assert.Empty(D.DetectSpans("42"));
    }

    [Fact]
    public void ConfidencesAreProbabilities()
    {
        IReadOnlyList<LanguageConfidence> c = D.Confidences("Das ist ein kurzer Satz");
        Assert.Equal(1.0, c.Sum(x => x.Confidence), 3);
        Assert.Equal(c.OrderByDescending(x => x.Confidence).First().Language, c[0].Language);
        Assert.Equal("de", c[0].Language.Code);
        Language l = D.Detect("Das ist ein kurzer Satz", out double conf);
        Assert.Equal(c[0].Language, l);
        Assert.Equal(c[0].Confidence, conf, 6);
    }

    [Fact]
    public void RestrictionNarrowsTheAnswer()
    {
        // "prologue" is a word of both languages; whichever the full model prefers, the restricted detector must pick from its set.
        LanguageDetector fr = D.Restrict([Language.French, Language.German]);
        Language got = fr.Detect("prologue");
        Assert.True(got == Language.French || got == Language.German);
        Assert.Equal(2, fr.Languages.Count);
        Assert.Equal(2, fr.Confidences("prologue").Count);
        // A Cyrillic text has no candidate among Latin-script languages.
        Assert.Equal(Language.Unknown, fr.Detect("Привет, как дела?"));
        Assert.Throws<ArgumentException>(() => D.Restrict([Language.FromCode("xx")]));
        Assert.Throws<ArgumentNullException>(() => D.Restrict(null!));
    }

    [Fact]
    public void ScriptDecidesSingleLanguageScripts()
    {
        Language l = D.Detect("γεια σου", out double conf);
        Assert.Equal("el", l.Code);
        Assert.Equal(1.0, conf, 6);
    }

    [Fact]
    public void MixedTextSplitsIntoRunsThatTileTheText()
    {
        const string text = "Parlez-vous français aujourd'hui avec nous? Ich spreche Französisch nur ein bisschen, aber es geht gut. A little bit is better than nothing at all.";
        IReadOnlyList<LanguageSpan> spans = D.DetectSpans(text);
        Assert.Equal(["fr", "de", "en"], spans.Select(s => s.Language.Code));
        Assert.Equal(0, spans[0].Start);
        Assert.Equal(text.Length, spans[^1].End);
        for (int i = 1; i < spans.Count; i++)
        {
            Assert.Equal(spans[i - 1].End, spans[i].Start);
        }

        Assert.Equal(text.IndexOf("Ich", StringComparison.Ordinal), spans[1].Start);
        Assert.All(spans, s => Assert.InRange(s.Confidence, 0.5, 1.0));
    }

    [Fact]
    public void SingleLanguageTextIsOneRun()
    {
        IReadOnlyList<LanguageSpan> spans = D.DetectSpans("The weather today is lovely and we are going to the park, then we will have lunch.");
        Assert.Single(spans);
        Assert.Equal("en", spans[0].Language.Code);
    }

    [Fact]
    public void ShortWordsOfTheWholeAlphabetDoNotThrow()
    {
        foreach (string s in new[] { "a", "é", "ñ", "\uD83D\uDE00", "x\u0301", "\uD800", "ß", "İ", "ǅ", new string('a', 100000), string.Concat(Enumerable.Repeat("слово ", 20000)) })
        {
            _ = D.Detect(s);
            _ = D.DetectSpans(s);
        }
    }

    [Fact]
    public void DefaultModelIsAFewMegabytes()
    {
        using Stream s = ModelResource();
        Assert.InRange(s.Length, 1 << 20, 8 << 20);
        Assert.Equal(75, D.Languages.Count);
    }

    [Fact]
    public void ModelRoundTripsAndRejectsDamage()
    {
        using Stream s = ModelResource();
        byte[] bytes = new byte[s.Length];
        s.ReadExactly(bytes);
        LanguageDetector loaded = LanguageDetector.Load(new MemoryStream(bytes));
        Assert.Equal(D.Detect("Guten Morgen, wie geht es dir heute?"), loaded.Detect("Guten Morgen, wie geht es dir heute?"));

        Assert.Throws<InvalidDataException>(() => LanguageDetector.Load(new MemoryStream(bytes[..100])));
        Assert.Throws<InvalidDataException>(() => LanguageDetector.Load(new MemoryStream(bytes[..(bytes.Length - 1)])));
        Assert.Throws<InvalidDataException>(() => LanguageDetector.Load(new MemoryStream([])));
        byte[] wrong = (byte[])bytes.Clone();
        wrong[0] ^= 0xFF;
        Assert.Throws<InvalidDataException>(() => LanguageDetector.Load(new MemoryStream(wrong)));
        byte[] hugeCount = (byte[])bytes.Clone();
        hugeCount[5] = 0xFF;
        hugeCount[6] = 0xFF; // 65535 languages
        Assert.Throws<InvalidDataException>(() => LanguageDetector.Load(new MemoryStream(hugeCount)));
        Assert.Throws<ArgumentNullException>(() => LanguageDetector.Load(null!));
    }

    [Fact]
    public void NlpDetectsAndSegmentsByLanguage()
    {
        _ = D;
        Assert.Equal("en", Nlp.DetectLanguage("Mr. Smith can't go. He's gone!").Code);
        Assert.Equal(2, Nlp.Sentences("Mr. Smith can't go. He's gone!").Count);
        Doc german = Nlp.Analyze("Das ist ein Test. Und noch einer.");
        Assert.Equal(2, german.Sentences.Count);
        Assert.Throws<ArgumentNullException>(() => Nlp.DetectLanguage(null!));
    }

    [Fact]
    public void ScriptTable()
    {
        Assert.Equal(Script.Latin, Scripts.Of('a'));
        Assert.Equal(Script.Latin, Scripts.Of('ß'));
        Assert.Equal(Script.Other, Scripts.Of('×'));
        Assert.Equal(Script.Cyrillic, Scripts.Of('ж'));
        Assert.Equal(Script.Cjk, Scripts.Of('語'));
        Assert.Equal(Script.Cjk, Scripts.Of('あ'));
        Assert.Equal(Script.Hangul, Scripts.Of('한'));
        Assert.Equal(Script.Arabic, Scripts.Of('ع'));
        Assert.Equal(Script.Devanagari, Scripts.Of('ह'));
    }

    [Fact]
    public void WordsAreLetterRunsWithOffsets()
    {
        var t = new LangIdText();
        const string text = "Hello, wörld42 ПриветHello x\u0301y";
        t.Run(text);
        string[] words = [.. Enumerable.Range(0, t.WordCount).Select(w => text[t.CharStart[w]..t.CharEnd[w]])];
        // Digits end a word; a change of script ends a word; a combining mark stays with its letter.
        Assert.Equal(["Hello", "wörld", "Привет", "Hello", "x\u0301y"], words);
        Assert.Equal(Script.Cyrillic, t.WordScript[2]);
        Assert.Equal(Script.Latin, t.WordScript[3]);
        for (int w = 0; w < t.WordCount; w++)
        {
            Assert.True(t.HashStart[w + 1] > t.HashStart[w]);
        }
    }

    [Fact]
    public void WithoutAModelDefaultThrowsAndNlpFallsBackToEnglish()
    {
        if (LanguageDetector.TryGetDefault(out _))
        {
            Assert.Skip("This build carries the model; the no-model behaviour is checked by a build without ONENLP_LANGID_WEIGHTS.");
        }

        InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => LanguageDetector.Default);
        Assert.Contains("ONENLP_LANGID_WEIGHTS", e.Message, StringComparison.Ordinal);
        Assert.Equal(2, Nlp.Sentences("Mr. Smith can't go. He's gone!").Count);
    }
}
