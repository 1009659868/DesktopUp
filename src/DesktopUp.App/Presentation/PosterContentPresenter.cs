using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;

namespace DesktopUp;

internal sealed record ContentCardGhost(ContentItemView View, ContentPlacement Placement,
    ContentResolvedPlacement Slot, double X, double Y);
internal sealed record ContentRenderFrame(ContentLayoutDocument Document, ContentLayoutSnapshot Snapshot,
    ContentLayoutViewport Viewport, ContentBrowseMode BrowseMode, IReadOnlyList<ContentItemView> Views,
    IncomingLayoutPreview? Incoming, IReadOnlyList<string> Selection, bool Highlight, bool Editing,
    string? SelectedItem, bool Locked, bool ReadOnly, bool Real, ResolvedPosterPresentation Presentation,
    string? NamingItem, bool ParseReadOnly, bool ProjectionFault, ContentCardGhost? Ghost);

/// <summary>WinUI-only incremental projection. No store, file actions, layout solver or input routing.
/// A scene callback can paint card material only; the host translates native input into typed intents.</summary>
internal sealed class PosterContentPresenter(Action<Border, bool, Grid?> paintSceneCard)
{
    private ContentRenderFrame? _frame;
    private string? _hoverTile;
    public ContentRenderFrame? Frame => _frame;
    public Canvas? Surface { get; private set; }
    public Dictionary<string, FrameworkElement> Cards { get; } = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CardPaint> Paints = new(StringComparer.Ordinal);
    public Dictionary<ContentAssetKey, WriteableBitmap> Bitmaps { get; } = new();
    public IReadOnlyDictionary<string, ContentAssetKey> VisibleKeys { get; set; } =
        new Dictionary<string, ContentAssetKey>();
    private sealed record CardPaint(ContentItemView View, ContentImageFit Fit, double Width, double Height,
        double Columns, double Rows, double WantedColumns, double WantedRows, bool Fixed, ContentPinAdornment Pin, int? PinIndex,
        bool Selected, bool Handle, bool Spans, bool Real, ResolvedPosterPresentation Presentation, ContentAssetKey? Asset);
    public void Present(Grid itemsGrid, ContentRenderFrame frame)
    {
        _frame = frame;
        var snapshot = frame.Snapshot; var viewport = frame.Viewport;
        if (Surface is null || !itemsGrid.Children.Contains(Surface))
        {
            itemsGrid.Children.Clear(); itemsGrid.ColumnDefinitions.Clear(); itemsGrid.RowDefinitions.Clear();
            itemsGrid.ColumnSpacing = 0; itemsGrid.RowSpacing = 0;
            Surface = new Canvas { IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            itemsGrid.Children.Add(Surface); Cards.Clear(); Paints.Clear();
        }
        itemsGrid.Width = viewport.WidthDip;
        itemsGrid.Height = viewport.HeightDip;
        Surface.Width = viewport.WidthDip; Surface.Height = viewport.HeightDip;
        Surface.Clip = new RectangleGeometry { Rect = new global::Windows.Foundation.Rect(0, 0, viewport.WidthDip, viewport.HeightDip) };
        var doc = frame.Document;
        // Lamp paint and host title ink never invalidate card identity or solver geometry.
        var cardPresentation = frame.Presentation with { Parameters = frame.Presentation.Parameters with { Lamp = null, TitleColors = null, ShowTitle = null, Canvas = null,
            Theme = frame.Presentation.Parameters.Theme with { Foreground = frame.Presentation.EffectiveContentForeground } } };
        // Retain cards in their visual parent. Only ephemeral grid/gesture overlays are rebuilt.
        var existingCards = Cards.Values.ToHashSet();
        foreach (var child in Surface.Children.Where(child => child is not FrameworkElement element || !existingCards.Contains(element)).ToArray())
            Surface.Children.Remove(child);
        double pitch = snapshot.CellDip + snapshot.GapDip;
        if (doc.ShowBaselineGrid)
        {
            double x0 = -(snapshot.ScrollXDip % pitch), y0 = -(snapshot.ScrollYDip % pitch);
            for (double x = x0; x <= viewport.WidthDip; x += pitch) Surface.Children.Add(new Line
            { X1=x, X2=x, Y1=0, Y2=viewport.HeightDip, Stroke=Brush(30,180,200,218), StrokeThickness=.5, IsHitTestVisible=false });
            for (double y = y0; y <= viewport.HeightDip; y += pitch) Surface.Children.Add(new Line
            { X1=0, X2=viewport.WidthDip, Y1=y, Y2=y, Stroke=Brush(30,180,200,218), StrokeThickness=.5, IsHitTestVisible=false });
        }
        var views = frame.Views;
        var incoming = frame.Incoming;
        var source = views.ToDictionary(p => p.ItemId, StringComparer.Ordinal);
        // Keep the author/solver/native input basis untouched. Only painting uses incoming slots.
        var originalTiles = incoming is null ? null : ContentLayoutFlow.ProjectTiles(snapshot, viewport, frame.BrowseMode)
            .Select(TileKey).ToHashSet(StringComparer.Ordinal);
        if (incoming is not null) { snapshot = incoming.Snapshot; doc = incoming.Document; }
        // Loop projection: a card straddling the seam is drawn on both sides, so the cache key carries
        // the loop index while the saved state (ItemId/SourceIndex) stays single-owned. At most one
        // cell of overscan is kept; no permanent XAML object exists per offscreen item.
        var byId = snapshot.Items.ToDictionary(p => p.ItemId, StringComparer.Ordinal);
        var retained = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tile in ContentLayoutFlow.ProjectTiles(snapshot, viewport, frame.BrowseMode))
        {
            if (tile.Bounds.Bottom < -pitch || tile.Bounds.Y > viewport.HeightDip + pitch) continue;
            if (incoming?.Placeholders.Contains(tile.ItemId) == true)
            {
                // Target footprint, not a second drag image or a real/activatable card.
                var footprint = new Border { Width = tile.Bounds.Width, Height = tile.Bounds.Height,
                    BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(5),
                    BorderBrush = ColorBrush(frame.Presentation.Parameters.Theme.Accent, 190),
                    Background = ColorBrush(frame.Presentation.Parameters.Theme.Accent, 28), IsHitTestVisible = false };
                Canvas.SetLeft(footprint, tile.Bounds.X); Canvas.SetTop(footprint, tile.Bounds.Y);
                Canvas.SetZIndex(footprint, 2); Surface.Children.Add(footprint);
                continue;
            }
            string key = TileKey(tile);
            retained.Add(key);
            var placement = doc.Placements[tile.SourceIndex]; var slot = byId[tile.ItemId];
            bool selected = ContentCardPresentationPolicy.ShowOutline(frame.Selection, tile.ItemId,
                frame.Real && frame.Highlight, frame.Editing);
            var paint = new CardPaint(source[tile.ItemId], placement.ImageFit, tile.Bounds.Width, tile.Bounds.Height,
                slot.ColumnSpan, slot.RowSpan, placement.ColumnSpan, placement.RowSpan, placement.FixedPosition,
                ContentPinPresentationPolicy.Adornment(doc, placement), placement.PinnedIndex, selected,
                selected && frame.Editing && frame.SelectedItem == tile.ItemId && !frame.Locked && !frame.ReadOnly,
                doc.ShowSpanLabels, frame.Real, cardPresentation, VisibleKeys.GetValueOrDefault(tile.ItemId));
            if (!Cards.TryGetValue(key, out var card) || !Paints.TryGetValue(key, out var oldPaint) || oldPaint != paint)
            {
                if (card is not null) Surface.Children.Remove(card);
                Cards[key] = card = CreateContentCard(source[tile.ItemId], placement, slot);
                Paints[key] = paint;
                Canvas.SetZIndex(card, 1); Surface.Children.Add(card);
            }
            // Tile bounds are already viewport-relative and carry the loop offset.
            card.Opacity = 1;
            if (card is Grid grid) grid.Background = key == _hoverTile ? HoverBrush() : null;
            Canvas.SetLeft(card, tile.Bounds.X); Canvas.SetTop(card, tile.Bounds.Y);
            UpdateImage(tile.ItemId, card);
        }
        foreach (string id in Cards.Keys.Where(id => !retained.Contains(id)).ToArray())
        {
            // A pushed-out original stays attached until hover ends; cancel cannot flash a fallback.
            if (originalTiles?.Contains(id) == true) { Cards[id].Opacity = 0; continue; }
            Surface.Children.Remove(Cards[id]); Cards.Remove(id); Paints.Remove(id);
        }
        if (frame.Ghost is { } ghost)
        {
            // One non-interactive transient copy follows the actual grabbed tile. The snapped
            // layout below it is the candidate; this shadow never adds a member or hit region.
            var shadow = CreateContentCard(ghost.View, ghost.Placement, ghost.Slot);
            shadow.Opacity = .62;
            Canvas.SetLeft(shadow, ghost.X); Canvas.SetTop(shadow, ghost.Y);
            Canvas.SetZIndex(shadow, 2); Surface.Children.Add(shadow);
        }
        // No scrollbar chrome on either edge. Content stays reachable through the wheel, the drag-edge
        // auto-scroll and the clamped extent; hiding a bar is never used to make content unreachable.
        if (frame.ParseReadOnly || frame.ProjectionFault)
            Surface.Children.Add(new TextBlock { Text=frame.ProjectionFault ? "当前布局无法安全呈现 · 原配置保留" : "布局版本暂不支持 · 原配置只读保留", FontSize=12, TextWrapping=TextWrapping.Wrap, Width=viewport.WidthDip, Foreground=ColorBrush(frame.Presentation.Parameters.Theme.Foreground,170) });
    }

    public void UpdateImage(string itemId, FrameworkElement? only = null)
    {
        var frame = _frame;
        if (frame is null) return;
        if (!VisibleKeys.TryGetValue(itemId, out var key) || !Bitmaps.TryGetValue(key, out var bitmap)) return;
        IEnumerable<FrameworkElement> cards = only is null ? Cards.Where(p => Paints[p.Key].View.ItemId == itemId).Select(p => p.Value) : [only];
        foreach (var cell in cards.OfType<Grid>())
        {
            if (cell.Children.OfType<Border>().FirstOrDefault()?.Child is not Grid host) continue;
            if (host.Children.OfType<Image>().FirstOrDefault() is { } existing && ReferenceEquals(existing.Source, bitmap)) continue;
            var placement = frame.Document.Placements.FirstOrDefault(p => p.ItemId == itemId);
            if (placement is null) continue;
            var area = ContentCardPresentationPolicy.ImageArea(cell.Width, cell.Height, Math.Max(20, frame.Presentation.EffectiveContent.LabelHeightDip + 7));
            host.Children.Clear();
            host.Children.Add(new Image { Source = bitmap, Stretch = placement.ImageFit == ContentImageFit.Cover ? Stretch.UniformToFill : Stretch.Uniform,
                Width = area.Width, Height = area.Height, HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false });
        }
    }

    private FrameworkElement CreateContentCard(ContentItemView item, ContentPlacement placement, ContentResolvedPlacement slot)
    {
        var frame = _frame ?? throw new InvalidOperationException("No presentation frame.");
        // Explicit modifier selection can contain just one item. Plain browsing clicks/open
        // keep their target without an outline; editing handles retain their existing scope.
        bool selected = ContentCardPresentationPolicy.ShowOutline(frame.Selection, item.ItemId,
            frame.Real && frame.Highlight, frame.Editing);
        var cell = new Grid { Width=slot.Bounds.Width, Height=slot.Bounds.Height, IsHitTestVisible=false };
        double labelHeight = Math.Max(20, frame.Presentation.EffectiveContent.LabelHeightDip+7);
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
            double size=frame.Presentation.EffectiveContent.IconSizeDip*Math.Min(slot.ColumnSpan,slot.RowSpan)/4d;
            imageBox.Width=Math.Max(1,Math.Min(size,slot.Bounds.Width-12));
            imageBox.Height=Math.Max(1,Math.Min(size,slot.Bounds.Height-labelHeight-8));
            imageBox.HorizontalAlignment=HorizontalAlignment.Center; imageBox.VerticalAlignment=VerticalAlignment.Center;
        }
        if (frame.Real)
        {
            imageBox.Width = imageArea.Width; imageBox.Height = imageArea.Height;
            imageBox.Stretch = Stretch.Uniform;
        }
        var imageHost = new Grid { IsHitTestVisible=false };
        imageHost.Children.Add(imageBox);
        if (VisibleKeys.TryGetValue(item.ItemId, out var assetKey) && Bitmaps.TryGetValue(assetKey, out var bitmap))
        {
            imageHost.Children.Clear();
            imageHost.Children.Add(new Image { Source = bitmap, Stretch = placement.ImageFit == ContentImageFit.Cover ? Stretch.UniformToFill : Stretch.Uniform,
                Width = imageArea.Width, Height = imageArea.Height,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false });
        }
        imageHost.Clip = new RectangleGeometry { Rect=new global::Windows.Foundation.Rect(0,0,Math.Max(0,slot.Bounds.Width-2),Math.Max(0,slot.Bounds.Height-labelHeight-2)) };
        var card = new Border { Background=frame.Real ? null : Brush(228,22,24,29), BorderThickness=new Thickness(selected ? 2 : frame.Real ? 0 : .8),
            BorderBrush=selected ? ColorBrush(frame.Presentation.Parameters.Theme.Accent) : frame.Real ? null : Brush(32,255,255,255), CornerRadius=new CornerRadius(5), Child=imageHost };
        paintSceneCard(card, selected, null);
        cell.Children.Add(card);
        var name = new TextBlock { Text=item.Name + (item.IsAvailable ? "" : " · 暂不可用"), FontSize=frame.Presentation.EffectiveContent.LabelFontSizeDip, MaxLines=1, TextTrimming=TextTrimming.CharacterEllipsis,
            VerticalAlignment=VerticalAlignment.Center, Foreground=ColorBrush(frame.Presentation.EffectiveContentForeground,210) };
        Grid.SetRow(name,1); cell.Children.Add(name);
        if (frame.NamingItem == item.ItemId) name.Visibility = Visibility.Collapsed;
        if (frame.Document.ShowSpanLabels)
        {
            var badge = new Border { Background=Brush(215,15,20,27), Padding=new Thickness(4,2,4,2), HorizontalAlignment=HorizontalAlignment.Left, VerticalAlignment=VerticalAlignment.Top,
                Child=new TextBlock { Text=$"{slot.ColumnSpan} × {slot.RowSpan}" +
                    (slot.ColumnSpan!=placement.ColumnSpan || slot.RowSpan!=placement.RowSpan ? $"〔偏好 {placement.ColumnSpan}×{placement.RowSpan}〕" : "") +
                    (placement.FixedPosition ? " · 固" : ""), FontSize=10, Foreground=Brush(255,235,240,246) }, IsHitTestVisible=false };
            cell.Children.Add(badge);
        }
        if (ContentPinPresentationPolicy.Adornment(frame.Document, placement) is var adornment && adornment != ContentPinAdornment.None)
        {
            var pinBounds = ContentPinPresentationPolicy.Bounds(new(0, 0, cell.Width, cell.Height));
            var pin = new Border { Background=Brush(230,22,28,34), Width=pinBounds.Width, Height=pinBounds.Height,
                Margin=new Thickness(0,pinBounds.Y,cell.Width-pinBounds.Right,0), CornerRadius=new CornerRadius(3),
                HorizontalAlignment=HorizontalAlignment.Right, VerticalAlignment=VerticalAlignment.Top, IsHitTestVisible=false,
                Child=new Viewbox { Width=14, Height=14, IsHitTestVisible=false,
                    Child=new FontIcon { FontFamily=new FontFamily("Segoe Fluent Icons"), Glyph=adornment==ContentPinAdornment.LegacyLock?"\uE72E":"\uE718",
                        Foreground=ColorBrush(frame.Presentation.Parameters.Theme.Accent), IsHitTestVisible=false } } };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(pin, adornment==ContentPinAdornment.LegacyLock
                ? "旧硬位置，需兼容转换后才能解除" : $"顺序图钉，第 {placement.PinnedIndex} 位；右键图钉解除");
            cell.Children.Add(pin);
        }
        if (selected && frame.Editing && frame.SelectedItem == item.ItemId && !frame.Locked && !frame.ReadOnly)
        {
            var handle = new Border { Width=14, Height=14, Background=ColorBrush(frame.Presentation.Parameters.Theme.Accent), CornerRadius=new CornerRadius(2),
                HorizontalAlignment=HorizontalAlignment.Right, VerticalAlignment=VerticalAlignment.Bottom, IsHitTestVisible=false };
            Grid.SetRowSpan(handle,2); cell.Children.Add(handle);
        }
        return cell;
    }
    public string ItemForTile(string key) => Paints[key].View.ItemId;
    public void ClearVisuals() { Surface = null; Cards.Clear(); Paints.Clear(); _frame = null; _hoverTile = null; }
    public void SetHover(string? tile)
    {
        if (tile == _hoverTile) return;
        if (_hoverTile is not null && Cards.GetValueOrDefault(_hoverTile) is Grid old) old.Background = null;
        _hoverTile = tile;
        if (tile is not null && Cards.GetValueOrDefault(tile) is Grid next) next.Background = HoverBrush();
    }
    public void HoverAt(double x, double y)
    {
        // The host already admitted the current native hit. Inspect only retained visible tiles,
        // without solving/projecting every Item, allocating geometry, or refreshing its image.
        foreach (var (key, card) in Cards)
            if (card.Opacity > 0 && x >= Canvas.GetLeft(card) && x < Canvas.GetLeft(card) + card.Width &&
                y >= Canvas.GetTop(card) && y < Canvas.GetTop(card) + card.Height)
            { SetHover(key); return; }
        SetHover(null);
    }
    private SolidColorBrush HoverBrush() => ColorBrush(_frame!.Presentation.EffectiveContentForeground, 36);
    public void SetNameVisible(string itemId, bool visible)
    {
        foreach (var entry in Cards)
            if (Paints.TryGetValue(entry.Key, out var paint) && paint.View.ItemId == itemId && entry.Value is Grid cell)
                foreach (var text in cell.Children.OfType<TextBlock>().Where(t => Grid.GetRow(t) == 1))
                    text.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }
    public void PaintScene()
    {
        foreach (var cell in Cards.Values.OfType<Grid>())
            if (cell.Children.OfType<Border>().FirstOrDefault() is { } card)
                paintSceneCard(card, card.BorderThickness.Left > 1, cell);
    }
    private static string TileKey(ContentLayoutTile tile) => ContentLayoutTileIdentity.Key(tile);
    private static SolidColorBrush Brush(byte a, byte r, byte g, byte b) =>
        new(global::Windows.UI.Color.FromArgb(a, r, g, b));
    private static SolidColorBrush ColorBrush(string hex, byte alpha = 255) =>
        Brush(alpha, Convert.ToByte(hex.Substring(1, 2), 16), Convert.ToByte(hex.Substring(3, 2), 16),
            Convert.ToByte(hex.Substring(5, 2), 16));
}
