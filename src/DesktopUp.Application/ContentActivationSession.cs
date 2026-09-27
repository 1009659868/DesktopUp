namespace DesktopUp.Application;

/// <summary>Transient identity for one presented item. The witness describes effective geometry and
/// its author/environment basis, not the input-table revision produced by selection paint.</summary>
public sealed record ContentActivationTarget(string PosterId, string ItemId, long ItemSetVersion,
    long DocumentVersion, string PresentationWitness);

/// <summary>System policy supplied by the native adapter. Tolerances are maximum absolute physical
/// coordinate differences, not the full width/height of the system double-click rectangle.</summary>
public sealed record ContentDoubleClickSettings(long MaximumIntervalMilliseconds,
    int HorizontalTolerancePixels, int VerticalTolerancePixels);

public enum ContentOpenOrigin { DoubleClick, Enter }
public sealed record ContentOpenIntent(ContentActivationTarget Target, Guid ActivationId, ContentOpenOrigin Origin);

/// <summary>UI-thread-owned input arbitration only: no timer, persistence, focus acquisition or target
/// execution. Call Click only for completed, non-drag item clicks; cancel/drag/invalidation calls Reset.
/// The adapter supplies a monotonic timestamp and the native capture identity for each physical click.</summary>
public sealed class ContentActivationSession
{
    private const int RecentClickCapacity = 32;
    private sealed record PendingClick(ContentActivationTarget Target, long TimestampMilliseconds, PointerScreenPoint Point);
    private readonly Queue<Guid> _recentClickOrder = new();
    private readonly HashSet<Guid> _recentClickIds = new();
    private PendingClick? _pendingClick;
    private long _latestClickTime = -1;
    private bool _enterHeld;

    public ContentOpenIntent? Click(ContentActivationTarget target, Guid clickId, long timestampMilliseconds,
        PointerScreenPoint point, ContentDoubleClickSettings settings)
    {
        try
        {
            ValidateTarget(target);
            if (clickId == Guid.Empty) throw new ArgumentException("A physical click requires its capture identity.", nameof(clickId));
            if (timestampMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(timestampMilliseconds));
            if (settings is null || settings.MaximumIntervalMilliseconds <= 0 ||
                settings.HorizontalTolerancePixels < 0 || settings.VerticalTolerancePixels < 0)
                throw new ArgumentException("Double-click timing and physical tolerances must be supplied explicitly.", nameof(settings));
        }
        catch (ArgumentException)
        {
            _pendingClick = null;
            throw;
        }

        // Repeated release delivery is not a second click. A late older event cannot displace a
        // newer pending pair. The small token window is transient and bounded; native routing still
        // owns rejection of events for ended captures or obsolete surfaces.
        if (_recentClickIds.Contains(clickId) || timestampMilliseconds < _latestClickTime) return null;
        _latestClickTime = timestampMilliseconds;
        _recentClickIds.Add(clickId);
        _recentClickOrder.Enqueue(clickId);
        if (_recentClickOrder.Count > RecentClickCapacity) _recentClickIds.Remove(_recentClickOrder.Dequeue());

        if (_pendingClick is { } previous && previous.Target == target &&
            timestampMilliseconds - previous.TimestampMilliseconds <= settings.MaximumIntervalMilliseconds &&
            Math.Abs((long)point.X - previous.Point.X) <= settings.HorizontalTolerancePixels &&
            Math.Abs((long)point.Y - previous.Point.Y) <= settings.VerticalTolerancePixels)
        {
            _pendingClick = null;
            return new(target, Guid.NewGuid(), ContentOpenOrigin.DoubleClick);
        }

        // A different identity, expired time or distant click begins a fresh pair; it never opens
        // either the previous item or the new item on its own.
        _pendingClick = new(target, timestampMilliseconds, point);
        return null;
    }

    public ContentOpenIntent? KeyDown(ContentActivationTarget target, bool hasItemFocus, bool isRepeat = false)
    {
        if (!hasItemFocus) { LostFocus(); return null; }
        _pendingClick = null;
        if (_enterHeld) return null;
        _enterHeld = true;
        if (isRepeat) return null;
        ValidateTarget(target);
        return new(target, Guid.NewGuid(), ContentOpenOrigin.Enter);
    }

    public void KeyUp() => _enterHeld = false;
    public void LostFocus() => Reset();

    /// <summary>Ends pairing/key ownership. Recent consumed click IDs and the monotonic clock witness
    /// remain so a delayed duplicate completion cannot resurrect a cancelled interaction.</summary>
    public void Reset() { _pendingClick = null; _enterHeld = false; }

    private static void ValidateTarget(ContentActivationTarget target)
    {
        if (target is null || string.IsNullOrWhiteSpace(target.PosterId) || string.IsNullOrWhiteSpace(target.ItemId) ||
            target.ItemSetVersion < 1 || target.DocumentVersion < 1 || string.IsNullOrWhiteSpace(target.PresentationWitness))
            throw new ArgumentException("An opening intent requires the current item identity and presentation witness.", nameof(target));
    }
}
