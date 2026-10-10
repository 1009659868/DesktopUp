using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DesktopUp;


public sealed partial class ControlCenterWindow
{
    // Presentation only. Selection, template edits and sliders retain their existing command owners.
    private bool? _uiCCompact;

    private void InitializeUiCSample()
    {
        _template.HorizontalAlignment = HorizontalAlignment.Stretch;
        _template.MaxWidth = 320;
        _template.SelectionChanged += (_, _) => { RenderUiCTemplateSummary(); RenderTemplateSamples(); };
        InitializeUiCWorkspace();
        UiCRoot.SizeChanged += (_, _) => ArrangeUiCShell();
        UiCRoot.Loaded += (_, _) => ArrangeUiCShell();
        RenderUiCTemplateSummary();
    }

    private void RenderUiCTemplateSummary()
    {
        string? key = SelectedTemplateKey ?? _view?.SelectedPoster?.Poster.TemplateKey;
        UiCTemplateSummary.Text = _view?.SelectedPoster is null ? "请选择海报" : _templateCatalog.FirstOrDefault(r => r.Id == key)?.Name ?? "模板暂不可用";
        ToolTipService.SetToolTip(UiCTemplateSummary, "样式只作用于当前海报；折叠样片夹会保留设置。");
    }

    private void ArrangeUiCShell()
    {
        double width = UiCRoot.ActualWidth;
        if (width <= 0) return;
        bool compact = width < 720;
        if (_uiCCompact == compact) return;
        _uiCCompact = compact;
        UiCSidebarColumn.Width = new GridLength(compact ? 0 : 170);
        // Preserve one complete title/summary row; the collapsed middle row still consumes spacing.
        UiCSidebarRow.Height = compact ? new GridLength(126) : new GridLength(1, GridUnitType.Star);
        UiCSidebarPanel.RowSpacing = compact ? 6 : 10;
        UiCInspectorRow.Height = compact ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        Grid.SetColumnSpan(UiCSidebar, compact ? 2 : 1);
        UiCSidebar.BorderThickness = compact ? new Thickness(0, 0, 0, 1) : new Thickness(0, 0, 1, 0);
        UiCSidebar.Padding = compact ? new Thickness(16, 10, 16, 8) : new Thickness(14, 22, 14, 12);
        UiCSidebarPanel.RowDefinitions[1].Height = compact ? new GridLength(0) : GridLength.Auto;
        Grid.SetRow(CreatePosterButton, compact ? 0 : 1);
        CreatePosterButton.HorizontalAlignment = compact ? HorizontalAlignment.Right : HorizontalAlignment.Stretch;
        Grid.SetRow(UiCInspector, compact ? 1 : 0);
        Grid.SetColumn(UiCInspector, compact ? 0 : 1);
        Grid.SetColumnSpan(UiCInspector, compact ? 2 : 1);
        UiCInspector.Margin = compact ? new Thickness(16, 12, 16, 8) : new Thickness(26, 22, 22, 12);
    }

}
