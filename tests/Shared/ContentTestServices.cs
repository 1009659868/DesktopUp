global using DesktopUp.Testing;
using DesktopUp.Application;
using DesktopUp.Infrastructure;

namespace DesktopUp.Testing;

// Explicit test composition; exercises the same use cases as Bootstrap.
internal static class ContentTestServices
{
    public static MappedDirectoryUseCase Mapping(SqliteStateStore store, TimeProvider? clock = null,
        Action? beforeCommit = null) =>
        new(new SqliteMappedDirectoryStorage(store), new LocalContentObservationFiles(), clock, beforeCommit);
    public static CrossPosterTransferUseCase CrossPoster(SqliteStateStore store,
        IShellDirectoryTransfer? shell = null, Action<string>? checkpoint = null) =>
        new(new CrossPosterTransfer(store), shell ?? new ShellDirectoryTransfer(), checkpoint);

    public static MappedIngressUseCase MappedIngress(SqliteStateStore store,
        IShellDirectoryTransfer? shell = null, Action<string>? checkpoint = null) =>
        new(new MappedDirectoryIngress(store), shell ?? new ShellDirectoryTransfer(), checkpoint);
}
