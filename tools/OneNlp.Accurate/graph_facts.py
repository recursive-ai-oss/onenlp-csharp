"""Prints the structural constants of an exported ModernBERT-family graph: rotary constants, the mask constants, the epsilon of the
layer norms, and which mask each layer's attention adds. Reads the file only. Usage: python graph_facts.py model.onnx"""
import sys

import numpy as np
import onnx
from onnx import numpy_helper

model = onnx.load(sys.argv[1], load_external_data=False)
nodes = {n.output[0]: n for n in model.graph.node}


def const(name):
    n = nodes.get(name)
    if n is not None and n.op_type == "Constant":
        return numpy_helper.to_array(n.attribute[0].t)
    return None


for n in model.graph.node:
    if "rotary" in n.name and n.op_type == "Constant":
        print(n.name, const(n.output[0]))
    if n.name.startswith("/model/Constant_") and n.op_type == "Constant":
        v = const(n.output[0])
        if v is not None and v.size <= 4:
            print(n.name, v.tolist())
eps = set()
for n in model.graph.node:
    if n.op_type == "LayerNormalization":
        for a in n.attribute:
            if a.name == "epsilon":
                eps.add(round(a.f, 12))
print("layernorm epsilons", eps)
for n in model.graph.node:
    if n.op_type == "Add" and n.name.endswith("/attn/Add_2"):
        print(n.name, "mask:", n.input[1])
for n in model.graph.node:
    if n.op_type == "Mul" and n.name.endswith("/attn/Mul_4"):
        print(n.name, "scale", const(n.input[1]))
        break
# rotary users: which cos/sin feed which layer
users = {}
for n in model.graph.node:
    if "/attn/" in n.name and n.op_type == "Mul":
        for i in n.input:
            if "rotary" in i:
                users.setdefault(n.name.split("/attn/")[0], set()).add(i)
for k in sorted(users)[:6]:
    print(k, sorted(users[k]))
print("rotary-related node names:", sorted({n.name.split("/")[2] for n in model.graph.node if n.name.startswith("/model/rotary")}))
