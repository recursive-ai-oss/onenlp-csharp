using System.Buffers;
using System.Text;
using RecursiveAI.OneNlp;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Matching;
#if !NO_MODELS
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;
#endif
using RecursiveAI.OneNlp.Tokenizers;

namespace RecursiveAI.OneNlp.SampleConsumer;

// The happy-path sample of the design, line for line, as an application compiles it. The numbered lines inside Sample are the sample;
// the code around them only supplies inputs and checks that every line did what its comment says.
internal static class Program
{
    private static int failures;

    private static int Main()
    {
        string tokenizerFile = Environment.GetEnvironmentVariable("ONENLP_SAMPLE_TOKENIZER") ?? "tokenizer.json";
#if NO_MODELS
        return MissingPackages();
#else
        string english = "Ada Lovelace met Charles Babbage in London and paid $20 for 3 kg of paper.";
        string german = "Im Fall 12-3456 entschied Angela Merkel in Hamburg.";
        // No package ships the open-type model yet: the verb names the package to add; then a tiny trained one stands in for it.
        InvalidOperationException? missing = Capture(() => Nlp.Entities(english, "contract party", "governing law"));
        Check("open-type error is typed", missing is ModelNotAvailableException { PackageIds.Count: > 0 });
        Check("open-type error names the package", missing?.Message.Contains("RecursiveAI.OneNlp.Models.Multilingual", StringComparison.Ordinal) == true);
        Console.WriteLine(missing?.Message);
        RegisterTinyOpenModel();
        Sample(english, [english, "Paris is in France."], tokenizerFile, english: true);
        Sample(german, [german, "Berlin liegt in Deutschland.", "Ein kleiner Test."], tokenizerFile, english: false);
        Console.WriteLine(failures == 0 ? "OK" : $"FAILED {failures}");
        return failures == 0 ? 0 : 1;
#endif
    }

#if !NO_MODELS
    private static void Sample(string text, string[] texts, string tokenizerFile, bool english)
    {
        DateTimeOffset today = new(2026, 11, 6, 0, 0, 0, TimeSpan.Zero);
        IReadOnlyList<Example> examples = ReadExamples();

        // ---- the sample ----
        Language lang = Nlp.DetectLanguage("Bonjour tout le monde");
        var people = Nlp.Entities(text).Where(e => e.Label == EntityLabels.Person);
        var parties = Nlp.Entities(text, "contract party", "governing law");
        DateRange? when = Nlp.ParseDate("the first Monday after Easter 2027", reference: today);
        IEnumerable<Quantity> money = Nlp.Quantities(text);
        float[] v = Nlp.Embed("how do I reset my password");
        Doc doc = Nlp.Analyze(text);

        using var nlp = Pipeline.Create(Language.German, Tier.Fast)
            .Add<Tagger>().Add<Lemmatizer>().Add<DependencyParser>().Add<EntityRecognizer>()
            .Add(new EntityRuler { { "CASE_NO", Pattern.Shape("dd-dddd") } });
        Doc d = nlp.Process(text);
        int batch = 0;
        foreach (Doc x in nlp.ProcessMany(texts)) { batch++; }
        nlp.Train(examples, epochs: 10);

        var tok = Tokenizer.FromFile(tokenizerFile);
        byte[] utf8 = Encoding.UTF8.GetBytes(text);
        int[] ids = new int[64];
        OperationStatus s = tok.EncodeToIds(utf8, ids, out int written, out int consumed);
        // ---- end of the sample ----

        Console.WriteLine($"[{(english ? "en" : "de")}] lang={lang} people={string.Join(",", people)} when={when} money={string.Join(",", money.Select(q => q.Text))} embed={v.Length} "
            + $"tokens={doc.Length} chunks={string.Join("|", doc.NounChunks)} d.pos={string.Join("/", d.Select(t => t.Pos))} d.ents={string.Join(",", d.Ents.Select(e => e.Label + ":" + e))} batch={batch} tok={s}/{written}/{consumed}");
        Check("open-type call returns", parties is not null);
        Check("language id", lang == Language.French);
        if (english)
        {
            Check("people", people.Any());
            Check("money", money.Any());
            Check("date", when is not null);
            Check("noun chunks", doc.NounChunks.Count > 0);
        }
        else
        {
            Check("ruler", d.Ents.Any(e => e.Label == "CASE_NO"));
            Check("german noun chunks", doc.NounChunks.Count > 0);
        }

        // Short texts are read in the referenced language instead of throwing, and both verbs agree.
        foreach (string shortText in new[] { "Paris", "Ok", "Angela Merkel", "Sushi in Tokyo" })
        {
            Check($"short text '{shortText}' has a model", Capture(() => Nlp.Entities(shortText)) is null);
            Check($"short text '{shortText}' analyzes", Nlp.Analyze(shortText).Length > 0);
        }

        Check("embed", v.Length > 0);
        Check("processed", batch == texts.Length);
        Check("tokenizer", s == OperationStatus.Done && written > 0 && consumed == utf8.Length);
    }

    // Trains an open-type recognizer on a toy corpus (made-up names), saves and loads it, and registers it as the open-type model.
    private static void RegisterTinyOpenModel()
    {
        var strings = new StringStore();
        var examples = new List<Example>();
        string[] names = ["Zorp", "Blix", "Quan"];
        string[] places = ["Aldor", "Brenn", "Cyth"];
        for (int s = 0; s < 60; s++)
        {
            bool person = s % 2 == 0;
            string[] words = ["we", "met", person ? names[s % 3] : places[s % 3], "today"];
            var tokens = new List<TokenBoundary>();
            int p = 0;
            foreach (string w in words)
            {
                tokens.Add(new TokenBoundary(p, w.Length));
                p += w.Length + 1;
            }

            var doc = new Doc(strings, string.Join(' ', words), [.. tokens]);
            var groups = new Dictionary<string, IReadOnlyList<SpanSpec>>
            {
                ["person"] = person ? [new SpanSpec(2, 3, "person")] : [],
                ["place"] = person ? [] : [new SpanSpec(2, 3, "place")],
            };
            examples.Add(Example.FromReference(doc, spans: groups));
        }

        using Pipeline trained = Pipeline.Blank(Language.English).Add(new OpenEntityRecognizer(new HashEmbeddingEncoder(32, 1024, 3), context: 1, hidden: 32, embedding: 16, maxSpanLength: 3, negativeTypes: 2) { OwnsEncoder = true });
        trained.Train(examples, new TrainOptions { Epochs = 150, BatchWords = 100, LearningRate = 0.01f, Seed = 5 });
        using var buffer = new MemoryStream();
        PipelineFile.Save(trained, buffer);
        Pipeline loaded = PipelineFile.Load(new MemoryStream(buffer.ToArray()));
        ModelPackages.RegisterOpenEntities("RecursiveAI.OneNlp.Models.Multilingual", () => loaded);
    }

    // A few German sentences in CoNLL-U, for the Train line.
    private static IReadOnlyList<Example> ReadExamples()
    {
        string path = Path.Combine(Path.GetTempPath(), "samples-" + Environment.ProcessId + ".conllu");
        File.WriteAllText(path, string.Concat(Enumerable.Repeat(
            "# text = Der Hund schläft.\n1\tDer\tder\tDET\tART\tCase=Nom|Definite=Def|Gender=Masc|Number=Sing|PronType=Art\t2\tdet\t_\t_\n"
            + "2\tHund\tHund\tNOUN\tNN\tCase=Nom|Gender=Masc|Number=Sing\t3\tnsubj\t_\t_\n3\tschläft\tschlafen\tVERB\tVVFIN\tMood=Ind|Number=Sing|Person=3|Tense=Pres|VerbForm=Fin\t0\troot\t_\tSpaceAfter=No\n"
            + "4\t.\t.\tPUNCT\t$.\t_\t3\tpunct\t_\t_\n\n", 12)));
        try
        {
            return [.. ConllUReader.ReadExamples(path)];
        }
        finally
        {
            File.Delete(path);
        }
    }
#endif

    private static int MissingPackages()
    {
        // Without any model package: the verbs and Pipeline.Create say which package to add, and nothing is downloaded.
        Language german = Language.German;
        InvalidOperationException? create = Capture(() => Pipeline.Create(german, Tier.Fast));
        InvalidOperationException? accurate = Capture(() => Pipeline.Create(german, Tier.Accurate));
        InvalidOperationException? entities = Capture(() => Nlp.Entities("Angela Merkel besuchte Hamburg."));
        Console.WriteLine(create?.Message);
        Console.WriteLine(accurate?.Message);
        Check("create names the German package", create?.Message.Contains("RecursiveAI.OneNlp.Models.German", StringComparison.Ordinal) == true);
        Check("accurate names the multilingual package", accurate?.Message.Contains("RecursiveAI.OneNlp.Models.Multilingual", StringComparison.Ordinal) == true);
        Check("entities error", entities is not null);
        InvalidOperationException? open = Capture(() => Nlp.Entities("Angela Merkel besuchte Hamburg.", "contract party"));
        Check("open-type error names the package", open?.Message.Contains("RecursiveAI.OneNlp.Models.Multilingual", StringComparison.Ordinal) == true);
        Console.WriteLine(open?.Message);
        Console.WriteLine(failures == 0 ? "OK" : $"FAILED {failures}");
        return failures == 0 ? 0 : 1;
    }

    private static InvalidOperationException? Capture(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (InvalidOperationException e)
        {
            return e;
        }
    }

    private static void Check(string what, bool ok)
    {
        if (!ok)
        {
            failures++;
            Console.WriteLine("FAIL: " + what);
        }
    }
}
