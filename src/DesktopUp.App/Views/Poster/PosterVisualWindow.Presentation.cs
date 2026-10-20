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
        var previous = _presentation;
        bool paintOnlyTemplate = TemplatePresentationRefreshPolicy.CanPaint(previous, next,
            _renderedTitle, _authoritativePoster?.Title, _renderedSubtitle, _authoritativePoster?.Subtitle);
        bool changed = _presentation != next || _renderedTitle != _authoritativePoster?.Title ||
            _renderedSubtitle != _authoritativePoster?.Subtitle;
        _presentation = next;
        _renderedTitle = _authoritativePoster?.Title;
        _renderedSubtitle = _authoritativePoster?.Subtitle;
        if (!changed || _window is null) return;
        if (paintOnlyTemplate)
        {
            ApplyWindowBackdrop(_window); RefreshTemplatePaint();
            return;
        }
        ReleaseScene();
        CancelContentGesture("presentation-changed");
        StopLocate();
        RestoreTransitionLayout();
        _posterMenu?.Hide();
        StopExpansionFeedback();
        _itemColumnCount = -1;
        Presenter.ClearVisuals();
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
        static bool XamlNumber(double actual,double expected)=>double.IsFinite(actual) && double.IsFinite(expected) &&
            (actual==expected || actual==(double)(float)expected);
        var expected=target.Presentation;
        if(!PresentationReadbackEquality.Same(_presentation,expected) || _renderedTitle!=target.Poster.Title || _window?.Title!="DesktopUp V3 · "+target.Poster.Title ||
            !VerifyWindowBackdrop() || _surface?.Child is not Grid structure ||
            !XamlNumber(structure.RowDefinitions[1].Height.Value,target.Poster.IsCollapsed?0:expected.MetaHeightDip) ||
            Presenter.Surface is null || _itemsGrid?.Children.Contains(Presenter.Surface)!=true || _contentLayout.Snapshot is not { } snapshot ||
            snapshot.PosterId!=target.Poster.PosterId || snapshot.DocumentVersion!=ContentDocument.DocumentVersion) return false;
        foreach(var pair in Presenter.Cards)
        {
            var slot=snapshot.Items.FirstOrDefault(p=>p.ItemId==pair.Key);
            if(slot is null || !XamlNumber(pair.Value.Width,slot.Bounds.Width) || !XamlNumber(pair.Value.Height,slot.Bounds.Height) ||
                !XamlNumber(Canvas.GetLeft(pair.Value),slot.Bounds.X-snapshot.ScrollXDip) ||
                !XamlNumber(Canvas.GetTop(pair.Value),slot.Bounds.Y-snapshot.ScrollYDip)) return false;
        }
        return true;
    }
}
