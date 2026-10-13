using DesktopUp.Domain;

namespace DesktopUp.Application;

public interface IUserDataPaths
{
    string Root { get; }
    string Logs { get; }
    string State { get; }
    string Backups { get; }
    string Temp { get; }
    void EnsureCreated();
}

public interface IStructuredLog
{
    void Write(string level, string eventName, object? data = null);
}

public interface IResourceLedger
{
    IReadOnlyCollection<string> ActiveResources { get; }
    IDisposable Acquire(string resourceId, string kind);
    void WriteSnapshot();
}

public interface IStartupIntentSink
{
    void Dispatch(StartupIntent intent);
}

public readonly record struct DesktopShellHostIdentity(
    nint IconViewHandle,
    nint ContainerHandle,
    uint ProcessId)
{
    public bool IsEmpty =>
        IconViewHandle == 0 ||
        ContainerHandle == 0 ||
        ProcessId == 0;
}

public sealed record DesktopShellHostSnapshot(
    DesktopShellHostIdentity Identity,
    long Generation,
    DateTimeOffset StableAtUtc);

public enum DesktopShellProbeState
{
    Missing,
    Candidate,
    Stable,
    Unchanged,
    Invalidated
}

public sealed record DesktopShellProbeResult(
    DesktopShellProbeState State,
    DesktopShellHostSnapshot? Snapshot,
    DesktopShellHostIdentity? Candidate,
    int ConsecutiveSamples);

public interface IDesktopShellLocator
{
    DesktopShellHostSnapshot? Current { get; }
    DesktopShellProbeResult Probe();
    ValueTask<DesktopShellHostSnapshot?> WaitForStableHostAsync(
        CancellationToken cancellationToken = default);
    void Invalidate();
}

public sealed record DesktopLayerAttachResult(
    bool Succeeded,
    nint WindowHandle,
    nint ExpectedOwner,
    nint ActualOwner,
    long DesktopGeneration,
    int NativeError,
    string? Error);

public interface IDesktopLayerAttachment
{
    DesktopLayerAttachResult Attach(
        nint windowHandle,
        DesktopShellHostSnapshot desktopHost,
        bool showWindow = false,
        bool inputTransparent = false);
    bool ReassertDesktopBottom(
        nint windowHandle,
        DesktopShellHostSnapshot desktopHost);
    bool ApplyRoundedRegion(
        nint windowHandle,
        int width,
        int height,
        int cornerRadius);
    bool ApplyPreparedBounds(
        nint windowHandle,
        DesktopPixelRect bounds);
    // Unsupported adapters fail closed; do not emulate a group with visible sequential moves.
    bool ApplyPreparedBoundsBatch(IReadOnlyDictionary<nint, DesktopPixelRect> bounds) => false;
    bool ApplyPreparedBoundsBatch(IReadOnlyDictionary<nint, DesktopPixelRect> bounds, DesktopFramePhase phase) =>
        phase == DesktopFramePhase.Regular && ApplyPreparedBoundsBatch(bounds);
    bool CompletePreparedVisualFrame(IReadOnlyCollection<nint> windows) => false;
    // Only after native capacity and the new visible HRGN are installed. Clears the newly
    // exposed native underlay without synchronously repainting XAML or touching input HWNDs.
    bool ClearExposedVisualBackground(nint window, DesktopRoundedRegion previous, DesktopRoundedRegion current) => false;
    bool VerifyVisualSurface(nint window, DesktopPixelRect surface, DesktopPixelRect visible) => false;
    DesktopVisualSurfaceReadback InspectVisualSurface(nint window, DesktopPixelRect surface, DesktopPixelRect visible) =>
        new(false, "surface-inspection-unsupported");
    bool Detach(nint windowHandle);
    bool IsAttached(nint windowHandle, DesktopShellHostSnapshot desktopHost);
}

public enum DesktopFramePhase { Regular, Animation, FinalRestore, SurfacePreparation }

public readonly record struct DesktopPixelRect(
    int X,
    int Y,
    int Width,
    int Height)
{
    public int Right => checked(X + Width);
    public int Bottom => checked(Y + Height);
    public bool IsUsable => Width > 0 && Height > 0;
}

public sealed record DisplayMonitorSnapshot(
    string DeviceName,
    bool IsPrimary,
    DesktopPixelRect MonitorBounds,
    DesktopPixelRect WorkArea,
    uint DpiX,
    uint DpiY)
{
    public double ScaleX => Math.Max(1, DpiX) / 96d;
    public double ScaleY => Math.Max(1, DpiY) / 96d;
}

public sealed record DisplayTopologySnapshot(
    string Signature,
    IReadOnlyList<DisplayMonitorSnapshot> Monitors,
    long Generation,
    DateTimeOffset StableAtUtc)
{
    public DisplayMonitorSnapshot? PrimaryMonitor =>
        Monitors.FirstOrDefault(static monitor => monitor.IsPrimary) ??
        Monitors.FirstOrDefault();
}

public enum DisplayTopologyProbeState
{
    Missing,
    Candidate,
    Stable,
    Unchanged
}

public sealed record DisplayTopologyProbeResult(
    DisplayTopologyProbeState State,
    DisplayTopologySnapshot? Snapshot,
    string? CandidateSignature,
    int ConsecutiveSamples);

public interface IDisplayTopologyService
{
    DisplayTopologySnapshot? Current { get; }
    DisplayTopologyProbeResult Probe();
    ValueTask<DisplayTopologySnapshot?> WaitForStableTopologyAsync(
        CancellationToken cancellationToken = default);
    bool IsCurrent(DisplayTopologySnapshot snapshot);
}

public readonly record struct PosterPlacementState(
    double WidthDip,
    double HeightDip,
    double HorizontalAnchor,
    double VerticalAnchor);

public readonly record struct DesktopRecoveryGenerationPair(
    long DesktopShellGeneration,
    long DisplayTopologyGeneration);

public enum PosterInputTargetKind
{
    TitleBar = 0,
    EditSurface = 1,
    LockButton = 2,
    CollapseButton = 3,
    Item = 4,
    ResizeRight = 5,
    ResizeBottom = 6,
    ResizeBottomRight = 7,
    ItemScroll = 8,
    MoreButton = 9,
    ResizeLeft = 10,
    ResizeTop = 11,
    ResizeTopLeft = 12,
    ResizeTopRight = 13,
    ResizeBottomLeft = 14,
    ContentItem = 15,
    ContentSpanHandle = 16,
    ContentViewport = 17,
    ContentBrowseNavigation = 18
}

public readonly record struct PosterInputRegion(
    PosterInputTargetKind Kind,
    int Index,
    DesktopPixelRect Bounds)
{
    public string? ContentItemId { get; init; }
    public long ContentSnapshotGeneration { get; init; }
}

public sealed record PosterInputLayoutSnapshot(
    long Revision,
    int FrameWidth,
    int FrameHeight,
    IReadOnlyList<PosterInputRegion> Regions,
    int OuterCornerRadius = 0);

public enum PosterInputEventKind
{
    GesturePreview = 0,
    GestureCompleted = 1,
    GestureCancelled = 2,
    Invoked = 3,
    Scrolled = 4,
    Pressed = 5
}

public sealed record PosterInputEventArgs(
    PosterInputEventKind EventKind,
    PosterInputTargetKind TargetKind,
    int TargetIndex,
    DesktopPixelRect InitialFrame,
    int DeltaX,
    int DeltaY,
    long LayoutRevision,
    int WheelDelta = 0)
{
    public PosterInputSessionKey? Session { get; init; }
    public string? ContentItemId { get; init; }
    public long ContentSnapshotGeneration { get; init; }
    public PosterInputCapture? Capture { get; init; }
    public PointerScreenPoint LatestPoint { get; init; }
    public TitleNativeEndReason EndReason { get; init; } = TitleNativeEndReason.None;
    public long Sequence { get; init; }
    // Set synchronously on content Pressed only. No claim means cancellation, never legacy fallback.
    public bool Handled { get; set; }
}

public static class PosterContentInputPolicy
{
    public static bool IsTarget(PosterInputTargetKind kind) => kind is
        PosterInputTargetKind.ContentItem or PosterInputTargetKind.ContentSpanHandle or PosterInputTargetKind.ContentViewport;

    public static bool CanDrag(PosterInputTargetKind kind) => kind is
        PosterInputTargetKind.ContentItem or PosterInputTargetKind.ContentSpanHandle or PosterInputTargetKind.ContentViewport;

    public static bool HasValidIdentity(PosterInputRegion region) => IsTarget(region.Kind) &&
        region.ContentSnapshotGeneration > 0 &&
        (region.Kind == PosterInputTargetKind.ContentViewport || !string.IsNullOrWhiteSpace(region.ContentItemId));

    // Match the existing native island's five-physical-pixel threshold exactly.
    public static bool CrossedDragThreshold(int deltaX, int deltaY) =>
        ((long)deltaX * deltaX) + ((long)deltaY * deltaY) >= 25;

    public static PosterInputEventArgs Event(PosterInputCapture capture, PosterInputEventKind kind,
        PointerScreenPoint press, PointerScreenPoint latest, long currentLayoutRevision, long sequence,
        TitleNativeEndReason endReason = TitleNativeEndReason.None)
    {
        PosterInputRegion region = capture.Press.Region;
        if (!HasValidIdentity(region)) throw new ArgumentException("A content event requires a stable target and snapshot.", nameof(capture));
        return new(kind, region.Kind, region.Index, capture.Press.Frame,
            latest.X - press.X, latest.Y - press.Y, currentLayoutRevision)
        {
            Session = capture.Press.Session, Capture = capture,
            ContentItemId = region.ContentItemId, ContentSnapshotGeneration = region.ContentSnapshotGeneration,
            LatestPoint = latest, EndReason = endReason, Sequence = sequence
        };
    }
}

public interface IInputRegionHost : IDisposable
{
    nint WindowHandle { get; }
    long AppliedLayoutRevision { get; }
    event EventHandler<PosterInputEventArgs>? Input;
    bool Create(
        DesktopShellHostSnapshot desktopHost,
        DesktopPixelRect frame,
        PosterInputLayoutSnapshot layout);
    bool ApplyLayout(
        DesktopPixelRect frame,
        PosterInputLayoutSnapshot layout);
    bool Reattach(DesktopShellHostSnapshot desktopHost);
    bool ReassertDesktopBottom(DesktopShellHostSnapshot desktopHost);
    bool IsAttached(DesktopShellHostSnapshot desktopHost);
}
