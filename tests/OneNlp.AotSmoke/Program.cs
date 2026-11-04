using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using RecursiveAI.OneNlp;
using RecursiveAI.OneNlp.Serialization;
using RecursiveAI.OneNlp.Tokenizers;

namespace RecursiveAI.OneNlp.AotSmoke;

/// <summary>
/// Builds a pipeline from registered names, processes text in batches, and uses the tokenizer contract, all under NativeAOT.
/// Registration through a module initializer is the way a package adds components without reflection.
/// </summary>
internal static class Program
{
    private static int failures;

    [ModuleInitializer]
    internal static void RegisterComponents() =>
        ComponentRegistry.Register("shout", settings => new ShoutComponent(settings.TryGetValue("suffix", out string? s) ? s : "!"));

    private static int Main()
    {
        // Doc, Token and Span views.
        Doc doc = Nlp.Analyze("Ada Lovelace wrote notes, 1843.");
        Check("token count", doc.Length == 7);
        Check("offsets", doc[1].Idx == 4 && doc[1].EndIdx == 12 && doc[1].Text.SequenceEqual("Lovelace"));
        doc.SetEntities([new SpanSpec(0, 2, "PER", 0.75f)]);
        Check("entity", doc.Ents.Count == 1 && doc.Ents[0].ToString() == "Ada Lovelace" && doc[1].EntIob == EntIob.Inside && doc.Ents[0].Confidence == 0.75f);
        doc.SetAnnotation(TokenColumn.Pos, 0, "PROPN");
        Check("annotation", doc[0].Pos == "PROPN" && doc[1].Pos.Length == 0);
        Check("char span", doc.TryGetCharSpan(4, 18, out Span span) && span.ToString() == "Lovelace wrote");

        // Word and sentence segmentation, with English data and the registered sentencizer.
        Token[] tokens = Nlp.Tokens("Mr. Smith can't go. He's gone!");
        Check("contraction split", tokens.Length == 10 && tokens[2].ToString() == "ca" && tokens[3].ToString() == "n't");
        Check("sentences", Nlp.Sentences("Mr. Smith can't go. He's gone!").Count == 2);

        // Registration by name, built-in and from this assembly's module initializer.
        using Pipeline pipeline = Pipeline.Blank(Language.English).Add("norm").Add("shout", new Dictionary<string, string> { ["suffix"] = "?" });
        Doc[] docs = [.. pipeline.ProcessMany(["One Two", "THREE"], batchSize: 1)];
        Check("pipeline order", docs.Length == 2 && docs[0][0].Norm == "one" && docs[0][1].Lemma == "Two?" && docs[1][0].Norm == "three");
        Check("unknown component", Throws<InvalidOperationException>(() => pipeline.Add("missing")));

        // The tokenizer contract.
        var tokenizer = new SpaceTokenizer();
        Span<int> ids = stackalloc int[2];
        OperationStatus status = tokenizer.EncodeToIds("héllo wörld again"u8, ids, out int written, out int consumed);
        Check("encode to ids", status == OperationStatus.DestinationTooSmall && written == 2 && consumed == 13);
        TokenEncoding encoding = tokenizer.Encode("héllo wörld"u8);
        Check("utf8 offsets", encoding.Offsets[1].Start == 7 && encoding.Offsets[1].End == 13);
        Check("trim", tokenizer.TrimToTokenBudget("a b c", 2).SequenceEqual("a b"));
        Check("empty vocab.txt rejected", Throws<InvalidDataException>(() => Tokenizer.FromStream(new MemoryStream(), TokenizerFormat.WordPieceVocabulary)));

        // Both serialization formats, and the container, under NativeAOT.
        Doc annotated = Nlp.Analyze("Ada Lovelace wrote notes in London \uD83D\uDE00.");
        annotated.SetAnnotation(TokenColumn.Pos, 0, "PROPN");
        annotated.SetAnnotation(TokenColumn.Norm, 1, "lovelace");
        annotated.SetHeads(new int[annotated.Length]);
        annotated.SetSentenceStart(0, true);
        annotated.SetConfidence(2, 0.5f);
        annotated.SetEntities([new SpanSpec(0, 2, "PER", 0.75f)]);
        Doc fromJson = Doc.FromJson(annotated.ToJson());
        Check("json round trip", fromJson.Length == annotated.Length && fromJson[0].Pos == "PROPN" && fromJson[1].Norm == "lovelace" && fromJson.Ents[0].Confidence == 0.75f && fromJson[2].Confidence == 0.5f);
        Doc fromBytes = Doc.FromBytes(annotated.ToBytes());
        Check("binary round trip", fromBytes.ToJson() == annotated.ToJson() && fromBytes[5].HeadIndex == 0);
        var bin = new DocBin();
        bin.Add(annotated);
        bin.Add(Nlp.Analyze("second"));
        DocBin binBack = DocBin.FromBytes(bin.ToBytes());
        int streamed = 0;
        var binReader = new DocBinReader(new MemoryStream(bin.ToBytes()));
        while (binReader.TryRead(out _))
        {
            streamed++;
        }

        Check("docbin", binBack.Count == 2 && streamed == 2);
        Check("hostile input", Throws<InvalidDataException>(() => Doc.FromBytes(new byte[] { 1, 2, 3 })) && Throws<InvalidDataException>(() => Doc.FromJson("{")));

        Check("tiktoken ranks", TiktokenRoundTrip());

        // The tokenizer.json stages: loader, normalizers with generated tables, split patterns, post-processing, decoding.
        foreach (string failed in StageSmoke.Run())
        {
            Check(failed, false);
        }

        Console.WriteLine(failures == 0 ? "OK" : $"{failures} check(s) failed");
        return failures == 0 ? 0 : 1;
    }

    private static bool Throws<T>(Action action)
        where T : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (T)
        {
            return true;
        }
    }

    private static void Check(string name, bool ok)
    {
        if (!ok)
        {
            failures++;
            Console.Error.WriteLine("FAILED: " + name);
        }
    }

    private sealed class ShoutComponent(string suffix) : IPipelineComponent
    {
        public string Name => "shout";

        public void Process(Doc doc)
        {
            for (int i = 0; i < doc.Length; i++)
            {
                doc.SetAnnotation(TokenColumn.Lemma, i, doc[i].ToString() + suffix);
            }
        }
    }

    /// <summary>One token per space-separated word; the ID is the word's length.</summary>
    private sealed class SpaceTokenizer : Tokenizer
    {
        public override int VocabularySize => 0;

        public override string Decode(ReadOnlySpan<int> ids, bool skipSpecialTokens = true) => string.Join(' ', ids.ToArray());

        public override string? IdToToken(int id) => null;

        public override int? TokenToId(string token) => null;

        protected override TokenEncoding EncodeCore(ReadOnlySpan<char> text, bool addSpecialTokens, TruncationOptions? truncation)
        {
            var ids = new List<int>();
            var offsets = new List<TokenOffset>();
            var tokens = new List<string>();
            int i = 0;
            while (i < text.Length)
            {
                if (text[i] == ' ')
                {
                    i++;
                    continue;
                }

                int start = i;
                while (i < text.Length && text[i] != ' ')
                {
                    i++;
                }

                ids.Add(i - start);
                offsets.Add(new TokenOffset(start, i));
                tokens.Add(text[start..i].ToString());
            }

            return new TokenEncoding([.. ids], [.. tokens], [.. offsets], new bool[ids.Count]);
        }
    }

    // A tiny Tiktoken encoding: the 256 bytes and the token "ab".
    private static bool TiktokenRoundTrip()
    {
        var ranks = new System.Text.StringBuilder();
        for (int b = 0; b < 256; b++)
        {
            ranks.Append(Convert.ToBase64String([(byte)b])).Append(' ').Append(b).Append('\n');
        }

        ranks.Append(Convert.ToBase64String("ab"u8)).Append(" 256\n");
        var tokenizer = TiktokenTokenizer.Create(new MemoryStream(System.Text.Encoding.ASCII.GetBytes(ranks.ToString())), new System.Text.RegularExpressions.Regex(@"\w+|\s+|[^\w\s]+"));
        TokenEncoding e = tokenizer.Encode("abab 日");
        return e.Ids[0] == 256 && e.Ids[1] == 256 && tokenizer.Decode(e.Ids) == "abab 日";
    }
}
