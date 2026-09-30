using System.Runtime.CompilerServices;
using DesktopUp.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
namespace DesktopUp;
internal static class RainRegistration
{
    [ModuleInitializer]
    internal static void Register() => SceneModuleRegistry.Register("rain.paint@1", () => new RainSceneInstance(), Sample);
    private static Grid Sample(TemplateRecipe recipe)
    {
        var art = new Grid { Height = 132, IsHitTestVisible = false };
        var cloud = RainSceneInstance.CloudArt(out _); cloud.Height = 56; cloud.Margin = new(4, 2, 4, 0); cloud.VerticalAlignment = VerticalAlignment.Top;
        art.Children.Add(cloud);
        var brush = new SolidColorBrush(global::Windows.UI.Color.FromArgb(110, 147, 201, 233));
        for (int i = 0; i < 7; i++) art.Children.Add(new Line { X1 = 0, Y1 = 0, X2 = -2, Y2 = 20 + i % 3 * 5,
            Stroke = brush, StrokeThickness = .8, HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top, Margin = new(15 + i * 17, 50 + i % 3 * 10, 0, 0) });
        art.Children.Add(new TextBlock { Text = "雨声收藏", FontSize = 12, Opacity = .8, Margin = new(12, 0, 4, 8), VerticalAlignment = VerticalAlignment.Bottom });
        return art;
    }
}
