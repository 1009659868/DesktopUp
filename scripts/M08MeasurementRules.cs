using System;
public static class DesktopUpAutoMeasurementRules
{
    // Counting contract only. The collector additionally verifies native input, build/display
    // identity, marker times, process exit, layout restoration and resource cleanup.
    public static bool CountsMatch(int count, string mode, int samples, int requests, int commits, int postureStarts, long revisionDelta)
    {
        if (count != 1 && count != 3 && count != 6 || samples < 19) return false;
        if (mode != "off-expanded" && mode != "on-expanded" && mode != "off-collapsed" &&
            mode != "on-collapsed" && mode != "off-trigger" && mode != "on-trigger") return false;
        int transitions = mode.EndsWith("-trigger", StringComparison.Ordinal) ? count : 0;
        int automatic = mode == "on-trigger" ? count : 0;
        return requests == automatic && commits == automatic && postureStarts == transitions && revisionDelta == transitions;
    }
}
