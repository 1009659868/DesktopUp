using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinRT;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    // One backdrop per HWND, not per frame or shared between posters. The window compositor
    // supplies the background even when the XAML content surface is still at the previous size.
    private WinUIEx.TransparentTintBackdrop? _windowBackdrop;

    internal bool HasWindowBackdrop => _windowBackdrop is not null &&
        _window is not null && ReferenceEquals(_window.SystemBackdrop, _windowBackdrop);

    private void ApplyWindowBackdrop(Window window)
    {
        var color = BackdropColor();
        if (_windowBackdrop is null) _windowBackdrop = new WinUIEx.TransparentTintBackdrop(color);
        else if (_windowBackdrop.TintColor != color) _windowBackdrop.TintColor = color;
        if (!ReferenceEquals(window.SystemBackdrop, _windowBackdrop)) window.SystemBackdrop = _windowBackdrop;
    }

    private void ReleaseWindowBackdrop()
    {
        // Disconnect invokes the component's brush/message-monitor cleanup before HWND disposal.
        if (_window is not null && ReferenceEquals(_window.SystemBackdrop, _windowBackdrop))
            _window.SystemBackdrop = null;
        _windowBackdrop = null;
    }

    internal bool VerifyWindowBackdrop()
    {
        return HasWindowBackdrop &&
            _windowBackdrop!.TintColor == BackdropColor() &&
            _window!.Content is Grid { Background: SolidColorBrush background } &&
            background.Color == Colors.Transparent &&
            _window.As<ICompositionSupportsSystemBackdrop>().SystemBackdrop is not null;
    }
    private global::Windows.UI.Color BackdropColor()
    {
        if (_geometry.IsCollapsed && _presentation.Parameters.Canvas?.Current is { } design)
        {
            var baseline = _presentation.Parameters.Surface;
            var collapsed = DesktopUp.Domain.CanvasBehaviorPolicy.Collapsed(design,
                baseline?.Color ?? _presentation.Parameters.Theme.Background, (baseline?.OpacityPercent ?? 96) / 100);
            return ColorBrush(collapsed.Color, (byte)Math.Round(collapsed.Opacity * 255)).Color;
        }
        var paint = CurrentSceneParameters.Surface;
        return ColorBrush(paint.Background, paint.BackgroundAlpha).Color;
    }
}
