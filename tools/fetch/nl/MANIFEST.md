# Dutch data manifest

Fetched by `tools/fetch/nl/fetch.sh <data-root>`. UD release r2.18.

## Choice

- **Gating treebank: UD Dutch-Alpino** (CC BY-SA 4.0; 12,289 / 718 / 596 sentences, news). LassySmall (CC BY-SA 4.0, Wikipedia, 13,817 / 1,542 / 1,761 sentences) is allowed and not used; it is the second Dutch treebank and the natural second test set when a model is trained on both.
- **NER: WikiNER, Dutch wp3** (CC BY 4.0 per its figshare record; silver labels; PER, LOC, ORG, MISC). The file is ISO-8859-1; the fetch script decodes it as such. Same split rule as French. There is no allowed gold-annotated Dutch NER corpus (CoNLL-2002 has no open terms).

## Splits and how they are used

The official train, dev and test splits of every release are used as they come (the two corpora without an official split say how they were cut below). The test side is never read by training, tuning or model selection. Every file below was fetched, hashed and recorded before any model saw the training side. Re-running `fetch.sh` verifies the hashes of files already present and rewrites the manifest.

UD test splits are scored with the CoNLL 2018 definitions; entity corpora with entity-level micro F1. The per-treebank licence table for the language is in `docs/ud-licences.md`.

## Files

| Role | Dataset | File | Sentences | Tokens | Licence | URL | SHA-256 |
|---|---|---|---|---|---|---|---|
| train | ud-nl-alpino | `nl_alpino-ud-train.conllu` | 12,289 | 186,027 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalDependencies/UD_Dutch-Alpino/r2.18/nl_alpino-ud-train.conllu | `d6612a025ae77a8f58494458b987be6f39fdcaa40c94b5e2a5522e50c4166cfb` |
| dev | ud-nl-alpino | `nl_alpino-ud-dev.conllu` | 718 | 11,541 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalDependencies/UD_Dutch-Alpino/r2.18/nl_alpino-ud-dev.conllu | `0444fc2590d7f522b5f201473940a40f0f1790770fcb568450581f12bcbcffd9` |
| eval | ud-nl-alpino | `nl_alpino-ud-test.conllu` | 596 | 11,046 | CC BY-SA 4.0 | https://raw.githubusercontent.com/UniversalDependencies/UD_Dutch-Alpino/r2.18/nl_alpino-ud-test.conllu | `20d0f718cdc73b51dbb2744386c9d4ecb1a9df6a076efa81b4c008ace3801fc0` |
| train | wikiner-nl-wp3 | `wikiner-nl-wp3-train.iob2` | 163,725 | 3,181,206 | CC BY 4.0 (figshare record 5462500); silver labels projected from Wikipedia links | https://ndownloader.figshare.com/files/9446350 (converted by tools/fetch/fetch_lang.py) | `d8b5aec809b4ceddae16ad9facb93ec9892756f8bdaa3079c3815964c6328a7d` |
| dev | wikiner-nl-wp3 | `wikiner-nl-wp3-dev.iob2` | 1,645 | 31,924 | CC BY 4.0 (figshare record 5462500); silver labels projected from Wikipedia links | https://ndownloader.figshare.com/files/9446350 (converted by tools/fetch/fetch_lang.py) | `4328b8bb4a9d5a7edfdc5c6d3ed4d5536520dac12e3a944d4f3002b63a5750fe` |
| eval | wikiner-nl-wp3 | `wikiner-nl-wp3-test.iob2` | 8,358 | 160,261 | CC BY 4.0 (figshare record 5462500); silver labels projected from Wikipedia links | https://ndownloader.figshare.com/files/9446350 (converted by tools/fetch/fetch_lang.py) | `6c2bc736a5789664662d3dec98822dc06370a9cd3c4b5b0484c7b10ed83801a2` |

## Raw downloads converted by the script

| Dataset | Raw file | SHA-256 |
|---|---|---|
| wikiner-nl-wp3 | `wikiner-nl-wp3.bz2` | `f7850e91401677a4263814d9d6cf5a7409572450a54d800013663c3f476f1a5f` |

## Overlap between test text and training text

Text-level check on the `# text =` line (joined forms for corpora without one): identical text between an evaluation file and the train or dev files of the same family. Folded = whitespace collapsed and case folded.

| Test file | Against | Test sentences | Identical | Folded |
|---|---|---|---|---|
| `eval/ud/nl/nl_alpino-ud-test.conllu` | ud-nl-alpino | 596 | 0 | 0 |
| `eval/ud/nl/nl_alpino-ud-test.conllu` | wikiner-nl-wp3 (other family) | 596 | 0 | 0 |
| `eval/ner/nl/wikiner-nl-wp3/wikiner-nl-wp3-test.iob2` | wikiner-nl-wp3 | 8,358 | 35 | 37 |
| `eval/ner/nl/wikiner-nl-wp3/wikiner-nl-wp3-test.iob2` | ud-nl-alpino (other family) | 8,358 | 0 | 0 |

## Notices

| File | SHA-256 |
|---|---|
| `notices/nl/ud-nl-alpino/LICENSE.txt` | `df653272772ee86b0098bfc640b0ab583f6ecf59bb6254b82feaba3aed3ed93c` |
| `notices/nl/ud-nl-alpino/README.txt` | `cb20b8bc9cdc5d1ed80f1892f61cab3bba3462532791094e2e02247031130b75` |
