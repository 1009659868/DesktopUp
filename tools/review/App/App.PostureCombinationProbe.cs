using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Xaml;

namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    internal int AcceptedPostureFramesForProbe(string id) =>
        _windows.TryGetValue(id, out var window) ? PostureFor(window)?.AcceptedFrames ?? 0 : 0;

    // Replay a captured, retired callback to prove its identity guard; never install another clock.
    internal Action ReadLatePostureCallbackForProbe(string id)
    {
        var run = PostureFor(_windows[id]) ?? throw new InvalidOperationException("No active group.");
        return () => AdvancePosture(run);
    }
}

internal sealed partial class PosterVisualWindow
{
    internal DesktopPixelRect FrameForCombinationProbe => _bounds;
    // Managed protocol ingress only: published hit region -> original handler -> original flush/commit.
    // No OS input/capture is synthesized and no desired layout is injected.
    internal Action BeginIndependentItemDragForProbe()
    {
        if (spatial.IsPosterSpatialBusy(PosterId!) || HasContentGesture || _geometry.IsLocked || IsEditing)
            throw new InvalidOperationException("Independent Item is unavailable.");
        (_window!.Content as FrameworkElement)!.UpdateLayout();
        var before = ReadCompositionForProbe();
        var tiles = ContentLayoutFlow.ProjectTiles(_contentLayout.Snapshot!, _contentLayout.Viewport!).Where(t =>
            !t.Clip.IsEmpty && Math.Abs(t.Clip.Width - t.Bounds.Width) < .01 &&
            Math.Abs(t.Clip.Height - t.Bounds.Height) < .01).OrderBy(t => t.ReadOrder).Take(2).ToArray();
        if (tiles.Length != 2) throw new InvalidOperationException("Two whole visible cards required.");
        var native = inputRouter.Capture;
        var surface = inputRouter.ReadSurface(inputSession)!;
        var region = surface.Layout.Regions.First(r => r.Kind == PosterInputTargetKind.ContentItem && r.ContentItemId == tiles[0].ItemId);
        var capture = new PosterInputCapture(Guid.NewGuid(), new(inputSession, surface.Layout.Revision, surface.Frame, region));
        var start = new PointerScreenPoint(surface.Frame.X + region.Bounds.X + region.Bounds.Width / 2,
            surface.Frame.Y + region.Bounds.Y + region.Bounds.Height / 2);
        var end = new PointerScreenPoint(start.X + (int)Math.Round((tiles[1].Bounds.X - tiles[0].Bounds.X) * _monitor!.ScaleX),
            start.Y + (int)Math.Round((tiles[1].Bounds.Y - tiles[0].Bounds.Y) * _monitor.ScaleY));
        long sequence = 0;
        void Send(PosterInputEventKind kind, PointerScreenPoint point)
        {
            if (!HandleContentInput(PosterContentInputPolicy.Event(capture, kind, start, point,
                inputRouter.ReadSurface(inputSession)!.Layout.Revision, ++sequence, TitleNativeEndReason.None)))
                throw new InvalidOperationException("Independent Item input rejected.");
        }
        Send(PosterInputEventKind.Pressed, start);
        Send(PosterInputEventKind.GesturePreview, end);
        FlushContentMotion(false);
        if (_contentSession.Draft is not { } proposal || ContentLayoutEditing.SameState(before.Document, proposal) ||
            ReadCompositionForProbe().StoredJson != before.StoredJson)
            throw new InvalidOperationException("Independent drag failed to produce a RAM-only change.");
        return () =>
        {
            long revision = _stateRevision;
            Send(PosterInputEventKind.GestureCompleted, end);
            var after = ReadCompositionForProbe();
            if (HasContentGesture || _contentMotionTimer is not null || inputRouter.Capture != native ||
                after.Revision != revision + 1 || after.EntityVersion != before.EntityVersion + 1 ||
                !ContentLayoutEditing.SameState(proposal, after.Document) || after.OpenIntents != before.OpenIntents)
                throw new InvalidOperationException("Independent Item release lost ownership or single-commit fidelity.");
        };
    }
}

public partial class App
{
    private async void RunPostureCombinationProbe(bool chain)
    {
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(55));
        using var lease = _ledger!.Acquire("fixture.posture-combinations", "rr06");
        var checks = new List<string>();
        var results = new List<StateCommandResult>();
        string stage = "ready", a = "", b = "";
        Action? release = null;
        TaskCompletionSource? heldFrame = null;
        bool passed = false; string? error = null;
        void Stop() { lifetime.Cancel(); heldFrame?.TrySetResult(); }
        _stopAutoSmoke = Stop;
        DesktopUpProjection Current() => _stateCoordinator!.Refresh();
        void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(stage + ": " + message); }
        async Task Until(Func<bool> predicate)
        { while (!predicate()) await Task.Delay(15, lifetime.Token); lifetime.Token.ThrowIfCancellationRequested(); }
        async Task Idle() => await Until(() => !_spatialRuntime!.IsSpatialBusy && _spatialRuntime.PendingPosturesForProbe == 0);
        void Edit(string id, PosterStateCommandKind kind, Func<PosterDefinition, PosterDefinition> edit)
        {
            var p = Current(); var target = p.PostersById[id];
            var result = _stateCommands!.Execute(new PosterStateCommand("rr06-edit-" + Guid.NewGuid().ToString("N"), StateCommandSource.ControlCenter,
                kind, p.Revision, id, edit(target.Poster)) { ReadSet = StateReadSet.ForPoster(id, target.EntityVersion) });
            Require(result.Succeeded && result.CurrentRevision == p.Revision + 1, result.Error ?? "Edit did not commit once.");
        }
        Task<StateCommandResult> Posture(string id, bool compact)
        {
            var p = Current(); var target = p.PostersById[id]; var done = new TaskCompletionSource<StateCommandResult>();
            _spatialRuntime!.SetCollapsed(new("rr06-posture-" + Guid.NewGuid().ToString("N"), StateCommandSource.Poster,
                PosterStateCommandKind.SetCollapsed, p.Revision, id, target.Poster with { IsCollapsed = compact })
                { ReadSet = StateReadSet.ForPoster(id, target.EntityVersion) }, result => { results.Add(result); done.TrySetResult(result); });
            return done.Task.WaitAsync(lifetime.Token);
        }
        async Task HoldFirstFrame(string id, Action start)
        {
            heldFrame = new TaskCompletionSource(); bool seen = false;
            _spatialRuntime!.BackdropFrameObserverForProbe = frame =>
            {
                if (frame.PosterId != id || seen) return Task.CompletedTask;
                seen = true; return heldFrame.Task;
            };
            start(); await Until(() => seen);
            Require(_spatialRuntime.AcceptedPostureFramesForProbe(id) > 0, "No actual R4 animation frame accepted.");
        }
        void ResumeFrame()
        { _spatialRuntime!.BackdropFrameObserverForProbe = null; heldFrame?.TrySetResult(); heldFrame = null; }
        try
        {
            await Until(() => _autoReady && _spatialRuntime is not null && !_spatialRuntime.IsSpatialBusy);
            var initial = Current(); Require(initial.Posters.Count == 2, "Requires isolated two-poster fixture.");
            a = initial.Posters[0].Poster.PosterId; b = initial.Posters[1].Poster.PosterId;
            if (chain)
            {
                stage = "locked-neighbor";
                Edit(a, PosterStateCommandKind.SetLocked, p => p with { IsLocked = true });
                var locked = Current().PostersById[a]; var bounds = _spatialRuntime!.BackdropWindowForProbe(a).FrameForCombinationProbe;
                var outcome = await Posture(b, false); await Idle();
                Require(outcome.Succeeded || outcome.Status == StateCommandStatus.InsufficientSpace, outcome.Error ?? "Unexpected rejection.");
                Require(Current().PostersById[a].Poster == locked.Poster && Current().PostersById[a].EntityVersion == locked.EntityVersion,
                    "Locked neighbor was changed.");
                Require(_spatialRuntime.BackdropWindowForProbe(a).FrameForCombinationProbe == bounds, "Locked neighbor surface changed.");
                checks.Add("locked-chain-neighbor-unchanged-or-expansion-safely-refused");
            }
            else
            {
                if (initial.PostersById[b].Poster.IsLocked) Edit(b, PosterStateCommandKind.SetLocked, p => p with { IsLocked = false });
                foreach (var (id, template) in new[] { (a, "rain"), (b, "lamp") })
                {
                    var recipe = TemplateRecipeCatalog.BuiltIns.Single(r => r.Id == template);
                    var parameters = TemplateRecipeInstancePolicy.Select(PosterPresentationPolicy.Defaults(), recipe);
                    Edit(id, PosterStateCommandKind.SetPresentation, p => p with
                    { TemplateKey = template, LayoutParametersJson = PosterPresentationPolicy.Serialize(parameters, p.LayoutParametersJson) });
                }
                Require((await Posture(a, true)).Succeeded, "Initial compact failed."); await Idle();
                stage = "unrelated-parameter-and-item-drag";
                Task<StateCommandResult>? active = null;
                await HoldFirstFrame(a, () => active = Posture(a, false));
                long basis = Current().Revision;
                Edit(b, PosterStateCommandKind.SetPresentation, p =>
                {
                    var appearance = PosterPresentationPolicy.Resolve(p);
                    var updated = TemplateRecipeInstancePolicy.UpdateValues(appearance.Parameters, "lamp",
                        appearance.TemplateValues.Set("surface.opacity", TemplateParameterValue.FromNumber(25)));
                    return p with { Subtitle = "RR06 independent parameter", LayoutParametersJson = PosterPresentationPolicy.Serialize(updated, p.LayoutParametersJson) };
                });
                Require(_spatialRuntime!.PostureGroupsForProbe == 1 && !active!.IsCompleted, "Parameter edit cancelled independent A.");
                release = _spatialRuntime.BackdropWindowForProbe(b).BeginIndependentItemDragForProbe();
                Require(_spatialRuntime.PostureGroupsForProbe == 1 && !active!.IsCompleted, "Item preview cancelled independent A.");
                release(); release = null;
                Require(Current().Revision == basis + 2 && _spatialRuntime.PostureGroupsForProbe == 1, "B writes lost CAS or cancelled A.");
                ResumeFrame(); Require((await active!).Succeeded, "A failed after independent B commits."); await Idle();
                Require(Current().Revision == basis + 3, "Three operations did not each commit once.");
                checks.Add("accepted-R4-frame-B-parameter-and-direct-Item-preview-release-A-rebase-three-single-commits");

                stage = "automatic-A-manual-B";
                Require((await Posture(a, true)).Succeeded && (await Posture(b, true)).Succeeded, "Compact reset failed."); await Idle();
                Edit(a, PosterStateCommandKind.SetAutoPosture, p => p with { AutoCollapseEnabled = true, AutoCollapseDelaySeconds = 300 });
                var p = Current(); var autoResult = new TaskCompletionSource<AutoPostureOutcome>();
                var request = new AutoPostureRequest(6006, _spatialRuntime.BackdropWindowForProbe(a).ReadPointerTarget().Session,
                    p.PostersById[a].EntityVersion, _autoEnvironment, p.PostersById[a].AutoPosture, _autoEvidence.ManualVersion(a), false);
                await HoldFirstFrame(a, () => _spatialRuntime.StartAutomatic(request, result => autoResult.TrySetResult(result)));
                var manual = Posture(b, false);
                Require(_spatialRuntime.PostureGroupsForProbe == 2 && !autoResult.Task.IsCompleted, "Manual B cancelled automatic A.");
                ResumeFrame(); Require(await autoResult.Task.WaitAsync(lifetime.Token) == AutoPostureOutcome.Committed &&
                    (await manual).Succeeded, "Automatic/manual commit failed."); await Idle();
                checks.Add("automatic-production-port-A-and-manual-B-overlap-without-cross-cancel");
                Edit(a, PosterStateCommandKind.SetAutoPosture, x => x with { AutoCollapseEnabled = false });

                stage = "remove-member-and-late-callback";
                Require((await Posture(a, true)).Succeeded, "Compact reset failed."); await Idle();
                var stablePeer = Current().PostersById[b];
                await HoldFirstFrame(a, () => active = Posture(a, false));
                var late = _spatialRuntime.ReadLatePostureCallbackForProbe(a);
                var pending = Posture(a, true); Require(_spatialRuntime.PendingPosturesForProbe == 1, "Missing queued intent.");
                var removal = _stateCoordinator!.PrepareRemoval(a)!;
                long environmentBeforeRemoval = _autoEnvironment;
                var removed = _stateCommands!.Execute(new RemovePosterCommand("rr06-remove", StateCommandSource.ControlCenter,
                    removal.Revision, removal.ReadSet, a, true));
                Require(removed.Succeeded && !(await active!).Succeeded && !(await pending).Succeeded, "Removal did not retire both callbacks.");
                ResumeFrame(); late(); await Task.Delay(150, lifetime.Token);
                var after = Current();
                Require(!after.PostersById.ContainsKey(a) && after.PostersById[b].Poster == stablePeer.Poster &&
                    after.PostersById[b].EntityVersion == stablePeer.EntityVersion && after.Revision == removed.CurrentRevision &&
                    _spatialRuntime.PostureGroupsForProbe == 0 && _spatialRuntime.PendingPosturesForProbe == 0 &&
                    _autoReady && _autoEnvironment == environmentBeforeRemoval,
                    "Removal/late callback changed committed peer or leaked work.");
                checks.Add("removed-member-pending-retired-late-render-ignored-committed-peer-preserved");
            }
            passed = true;
        }
        catch (Exception exception) { error = exception.ToString(); }
        finally
        {
            ResumeFrame();
            if (release is not null && b.Length > 0) _spatialRuntime?.CancelCompositionForProbe(b);
            if (_stopAutoSmoke == Stop) _stopAutoSmoke = null;
            _log!.Write(passed ? "info" : "error", "rr.groups_observed", new { passed, error, chain, stage, checks,
                combinations = true, nativeInput = false, timingCertified = false,
                frameHold = "First accepted R4 frame held for deterministic interleaving; not used for timing/visual certification",
                results = results.Select(r => new { r.RequestId, r.Status, r.Error, r.CurrentRevision }) });
        }
        lease.Dispose();
        ExitApplication();
    }
}
