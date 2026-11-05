using System.Buffers;
using System.Globalization;
using System.Text;

namespace RecursiveAI.OneNlp.LanguageId;

/// <summary>
/// Splits a text into lowercase letter words and hashes each word's character n-grams. The trainer and the detector both call this,
/// so a feature means the same thing in both.
/// </summary>
/// <remarks>
/// Cavnar and Trenkle 1994 §3: n-grams of a word padded with a blank on each side, n from 1 to 5, so word starts and ends are
/// features of their own. A hashed whole-word feature is added for words up to <see cref="MaxWholeWord"/> letters, which gives
/// the frequent short words (articles, particles) their own weight. Hashing instead of a dictionary keeps the model a fixed-size
/// table: Weinberger et al. 2009, "Feature hashing for large scale multitask learning".
/// </remarks>
internal sealed class LangIdText
{
    /// <summary>Longest n-gram length.</summary>
    public const int MaxN = 5;

    /// <summary>Words up to this many letters also get a whole-word feature.</summary>
    public const int MaxWholeWord = 15;

    /// <summary>Letters kept from one text; longer texts are cut here so a hostile input cannot cost more than this.</summary>
    public const int MaxLetters = 1 << 16;

    private int[] _cps = new int[256];
    private int[] _hashes = new int[4096];

    /// <summary>Number of words found by the last <see cref="Run"/>.</summary>
    public int WordCount { get; private set; }

    /// <summary>Per word: the index of its first character in the input text.</summary>
    public int[] CharStart { get; private set; } = new int[32];

    /// <summary>Per word: the index just after its last character in the input text.</summary>
    public int[] CharEnd { get; private set; } = new int[32];

    /// <summary>Per word: its script.</summary>
    public Script[] WordScript { get; private set; } = new Script[32];

    /// <summary>Per word: index of its first hash in <see cref="Hashes"/>; word <c>w</c> owns <c>HashStart[w]</c> up to <c>HashStart[w + 1]</c>.</summary>
    public int[] HashStart { get; private set; } = new int[33];

    /// <summary>The 32-bit feature hashes of all words, in word order.</summary>
    public int[] Hashes => _hashes;

    /// <summary>Total letters kept by the last run.</summary>
    public int LetterCount { get; private set; }

    /// <summary>Splits and hashes <paramref name="text"/>.</summary>
    /// <param name="text">Any text. Everything that is not a letter or a combining mark separates words; digits and punctuation are dropped.</param>
    public void Run(ReadOnlySpan<char> text)
    {
        WordCount = 0;
        LetterCount = 0;
        int cpCount = 0;
        int wordCpStart = 0;
        int wordCharStart = -1;
        Script wordScript = Script.None;
        int hashCount = 0;
        int charPos = 0;
        while (charPos < text.Length && LetterCount < MaxLetters)
        {
            int consumed = 1;
            Rune rune = Rune.ReplacementChar;
            bool valid = Rune.DecodeFromUtf16(text[charPos..], out Rune decoded, out int used) == OperationStatus.Done;
            if (valid)
            {
                rune = decoded;
                consumed = used;
            }

            UnicodeCategory cat = Rune.GetUnicodeCategory(rune);
            bool mark = cat is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;
            bool letter = valid && !mark && Rune.IsLetter(rune);
            bool inWord = cpCount > wordCpStart;
            if (letter || (mark && valid && inWord))
            {
                Script s = letter ? Scripts.Of(rune.Value) : Script.None;
                // A letter of another script than the word so far ends the word ("ПриветHello" is two words).
                if (letter && inWord && s != Script.Other && wordScript != Script.Other && s != wordScript)
                {
                    hashCount = EndWord(wordCpStart, cpCount, wordCharStart, charPos, wordScript, hashCount);
                    wordCpStart = cpCount;
                    wordCharStart = -1;
                    wordScript = Script.None;
                }

                if (wordCharStart < 0)
                {
                    wordCharStart = charPos;
                }

                if (letter)
                {
                    if (wordScript is Script.None or Script.Other)
                    {
                        wordScript = s;
                    }

                    LetterCount++;
                }

                if (cpCount == _cps.Length)
                {
                    Array.Resize(ref _cps, cpCount * 2);
                }

                _cps[cpCount++] = Rune.ToLowerInvariant(rune).Value;
            }
            else if (inWord)
            {
                hashCount = EndWord(wordCpStart, cpCount, wordCharStart, charPos, wordScript, hashCount);
                wordCpStart = cpCount;
                wordCharStart = -1;
                wordScript = Script.None;
            }

            charPos += consumed;
        }

        if (cpCount > wordCpStart)
        {
            hashCount = EndWord(wordCpStart, cpCount, wordCharStart, charPos, wordScript, hashCount);
        }

        HashStart[WordCount] = hashCount;
    }

    private int EndWord(int cpStart, int cpEnd, int charStart, int charEnd, Script script, int hashCount)
    {
        if (WordCount + 2 > CharStart.Length)
        {
            int n = CharStart.Length * 2;
            CharStart = Grow(CharStart, n);
            CharEnd = Grow(CharEnd, n);
            HashStart = Grow(HashStart, n + 1);
            Script[] ws = WordScript;
            Array.Resize(ref ws, n);
            WordScript = ws;
        }

        int w = WordCount++;
        CharStart[w] = charStart;
        CharEnd[w] = charEnd;
        WordScript[w] = script;
        HashStart[w] = hashCount;
        int len = cpEnd - cpStart;
        // Room for a padded word's n-grams: (len + 2) positions times MaxN lengths, plus the whole-word feature.
        int need = hashCount + ((len + 2) * MaxN) + 1;
        if (need > _hashes.Length)
        {
            Array.Resize(ref _hashes, Math.Max(need, _hashes.Length * 2));
        }

        return AddFeatures(_cps.AsSpan(cpStart, len), _hashes, hashCount);
    }

    private static int[] Grow(int[] a, int n)
    {
        Array.Resize(ref a, n);
        return a;
    }

    /// <summary>Writes the feature hashes of one lowercase word (code points) into <paramref name="dest"/> at <paramref name="at"/>.</summary>
    /// <returns>The index after the last hash written.</returns>
    internal static int AddFeatures(ReadOnlySpan<int> word, int[] dest, int at)
    {
        int padded = word.Length + 2;
        for (int i = 0; i < padded; i++)
        {
            uint h = 0;
            for (int n = 1; n <= MaxN && i + n <= padded; n++)
            {
                int k = i + n - 1;
                int cp = k == 0 || k == padded - 1 ? ' ' : word[k - 1];
                h = (h ^ (uint)cp) * 16777619u;
                if (n == 1 && cp == ' ')
                {
                    // A lone blank carries no information; keep the chain going for the longer n-grams.
                    continue;
                }

                dest[at++] = Finish(h, n);
            }
        }

        if (word.Length <= MaxWholeWord)
        {
            uint h = 0;
            foreach (int cp in word)
            {
                h = (h ^ (uint)cp) * 16777619u;
            }

            dest[at++] = Finish(h, MaxN + 1);
        }

        return at;
    }

    // Mixes the length into the hash and spreads the bits so the top bits (used as the bucket) are well distributed.
    private static int Finish(uint h, int n)
    {
        h ^= (uint)n * 0x9E3779B1u;
        h ^= h >> 16;
        h *= 0x85EBCA6Bu;
        h ^= h >> 13;
        h *= 0xC2B2AE35u;
        h ^= h >> 16;
        return (int)h;
    }
}
