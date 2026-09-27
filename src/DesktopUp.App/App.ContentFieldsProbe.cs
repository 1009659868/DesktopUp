using System.Globalization;
using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Dispatching;

namespace DesktopUp;

public partial class App
{
    // Exclusive isolated-profile startup guard belongs to App.xaml.cs. Use the ordinary 2-poster
    // SessionFixture: expanded/unlocked A and expanded/locked B. Never run against a daily profile.
    private void RunContentFieldsProbe(string dataRoot)
    {
        var timer = _dispatcher!.CreateTimer(); timer.Interval = TimeSpan.FromMilliseconds(100);
        var lease = _ledger!.Acquire("fixture.content-fields", "isolated-real-control-command-path");
        var observations = new List<object>(); var captures = new List<string>();
        var captureCases = new[] { ("wide-span", 980, 1040, false, "span"), ("narrow-span", 680, 1040, true, "span"),
            ("wide-position", 980, 1040, false, "position"), ("narrow-position", 680, 1040, true, "position") };
        long deadline = _autoClock.ElapsedMilliseconds + 80000, readyAfter = 0, revision = -1, documentVersion = -1, entityVersion = -1;
        int step = 0, captureIndex = 0, recordedStep = -1, checks = 0, manualRow = 0;
        bool ticking = false, stopped = false; Task? capture = null;
        string a = "", b = "", item = "";
        string? unchangedContent = null, savedContent = null, presentationJson = null;
        PosterDefinition? initialB = null;
        ContentCenterProposalWakeTicket? wakeTicket = null;
        long wakeGeneration = -1;
        void Require(bool value, string message) { checks++; if (!value) throw new InvalidOperationException(message); }
        void Stop()
        {
            if (stopped) return;
            stopped = true; timer.Stop(); timer.Tick -= Tick; lease.Dispose(); _stopAutoSmoke = null;
        }
        _stopAutoSmoke = Stop;
        void Finish(bool passed, string? error)
        {
            Stop();
            _log!.Write(passed ? "info" : "error", "content.fields_observed", new
            {
                passed, error, step, checks, revision, documentVersion, observations, captures,
                inputDriver = "actual-TextBox-ComboBox-properties-and-shared-production-click-handlers",
                cancellationDriver = "shared-content-cancel-handler-not-routed-native-Escape",
                staleBasisDriver = "formal-isolated-repository-Rename-and-ContentLayoutCommand",
                nativeMouse = false, nativeKeyboard = false, nativeWheel = false,
                desktopGestureCancellationTested = false, processRestartTested = false,
                desktopPixelVisualCertified = false, captureIncludesSystemBackdrop = false,
                cleanupEvidence = "application.stopped.activeResources-after-ExitApplication",
                fixtureLeaseReleased = !_ledger!.ActiveResources.Any(value => value.Contains("fixture.content-fields", StringComparison.Ordinal)),
                uiCProductAccepted = false, milestoneAccepted = false
            });
            ExitApplication();
        }
        void Next(int next, int delay = 400)
        { step = next; recordedStep = -1; readyAfter = _autoClock.ElapsedMilliseconds + delay; }
        ContentLayoutDocument Stored(DesktopUpProjection current, string id)
        {
            Require(ContentLayoutPolicy.TryRead(current.PostersById[id].Poster.ContentLayoutJson, id, out var document, out var error), error ?? "Stored document is unreadable.");
            return document!;
        }
        ContentLayoutEditorView Actual(string id) => _spatialRuntime!.ReadContentEditor(id) ?? throw new InvalidOperationException("Poster content is unavailable.");
        void UnchangedB(DesktopUpProjection current)
        {
            Require(current.PostersById[b].Poster == initialB, "A's operation modified locked B.");
            Require(current.PostersById[a].Poster.LayoutParametersJson == presentationJson, "Content fields or a conflicting spacing entry changed presentation parameters.");
        }
        void Record(string name, DesktopUpProjection current, ControlCenterWindow? center, string section = "span")
        {
            observations.Add(new { name, step, current.Revision,
                controls = center?.ReadContentFieldsForProbe(section),
                centerOwnsA = center?.HasPosterDraft(a), centerOwnsB = center?.HasPosterDraft(b),
                a = current.PostersById.TryGetValue(a, out var target) ? target.Poster : null,
                aEntityVersion = target?.EntityVersion,
                bUnchanged = current.PostersById.TryGetValue(b, out var other) && other.Poster == initialB });
        }
        void NoWrite(DesktopUpProjection current, string reason)
        {
            Require(current.Revision == revision && current.PostersById[a].Poster.ContentLayoutJson == unchangedContent, reason);
            UnchangedB(current);
        }
        void CaptureBasis(DesktopUpProjection current)
        {
            revision = current.Revision; documentVersion = Stored(current, a).DocumentVersion;
            entityVersion = current.PostersById[a].EntityVersion; unchangedContent = current.PostersById[a].Poster.ContentLayoutJson;
        }
        void Tick(DispatcherQueueTimer sender, object args)
        {
            if (ticking || stopped) return;
            ticking = true;
            try
            {
                Require(_autoClock.ElapsedMilliseconds < deadline, "Content field observation exceeded 80 seconds.");
                if (!_autoReady || _spatialRuntime!.IsSpatialBusy || _autoClock.ElapsedMilliseconds < readyAfter) return;
                var current = _stateCoordinator!.Refresh();
                var window = _controlCenter!.SettingsWindowForContractProbe;
                if (step != 0 && step != 20 && window?.Content.XamlRoot is null) return;
                if (a.Length > 0 && recordedStep != step)
                { Record("stage-readback", current, window); recordedStep = step; }
                switch (step)
                {
                    case 0:
                        Require(current.Posters.Count == 2 && current.Posters.All(p => p.Poster.IsVisible && !p.Poster.IsCollapsed && !p.AutoPosture.Enabled),
                            "Use two visible expanded ordinary posters with auto posture disabled.");
                        Require(current.Posters.Count(p => p.Poster.IsLocked) == 1, "Fixture must contain unlocked A and locked B.");
                        a = current.Posters.Single(p => !p.Poster.IsLocked).Poster.PosterId;
                        b = current.Posters.Single(p => p.Poster.IsLocked).Poster.PosterId;
                        initialB = current.PostersById[b].Poster; presentationJson = current.PostersById[a].Poster.LayoutParametersJson;
                        _controlCenter.Show(); Next(1); break;
                    case 1:
                        window!.SelectSettingsTargetForProbe(a);
                        var initial = Actual(a);
                        Require(initial.CanEdit && initial.Document.Mode == ContentLayoutMode.Automatic && initial.Document.Placements.Count >= 3 &&
                            initial.Document.Placements.All(p => !p.FixedPosition && p.PinnedIndex is null) && initial.Document.Regions.Count == 1 &&
                            initial.Document.ReservedSpaces.Count == 0, "A must provide ordinary automatic, unpinned DEMO content.");
                        item = initial.Document.Placements[0].ItemId;
                        window.SelectContentItemForProbe(item); window.PrepareContentFieldsForProbe(980, 1040);
                        CaptureBasis(current); Next(2, 600); break;
                    case 2:
                        Require(window!.ReadContentFieldsForProbe().SelectedSlot is not null, "Selected card has no production slot.");
                        window.SetContentFieldsForProbe("span", "bad");
                        // Same-dispatcher conflicts must already see the invalid raw owner.
                        window.SpacingForProbe(1.4); window.SetContentFieldsForProbe("order", "2");
                        window.SetContentModeForProbe(true); window.ApplyContentFieldsForProbe("span"); window.FinishSettingsForProbe();
                        wakeTicket = _spatialRuntime!.QueueCenterProposalWakeForProbe(a);
                        Record("same-dispatcher-invalid-and-conflicting-entries", _stateCoordinator.Refresh(), window); Next(3, 600); break;
                    case 3:
                        NoWrite(current, "Invalid span or conflicting spacing/order/mode wrote state.");
                        var invalid = window!.ReadContentFieldsForProbe();
                        Require(invalid.HasDraft && invalid.DraftPosterId == a && invalid.DraftItemId == item && invalid.DraftGroup == "Span" &&
                            invalid.DraftFirst == "bad" && invalid.ColumnSpan == "bad" && invalid.Mode == ContentLayoutMode.Automatic &&
                            window.CommonSettingsForProbe.Preview is null && window.HasPosterDraft(a), "Invalid field text/ownership was lost or another entry acquired ownership.");
                        var waiting = _spatialRuntime!.ReadCenterProposalWakeForProbe(a);
                        Require(waiting.CenterOwnsDraft && waiting.WaitingForCenterRelease && waiting.HeatPending && waiting.ThemePending &&
                            waiting.ActiveHeatVersion is null && waiting.GeometryFingerprint == wakeTicket!.Baseline.GeometryFingerprint &&
                            waiting.AuthorJson == wakeTicket.Baseline.AuthorJson, "Pending heat/theme changed protected content or did not retain the release wake-up.");
                        observations.Add(new { name = "center-owner-delays-latest-proposals", waiting });
                        window.CancelContentFieldsForProbe("span"); Next(27, 600); break;
                    case 4:
                        NoWrite(current, "Cancelling invalid fields wrote state.");
                        Require(!window!.IsContentFieldDraftActive && !window.HasPosterDraft(a), "Cancel retained ownership.");
                        Require(Actual(a).SelectedSlot is not { ColumnSpan: 4, RowSpan: 3 }, "Fixture already has the requested span, so it cannot prove a commit.");
                        window.SetContentFieldsForProbe("span", "4", "3"); Next(5, 600); break;
                    case 5:
                        NoWrite(current, "Paired span text committed before explicit completion.");
                        var paired = window!.ReadContentFieldsForProbe();
                        Require(paired.HasDraft && paired.DraftFirst == "4" && paired.DraftSecond == "3" && paired.SpanApplyEnabled && paired.SpanCancelEnabled,
                            "Both span fields were not retained as one actionable group.");
                        window.ApplyContentFieldsForProbe("span"); Next(6); break;
                    case 6:
                        var resized = Stored(current, a); var resizedItem = resized.Placements.Single(p => p.ItemId == item);
                        Require(current.Revision == revision + 1 && resized.DocumentVersion == documentVersion + 1 &&
                            resizedItem.ColumnSpan == 4 && resizedItem.RowSpan == 3 && resizedItem.ManualSize && !window!.HasPosterDraft(a),
                            "Paired span application did not produce exactly one persisted edit.");
                        UnchangedB(current); Record("span-single-revision", current, window);
                        CaptureBasis(current); window!.SetContentFieldsForProbe("order", "2"); Next(7); break;
                    case 7:
                        NoWrite(current, "Order input committed before explicit completion.");
                        Require(window!.ReadContentFieldsForProbe().DraftGroup == "Order", "Order did not acquire its own field group.");
                        window.ApplyContentFieldsForProbe("order"); Next(8); break;
                    case 8:
                        var ordered = Stored(current, a);
                        Require(current.Revision == revision + 1 && ordered.DocumentVersion == documentVersion + 1 &&
                            ordered.Placements.Single(p => p.ItemId == item).PinnedIndex == 2 && Actual(a).SelectedSlot?.ReadOrder == 1 && !window!.HasPosterDraft(a),
                            "Order field did not persist a one-revision second-position pin.");
                        UnchangedB(current); Record("automatic-order-single-revision", current, window, "order");
                        CaptureBasis(current); window!.SetContentModeForProbe(true); Next(9); break;
                    case 9:
                        var manual = Stored(current, a); var slot = Actual(a).SelectedSlot!;
                        Require(current.Revision == revision + 1 && manual.DocumentVersion == documentVersion + 1 && manual.Mode == ContentLayoutMode.Manual &&
                            manual.SavedAutomaticMode == ContentLayoutMode.Automatic, "Entering manual did not preserve automatic return intent in one edit.");
                        var position = window!.ReadContentFieldsForProbe("position");
                        Require(position.Column == (slot.Column - slot.RegionColumn + 1).ToString(CultureInfo.InvariantCulture) &&
                            position.Row == (slot.Row - slot.RegionRow + 1).ToString(CultureInfo.InvariantCulture), "Manual controls do not show the selected slot's local one-based position.");
                        // Put this card beyond the existing authored tail; no extra solver/oracle is used.
                        manualRow = manual.Placements.Max(p => (p.Row ?? 0) + p.RowSpan) + 2;
                        CaptureBasis(current); window.SetContentFieldsForProbe("position", "1", (manualRow + 1).ToString(CultureInfo.InvariantCulture)); Next(10); break;
                    case 10:
                        NoWrite(current, "Manual position fields committed before completion.");
                        Require(window!.ReadContentFieldsForProbe("position").DraftGroup == "Position", "Position did not own one group.");
                        window.ApplyContentFieldsForProbe("position"); Next(11); break;
                    case 11:
                        var positioned = Stored(current, a); var positionedItem = positioned.Placements.Single(p => p.ItemId == item);
                        Require(current.Revision == revision + 1 && positioned.DocumentVersion == documentVersion + 1 && positionedItem.Column == 0 &&
                            positionedItem.Row == manualRow && positionedItem.ColumnSpan == 4 && positionedItem.RowSpan == 3 && !window!.HasPosterDraft(a),
                            "Manual position did not save the requested local cell once while retaining span.");
                        UnchangedB(current); Record("manual-position-single-revision", current, window, "position");
                        CaptureBasis(current); window!.SetContentFieldsForProbe("position", "-");
                        window.SetContentModeForProbe(false); window.SetContentFieldsForProbe("span", "2", "2"); window.ApplyContentFieldsForProbe("position"); Next(12); break;
                    case 12:
                        NoWrite(current, "Invalid manual input or second group/mode stole or saved the draft.");
                        var badPosition = window!.ReadContentFieldsForProbe("position");
                        Require(badPosition.DraftGroup == "Position" && badPosition.DraftFirst == "-" && badPosition.Column == "-" && badPosition.Mode == ContentLayoutMode.Manual,
                            "Conflicting mode/span replaced the invalid manual group.");
                        window.CancelContentFieldsForProbe("position"); Next(13); break;
                    case 13:
                        NoWrite(current, "Manual field cancellation wrote state.");
                        Require(!window!.HasPosterDraft(a), "Manual cancellation retained ownership.");
                        window.SetContentFieldsForProbe("span", "3", "3");
                        var beforeRename = current.PostersById[a];
                        var renamed = _stateCommands!.Execute(new PosterStateCommand("content-fields-external-rename", StateCommandSource.ControlCenter,
                            PosterStateCommandKind.Rename, current.Revision, a, beforeRename.Poster with { Title = beforeRename.Poster.Title + " / stale basis" })
                            { ReadSet = StateReadSet.ForPoster(a, beforeRename.EntityVersion) });
                        Require(renamed.Succeeded, renamed.Error ?? "Formal external rename failed."); Next(14); break;
                    case 14:
                        Require(current.Revision == revision + 1 && current.PostersById[a].EntityVersion == entityVersion + 1 && Stored(current, a).DocumentVersion == documentVersion,
                            "Entity-only stale fixture did not change exactly the poster basis.");
                        var staleEntity = window!.ReadContentFieldsForProbe();
                        Require(staleEntity.DraftEntityVersion == entityVersion && staleEntity.DraftFirst == "3" && staleEntity.DraftSecond == "3", "External rename rebased or discarded raw fields.");
                        revision = current.Revision; window.ApplyContentFieldsForProbe("span"); Next(15); break;
                    case 15:
                        NoWrite(current, "Stale entity application overwrote current state.");
                        var rejectedEntity = window!.ReadContentFieldsForProbe();
                        Require(rejectedEntity.HasDraft && rejectedEntity.ColumnSpan == "3" && rejectedEntity.RowSpan == "3" && rejectedEntity.Notice.Contains("基准", StringComparison.Ordinal),
                            "Stale entity draft was not retained with a conflict notice.");
                        Record("stale-entity-retains-raw-input", current, window);
                        window.CancelContentFieldsForProbe("span"); CaptureBasis(current); window.SetContentFieldsForProbe("span", "3", "2");
                        var source = Stored(current, a); var currentA = current.PostersById[a];
                        var changed = _stateCommands!.Execute(new ContentLayoutCommand("content-fields-external-guides", StateCommandSource.ControlCenter,
                            current.Revision, a, StateReadSet.ForPoster(a, currentA.EntityVersion), source.DocumentVersion, source.ItemSetVersion,
                            source with { DocumentVersion = source.DocumentVersion + 1, ShowSpanLabels = !source.ShowSpanLabels }));
                        Require(changed.Succeeded, changed.Error ?? "Formal external document edit failed."); Next(16); break;
                    case 16:
                        Require(current.Revision == revision + 1 && Stored(current, a).DocumentVersion == documentVersion + 1 &&
                            current.PostersById[a].EntityVersion == entityVersion + 1, "Document stale fixture did not change one aggregate/document version.");
                        Require(window!.ReadContentFieldsForProbe().DraftDocumentVersion == documentVersion, "External document edit silently rebased the field draft.");
                        revision = current.Revision; unchangedContent = current.PostersById[a].Poster.ContentLayoutJson;
                        window.ApplyContentFieldsForProbe("span"); Next(17); break;
                    case 17:
                        NoWrite(current, "Stale document application overwrote the latest document.");
                        var rejectedDocument = window!.ReadContentFieldsForProbe();
                        Require(rejectedDocument.HasDraft && rejectedDocument.ColumnSpan == "3" && rejectedDocument.RowSpan == "2" &&
                            rejectedDocument.Notice.Contains("基准", StringComparison.Ordinal), "Stale document draft was lost or lacks a conflict notice.");
                        Record("stale-document-retains-raw-input", current, window);
                        window.CancelContentFieldsForProbe("span"); window.SetContentFieldsForProbe("span", "2", "3");
                        Require(window.HasPosterDraft(a), "Target-switch fixture did not own A's draft.");
                        window.SelectSettingsTargetForProbe(b); Next(18); break;
                    case 18:
                        NoWrite(current, "Switching to B saved A's draft.");
                        var locked = window!.ReadContentFieldsForProbe();
                        Require(locked.PosterId == b && !locked.CanEdit && !locked.SpanEnabled && !window.HasPosterDraft(a) && !window.HasPosterDraft(b),
                            "Target switch retained A owner or made locked B writable.");
                        window.SelectContentItemForProbe(Actual(b).Document.Placements[0].ItemId);
                        window.SetContentFieldsForProbe("span", "2", "2"); window.ApplyContentFieldsForProbe("span"); window.SetContentModeForProbe(true); Next(19); break;
                    case 19:
                        NoWrite(current, "Locked B's programmatic controls bypassed mutation guards.");
                        Require(!window!.IsContentFieldDraftActive, "Locked B acquired a content draft.");
                        window.SelectSettingsTargetForProbe(a); window.SelectContentItemForProbe(item);
                        savedContent = current.PostersById[a].Poster.ContentLayoutJson;
                        window.SetContentFieldsForProbe("span", "2", "2"); Require(window.HasPosterDraft(a), "Close fixture did not retain a draft.");
                        _controlCenter.CloseForContractProbe(); Next(20); break;
                    case 20:
                        NoWrite(current, "Closing center committed a content draft.");
                        Require(window is null && !_controlCenter.IsSettingsInteractionActive, "Closing center retained the field owner or window.");
                        _controlCenter.Show(); Next(21); break;
                    case 21:
                        window!.SelectSettingsTargetForProbe(a); window.SelectContentItemForProbe(item);
                        window.PrepareContentFieldsForProbe(980, 1040); Next(22, 600); break;
                    case 22:
                        NoWrite(current, "Reopening center modified saved content.");
                        var reopened = window!.ReadContentFieldsForProbe("position");
                        Require(current.PostersById[a].Poster.ContentLayoutJson == savedContent && !reopened.HasDraft && reopened.ColumnSpan == "4" &&
                            reopened.RowSpan == "3" && reopened.Column == "1" && reopened.Row == (manualRow + 1).ToString(CultureInfo.InvariantCulture),
                            "Reopened controls did not read saved span and local position.");
                        Record("center-close-cancels-and-reopen-reads-saved", current, window, "position"); Next(23, 0); break;
                    case 23:
                        var setup = captureCases[captureIndex];
                        window!.PrepareContentFieldsForProbe(setup.Item2, setup.Item3);
                        window.SetContentFieldsForProbe(setup.Item5, setup.Item5 == "span" ? "3" : "1",
                            setup.Item5 == "span" ? "3" : (manualRow + 2).ToString(CultureInfo.InvariantCulture));
                        Next(24, 600); break;
                    case 24:
                        window!.RevealContentFieldSectionForProbe(captureCases[captureIndex].Item5); Next(25, 600); break;
                    case 25:
                        NoWrite(current, "Capture-only raw draft modified content.");
                        var sample = captureCases[captureIndex]; var sampleRead = window!.ReadContentFieldsForProbe(sample.Item5);
                        observations.Add(new { name = sample.Item1, controls = sampleRead });
                        Require(sampleRead.ContentTabSelected && sampleRead.Compact == sample.Item4 && sampleRead.HorizontalBoundsValid && sampleRead.SectionFullyVisible && sampleRead.HasDraft,
                            "Actual content field group/buttons are clipped, stale, or in the wrong width mode.");
                        Require(sampleRead.ImpactShownOnce && !sampleRead.CommonSavedNoticeVisible,
                            "A live field draft must explain its impact once without a contradictory saved message.");
                        Require(sample.Item5 == "span" ? sampleRead.SpanApplyEnabled && sampleRead.SpanCancelEnabled : sampleRead.PositionApplyEnabled && sampleRead.PositionCancelEnabled,
                            "Screenshot did not expose actionable apply/cancel buttons.");
                        var surface = _spatialRuntime.ReadContentSurfaceForSmoke(a);
                        Require(surface.Items > 0 && surface.CanvasMatches && surface.InputMatches && surface.Clipped, "Production content canvas/input/clip disagree.");
                        string path = Path.Combine(dataRoot, "content-fields-" + sample.Item1 + ".png");
                        observations.Add(new { name = "native-surface-and-capture", surface, capturePath = path });
                        capture = window.CaptureSettingsForProbeAsync(path); Next(26, 0); break;
                    case 26:
                        if (capture?.IsCompleted != true) return;
                        capture.GetAwaiter().GetResult();
                        captures.Add(Path.Combine(dataRoot, "content-fields-" + captureCases[captureIndex].Item1 + ".png"));
                        window!.CancelContentFieldsForProbe(captureCases[captureIndex].Item5);
                        NoWrite(_stateCoordinator.Refresh(), "Cancelling screenshot draft changed saved state.");
                        Require(!window.HasPosterDraft(a), "Screenshot cleanup retained the center owner.");
                        if (++captureIndex < captureCases.Length) Next(23, 0);
                        else Finish(true, null);
                        break;
                    case 27:
                        NoWrite(current, "Releasing the center owner persisted a derived proposal.");
                        var awakened = _spatialRuntime!.ReadCenterProposalWakeForProbe(a);
                        var awakeFields = window!.ReadContentFieldsForProbe();
                        Require(!awakened.CenterOwnsDraft && !awakened.WaitingForCenterRelease && !awakened.HeatPending && !awakened.ThemePending &&
                            awakened.ActiveHeatVersion == wakeTicket!.LatestHeatVersion && awakened.ThemeParameters == wakeTicket.ExpectedTheme &&
                            awakened.AuthorJson == wakeTicket.Baseline.AuthorJson && awakened.GeometryFingerprint != wakeTicket.Baseline.GeometryFingerprint,
                            "Latest pending proposals did not wake after cancel, or changed author intent.");
                        Require(awakened.SelectedSlot is { } visible && awakeFields.SelectedSlot == visible &&
                            awakeFields.ColumnSpan == visible.ColumnSpan.ToString(CultureInfo.InvariantCulture) &&
                            awakeFields.RowSpan == visible.RowSpan.ToString(CultureInfo.InvariantCulture) &&
                            awakeFields.Order == (visible.ReadOrder + 1).ToString(CultureInfo.InvariantCulture),
                            "Center fields did not refresh from the new effective geometry.");
                        wakeGeneration = awakened.ViewportGeneration;
                        observations.Add(new { name = "owner-release-wakes-latest-and-refreshes-fields", awakened, awakeFields });
                        Next(28, 600); break;
                    case 28:
                        NoWrite(current, "Idle proposal observation wrote state.");
                        Require(_spatialRuntime!.ReadCenterProposalWakeForProbe(a).ViewportGeneration == wakeGeneration,
                            "The drained proposal caused a repeated idle reflow.");
                        _spatialRuntime.RestoreCenterProposalWakeForProbe(a, wakeTicket!); Next(29, 600); break;
                    case 29:
                        NoWrite(current, "Restoring derived presentation wrote author state.");
                        var restoredWake = _spatialRuntime!.ReadCenterProposalWakeForProbe(a);
                        observations.Add(new { name = "derived-proposals-restored-without-write", restoredWake });
                        Require(!restoredWake.HeatPending && !restoredWake.ThemePending &&
                            restoredWake.ActiveHeatVersion == wakeTicket!.LatestHeatVersion + 1 &&
                            restoredWake.ThemeParameters == wakeTicket.Baseline.ThemeParameters &&
                            restoredWake.AuthorJson == wakeTicket.Baseline.AuthorJson && restoredWake.LayoutFingerprint == wakeTicket.Baseline.LayoutFingerprint,
                            "Proposal fixture restoration did not recover the original layout and author intent.");
                        // Reordering and metrics changes retain the current reading anchor. Restoring
                        // the author layout must not incorrectly demand rewinding the browse offset.
                        var restoredSurface = _spatialRuntime.ReadContentSurfaceForSmoke(a);
                        Require(restoredSurface.CanvasMatches && restoredSurface.InputMatches && restoredSurface.Clipped,
                            "Restored browsing geometry no longer matches the actual clipped content/input.");
                        Next(4); break;
                }
            }
            catch (Exception error)
            {
                // The stage readback is recorded before each assertion. Also preserve a best-effort
                // final control snapshot when a synchronous handler fails inside the stage.
                try { Record("failure-readback", _stateCoordinator!.Refresh(), _controlCenter?.SettingsWindowForContractProbe); }
                catch (Exception readError) { observations.Add(new { name = "failure-readback-unavailable", error = readError.Message }); }
                Finish(false, error.ToString());
            }
            finally { ticking = false; }
        }
        timer.Tick += Tick; timer.Start();
    }
}
