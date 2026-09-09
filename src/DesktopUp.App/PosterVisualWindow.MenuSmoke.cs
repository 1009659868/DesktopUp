using DesktopUp.Application;
using DesktopUp.Windows;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    // Called only by the explicit, isolated --menu-smoke fixture. No persisted geometry writes.
    internal bool ArrangeForMenuSmoke(int index)
    {
        if (_monitor is null) return false;
        var area = _monitor.WorkArea;
        return PresentPostureFrame(new DesktopPixelRect(
            area.Right - (3 - index) * _bounds.Width, area.Y + 24, _bounds.Width, _bounds.Height));
    }

    internal void OpenForMenuSmoke() => ShowPosterMenu();
    internal void CloseForMenuSmoke() => _menuInteraction?.Cancel();
    internal DesktopMenuProbe? ReadMenuProbe() => _menuInteraction?.ReadProbe();
    internal bool MenuInputRestored() => _inputIsland is not null &&
        !_inputIsland.IsNativeInputTransparent &&
        !DesktopMenuInteraction.BlocksInput(_inputIsland.WindowHandle) &&
        !DesktopMenuInteraction.ProtectsSurface(_windowHandle);
}
