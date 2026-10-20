using DesktopUp.Domain;

namespace DesktopUp.Application;

// Called inside the storage transaction so preparation sees the guarded current state.
public static class MappedDirectoryCreation
{
    public static MappedCreationPlan Prepare(CreatePosterCommand command, DesktopStateSnapshot state, MappedDirectoryScan scan)
    {
        string path = scan.Path;
        Guid? newCollection;
        IReadOnlyList<MappedContentObservation> observations;
        var create = DirectoryMappingPolicy.PreparePoster(command, state); if (create.Rejection is not null) return new(create, null, null);
        var poster = create.Creates.Single(); var collection = new Collection(Guid.NewGuid(), Path.GetFileName(path), ContentCollectionKind.Mapped, path, ContentStorageOwnership.Borrowed);
        newCollection = collection.Id;
        if (!ContentLayoutPolicy.TryRead(poster.ContentLayoutJson, poster.PosterId, out var emptyLayout, out var layoutError))
            throw new IOException(layoutError);
        poster = poster with { CollectionId = collection.Id.ToString(), ContentLayoutJson = ContentLayoutPolicy.Serialize(emptyLayout!) };
        var catalog = state.ContentCatalog with { Collections = state.ContentCatalog.Collections.Append(collection).ToArray(),
            Bindings = state.ContentCatalog.Bindings.Append(new ContentPosterBinding(poster.PosterId, collection.Id)).ToArray() };
        // Run the same reconciliation for the initial empty mapping, in the SAME transaction.
        var synthetic = new PosterStateSnapshot(state.Revision, state.Revision, poster, DateTimeOffset.UtcNow);
        var prepared = DirectoryMappingPolicy.Reconcile(state with { ContentCatalog = catalog,
            Catalog = state.Catalog with { Posters = state.Catalog.Posters.Append(synthetic).ToArray() } }, poster.PosterId, scan.Items, []);
        observations = prepared.Observations;
        return new(create with { Creates = [prepared.Plan.Updates.SingleOrDefault() ?? poster], ContentCatalog = prepared.Plan.ContentCatalog ?? catalog }, newCollection, new(scan.Identity, observations));
    }
}
