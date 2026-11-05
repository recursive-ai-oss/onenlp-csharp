using RecursiveAI.OneNlp.Neural;

namespace RecursiveAI.OneNlp.Recipes.EnglishAssemble;

// Combines separately trained pipelines (here: tagging, parser and entity recognizer, each with its own encoder) into one pipeline file.
//
//   assemble <out.onenlp> <part.onenlp>...
//
// The parts must use the same tokenizer data. The components of every part are added in order, with one exception: a rule
// "sentencizer" saved in a part is dropped, because the parser predicts sentence starts better than the rule does on raw text and sets
// them itself. Components keep their own encoders and weights, so each part predicts what it predicted alone.
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("usage: assemble <out.onenlp> <part.onenlp>...");
            return 2;
        }

        var parts = new List<Pipeline>();
        foreach (string path in args.Skip(1))
        {
            parts.Add(PipelineFile.Load(path));
        }

        Pipeline first = parts[0];
        using var combined = new Pipeline(first.Tokenizer, first.Language, first.Strings);
        foreach (Pipeline part in parts)
        {
            if (part.Language != first.Language)
            {
                throw new InvalidOperationException("The parts are for different languages.");
            }

            foreach (IPipelineComponent component in part.Components)
            {
                if (component.Name != "sentencizer")
                {
                    combined.Add(component);
                }
            }
        }

        PipelineFile.Save(combined, args[0]);
        Console.WriteLine("components=" + string.Join(",", combined.Components.Select(c => c.Name)) + " bytes=" + new FileInfo(args[0]).Length);
        return 0;
    }
}
