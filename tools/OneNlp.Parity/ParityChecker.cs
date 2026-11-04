using System.Buffers;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using RecursiveAI.OneNlp.Tokenizers;

namespace RecursiveAI.OneNlp.Parity;

/// <summary>Limits for a parity run.</summary>
public sealed class ParityOptions
{
    /// <summary>How many mismatches the result keeps with their details.</summary>
    public int MaxExamples { get; init; } = 20;

    /// <summary>How many ready-to-commit test cases are written from the shortest failing inputs.</summary>
    public int MaxTestCases { get; init; } = 10;

    /// <summary>Stop after this many records (0 = all); for quick looks.</summary>
    public int MaxRecords { get; init; }
}

/// <summary>One difference between a tokenizer and the oracle's golden record.</summary>
/// <param name="Check">The API call compared, e.g. <c>encode.special</c>, <c>encode.utf8.plain</c>, <c>ids.char.small</c>, <c>decode.skip</c>.</param>
/// <param name="Field">The field of the result, e.g. <c>ids</c>, <c>offsets</c>, <c>tokens</c>.</param>
/// <param name="RecordId">The corpus record id (pairs: the pair id, class <c>*-pair</c>).</param>
/// <param name="Class">The corpus input class.</param>
/// <param name="Detail">What differed.</param>
/// <param name="InputLength">Input length in UTF-16 units (bytes for UTF-8-only inputs).</param>
/// <param name="TestBody">A self-contained xunit test body that reproduces it, or null when the input is long or the check has no snippet.</param>
public sealed record Mismatch(string Check, string Field, int RecordId, string Class, string Detail, int InputLength, string? TestBody);

/// <summary>The outcome of comparing one tokenizer with one golden file.</summary>
public sealed class FamilyResult
{
    /// <summary>Family id.</summary>
    public required string Family { get; init; }

    /// <summary><c>ok</c>, <c>mismatches</c>, <c>not supported</c> or <c>load failed</c>.</summary>
    public string Status { get; set; } = "ok";

    /// <summary>Why, for the last two statuses.</summary>
    public string? Message { get; set; }

    /// <summary>Oracle name and version from the golden header.</summary>
    public string? Oracle { get; set; }

    /// <summary>Corpus records compared.</summary>
    public int Records { get; set; }

    /// <summary>Pair records compared.</summary>
    public int Pairs { get; set; }

    /// <summary>Field comparisons made (each array, string or digest compared counts one).</summary>
    public long Comparisons { get; set; }

    /// <summary>Total mismatches.</summary>
    public long MismatchCount { get; set; }

    /// <summary>Mismatches by <c>check.field</c>.</summary>
    public SortedDictionary<string, long> ByField { get; } = [];

    /// <summary>Mismatches by input class.</summary>
    public SortedDictionary<string, long> ByClass { get; } = [];

    /// <summary>Mismatches by <c>check.field</c> and class.</summary>
    public SortedDictionary<string, SortedDictionary<string, long>> ByFieldAndClass { get; } = [];

    /// <summary>The first mismatches, shortest inputs first.</summary>
    public List<Mismatch> Examples { get; } = [];

    /// <summary>Test cases built from the shortest failing inputs, one per distinct check and field first.</summary>
    public List<Mismatch> TestCases { get; } = [];

    /// <summary>Seconds spent comparing.</summary>
    public double Seconds { get; set; }

    /// <summary>True when every comparison matched.</summary>
    public bool Passed => Status == "ok";
}

/// <summary>
/// Compares a <see cref="Tokenizer"/> with a golden file written by <c>tools/parity/oracle.py</c>, byte for byte: for every corpus
/// record it runs <c>Encode</c> (char and UTF-8, with and without special tokens, with truncation and padding), <c>EncodeToIds</c>
/// (full and short buffers), <c>CountTokens</c> and <c>Decode</c>, and for pair records the pair <c>Encode</c>.
/// </summary>
public static class ParityChecker
{
    private const int SnippetMaxLength = 600;
    private const int KeptMismatches = 400;

    /// <summary>Runs the comparison.</summary>
    /// <param name="family">The family id (names the loader call in generated tests).</param>
    /// <param name="tokenizer">The tokenizer under test. Its <see cref="Tokenizer.Truncation"/> and <see cref="Tokenizer.Padding"/> are reset to <see langword="null"/> and restored.</param>
    /// <param name="goldenPath">The <c>.golden.jsonl.gz</c> file.</param>
    /// <param name="corpusDir">The directory holding <c>records.jsonl</c> and <c>pairs.jsonl</c> that the golden was made from.</param>
    /// <param name="options">Limits; null for the defaults.</param>
    public static FamilyResult Run(string family, Tokenizer tokenizer, string goldenPath, string corpusDir, ParityOptions? options = null)
    {
        options ??= new ParityOptions();
        var result = new FamilyResult { Family = family };
        var started = System.Diagnostics.Stopwatch.StartNew();
        TruncationOptions? savedT = tokenizer.Truncation;
        PaddingOptions? savedP = tokenizer.Padding;
        try
        {
            tokenizer.Truncation = null;
            tokenizer.Padding = null;
            using FileStream fs = File.OpenRead(goldenPath);
            using var gz = new GZipStream(fs, CompressionMode.Decompress);
            using var golden = new StreamReader(gz, Encoding.UTF8);
            string headerLine = golden.ReadLine() ?? throw new InvalidDataException("Empty golden file.");
            using JsonDocument headerDoc = JsonDocument.Parse(headerLine);
            JsonElement header = headerDoc.RootElement.GetProperty("header");
            result.Oracle = header.GetProperty("oracle").GetString() + " " + header.GetProperty("oracle_version").GetString();
            var run = new Session(family, tokenizer, header, result, options);

            using var corpus = new StreamReader(Path.Combine(corpusDir, "records.jsonl"), Encoding.UTF8);
            string? line;
            while ((line = golden.ReadLine()) is not null)
            {
                using JsonDocument gd = JsonDocument.Parse(line);
                JsonElement g = gd.RootElement;
                if (g.TryGetProperty("pair", out _))
                {
                    run.CheckPair(g, ReadPairs(corpusDir));
                    continue;
                }

                string corpusLine = corpus.ReadLine() ?? throw new InvalidDataException("The corpus has fewer records than the golden file.");
                using JsonDocument cd = JsonDocument.Parse(corpusLine);
                int id = g.GetProperty("id").GetInt32();
                if (cd.RootElement.GetProperty("id").GetInt32() != id)
                {
                    throw new InvalidDataException($"Golden record {id} does not line up with the corpus; was the golden made from this corpus?");
                }

                run.CheckRecord(g, cd.RootElement);
                result.Records++;
                if (options.MaxRecords > 0 && result.Records >= options.MaxRecords)
                {
                    break;
                }
            }

            run.Finish();
        }
        finally
        {
            tokenizer.Truncation = savedT;
            tokenizer.Padding = savedP;
            started.Stop();
            result.Seconds = started.Elapsed.TotalSeconds;
        }

        if (result.MismatchCount > 0)
        {
            result.Status = "mismatches";
        }

        return result;
    }

    private static Dictionary<int, (string A, string B)>? s_pairs;
    private static string? s_pairsDir;

    private static Dictionary<int, (string A, string B)> ReadPairs(string corpusDir)
    {
        if (s_pairs is not null && s_pairsDir == corpusDir)
        {
            return s_pairs;
        }

        var map = new Dictionary<int, (string, string)>();
        string path = Path.Combine(corpusDir, "pairs.jsonl");
        if (File.Exists(path))
        {
            foreach (string l in File.ReadLines(path, Encoding.UTF8))
            {
                using JsonDocument d = JsonDocument.Parse(l);
                map[d.RootElement.GetProperty("id").GetInt32()] = (d.RootElement.GetProperty("a").GetString()!, d.RootElement.GetProperty("b").GetString()!);
            }
        }

        s_pairsDir = corpusDir;
        return s_pairs = map;
    }

    private sealed class Session
    {
        private readonly string _family;
        private readonly Tokenizer _tok;
        private readonly FamilyResult _result;
        private readonly ParityOptions _options;
        private readonly bool _hasTokens;
        private readonly bool _hasWords;
        private readonly TruncationOptions? _trunc;
        private readonly PaddingOptions? _pad;
        private readonly List<Mismatch> _kept = [];

        public Session(string family, Tokenizer tok, JsonElement header, FamilyResult result, ParityOptions options)
        {
            _family = family;
            _tok = tok;
            _result = result;
            _options = options;
            _hasTokens = header.GetProperty("has_tokens").GetBoolean();
            _hasWords = header.GetProperty("has_word_ids").GetBoolean();
            if (header.TryGetProperty("trunc_pad", out JsonElement tp) && tp.ValueKind == JsonValueKind.Object)
            {
                var side = tp.GetProperty("truncate_side").GetString() == "left" ? TokenizerSide.Left : TokenizerSide.Right;
                var padSide = tp.GetProperty("pad_side").GetString() == "left" ? TokenizerSide.Left : TokenizerSide.Right;
                _trunc = new TruncationOptions(tp.GetProperty("max_length").GetInt32(), side);
                _pad = new PaddingOptions(tp.GetProperty("pad_id").GetInt32(), tp.GetProperty("pad_token").GetString()!, tp.GetProperty("pad_length").GetInt32(), null, padSide, tp.GetProperty("pad_type_id").GetInt32());
            }
        }

        // ---- one corpus record

        public void CheckRecord(JsonElement g, JsonElement c)
        {
            int id = g.GetProperty("id").GetInt32();
            string cls = g.GetProperty("cls").GetString()!;
            string? chars = null;
            byte[]? utf8 = null;
            if (c.TryGetProperty("text", out JsonElement t))
            {
                chars = t.GetString()!;
                utf8 = Encoding.UTF8.GetBytes(chars);
            }
            else if (c.TryGetProperty("u16", out JsonElement u))
            {
                var sb = new StringBuilder(u.GetArrayLength());
                foreach (JsonElement x in u.EnumerateArray())
                {
                    sb.Append((char)x.GetInt32());
                }

                chars = sb.ToString();
            }
            else if (c.TryGetProperty("utf8_hex", out JsonElement h))
            {
                utf8 = Convert.FromHexString(h.GetString()!);
            }

            int length = chars?.Length ?? utf8!.Length;
            foreach (bool special in new[] { true, false })
            {
                string variant = special ? "special" : "plain";
                JsonElement gv = g.GetProperty(variant);
                if (chars is not null)
                {
                    var ctx = new Ctx($"encode.{variant}", id, cls, length, $"tokenizer.Encode({Lit(chars)}, {(special ? "true" : "false")})", null, false);
                    Guard(ctx, () => CompareEncoding(ctx, _tok.Encode(chars, special), gv, false, false));
                }

                if (utf8 is not null)
                {
                    var ctx = new Ctx($"encode.utf8.{variant}", id, cls, length, $"tokenizer.Encode((ReadOnlySpan<byte>)new byte[] {{ {string.Join(", ", utf8)} }}, {(special ? "true" : "false")})", null, true);
                    Guard(ctx, () => CompareEncoding(ctx, _tok.Encode(utf8, special), gv, true, false));
                }

                CheckIds(g, id, cls, length, chars, utf8, special, gv);
            }

            if (_trunc is not null && g.TryGetProperty("trunc", out JsonElement gt))
            {
                _tok.Truncation = _trunc;
                _tok.Padding = _pad;
                try
                {
                    string setup = $"tokenizer.Truncation = new TruncationOptions({_trunc.MaxLength}, TokenizerSide.{_trunc.Side});\n" +
                        $"tokenizer.Padding = new PaddingOptions({_pad!.PadId}, {Lit(_pad.PadToken)}, {_pad.Length}, null, TokenizerSide.{_pad.Side}, {_pad.PadTypeId});";
                    if (chars is not null)
                    {
                        var ctx = new Ctx("encode.trunc", id, cls, length, $"tokenizer.Encode({Lit(chars)}, true)", setup, false);
                        Guard(ctx, () => CompareEncoding(ctx, _tok.Encode(chars, true), gt, false, true));
                    }

                    if (utf8 is not null)
                    {
                        var ctx = new Ctx("encode.utf8.trunc", id, cls, length, $"tokenizer.Encode((ReadOnlySpan<byte>)new byte[] {{ {string.Join(", ", utf8)} }}, true)", setup, true);
                        Guard(ctx, () => CompareEncoding(ctx, _tok.Encode(utf8, true), gt, true, true));
                    }
                }
                finally
                {
                    _tok.Truncation = null;
                    _tok.Padding = null;
                }
            }

            // Decode the oracle's own ids (or ours when the golden only has a digest and ours match it).
            JsonElement ids = g.GetProperty("special").GetProperty("ids");
            int[]? decodeIds = null;
            if (!Digests.IsDigest(ids))
            {
                decodeIds = ReadInts(ids);
            }
            else if (chars is not null || utf8 is not null)
            {
                try
                {
                    int[] ours = chars is not null ? _tok.Encode(chars, true).Ids : _tok.Encode(utf8!, true).Ids;
                    if (Digests.OfInts(ours) == Digests.Read(ids))
                    {
                        decodeIds = ours;
                    }
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    // already reported by the encode checks
                    _ = ex;
                }
            }

            if (decodeIds is not null && g.TryGetProperty("dec", out JsonElement dec))
            {
                foreach (bool skip in new[] { true, false })
                {
                    string key = skip ? "skip" : "keep";
                    var ctx = new Ctx($"decode.{key}", id, cls, length, $"tokenizer.Decode(new int[] {{ {string.Join(", ", decodeIds.Length <= 400 ? decodeIds : [])} }}, {(skip ? "true" : "false")})", null, false);
                    Guard(ctx, () => CompareText(ctx, "text", _tok.Decode(decodeIds, skip), dec.GetProperty(key)));
                }
            }
        }

        private void CheckIds(JsonElement g, int id, string cls, int length, string? chars, byte[]? utf8, bool special, JsonElement gv)
        {
            JsonElement expIds = gv.GetProperty("ids");
            bool digest = Digests.IsDigest(expIds);
            int n = digest ? Digests.Read(expIds).N : expIds.GetArrayLength();
            for (int form = 0; form < 2; form++)
            {
                bool isUtf8 = form == 1;
                if (isUtf8 ? utf8 is null : chars is null)
                {
                    continue;
                }

                string api = isUtf8 ? "utf8" : "char";
                var full = new Ctx($"ids.{api}.{(special ? "special" : "plain")}", id, cls, length, null, null, isUtf8);
                Guard(full, () =>
                {
                    int[] buf = new int[n];
                    OperationStatus st = isUtf8 ? _tok.EncodeToIds(utf8!, buf, out int w, out _, special) : _tok.EncodeToIds(chars!, buf, out w, out _, special);
                    CompareInts(full, "status", [(int)st], StatusDone);
                    CompareInts(full, "written", [w], Single(n));
                    CompareInts(full, "ids", buf.AsSpan(0, w).ToArray(), expIds);
                    int count = isUtf8 ? _tok.CountTokens(utf8!, special) : _tok.CountTokens(chars!, special);
                    CompareInts(full, "count", [count], Single(n));
                });

                if (!digest && n >= 1)
                {
                    // A short buffer: the written IDs must be an exact prefix and `consumed` the end of the last written token.
                    var small = new Ctx($"ids.{api}.{(special ? "special" : "plain")}.small", id, cls, length, null, null, isUtf8);
                    Guard(small, () =>
                    {
                        int[] expected = ReadInts(expIds);
                        int[] offsets = ReadInts(gv.GetProperty(isUtf8 ? "o8" : "o16"));
                        int cap = n / 2;
                        int[] buf = new int[cap];
                        OperationStatus st = isUtf8 ? _tok.EncodeToIds(utf8!, buf, out int w, out int consumed, special) : _tok.EncodeToIds(chars!, buf, out w, out consumed, special);
                        CompareInts(small, "status", [(int)st], Single((int)OperationStatus.DestinationTooSmall));
                        CompareInts(small, "written", [w], Single(cap));
                        CompareInts(small, "ids", buf.AsSpan(0, w).ToArray(), JsonSerializer.SerializeToElement(expected.AsSpan(0, Math.Min(w, expected.Length)).ToArray()));
                        int end = 0;
                        for (int i = 0; i < Math.Min(w, expected.Length); i++)
                        {
                            end = Math.Max(end, offsets[(2 * i) + 1]);
                        }

                        CompareInts(small, "consumed", [consumed], Single(end));
                    });
                }
            }

        }

        // ---- pairs

        public void CheckPair(JsonElement g, Dictionary<int, (string A, string B)> pairs)
        {
            int id = g.GetProperty("pair").GetInt32();
            (string a, string b) = pairs[id];
            _result.Pairs++;
            string call = $"tokenizer.Encode({Lit(a)}, {Lit(b)}, true)";
            int length = a.Length + b.Length;
            var ctx = new Ctx("pair.special", id, "pair", length, call, null, false);
            Guard(ctx, () => CompareEncoding(ctx, _tok.Encode(a, b, true), g.GetProperty("special"), false, false));
            if (_trunc is not null && g.TryGetProperty("trunc", out JsonElement gt))
            {
                _tok.Truncation = _trunc;
                _tok.Padding = _pad;
                try
                {
                    string setup = $"tokenizer.Truncation = new TruncationOptions({_trunc.MaxLength}, TokenizerSide.{_trunc.Side});\n" +
                        $"tokenizer.Padding = new PaddingOptions({_pad!.PadId}, {Lit(_pad.PadToken)}, {_pad.Length}, null, TokenizerSide.{_pad.Side}, {_pad.PadTypeId});";
                    var tc = new Ctx("pair.trunc", id, "pair", length, call, setup, false);
                    Guard(tc, () => CompareEncoding(tc, _tok.Encode(a, b, true), gt, false, true));
                }
                finally
                {
                    _tok.Truncation = null;
                    _tok.Padding = null;
                }
            }
        }

        public void Finish()
        {
            // The shortest failing inputs become test cases, spreading over distinct check+field pairs before repeating one.
            var byKey = _kept.Where(m => m.TestBody is not null).GroupBy(m => m.Check + "." + m.Field).Select(g => g.OrderBy(m => m.InputLength).ToList()).ToList();
            for (int round = 0; _result.TestCases.Count < _options.MaxTestCases && byKey.Any(l => l.Count > round); round++)
            {
                foreach (List<Mismatch> l in byKey)
                {
                    if (round < l.Count && _result.TestCases.Count < _options.MaxTestCases)
                    {
                        _result.TestCases.Add(l[round]);
                    }
                }
            }

            _result.Examples.AddRange(_kept.OrderBy(m => m.InputLength).Take(_options.MaxExamples));
        }

        // ---- comparing

        private static JsonElement Single(int v) => JsonSerializer.SerializeToElement(new[] { v });

        private static readonly JsonElement StatusDone = Single((int)OperationStatus.Done);

        private void CompareEncoding(Ctx ctx, TokenEncoding e, JsonElement g, bool utf8, bool padded)
        {
            CompareInts(ctx, "ids", e.Ids, g.GetProperty("ids"));
            if (_hasTokens && g.TryGetProperty("tok", out JsonElement tok))
            {
                CompareStrings(ctx, "tokens", e.Tokens, tok);
            }

            var flat = new int[e.Length * 2];
            for (int i = 0; i < e.Length; i++)
            {
                flat[2 * i] = e.Offsets[i].Start;
                flat[(2 * i) + 1] = e.Offsets[i].End;
            }

            CompareInts(ctx, "offsets", flat, g.GetProperty(utf8 && g.TryGetProperty("o8", out _) ? "o8" : "o16"));
            CompareInts(ctx, "special_mask", e.SpecialTokensMask.Select(b => b ? 1 : 0).ToArray(), g.GetProperty("sm"));
            CompareInts(ctx, "type_ids", e.TypeIds, g.GetProperty("ty"));
            if (_hasWords && g.TryGetProperty("wi", out JsonElement wi))
            {
                CompareInts(ctx, "word_ids", e.WordIds, wi);
            }

            if (padded && g.TryGetProperty("am", out JsonElement am))
            {
                CompareInts(ctx, "attention_mask", e.AttentionMask, am);
            }
        }

        private static int[] ReadInts(JsonElement e)
        {
            var a = new int[e.GetArrayLength()];
            int i = 0;
            foreach (JsonElement x in e.EnumerateArray())
            {
                a[i++] = x.GetInt32();
            }

            return a;
        }

        private void CompareInts(Ctx ctx, string field, int[] actual, JsonElement expected)
        {
            _result.Comparisons++;
            if (Digests.IsDigest(expected))
            {
                var want = Digests.Read(expected);
                var got = Digests.OfInts(actual);
                if (got != want)
                {
                    Fail(ctx, field, $"digest of {got.N} values differs from the oracle's {want.N} values", null);
                }

                return;
            }

            int[] exp = ReadInts(expected);
            if (actual.AsSpan().SequenceEqual(exp))
            {
                return;
            }

            string detail = actual.Length != exp.Length ? $"length {actual.Length}, oracle {exp.Length}" : "";
            int k = 0;
            while (k < actual.Length && k < exp.Length && actual[k] == exp[k])
            {
                k++;
            }

            detail += (detail.Length > 0 ? "; " : "") + $"first difference at index {k}: {(k < actual.Length ? actual[k].ToString() : "-")}, oracle {(k < exp.Length ? exp[k].ToString() : "-")}";
            string? body = null;
            if (ctx.Call is not null && ctx.InputLength <= SnippetMaxLength && exp.Length <= 400)
            {
                body = Body(ctx, Accessor(field, ints: true), $"new int[] {{ {string.Join(", ", exp)} }}");
            }

            Fail(ctx, field, detail, body);
        }

        private void CompareStrings(Ctx ctx, string field, string[] actual, JsonElement expected)
        {
            _result.Comparisons++;
            if (Digests.IsDigest(expected))
            {
                var want = Digests.Read(expected);
                var got = Digests.OfStrings(actual);
                if (got != want)
                {
                    Fail(ctx, field, $"digest of {got.N} strings differs from the oracle's {want.N}", null);
                }

                return;
            }

            string[] exp = [.. expected.EnumerateArray().Select(x => x.GetString()!)];
            if (actual.AsSpan().SequenceEqual(exp))
            {
                return;
            }

            int k = 0;
            while (k < actual.Length && k < exp.Length && actual[k] == exp[k])
            {
                k++;
            }

            string detail = (actual.Length != exp.Length ? $"length {actual.Length}, oracle {exp.Length}; " : "") +
                $"first difference at index {k}: {(k < actual.Length ? Lit(actual[k]) : "-")}, oracle {(k < exp.Length ? Lit(exp[k]) : "-")}";
            string? body = ctx.Call is not null && ctx.InputLength <= SnippetMaxLength && exp.Length <= 400
                ? Body(ctx, Accessor(field, ints: false), $"new string[] {{ {string.Join(", ", exp.Select(Lit))} }}")
                : null;
            Fail(ctx, field, detail, body);
        }

        private void CompareText(Ctx ctx, string field, string actual, JsonElement expected)
        {
            _result.Comparisons++;
            if (Digests.IsDigest(expected))
            {
                var want = Digests.Read(expected);
                var got = Digests.OfText(actual);
                if (got != want)
                {
                    Fail(ctx, field, "digest of the decoded text differs from the oracle's", null);
                }

                return;
            }

            string exp = expected.GetString()!;
            if (string.Equals(actual, exp, StringComparison.Ordinal))
            {
                return;
            }

            int k = 0;
            while (k < actual.Length && k < exp.Length && actual[k] == exp[k])
            {
                k++;
            }

            string detail = $"first difference at index {k}: actual {Lit(actual[Math.Max(0, k - 5)..Math.Min(actual.Length, k + 10)])}, oracle {Lit(exp[Math.Max(0, k - 5)..Math.Min(exp.Length, k + 10)])}";
            string? body = ctx.Call is not null && ctx.InputLength <= SnippetMaxLength ? Body(ctx, "", Lit(exp)) : null;
            Fail(ctx, field, detail, body);
        }

        private static string Accessor(string field, bool ints) => field switch
        {
            "ids" => ".Ids",
            "tokens" => ".Tokens",
            "offsets" => ".Offsets.SelectMany(o => new[] { o.Start, o.End }).ToArray()",
            "special_mask" => ".SpecialTokensMask.Select(b => b ? 1 : 0).ToArray()",
            "type_ids" => ".TypeIds",
            "word_ids" => ".WordIds",
            "attention_mask" => ".AttentionMask",
            _ => ints ? ".Ids" : "",
        };

        private string Body(Ctx ctx, string accessor, string expected) =>
            $"var tokenizer = ParityTokenizers.Load({Lit(_family)});\n{(ctx.Setup is null ? "" : ctx.Setup + "\n")}var actual = {ctx.Call}{accessor};\nAssert.Equal({expected}, actual);";

        private void Fail(Ctx ctx, string field, string detail, string? body)
        {
            string key = ctx.Check + "." + field;
            _result.MismatchCount++;
            _result.ByField[key] = _result.ByField.GetValueOrDefault(key) + 1;
            _result.ByClass[ctx.Class] = _result.ByClass.GetValueOrDefault(ctx.Class) + 1;
            if (!_result.ByFieldAndClass.TryGetValue(key, out SortedDictionary<string, long>? per))
            {
                _result.ByFieldAndClass[key] = per = [];
            }

            per[ctx.Class] = per.GetValueOrDefault(ctx.Class) + 1;
            _kept.Add(new Mismatch(ctx.Check, field, ctx.Id, ctx.Class, detail, ctx.InputLength, body));
            if (_kept.Count > KeptMismatches)
            {
                // Keep the shortest inputs: they are the ones worth reading and turning into tests.
                _kept.Sort((x, y) => x.InputLength.CompareTo(y.InputLength));
                _kept.RemoveRange(KeptMismatches / 2, _kept.Count - (KeptMismatches / 2));
            }
        }

        private void Guard(Ctx ctx, Action compare)
        {
            try
            {
                compare();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _result.Comparisons++;
                Fail(ctx, "exception", $"{ex.GetType().Name}: {ex.Message}", null);
            }
        }
    }

    private sealed record Ctx(string Check, int Id, string Class, int InputLength, string? Call, string? Setup, bool Utf8);

    /// <summary>A C# string literal using only ASCII, with \u escapes for everything else, so the text survives any editor.</summary>
    internal static string Lit(string s)
    {
        var sb = new StringBuilder(s.Length + 2).Append('"');
        foreach (char c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case >= ' ' and <= '~': sb.Append(c); break;
                default: sb.Append("\\u").Append(((int)c).ToString("x4", System.Globalization.CultureInfo.InvariantCulture)); break;
            }
        }

        return sb.Append('"').ToString();
    }
}
