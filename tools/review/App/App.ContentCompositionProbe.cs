using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Windows;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace DesktopUp;

internal sealed record ContentCompositionRead(ContentLayoutDocument Document, ContentLayoutSnapshot Snapshot,
    ContentLayoutViewport Viewport, string? StoredJson, long Revision, long EntityVersion, long OpenIntents,
    double WidthDip, string? Notice, string? DisplayText, bool FeedbackVisible, ContentSurfaceProbe Surface);

internal sealed record ContentCompositionPress(PosterInputCapture Capture, PointerScreenPoint Start,
    PointerScreenPoint End, ContentCompositionRead Before, ContentLayoutDocument Expected,
    ContentInteractionFeedback Feedback);

internal sealed partial class PosterVisualWindow
{
    internal ContentCompositionRead ReadCompositionForProbe()
    {
        (_window?.Content as FrameworkElement)?.UpdateLayout();
        return new(ContentLayoutPolicy.Freeze(ContentDocument), _contentLayout.Snapshot!, _contentLayout.Viewport!,
            _authoritativePoster?.ContentLayoutJson, _stateRevision, _posterEntityVersion, _contentOpenIntentCount,
            _geometry.WidthDip, _contentNotice, _contentFeedbackText?.Text,
            _contentFeedbackCallout?.Visibility == Visibility.Visible, ReadContentSurfaceForSmoke());
    }

    // Isolated protocol driver: real published regions and production handler/flush, no OS input,
    // native capture, fabricated desired document, or alternate placement solver.
    internal ContentCompositionPress BeginCompositionForProbe()
    {
        void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        Require(TestContentFeedbackEnabled, "Composition feedback observation requires explicit DESKTOPUP_TEST_CONTENT_FEEDBACK=1 from the test runner.");
        Require(!_disposed && !_contentSession.IsSuspended && !HasContentGesture && !_contentReadOnly &&
            !_geometry.IsLocked && !IsEditing && _transitionLayout is null && inputRouter.Capture is null,
            "Composition probe requires an idle writable native surface.");
        var before = ReadCompositionForProbe();
        Require(before.Document.Mode == ContentLayoutMode.Manual && before.Snapshot.SpaceStatus == ContentLayoutSpaceStatus.Normal,
            "Narrow composition must remain ordinary writable Manual, not a restricted-space mode.");
        var tile = ContentLayoutFlow.ProjectTiles(before.Snapshot, before.Viewport).OrderBy(t => t.ReadOrder).First(t =>
            t.Clip.Width == t.Bounds.Width && t.Clip.Height == t.Bounds.Height && !t.Clip.IsEmpty);
        var surface = inputRouter.ReadSurface(inputSession) ?? throw new InvalidOperationException("No published input surface.");
        var region = surface.Layout.Regions.Single(r => r.Kind == PosterInputTargetKind.ContentItem && r.ContentItemId == tile.ItemId);
        Require(region.ContentSnapshotGeneration == before.Snapshot.ViewportGeneration, "Press generation differs from actual composition.");
        var capture = new PosterInputCapture(Guid.NewGuid(), new(inputSession, surface.Layout.Revision, surface.Frame, region));
        var start = new PointerScreenPoint(surface.Frame.X + region.Bounds.X + region.Bounds.Width / 2,
            surface.Frame.Y + region.Bounds.Y + region.Bounds.Height / 2);
        var end = start with { Y = start.Y + (int)Math.Round((before.Snapshot.CellDip + before.Snapshot.GapDip) * _monitor!.ScaleY) };
        void Send(PosterInputEventKind kind, PointerScreenPoint point, long sequence) =>
            HandleContentInput(PosterContentInputPolicy.Event(capture, kind, start, point,
                inputRouter.ReadSurface(inputSession)!.Layout.Revision, sequence, TitleNativeEndReason.None));
        try
        {
            Send(PosterInputEventKind.Pressed, start, 1);
            Require(_contentGesture?.Capture == capture && _contentGesture.CanEdit, "Real press did not acquire editable RAM gesture.");
            Send(PosterInputEventKind.GesturePreview, end, 2);
            FlushContentMotion(false);
            var gesture = _contentGesture!;
            Require(_contentSession.Draft is not null && gesture.LastEdit is not null && gesture.EditResult is { Accepted: true },
                "Production drag did not produce a valid preview.");
            var feedback = ContentInteractionFeedbackPolicy.Describe(gesture.Document, gesture.Snapshot, gesture.LastEdit!, gesture.EditResult!);
            Require(feedback.Impact.AdoptsProjection && feedback.Impact.OffscreenAffectedItemIds.Count > 0,
                "Preview did not exercise full-picture adoption with offscreen impact.");
            var preview = ReadCompositionForProbe();
            Require(preview.Revision == before.Revision && preview.EntityVersion == before.EntityVersion && preview.StoredJson == before.StoredJson,
                "Preview persisted author intent.");
            Require(preview.FeedbackVisible && preview.Notice?.Contains("整张", StringComparison.Ordinal) == true &&
                preview.Notice.Contains("屏外", StringComparison.Ordinal) && preview.Notice.Contains("撤销", StringComparison.Ordinal) &&
                preview.DisplayText?.Contains("整张", StringComparison.Ordinal) == true && preview.DisplayText.Contains("屏外", StringComparison.Ordinal),
                "Actual poster feedback does not explain full-picture and offscreen impact.");
            Require(preview.Surface.CanvasMatches && preview.Surface.InputMatches && preview.Surface.Clipped,
                "Native preview canvas/input/clip disagree.");
            return new(capture, start, end, before, ContentLayoutPolicy.Freeze(_contentSession.Draft!), feedback);
        }
        catch { if (_contentGesture?.Capture == capture) CancelContentGesture("composition-probe-failed"); throw; }
    }

    internal void VerifyCompositionFeedbackForProbe()
    {
        // Read after the actual preview capture/render, rather than the same dispatcher's
        // Text assignment: preserve the image even when its rendered visibility check fails.
        if (_contentFeedbackText is { IsTextTrimmed: false } text && text.ActualHeight >= 28 &&
            _contentFeedbackCallout is { } callout &&
            text.TransformToVisual(callout).TransformPoint(new(0, 0)).Y + text.ActualHeight <= callout.ActualHeight) return;
        throw new InvalidOperationException("Narrow full-picture feedback must show both lines without trimming: " +
            JsonSerializer.Serialize(new { _contentFeedbackText!.Text, _contentFeedbackText.IsTextTrimmed,
                _contentFeedbackText.ActualWidth, _contentFeedbackText.ActualHeight, _contentFeedbackText.DesiredSize,
                CalloutWidth = _contentFeedbackCallout!.ActualWidth, CalloutHeight = _contentFeedbackCallout.ActualHeight,
                TextTop = _contentFeedbackText.TransformToVisual(_contentFeedbackCallout).TransformPoint(new(0, 0)).Y }));
    }

    internal ContentCompositionRead CompleteCompositionForProbe(ContentCompositionPress press)
    {
        if (_contentGesture?.Capture != press.Capture) throw new InvalidOperationException("Composition preview expired before release.");
        HandleContentInput(PosterContentInputPolicy.Event(press.Capture, PosterInputEventKind.GestureCompleted, press.Start, press.End,
            inputRouter.ReadSurface(inputSession)!.Layout.Revision, 3, TitleNativeEndReason.None));
        var after = ReadCompositionForProbe();
        if (HasContentGesture || _contentSession.Draft is not null || _contentSession.DraftProjection is not null || _contentMotionTimer is not null ||
            inputRouter.Capture is not null || after.Revision != press.Before.Revision + 1 ||
            after.EntityVersion != press.Before.EntityVersion + 1 || after.Document.DocumentVersion != press.Before.Document.DocumentVersion + 1 ||
            after.OpenIntents != press.Before.OpenIntents || !ContentLayoutEditing.SameState(press.Expected, after.Document) ||
            !after.FeedbackVisible || after.Notice?.Contains("已采用当前整张", StringComparison.Ordinal) != true ||
            after.DisplayText?.Contains("已采用当前整张", StringComparison.Ordinal) != true)
            throw new InvalidOperationException("Release did not commit exactly the production preview once and publish its actual result.");
        return after;
    }

    internal void CancelCompositionForProbe()
    { if (HasContentGesture) CancelContentGesture("composition-probe-cleanup"); }
}

internal sealed partial class PosterSpatialRuntime
{
    internal ContentCompositionRead ReadCompositionForProbe(string id) => _windows[id].ReadCompositionForProbe();
    internal ContentCompositionPress BeginCompositionForProbe(string id) => _windows[id].BeginCompositionForProbe();
    internal void VerifyCompositionFeedbackForProbe(string id) => _windows[id].VerifyCompositionFeedbackForProbe();
    internal ContentCompositionRead CompleteCompositionForProbe(string id, ContentCompositionPress press) => _windows[id].CompleteCompositionForProbe(press);
    internal void CancelCompositionForProbe(string id) { if (_windows.TryGetValue(id, out var window)) window.CancelCompositionForProbe(); }
}

public partial class App
{
    private void RunContentCompositionProbe(string dataRoot)
    {
        var timer = _dispatcher!.CreateTimer(); timer.Interval = TimeSpan.FromMilliseconds(120);
        var lease = _ledger!.Acquire("fixture.content-composition", "isolated-full-picture-adoption");
        long deadline = _autoClock.ElapsedMilliseconds + 45000;
        int stage = 0, narrowVisits = 0; bool stopped = false, ticking = false, geometryPending = false;
        string target = ""; double wide = 0, narrow = 0; long geometryRevision = 0;
        StateCommandResult? geometryResult = null; Task? capture = null;
        ContentCompositionRead? wideAuthor = null, adopted = null;
        ContentCompositionPress? press = null;
        var observations = new List<object>(); var captures = new List<string>();
        void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        void Stop() { if (stopped) return; stopped = true; timer.Stop(); timer.Tick -= Tick; lease.Dispose(); _stopAutoSmoke = null; }
        _stopAutoSmoke = Stop;
        void Finish(bool passed, string? error)
        {
            if (!passed && target.Length > 0) _spatialRuntime?.CancelCompositionForProbe(target);
            Stop(); _log!.Write(passed ? "info" : "error", "content.composition_observed", new
            { passed, error, stage, observations, captures, nativeMouse = false, nativeKeyboard = false,
                productionHandler = true, formalGeometryCommands = true, processRestartTested = false,
                desktopPixelVisualCertified = false, uiCProductAccepted = false, milestoneAccepted = false });
            ExitApplication();
        }
        void Capture(string name)
        {
            string path = Path.Combine(dataRoot, "content-composition-" + name + ".png");
            Require(!File.Exists(path), "Capture evidence already exists.");
            captures.Add(path); capture = _spatialRuntime!.CapturePosterXamlForProbeAsync(target, path);
        }
        void Resize(DesktopUpProjection current, double width)
        {
            var basis = _spatialRuntime!.ReadControlCenterGeometry(target) ?? throw new InvalidOperationException("Actual geometry basis unavailable.");
            var poster = current.PostersById[target];
            var request = new ControlCenterGeometryRequest("composition-width-" + Guid.NewGuid().ToString("N"), poster, current.Revision,
                new StateReadSet(current.Posters.ToDictionary(p => p.Poster.PosterId, p => p.EntityVersion, StringComparer.Ordinal),
                    CatalogVersion: current.CatalogVersion), new(0, 0, width, poster.Poster.ExpandedHeightDip))
                { ActualBasis = basis, ChangedFields = PosterGeometryFields.Width };
            geometryRevision = current.Revision; geometryResult = null; geometryPending = true;
            _spatialRuntime.ApplyControlCenterGeometry(request, result => { geometryResult = result; geometryPending = false; });
        }
        void Surface(ContentCompositionRead read)
        {
            Require(read.Snapshot.SpaceStatus == ContentLayoutSpaceStatus.Normal && read.Surface.CanvasMatches &&
                read.Surface.InputMatches && read.Surface.Clipped && !read.Surface.Transition,
                "Actual composition is not normal, stable and aligned with canvas/input.");
        }
        void Tick(DispatcherQueueTimer sender, object args)
        {
            if (stopped || ticking) return; ticking = true;
            try
            {
                Require(_autoClock.ElapsedMilliseconds < deadline, "Composition probe deadline exceeded.");
                if (!_autoReady || geometryPending || _spatialRuntime is null) return;
                // Stage 3 owns the deliberately held content gesture. Waiting for idle here
                // would wait on our own release forever, rather than exercise completion.
                if (_spatialRuntime.IsSpatialBusy && stage != 3) return;
                if (capture is not null) { if (!capture.IsCompleted) return; capture.GetAwaiter().GetResult(); capture = null; }
                var current = _stateCoordinator!.Refresh();
                if (stage == 0 && current.Posters.Count > 0 && !_spatialRuntime.ContentVisualReadyForProbe(current.Posters[0].Poster.PosterId)) return;
                switch (stage)
                {
                    case 0:
                        Require(current.Posters.Count == 1, "Composition probe needs one isolated hundred-item fixture.");
                        target = current.Posters[0].Poster.PosterId;
                        var initial = _spatialRuntime.ReadCompositionForProbe(target); Surface(initial);
                        Require(initial.Document.Placements.Count == 100 && initial.Document.Mode == ContentLayoutMode.Automatic && !initial.Document.LoopScroll,
                            "Expected the explicit hundred-item finite automatic fixture.");
                        var members = initial.Document.Placements.OrderBy(p => p.Order).ToArray();
                        Require(_spatialRuntime.ApplyContentEditor(target, new(ContentLayoutEditKind.Pinned, members[0].ItemId) { Enabled = true, PinnedIndex = 1 }) is null,
                            "Formal automatic pin fixture failed.");
                        Require(_spatialRuntime.ApplyContentEditor(target, new(ContentLayoutEditKind.Resize, members[1].ItemId)
                            { ColumnSpan = members[1].ColumnSpan - 1, RowSpan = members[1].RowSpan }) is null, "Formal automatic size fixture failed.");
                        var automatic = _spatialRuntime.ReadCompositionForProbe(target).Document;
                        Require(automatic.Placements[0].PinnedIndex == 1 && automatic.Placements[1].ManualSize && automatic.Placements[1].AutomaticSizeBasis is not null,
                            "Automatic fixture lacks a genuine saved pin/size basis.");
                        Require(_spatialRuntime.ApplyContentEditor(target, new(ContentLayoutEditKind.Mode) { Mode = ContentLayoutMode.Manual }) is null,
                            "Formal enter-Manual operation failed.");
                        wideAuthor = _spatialRuntime.ReadCompositionForProbe(target); Surface(wideAuthor);
                        Require(wideAuthor.Document.SavedAutomaticMode == automatic.Mode && automatic.Placements.All(p =>
                            wideAuthor.Document.Placements.Single(n => n.ItemId == p.ItemId).SavedAutomaticState ==
                                new ContentAutomaticPlacementState(p.Order, p.ColumnSpan, p.RowSpan, p.ManualSize, p.PinnedIndex, p.Column, p.Row, p.FixedPosition)
                                    { AutomaticSizeBasis = p.AutomaticSizeBasis }), "Entering Manual lost original automatic intent.");
                        Require(wideAuthor.Snapshot.Items.Any(p => p.Clip.IsEmpty), "Wide author fixture needs offscreen members.");
                        wide = wideAuthor.WidthDip;
                        narrow = Math.Ceiling(Math.Max(240, wideAuthor.Snapshot.MinimumCrossAxisDip + wide - wideAuthor.Viewport.WidthDip + 2));
                        Require(narrow < wide - 80, "Actual workspace cannot provide a meaningful safe wide/narrow difference.");
                        observations.Add(new { name = "formal-pin-size-manual-wide-author", automatic, wideAuthor });
                        Capture("wide-author"); stage = 1; break;
                    case 1:
                        Resize(current, narrow); stage = 2; break;
                    case 2:
                        Require(geometryResult is { Succeeded: true } && current.Revision == geometryRevision + 1, "Formal narrow geometry did not commit once.");
                        var narrowed = _spatialRuntime.ReadCompositionForProbe(target); Surface(narrowed);
                        Require(narrowed.StoredJson == wideAuthor!.StoredJson && narrowed.Document.DocumentVersion == wideAuthor.Document.DocumentVersion &&
                            Math.Abs(narrowed.WidthDip - narrow) < .01 && narrowed.Viewport.WidthDip >= narrowed.Snapshot.MinimumCrossAxisDip,
                            "Narrowing rewrote content or entered insufficient cross-axis space.");
                        observations.Add(new { name = "narrow-only-geometry-author-unchanged", narrowed });
                        if (narrowVisits++ == 0)
                        {
                            // Observe actual restoration before any edit adopts responsive geometry.
                            Resize(current, wide); stage = 8; break;
                        }
                        press = _spatialRuntime.BeginCompositionForProbe(target);
                        Require(_stateCoordinator.Refresh().Revision == current.Revision &&
                            _stateCoordinator.Refresh().PostersById[target].Poster.ContentLayoutJson == wideAuthor.StoredJson,
                            "Real preview wrote SQLite.");
                        observations.Add(new { name = "native-handler-full-picture-offscreen-preview-zero-write", press.Feedback,
                            actual = _spatialRuntime.ReadCompositionForProbe(target) });
                        Capture("preview"); stage = 3; break;
                    case 3:
                        _spatialRuntime.VerifyCompositionFeedbackForProbe(target);
                        adopted = _spatialRuntime.CompleteCompositionForProbe(target, press!); Surface(adopted);
                        Require(adopted.StoredJson != wideAuthor!.StoredJson, "Manual release did not author the adapted picture.");
                        observations.Add(new { name = "one-release-commits-full-picture", adopted });
                        Capture("adopted"); stage = 4; break;
                    case 4:
                        Resize(current, wide); stage = 5; break;
                    case 5:
                        Require(geometryResult is { Succeeded: true } && current.Revision == geometryRevision + 1, "Formal widening did not commit once.");
                        var widened = _spatialRuntime.ReadCompositionForProbe(target); Surface(widened);
                        Require(widened.StoredJson == adopted!.StoredJson && Math.Abs(widened.WidthDip - wide) < .01 &&
                            widened.Document.Placements.All(p => widened.Snapshot.Items.Any(s => s.ItemId == p.ItemId &&
                                s.Column - s.RegionColumn == p.Column && s.Row - s.RegionRow == p.Row)),
                            "Widening restored discarded pre-adoption coordinates or changed saved intent.");
                        observations.Add(new { name = "wide-retains-adopted-author", widened }); Capture("widened"); stage = 6; break;
                    case 6:
                        long revision = current.Revision;
                        Require(_spatialRuntime.UndoContentEditor(target, false) is null, "Single content Undo failed after geometry change.");
                        var undone = _spatialRuntime.ReadCompositionForProbe(target); Surface(undone);
                        Require(undone.Revision == revision + 1 && ContentLayoutEditing.SameState(wideAuthor!.Document, undone.Document, true) &&
                            Math.Abs(undone.WidthDip - wide) < .01 && _stateCoordinator.Refresh().PostersById[target].Poster.WidthDip == wide,
                            "Single Undo did not restore all old author intent while preserving current outer width.");
                        observations.Add(new { name = "single-undo-restores-author-not-outer-geometry", undone }); Capture("undo"); stage = 7; break;
                    case 7:
                        string expected = Path.Combine(dataRoot, "content-interaction-expected.json");
                        Require(!File.Exists(expected), "Restart evidence already exists.");
                        string fingerprint = StateRequestFingerprint.Compute(new { current.Revision, current.CatalogVersion, current.ControlCenter.Settings,
                            posters = current.Posters.Select(p => new { p.Poster, p.EntityVersion, p.PosterRevision }).ToArray() });
                        File.WriteAllText(expected, JsonSerializer.Serialize(new { fingerprint, current.Revision,
                            posters = current.Posters.Select(p => p.Poster).ToArray() }, new JsonSerializerOptions { WriteIndented = true }));
                        Finish(true, null); break;
                    case 8:
                        Require(geometryResult is { Succeeded: true } && current.Revision == geometryRevision + 1,
                            "Unedited widening did not commit exactly one geometry change.");
                        var uneditedWide = _spatialRuntime.ReadCompositionForProbe(target); Surface(uneditedWide);
                        Require(uneditedWide.StoredJson == wideAuthor!.StoredJson &&
                            ContentLayoutEditing.SameState(wideAuthor.Document, uneditedWide.Document) &&
                            Math.Abs(uneditedWide.WidthDip - wide) < .01 &&
                            uneditedWide.Snapshot.Items.Count == wideAuthor.Snapshot.Items.Count &&
                            wideAuthor.Snapshot.Items.All(original => uneditedWide.Snapshot.Items.Any(actual =>
                                actual.ItemId == original.ItemId && actual.Column == original.Column && actual.Row == original.Row &&
                                actual.ColumnSpan == original.ColumnSpan && actual.RowSpan == original.RowSpan && actual.Bounds == original.Bounds)),
                            "Narrow then wide without editing did not restore every actual logical rectangle and all original intent.");
                        observations.Add(new { name = "unedited-wide-restores-every-logical-rectangle", uneditedWide });
                        Resize(current, narrow); stage = 2; break;
                }
            }
            catch (Exception error) { Finish(false, error.ToString()); }
            finally { ticking = false; }
        }
        timer.Tick += Tick; timer.Start();
    }
}
