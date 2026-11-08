using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;

namespace RecursiveAI.OneNlp.Tests;

public class CheckpointTests
{
    [Fact]
    public void LoadsWhatCheckpointSavedIncludingOptimizerAndExtras()
    {
        string path = Path.Combine(Path.GetTempPath(), "ckpt-" + Guid.NewGuid().ToString("N") + ".ckpt");
        try
        {
            var source = new Linear(5, 3, new Generator(1));
            var optimizer = new AdamW(source.Parameters(), 0.01f);
            optimizer.RestoreStepCount(7);
            Checkpoint.Save(path, source, optimizer, new Dictionary<string, long> { ["step"] = 7, ["seed"] = -3 });

            var target = new Linear(5, 3, new Generator(2));
            var targetOptimizer = new AdamW(target.Parameters(), 0.01f);
            IReadOnlyDictionary<string, long> extra = Checkpoint.Load(path, target, targetOptimizer).Extra;

            Assert.Equal(7, extra["step"]);
            Assert.Equal(-3, extra["seed"]);
            List<(string Name, Tensor Tensor)> a = source.StateDict();
            List<(string Name, Tensor Tensor)> b = target.StateDict();
            for (int i = 0; i < a.Count; i++)
            {
                Assert.Equal(a[i].Name, b[i].Name);
                Assert.True(a[i].Tensor.Data<float>().SequenceEqual(b[i].Tensor.Data<float>()));
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RefusesAFileThatDoesNotFitAndChangesNothing()
    {
        string path = Path.Combine(Path.GetTempPath(), "ckpt-" + Guid.NewGuid().ToString("N") + ".ckpt");
        try
        {
            Checkpoint.Save(path, new Linear(5, 3, new Generator(1)));
            var target = new Linear(4, 3, new Generator(2));
            float[] before = [.. target.StateDict()[0].Tensor.Data<float>()];
            Assert.Throws<CheckpointException>(() => Checkpoint.Load(path, target));
            Assert.True(before.SequenceEqual(target.StateDict()[0].Tensor.Data<float>()));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void HashOfEqualsTheHashOfTheSavedBytes()
    {
        var module = new Linear(6, 2, new Generator(3));
        using var ms = new MemoryStream();
        Checkpoint.Save(ms, module);
        Assert.Equal(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(ms.ToArray())), CheckpointHash.Of(module));
    }
}
