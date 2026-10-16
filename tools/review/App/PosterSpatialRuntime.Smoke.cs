using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Dispatching;

namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    // Invoked only by App after the explicit CLI flag and isolated profile guard.
    public void RunIsolatedSmoke(DispatcherQueue dispatcher, string? expectedExpansion = null, bool leaveExpanded = false)
    {
        _measurePostureFrames = true;
        var timer = dispatcher.CreateTimer(); timer.Interval = TimeSpan.FromMilliseconds(300); timer.IsRepeating = true;
        DateTime deadline = DateTime.UtcNow.AddSeconds(16);
        bool busy = false;
        Queue<(string Id, bool Collapsed)>? pending = null;
        timer.Tick += Tick; timer.Start();
        void Finish(bool passed, string? error)
        {
            _measurePostureFrames = false;
            timer.Stop(); timer.Tick -= Tick;
            log.Write(passed ? "info" : "error", "spatial.smoke_completed", new { passed, error });
        }
        void Tick(DispatcherQueueTimer sender, object args)
        {
            if (DateTime.UtcNow > deadline) { Finish(false, "timeout"); return; }
            if (busy || _suspended || !state.TryGetCurrent(out var current) || current is null) return;
            pending ??= new Queue<(string, bool)>(current.Posters.Where(p => p.Poster.IsVisible && !p.Poster.IsLocked)
                .SelectMany(p => leaveExpanded ? new[] { (p.Poster.PosterId, false) } :
                    new[] { (p.Poster.PosterId, !p.Poster.IsCollapsed), (p.Poster.PosterId, p.Poster.IsCollapsed) }));
            if (!pending.TryDequeue(out var next)) { Finish(true, null); return; }
            var target = current.PostersById[next.Id]; busy = true;
            var owner = _windows.GetValueOrDefault(next.Id);
            if (owner is null || !TrySnapshot(owner, out var before, out _)) { Finish(false, "missing-physical-before"); return; }
            bool expanding = target.Poster.IsCollapsed && !next.Collapsed;
            string probeRequest = "spatial-smoke-" + Guid.NewGuid().ToString("N");
            long probeStart = System.Diagnostics.Stopwatch.GetTimestamp();
            SetCollapsed(new(probeRequest, StateCommandSource.ControlCenter,
                PosterStateCommandKind.SetCollapsed, current.Revision, next.Id, target.Poster with { IsCollapsed = next.Collapsed })
                { ReadSet = StateReadSet.ForPoster(next.Id, target.EntityVersion) }, result =>
            {
                busy = false;
                log.Write("info", "spatial.probe_command_completed", new { RequestId = probeRequest, next.Id, next.Collapsed,
                    elapsedMs = System.Diagnostics.Stopwatch.GetElapsedTime(probeStart).TotalMilliseconds,
                    status = result.Status.ToString(), result.CurrentRevision });
                if (expanding && expectedExpansion == "InsufficientSpace")
                {
                    bool unchanged = result.Status == StateCommandStatus.InsufficientSpace && result.CurrentRevision == current.Revision &&
                        TrySnapshot(owner, out var after, out _) && before!.Posters.SequenceEqual(after!.Posters);
                    // Same-tick repeated visual requests must coalesce; no duplicate timer/animation.
                    owner.ShowInsufficientSpaceFeedback(); owner.ShowInsufficientSpaceFeedback();
                    log.Write("info", "spatial.smoke_outcome", new { expectedExpansion, unchanged, status = result.Status.ToString() });
                    if (!unchanged) Finish(false, "insufficient-space-mutated-layout");
                    return;
                }
                if (!result.Succeeded) { Finish(false, result.Error); return; }
                if (expanding)
                {
                    var refreshed = state.Refresh();
                    var effective = _effective.Find(refreshed.PostersById[next.Id]);
                    var actual = effective?.IsConstrained == true ? "Constrained" : "Full";
                    bool valid = effective is not null && refreshed.PostersById[next.Id].Poster.ExpandedHeightDip == target.Poster.ExpandedHeightDip &&
                        TrySnapshot(owner, out var physical, out _) && PosterSpatialSolver.ValidateLayout(physical!).Count == 0 &&
                        physical!.Posters.Single(p => p.PosterId == next.Id).Bounds == effective.Bounds;
                    log.Write("info", "spatial.smoke_outcome", new { expectedExpansion, actual, valid,
                        effectiveHeightDip = effective?.HeightDip, verifiedBounds = valid ? effective?.Bounds : null });
                    if (!valid || expectedExpansion is not null && actual != expectedExpansion) Finish(false, "effective-projection-or-outcome-mismatch");
                }
            });
        }
    }
}
