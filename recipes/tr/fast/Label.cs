using System.Text;
using System.Text.Json;
using RecursiveAI.OneNlp.Neural;

namespace RecursiveAI.OneNlp.Recipes.Turkish;

// label --models DIR --parts splitter,tagging,parser --text F.jsonl --out F.conllu [--max N] [--reverse 1] [--min-words 5 --max-words 40]
//
// Parses web text with the parts in DIR (the rule sentencizer sets the sentences) and writes the sentences as CoNLL-U with words, heads and
// relations only, to be filtered and used as extra parser training data. Paragraphs are the lines of each document's "t" field.
internal static partial class Program
{
    private static int Label(string[] args)
    {
        string[] withSentencizer = [.. args, "--keep-sentencizer", "1"];
        using Pipeline pipeline = LoadParts(Required(args, "models"), withSentencizer);
        int max = Num(args, "max", 100000);
        int minWords = Num(args, "min-words", 5);
        int maxWords = Num(args, "max-words", 40);
        var sb = new StringBuilder();
        int written = 0;
        // --reverse 1 reads the documents from the last one, so that a second round mostly meets text the first did not.
        IEnumerable<string> lines = Num(args, "reverse", 0) == 1 ? File.ReadAllLines(Required(args, "text")).Reverse() : File.ReadLines(Required(args, "text"));
        foreach (string line in lines)
        {
            if (written >= max)
            {
                break;
            }

            using JsonDocument json = JsonDocument.Parse(line);
            string text = json.RootElement.GetProperty("t").GetString() ?? string.Empty;
            foreach (string paragraph in text.Split('\n'))
            {
                if (paragraph.Length is < 20 or > 800)
                {
                    continue;
                }

                Doc doc = pipeline.Process(paragraph);
                foreach (Span sentence in doc.Sentences)
                {
                    int n = sentence.End - sentence.Start;
                    if (n < minWords || n > maxWords || written >= max)
                    {
                        continue;
                    }

                    sb.Append("# sent_id = pseudo-").Append(written).Append('\n');
                    WriteSentence(sb, doc, sentence.Start, sentence.End, [], i => doc[sentence.Start + i].ToString());
                    sb.Append('\n');
                    written++;
                }
            }
        }

        File.WriteAllText(Required(args, "out"), sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"sentences={written}");
        return 0;
    }
}
