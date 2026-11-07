using System.Buffers.Binary;
using RecursiveAI.OneNlp;
using RecursiveAI.OneNlp.Serialization;

namespace RecursiveAI.OneNlp.Fuzz;

/// <summary>
/// Targets for the three readers of serialized documents. Only <see cref="InvalidDataException"/> may escape a reader. A document that
/// reads must write back and read again to the same bytes.
/// </summary>
internal static class SerializationTargets
{
    /// <summary>Documents the seed corpus is built from: annotated, with entities, sentences, confidences, non-BMP text and an unpaired surrogate.</summary>
    internal static Doc[] SeedDocs()
    {
        var docs = new List<Doc>();
        Doc a = Nlp.Analyze("Ada Lovelace wrote notes in London. She stayed 😀 café.");
        for (int i = 0; i < a.Length; i++)
        {
            a.SetAnnotation(TokenColumn.Pos, i, i % 2 == 0 ? "PROPN" : "VERB");
            a.SetAnnotation(TokenColumn.Lemma, i, a[i].ToString().ToLowerInvariant());
            a.SetAnnotation(TokenColumn.Morph, i, "Number=Sing");
            a.SetConfidence(i, 0.5f + (i % 3 * 0.25f));
        }

        a.SetHeads([.. Enumerable.Range(0, a.Length).Select(i => i == 0 ? 0 : i - 1)]);
        a.SetSentenceStart(0, true);
        a.SetSentenceStart(7, true);
        a.SetEntities([new SpanSpec(0, 2, "PER", 0.9f), new SpanSpec(5, 6, "LOC")]);
        a.SetCats([new KeyValuePair<string, float>("HISTORY", 0.75f), new KeyValuePair<string, float>("SCIENCE", 0.125f)]);
        a.SetSpans("sc", [new SpanSpec(0, 2, "PERSON", 0.9f), new SpanSpec(1, 2, "NAME", 0.6f), new SpanSpec(0, 4, "EVENT")]);
        a.SetSpans("other", [new SpanSpec(5, 7, "PLACE", 0.5f)]);
        docs.Add(a);
        docs.Add(Nlp.Analyze(""));
        docs.Add(Nlp.Analyze("plain words only"));
        docs.Add(Nlp.Analyze("lone \uD800 surrogate"));
        return [.. docs];
    }

    internal static void SeedJson(string directory)
    {
        int n = 0;
        foreach (Doc d in SeedDocs())
        {
            if (TextUtilIsWellFormed(d.Text))
            {
                File.WriteAllBytes(Path.Combine(directory, $"seed-json-{n++}"), d.ToJsonUtf8());
            }
        }
    }

    internal static void SeedBinary(string directory)
    {
        int n = 0;
        foreach (Doc d in SeedDocs())
        {
            File.WriteAllBytes(Path.Combine(directory, $"seed-doc-{n}"), [0, .. d.ToBytes(checksum: false)]);
            File.WriteAllBytes(Path.Combine(directory, $"seed-doc-sum-{n++}"), [0, .. d.ToBytes()]);
        }
    }

    internal static void SeedBin(string directory)
    {
        var bin = new DocBin();
        foreach (Doc d in SeedDocs())
        {
            bin.Add(d);
        }

        File.WriteAllBytes(Path.Combine(directory, "seed-bin"), [0, .. bin.ToBytes(checksum: false)]);
        File.WriteAllBytes(Path.Combine(directory, "seed-bin-sum"), [0, .. bin.ToBytes()]);
        File.WriteAllBytes(Path.Combine(directory, "seed-bin-empty"), [0, .. new DocBin().ToBytes(checksum: false)]);
    }

    private static bool TextUtilIsWellFormed(string s)
    {
        try
        {
            _ = new System.Text.UTF8Encoding(false, true).GetBytes(s);
            return true;
        }
        catch (System.Text.EncoderFallbackException)
        {
            return false;
        }
    }

    private static void Require(bool condition, string what)
    {
        if (!condition)
        {
            throw new InvalidOperationException("Invariant failed: " + what);
        }
    }

    /// <summary>
    /// Input: byte 0 is the mode, the rest the stream. Bit 0 rewrites the 12-byte header so the declared body length and the flags fit the
    /// rest of the input (and bit 1 then appends a valid checksum), so mutations reach the section decoders instead of stopping at the framing.
    /// </summary>
    private static byte[] Frame(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0)
        {
            return [];
        }

        byte mode = data[0];
        byte[] bytes = data[1..].ToArray();
        if ((mode & 1) == 0 || bytes.Length < DocBinaryHeader)
        {
            return bytes;
        }

        bool checksum = (mode & 2) != 0;
        int body = bytes.Length - DocBinaryHeader;
        if (checksum)
        {
            body = Math.Max(0, body - 4);
        }

        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), checksum ? (ushort)1 : (ushort)0);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), (uint)body);
        if (checksum && bytes.Length >= DocBinaryHeader + 4)
        {
            uint crc = 0xFFFFFFFF;
            foreach (byte b in bytes.AsSpan(0, bytes.Length - 4))
            {
                crc ^= b;
                for (int k = 0; k < 8; k++)
                {
                    crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
                }
            }

            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(bytes.Length - 4), ~crc);
        }

        return bytes;
    }

    private const int DocBinaryHeader = 12;

    internal static void Json(ReadOnlySpan<byte> data)
    {
        Doc doc;
        try
        {
            doc = Doc.FromJson(data);
        }
        catch (InvalidDataException)
        {
            return;
        }

        byte[] bytes = doc.ToBytes(checksum: false);
        Require(Doc.FromBytes(bytes).ToBytes(checksum: false).AsSpan().SequenceEqual(bytes), "binary round trip");
        string json;
        try
        {
            json = doc.ToJson();
        }
        catch (InvalidOperationException)
        {
            // An escape such as \ud800 reads as an unpaired surrogate, which JSON text cannot be written back with.
            return;
        }

        Require(Doc.FromJson(json).ToBytes(checksum: false).AsSpan().SequenceEqual(bytes), "JSON round trip");
    }

    internal static void Binary(ReadOnlySpan<byte> data)
    {
        byte[] input = Frame(data);
        Doc doc;
        try
        {
            doc = Doc.FromBytes(input);
        }
        catch (InvalidDataException)
        {
            return;
        }

        byte[] again = doc.ToBytes(checksum: false);
        Require(Doc.FromBytes(again).ToBytes(checksum: false).AsSpan().SequenceEqual(again), "binary round trip");
        try
        {
            Require(Doc.FromJson(doc.ToJson()).ToBytes(checksum: false).AsSpan().SequenceEqual(again), "JSON round trip");
        }
        catch (InvalidOperationException)
        {
            // Unpaired surrogates cannot be written as JSON text.
        }
    }

    internal static void Bin(ReadOnlySpan<byte> data)
    {
        byte[] input = Frame(data);

        // Also make the document-list section fit, which Frame cannot know about.
        if (data.Length > 0 && (data[0] & 1) != 0 && input.Length >= DocBinaryHeader + 5)
        {
            uint first = BinaryPrimitives.ReadUInt32LittleEndian(input.AsSpan(DocBinaryHeader + 1));
            long docsAt = DocBinaryHeader + 5L + first;
            if (docsAt + 5 <= input.Length)
            {
                int tail = input.Length - (int)(docsAt + 5) - ((data[0] & 2) != 0 ? 4 : 0);
                if (tail >= 0)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(input.AsSpan((int)docsAt + 1), (uint)tail);
                }
            }
        }

        DocBin bin;
        try
        {
            bin = DocBin.FromBytes(input);
        }
        catch (InvalidDataException)
        {
            return;
        }

        byte[] again = bin.ToBytes(checksum: false);
        Require(DocBin.FromBytes(again).ToBytes(checksum: false).AsSpan().SequenceEqual(again), "bin round trip");

        // The streaming reader agrees with the whole-bin reader.
        using var ms = new MemoryStream(again);
        var reader = new DocBinReader(ms);
        int count = 0;
        while (reader.TryRead(out Doc? d))
        {
            Require(d.ToBytes(checksum: false).AsSpan().SequenceEqual(bin.Docs[count].ToBytes(checksum: false)), "streamed document");
            count++;
        }

        Require(count == bin.Count, "streamed count");
    }
}
