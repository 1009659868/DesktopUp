using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    private Canvas? _contentCanvas;
    private ContentLayoutDocument? _contentDocument, _contentDraft, _contentDraftProjection;
    private string? _contentRaw, _contentOwner, _selectedContentItem;
    private bool _contentReadOnly;
    private bool _contentParseReadOnly, _contentProjectionFault;
    private ContentLayoutSnapshot? _contentSnapshot;
    private ContentLayoutViewport? _contentViewport;
    private ContentLayoutDocument? _solvedDocument;
    private long _contentGeneration;
    private double _contentScrollX;
    private ContentBrowseAnchor? _contentEndAnchor;
    private readonly Dictionary<string, FrameworkElement> _contentCards = new(StringComparer.Ordinal);
    private sealed record CardPaint(ContentItemView View, ContentImageFit Fit, double Width, double Height,
        int Columns, int Rows, int WantedColumns, int WantedRows, bool Fixed, ContentPinAdornment Pin, int? PinIndex,
        bool Selected, bool Handle, bool Spans, bool Real, ResolvedPosterPresentation Presentation, ContentAssetKey? Asset);
    private readonly Dictionary<string, CardPaint> _contentCardPaint = new(StringComparer.Ordinal);

    private ContentLayoutDocument ContentDocument
    {
        get
        {
            string owner = PosterId ?? inputSession.PosterId;
            string? raw = _authoritativePoster?.ContentLayoutJson;
            if (_contentDocument is null || owner != _contentOwner || raw != _contentRaw)
            {
                var previous=_contentDocument;
                _contentOwner = owner; _contentRaw = raw;
                _contentParseReadOnly = !ContentLayoutPolicy.TryRead(raw, owner, out _contentDocument, out var error);
                _contentReadOnly = _contentParseReadOnly; _contentProjectionFault=false;
                // Unknown raw bytes remain authoritative. Empty read-only fallback never overwrites them.
                _contentDocument ??= ContentLayoutSamples.Equal(owner, 0);
                ResetContentProposals(previous,_contentDocument);
                _contentSelection = _contentSelection.Prune(_contentDocument.Placements.Select(p => p.ItemId).ToArray());
                _contentDraft = _contentDraftProjection = null; _solvedDocument = null;
                if (_contentReadOnly) log.Write("warning", "content.layout_read_only", new { PosterId, error });
                if (_selectedContentItem is { } selected && !_contentDocument.Placements.Any(p => p.ItemId == selected)) _selectedContentItem = null;
            }
            return _contentDraft ?? _contentDocument;
        }
    }
    private double ContentLeftDip => HorizontalPaddingDip + (_surface?.BorderThickness.Left ?? 0);
    private double ContentTopDip => HeaderHeightDip + MetaHeightDip + ItemsTopInsetDip + (_surface?.BorderThickness.Top ?? 0);
    private ContentLayoutViewport ContentViewportFor(DesktopPixelRect frame)
    {
        double sx = _monitor?.ScaleX ?? 1, sy = _monitor?.ScaleY ?? 1;
        double borderX = (_surface?.BorderThickness.Left ?? 0) + (_surface?.BorderThickness.Right ?? 0);
        double borderY = (_surface?.BorderThickness.Top ?? 0) + (_surface?.BorderThickness.Bottom ?? 0);
        var preference = _presentation.Parameters.Preferences ?? new();
        return new(Math.Max(0, frame.Width / sx - HorizontalPaddingDip * 2 - borderX),
            Math.Max(0, frame.Height / sy - HeaderHeightDip - MetaHeightDip - ItemsTopInsetDip - FooterHeightDip - borderY),
            _contentScrollX, _itemScrollOffsetDip, _contentGeneration, preference.Scale, preference.Spacing);
    }
    internal bool TryReadContentFrameMinimum(out int width, out int height)
    {
        width = height = 0;
        if (_disposed || _monitor is null || _authoritativePoster is null) return false;
        var snapshot = EnsureContentSnapshot(_bounds);
        if (_contentParseReadOnly || _contentProjectionFault ||
            !ContentFrameMinimumPolicy.TryForPoster(_authoritativePoster, out var authored, out _)) return false;
        var effective = ContentFrameMinimumPolicy.FromSnapshot(snapshot,
            HorizontalPaddingDip * 2 + ContentFrameMinimumPolicy.EditingBorderDip,
            HeaderHeightDip + MetaHeightDip + ItemsTopInsetDip + FooterHeightDip + ContentFrameMinimumPolicy.EditingBorderDip);
        // A currently accepted heat projection may have larger cards than its author basis.
        (width, height) = new ContentFrameMinimum(Math.Max(authored!.WidthDip, effective.WidthDip),
            Math.Max(authored.HeightDip, effective.HeightDip)).Pixels(_monitor.DpiX, _monitor.DpiY);
        return true;
    }
    private ContentLayoutSnapshot EnsureContentSnapshot(DesktopPixelRect frame)
    {
        _ = ContentDocument;
        var doc = EffectiveContentDocument;
        var viewport = ContentViewportFor(frame);
        if (ReferenceEquals(_solvedDocument, doc) && _contentViewport == viewport && _contentSnapshot is not null && _contentEndAnchor is null) return _contentSnapshot;
        bool geometryChanged = _contentProjectionFault || !ReferenceEquals(_solvedDocument, doc) || _contentViewport?.WidthDip != viewport.WidthDip ||
            _contentViewport?.HeightDip != viewport.HeightDip ||
            _contentViewport?.Scale != viewport.Scale || _contentViewport?.Spacing != viewport.Spacing ||
            _contentViewport?.ThemeMetricsVersion != viewport.ThemeMetricsVersion;
        viewport = viewport with { Generation = checked(++_contentGeneration) };
        ContentLayoutSnapshot next;
        try
        {
            var anchor = _contentEndAnchor ?? (geometryChanged && !HasContentGesture && !_contentProjectionFault &&
                _contentSnapshot is not null && _contentViewport is not null
                ? ContentLayoutFlow.CaptureAnchor(_contentSnapshot, _contentViewport, _contentBrowseMode) : null);
            _contentEndAnchor = null;
            next = geometryChanged || _contentSnapshot is null
                ? ContentLayoutSolver.Resolve(doc, viewport)
                : ContentLayoutSolver.Reclip(_contentSnapshot, viewport, _contentBrowseMode);
            _contentSpaceCandidate = EvaluateContentSpace(next, viewport, frame, anchor);
            if (_contentSpaceCandidate?.Disposition == ContentSpaceDisposition.PendingSafePoint)
            {
                if (_contentSpacePublication.Published?.Witness is { } previous && previous.Environment == _contentSpaceEnvironment &&
                    previous.EffectiveFrame == PosterSpatialRuntime.Rect(frame) && _contentSnapshot is { } retained && _contentViewport is not null)
                {
                    _contentEndAnchor = anchor;
                    _solvedDocument = null;
                    return retained;
                }
                throw new ContentLayoutException("空间恢复尚未到达安全点，旧命中已停用。");
            }
            _contentBrowseMode = _contentSpaceCandidate?.BrowseMode ??
                (_transitionLayout is not null && next.SpaceStatus == ContentLayoutSpaceStatus.InsufficientCrossAxis &&
                 _contentSpacePublication.Published?.IsRestricted == true ? ContentBrowseMode.ReadOnlyOverflow : ContentBrowseMode.SingleAxis);
            if (_contentSpaceCandidate?.Offsets is { } accepted)
                next = ContentLayoutSolver.Reclip(next, viewport with { ScrollXDip = accepted.ScrollXDip, ScrollYDip = accepted.ScrollYDip }, _contentBrowseMode);
            else if (anchor is not null)
            {
                var restored = ContentLayoutFlow.RestoreAnchor(next, viewport, anchor, _contentBrowseMode);
                next = ContentLayoutSolver.Reclip(next, viewport with { ScrollXDip = restored.ScrollXDip, ScrollYDip = restored.ScrollYDip }, _contentBrowseMode);
            }
            if(_contentProjectionFault || _contentSnapshot?.IsLayoutReadOnly == true) _contentNotice=null;
            _contentProjectionFault=false; _contentReadOnly=_contentParseReadOnly || next.IsLayoutReadOnly || _contentSpaceCandidate?.IsReadOnly == true;
            if (next.IsLayoutReadOnly)
                _contentNotice = next.SpaceStatus == ContentLayoutSpaceStatus.LegacyConstraintConflict
                    ? "旧布局约束超出当前内容空间，原配置已保留；暂不可编辑。"
                    : IsContentSpaceRestricted ? "屏幕空间受限：方向按钮或滚轮浏览，Shift＋滚轮切换另一轴；原布局保留，恢复空间后自动退出。"
                    : "当前内容空间不足，卡片尺寸和原布局已保留；暂不可编辑。";
        }
        catch (ArgumentException error)
        {
            _contentProjectionFault=true; _contentReadOnly=true;
            _contentSpaceCandidate=null; _contentBrowseMode=ContentBrowseMode.SingleAxis;
            _contentNotice="布局不能安全呈现，已保留原文并禁用编辑："+error.Message;
            next=ContentLayoutSolver.Resolve(ContentLayoutSamples.Equal(doc.PosterId,0),viewport);
            log.Write("warning","content.layout_projection_rejected",new{PosterId,error.Message});
        }
        _contentScrollX = next.ScrollXDip; _itemScrollOffsetDip = next.ScrollYDip;
        _contentViewport = viewport with { ScrollXDip = next.ScrollXDip, ScrollYDip = next.ScrollYDip };
        _contentSnapshot = next; _solvedDocument = doc;
        return next;
    }

    private void DrawContentSnapshot(ContentLayoutSnapshot snapshot, ContentLayoutViewport viewport)
    {
        if (_itemsGrid is null) return;
        if (_contentCanvas is null || !_itemsGrid.Children.Contains(_contentCanvas))
        {
            _itemsGrid.Children.Clear(); _itemsGrid.ColumnDefinitions.Clear(); _itemsGrid.RowDefinitions.Clear();
            _itemsGrid.ColumnSpacing = 0; _itemsGrid.RowSpacing = 0;
            _contentCanvas = new Canvas { IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            _itemsGrid.Children.Add(_contentCanvas); _contentCards.Clear(); _contentCardPaint.Clear();
        }
        _itemsGrid.Width = viewport.WidthDip;
        _itemsGrid.Height = viewport.HeightDip;
        _contentCanvas.Width = viewport.WidthDip; _contentCanvas.Height = viewport.HeightDip;
        _contentCanvas.Clip = new RectangleGeometry { Rect = new global::Windows.Foundation.Rect(0, 0, viewport.WidthDip, viewport.HeightDip) };
        _itemColumnCount = snapshot.Columns;
        var doc = ContentDocument;
        const string itemKind = "ITEMS";
        if (_footer?.Children.OfType<TextBlock>().FirstOrDefault() is { } count) count.Text=$"{doc.Placements.Count} {itemKind}";
        if (_metaPanel?.Children.OfType<TextBlock>().LastOrDefault() is { } metaCount) metaCount.Text=$"{doc.Placements.Count:00} {itemKind}";
        // Lamp paint and host title ink never invalidate card identity or solver geometry.
        var cardPresentation = _presentation with { Parameters = _presentation.Parameters with { Lamp = null, TitleColors = null, ShowTitle = null } };
        // Retain cards in their visual parent. Only ephemeral grid/gesture overlays are rebuilt.
        var existingCards = _contentCards.Values.ToHashSet();
        foreach (var child in _contentCanvas.Children.Where(child => child is not FrameworkElement element || !existingCards.Contains(element)).ToArray())
            _contentCanvas.Children.Remove(child);
        double pitch = snapshot.CellDip + snapshot.GapDip;
        if (doc.ShowBaselineGrid)
        {
            double x0 = -(snapshot.ScrollXDip % pitch), y0 = -(snapshot.ScrollYDip % pitch);
            for (double x = x0; x <= viewport.WidthDip; x += pitch) _contentCanvas.Children.Add(new Line
            { X1=x, X2=x, Y1=0, Y2=viewport.HeightDip, Stroke=Brush(30,180,200,218), StrokeThickness=.5, IsHitTestVisible=false });
            for (double y = y0; y <= viewport.HeightDip; y += pitch) _contentCanvas.Children.Add(new Line
            { X1=0, X2=viewport.WidthDip, Y1=y, Y2=y, Stroke=Brush(30,180,200,218), StrokeThickness=.5, IsHitTestVisible=false });
        }
        var views = ReadContentViews();
        var incoming = IncomingPaintPreview(viewport);
        SyncContentAssets(EffectiveContentDocument,snapshot,viewport,views,incoming);
        var source = views.ToDictionary(p => p.ItemId, StringComparer.Ordinal);
        // Keep the author/solver/native input basis untouched. Only painting uses incoming slots.
        var originalTiles = incoming is null ? null : ContentLayoutFlow.ProjectTiles(snapshot, viewport, _contentBrowseMode)
            .Select(TileKey).ToHashSet(StringComparer.Ordinal);
        if (incoming is not null) { snapshot = incoming.Snapshot; doc = incoming.Document; }
        // Loop projection: a card straddling the seam is drawn on both sides, so the cache key carries
        // the loop index while the saved state (ItemId/SourceIndex) stays single-owned. At most one
        // cell of overscan is kept; no permanent XAML object exists per offscreen item.
        var byId = snapshot.Items.ToDictionary(p => p.ItemId, StringComparer.Ordinal);
        var retained = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tile in ContentLayoutFlow.ProjectTiles(snapshot, viewport, _contentBrowseMode))
        {
            if (tile.Bounds.Bottom < -pitch || tile.Bounds.Y > viewport.HeightDip + pitch) continue;
            if (incoming?.Placeholders.Contains(tile.ItemId) == true)
            {
                // Target footprint, not a second drag image or a real/activatable card.
                var footprint = new Border { Width = tile.Bounds.Width, Height = tile.Bounds.Height,
                    BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(5),
                    BorderBrush = ColorBrush(_presentation.Parameters.Theme.Accent, 190),
                    Background = ColorBrush(_presentation.Parameters.Theme.Accent, 28), IsHitTestVisible = false };
                Canvas.SetLeft(footprint, tile.Bounds.X); Canvas.SetTop(footprint, tile.Bounds.Y);
                Canvas.SetZIndex(footprint, 2); _contentCanvas.Children.Add(footprint);
                continue;
            }
            string key = TileKey(tile);
            retained.Add(key);
            var placement = doc.Placements[tile.SourceIndex]; var slot = byId[tile.ItemId];
            bool selected = ContentCardPresentationPolicy.ShowOutline(SelectedContentIds(), tile.ItemId,
                IsRealContent && _contentSelection.ShowHighlight, IsEditing);
            var paint = new CardPaint(source[tile.ItemId], placement.ImageFit, tile.Bounds.Width, tile.Bounds.Height,
                slot.ColumnSpan, slot.RowSpan, placement.ColumnSpan, placement.RowSpan, placement.FixedPosition,
                ContentPinPresentationPolicy.Adornment(doc, placement), placement.PinnedIndex, selected,
                selected && IsEditing && _selectedContentItem == tile.ItemId && !_geometry.IsLocked && !_contentReadOnly,
                doc.ShowSpanLabels, IsRealContent, cardPresentation, _contentVisibleAssetKeys.GetValueOrDefault(tile.ItemId));
            if (!_contentCards.TryGetValue(key, out var card) || !_contentCardPaint.TryGetValue(key, out var oldPaint) || oldPaint != paint)
            {
                if (card is not null) _contentCanvas.Children.Remove(card);
                _contentCards[key] = card = CreateContentCard(source[tile.ItemId], placement, slot);
                _contentCardPaint[key] = paint;
                Canvas.SetZIndex(card, 1); _contentCanvas.Children.Add(card);
            }
            // Tile bounds are already viewport-relative and carry the loop offset.
            card.Opacity = 1;
            Canvas.SetLeft(card, tile.Bounds.X); Canvas.SetTop(card, tile.Bounds.Y);
            UpdateContentCardImage(tile.ItemId, card);
        }
        foreach (string id in _contentCards.Keys.Where(id => !retained.Contains(id)).ToArray())
        {
            // A pushed-out original stays attached until hover ends; cancel cannot flash a fallback.
            if (originalTiles?.Contains(id) == true) { _contentCards[id].Opacity = 0; continue; }
            _contentCanvas.Children.Remove(_contentCards[id]); _contentCards.Remove(id); _contentCardPaint.Remove(id);
        }
        if (!IsRealContent && _contentGesture is { IsDragging: true, CanEdit: true, Target: PosterInputTargetKind.ContentItem } gesture &&
            source.TryGetValue(gesture.Slot.ItemId, out var dragged))
        {
            // One non-interactive transient copy follows the actual grabbed tile. The snapped
            // layout below it is the candidate; this shadow never adds a member or hit region.
            var shadow = CreateContentCard(dragged, gesture.Document.Placements[gesture.Slot.SourceIndex], gesture.Slot);
            shadow.Opacity = .62;
            Canvas.SetLeft(shadow, gesture.GhostX); Canvas.SetTop(shadow, gesture.GhostY);
            Canvas.SetZIndex(shadow, 2); _contentCanvas.Children.Add(shadow);
        }
        RenderContentFeedback();
        DrawContentMarquee();
        // No scrollbar chrome on either edge. Content stays reachable through the wheel, the drag-edge
        // auto-scroll and the clamped extent; hiding a bar is never used to make content unreachable.
        if (_contentParseReadOnly || _contentProjectionFault)
            _contentCanvas.Children.Add(new TextBlock { Text=_contentProjectionFault ? "当前布局无法安全呈现 · 原配置保留" : "布局版本暂不支持 · 原配置只读保留", FontSize=12, TextWrapping=TextWrapping.Wrap, Width=viewport.WidthDip, Foreground=ColorBrush(_presentation.Parameters.Theme.Foreground,170) });
    }

    private void UpdateContentCardImage(string itemId, FrameworkElement? only = null)
    {
        if (!_contentVisibleAssetKeys.TryGetValue(itemId, out var key) || !_contentBitmaps.TryGetValue(key, out var bitmap)) return;
        IEnumerable<FrameworkElement> cards = only is null ? _contentCards.Where(p => _contentCardPaint[p.Key].View.ItemId == itemId).Select(p => p.Value) : [only];
        foreach (var cell in cards.OfType<Grid>())
        {
            if (cell.Children.OfType<Border>().FirstOrDefault()?.Child is not Grid host) continue;
            if (host.Children.OfType<Image>().FirstOrDefault() is { } existing && ReferenceEquals(existing.Source, bitmap)) continue;
            var placement = ContentDocument.Placements.FirstOrDefault(p => p.ItemId == itemId);
            if (placement is null) continue;
            var area = ContentCardPresentationPolicy.ImageArea(cell.Width, cell.Height, Math.Max(20, ContentParameters.LabelHeightDip + 7));
            host.Children.Clear();
            host.Children.Add(new Image { Source = bitmap, Stretch = placement.ImageFit == ContentImageFit.Cover ? Stretch.UniformToFill : Stretch.Uniform,
                Width = area.Width, Height = area.Height, HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false });
        }
    }

    private FrameworkElement CreateContentCard(ContentItemView item, ContentPlacement placement, ContentResolvedPlacement slot)
    {
        // Explicit modifier selection can contain just one item. Plain browsing clicks/open
        // keep their target without an outline; editing handles retain their existing scope.
        bool selected = ContentCardPresentationPolicy.ShowOutline(SelectedContentIds(), item.ItemId,
            IsRealContent && _contentSelection.ShowHighlight, IsEditing);
        var cell = new Grid { Width=slot.Bounds.Width, Height=slot.Bounds.Height, IsHitTestVisible=false };
        double labelHeight = Math.Max(20, ContentParameters.LabelHeightDip+7);
        var imageArea = ContentCardPresentationPolicy.ImageArea(slot.Bounds.Width, slot.Bounds.Height, labelHeight);
        cell.RowDefinitions.Add(new RowDefinition { Height=new GridLength(1,GridUnitType.Star) });
        cell.RowDefinitions.Add(new RowDefinition { Height=new GridLength(labelHeight) });
        byte r=(byte)(item.Color>>16), g=(byte)(item.Color>>8), b=(byte)item.Color;
        bool contain=placement.ImageFit==ContentImageFit.Contain;
        var art = new Grid { Width=contain?64:160, Height=contain?64:100, Background=contain?null:Brush(150,r,g,b) };
        var accent = new Border { Width=contain?24:32, Height=contain?52:84, Background=Brush(225,r,g,b), HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center,
            RenderTransform=new RotateTransform { Angle=25 }, RenderTransformOrigin=new global::Windows.Foundation.Point(.5,.5) };
        art.Children.Add(accent);
        art.Children.Add(new TextBlock { Text=item.Monogram, FontSize=32, FontWeight=new global::Windows.UI.Text.FontWeight { Weight=700 },
            Foreground=Brush(255,245,248,250), HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center });
        var imageBox = new Viewbox { Stretch=placement.ImageFit == ContentImageFit.Contain ? Stretch.Uniform : Stretch.UniformToFill,
            Child=art, MaxHeight=Math.Max(1,slot.Bounds.Height-labelHeight-4), IsHitTestVisible=false };
        if(contain)
        {
            double size=ContentParameters.IconSizeDip*Math.Min(slot.ColumnSpan,slot.RowSpan)/4d;
            imageBox.Width=Math.Max(1,Math.Min(size,slot.Bounds.Width-12));
            imageBox.Height=Math.Max(1,Math.Min(size,slot.Bounds.Height-labelHeight-8));
            imageBox.HorizontalAlignment=HorizontalAlignment.Center; imageBox.VerticalAlignment=VerticalAlignment.Center;
        }
        if (IsRealContent)
        {
            imageBox.Width = imageArea.Width; imageBox.Height = imageArea.Height;
            imageBox.Stretch = Stretch.Uniform;
        }
        var imageHost = new Grid { IsHitTestVisible=false };
        imageHost.Children.Add(imageBox);
        if (_contentVisibleAssetKeys.TryGetValue(item.ItemId, out var assetKey) && _contentBitmaps.TryGetValue(assetKey, out var bitmap))
        {
            imageHost.Children.Clear();
            imageHost.Children.Add(new Image { Source = bitmap, Stretch = placement.ImageFit == ContentImageFit.Cover ? Stretch.UniformToFill : Stretch.Uniform,
                Width = imageArea.Width, Height = imageArea.Height,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false });
        }
        imageHost.Clip = new RectangleGeometry { Rect=new global::Windows.Foundation.Rect(0,0,Math.Max(0,slot.Bounds.Width-2),Math.Max(0,slot.Bounds.Height-labelHeight-2)) };
        var card = new Border { Background=IsRealContent ? null : Brush(228,22,24,29), BorderThickness=new Thickness(selected ? 2 : IsRealContent ? 0 : .8),
            BorderBrush=selected ? ColorBrush(_presentation.Parameters.Theme.Accent) : IsRealContent ? null : Brush(32,255,255,255), CornerRadius=new CornerRadius(5), Child=imageHost };
        ApplyTemplateCardPaint(card, selected);
        cell.Children.Add(card);
        var name = new TextBlock { Text=item.Name + (item.IsAvailable ? "" : " · 暂不可用"), FontSize=ContentParameters.LabelFontSizeDip, MaxLines=1, TextTrimming=TextTrimming.CharacterEllipsis,
            VerticalAlignment=VerticalAlignment.Center, Foreground=ColorBrush(_presentation.Parameters.Theme.Foreground,210) };
        Grid.SetRow(name,1); cell.Children.Add(name);
        if (_inlineRenameItem == item.ItemId) name.Visibility = Visibility.Collapsed;
        if (ContentDocument.ShowSpanLabels)
        {
            var badge = new Border { Background=Brush(215,15,20,27), Padding=new Thickness(4,2,4,2), HorizontalAlignment=HorizontalAlignment.Left, VerticalAlignment=VerticalAlignment.Top,
                Child=new TextBlock { Text=$"{slot.ColumnSpan} × {slot.RowSpan}" +
                    (slot.ColumnSpan!=placement.ColumnSpan || slot.RowSpan!=placement.RowSpan ? $"〔偏好 {placement.ColumnSpan}×{placement.RowSpan}〕" : "") +
                    (placement.FixedPosition ? " · 固" : ""), FontSize=10, Foreground=Brush(255,235,240,246) }, IsHitTestVisible=false };
            cell.Children.Add(badge);
        }
        if (ContentPinPresentationPolicy.Adornment(ContentDocument, placement) is var adornment && adornment != ContentPinAdornment.None)
        {
            var pinBounds = ContentPinPresentationPolicy.Bounds(new(0, 0, cell.Width, cell.Height));
            var pin = new Border { Background=Brush(230,22,28,34), Width=pinBounds.Width, Height=pinBounds.Height,
                Margin=new Thickness(0,pinBounds.Y,cell.Width-pinBounds.Right,0), CornerRadius=new CornerRadius(3),
                HorizontalAlignment=HorizontalAlignment.Right, VerticalAlignment=VerticalAlignment.Top, IsHitTestVisible=false,
                Child=new Viewbox { Width=14, Height=14, IsHitTestVisible=false,
                    Child=new FontIcon { FontFamily=new FontFamily("Segoe Fluent Icons"), Glyph=adornment==ContentPinAdornment.LegacyLock?"\uE72E":"\uE718",
                        Foreground=ColorBrush(_presentation.Parameters.Theme.Accent), IsHitTestVisible=false } } };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(pin, adornment==ContentPinAdornment.LegacyLock
                ? "旧硬位置，需兼容转换后才能解除" : $"顺序图钉，第 {placement.PinnedIndex} 位；右键图钉解除");
            cell.Children.Add(pin);
        }
        if (selected && IsEditing && _selectedContentItem == item.ItemId && !_geometry.IsLocked && !_contentReadOnly)
        {
            var handle = new Border { Width=14, Height=14, Background=ColorBrush(_presentation.Parameters.Theme.Accent), CornerRadius=new CornerRadius(2),
                HorizontalAlignment=HorizontalAlignment.Right, VerticalAlignment=VerticalAlignment.Bottom, IsHitTestVisible=false };
            Grid.SetRowSpan(handle,2); cell.Children.Add(handle);
        }
        return cell;
    }
    private static string TileKey(ContentLayoutTile tile) => tile.LoopIndex == 0
        ? tile.ItemId
        : tile.ItemId + "#" + tile.LoopIndex.ToString(global::System.Globalization.CultureInfo.InvariantCulture);

    private void RebuildItemGrid(double widthDip)
    {
        var frame = _bounds.IsUsable ? _bounds : new DesktopPixelRect(0,0,(int)Math.Ceiling(widthDip*(_monitor?.ScaleX??1)),(int)Math.Ceiling(_geometry.ExpandedHeightDip*(_monitor?.ScaleY??1)));
        frame = frame with { Width=(int)Math.Ceiling(widthDip*(_monitor?.ScaleX??1)) };
        var snapshot = EnsureContentSnapshot(frame);
        DrawContentSnapshot(snapshot, _contentViewport!);
    }
    private void UpdateItemScrollState(DesktopPixelRect frame)
    {
        var snapshot = EnsureContentSnapshot(frame);
        DrawContentSnapshot(snapshot,_contentViewport!);
        UpdateScene();
    }
    private void ScrollItems(int wheelDelta, bool crossAxis = false)
    {
        if (_contentPresentationSuspended || _monitor is null || _inputIsland is null || _geometry.IsCollapsed || wheelDelta==0 || HasContentGesture || _inlineRenameItem is not null || _transitionLayout is not null) return;
        // Shift exposes the other axis only after qualified physical shortage; normal browsing
        // stays single-axis. A held content gesture never competes with wheel navigation.
        var flowSnapshot = EnsureContentSnapshot(_bounds);
        // One full notch remains one grid pitch. Signed and fractional input reaches the shared
        // finite/loop policy before clamping, so the head can wrap backward and small deltas add up.
        var offsets = ContentWheelScrollPolicy.Advance(flowSnapshot, _contentViewport!, wheelDelta, _contentBrowseMode, crossAxis);
        _contentScrollX = offsets.ScrollXDip;
        _itemScrollOffsetDip = offsets.ScrollYDip;
        UpdateItemScrollState(_bounds);
        if (_inputIsland.ApplyLayout(_bounds,BuildInputLayout(_bounds))) PublishContentSpace(_contentSpaceCandidate);
    }
    private void AddContentInputRegions(DesktopPixelRect frame,List<PosterInputRegion> regions)
    {
        if (_monitor is null || _geometry.IsCollapsed) return;
        var snapshot=EnsureContentSnapshot(frame); var viewport=_contentViewport!;
        DesktopPixelRect Pixels(ContentRect r)
        {
            int x=(int)Math.Round((ContentLeftDip+r.X)*_monitor.ScaleX), y=(int)Math.Round((ContentTopDip+r.Y)*_monitor.ScaleY);
            int right=(int)Math.Round((ContentLeftDip+r.Right)*_monitor.ScaleX), bottom=(int)Math.Round((ContentTopDip+r.Bottom)*_monitor.ScaleY);
            return new(x,y,Math.Max(1,right-x),Math.Max(1,bottom-y));
        }
        // Hit regions follow the same tiles that were drawn, so a card crossing the seam is clickable
        // on both sides while still resolving to one ItemId.
        foreach (var tile in ContentLayoutFlow.ProjectTiles(snapshot, viewport, _contentBrowseMode))
        {
            if (tile.Clip.IsEmpty) continue;
            if (IsEditing && !_geometry.IsLocked && !_contentReadOnly && _selectedContentItem==tile.ItemId)
            {
                var handle=new ContentRect(tile.Bounds.Right-16,tile.Bounds.Bottom-16,16,16).Intersect(new(0,0,viewport.WidthDip,viewport.HeightDip));
                if(!handle.IsEmpty) regions.Add(new(PosterInputTargetKind.ContentSpanHandle,tile.SourceIndex,Pixels(handle))
                { ContentItemId=tile.ItemId,ContentSnapshotGeneration=snapshot.ViewportGeneration });
            }
            regions.Add(new(PosterInputTargetKind.ContentItem,tile.SourceIndex,Pixels(tile.Clip))
            { ContentItemId=tile.ItemId,ContentSnapshotGeneration=snapshot.ViewportGeneration });
        }
        if (viewport.WidthDip>0 && viewport.HeightDip>0) regions.Add(new(PosterInputTargetKind.ContentViewport,-1,Pixels(new(0,0,viewport.WidthDip,viewport.HeightDip)))
        {ContentSnapshotGeneration=snapshot.ViewportGeneration});
    }
    private void ClearContentVisuals()
    {
        ReleaseScene();
        CancelContentGesture("surface-release");
        ReleaseContentAssets();
        _contentEndAnchor=null;
        _contentCanvas=null; _contentCards.Clear(); _contentCardPaint.Clear(); _contentSnapshot=null; _contentViewport=null; _solvedDocument=null;
    }
}
