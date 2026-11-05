"""Prints the initializers and the node names of an exported encoder graph (names, shapes, node types), to see how the exporter named
the weights. Usage: python dump_graph.py model.onnx [name-substring ...]. Reads the file only; runs nothing."""
import sys

import onnx

model = onnx.load(sys.argv[1], load_external_data=False)
filters = sys.argv[2:]
graph = model.graph
print("inputs:", [i.name for i in graph.input], "outputs:", [o.name for o in graph.output])
print("initializers:", len(graph.initializer), "nodes:", len(graph.node))
for t in graph.initializer:
    if not filters or any(f in t.name for f in filters):
        print("INIT", t.name, list(t.dims), t.data_type)
for n in graph.node:
    if not filters or any(f in n.name for f in filters):
        print("NODE", n.op_type, n.name, list(n.input), list(n.output))
