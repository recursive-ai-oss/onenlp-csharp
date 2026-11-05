"""CPU smoke test of the data pipeline and the loss plumbing, with random tensors standing in for the teacher."""
import sys
import torch
from distill import Data, Distiller, TEACHER_LAYERS
from student import StudentConfig

data = Data(sys.argv[1])
print(len(data.langs), "languages", sum(len(t) for t in data.train), "train windows")
for step in (0, 1, 2, 10 ** 9):
    b = data.batch(1, step, 32768)
    print(step, b.shape, int((b != 0).sum()), b.max())
dev = data.dev_batches()
print("dev batches", len(dev), dev[0].shape)
cfg = StudentConfig(vocab_size=256000, embed_rank=96)
m = Distiller(cfg, 768)
ids = torch.from_numpy(data.batch(1, 3, 4096))
mask = (ids != 0).long()
t_final = torch.randn(*ids.shape, 768)
t_mid = [torch.randn(*ids.shape, 768) for _ in TEACHER_LAYERS]
loss, parts = m.losses(ids, mask, t_final, t_mid)
loss.backward()
print(float(loss), {k: round(float(v), 3) for k, v in parts.items()})
print("params", sum(p.numel() for p in m.student.parameters()))
