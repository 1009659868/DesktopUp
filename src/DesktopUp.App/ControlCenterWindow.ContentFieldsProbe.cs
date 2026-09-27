using DesktopUp.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DesktopUp;

internal sealed record ContentFieldsProbeReadback
{
    public string? PosterId { get; init; }
    public string? SelectedItemId { get; init; }
    public ContentResolvedPlacement? SelectedSlot { get; init; }
    public long EntityVersion { get; init; }
    public long DocumentVersion { get; init; }
    public ContentLayoutMode? Mode { get; init; }
    public bool CanEdit { get; init; }
    public bool HasDraft { get; init; }
    public string? DraftPosterId { get; init; }
    public string? DraftItemId { get; init; }
    public string? DraftGroup { get; init; }
    public long? DraftEntityVersion { get; init; }
    public long? DraftDocumentVersion { get; init; }
    public string? DraftFirst { get; init; }
    public string? DraftSecond { get; init; }
    public string ColumnSpan { get; init; } = "";
    public string RowSpan { get; init; } = "";
    public string Order { get; init; } = "";
    public string Column { get; init; } = "";
    public string Row { get; init; } = "";
    public bool SpanEnabled { get; init; }
    public bool OrderEnabled { get; init; }
    public bool PositionEnabled { get; init; }
    public bool ModeEnabled { get; init; }
    public bool SpanApplyEnabled { get; init; }
    public bool SpanCancelEnabled { get; init; }
    public bool PositionApplyEnabled { get; init; }
    public bool PositionCancelEnabled { get; init; }
    public string Status { get; init; } = "";
    public string Notice { get; init; } = "";
    public bool ContentTabSelected { get; init; }
    public bool Compact { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public int RequestedWidthDip { get; init; }
    public int RequestedHeightDip { get; init; }
    public int RequestedWindowWidthPixels { get; init; }
    public int RequestedWindowHeightPixels { get; init; }
    public int ImmediateWindowWidthPixels { get; init; }
    public int ImmediateWindowHeightPixels { get; init; }
    public int WindowWidthPixels { get; init; }
    public int WindowHeightPixels { get; init; }
    public int ClientWidthPixels { get; init; }
    public int ClientHeightPixels { get; init; }
    public double RasterizationScale { get; init; }
    public double XamlRootWidthDip { get; init; }
    public double XamlRootHeightDip { get; init; }
    public double RootDesiredWidth { get; init; }
    public double MacroPairWidth { get; init; }
    public double ScaleHostWidth { get; init; }
    public double ViewportWidth { get; init; }
    public bool RootFitsClient { get; init; }
    public bool ImpactShownOnce { get; init; }
    public bool CommonSavedNoticeVisible { get; init; }
    public bool CollapsedDraftCancelVisible { get; init; }
    public string Section { get; init; } = "";
    public bool HorizontalBoundsValid { get; init; }
    public bool SectionFullyVisible { get; init; }
}

// Drivers use real control properties and the same handlers as the visible UI. They deliberately
// do not claim to synthesize native pointer/keyboard input or routed Enter/Escape events.
public sealed partial class ControlCenterWindow
{
    private int _contentProbeWidthDip, _contentProbeHeightDip, _contentProbeWidthPixels, _contentProbeHeightPixels;
    private global::Windows.Graphics.SizeInt32 _contentProbeImmediateSize;
    internal void PrepareContentFieldsForProbe(int widthDip, int heightDip)
    {
        double scale = Content.XamlRoot?.RasterizationScale ?? throw new InvalidOperationException("Center is not connected.");
        _contentProbeWidthDip = widthDip; _contentProbeHeightDip = heightDip;
        _contentProbeWidthPixels = checked((int)Math.Ceiling(widthDip * scale));
        _contentProbeHeightPixels = checked((int)Math.Ceiling(heightDip * scale));
        PrepareUiCSampleForProbe(false, _contentProbeWidthPixels, _contentProbeHeightPixels);
        _contentProbeImmediateSize = AppWindow.Size;
        ShowWorkspacePage(false); ContentFineTune.IsExpanded = true;
    }
    internal void RevealContentFieldSectionForProbe(string section)
    {
        ShowWorkspacePage(false); ContentFineTune.IsExpanded = true;
        FrameworkElement anchor = section switch
        {
            "span" => ContentColumns,
            "position" => ContentColumnPosition,
            "order" => ContentOrder,
            _ => throw new ArgumentOutOfRangeException(nameof(section))
        };
        anchor.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false, VerticalAlignmentRatio = .2 });
    }
    internal void SelectContentItemForProbe(string itemId)
    {
        ShowWorkspacePage(false); ContentFineTune.IsExpanded = true;
        ContentItemSelector.SelectedItem = ((IReadOnlyList<ContentItemView>)ContentItemSelector.ItemsSource).Single(item => item.ItemId == itemId);
    }
    internal void SetContentFieldsForProbe(string group, string first, string? second = null)
    {
        var boxes = ContentFieldBoxes(ParseContentProbeGroup(group));
        boxes.First.Text = first;
        if (second is not null && boxes.Second is { } last) last.Text = second;
    }
    internal void ApplyContentFieldsForProbe(string group) => ContentFieldApply_Click(ContentProbeButton(group, false), new RoutedEventArgs());
    internal void CancelContentFieldsForProbe(string group) => ContentFieldCancel_Click(ContentProbeButton(group, true), new RoutedEventArgs());
    internal void SetContentModeForProbe(bool manual) => ContentModeSelector.SelectedIndex = manual ? 1 : 0;
    private static ContentFieldGroup ParseContentProbeGroup(string group) => group switch
    { "span" => ContentFieldGroup.Span, "order" => ContentFieldGroup.Order, "position" => ContentFieldGroup.Position, _ => throw new ArgumentOutOfRangeException(nameof(group)) };
    private Button ContentProbeButton(string group, bool cancel) => (ParseContentProbeGroup(group), cancel) switch
    {
        (ContentFieldGroup.Span, false) => ContentSpanApply,
        (ContentFieldGroup.Span, true) => ContentSpanCancel,
        (ContentFieldGroup.Order, false) => ContentOrderApply,
        (ContentFieldGroup.Order, true) => ContentOrderCancel,
        (ContentFieldGroup.Position, false) => ContentPositionApply,
        _ => ContentPositionCancel
    };
    internal ContentFieldsProbeReadback ReadContentFieldsForProbe(string section = "span")
    {
        var view = _contentRuntime?.Invoke()?.ReadContentEditor(_view?.SelectedPosterId);
        FrameworkElement[] controls = section switch
        {
            "span" => [ContentColumns, ContentRows, ContentSpanApply, ContentSpanCancel],
            "position" => [ContentColumnPosition, ContentRowPosition, ContentPositionApply, ContentPositionCancel],
            "order" => [ContentOrder, ContentOrderApply, ContentOrderCancel],
            _ => throw new ArgumentOutOfRangeException(nameof(section))
        };
        ScrollViewer? viewport = null;
        for (DependencyObject? cursor = ContentLayoutHost; cursor is not null; cursor = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(cursor))
            if (cursor is ScrollViewer found) { viewport = found; break; }
        bool Fits(FrameworkElement control, bool vertical)
        {
            if (viewport is null || !control.IsLoaded || control.Visibility != Visibility.Visible || control.ActualWidth <= 0 || control.ActualHeight <= 0) return false;
            var point = control.TransformToVisual(viewport).TransformPoint(new(0, 0));
            return double.IsFinite(point.X) && double.IsFinite(point.Y) && point.X >= -1 && point.X + control.ActualWidth <= viewport.ActualWidth + 1 &&
                (!vertical || point.Y >= -1 && point.Y + control.ActualHeight <= viewport.ActualHeight + 1);
        }
        var nativeSize = AppWindow.Size; var client = AppWindow.ClientSize;
        double scale = Content.XamlRoot?.RasterizationScale ?? 1;
        bool rootFitsClient = UiCRoot.ActualWidth <= client.Width / scale + 1 && UiCRoot.ActualHeight <= client.Height / scale + 1;
        return new()
        {
            PosterId = _view?.SelectedPosterId, SelectedItemId = view?.SelectedItemId, SelectedSlot = view?.SelectedSlot,
            EntityVersion = view?.EntityVersion ?? 0, DocumentVersion = view?.Document.DocumentVersion ?? 0,
            Mode = view?.Document.Mode, CanEdit = view?.CanEdit == true, HasDraft = IsContentFieldDraftActive,
            DraftPosterId = _contentFieldDraft?.PosterId, DraftItemId = _contentFieldDraft?.ItemId,
            DraftGroup = _contentFieldDraft?.Group.ToString(), DraftEntityVersion = _contentFieldDraft?.EntityVersion,
            DraftDocumentVersion = _contentFieldDraft?.DocumentVersion, DraftFirst = _contentFieldDraft?.First,
            DraftSecond = _contentFieldDraft?.Second, ColumnSpan = ContentColumns.Text, RowSpan = ContentRows.Text,
            Order = ContentOrder.Text, Column = ContentColumnPosition.Text, Row = ContentRowPosition.Text,
            SpanEnabled = ContentColumns.IsEnabled && ContentRows.IsEnabled, OrderEnabled = ContentOrder.IsEnabled,
            PositionEnabled = ContentColumnPosition.IsEnabled && ContentRowPosition.IsEnabled, ModeEnabled = ContentModeSelector.IsEnabled,
            SpanApplyEnabled = ContentSpanApply.IsEnabled, SpanCancelEnabled = ContentSpanCancel.IsEnabled,
            PositionApplyEnabled = ContentPositionApply.IsEnabled, PositionCancelEnabled = ContentPositionCancel.IsEnabled,
            Status = ContentFieldStatus.Text, Notice = ContentLayoutNotice.Text, ContentTabSelected = IsAppearanceWorkspace && ContentFineTune.IsExpanded,
            Compact = _uiCCompact == true, Width = UiCRoot.ActualWidth, Height = UiCRoot.ActualHeight, Section = section,
            RequestedWidthDip = _contentProbeWidthDip, RequestedHeightDip = _contentProbeHeightDip,
            RequestedWindowWidthPixels = _contentProbeWidthPixels, RequestedWindowHeightPixels = _contentProbeHeightPixels,
            ImmediateWindowWidthPixels = _contentProbeImmediateSize.Width, ImmediateWindowHeightPixels = _contentProbeImmediateSize.Height,
            WindowWidthPixels = nativeSize.Width, WindowHeightPixels = nativeSize.Height,
            ClientWidthPixels = client.Width, ClientHeightPixels = client.Height, RasterizationScale = scale,
            XamlRootWidthDip = Content.XamlRoot?.Size.Width ?? 0, XamlRootHeightDip = Content.XamlRoot?.Size.Height ?? 0,
            RootDesiredWidth = UiCRoot.DesiredSize.Width, MacroPairWidth = MacroPair.ActualWidth,
            ScaleHostWidth = ScaleHost.ActualWidth, ViewportWidth = viewport?.ActualWidth ?? 0, RootFitsClient = rootFitsClient,
            ImpactShownOnce = !IsContentFieldDraftActive || ContentFineTune.IsExpanded && !string.IsNullOrEmpty(ContentFieldStatus.Text) &&
                (ContentLayoutNotice.Visibility == Visibility.Collapsed || ContentLayoutNotice.Text != ContentFieldStatus.Text) ||
                !ContentFineTune.IsExpanded && ContentLayoutNotice.Visibility == Visibility.Visible && !string.IsNullOrEmpty(ContentLayoutNotice.Text),
            CommonSavedNoticeVisible = CommonSettingsNotice.Visibility == Visibility.Visible && CommonSettingsNotice.Text.StartsWith("已保存", StringComparison.Ordinal),
            CollapsedDraftCancelVisible = ContentDraftSummary.Visibility == Visibility.Visible,
            HorizontalBoundsValid = rootFitsClient && controls.All(control => Fits(control, false)), SectionFullyVisible = rootFitsClient && controls.All(control => Fits(control, true))
        };
    }
}
