using DesktopUp.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DesktopUp;

internal static class CanvasContentPlacement
{
    internal static void Apply(CanvasLayoutResult layout, Grid structure, Grid items, ScrollViewer scroll, FrameworkElement? meta, FrameworkElement? footer)
    {
        var area = layout.Content;
        // Normalized design coordinates may be fractional DIP. Preserve them through XAML;
        // the existing input boundary performs the single final conversion to device pixels.
        structure.UseLayoutRounding = false; items.UseLayoutRounding = false; scroll.UseLayoutRounding = false;
        structure.RowDefinitions[0].Height = new(CanvasLayout.Header);
        structure.RowDefinitions[1].Height = new(Math.Max(0, area.Y - CanvasLayout.Header));
        structure.RowDefinitions[3].Height = new(Math.Max(0, layout.Height - area.Bottom));
        items.Margin = new(area.X, 0, Math.Max(0, layout.Width - area.Right), 0);
        scroll.Margin = new(0);
        if (meta is not null) meta.Visibility = Visibility.Collapsed;
        if (footer is not null) footer.Visibility = Visibility.Collapsed;
    }
}
