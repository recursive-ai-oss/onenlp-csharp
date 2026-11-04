using System.Text.RegularExpressions;

namespace RecursiveAI.OneNlp.Tokenizers.Tiktoken;

/// <summary>
/// The pre-tokenizing patterns and special tokens of the named Tiktoken encodings. Neither is in a <c>.tiktoken</c> file, which holds
/// only the ranks. The patterns are the published ones (observed in the <c>Encoding</c> objects of tiktoken 0.14.0, the attributes
/// <c>_pat_str</c> and <c>_special_tokens</c> of the running object), with these changes so they run on the .NET regex engine, which
/// has no possessive quantifiers: <c>X?+</c>, <c>X++</c> and <c>X{1,3}+</c> become atomic groups <c>(?&gt;X?)</c>, <c>(?&gt;X+)</c> and
/// <c>(?&gt;X{1,3})</c> (the same matches, since a possessive quantifier is an atomic greedy one), and a final <c>$</c> becomes <c>\z</c>
/// because the Rust engine tiktoken runs on reads <c>$</c> as the end of the text only.
/// </summary>
internal static partial class TiktokenPatterns
{
    /// <summary>The GPT-2 pattern: <c>r50k_base</c> and <c>p50k_base</c>.</summary>
    [GeneratedRegex(@"'(?:[sdmt]|ll|ve|re)| ?(?>\p{L}+)| ?(?>\p{N}+)| ?(?>[^\s\p{L}\p{N}]+)|(?>\s+)\z|\s+(?!\S)|\s", RegexOptions.CultureInvariant)]
    internal static partial Regex Gpt2();

    /// <summary>The <c>cl100k_base</c> pattern.</summary>
    [GeneratedRegex(@"'(?i:[sdmt]|ll|ve|re)|(?>[^\r\n\p{L}\p{N}]?)(?>\p{L}+)|(?>\p{N}{1,3})| ?(?>[^\s\p{L}\p{N}]+)(?>[\r\n]*)|(?>\s+)\z|\s*[\r\n]|\s+(?!\S)|\s", RegexOptions.CultureInvariant)]
    internal static partial Regex Cl100k();

    /// <summary>The <c>o200k_base</c> pattern.</summary>
    [GeneratedRegex(@"[^\r\n\p{L}\p{N}]?[\p{Lu}\p{Lt}\p{Lm}\p{Lo}\p{M}]*[\p{Ll}\p{Lm}\p{Lo}\p{M}]+(?i:'s|'t|'re|'ve|'m|'ll|'d)?|[^\r\n\p{L}\p{N}]?[\p{Lu}\p{Lt}\p{Lm}\p{Lo}\p{M}]+[\p{Ll}\p{Lm}\p{Lo}\p{M}]*(?i:'s|'t|'re|'ve|'m|'ll|'d)?|\p{N}{1,3}| ?[^\s\p{L}\p{N}]+[\r\n/]*|\s*[\r\n]+|\s+(?!\S)|\s+", RegexOptions.CultureInvariant)]
    internal static partial Regex O200k();
}
