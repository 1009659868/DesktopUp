using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Testing;

public static class MeasurementFixtureData
{
    public static PosterDefinition Create(int index, int count, string posture)
    {
        if (count is not (1 or 3 or 6) || index < 0 || index >= count)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (posture is not ("expanded" or "collapsed" or "hidden")) throw new ArgumentException(nameof(posture));
        var seed = MultiPosterFixtureData.Create(index % 3);
        return seed with
        {
            Title = $"{(char)('A' + index)} / M07 {seed.TemplateKey}",
            Subtitle = "M07: six DEMO ITEMS, no files collected",
            WidthDip = 300, ExpandedHeightDip = 360,
            HorizontalAnchor = new[] { .04, .40, .76 }[index % 3],
            VerticalAnchor = index < 3 ? .04 : .62,
            NormalizedWidth = .12, NormalizedExpandedHeight = .26,
            IsLocked = false, IsCollapsed = posture == "collapsed", IsVisible = posture != "hidden"
        };
    }
}
