using DesktopUp.Domain;

namespace DesktopUp.Application;

// One semantic edit owns the whole poster aggregate; pointer preview never calls this command.
public sealed record ContentLayoutCommand(string RequestId, StateCommandSource Source, long ExpectedRevision,
    string TargetPosterId, StateReadSet ReadSet, long ExpectedDocumentVersion, long ExpectedItemSetVersion,
    ContentLayoutDocument DesiredDocument, bool ReplaceItemSet = false);

public static class ContentLayoutCommandPolicy
{
    public static StateMutationPlan Prepare(ContentLayoutCommand command, DesktopStateSnapshot state)
    {
        if (command.Source is not (StateCommandSource.Poster or StateCommandSource.ControlCenter))
            return StateMutationPlan.Reject(StateCommandStatus.PermissionDenied, "Content editing is not available from this surface.");
        var entry = state.Catalog.Posters.FirstOrDefault(p => p.Poster.PosterId == command.TargetPosterId);
        if (entry is null) return StateMutationPlan.Reject(StateCommandStatus.NotFound, "Poster no longer exists.");
        if (!command.ReadSet.Posters.TryGetValue(command.TargetPosterId, out long entityVersion) || entityVersion != entry.EntityVersion)
            return StateMutationPlan.Reject(StateCommandStatus.Conflict, "The complete target poster read version is required.");
        if (!ContentLayoutPolicy.TryRead(entry.Poster.ContentLayoutJson, command.TargetPosterId, out var current, out var error))
            return Invalid(error ?? "Unknown content layout is read-only.");
        var desired = command.DesiredDocument;
        if (ContentLayoutPolicy.Validate(desired) is { } invalid) return Invalid(invalid);
        if (desired.PosterId != command.TargetPosterId) return Invalid("Foreign layout owner.");
        try
        {
            var preferences = PosterPresentationPolicy.Resolve(entry.Poster).Parameters.Preferences ?? new();
            ValidateAuthoring(desired);
            ValidateAuthoring(desired, preferences.Scale, preferences.Spacing);
        }
        catch (ArgumentException failure) { return Invalid(failure.Message); }
        if (command.ExpectedDocumentVersion != current!.DocumentVersion || command.ExpectedItemSetVersion != current.ItemSetVersion)
            return StateMutationPlan.Reject(StateCommandStatus.Conflict, "Content layout or membership changed.");
        if (current.DocumentVersion == long.MaxValue || desired.DocumentVersion != current.DocumentVersion + 1)
            return Invalid("Content document must advance exactly one semantic version.");
        var before = current.Placements.ToDictionary(p => p.ItemId, StringComparer.Ordinal);
        var after = desired.Placements.ToDictionary(p => p.ItemId, StringComparer.Ordinal);
        bool membershipChanged = !before.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(after.Keys);
        if (membershipChanged && !command.ReplaceItemSet) return Invalid("Membership replacement requires an explicit operation.");
        if (membershipChanged && current.ItemSetVersion == long.MaxValue ||
            desired.ItemSetVersion != current.ItemSetVersion + (membershipChanged ? 1 : 0))
            return Invalid("Invalid membership version.");
        foreach (var placement in desired.Placements)
        {
            if (!before.TryGetValue(placement.ItemId, out var old))
            {
                if (placement.Version != 1) return Invalid("New membership starts at version one.");
                continue;
            }
            bool changed = placement with { Version = old.Version } != old;
            if (changed && old.Version == long.MaxValue || placement.Version != old.Version + (changed ? 1 : 0))
                return Invalid("Only changed authored placements advance their version.");
        }
        // A version-only proposal is not an edit. Do not materialize the virtual legacy layout on read/no-op.
        if (ContentLayoutPolicy.Serialize(desired with { DocumentVersion = current.DocumentVersion }) == ContentLayoutPolicy.Serialize(current))
            return new();
        return new() { Updates = new[] { entry.Poster with { ContentLayoutJson = ContentLayoutPolicy.Serialize(desired) } } };
    }

    public static string? RemapStored(PosterDefinition source, string newPosterId)
    {
        if (source.ContentLayoutJson is null) return null;
        if (!ContentLayoutPolicy.TryRead(source.ContentLayoutJson, source.PosterId, out var document, out var error))
            throw new ArgumentException("Cannot clone an unknown content layout without losing its identity contract: " + error);
        ValidateAuthoring(document!);
        return ContentLayoutPolicy.Serialize(ContentLayoutPolicy.Remap(document!, newPosterId));
    }
    // Canonical logical board only; a current monitor/scroll/effective span never enters persistence.
    public static void ValidateAuthoring(ContentLayoutDocument document, double scale = 1, double spacing = 1) => ContentLayoutSolver.Resolve(document,
        new(document.MaximumColumns * (document.BaseCellDip * scale + document.BaseGapDip * spacing) - document.BaseGapDip * spacing,
            1024, Scale: scale, Spacing: spacing));
    private static StateMutationPlan Invalid(string error) => StateMutationPlan.Reject(StateCommandStatus.ValidationFailed, error);
}
