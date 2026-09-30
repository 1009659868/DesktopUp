using DesktopUp.Domain;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
namespace DesktopUp;
public sealed partial class ControlCenterWindow
{
    internal static Grid CreateLampSampleArt(TemplateRecipe recipe)
    {
        string key = recipe.EffectiveTemplateKey;
        var theme = PosterPresentationPolicy.Defaults(key).Theme;
        var lamp = LampTemplatePolicy.Defaults;
        var art = new Grid { Height = 132, CornerRadius = new CornerRadius(2),
            Background = SampleBrush(recipe.Base.Background), IsHitTestVisible = false };
        var clip = new RectangleGeometry(); art.Clip = clip;
        art.SizeChanged += (_, _) => clip.Rect = new(0, 0, Math.Max(0, art.ActualWidth), art.ActualHeight);
        var ink = SampleBrush(theme.Foreground);
        var cardFill = SampleBrush(recipe.Base.Card);
        if (key == "lamp")
        {
            var light = SampleBrush(lamp.LightColor).Color;
            var glow = new LinearGradientBrush { StartPoint = new(.5, 0), EndPoint = new(.5, 1) };
            glow.GradientStops.Add(new GradientStop { Color = global::Windows.UI.Color.FromArgb(56, light.R, light.G, light.B), Offset = 0 });
            glow.GradientStops.Add(new GradientStop { Color = global::Windows.UI.Color.FromArgb(0, light.R, light.G, light.B), Offset = 1 });
            art.Children.Add(new Polygon { Points = new PointCollection { new(30, 0), new(60, 0), new(90, 90), new(0, 90) },
                Width = 90, Height = 90, Stretch = Stretch.Fill, Margin = new Thickness(0, 37, -16, 0), Fill = glow,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top });
            var shade = new LinearGradientBrush { StartPoint = new(0, 0), EndPoint = new(1, 0) };
            shade.GradientStops.Add(new GradientStop { Color = SampleBrush("#323D3B").Color, Offset = 0 });
            shade.GradientStops.Add(new GradientStop { Color = SampleBrush("#7D8672").Color, Offset = .55 });
            shade.GradientStops.Add(new GradientStop { Color = SampleBrush("#263232").Color, Offset = 1 });
            art.Children.Add(new Border { Width = 40, Height = 22, Margin = new Thickness(0, 12, 9, 0), CornerRadius = new(25, 25, 5, 5), Background = shade,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top });
            art.Children.Add(new Border { Width = 38, Height = 3, Margin = new Thickness(0, 33, 10, 0), CornerRadius = new(2), Background = SampleBrush(lamp.LightColor),
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top });
        }
        else
        {
            art.Children.Add(new Ellipse { Width = 72, Height = 72, Stroke = ink, StrokeThickness = 1, Opacity = .2,
                Margin = new Thickness(0, -27, -15, 0), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top });
            art.Children.Add(new Border { Width = 90, Height = 130, Background = cardFill, Opacity = .18,
                Margin = new Thickness(-30, 35, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
                RenderTransformOrigin = new(.5, .5), RenderTransform = new RotateTransform { Angle = 38 } });
        }
        art.Children.Add(new TextBlock { Text = key == "basic-box" ? "MAKE\nROOM." : key == "lamp" ? "AFTER\nDARK." : "NIGHT\nPLAY.",
            Margin = new Thickness(10, 12, 8, 0), FontSize = 13, LineHeight = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = ink, CharacterSpacing = 38, VerticalAlignment = VerticalAlignment.Top });
        var cards = new Grid { Height = 31, ColumnSpacing = 5, Margin = new Thickness(10, 0, 10, 12), VerticalAlignment = VerticalAlignment.Bottom };
        for (int i = 0; i < 3; i++)
        {
            cards.ColumnDefinitions.Add(new ColumnDefinition());
            var card = new Border { Background = cardFill, BorderBrush = ink, BorderThickness = new(.5), Opacity = key == "lamp" ? .85 : .35,
                CornerRadius = new CornerRadius(2), Height = i == 0 ? 31 : i == 1 ? 20 : 12, VerticalAlignment = VerticalAlignment.Bottom };
            Grid.SetColumn(card, i); cards.Children.Add(card);
        }
        art.Children.Add(cards); return art;
    }
}
