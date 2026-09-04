using DesktopUp.Application;

namespace DesktopUp;

internal sealed class ControlCenterService(
    IResourceLedger ledger,
    IStructuredLog log,
    Action toggleLocked,
    Action toggleCollapsed,
    Action toggleDynamicEffectsPaused) : IDisposable
{
    private ControlCenterWindow? _window;
    private IDisposable? _lease;
    private ControlCenterProjection? _projection;

    public void Show()
    {
        if (_window is null)
        {
            _window = new ControlCenterWindow(
                toggleLocked,
                toggleCollapsed,
                toggleDynamicEffectsPaused);
            _lease = ledger.Acquire("window.control-center", "winui-window");
            _window.Closed += OnClosed;
            if (_projection is not null)
            {
                _window.ApplyProjection(_projection);
            }
            log.Write("info", "control_center.created");
        }

        _window.Activate();
        log.Write("info", "control_center.shown");
    }

    public void ApplyProjection(ControlCenterProjection projection)
    {
        if (_projection is not null && projection.Revision < _projection.Revision)
        {
            return;
        }
        _projection = projection;
        _window?.ApplyProjection(projection);
    }

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
