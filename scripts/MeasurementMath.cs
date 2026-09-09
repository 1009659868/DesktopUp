using System;
using System.Linq;

// Shared verbatim with contract tests; used by the external sampler, never production UI.
public static class DesktopUpMeasurementMath
{
    // .NET Framework (Windows PowerShell 5.1) does not expose double.IsFinite.
    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    public static double Quantile(double[] values, double p)
    {
        if (values.Length == 0 || p < 0 || p > 1 || !IsFinite(p) || values.Any(v => !IsFinite(v)))
            throw new ArgumentException("Finite nonempty samples and p in [0,1] required.");
        var sorted = values.OrderBy(v => v).ToArray();
        return sorted[Math.Max(0, (int)Math.Ceiling(p * sorted.Length) - 1)];
    }
    public static double CpuPercent(double cpuMilliseconds, double elapsedMilliseconds, int logicalProcessors)
    {
        if (!IsFinite(cpuMilliseconds) || !IsFinite(elapsedMilliseconds) ||
            cpuMilliseconds < 0 || elapsedMilliseconds <= 0 || logicalProcessors <= 0)
            throw new ArgumentException("Monotonic deltas and positive processor count required.");
        return 100 * cpuMilliseconds / elapsedMilliseconds / logicalProcessors;
    }
}
