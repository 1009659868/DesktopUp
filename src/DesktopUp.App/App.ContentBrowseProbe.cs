using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DesktopUp;

internal sealed record ContentBrowseTileProbe(string ItemId, int SourceIndex, int LoopIndex,
    ContentRect Bounds, ContentRect Clip, double? CanvasX, double? CanvasY,
    DesktopPixelRect ExpectedInput, DesktopPixelRect? ActualInput, bool CanvasMatches, bool InputMatches);

internal sealed record ContentBrowseProbeRead(long Revision, long EntityVersion, long DocumentVersion,
    ContentFlowAxis Axis, bool LoopPreference, bool IsLooping, double ScrollX, double ScrollY,
    double Step, double Period, double Extent, double View, int Members, int Cards, int HitItems,
    bool CanvasMatches, bool InputMatches, bool Clipped, string LastItemId,
    string MembershipFingerprint, string StoredFingerprint, string AuthorFingerprint,
    IReadOnlyList<ContentBrowseTileProbe> Tiles,
    [property: JsonIgnore] string? StoredRaw, [property: JsonIgnore] string AuthorRaw);

internal sealed partial class PosterSpatialRuntime
{
    internal Task<object> RunContentBrowseForProbeAsync(string id, string dataRoot,
        Func<DesktopUpProjection> current, CancellationToken cancellation) =>
        _windows[id].RunContentBrowseForProbeAsync(dataRoot, current, cancellation);
}

internal sealed partial class PosterVisualWindow
{
    // Read the exact production projection, live XAML cards and published regions. In particular,
    // loop copies use TileKey, not the old finite-only reader's one-control-per-ItemId assumption.
    private ContentBrowseProbeRead ReadBrowseForProbe()
    {
        (_window?.Content as FrameworkElement)?.UpdateLayout();
        var snapshot = _contentSnapshot ?? throw new InvalidOperationException("No presented content snapshot.");
        var viewport = _contentViewport ?? throw new InvalidOperationException("No presented content viewport.");
        var published = inputRouter.ReadSurface(inputSession) ?? throw new InvalidOperationException("No published input surface.");
        if (_monitor is null) throw new InvalidOperationException("No display scale.");
        var document = ContentDocument;
        var tiles = ContentLayoutFlow.ProjectTiles(snapshot, viewport);
        var hits = published.Layout.Regions.Where(r => r.Kind == PosterInputTargetKind.ContentItem).ToList();
        int hitCount = hits.Count;
        var reads = new List<ContentBrowseTileProbe>();
        foreach (var tile in tiles)
        {
            _contentCards.TryGetValue(TileKey(tile), out var card);
            bool canvas = card is not null && _contentCanvas?.Children.Contains(card) == true &&
                Math.Abs(Canvas.GetLeft(card) - tile.Bounds.X) < .01 &&
                Math.Abs(Canvas.GetTop(card) - tile.Bounds.Y) < .01 &&
                Math.Abs(card.Width - tile.Bounds.Width) < .01 && Math.Abs(card.Height - tile.Bounds.Height) < .01;
            int left = (int)Math.Round((ContentLeftDip + tile.Clip.X) * _monitor.ScaleX);
            int top = (int)Math.Round((ContentTopDip + tile.Clip.Y) * _monitor.ScaleY);
            int right = (int)Math.Round((ContentLeftDip + tile.Clip.Right) * _monitor.ScaleX);
            int bottom = (int)Math.Round((ContentTopDip + tile.Clip.Bottom) * _monitor.ScaleY);
            var expected = new DesktopPixelRect(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
            int hitIndex = hits.FindIndex(h => h.ContentItemId == tile.ItemId && h.Index == tile.SourceIndex &&
                h.ContentSnapshotGeneration == snapshot.ViewportGeneration && h.Bounds == expected);
            DesktopPixelRect? actual = hitIndex >= 0 ? hits[hitIndex].Bounds : null;
            if (hitIndex >= 0) hits.RemoveAt(hitIndex);
            bool identity = tile.SourceIndex >= 0 && tile.SourceIndex < document.Placements.Count &&
                document.Placements[tile.SourceIndex].ItemId == tile.ItemId;
            reads.Add(new(tile.ItemId, tile.SourceIndex, tile.LoopIndex, tile.Bounds, tile.Clip,
                card is null ? null : Canvas.GetLeft(card), card is null ? null : Canvas.GetTop(card),
                expected, actual, canvas && identity, hitIndex >= 0 && identity));
        }
        var clip = (_contentCanvas?.Clip as RectangleGeometry)?.Rect;
        string raw = ContentLayoutPolicy.Serialize(document);
        string? stored = _authoritativePoster?.ContentLayoutJson;
        bool vertical = snapshot.FlowAxis == ContentFlowAxis.Vertical;
        return new(_stateRevision, _posterEntityVersion, document.DocumentVersion, snapshot.FlowAxis,
            document.LoopScroll, ContentLayoutFlow.IsLooping(snapshot, viewport), snapshot.ScrollXDip, snapshot.ScrollYDip,
            Math.Max(1, snapshot.CellDip + snapshot.GapDip), ContentLayoutFlow.LoopPeriodDip(snapshot, viewport),
            vertical ? snapshot.ExtentHeightDip : snapshot.ExtentWidthDip,
            vertical ? viewport.HeightDip : viewport.WidthDip, document.Placements.Count, _contentCards.Count, hitCount,
            reads.All(r => r.CanvasMatches) && _contentCards.Count == tiles.Count,
            reads.All(r => r.InputMatches) && hits.Count == 0 && hitCount == tiles.Count,
            clip.HasValue && Math.Abs(clip.Value.Width - viewport.WidthDip) < .01 && Math.Abs(clip.Value.Height - viewport.HeightDip) < .01,
            snapshot.Items.OrderBy(p => p.ReadOrder).Last().ItemId,
            StateRequestFingerprint.Compute(document.Placements.Select(p => p.ItemId).ToArray()),
            StateRequestFingerprint.Compute(stored), StateRequestFingerprint.Compute(raw), reads, stored, raw);
    }

    internal async Task<object> RunContentBrowseForProbeAsync(string dataRoot,
        Func<DesktopUpProjection> current, CancellationToken cancellation)
    {
        var observations = new List<object>(); var captures = new List<string>(); var cases = new List<string>();
        string stage = "preconditions"; int checks = 0, wheelEvents = 0;
        var nativeCapture = inputRouter.Capture;
        void Require(bool ok, string message)
        { checks++; if (!ok) throw new InvalidOperationException(stage + ": " + message); }
        ContentBrowseProbeRead Read(string name)
        {
            cancellation.ThrowIfCancellationRequested();
            var read = ReadBrowseForProbe(); observations.Add(new { name, read });
            Require(read.CanvasMatches && read.InputMatches && read.Clipped, "Production tiles, XAML and input regions disagree.");
            Require(read.Cards > 0 && read.Cards < 200, "Visible controls are empty or unbounded.");
            Require(read.Axis == ContentFlowAxis.Vertical ? Math.Abs(read.ScrollX) < 1e-7 : Math.Abs(read.ScrollY) < 1e-7,
                "Browsing changed the inactive axis.");
            Require(inputRouter.Capture == nativeCapture && !HasContentGesture && _transitionLayout is null,
                "Managed browsing acquired native capture or a spatial/content gesture.");
            return read;
        }
        static double Offset(ContentBrowseProbeRead read) => read.Axis == ContentFlowAxis.Vertical ? read.ScrollY : read.ScrollX;
        void At(ContentBrowseProbeRead read, double expected)
        {
            double distance = Math.Abs(Offset(read) - expected);
            if (read.IsLooping) distance = Math.Min(distance, Math.Abs(read.Period - distance));
            Require(distance < 1e-7, $"Expected phase {expected:R}; actual {Offset(read):R}.");
        }
        ContentBrowseProbeRead Send(string name, int wheel)
        {
            cancellation.ThrowIfCancellationRequested();
            var before = ReadBrowseForProbe(); var saved = current();
            var published = inputRouter.ReadSurface(inputSession)!;
            var region = published.Layout.Regions.First(r => r.Kind == PosterInputTargetKind.ContentItem);
            var args = new PosterInputEventArgs(PosterInputEventKind.Scrolled, region.Kind, region.Index,
                published.Frame, 0, 0, published.Layout.Revision, wheel)
            {
                Session = inputSession, ContentItemId = region.ContentItemId,
                ContentSnapshotGeneration = region.ContentSnapshotGeneration,
                LatestPoint = new(published.Frame.X + region.Bounds.X + region.Bounds.Width / 2,
                    published.Frame.Y + region.Bounds.Y + region.Bounds.Height / 2)
            };
            Require(HandleContentInput(args), "Scrolled event escaped the production content handler.");
            wheelEvents++;
            var after = Read(name); var savedAfter = current();
            observations.Add(new { name = name + "-event", wheel, region.ContentItemId,
                beforePhase = Offset(before), afterPhase = Offset(after), before.Revision,
                storedRevisionBefore = saved.Revision, storedRevisionAfter = savedAfter.Revision });
            Require(before.Revision == after.Revision && before.EntityVersion == after.EntityVersion &&
                before.DocumentVersion == after.DocumentVersion && before.StoredRaw == after.StoredRaw &&
                before.AuthorRaw == after.AuthorRaw && before.MembershipFingerprint == after.MembershipFingerprint,
                "Browsing changed raw author content, identity, or an in-memory version.");
            Require(saved.Revision == savedAfter.Revision && saved.PostersById[inputSession.PosterId].Poster ==
                savedAfter.PostersById[inputSession.PosterId].Poster, "Browsing persisted poster state.");
            Require(Offset(after) >= 0 && (after.IsLooping ? Offset(after) < after.Period :
                Offset(after) <= Math.Max(0, after.Extent - after.View) + 1e-7), "Scroll phase escaped its valid range.");
            return after;
        }
        void Configure(string name, Func<string?> action)
        {
            cancellation.ThrowIfCancellationRequested();
            var before = current(); string? error = action(); var after = current();
            observations.Add(new { name, fixtureConfiguration = true, beforeRevision = before.Revision, afterRevision = after.Revision, error });
            Require(error is null, "Formal fixture configuration failed: " + error);
            Require(after.Revision >= before.Revision && after.Revision <= before.Revision + 1,
                "Fixture configuration committed an unexpected number of revisions.");
        }
        async Task Capture(string name)
        {
            cancellation.ThrowIfCancellationRequested();
            string path = Path.Combine(dataRoot, "content-browse-" + name + ".png");
            await CaptureLampXamlForProbeAsync(path);
            captures.Add(path);
            cancellation.ThrowIfCancellationRequested();
        }
        void Head() => ScrollContentForSmoke(0, 0); // Explicit RAM-only fixture reset, never a wheel assertion.
        try
        {
            Require(!_disposed && !_contentPresentationSuspended && !_geometry.IsCollapsed && !_geometry.IsLocked &&
                !IsEditing && !HasContentGesture && _transitionLayout is null, "Use an expanded unlocked idle fixture.");
            var initial = Read("fixture-initial");
            Require(initial.Members == 100 && ContentDocument.Mode == ContentLayoutMode.Automatic,
                "Expected the explicit hundred-member fixture.");
            foreach (var axis in new[] { ContentFlowAxis.Vertical, ContentFlowAxis.HorizontalLoop })
            {
                string name = axis == ContentFlowAxis.Vertical ? "vertical" : "horizontal";
                stage = name + "-loop-seam";
                Configure(name + "-axis", () => SetContentFlowAxis(axis));
                Configure(name + "-loop-on", () => SetContentLoopScroll(true));
                Head(); var head = Read(name + "-head-before");
                Require(head.IsLooping && head.Period > head.View + head.Step, "Hundred fixture did not overflow its main axis.");
                At(head, 0); await Capture(name + "-head-before");
                var up = Send(name + "-head-after-up", 120);
                At(up, head.Period - head.Step);
                Require(up.Tiles.Any(t => t.ItemId == head.LastItemId) && up.Tiles.Any(t => t.LoopIndex != 0),
                    "Reverse from the head did not expose the last identity plus a seam copy.");
                await Capture(name + "-head-after-up");
                At(Send(name + "-reverse-to-head", -120), 0); cases.Add(stage);

                stage = name + "-bidirectional";
                for (int i = 0; i < 7; i++) Send(name + "-up-" + i, 120);
                for (int i = 0; i < 7; i++) Send(name + "-down-back-" + i, -120);
                At(Read(name + "-up-down-return"), 0);
                for (int i = 0; i < 5; i++) Send(name + "-down-" + i, -120);
                for (int i = 0; i < 5; i++) Send(name + "-up-back-" + i, 120);
                At(Read(name + "-down-up-return"), 0); cases.Add(stage);

                stage = name + "-small-delta";
                for (int i = 1; i <= 6; i++) At(Send(name + "-small-up-" + i, 20), head.Period - head.Step * i / 6d);
                var small = Read(name + "-six-small-equal-one"); At(small, Offset(up));
                for (int i = 1; i <= 6; i++) At(Send(name + "-small-down-" + i, -20), head.Period - head.Step + head.Step * i / 6d);
                At(Read(name + "-small-return"), 0); cases.Add(stage);

                stage = name + "-finite";
                Configure(name + "-loop-off", () => SetContentLoopScroll(false)); Head();
                At(Send(name + "-finite-head-up", 120), 0);
                At(Send(name + "-finite-down", -120), head.Step);
                At(Send(name + "-finite-back", 120), 0);
                var tail = Send(name + "-finite-tail-clamp", -1200000);
                At(tail, Math.Max(0, tail.Extent - tail.View));
                At(Send(name + "-finite-head-clamp", 1200000), 0); cases.Add(stage);

                stage = name + "-disable-while-wrapped";
                Configure(name + "-loop-on-again", () => SetContentLoopScroll(true)); Head();
                Send(name + "-wrapped-before-disable", 120);
                Configure(name + "-disable-wrapped", () => SetContentLoopScroll(false));
                var disabled = Read(name + "-wrapped-loop-disabled");
                Require(!disabled.IsLooping && !disabled.LoopPreference && Offset(disabled) <= Math.Max(0, disabled.Extent - disabled.View) + 1e-7,
                    "Disabling looping did not clamp to the finite strip.");
                At(Send(name + "-disabled-head-clamp", 1200000), 0); cases.Add(stage);
            }
            foreach (var axis in new[] { ContentFlowAxis.Vertical, ContentFlowAxis.HorizontalLoop })
            {
                string name = axis == ContentFlowAxis.Vertical ? "vertical" : "horizontal";
                stage = name + "-short-content";
                // Existing sample factory, version rebasing, validator and transaction. This is
                // explicitly setup in an isolated profile; it is not counted as a browsing action.
                Configure(name + "-one-item-fixture", () =>
                {
                    var before = ContentDocument;
                    var one = ContentLayoutSamples.Equal(before.PosterId, 1) with { FlowAxis = axis, LoopScroll = true };
                    return CommitContentDocument(before, RebaseContentIntent(before, one), _posterEntityVersion, _stateRevision);
                });
                Head(); var shortContent = Read(name + "-short-before");
                Require(shortContent.LoopPreference && !shortContent.IsLooping && shortContent.Members == 1 &&
                    shortContent.Extent <= shortContent.View + .5, "Single-card fixture is not shorter than its viewport.");
                foreach (int wheel in new[] { 120, -120, 20, -20, 1200000, -1200000 })
                {
                    var read = Send(name + "-short-wheel-" + wheel, wheel); At(read, 0);
                    Require(read.Tiles.Count == 1 && read.Tiles[0].LoopIndex == 0, "Short content duplicated a tile.");
                }
                cases.Add(stage);
            }
            return new { passed = true, stage, checks, wheelEvents, cases, observations, captures };
        }
        catch (Exception error)
        { return new { passed = false, stage, checks, wheelEvents, cases, observations, captures, error = error.ToString() }; }
    }
}

public partial class App
{
    private async void RunContentBrowseProbe(string dataRoot)
    {
        using var lease = _ledger!.Acquire("fixture.content-browse", "isolated-production-scroll-handler");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(75));
        void Stop() { cancellation.Cancel(); lease.Dispose(); }
        _stopAutoSmoke = Stop;
        object? observation = null; bool passed = false; string? error = null;
        try
        {
            for (int i = 0; ; i++)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                var current = _stateCoordinator!.Refresh();
                if (_autoReady && _spatialRuntime is { IsSpatialBusy: false } && current.Posters.Count == 1 &&
                    _spatialRuntime.ContentVisualReadyForProbe(current.Posters[0].Poster.PosterId)) break;
                if (i >= 100) throw new TimeoutException("Hundred-item fixture did not become ready.");
                await Task.Delay(100, cancellation.Token);
            }
            var state = _stateCoordinator!.Refresh(); var poster = state.Posters.Single();
            if (poster.AutoPosture.Enabled || poster.Poster.IsCollapsed || poster.Poster.IsLocked || !poster.Poster.IsVisible)
                throw new InvalidOperationException("Use a visible expanded unlocked fixture without automatic posture.");
            observation = await _spatialRuntime!.RunContentBrowseForProbeAsync(poster.Poster.PosterId, dataRoot,
                () => _stateCoordinator.Refresh(), cancellation.Token);
            using var json = JsonDocument.Parse(JsonSerializer.Serialize(observation));
            passed = json.RootElement.GetProperty("passed").GetBoolean();
        }
        catch (Exception failure) { error = failure.ToString(); }
        finally { if (_stopAutoSmoke == Stop) _stopAutoSmoke = null; }
        var report = new { passed, error, observation,
            inputDriver = "managed-Scrolled-event-with-current-published-region-to-production-HandleContentInput-and-ScrollItems",
            layoutEvidence = "existing-ProjectTiles-versus-live-XAML-and-published-input-regions",
            fixtureSetup = "formal-axis-loop-and-single-item-commands; RAM-only-head-reset",
            nativeMouse = false, nativeKeyboard = false, nativeWheel = false,
            desktopPixelVisualCertified = false, captureIncludesSystemBackdrop = false,
            processRestartTested = false, uiCProductAccepted = false, milestoneAccepted = false };
        File.WriteAllText(Path.Combine(dataRoot, "content-browse-observation.json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        lease.Dispose();
        _log!.Write(passed ? "info" : "error", "content.browse_observed", report);
        ExitApplication();
    }
}
