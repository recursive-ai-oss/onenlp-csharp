# RecursiveAI.OneNlp.Models.Multilingual.Large

**spaCy-class NLP for .NET.** One fine-tuned mmBERT-base encoder with UD heads for **84 languages** and entity heads for **19**. Tags, morphology, lemmas, dependencies and sentence starts are supplied where the training treebanks have those annotations. Japanese, Classical Chinese, Thai, Vietnamese and Chinese include trained word segmentation. No downloads and no native dependencies. Unaffiliated with other projects named OneNLP.

Reference this package only. Its 18 component dependencies keep every model package under 250 MB without reducing any tensor: six byte-preserving pieces of the encoder graph, the Chinese heads, and 11 additional language-head groups. Installed weights total about 3.5 GB. The shared encoder loads on first use; a language's heads load only when requested. The additional head groups are package assets copied to `weights/multilingual-large/heads/` in the build and publish directories; deploy the complete publish directory, not just the executable. The graph and original nine-language heads remain embedded. This layout supports NativeAOT without combining all weights into one resource image.

The plain `Nlp` verbs prefer a referenced fast-tier model for its language, otherwise this model. Explicit accurate-tier requests prefer this base-size package over the distilled multilingual package. The distilled package covers only the original nine languages; this package covers the 84 listed by `MultilingualLargeModel.Languages`. Language detection and UD coverage are separate: provide a language explicitly for text that the language detector does not cover.

```csharp
using RecursiveAI.OneNlp;
using RecursiveAI.OneNlp.Neural.Components;

using var nlp = Pipeline.Create(Language.FromCode("cs"), Tier.Accurate)
    .Add<Morphologizer>().Add<Lemmatizer>().Add<DependencyParser>();
Doc doc = nlp.Process("Praha je hlavní město České republiky.");
foreach (Token token in doc)
{
    Console.WriteLine($"{token}: {token.Pos}, {token.Lemma}, {token.Dep}");
}
```

These are development weights. Published-reference quality gaps, per language and metric, are reported in the repository's `docs/accurate-tier-rows.md`, not hidden behind a multilingual average. Training data, licences, hashes, recipe settings and evaluation roles are in `PROVENANCE.md`, `corpus.json`, the included manifests and `THIRD-PARTY-NOTICES.md`.
