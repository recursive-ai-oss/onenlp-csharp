using System.Buffers;
using RecursiveAI.OneNlp;
using RecursiveAI.OneNlp.LanguageId;
using RecursiveAI.OneNlp.Recognizers;
using RecursiveAI.OneNlp.Tokenizers;

namespace RecursiveAI.OneNlp.Samples;

// The "after" side of the migration pages in docs/migration/. Each block is what a call from another library becomes here.
internal static class MigrationSamples
{
    internal static void Run(string? tokenizerJson)
    {
        Console.WriteLine("migration");
        LanguageId();
        Recognizers();
        if (tokenizerJson is not null)
        {
            Tokens(tokenizerJson);
        }

        Pipelines();
    }

    private static void LanguageId()
    {
        // <lingua>
        // LanguageDetectorBuilder.FromLanguages(...).Build()  becomes  Restrict(...) on the detector that ships with the library.
        LanguageDetector detector = LanguageDetector.Default.Restrict([Language.English, Language.French, Language.German]);

        Language language = detector.Detect("languages are awesome");                         // DetectLanguageOf
        IReadOnlyList<LanguageConfidence> ranked = detector.Confidences("languages are awesome"); // ComputeLanguageConfidenceValues
        IReadOnlyList<LanguageSpan> runs = detector.DetectSpans(                              // DetectMultipleLanguagesOf
            "Parlez-vous français? Ich spreche Französisch nur ein bisschen.");
        foreach (LanguageSpan run in runs)
        {
            Console.WriteLine($"{run.Language} {run.Start}..{run.End} {run.Confidence:F2}");
        }
        // </lingua>

        Program.Check("lingua: English", language == Language.English);
        Program.Check("lingua: confidences are ranked, the first is the answer", ranked.Count == 3 && ranked[0].Language == language);
        Program.Check("lingua: French then German", runs.Count >= 2 && runs[0].Language == Language.French && runs[^1].Language == Language.German);
    }

    private static void Recognizers()
    {
        // <recognizers>
        // NumberRecognizer.RecognizeNumber / DateTimeRecognizer.RecognizeDateTime / ...  become one engine that returns every kind in text order.
        Recognizer english = Recognizer.English;                              // Recognizer.For(Language.French), ... for the other nine
        DateTimeOffset reference = new(2026, 3, 18, 10, 30, 0, TimeSpan.Zero);

        foreach (Recognition r in english.Recognize("Pay $20 to bob@example.com by 5 March 2026, or within two weeks.", new RecognitionContext(reference)))
        {
            string detail = r switch
            {
                Quantity q => $"{q.Kind} {q.Value} {q.Unit ?? q.Currency}",     // ModelResult.Resolution["value"], ["unit"]
                TemporalMatch t => $"{t.Kind} {t.Value} {t.Range?.Start:yyyy-MM-dd}", // Resolution["values"][..]["timex"], ["start"], ["end"]
                SequenceMatch s => $"{s.Kind}",
                _ => string.Empty,
            };
            Console.WriteLine($"{r.Label,-10} [{r.Start},{r.End}) {r.Text}  {detail}");   // TypeName, Start, End (inclusive in the old library)
        }
        // </recognizers>

        Recognition[] found = [.. english.Recognize("Pay $20 to bob@example.com by 5 March 2026, or within two weeks.", new RecognitionContext(reference))];
        Program.Check("recognizers: money", found.OfType<Quantity>().Any(q => q.Kind == QuantityKind.Money && q.Value == 20m && q.Currency == "USD"));
        Program.Check("recognizers: e-mail", found.OfType<SequenceMatch>().Any(s => s.Kind == SequenceKind.Email));
        Program.Check("recognizers: the date", found.OfType<TemporalMatch>().Any(t => t.Range?.Start.Date == new DateTime(2026, 3, 5)));
        Program.Check("recognizers: the duration", found.OfType<TemporalMatch>().Any(t => t.Kind == TemporalKind.Duration));
        Program.Check("recognizers: end is exclusive", found.All(r => r.End - r.Start == r.Text.Length));
    }

    private static void Tokens(string tokenizerJson)
    {
        // <tokenizers>
        // BertTokenizer.Create / TiktokenTokenizer.CreateForModel / SentencePieceTokenizer.Create  become one type that reads the file's own format.
        Tokenizer tokenizer = Tokenizer.FromFile(tokenizerJson);

        int[] ids = tokenizer.EncodeToIds("a text to encode", addSpecialTokens: false);                              // EncodeToIds(text): a new array, nothing cut off
        int count = tokenizer.CountTokens("a text to encode", addSpecialTokens: false);                             // CountTokens(text)
        string text = tokenizer.Decode(ids);                                                                        // Decode(ids)
        ReadOnlySpan<char> head = tokenizer.TrimToTokenBudget("a text to encode", 2);                               // GetIndexByTokenCount

        // For a fixed buffer with no allocation, check the status: DestinationTooSmall means the buffer cut the text short.
        int[] buffer = new int[512];
        OperationStatus status = tokenizer.EncodeToIds("a text to encode", buffer, out int written, out int consumed);
        // </tokenizers>

        Program.Check("tokenizers: done, and the count agrees", status == OperationStatus.Done && count + 2 == written && ids.Length == count && consumed == "a text to encode".Length);
        Program.Check("tokenizers: decode round trip", text.Length > 0 && head.Length > 0);
    }

    private static void Pipelines()
    {
        // <catalyst>
        // Catalyst.Models.English.Register(); Pipeline.ForAsync(Language.English); new Document(text, Language.English); nlp.ProcessSingle(doc)
        // become: reference the model package, then call the verb. Nothing is registered by hand and nothing is downloaded.
        Doc doc = Nlp.Analyze("Marie Curie won the Nobel Prize in Paris.");
        foreach (Token token in doc)                                  // foreach span in doc.Spans / foreach token in span
        {
            Console.WriteLine($"{token.Idx} {token} {token.Pos} {token.Lemma}");   // token.Value, token.POS, token.Lemma
        }

        foreach (Span entity in doc.Ents)                             // token.EntityTypes
        {
            Console.WriteLine($"{entity.Label} {entity}");
        }
        // </catalyst>

        Program.Check("catalyst: tokens carry tags and lemmas", doc.All(t => t.Pos.Length > 0 && t.Lemma.Length > 0));
    }
}
