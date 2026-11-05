using System.Text;

namespace RecursiveAI.OneNlp.Eval;

/// <summary>The <c>segment</c> command: raw text to CoNLL-U (ID and FORM only) through the default word tokenizer and sentencizer.</summary>
internal static class Segment
{
    // The pieces UD English splits off a word, and the fused forms it splits in two.
    private static readonly string[] Clitics =
        ["n't", "n\u2019t", "'s", "\u2019s", "'re", "\u2019re", "'ve", "\u2019ve", "'ll", "\u2019ll", "'d", "\u2019d", "'m", "\u2019m"];

    private static readonly string[] Fused = ["cannot", "gonna", "gotta", "wanna", "gimme", "lemme"];

    internal static int Run(Dictionary<string, List<string>> o)
    {
        string text = File.ReadAllText(Option(o, "raw"), Encoding.UTF8);
        Language language = o.TryGetValue("language", out List<string>? l) && l.Count > 0 ? Language.FromCode(l[0]) : Language.English;
        bool mwt = o.ContainsKey("mwt");
        using Pipeline pipeline = Pipeline.Blank(language).Add("sentencizer", new Dictionary<string, string> { ["language"] = language.Code });
        Doc doc = pipeline.Process(text);
        Segmentation.SegmentationData data = Segmentation.SegmentationData.For(language);
        var sb = new StringBuilder();
        foreach (Span sentence in doc.Sentences)
        {
            var tokens = new List<Token>();
            foreach (Token token in sentence)
            {
                tokens.Add(token);
            }

            for (int i = 0; i < tokens.Count; i++)
            {
                int groupEnd = i;
                if (mwt && data.TryMatchMultiword(doc, tokens[i].Index, out int pieces, out string[]? words))
                {
                    // The pieces of a contraction (French "d" + "u") are written as the words they stand for ("de", "le").
                    sb.Append(i + 1).Append('-').Append(i + pieces).Append('\t')
                        .Append(text, tokens[i].Idx, tokens[i + pieces - 1].EndIdx - tokens[i].Idx).Append("\t_\t_\t_\t_\t_\t_\t_\t_\n");
                    for (int j = 0; j < pieces; j++)
                    {
                        sb.Append(i + j + 1).Append('\t').Append(words[j]).Append("\t_\t_\t_\t_\t")
                            .Append(i + j == 0 ? "0\troot" : "1\tdep").Append("\t_\t_\n");
                    }

                    i += pieces - 1;
                    continue;
                }

                if (mwt && i + 1 < tokens.Count && tokens[i].EndIdx == tokens[i + 1].Idx)
                {
                    string second = tokens[i + 1].ToString();
                    string whole = tokens[i].ToString() + second;
                    if (Array.Exists(Clitics, c => string.Equals(c, second, StringComparison.OrdinalIgnoreCase))
                        || Array.Exists(Fused, f => string.Equals(f, whole, StringComparison.OrdinalIgnoreCase)))
                    {
                        groupEnd = i + 1;
                    }
                }

                if (groupEnd > i)
                {
                    sb.Append(i + 1).Append('-').Append(groupEnd + 1).Append('\t')
                        .Append(text, tokens[i].Idx, tokens[groupEnd].EndIdx - tokens[i].Idx)
                        .Append("\t_\t_\t_\t_\t_\t_\t_\t_\n");
                }

                for (int j = i; j <= groupEnd; j++)
                {
                    sb.Append(j + 1).Append('\t').Append(tokens[j].Text).Append("\t_\t_\t_\t_\t")
                        .Append(j == 0 ? "0\troot" : "1\tdep").Append("\t_\t_\n");
                }

                i = groupEnd;
            }

            sb.Append('\n');
        }

        File.WriteAllText(Option(o, "out"), sb.ToString(), new UTF8Encoding(false));
        return 0;
    }

    private static string Option(Dictionary<string, List<string>> o, string name) =>
        o.TryGetValue(name, out List<string>? v) && v.Count > 0 ? v[0] : throw new ArgumentException($"--{name} is required.");
}
