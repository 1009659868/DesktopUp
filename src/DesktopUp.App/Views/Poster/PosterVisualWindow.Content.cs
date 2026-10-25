using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Xaml.Controls;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    private PosterContentPresenter? _presenter;
    private PosterContentPresenter Presenter => _presenter ??= new(ApplyTemplateCardPaint);
    private ContentLayoutDocument? _contentDocument;
    private string? _contentRaw, _contentOwner, _selectedContentItem;
    private bool _contentReadOnly;
    private bool _contentParseReadOnly;
    private readonly ContentPresentationLayout _contentLayout = new();
    private double _contentScrollX;
    private ContentLayoutDocument ContentDocument
    {
        get
        {
            string owner = PosterId ?? inputSession.PosterId;
            string? raw = _authoritativePoster?.ContentLayoutJson;
            if (_contentDocument is null || owner != _contentOwner || raw != _contentRaw)
            {
                _contentOwner = owner; _contentRaw = raw;
                _contentParseReadOnly = !ContentLayoutPolicy.TryRead(raw, owner, out _contentDocument, out var error);
                _contentReadOnly = _contentParseReadOnly;
                // Unknown raw bytes remain authoritative. Empty read-only fallback never overwrites them.
                _contentDocument ??= ContentLayoutSamples.Equal(owner, 0);
                ResetContentProposals(_contentDocument);
                _contentSelection = _contentSelection.Prune(_contentDocument.Placements.Select(p => p.ItemId).ToArray());
                _contentSession.ClearDraft(); _contentLayout.Invalidate();
                if (_contentReadOnly) log.Write("warning", "content.layout_read_only", new { PosterId, error });
                if (_selectedContentItem is { } selected && !_contentDocument.Placements.Any(p => p.ItemId == selected)) _selectedContentItem = null;
            }
            return _contentSession.Draft ?? _contentDocument;
        }
    }
    private double ContentLeftDip => HorizontalPaddingDip + (_surface?.BorderThickness.Left ?? 0);
    private double ContentTopDip => HeaderHeightDip + MetaHeightDip + ItemsTopInsetDip + (_surface?.BorderThickness.Top ?? 0);
    private ContentLayoutViewport ContentViewportFor(DesktopPixelRect frame)
    {
        double sx = _monitor?.ScaleX ?? 1, sy = _monitor?.ScaleY ?? 1;
        double borderX = (_surface?.BorderThickness.Left ?? 0) + (_surface?.BorderThickness.Right ?? 0);
        double borderY = (_surface?.BorderThickness.Top ?? 0) + (_surface?.BorderThickness.Bottom ?? 0);
        return new ContentLayoutViewport(Math.Max(0, frame.Width / sx - HorizontalPaddingDip * 2 - borderX),
            Math.Max(0, frame.Height / sy - HeaderHeightDip - MetaHeightDip - ItemsTopInsetDip - FooterHeightDip - borderY),
            _contentScrollX, _itemScrollOffsetDip, _contentLayout.Generation).WithPresentation(_presentation.Parameters);
    }
    internal bool TryReadContentFrameMinimum(out int width, out int height)
    {
        width = height = 0;
        if (_disposed || _monitor is null || _authoritativePoster is null) return false;
        var snapshot = EnsureContentSnapshot(_bounds);
        if (_contentParseReadOnly || _contentLayout.HasFault ||
            !ContentFrameMinimumPolicy.TryForPoster(_authoritativePoster, out var authored, out _,
                heatLowerBound: _contentSession.HasContinuousHeat)) return false;
        var effective = ContentFrameMinimumPolicy.FromSnapshot(snapshot,
            HorizontalPaddingDip * 2 + ContentFrameMinimumPolicy.EditingBorderDip,
            HeaderHeightDip + MetaHeightDip + ItemsTopInsetDip + FooterHeightDip + ContentFrameMinimumPolicy.EditingBorderDip);
        // A currently accepted heat projection may have larger cards than its author basis.
        (width, height) = (_contentSession.HasContinuousHeat ? authored! :
            new ContentFrameMinimum(Math.Max(authored!.WidthDip, effective.WidthDip),
                Math.Max(authored.HeightDip, effective.HeightDip))).Pixels(_monitor.DpiX, _monitor.DpiY);
        return true;
    }
    private ContentLayoutSnapshot EnsureContentSnapshot(DesktopPixelRect frame)
    {
        _ = ContentDocument;
        _contentSession.Reconcile(ContentDocument, ContentViewportFor(frame));
        bool wasFault = _contentLayout.HasFault || _contentLayout.Snapshot?.IsLayoutReadOnly == true;
        var prepared = _contentLayout.Prepare(EffectiveContentDocument, ContentViewportFor(frame),
            ContentSurface(frame), HasContentGesture, _transitionLayout is not null,
            _geometry.IsCollapsed, _contentSession.AtSpaceSafePoint);
        var snapshot = prepared.Snapshot;
        _contentSession.SetSpaceRestricted(prepared.BrowseMode == ContentBrowseMode.ReadOnlyOverflow);
        _contentReadOnly = _contentParseReadOnly || prepared.Fault is not null ||
            snapshot.IsLayoutReadOnly || prepared.Space?.IsReadOnly == true;
        if (wasFault) _contentNotice = null;
        if (prepared.Fault is { } fault)
        {
            _contentNotice = "布局不能安全呈现，已保留原文并禁用编辑：" + fault;
            log.Write("warning", "content.layout_projection_rejected", new { PosterId, Message = fault });
        }
        else if (snapshot.IsLayoutReadOnly)
            _contentNotice = snapshot.SpaceStatus == ContentLayoutSpaceStatus.LegacyConstraintConflict
                ? "旧布局约束超出当前内容空间，原配置已保留；暂不可编辑。"
                : IsContentSpaceRestricted ? "屏幕空间受限：方向按钮或滚轮浏览，Shift＋滚轮切换另一轴；原布局保留，恢复空间后自动退出。"
                : "当前内容空间不足，卡片尺寸和原布局已保留；暂不可编辑。";
        _contentScrollX = snapshot.ScrollXDip; _itemScrollOffsetDip = snapshot.ScrollYDip;
        return snapshot;
    }

    private void DrawContentSnapshot(ContentLayoutSnapshot snapshot, ContentLayoutViewport viewport)
    {
        if (_itemsGrid is null) return;
        var doc = ContentDocument;
        var views = ReadContentViews();
        var incoming = IncomingPaintPreview(viewport);
        SyncContentAssets(EffectiveContentDocument, snapshot, viewport, views, incoming);
        Presenter.VisibleKeys = Rasters.VisibleKeys;
        ContentCardGhost? ghost = null;
        if (!IsRealContent && _contentGesture is { IsDragging: true, CanEdit: true,
            Target: PosterInputTargetKind.ContentItem } gesture &&
            views.FirstOrDefault(v => v.ItemId == gesture.Slot.ItemId) is { } dragged)
            ghost = new(dragged, gesture.Document.Placements[gesture.Slot.SourceIndex],
                gesture.Slot, gesture.GhostX, gesture.GhostY);
        Presenter.Present(_itemsGrid, new(doc, snapshot, viewport, _contentLayout.BrowseMode, views, incoming,
            SelectedContentIds(), _contentSelection.ShowHighlight, IsEditing, _selectedContentItem,
            _geometry.IsLocked, _contentReadOnly, IsRealContent, _presentation, _inlineRenameItem,
            _contentParseReadOnly, _contentLayout.HasFault, ghost));
        _inputIsland?.RefreshContentHover();
        _itemColumnCount = snapshot.Columns;
        string kind = !TestContentFeedbackEnabled || IsRealContent || doc.Placements.Count == 0 ? "ITEMS" : "DEMO ITEMS";
        if (_footer?.Children.OfType<TextBlock>().FirstOrDefault() is { } count) count.Text = $"{doc.Placements.Count} {kind}";
        if (_metaPanel?.Children.OfType<TextBlock>().LastOrDefault() is { } meta) meta.Text = $"{doc.Placements.Count:00} {kind}";
        RenderContentFeedback();
        DrawContentMarquee();
    }

    private static string TileKey(ContentLayoutTile tile) => ContentLayoutTileIdentity.Key(tile);

    private void OnContentHover(PointerScreenPoint? point)
    {
        if (point is { } p && !_disposed && !_geometry.IsCollapsed && !_contentSession.IsSuspended &&
            !HasContentGesture && !HasFileAction && _transitionLayout is null && _incomingLayout is null &&
            _monitor is { } monitor && Presenter.Frame is { } frame &&
            inputRouter.ReadSurface(inputSession)?.Layout.ContentSnapshotGeneration == frame.Snapshot.ViewportGeneration)
        {
            double x = (p.X - _bounds.X) / monitor.ScaleX - ContentLeftDip;
            double y = (p.Y - _bounds.Y) / monitor.ScaleY - ContentTopDip;
            Presenter.HoverAt(x, y);
            return;
        }
        Presenter.SetHover(null);
    }

    private void RebuildItemGrid(double widthDip)
    {
        var frame = _bounds.IsUsable ? _bounds : new DesktopPixelRect(0,0,(int)Math.Ceiling(widthDip*(_monitor?.ScaleX??1)),(int)Math.Ceiling(_geometry.ExpandedHeightDip*(_monitor?.ScaleY??1)));
        frame = frame with { Width=(int)Math.Ceiling(widthDip*(_monitor?.ScaleX??1)) };
        var snapshot = EnsureContentSnapshot(frame);
        DrawContentSnapshot(snapshot, _contentLayout.Viewport!);
    }
    private void UpdateItemScrollState(DesktopPixelRect frame)
    {
        var snapshot = EnsureContentSnapshot(frame);
        DrawContentSnapshot(snapshot,_contentLayout.Viewport!);
        UpdateScene();
    }
    private void ScrollItems(int wheelDelta, bool crossAxis = false)
    {
        if (_contentSession.IsSuspended || _monitor is null || _inputIsland is null || _geometry.IsCollapsed || wheelDelta==0 || HasContentGesture || _inlineRenameItem is not null || _transitionLayout is not null) return;
        // Shift exposes the other axis only after qualified physical shortage; normal browsing
        // stays single-axis. A held content gesture never competes with wheel navigation.
        var flowSnapshot = EnsureContentSnapshot(_bounds);
        // One full notch remains one grid pitch. Signed and fractional input reaches the shared
        // finite/loop policy before clamping, so the head can wrap backward and small deltas add up.
        var offsets = ContentWheelScrollPolicy.Advance(flowSnapshot, _contentLayout.Viewport!, wheelDelta, _contentLayout.BrowseMode, crossAxis);
        _contentScrollX = offsets.ScrollXDip;
        _itemScrollOffsetDip = offsets.ScrollYDip;
        RefreshContentSurface();
    }
    private void ClearContentVisuals()
    {
        ReleaseScene();
        CancelContentGesture("surface-release");
        ReleaseContentAssets();
        _contentLayout.Clear();
        Presenter.ClearVisuals();
    }
}
