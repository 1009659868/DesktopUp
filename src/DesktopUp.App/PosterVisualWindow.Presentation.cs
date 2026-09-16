using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Controls;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    private ResolvedPosterPresentation _presentation = new("night-ops", PosterPresentationPolicy.Defaults(),
        PosterContentMetrics.Current, true, true, null);
    private string? _renderedTitle, _renderedSubtitle;
    private PosterContentParameters ContentParameters => _presentation.EffectiveContent;
    private double MetaHeightDip => _presentation.MetaHeightDip;
    private double ItemGapDip => ContentParameters.GapDip;
    private double ItemCardHeightDip => ContentParameters.ItemHeightDip;
    private double ItemRowHeightDip => ContentParameters.RowHeightDip;

    private void ApplyPresentation(ResolvedPosterPresentation next)
    {
        if (_centerPresentation is not null)
        {
            if (_centerPresentationVersion == _posterEntityVersion) next = _centerPresentation;
            else _centerPresentation = null;
        }
        bool changed = _presentation != next || _renderedTitle != _authoritativePoster?.Title ||
            _renderedSubtitle != _authoritativePoster?.Subtitle;
        _presentation = next;
        _renderedTitle = _authoritativePoster?.Title;
        _renderedSubtitle = _authoritativePoster?.Subtitle;
        if (!changed || _window is null) return;
        StopLocate();
        RestoreTransitionLayout();
        _posterMenu?.Hide();
        StopExpansionFeedback();
        _itemColumnCount = -1;
        // Replace only this instance's XAML tree on an explicit presentation revision.
        // No window/host replacement, no global Application.Resources, no outer geometry write.
        ApplyWindowBackdrop(_window);
        _window.Content = CreateContent();
        _window.Title = "DesktopUp V3 · " + _renderedTitle;
        log.Write(next.IsFallback ? "warning" : "info", "poster.presentation_applied", new
        {
            PosterId, template = next.EffectiveTemplateKey, next.IsLegacy, next.Warning,
            metricVersion = next.Metrics.Version, rowHeightDip = next.Metrics.CompleteItemRowDip,
            content = next.Parameters.Content, theme = next.Parameters.Theme, bounds = _bounds
        });
    }

    private static SolidColorBrush ColorBrush(string hex, byte alpha = 255) =>
        Brush(alpha, Convert.ToByte(hex.Substring(1, 2), 16), Convert.ToByte(hex.Substring(3, 2), 16),
            Convert.ToByte(hex.Substring(5, 2), 16));

    // Read real XAML objects without synthesizing input or replacing a native layout.
    internal bool VerifyPresentation(PosterSurfaceProjection target)
    {
        // WinUI stores these dependency values as float32. Accept only the exact
        // value or its exact float32 representation, not a loose pixel tolerance.
        static bool XamlNumber(double actual, double expected) => double.IsFinite(actual) && double.IsFinite(expected) &&
            (actual == expected || actual == (double)(float)expected);
        var expected = target.Presentation;
        if (_presentation != expected || _renderedTitle != target.Poster.Title ||
            _window?.Title != "DesktopUp V3 · " + target.Poster.Title ||
            !VerifyWindowBackdrop() ||
            _surface?.Child is not Grid structure || !XamlNumber(structure.RowDefinitions[1].Height.Value, target.Poster.IsCollapsed ? 0 : expected.MetaHeightDip) ||
            _itemsGrid is null || _itemsGrid.Children.Count != Items.Length ||
            !XamlNumber(_itemsGrid.RowSpacing, ContentParameters.GapDip) || !XamlNumber(_itemsGrid.ColumnSpacing, ContentParameters.GapDip) ||
            _itemColumnCount != PosterPresentationPolicy.ResolveColumns(FrameWidthDip(_bounds), expected.EffectiveContent)) return false;
        if (_itemsGrid.RowDefinitions.Any(r => !XamlNumber(r.Height.Value, expected.Metrics.CompleteItemRowDip))) return false;
        foreach (Grid cell in _itemsGrid.Children.Cast<Grid>())
        {
            if (!XamlNumber(cell.RowDefinitions[0].Height.Value, ContentParameters.ItemHeightDip) ||
                cell.Children[0] is not Border { Child: Grid card } ||
                !card.Children.OfType<TextBlock>().Any(t => XamlNumber(t.Width, ContentParameters.IconSizeDip) && XamlNumber(t.Height, ContentParameters.IconSizeDip)) ||
                cell.Children[1] is not TextBlock label || !XamlNumber(label.FontSize, ContentParameters.LabelFontSizeDip)) return false;
        }
        return true;
    }
}
