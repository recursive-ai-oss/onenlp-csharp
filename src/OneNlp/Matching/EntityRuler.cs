namespace RecursiveAI.OneNlp.Matching;

/// <summary>An entity found by an <see cref="EntityRuler"/>: its span (labelled, confidence 1) and the ID of the rule that matched.</summary>
/// <param name="Span">The matched tokens, labelled with the rule's label.</param>
/// <param name="Id">The rule's ID, or <see langword="null"/> when the rule has none.</param>
public readonly record struct RuleEntity(Span Span, string? Id);

/// <summary>
/// A pipeline component that sets entities from rules: token patterns (as <see cref="Matcher"/> takes) and phrases (as
/// <see cref="PhraseMatcher"/> takes), each with a label and an optional ID. Registered as <c>entity_ruler</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Choosing between overlapping matches:</b> all matches are collected, then taken longest first (ties go to the earlier start, then
/// to the rule added first); a match is dropped when it overlaps one already taken. So a longer rule always beats a shorter one inside it.
/// </para>
/// <para>
/// <b>Existing entities</b> (from a model or an earlier ruler): with <see cref="OverwriteEnts"/> false, a match that overlaps an existing
/// entity is dropped and the existing entities stay. With it true, matches win, and each existing entity that a taken match overlaps is removed.
/// Either way the result keeps entities in text order and non-overlapping, with confidence 1 for rule entities.
/// </para>
/// <para>
/// A rule's ID is not stored in the document; read it from <see cref="FindEntities"/>. Rules may be added at any time but not while a
/// document is being processed. Processing is safe from several threads.
/// </para>
/// </remarks>
public sealed class EntityRuler : IPipelineComponent
{
    /// <summary>The registry name, <c>entity_ruler</c>.</summary>
    public const string ComponentName = "entity_ruler";

    private readonly List<EntityRulerPattern> _rules = [];
    private readonly IWordTokenizer? _tokenizer;
    private readonly Lock _gate = new();
    private Snapshot? _snapshot;

    private sealed class Snapshot
    {
        internal required Matcher TokenMatcher { get; init; }

        internal required PhraseMatcher Phrases { get; init; }

        internal required (string Label, string? Id)[] Entities { get; init; }

        internal required Dictionary<string, int> KeyToEntity { get; init; }
    }

    /// <summary>Creates a ruler with no rules.</summary>
    /// <param name="overwriteEnts">Whether matches replace overlapping entities already in the document; see the remarks.</param>
    /// <param name="phraseMatcherAttribute">The attribute phrase rules compare on: Orth (the default), Lower, Norm or Shape (the others need annotated phrases and are not available for rules given as text).</param>
    /// <param name="phraseTokenizer">Splits phrase rules into tokens; the rule word tokenizer when <see langword="null"/>. It should be the tokenizer of the pipeline the ruler runs in.</param>
    /// <exception cref="ArgumentException">The attribute cannot be used for phrases.</exception>
    public EntityRuler(bool overwriteEnts = false, TokenProperty phraseMatcherAttribute = TokenProperty.Orth, IWordTokenizer? phraseTokenizer = null)
    {
        if (phraseMatcherAttribute is not (TokenProperty.Orth or TokenProperty.Lower or TokenProperty.Norm or TokenProperty.Shape))
        {
            throw new ArgumentException("Phrase rules given as text compare on Orth, Lower, Norm or Shape.", nameof(phraseMatcherAttribute));
        }

        OverwriteEnts = overwriteEnts;
        PhraseMatcherAttribute = phraseMatcherAttribute;
        _tokenizer = phraseTokenizer;
    }

    /// <inheritdoc/>
    public string Name => ComponentName;

    /// <summary>
    /// The registry factory. Settings: <c>overwrite_ents</c> (<c>true</c> or <c>false</c>), <c>phrase_matcher_attr</c> (<c>ORTH</c>, <c>LOWER</c>, <c>NORM</c> or
    /// <c>SHAPE</c>) and <c>patterns_path</c> (a pattern file to load).
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <exception cref="ArgumentException">A setting has an unusable value.</exception>
    /// <returns>A new ruler.</returns>
    public static EntityRuler FromSettings(IReadOnlyDictionary<string, string> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        bool overwrite = false;
        if (settings.TryGetValue("overwrite_ents", out string? o) && !bool.TryParse(o, out overwrite))
        {
            throw new ArgumentException("overwrite_ents must be true or false.", nameof(settings));
        }

        TokenProperty attribute = TokenProperty.Orth;
        if (settings.TryGetValue("phrase_matcher_attr", out string? a) && !TokenProperties.TryParse(a, out attribute))
        {
            throw new ArgumentException($"phrase_matcher_attr '{a}' is not a token attribute.", nameof(settings));
        }

        var ruler = new EntityRuler(overwrite, attribute);
        if (settings.TryGetValue("patterns_path", out string? path))
        {
            ruler.LoadPatterns(path);
        }

        return ruler;
    }

    /// <summary>Whether matches replace overlapping entities already in the document. Default false.</summary>
    public bool OverwriteEnts { get; set; }

    /// <summary>The token attribute phrase rules compare on.</summary>
    public TokenProperty PhraseMatcherAttribute { get; }

    /// <summary>The rules added so far, in order.</summary>
    public IReadOnlyList<EntityRulerPattern> Patterns
    {
        get
        {
            lock (_gate)
            {
                return [.. _rules];
            }
        }
    }

    /// <summary>Adds a token-pattern rule with the label as its index: <c>new EntityRuler { ["CASE_NO"] = Pattern.Shape("dd-dddd") }</c>. Assigning again adds another rule; nothing is replaced.</summary>
    /// <param name="label">The entity label; not empty.</param>
    public Pattern this[string label]
    {
        set => Add(label, value);
    }

    /// <summary>Adds a token-pattern rule.</summary>
    /// <param name="label">The entity label; not empty.</param>
    /// <param name="pattern">The pattern.</param>
    /// <param name="id">An optional entity ID.</param>
    public void Add(string label, Pattern pattern, string? id = null) => Add(EntityRulerPattern.ForTokens(label, pattern, id));

    /// <summary>Adds a phrase rule.</summary>
    /// <param name="label">The entity label; not empty.</param>
    /// <param name="phrase">The phrase text; split into tokens with the ruler's tokenizer.</param>
    /// <param name="id">An optional entity ID.</param>
    public void AddPhrase(string label, string phrase, string? id = null) => Add(EntityRulerPattern.ForPhrase(label, phrase, id));

    /// <summary>Adds a rule.</summary>
    /// <param name="rule">The rule.</param>
    public void Add(EntityRulerPattern rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        lock (_gate)
        {
            _rules.Add(rule);
            _snapshot = null;
        }
    }

    /// <summary>Adds rules read from a pattern file (JSON Lines or a JSON array; see <see cref="PatternJson"/>).</summary>
    /// <param name="stream">The UTF-8 stream, read to its end.</param>
    /// <exception cref="PatternFormatException">The file is invalid. No rule from it is added.</exception>
    public void LoadPatterns(Stream stream)
    {
        IReadOnlyList<EntityRulerPattern> rules = PatternJson.ReadEntityPatterns(stream);
        lock (_gate)
        {
            _rules.AddRange(rules);
            _snapshot = null;
        }
    }

    /// <summary>Adds rules read from a pattern file.</summary>
    /// <param name="path">The path of a JSON Lines file or a JSON array file.</param>
    /// <exception cref="PatternFormatException">The file is invalid. No rule from it is added.</exception>
    public void LoadPatterns(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        using FileStream stream = File.OpenRead(path);
        LoadPatterns(stream);
    }

    /// <summary>Writes the rules as JSON Lines.</summary>
    /// <param name="stream">The destination.</param>
    public void SavePatterns(Stream stream) => PatternJson.WriteEntityPatterns(stream, Patterns);

    /// <summary>Writes the rules to a JSON Lines file, replacing it.</summary>
    /// <param name="path">The path.</param>
    public void SavePatterns(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        using FileStream stream = File.Create(path);
        SavePatterns(stream);
    }

    /// <inheritdoc/>
    public void Process(Doc doc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        IReadOnlyList<RuleEntity> found = FindEntities(doc);
        IReadOnlyList<Span> existing = doc.Ents;
        bool hasAnnotation = doc.Length > 0 && doc[0].EntIob != EntIob.Unset;
        if (found.Count == 0 && hasAnnotation)
        {
            return;
        }

        var all = new List<SpanSpec>(existing.Count + found.Count);
        foreach (Span e in existing)
        {
            bool replaced = false;
            if (OverwriteEnts)
            {
                foreach (RuleEntity r in found)
                {
                    if (r.Span.Start < e.End && e.Start < r.Span.End)
                    {
                        replaced = true;
                        break;
                    }
                }
            }

            if (!replaced)
            {
                all.Add(new SpanSpec(e.Start, e.End, e.Label, e.Confidence));
            }
        }

        foreach (RuleEntity r in found)
        {
            all.Add(new SpanSpec(r.Span.Start, r.Span.End, r.Span.Label));
        }

        all.Sort(static (a, b) => a.Start.CompareTo(b.Start));
        doc.SetEntities([.. all]);
    }

    /// <summary>Finds the entities the rules give a document, after choosing between overlapping matches and against existing entities, without changing the document.</summary>
    /// <param name="doc">The document.</param>
    /// <returns>The entities in text order, labelled and with their rule IDs.</returns>
    public IReadOnlyList<RuleEntity> FindEntities(Doc doc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        Snapshot s = GetSnapshot();
        List<(int Start, int End, int Entity)> matches = CollectMatches(doc, s);

        // Longest first, then earliest, then the rule added first.
        matches.Sort(static (a, b) =>
        {
            int c = (b.End - b.Start).CompareTo(a.End - a.Start);
            if (c != 0)
            {
                return c;
            }

            c = a.Start.CompareTo(b.Start);
            return c != 0 ? c : a.Entity.CompareTo(b.Entity);
        });

        IReadOnlyList<Span> existing = doc.Ents;
        var taken = new bool[doc.Length];
        var existingTokens = new bool[doc.Length];
        foreach (Span e in existing)
        {
            Array.Fill(existingTokens, true, e.Start, e.Length);
        }

        var chosen = new List<(int Start, int End, int Entity)>();
        foreach ((int start, int end, int entity) in matches)
        {
            if (Any(taken, start, end) || (!OverwriteEnts && Any(existingTokens, start, end)))
            {
                continue;
            }

            Array.Fill(taken, true, start, end - start);
            chosen.Add((start, end, entity));
        }

        chosen.Sort(static (a, b) => a.Start.CompareTo(b.Start));
        var result = new RuleEntity[chosen.Count];
        for (int i = 0; i < result.Length; i++)
        {
            (string label, string? id) = s.Entities[chosen[i].Entity];
            result[i] = new RuleEntity(new Span(doc, chosen[i].Start, chosen[i].End, label), id);
        }

        return result;
    }

    // Every match of every rule, before overlaps are resolved.
    internal List<(int Start, int End, int Entity)> CollectMatches(Doc doc) => CollectMatches(doc, GetSnapshot());

    private static List<(int Start, int End, int Entity)> CollectMatches(Doc doc, Snapshot s)
    {
        var matches = new List<(int Start, int End, int Entity)>();
        foreach (PatternMatch m in s.TokenMatcher.Match(doc))
        {
            matches.Add((m.Start, m.End, s.KeyToEntity[m.Key]));
        }

        foreach (PatternMatch m in s.Phrases.Match(doc))
        {
            matches.Add((m.Start, m.End, s.KeyToEntity[m.Key]));
        }

        return matches;
    }

    private static bool Any(bool[] flags, int start, int end)
    {
        for (int i = start; i < end; i++)
        {
            if (flags[i])
            {
                return true;
            }
        }

        return false;
    }

    private Snapshot GetSnapshot()
    {
        if (_snapshot is { } ready)
        {
            return ready;
        }

        lock (_gate)
        {
            return _snapshot ??= Build();
        }
    }

    private Snapshot Build()
    {
        var matcher = new Matcher();
        var phrases = new PhraseMatcher(PhraseMatcherAttribute, _tokenizer);
        var entities = new List<(string Label, string? Id)>();
        var keyToEntity = new Dictionary<string, int>(StringComparer.Ordinal);
        var phraseGroups = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (EntityRulerPattern rule in _rules)
        {
            // One key per distinct (label, id): rules that name the same entity share it.
            string key = rule.Label + "\u001F" + (rule.Id ?? string.Empty) + (rule.Id is null ? "\u001E" : string.Empty);
            if (!keyToEntity.TryGetValue(key, out int entity))
            {
                entity = entities.Count;
                keyToEntity[key] = entity;
                entities.Add((rule.Label, rule.Id));
            }

            if (rule.Tokens is { } tokens)
            {
                matcher.Add(key, tokens);
            }
            else
            {
                if (!phraseGroups.TryGetValue(key, out List<string>? group))
                {
                    phraseGroups[key] = group = [];
                }

                group.Add(rule.Phrase!);
            }
        }

        foreach ((string key, List<string> group) in phraseGroups)
        {
            phrases.Add(key, group);
        }

        return new Snapshot { TokenMatcher = matcher, Phrases = phrases, Entities = [.. entities], KeyToEntity = keyToEntity };
    }
}
