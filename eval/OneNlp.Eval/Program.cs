using System.Text;
using RecursiveAI.OneNlp;

// Evaluation helpers. Scoring itself is done by the CoNLL 2018 shared-task script on the files written here.
//
//   segment <raw.txt> <out.conllu> [language-code] [mwt]
//       Tokenizes and splits sentences with the default pipeline and writes CoNLL-U with only ID and FORM filled, the shape the
//       scorer expects from a tokenization-only system. Every token is a word. With "mwt", the pieces of a split contraction
//       (do + n't) are also written as one multi-word token line, as UD English treebanks annotate them.
if (args.Length >= 3 && args[0] == "segment")
{
    string text = File.ReadAllText(args[1], Encoding.UTF8);
    Language language = args.Length > 3 ? Language.FromCode(args[3]) : Language.English;
    bool mwt = args.Length > 4 && args[4] == "mwt";
    using Pipeline pipeline = Pipeline.Blank(language).Add("sentencizer", new Dictionary<string, string> { ["language"] = language.Code });
    Doc doc = pipeline.Process(text);
    string[] clitics = ["n't", "n\u2019t", "'s", "\u2019s", "'re", "\u2019re", "'ve", "\u2019ve", "'ll", "\u2019ll", "'d", "\u2019d", "'m", "\u2019m"];
    string[] fused = ["cannot", "gonna", "gotta", "wanna", "gimme", "lemme"];
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
            if (mwt && i + 1 < tokens.Count && tokens[i].EndIdx == tokens[i + 1].Idx)
            {
                string second = tokens[i + 1].ToString();
                string whole = tokens[i].ToString() + second;
                if (Array.Exists(clitics, c => string.Equals(c, second, StringComparison.OrdinalIgnoreCase))
                    || Array.Exists(fused, f => string.Equals(f, whole, StringComparison.OrdinalIgnoreCase)))
                {
                    groupEnd = i + 1;
                }
            }

            if (groupEnd > i)
            {
                sb.Append(i + 1).Append('-').Append(groupEnd + 1).Append('\t').Append(text, tokens[i].Idx, tokens[groupEnd].EndIdx - tokens[i].Idx)
                    .Append("\t_\t_\t_\t_\t_\t_\t_\t_\n");
            }

            for (int j = i; j <= groupEnd; j++)
            {
                sb.Append(j + 1).Append('\t').Append(tokens[j].Text).Append("\t_\t_\t_\t_\t").Append(j == 0 ? "0\troot" : "1\tdep").Append("\t_\t_\n");
            }

            i = groupEnd;
        }

        sb.Append('\n');
    }

    File.WriteAllText(args[2], sb.ToString(), new UTF8Encoding(false));
    return 0;
}

Console.Error.WriteLine("usage: segment <raw.txt> <out.conllu> [language-code] [mwt]");
return 2;
