namespace RecursiveAI.OneNlp.LanguageId;

/// <summary>
/// Identifies the language of a text from a compact model of 75 languages. Works from single words to long documents, can be
/// restricted to the languages you expect, reports a confidence for each candidate, and can split a text that mixes languages.
/// </summary>
/// <remarks>
/// <para>
/// How it works (Cavnar and Trenkle 1994; Joulin et al. 2016): the letters of the text decide its script, and the script picks a
/// group of candidate languages (Cyrillic: Russian, Ukrainian, …). Within the group a linear softmax model over hashed character
/// 1- to 5-grams of the words, plus whole-word features, scores each language. Digits and punctuation are ignored.
/// </para>
/// <para>
/// A detector is immutable and thread-safe. <see cref="Default"/> loads the model that ships inside this assembly on first use.
/// Nothing is downloaded.
/// </para>
/// </remarks>
public sealed class LanguageDetector
{
    internal const string ResourceName = "weights/langid.bin";
    private static readonly Lazy<LanguageDetector?> DefaultInstance = new(LoadEmbedded);

    [ThreadStatic]
    private static LangIdText? s_scratch;

    private readonly LanguageModel _model;
    private readonly Language[] _languages;
    private readonly bool[] _allowed;

    private LanguageDetector(LanguageModel model, bool[]? allowed)
    {
        _model = model;
        _languages = [.. model.Codes.Select(Language.FromCode)];
        _allowed = allowed ?? [.. model.Codes.Select(static _ => true)];
        Languages = [.. _languages.Where((_, i) => _allowed[i])];
    }

    /// <summary>The detector over all languages of the shipped model.</summary>
    /// <exception cref="InvalidOperationException">This build of the library carries no language-ID model (a development build made without the environment variable <c>ONENLP_LANGID_WEIGHTS</c>; packed builds always carry it).</exception>
    public static LanguageDetector Default => DefaultInstance.Value
        ?? throw new InvalidOperationException("This build of RecursiveAI.OneNlp carries no language-identification model. Build with the environment variable ONENLP_LANGID_WEIGHTS set to the directory that holds langid.bin (see src/OneNlp/LanguageId/weights.manifest.props), or use a published package.");

    /// <summary>Whether this build carries the language-ID model, and the detector over it.</summary>
    internal static bool TryGetDefault([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out LanguageDetector? detector)
    {
        detector = DefaultInstance.Value;
        return detector is not null;
    }

    /// <summary>The languages this detector can answer with.</summary>
    public IReadOnlyList<Language> Languages { get; }

    /// <summary>Reads a detector from a model stream written by the training tool.</summary>
    /// <param name="stream">A readable stream positioned at the start of a model.</param>
    /// <returns>A detector over all languages in the model.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">The data is not a valid model: wrong magic number, truncated, or sizes beyond the limits.</exception>
    public static LanguageDetector Load(Stream stream) => new(LanguageModel.Read(stream), null);

    /// <summary>A detector over only <paramref name="languages"/>.</summary>
    /// <param name="languages">The languages to choose between. Languages the model does not know are ignored.</param>
    /// <returns>A detector that never answers with a language outside the set. It shares this detector's model.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="languages"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">None of the languages is in the model.</exception>
    /// <remarks>Restricting helps most for short texts. When the text is in a script that none of the allowed languages uses, the answer is <see cref="Language.Unknown"/>.</remarks>
    public LanguageDetector Restrict(IEnumerable<Language> languages)
    {
        ArgumentNullException.ThrowIfNull(languages);
        var set = new HashSet<Language>(languages);
        bool[] allowed = [.. _languages.Select((l, i) => set.Contains(l) && _allowed[i])];
        if (!allowed.Contains(true))
        {
            throw new ArgumentException("None of the languages is supported by the model.", nameof(languages));
        }

        return new LanguageDetector(_model, allowed);
    }

    /// <summary>The most likely language of a text.</summary>
    /// <param name="text">Any text, from one word up. Digits and punctuation are ignored.</param>
    /// <returns>The language, or <see cref="Language.Unknown"/> when the text has no letters of a supported script.</returns>
    public Language Detect(ReadOnlySpan<char> text) => Detect(text, out _);

    /// <summary>The most likely language of a text and its confidence.</summary>
    /// <param name="text">Any text, from one word up.</param>
    /// <param name="confidence">The probability of the answer, 0 when it is <see cref="Language.Unknown"/>. A text in a script used by one language only gets 1.</param>
    /// <returns>The language, or <see cref="Language.Unknown"/> when the text has no letters of a supported script or none of the allowed languages use its script.</returns>
    public Language Detect(ReadOnlySpan<char> text, out double confidence)
    {
        List<LanguageConfidence> all = Rank(text, 1);
        if (all.Count == 0)
        {
            confidence = 0;
            return default;
        }

        confidence = all[0].Confidence;
        return all[0].Language;
    }

    /// <summary>The candidate languages of a text, most likely first.</summary>
    /// <param name="text">Any text, from one word up.</param>
    /// <returns>The languages that use the text's script, with probabilities that sum to 1; empty when the text has no letters of a supported script.</returns>
    public IReadOnlyList<LanguageConfidence> Confidences(ReadOnlySpan<char> text) => Rank(text, int.MaxValue);

    private List<LanguageConfidence> Rank(ReadOnlySpan<char> text, int limit)
    {
        LangIdText t = Analyze(text);
        var result = new List<LanguageConfidence>();
        if (t.WordCount == 0)
        {
            return result;
        }

        int group = ChooseGroup(t, 0, t.WordCount);
        if (group < 0)
        {
            return result;
        }

        LanguageModel.Group g = _model.Groups[group];
        Span<float> z = stackalloc float[g.Classes.Length]; // at most 255 classes
        _ = _model.Score(t, 0, t.WordCount, group, z);
        Softmax(g, z);
        for (int c = 0; c < z.Length; c++)
        {
            if (_allowed[g.Classes[c]])
            {
                result.Add(new LanguageConfidence(_languages[g.Classes[c]], z[c]));
            }
        }

        result.Sort(static (a, b) => b.Confidence.CompareTo(a.Confidence));
        if (result.Count > limit)
        {
            result.RemoveRange(limit, result.Count - limit);
        }

        return result;
    }

    // Turns scores into probabilities over the allowed classes of the group; the others get 0.
    private void Softmax(LanguageModel.Group g, Span<float> z)
    {
        if (z.Length == 1)
        {
            z[0] = _allowed[g.Classes[0]] ? 1f : 0f;
            return;
        }

        float max = float.NegativeInfinity;
        for (int c = 0; c < z.Length; c++)
        {
            if (_allowed[g.Classes[c]] && z[c] > max)
            {
                max = z[c];
            }
        }

        float sum = 0;
        for (int c = 0; c < z.Length; c++)
        {
            z[c] = _allowed[g.Classes[c]] ? MathF.Exp((z[c] - max) * _model.Temperature) : 0f;
            sum += z[c];
        }

        for (int c = 0; c < z.Length && sum > 0; c++)
        {
            z[c] /= sum;
        }
    }

    /// <summary>Splits a text that may mix languages into runs of one language.</summary>
    /// <param name="text">Any text.</param>
    /// <param name="switchPenalty">How strongly the detector prefers staying in one language, in nats of log-probability per switch. Higher means fewer, longer runs. Default 9.</param>
    /// <returns>
    /// Runs in text order that together cover the text from index 0 to its length, each with its language and confidence; empty
    /// when the text has no letters. A run begins at the first word of a new language, so punctuation and spaces after a run
    /// belong to it. Runs are words long at the shortest, so a single foreign word inside a sentence is usually absorbed.
    /// </returns>
    /// <remarks>
    /// Each word is scored alone, and a Viterbi search (Viterbi 1967) over the words picks the language sequence that maximizes the summed
    /// word log-probabilities minus <paramref name="switchPenalty"/> for every change of language.
    /// </remarks>
    public IReadOnlyList<LanguageSpan> DetectSpans(ReadOnlySpan<char> text, double switchPenalty = 9.0)
    {
        LangIdText t = Analyze(text);
        int words = t.WordCount;
        var spans = new List<LanguageSpan>();
        if (words == 0)
        {
            return spans;
        }

        int nl = _languages.Length;
        const float Floor = -12f;
        var emit = new float[words * nl];
        Span<float> z = stackalloc float[256]; // a group has at most 255 classes
        for (int w = 0; w < words; w++)
        {
            int g = _model.GroupOf(t.WordScript[w]);
            Span<float> row = emit.AsSpan(w * nl, nl);
            if (g < 0)
            {
                continue; // a word of an unsupported script says nothing: every language scores 0
            }

            LanguageModel.Group grp = _model.Groups[g];
            Span<float> zs = z[..grp.Classes.Length];
            _ = _model.Score(t, w, w + 1, g, zs);
            Softmax(grp, zs);
            row.Fill(Floor);
            for (int c = 0; c < zs.Length; c++)
            {
                if (_allowed[grp.Classes[c]])
                {
                    row[grp.Classes[c]] = MathF.Max(Floor, MathF.Log(MathF.Max(zs[c], 1e-6f)));
                }
            }
        }

        // Viterbi: score[l] is the best total of a path ending in language l; back[w, l] says whether it switched.
        var score = new float[nl];
        var next = new float[nl];
        var from = new int[words * nl];
        float pen = (float)switchPenalty;
        for (int l = 0; l < nl; l++)
        {
            score[l] = _allowed[l] ? emit[l] : float.NegativeInfinity;
        }

        for (int w = 1; w < words; w++)
        {
            int bestPrev = 0;
            for (int l = 1; l < nl; l++)
            {
                if (score[l] > score[bestPrev])
                {
                    bestPrev = l;
                }
            }

            for (int l = 0; l < nl; l++)
            {
                if (!_allowed[l])
                {
                    next[l] = float.NegativeInfinity;
                    continue;
                }

                float stay = score[l];
                float move = score[bestPrev] - pen;
                if (stay >= move)
                {
                    next[l] = stay + emit[(w * nl) + l];
                    from[(w * nl) + l] = l;
                }
                else
                {
                    next[l] = move + emit[(w * nl) + l];
                    from[(w * nl) + l] = bestPrev;
                }
            }

            (score, next) = (next, score);
        }

        int cur = 0;
        for (int l = 1; l < nl; l++)
        {
            if (score[l] > score[cur])
            {
                cur = l;
            }
        }

        var path = new int[words];
        for (int w = words - 1; w >= 0; w--)
        {
            path[w] = cur;
            if (w > 0)
            {
                cur = from[(w * nl) + cur];
            }
        }

        int runStart = 0;
        for (int w = 1; w <= words; w++)
        {
            if (w < words && path[w] == path[runStart])
            {
                continue;
            }

            int start = runStart == 0 ? 0 : t.CharStart[runStart];
            int end = w == words ? text.Length : t.CharStart[w];
            spans.Add(new LanguageSpan(_languages[path[runStart]], start, end, RunConfidence(t, runStart, w, path[runStart])));
            runStart = w;
        }

        return spans;
    }

    private double RunConfidence(LangIdText t, int from, int to, int language)
    {
        int group = ChooseGroup(t, from, to);
        if (group < 0)
        {
            return 0;
        }

        LanguageModel.Group g = _model.Groups[group];
        Span<float> z = stackalloc float[g.Classes.Length]; // at most 255 classes
        _ = _model.Score(t, from, to, group, z);
        Softmax(g, z);
        for (int c = 0; c < z.Length; c++)
        {
            if (g.Classes[c] == language)
            {
                return z[c];
            }
        }

        return 0;
    }

    // The group whose scripts have the most letters among words [from, to); -1 when no word has a supported script.
    private int ChooseGroup(LangIdText t, int from, int to)
    {
        Span<int> letters = stackalloc int[_model.Groups.Length]; // at most 32 groups
        letters.Clear();
        bool any = false;
        for (int w = from; w < to; w++)
        {
            int g = _model.GroupOf(t.WordScript[w]);
            if (g >= 0)
            {
                letters[g] += t.CharEnd[w] - t.CharStart[w];
                any = true;
            }
        }

        if (!any)
        {
            return -1;
        }

        // Prefer a group that has an allowed language; a group with none cannot answer.
        int best = -1;
        for (int g = 0; g < letters.Length; g++)
        {
            if (letters[g] > 0 && GroupAllowed(g) && (best < 0 || letters[g] > letters[best]))
            {
                best = g;
            }
        }

        return best;
    }

    private bool GroupAllowed(int g)
    {
        foreach (int c in _model.Groups[g].Classes)
        {
            if (_allowed[c])
            {
                return true;
            }
        }

        return false;
    }

    private static LangIdText Analyze(ReadOnlySpan<char> text)
    {
        LangIdText t = s_scratch ??= new LangIdText();
        t.Run(text);
        return t;
    }

    private static LanguageDetector? LoadEmbedded()
    {
        using Stream? stream = typeof(LanguageDetector).Assembly.GetManifestResourceStream(ResourceName);
        return stream is null ? null : Load(stream);
    }
}
