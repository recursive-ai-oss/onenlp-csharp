# Reads like

One sample per layer, each next to the library or API it will feel familiar from. The C# is the code in [`samples/OneNlp.Samples`](../samples/OneNlp.Samples), which builds and runs (`dotnet run --project samples/OneNlp.Samples -- all <path to a tokenizer.json>`); the other side is that library's own documented usage. The samples need the English model package, the embeddings package and the language-ID weights, as an application that references the packages would.

Three layers, three shapes:

- **Happy path (`Nlp`).** Static task verbs with no options objects, like Apple's NaturalLanguage framework: ask a question of a string, get an answer. The language is detected for you and the model comes from the packages you reference.
- **Low level (`Pipeline`, `Doc`, `Matcher`, `Train`).** spaCy's documented shape and terms: a pipeline of components over a `Doc` of tokens, spans and attributes, with rule matchers and training.
- **Tokenizers (`Tokenizer`).** The .NET span style: your buffers, an `OperationStatus`, nothing allocated; and the Hugging Face `Encoding` fields when you want them.

## Happy path: Apple NaturalLanguage and `Nlp`

Apple's side (Swift):

```swift
import NaturalLanguage

let text = "Angela Merkel met Emmanuel Macron in Paris on 5 March 2026."

let recognizer = NLLanguageRecognizer()
recognizer.processString(text)
let language = recognizer.dominantLanguage                 // .english

let words = NLTokenizer(unit: .word)
words.string = text
words.enumerateTokens(in: text.startIndex..<text.endIndex) { range, _ in
    print(text[range]); return true
}

let tagger = NLTagger(tagSchemes: [.nameType])
tagger.string = text
tagger.enumerateTags(in: text.startIndex..<text.endIndex, unit: .word, scheme: .nameType,
                     options: [.omitWhitespace, .omitPunctuation, .joinNames]) { tag, range in
    if let tag, tag != .otherWord { print(text[range], tag) }
    return true
}

let vector = NLEmbedding.sentenceEmbedding(for: .english)?.vector(for: text)
```

OneNLP:

```csharp
string text = "Angela Merkel met Emmanuel Macron in Paris on 5 March 2026. The deal cost $5m.";

Language language = Nlp.DetectLanguage(text);                    // Language.English
IReadOnlyList<Span> sentences = Nlp.Sentences(text);             // sentence spans, offsets into text
Token[] words = Nlp.Tokens(text);                                // word tokens
string[] lemmas = Nlp.Lemmas("The cats were running.");          // "the", "cat", "be", "run", "."
IReadOnlyList<Span> entities = Nlp.Entities(text);               // "Angela Merkel" PER, "Paris" LOC, ...

DateTimeOffset today = new(2026, 3, 18, 10, 30, 0, TimeSpan.Zero);
DateRange? when = Nlp.ParseDate("the first Monday after Easter 2027", today);
IEnumerable<Quantity> money = Nlp.Quantities(text);              // 5000000 USD
```

```csharp
float[] question = Nlp.Embed("how do I reset my password");        // 384 floats, length 1 (normalized)
float[] related = Nlp.Embed("I forgot my login credentials");
float[] unrelated = Nlp.Embed("the best way to bake sourdough bread");

static float Dot(float[] a, float[] b) => a.Zip(b, (x, y) => x * y).Sum();
```

- `Nlp.Analyze(text)` returns a `Doc` with everything at once: tokens, sentences, tags, lemmas, the dependency parse and the entities.
- Entity labels are strings (`"PER"`, `"ORG"`, `"LOC"`, `"MISC"`); `EntityLabels` has the names as constants.
- Relative dates need a reference you pass in. There is no hidden clock, and time zones are explicit.
- A verb that needs a model you did not add throws an exception that names the package ([model-packages.md](model-packages.md)); nothing is downloaded.
- What the verbs do not do (yet): Apple's `NLTagger` also gives lexical classes and sentiment; the equivalents here are `Token.Pos` on the analyzed `Doc` and, for classification, `TextClassifier`.

## Low level: spaCy and `Pipeline`

spaCy's side (Python):

```python
import spacy
from spacy.matcher import Matcher

nlp = spacy.blank("en")
ruler = nlp.add_pipe("entity_ruler", config={"overwrite_ents": True})
ruler.add_patterns([
    {"label": "CASE_NO", "pattern": [{"SHAPE": "dd-dddd"}]},
    {"label": "ORG", "pattern": "Recursive AI"},
])

doc = nlp("Recursive AI filed case 21-1234 on Monday.")
for ent in doc.ents:
    print(ent.label_, ent.text, ent.start_char, ent.end_char)

matcher = Matcher(nlp.vocab)
matcher.add("GREETING", [[{"LOWER": "hello"}, {"IS_PUNCT": True, "OP": "?"}, {"LOWER": "world"}]])
matches = matcher(nlp("Hello, world! hello world"))

for d in nlp.pipe(["First text.", "Second text."]):
    print(len(list(d.sents)))
```

OneNLP:

```csharp
// A blank English pipeline: the rule tokenizer, then the components in the order they are added.
using Pipeline nlp = Pipeline.Blank(Language.English);

// Rule entities, from token patterns (the vocabulary of spaCy's rule-based matching) or from phrases.
EntityRuler ruler = new() { OverwriteEnts = true };
ruler.Add("CASE_NO", Pattern.Shape("dd-dddd"));
ruler.AddPhrase("ORG", "Recursive AI");
nlp.Add(ruler);

Doc doc = nlp.Process("Recursive AI filed case 21-1234 on Monday.");
foreach (Span e in doc.Ents)
{
    Console.WriteLine($"{e.Label} {e} {e.StartChar}..{e.EndChar}");
}

// The Matcher finds token patterns in any Doc.
Matcher matcher = new();
matcher.Add("GREETING", Pattern.Token(new TokenSpec().Lower("hello"), new TokenSpec().Is(TokenProperty.IsPunct, true).Optional(), new TokenSpec().Lower("world")));
IReadOnlyList<PatternMatch> matches = matcher.Match(nlp.Process("Hello, world! hello world"));

// Many texts at once, lazily, in batches.
foreach (Doc d in nlp.ProcessMany(["First text.", "Second text."]))
{
    Console.WriteLine(d.Sentences.Count);
}
```

Training uses the same shape as spaCy's `nlp.update` loop, wrapped as one call ([training.md](training.md)):

```csharp
TrainResult result = nlp.Train(examples, new TrainOptions { Epochs = 30, Seed = 7, Dropout = 0f, DevExamples = examples });
PipelineFile.Save(nlp, path);                                  // one file: tokenizer, encoder, heads, labels
```

How the terms map:

| spaCy | OneNLP | Note |
|---|---|---|
| `spacy.blank("en")` | `Pipeline.Blank(Language.English)` | rule tokenizer; add components with `Add` |
| `spacy.load("en_core_web_sm")` | reference `RecursiveAI.OneNlp.Models.English`; `Nlp.Analyze`, or `PipelineFile.Load(path)` for a file | models are NuGet packages, never downloaded |
| `nlp(text)` / `nlp.pipe(texts)` | `nlp.Process(text)` / `nlp.ProcessMany(texts)` (and `ProcessManyAsync`) | synchronous; batches are read lazily |
| `doc.ents`, `ent.label_`, `ent.start_char` | `doc.Ents`, `span.Label`, `span.StartChar` | offsets are UTF-16 units in the string you passed |
| `token.pos_`, `token.tag_`, `token.lemma_`, `token.dep_`, `token.head` | `token.Pos`, `token.Tag`, `token.Lemma`, `token.Dep`, `token.Head` | `Token` and `Span` are struct views over the `Doc` |
| `Matcher`, `PhraseMatcher`, `entity_ruler` | `Matcher`, `PhraseMatcher`, `EntityRuler` | the pattern vocabulary is spaCy's ([matching.md](matching.md)) |
| `doc.cats`, `textcat`, `spancat` | `doc.Cats`, `TextClassifier`, `SpanCategorizer` | |
| `spacy train` with a config file | `nlp.Train(examples, new TrainOptions {...})` and the recipes in `recipes/` | |

## Tokenizers: Hugging Face `tokenizers` and `Tokenizer`

Hugging Face `tokenizers` (Python):

```python
from tokenizers import Tokenizer

tok = Tokenizer.from_file("tokenizer.json")
enc = tok.encode("Hello, world! Tokenizers count in UTF-16 units.")
for i, t in enumerate(enc.tokens):
    print(enc.ids[i], t, enc.offsets[i])

count = len(tok.encode("How long is this text, in tokens?").ids)
text = tok.decode(enc.ids)                       # skip_special_tokens=True by default
```

OneNLP:

```csharp
Tokenizer tok = Tokenizer.FromFile(tokenizerJson);                  // tokenizer.json, SentencePiece .model, vocab.txt, .tiktoken

// Ids, tokens and character offsets into the input, as the Hugging Face Encoding has them.
TokenEncoding enc = tok.Encode("Hello, world! Tokenizers count in UTF-16 units.");
for (int i = 0; i < enc.Length; i++)
{
    Console.WriteLine($"{enc.Ids[i],6} {enc.Tokens[i],-12} {enc.Offsets[i].Start}..{enc.Offsets[i].End}");
}

// The span form: ids go into your buffer, nothing is allocated, and the status says whether it all fit.
int[] buffer = new int[8];
OperationStatus status = tok.EncodeToIds("Hello, world! Tokenizers count in UTF-16 units.", buffer, out int written, out int consumed);

int count = tok.CountTokens("How long is this text, in tokens?");
string back = tok.Decode(enc.Ids);                                  // special tokens skipped
ReadOnlySpan<char> budget = tok.TrimToTokenBudget("one two three four five six seven eight", 4);
```

- The ids, tokens, offsets, special-token mask, type ids, attention mask and word ids equal the Python library's, byte for byte, on every family in [parity.md](parity.md). Offsets count UTF-16 units for `char` input and UTF-8 bytes for byte input; special tokens carry `(0, 0)`.
- `EncodeToIds` writes into your buffer and returns `Done` or `DestinationTooSmall`; after `DestinationTooSmall` the ids written are an exact prefix of the full encoding.
- `Truncation` and `Padding` are properties of the tokenizer (loaded from the file's own settings) and apply to `Encode` and `EncodeBatch`; `EncodeToIds`, `CountTokens` and `TrimToTokenBudget` ignore them.
- `TrimToTokenBudget` returns the slice of your text covered by the first (or last) *n* tokens, for fitting text to a model's context.
