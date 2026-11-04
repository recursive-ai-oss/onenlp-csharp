using System.Buffers;
using System.Text;
using RecursiveAI.OneNlp.Tokenizers;

namespace RecursiveAI.OneNlp.Tests.Parity;

/// <summary>Deliberate defects for checking that the parity harness notices them.</summary>
public enum ToyFault
{
    None,
    WrongId,
    WrongOffset,
    WrongSpecialMask,
    WrongDecode,
    WrongConsumed,
    WrongTruncationSide,
    WrongPairTieBreak,
}

/// <summary>
/// A character-level tokenizer that mirrors <c>toy/tokenizer.json</c> (a WordLevel model over single characters, four special
/// tokens, a [CLS]/[SEP] template and a fuse decoder), written from that file and the published tokenizer.json description. It
/// stands in for a real family so the harness can be proven before any family exists.
/// </summary>
internal sealed class ToyCharTokenizer : Tokenizer
{
    private const string Characters = "abcdefghijklmnopqrstuvwxyz0123456789 .,!?-:/@#";
    private static readonly string[] Specials = ["[UNK]", "[CLS]", "[SEP]", "[PAD]"];
    private readonly ToyFault _fault;

    public ToyCharTokenizer(ToyFault fault = ToyFault.None) => _fault = fault;

    public override int VocabularySize => Specials.Length + Characters.Length;

    public override string? IdToToken(int id) =>
        id < 0 || id >= VocabularySize ? null : id < Specials.Length ? Specials[id] : Characters[id - Specials.Length].ToString();

    public override int? TokenToId(string token)
    {
        int s = Array.IndexOf(Specials, token);
        if (s >= 0)
        {
            return s;
        }

        return token.Length == 1 && Characters.IndexOf(token[0], StringComparison.Ordinal) is var c and >= 0 ? Specials.Length + c : null;
    }

    public override string Decode(ReadOnlySpan<int> ids, bool skipSpecialTokens = true)
    {
        var sb = new StringBuilder();
        foreach (int id in ids)
        {
            if (id < 0 || id >= VocabularySize || (skipSpecialTokens && id < Specials.Length))
            {
                continue;
            }

            sb.Append(IdToToken(id));
        }

        if (_fault == ToyFault.WrongDecode && sb.Length > 0)
        {
            sb.Length--;
        }

        return sb.ToString();
    }

    public override OperationStatus EncodeToIds(ReadOnlySpan<char> text, Span<int> ids, out int written, out int consumed, bool addSpecialTokens = true)
    {
        OperationStatus status = base.EncodeToIds(text, ids, out written, out consumed, addSpecialTokens);
        if (_fault == ToyFault.WrongConsumed && status == OperationStatus.DestinationTooSmall)
        {
            consumed++;
        }

        return status;
    }

    protected override TokenEncoding EncodeCore(ReadOnlySpan<char> text, bool addSpecialTokens, TruncationOptions? truncation)
    {
        List<Piece> pieces = Split(text);
        if (truncation is { } t)
        {
            int budget = t.MaxLength - (addSpecialTokens ? 2 : 0);
            pieces = Truncate(pieces, budget, t.Side);
        }

        return Assemble(addSpecialTokens, pieces, null, 0);
    }

    protected override TokenEncoding EncodePairCore(ReadOnlySpan<char> text, ReadOnlySpan<char> pair, bool addSpecialTokens, TruncationOptions? truncation)
    {
        List<Piece> a = Split(text), b = Split(pair);
        if (truncation is { } t)
        {
            // Longest-first. Observed in the output of tokenizers 0.22.1: the budget is split in halves, the shorter sequence keeps
            // at most half (rounded down) and the longer one the rest; when the lengths are equal the second counts as the longer.
            int budget = Math.Max(t.MaxLength - (addSpecialTokens ? 3 : 0), 0);
            if (a.Count + b.Count > budget)
            {
                bool secondLonger = _fault == ToyFault.WrongPairTieBreak ? b.Count > a.Count : b.Count >= a.Count;
                int shortKeep = Math.Min(secondLonger ? a.Count : b.Count, budget / 2);
                int longKeep = budget - shortKeep;
                a = Truncate(a, secondLonger ? shortKeep : longKeep, t.Side);
                b = Truncate(b, secondLonger ? longKeep : shortKeep, t.Side);
            }
        }

        return Assemble(addSpecialTokens, a, b, 1);
    }

    private List<Piece> Truncate(List<Piece> pieces, int budget, TokenizerSide side)
    {
        if (_fault == ToyFault.WrongTruncationSide)
        {
            side = side == TokenizerSide.Right ? TokenizerSide.Left : TokenizerSide.Right;
        }

        budget = Math.Max(budget, 0);
        if (pieces.Count <= budget)
        {
            return pieces;
        }

        return side == TokenizerSide.Right ? pieces.GetRange(0, budget) : pieces.GetRange(pieces.Count - budget, budget);
    }

    private TokenEncoding Assemble(bool addSpecialTokens, List<Piece> a, List<Piece>? b, int secondType)
    {
        var ids = new List<int>();
        var tokens = new List<string>();
        var offsets = new List<TokenOffset>();
        var mask = new List<bool>();
        var types = new List<int>();
        var words = new List<int>();

        void Special(int id, int type)
        {
            ids.Add(id);
            tokens.Add(Specials[id]);
            offsets.Add(default);
            mask.Add(true);
            types.Add(type);
            words.Add(-1);
        }

        void Add(List<Piece> pieces, int type)
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                Piece p = pieces[i];
                ids.Add(_fault == ToyFault.WrongId && p.Token == "e" ? p.Id + 1 : p.Id);
                tokens.Add(p.Token);
                offsets.Add(_fault == ToyFault.WrongOffset && i == 3 ? new TokenOffset(p.Start, p.End + 1) : new TokenOffset(p.Start, p.End));
                mask.Add(_fault == ToyFault.WrongSpecialMask && i == 2 ? !p.IsSpecial : p.IsSpecial);
                types.Add(type);
                words.Add(p.Word);
            }
        }

        if (addSpecialTokens)
        {
            Special(1, 0);
        }

        Add(a, 0);
        if (addSpecialTokens)
        {
            Special(2, 0);
        }

        if (b is not null)
        {
            Add(b, secondType);
            if (addSpecialTokens)
            {
                Special(2, secondType);
            }
        }

        return new TokenEncoding([.. ids], [.. tokens], [.. offsets], [.. mask], [.. types], null, [.. words]);
    }

    /// <summary>
    /// Added tokens are cut out of the text first (they match literally), then every other character is its own piece. Observed in
    /// the output of tokenizers 0.22.1: a special token that appears in the text gets mask 0 and its own word index; only the
    /// tokens the template adds are marked special.
    /// </summary>
    private static List<Piece> Split(ReadOnlySpan<char> text)
    {
        var pieces = new List<Piece>();
        int word = 0;
        int i = 0;
        while (i < text.Length)
        {
            int matched = -1;
            if (text[i] == '[')
            {
                for (int s = 0; s < Specials.Length; s++)
                {
                    if (text[i..].StartsWith(Specials[s], StringComparison.Ordinal))
                    {
                        matched = s;
                        break;
                    }
                }
            }

            if (matched >= 0)
            {
                pieces.Add(new Piece(matched, Specials[matched], i, i + Specials[matched].Length, false, word++));
                i += Specials[matched].Length;
                continue;
            }

            int len = char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]) ? 2 : 1;
            string unit = len == 2 || !char.IsSurrogate(text[i]) ? text.Slice(i, len).ToString() : "\ufffd";
            int c = unit.Length == 1 ? Characters.IndexOf(unit[0], StringComparison.Ordinal) : -1;
            pieces.Add(c < 0 ? new Piece(0, Specials[0], i, i + len, false, word++) : new Piece(Specials.Length + c, unit, i, i + len, false, word++));
            i += len;
        }

        return pieces;
    }

    private readonly record struct Piece(int Id, string Token, int Start, int End, bool IsSpecial, int Word);
}
