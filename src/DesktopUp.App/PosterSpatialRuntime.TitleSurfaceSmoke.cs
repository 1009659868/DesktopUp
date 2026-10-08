using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Dispatching;

namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    // Isolated COMPONENT fixture. Semantic title input goes only to the pure driver, never to
    // HWNDs/capture/the OS. Exercises the real R4 surface API and shared committed-state restore;
    // it does NOT attest BeginTitle, native pointer routing or a live title runtime lease.
    internal Action RunTitleSurfaceSmoke(DispatcherQueue dispatcher, Action completed)
    {
        string[] cases = ["prepare-partial", "submit-before", "submit-partial", "restore-partial", "complete",
            "handoff-reuse", "handoff-grow", "translate-fast", "prepare-region-reset",
            "handoff-reset-reuse", "handoff-reset-grow"];
        var timer = dispatcher.CreateTimer(); timer.Interval = TimeSpan.FromMilliseconds(100);
        var deadline = DateTime.UtcNow.AddSeconds(20);
        PosterVisualWindow? owner = null;
        PosterVisualWindow[] participants = [];
        TitleSpatialSession? session = null;
        TitlePresentationDriver? driver = null;
        SpatialSnapshot? initial = null;
        long revision = -1;
        int index = 0, step = 0, ready = 0, submitted = 0, passedCases = 0, moveIndex = 0;
        bool active = false, stopped = false;
        string request = "";
        void Require(bool value, string error) { if (!value) throw new InvalidOperationException(error); }
        void Restore()
        {
            if (!active) return;
            driver?.Cancel("surface-fixture-finish");
            Require(RestorePresentedGroup(owner!, participants, request), "shared committed restore failed");
            active = false;
        }
        void Stop()
        {
            if (stopped) return;
            stopped = true; timer.Stop(); timer.Tick -= Tick;
            Restore();
        }
        void Finish(bool passed, string? error)
        {
            try { Stop(); }
            catch (Exception e) { passed = false; error = (error ?? "") + " restore: " + e; }
            log.Write(passed ? "info" : "error", "title_surface.smoke_completed",
                new { passed, error, passedCases, nativeInput = false, databaseWrites = false });
            completed();
        }
        void Tick(DispatcherQueueTimer sender, object args)
        {
            try
            {
                Require(DateTime.UtcNow < deadline, "surface fixture timeout");
                if (_suspended || !state.TryGetCurrent(out var projection) || projection is null) return;
                Require(!IsSpatialBusy, "another spatial owner interfered");
                if (step == 0)
                {
                    Require(projection.Posters.Count == 3 && projection.Posters.All(p => !p.AutoPosture.Enabled),
                        "requires fresh three-poster auto-OFF spatial-chain fixture");
                    owner = _windows.Values.OrderBy(p => p.PosterId, StringComparer.Ordinal)
                        .FirstOrDefault(p => projection.PostersById[p.PosterId!].Poster.Title.StartsWith("A /", StringComparison.Ordinal));
                    if (owner is null || !TrySnapshot(owner, out initial, out _)) return;
                    Require(initial!.Posters.Count == 3, "all fixture posters must share one work area");
                    participants = initial.Posters.Select(p => _windows[p.PosterId]).ToArray();
                    var source = initial.Posters.Single(p => p.PosterId == owner.PosterId);
                    Require(source.IsCollapsed, "source must be compact");
                    revision = projection.Revision; request = "title-surface-" + cases[index];
                    var frame = Pixels(source.Bounds);
                    var key = owner.ReadPointerTarget().Session;
                    var hit = new PosterInputHit(key, 1, frame,
                        new(PosterInputTargetKind.TitleBar, 0, new(12, 8, 100, 36)));
                    var press = new TitlePress(request, 1, 1, new(Guid.NewGuid(), hit),
                        new(frame.X + 20, frame.Y + 20), TitlePresentedPosture.Compact,
                        new(AutoPosturePresence.ExpansionZone, 1, 0));
                    session = new(new(initial, press, animationOptions),
                        projection.PostersById[owner.PosterId!].Presentation.Metrics,
                        projection.SpatialContinuities.FirstOrDefault(c => c.Environment.WorkspaceId == initial.Environment.WorkspaceId));
                    driver = new(session); ready = 0; submitted = 0; active = true;
                    int preparedCount = 0;
                    foreach (var window in participants)
                    {
                        var physical = initial.Posters.Single(p => p.PosterId == window.PosterId);
                        int capacity = window == owner ? Math.Max(physical.Bounds.Height,
                            Math.Min(initial.Environment.WorkArea.Height, physical.ExpandedHeight)) : physical.Bounds.Height;
                        window.BeginTitlePreview(request, Pixels(physical.Bounds with { Height = capacity }), window == owner);
                        if (++preparedCount == 1 && cases[index] == "prepare-partial") break;
                    }
                    if (cases[index] == "prepare-partial") { Restore(); step = 2; return; }
                    var prepared = participants.Select(w => w.PrepareSpatialFrame(Pixels(driver.Physical[w.PosterId!]))).ToArray();
                    Require(prepared.All(p => p is not null) &&
                        owner.CommitSpatialFrames(prepared.Select(p => p!).ToArray(), DesktopFramePhase.SurfacePreparation,
                            cases[index] == "prepare-region-reset" ? owner.ResetVisualRegionForProbe : null),
                        "surface preparation did not verify");
                    step = 1; return;
                }
                if (step == 1)
                {
                    ready = participants.All(p => p.VerifyTransitionSurfaceReady()) ? ready + 1 : 0;
                    if (ready < 2) return;
                    if (cases[index].StartsWith("handoff-", StringComparison.Ordinal))
                    {
                        // Start with an ACTUAL prepared surface and an accepted expanding prefix.
                        // This is the missing boundary in the original fresh-surface-only fixture.
                        var proposal = session!.Propose(new(new(session.Intent.Token, 2, TitlePointerEventKind.Release,
                            session.Start.Press.Position, true, new(AutoPosturePresence.ExpansionZone, 1, 0))));
                        Require(proposal is not null, "expanding prefix unavailable");
                        var visible = initial!.Posters.ToDictionary(p => p.PosterId, p => p.Bounds);
                        foreach (var batch in proposal!.Frames)
                        {
                            var prepared = batch.Changes.Select(p => _windows[p.PosterId].PrepareSpatialFrame(Pixels(p.To))).ToArray();
                            Require(prepared.All(p => p is not null) &&
                                owner!.CommitSpatialFrames(prepared.Select(p => p!).ToArray(), DesktopFramePhase.Animation),
                                "prefix presentation failed");
                            foreach (var change in batch.Changes) visible[change.PosterId] = change.To;
                            submitted++;
                            if (visible[owner!.PosterId!].Height > initial.Posters.Single(p => p.PosterId == owner.PosterId).Bounds.Height) break;
                        }
                        var source = initial.Posters.Single(p => p.PosterId == owner!.PosterId);
                        Require(visible[source.PosterId].Height > source.Bounds.Height &&
                            visible[source.PosterId].Height < source.ExpandedHeight, "expected an incomplete expansion");
                        foreach (var window in participants)
                        {
                            var p = initial.Posters.Single(p => p.PosterId == window.PosterId);
                            int height = Math.Max(visible[p.PosterId].Height, p.ExpandedHeight);
                            if (window == owner && cases[index] is "handoff-grow" or "handoff-reset-grow")
                                height = Math.Min(initial.Environment.WorkArea.Height, height + 120);
                            window.BeginTitlePreview(request + "-transfer", Pixels(visible[p.PosterId] with { Height = height }), window == owner);
                            Require(window.TryReadSpatial(_runtimeId, out _, out var readback, out _) &&
                                readback!.Bounds == visible[p.PosterId], "handoff lost the previous native/visible baseline");
                        }
                        var handoff = participants.Select(w => w.PrepareSpatialFrame(Pixels(visible[w.PosterId!]))).ToArray();
                        Require(handoff.All(p => p is not null) &&
                            owner!.CommitSpatialFrames(handoff.Select(p => p!).ToArray(), DesktopFramePhase.SurfacePreparation,
                                cases[index].StartsWith("handoff-reset-", StringComparison.Ordinal) ? owner.ResetVisualRegionForProbe : null),
                            "retained R4 surface rejected during title handoff");
                        ready = 0; step = 3; return;
                    }
                    driver!.Queue(new(session!.Intent.Token, 2, TitlePointerEventKind.Release,
                        session.Start.Press.Position, true, new(AutoPosturePresence.ExpansionZone, 1, 0)));
                    driver.Tick(() => state.Refresh().Revision == revision, batch =>
                    {
                        if (cases[index] == "submit-before") return false;
                        var prepared = batch.Changes.Select(p => _windows[p.PosterId].PrepareSpatialFrame(Pixels(p.To))).ToArray();
                        Require(prepared.Length > 1 && prepared.All(p => p is not null),
                            "fixture must exercise an actual multi-window title batch");
                        var subset = cases[index] == "submit-partial" ? prepared.Take(1) : prepared;
                        Require(owner!.CommitSpatialFrames(subset.Select(p => p!).ToArray(), DesktopFramePhase.Animation),
                            "native batch failed before injection");
                        submitted = subset.Count();
                        return cases[index] != "submit-partial";
                    });
                    bool rejected = cases[index] is "submit-before" or "submit-partial";
                    Require(rejected ? session.Cancelled && !session.Complete : session.Complete,
                        "partial group was accepted or complete group was rejected");
                    if (cases[index] == "translate-fast")
                    {
                        // Component-only native translation after expansion. No pointer injection,
                        // runtime title lease or database commit; restore the original scene below.
                        moveIndex = 0; timer.Interval = TimeSpan.FromMilliseconds(16); step = 4; return;
                    }
                    if (cases[index] == "restore-partial")
                    {
                        var first = participants[0].PreparePostureRestore();
                        Require(first is not null && owner!.CommitSpatialFrames([first], DesktopFramePhase.FinalRestore),
                            "partial final restore failed before injection");
                        // Simulate interruption after ONE participant. Retry the exact shared restore.
                    }
                    Restore(); step = 2; return;
                }
                if (step == 4)
                {
                    var basis = driver!.Physical;
                    var area = initial!.Environment.WorkArea;
                    int minX = area.X - basis.Values.Min(p => p.X), maxX = checked((int)(area.Right - basis.Values.Max(p => p.Right)));
                    int minY = area.Y - basis.Values.Min(p => p.Y), maxY = checked((int)(area.Bottom - basis.Values.Max(p => p.Bottom)));
                    Require(maxX >= minX && maxY >= minY, "translation fixture exceeds work area");
                    int dx = minX + moveIndex * 137 % (maxX - minX + 1);
                    int dy = minY + moveIndex * 47 % (maxY - minY + 1);
                    var frames = participants.Select(w =>
                    {
                        var p = basis[w.PosterId!];
                        return w.PrepareSpatialFrame(Pixels(p with { X = p.X + dx, Y = p.Y + dy }));
                    }).ToArray();
                    Require(frames.All(p => p is not null) &&
                        owner!.CommitSpatialFrames(frames.Select(p => p!).ToArray(), DesktopFramePhase.Animation),
                        "fast native translation rejected at frame " + moveIndex);
                    submitted++;
                    if (++moveIndex < 120) return;
                    timer.Interval = TimeSpan.FromMilliseconds(100);
                    Restore(); step = 2; return;
                }
                if (step == 3)
                {
                    ready = participants.All(p => p.VerifyTransitionSurfaceReady()) ? ready + 1 : 0;
                    if (ready < 2) return;
                    Require(participants.All(p => p.VerifyTransitionLayout(true)), "handoff lost stable R4 layout");
                    Restore(); step = 2; return;
                }
                var current = state.Refresh();
                var restoredFrames = InspectPresentation(current);
                Require(current.Revision == revision && restoredFrames.All(p => p.XamlVerified &&
                    p.Bounds == initial!.Posters.Single(q => q.PosterId == p.PosterId).Bounds),
                    "final XAML/native geometry or committed revision changed");
                Require(participants.All(p => p.VerifyTransitionLayout(false)), "transition layout leaked");
                driver!.Tick(() => true, _ => throw new InvalidOperationException("late callback drew after cancellation"));
                Require(!driver.Queue(new(session!.Intent.Token, 999, TitlePointerEventKind.Move,
                    session.Start.Press.Position)), "late input survived closed fixture");
                passedCases++;
                log.Write("info", "title_surface.case_passed", new { fault = cases[index], submitted,
                    current.Revision, restoredFrames, layoutsReleased = true, nativeInput = false });
                if (++index == cases.Length) { Finish(true, null); return; }
                step = 0;
            }
            catch (Exception e) { Finish(false, e.ToString()); }
        }
        timer.Tick += Tick; timer.Start();
        return Stop;
    }
}
