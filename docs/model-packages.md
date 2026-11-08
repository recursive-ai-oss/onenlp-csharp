# Model packages

A model package carries trained weights for one language (or one task) and makes the `Nlp` verbs use them. Nothing is ever downloaded: the weights are inside the package, embedded as resources.

## The packages

| Package | Language or task | Registration class | Used by |
|---|---|---|---|
| `RecursiveAI.OneNlp.Models.English` | English fast tier | `EnglishModel` | `Nlp.Analyze`, `Nlp.Entities`, `Nlp.Lemmas` for English text |
| `RecursiveAI.OneNlp.Models.French`, `.German`, `.Spanish`, `.Portuguese`, `.Dutch`, `.Italian`, `.Turkish`, `.Chinese` | the fast tier of that language | `FrenchModel`, `GermanModel`, `SpanishModel`, `PortugueseModel`, `DutchModel`, `ItalianModel`, `TurkishModel`, `ChineseModel` | the same verbs, for text of that language |
| `RecursiveAI.OneNlp.Models.Embeddings` | English sentence embeddings (bge-small-en-v1.5) | `EmbeddingsModel` | `Nlp.Embed`, `Nlp.EmbedMany` |
| `RecursiveAI.OneNlp.Models.Embeddings.Multilingual` | multilingual sentence embeddings (multilingual-e5-small); its three `...Shard1..3` packages hold the weights and come with it | `MultilingualEmbeddingsModel` | `Nlp.Embed`, `Nlp.EmbedMany` (wins over the English embedder when both are referenced) |
| `RecursiveAI.OneNlp.Models.Multilingual` | the distilled accurate tier for English, German, French, Spanish, Portuguese, Italian, Dutch, Turkish and Chinese, and open-type entities (`Nlp.Entities(text, types)`); its parts `.Encoder`, `.FrozenEncoder` and `.Segmenters` come with it (`distilled-tier.md`) | `MultilingualModel` | `Nlp.Analyze`, `Nlp.Entities`, `Nlp.Lemmas` where no fast-tier package is referenced |
| `RecursiveAI.OneNlp.Models.Multilingual.FrozenEncoder` | the frozen distilled encoder alone, for your own heads | `FrozenStudentEncoder.Create()` | an `IEncoder` |
| `RecursiveAI.OneNlp.Models.Multilingual.Large` | the base-size accurate tier (`accurate-tier.md`) | `MultilingualLargeModel` | as above, preferred over `.Multilingual` when both are referenced |

`RecursiveAI.OneNlp.Models.Hindi` (`HindiModel`) is the fast tier for Hindi: UPOS, features, parse and PER/ORG/LOC entities, with no real lemmas (`Token.Lemma` is the token text) and no XPOS. There is no allowed Hindi treebank (`ud-licences.md`), so it is trained on web text labelled by the accurate tier and by heads for related languages (`recipes/hi/fast/README.md`) and is markedly weaker than the other languages. Hindi text is also identified, tokenized, split into sentences and run through the Hindi recognizers without it.

## What the user does

Add the package. That is all.

```csharp
Doc doc = Nlp.Analyze(text);            // tokens, sentences, tags, morphology, lemmas, parse, entities
IReadOnlyList<Span> ents = Nlp.Entities(text);
```

The language of the text is detected with the built-in language identifier, and the package registered for that language is used. A text with no letters is treated as English.

Without a package for the text's language, `Nlp.Analyze` returns tokens and sentence boundaries only, and the model verbs (`Nlp.Entities`, `Nlp.Lemmas`) throw `InvalidOperationException`. The message names the exact package to add:

> No model is available for language 'de'. Add the NuGet package RecursiveAI.OneNlp.Models.German (or RecursiveAI.OneNlp.Models.Multilingual); models are never downloaded.

The same for embeddings, when no embedding package is referenced:

> No embedding model is available. Add the NuGet package RecursiveAI.OneNlp.Models.Embeddings; models are never downloaded.

Resolution order for a text: the fast-tier package of its language if referenced, otherwise an accurate-tier package that covers it: `RecursiveAI.OneNlp.Models.Multilingual.Large` if referenced, else `RecursiveAI.OneNlp.Models.Multilingual`. To use a particular package regardless of detection, load its pipeline (`EnglishModel.Load()`, ...) or any saved pipeline file (`PipelineFile.Load`) and call `Process` on it; the low level takes any file or stream, including models you trained yourself or fine-tuned from another source.

## How core reaches a pipeline that lives in a package

Core (`RecursiveAI.OneNlp`) knows nothing about `.Neural` or any model. It holds `ModelPackages`, a table from language to a registered factory (`Func<Pipeline>`). A model package registers its factory; the `Nlp` verbs look the language up.

The registration runs from a module initializer, and the module initializer lives in the **referencing project**, not in the package assembly:

- The package ships a C# file under `contentFiles/cs/any/` (build action `Compile`). NuGet adds it to every C# project that references the package, directly or through another package. It contains one `[ModuleInitializer]` that calls `EnglishModel.Register()`.
- Because the file is compiled into the application, the call is a normal, statically visible reference to the model assembly. Nothing has to load the assembly by name, scan assemblies or use reflection, and the trimmer and the NativeAOT compiler see the model code, its embedded weights and everything it calls as reachable. (A module initializer placed in the package assembly itself would not run in a JIT program that never touches a type from it, and could be trimmed away in one that does not reference it.)
- `Register` is idempotent, so several projects (an application and a library, both referencing the package) may add the file.
- A project that cannot compile the file (F#, Visual Basic) calls `EnglishModel.Register()` once at start-up.
- Registration stores a factory only. The weights are read on first use, once, under a lock, and the resulting pipeline is shared by all callers (components are safe for concurrent `Process`).

**Why this is AOT- and trim-safe:** there is no `Assembly.Load`, no `Type.GetType`, no `Activator`, no IL emission and no reflection-based discovery. The weights are `EmbeddedResource`s read with `Assembly.GetManifestResourceStream` of a type's own assembly, which NativeAOT supports; head kinds and components are created through explicit tables (`HeadRegistry`, `ComponentRegistry`). The consumer app in `tests/OneNlp.ModelConsumer` publishes with NativeAOT with no warnings and runs the verbs.

## The English pipeline

One pipeline from one saved pipeline (`PipelineFile`), `english.onenlp`, assembled by `recipes/en/assemble` from three separately trained models, each with its own encoder: tagging (tagger for XPOS, morphologizer for UPOS and UFeats, edit-tree lemmatizer; `recipes/en/fast-tagging`), the dependency parser, which also predicts the sentence starts (`recipes/en/parser`), and the entity recognizer for PER, ORG and LOC (`recipes/en/ner`). The rule sentencizer saved with the tagging model is left out, because the parser sets the sentence boundaries. The assembled file predicts what each part predicted alone (the scores are in [results.md](results.md)).

A single shared encoder for all heads (`recipes/en/joint`, 23.7 MB) is much faster and has weaker rows; it is documented in [results.md](results.md) as an option, and a user can load it with `PipelineFile.Load` like any saved pipeline.

The tokenizer is the UAX #29 word tokenizer with English data, shared with the string table; the pipeline owns and disposes every component.

## Package layout and the weights

```
src/OneNlp.Models.English/
  OneNlp.Models.English.csproj    references OneNlp.Neural only
  EnglishModel.cs                 Register(), Load()
  contentFiles/...Registration.cs the file compiled into referencing projects
  weights.manifest.props          name, size, sha256, recipe, data of each weights file
  PROVENANCE.md, THIRD-PARTY-NOTICES.md, PACKAGE.md
```

- Weights never enter git. Building or packing with the environment variable `ONENLP_ENGLISH_WEIGHTS` (or the property `EnglishWeightsDir`) set to a directory holding the files named in the manifest embeds them as resources `weights/<name>`.
- Every file is hashed (SHA-256) and compared with the manifest at build. A missing or different file fails the build with the file name and both hashes. Without the variable the project builds (for development) but `dotnet pack` fails, saying what to set.
- **Re-pack with retrained weights:** put the new files in the directory, change the `Size` and `Sha256` of their rows in `weights.manifest.props`, update `PROVENANCE.md` (datasets, recipe, seed), and pack. No code changes. 
- The package contains: the assembly (with the weights embedded), the manifest, `PROVENANCE.md`, `THIRD-PARTY-NOTICES.md`, `PACKAGE.md`, both licences and the registration file.

`tests/OneNlp.ModelConsumer/run.sh <weights-dir> <work-dir>` packs the core, neural and model packages into a throwaway folder feed, then builds, runs and NativeAOT-publishes a consumer app against that feed with and without the model package.

## Writing another model package

Same shape: a project referencing `RecursiveAI.OneNlp.Neural`, a manifest with hashes, a loader that builds a `Pipeline` from its resources, a `Register` that calls `ModelPackages.Register(language, packageId, factory)`, and the contentFiles registration. The package for language X names itself `RecursiveAI.OneNlp.Models.<EnglishNameOfX>`.

## Pipeline.Create, Add&lt;T&gt;() and tiers

`Pipeline.Create(language, tier)` makes a pipeline from the model package referenced for the language, with the package's tokenizer and no components. `.Add<T>()` then takes the package's trained component of type `T` (`Tagger`, `Morphologizer`, `Lemmatizer`, `DependencyParser`, `EntityRecognizer`, ...) and appends it; `.Add(component)` appends one you built (an `EntityRuler`). Components run in the order added. Components never added are released when the pipeline is disposed. The `Tagger` of the fast-tier packages sets fine-grained tags (`Token.Tag`); add the `Morphologizer` for `Pos` and morphology, which noun chunks need together with the parser.

- `Tier.Fast`: the language's fast-tier package, else `RecursiveAI.OneNlp.Models.Multilingual`.
- `Tier.Accurate`: `RecursiveAI.OneNlp.Models.Multilingual.Large` if referenced, else `RecursiveAI.OneNlp.Models.Multilingual`; never the fast model.
- With nothing referenced the call throws `InvalidOperationException` naming the package to add. Nothing is downloaded and nothing is found by reflection: packages register from a module initializer.
- Each `Create` reads the weights into a new copy owned by the pipeline; the `Nlp` verbs keep their own shared one.
- `Doc.NounChunks` returns the base noun phrases of a parsed document as spans; it throws for a document without a parse.
- `pipeline.Train(examples, epochs: 10)` needs only `using RecursiveAI.OneNlp;`.
