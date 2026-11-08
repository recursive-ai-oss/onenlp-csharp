# RecursiveAI.OneNLP.Models.Multilingual.FrozenEncoder

**spaCy-class NLP for .NET.** The frozen distilled multilingual encoder of [RecursiveAI.OneNlp](https://www.nuget.org/packages/RecursiveAI.OneNlp): a six-layer, width-384 student of mmBERT-base (113 languages, 35.5 M weights, 142 MB as one ONNX file) and the model's `tokenizer.json`, embedded in the package. It maps the tokens of a `Doc` to 384-wide vectors, for your own heads or for `Train`. No downloads, no native code, NativeAOT and trim clean. Unaffiliated with other projects named OneNLP.

```csharp
using RecursiveAI.OneNlp;
using RecursiveAI.OneNlp.Models.Multilingual.FrozenEncoder;
using RecursiveAI.OneNlp.Neural.Encoders;

using TransformerEncoder encoder = FrozenStudentEncoder.Create();   // IEncoder, width 384
using Pipeline tokenizer = Pipeline.Blank(Language.German);
Doc doc = tokenizer.Process("Der Hund läuft schnell.");
EncodedBatch batch = encoder.Encode([doc]);                         // one 384-wide row per token
```

`FrozenStudentEncoder.Create` reads the embedded weights; keep one encoder and share it between threads. Use it wherever an `IEncoder` is taken, for example as the encoder of a head you train with `Train`. The raw files are available too: `Part.Open()` (the ONNX graph) and `Part.OpenTokenizer()`.

The student was distilled from `jhu-clsp/mmBERT-base` (MIT) on FineWeb-2 (ODC-By 1.0, subject to Common Crawl's terms) and Wikipedia text (CC BY-SA 3.0); the weights therefore carry share-alike lineage, credited in `THIRD-PARTY-NOTICES.md`. `PROVENANCE.md` lists every source, recipe and seed. The weights are licensed `MIT OR Apache-2.0`.

- Source and documentation: <https://github.com/recursive-ai-oss/onenlp-csharp>
- Home: <https://recursiveai.org/onenlp/>

Copyright (c) Recursive AI, LLC.
