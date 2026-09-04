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
    SetCollapsed = 3
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
    PosterDefinition DesiredState);

public sealed record SettingsStateCommand(
    string RequestId,
    StateCommandSource Source,
    SettingsStateCommandKind Kind,
    long ExpectedRevision,
    DesktopUpSettings DesiredState);

public enum StateCommandStatus
{
    Committed,
    NoChange,
    Replayed,
    Conflict,
    PermissionDenied,
    ValidationFailed,
    NotReady,
    Failed
}

public sealed record StateCommandResult(
    StateCommandStatus Status,
    string RequestId,
    long ExpectedRevision,
    long CurrentRevision,
    DesktopUpProjection? Projection,
    string? Error)
{
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
    bool CanToggleCollapsed);

public sealed record SettingsProjection(
    long Revision,
    long SettingsRevision,
    DesktopUpSettings Settings);

public sealed record ControlCenterProjection(
    long Revision,
    PosterSurfaceProjection Poster,
    SettingsProjection Settings);

public sealed record TrayProjection(
    long Revision,
    int PosterCount,
    bool AllPostersLocked,
    bool DynamicEffectsPaused,
    string ToolTipText);

public sealed record DesktopUpProjection(
    long Revision,
    PosterSurfaceProjection Poster,
    ControlCenterProjection ControlCenter,
    TrayProjection Tray);

public sealed class StateProjectionChangedEventArgs(
    DesktopUpProjection projection,
    StateCommandSource source,
    string requestId) : EventArgs
{
    public DesktopUpProjection Projection { get; } = projection;
    public StateCommandSource Source { get; } = source;
    public string RequestId { get; } = requestId;
}

public interface IStateProjectionCoordinator
{
    event EventHandler<StateProjectionChangedEventArgs>? ProjectionChanged;

    DesktopUpProjection EnsureInitialized(PosterDefinition defaultPoster);
    bool TryGetCurrent(out DesktopUpProjection? projection);
}

public interface IStateCommandService
{
    StateCommandResult Execute(PosterStateCommand command);
    StateCommandResult Execute(SettingsStateCommand command);
}
