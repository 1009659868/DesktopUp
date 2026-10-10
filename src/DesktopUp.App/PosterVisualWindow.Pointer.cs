using DesktopUp.Application;
using DesktopUp.Windows;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    public PosterPointerTarget ReadPointerTarget() => new(inputSession, _windowHandle,
        _inputIsland?.WindowHandle ?? 0, _desktopHost,
        _authoritativePoster?.AutoCollapseEnabled == true,
        !_disposed && _window is not null && _inputIsland is not null && inputRouter.ReadSurface(inputSession) is not null,
        _authoritativePoster?.IsVisible == true, _authoritativePoster?.IsCollapsed == true,
        (IsEditing ? AutoPostureProtection.Editing : AutoPostureProtection.None) |
        (_spatialGesture is not null || HasContentGesture || _fileDrop?.IsHovering==true || spatial.IsFileTransferActive(PosterId!)
            ? AutoPostureProtection.Gesture : AutoPostureProtection.None));
}
