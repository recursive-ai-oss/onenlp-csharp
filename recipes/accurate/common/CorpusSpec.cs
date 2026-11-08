using System.Globalization;
using System.Text.Json;

namespace RecursiveAI.OneNlp.Recipes.Accurate;

/// <summary>One treebank of a language: its files (relative to the data root) and sizes. <see cref="Role"/> is <c>gating</c> (its test split is the
/// language's test set) or <c>extra</c> (trained on, never scored). <see cref="Leaks"/> counts training sentences that repeat a gating test sentence.</summary>
public sealed record CorpusTreebank(string Name, string Role, string Licence, string Train, string? Dev, string? Test, int TrainSentences, int TrainWords, int Leaks);

/// <summary>An entity corpus (.iob2 files) of a language.</summary>
public sealed record CorpusEntitySet(string Name, string Licence, string Train, string? Dev, string? Test, int TrainSentences);

/// <summary>A language of the corpus: what to train on, what to hold out, and what raw text needs.</summary>
/// <param name="Code">ISO 639 code, also the name of the language's heads.</param>
/// <param name="Name">English name of the language.</param>
/// <param name="Nine">One of the nine languages of the earlier recipe: its data stays what it was so rows stay comparable.</param>
/// <param name="Treebanks">The gating treebank first, then the extra ones.</param>
/// <param name="Ner">Entity corpora, possibly none.</param>
/// <param name="DevFromTrain">No treebank has a dev split: every <see cref="CorpusSpec.HeldOutEvery"/>th training sentence is held out as dev.</param>
/// <param name="HasDev">Whether selection sees this language (a language with fewer than 200 sentences and no dev split trains on all of it).</param>
/// <param name="Small">Fewer than 500 training sentences in all: the sampler caps its repetition.</param>
/// <param name="Segmenter"><c>none</c> or <c>character-tagger</c>: raw text of this language needs a trained word segmenter in front.</param>
/// <param name="TrainSentences">Training sentences over all treebanks (before any hold-out).</param>
/// <param name="TrainWords">Training words over all treebanks (before any hold-out).</param>
public sealed record CorpusLanguage(
    string Code,
    string Name,
    bool Nine,
    IReadOnlyList<CorpusTreebank> Treebanks,
    IReadOnlyList<CorpusEntitySet> Ner,
    bool DevFromTrain,
    bool HasDev,
    bool Small,
    string Segmenter,
    int TrainSentences,
    int TrainWords)
{
    /// <summary>The treebank whose test split is the language's test set.</summary>
    public CorpusTreebank Gating => Treebanks[0];
}

/// <summary>
/// The multilingual training corpus as data: which languages, which files, which splits. Written by <c>tools/accurate/make_corpus_spec.py</c> from the
/// fetched data and read by every accurate-tier recipe, so a language is a row of this file and not a branch of code.
/// </summary>
public sealed class CorpusSpec
{
    private const int MaxLanguages = 1000;
    private const int MaxTreebanks = 64;

    private readonly Dictionary<string, CorpusLanguage> _byCode;

    private CorpusSpec(IReadOnlyList<CorpusLanguage> languages, int heldOutEvery)
    {
        Languages = languages;
        HeldOutEvery = heldOutEvery;
        _byCode = languages.ToDictionary(l => l.Code, StringComparer.Ordinal);
    }

    /// <summary>Every language, ordered by code.</summary>
    public IReadOnlyList<CorpusLanguage> Languages { get; }

    /// <summary>For a language with <see cref="CorpusLanguage.DevFromTrain"/>: every this-many-th training sentence is dev.</summary>
    public int HeldOutEvery { get; }

    /// <summary>The language with this code.</summary>
    /// <param name="code">An ISO 639 code.</param>
    /// <exception cref="KeyNotFoundException">The corpus has no such language.</exception>
    public CorpusLanguage this[string code] => _byCode.TryGetValue(code, out CorpusLanguage? l) ? l : throw new KeyNotFoundException("The corpus has no language " + code + ".");

    /// <summary>Whether the corpus has this language.</summary>
    /// <param name="code">An ISO 639 code.</param>
    public bool Contains(string code) => _byCode.ContainsKey(code);

    /// <summary>Reads a spec file.</summary>
    /// <param name="path">Path of the JSON file.</param>
    /// <exception cref="InvalidDataException">The file is not a corpus spec.</exception>
    public static CorpusSpec Load(string path)
    {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllBytes(path));
        JsonElement root = doc.RootElement;
        int every = Int(root, "heldOutEvery");
        if (every < 2)
        {
            throw new InvalidDataException("heldOutEvery must be at least 2.");
        }

        JsonElement langs = Prop(root, "languages");
        if (langs.ValueKind != JsonValueKind.Array || langs.GetArrayLength() is 0 or > MaxLanguages)
        {
            throw new InvalidDataException("languages must be a non-empty array.");
        }

        var result = new List<CorpusLanguage>();
        foreach (JsonElement l in langs.EnumerateArray())
        {
            var treebanks = new List<CorpusTreebank>();
            JsonElement tbs = Prop(l, "treebanks");
            if (tbs.GetArrayLength() is 0 or > MaxTreebanks)
            {
                throw new InvalidDataException("A language needs between 1 and " + MaxTreebanks.ToString(CultureInfo.InvariantCulture) + " treebanks.");
            }

            foreach (JsonElement t in tbs.EnumerateArray())
            {
                JsonElement sizes = Prop(Prop(t, "sizes"), "train");
                treebanks.Add(new CorpusTreebank(Str(t, "name"), Str(t, "role"), Str(t, "licence"), Str(t, "train"), OptStr(t, "dev"), OptStr(t, "test"),
                    Int(sizes, "sentences"), Int(sizes, "words"), t.TryGetProperty("leaks", out JsonElement lk) ? lk.GetInt32() : 0));
            }

            if (treebanks[0].Role != "gating")
            {
                throw new InvalidDataException("The first treebank of a language must be its gating treebank.");
            }

            var ner = new List<CorpusEntitySet>();
            foreach (JsonElement n in Prop(l, "ner").EnumerateArray())
            {
                ner.Add(new CorpusEntitySet(Str(n, "name"), Str(n, "licence"), Str(n, "train"), OptStr(n, "dev"), OptStr(n, "test"), Int(Prop(Prop(n, "sizes"), "train"), "sentences")));
            }

            result.Add(new CorpusLanguage(Str(l, "code"), Str(l, "name"), Bool(l, "nine"), treebanks, ner, Bool(l, "devFromTrain"), Bool(l, "hasDev"), Bool(l, "small"),
                Str(l, "segmenter"), Int(l, "trainSentences"), Int(l, "trainWords")));
        }

        if (result.Select(r => r.Code).Distinct(StringComparer.Ordinal).Count() != result.Count)
        {
            throw new InvalidDataException("Language codes must be unique.");
        }

        return new CorpusSpec(result, every);
    }

    private static JsonElement Prop(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out JsonElement v) ? v : throw new InvalidDataException("Missing property " + name + ".");

    private static string Str(JsonElement e, string name) => Prop(e, name).GetString() ?? throw new InvalidDataException(name + " must be a string.");

    private static string? OptStr(JsonElement e, string name) => e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int Int(JsonElement e, string name) => Prop(e, name).GetInt32();

    private static bool Bool(JsonElement e, string name) => Prop(e, name).GetBoolean();
}
