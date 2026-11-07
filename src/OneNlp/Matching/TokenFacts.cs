using System.Text.RegularExpressions;

namespace RecursiveAI.OneNlp.Matching;

/// <summary>
/// The values of a document's tokens that patterns test, computed once per token on first use. One instance serves one match call.
/// </summary>
internal sealed class TokenFacts
{
    private readonly Doc _doc;
    private readonly HashSet<string>? _stopWords;
    private readonly string?[] _lower;
    private readonly string?[] _shape;
    private readonly uint[] _known;
    private readonly uint[] _values;

    internal TokenFacts(Doc doc, HashSet<string>? stopWords)
    {
        _doc = doc;
        _stopWords = stopWords;
        _lower = new string?[doc.Length];
        _shape = new string?[doc.Length];
        _known = new uint[doc.Length];
        _values = new uint[doc.Length];
    }

    internal int Length => _doc.Length;

    internal ReadOnlySpan<char> Orth(int i) => _doc[i].TextSpan;

    internal string Lower(int i) => _lower[i] ??= _doc[i].TextSpan.ToString().ToLowerInvariant();

    internal string Shape(int i) => _shape[i] ??= TokenText.Shape(_doc[i].TextSpan);

    internal int CodePoints(int i) => TokenText.CodePointCount(_doc[i].TextSpan);

    /// <summary>The text value of a string attribute. The span is valid until the document changes.</summary>
    internal ReadOnlySpan<char> Value(TokenProperty attribute, int i)
    {
        Token t = _doc[i];
        return attribute switch
        {
            TokenProperty.Orth => t.TextSpan,
            TokenProperty.Lower => Lower(i),
            TokenProperty.Norm => NormOf(i),
            TokenProperty.Lemma => t.Lemma,
            TokenProperty.Pos => t.Pos,
            TokenProperty.Tag => t.Tag,
            TokenProperty.Dep => t.Dep,
            TokenProperty.Morph => t.Morph,
            TokenProperty.EntType => t.EntType,
            TokenProperty.EntIob => t.EntIob switch
            {
                EntIob.Begin => "B",
                EntIob.Inside => "I",
                EntIob.Outside => "O",
                _ => string.Empty,
            },
            TokenProperty.Shape => Shape(i),
            _ => throw new ArgumentOutOfRangeException(nameof(attribute)),
        };
    }

    internal bool Flag(TokenProperty attribute, int i)
    {
        switch (attribute)
        {
            case TokenProperty.IsSentStart:
                // The first token starts a sentence even when no component has set boundaries.
                return i == 0 || _doc.GetSentenceBoundaries()[i] == SentenceBoundary.Start;
            case TokenProperty.HasSpaceAfter:
                return _doc[i].HasSpaceAfter;
        }

        uint bit = 1u << (attribute - TokenProperty.IsAlpha);
        if ((_known[i] & bit) == 0)
        {
            if (Compute(attribute, i))
            {
                _values[i] |= bit;
            }

            _known[i] |= bit;
        }

        return (_values[i] & bit) != 0;
    }

    private string NormOf(int i)
    {
        string norm = _doc[i].Norm;
        return norm.Length > 0 ? norm : Lower(i);
    }

    private bool Compute(TokenProperty attribute, int i)
    {
        ReadOnlySpan<char> text = _doc[i].TextSpan;
        return attribute switch
        {
            TokenProperty.IsAlpha => TokenText.IsAlpha(text),
            TokenProperty.IsAscii => TokenText.IsAscii(text),
            TokenProperty.IsDigit => TokenText.IsDigit(text),
            TokenProperty.IsLower => TokenText.IsLower(text),
            TokenProperty.IsUpper => TokenText.IsUpper(text),
            TokenProperty.IsTitle => TokenText.IsTitle(text),
            TokenProperty.IsPunct => TokenText.IsPunct(text),
            TokenProperty.IsSpace => TokenText.IsSpace(text),
            TokenProperty.IsStop => _stopWords is not null && _stopWords.Contains(Lower(i)),
            TokenProperty.IsCurrency => TokenText.IsCurrency(text),
            TokenProperty.IsLeftPunct => TokenText.IsLeftPunct(text),
            TokenProperty.IsRightPunct => TokenText.IsRightPunct(text),
            TokenProperty.IsQuote => TokenText.IsQuote(text),
            TokenProperty.IsBracket => TokenText.IsBracket(text),
            TokenProperty.LikeNum => TokenText.LikeNum(text),
            TokenProperty.LikeUrl => TokenText.LikeUrl(text),
            TokenProperty.LikeEmail => TokenText.LikeEmail(text),
            _ => throw new ArgumentOutOfRangeException(nameof(attribute)),
        };
    }

    internal bool Satisfies(Constraint c, int i)
    {
        TokenProperty a = c.Attribute;
        if (TokenProperties.IsFlag(a))
        {
            return Flag(a, i) == c.Flag;
        }

        if (a == TokenProperty.Length)
        {
            int length = CodePoints(i);
            return c.Test switch
            {
                Test.Same => length == c.Number,
                Test.In => Array.IndexOf(c.Numbers!, length) >= 0,
                Test.NotIn => Array.IndexOf(c.Numbers!, length) < 0,
                _ => c.Comparison switch
                {
                    Comparison.Equal => length == c.Number,
                    Comparison.NotEqual => length != c.Number,
                    Comparison.GreaterOrEqual => length >= c.Number,
                    Comparison.LessOrEqual => length <= c.Number,
                    Comparison.Greater => length > c.Number,
                    _ => length < c.Number,
                },
            };
        }

        ReadOnlySpan<char> v = Value(a, i);
        switch (c.Test)
        {
            case Test.Same:
                // Observed in output of spaCy 3.8.18: a MORPH test against the empty string never matches, not even tokens with no
                // morphological analysis. Use NOT_IN or an IS_SUBSET test to select tokens without features.
                return !(a == TokenProperty.Morph && c.Text!.Length == 0) && v.SequenceEqual(c.Text);
            case Test.In:
                return c.TextSet!.GetAlternateLookup<ReadOnlySpan<char>>().Contains(v);
            case Test.NotIn:
                return !c.TextSet!.GetAlternateLookup<ReadOnlySpan<char>>().Contains(v);
            case Test.Regex:
                try
                {
                    return c.Expression!.IsMatch(v);
                }
                catch (RegexMatchTimeoutException)
                {
                    return false;
                }

            default:
                return MorphTest(c, v);
        }
    }

    private static bool MorphTest(Constraint c, ReadOnlySpan<char> morph)
    {
        // Morphological features are the Key=Value pieces of a '|'-separated string; the tests compare them as sets.
        var lookup = c.TextSet!.GetAlternateLookup<ReadOnlySpan<char>>();
        switch (c.Test)
        {
            case Test.IsSubset:
                foreach (Range r in morph.Split('|'))
                {
                    if (morph[r].Length > 0 && !lookup.Contains(morph[r]))
                    {
                        return false;
                    }
                }

                return true;
            case Test.Intersects:
                foreach (Range r in morph.Split('|'))
                {
                    if (morph[r].Length > 0 && lookup.Contains(morph[r]))
                    {
                        return true;
                    }
                }

                return false;
            default:
                foreach (string required in c.Texts!)
                {
                    bool found = false;
                    foreach (Range r in morph.Split('|'))
                    {
                        if (morph[r].SequenceEqual(required))
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        return false;
                    }
                }

                return true;
        }
    }
}
