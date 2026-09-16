using System.Runtime.InteropServices;
using DesktopUp.Application;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    internal bool HasLocateForProbe => _locateOutline?.Visibility == Visibility.Visible && _locateTimer is not null;
    internal object ReadPresentationForProbe(PosterSurfaceProjection target) => new
    {
        samePresentation = _presentation == target.Presentation,
        title = _renderedTitle, expectedTitle = target.Poster.Title, windowTitle = _window?.Title,
        backdrop = VerifyWindowBackdrop(),
        meta = (_surface?.Child as Grid)?.RowDefinitions[1].Height.Value,
        expectedMeta = target.Poster.IsCollapsed ? 0 : target.Presentation.MetaHeightDip,
        columns = _itemColumnCount, expectedColumns = DesktopUp.Domain.PosterPresentationPolicy.ResolveColumns(FrameWidthDip(_bounds), target.Presentation.EffectiveContent),
        rowGap = _itemsGrid?.RowSpacing, columnGap = _itemsGrid?.ColumnSpacing, expected = target.Presentation.EffectiveContent,
        rows = _itemsGrid?.RowDefinitions.Select(r => r.Height.Value).ToArray(),
        expectedRow = target.Presentation.Metrics.CompleteItemRowDip,
        cells = _itemsGrid?.Children.OfType<Grid>().Select(c => new
        {
            height = c.RowDefinitions[0].Height.Value, labelFont = (c.Children[1] as TextBlock)?.FontSize,
            icons = (c.Children[0] as Border)?.Child is Grid card ? card.Children.OfType<TextBlock>().Select(t => new { Width = t.Width.ToString("R"), Height = t.Height.ToString("R") }).ToArray() : null
        }).ToArray()
    };
}
internal sealed partial class PosterSpatialRuntime
{
    internal bool VerifyCenterPreviewForProbe(PosterSettingEdit edit) =>
        _windows.TryGetValue(edit.PosterId, out var window) &&
        window.VerifyPresentation(edit.Basis with { Poster = edit.Command("inspect-only").DesiredState });
    internal object ReadCenterPreviewForProbe(PosterSettingEdit edit) =>
        _windows[edit.PosterId].ReadPresentationForProbe(edit.Basis with { Poster = edit.Command("inspect-only").DesiredState });
    internal bool HasLocateForProbe(string id) => _windows.TryGetValue(id, out var window) && window.HasLocateForProbe;
    internal sealed record LocateNativeSnapshot(nint Handle, nint Foreground, nint Owner, nint Previous, nint ExStyle, ControlCenterGeometryBasis? Geometry);
    internal LocateNativeSnapshot ReadLocateForProbe(string id)
    {
        var hwnd = _windows[id].WindowHandle;
        return new(hwnd, GetForegroundWindow(), GetWindow(hwnd, 4), GetWindow(hwnd, 3), GetWindowLongPtrW(hwnd, -20), ReadControlCenterGeometry(id));
    }
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern nint GetWindow(nint hwnd, uint command);
    [DllImport("user32.dll")] private static extern nint GetWindowLongPtrW(nint hwnd, int index);
}
