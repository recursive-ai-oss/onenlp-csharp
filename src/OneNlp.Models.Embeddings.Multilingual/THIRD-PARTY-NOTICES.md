# Third-party notices: RecursiveAI.OneNlp.Models.Embeddings.Multilingual (and its Shard packages)

The code of these packages is licensed `MIT OR Apache-2.0`, Copyright (c) Recursive AI, LLC. They depend on `RecursiveAI.OneNlp.Neural` and, through it, on `RecursiveAI.OneNlp`, `RecursiveAI.OneModel`, `RecursiveAI.OneModel.Training` and `RecursiveAI.OneFont`, which carry their own notices.

## Model weights

The embedded model is **multilingual-e5-small** by Liang Wang, Nan Yang, Xiaolong Huang, Linjun Yang, Rangan Majumder and Furu Wei, released under the **MIT License** (model card metadata `license: mit`, <https://huggingface.co/intfloat/multilingual-e5-small>). The weights are redistributed converted to ONNX without change to their values.

```
MIT License

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
```

Wang, Yang, Huang, Yang, Majumder, Wei, "Multilingual E5 Text Embeddings: A Technical Report", arXiv:2402.05672.

The tokenizer (`tokenizer.json`) is the XLM-RoBERTa SentencePiece vocabulary shipped with the model, under the same licence.
