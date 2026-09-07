using DesktopUp.Domain;

namespace DesktopUp.Application;

public enum StateCommandSource
{
    System = 0,
    Poster = 1,
    ControlCenter = 2,
    Tray = 3
}

public enum PosterStateCommandKind
{
    Move = 0,
    Resize = 1,
    SetLocked = 2,
    SetCollapsed = 3,
    SetVisible = 4,
    Rename = 5
}

public enum SettingsStateCommandKind
{
    SetDynamicEffectsPaused = 0,
    SetPerformanceMode = 1,
    SetLaunchAtStartupRequested = 2
}

public sealed record PosterStateCommand(
    string RequestId,
    StateCommandSource Source,
    PosterStateCommandKind Kind,
    long ExpectedRevision,
    string PosterId,
    PosterDefinition DesiredState)
{
    // Null retains the old global-CAS contract for compatibility callers only.
    public StateReadSet? ReadSet { get; init; }
}

public sealed record SettingsStateCommand(
    string RequestId,
    StateCommandSource Source,
    SettingsStateCommandKind Kind,
    long ExpectedRevision,
    DesktopUpSettings DesiredState)
{
    public StateReadSet? ReadSet { get; init; }
}

public enum StateCommandStatus
{
    Committed,
    NoChange,
    Replayed,
    Conflict,
    PermissionDenied,
    ValidationFailed,
    NotReady,
    Failed,
    NotFound,
    ConfirmationRequired
}

public sealed record StateCommandResult(
    StateCommandStatus Status,
    string RequestId,
    long ExpectedRevision,
    long CurrentRevision,
    DesktopUpProjection? Projection,
    string? Error)
{
    public long? CommittedRevision { get; init; }
    public string? ResultPosterId { get; init; }
    public IReadOnlyList<StateReadConflict> Conflicts { get; init; } = Array.Empty<StateReadConflict>();
    public bool Succeeded => Status is
        StateCommandStatus.Committed or
        StateCommandStatus.NoChange or
        StateCommandStatus.Replayed;
}

public sealed record PosterSurfaceProjection(
    long Revision,
    long PosterRevision,
    PosterDefinition Poster,
    bool CanMove,
    bool CanResize,
    bool CanToggleCollapsed)
{
    public long EntityVersion { get; init; }
}

public sealed record SettingsProjection(
    long Revision,
    long SettingsRevision,
    DesktopUpSettings Settings)
{
    public long EntityVersion { get; init; }
}

public sealed record ControlCenterProjection(
    long Revision,
    PosterSurfaceProjection? Poster,
    SettingsProjection Settings)
{
    public IReadOnlyList<PosterSurfaceProjection> Posters { get; init; } = Array.Empty<PosterSurfaceProjection>();
    public IReadOnlyDictionary<string, PosterSurfaceProjection> PostersById { get; init; } =
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, PosterSurfaceProjection>(
            new Dictionary<string, PosterSurfaceProjection>());
    // Legacy catalog alias, NOT the control center's UI selection or a command target.
    public string? SelectedPosterId => Poster?.Poster.PosterId;
    public long CatalogVersion { get; init; }
}

public enum PosterLockSummary { Empty, None, Some, All }

public sealed record TrayProjection(
    long Revision,
    int PosterCount,
    bool AllPostersLocked,
    bool DynamicEffectsPaused,
    string ToolTipText)
{
    public int VisiblePosterCount { get; init; }
    public int LockedPosterCount { get; init; }
    public PosterLockSummary LockSummary { get; init; }
}

public sealed record DesktopUpProjection(
    long Revision,
    PosterSurfaceProjection? Poster,
    ControlCenterProjection ControlCenter,
    TrayProjection Tray)
{
    public IReadOnlyDictionary<string, PosterSurfaceProjection> PostersById => ControlCenter.PostersById;
    public IReadOnlyList<PosterSurfaceProjection> Posters => ControlCenter.Posters;
    public long CatalogVersion => ControlCenter.CatalogVersion;
    public bool ProfileInitialized { get; init; }
}

public sealed class StateProjectionChangedEventArgs(
    DesktopUpProjection projection,
    StateCommandSource source,
    string requestId) : EventArgs
{
    public DesktopUpProjection Projection { get; } = projection;
    public StateCommandSource Source { get; } = source;
    public string RequestId { get; } = requestId;
    public IReadOnlyList<string> ChangedPosterIds { get; init; } = Array.Empty<string>();
    public bool SettingsChanged { get; init; }
    public bool CatalogChanged { get; init; }
}

public interface IStateProjectionCoordinator
{
    event EventHandler<StateProjectionChangedEventArgs>? ProjectionChanged;

    DesktopUpProjection EnsureInitialized(PosterDefinition defaultPoster);
    bool TryGetCurrent(out DesktopUpProjection? projection);
    DesktopUpProjection Refresh();
    PosterRemovalPreview? PrepareRemoval(string posterId);
}

public interface IStateCommandService
{
    StateCommandResult Execute(PosterStateCommand command);
    StateCommandResult Execute(SettingsStateCommand command);
    StateCommandResult Execute(CreatePosterCommand command);
    StateCommandResult Execute(ClonePosterCommand command);
    StateCommandResult Execute(RemovePosterCommand command);
    StateCommandResult Execute(BatchPosterCommand command);
}
