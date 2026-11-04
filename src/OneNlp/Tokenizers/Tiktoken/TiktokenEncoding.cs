namespace RecursiveAI.OneNlp.Tokenizers;

/// <summary>The Tiktoken encodings whose pre-tokenizing pattern and special tokens the library knows.</summary>
public enum TiktokenEncoding
{
    /// <summary><c>r50k_base</c> (GPT-2): 50,256 ranks, special token <c>&lt;|endoftext|&gt;</c> = 50256.</summary>
    R50kBase,

    /// <summary><c>p50k_base</c>: the GPT-2 pattern with a larger rank table (50,280 ranks); special token <c>&lt;|endoftext|&gt;</c> = 50256.</summary>
    P50kBase,

    /// <summary><c>cl100k_base</c>: 100,256 ranks; special tokens <c>&lt;|endoftext|&gt;</c> 100257, <c>&lt;|fim_prefix|&gt;</c> 100258, <c>&lt;|fim_middle|&gt;</c> 100259, <c>&lt;|fim_suffix|&gt;</c> 100260 and <c>&lt;|endofprompt|&gt;</c> 100276.</summary>
    Cl100kBase,

    /// <summary><c>o200k_base</c>: 199,998 ranks; special tokens <c>&lt;|endoftext|&gt;</c> 199999 and <c>&lt;|endofprompt|&gt;</c> 200018.</summary>
    O200kBase,
}
