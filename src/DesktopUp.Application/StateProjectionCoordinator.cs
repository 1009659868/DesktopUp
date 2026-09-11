using System.Collections.ObjectModel;
using DesktopUp.Domain;

namespace DesktopUp.Application;

public sealed class StateProjectionCoordinator(
    IStateCommandRepository repository,
    IStructuredLog? log = null) : IStateProjectionCoordinator, IStateCommandService, IAutomaticPostureCommitter
{
    private readonly object _sync = new();
    private readonly Queue<StateProjectionChangedEventArgs> _pending = new();
    private DesktopUpProjection? _current;
    private bool _publishing;

    public event EventHandler<StateProjectionChangedEventArgs>? ProjectionChanged;

    public DesktopUpProjection EnsureInitialized(PosterDefinition defaultPoster)
    {
        DesktopUpProjection projection;
        lock (_sync) projection = AdoptLocked(repository.EnsureInitialized(defaultPoster), StateCommandSource.System, "initialize");
        DrainEvents();
        return projection;
    }

    public DesktopUpProjection Refresh()
    {
        DesktopUpProjection projection;
        lock (_sync) projection = AdoptLocked(repository.Read(), StateCommandSource.System, "refresh");
        DrainEvents();
        return projection;
    }

    public bool TryGetCurrent(out DesktopUpProjection? projection)
    {
        lock (_sync) { projection = _current; return projection is not null; }
    }

    public PosterRemovalPreview? PrepareRemoval(string posterId)
    {
        Refresh();
        lock (_sync)
        {
            if (_current is null || !_current.PostersById.TryGetValue(posterId, out PosterSurfaceProjection? poster)) return null;
            return new(posterId, poster.Poster.Title, _current.Revision,
                StateReadSet.ForPoster(posterId, poster.EntityVersion) with { CatalogVersion = _current.CatalogVersion },
                _current.Posters.Count == 1, poster.Poster.CollectionId,
                "仅删除此海报及其展示配置，不删除、移动或清理实际文件。删除最后一张后保留空目录，控制中心和托盘继续运行。");
        }
    }

    public StateCommandResult Execute(PosterStateCommand command) => Run(
        command.RequestId, command.Source, command.ExpectedRevision, command.ReadSet,
        reads => command with { ReadSet = reads, SpatialGesture = command.SpatialGesture is { } gesture
            ? gesture with { Path = Array.AsReadOnly(gesture.Path.ToArray()) } : null }, StateCommandPolicy.Prepare);
    public StateCommandResult Execute(SettingsStateCommand command) => Run(
        command.RequestId, command.Source, command.ExpectedRevision, command.ReadSet,
        reads => command with { ReadSet = reads }, StateCommandPolicy.Prepare);
    public StateCommandResult Execute(CreatePosterCommand command) => Run(
        command.RequestId, command.Source, command.ExpectedRevision, command.ReadSet,
        reads => command with { ReadSet = Required(reads) }, StateCommandPolicy.Prepare);
    public StateCommandResult Execute(ClonePosterCommand command) => Run(
        command.RequestId, command.Source, command.ExpectedRevision, command.ReadSet,
        reads => command with { ReadSet = Required(reads) }, StateCommandPolicy.Prepare);
    public StateCommandResult Execute(RemovePosterCommand command) => Run(
        command.RequestId, command.Source, command.ExpectedRevision, command.ReadSet,
        reads => command with { ReadSet = Required(reads) }, StateCommandPolicy.Prepare);
    public StateCommandResult Execute(BatchPosterCommand command) => Run(
        command.RequestId, command.Source, command.ExpectedRevision, command.ReadSet,
        reads => command with { ReadSet = Required(reads), PosterIds = Array.AsReadOnly(command.PosterIds.Order(StringComparer.Ordinal).ToArray()) },
        StateCommandPolicy.Prepare);

    public StateCommandResult ExecuteAutomaticPosture(PosterStateCommand command) => Run(
        command.RequestId, command.Source, command.ExpectedRevision, command.ReadSet,
        reads => command with { ReadSet = reads }, AutomaticPostureCommitPolicy.Prepare, allowSystem: true);

    private static StateReadSet Required(StateReadSet? reads) => reads ?? throw new ArgumentException("Entity read set is required.");

    private StateCommandResult Run<T>(string requestId, StateCommandSource source, long observedRevision,
        StateReadSet? readSet, Func<StateReadSet?, T> freeze, Func<T, DesktopStateSnapshot, StateMutationPlan> prepare, bool allowSystem = false)
    {
        StateCommandResult result;
        lock (_sync)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(requestId) || requestId.Length > 128 || observedRevision < 0 ||
                    !Enum.IsDefined(source) || (source == StateCommandSource.System && !allowSystem))
                    throw new ArgumentException("Request id, source or observed revision is invalid.");
                StateReadSet? reads = readSet?.Freeze();
                if (reads is not null && (reads.Posters.Any(p => string.IsNullOrWhiteSpace(p.Key) || p.Key.Length > 128 || p.Value < 1) ||
                    reads.SettingsVersion is < 1 || reads.CatalogVersion is < 0))
                    throw new ArgumentException("Entity read set contains invalid identities or versions.");
                T frozen = freeze(reads);
                string hash = StateRequestFingerprint.Compute(new
                {
                    protocol = "desktopup-state-command-v2", type = typeof(T).Name, command = frozen
                });
                if (_current is null)
                    result = Failure(StateCommandStatus.NotReady, requestId, observedRevision, "State projection is not initialized.");
                else
                {
                    StateTransactionResult write = repository.Execute(new(requestId, hash, observedRevision, reads), state => prepare(frozen, state));
                    DesktopUpProjection projection = AdoptLocked(write.Snapshot, source, requestId);
                    result = new(write.Status, requestId, observedRevision, projection.Revision, projection, write.Error)
                    {
                        CommittedRevision = write.CommittedRevision, ResultPosterId = write.ResultPosterId, Conflicts = write.Conflicts
                    };
                }
            }
            catch (ArgumentException exception)
            {
                result = Failure(StateCommandStatus.ValidationFailed, requestId, observedRevision, exception.Message);
            }
            catch (Exception exception)
            {
                result = Failure(StateCommandStatus.Failed, requestId, observedRevision, exception.Message);
                SafeLog("error", "state_command.failed", new { requestId, exception.Message });
            }
        }
        DrainEvents();
        return result;
    }

    private StateCommandResult Failure(StateCommandStatus status, string id, long observed, string error) =>
        new(status, id, observed, _current?.Revision ?? -1, _current, error);

    private DesktopUpProjection AdoptLocked(DesktopStateSnapshot snapshot, StateCommandSource source, string requestId)
    {
        if (_current is not null && snapshot.Revision <= _current.Revision) return _current;
        DesktopUpProjection projection = BuildProjection(snapshot);
        DesktopUpProjection? previous = _current;
        var changed = projection.PostersById.Keys.Concat(previous?.PostersById.Keys ?? Array.Empty<string>())
            .Distinct(StringComparer.Ordinal).Where(id => previous is null ||
                !previous.PostersById.TryGetValue(id, out PosterSurfaceProjection? old) ||
                !projection.PostersById.TryGetValue(id, out PosterSurfaceProjection? next) || old.EntityVersion != next.EntityVersion)
            .Order(StringComparer.Ordinal).ToArray();
        _current = projection;
        _pending.Enqueue(new(projection, source, requestId)
        {
            ChangedPosterIds = Array.AsReadOnly(changed),
            SettingsChanged = previous?.ControlCenter.Settings.EntityVersion != projection.ControlCenter.Settings.EntityVersion,
            CatalogChanged = previous?.CatalogVersion != projection.CatalogVersion || previous?.ProfileInitialized != projection.ProfileInitialized
        });
        return projection;
    }

    // FIFO serializes concurrent/re-entrant publications. One faulty subscriber cannot undo a commit or starve another surface.
    private void DrainEvents()
    {
        lock (_sync) { if (_publishing) return; _publishing = true; }
        while (true)
        {
            StateProjectionChangedEventArgs next;
            lock (_sync)
            {
                if (_pending.Count == 0) { _publishing = false; return; }
                next = _pending.Dequeue();
            }
            Delegate[] subscribers = ProjectionChanged?.GetInvocationList() ?? Array.Empty<Delegate>();
            foreach (EventHandler<StateProjectionChangedEventArgs> subscriber in subscribers)
            {
                try { subscriber(this, next); }
                catch (Exception exception) { SafeLog("error", "state_projection.subscriber_failed", new { next.Projection.Revision, exception.Message }); }
            }
            SafeLog("info", "state_projection.published", new { next.Projection.Revision, next.RequestId, next.ChangedPosterIds });
        }
    }

    private void SafeLog(string level, string name, object data)
    {
        try { log?.Write(level, name, data); } catch { /* Logging is not state authority. */ }
    }

    private static DesktopUpProjection BuildProjection(DesktopStateSnapshot state)
    {
        if (state.Settings.Revision != state.Revision || state.Catalog.Posters.Any(p => p.Revision != state.Revision))
            throw new InvalidDataException("Projection inputs must come from a single committed revision.");
        var posters = state.Catalog.Posters.Select(p => new PosterSurfaceProjection(
            state.Revision, p.PosterRevision, p.Poster, !p.Poster.IsLocked, !p.Poster.IsLocked && !p.Poster.IsCollapsed, true)
            { EntityVersion = p.EntityVersion, LayoutGeneration = p.LayoutGeneration, LastLayoutOperationId = p.LastLayoutOperationId }).ToList().AsReadOnly();
        var byId = new ReadOnlyDictionary<string, PosterSurfaceProjection>(posters.ToDictionary(p => p.Poster.PosterId, StringComparer.Ordinal));
        // Transitional selection alias, never the identity used to route a target command.
        PosterSurfaceProjection? selected = byId.GetValueOrDefault(BuiltInStateIdentities.PrimaryPosterId) ?? posters.FirstOrDefault();
        var settings = new SettingsProjection(state.Revision, state.Settings.SettingsRevision, state.Settings.Settings)
            { EntityVersion = state.Settings.EntityVersion };
        var center = new ControlCenterProjection(state.Revision, selected, settings)
            { Posters = posters, PostersById = byId, CatalogVersion = state.Catalog.CatalogVersion };
        int locked = posters.Count(p => p.Poster.IsLocked);
        int visible = posters.Count(p => p.Poster.IsVisible);
        PosterLockSummary summary = posters.Count == 0 ? PosterLockSummary.Empty : locked == 0 ? PosterLockSummary.None :
            locked == posters.Count ? PosterLockSummary.All : PosterLockSummary.Some;
        var tray = new TrayProjection(state.Revision, posters.Count, summary == PosterLockSummary.All,
            settings.Settings.DynamicEffectsPaused, $"DesktopUp · {posters.Count} 张海报 · {visible} 可见 · {locked} 锁定" +
                (settings.Settings.DynamicEffectsPaused ? " · 已暂停" : ""))
            { VisiblePosterCount = visible, LockedPosterCount = locked, LockSummary = summary,
                AutoPostureEnabledPosterCount = posters.Count(p => p.Poster.AutoCollapseEnabled) };
        return new(state.Revision, selected, center, tray) { ProfileInitialized = state.Catalog.ProfileInitialized,
            SpatialOperations = state.SpatialOperations, SpatialContinuities = state.SpatialContinuities };
    }
}
