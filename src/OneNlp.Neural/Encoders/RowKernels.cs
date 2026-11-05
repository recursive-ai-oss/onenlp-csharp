using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace RecursiveAI.OneNlp.Neural.Encoders;

// Row-wise kernels of the fast encoder's inference path. A row is one token's vector (a few dozen to a few hundred floats), so the
// loops are short and fixed-width Vector128 steps beat the scalar loop. Vector128 exists on every platform (x64 SSE, arm64 NEON, a
// software fallback elsewhere, all with the same results as the scalar tails below).
//
// Bounds: every method first checks (by slicing) that each span argument is at least as long as the row it processes, then reads and
// writes only indices below that length: the vector loops run while i + 4 <= length, the scalar tail covers the rest. Loads and stores
// go through Vector128.LoadUnsafe/StoreUnsafe on references to the spans' first elements, which skips the per-step span bounds checks
// that otherwise cost as much as the arithmetic.
internal static class RowKernels
{
    private const float Epsilon = 1e-5f;

    // dst[i] += src[i]; src is at least as long as dst.
    public static void Add(Span<float> dst, ReadOnlySpan<float> src)
    {
        src = src[..dst.Length];
        ref float d = ref MemoryMarshal.GetReference(dst);
        ref float s = ref MemoryMarshal.GetReference(src);
        int n = dst.Length;
        int i = 0;
        for (; i <= n - 4; i += 4)
        {
            (Vector128.LoadUnsafe(ref d, (nuint)i) + Vector128.LoadUnsafe(ref s, (nuint)i)).StoreUnsafe(ref d, (nuint)i);
        }

        for (; i < n; i++)
        {
            Unsafe.Add(ref d, i) += Unsafe.Add(ref s, i);
        }
    }

    public static void Relu(Span<float> v)
    {
        ref float d = ref MemoryMarshal.GetReference(v);
        int n = v.Length;
        int i = 0;
        for (; i <= n - 4; i += 4)
        {
            Vector128.Max(Vector128.LoadUnsafe(ref d, (nuint)i), Vector128<float>.Zero).StoreUnsafe(ref d, (nuint)i);
        }

        for (; i < n; i++)
        {
            ref float e = ref Unsafe.Add(ref d, i);
            e = Math.Max(e, 0f);
        }
    }

    // v[i] = gelu(v[i]) = v[i] * Phi(v[i]) = 0.5 * v[i] * (1 + erf(v[i] / sqrt(2))) (Hendrycks and Gimpel 2016, eq. 1: the exact form, not the tanh
    // approximation). erf comes from Abramowitz and Stegun 7.1.26, erf(z) = 1 - (a1 t + a2 t^2 + ... + a5 t^5) exp(-z^2) with t = 1 / (1 + p z) for z >= 0 and
    // erf(-z) = -erf(z); its absolute error is at most 1.5e-7, so a value is off by at most 0.5 |v| * 1.5e-7, far below float noise in the layers around it.
    // exp uses the polynomial below, so the result is the same on every platform.
    public static void Gelu(Span<float> v)
    {
        ref float d = ref MemoryMarshal.GetReference(v);
        int n = v.Length;
        int i = 0;
        for (; i <= n - 4; i += 4)
        {
            Gelu(Vector128.LoadUnsafe(ref d, (nuint)i)).StoreUnsafe(ref d, (nuint)i);
        }

        for (; i < n; i++)
        {
            ref float e = ref Unsafe.Add(ref d, i);
            e = Gelu(Vector128.Create(e)).ToScalar();
        }
    }

    private static Vector128<float> Gelu(Vector128<float> x)
    {
        Vector128<float> z = Vector128.Abs(x) * Vector128.Create(0.70710678118f);
        Vector128<float> t = Vector128<float>.One / (Vector128<float>.One + (Vector128.Create(0.3275911f) * z));
        Vector128<float> poly = Vector128.Create(1.061405429f);
        poly = (poly * t) + Vector128.Create(-1.453152027f);
        poly = (poly * t) + Vector128.Create(1.421413741f);
        poly = (poly * t) + Vector128.Create(-0.284496736f);
        poly = (poly * t) + Vector128.Create(0.254829592f);
        Vector128<float> erf = Vector128<float>.One - (poly * t * Exp(Vector128<float>.Zero - (z * z)));
        // sign of x on erf: erf is odd
        Vector128<float> signed = Vector128.ConditionalSelect(Vector128.LessThan(x, Vector128<float>.Zero), Vector128<float>.Zero - erf, erf);
        return Vector128.Create(0.5f) * x * (Vector128<float>.One + signed);
    }

    // Mean and 1/sqrt(variance + epsilon) of a row, the variance taken around the mean over all values (as LayerNorm does).
    private static (float Mean, float InvStd) Moments(ReadOnlySpan<float> v)
    {
        ref float p = ref MemoryMarshal.GetReference(v);
        int n = v.Length;
        Vector128<float> acc = Vector128<float>.Zero;
        int i = 0;
        for (; i <= n - 4; i += 4)
        {
            acc += Vector128.LoadUnsafe(ref p, (nuint)i);
        }

        float sum = Vector128.Sum(acc);
        for (; i < n; i++)
        {
            sum += Unsafe.Add(ref p, i);
        }

        float mean = sum / n;
        Vector128<float> m = Vector128.Create(mean);
        acc = Vector128<float>.Zero;
        i = 0;
        for (; i <= n - 4; i += 4)
        {
            Vector128<float> c = Vector128.LoadUnsafe(ref p, (nuint)i) - m;
            acc += c * c;
        }

        float sq = Vector128.Sum(acc);
        for (; i < n; i++)
        {
            float c = Unsafe.Add(ref p, i) - mean;
            sq += c * c;
        }

        return (mean, 1f / MathF.Sqrt((sq / n) + Epsilon));
    }

    // dst = (src - mean) / std * gamma + beta; dst may be src; gamma and beta are at least as long as src.
    public static void LayerNorm(ReadOnlySpan<float> src, ReadOnlySpan<float> gamma, ReadOnlySpan<float> beta, Span<float> dst)
    {
        gamma = gamma[..src.Length];
        beta = beta[..src.Length];
        dst = dst[..src.Length];
        (float mean, float inv) = Moments(src);
        Vector128<float> m = Vector128.Create(mean), s = Vector128.Create(inv);
        ref float p = ref MemoryMarshal.GetReference(src);
        ref float g = ref MemoryMarshal.GetReference(gamma);
        ref float b = ref MemoryMarshal.GetReference(beta);
        ref float o = ref MemoryMarshal.GetReference(dst);
        int n = src.Length;
        int i = 0;
        for (; i <= n - 4; i += 4)
        {
            (((Vector128.LoadUnsafe(ref p, (nuint)i) - m) * s * Vector128.LoadUnsafe(ref g, (nuint)i)) + Vector128.LoadUnsafe(ref b, (nuint)i)).StoreUnsafe(ref o, (nuint)i);
        }

        for (; i < n; i++)
        {
            Unsafe.Add(ref o, i) = ((Unsafe.Add(ref p, i) - mean) * inv * Unsafe.Add(ref g, i)) + Unsafe.Add(ref b, i);
        }
    }

    // x += LayerNorm(src): the residual step of a window layer; x, gamma and beta are at least as long as src.
    public static void LayerNormAdd(ReadOnlySpan<float> src, ReadOnlySpan<float> gamma, ReadOnlySpan<float> beta, Span<float> x)
    {
        gamma = gamma[..src.Length];
        beta = beta[..src.Length];
        x = x[..src.Length];
        (float mean, float inv) = Moments(src);
        Vector128<float> m = Vector128.Create(mean), s = Vector128.Create(inv);
        ref float p = ref MemoryMarshal.GetReference(src);
        ref float g = ref MemoryMarshal.GetReference(gamma);
        ref float b = ref MemoryMarshal.GetReference(beta);
        ref float o = ref MemoryMarshal.GetReference(x);
        int n = src.Length;
        int i = 0;
        for (; i <= n - 4; i += 4)
        {
            (Vector128.LoadUnsafe(ref o, (nuint)i) + ((Vector128.LoadUnsafe(ref p, (nuint)i) - m) * s * Vector128.LoadUnsafe(ref g, (nuint)i)) + Vector128.LoadUnsafe(ref b, (nuint)i)).StoreUnsafe(ref o, (nuint)i);
        }

        for (; i < n; i++)
        {
            Unsafe.Add(ref o, i) += ((Unsafe.Add(ref p, i) - mean) * inv * Unsafe.Add(ref g, i)) + Unsafe.Add(ref b, i);
        }
    }

    // logits[0..stride) = bias + sum_i x[i] * wT[i * stride ..]. wT is the layer's weights transposed to [inputs, stride] (stride a multiple
    // of 4, the classes padded with zeros), so one input value is broadcast against contiguous output columns: no packing, no temporaries.
    // Four output vectors (16 columns) stay in registers across the whole input loop. Products and sums are separate operations (no
    // fused multiply-add) so that every machine rounds the same way.
    public static void Dense(ReadOnlySpan<float> x, ReadOnlySpan<float> wT, int stride, ReadOnlySpan<float> bias, Span<float> logits)
    {
        int k = x.Length;
        if ((stride & 3) != 0 || stride <= 0 || wT.Length < (long)k * stride || bias.Length < stride || logits.Length < stride)
        {
            throw new ArgumentException("Dense: the buffers do not fit the stride.");
        }

        ref float xr = ref MemoryMarshal.GetReference(x);
        ref float wr = ref MemoryMarshal.GetReference(wT);
        ref float br = ref MemoryMarshal.GetReference(bias);
        ref float lr = ref MemoryMarshal.GetReference(logits);
        int c = 0;
        if (Vector256.IsHardwareAccelerated)
        {
            // The same sums with eight columns per register (each column still adds its products in input order, so the results are
            // bit for bit those of the four-wide loops below). Vector256 is only used where it is a real register: on NEON it is emulated.
            for (; c <= stride - 32; c += 32)
            {
                Vector256<float> b0 = Vector256.LoadUnsafe(ref br, (nuint)c);
                Vector256<float> b1 = Vector256.LoadUnsafe(ref br, (nuint)(c + 8));
                Vector256<float> b2 = Vector256.LoadUnsafe(ref br, (nuint)(c + 16));
                Vector256<float> b3 = Vector256.LoadUnsafe(ref br, (nuint)(c + 24));
                for (int i = 0; i < k; i++)
                {
                    Vector256<float> xv = Vector256.Create(Unsafe.Add(ref xr, i));
                    ref float row = ref Unsafe.Add(ref wr, (i * stride) + c);
                    b0 += xv * Vector256.LoadUnsafe(ref row, 0);
                    b1 += xv * Vector256.LoadUnsafe(ref row, 8);
                    b2 += xv * Vector256.LoadUnsafe(ref row, 16);
                    b3 += xv * Vector256.LoadUnsafe(ref row, 24);
                }

                b0.StoreUnsafe(ref lr, (nuint)c);
                b1.StoreUnsafe(ref lr, (nuint)(c + 8));
                b2.StoreUnsafe(ref lr, (nuint)(c + 16));
                b3.StoreUnsafe(ref lr, (nuint)(c + 24));
            }

            for (; c <= stride - 16; c += 16)
            {
                Vector256<float> b0 = Vector256.LoadUnsafe(ref br, (nuint)c);
                Vector256<float> b1 = Vector256.LoadUnsafe(ref br, (nuint)(c + 8));
                for (int i = 0; i < k; i++)
                {
                    Vector256<float> xv = Vector256.Create(Unsafe.Add(ref xr, i));
                    ref float row = ref Unsafe.Add(ref wr, (i * stride) + c);
                    b0 += xv * Vector256.LoadUnsafe(ref row, 0);
                    b1 += xv * Vector256.LoadUnsafe(ref row, 8);
                }

                b0.StoreUnsafe(ref lr, (nuint)c);
                b1.StoreUnsafe(ref lr, (nuint)(c + 8));
            }
        }

        for (; c <= stride - 16; c += 16)
        {
            Vector128<float> a0 = Vector128.LoadUnsafe(ref br, (nuint)c);
            Vector128<float> a1 = Vector128.LoadUnsafe(ref br, (nuint)(c + 4));
            Vector128<float> a2 = Vector128.LoadUnsafe(ref br, (nuint)(c + 8));
            Vector128<float> a3 = Vector128.LoadUnsafe(ref br, (nuint)(c + 12));
            for (int i = 0; i < k; i++)
            {
                Vector128<float> xv = Vector128.Create(Unsafe.Add(ref xr, i));
                ref float row = ref Unsafe.Add(ref wr, (i * stride) + c);
                a0 += xv * Vector128.LoadUnsafe(ref row, 0);
                a1 += xv * Vector128.LoadUnsafe(ref row, 4);
                a2 += xv * Vector128.LoadUnsafe(ref row, 8);
                a3 += xv * Vector128.LoadUnsafe(ref row, 12);
            }

            a0.StoreUnsafe(ref lr, (nuint)c);
            a1.StoreUnsafe(ref lr, (nuint)(c + 4));
            a2.StoreUnsafe(ref lr, (nuint)(c + 8));
            a3.StoreUnsafe(ref lr, (nuint)(c + 12));
        }

        for (; c < stride; c += 4)
        {
            Vector128<float> a = Vector128.LoadUnsafe(ref br, (nuint)c);
            for (int i = 0; i < k; i++)
            {
                a += Vector128.Create(Unsafe.Add(ref xr, i)) * Vector128.LoadUnsafe(ref wr, (nuint)((i * stride) + c));
            }

            a.StoreUnsafe(ref lr, (nuint)c);
        }
    }

    // The index of the first largest value among the first count values, and that value; NaN never wins.
    public static (int Index, float Value) ArgMax(ReadOnlySpan<float> v, int count)
    {
        v = v[..count];
        int best = 0;
        float top = v[0];
        for (int i = 1; i < v.Length; i++)
        {
            float e = v[i];
            if (e > top)
            {
                top = e;
                best = i;
            }
        }

        return (best, top);
    }

    // sum over the first count values of exp(v - max): the denominator of a softmax whose largest logit is max. exp is a degree-6
    // polynomial after range reduction to a power of two (relative error about 1e-7), the same on every platform.
    public static float ExpSum(ReadOnlySpan<float> v, int count, float max)
    {
        v = v[..count];
        ref float p = ref MemoryMarshal.GetReference(v);
        Vector128<float> m = Vector128.Create(max);
        Vector128<float> acc = Vector128<float>.Zero;
        int i = 0;
        for (; i <= count - 4; i += 4)
        {
            acc += Exp(Vector128.LoadUnsafe(ref p, (nuint)i) - m);
        }

        float sum = Vector128.Sum(acc);
        for (; i < count; i++)
        {
            sum += Exp(Vector128.Create(Unsafe.Add(ref p, i) - max)).ToScalar();
        }

        return sum;
    }

    private static Vector128<float> Exp(Vector128<float> x)
    {
        x = Vector128.Min(Vector128.Max(x, Vector128.Create(-87f)), Vector128.Create(87f));
        Vector128<float> t = x * Vector128.Create(1.44269504f);
        Vector128<float> n = Vector128.Floor(t + Vector128.Create(0.5f));
        Vector128<float> u = (t - n) * Vector128.Create(0.693147181f);
        Vector128<float> r = Vector128.Create(1f / 720f);
        r = (r * u) + Vector128.Create(1f / 120f);
        r = (r * u) + Vector128.Create(1f / 24f);
        r = (r * u) + Vector128.Create(1f / 6f);
        r = (r * u) + Vector128.Create(0.5f);
        r = (r * u) + Vector128<float>.One;
        r = (r * u) + Vector128<float>.One;
        Vector128<int> bits = Vector128.ShiftLeft(Vector128.ConvertToInt32(n) + Vector128.Create(127), 23);
        return r * bits.AsSingle();
    }
}
