using DesktopUp.Domain;

namespace DesktopUp.Application;

public sealed class StateProjectionCoordinator(
    IPosterRepository posterRepository,
    ISettingsRepository settingsRepository,
    IStateProjectionRepository projectionRepository,
    IStructuredLog? log = null) : IStateProjectionCoordinator, IStateCommandService
{
    private readonly object _sync = new();
    private readonly Dictionary<string, StateCommandResult> _completedRequests =
        new(StringComparer.Ordinal);
    private readonly Queue<string> _completedRequestOrder = new();
    private DesktopUpProjection? _current;
    private const int CompletedRequestLimit = 512;

    public event EventHandler<StateProjectionChangedEventArgs>? ProjectionChanged;

    public DesktopUpProjection EnsureInitialized(PosterDefinition defaultPoster)
    {
        ArgumentNullException.ThrowIfNull(defaultPoster);
        StateProjectionChangedEventArgs? changed = null;
        DesktopUpProjection projection;
        lock (_sync)
        {
            _ = posterRepository.GetOrCreateDefault(defaultPoster);
            projection = BuildProjection(
                projectionRepository.Read(defaultPoster.PosterId));
            if (_current is null || projection.Revision > _current.Revision)
            {
                _current = projection;
                changed = new StateProjectionChangedEventArgs(
                    projection,
                    StateCommandSource.System,
                    "initialize");
            }
            else
            {
                projection = _current;
            }
        }

        Publish(changed);
        return projection;
    }

    public bool TryGetCurrent(out DesktopUpProjection? projection)
    {
        lock (_sync)
        {
            projection = _current;
            return projection is not null;
        }
    }

    public StateCommandResult Execute(PosterStateCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        StateProjectionChangedEventArgs? changed = null;
        StateCommandResult result;
        lock (_sync)
        {
            if (!TryReplayLocked(command.RequestId, command.ExpectedRevision, out result))
            {
                result = ExecutePosterLocked(command, out changed);
                RememberLocked(result);
            }
        }

        Publish(changed);
        return result;
    }

    public StateCommandResult Execute(SettingsStateCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        StateProjectionChangedEventArgs? changed = null;
        StateCommandResult result;
        lock (_sync)
        {
            if (!TryReplayLocked(command.RequestId, command.ExpectedRevision, out result))
            {
                result = ExecuteSettingsLocked(command, out changed);
                RememberLocked(result);
            }
        }

        Publish(changed);
        return result;
    }

    private StateCommandResult ExecutePosterLocked(
        PosterStateCommand command,
        out StateProjectionChangedEventArgs? changed)
    {
        changed = null;
        StateCommandResult? rejected = ValidateCommon(
            command.RequestId,
            command.Source,
            command.ExpectedRevision);
        if (rejected is not null)
        {
            return rejected;
        }
        if (_current is null)
        {
            return NotReady(command.RequestId, command.ExpectedRevision);
        }
        if (command.ExpectedRevision != _current.Revision)
        {
            return Conflict(command.RequestId, command.ExpectedRevision, _current);
        }
        PosterDefinition current = _current.Poster.Poster;
        if (!string.Equals(command.PosterId, current.PosterId, StringComparison.Ordinal) ||
            !string.Equals(command.DesiredState.PosterId, current.PosterId, StringComparison.Ordinal))
        {
            return Failed(
                command.RequestId,
                command.ExpectedRevision,
                "The command target does not match the projected poster.");
        }
        PosterDefinition desired = NormalizePosterMutation(
            command.Kind,
            current,
            command.DesiredState);
        PosterStateCommand normalizedCommand = command with { DesiredState = desired };
        if (desired == current)
        {
            return NoChange(command.RequestId, command.ExpectedRevision, _current);
        }

        string? mutationError = ValidatePosterMutation(normalizedCommand, current);
        if (mutationError is not null)
        {
            return new StateCommandResult(
                StateCommandStatus.PermissionDenied,
                command.RequestId,
                command.ExpectedRevision,
                _current.Revision,
                _current,
                mutationError);
        }

        RepositoryWriteResult<PosterStateSnapshot> write = posterRepository.Save(
            command.ExpectedRevision,
            command.RequestId,
            desired);
        DesktopUpProjection? committedFallback =
            write.Succeeded && write.Snapshot is { } posterSnapshot
                ? BuildProjection(new AuthoritativeStateSnapshot(
                    posterSnapshot.Revision,
                    posterSnapshot.PosterRevision,
                    _current.ControlCenter.Settings.SettingsRevision,
                    posterSnapshot.Poster,
                    _current.ControlCenter.Settings.Settings,
                    posterSnapshot.UpdatedAtUtc,
                    DateTimeOffset.UtcNow))
                : null;
        return CompleteWriteLocked(
            command.RequestId,
            command.ExpectedRevision,
            command.Source,
            write.Status,
            write.Error,
            current.PosterId,
            committedFallback,
            out changed);
    }

    private StateCommandResult ExecuteSettingsLocked(
        SettingsStateCommand command,
        out StateProjectionChangedEventArgs? changed)
    {
        changed = null;
        StateCommandResult? rejected = ValidateCommon(
            command.RequestId,
            command.Source,
            command.ExpectedRevision);
        if (rejected is not null)
        {
            return rejected;
        }
        if (_current is null)
        {
            return NotReady(command.RequestId, command.ExpectedRevision);
        }
        if (command.ExpectedRevision != _current.Revision)
        {
            return Conflict(command.RequestId, command.ExpectedRevision, _current);
        }
        DesktopUpSettings current = _current.ControlCenter.Settings.Settings;
        if (command.DesiredState == current)
        {
            return NoChange(command.RequestId, command.ExpectedRevision, _current);
        }

        string? mutationError = ValidateSettingsMutation(command, current);
        if (mutationError is not null)
        {
            return new StateCommandResult(
                StateCommandStatus.PermissionDenied,
                command.RequestId,
                command.ExpectedRevision,
                _current.Revision,
                _current,
                mutationError);
        }

        RepositoryWriteResult<SettingsStateSnapshot> write = settingsRepository.Save(
            command.ExpectedRevision,
            command.RequestId,
            command.DesiredState);
        DesktopUpProjection? committedFallback =
            write.Succeeded && write.Snapshot is { } settingsSnapshot
                ? BuildProjection(new AuthoritativeStateSnapshot(
                    settingsSnapshot.Revision,
                    _current.Poster.PosterRevision,
                    settingsSnapshot.SettingsRevision,
                    _current.Poster.Poster,
                    settingsSnapshot.Settings,
                    DateTimeOffset.UtcNow,
                    settingsSnapshot.UpdatedAtUtc))
                : null;
        return CompleteWriteLocked(
            command.RequestId,
            command.ExpectedRevision,
            command.Source,
            write.Status,
            write.Error,
            _current.Poster.Poster.PosterId,
            committedFallback,
            out changed);
    }

    private StateCommandResult CompleteWriteLocked(
        string requestId,
        long expectedRevision,
        StateCommandSource source,
        RepositoryWriteStatus repositoryStatus,
        string? error,
        string posterId,
        DesktopUpProjection? committedFallback,
        out StateProjectionChangedEventArgs? changed)
    {
        changed = null;
        DesktopUpProjection? refreshed = null;
        try
        {
            AuthoritativeStateSnapshot aggregate = projectionRepository.Read(posterId);
            DesktopUpProjection candidate = BuildProjection(aggregate);
            if (_current is null || candidate.Revision >= _current.Revision)
            {
                refreshed = candidate;
                if (_current is null || candidate.Revision > _current.Revision)
                {
                    _current = candidate;
                    changed = new StateProjectionChangedEventArgs(
                        candidate,
                        source,
                        requestId);
                }
            }
        }
        catch (Exception exception)
        {
            error = error is null
                ? exception.Message
                : $"{error} Projection refresh failed: {exception.Message}";
        }

        if (refreshed is null && committedFallback is not null)
        {
            refreshed = committedFallback;
            if (_current is null || committedFallback.Revision > _current.Revision)
            {
                _current = committedFallback;
                changed = new StateProjectionChangedEventArgs(
                    committedFallback,
                    source,
                    requestId);
            }
        }

        DesktopUpProjection? projection = refreshed ?? _current;
        StateCommandStatus status = repositoryStatus switch
        {
            RepositoryWriteStatus.Committed => StateCommandStatus.Committed,
            RepositoryWriteStatus.Replayed => StateCommandStatus.Replayed,
            RepositoryWriteStatus.Conflict => StateCommandStatus.Conflict,
            RepositoryWriteStatus.ValidationFailed => StateCommandStatus.ValidationFailed,
            _ => StateCommandStatus.Failed
        };
        long currentRevision = projection?.Revision ?? -1;
        log?.Write(
            status is StateCommandStatus.Committed or StateCommandStatus.Replayed
                ? "info"
                : "warning",
            "state_command.completed",
            new
            {
                requestId,
                source = source.ToString(),
                status = status.ToString(),
                expectedRevision,
                currentRevision,
                error
            });
        return new StateCommandResult(
            status,
            requestId,
            expectedRevision,
            currentRevision,
            projection,
            error);
    }

    private StateCommandResult? ValidateCommon(
        string requestId,
        StateCommandSource source,
        long expectedRevision)
    {
        if (string.IsNullOrWhiteSpace(requestId) || requestId.Length > 256)
        {
            return Failed(requestId ?? string.Empty, expectedRevision, "Request id is invalid.");
        }
        if (!Enum.IsDefined(source) || source == StateCommandSource.System)
        {
            return Failed(requestId, expectedRevision, "Command source is invalid.");
        }
        if (expectedRevision < 0)
        {
            return Failed(requestId, expectedRevision, "Expected revision is invalid.");
        }
        return null;
    }

    private bool TryReplayLocked(
        string requestId,
        long expectedRevision,
        out StateCommandResult result)
    {
        if (!string.IsNullOrWhiteSpace(requestId) &&
            _completedRequests.TryGetValue(requestId, out StateCommandResult? previous))
        {
            result = previous with
            {
                Status = StateCommandStatus.Replayed,
                ExpectedRevision = expectedRevision,
                CurrentRevision = _current?.Revision ?? previous.CurrentRevision,
                Projection = _current ?? previous.Projection
            };
            return true;
        }
        result = null!;
        return false;
    }

    private void RememberLocked(StateCommandResult result)
    {
        if (!result.Succeeded ||
            result.Status == StateCommandStatus.Replayed ||
            string.IsNullOrWhiteSpace(result.RequestId) ||
            _completedRequests.ContainsKey(result.RequestId))
        {
            return;
        }
        _completedRequests.Add(result.RequestId, result);
        _completedRequestOrder.Enqueue(result.RequestId);
        while (_completedRequestOrder.Count > CompletedRequestLimit)
        {
            string expired = _completedRequestOrder.Dequeue();
            _completedRequests.Remove(expired);
        }
    }

    private static string? ValidatePosterMutation(
        PosterStateCommand command,
        PosterDefinition current)
    {
        if (!Enum.IsDefined(command.Kind))
        {
            return "Poster command kind is invalid.";
        }
        if (command.Kind is PosterStateCommandKind.Move or PosterStateCommandKind.Resize)
        {
            if (current.IsLocked)
            {
                return "The poster is locked.";
            }
            if (command.Kind == PosterStateCommandKind.Resize && current.IsCollapsed)
            {
                return "A collapsed poster cannot be resized.";
            }
            return PreserveNonGeometryFields(current, command.DesiredState)
                ? null
                : "A geometry command changed non-geometry fields.";
        }
        if (command.Kind == PosterStateCommandKind.SetLocked)
        {
            return current with { IsLocked = command.DesiredState.IsLocked } ==
                command.DesiredState
                ? null
                : "A lock command changed unrelated fields.";
        }
        return current with
        {
            IsCollapsed = command.DesiredState.IsCollapsed,
            VerticalAnchor = command.DesiredState.VerticalAnchor
        } ==
            command.DesiredState
            ? null
            : "A collapse command changed unrelated fields.";
    }

    private static PosterDefinition NormalizePosterMutation(
        PosterStateCommandKind kind,
        PosterDefinition current,
        PosterDefinition desired)
    {
        if (kind != PosterStateCommandKind.SetCollapsed ||
            desired.IsCollapsed == current.IsCollapsed ||
            current.NormalizedExpandedHeight <= 0)
        {
            return desired;
        }

        double workHeightDip =
            current.ExpandedHeightDip / current.NormalizedExpandedHeight;
        if (!double.IsFinite(workHeightDip) || workHeightDip <= 0)
        {
            return desired;
        }
        double collapsedHeightNormalized = Math.Clamp(
            PosterFrameGeometryPolicy.CollapsedHeightDip / workHeightDip,
            0.000001,
            1);
        double currentHeightNormalized = current.IsCollapsed
            ? collapsedHeightNormalized
            : current.NormalizedExpandedHeight;
        double targetHeightNormalized = desired.IsCollapsed
            ? collapsedHeightNormalized
            : current.NormalizedExpandedHeight;
        double normalizedTop = current.VerticalAnchor *
            Math.Max(0, 1 - currentHeightNormalized);
        double targetRange = Math.Max(0.000001, 1 - targetHeightNormalized);
        return desired with
        {
            VerticalAnchor = Math.Clamp(normalizedTop / targetRange, 0, 1)
        };
    }

    private static bool PreserveNonGeometryFields(
        PosterDefinition current,
        PosterDefinition desired) =>
        current with
        {
            MonitorDeviceName = desired.MonitorDeviceName,
            FallbackXDip = desired.FallbackXDip,
            FallbackYDip = desired.FallbackYDip,
            WidthDip = desired.WidthDip,
            ExpandedHeightDip = desired.ExpandedHeightDip,
            HorizontalAnchor = desired.HorizontalAnchor,
            VerticalAnchor = desired.VerticalAnchor,
            NormalizedWidth = desired.NormalizedWidth,
            NormalizedExpandedHeight = desired.NormalizedExpandedHeight
        } == desired;

    private static string? ValidateSettingsMutation(
        SettingsStateCommand command,
        DesktopUpSettings current)
    {
        if (!Enum.IsDefined(command.Kind))
        {
            return "Settings command kind is invalid.";
        }
        DesktopUpSettings allowed = command.Kind switch
        {
            SettingsStateCommandKind.SetDynamicEffectsPaused => current with
            {
                DynamicEffectsPaused = command.DesiredState.DynamicEffectsPaused
            },
            SettingsStateCommandKind.SetPerformanceMode => current with
            {
                PerformanceMode = command.DesiredState.PerformanceMode
            },
            _ => current with
            {
                LaunchAtStartupRequested = command.DesiredState.LaunchAtStartupRequested
            }
        };
        return allowed == command.DesiredState
            ? null
            : "A settings command changed unrelated fields.";
    }

    private StateCommandResult Failed(
        string requestId,
        long expectedRevision,
        string error) => new(
            StateCommandStatus.Failed,
            requestId,
            expectedRevision,
            _current?.Revision ?? -1,
            _current,
            error);

    private static StateCommandResult NotReady(
        string requestId,
        long expectedRevision) => new(
            StateCommandStatus.NotReady,
            requestId,
            expectedRevision,
            -1,
            null,
            "The state projection is not ready.");

    private static StateCommandResult Conflict(
        string requestId,
        long expectedRevision,
        DesktopUpProjection current) => new(
            StateCommandStatus.Conflict,
            requestId,
            expectedRevision,
            current.Revision,
            current,
            "The expected revision is stale.");

    private static StateCommandResult NoChange(
        string requestId,
        long expectedRevision,
        DesktopUpProjection current) => new(
            StateCommandStatus.NoChange,
            requestId,
            expectedRevision,
            current.Revision,
            current,
            null);

    private static DesktopUpProjection BuildProjection(
        AuthoritativeStateSnapshot state)
    {
        var poster = new PosterSurfaceProjection(
            state.Revision,
            state.PosterRevision,
            state.Poster,
            CanMove: !state.Poster.IsLocked,
            CanResize: !state.Poster.IsLocked && !state.Poster.IsCollapsed,
            CanToggleCollapsed: true);
        var settings = new SettingsProjection(
            state.Revision,
            state.SettingsRevision,
            state.Settings);
        var control = new ControlCenterProjection(
            state.Revision,
            poster,
            settings);
        string tooltip = $"DesktopUp · 1 张海报" +
            (state.Settings.DynamicEffectsPaused ? " · 已暂停" : string.Empty) +
            (state.Poster.IsLocked ? " · 已锁定" : string.Empty);
        var tray = new TrayProjection(
            state.Revision,
            PosterCount: 1,
            AllPostersLocked: state.Poster.IsLocked,
            state.Settings.DynamicEffectsPaused,
            tooltip);
        return new DesktopUpProjection(
            state.Revision,
            poster,
            control,
            tray);
    }

    private void Publish(StateProjectionChangedEventArgs? changed)
    {
        if (changed is null)
        {
            return;
        }
        log?.Write("info", "state_projection.published", new
        {
            changed.Projection.Revision,
            source = changed.Source.ToString(),
            changed.RequestId,
            posterRevision = changed.Projection.Poster.PosterRevision,
            settingsRevision = changed.Projection.ControlCenter.Settings.SettingsRevision
        });
        ProjectionChanged?.Invoke(this, changed);
    }
}
