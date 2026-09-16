using CommunityToolkit.Mvvm.Input;
using DesktopUp.Application;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DesktopUp;

internal sealed class TrayService(IResourceLedger ledger, IStructuredLog log) : IDisposable
{
    private Window? _ownerWindow;
    private TaskbarIcon? _icon;
    private readonly Dictionary<TrayAction, MenuFlyoutItem> _items = new();
    private TrayProjection? _projection;
    private bool _globallyHidden, _environmentPending = true;
    private string? _feedback;
    private IDisposable? _ownerLease;
    private IDisposable? _iconLease;
    private long _projectionRevision = -1;

    public void Create(Action<TrayAction> dispatch)
    {
        if (_icon is not null)
        {
            return;
        }

        var menu = new MenuFlyout();
        foreach (var entry in TrayManagement.Menu(null, false, true))
        {
            MenuFlyoutItem item = entry.Checked is null ? new MenuFlyoutItem() : new ToggleMenuFlyoutItem();
            item.Text = entry.Text; item.IsEnabled = entry.Enabled;
            item.Command = new RelayCommand(() => dispatch(entry.Action));
            _items.Add(entry.Action, item);
            if (entry.Action == TrayAction.Exit) menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(item);
        }

        _ownerWindow = new Window();
        _ownerWindow.AppWindow.IsShownInSwitchers = false;
        _ownerWindow.AppWindow.Resize(new global::Windows.Graphics.SizeInt32(1, 1));

        _icon = new TaskbarIcon
        {
            ToolTipText = "DesktopUp V3",
            ContextMenuMode = ContextMenuMode.PopupMenu,
            MenuActivation = PopupActivationMode.RightClick,
            NoLeftClickDelay = true,
            DoubleClickCommand = new RelayCommand(() => dispatch(TrayAction.OpenCenter)),
            IconSource = new GeneratedIconSource
            {
                Text = "DU",
                FontSize = 38,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                Background = new SolidColorBrush(ColorHelper.FromArgb(255, 13, 110, 253))
            },
            ContextFlyout = menu
        };

        _ownerWindow.Content = new Grid
        {
            Width = 1,
            Height = 1,
            Background = new SolidColorBrush(Colors.Transparent),
            Children = { _icon }
        };
        _ownerLease = ledger.Acquire("window.tray-owner", "hidden-winui-window");
        _iconLease = ledger.Acquire("tray.desktopup", "notify-icon");
        _ownerWindow.Activate();
        if (!_icon.IsCreated)
        {
            _icon.ForceCreate(enablesEfficiencyMode: false);
        }
        H.NotifyIcon.WindowExtensions.Hide(_ownerWindow, enableEfficiencyMode: false);
        log.Write("info", "tray.created", new { created = _icon.IsCreated });
    }

    public void ApplyRuntime(bool hidden, bool environmentPending)
    { _globallyHidden = hidden; _environmentPending = environmentPending; Render(); }

    internal bool VerifyProjection(TrayProjection projection) => _projectionRevision == projection.Revision &&
        _icon?.IsCreated == true && _items.Count == 6 &&
        TrayManagement.Menu(projection, _globallyHidden, _environmentPending).All(e =>
            _items.TryGetValue(e.Action, out var item) && item.Text == e.Text && item.IsEnabled == e.Enabled &&
            (e.Checked is null || item is ToggleMenuFlyoutItem toggle && toggle.IsChecked == e.Checked));

    // Runs the real command binding, not native input; isolated process probes explicitly report this limit.
    internal void InvokeForProbe(TrayAction action)
    { if (_items.TryGetValue(action, out var item) && item.IsEnabled) item.Command.Execute(null); }

    public void Notify(string message, bool error = true)
    {
        _feedback = message; Render();
        try { _icon?.ShowNotification("DesktopUp", message, error ? NotificationIcon.Warning : NotificationIcon.Info, sound: false); }
        catch (Exception ex) { log.Write("warning", "tray.notification_failed", new { ex.Message }); }
    }

    public void ClearFeedback() { _feedback = null; Render(); }

    public void ApplyProjection(TrayProjection projection)
    {
        if (projection.Revision < _projectionRevision)
        {
            return;
        }
        _projectionRevision = projection.Revision;
        _projection = projection;
        Render();
    }

    private void Render()
    {
        if (_icon is not null)
        {
            string text = _feedback is not null ? "DesktopUp · " + _feedback :
                (_projection?.ToolTipText ?? "DesktopUp") + (_globallyHidden ? " · 全局隐藏" : "") +
                (_environmentPending ? " · 等待桌面" : "");
            _icon.ToolTipText = text.Length <= 120 ? text : text[..120];
        }
        foreach (var entry in TrayManagement.Menu(_projection, _globallyHidden, _environmentPending))
        {
            if (!_items.TryGetValue(entry.Action, out var item)) continue;
            item.Text = entry.Text; item.IsEnabled = entry.Enabled;
            if (item is ToggleMenuFlyoutItem toggle) toggle.IsChecked = entry.Checked == true;
        }
    }

    public void Dispose()
    {
        if (_icon is not null)
        {
            _icon.Dispose();
            _icon = null;
            log.Write("info", "tray.disposed");
        }
        if (_ownerWindow is not null)
        {
            _ownerWindow.Close();
            _ownerWindow = null;
        }
        _iconLease?.Dispose();
        _iconLease = null;
        _ownerLease?.Dispose();
        _ownerLease = null;
        _items.Clear(); _projection = null; _feedback = null;
        _projectionRevision = -1;
    }
}
