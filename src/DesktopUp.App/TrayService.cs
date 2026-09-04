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
    private MenuFlyoutItem? _editItem;
    private ToggleMenuFlyoutItem? _lockItem;
    private ToggleMenuFlyoutItem? _pauseItem;
    private IDisposable? _ownerLease;
    private IDisposable? _iconLease;
    private long _projectionRevision = -1;

    public void Create(
        Action openControlCenter,
        Action togglePosterEditing,
        Action togglePosterLocked,
        Action toggleDynamicEffectsPaused,
        Action exitApplication)
    {
        if (_icon is not null)
        {
            return;
        }

        var menu = new MenuFlyout();
        var openItem = new MenuFlyoutItem
        {
            Text = "打开控制中心",
            Command = new RelayCommand(openControlCenter)
        };
        var exitItem = new MenuFlyoutItem
        {
            Text = "退出 DesktopUp",
            Command = new RelayCommand(exitApplication)
        };
        menu.Items.Add(openItem);
        _editItem = new MenuFlyoutItem
        {
            Text = "进入海报编辑",
            Command = new RelayCommand(togglePosterEditing)
        };
        menu.Items.Add(_editItem);
        _lockItem = new ToggleMenuFlyoutItem
        {
            Text = "锁定全部海报",
            Command = new RelayCommand(togglePosterLocked),
            IsEnabled = false
        };
        menu.Items.Add(_lockItem);
        _pauseItem = new ToggleMenuFlyoutItem
        {
            Text = "暂停动态效果",
            Command = new RelayCommand(toggleDynamicEffectsPaused),
            IsEnabled = false
        };
        menu.Items.Add(_pauseItem);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(exitItem);

        _ownerWindow = new Window();
        _ownerWindow.AppWindow.IsShownInSwitchers = false;
        _ownerWindow.AppWindow.Resize(new global::Windows.Graphics.SizeInt32(1, 1));

        _icon = new TaskbarIcon
        {
            ToolTipText = "DesktopUp V3",
            ContextMenuMode = ContextMenuMode.PopupMenu,
            MenuActivation = PopupActivationMode.RightClick,
            NoLeftClickDelay = true,
            DoubleClickCommand = new RelayCommand(openControlCenter),
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

    public void SetPosterEditing(bool editing)
    {
        if (_editItem is not null)
        {
            _editItem.Text = editing ? "完成海报编辑" : "进入海报编辑";
        }
    }

    public void ApplyProjection(TrayProjection projection)
    {
        if (projection.Revision < _projectionRevision)
        {
            return;
        }
        _projectionRevision = projection.Revision;
        if (_icon is not null)
        {
            _icon.ToolTipText = projection.ToolTipText;
        }
        if (_lockItem is not null)
        {
            _lockItem.IsChecked = projection.AllPostersLocked;
            _lockItem.IsEnabled = true;
        }
        if (_pauseItem is not null)
        {
            _pauseItem.IsChecked = projection.DynamicEffectsPaused;
            _pauseItem.IsEnabled = true;
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
        _editItem = null;
        _lockItem = null;
        _pauseItem = null;
        _projectionRevision = -1;
    }
}
