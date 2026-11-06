# Provenance: RecursiveAI.OneNlp.Models.Embeddings.Multilingual

This package (with its three Shard packages) redistributes a third-party model. **Nothing in it was trained by Recursive AI.**

| Item | Value |
|---|---|
| Model | `intfloat/multilingual-e5-small`, <https://huggingface.co/intfloat/multilingual-e5-small> |
| Revision | `614241f622f53c4eeff9890bdc4f31cfecc418b3` |
| Weights licence | MIT (model card metadata `license: mit`) |
| Authors | Liang Wang, Nan Yang, Xiaolong Huang, Linjun Yang, Rangan Majumder, Furu Wei, "Multilingual E5 Text Embeddings: A Technical Report", arXiv 2402.05672 (2024) |
| Training data (stated by the authors on the model card) | Stage 1, contrastive pre-training with weak supervision: filtered mC4, CC News, NLLB, Wikipedia, Reddit, S2ORC, Stackexchange, xP3 and other pairs, about 1 billion pairs in all. Stage 2, supervised fine-tuning on labelled sets (MS MARCO, NQ, TriviaQA, NLI, Mr. TyDi, MIRACL and others), training splits only. The card and report govern; this package makes no claim beyond them. |
| What we did | Exported the published PyTorch weights to ONNX (opset 17, float32) with `tools/accurate/export_onnx.py`; no weight was changed. The ONNX file (470,228,877 bytes, sha256 `44596df6...f23e78`) is cut into three byte ranges of 156,742,959 bytes (the last is the remainder) so each package stays under the size limit; the ranges are concatenated in memory at load. `tokenizer.json`, `modules.json`, `1_Pooling/config.json` (as `pooling.json`) and `sentence_bert_config.json` are the published files, unchanged. |
| Parity | Our embeddings equal sentence-transformers' on 2,103 texts (all scripts of the parity corpus, 62 over the 512-token limit), with the `"query: "` prefix: identical token ids, maximum absolute difference 3.2e-07, minimum cosine 0.99999988. Re-checked on the shards joined from the packages. |

Sizes and SHA-256 of the packed files are in each project's `weights.manifest.props`. A pack fails when a file differs.
