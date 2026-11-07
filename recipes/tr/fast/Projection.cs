using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Segmentation;

namespace RecursiveAI.OneNlp.Recipes.Turkish;

// Training documents as the parser sees them at run time: the words are the tokenizer's, not the treebank's.
//
// The treebank's words and the tokenizer's tokens differ where the tokenizer cuts a surface token the treebank keeps whole, or keeps
// whole one the treebank cuts. A parser that only ever trained on treebank words has never seen such a token; this projects the
// treebank annotation onto the tokenizer's tokens so that it does:
//   - a token that is exactly one treebank word keeps that word's head and relation;
//   - a token that covers several treebank words (a missed cut) takes the head and relation of the word that heads them;
//   - the first piece of a treebank word that the tokenizer cut keeps the word's head and relation, the other pieces attach to it with no relation;
//   - a head is the first token that overlaps the treebank head word, a sentence starts at the token that starts the treebank sentence.
internal static partial class Program
{
    private static int projectedChanged;
    private static int projectedTokens;

    // Returns the projection of the treebank document onto the tokens of the tokenizer, or the document itself where the two cannot be
    // laid over each other (a token across a sentence start).
    private static Doc Project(Doc gold, Doc tokens, StringStore strings)
    {
        int n = gold.Length;
        int m = tokens.Length;
        ReadOnlySpan<int> gs = gold.GetStarts();
        ReadOnlySpan<int> gl = gold.GetLengths();
        ReadOnlySpan<int> ps = tokens.GetStarts();
        ReadOnlySpan<int> pl = tokens.GetLengths();
        ReadOnlySpan<int> gheads = gold.GetHeads();
        ReadOnlySpan<int> gdeps = gold.GetColumn(TokenColumn.Dep);
        ReadOnlySpan<SentenceBoundary> gmarks = gold.GetSentenceBoundaries();

        var firstPiece = new int[n]; // the first token that overlaps the word
        var covered = new List<int>[m];
        for (int p = 0; p < m; p++)
        {
            covered[p] = [];
        }

        for (int w = 0; w < n; w++)
        {
            firstPiece[w] = -1;
        }

        int pStart = 0;
        for (int w = 0; w < n; w++)
        {
            int gEnd = gs[w] + gl[w];
            while (pStart < m && ps[pStart] + pl[pStart] <= gs[w])
            {
                pStart++;
            }

            for (int p = pStart; p < m && ps[p] < gEnd; p++)
            {
                if (ps[p] + pl[p] > gs[w])
                {
                    if (firstPiece[w] < 0)
                    {
                        firstPiece[w] = p;
                    }

                    covered[p].Add(w);
                }
            }
        }

        var heads = new int[m];
        var deps = new int[m];
        var marks = new SentenceBoundary[m];
        for (int p = 0; p < m; p++)
        {
            if (covered[p].Count == 0)
            {
                return gold; // a token with no treebank word under it
            }

            // The word of the token that heads the others, or the only one.
            int main = covered[p][0];
            foreach (int w in covered[p])
            {
                if (!covered[p].Contains(gheads[w]) || gheads[w] == w)
                {
                    main = w;
                    break;
                }
            }

            if (firstPiece[main] != p)
            {
                // An extra piece of a word the tokenizer cut: it hangs on the first piece, with no relation.
                heads[p] = firstPiece[main];
                deps[p] = 0;
                marks[p] = SentenceBoundary.Inside;
                continue;
            }

            int h = gheads[main];
            if (h == main || h < 0 || h >= n)
            {
                heads[p] = p;
            }
            else
            {
                int hp = firstPiece[h];
                if (hp < 0)
                {
                    return gold;
                }

                heads[p] = hp == p ? p : hp;
            }

            deps[p] = gdeps[main];
            marks[p] = gmarks[covered[p][0]] == SentenceBoundary.Start && firstPiece[covered[p][0]] == p ? SentenceBoundary.Start : SentenceBoundary.Inside;
        }

        marks[0] = SentenceBoundary.Start;
        var boundaries = new TokenBoundary[m];
        for (int p = 0; p < m; p++)
        {
            boundaries[p] = new TokenBoundary(ps[p], pl[p]);
        }

        var result = new Doc(strings, gold.Text, boundaries);
        result.SetHeads(heads);
        result.SetColumn(TokenColumn.Dep, deps);
        result.SetSentenceBoundaries(marks);
        projectedTokens += m;
        if (m != n)
        {
            projectedChanged++;
        }

        return result;
    }
}
