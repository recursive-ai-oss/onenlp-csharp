# Provenance: RecursiveAI.OneNlp.Models.Embeddings

This package redistributes a third-party model. **Nothing in it was trained by Recursive AI.**

| Item | Value |
|---|---|
| Model | `BAAI/bge-small-en-v1.5`, <https://huggingface.co/BAAI/bge-small-en-v1.5> |
| Revision | `5c38ec7c405ec4b44b94cc5a9bb96e735b38267a` |
| Weights licence | MIT (model card metadata `license: mit`; the card's License section: "FlagEmbedding is licensed under the MIT License. The released models can be used for commercial purposes free of charge.") |
| Authors | Beijing Academy of Artificial Intelligence (BAAI); Xiao et al., "C-Pack: Packaged Resources To Advance General Chinese Embedding", arXiv 2309.07597 |
| Training data (stated by the authors) | Large-scale text-pair pre-training followed by contrastive fine-tuning on labelled retrieval data, described in the technical report above. The authors' own statement governs; this package makes no claim beyond it. |
| What we did | Exported the published PyTorch weights to ONNX (opset 17, float32) with `tools/accurate/export_onnx.py`; no weight was changed. `tokenizer.json`, `modules.json`, `1_Pooling/config.json` (as `pooling.json`) and `sentence_bert_config.json` are the published files, unchanged. |
| Parity | Our embeddings equal sentence-transformers' on 2,103 texts (all scripts of the parity corpus, 62 of them longer than the 512-token limit): identical token ids, maximum absolute difference 4.0e-07, minimum cosine 0.99999988. |

Sizes and SHA-256 of the packed files are in `weights.manifest.props`. A pack fails when a file differs.
