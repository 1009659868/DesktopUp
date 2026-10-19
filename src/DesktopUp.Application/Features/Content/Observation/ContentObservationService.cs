namespace DesktopUp.Application;

// One per profile; the platform monitor owns scheduling and invokes this serially.
public sealed class ContentObservationService(ManagedContentSyncUseCase managed, MappedDirectoryUseCase mapped) : IContentObservation
{
    public IReadOnlyList<ContentWatchTarget> WatchTargets()
    {
        var owned = managed.ActivePaths().ToHashSet(StringComparer.OrdinalIgnoreCase);
        return owned.Concat(mapped.ActivePaths()).Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => new ContentWatchTarget(path, owned.Contains(path))).ToArray();
    }

    public ManagedContentSyncResult Reconcile(CancellationToken token = default)
    {
        var result = managed.Reconcile(token);
        var mapping = mapped.Reconcile(token);
        return result with { IndexChanged = result.IndexChanged || mapping.Changed, MappingStates = mapping.States,
            Pending = result.Pending || mapping.Pending,
            Notices = result.Notices.Concat(mapping.Notices.Select(n => "目录映射：" + n)).ToArray() };
    }
}
