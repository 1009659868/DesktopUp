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
    internal void VerifyContentPublicationForProbe()
    {
        RefreshContentSurface();
        var previous = Presenter.Frame ?? throw new IOException("No published paint.");
        var continuity = _contentLayout.Space.Published;
        var prepared = PrepareSpatialFrame(_bounds) ?? throw new IOException("No prepared frame.");
        // A stale pair is rejected before native writes; a mismatched native size is rejected by
        // the actual input adapter after preparation. Both must retain the previous paint.
        if (CommitSpatialFrames([prepared with { Input = prepared.Input with
            { ContentSnapshotGeneration = prepared.Input.ContentSnapshotGeneration + 1 } }]) ||
            !ReferenceEquals(previous, Presenter.Frame) || !ReferenceEquals(continuity, _contentLayout.Space.Published))
            throw new IOException("Stale presentation pair advanced paint/continuity.");
        RefreshContentSurface();
        previous = Presenter.Frame;
        prepared = PrepareSpatialFrame(_bounds)!;
        if (ApplyContentFrame(_bounds, prepared.Input with { FrameWidth = prepared.Input.FrameWidth + 1 }) ||
            !ReferenceEquals(previous, Presenter.Frame))
            throw new IOException("Native input rejection advanced paint.");
        RefreshContentSurface();
        previous = Presenter.Frame;
        prepared = PrepareSpatialFrame(_bounds)!;
        if (CommitSpatialFrames([prepared with { Input = prepared.Input with
            { FrameWidth = prepared.Input.FrameWidth + 1 } }]) || !ReferenceEquals(previous, Presenter.Frame))
            throw new IOException("Partial batch failed to restore paint.");
        RefreshContentSurface();
        var surface = inputRouter.ReadSurface(inputSession);
        if (surface is null || surface.Layout.ContentSnapshotGeneration != Presenter.Frame!.Snapshot.ViewportGeneration)
            throw new IOException("Recovery did not publish the same generation.");
        var retained = RenderIdentity();
        var tile = Presenter.Cards.First(p => p.Value.Opacity > 0);
        var cell = (Grid)tile.Value;
        var selection = SelectedContentIds().ToArray();
        for (int i = 0; i < 60; i++)
        {
            Presenter.HoverAt(Canvas.GetLeft(cell) + cell.Width / 2, Canvas.GetTop(cell) + cell.Height / 2);
            if (cell.Background is null) throw new IOException("No local hover feedback.");
            Presenter.SetHover(null);
            if (cell.Background is not null) throw new IOException("Hover did not clear.");
        }
        RequireRetainedRenderForProbe(retained);
        if (!selection.SequenceEqual(SelectedContentIds())) throw new IOException("Hover changed selection.");
        log.Write("info", "contract.content_hover_passed", new { repeats = 60, newRasterRequests = 0, selectionUnchanged = true });
        log.Write("info", "contract.r3_frame_publication_passed", new { stale = true, nativeReject = true, batchReject = true });
    }

    internal async Task<ContentRenderIdentity> CaptureRenderIdentityForProbe()
    {
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while (Rasters.Cache is null || Rasters.Cache.RunningLoads != 0 || Rasters.Requests != 0 ||
            _contentPorts.Views.Version != _contentSession.SourceVersion || ContentDocument.Placements.Any(p =>
                !Rasters.VisibleKeys.TryGetValue(p.ItemId, out var key) || !Presenter.Bitmaps.ContainsKey(key)))
        {
            if (timeout.Elapsed.TotalSeconds > 20) throw new IOException("Incremental raster fixture did not finish loading: " +
                System.Text.Json.JsonSerializer.Serialize(new { cache = Rasters.Cache is not null, running = Rasters.Cache?.RunningLoads,
                    requests = Rasters.Requests, totalRequests = Rasters.RequestedCount, accepted = Rasters.AcceptedCount,
                    sourceVersion = _contentPorts.Views.Version, observedVersion = _contentSession.SourceVersion, pending = _contentSession.SourceRefreshPending,
                    transition = _transitionLayout is not null, command = _contentSession.ActiveCommand, suspended = _contentSession.IsSuspended,
                    placements = ContentDocument.Placements.Count, demand = Rasters.VisibleKeys.Count, bitmaps = Presenter.Bitmaps.Count,
                    attempted = Rasters.Attempted, views = ReadContentViews().Select(v => new { v.ItemId, v.AssetKey, v.IsAvailable }) }));
            await Task.Delay(50);
        }
        return RenderIdentity();
    }
    private ContentRenderIdentity RenderIdentity()
    {
        foreach (var (tile, card) in Presenter.Cards)
        {
            string item = Presenter.ItemForTile(tile);
            if (Rasters.VisibleKeys.TryGetValue(item, out var key) && Presenter.Bitmaps.TryGetValue(key, out var bitmap) &&
                (card is not Grid cell || cell.Children.OfType<Border>().FirstOrDefault()?.Child is not Grid host ||
                 !host.Children.OfType<Image>().Any(image => ReferenceEquals(image.Source, bitmap))))
                throw new IOException("Cached raster is not displayed by its actual card: " + item);
        }
        return new(_contentPorts, Rasters.Cache!, Rasters.RequestedCount,
            Presenter.Cards.ToDictionary(p => p.Key, p => p.Value),
            Rasters.VisibleKeys.Where(p => Presenter.Bitmaps.ContainsKey(p.Value)).ToDictionary(p => p.Key, p => Presenter.Bitmaps[p.Value]));
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

    // Exercise production posture commands, not a test-only visibility toggle. Cards may be
    // virtualized at compact height, but every warm image must survive and paint immediately.
    internal async Task VerifyPostureRastersForProbe(Func<bool, Task> posture)
    {
        var before = await CaptureRenderIdentityForProbe();
        int frames = 0, transitionFrames = 0;
        Exception? frameError = null;
        void Check()
        {
            var after = RenderIdentity();
            if (!ReferenceEquals(before.Cache, after.Cache) || !ReferenceEquals(before.Source, after.Source) ||
                after.Requests != before.Requests) throw new IOException("Posture discarded or re-requested warm rasters.");
            foreach (var (id, image) in before.Images)
                if (!after.Images.TryGetValue(id, out var retained) || !ReferenceEquals(image, retained))
                    throw new IOException("Posture lost a warm bitmap: " + id);
        }
        void Frame(object? sender, object args)
        {
            try { frames++; if (_transitionLayout is not null) transitionFrames++; Check(); }
            catch (Exception error) { frameError ??= error; }
        }
        Microsoft.UI.Xaml.Media.CompositionTarget.Rendering += Frame;
        try
        {
            for (int cycle = 0; cycle < 6; cycle++)
            {
                await posture(true); Check();
                await Task.Delay(60);
                await posture(false); Check();
                await Task.Delay(60);
                if (frameError is not null) throw frameError;
            }
            if (frames == 0 || transitionFrames == 0) throw new IOException("No composed transition frames observed.");
            log.Write("info", "contract.posture_rasters_passed", new { cycles = 6, frames, transitionFrames,
                retainedImages = before.Images.Count, requestsDuringPosture = Rasters.RequestedCount - before.Requests,
                nativeMouse = false, nativeKeyboard = false });
        }
        finally { Microsoft.UI.Xaml.Media.CompositionTarget.Rendering -= Frame; }
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
        var first = _contentLayout.Snapshot!.Items.OrderBy(i => i.ReadOrder).First();
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
                if (Presenter.Cards.Keys.Any(plan.Placeholders.Contains) || Rasters.VisibleKeys.Keys.Any(plan.Placeholders.Contains))
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
