# Model packages

A model package carries trained weights for one language (or tier) and makes the `Nlp` verbs use them. The first is `RecursiveAI.OneNlp.Models.English` (fast tier). Nothing is ever downloaded: the weights are inside the package.

## What the user does

Add the package. That is all.

```csharp
Doc doc = Nlp.Analyze(text);            // tokens, sentences, tags, morphology, lemmas, parse, entities
IReadOnlyList<Span> ents = Nlp.Entities(text);
```

Without a package for the text's language, `Nlp.Analyze` returns tokens and sentence boundaries only, and the model verbs (`Nlp.Entities`, `Nlp.Lemmas`) throw `InvalidOperationException`:

> No model is available for language 'en'. Add the NuGet package RecursiveAI.OneNlp.Models.English (fast tier), or RecursiveAI.OneNlp.Models.Multilingual; models are never downloaded.

Resolution order for a text: the fast-tier package of its language if referenced, otherwise the multilingual package once it exists. The language of a text is decided in one internal place (`Nlp.LanguageOf`); until language identification lands it is English.

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

One pipeline from one saved pipeline (`PipelineFile`), `english.onenlp`, trained by `recipes/en/joint`: a tagger (XPOS), morphologizer (UPOS, UFeats), lemmatizer, dependency parser (predicts the sentence starts) and entity recognizer (PER, ORG, LOC), all reading one shared fast encoder, so a text is encoded once.

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
