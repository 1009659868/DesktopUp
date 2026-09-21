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
    private ContentLayoutDocument? _contentDocument, _contentDraft;
    private string? _contentRaw, _contentOwner, _selectedContentItem;
    private bool _contentReadOnly;
    private bool _contentParseReadOnly, _contentProjectionFault;
    private ContentLayoutSnapshot? _contentSnapshot;
    private ContentLayoutViewport? _contentViewport;
    private ContentLayoutDocument? _solvedDocument;
    private long _contentGeneration;
    private double _contentScrollX;
    private readonly Dictionary<string, FrameworkElement> _contentCards = new(StringComparer.Ordinal);
    private string? _contentPaintKey;
    private readonly DemoContentItemViewSource _contentSource = new();

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
                _contentDraft = null; _solvedDocument = null;
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
    private ContentLayoutSnapshot EnsureContentSnapshot(DesktopPixelRect frame)
    {
        _ = ContentDocument;
        var doc = EffectiveContentDocument;
        var viewport = ContentViewportFor(frame);
        if (ReferenceEquals(_solvedDocument, doc) && _contentViewport == viewport && _contentSnapshot is not null) return _contentSnapshot;
        bool geometryChanged = _contentProjectionFault || !ReferenceEquals(_solvedDocument, doc) || _contentViewport?.WidthDip != viewport.WidthDip ||
            _contentViewport?.Scale != viewport.Scale || _contentViewport?.Spacing != viewport.Spacing;
        var anchor = geometryChanged && !HasContentGesture ? _contentSnapshot?.Anchor() : null;
        viewport = viewport with { Generation = checked(++_contentGeneration) };
        ContentLayoutSnapshot next;
        try
        {
            next = geometryChanged || _contentSnapshot is null
                ? ContentLayoutSolver.Resolve(doc, viewport, anchor)
                : ContentLayoutSolver.Reclip(_contentSnapshot, viewport);
            if(_contentProjectionFault) _contentNotice=null;
            _contentProjectionFault=false; _contentReadOnly=_contentParseReadOnly;
        }
        catch (ArgumentException error)
        {
            _contentProjectionFault=true; _contentReadOnly=true;
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
            _itemsGrid.Children.Add(_contentCanvas); _contentCards.Clear();
        }
        _itemsGrid.Width = viewport.WidthDip;
        _itemsGrid.Height = viewport.HeightDip;
        _contentCanvas.Width = viewport.WidthDip; _contentCanvas.Height = viewport.HeightDip;
        _contentCanvas.Clip = new RectangleGeometry { Rect = new global::Windows.Foundation.Rect(0, 0, viewport.WidthDip, viewport.HeightDip) };
        _itemColumnCount = snapshot.Columns;
        var doc = ContentDocument;
        if (_footer?.Children.OfType<TextBlock>().FirstOrDefault() is { } count) count.Text=$"{doc.Placements.Count} DEMO ITEMS";
        if (_metaPanel?.Children.OfType<TextBlock>().LastOrDefault() is { } metaCount) metaCount.Text=$"{doc.Placements.Count:00} DEMO ITEMS";
        string paintKey = $"{doc.DocumentVersion}/{_mode}/{_selectedContentItem}/{_presentation}/{doc.ShowBaselineGrid}/{doc.ShowSpanLabels}";
        if (_contentPaintKey != paintKey) { _contentCards.Clear(); _contentPaintKey = paintKey; }
        _contentCanvas.Children.Clear();
        double pitch = snapshot.CellDip + snapshot.GapDip;
        if (IsEditing && doc.ShowBaselineGrid)
        {
            double x0 = -(snapshot.ScrollXDip % pitch), y0 = -(snapshot.ScrollYDip % pitch);
            for (double x = x0; x <= viewport.WidthDip; x += pitch) _contentCanvas.Children.Add(new Line
            { X1=x, X2=x, Y1=0, Y2=viewport.HeightDip, Stroke=Brush(30,180,200,218), StrokeThickness=.5, IsHitTestVisible=false });
            for (double y = y0; y <= viewport.HeightDip; y += pitch) _contentCanvas.Children.Add(new Line
            { X1=0, X2=viewport.WidthDip, Y1=y, Y2=y, Stroke=Brush(30,180,200,218), StrokeThickness=.5, IsHitTestVisible=false });
        }
        var views = _contentSource.Read(doc);
        SyncContentAssets(EffectiveContentDocument,snapshot,viewport,views);
        var source = views.ToDictionary(p => p.ItemId, StringComparer.Ordinal);
        // At most one cell of overscan; no permanent XAML object per offscreen item.
        var visible = new ContentRect(snapshot.ScrollXDip, Math.Max(0,snapshot.ScrollYDip-pitch), viewport.WidthDip, viewport.HeightDip+pitch*2);
        var retained = new HashSet<string>(StringComparer.Ordinal);
        foreach (var slot in snapshot.Items.Where(p => p.Bounds.Intersects(visible)))
        {
            retained.Add(slot.ItemId);
            if (!_contentCards.TryGetValue(slot.ItemId, out var card) || card.Width != slot.Bounds.Width || card.Height != slot.Bounds.Height)
                _contentCards[slot.ItemId] = card = CreateContentCard(source[slot.ItemId], doc.Placements[slot.SourceIndex], slot);
            Canvas.SetLeft(card, slot.Bounds.X-snapshot.ScrollXDip); Canvas.SetTop(card, slot.Bounds.Y-snapshot.ScrollYDip);
            _contentCanvas.Children.Add(card);
        }
        foreach (string id in _contentCards.Keys.Where(id => !retained.Contains(id)).ToArray()) _contentCards.Remove(id);
        if (snapshot.ExtentHeightDip > viewport.HeightDip && viewport.HeightDip > 0)
        {
            var thumb = new Rectangle { Width=3, Height=Math.Max(12, viewport.HeightDip*viewport.HeightDip/snapshot.ExtentHeightDip), Fill=Brush(110,210,220,225), IsHitTestVisible=false };
            Canvas.SetLeft(thumb, Math.Max(0,viewport.WidthDip-3));
            Canvas.SetTop(thumb, (viewport.HeightDip-thumb.Height)*snapshot.ScrollYDip/Math.Max(1,snapshot.ExtentHeightDip-viewport.HeightDip));
            _contentCanvas.Children.Add(thumb);
        }
        if (snapshot.ExtentWidthDip > viewport.WidthDip && viewport.WidthDip > 0)
        {
            var thumb = new Rectangle { Height=3, Width=Math.Max(12, viewport.WidthDip*viewport.WidthDip/snapshot.ExtentWidthDip), Fill=Brush(110,210,220,225), IsHitTestVisible=false };
            Canvas.SetTop(thumb, Math.Max(0,viewport.HeightDip-3));
            Canvas.SetLeft(thumb, (viewport.WidthDip-thumb.Width)*snapshot.ScrollXDip/Math.Max(1,snapshot.ExtentWidthDip-viewport.WidthDip));
            _contentCanvas.Children.Add(thumb);
        }
        if (_contentReadOnly || doc.Placements.Count == 0)
            _contentCanvas.Children.Add(new TextBlock { Text=_contentProjectionFault ? "当前布局无法安全呈现 · 原配置保留" : _contentReadOnly ? "布局版本暂不支持 · 原配置只读保留" : "暂无内容 · DEMO 布局", FontSize=12, TextWrapping=TextWrapping.Wrap, Width=viewport.WidthDip, Foreground=ColorBrush(_presentation.Parameters.Theme.Foreground,170) });
    }

    private FrameworkElement CreateContentCard(ContentItemView item, ContentPlacement placement, ContentResolvedPlacement slot)
    {
        bool selected = IsEditing && _selectedContentItem == item.ItemId;
        var cell = new Grid { Width=slot.Bounds.Width, Height=slot.Bounds.Height, IsHitTestVisible=false };
        double labelHeight = Math.Max(20, ContentParameters.LabelHeightDip+7);
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
        var imageHost = new Grid { IsHitTestVisible=false };
        imageHost.Children.Add(imageBox);
        imageHost.Clip = new RectangleGeometry { Rect=new global::Windows.Foundation.Rect(0,0,Math.Max(0,slot.Bounds.Width-2),Math.Max(0,slot.Bounds.Height-labelHeight-2)) };
        var card = new Border { Background=Brush(228,22,24,29), BorderThickness=new Thickness(selected ? 2 : .8),
            BorderBrush=selected ? ColorBrush(_presentation.Parameters.Theme.Accent) : Brush(32,255,255,255), CornerRadius=new CornerRadius(5), Child=imageHost };
        cell.Children.Add(card);
        var name = new TextBlock { Text=item.Name, FontSize=ContentParameters.LabelFontSizeDip, MaxLines=1, TextTrimming=TextTrimming.CharacterEllipsis,
            VerticalAlignment=VerticalAlignment.Center, Foreground=ColorBrush(_presentation.Parameters.Theme.Foreground,210) };
        Grid.SetRow(name,1); cell.Children.Add(name);
        if (IsEditing && ContentDocument.ShowSpanLabels)
        {
            var badge = new Border { Background=Brush(215,15,20,27), Padding=new Thickness(4,2,4,2), HorizontalAlignment=HorizontalAlignment.Left, VerticalAlignment=VerticalAlignment.Top,
                Child=new TextBlock { Text=$"{slot.ColumnSpan} × {slot.RowSpan}" +
                    (slot.ColumnSpan!=placement.ColumnSpan || slot.RowSpan!=placement.RowSpan ? $"〔偏好 {placement.ColumnSpan}×{placement.RowSpan}〕" : "") +
                    (placement.FixedPosition ? " · 固" : ""), FontSize=10, Foreground=Brush(255,235,240,246) }, IsHitTestVisible=false };
            cell.Children.Add(badge);
        }
        if (selected)
        {
            var handle = new Border { Width=14, Height=14, Background=ColorBrush(_presentation.Parameters.Theme.Accent), CornerRadius=new CornerRadius(2),
                HorizontalAlignment=HorizontalAlignment.Right, VerticalAlignment=VerticalAlignment.Bottom, IsHitTestVisible=false };
            Grid.SetRowSpan(handle,2); cell.Children.Add(handle);
        }
        return cell;
    }
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
    }
    private void ScrollItems(int wheelDelta)
    {
        if (_contentPresentationSuspended || _monitor is null || _inputIsland is null || _geometry.IsCollapsed || wheelDelta==0 || HasContentGesture || _transitionLayout is not null) return;
        double delta=-wheelDelta/120d*72;
        bool horizontal=global::DesktopUp.Windows.NativeInputKeys.ShiftDown;
        if (horizontal) _contentScrollX=Math.Max(0,_contentScrollX+delta); else _itemScrollOffsetDip=Math.Max(0,_itemScrollOffsetDip+delta);
        UpdateItemScrollState(_bounds);
        _inputIsland.ApplyLayout(_bounds,BuildInputLayout(_bounds));
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
        foreach (var slot in snapshot.Items.Where(p=>!p.Clip.IsEmpty))
        {
            if (IsEditing && !_geometry.IsLocked && !_contentReadOnly && _selectedContentItem==slot.ItemId)
            {
                var handle=new ContentRect(slot.Bounds.Right-snapshot.ScrollXDip-16,slot.Bounds.Bottom-snapshot.ScrollYDip-16,16,16).Intersect(new(0,0,viewport.WidthDip,viewport.HeightDip));
                if(!handle.IsEmpty) regions.Add(new(PosterInputTargetKind.ContentSpanHandle,slot.SourceIndex,Pixels(handle))
                { ContentItemId=slot.ItemId,ContentSnapshotGeneration=snapshot.ViewportGeneration });
            }
            regions.Add(new(PosterInputTargetKind.ContentItem,slot.SourceIndex,Pixels(slot.Clip))
            { ContentItemId=slot.ItemId,ContentSnapshotGeneration=snapshot.ViewportGeneration });
        }
        if (viewport.WidthDip>0 && viewport.HeightDip>0) regions.Add(new(PosterInputTargetKind.ContentViewport,-1,Pixels(new(0,0,viewport.WidthDip,viewport.HeightDip)))
        {ContentSnapshotGeneration=snapshot.ViewportGeneration});
    }
    private void ClearContentVisuals()
    {
        CancelContentGesture("surface-release");
        ReleaseContentAssets();
        _contentCanvas=null; _contentCards.Clear(); _contentSnapshot=null; _contentViewport=null; _solvedDocument=null;
    }
}
