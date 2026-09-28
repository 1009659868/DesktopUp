using DesktopUp.Application;

namespace DesktopUp;

internal sealed partial class ControlCenterService(
    IResourceLedger ledger,
    IStructuredLog log,
    Action<PosterStateCommand> posterCommand,
    Action toggleDynamicEffectsPaused,
    IStateProjectionCoordinator state, IStateCommandService commands, PosterInputRouter inputRouter,
    Func<PosterSurfaceProjection, EffectivePosterLayout?> effectiveLayout,
    Func<PosterSpatialRuntime?>? spatialRuntime = null, PortableProfileController? portable = null) : IDisposable
{
    private ControlCenterWindow? _window;
    private IDisposable? _lease;
    private ControlCenterProjection? _projection;
    private readonly ControlCenterSelection _selection = new();
    private IReadOnlyList<PosterSessionSnapshot> _sessions = Array.Empty<PosterSessionSnapshot>();
    private bool _environmentPending = true;
    private bool _globallyHidden;
    private string? _commandError;
    private readonly PosterRemovalConfirmation _removal = new();
    private bool _removalShowing;
    private bool _disposed;
    private IDisposable? _removalLease;
    public string? SelectedPosterId => _selection.SelectedPosterId;
    public Task CancelPortableAndWaitAsync() => _window?.CancelPortableAndWaitAsync() ?? Task.CompletedTask;
    internal ControlCenterWindow? WindowForPortableProbe => _window;
    // Includes async show/dismiss: cancelling the ticket is not proof the dialog has closed.
    public bool IsRemovalInteractionActive => _removalShowing;
    public event Action? RemovalInteractionChanged;
    public bool IsSettingsInteractionActive => _window?.IsSettingsInteractionActive == true;
    internal bool HasPosterDraft(string id) => _window?.HasPosterDraft(id) == true;
    internal bool OwnsContentFieldDraft(string id, long entityVersion, long documentVersion) =>
        _window?.OwnsContentFieldDraft(id, entityVersion, documentVersion) == true;
    public event Action? SettingsInteractionChanged;
    private void OnSettingsInteractionChanged() => SettingsInteractionChanged?.Invoke();

    public void Show()
    {
        if (_disposed) return;
        ControlCenterWindow? pendingWindow = null;
        string stage = "activate-existing";
        try
        {
            if (_window is null)
            {
                stage = "construct-window";
                pendingWindow = new ControlCenterWindow(
                    SelectPoster,
                    posterCommand,
                    toggleDynamicEffectsPaused, RequestRemoval, effectiveLayout);
                stage = "load-xaml";
                pendingWindow.InitializeView();
                pendingWindow.ConfigureEditing(() => state.TryGetCurrent(out var p) ? p : null,
                    id => spatialRuntime?.Invoke()?.ReadControlCenterGeometry(id),
                    request =>
                    {
                        if (spatialRuntime?.Invoke() is { } runtime) runtime.ApplyControlCenterGeometry(request, ShowCommandResult);
                        else ShowCommandResult(Unavailable(request.RequestId, request.ObservedRevision));
                    }, request =>
                    {
                        var result = spatialRuntime?.Invoke()?.ApplyControlCenterCatalog(request) ?? Unavailable(request.RequestId, request.Revision);
                        ShowCommandResult(result);
                        if (result.Succeeded && result.ResultPosterId is { } id) SelectPoster(id);
                    });
                stage = "apply-projection";
                pendingWindow.ConfigureContentLayout(() => spatialRuntime?.Invoke());
                pendingWindow.ConfigureCenterTheme(theme =>
                {
                    if (!state.TryGetCurrent(out var current) || current is null) return;
                    ShowCommandResult(commands.Execute(new SettingsStateCommand(Guid.NewGuid().ToString("N"),
                        StateCommandSource.ControlCenter, SettingsStateCommandKind.SetCenterTheme, current.Revision,
                        current.ControlCenter.Settings.Settings with { CenterTheme = theme })));
                });
                pendingWindow.ConfigureTemplateRegistration((source, targetId, revision) =>
                {
                    string requestId = "template-register-" + Guid.NewGuid().ToString("N");
                    if (!state.TryGetCurrent(out var current) || current is null || current.Revision != revision)
                        return Unavailable(requestId, revision);
                    var read = StateReadSet.ForSettings(current.ControlCenter.Settings.EntityVersion);
                    if (targetId is not null)
                    {
                        if (!current.PostersById.TryGetValue(targetId, out var target)) return Unavailable(requestId, revision);
                        read = StateReadSet.ForPoster(targetId, target.EntityVersion) with { SettingsVersion = current.ControlCenter.Settings.EntityVersion };
                    }
                    var result = commands.Execute(new RegisterTemplateCommand(requestId, StateCommandSource.ControlCenter, revision, read, source, targetId));
                    ShowCommandResult(result); return result;
                });
                pendingWindow.ConfigureUsability(
                    edit => spatialRuntime?.Invoke()?.PreviewControlCenterSetting(edit) == true,
                    () => spatialRuntime?.Invoke()?.CancelControlCenterVisuals(),
                    id => spatialRuntime?.Invoke()?.LocateControlCenterPoster(id) ?? "桌面尚未就绪。", ledger);
                if (portable is not null) pendingWindow.ConfigurePortable(portable, ledger, log);
                if (_selection.BuildView(_sessions, _environmentPending, _globallyHidden) is { } initialView)
                {
                    pendingWindow.ApplyView(initialView, _commandError);
                    LogProjectionApplied(initialView.Projection);
                }
                stage = "activate-new";
                pendingWindow.Activate();
                _lease = ledger.Acquire("window.control-center", "winui-window");
                _window = pendingWindow;
                pendingWindow = null;
                _window.Closed += OnClosed;
                _window.SettingsInteractionChanged += OnSettingsInteractionChanged;
                log.Write("info", "control_center.created");
            }
            else
            {
                _window.Activate();
            }
            log.Write("info", "control_center.shown");
        }
        catch (Exception exception)
        {
            log.Write("error", "control_center.open_failed", new
            {
                stage,
                exceptionType = exception.GetType().FullName,
                exception.HResult,
                exception.Message,
                restrictedDescription = exception.Data["RestrictedDescription"] as string,
                detail = exception.ToString()
            });
            // A window-open failure must not escape a native tray callback.
            if (pendingWindow is not null)
            {
                try
                {
                    pendingWindow.Close();
                }
                catch (Exception cleanupException)
                {
                    log.Write("error", "control_center.cleanup_failed", new
                    {
                        cleanupException.Message,
                        cleanupException.HResult
                    });
                }
            }
        }
    }

    public void ApplyProjection(ControlCenterProjection projection)
    {
        if (!_selection.ApplyProjection(projection)) return;
        _projection = projection;
        _commandError = null;
        if (_removal.Pending is not null && !_removal.IsCurrent(projection))
            CancelRemoval("海报或目录已变化，删除确认已取消；请查看最新状态后重试。");
        Present();
    }

    public void ApplySessions(IReadOnlyList<PosterSessionSnapshot> sessions, bool environmentPending, bool globallyHidden = false)
    {
        _sessions = sessions;
        _environmentPending = environmentPending;
        _globallyHidden = globallyHidden;
        Present();
    }

    public void ShowCommandResult(StateCommandResult result)
    {
        if (result.Projection is not null) ApplyProjection(result.Projection.ControlCenter);
        if (_window?.CompleteEditing(result) == true) return;
        if (result.RequestId.StartsWith("controlcenter-edit-", StringComparison.Ordinal)) return;
        if (_window?.CompleteAutoPosture(result) == true) return;
        if (result.RequestId.StartsWith("controlcenter-settings-", StringComparison.Ordinal)) return;
        if (!result.Succeeded)
        {
            _commandError = $"{result.Status}：{result.Error ?? "目标已变化，请查看最新状态后重试。"}";
            Present();
        }
    }

    private void SelectPoster(string posterId)
    {
        if (!_selection.Select(posterId)) return;
        _commandError = null;
        Present();
        _window?.LocateSelected();
        log.Write("info", "control_center.selection_changed", new { posterId, revision = _projection?.Revision });
    }

    private StateCommandResult Unavailable(string id, long revision)
    {
        state.TryGetCurrent(out var p);
        return new(StateCommandStatus.Failed, id, revision, p?.Revision ?? 0, p, "空间服务尚未就绪。");
    }

    private void Present()
    {
        if (_window is not null && _selection.BuildView(_sessions, _environmentPending, _globallyHidden) is { } view)
        {
            _window.ApplyView(view, _commandError);
            LogProjectionApplied(view.Projection);
        }
    }

    private void LogProjectionApplied(ControlCenterProjection projection) =>
        log.Write("info", "control_center.projection_applied", new
        {
            projection.Revision,
            PosterRevision = projection.Poster?.PosterRevision,
            PosterCount = projection.Posters.Count,
            SelectedPosterId,
            PosterIds = projection.Posters.Select(p => p.Poster.PosterId).ToArray(),
            Runtime = _sessions.Select(s => new { s.PosterId, Status = s.Status.ToString() }).ToArray(),
            projection.Settings.SettingsRevision
        });

    private void OnClosed(object sender, Microsoft.UI.Xaml.WindowEventArgs args)
    {
        CancelRemoval("控制中心已关闭，未删除海报。");
        if (_window is not null)
        {
            _window.Closed -= OnClosed;
            _window.SettingsInteractionChanged -= OnSettingsInteractionChanged;
        }
        _window = null;
        SettingsInteractionChanged?.Invoke();
        _lease?.Dispose();
        _lease = null;
        log.Write("info", "control_center.closed");
    }

    public void Dispose()
    {
        _disposed = true;
        CancelRemoval("application-exit");
        if (_window is not null)
        {
            _window.Closed -= OnClosed;
            _window.SettingsInteractionChanged -= OnSettingsInteractionChanged;
            _window.Close();
            _window = null;
        }
        _lease?.Dispose();
        _lease = null;
        _projection = null;
    }

    public void CancelRemoval(string reason)
    {
        if (_removal.Pending is null) return;
        _removal.Cancel();
        _window?.CancelRemovalDialog();
        _removalLease?.Dispose();
        _removalLease = null;
        _commandError = reason;
        log.Write("info", "poster.removal_cancelled", new { reason });
    }

    public async void RequestRemoval(PosterRemovalRequest request)
    {
        if (_disposed || _removalShowing || _window?.IsPortableInteractionActive == true) return;
        bool OriginCurrent() => request.Origin is not { } key || inputRouter.IsCurrent(key, request.OriginLayoutRevision);
        _removalShowing = true;
        RemovalInteractionChanged?.Invoke();
        IDisposable? dialogLease = null;
        try
        {
            if (!OriginCurrent()) return;
            var preview = state.PrepareRemoval(request.PosterId);
            if (preview is null || _removal.Begin(request, preview) is not { } ticket)
            {
                _commandError = "目标已变化或不存在；未删除任何海报，请重试。";
                Present();
                return;
            }
            Show();
            await Task.Yield();
            if (_disposed || _removal.Pending?.Id != ticket.Id || _window is null) return;
            dialogLease = ledger.Acquire("dialog.poster-removal", request.PosterId);
            _removalLease = dialogLease;
            log.Write("info", "poster.removal_confirmation_opened", new { ticket.Id, request.PosterId, request.EntityVersion, preview.IsLastPoster });
            bool confirmed = await _window.ShowRemovalDialogAsync(ticket);
            if (_disposed || !OriginCurrent()) { _removal.Cancel(); return; }
            // Refresh before completion; projection updates invalidate a stale target/catalog ticket.
            state.Refresh();
            if (_removal.Complete(ticket.Id, confirmed) is { } command)
            {
                var result = commands.Execute(command);
                ShowCommandResult(result);
                log.Write(result.Succeeded ? "info" : "warning", "poster.removal_completed",
                    new { command.PosterId, command.RequestId, Status = result.Status.ToString(), result.Error });
            }
            else log.Write("info", "poster.removal_confirmation_dismissed", new { request.PosterId, confirmed });
        }
        catch (Exception exception)
        {
            _removal.Cancel();
            _commandError = "删除确认未完成，未自动重试；请检查当前目录后重试。";
            log.Write("error", "poster.removal_confirmation_failed", new { exception.Message, exception.HResult });
            if (!_disposed) Present();
        }
        finally
        {
            _removal.Cancel();
            dialogLease?.Dispose();
            if (ReferenceEquals(_removalLease, dialogLease)) _removalLease = null;
            _removalShowing = false;
            RemovalInteractionChanged?.Invoke();
        }
    }
}
