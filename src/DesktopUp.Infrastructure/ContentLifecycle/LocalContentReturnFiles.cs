using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Infrastructure;

public sealed class LocalContentReturnFiles(SqliteStateStore store, string? desktopDirectory = null) : IContentReturnFiles
{
    public string DesktopDirectory => desktopDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    public string Identity(string path) => LocalTransferFiles.Identity(path);

    public string? UsableDestination(string? path, ContentCatalogSnapshot catalog)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            path = LocalTransferFiles.Normalize(path);
            if (store.ProtectedTransferPath(path, true) || catalog.Collections.Any(c => c.Kind == ContentCollectionKind.Managed && Path.GetDirectoryName(c.StorageLocator) is { } root &&
                (string.Equals(path, root, StringComparison.OrdinalIgnoreCase) || LocalTransferFiles.Contains(root, path)))) return null;
            using var lease = LocalTransferFiles.LockDirectory(path);
            return path;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { return null; }
    }
}
