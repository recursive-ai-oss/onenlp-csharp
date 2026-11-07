using System.Buffers;
using System.Text;
using RecursiveAI.OneNlp.LanguageId;
using RecursiveAI.OneNlp.Matching;
using RecursiveAI.OneNlp.Recognizers;
using RecursiveAI.OneNlp.Tokenizers;

namespace RecursiveAI.OneNlp.Tests;

// The entry points, errors, limits and loaders that a first-time caller meets. Registration is process-wide, so the model tests use
// languages (Galician, Faroese) that no other test and no model package registers.
public class EntryPointTests
{
    // Gives every token that starts with an upper-case letter the entity label "NAME".
    private sealed class Names : IPipelineComponent
    {
        public string Name => "names";

        public void Process(Doc doc)
        {
            var found = new List<SpanSpec>();
            for (int i = 0; i < doc.Length; i++)
            {
                if (char.IsUpper(doc[i].Text[0]))
                {
                    found.Add(new SpanSpec(i, i + 1, "NAME", 0.9f));
                }
            }

            doc.SetEntities(found.ToArray());
        }
    }

    private static readonly Language Galician = Language.FromCode("gl");

    private static void RegisterGalician() =>
        ModelPackages.RegisterQuietly(Galician, "Test.Models.Galician", () => Pipeline.Blank(Galician).Add(new Names()));

    // ---- C1 ----

    [Fact]
    public void LanguageOverloadsUseTheNamedLanguageWithoutDetecting()
    {
        RegisterGalician();
        // "Paris" is detected as another language, but the caller says which one to use.
        Assert.Equal(["Paris"], Nlp.Entities("Paris", Galician).Select(e => e.Text.ToString()));
        Assert.Equal(["Paris", "Bob"], Nlp.Analyze("Paris and Bob", Galician).Ents.Select(e => e.Text.ToString()));
        Assert.Single(Nlp.AnalyzeMany(["Paris", "x"], Galician).First().Ents);
        Assert.Equal(2, Nlp.Lemmas("Paris Bob", Galician).Length);
    }

    [Fact]
    public void LanguageOverloadsFailForAMissingModelWhereAnalyzeStillTokenizes()
    {
        var faroese = Language.FromCode("kk");
        ModelNotAvailableException ex = Assert.Throws<ModelNotAvailableException>(() => Nlp.Entities("Tórshavn", faroese));
        Assert.Equal(faroese, ex.Language);
        Assert.Throws<ModelNotAvailableException>(() => Nlp.Lemmas("Tórshavn", faroese));
        Assert.Equal(1, Nlp.Analyze("Tórshavn", faroese).Length);
        Assert.Equal(1, Nlp.AnalyzeMany(["Tórshavn"], faroese).Single().Length);
    }

    [Fact]
    public void UnsureDetectionFallsBackToALanguageWithAModel()
    {
        if (!LanguageDetector.TryGetDefault(out LanguageDetector? detector))
        {
            Assert.Skip("This build carries no language-ID model; set ONENLP_LANGID_WEIGHTS to the directory with langid.bin and rebuild.");
        }

        LanguageDetector among = detector.Restrict([Language.English]);
        bool OnlyEnglish(Language l) => l == Language.English;

        // The detector's unsure answers for a word and a name (observed with the shipped model) become English, the only language with a model.
        Assert.Equal(Language.English, Nlp.Choose("Paris", Language.French, 0.12, among, OnlyEnglish));
        Assert.Equal(Language.English, Nlp.Choose("Angela Merkel", Language.FromCode("eu"), 0.13, among, OnlyEnglish));
        // A confident answer stays, so the caller learns which package is missing.
        Assert.Equal(Language.French, Nlp.Choose("Bonjour tout le monde", Language.French, 1.0, among, OnlyEnglish));
        // A detected language with a model stays; no registered detector means nothing to fall back to; nothing detected is English.
        Assert.Equal(Language.German, Nlp.Choose("Berlin", Language.German, 0.3, among, l => l == Language.German));
        Assert.Equal(Language.French, Nlp.Choose("Paris", Language.French, 0.12, null, OnlyEnglish));
        Assert.Equal(Language.English, Nlp.Choose("123", Language.Unknown, 0, null, OnlyEnglish));

        // The shipped detector really is unsure about these (the assertion that makes the rule worth having).
        foreach (string text in new[] { "Paris", "Ok", "Angela Merkel", "Sushi in Tokyo" })
        {
            LanguageDetector.Default.Detect(text, out double confidence);
            Assert.True(confidence < Nlp.TrustedConfidence, text);
        }
    }

    // ---- C5 ----

    [Fact]
    public void MissingModelErrorCarriesLanguageTierAndPackages()
    {
        ModelNotAvailableException german = Assert.Throws<ModelNotAvailableException>(() => Pipeline.Create(Language.German));
        Assert.Equal(Language.German, german.Language);
        Assert.Equal(Tier.Fast, german.Tier);
        Assert.Equal(["RecursiveAI.OneNlp.Models.German", "RecursiveAI.OneNlp.Models.Multilingual"], german.PackageIds);
        Assert.Contains("RecursiveAI.OneNlp.Models.German", german.Message, StringComparison.Ordinal);

        ModelNotAvailableException accurate = Assert.Throws<ModelNotAvailableException>(() => Pipeline.Create(Language.German, Tier.Accurate));
        Assert.Equal(Tier.Accurate, accurate.Tier);
        Assert.Contains("RecursiveAI.OneNlp.Models.Multilingual.Large", accurate.PackageIds);

        // A language no package covers is told so, and no package is named.
        ModelNotAvailableException swahili = Assert.Throws<ModelNotAvailableException>(() => ModelPackages.Get(Language.FromCode("sw")));
        Assert.Empty(swahili.PackageIds);

        ModelNotAvailableException hindi = Assert.Throws<ModelNotAvailableException>(() => ModelPackages.Get(Language.FromCode("hi")));
        Assert.Empty(hindi.PackageIds);

        // Code that caught InvalidOperationException keeps working.
        Assert.IsAssignableFrom<InvalidOperationException>(german);
        Assert.Throws<ModelNotAvailableException>(() => Nlp.Embed("x"));
    }

    // ---- C7 ----

    [Fact]
    public void PipelineRefusesTextOverItsMaxLengthAndNamesTheLimit()
    {
        Pipeline pipeline = Pipeline.Blank(Language.English);
        Assert.Equal(Nlp.MaxLength, pipeline.MaxLength);
        Assert.Equal(1_000_000, Nlp.MaxLength);

        pipeline.MaxLength = 10;
        Assert.Equal(10, pipeline.Process("0123456789").Text.Length);
        ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => pipeline.Process("0123456789A"));
        Assert.Contains("Pipeline.MaxLength is 10", ex.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentOutOfRangeException>(() => pipeline.ProcessMany(["ok", "0123456789A"]).ToList());
        Assert.Throws<ArgumentOutOfRangeException>(() => pipeline.MaxLength = 0);
        Assert.Throws<ArgumentNullException>(() => pipeline.Process((string)null!));
    }

    [Fact]
    public void NlpVerbsRefuseTextOverTheStaticLimit()
    {
        string big = new('a', Nlp.MaxLength + 1);
        ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => Nlp.Quantities(big));
        Assert.Contains("Nlp.MaxLength is 1000000", ex.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentOutOfRangeException>(() => Nlp.DetectLanguage(big));
        Assert.Throws<ArgumentOutOfRangeException>(() => Nlp.Analyze(big));
        Assert.Throws<ArgumentOutOfRangeException>(() => Nlp.Dates(big, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Nlp.MaxLength = 0);
    }

    // ---- C4 ----

    private static readonly string[] Specials = ["[PAD]", "[UNK]", "[CLS]", "[SEP]", "[MASK]"];

    private static MemoryStream Vocabulary(params string[] tokens) =>
        new(Encoding.UTF8.GetBytes(string.Join('\n', Specials.Concat(tokens)) + "\n"));

    [Fact]
    public void CaselessLettersDoNotMakeAVocabularyCased()
    {
        // bert-base-uncased holds U+211D (double-struck R) and ##U+211D: upper-case letters with no lower-case form.
        Tokenizer uncased = Tokenizer.FromStream(Vocabulary("hello", "world", "\u211D", "##\u211D"), TokenizerFormat.WordPieceVocabulary);
        Assert.Equal(["[CLS]", "hello", "world", "[SEP]"], uncased.Encode("Hello World").Tokens);

        Tokenizer cased = Tokenizer.FromStream(Vocabulary("Hello", "World", "hello"), TokenizerFormat.WordPieceVocabulary);
        Assert.Equal(["[CLS]", "Hello", "World", "[SEP]"], cased.Encode("Hello World").Tokens);
    }

    [Fact]
    public void WordPieceVocabularyCasingCanBeStated()
    {
        // Upper-case entries but a lower-casing model: the caller knows better than the guess.
        Tokenizer lower = Tokenizer.FromWordPieceVocabulary(Vocabulary("Hello", "hello", "world"), new WordPieceVocabularyOptions { LowerCase = true });
        Assert.Equal(["[CLS]", "hello", "world", "[SEP]"], lower.Encode("Hello World").Tokens);

        Tokenizer keep = Tokenizer.FromWordPieceVocabulary(Vocabulary("Hello", "hello", "World"), new WordPieceVocabularyOptions { LowerCase = false });
        Assert.Equal(["[CLS]", "Hello", "World", "[SEP]"], keep.Encode("Hello World").Tokens);

        // Accents: lower-casing without stripping keeps the accent; the default follows LowerCase.
        Tokenizer accents = Tokenizer.FromWordPieceVocabulary(Vocabulary("caf\u00E9", "cafe"), new WordPieceVocabularyOptions { LowerCase = true, StripAccents = false });
        Assert.Equal(["[CLS]", "caf\u00E9", "[SEP]"], accents.Encode("Caf\u00E9").Tokens);
        Tokenizer stripped = Tokenizer.FromWordPieceVocabulary(Vocabulary("caf\u00E9", "cafe"), new WordPieceVocabularyOptions { LowerCase = true });
        Assert.Equal(["[CLS]", "cafe", "[SEP]"], stripped.Encode("Caf\u00E9").Tokens);

        Assert.Throws<ArgumentNullException>(() => Tokenizer.FromWordPieceVocabulary(Vocabulary("a"), null!));
    }

    // ---- C8 ----

    [Fact]
    public void AllocatingEncodeToIdsNeverCutsTheText()
    {
        Tokenizer tok = Tokenizer.FromStream(Vocabulary("a", "b"), TokenizerFormat.WordPieceVocabulary);
        string text = string.Join(' ', Enumerable.Repeat("a b", 600));
        int[] ids = tok.EncodeToIds(text);
        Assert.Equal(1200 + 2, ids.Length);
        Assert.Equal(1200, tok.EncodeToIds(text, addSpecialTokens: false).Length);

        // The buffer form reports the cut instead.
        int[] small = new int[16];
        Assert.Equal(OperationStatus.DestinationTooSmall, tok.EncodeToIds(text, small, out int written, out _));
        Assert.Equal(16, written);
        Assert.Equal(ids[..16], small);
    }

    // ---- C10 ----

    [Fact]
    public void PatternErrorsCarryAPositionAndNoParameterNames()
    {
        PatternFormatException op = Assert.Throws<PatternFormatException>(() => PatternJson.ParsePattern("""[{"TEXT": "a", "OP": "++"}]"""));
        Assert.DoesNotContain("(Parameter", op.Message, StringComparison.Ordinal);

        PatternFormatException syntax = Assert.Throws<PatternFormatException>(() => PatternJson.ParsePattern("[\n{\"TEXT\": }]"));
        Assert.Equal(2, syntax.LineNumber);
        Assert.NotNull(syntax.BytePositionInLine);

        PatternFormatException lines = Assert.Throws<PatternFormatException>(() =>
            PatternJson.ReadEntityPatterns(Encoding.UTF8.GetBytes("{\"label\":\"A\",\"pattern\":\"x\"}\n{\"label\": }\n")));
        Assert.Equal(2, lines.LineNumber);
        Assert.NotNull(lines.BytePositionInLine);
    }

    [Fact]
    public void RecognizerRulesErrorsCarryTheLineNumber()
    {
        RecognizerRulesException ex = Assert.Throws<RecognizerRulesException>(() => Recognizer.FromRules([new ReadOnlyMemory<byte>("# c\nrule before any section\n"u8.ToArray())]));
        Assert.Equal(2, ex.LineNumber);
        Assert.StartsWith("Line 2:", ex.Message, StringComparison.Ordinal);
    }

    // ---- C15 ----

    [Fact]
    public void DefaultReferenceDateIsRefused()
    {
        Assert.Throws<ArgumentException>(() => Nlp.ParseDate("tomorrow", default));
        Assert.Throws<ArgumentException>(() => Nlp.Dates("tomorrow", default, Language.German));
        Assert.Throws<ArgumentException>(() => Recognizer.English.Recognize("tomorrow", new RecognitionContext(default(DateTimeOffset))));

        // Without a context relative expressions are skipped, standing-alone ones are kept.
        var found = Recognizer.English.Recognize("Pay $20 by next Friday or on March 5, 2027.");
        Assert.DoesNotContain(found, r => r.Text == "next Friday");
        Assert.Contains(found, r => r.Text.Contains("March 5, 2027", StringComparison.Ordinal));
        Assert.Empty(Recognizer.English.Recognize("tomorrow", default(RecognitionContext)).OfType<TemporalMatch>());
    }

    // ---- C16 ----

    [Fact]
    public void UnknownLanguagePrintsAsUnknown()
    {
        Assert.Equal("Unknown", Language.Unknown.ToString());
        Assert.Equal(string.Empty, Language.Unknown.Code);
        Assert.Equal("en", Language.English.ToString());
    }

    [Fact]
    public void UnknownComponentErrorPointsToPipelineCreate()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => Pipeline.Blank().Add("ner"));
        Assert.DoesNotContain("registered once that package is loaded", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Pipeline.Create", ex.Message, StringComparison.Ordinal);
    }
}
