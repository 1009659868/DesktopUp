using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Dispatching;
namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    internal int PostureGroupsForProbe => _postures.Count;
    internal int PendingPosturesForProbe => _pendingPostures.Count;
    internal void CancelPostureForProbe(string id)
    { if (_windows.TryGetValue(id, out var window) && PostureFor(window) is { } run) CancelPosture("injected-one-group", specific: run); }
    internal void TogglePostureForProbe(string id) => ToggleCollapsed(_windows[id]);
}

public partial class App
{
    // Explicit isolated runner only. Actual production admission / R4 / SQLite / recovery,
    // but NOT synthetic mouse input and NOT a desktop-pixel certificate.
    private void RunPostureGroupsProbe(bool chain)
    {
        var timer = _dispatcher!.CreateTimer(); timer.Interval = TimeSpan.FromMilliseconds(40);
        var lease = _ledger!.Acquire("fixture.posture-groups", "rr02-05");
        int stage = 0; long until = _autoClock.ElapsedMilliseconds + 55000, revision = 0;
        string a = "", b = ""; bool stopped = false;
        var results = new List<StateCommandResult>(); var checks = new List<string>();
        DesktopUpProjection Current() => _stateCoordinator!.Refresh();
        void Require(bool value, string error) { if (!value) throw new InvalidOperationException($"stage {stage}: {error}"); }
        void Set(string id, bool collapsed)
        {
            var state = Current(); var p = state.PostersById[id];
            _spatialRuntime!.SetCollapsed(new("rr-probe-" + Guid.NewGuid().ToString("N"), StateCommandSource.Poster,
                PosterStateCommandKind.SetCollapsed, state.Revision, id, p.Poster with { IsCollapsed = collapsed })
                { ReadSet = StateReadSet.ForPoster(id, p.EntityVersion) }, results.Add);
        }
        void Stop() { if (stopped) return; stopped = true; timer.Stop(); timer.Tick -= Tick; lease.Dispose(); _stopAutoSmoke = null; }
        _stopAutoSmoke = Stop;
        void Finish(bool passed, string? error)
        {
            Stop(); _log!.Write(passed ? "info" : "error", "rr.groups_observed", new { passed, error, chain, stage, checks,
                results = results.Select(r => new { r.RequestId, r.Status, r.Error, r.CurrentRevision }), nativeInput = false }); ExitApplication();
        }
        void Tick(DispatcherQueueTimer sender, object args)
        {
            if (stopped) return;
            try
            {
                Require(_autoClock.ElapsedMilliseconds < until, "timeout");
                if (!_autoReady || _spatialRuntime is null || _spatialRuntime.IsSpatialBusy || _spatialRuntime.PendingPosturesForProbe > 0) return;
                var current = Current();
                if (stage == 0)
                {
                    Require(current.Posters.Count == 2, "requires two isolated posters");
                    a = current.Posters[0].Poster.PosterId; b = current.Posters[1].Poster.PosterId;
                    Set(a, true); stage++; return;
                }
                if (stage == 1) { Set(b, true); stage++; return; }
                if (stage == 2)
                {
                    Require(results.All(r => r.Succeeded), "initial compaction failed"); results.Clear(); revision = current.Revision;
                    Set(a, false); Set(b, false);
                    Require(_spatialRuntime.PostureGroupsForProbe == (chain ? 1 : 2), "wrong active group count");
                    Require(_spatialRuntime.PendingPosturesForProbe == (chain ? 1 : 0), "wrong queued intent count");
                    checks.Add(chain ? "intersecting-request-retained" : "two-native-groups-started-before-first-completed"); stage++; return;
                }
                if (stage == 3)
                {
                    Require(results.Count == 2 && results.All(r => r.Succeeded), "parallel/queued commit failed");
                    Require(current.Posters.All(p => !p.Poster.IsCollapsed) && current.Revision == revision + 2, "endpoint/revision mismatch");
                    checks.Add("two-full-CAS-single-revision-commits"); results.Clear(); Set(a, true); Set(b, true); stage++; return;
                }
                if (stage == 4)
                {
                    Require(results.All(r => r.Succeeded), "reset compaction failed"); results.Clear();
                    Set(a, false); _spatialRuntime.TogglePostureForProbe(a); _spatialRuntime.TogglePostureForProbe(a); _spatialRuntime.TogglePostureForProbe(a);
                    Require(_spatialRuntime.PendingPosturesForProbe == 1, "same target must coalesce"); stage++; return;
                }
                if (stage == 5)
                {
                    Require(current.PostersById[a].Poster.IsCollapsed, "last reverse toggle did not win");
                    checks.Add("same-target-latest-intent-not-stale-committed-toggle"); Set(a, true); stage++; return;
                }
                if (stage == 6)
                {
                    if (chain) { Finish(true, null); return; }
                    results.Clear(); Set(a, false); Set(b, false);
                    Require(_spatialRuntime.PostureGroupsForProbe == 2, "failure case did not begin both");
                    _spatialRuntime.CancelPostureForProbe(a);
                    Require(_spatialRuntime.PostureGroupsForProbe == 1, "one failure cancelled peer"); stage++; return;
                }
                if (stage == 7)
                {
                    Require(results.Count == 2 && results.Count(r => r.Succeeded) == 1 &&
                        current.PostersById[a].Poster.IsCollapsed && !current.PostersById[b].Poster.IsCollapsed, "failure/restore crossed group");
                    checks.Add("one-group-failure-peer-continues"); results.Clear(); Set(b, true); stage++; return;
                }
                if (stage == 8)
                {
                    results.Clear(); Set(a, false); Set(b, false); _spatialRuntime.Suspend();
                    Require(_spatialRuntime.PostureGroupsForProbe == 0 && results.Count == 2 && results.All(r => !r.Succeeded), "global invalidation leaked groups");
                    Require(_spatialRuntime.RecoverContentForSmoke(a) && _spatialRuntime.RecoverContentForSmoke(b), "global restore failed");
                    _spatialRuntime.Resume(); checks.Add("global-invalidation-cleans-every-group"); Finish(true, null);
                }
            }
            catch (Exception error) { Finish(false, error.ToString()); }
        }
        timer.Tick += Tick; timer.Start();
    }
}
