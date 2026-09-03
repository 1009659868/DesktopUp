using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace DesktopUp;

public sealed partial class ControlCenterWindow : Window
{
    public ControlCenterWindow()
    {
        InitializeComponent();
        AppWindow.Resize(new global::Windows.Graphics.SizeInt32(840, 560));
        AppWindow.IsShownInSwitchers = true;
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = true;
            presenter.IsMaximizable = true;
            presenter.IsMinimizable = true;
        }
    }
}
