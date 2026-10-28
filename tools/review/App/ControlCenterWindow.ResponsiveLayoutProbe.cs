using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DesktopUp;

internal sealed record UiCSampleObservation(double ContentWidth, double ContentHeight, bool Compact,
    bool FolderExpanded, bool ControlsConnected, bool MacrosInSettingsFolder, bool HorizontalBoundsValid,
    string TemplateKey, string TemplateSummary, string? SelectedPosterId, int PosterCount, long Revision)
{
    public int VisualSampleCount { get; init; }
    public bool ParallelMacros { get; init; }
    public bool BrowsingInAppearance { get; init; }
    public bool FineTuneCollapsed { get; init; }
    public bool AdvancedCollapsed { get; init; }
    public bool GlobalControlsSeparated { get; init; }
    public bool BehaviorActionsGrouped { get; init; }
    public bool BehaviorSelected { get; init; }
    public bool NativeTitleWithoutBrandHeader { get; init; }
    public bool RecipeEntrypointsConnected { get; init; }
    public bool SettingsExpanded { get; init; }
    public bool ItemToolsOutsideSettings { get; init; }
}

public sealed partial class ControlCenterWindow
{
    // Isolated App probes call this, then wait for layout before reusing CaptureSettingsForProbeAsync.
    // Width/height are native pixels; this does not synthesize input or write a user preference.
    internal void PrepareUiCSampleForProbe(bool folderExpanded, int widthPixels, int heightPixels, bool behavior = false)
    {
        if (widthPixels is < 320 or > 4096 || heightPixels is < 320 or > 4096)
            throw new ArgumentOutOfRangeException(nameof(widthPixels), "Sample window bounds must be 320..4096 pixels.");
        ShowWorkspacePage(behavior);
        UiCTemplateFolder.IsExpanded = folderExpanded;
        AppWindow.Resize(new global::Windows.Graphics.SizeInt32(widthPixels, heightPixels));
    }

    internal UiCSampleObservation ReadUiCSampleForProbe()
    {
        static bool FitsHorizontally(FrameworkElement child, FrameworkElement parent)
        {
            if (!child.IsLoaded || !parent.IsLoaded || child.ActualWidth <= 0 || parent.ActualWidth <= 0) return false;
            double left = child.TransformToVisual(parent).TransformPoint(new(0, 0)).X;
            return double.IsFinite(left) && left >= -1 && left + child.ActualWidth <= parent.ActualWidth + 1;
        }
        bool bounds = FitsHorizontally(UiCSidebar, UiCRoot) && FitsHorizontally(UiCInspector, UiCRoot) &&
            FitsHorizontally(PosterSelector, UiCSidebar) && FitsHorizontally(CreatePosterButton, UiCSidebar) &&
            FitsHorizontally(PosterTitleText, UiCInspector) && FitsHorizontally(LocateButton, UiCInspector) &&
            FitsHorizontally(MoreActionsButton, UiCInspector) && (IsAppearanceWorkspace ? FitsHorizontally(UiCTemplateFolder, UiCInspector) &&
            FitsHorizontally(LayoutFolder, UiCInspector) && (!LayoutFolder.IsExpanded ||
            FitsHorizontally(ScaleHost, UiCInspector) && FitsHorizontally(SpacingHost, UiCInspector)) &&
            (!UiCTemplateFolder.IsExpanded || _templateSamples.Values.All(button => FitsHorizontally(button, UiCTemplateFolder))) :
            FitsHorizontally(LockButton, UiCInspector) && FitsHorizontally(CollapseButton, UiCInspector) && FitsHorizontally(VisibilityButton, UiCInspector));
        return new(UiCRoot.ActualWidth, UiCRoot.ActualHeight, _uiCCompact == true,
            UiCTemplateFolder.IsExpanded, TemplateSection.Children.Contains(_template) &&
            ScaleHost.Children.OfType<Grid>().Any(g => g.Children.Contains(ScaleSlider)) &&
            SpacingHost.Children.OfType<Grid>().Any(g => g.Children.Contains(SpacingSlider)) &&
            _templateSamples.Count == 3 && AppearanceTab.Parent == BehaviorTab.Parent,
            MacroPair.Children.Contains(ScaleHost) && MacroPair.Children.Contains(SpacingHost) && LayoutFolderPanel.Children.Contains(MacroPair) &&
            LayoutFolderPanel.Children.Contains(TitleCard) && TitleCard.Child == TitleColorsFolder && LayoutFolderPanel.Children.Contains(ContentLayoutStatus), bounds,
            SelectedTemplateKey ?? _view?.SelectedPoster?.Poster.TemplateKey ?? "",
            UiCTemplateSummary.Text, _view?.SelectedPosterId, _view?.Posters.Count ?? 0,
            _view?.Projection.Revision ?? 0)
        {
            VisualSampleCount = _templateSamples.Count,
            ParallelMacros = Grid.GetColumn(ScaleHost) == 0 && Grid.GetColumn(SpacingHost) == 1 && ScaleHost.Parent == SpacingHost.Parent,
            BrowsingInAppearance = AppearanceSection.Children.Contains(ContentLayoutHost) &&
                LayoutFolderPanel.Children.OfType<Grid>().Any(g => g.Children.Contains(ContentLoopSwitch)),
            FineTuneCollapsed = !ContentFineTune.IsExpanded,
            AdvancedCollapsed = !AdvancedSettings.IsExpanded,
            // A closed Flyout has no connected visual Parent; inspect its declared content.
            GlobalControlsSeparated = UiCGlobalPanel.Children.Contains(SettingsDetailText) && UiCGlobalPanel.Children.Contains(PauseButton),
            BehaviorActionsGrouped = BehaviorScroll.Content is Panel behavior &&
                new[] { LockButton, CollapseButton, VisibilityButton }.All(button => behavior.Children.OfType<Panel>().Any(row => row.Children.Contains(button))),
            BehaviorSelected = !IsAppearanceWorkspace,
            NativeTitleWithoutBrandHeader = !ExtendsContentIntoTitleBar && Title == "DesktopUp · 控制中心" && UiCRoot.FindName("UiCCaption") is null,
            RecipeEntrypointsConnected = TemplateActionsHost.Children.Count == 3 && TemplateLibraryActionsHost.Children.Count == 1,
            SettingsExpanded = LayoutFolder.IsExpanded,
            ItemToolsOutsideSettings = ContentItemSelector.Parent is Grid itemRow && itemRow.Parent == ContentLayoutPanel && ContentFineTune.Parent == ContentLayoutPanel
        };
    }
    internal void ExpandLayoutFolderForProbe(bool expanded)
    {
        LayoutFolder.IsExpanded = expanded;
        UiCRoot.UpdateLayout();
        AppearanceScroll.ChangeView(null, 0, null, true);
    }
}
