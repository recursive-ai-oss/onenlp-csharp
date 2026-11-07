using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace RecursiveAI.OneNlp.Serialization;

/// <summary>
/// The JSON form of a document: the shape spaCy documents for <c>Doc.to_json</c> (<c>text</c>, <c>ents</c>, <c>sents</c>, <c>tokens</c>)
/// with a few extra fields. See docs/json-format.md.
/// </summary>
internal static class DocJson
{
    /// <summary>The most input bytes a stream reader will take in: 512 MiB.</summary>
    internal const long MaxInputBytes = 512L << 20;

    private static readonly JsonWriterOptions WriterOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, Indented = false };

    // ---- code point offsets ----
    // spaCy's offsets count Unicode code points (Python string indexes); ours are UTF-16 code units. They differ only after a
    // character outside the BMP, so the mapping is built only when the text has a surrogate pair.

    // ArgumentException appends " (Parameter 'name')" to its message; that names our internal argument, which means nothing to a caller reading JSON.
    private static string PlainMessage(Exception e)
    {
        string message = e.Message;
        if (e is ArgumentException { ParamName: { } name })
        {
            int at = message.LastIndexOf(" (Parameter '" + name + "')", StringComparison.Ordinal);
            if (at >= 0)
            {
                message = message[..at];
            }
        }

        return message;
    }

    internal static int[]? BuildCodePointIndex(string text) => CodePointIndex(text);

    private static int[]? CodePointIndex(string text)
    {
        int[]? map = null;
        int cp = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (map is null && char.IsSurrogate(text[i]))
            {
                map = new int[text.Length + 1];
                for (int k = 0; k < i; k++)
                {
                    map[k] = k;
                }

                cp = i;
            }

            if (map is not null)
            {
                map[i] = cp;
            }

            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                if (map is not null)
                {
                    map[i + 1] = -1;
                }

                i++;
            }

            cp++;
        }

        if (map is not null)
        {
            map[text.Length] = cp;
        }

        return map;
    }

    private static int[] UnitIndex(string text, int codePoints)
    {
        var map = new int[codePoints + 1];
        int cp = 0;
        for (int i = 0; i < text.Length; i++)
        {
            map[cp++] = i;
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                i++;
            }
        }

        map[cp] = text.Length;
        return map;
    }

    // ---- writing ----

    private static void CheckString(string s)
    {
        if (!TextUtil.IsWellFormed(s))
        {
            throw new InvalidOperationException("The document holds a string with an unpaired surrogate, which JSON text cannot carry. Use the binary form.");
        }
    }

    private static void PropertyString(Utf8JsonWriter w, ReadOnlySpan<byte> name, string value)
    {
        CheckString(value);
        w.WriteString(name, value);
    }

    internal static void Write(Doc doc, Utf8JsonWriter w)
    {
        CheckString(doc.Text);
        int[]? cp = CodePointIndex(doc.Text);
        int Offset(int unit)
        {
            if (cp is null)
            {
                return unit;
            }

            int v = cp[unit];
            if (v < 0)
            {
                throw new InvalidOperationException("A token boundary splits a surrogate pair, so it has no code point offset.");
            }

            return v;
        }

        int n = doc.Length;
        w.WriteStartObject();
        PropertyString(w, "text"u8, doc.Text);

        if (n > 0 && doc[0].EntIob != EntIob.Unset)
        {
            w.WriteStartArray("ents"u8);
            foreach (Span e in doc.Ents)
            {
                w.WriteStartObject();
                w.WriteNumber("start"u8, Offset(e.StartChar));
                w.WriteNumber("end"u8, Offset(e.EndChar));
                PropertyString(w, "label"u8, e.Label);
                if (e.Confidence != 1f)
                {
                    w.WriteNumber("conf"u8, e.Confidence);
                }

                w.WriteEndObject();
            }

            w.WriteEndArray();
        }

        if (doc.CatLabels is { } catLabels)
        {
            w.WriteStartObject("cats"u8);
            ReadOnlySpan<float> scores = doc.CatScores;
            for (int i = 0; i < catLabels.Length; i++)
            {
                CheckString(catLabels[i]);
                w.WriteNumber(catLabels[i], scores[i]);
            }

            w.WriteEndObject();
        }

        IReadOnlyList<KeyValuePair<string, SpanSpec[]>> groups = doc.GroupSpecs();
        if (groups.Count > 0)
        {
            w.WriteStartObject("spans"u8);
            foreach (KeyValuePair<string, SpanSpec[]> g in groups)
            {
                CheckString(g.Key);
                w.WriteStartArray(g.Key);
                foreach (SpanSpec s in g.Value)
                {
                    Span span = new(doc, s.Start, s.End);
                    w.WriteStartObject();
                    w.WriteNumber("start"u8, Offset(span.StartChar));
                    w.WriteNumber("end"u8, Offset(span.EndChar));
                    PropertyString(w, "label"u8, s.Label);
                    if (s.Confidence != 1f)
                    {
                        w.WriteNumber("conf"u8, s.Confidence);
                    }

                    w.WriteEndObject();
                }

                w.WriteEndArray();
            }

            w.WriteEndObject();
        }

        ReadOnlySpan<SentenceBoundary> boundaries = doc.GetSentenceBoundaries();
        bool anyBoundary = false;
        foreach (SentenceBoundary b in boundaries)
        {
            anyBoundary |= b != SentenceBoundary.Unset;
        }

        if (anyBoundary)
        {
            w.WriteStartArray("sents"u8);
            foreach (Span s in doc.Sentences)
            {
                w.WriteStartObject();
                w.WriteNumber("start"u8, Offset(s.StartChar));
                w.WriteNumber("end"u8, Offset(s.EndChar));
                w.WriteEndObject();
            }

            w.WriteEndArray();
        }

        bool[] has = new bool[6];
        for (int a = 0; a < 6; a++)
        {
            foreach (int id in doc.GetColumn((TokenColumn)a))
            {
                if (id != 0)
                {
                    has[a] = true;
                    break;
                }
            }
        }

        ReadOnlySpan<int> heads = doc.GetHeads();
        bool writeHead = has[(int)TokenColumn.Dep];
        for (int i = 0; i < n && !writeHead; i++)
        {
            writeHead = heads[i] != i;
        }

        w.WriteStartArray("tokens"u8);
        for (int i = 0; i < n; i++)
        {
            Token t = doc[i];
            w.WriteStartObject();
            w.WriteNumber("id"u8, i);
            w.WriteNumber("start"u8, Offset(t.Idx));
            w.WriteNumber("end"u8, Offset(t.EndIdx));
            if (has[(int)TokenColumn.Tag])
            {
                PropertyString(w, "tag"u8, t.Tag);
            }

            if (has[(int)TokenColumn.Pos])
            {
                PropertyString(w, "pos"u8, t.Pos);
            }

            if (has[(int)TokenColumn.Morph])
            {
                PropertyString(w, "morph"u8, t.Morph);
            }

            if (has[(int)TokenColumn.Lemma])
            {
                PropertyString(w, "lemma"u8, t.Lemma);
            }

            if (has[(int)TokenColumn.Dep])
            {
                PropertyString(w, "dep"u8, t.Dep);
            }

            if (writeHead)
            {
                w.WriteNumber("head"u8, heads[i]);
            }

            // Fields below are ours; spaCy's reader ignores them.
            if (has[(int)TokenColumn.Norm])
            {
                PropertyString(w, "norm"u8, t.Norm);
            }

            if (t.Confidence != 1f)
            {
                w.WriteNumber("conf"u8, t.Confidence);
            }

            if (anyBoundary && boundaries[i] != SentenceBoundary.Unset)
            {
                w.WriteNumber("sb"u8, (int)boundaries[i]);
            }

            w.WriteEndObject();
        }

        w.WriteEndArray();
        w.WriteEndObject();
    }

    internal static byte[] ToUtf8(Doc doc)
    {
        var buffer = new ArrayBufferWriter<byte>(256 + (doc.Length * 64));
        using (var w = new Utf8JsonWriter(buffer, WriterOptions))
        {
            Write(doc, w);
        }

        return buffer.WrittenSpan.ToArray();
    }

    internal static void WriteTo(Doc doc, Stream stream)
    {
        using var w = new Utf8JsonWriter(stream, WriterOptions);
        Write(doc, w);
    }

    // ---- reading ----

    private struct TokenRecord
    {
        public int Id;
        public int Start;
        public int End;
        public string? Tag;
        public string? Pos;
        public string? Morph;
        public string? Lemma;
        public string? Dep;
        public string? Norm;
        public int Head;
        public float Conf;
        public int Sb;
    }

    private struct SpanRecord
    {
        public int Start;
        public int End;
        public string? Label;
        public float Conf;
    }

    internal static Doc Read(ReadOnlySpan<byte> utf8, StringStore store)
    {
        if (utf8.StartsWith("\uFEFF"u8))
        {
            utf8 = utf8[3..];
        }

        try
        {
            return ReadCore(utf8, store);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException or ArgumentException or OverflowException)
        {
            if (e is InvalidDataException)
            {
                throw;
            }

            throw new InvalidDataException("The JSON is not a valid document: " + PlainMessage(e), e);
        }
    }

    private static InvalidDataException Bad(string what) => new(what);

    private static string ReadString(ref Utf8JsonReader r) =>
        r.TokenType == JsonTokenType.String ? r.GetString()! : throw Bad("A string was expected.");

    private static int ReadInt(ref Utf8JsonReader r)
    {
        if (r.TokenType != JsonTokenType.Number || !r.TryGetInt32(out int v))
        {
            throw Bad("A 32-bit integer was expected.");
        }

        return v;
    }

    private static float ReadConfidence(ref Utf8JsonReader r)
    {
        if (r.TokenType != JsonTokenType.Number || !r.TryGetSingle(out float v) || !(v >= 0f && v <= 1f))
        {
            throw Bad("A confidence from 0 to 1 was expected.");
        }

        return v;
    }

    private static void ExpectStart(ref Utf8JsonReader r, JsonTokenType type)
    {
        if (r.TokenType != type)
        {
            throw Bad("The JSON has the wrong structure.");
        }
    }

    private static Doc ReadCore(ReadOnlySpan<byte> utf8, StringStore store)
    {
        var r = new Utf8JsonReader(utf8, new JsonReaderOptions { MaxDepth = 32 });
        if (!r.Read())
        {
            throw Bad("The JSON is empty.");
        }

        ExpectStart(ref r, JsonTokenType.StartObject);
        string? text = null;
        List<SpanRecord>? ents = null;
        List<SpanRecord>? sents = null;
        List<TokenRecord>? tokens = null;
        List<KeyValuePair<string, float>>? cats = null;
        List<KeyValuePair<string, List<SpanRecord>>>? groups = null;
        while (true)
        {
            if (!r.Read())
            {
                throw Bad("The JSON ends inside the document object.");
            }

            if (r.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (r.ValueTextEquals("text"u8))
            {
                r.Read();
                text = ReadString(ref r);
            }
            else if (r.ValueTextEquals("ents"u8))
            {
                r.Read();
                ents = ReadSpans(ref r);
            }
            else if (r.ValueTextEquals("sents"u8))
            {
                r.Read();
                sents = ReadSpans(ref r);
            }
            else if (r.ValueTextEquals("tokens"u8))
            {
                r.Read();
                tokens = ReadTokens(ref r);
            }
            else if (r.ValueTextEquals("cats"u8))
            {
                r.Read();
                cats = ReadCats(ref r);
            }
            else if (r.ValueTextEquals("spans"u8))
            {
                r.Read();
                groups = ReadGroups(ref r);
            }
            else
            {
                // user_data and anything else: not stored by a Doc, so not read.
                r.Read();
                r.Skip();
            }
        }

        if (r.Read())
        {
            throw Bad("There is data after the document object.");
        }

        if (text is null || tokens is null)
        {
            throw Bad("A document needs \"text\" and \"tokens\".");
        }

        Doc doc = Build(text, tokens, ents, sents, store);
        try
        {
            if (cats is not null)
            {
                doc.SetCats(cats.ToArray());
            }

            if (groups is not null)
            {
                int codePoints = text.Length;
                int[]? units = null;
                if (CodePointIndex(text) is { } cpMap)
                {
                    codePoints = cpMap[text.Length];
                    units = UnitIndex(text, codePoints);
                }

                foreach (KeyValuePair<string, List<SpanRecord>> g in groups)
                {
                    var specs = new SpanSpec[g.Value.Count];
                    for (int i = 0; i < specs.Length; i++)
                    {
                        SpanRecord e = g.Value[i];
                        if (!doc.TryGetCharSpan(ToUnitOffset(units, codePoints, e.Start), ToUnitOffset(units, codePoints, e.End), out Span span))
                        {
                            throw Bad($"Span {i} of group '{g.Key}' does not start and end on token boundaries.");
                        }

                        specs[i] = new SpanSpec(span.Start, span.End, e.Label ?? string.Empty, e.Conf);
                    }

                    doc.SetSpans(g.Key, specs);
                }
            }
        }
        catch (ArgumentException e)
        {
            throw Bad("The document's cats or spans are inconsistent: " + PlainMessage(e));
        }

        return doc;
    }

    private static int ToUnitOffset(int[]? units, int codePoints, int offset)
    {
        if (offset > codePoints)
        {
            throw Bad("An offset lies beyond the end of the text.");
        }

        return units is null ? offset : units[offset];
    }

    private static List<KeyValuePair<string, float>> ReadCats(ref Utf8JsonReader r)
    {
        ExpectStart(ref r, JsonTokenType.StartObject);
        var list = new List<KeyValuePair<string, float>>();
        while (true)
        {
            if (!r.Read())
            {
                throw Bad("The JSON ends inside the cats object.");
            }

            if (r.TokenType == JsonTokenType.EndObject)
            {
                return list;
            }

            string label = r.GetString() ?? string.Empty;
            r.Read();
            list.Add(new(label, ReadConfidence(ref r)));
        }
    }

    private static List<KeyValuePair<string, List<SpanRecord>>> ReadGroups(ref Utf8JsonReader r)
    {
        ExpectStart(ref r, JsonTokenType.StartObject);
        var list = new List<KeyValuePair<string, List<SpanRecord>>>();
        while (true)
        {
            if (!r.Read())
            {
                throw Bad("The JSON ends inside the spans object.");
            }

            if (r.TokenType == JsonTokenType.EndObject)
            {
                return list;
            }

            string key = r.GetString() ?? string.Empty;
            r.Read();
            list.Add(new(key, ReadSpans(ref r)));
        }
    }

    private static List<SpanRecord> ReadSpans(ref Utf8JsonReader r)
    {
        ExpectStart(ref r, JsonTokenType.StartArray);
        var list = new List<SpanRecord>();
        while (true)
        {
            if (!r.Read())
            {
                throw Bad("The JSON ends inside an array.");
            }

            if (r.TokenType == JsonTokenType.EndArray)
            {
                return list;
            }

            ExpectStart(ref r, JsonTokenType.StartObject);
            var s = new SpanRecord { Start = -1, End = -1, Conf = 1f };
            while (true)
            {
                r.Read();
                if (r.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                if (r.ValueTextEquals("start"u8))
                {
                    r.Read();
                    s.Start = ReadInt(ref r);
                }
                else if (r.ValueTextEquals("end"u8))
                {
                    r.Read();
                    s.End = ReadInt(ref r);
                }
                else if (r.ValueTextEquals("label"u8))
                {
                    r.Read();
                    s.Label = ReadString(ref r);
                }
                else if (r.ValueTextEquals("conf"u8))
                {
                    r.Read();
                    s.Conf = ReadConfidence(ref r);
                }
                else
                {
                    r.Read();
                    r.Skip();
                }
            }

            if (s.Start < 0 || s.End <= s.Start)
            {
                throw Bad("A span needs \"start\" and a larger \"end\".");
            }

            list.Add(s);
        }
    }

    private static List<TokenRecord> ReadTokens(ref Utf8JsonReader r)
    {
        ExpectStart(ref r, JsonTokenType.StartArray);
        var list = new List<TokenRecord>();
        while (true)
        {
            if (!r.Read())
            {
                throw Bad("The JSON ends inside the token array.");
            }

            if (r.TokenType == JsonTokenType.EndArray)
            {
                return list;
            }

            ExpectStart(ref r, JsonTokenType.StartObject);
            var t = new TokenRecord { Id = -1, Start = -1, End = -1, Head = -1, Conf = 1f };
            while (true)
            {
                r.Read();
                if (r.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                if (r.ValueTextEquals("id"u8))
                {
                    r.Read();
                    t.Id = ReadInt(ref r);
                }
                else if (r.ValueTextEquals("start"u8))
                {
                    r.Read();
                    t.Start = ReadInt(ref r);
                }
                else if (r.ValueTextEquals("end"u8))
                {
                    r.Read();
                    t.End = ReadInt(ref r);
                }
                else if (r.ValueTextEquals("tag"u8))
                {
                    r.Read();
                    t.Tag = ReadString(ref r);
                }
                else if (r.ValueTextEquals("pos"u8))
                {
                    r.Read();
                    t.Pos = ReadString(ref r);
                }
                else if (r.ValueTextEquals("morph"u8))
                {
                    r.Read();
                    t.Morph = ReadString(ref r);
                }
                else if (r.ValueTextEquals("lemma"u8))
                {
                    r.Read();
                    t.Lemma = ReadString(ref r);
                }
                else if (r.ValueTextEquals("dep"u8))
                {
                    r.Read();
                    t.Dep = ReadString(ref r);
                }
                else if (r.ValueTextEquals("head"u8))
                {
                    r.Read();
                    t.Head = ReadInt(ref r);
                }
                else if (r.ValueTextEquals("norm"u8))
                {
                    r.Read();
                    t.Norm = ReadString(ref r);
                }
                else if (r.ValueTextEquals("conf"u8))
                {
                    r.Read();
                    t.Conf = ReadConfidence(ref r);
                }
                else if (r.ValueTextEquals("sb"u8))
                {
                    r.Read();
                    t.Sb = ReadInt(ref r);
                }
                else
                {
                    r.Read();
                    r.Skip();
                }
            }

            if (t.Start < 0 || t.End <= t.Start)
            {
                throw Bad("A token needs \"start\" and a larger \"end\".");
            }

            list.Add(t);
        }
    }

    private static Doc Build(string text, List<TokenRecord> tokens, List<SpanRecord>? ents, List<SpanRecord>? sents, StringStore store)
    {
        int codePoints = text.Length;
        int[]? units = null;
        if (CodePointIndex(text) is { } cpMap)
        {
            codePoints = cpMap[text.Length];
            units = UnitIndex(text, codePoints);
        }

        int ToUnit(int offset)
        {
            if (offset > codePoints)
            {
                throw Bad("An offset lies beyond the end of the text.");
            }

            return units is null ? offset : units[offset];
        }

        int n = tokens.Count;
        var bounds = new TokenBoundary[n];
        for (int i = 0; i < n; i++)
        {
            TokenRecord t = tokens[i];
            if (t.Id != -1 && t.Id != i)
            {
                throw Bad($"Token {i} has id {t.Id}; ids count from 0 in order.");
            }

            int start = ToUnit(t.Start);
            bounds[i] = new TokenBoundary(start, ToUnit(t.End) - start);
        }

        var doc = new Doc(store, text, bounds);
        var ids = new int[n];

        void Column(TokenColumn column, Func<TokenRecord, string?> pick)
        {
            bool any = false;
            for (int i = 0; i < n; i++)
            {
                string? s = pick(tokens[i]);
                ids[i] = string.IsNullOrEmpty(s) ? 0 : store.Add(s);
                any |= ids[i] != 0;
            }

            if (any)
            {
                doc.SetColumn(column, ids);
            }
        }

        Column(TokenColumn.Norm, static t => t.Norm);
        Column(TokenColumn.Lemma, static t => t.Lemma);
        Column(TokenColumn.Pos, static t => t.Pos);
        Column(TokenColumn.Tag, static t => t.Tag);
        Column(TokenColumn.Dep, static t => t.Dep);
        Column(TokenColumn.Morph, static t => t.Morph);

        bool anyHead = false;
        for (int i = 0; i < n; i++)
        {
            int head = tokens[i].Head;
            anyHead |= head != -1;
            ids[i] = head == -1 ? i : head;
        }

        if (anyHead)
        {
            doc.SetHeads(ids);
        }

        var confidences = new float[n];
        for (int i = 0; i < n; i++)
        {
            confidences[i] = tokens[i].Conf;
        }

        doc.SetConfidences(confidences);

        bool anySb = false;
        var boundaries = new SentenceBoundary[n];
        for (int i = 0; i < n; i++)
        {
            int sb = tokens[i].Sb;
            if (sb is < 0 or > 2)
            {
                throw Bad("A sentence boundary \"sb\" is 0, 1 or 2.");
            }

            boundaries[i] = (SentenceBoundary)sb;
            anySb |= sb != 0;
        }

        if (!anySb && sents is { Count: > 0 })
        {
            Array.Fill(boundaries, SentenceBoundary.Inside);
            foreach (SpanRecord s in sents)
            {
                int token = doc.FindToken(ToUnit(s.Start));
                if (token < 0 || doc[token].Idx != ToUnit(s.Start))
                {
                    throw Bad("A sentence does not start at a token boundary.");
                }

                boundaries[token] = SentenceBoundary.Start;
            }

            anySb = true;
        }

        if (anySb)
        {
            doc.SetSentenceBoundaries(boundaries);
        }

        if (ents is not null)
        {
            var specs = new SpanSpec[ents.Count];
            for (int i = 0; i < specs.Length; i++)
            {
                SpanRecord e = ents[i];
                if (!doc.TryGetCharSpan(ToUnit(e.Start), ToUnit(e.End), out Span span))
                {
                    throw Bad($"Entity {i} does not start and end on token boundaries.");
                }

                specs[i] = new SpanSpec(span.Start, span.End, e.Label ?? string.Empty, e.Conf);
            }

            doc.SetEntities(specs);
        }

        return doc;
    }

    internal static byte[] ReadAll(Stream stream)
    {
        using var buffer = new MemoryStream();
        byte[] chunk = new byte[81920];
        int read;
        while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (buffer.Length + read > MaxInputBytes)
            {
                throw new InvalidDataException("The JSON is larger than the 512 MiB limit.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    internal static async ValueTask<byte[]> ReadAllAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        byte[] chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > MaxInputBytes)
            {
                throw new InvalidDataException("The JSON is larger than the 512 MiB limit.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    internal static string Utf8ToString(byte[] bytes) => Encoding.UTF8.GetString(bytes);
}
