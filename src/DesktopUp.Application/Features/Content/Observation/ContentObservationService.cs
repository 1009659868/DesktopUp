namespace DesktopUp.Application;

// One per profile; the platform monitor owns scheduling and invokes this serially.
public sealed class ContentObservationService(ManagedContentSyncUseCase managed, MappedDirectoryUseCase mapped,
    IContentWatchTargetSource targets) : IContentObservation
{
    public IReadOnlyList<ContentWatchTarget> WatchTargets() => targets.Read();

    public ManagedContentSyncResult Reconcile(CancellationToken token = default, IReadOnlySet<string>? directories = null)
    {
        var result = managed.Reconcile(token, directories);
        var mapping = mapped.Reconcile(token, directories);
        return result with { IndexChanged = result.IndexChanged || mapping.Changed, MappingStates = mapping.States,
            Pending = result.Pending || mapping.Pending,
            RetryAll = result.RetryAll || mapping.RetryAll, Retries = result.Retries.Concat(mapping.Retries).ToArray(),
            Notices = result.Notices.Concat(mapping.Notices.Select(n => "目录映射：" + n)).ToArray() };
    }
}
