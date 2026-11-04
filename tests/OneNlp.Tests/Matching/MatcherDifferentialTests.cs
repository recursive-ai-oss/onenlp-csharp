using System.Globalization;
using System.Text;
using System.Text.Json;
using RecursiveAI.OneNlp.Matching;

namespace RecursiveAI.OneNlp.Tests.Matching;

/// <summary>
/// Compares matching with the output of spaCy 3.8.18 on the same documents. The documents, patterns and spaCy's answers are committed
/// (tools/oracles/spacy_matcher regenerates them); tokens come from the cases, so tokenization does not enter the comparison.
/// </summary>
public class MatcherDifferentialTests
{
    private static List<Doc> BuildDocs(StringStore strings) =>
        [.. OracleFixtures.Cases.GetProperty("docs").EnumerateArray().Select(d => OracleFixtures.MakeDoc(strings, d))];

    [Fact]
    public void OracleVersionIsPinned()
    {
        Assert.Equal("3.8.18", OracleFixtures.Expected.GetProperty("spacy").GetString());
    }

    [Fact]
    public void MatcherAgreesWithSpacy()
    {
        var strings = new StringStore();
        List<Doc> docs = BuildDocs(strings);
        string[] stop = OracleFixtures.Strings(OracleFixtures.Cases.GetProperty("stop_words"));
        JsonElement expected = OracleFixtures.Expected.GetProperty("matcher");
        var problems = new List<string>();
        int specs = 0;
        long matchCount = 0;
        foreach (JsonElement spec in OracleFixtures.Cases.GetProperty("matcher").EnumerateArray())
        {
            string id = spec.GetProperty("id").GetInt32().ToString(CultureInfo.InvariantCulture);
            var matcher = new Matcher { StopWords = stop };
            GreedyMode greedy = spec.GetProperty("greedy").ValueKind == JsonValueKind.Null
                ? GreedyMode.None
                : spec.GetProperty("greedy").GetString() == "FIRST" ? GreedyMode.First : GreedyMode.Longest;
            matcher.Add("K", PatternJson.ParsePatterns(spec.GetProperty("patterns").GetRawText()), greedy);

            var mine = new List<(int Doc, int Start, int End)>();
            for (int d = 0; d < docs.Count; d++)
            {
                foreach (PatternMatch m in matcher.Match(docs[d]))
                {
                    mine.Add((d, m.Start, m.End));
                }
            }

            // spaCy's documentation does not say in what order matches come back, so the comparison is on the sets; ours are checked
            // for the order we document (end, then start) separately.
            var theirs = new List<(int, int, int)>();
            foreach (JsonElement t in expected.GetProperty(id).EnumerateArray())
            {
                theirs.Add((t[0].GetInt32(), t[1].GetInt32(), t[2].GetInt32()));
            }

            specs++;
            matchCount += theirs.Count;
            if (!mine.Order().SequenceEqual(theirs.Order()))
            {
                problems.Add($"spec {id} {spec.GetProperty("patterns").GetRawText()} greedy={greedy}: mine {mine.Count} theirs {theirs.Count}; "
                    + $"only mine {string.Join(",", mine.Except(theirs).Take(3))}; only theirs {string.Join(",", theirs.Except(mine).Take(3))}");
            }

            for (int i = 1; i < mine.Count; i++)
            {
                if (mine[i].Doc == mine[i - 1].Doc && (mine[i].End, mine[i].Start).CompareTo((mine[i - 1].End, mine[i - 1].Start)) < 0)
                {
                    problems.Add($"spec {id}: results are not ordered by end, then start");
                    break;
                }
            }
        }

        TestContext.Current.SendDiagnosticMessage($"matcher: {specs} specs over {docs.Count} documents, {matchCount} spaCy matches, {problems.Count} differ");
        Assert.True(specs >= 200, "at least 200 patterns");
        Assert.True(problems.Count == 0, $"{problems.Count} of {specs} specs differ ({matchCount} matches):\n" + string.Join("\n", problems.Take(12)));
    }

    [Fact]
    public void PhraseMatcherAgreesWithSpacy()
    {
        var strings = new StringStore();
        List<Doc> docs = BuildDocs(strings);
        JsonElement expected = OracleFixtures.Expected.GetProperty("phrase");
        var problems = new List<string>();
        foreach (JsonElement spec in OracleFixtures.Cases.GetProperty("phrase").EnumerateArray())
        {
            string id = spec.GetProperty("id").GetInt32().ToString(CultureInfo.InvariantCulture);
            Assert.True(TokenProperties.TryParse(spec.GetProperty("attr").GetString()!, out TokenProperty attr));
            var matcher = new PhraseMatcher(attr);
            foreach (JsonProperty key in spec.GetProperty("keys").EnumerateObject())
            {
                if (key.Value.GetArrayLength() > 0)
                {
                    matcher.Add(key.Name, key.Value.EnumerateArray().Select(p => OracleFixtures.MakePhraseDoc(strings, p)).ToList());
                }
            }

            var mine = new List<(int, int, int, string)>();
            for (int d = 0; d < docs.Count; d++)
            {
                foreach (PatternMatch m in matcher.Match(docs[d]))
                {
                    mine.Add((d, m.Start, m.End, m.Key));
                }
            }

            var theirs = new List<(int, int, int, string)>();
            foreach (JsonElement t in expected.GetProperty(id).EnumerateArray())
            {
                theirs.Add((t[0].GetInt32(), t[1].GetInt32(), t[2].GetInt32(), t[3].GetString()!));
            }

            // The order of matches with equal end and start but different keys is not defined by spaCy's documentation: compare as sets.
            if (!mine.Order().SequenceEqual(theirs.Order()))
            {
                problems.Add($"phrase spec {id} attr {attr}: mine {mine.Count} theirs {theirs.Count}; only mine {string.Join(",", mine.Except(theirs).Take(3))}; only theirs {string.Join(",", theirs.Except(mine).Take(3))}");
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems.Take(12)));
    }

    [Fact]
    public void EntityRulerAgreesWithSpacy()
    {
        var strings = new StringStore();
        JsonElement.ArrayEnumerator docCases = OracleFixtures.Cases.GetProperty("docs").EnumerateArray();
        List<JsonElement> docJson = [.. docCases];
        JsonElement expected = OracleFixtures.Expected.GetProperty("ruler");
        var problems = new List<string>();
        int compared = 0;
        int ambiguous = 0;
        foreach (JsonElement spec in OracleFixtures.Cases.GetProperty("ruler").EnumerateArray())
        {
            string id = spec.GetProperty("id").GetInt32().ToString(CultureInfo.InvariantCulture);
            var ruler = new EntityRuler(
                overwriteEnts: spec.GetProperty("overwrite").GetBoolean(),
                phraseMatcherAttribute: spec.GetProperty("attr").GetString() == "LOWER" ? TokenProperty.Lower : TokenProperty.Orth);
            foreach (EntityRulerPattern p in PatternJson.ReadEntityPatterns(Encoding.UTF8.GetBytes(spec.GetProperty("patterns").GetRawText())))
            {
                ruler.Add(p);
            }

            JsonElement perDoc = expected.GetProperty(id);
            for (int d = 0; d < docJson.Count; d++)
            {
                Doc doc = OracleFixtures.MakeDoc(strings, docJson[d]);

                // When two different rules match exactly the same tokens, which one spaCy keeps is not documented (it depends on internal
                // hash order), so those documents are left out of the comparison.
                List<(int Start, int End, int Entity)> all = ruler.CollectMatches(doc);
                if (all.GroupBy(m => (m.Start, m.End)).Any(g => g.Select(m => m.Entity).Distinct().Count() > 1))
                {
                    ambiguous++;
                    continue;
                }

                IReadOnlyList<RuleEntity> found = ruler.FindEntities(doc);
                ruler.Process(doc);
                var ids = found.ToDictionary(f => f.Span.Start, f => f.Id ?? string.Empty);
                string mine = string.Join(";", doc.Ents.Select(e => $"{e.Start},{e.End},{e.Label},{(ids.TryGetValue(e.Start, out string? i) ? i : string.Empty)}"));
                string theirs = string.Join(";", perDoc[d].EnumerateArray().Select(e => $"{e[0].GetInt32()},{e[1].GetInt32()},{e[2].GetString()},{e[3].GetString()}"));
                compared++;
                if (mine != theirs)
                {
                    problems.Add($"ruler {id} doc {d} overwrite={ruler.OverwriteEnts}: mine [{mine}] theirs [{theirs}] before [{string.Join(";", OracleFixtures.MakeDoc(strings, docJson[d]).Ents.Select(e => $"{e.Start},{e.End},{e.Label}"))}] rules {spec.GetProperty("patterns").GetRawText()[..Math.Min(300, spec.GetProperty("patterns").GetRawText().Length)]}");
                }
            }
        }

        TestContext.Current.SendDiagnosticMessage($"ruler: {compared} documents compared, {ambiguous} skipped for same-span ties, {problems.Count} differ");
        Assert.True(problems.Count == 0, $"{problems.Count} of {compared} differ ({ambiguous} ambiguous skipped):\n" + string.Join("\n", problems.Take(6)));
        Assert.True(compared > 20000, $"only {compared} documents compared");
    }

    [Fact]
    public void InvalidPatternsAreRejectedAsSpacyRejectsThem()
    {
        JsonElement cases = OracleFixtures.Cases;
        var problems = new List<string>();
        void Check(string property, string expectedProperty)
        {
            int i = 0;
            foreach (JsonElement p in cases.GetProperty(property).EnumerateArray())
            {
                bool theirs = OracleFixtures.Expected.GetProperty(expectedProperty)[i].GetBoolean();
                bool mine;
                try
                {
                    var m = new Matcher();
                    m.Add("K", PatternJson.ParsePatterns(p.GetRawText()));
                    mine = true;
                }
                catch (PatternFormatException)
                {
                    mine = false;
                }

                if (mine != theirs)
                {
                    problems.Add($"{p.GetRawText()}: mine accepts={mine}, spaCy accepts={theirs}");
                }

                i++;
            }
        }

        Check("invalid", "invalid");
        Check("valid_odd", "valid_odd");
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void LexicalFlagsAndShapesAgreeWithSpacy()
    {
        JsonElement strings = OracleFixtures.Cases.GetProperty("lexical");
        JsonElement rows = OracleFixtures.Expected.GetProperty("lexical");
        string[] columns = OracleFixtures.Strings(OracleFixtures.Expected.GetProperty("lexical_columns"));
        var problems = new List<string>();
        for (int i = 0; i < rows.GetArrayLength(); i++)
        {
            string s = strings[i].GetString()!;
            JsonElement row = rows[i];
            bool[] mine =
            [
                TokenText.IsAlpha(s), TokenText.IsAscii(s), TokenText.IsDigit(s), TokenText.IsLower(s), TokenText.IsUpper(s), TokenText.IsTitle(s),
                TokenText.IsPunct(s), TokenText.IsSpace(s), TokenText.LikeNum(s), TokenText.LikeUrl(s), TokenText.LikeEmail(s), TokenText.IsCurrency(s),
                TokenText.IsLeftPunct(s), TokenText.IsRightPunct(s), TokenText.IsQuote(s), TokenText.IsBracket(s),
            ];
            for (int c = 0; c < mine.Length; c++)
            {
                if (row[c].GetBoolean() != mine[c])
                {
                    problems.Add($"{Escape(s)} {columns[c]}: mine {mine[c]}, spaCy {row[c].GetBoolean()}");
                }
            }

            if (TokenText.Shape(s) != row[16].GetString())
            {
                problems.Add($"{Escape(s)} shape: mine {TokenText.Shape(s)}, spaCy {row[16].GetString()}");
            }

            if (TokenText.CodePointCount(s) != row[17].GetInt32())
            {
                problems.Add($"{Escape(s)} length: mine {TokenText.CodePointCount(s)}, spaCy {row[17].GetInt32()}");
            }
        }

        string[] known = KnownLexicalDifferences();
        string[] unexpected = [.. problems.Where(p => !known.Any(k => p.StartsWith(k + " ", StringComparison.Ordinal) || p == k))];
        Assert.True(unexpected.Length == 0, $"{problems.Count} differences, {unexpected.Length} not on the known list ({rows.GetArrayLength()} strings):\n" + string.Join("\n", unexpected.Take(40)));
        string[] stale = [.. known.Where(k => !problems.Any(p => p.StartsWith(k, StringComparison.Ordinal)))];
        Assert.True(stale.Length == 0, "known differences that no longer differ: " + string.Join(", ", stale));
    }

    [Fact]
    public void EveryCodePointFlagAgreesWithSpacy()
    {
        JsonElement chars = OracleFixtures.Expected.GetProperty("chars");
        HashSet<int> unassigned = Expand(OracleFixtures.Expected.GetProperty("chars_unassigned").GetString()!);
        Dictionary<string, Func<string, bool>> mine = new()
        {
            ["is_alpha"] = s => TokenText.IsAlpha(s), ["is_digit"] = s => TokenText.IsDigit(s), ["is_lower"] = s => TokenText.IsLower(s), ["is_upper"] = s => TokenText.IsUpper(s),
            ["is_title"] = s => TokenText.IsTitle(s), ["is_punct"] = s => TokenText.IsPunct(s), ["is_space"] = s => TokenText.IsSpace(s), ["is_currency"] = s => TokenText.IsCurrency(s),
            ["is_ascii"] = s => TokenText.IsAscii(s),
        };
        var problems = new List<string>();
        foreach (KeyValuePair<string, Func<string, bool>> flag in mine)
        {
            HashSet<int> theirs = Expand(chars.GetProperty(flag.Key).GetString()!);
            var bad = new List<int>();
            for (int cp = 0; cp < 0x20000; cp++)
            {
                if ((cp is >= 0xD800 and < 0xE000) || unassigned.Contains(cp))
                {
                    continue;
                }

                if (flag.Value(char.ConvertFromUtf32(cp)) != theirs.Contains(cp))
                {
                    bad.Add(cp);
                }
            }

            if (bad.Count > 0)
            {
                problems.Add($"{flag.Key}: {bad.Count} code points differ, first {string.Join(" ", bad.Take(12).Select(c => c.ToString("X", CultureInfo.InvariantCulture)))}");
            }
        }

        // Shapes of single characters: letters and digits map to x, X, d and everything else stays, which the fixture lists exceptions to.
        foreach (JsonProperty odd in OracleFixtures.Expected.GetProperty("chars_odd_shapes").EnumerateObject())
        {
            string ch = char.ConvertFromUtf32(int.Parse(odd.Name, CultureInfo.InvariantCulture));
            if (TokenText.Shape(ch) != odd.Value.GetString())
            {
                problems.Add($"shape of U+{int.Parse(odd.Name, CultureInfo.InvariantCulture):X4}: mine {TokenText.Shape(ch)}, spaCy {odd.Value.GetString()}");
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    private static HashSet<int> Expand(string ranges)
    {
        var set = new HashSet<int>();
        foreach (string part in ranges.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] ends = part.Split('-');
            int lo = int.Parse(ends[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            int hi = ends.Length > 1 ? int.Parse(ends[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture) : lo;
            for (int c = lo; c <= hi; c++)
            {
                set.Add(c);
            }
        }

        return set;
    }

    private static string Escape(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in s)
        {
            sb.Append(c is < ' ' or > '~' ? $"\\u{(int)c:X4}" : c.ToString());
        }

        return sb.ToString();
    }

    // Strings whose LIKE_URL or LIKE_EMAIL differ from spaCy 3.8.18. The documented definition leaves them open and they are rare forms;
    // each entry is "<escaped string> <column>". The test fails if an entry stops being a difference, so the list cannot go stale.
    private static string[] KnownLexicalDifferences() => [];
}
