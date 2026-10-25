using DesktopUp.Application;

namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    private AutoPostureRequest? _automatic;
    private string? _automaticCommand;
    private AutoPostureOutcome? _automaticCancellation;
    public event Action? ActivityChanged;
    public event Action<string, bool?>? ManualIntent;

    public void BeforeManualIntent(string posterId, bool? collapse = null)
    {
        if (collapse is null && _pendingPostures.Remove(posterId, out var pending))
        {
            if (_windows.TryGetValue(posterId, out var window)) window.ShowPosturePending(false);
            pending.Completed(Failure(pending.Command, "新的直接操作取代等待中的收展。"));
        }
        if (!HasCenterDraft(posterId) && _windows.TryGetValue(posterId, out var target)) target.ClearCenterPresentation();
        ManualIntent?.Invoke(posterId, collapse);
        if (_automatic is { } active && active.Session.PosterId == posterId) CancelAutomatic(active);
    }

    public void StartAutomatic(AutoPostureRequest request, Action<AutoPostureOutcome> completed)
    {
        if (HasCenterDraft(request.Session.PosterId)) { completed(AutoPostureOutcome.Cancelled); return; }
        if (_suspended || IsSpatialBusy || !_windows.TryGetValue(request.Session.PosterId, out var window) ||
            window.ReadPointerTarget().Session != request.Session ||
            !state.TryGetCurrent(out var current) || current?.PostersById.GetValueOrDefault(request.Session.PosterId) is not { } target)
        { completed(AutoPostureOutcome.HostUnavailable); return; }
        if (target.EntityVersion != request.ExpectedEntityRevision || target.AutoPosture != request.Policy ||
        !target.AutoPosture.Enabled || !target.IsEffectivelyVisible || target.Poster.IsCollapsed == request.Collapse)
        { completed(AutoPostureOutcome.Conflict); return; }
        _automatic = request;
        _automaticCancellation = null;
        _automaticCommand = $"auto-{_runtimeId:N}-{request.Id}";
        var command = new PosterStateCommand(_automaticCommand, StateCommandSource.System,
            PosterStateCommandKind.SetCollapsed, current.Revision, request.Session.PosterId,
            target.Poster with { IsCollapsed = request.Collapse })
            { ReadSet = StateReadSet.ForPoster(request.Session.PosterId, target.EntityVersion) };
        void Finish(StateCommandResult result)
        {
            if (_automatic != request) return;
            var outcome = _automaticCancellation ?? (result.Succeeded ? AutoPostureOutcome.Committed : result.Status switch
            {
                StateCommandStatus.InsufficientSpace => AutoPostureOutcome.InsufficientSpace,
                StateCommandStatus.Conflict => AutoPostureOutcome.Conflict,
                _ => AutoPostureOutcome.HostUnavailable
            });
            _automatic = null; _automaticCommand = null; _automaticCancellation = null;
            completed(outcome);
        }
        try { SetCollapsed(command, Finish); }
        catch (Exception exception)
        {
            log.Write("error", "auto_posture.start_failed", new { request.Id, exception.Message });
            if (_postures.GetValueOrDefault(command.RequestId) is { } run) CancelPosture("automatic-start-failed", specific: run);
            else Finish(Failure(command, exception.Message));
        }
    }

    public void CancelAutomatic(AutoPostureRequest request)
    {
        if (_automatic != request) return;
        _automaticCancellation = AutoPostureOutcome.Cancelled;
        if (_automaticCommand is not null && _postures.GetValueOrDefault(_automaticCommand) is { } run)
            CancelPosture("automatic-invalidated", specific: run);
    }

    private StateCommandResult CommitPosture(PosterStateCommand command) => command.ContentHeatExpansion is not null
        ? commands is IContentHeatExpansionCommitter heat ? heat.ExecuteContentHeatExpansion(command)
            : Failure(command, "Activity expansion commit port is unavailable.")
        : command.Source == StateCommandSource.System
        ? commands is IAutomaticPostureCommitter automatic ? automatic.ExecuteAutomaticPosture(command)
            : Failure(command, "Automatic posture commit port is unavailable.")
        : commands.Execute(command);
}
