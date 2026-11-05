#!/usr/bin/env python3
"""Writes an int8 copy of an exported encoder with ONNX Runtime's documented dynamic quantization
(onnxruntime.quantization.quantize_dynamic, int8 weights, per-tensor): dev tooling for the opt-in int8 mode.

    quantize_int8.py IN.onnx OUT.onnx
"""
import sys
from onnxruntime.quantization import quantize_dynamic, QuantType

quantize_dynamic(sys.argv[1], sys.argv[2], weight_type=QuantType.QInt8)
print("wrote", sys.argv[2])
