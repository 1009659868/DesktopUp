using DesktopUp.Domain;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DesktopUp;

internal static class CanvasTypography
{
    internal static void Apply(TextBlock text, CanvasTextStyle? style, double size)
    {
        text.FontSize = size;
        text.FontFamily = new FontFamily(style?.Family switch { "serif" => "Georgia", "mono" => "Consolas", _ => "Segoe UI" });
        text.CharacterSpacing = (int)Math.Round((style?.LetterSpacing ?? 0) / Math.Max(1, size) * 1000);
    }
}
