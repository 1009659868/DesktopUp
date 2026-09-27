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
            ValidateAuthoring(current!);
            ValidateAuthoring(current!, preferences.Scale, preferences.Spacing);
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
        if (ValidateTransition(current, desired, membershipChanged && command.ReplaceItemSet) is { } transitionError)
            return Invalid(transitionError);
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
    public static void ValidateAuthoring(ContentLayoutDocument document, double scale = 1, double spacing = 1)
        => ContentLayoutCapabilities.RequireApplicable(document, scale, spacing);

    // Validate the old and new meaning together. Validating only the destination would allow a
    // caller to fabricate automatic return intent, or to erase it with a valid manual document.
    private static string? ValidateTransition(ContentLayoutDocument before, ContentLayoutDocument after, bool replaceMembers)
    {
        if (before.SchemaVersion < 3 && before.Mode == ContentLayoutMode.Automatic && after.SchemaVersion >= 3 &&
            before.Placements.Any(p => p.PinnedIndex.HasValue))
            return "Legacy dormant automatic pins require an explicit conversion before upgrading.";
        if (replaceMembers)
        {
            // The DEMO replacement operation replaces a composition, not a hidden constraint
            // conversion. A future real-item operation must supply its own preservation contract.
            static bool Ordinary(ContentLayoutDocument d) => !d.Placements.Any(p => p.FixedPosition) && d.Regions.Count == 1 &&
                d.Regions[0] == new ContentLayoutRegion("main", 0) && d.ReservedSpaces.Count == 0;
            if (!Ordinary(before) || !Ordinary(after))
                return "Membership replacement cannot discard preserved regions, hard positions or reserved spaces.";
            return null;
        }
        var previous = before.Placements.ToDictionary(p => p.ItemId, StringComparer.Ordinal);
        if (before.SchemaVersion < 3 && before.Mode == ContentLayoutMode.Automatic && after.SchemaVersion < 3 &&
            after.Placements.Any(p => previous[p.ItemId].PinnedIndex != p.PinnedIndex))
            return "Legacy automatic dormant pins must remain unchanged until an explicit compatibility conversion.";
        if (!before.Regions.SequenceEqual(after.Regions) || !before.ReservedSpaces.SequenceEqual(after.ReservedSpaces) ||
            after.Placements.Any(p => previous[p.ItemId].RegionId != p.RegionId))
            return "This content operation must preserve all regions, reserved-space intent and member region ownership.";
        if (before.Mode != ContentLayoutMode.Manual && after.Mode != ContentLayoutMode.Manual && before.Mode != after.Mode)
            return "Changing legacy automatic mode semantics requires an explicit compatibility conversion.";
        if (before.Mode == ContentLayoutMode.Automatic && after.Mode == ContentLayoutMode.Automatic &&
            before.SchemaVersion >= 3 && after.SchemaVersion < 3 && after.Placements.Any(p => p.PinnedIndex.HasValue))
            return "Changing schema cannot activate or deactivate automatic order pins.";
        if (after.Placements.Any(p => previous[p.ItemId].FixedPosition != p.FixedPosition))
            return "Hard-position conversion requires a separately defined compatibility operation.";
        if (before.Mode != ContentLayoutMode.Manual && after.Mode == ContentLayoutMode.Manual)
        {
            if (after.SavedAutomaticMode != before.Mode || after.Placements.Any(p =>
                p.SavedAutomaticState != AutomaticState(previous[p.ItemId]) || p.AutomaticSizeBasis != previous[p.ItemId].AutomaticSizeBasis))
                return "Entering manual mode must preserve every member's original automatic intent.";
        }
        else if (before.Mode == ContentLayoutMode.Manual && after.Mode == ContentLayoutMode.Manual)
        {
            if (after.SavedAutomaticMode != before.SavedAutomaticMode ||
                after.Placements.Any(p => p.SavedAutomaticState != previous[p.ItemId].SavedAutomaticState ||
                    p.AutomaticSizeBasis != previous[p.ItemId].AutomaticSizeBasis))
                return "Manual edits cannot rewrite or erase automatic return intent.";
        }
        else if (before.Mode == ContentLayoutMode.Manual)
        {
            if (before.SavedAutomaticMode is null || after.Mode != before.SavedAutomaticMode ||
                after.Placements.Any(p => AutomaticState(p) != previous[p.ItemId].SavedAutomaticState))
                return "Leaving manual mode requires the complete original automatic intent.";
        }
        else
        {
            foreach (var p in after.Placements)
            {
                var old = previous[p.ItemId];
                if (!old.ManualSize && !p.ManualSize && (old.ColumnSpan != p.ColumnSpan || old.RowSpan != p.RowSpan))
                    return "Changing automatic spans requires an explicit size override and its original basis.";
                if (!old.ManualSize && p.ManualSize && p.AutomaticSizeBasis != new ContentAutomaticSizeBasis(old.ColumnSpan, old.RowSpan))
                    return "A new size override must retain the original automatic spans.";
                if (old.ManualSize && p.ManualSize && old.AutomaticSizeBasis != p.AutomaticSizeBasis)
                    return "A size override cannot rewrite its automatic size basis.";
                if (old.ManualSize && !p.ManualSize && (old.AutomaticSizeBasis is not { } basis ||
                    p.ColumnSpan != basis.ColumnSpan || p.RowSpan != basis.RowSpan || p.AutomaticSizeBasis is not null))
                    return "Restoring automatic size requires its known original spans.";
            }
        }
        return null;
    }

    private static ContentAutomaticPlacementState AutomaticState(ContentPlacement p) =>
        new(p.Order, p.ColumnSpan, p.RowSpan, p.ManualSize, p.PinnedIndex, p.Column, p.Row, p.FixedPosition)
        { AutomaticSizeBasis = p.AutomaticSizeBasis };
    private static StateMutationPlan Invalid(string error) => StateMutationPlan.Reject(StateCommandStatus.ValidationFailed, error);
}
