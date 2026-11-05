using System.Runtime.Intrinsics;

namespace RecursiveAI.OneNlp.Neural.Encoders;

// Row-wise kernels of the fast encoder's inference path. A row is one token's vector (a few dozen to a few hundred floats), so the
// loops are short and fixed width Vector128 steps beat both the scalar loop and the per-operation cost of a tensor library. Vector128
// works on every platform (x64 SSE, arm64 NEON, and a software fallback with the same results as the scalar tail below). Slices are
// bounds-checked by the span; nothing here reads past its arguments.
internal static class RowKernels
{
    private const float Epsilon = 1e-5f;

    // dst[i] += src[i]; src is at least as long as dst.
    public static void Add(Span<float> dst, ReadOnlySpan<float> src)
    {
        src = src[..dst.Length];
        int i = 0;
        for (; i <= dst.Length - 4; i += 4)
        {
            (Vector128.Create(dst.Slice(i, 4)) + Vector128.Create(src.Slice(i, 4))).CopyTo(dst.Slice(i, 4));
        }

        for (; i < dst.Length; i++)
        {
            dst[i] += src[i];
        }
    }

    public static void Relu(Span<float> v)
    {
        int i = 0;
        for (; i <= v.Length - 4; i += 4)
        {
            Vector128.Max(Vector128.Create(v.Slice(i, 4)), Vector128<float>.Zero).CopyTo(v.Slice(i, 4));
        }

        for (; i < v.Length; i++)
        {
            v[i] = Math.Max(v[i], 0f);
        }
    }

    // Mean and 1/sqrt(variance + epsilon) of a row, the variance taken around the mean over all values (as LayerNorm does).
    private static (float Mean, float InvStd) Moments(ReadOnlySpan<float> v)
    {
        int n = v.Length;
        Vector128<float> acc = Vector128<float>.Zero;
        int i = 0;
        for (; i <= n - 4; i += 4)
        {
            acc += Vector128.Create(v.Slice(i, 4));
        }

        float sum = Vector128.Sum(acc);
        for (; i < n; i++)
        {
            sum += v[i];
        }

        float mean = sum / n;
        Vector128<float> m = Vector128.Create(mean);
        acc = Vector128<float>.Zero;
        i = 0;
        for (; i <= n - 4; i += 4)
        {
            Vector128<float> c = Vector128.Create(v.Slice(i, 4)) - m;
            acc += c * c;
        }

        float sq = Vector128.Sum(acc);
        for (; i < n; i++)
        {
            float c = v[i] - mean;
            sq += c * c;
        }

        return (mean, 1f / MathF.Sqrt((sq / n) + Epsilon));
    }

    // dst = (src - mean) / std * gamma + beta; dst may be src.
    public static void LayerNorm(ReadOnlySpan<float> src, ReadOnlySpan<float> gamma, ReadOnlySpan<float> beta, Span<float> dst)
    {
        (float mean, float inv) = Moments(src);
        Vector128<float> m = Vector128.Create(mean), s = Vector128.Create(inv);
        int i = 0;
        for (; i <= dst.Length - 4; i += 4)
        {
            ((Vector128.Create(src.Slice(i, 4)) - m) * s * Vector128.Create(gamma.Slice(i, 4)) + Vector128.Create(beta.Slice(i, 4))).CopyTo(dst.Slice(i, 4));
        }

        for (; i < dst.Length; i++)
        {
            dst[i] = ((src[i] - mean) * inv * gamma[i]) + beta[i];
        }
    }

    // x += LayerNorm(src): the residual step of a window layer.
    public static void LayerNormAdd(ReadOnlySpan<float> src, ReadOnlySpan<float> gamma, ReadOnlySpan<float> beta, Span<float> x)
    {
        (float mean, float inv) = Moments(src);
        Vector128<float> m = Vector128.Create(mean), s = Vector128.Create(inv);
        int i = 0;
        for (; i <= x.Length - 4; i += 4)
        {
            (Vector128.Create(x.Slice(i, 4)) + ((Vector128.Create(src.Slice(i, 4)) - m) * s * Vector128.Create(gamma.Slice(i, 4))) + Vector128.Create(beta.Slice(i, 4))).CopyTo(x.Slice(i, 4));
        }

        for (; i < x.Length; i++)
        {
            x[i] += ((src[i] - mean) * inv * gamma[i]) + beta[i];
        }
    }
}
