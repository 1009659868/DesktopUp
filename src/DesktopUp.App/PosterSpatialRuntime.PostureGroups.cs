using DesktopUp.Application;
using DesktopUp.Domain;
namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    private readonly Dictionary<string, PostureRun> _postures = new(StringComparer.Ordinal);
    private PostureRun? _posture => _postures.Values.FirstOrDefault();
    private sealed record PendingPosture(PosterStateCommand Command, Action<StateCommandResult> Completed, long Until);
    private readonly Dictionary<string, PendingPosture> _pendingPostures = new(StringComparer.Ordinal);
    private PostureRun? PostureFor(PosterVisualWindow owner) => _postures.Values.FirstOrDefault(r => r.Participants.Contains(owner));
    internal bool IsPosterSpatialBusy(string id, bool includeFileTransfer=true) => includeFileTransfer&&IsFileTransferActive(id) || _owner is not null || _title is not null ||
        _postures.Values.Any(r => r.Participants.Any(p => p.PosterId == id)) ||
        _windows.TryGetValue(id, out var window) && window.HasContentGesture;

    private bool TryPostureSnapshot(PosterVisualWindow owner, out SpatialSnapshot? snapshot, out DesktopUpProjection? projection)
    {
        if (!TrySnapshot(owner, out snapshot, out projection)) return false;
        // Never feed another run's intermediate physical frame to a new space transaction.
        var current = snapshot!;
        snapshot = new(current.Environment, current.Posters.Select(p =>
            _postures.Values.FirstOrDefault(r => r.Participants.Any(w => w.PosterId == p.PosterId))?.Initial.Posters
                .FirstOrDefault(s => s.PosterId == p.PosterId) ?? p));
        return true;
    }

    private void QueuePosture(PosterStateCommand command, Action<StateCommandResult> completed)
    {
        if (command.Source == StateCommandSource.System)
        { completed(Failure(command, "空间组被占用，自动意图等待后续观察。")); return; }
        if (_pendingPostures.Remove(command.PosterId, out var superseded))
            superseded.Completed(Failure(superseded.Command, "已由该海报的最新收展意图替代。"));
        if (_pendingPostures.Count >= PosturePendingPolicy.Capacity) { completed(Failure(command, "等待队列已满。")); return; }
        _pendingPostures[command.PosterId] = new(command, completed, Environment.TickCount64 + PosturePendingPolicy.LifetimeMilliseconds);
        if (_windows.TryGetValue(command.PosterId, out var window)) window.ShowPosturePending(true);
        log.Write("info", "spatial.posture_queued", new { command.RequestId, command.PosterId, command.DesiredState.IsCollapsed });
    }

    private void DrainPostures()
    {
        if (_suspended || _title is not null || _owner is not null) return;
        foreach (var (id, pending) in _pendingPostures.ToArray())
        {
            if (!_windows.TryGetValue(id, out var owner) || !state.TryGetCurrent(out var p) ||
                p?.PostersById.GetValueOrDefault(id) is not { } target ||
                Environment.TickCount64 > pending.Until || !target.IsEffectivelyVisible || HasCenterDraft(id))
            {
                _pendingPostures.Remove(id); owner?.ShowPosturePending(false);
                pending.Completed(Failure(pending.Command, "等待的收展意图已过期或目标失效。")); continue;
            }
            if (PostureFor(owner) is not null) continue;
            // Our own previous posture may change geometry/version, but unrelated edits may not
            // be silently adopted. Compare all saved non-geometric intent before rebuilding CAS.
            var old = pending.Command.DesiredState;
            _pendingPostures.Remove(id); owner.ShowPosturePending(false);
            if (!PosturePendingPolicy.CanAdopt(old, target.Poster)) { pending.Completed(Failure(pending.Command, "等待期间配置已变化，请重新操作。")); continue; }
            var command = pending.Command with { ExpectedRevision = p.Revision, DesiredState = target.Poster with { IsCollapsed = old.IsCollapsed },
                ReadSet = StateReadSet.ForPoster(id, target.EntityVersion) };
            SetCollapsed(command, pending.Completed);
            if (_pendingPostures.TryGetValue(id, out var again)) _pendingPostures[id] = again with { Until = pending.Until };
        }
    }

    private bool TryRebasePeer(PostureRun waiting, PostureRun committed, DesktopUpProjection projection)
    {
        if (waiting.Reservation.Conflicts(committed.Reservation) || waiting.Command.SpatialPosture is null ||
            projection.CatalogVersion != waiting.Command.ReadSet!.CatalogVersion) return false;
        var changed = projection.Posters.Where(p => waiting.Command.ReadSet.Posters.GetValueOrDefault(p.Poster.PosterId) != p.EntityVersion).ToArray();
        if (changed.Any(p => !committed.Changes.Any(c => c.PosterId == p.Poster.PosterId))) return false;
        var initial = new SpatialSnapshot(waiting.Initial.Environment, waiting.Initial.Posters.Select(p =>
        {
            var change = committed.Changes.FirstOrDefault(c => c.PosterId == p.PosterId);
            if (change is null) return p;
            var target = projection.PostersById[p.PosterId];
            return p with { Bounds = change.After, IsCollapsed = target.Poster.IsCollapsed, LayoutGeneration = target.LayoutGeneration,
                LastLayoutOperationId = target.LastLayoutOperationId };
        }));
        var command = PostureGroupRebase.TryRebase(waiting.Command, initial, projection, waiting.Changes);
        if (command is null) return false;
        waiting.Initial = initial; waiting.Command = command;
        log.Write("info", "spatial.posture_rebased", new { command.RequestId, peer = committed.Command.RequestId, projection.Revision });
        return true;
    }

    private bool TryRebaseUnrelatedEdit(PostureRun run, DesktopUpProjection projection)
    {
        if (projection.CatalogVersion != run.Command.ReadSet!.CatalogVersion || projection.Posters.Count != run.Command.ReadSet.Posters.Count) return false;
        foreach (var target in projection.Posters.Where(p => run.Command.ReadSet.Posters.GetValueOrDefault(p.Poster.PosterId) != p.EntityVersion))
        {
            string id = target.Poster.PosterId;
            if (run.Participants.Any(w => w.PosterId == id)) return false;
            var before = run.Initial.Posters.FirstOrDefault(p => p.PosterId == id);
            if (before is null || !target.IsEffectivelyVisible || before.IsCollapsed != target.Poster.IsCollapsed ||
                before.IsLocked != target.Poster.IsLocked || before.LayoutGeneration != target.LayoutGeneration ||
                before.LastLayoutOperationId != target.LastLayoutOperationId ||
                !_windows.TryGetValue(id, out var window) || !window.TryReadSpatial(_runtimeId, out var environment, out var after, out _) ||
                environment != run.Initial.Environment || after!.Bounds != before.Bounds) return false;
        }
        var command = PostureGroupRebase.TryRebase(run.Command, run.Initial, projection, run.Changes);
        if (command is null) return false;
        run.Command = command; return true;
    }
}
