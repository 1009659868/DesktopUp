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
        if (!HasCenterDraft(posterId) && _windows.TryGetValue(posterId, out var target)) target.ClearCenterPresentation();
        ManualIntent?.Invoke(posterId, collapse);
        if (_automatic is { } active) CancelAutomatic(active);
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
            if (_posture?.Command.RequestId == command.RequestId) CancelPosture("automatic-start-failed");
            else Finish(Failure(command, exception.Message));
        }
    }

    public void CancelAutomatic(AutoPostureRequest request)
    {
        if (_automatic != request) return;
        _automaticCancellation = AutoPostureOutcome.Cancelled;
        if (_posture?.Command.RequestId == _automaticCommand) CancelPosture("automatic-invalidated");
    }

    private StateCommandResult CommitPosture(PosterStateCommand command) => command.Source == StateCommandSource.System
        ? commands is IAutomaticPostureCommitter automatic ? automatic.ExecuteAutomaticPosture(command)
            : Failure(command, "Automatic posture commit port is unavailable.")
        : commands.Execute(command);
}
