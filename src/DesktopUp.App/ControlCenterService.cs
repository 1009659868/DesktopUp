using DesktopUp.Application;

namespace DesktopUp;

internal sealed class ControlCenterService(
    IResourceLedger ledger,
    IStructuredLog log,
    Action<PosterStateCommand> posterCommand,
    Action toggleDynamicEffectsPaused,
    Action<string?> selectionChanged) : IDisposable
{
    private ControlCenterWindow? _window;
    private IDisposable? _lease;
    private ControlCenterProjection? _projection;
    private readonly ControlCenterSelection _selection = new();
    private IReadOnlyList<PosterSessionSnapshot> _sessions = Array.Empty<PosterSessionSnapshot>();
    private bool _environmentPending = true;
    private string? _commandError;
    public string? SelectedPosterId => _selection.SelectedPosterId;

    public void Show()
    {
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
                    toggleDynamicEffectsPaused);
                stage = "load-xaml";
                pendingWindow.InitializeView();
                stage = "apply-projection";
                if (_selection.BuildView(_sessions, _environmentPending) is { } initialView)
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
        string? previousSelection = SelectedPosterId;
        if (!_selection.ApplyProjection(projection)) return;
        _projection = projection;
        _commandError = null;
        Present();
        if (previousSelection != SelectedPosterId) selectionChanged(SelectedPosterId);
    }

    public void ApplySessions(IReadOnlyList<PosterSessionSnapshot> sessions, bool environmentPending)
    {
        _sessions = sessions;
        _environmentPending = environmentPending;
        Present();
    }

    public void ShowCommandResult(StateCommandResult result)
    {
        if (result.Projection is not null) ApplyProjection(result.Projection.ControlCenter);
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
        selectionChanged(SelectedPosterId);
        log.Write("info", "control_center.selection_changed", new { posterId, revision = _projection?.Revision });
    }

    private void Present()
    {
        if (_window is not null && _selection.BuildView(_sessions, _environmentPending) is { } view)
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
        if (_window is not null)
        {
            _window.Closed -= OnClosed;
        }
        _window = null;
        _lease?.Dispose();
        _lease = null;
        log.Write("info", "control_center.closed");
    }

    public void Dispose()
    {
        if (_window is not null)
        {
            _window.Closed -= OnClosed;
            _window.Close();
            _window = null;
        }
        _lease?.Dispose();
        _lease = null;
        _projection = null;
    }
}
