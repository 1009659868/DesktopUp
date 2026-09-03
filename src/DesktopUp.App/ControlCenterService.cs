using DesktopUp.Application;

namespace DesktopUp;

internal sealed class ControlCenterService(IResourceLedger ledger, IStructuredLog log) : IDisposable
{
    private ControlCenterWindow? _window;
    private IDisposable? _lease;

    public void Show()
    {
        if (_window is null)
        {
            _window = new ControlCenterWindow();
            _lease = ledger.Acquire("window.control-center", "winui-window");
            _window.Closed += OnClosed;
            log.Write("info", "control_center.created");
        }

        _window.Activate();
        log.Write("info", "control_center.shown");
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
    }
}
