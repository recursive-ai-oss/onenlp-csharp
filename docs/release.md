# Release weights and packages

Weights never enter git. Each `weights.manifest.props` records file sizes and
SHA-256 digests; these committed values are the trust anchor, not the downloaded
archive checksums. Runtime code never downloads anything.

## Prepare the weight assets

Use Python 3.12 or newer and directories outside the checkout. The search roots
must contain the trained/exported files named by the manifests, including the
core language-ID model, every model package, and nested accurate-tier parts.

```sh
python3 tools/gate/stage_weights.py "$STAGED" "$MODEL_ROOT" "$DISTILLED_ROOT" > "$ENV_FILE"
python3 tools/gate/weight_assets.py create "$STAGED" "$ASSET_DIR"
python3 tools/gate/weight_assets.py list
```

`STAGED` holds symlinks, so the search roots must remain in place until asset
creation finishes. `ASSET_DIR` must not exist yet. Each archive contains the
regular file bytes of one manifest (not symlinks). It must be strictly smaller
than GitHub's 2 GiB asset limit; an oversized manifest fails rather than silently
splitting or omitting files. Archive headers are deterministic. The current set
has 39 archives and `SHA256SUMS`:

```text
weights-LangId.tar
weights-Chinese.tar
weights-Dutch.tar
weights-Embeddings.tar
weights-Embeddings.Multilingual.tar
weights-Embeddings.Multilingual.Shard1.tar
weights-Embeddings.Multilingual.Shard2.tar
weights-Embeddings.Multilingual.Shard3.tar
weights-English.tar
weights-French.tar
weights-German.tar
weights-Hindi.tar
weights-Italian.tar
weights-Multilingual.tar
weights-Multilingual.Large.tar
weights-Multilingual.Large.Chinese.tar
weights-Multilingual.Large.Encoder1.tar
weights-Multilingual.Large.Encoder2.tar
weights-Multilingual.Large.Heads1.tar
weights-Multilingual.Large.Heads10.tar
weights-Multilingual.Large.Heads11.tar
weights-Multilingual.Large.Heads2.tar
weights-Multilingual.Large.Heads3.tar
weights-Multilingual.Large.Heads4.tar
weights-Multilingual.Large.Heads5.tar
weights-Multilingual.Large.Heads6.tar
weights-Multilingual.Large.Heads7.tar
weights-Multilingual.Large.Heads8.tar
weights-Multilingual.Large.Heads9.tar
weights-Multilingual.Large.TokenTable1.tar
weights-Multilingual.Large.TokenTable2.tar
weights-Multilingual.Large.TokenTable3.tar
weights-Multilingual.Large.TokenTable4.tar
weights-Multilingual.Encoder.tar
weights-Multilingual.FrozenEncoder.tar
weights-Multilingual.Segmenters.tar
weights-Portuguese.tar
weights-Spanish.tar
weights-Turkish.tar
SHA256SUMS
```

Only a release maintainer performs the following upload, after approving the
weights and selecting the existing source commit. The weights release is separate
from version releases; its tag must **not** start with `v` (which triggers package
publication). Use a new tag, never replace an existing asset set.

```sh
REPO=recursive-ai-oss/onenlp-csharp
WEIGHTS_TAG=weights-1.0.0
# SOURCE_COMMIT is the full reviewed source commit hash, not a branch name.
gh release create "$WEIGHTS_TAG" "$ASSET_DIR"/weights-*.tar "$ASSET_DIR/SHA256SUMS" \
  --repo "$REPO" --target "$SOURCE_COMMIT" --draft --latest=false \
  --title "OneNLP 1.0.0 model weights" \
  --notes "Model weights; file sizes and SHA-256 digests are pinned in the source manifests."
```

Review the asset list and checksums before publishing that draft. A read-only
workflow token cannot download another draft's assets, so publish the weights
release before creating the package version tag. Set the repository Actions
variable `ONENLP_WEIGHTS_RELEASE` to that weights tag. This variable is required;
there is no fallback to the latest release. Creating or publishing releases is
not part of the preparation scripts.

## Verify and pack without uploading

```sh
# Start with an empty download directory. GH_TOKEN needs contents:read only.
mkdir -p "$DOWNLOAD_DIR"
while IFS= read -r asset; do
  gh release download "$WEIGHTS_TAG" --repo "$REPO" --pattern "$asset" --dir "$DOWNLOAD_DIR"
done < <(python3 tools/gate/weight_assets.py list)
python3 tools/gate/weight_assets.py unpack "$DOWNLOAD_DIR" "$VERIFIED" > "$ENV_FILE"
source "$ENV_FILE"
python3 tools/gate/stage_weights.py "$VERIFIED" --verify
rm -r "$DOWNLOAD_DIR"
dotnet pack OneNlp.slnx -c Release -o "$PACKAGES_DIR"
```

Unpacking requires a new output directory. It rejects missing or extra assets,
checksum mismatches, unexpected/duplicate tar members, links, traversal, wrong
sizes and file-content mismatches. Files are hashed while writing, then checked
against every manifest. Environment exports are produced only after all files
pass. `--github-env FILE` appends those same variables for subsequent Actions
steps. Shared parts use one root directory per weights variable:
`ONENLP_MULTILINGUAL_WEIGHTS`, `ONENLP_MLLARGE_WEIGHTS` and
`ONENLP_EMBEDDINGS_E5_WEIGHTS`; nested paths such as `heads/en.onenlp` are preserved.
The language-ID and ten fast-tier variables are also set explicitly.

`release.yml` downloads that exact list with the job's read-only `GITHUB_TOKEN`,
verifies and stages it **before** restore/build/test/pack, and removes the archives
to save disk. Existing pinned actions and publishing permissions are unchanged.
Missing weights or any mismatched file stop the job before publication. Model
projects also retain their own hash checks and reject packing without weights.
Allow at least 35 GiB of free disk, in addition to SDK and package caches, for a
full solution build and pack. Weights alone total about 5.5 GiB; dependent tools
copy model assemblies and assets into their own outputs. A disk-bounded pack
check can pack one project at a time and remove its packages after recording
their hashes, but publication needs to retain the complete package set. After verification, delete scratch archives, staged
weights, package outputs and build artifacts that are no longer needed.
