using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Xaml.Controls;

namespace DesktopUp;

public partial class App
{
    private async Task VerifyTemplateComponentsForProbe(string posterId, CancellationToken cancellation)
    {
        var coordinator = (StateProjectionCoordinator)_stateCoordinator!;
        var spatial = _spatialRuntime!;
        var window = spatial.BackdropWindowForProbe(posterId);
        _controlCenter!.Show();
        async Task Until(Func<bool> predicate)
        {
            while (!predicate()) await Task.Delay(50, cancellation);
        }
        await Until(() => _controlCenter.SettingsWindowForContractProbe is not null);
        var center = _controlCenter.SettingsWindowForContractProbe!;
        int number = 0;
        foreach (var components in new TemplateHeadingComponent[][] {
            [TemplateHeadingComponent.Count, TemplateHeadingComponent.Title], [TemplateHeadingComponent.Subtitle], [] })
        {
            var basis = TemplateRecipeCatalog.BuiltIns.Single(r => r.Id == "basic-box");
            var recipe = basis with
            {
                Schema = basis.Schema with { TemplateId = "probe-components-" + ++number, SchemaVersion = 2 },
                Base = basis.Base with { Heading = components.Length > 0 }, Components = components
            };
            var current = coordinator.Refresh();
            var result = coordinator.Execute(new RegisterTemplateCommand(Guid.NewGuid().ToString(), StateCommandSource.ControlCenter,
                current.Revision, StateReadSet.ForPoster(posterId, current.PostersById[posterId].EntityVersion) with
                { SettingsVersion = current.ControlCenter.Settings.EntityVersion }, TemplateRecipeCodec.Serialize(recipe), posterId));
            if (!result.Succeeded) throw new IOException(result.Error);
            await Until(() => !spatial.IsSpatialBusy && window.ReadTemplateComponentsForProbe(recipe.Id, components) &&
                center.ReadTemplateComponentsForProbe(components));
            foreach (int show in new[] { 0, 1 })
            {
                result = coordinator.Execute(new PosterSettingEdit(coordinator.Refresh().PostersById[posterId],
                    PosterSetting.TitleVisibility, Number: show).Command(Guid.NewGuid().ToString()));
                if (!result.Succeeded) throw new IOException(result.Error);
                await Until(() => !spatial.IsSpatialBusy && window.VerifyTitleVisibilityForProbe(show == 1) &&
                    window.ReadTemplateComponentsForProbe(recipe.Id, components));
            }
        }
        _log!.Write("info", "contract.template_components_passed", new {
            cases = number, mountedControls = true, omittedRowsReclaimed = true, titleAndCountAddressedByRole = true,
            sameGenerationInput = true, nativeMouse = false, pixelCertification = false });
    }
}

internal sealed partial class PosterVisualWindow
{
    internal bool ReadTemplateComponentsForProbe(string recipeId, IReadOnlyList<TemplateHeadingComponent> expected)
    {
        if (_presentation.SelectedRecipeId != recipeId || _metaPanel is null ||
            !_metaPanel.Children.Cast<TextBlock>().Select(t => (TemplateHeadingComponent)t.Tag).SequenceEqual(expected) ||
            ReadContentSurfaceForSmoke() is not { CanvasMatches: true, InputMatches: true }) return false;
        foreach (TextBlock text in _metaPanel.Children)
        {
            string value = (TemplateHeadingComponent)text.Tag switch
            {
                TemplateHeadingComponent.Subtitle => _authoritativePoster!.Subtitle,
                TemplateHeadingComponent.Title => _authoritativePoster!.Title,
                _ => $"{ContentDocument.Placements.Count:00} ITEMS"
            };
            if (text.Text != value) return false;
        }
        return (expected.Count > 0) == (MetaHeightDip > 0) &&
            (_metaTitleText is not null) == expected.Contains(TemplateHeadingComponent.Title) &&
            (_metaCountText is not null) == expected.Contains(TemplateHeadingComponent.Count);
    }
}

public sealed partial class ControlCenterWindow
{
    internal bool ReadTemplateComponentsForProbe(IReadOnlyList<TemplateHeadingComponent> expected) =>
        (ContentHeadingFolder is not null) == (expected.Count > 0) &&
        (ContentHeadingSubtitle is not null) == expected.Contains(TemplateHeadingComponent.Subtitle) &&
        (ContentHeadingRename is not null) == expected.Contains(TemplateHeadingComponent.Title);
}
