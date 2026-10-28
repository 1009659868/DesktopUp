using DesktopUp.Application;
using DesktopUp.Windows;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace DesktopUp;

// App-side instrumentation, not an alternative input implementation. Never marks events handled.
internal sealed class MenuInputDiagnostic : IDisposable
{
    private readonly MenuFlyout _flyout;
    private readonly IStructuredLog _log;
    private readonly Func<object> _state;
    private readonly DispatcherQueueTimer _timer;
    private readonly List<Action> _unsubscribe = [];
    private readonly string _traceId = Guid.NewGuid().ToString("N");
    private int _records;
    private bool _collecting;
    private bool _sampling;
    private bool _disposed;

    private MenuInputDiagnostic(MenuFlyout flyout, DispatcherQueue dispatcher, IStructuredLog log, Func<object> state)
    {
        _flyout = flyout; _log = log; _state = state;
        _timer = dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(250);
        _timer.Tick += Tick;
    }

    public static MenuInputDiagnostic? TryCreate(MenuFlyout flyout, DispatcherQueue dispatcher, IStructuredLog log, Func<object> state)
    {
        MenuInputDiagnostic? probe = null;
        try
        {
            probe = new(flyout, dispatcher, log, state);
            probe._flyout.Opened += probe.Opened;
            foreach (MenuFlyoutItem item in flyout.Items.OfType<MenuFlyoutItem>())
            {
                probe.Bind(item, UIElement.PointerEnteredEvent, "entered");
                probe.Bind(item, UIElement.PointerExitedEvent, "exited");
                probe.Bind(item, UIElement.PointerPressedEvent, "pressed");
                probe.Bind(item, UIElement.PointerReleasedEvent, "released");
            }
            return probe;
        }
        catch (Exception exception)
        {
            probe?.Dispose();
            try { log.Write("warning", "menu_diag.start_failed", new { exception.HResult }); } catch { }
            return null;
        }
    }

    private void Bind(MenuFlyoutItem item, RoutedEvent routedEvent, string phase)
    {
        PointerEventHandler handler = (_, args) =>
        {
            try
            {
                Record("menu_pointer", new { phase, item = item.Text, args.Handled,
                    device = args.Pointer.PointerDeviceType.ToString(), localPoint = args.GetCurrentPoint(item).Position });
            }
            catch { /* No effect on pointer delivery if the observed element is closing. */ }
        };
        item.AddHandler(routedEvent, handler, handledEventsToo: true);
        _unsubscribe.Add(() => item.RemoveHandler(routedEvent, handler));
    }

    private void Opened(object? sender, object args)
    {
        if (_disposed) return;
        _collecting = true;
        MenuInputDiagnostics.NativeMessage += Native;
        Record("opened", null);
        _timer.Start();
    }

    private void Tick(DispatcherQueueTimer sender, object args) => Record("sample", null);
    private void Native(object message) => Record("native_received", message);
    public void Guard(string command, bool accepted, long menuEntity, long menuLayout) =>
        Record("command_guard", new { command, accepted, menuEntity, menuLayout });

    private void Record(string kind, object? detail)
    {
        if (_sampling || (_disposed && kind != "command_guard") || _records >= 1200) return;
        _sampling = true;
        try
        {
            _log.Write("info", "menu_diag." + kind, new
            {
                traceId = _traceId, sequence = ++_records, detail, state = _state(),
                pointer = MenuInputDiagnostics.PointerSnapshot(),
                shouldConstrain = _flyout.ShouldConstrainToRootBounds,
                items = _flyout.Items.OfType<MenuFlyoutItem>().Select(ReadItem).ToArray(),
                ownWindowsFrontToBack = MenuInputDiagnostics.OwnWindowSnapshot()
            });
        }
        catch (Exception exception)
        {
            try { _log.Write("warning", "menu_diag.sample_failed", new { traceId = _traceId, kind, exception.HResult, exceptionType = exception.GetType().FullName }); }
            catch { /* Observation failure must not fail a command or pointer event. */ }
        }
        finally { _sampling = false; }
        if (_records >= 1200 && !_disposed) Stop();
    }

    private static object ReadItem(MenuFlyoutItem item)
    {
        try
        {
            var peer = FrameworkElementAutomationPeer.CreatePeerForElement(item);
            // UIA provider returns SCREEN coordinates; TransformToVisual(null) does not.
            var screen = peer?.GetBoundingRectangle();
            var rootBounds = item.TransformToVisual(null).TransformBounds(new(0, 0, item.ActualWidth, item.ActualHeight));
            return new
            {
                item.Text, item.IsEnabled, item.IsHitTestVisible, item.ActualWidth, item.ActualHeight,
                screenBoundsSource = "AutomationPeer.GetBoundingRectangle", screenBounds = screen,
                rootBoundsDip = rootBounds, rasterizationScale = item.XamlRoot?.RasterizationScale,
                xamlAppWindowId = item.XamlRoot?.ContentIslandEnvironment?.AppWindowId.Value,
                openPopupCount = item.XamlRoot is { } root ? VisualTreeHelper.GetOpenPopupsForXamlRoot(root).Count : 0
            };
        }
        catch (Exception exception) { return new { item.Text, boundsUnavailable = true, exception.HResult }; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        Record("closed_or_stopped", null);
        Stop();
    }

    private void Stop()
    {
        if (_disposed) return;
        _disposed = true;
        try { _timer.Stop(); _timer.Tick -= Tick; _flyout.Opened -= Opened; } catch { }
        if (_collecting) MenuInputDiagnostics.NativeMessage -= Native;
        foreach (var remove in _unsubscribe) { try { remove(); } catch { } }
        _unsubscribe.Clear();
    }
}
