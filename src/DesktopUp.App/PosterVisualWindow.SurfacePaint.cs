using DesktopUp.Application;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    private DesktopUp.Domain.ResolvedPosterPresentation? _sceneParametersSource;
    private SceneRenderParameters? _sceneParameters;
    private SceneRenderParameters CurrentSceneParameters
    {
        get
        {
            if (!ReferenceEquals(_sceneParametersSource, _presentation))
            {
                _sceneParameters = ScenePresentationAdapter.Resolve(_presentation);
                _sceneParametersSource = _presentation;
            }
            return _sceneParameters!;
        }
    }

    private void RefreshTemplatePaint()
    {
        RefreshTitleChrome();
        UpdateScene();
        PaintSceneCards();
    }

    private void PaintSceneCards()
    {
        foreach (var cell in _contentCards.Values.OfType<Grid>())
            if (cell.Children.OfType<Border>().FirstOrDefault() is { } card)
                ApplyTemplateCardPaint(card, card.BorderThickness.Left > 1, cell);
    }

    private void ApplyTemplateCardPaint(Border card, bool selected, Grid? cell = null)
    {
        // Real references are icon-only surfaces. Scene ticks/theme changes must not paint
        // a card plate back underneath them; geometry and transparent hit regions stay intact.
        if (IsRealContent) { card.Background = null; return; }
        // Static surface remains available before a module is created, after a module fails,
        // or when the registry has no decoration. Never change dimensions or selection state.
        if ((_sceneInstance is null || _sceneFailure.Failure is not null) &&
            CurrentSceneParameters.Surface.Card is { } color)
        {
            var material = ColorBrush(color).Color;
            if (card.Background is SolidColorBrush brush) brush.Color = material;
            else card.Background = new SolidColorBrush(material);
        }
        if (_sceneInstance is null || _sceneFailure.Failure is not null) return;
        RunScene(() => _sceneInstance.PaintCard(new(card, selected,
            cell is null ? null : ContentLeftDip + Canvas.GetLeft(cell),
            cell is null ? null : ContentTopDip + Canvas.GetTop(cell))));
    }
}
