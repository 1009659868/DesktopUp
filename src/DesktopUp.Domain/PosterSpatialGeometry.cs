namespace DesktopUp.Domain;

// Absolute physical screen pixels. The Domain deliberately has no native-window types.
public readonly record struct SpatialRect(int X, int Y, int Width, int Height)
{
    public long Right => (long)X + Width;
    public long Bottom => (long)Y + Height;
    public bool IsValid => Width > 0 && Height > 0 && Right <= int.MaxValue && Bottom <= int.MaxValue;
    public bool Contains(SpatialRect other) => IsValid && other.IsValid &&
        other.X >= X && other.Y >= Y && other.Right <= Right && other.Bottom <= Bottom;
    public bool Overlaps(SpatialRect other) => IsValid && other.IsValid &&
        X < other.Right && Right > other.X && Y < other.Bottom && Bottom > other.Y;
}

public static class SpatialPixelProjection
{
    // DIP coordinates are local to the supplied physical origin, never global DIP coordinates.
    // Round occupation outwards, then callers must revalidate it against native work-area pixels.
    public static bool TryProjectLocalDip(double x, double y, double width, double height,
        int originX, int originY, uint dpiX, uint dpiY, out SpatialRect result)
    {
        result = default;
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(width) ||
            !double.IsFinite(height) || width <= 0 || height <= 0 || dpiX == 0 || dpiY == 0)
            return false;
        double left = Math.Floor(originX + x * (dpiX / 96d));
        double top = Math.Floor(originY + y * (dpiY / 96d));
        double right = Math.Ceiling(originX + (x + width) * (dpiX / 96d));
        double bottom = Math.Ceiling(originY + (y + height) * (dpiY / 96d));
        if (!InRange(left) || !InRange(top) || !InRange(right) || !InRange(bottom) ||
            right - left > int.MaxValue || bottom - top > int.MaxValue) return false;
        result = new((int)left, (int)top, (int)(right - left), (int)(bottom - top));
        return result.IsValid;
    }

    private static bool InRange(double number) => double.IsFinite(number) &&
        number >= int.MinValue && number <= int.MaxValue;
}

public sealed record SpatialEnvironment(string WorkspaceId, SpatialRect WorkArea,
    uint DpiX, uint DpiY, long DisplayGeneration, long ShellGeneration, Guid RuntimeSessionId)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(WorkspaceId) && WorkArea.IsValid &&
        DpiX > 0 && DpiY > 0 && DisplayGeneration >= 0 && ShellGeneration >= 0 && RuntimeSessionId != Guid.Empty;
}

public sealed record SpatialPoster(string PosterId, SpatialRect Bounds, int ExpandedHeight,
    bool IsVisible, bool IsLocked, bool IsCollapsed, long LayoutGeneration,
    string? LastLayoutOperationId = null);

// Only posters assigned to this workspace. The application read set covers the full catalog.
public sealed class SpatialSnapshot
{
    public SpatialEnvironment Environment { get; }
    public IReadOnlyList<SpatialPoster> Posters { get; }
    public SpatialSnapshot(SpatialEnvironment environment, IEnumerable<SpatialPoster> posters)
    {
        Environment = environment;
        Posters = Array.AsReadOnly(posters.OrderBy(p => p.PosterId, StringComparer.Ordinal).ToArray());
    }
}

public enum SpatialFailure
{
    InvalidInput, MissingTarget, HiddenTarget, PermissionDenied, NoSpace,
    OutsideWorkArea, InitialOverlap, PathBlocked, StaleEnvironment,
    MissingProof, ConsumedProof, SourceChanged, TargetChanged, LockedTarget,
    BudgetExceeded, IncompatibleMetrics, InsufficientSpace
}
public sealed record SpatialIssue(SpatialFailure Reason, string? PosterId, string? ObstacleId = null);
public enum SpatialPlanStatus { Accepted, NoChange, Rejected }
public enum SpatialChangeCause { Move, Resize, Expand, Collapse, Push, ConditionalReturn, Placement, Recovery }
public sealed record SpatialChange(string PosterId, SpatialRect Before, SpatialRect After,
    bool WasCollapsed, bool IsCollapsed, SpatialChangeCause Cause);

public sealed record SpatialPathLeg(string PosterId, SpatialRect From, SpatialRect To)
{
    // Expansion-only sampler: down first, then up. No duration or render-frame assumptions.
    public SpatialRect SampleGrowth(int growthPixels)
    {
        int growth = To.Height - From.Height;
        if (growth < 0 || From.X != To.X || From.Width != To.Width ||
            (From.Y != To.Y && From.Bottom != To.Bottom))
            throw new InvalidOperationException("This leg is not an anchored expansion.");
        ArgumentOutOfRangeException.ThrowIfNegative(growthPixels);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(growthPixels, growth);
        return new(From.X, From.Y == To.Y ? From.Y : From.Y - growthPixels,
            From.Width, From.Height + growthPixels);
    }
}

public sealed record SpatialPlan(SpatialPlanStatus Status, IReadOnlyList<SpatialChange> Changes,
    IReadOnlyList<SpatialPathLeg> Path, IReadOnlyList<SpatialIssue> Issues)
{
    public bool Succeeded => Status != SpatialPlanStatus.Rejected;
    public static SpatialPlan Reject(IEnumerable<SpatialIssue> issues) => new(SpatialPlanStatus.Rejected,
        Array.Empty<SpatialChange>(), Array.Empty<SpatialPathLeg>(), Array.AsReadOnly(issues.ToArray()));
    public static SpatialPlan Reject(SpatialFailure reason, string? id = null) => Reject([new(reason, id)]);
    public static SpatialPlan Create(SpatialChange change, IEnumerable<SpatialPathLeg> path) =>
        change.Before == change.After && change.WasCollapsed == change.IsCollapsed
            ? new(SpatialPlanStatus.NoChange, Array.Empty<SpatialChange>(), Array.Empty<SpatialPathLeg>(), Array.Empty<SpatialIssue>())
            : new(SpatialPlanStatus.Accepted, Array.AsReadOnly(new[] { change }),
                Array.AsReadOnly(path.ToArray()), Array.Empty<SpatialIssue>());
}

public static class SpatialPathPolicy
{
    public static bool CrossesMoving(SpatialRect a0, SpatialRect a1, SpatialRect b0, SpatialRect b1)
    {
        decimal lower = 0, upper = 1;
        return Positive(a0.Right - b0.X, a1.Right - b1.X, ref lower, ref upper) &&
            Positive(b0.Right - a0.X, b1.Right - a1.X, ref lower, ref upper) &&
            Positive(a0.Bottom - b0.Y, a1.Bottom - b1.Y, ref lower, ref upper) &&
            Positive(b0.Bottom - a0.Y, b1.Bottom - a1.Y, ref lower, ref upper) && lower < upper;
    }
    // Solve the open interval where all four separating-axis inequalities are positive.
    // This is continuous collision detection for linearly moving/resizing rectangle edges.
    public static bool Crosses(SpatialRect from, SpatialRect to, SpatialRect obstacle)
        => TryContact(from, to, obstacle, out _);

    public static bool TryContact(SpatialRect from, SpatialRect to, SpatialRect obstacle, out decimal fraction)
    {
        if (!from.IsValid || !to.IsValid || !obstacle.IsValid)
            throw new ArgumentException("Continuous path rectangles must be valid.");
        decimal lower = 0, upper = 1;
        bool hit = Positive(from.Right - obstacle.X, to.Right - obstacle.X, ref lower, ref upper) &&
            Positive(obstacle.Right - from.X, obstacle.Right - to.X, ref lower, ref upper) &&
            Positive(from.Bottom - obstacle.Y, to.Bottom - obstacle.Y, ref lower, ref upper) &&
            Positive(obstacle.Bottom - from.Y, obstacle.Bottom - to.Y, ref lower, ref upper) &&
            lower < upper;
        fraction = hit ? lower : 1;
        return hit;
    }

    private static bool Positive(long start, long end, ref decimal lower, ref decimal upper)
    {
        if (start <= 0 && end <= 0) return false;
        if (start > 0 && end > 0) return true;
        decimal root = -(decimal)start / (end - start);
        if (end > start) lower = Math.Max(lower, root);
        else upper = Math.Min(upper, root);
        return lower < upper;
    }
}
