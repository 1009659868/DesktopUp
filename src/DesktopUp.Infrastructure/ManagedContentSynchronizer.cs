using DesktopUp.Application;

namespace DesktopUp.Infrastructure;

// Compatibility entry for existing callers/tests; the only observation loop lives in Application.
public sealed class ManagedContentSynchronizer
{
    private readonly ManagedContentSyncUseCase _useCase;
    private readonly LocalContentObservationFiles _files = new();
    internal Action? BeforeCommit { get; set; }
    internal Action<string>? Enumerating { get => _files.Enumerating; set => _files.Enumerating = value; }
    public ManagedContentSynchronizer(SqliteStateStore store, TimeProvider? clock = null) =>
        _useCase = new(new SqliteManagedContentObservationStorage(store), _files, clock, () => BeforeCommit?.Invoke());
    public ManagedContentSyncResult Reconcile(CancellationToken token = default) => _useCase.Reconcile(token);
}
