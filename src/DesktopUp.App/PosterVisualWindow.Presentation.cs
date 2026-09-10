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
    private PosterContentParameters ContentParameters => _presentation.Parameters.Content;
    private double MetaHeightDip => _presentation.MetaHeightDip;
    private double ItemGapDip => ContentParameters.GapDip;
    private double ItemCardHeightDip => ContentParameters.ItemHeightDip;
    private double ItemRowHeightDip => ContentParameters.RowHeightDip;

    private void ApplyPresentation(ResolvedPosterPresentation next)
    {
        bool changed = _presentation != next || _renderedTitle != _authoritativePoster?.Title ||
            _renderedSubtitle != _authoritativePoster?.Subtitle;
        _presentation = next;
        _renderedTitle = _authoritativePoster?.Title;
        _renderedSubtitle = _authoritativePoster?.Subtitle;
        if (!changed || _window is null) return;
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
        var expected = target.Presentation;
        if (_presentation != expected || _renderedTitle != target.Poster.Title ||
            _window?.Title != "DesktopUp V3 · " + target.Poster.Title ||
            !VerifyWindowBackdrop() ||
            _surface?.Child is not Grid structure || structure.RowDefinitions[1].Height.Value != (target.Poster.IsCollapsed ? 0 : expected.MetaHeightDip) ||
            _itemsGrid is null || _itemsGrid.Children.Count != Items.Length ||
            _itemsGrid.RowSpacing != ContentParameters.GapDip || _itemsGrid.ColumnSpacing != ContentParameters.GapDip ||
            _itemColumnCount != PosterPresentationPolicy.ResolveColumns(FrameWidthDip(_bounds), expected.Parameters.Content)) return false;
        if (_itemsGrid.RowDefinitions.Any(r => r.Height.Value != expected.Metrics.CompleteItemRowDip)) return false;
        foreach (Grid cell in _itemsGrid.Children.Cast<Grid>())
        {
            if (cell.RowDefinitions[0].Height.Value != ContentParameters.ItemHeightDip ||
                cell.Children[0] is not Border { Child: Grid card } ||
                !card.Children.OfType<TextBlock>().Any(t => t.Width == ContentParameters.IconSizeDip && t.Height == ContentParameters.IconSizeDip) ||
                cell.Children[1] is not TextBlock label || label.FontSize != ContentParameters.LabelFontSizeDip) return false;
        }
        return true;
    }
}
