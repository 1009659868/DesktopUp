namespace DesktopUp.Application;

public sealed record ContentRepairPoster(string Id, string Title);
public sealed record ContentRepairTarget(string PosterId, string Title, Guid ItemId, string OriginalPath,
    long Revision, StateReadSet ReadSet, IReadOnlyList<ContentRepairPoster> AffectedPosters);
public sealed record ContentRepairPreview(ContentRepairTarget Target, LocalReferencePreview Source);
public enum ContentRepairStatus { SourceChanged, HostUnavailable, Completed }
public sealed record ContentRepairResult(ContentRepairStatus Status, StateCommandResult? Command = null);

/// <summary>Repair one existing reference, using its frozen relation versions and the existing atomic command.
/// The host owns picker/confirmation and supplies a final interaction/lifetime guard; no file is moved.</summary>
public sealed class ContentReferenceRepairUseCase(IContentReferenceSource sources, IStateCommandService commands)
{
    public static ContentRepairTarget? Capture(DesktopUpProjection basis, string posterId, string? layoutItemId)
    {
        if (layoutItemId is null || !basis.PostersById.TryGetValue(posterId, out var poster) ||
            ContentRelationPolicy.Resolve(basis.ContentCatalog, posterId, layoutItemId) is not { } relation) return null;
        var affected = ContentReferencePolicy.AffectedPosters(basis.ContentCatalog, relation.Item.Id);
        return new(posterId, poster.Poster.Title, relation.Item.Id, relation.Item.Target.Locator, basis.Revision,
            new StateReadSet(affected.ToDictionary(id => id, id => basis.PostersById[id].EntityVersion),
                ContentVersion: basis.ContentVersion).Freeze(),
            Array.AsReadOnly(affected.Select(id => new ContentRepairPoster(id, basis.PostersById[id].Poster.Title)).ToArray()));
    }

    public async Task<ContentRepairPreview> PreviewAsync(ContentRepairTarget target, string path,
        CancellationToken cancellation = default)
    {
        var source = await Task.Run(() => sources.Inspect(path), cancellation).ConfigureAwait(false);
        cancellation.ThrowIfCancellationRequested();
        return new(target, source);
    }

    public async Task<ContentRepairResult> ApplyAsync(ContentRepairPreview preview, Func<bool> hostCanApply,
        CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        if (preview.Source.Item is null) return new(ContentRepairStatus.SourceChanged);
        // Preserve the caller context: the final guard reads host interaction facts. No await may occur
        // between that guard and the command; authoritative versions are checked again by the transaction.
        if (!await Task.Run(() => sources.IsCurrent(preview.Source), cancellation))
            return new(ContentRepairStatus.SourceChanged);
        cancellation.ThrowIfCancellationRequested();
        if (!hostCanApply()) return new(ContentRepairStatus.HostUnavailable);
        var target = preview.Target;
        var command = new ContentReferenceCommand("reference-" + Guid.NewGuid().ToString("N"),
            StateCommandSource.ControlCenter, target.Revision, target.ReadSet, target.PosterId,
            [new(preview.Source.Item, ContentReferenceChoice.CreateIndependent)], false, target.ItemId);
        return new(ContentRepairStatus.Completed, commands.Execute(command));
    }
}
