using DesktopUp.Application;

namespace DesktopUp.Infrastructure;

// Compatibility entry for existing tools/tests. Production hosts compose the use cases directly.
public sealed class PosterContentReturn
{
    private readonly ContentReturnUseCase _returns;
    private readonly UninstallContentUseCase _uninstall;
    internal Action<string>? Checkpoint { get; set; }

    public PosterContentReturn(SqliteStateStore store, IShellDirectoryTransfer? shell = null, string? desktopDirectory = null)
    {
        var storage = new SqliteContentReturnStorage(store);
        _returns = new(storage, new LocalContentReturnFiles(store, desktopDirectory),
            shell ?? new ShellDirectoryTransfer(), stage => Checkpoint?.Invoke(stage));
        _uninstall = new(storage, _returns);
    }

    public string? OriginalPath(Guid itemId) => _returns.OriginalPath(itemId);
    public Task<PosterContentReturnResult> Execute(RemovePosterCommand command, nint owner,
        Func<bool> current, CancellationToken token = default) => _returns.Execute(command, owner, current, token);
    public IReadOnlyList<string> Reconcile() => _returns.Reconcile();
    public IReadOnlyList<string> RetainedLocations() => _uninstall.RetainedLocations();
    public UninstallContentInventory InspectUninstall() => _uninstall.Inspect();
    public Task<UninstallContentResult> ReturnForUninstall(UninstallContentInventory preview, nint owner,
        CancellationToken token = default, Action<string>? progress = null) => _uninstall.Return(preview, owner, token, progress);
}
