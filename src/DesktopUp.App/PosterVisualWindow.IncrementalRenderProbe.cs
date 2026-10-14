using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace DesktopUp;

internal sealed record ContentRenderIdentity(object Source, object Cache, long Requests,
    IReadOnlyDictionary<string, FrameworkElement> Cards, IReadOnlyDictionary<string, WriteableBitmap> Images);

internal sealed partial class PosterVisualWindow
{
    internal async Task<ContentRenderIdentity> CaptureRenderIdentityForProbe()
    {
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while (_contentAssets is null || _contentAssets.RunningLoads != 0 || _contentAssetRequests.Count != 0 ||
            _contentPorts.Views.Version != _contentSourceVersion || ContentDocument.Placements.Any(p =>
                !_contentVisibleAssetKeys.TryGetValue(p.ItemId, out var key) || !_contentBitmaps.ContainsKey(key)))
        {
            if (timeout.Elapsed.TotalSeconds > 20) throw new IOException("Incremental raster fixture did not finish loading: " +
                System.Text.Json.JsonSerializer.Serialize(new { cache = _contentAssets is not null, running = _contentAssets?.RunningLoads,
                    requests = _contentAssetRequests.Count, totalRequests = _contentAssetRequestCount, accepted = _contentAssetAcceptedCount,
                    sourceVersion = _contentPorts.Views.Version, observedVersion = _contentSourceVersion, pending = _contentSourceRefreshPending,
                    transition = _transitionLayout is not null, command = _activeCommandRequestId, suspended = _contentPresentationSuspended,
                    placements = ContentDocument.Placements.Count, demand = _contentVisibleAssetKeys.Count, bitmaps = _contentBitmaps.Count,
                    attempted = _contentAssetAttempted.Count, views = ReadContentViews().Select(v => new { v.ItemId, v.AssetKey, v.IsAvailable }) }));
            await Task.Delay(50);
        }
        return RenderIdentity();
    }
    private ContentRenderIdentity RenderIdentity()
    {
        foreach (var (tile, card) in _contentCards)
        {
            string item = _contentCardPaint[tile].View.ItemId;
            if (_contentVisibleAssetKeys.TryGetValue(item, out var key) && _contentBitmaps.TryGetValue(key, out var bitmap) &&
                (card is not Grid cell || cell.Children.OfType<Border>().FirstOrDefault()?.Child is not Grid host ||
                 !host.Children.OfType<Image>().Any(image => ReferenceEquals(image.Source, bitmap))))
                throw new IOException("Cached raster is not displayed by its actual card: " + item);
        }
        return new(_contentPorts, _contentAssets!, _contentAssetRequestCount,
            _contentCards.ToDictionary(p => p.Key, p => p.Value),
            _contentVisibleAssetKeys.Where(p => _contentBitmaps.ContainsKey(p.Value)).ToDictionary(p => p.Key, p => _contentBitmaps[p.Value]));
    }

    internal void RequireRetainedRenderForProbe(ContentRenderIdentity before, int addedRequests = 0)
    {
        var after = RenderIdentity();
        if (!ReferenceEquals(before.Source, after.Source) || !ReferenceEquals(before.Cache, after.Cache) ||
            after.Requests - before.Requests != addedRequests) throw new IOException($"Whole-source/cache reload: {after.Requests - before.Requests} requests, expected {addedRequests}.");
        foreach (var (id, card) in before.Cards)
            if (!after.Cards.TryGetValue(id, out var retained) || !ReferenceEquals(card, retained)) throw new IOException("Unchanged card rebuilt: " + id);
        foreach (var (id, image) in before.Images)
            if (!after.Images.TryGetValue(id, out var retained) || !ReferenceEquals(image, retained)) throw new IOException("Unchanged raster replaced: " + id);
    }

    // Actual published hit regions and production gesture handlers; no OS input synthesis.
    internal async Task VerifyIncrementalRenderForProbe()
    {
        await CaptureRenderIdentityForProbe();
        var doc = ContentDocument;
        if (doc.Mode != ContentLayoutMode.Manual || doc.Placements.Count < 2) throw new IOException("Manual multi-item fixture required.");
        string itemId = doc.Placements[0].ItemId;
        if (SelectContentItem(itemId) is { } selectError) throw new IOException(selectError);
        var before = await CaptureRenderIdentityForProbe();
        var surface = inputRouter.ReadSurface(inputSession)!;
        var region = surface.Layout.Regions.First(r => r.Kind == PosterInputTargetKind.ContentItem && r.ContentItemId == itemId);
        var capture = new PosterInputCapture(Guid.NewGuid(), new(inputSession, surface.Layout.Revision, surface.Frame, region));
        var press = new PointerScreenPoint(surface.Frame.X + region.Bounds.X + region.Bounds.Width / 2,
            surface.Frame.Y + region.Bounds.Y + region.Bounds.Height / 2);
        var other = surface.Layout.Regions.First(r => r.Kind == PosterInputTargetKind.ContentItem && r.ContentItemId != itemId);
        var destination = new PointerScreenPoint(surface.Frame.X + other.Bounds.X + other.Bounds.Width / 2,
            surface.Frame.Y + other.Bounds.Y + other.Bounds.Height / 2);
        long sequence = 0;
        void Send(PosterInputEventKind kind, PointerScreenPoint point) => HandleContentInput(PosterContentInputPolicy.Event(
            capture, kind, press, point, inputRouter.ReadSurface(inputSession)!.Layout.Revision, ++sequence));
        try
        {
            Send(PosterInputEventKind.Pressed, press);
            if (_contentGesture is not { CanEdit: true }) throw new IOException("Incremental probe did not enter a real editable gesture.");
            for (int i = 0; i < 40; i++)
            {
                Send(PosterInputEventKind.GesturePreview, i % 2 == 0 ? destination : press);
                FlushContentMotion(false);
                RequireRetainedRenderForProbe(before);
            }
        }
        finally { CancelContentGesture("incremental-render-probe"); }
        RequireRetainedRenderForProbe(before);
        if (NudgeContent(1, 0, false) is { } moveError) throw new IOException(moveError);
        await Task.Delay(150);
        RequireRetainedRenderForProbe(before);
        if (UndoContent(false) is { } undoError) throw new IOException(undoError);
        await Task.Delay(150);
        RequireRetainedRenderForProbe(before);
        log.Write("info", "contract.incremental_render_passed", new { PosterId, previews = 40, requestsDuringLayout = 0,
            retainedCards = before.Cards.Count, retainedImages = before.Images.Count, nativeMouse = false });
        await VerifyIncomingHoverForProbe();
    }

    private async Task VerifyIncomingHoverForProbe()
    {
        var before = await CaptureRenderIdentityForProbe();
        var positions = before.Cards.ToDictionary(p => p.Key, p => (Canvas.GetLeft(p.Value), Canvas.GetTop(p.Value), p.Value.Opacity));
        string raw = ContentLayoutPolicy.Serialize(ContentDocument);
        long entity = _posterEntityVersion;
        var surface = IncomingSurface();
        var first = _contentSnapshot!.Items.OrderBy(i => i.ReadOrder).First();
        var point = new PointerScreenPoint(surface.Frame.X + (int)((surface.ContentViewport.X + first.Bounds.X + 1) * surface.ScaleX),
            surface.Frame.Y + (int)((surface.ContentViewport.Y + first.Bounds.Y + 1) * surface.ScaleY));
        void Unchanged()
        {
            if (raw != ContentLayoutPolicy.Serialize(ContentDocument) || entity != _posterEntityVersion || surface != IncomingSurface())
                throw new IOException("Incoming hover changed the committed document or native geometry witness.");
            RequireRetainedRenderForProbe(before);
        }
        void Restored()
        {
            Unchanged();
            if (_incomingLayout is not null || before.Cards.Any(p => positions[p.Key] !=
                (Canvas.GetLeft(p.Value), Canvas.GetTop(p.Value), p.Value.Opacity)))
                throw new IOException("Incoming cancel/leave failed to restore original visuals.");
        }
        foreach (int count in new[] { 1, 3 })
        {
            var session = new IncomingDropSession();
            var paths = Enumerable.Range(0, count).Select(i => @"Z:\not-accessed-hover-fixture\" + i + ".txt").ToArray();
            try
            {
                ShowIncomingPreview(session.Enter(paths, surface, point, 0, 2));
                var plan = _incomingLayout ?? throw new IOException("Incoming hover did not create its transient layout.");
                if (plan.Placeholders.Count != count || !before.Cards.Any(p => positions[p.Key] !=
                    (Canvas.GetLeft(p.Value), Canvas.GetTop(p.Value), p.Value.Opacity)))
                    throw new IOException("Incoming hover did not visibly displace an existing item.");
                for (int i = 0; i < 40; i++)
                {
                    ShowIncomingPreview(session.Over(IncomingSurface(), point, 0, 2));
                    if (!ReferenceEquals(plan, _incomingLayout)) throw new IOException("Repeated same landing recalculated or drifted.");
                    Unchanged();
                }
                if (_contentCards.Keys.Any(plan.Placeholders.Contains) || _contentVisibleAssetKeys.Keys.Any(plan.Placeholders.Contains))
                    throw new IOException("A hover placeholder became a real card or asset request.");
                session.Leave(); ShowIncomingPreview(null); Restored();
                ShowIncomingPreview(session.Enter(paths, surface, point, 0, 2));
                var title = new PointerScreenPoint(surface.Frame.X + 4, surface.Frame.Y + 4);
                ShowIncomingPreview(session.Over(surface, title, 0, 2)); Restored();
                ShowIncomingPreview(session.Over(surface, point, 0, 2));
                var ticket = session.Drop(paths, IncomingSurface(), point, 0, 2);
                ShowIncomingPreview(null); Restored();
                if (ticket is null) throw new IOException("Visual hover invalidated a valid drop ticket.");
                // Do not dispatch these fake paths. Actual commits are checked by IncomingProbe/pure integration tests.
            }
            finally { session.Leave(); ShowIncomingPreview(null); }
        }
        log.Write("info", "contract.incoming_hover_passed", new { counts = new[] { 1, 3 }, repeatedOver = 40,
            retainedCards = before.Cards.Count, requestsDuringHover = 0, authorUnchanged = true, nativeMouse = false });
    }
}
