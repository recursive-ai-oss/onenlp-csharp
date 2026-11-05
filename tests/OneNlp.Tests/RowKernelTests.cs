using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;
using RecursiveAI.OneNlp.Neural.Encoders;

namespace RecursiveAI.OneNlp.Tests;

public sealed class RowKernelTests
{
    [Theory]
    [InlineData(5, 1)]
    [InlineData(64, 33)]
    [InlineData(96, 65)]
    [InlineData(48, 260)]
    public void DenseTopAndConfidenceMatchTheTensorOperations(int inputs, int classes)
    {
        var layer = new Linear(inputs, classes, new Generator(7));
        using var scope = new ActivationScope();
        var rng = new Random(3);
        float[] x = new float[inputs * 6];
        for (int i = 0; i < x.Length; i++)
        {
            x[i] = (float)((rng.NextDouble() * 4) - 2);
        }

        Tensor input = Tensor.FromSpan<float>(x, Shape.Create(6, inputs));
        ReadOnlySpan<float> expected = Ops.Softmax(layer.Forward(input)).Data<float>();
        RowClassifier fast = new FrozenLinear(layer).Get();
        float[] logits = new float[fast.Stride];
        for (int r = 0; r < 6; r++)
        {
            fast.Logits(x.AsSpan(r * inputs, inputs), logits);
            (int best, float top) = RowKernels.ArgMax(logits, classes);
            float p = 1f / RowKernels.ExpSum(logits, classes, top);
            ReadOnlySpan<float> row = expected.Slice(r * classes, classes);
            int expectedBest = 0;
            for (int c = 1; c < classes; c++)
            {
                if (row[c] > row[expectedBest])
                {
                    expectedBest = c;
                }
            }

            Assert.Equal(expectedBest, best);
            Assert.True(Math.Abs(p - row[best]) < 1e-5f, $"row {r}: {p} vs {row[best]}");
        }
    }

    [Fact]
    public void ExpSumAgreesWithTheLibraryExponentialAcrossTheRange()
    {
        float[] v = [0f, -0.1f, -1f, -3.3f, -10f, -30f, -80f, -200f, 0f];
        double sum = 0;
        foreach (float e in v)
        {
            sum += Math.Exp(e);
        }

        float got = RowKernels.ExpSum(v, v.Length, 0f);
        Assert.True(Math.Abs(got - sum) / sum < 1e-6, $"{got} vs {sum}");
    }

    [Fact]
    public void LayerNormMatchesTheTensorOperationOnAnOddWidth()
    {
        const int width = 37;
        var norm = new LayerNorm(width);
        var rng = new Random(5);
        float[] x = new float[width];
        for (int i = 0; i < width; i++)
        {
            x[i] = (float)((rng.NextDouble() * 6) - 3);
        }

        using var scope = new ActivationScope();
        Tensor input = Tensor.FromSpan<float>(x, Shape.Create(1, width));
        float[] expected = norm.Forward(input).Data<float>().ToArray();
        float[] got = new float[width];
        RowKernels.LayerNorm(x, norm.Weight.Data<float>(), norm.Bias.Data<float>(), got);
        for (int i = 0; i < width; i++)
        {
            Assert.True(Math.Abs(got[i] - expected[i]) < 1e-5f, $"{i}: {got[i]} vs {expected[i]}");
        }
    }
}
