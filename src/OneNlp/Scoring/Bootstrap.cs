namespace RecursiveAI.OneNlp.Scoring;

/// <summary>Computes a statistic over a set of units (documents or sentences), such as the F1 of their summed counts.</summary>
/// <typeparam name="T">The unit type, for example <see cref="MetricCounts"/>.</typeparam>
/// <param name="units">The units of one resample, in draw order.</param>
/// <returns>The statistic, in points.</returns>
public delegate double Statistic<T>(ReadOnlySpan<T> units);

/// <summary>A point estimate with a percentile bootstrap interval.</summary>
/// <param name="Estimate">The statistic on the full test set.</param>
/// <param name="Low">The 2.5th percentile of the resampled statistic.</param>
/// <param name="High">The 97.5th percentile of the resampled statistic.</param>
/// <param name="Resamples">The number of resamples drawn.</param>
/// <param name="Seed">The seed of the generator.</param>
public readonly record struct ConfidenceInterval(double Estimate, double Low, double High, int Resamples, ulong Seed);

/// <summary>The outcome of a paired bootstrap between two systems scored on the same units.</summary>
/// <param name="First">The first system's estimate and interval.</param>
/// <param name="Second">The second system's estimate and interval.</param>
/// <param name="Difference">First minus second: its estimate and interval from the paired resamples.</param>
public readonly record struct PairedResult(ConfidenceInterval First, ConfidenceInterval Second, ConfidenceInterval Difference)
{
    /// <summary>Whether the difference's interval contains 0, that is, the systems are not told apart at 95%.</summary>
    public bool DifferenceContainsZero => Difference.Low <= 0 && Difference.High >= 0;
}

/// <summary>
/// Percentile bootstrap (Efron and Tibshirani 1993, §13.3) over test units. Each resample draws as many units as the test set has,
/// with replacement, from a generator seeded by the caller (SplitMix64, so the draws are identical on every platform and runtime).
/// </summary>
public static class Bootstrap
{
    /// <summary>The default number of resamples.</summary>
    public const int DefaultResamples = 1000;

    /// <summary>The default seed.</summary>
    public const ulong DefaultSeed = 20260101;

    /// <summary>The 95% percentile interval of a statistic.</summary>
    /// <typeparam name="T">The unit type.</typeparam>
    /// <param name="units">The test units: sentences or documents. The resampling unit is whatever these are.</param>
    /// <param name="statistic">The statistic, computed on the full set and on each resample.</param>
    /// <param name="resamples">The number of resamples; at least 100.</param>
    /// <param name="seed">The generator seed; the same seed gives the same interval.</param>
    /// <returns>The estimate and interval, in the statistic's units.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="resamples"/> is below 100.</exception>
    /// <exception cref="ArgumentException">There are no units.</exception>
    public static ConfidenceInterval Interval<T>(IReadOnlyList<T> units, Statistic<T> statistic, int resamples = DefaultResamples, ulong seed = DefaultSeed)
    {
        ArgumentNullException.ThrowIfNull(units);
        ArgumentNullException.ThrowIfNull(statistic);
        Check(units.Count, resamples);
        T[] all = [.. units];
        double estimate = statistic(all);
        var values = new double[resamples];
        var generator = new SplitMix64(seed);
        var buffer = new T[all.Length];
        for (int r = 0; r < resamples; r++)
        {
            Draw(all, buffer, ref generator);
            values[r] = statistic(buffer);
        }

        return Percentiles(estimate, values, resamples, seed);
    }

    /// <summary>
    /// A paired bootstrap of two systems scored on the same units: both are evaluated on every resample, so the interval of the
    /// difference reflects how the systems vary together.
    /// </summary>
    /// <typeparam name="T">The unit type.</typeparam>
    /// <param name="first">The first system's per-unit values.</param>
    /// <param name="second">The second system's per-unit values, for the same units in the same order.</param>
    /// <param name="statistic">The statistic.</param>
    /// <param name="resamples">The number of resamples; at least 100.</param>
    /// <param name="seed">The generator seed.</param>
    /// <returns>Each system's interval and the interval of first minus second.</returns>
    /// <exception cref="ArgumentException">The lists differ in length or are empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="resamples"/> is below 100.</exception>
    public static PairedResult Paired<T>(
        IReadOnlyList<T> first, IReadOnlyList<T> second, Statistic<T> statistic, int resamples = DefaultResamples, ulong seed = DefaultSeed)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        ArgumentNullException.ThrowIfNull(statistic);
        if (first.Count != second.Count)
        {
            throw new ArgumentException("A paired bootstrap needs the same units for both systems.", nameof(second));
        }

        Check(first.Count, resamples);
        T[] a = [.. first];
        T[] b = [.. second];
        double estimateA = statistic(a);
        double estimateB = statistic(b);
        var valuesA = new double[resamples];
        var valuesB = new double[resamples];
        var differences = new double[resamples];
        var generator = new SplitMix64(seed);
        var indexes = new int[a.Length];
        var bufferA = new T[a.Length];
        var bufferB = new T[a.Length];
        for (int r = 0; r < resamples; r++)
        {
            for (int i = 0; i < indexes.Length; i++)
            {
                indexes[i] = generator.NextBelow(indexes.Length);
                bufferA[i] = a[indexes[i]];
                bufferB[i] = b[indexes[i]];
            }

            valuesA[r] = statistic(bufferA);
            valuesB[r] = statistic(bufferB);
            differences[r] = valuesA[r] - valuesB[r];
        }

        return new PairedResult(
            Percentiles(estimateA, valuesA, resamples, seed),
            Percentiles(estimateB, valuesB, resamples, seed),
            Percentiles(estimateA - estimateB, differences, resamples, seed));
    }

    /// <summary>The F1 in points of a set of units' summed counts, the statistic for UD and span scores.</summary>
    /// <param name="units">The units' counts.</param>
    /// <returns>100 times the F1 of the sum.</returns>
    public static double F1Points(ReadOnlySpan<MetricCounts> units)
    {
        MetricCounts sum = default;
        foreach (MetricCounts c in units)
        {
            sum += c;
        }

        return 100 * sum.F1;
    }

    /// <summary>The aligned accuracy in points of a set of units' summed counts (correct over aligned gold items).</summary>
    /// <param name="units">The units' counts.</param>
    /// <returns>100 times the aligned accuracy of the sum.</returns>
    public static double AlignedAccuracyPoints(ReadOnlySpan<MetricCounts> units)
    {
        MetricCounts sum = default;
        foreach (MetricCounts c in units)
        {
            sum += c;
        }

        return 100 * sum.AlignedAccuracy;
    }

    private static void Check(int units, int resamples)
    {
        if (units == 0)
        {
            throw new ArgumentException("There are no units to resample.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(resamples, 100);
    }

    private static void Draw<T>(T[] source, T[] buffer, ref SplitMix64 generator)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = source[generator.NextBelow(source.Length)];
        }
    }

    // Percentile method: sort the resampled values and take the entries at 2.5% and 97.5% (nearest rank, 0-based
    // floor(0.025 B) and ceil(0.975 B) - 1).
    private static ConfidenceInterval Percentiles(double estimate, double[] values, int resamples, ulong seed)
    {
        Array.Sort(values);
        int low = (int)Math.Floor(0.025 * resamples);
        int high = (int)Math.Ceiling(0.975 * resamples) - 1;
        return new ConfidenceInterval(estimate, values[low], values[high], resamples, seed);
    }

    private struct SplitMix64(ulong seed)
    {
        private ulong _state = seed;

        public int NextBelow(int bound)
        {
            _state += 0x9E3779B97F4A7C15UL;
            ulong z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            return (int)(((z >> 32) * (ulong)bound) >> 32); // multiply-shift: uniform enough for bounds far below 2^32
        }
    }
}

/// <summary>The rule that decides whether our score is "within noise" of a reference, as the quality bar uses it.</summary>
public static class NoiseRule
{
    /// <summary>The tolerance, in points, when the reference has no interval of its own.</summary>
    public const double PublishedTolerancePoints = 0.5;

    /// <summary>The reference has an interval too: within noise when the two intervals overlap.</summary>
    /// <param name="ours">Our estimate and interval, in points.</param>
    /// <param name="reference">The reference's estimate and interval, in points.</param>
    /// <returns>Whether the intervals overlap.</returns>
    public static bool WithinNoise(ConfidenceInterval ours, ConfidenceInterval reference) =>
        ours.Low <= reference.High && reference.Low <= ours.High;

    /// <summary>
    /// The reference is a published number without an interval: within noise when it lies inside our interval, or within
    /// <see cref="PublishedTolerancePoints"/> of our estimate.
    /// </summary>
    /// <param name="ours">Our estimate and interval, in points.</param>
    /// <param name="reference">The reference score in points.</param>
    /// <returns>Whether the reference is inside our interval or within the tolerance of our estimate.</returns>
    public static bool WithinNoise(ConfidenceInterval ours, double reference) =>
        (reference >= ours.Low && reference <= ours.High) || Math.Abs(ours.Estimate - reference) <= PublishedTolerancePoints;
}
