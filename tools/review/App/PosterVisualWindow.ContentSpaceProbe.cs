using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Xaml;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    // Synthetic topology supplied to the real recovery owner; never changes OS display settings.
    // Actual HWND/XAML/input publications are read back. This is not native pointer certification.
    private async Task<object> RunContentSpaceForProbeAsync(string dataRoot)
    {
        var original = ContentDocument;
        var monitor = _monitor!;
        var observations = new List<object>(); int checks = 0;
        var nativeCapture = inputRouter.Capture;
        long generation = _appliedTopologyGeneration;
        int environmentSamples = 0;
        void Require(bool value, string message)
        { checks++; if (!value) throw new InvalidOperationException("passive-space: " + message); }
        void RecoverMonitor(DisplayMonitorSnapshot target)
        {
            // Do not fabricate a future OS generation: normal state publication may immediately
            // reconcile against the real topology service. Changed work-area/DPI facts are the
            // test environment; monotonic generation rejection has independent pure tests.
            var topology = new DisplayTopologySnapshot("space-probe-" + ++environmentSamples, [target], generation, DateTimeOffset.UtcNow);
            spatial.SetEffectiveTopology(topology);
            SuspendInput();
            Require(RecoverContentForSmoke(topology), "Owner recovery failed.");
            Require(!HasContentGesture && _contentMotionTimer is null && _contentSession.Draft is null, "Old draft/capture survived recovery.");
            Require(inputRouter.Capture == nativeCapture, "Managed probe acquired native capture.");
            var read = ReadBrowseForProbe();
            Require(read.CanvasMatches && read.InputMatches && read.Clipped, "Actual XAML and hit regions disagree.");
            observations.Add(new { target.WorkArea, target.DpiX, target.DpiY, read,
                decision = _contentLayout.Space.Published, candidate = _contentLayout.Candidate });
        }
        try
        {
            bool vertical = original.FlowAxis == ContentFlowAxis.Vertical;
            var first = original.Placements[0];
            double pitch = _contentLayout.Snapshot!.CellDip + _contentLayout.Snapshot.GapDip;
            int span = Math.Min(12, (int)Math.Floor(((vertical ? _contentLayout.Viewport!.WidthDip : _contentLayout.Viewport!.HeightDip) + _contentLayout.Snapshot.GapDip) / pitch));
            Require(span >= 8, "Large-item fixture unavailable.");
            Require(EditContent(new(ContentLayoutEditKind.Resize, first.ItemId) {
                ColumnSpan = vertical ? span : first.ColumnSpan, RowSpan = vertical ? first.RowSpan : span }) is null, "Fixture resize failed.");
            foreach (var mode in new[] { ContentLayoutMode.Automatic, ContentLayoutMode.Manual })
            {
                Require(EditContent(new(ContentLayoutEditKind.Mode) { Mode = mode }) is null, "Fixture mode failed.");
                ScrollContentForSmoke(0, 0); ScrollItems(120);
                var published = _contentLayout.Space.Published;
                Require(published is { IsReadOnly: false }, "Initial normal publication missing.");
                var viewport = _contentLayout.Viewport!;
                double chrome = vertical ? _bounds.Width / monitor.ScaleX - viewport.WidthDip : _bounds.Height / monitor.ScaleY - viewport.HeightDip;
                double minimum = _contentLayout.Snapshot!.MinimumCrossAxisDip;
                var sizes = _contentLayout.Snapshot.Items.ToDictionary(i => i.ItemId, i => (i.Bounds.Width, i.Bounds.Height));
                long revision = _stateRevision; string? raw = _authoritativePoster!.ContentLayoutJson;
                // Prepare-only is not evidence. No native mutation is requested for this candidate.
                var prepared = PrepareSpatialFrame(_bounds);
                Require(prepared is not null && ReferenceEquals(published, _contentLayout.Space.Published), "Prepare advanced publication.");
                var surface = inputRouter.ReadSurface(inputSession)!;
                var region = surface.Layout.Regions.First(r => r.Kind == PosterInputTargetKind.ContentItem);
                var press = new PointerScreenPoint(surface.Frame.X + region.Bounds.X + region.Bounds.Width / 2,
                    surface.Frame.Y + region.Bounds.Y + region.Bounds.Height / 2);
                var capture = new PosterInputCapture(Guid.NewGuid(), new(inputSession, surface.Layout.Revision, surface.Frame, region));
                PosterInputEventArgs Input(PosterInputEventKind kind, PointerScreenPoint point, long sequence) =>
                    PosterContentInputPolicy.Event(capture, kind, press, point, _inputIsland!.AppliedLayoutRevision, sequence);
                Require(HandleContentInput(Input(PosterInputEventKind.Pressed, press, 1)) && HasContentGesture, "Initial held item was not acquired.");
                HandleContentInput(Input(PosterInputEventKind.GesturePreview, new(press.X + 9, press.Y + 9), 2));
                // Display work area, not an active resize. Cross-axis falls one physical pixel short.
                int crossPixels = (int)Math.Ceiling((minimum + chrome) * (vertical ? monitor.ScaleX : monitor.ScaleY)) - 1;
                var small = monitor with { WorkArea = vertical ? monitor.WorkArea with { Width = crossPixels }
                    : monitor.WorkArea with { Height = crossPixels } };
                RecoverMonitor(small);
                Require(IsContentSpaceRestricted && _contentLayout.Space.Published?.IsRestricted == true && !ReadContentEditor().CanEdit,
                    "Physical shortage did not publish finite read-only overflow.");
                Require(_contentSpaceNavigation?.Visibility == Visibility.Visible &&
                    inputRouter.ReadSurface(inputSession)!.Layout.Regions.Count(r => r.Kind == PosterInputTargetKind.ContentBrowseNavigation) == 4,
                    "Restricted navigation is not visible or clickable.");
                Require(_contentLayout.Snapshot!.Items.All(i => sizes[i.ItemId] == (i.Bounds.Width, i.Bounds.Height)), "Item sizes changed.");
                HandleContentInput(Input(PosterInputEventKind.GestureCompleted, new(press.X + 90, press.Y + 90), 3));
                Require(!HasContentGesture && _stateRevision == revision && _authoritativePoster.ContentLayoutJson == raw, "Late release persisted an old draft.");
                Require(SetContentLoopScroll(false) is not null && EditContent(new(ContentLayoutEditKind.Mode) { Mode = ContentLayoutMode.Automatic }) is not null,
                    "Restricted layout allowed a write.");
                // Use the exact native button ingress, not a test setter, to reach both finite edges.
                foreach (int direction in new[] { 0, 1, 2, 3 })
                {
                    var current = inputRouter.ReadSurface(inputSession)!;
                    OnInput(_inputIsland, new(PosterInputEventKind.Invoked, PosterInputTargetKind.ContentBrowseNavigation,
                        direction, current.Frame, 0, 0, current.Layout.Revision) { Session = inputSession });
                    var read = ReadBrowseForProbe();
                    Require(read.CanvasMatches && read.InputMatches && read.Clipped && read.Tiles.All(t => t.LoopIndex == 0), "Navigation used stale/cyclic tiles.");
                }
                ScrollItems(-120000, true); ScrollItems(-120000);
                Require(_contentLayout.Snapshot.ScrollXDip == Math.Max(0, _contentLayout.Snapshot.ExtentWidthDip - _contentLayout.Viewport!.WidthDip) &&
                    _contentLayout.Snapshot.ScrollYDip == Math.Max(0, _contentLayout.Snapshot.ExtentHeightDip - _contentLayout.Viewport.HeightDip), "Two-axis tail is unreachable.");
                // Selecting/opening still works at a cross-axis offset; dragging must not write.
                var tail = inputRouter.ReadSurface(inputSession)!;
                var tailRegion = tail.Layout.Regions.First(r => r.Kind == PosterInputTargetKind.ContentItem);
                Require(SelectContentItem(tailRegion.ContentItemId) is null && ActivateSelectedContent(false) is null, "Read-only content cannot be opened.");
                ReleaseContentActivationKey();
                Require(_stateRevision == revision && _authoritativePoster.ContentLayoutJson == raw && ContentDocument.LoopScroll, "Browsing changed stored intent.");
                if (mode == ContentLayoutMode.Automatic)
                    await CapturePosterXamlForProbeAsync(Path.Combine(dataRoot, "content-browse-" + (vertical ? "vertical" : "horizontal") + "-space-restricted.png"));
                // A further per-axis DPI change must retain DIP card size and the restricted
                // capability; these are provider facts, not an OS DPI or raster-quality claim.
                RecoverMonitor(small with { DpiX = 120, DpiY = 144 });
                Require(IsContentSpaceRestricted && _contentLayout.Snapshot!.Items.All(i => sizes[i.ItemId] == (i.Bounds.Width, i.Bounds.Height)),
                    "A second DPI observation scaled Items or lost restriction.");
                RecoverMonitor(small);
                var anchor = ContentLayoutFlow.CaptureAnchor(_contentLayout.Snapshot, _contentLayout.Viewport!, _contentLayout.BrowseMode);
                if (mode == ContentLayoutMode.Automatic)
                {
                    // Explicit isolated external-source fixture change via the real authority.
                    // This is not a user layout edit, nor third-batch fake-source certification.
                    var before = ContentDocument;
                    var desired = RebaseContentIntent(before, before with { Placements = before.Placements.Take(before.Placements.Count - 1).ToArray() });
                    var result = stateCommands.Execute(new ContentLayoutCommand("space-fixture-" + Guid.NewGuid().ToString("N"), StateCommandSource.Poster,
                        _stateRevision, before.PosterId, StateReadSet.ForPoster(before.PosterId, _posterEntityVersion),
                        before.DocumentVersion, before.ItemSetVersion, desired, true));
                    Require(result.Succeeded, "Latest-members fixture command rejected: " + result.Error);
                    revision = result.CurrentRevision;
                    raw = result.Projection!.PostersById[before.PosterId].Poster.ContentLayoutJson;
                    // The external member commit is an earlier completed event. Its finite
                    // boundary may already require minimal clamping; restore the CURRENT
                    // identity/internal point, not the deleted tail's old physical coordinate.
                    anchor = ContentLayoutFlow.CaptureAnchor(_contentLayout.Snapshot!, _contentLayout.Viewport!, _contentLayout.BrowseMode);
                }
                RecoverMonitor(monitor);
                Require(!IsContentSpaceRestricted && ReadContentEditor().CanEdit && _contentLayout.Space.Published?.IsRestricted == false,
                    "Recovered physical environment did not restore editing.");
                var offsets = ContentLayoutFlow.RestoreAnchor(_contentLayout.Snapshot!, _contentLayout.Viewport!, anchor);
                Require(Math.Abs(offsets.ScrollXDip - _contentScrollX) < .001 && Math.Abs(offsets.ScrollYDip - _itemScrollOffsetDip) < .001,
                    "Recovery lost the identity/internal browse anchor.");
                Require(_stateRevision == revision && _authoritativePoster.ContentLayoutJson == raw && ContentLayoutFlow.IsLooping(_contentLayout.Snapshot!, _contentLayout.Viewport!),
                    "Recovery overwrote author intent or lost loop eligibility.");
                Require(_contentLayout.Snapshot!.ItemSetVersion == ContentDocument.ItemSetVersion && _contentLayout.Snapshot.Items.Count == ContentDocument.Placements.Count,
                    "Recovery replayed an old member set.");
                Require(SelectContentItem(_contentLayout.Snapshot.Items.First(i => !i.Clip.IsEmpty).ItemId) is null &&
                    ControlCenterWindow.VerifyContentSpaceDraftForProbe(ReadContentEditor()), "An old center draft became valid again.");
                if (mode == ContentLayoutMode.Automatic)
                    await CapturePosterXamlForProbeAsync(Path.Combine(dataRoot, "content-browse-" + (vertical ? "vertical" : "horizontal") + "-space-recovered.png"));
            }
            return new { passed = true, checks, observations, simulatedTopology = true, nativeMouse = false };
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(dataRoot, "content-space-failure.json"),
                System.Text.Json.JsonSerializer.Serialize(new { error = error.ToString(), checks, observations }));
            throw;
        }
        finally
        {
            RecoverMonitor(monitor);
            if (ContentDocument.Mode == ContentLayoutMode.Manual)
                Require(EditContent(new(ContentLayoutEditKind.Mode) { Mode = ContentLayoutMode.Automatic }) is null,
                    "Formal exit from manual fixture failed.");
            var now = ContentDocument;
            string? restoreError = CommitContentDocument(now, RebaseContentIntent(now, original), _posterEntityVersion, _stateRevision);
            Require(restoreError is null, "Formal fixture restore failed: " + restoreError);
        }
    }
}
