using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Windows;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    // Protocol evidence only. The real window, published regions, production handler, command
    // coordinator and history are used, but this method never acquires native mouse capture or
    // synthesizes an OS event. The caller must supply an isolated, ordinary automatic fixture.
    internal async Task<object> RunDirectContentProtocolProbe(string dataRoot)
    {
        var cases = new List<object>();
        var ownedCaptures = new HashSet<Guid>();
        var nativeBasis = inputRouter.Capture;
        string caseName = "preconditions";
        var assertions = new List<string>();
        var inputs = new List<object>();
        void Require(bool pass, string message)
        {
            if (!pass) throw new InvalidOperationException(caseName + ": " + message);
            assertions.Add(message);
        }
        DirectContentProtocolState State()
        {
            var document = ContentLayoutPolicy.Freeze(ContentDocument);
            return new(_stateRevision, _posterEntityVersion, document.DocumentVersion,
                _contentOpenIntentCount, _authoritativePoster?.ContentLayoutJson,
                ContentLayoutPolicy.Serialize(document), document);
        }
        bool Released() => _contentGesture is null && _contentSession.Draft is null && _contentSession.DraftProjection is null &&
            _contentMotionTimer is null && _contentMotionLease is null && _contentLatestMotion is null && !_contentMotionPending;
        void CancelOwnedGesture()
        {
            if (_contentGesture is { } gesture && ownedCaptures.Contains(gesture.Capture.Id))
                CancelContentGesture("direct-protocol-probe-cleanup");
        }
        async Task Case(string name, Action<DirectContentProtocolState> action)
        {
            caseName = name;
            assertions = new(); inputs = new();
            var before = State();
            try
            {
                action(before);
                Require(!IsEditing, "The case completed back in daily mode; direct dragging required no editing mode.");
                Require(Released(), "The owned gesture, preview, timer and motion lease were released.");
                Require(inputRouter.Capture == nativeBasis, "The native router capture was unchanged.");
                if (_contentNotice is not null) Feedback("completed", null);
                string capturePath = Path.Combine(dataRoot, "content-" + name + ".png");
                await CapturePosterXamlForProbeAsync(capturePath);
                var result = new { Name = name, Passed = true, Before = before, After = State(),
                    Assertions = assertions.ToArray(), Inputs = inputs.ToArray(), capturePath, NativeMouse = false, NativeKeyboard = false };
                cases.Add(result);
                log.Write("info", "content.direct_protocol_case", result);
            }
            catch (Exception error)
            {
                CancelOwnedGesture();
                log.Write("error", "content.direct_protocol_case", new { Name = name, Passed = false,
                    Before = before, After = State(), Error = error.ToString(), Assertions = assertions.ToArray(),
                    Inputs = inputs.ToArray(), NativeMouse = false, NativeKeyboard = false });
                throw;
            }
        }
        void Feedback(string stage, string? expected)
        {
            (_window!.Content as FrameworkElement)!.UpdateLayout();
            if (expected is not null)
                Require(_contentNotice?.Contains(expected, StringComparison.Ordinal) == true, "Feedback explains " + expected + ".");
            if (!TestContentFeedbackEnabled)
            {
                Require(_contentFeedbackCallout is null && _contentFeedbackText is null && _footer?.Visibility == Visibility.Collapsed,
                    "Daily use has no allocated feedback callout or visible diagnostic footer.");
                inputs.Add(new { Event = "daily-feedback-hidden-" + stage, Notice = _contentNotice, DiagnosticsEnabled = false });
                return;
            }
            Require(_contentFeedbackCallout is { Visibility: Visibility.Visible, IsHitTestVisible: false } &&
                _contentFeedbackText is { Text.Length: > 0 }, "The explicitly enabled test poster has readable feedback with the center closed.");
            if (stage != "preview") Require(!_contentFeedbackText!.Text.Contains("松手", StringComparison.Ordinal),
                "No obsolete release instruction remains after the operation.");
            Require(Grid.GetRow(_contentFeedbackCallout!) == 3 && _footer!.Visibility == Visibility.Collapsed,
                "Feedback replaces fixed footer chrome and does not cover content.");
            var bounds = _contentFeedbackCallout!.TransformToVisual(_window.Content).TransformBounds(
                new global::Windows.Foundation.Rect(0, 0, _contentFeedbackCallout.ActualWidth, _contentFeedbackCallout.ActualHeight));
            var surface = inputRouter.ReadSurface(inputSession)!;
            foreach (var region in surface.Layout.Regions.Where(r => r.Kind is PosterInputTargetKind.ContentItem or PosterInputTargetKind.ContentSpanHandle or PosterInputTargetKind.ContentViewport))
            {
                double x = region.Bounds.X / _monitor!.ScaleX, y = region.Bounds.Y / _monitor.ScaleY;
                double right = (region.Bounds.X + region.Bounds.Width) / _monitor.ScaleX;
                double bottom = (region.Bounds.Y + region.Bounds.Height) / _monitor.ScaleY;
                Require(bounds.Right <= x + 1 || bounds.Left >= right - 1 || bounds.Bottom <= y + 1 || bounds.Top >= bottom - 1,
                    "Feedback has no overlap with the published " + region.Kind + " region.");
            }
            Require(_contentFeedbackText!.ActualHeight <= _contentFeedbackCallout.ActualHeight &&
                bounds.Height <= FooterHeightDip && bounds.Bottom <= _bounds.Height / _monitor!.ScaleY + 1,
                "The bounded message stays inside the existing footer height.");
            inputs.Add(new { Event = "actual-feedback-" + stage, Notice = _contentNotice,
                Text = _contentFeedbackText.Text, bounds, ModeText = _modeLabel!.Text, NativeMouse = false });
        }
        void Adornments(string id, bool editing)
        {
            var read = ReadContentAdornmentsForProbe();
            double expectedThickness = editing ? 2 : .8;
            int expectedHandles = editing ? 1 : 0;
            // Record before assertions so failed runs retain the actual XAML/native readback.
            inputs.Add(new { Event = "actual-selection-adornments", Readback = read,
                ExpectedThickness = expectedThickness, ExpectedHandles = expectedHandles });
            string evidence = System.Text.Json.JsonSerializer.Serialize(read);
            Require(read.SelectedItemId == id && read.IsEditing == editing && read.SelectedCardPresent,
                "The selected card remains present in the actual visual tree in " + (editing ? "editing" : "daily") + " mode. " + evidence);
            // Fractional XAML values may cross a native single-precision representation;
            // 0.8f widened to double is 0.800000011920929. This sub-micro-DIP tolerance
            // admits that representation only, while still rejecting the 2-DIP highlight.
            Require(read.SelectedBorderThickness is { } actualThickness && double.IsFinite(actualThickness) &&
                Math.Abs(actualThickness - expectedThickness) <= .000001,
                "Actual selected border thickness must equal " + expectedThickness + " DIP within 0.000001 DIP. " + evidence);
            Require(read.VisualResizeHandles == expectedHandles,
                "Actual visual resize-handle count must be " + expectedHandles + ". " + evidence);
            Require(read.PublishedResizeHandles == expectedHandles,
                "Published native resize-handle region count must be " + expectedHandles + ". " + evidence);
            Require(read.PublishedItems > 0 && read.Surface.CanvasMatches && read.Surface.InputMatches && read.Surface.Clipped,
                "Ordinary item hit regions still match the displayed, clipped cards. " + evidence);
            if (!TestContentFeedbackEnabled)
                Require(!read.FeedbackVisualAllocated && !read.FeedbackVisible && !read.FooterVisible,
                    "The daily poster contains no diagnostic footer or callout visuals. " + evidence);
        }
        void Unchanged(DirectContentProtocolState before)
        {
            var after = State();
            Require(after.Revision == before.Revision && after.EntityVersion == before.EntityVersion &&
                after.DocumentVersion == before.DocumentVersion, "No state, entity or document version was written.");
            Require(after.StoredRaw == before.StoredRaw && after.Raw == before.Raw, "Stored and canonical author JSON remained byte-for-byte unchanged.");
            Require(after.OpenIntents == before.OpenIntents, "The non-opening interaction produced no DEMO open intent.");
        }
        void OneCommit(DirectContentProtocolState before)
        {
            var after = State();
            Require(after.Revision == before.Revision + 1 && after.EntityVersion == before.EntityVersion + 1 &&
                after.DocumentVersion == before.DocumentVersion + 1, "Exactly one state, entity and document revision was committed.");
            Require(after.Raw != before.Raw && after.StoredRaw != before.StoredRaw, "The committed author JSON changed.");
            Require(after.OpenIntents == before.OpenIntents, "Editing did not produce a DEMO open intent.");
        }
        PosterInputEventArgs Send(DirectContentProtocolPress press, PosterInputEventKind kind,
            PointerScreenPoint latest, TitleNativeEndReason reason = TitleNativeEndReason.None)
        {
            var surface = inputRouter.ReadSurface(inputSession) ?? throw new InvalidOperationException("The real published surface disappeared.");
            var args = PosterContentInputPolicy.Event(press.Capture, kind, press.Point, latest,
                surface.Layout.Revision, ++press.Sequence, reason);
            long beforeRevision = _stateRevision;
            string? beforeRaw = _authoritativePoster?.ContentLayoutJson;
            Require(HandleContentInput(args), "The event entered the production content handler.");
            inputs.Add(new { Event = kind.ToString(), press.Capture.Id, press.Point, Latest = latest,
                args.Sequence, PublishedRevision = surface.Layout.Revision,
                PressGeneration = press.Capture.Press.Region.ContentSnapshotGeneration,
                BeforeRevision = beforeRevision, AfterRevision = _stateRevision,
                BeforeRaw = beforeRaw, AfterRaw = _authoritativePoster?.ContentLayoutJson,
                args.Handled, OpenIntents = _contentOpenIntentCount, HasContentGesture });
            return args;
        }
        DirectContentProtocolPress Begin(string itemId, PosterInputTargetKind kind = PosterInputTargetKind.ContentItem, bool editingItem = false)
        {
            Require(Released(), "A fresh press began without another content gesture or motion lease.");
            Require(IsEditing == (kind == PosterInputTargetKind.ContentSpanHandle || editingItem),
                kind == PosterInputTargetKind.ContentSpanHandle || editingItem
                    ? "The handle interaction began after the real editing entry point."
                    : "Direct item input began with editing mode off.");
            var surface = inputRouter.ReadSurface(inputSession) ?? throw new InvalidOperationException("No actual input surface.");
            var region = surface.Layout.Regions.Single(r => r.Kind == kind && r.ContentItemId == itemId);
            Require(region.ContentSnapshotGeneration == _contentLayout.Snapshot!.ViewportGeneration, "The press uses the currently presented generation.");
            var capture = new PosterInputCapture(Guid.NewGuid(), new(inputSession, surface.Layout.Revision, surface.Frame, region));
            var press = new DirectContentProtocolPress(capture, new(
                surface.Frame.X + region.Bounds.X + region.Bounds.Width / 2,
                surface.Frame.Y + region.Bounds.Y + region.Bounds.Height / 2));
            ownedCaptures.Add(capture.Id);
            var args = Send(press, PosterInputEventKind.Pressed, press.Point);
            Require(args.Handled && _contentGesture?.Capture == capture && _contentGesture.CanEdit,
                "The first press acquired its editable RAM gesture from the real published hit region.");
            return press;
        }
        ContentLayoutTile[] VisibleTiles()
        {
            var snapshot = _contentLayout.Snapshot ?? throw new InvalidOperationException("No presented snapshot.");
            var viewport = _contentLayout.Viewport ?? throw new InvalidOperationException("No current viewport.");
            return ContentLayoutFlow.ProjectTiles(snapshot, viewport).Where(t =>
                !t.Clip.IsEmpty && Math.Abs(t.Clip.Width - t.Bounds.Width) < .01 &&
                Math.Abs(t.Clip.Height - t.Bounds.Height) < .01).OrderBy(t => t.ReadOrder).ToArray();
        }
        (ContentLayoutTile Source, ContentLayoutTile Destination) Pair()
        {
            var tiles = VisibleTiles();
            // Equal actual dimensions make the displacement a whole number of grid pitches. The
            // probe reads the production geometry; it neither reimplements placement nor guesses it.
            foreach (var source in tiles)
                foreach (var destination in tiles.Where(t => t.ReadOrder > source.ReadOrder))
                    if (Math.Abs(source.Bounds.Width - destination.Bounds.Width) < .01 &&
                        Math.Abs(source.Bounds.Height - destination.Bounds.Height) < .01)
                        return (source, destination);
            throw new InvalidOperationException("The isolated fixture needs two complete visible equal-sized cards in different read positions.");
        }
        PointerScreenPoint Destination(DirectContentProtocolPress press, ContentLayoutTile source, ContentLayoutTile destination) =>
            new(press.Point.X + (int)Math.Round((destination.Bounds.X - source.Bounds.X) * _monitor!.ScaleX),
                press.Point.Y + (int)Math.Round((destination.Bounds.Y - source.Bounds.Y) * _monitor!.ScaleY));
        void Preview(DirectContentProtocolPress press, PointerScreenPoint latest, bool changed)
        {
            Send(press, PosterInputEventKind.GesturePreview, latest);
            Require(_contentMotionPending && _contentMotionTimer is not null && _contentMotionLease is not null,
                "The production handler retained the latest motion under its real timer lease.");
            // One synchronous batch intentionally bypasses wall-clock timer scheduling only.
            // This is the same flush used by the production release; no Proposal is injected.
            FlushContentMotion(false);
            var gesture = _contentGesture!;
            inputs.Add(new { Event = "production-motion-flush", Revision = _stateRevision,
                StoredRaw = _authoritativePoster?.ContentLayoutJson, gesture.LastEdit, gesture.LastMapping,
                Proposal = _contentSession.Draft is null ? null : ContentLayoutPolicy.Serialize(_contentSession.Draft) });
            Require(_contentSession.Draft is not null, "The real preview produced an accepted proposal.");
            Require(ContentLayoutEditing.SameState(gesture.Document, _contentSession.Draft!) != changed,
                changed ? "The preview exercises a real author change." : "The return-origin preview is an exact author no-op.");
            Feedback("preview", changed ? null : "未改变");
        }
        void Click(string itemId, bool editing = false)
        {
            var press = Begin(itemId, editingItem: editing);
            Send(press, PosterInputEventKind.Invoked, press.Point);
            Require(Released(), "The completed click released its RAM owner.");
        }
        void History(bool redo, ContentLayoutDocument expected)
        {
            var before = State();
            string? error = UndoContent(redo);
            Require(error is null, (redo ? "Redo" : "Undo") + " succeeded through the real content history and state command.");
            OneCommit(before);
            Require(ContentLayoutEditing.SameState(expected, ContentDocument, true),
                (redo ? "Redo" : "Undo") + " restored every author field while rebasing only versions.");
            inputs.Add(new { Event = redo ? "formal-redo" : "formal-undo", Before = before, After = State() });
        }

        Require(!_disposed && !_contentSession.IsSuspended && _monitor is not null && _inputIsland is not null &&
            !_geometry.IsCollapsed && !_geometry.IsLocked && _transitionLayout is null && !spatial.IsSpatialBusy,
            "The actual expanded unlocked poster is idle and ready.");
        Require(PosterId is { } posterId && !spatial.HasCenterDraft(posterId) && nativeBasis is null && Released(),
            "There is no existing center draft, content gesture or native capture.");
        RefreshContentSurface();
        var initial = State();
        Require(!IsEditing && !_contentReadOnly && _contentLayout.Snapshot is { SpaceStatus: ContentLayoutSpaceStatus.Normal } &&
            !initial.Document.LoopScroll && initial.Document.Mode == ContentLayoutMode.Automatic,
            "The finite ordinary automatic fixture is editable without organize mode.");
        Require(initial.Document.Regions.Count == 1 && initial.Document.ReservedSpaces.All(r => r.Kind != ContentReservedKind.Explicit) &&
            initial.Document.Placements.All(p => !p.FixedPosition && p.PinnedIndex is null),
            "The isolated baseline contains no legacy hard layout, managed keep-out area or pre-existing pin.");
        _ = Pair();
        ContentLayoutDocument? dragged = null;
        try
        {
            await Case("single-click-selects-without-open", before =>
            {
                string id = Pair().Source.ItemId;
                Require(SelectContentItem(null) is null, "Selection pairing was reset through the formal selection operation.");
                Click(id);
                Require(_selectedContentItem == id, "The clicked item became the selected item.");
                Adornments(id, false);
                Unchanged(before);
            });
            await Case("selection-adornments-only-in-editing", before =>
            {
                string id = Pair().Source.ItemId;
                Require(SelectContentItem(null) is null, "Selection pairing was reset before the everyday click.");
                Click(id);
                Adornments(id, false);
                Require(SetEditing(true), "The production editing entry point entered editing mode.");
                Adornments(id, true);
                Require(SetEditing(false), "The production editing entry point returned to daily mode.");
                Adornments(id, false);
                Unchanged(before);
            });
            await Case("double-click-opens-once-third-click-does-not", before =>
            {
                string id = Pair().Source.ItemId;
                Require(SelectContentItem(id) is null, "A fresh double-click pair was started through formal selection.");
                long started = Environment.TickCount64;
                Click(id);
                Require(_contentOpenIntentCount == before.OpenIntents, "The first click did not open.");
                Click(id);
                long completed = Environment.TickCount64;
                Require(_contentOpenIntentCount == before.OpenIntents + 1, "The second real-handler click produced exactly one DEMO open intent.");
                Feedback("opened", "演示");
                Click(id);
                Require(_contentOpenIntentCount == before.OpenIntents + 1, "A third click did not reuse the consumed double-click pair.");
                Require(_stateRevision == before.Revision && _authoritativePoster?.ContentLayoutJson == before.StoredRaw &&
                    ContentLayoutPolicy.Serialize(ContentDocument) == before.Raw, "Click activation wrote no layout state.");
                inputs.Add(new { Event = "system-double-click-policy", Started = started, Completed = completed,
                    Settings = NativeContentInteraction.DoubleClickSettings });
            });
            await Case("span-handle-click-interrupts-card-double-click-pair", before =>
            {
                Require(SetEditing(true), "The production editing entry point enabled resize handles.");
                string id = Pair().Source.ItemId;
                Require(SelectContentItem(id) is null, "A fresh card click pair was started.");
                Click(id, editing: true);
                var handle = Begin(id, PosterInputTargetKind.ContentSpanHandle);
                Send(handle, PosterInputEventKind.Invoked, handle.Point);
                Click(id, editing: true);
                Require(SetEditing(false), "The handle case returned through the production daily-mode entry point.");
                Adornments(id, false);
                Unchanged(before);
            });
            await Case("first-auto-drag-pins-only-selected-item", before =>
            {
                var pair = Pair();
                var press = Begin(pair.Source.ItemId);
                var end = Destination(press, pair.Source, pair.Destination);
                Preview(press, end, true);
                var requested = _contentGesture!.LastEdit!;
                Require(requested.Kind == ContentLayoutEditKind.Move && requested.TargetReadOrder.HasValue &&
                    requested.Column is null && requested.Row is null, "Automatic input mapped to a final read position, not hard coordinates.");
                Send(press, PosterInputEventKind.GestureCompleted, end);
                OneCommit(before);
                var item = ContentDocument.Placements.Single(p => p.ItemId == pair.Source.ItemId);
                var original = before.Document.Placements.Single(p => p.ItemId == item.ItemId);
                Require(ContentDocument.Mode == ContentLayoutMode.Automatic && !item.FixedPosition &&
                    item.PinnedIndex == requested.TargetReadOrder + 1, "The first direct drag created the selected item's final read-order pin without entering manual mode.");
                Require((item with { Version = original.Version, PinnedIndex = original.PinnedIndex }) == original &&
                    before.Document.Placements.Where(p => p.ItemId != item.ItemId).All(p => ContentDocument.Placements.Single(n => n.ItemId == p.ItemId) == p),
                    "Only the selected pin and its version changed; every other placement field/member was retained.");
                dragged = ContentLayoutPolicy.Freeze(ContentDocument);
            });
            await Case("auto-drag-undo-redo-and-restore-baseline", _ =>
            {
                History(false, initial.Document);
                History(true, dragged!);
                History(false, initial.Document);
            });
            await Case("pin-icon-right-click-releases-once-and-undoes", before =>
            {
                inputs.Add(new { Event = "native-sparse-region", Readback = ReadSparseInputForProbe() });
                string id = VisibleTiles().First().ItemId;
                Require(EditContent(new(ContentLayoutEditKind.Pinned, id) { Enabled = true }) is null, "Formal order pin created for the right-click case.");
                var pinned = State();
                var tile = VisibleTiles().First(t => t.ItemId == id);
                var icon = ContentPinPresentationPolicy.VisibleBounds(tile);
                Require(!icon.IsEmpty, "The actual pin icon has a visible clipped box.");
                var point = new PointerScreenPoint(_bounds.X + (int)Math.Round((ContentLeftDip + icon.X + icon.Width / 2) * _monitor!.ScaleX),
                    _bounds.Y + (int)Math.Round((ContentTopDip + icon.Y + icon.Height / 2) * _monitor.ScaleY));
                var hit = inputRouter.Resolve(point.X, point.Y, [inputSession])!;
                var request = inputRouter.ResolveContext(hit, point, [inputSession], point)!;
                var args = new PosterContentContextEventArgs(request);
                OnContentContextRequested(_inputIsland, args);
                Require(args.Handled && ContentDocument.Placements.Single(p => p.ItemId == id).PinnedIndex is null,
                    "The real context handler consumed the pin click and released its order pin.");
                OneCommit(pinned);
                Require(!DesktopMenuInteraction.IsActive, "Pin release did not open the item menu.");
                OnContentContextRequested(_inputIsland, new(request));
                Require(State().Revision == pinned.Revision + 1, "A stale right-click witness cannot release or write twice.");
                History(false, pinned.Document);
                History(false, before.Document);
                inputs.Add(new { Event = "native-sparse-region-after-pin-history", Readback = ReadSparseInputForProbe() });
            });
            await Case("return-origin-does-not-write", before =>
            {
                var pair = Pair(); var press = Begin(pair.Source.ItemId);
                Preview(press, Destination(press, pair.Source, pair.Destination), true);
                Preview(press, press.Point, false);
                Send(press, PosterInputEventKind.GestureCompleted, press.Point);
                Unchanged(before);
            });
            await Case("invalid-release-discards-last-valid-preview", before =>
            {
                var pair = Pair(); var press = Begin(pair.Source.ItemId);
                Preview(press, Destination(press, pair.Source, pair.Destination), true);
                // Protocol-only: this is not an OS click outside our window. The release must replace
                // the previous valid proposal, not accidentally commit that cached proposal.
                var invalid = press.Point with { X = _bounds.X - 32 };
                Send(press, PosterInputEventKind.GestureCompleted, invalid);
                Require(_contentNotice is not null, "The invalid final release reported a refusal.");
                Unchanged(before);
            });
            await Case("cancel-and-late-release-do-not-write-or-open", before =>
            {
                var pair = Pair(); var press = Begin(pair.Source.ItemId);
                var end = Destination(press, pair.Source, pair.Destination);
                Preview(press, end, true);
                Send(press, PosterInputEventKind.GestureCancelled, end, TitleNativeEndReason.CancelMode);
                Feedback("cancelled", "取消");
                Require(Released(), "Cancellation immediately discarded its proposal and released all owned leases.");
                Send(press, PosterInputEventKind.GestureCompleted, end);
                Send(press, PosterInputEventKind.Invoked, end);
                Unchanged(before);
            });
            await Case("center-selection-cannot-cancel-live-content-owner", before =>
            {
                var pair = Pair(); var press = Begin(pair.Source.ItemId);
                Preview(press, Destination(press, pair.Source, pair.Destination), true);
                var owner = _contentGesture;
                var proposal = _contentSession.Draft;
                string? error = SelectContentItem(pair.Destination.ItemId);
                Require(error is not null && ReferenceEquals(owner, _contentGesture) &&
                    ReferenceEquals(proposal, _contentSession.Draft) && _selectedContentItem == pair.Source.ItemId,
                    "Central selection was refused without cancelling, replacing or selecting through the live content owner.");
                inputs.Add(new { Event = "formal-center-selection-refused", Error = error, Revision = _stateRevision });
                Send(press, PosterInputEventKind.GestureCancelled, press.Point, TitleNativeEndReason.CancelMode);
                Unchanged(before);
            });
            await Case("enter-manual-captures-complete-presented-picture", before =>
            {
                var picture = _contentLayout.Snapshot!;
                Require(EditContent(new(ContentLayoutEditKind.Mode) { Mode = ContentLayoutMode.Manual }) is null,
                    "The formal mode edit entered manual layout.");
                OneCommit(before);
                Require(ContentDocument.Mode == ContentLayoutMode.Manual && ContentDocument.SavedAutomaticMode == before.Document.Mode &&
                    ContentDocument.Placements.Count == picture.Items.Count, "Manual entry retained the complete member set and automatic return mode.");
                foreach (var item in ContentDocument.Placements)
                {
                    var slot = picture.Items.Single(p => p.ItemId == item.ItemId);
                    var original = before.Document.Placements.Single(p => p.ItemId == item.ItemId);
                    Require(item.Column == slot.Column - slot.RegionColumn && item.Row == slot.Row - slot.RegionRow &&
                        item.ColumnSpan == slot.ColumnSpan && item.RowSpan == slot.RowSpan && item.Order == original.Order,
                        "Manual entry captured the presented position/span and preserved author order for " + item.ItemId + ".");
                    Require(item.SavedAutomaticState == new ContentAutomaticPlacementState(original.Order, original.ColumnSpan,
                        original.RowSpan, original.ManualSize, original.PinnedIndex, original.Column, original.Row, original.FixedPosition)
                        { AutomaticSizeBasis = original.AutomaticSizeBasis }, "Manual entry retained original automatic author intent for " + item.ItemId + ".");
                }
                inputs.Add(new { Event = "complete-picture", picture.ViewportGeneration, picture.Items,
                    IncludesOffscreen = picture.Items.Any(p => p.Clip.IsEmpty) });
            });
            await Case("manual-direct-move-and-history", before =>
            {
                var source = VisibleTiles().First(); var press = Begin(source.ItemId);
                var gesture = _contentGesture!;
                bool vertical = gesture.Snapshot.FlowAxis == ContentFlowAxis.Vertical;
                double pitch = gesture.Snapshot.CellDip + gesture.Snapshot.GapDip;
                var end = vertical
                    ? press.Point with { Y = press.Point.Y + (int)Math.Round(pitch * gesture.ScaleY) }
                    : press.Point with { X = press.Point.X + (int)Math.Round(pitch * gesture.ScaleX) };
                int expectedColumn = gesture.Slot.Column - gesture.Slot.RegionColumn + (vertical ? 0 : 1);
                int expectedRow = gesture.Slot.Row - gesture.Slot.RegionRow + (vertical ? 1 : 0);
                Preview(press, end, true);
                Send(press, PosterInputEventKind.GestureCompleted, end);
                OneCommit(before);
                var moved = ContentLayoutPolicy.Freeze(ContentDocument);
                var item = moved.Placements.Single(p => p.ItemId == source.ItemId);
                Require(moved.Mode == ContentLayoutMode.Manual && item.Column == expectedColumn && item.Row == expectedRow,
                    "The manual drag used the presented source coordinate and one real main-axis grid pitch.");
                Require(moved.Placements.All(p => p.Column.HasValue && p.Row.HasValue &&
                    p.SavedAutomaticState == before.Document.Placements.Single(b => b.ItemId == p.ItemId).SavedAutomaticState),
                    "The entire manual picture remained authored and automatic return states survived local movement/pushing.");
                History(false, before.Document); History(true, moved);
            });
            await Case("selected-span-handle-in-editing-and-history", before =>
            {
                Require(SetEditing(true), "The production editing entry point enabled span resizing.");
                bool vertical = _contentLayout.Snapshot!.FlowAxis == ContentFlowAxis.Vertical;
                var source = VisibleTiles().First(t =>
                {
                    var slot = _contentLayout.Snapshot.Items.Single(p => p.ItemId == t.ItemId);
                    return (vertical ? slot.ColumnSpan : slot.RowSpan) > 2;
                });
                Require(SelectContentItem(source.ItemId) is null, "The card was selected through the formal selection port.");
                var press = Begin(source.ItemId, PosterInputTargetKind.ContentSpanHandle);
                var gesture = _contentGesture!;
                double pitch = gesture.Snapshot.CellDip + gesture.Snapshot.GapDip;
                int columns = gesture.Slot.ColumnSpan - (vertical ? 1 : 0);
                int rows = gesture.Slot.RowSpan - (vertical ? 0 : 1);
                var end = vertical
                    ? press.Point with { X = press.Point.X - (int)Math.Round(pitch * gesture.ScaleX) }
                    : press.Point with { Y = press.Point.Y - (int)Math.Round(pitch * gesture.ScaleY) };
                Preview(press, end, true);
                Send(press, PosterInputEventKind.GestureCompleted, end);
                OneCommit(before);
                var resized = ContentLayoutPolicy.Freeze(ContentDocument);
                var item = resized.Placements.Single(p => p.ItemId == source.ItemId);
                Require(item.ColumnSpan == columns && item.RowSpan == rows && item.ManualSize,
                    "The real selected span handle changed the intended span by one cross-axis pitch.");
                Require(resized.Placements.All(p => p.SavedAutomaticState == before.Document.Placements.Single(b => b.ItemId == p.ItemId).SavedAutomaticState),
                    "Manual resize preserved every original automatic return state.");
                History(false, before.Document); History(true, resized);
                Require(SetEditing(false), "The resize case exited editing through the production entry point.");
                Adornments(source.ItemId, false);
            });
            await Case("exit-manual-restores-auto-author-and-history", before =>
            {
                Require(EditContent(new(ContentLayoutEditKind.Mode) { Mode = ContentLayoutMode.Automatic }) is null,
                    "The formal exit operation returned to saved automatic intent.");
                OneCommit(before);
                var returned = ContentLayoutPolicy.Freeze(ContentDocument);
                Require(ContentLayoutEditing.SameState(initial.Document, returned, true),
                    "Exit restored the original automatic order, pins, coordinates and size participation without retaining manual edits.");
                History(false, before.Document); History(true, returned);
            });
            return new { PosterId, Passed = true, NativeMouse = false, NativeKeyboard = false, NativeWheel = false,
                FeedbackDiagnosticsEnabled = TestContentFeedbackEnabled,
                Scope = "Actual WinUI production-handler protocol; OS routing and UI C acceptance are separate gates",
                Before = initial, After = State(), Cases = cases.ToArray(), AllOwnedLeasesReleased = Released() };
        }
        finally
        {
            CancelOwnedGesture();
            if (IsEditing) SetEditing(false);
            // Clear only this probe's transient selection/open-pair state through the normal port.
            if (!HasContentGesture) SelectContentItem(null);
            log.Write("info", "content.direct_protocol_cleanup", new { PosterId, Case = caseName,
                OwnedCaptures = ownedCaptures.Count, Released = Released(), NativeCaptureUnchanged = inputRouter.Capture == nativeBasis,
                NativeMouse = false, NativeKeyboard = false });
        }
    }

    private sealed record DirectContentProtocolState(long Revision, long EntityVersion, long DocumentVersion,
        long OpenIntents, string? StoredRaw, string Raw, ContentLayoutDocument Document);
    private sealed class DirectContentProtocolPress(PosterInputCapture capture, PointerScreenPoint point)
    {
        public PosterInputCapture Capture { get; } = capture;
        public PointerScreenPoint Point { get; } = point;
        public long Sequence { get; set; }
    }
}

internal sealed partial class PosterSpatialRuntime
{
    internal Task<object> RunDirectContentProtocolProbe(string id, string dataRoot)
    {
        if (_suspended || IsSpatialBusy || HasCenterDraft(id) || !_windows.TryGetValue(id, out var window))
            throw new InvalidOperationException("The isolated content protocol poster is unavailable or busy.");
        return window.RunDirectContentProtocolProbe(dataRoot);
    }
}
