# Packed open-type entity evaluation

This consumer references NuGet packages, not the source projects. It links the
open-type recipe's scorer: identical sentence filtering, type inventories, strict
span/type matching and bootstrap. `MultilingualModel.LoadOpenEntities()` loads the
embedded head and frozen encoder. Before loading, the consumer checks the size
and SHA-256 of the embedded head, tokenizer and frozen encoder against the source
weight manifests. It never loads a loose weights file.

Set `ONENLP_CONSUMER_FEED` to a folder feed containing the packages and
`ONENLP_ARTIFACTS` to an external build-output directory. From the repository root:

```sh
dotnet restore tools/OneNlp.OpenNerGate
dotnet build tools/OneNlp.OpenNerGate --no-restore -m:4
# DATA holds the JSONL files fetched by tools/openner/fetch.py and convert_uner.py.
# BIN is the directory containing RecursiveAI.OneNlp.OpenNerGate.dll.
for row in uner-en-ewt few-nerd crossner-politics crossner-science crossner-music crossner-literature crossner-ai; do
  dotnet "$BIN/RecursiveAI.OneNlp.OpenNerGate.dll" eval \
    --manifest-root "$PWD" --data "$DATA/$row-test.jsonl" \
    --threshold 0.5 --bootstrap 1000 --threads 4 --pred "$OUTPUT/$row.pred.tsv"
done
```

The model package defaults to `0.3.0-preview.1` (`OpenNerModelVersion` overrides it
at restore/build); core and Neural use the repository version. The recorded run
used core and Neural `0.4.0-preview.1` and the previously packed multilingual model
`0.3.0-preview.1`: the newer scratch feed did not contain a multilingual package.
This is **not** a verification of a newly packed multilingual `0.4.0-preview.1`.
The actual embedded weights match the current manifests byte for byte. See
[the protocol and results](../../docs/open-ner.md#published-comparison) for the
limits of the published comparison.
