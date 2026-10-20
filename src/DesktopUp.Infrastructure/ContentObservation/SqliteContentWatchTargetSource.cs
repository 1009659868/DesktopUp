using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Infrastructure;

// One full, validated snapshot shared by both target kinds, rather than two independent reads.
// Keep metadata/proof decoding too: schema verification alone does not replace those guards.
public sealed class SqliteContentWatchTargetSource(SqliteStateStore store) : IContentWatchTargetSource
{
    public IReadOnlyList<ContentWatchTarget> Read() => store.ExecuteRead((connection, transaction) =>
    {
        var catalog = SqliteStateCommandRepository.ReadSnapshot(connection, transaction).ContentCatalog;
        var bound = catalog.Bindings.Select(b => b.CollectionId).ToHashSet();
        var paths = new Dictionary<string, ContentWatchTarget>(StringComparer.OrdinalIgnoreCase);
        foreach (var location in SqliteTransferJournal.ReadStorageLocations(connection, transaction, catalog))
        {
            string path = location.Collection.StorageLocator!;
            paths.TryAdd(path, new(path, true));
        }
        foreach (var collection in catalog.Collections.Where(c => c.Kind == ContentCollectionKind.Mapped && bound.Contains(c.Id)))
        {
            string path = collection.StorageLocator!;
            paths.TryAdd(path, new(path, false)); // Registered managed storage retains recursive priority.
        }
        return (IReadOnlyList<ContentWatchTarget>)paths.Values.ToArray();
    });
}
