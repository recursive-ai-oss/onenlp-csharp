using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using RecursiveAI.OneNlp;
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
        Check("loader not yet supported", Throws<NotSupportedException>(() => Tokenizer.FromFile("vocab.txt")));

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
}
