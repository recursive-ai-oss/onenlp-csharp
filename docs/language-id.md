# Language identification

`LanguageDetector` (namespace `RecursiveAI.OneNlp.LanguageId`) names the language of a text from one word up to a document, among the 75 languages listed in `tools/fetch/langid/languages.tsv`. The model is one file of 3.6 MB that is embedded in the core assembly at build time. This page is normative for the method, the model file and the way it is measured.

## Behaviour

- `Detect(text)` returns the most likely language, or `Language.Unknown` when the text has no letter of a supported script. Digits, punctuation and white space are ignored.
- `Confidences(text)` returns every candidate with a probability; the values sum to 1. Only languages that use the text's script are candidates.
- `Restrict(languages)` returns a detector that never answers outside the set. A text whose script none of the allowed languages use is `Unknown`.
- `DetectSpans(text, switchPenalty = 9)` returns runs of one language that tile the text from index 0 to its length. See "Mixed text".
- The happy path is `Nlp.DetectLanguage(text)`. `Nlp.Analyze`, `Nlp.Tokens` and `Nlp.Sentences` detect the language first and use its segmentation data (the language's own exceptions and multiword-token rules where it has them, plain UAX #29 otherwise).
- A detector is immutable and thread-safe.

## Where the weights come from

The weights are not in git. `src/OneNlp/LanguageId/weights.manifest.props` lists the file with its size, sha256 and recipe. A build embeds it when the environment variable `ONENLP_LANGID_WEIGHTS` (or the property `LangIdWeightsDir`) names a directory holding `langid.bin`; the build fails if the file is missing or its hash differs from the manifest, and packing fails without it. A build without the variable still compiles and runs, but carries no model: `LanguageDetector.Default` throws `InvalidOperationException` naming the variable, `LanguageDetector.Load` still reads any model file, and the `Nlp` verbs treat every text as English. The recipe in `recipes/langid` rebuilds the file.

## Method

1. **Words.** The text is cut into runs of letters and combining marks (Unicode general categories L\* and M\*), lowercased with the invariant simple case mapping. A letter of a different script than the run so far ends the run. At most 65,536 letters of one text are used.
2. **Script and group.** Each word has a script (`Script.cs`: Latin, Cyrillic, Greek, Armenian, Hebrew, Arabic, Devanagari, Bengali, Gurmukhi, Gujarati, Tamil, Telugu, Thai, Georgian, Hangul, and one CJK group for Han and Kana). The model has one group per script, listing the languages written in it. A language belongs to every group whose script makes up at least 3% of its training letters (Serbian is in Cyrillic and Latin). The text goes to the group that holds most of its letters.
3. **Single-language scripts.** Greek, Armenian, Hebrew, Gujarati, Gurmukhi, Tamil, Telugu, Thai, Georgian, Bengali and Hangul each have one language in the list: the answer follows from the script, with confidence 1.
4. **Features.** For each word padded with a blank on each side, every character n-gram of length 1 to 5 (Cavnar and Trenkle 1994, §3), plus the whole word when it has at most 15 letters. Each feature is hashed to 32 bits and the top bits are the bucket (Weinberger et al. 2009). Group bucket counts: 2^16 for Latin, 2^14 for the other groups.
5. **Scores.** Per group, a table of signed 8-bit weights, `2^bits × classes`. The score of a class is `bias + scale × Σ weights / n^0.5`, with `n` the number of features. The probabilities are the softmax of `temperature × score` (temperature 1.218, fitted on held-out text). This is a linear softmax classifier as in Joulin et al. 2016, without the low-rank layer.
6. **Training** (`tools/OneNlp.LangId`): sparse AdaGrad (Duchi et al. 2011) on the cross-entropy loss. A sample is a random view of a training sentence: one word (22%), two adjacent words (28%), three to five words (15%) or the whole sentence (35%), so the model sees the lengths it is judged on. After training, the weights are quantized to int8 with a clip at the 99.95th percentile of magnitudes. The trainer and the detector call the same featurizer.

## Mixed text

`DetectSpans` scores every word on its own (the softmax of its group, floored at e^-12), then finds the language sequence that maximizes the sum of word log-probabilities minus `switchPenalty` for every change of language (Viterbi 1967). Words of an unsupported script say nothing. Runs begin at the first word of a new language, so the spaces and punctuation after a run belong to it. Confidence of a run is the probability of its language computed from the words of the run together.

The default penalty 9 was chosen on synthetic documents of two or three held-out sentences in different languages (2,000 documents, `recipes/langid/results/mixed.txt`):

| switch penalty | characters in the right language | documents with exactly the right runs |
|---|---|---|
| 2 | 92.35% | 31.80% |
| 4 | 95.96% | 65.85% |
| 6 | 97.11% | 82.00% |
| 9 | 97.53% | 89.30% |

## Model file

All integers little-endian. `"ONLI"` (4 bytes), version (1 byte, 1), language count (u16), per language a length byte (2 or 3) and the ASCII code, exponent (f32), temperature (f32), group count (u8), then per group: script mask (u32, bit per `Script` value), bucket bits (u8), class count (u8), the class language indexes (u16 each), weight scale (f32), class biases (f32 each) and, when the group has more than one class, `2^bits × classes` signed bytes, row-major by bucket. A group with one class carries no weights. The reader checks every count before allocating, limits the weights to 96 MB, and throws `InvalidDataException` for anything else. `LanguageDetector.Load(Stream)` reads a model file; the shipped one is the embedded resource `langid.bin`.

## Data

Permissive and attribution data only; `recipes/langid/PROVENANCE.md` has the full log.

- Leipzig Corpora Collection downloads, offered under CC BY 4.0 (their usage page): up to three corpora per language (newest news, then Wikipedia text, then older news), 100,000 sentences each at most. One corpus per language is held out for evaluation and never used for training (68 languages). For the 7 languages that have too little other text (Maori, Shona, Southern Sotho, Tswana, Tsonga, Xhosa, Yoruba) the evaluation texts are a hash slice of the one corpus, marked `same-corpus` in `dedup-report.tsv`.
- Tatoeba, default licence CC BY 2.0 FR: at most 20,000 sentences per language for training; a 5% hash slice is a second evaluation set (64 languages).
- Southern Sotho, Tswana and Tsonga have no Leipzig corpus. Their training and held-out text is their Wikipedia text (CC BY-SA 4.0).
- Share-alike lineage: yes. Some Leipzig corpora are Wikipedia text and the three Wikipedia dumps above are CC BY-SA 4.0; the provenance log lists them.

Sentences are identified by their letters only (lowercase, no digits, punctuation or spaces); a sentence whose identity occurs in an evaluation set is removed from training.

## Evaluation

Buckets follow the way Lingua's README describes its own: single words of at least 5 letters, word pairs of at least 10 characters, and whole sentences. For Chinese, Japanese and Thai, which have no spaces, a word is a run of 5 to 7 letters and a pair a run of 10 to 14. Up to 1,000 texts per bucket per language, drawn by a fixed seed from the held-out pool. The set is listed with SHA-256 in `recipes/langid/MANIFEST.sha256` and was written before any model was trained.

The score is the macro average of per-language accuracy (every language weighs the same). Intervals are percentile bootstrap (1,000 resamples, seed 20260101) over test texts; differences between systems are paired. The references are run only through their public APIs: `lingua-language-detector` 2.2.0 (Python, native core) and `SearchPioneer.Lingua` 2.0.0 (.NET), each in the high-accuracy and low-accuracy modes the README documents, built from the same 75 languages (`tools/oracles/Lingua`).

Results are in `recipes/langid/results/` (tables, per-language CSVs with paired intervals, and the confusions of every language below 90%). Summary on the held-out Leipzig texts (main) and the Tatoeba slice, macro accuracy %, difference to Lingua high accuracy with its 95% interval:

| set / bucket | ours | Lingua Python high | Lingua .NET high | ours − Python high |
|---|---|---|---|---|
| main / words | 77.14 | 75.59 | 75.35 | +1.56 [+1.31, +1.86] |
| main / pairs | 89.53 | 85.66 | 85.49 | +3.87 [+3.65, +4.08] |
| main / sentences | 98.03 | 95.98 | 95.81 | +2.05 [+1.95, +2.15] |
| Tatoeba / words | 79.14 | 75.03 | 74.79 | +4.10 [+3.39, +4.77] |
| Tatoeba / pairs | 91.14 | 85.79 | 85.75 | +5.35 [+4.66, +6.18] |
| Tatoeba / sentences | 96.15 | 93.27 | 93.19 | +2.88 [+2.55, +3.20] |

Caveats: the Leipzig held-out texts come from the same collection Lingua's README says its models were built from, so the main set may favour Lingua, not us. The Tatoeba slice is a different style (short, conversational) and our training includes other Tatoeba sentences, so it may favour us. Both are reported.

Languages where we are significantly below Lingua Python high on the main set (paired interval of the difference entirely below zero): words 14 (ar da ga hr ko lg mk nl ru so ur xh yo zh), pairs 8 (hr ja lg ms ru xh yo zh), sentences 2 (en hr). The closely related Latin-script pairs are the hard rows: Croatian/Bosnian, Indonesian/Malay, Norwegian Bokmål/Nynorsk/Danish.

## Speed and memory (indicative)

One thread, the 75,000 main sentences, a 32-thread host that also ran other jobs (`recipes/langid/results/timing-indicative.txt`): ours 1.0 s; SearchPioneer.Lingua high accuracy 53 s, low accuracy 63 s. Words: ours 0.38 s, Lingua .NET 7.6 s. Model load 12 ms, process peak RSS 127 MB (includes the .NET runtime and the test harness). Not a publishable run.
