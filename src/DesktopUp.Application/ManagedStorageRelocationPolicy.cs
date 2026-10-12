using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text.Json;
using DesktopUp.Domain;

namespace DesktopUp.Application;

// Resolved adapter facts, not authority to touch a path. All roots/collections of the current
// profile are covered by an explicit whole-storage migration; legacy roots are never auto-merged.
public sealed record StorageRootInventory(ManagedStorageRoot Root, string OwnerProfileIdentity,
    string OwnershipWitness, IReadOnlyList<Guid> CollectionIds);
public sealed record StorageCollectionInventory(Guid CollectionId, Guid RootId, string DirectoryIdentity,
    string TreeWitness, long FileCount, long ByteCount, IReadOnlyList<Guid> AffectedContentIds, bool Complete);
public sealed record StorageRelocationReads(string ProfileIdentity, long SelectionVersion, long RootSetVersion,
    long ContentVersion, long RuntimeGeneration, IReadOnlyDictionary<string, long> PosterVersions);
public enum StorageDestinationRelation { Disjoint, SameLocation, InsideSource, ContainsSource, Unknown }
public sealed record StorageRelocationRequest(Guid OperationId, ManagedStorageRoot Destination,
    string DestinationWitness, StorageDestinationRelation Relation, bool TargetEmpty, bool ResolvedSafe,
    IReadOnlyList<StorageCollectionInventory> Inventories, int FormatVersion = 1);
public sealed class StorageRelocationPlan
{
    internal StorageRelocationPlan(StorageRelocationRequest request, StorageRelocationReads reads,
        IReadOnlyList<StorageRootInventory> roots, ContentCatalogSnapshot catalog, IReadOnlyList<Guid> items,
        IReadOnlyList<string> posters, string fingerprint)
    { Request = request; Reads = reads; Roots = roots; Catalog = catalog; AffectedContentIds = items; AffectedPosterIds = posters; Fingerprint = fingerprint; }
    public StorageRelocationRequest Request { get; }
    public StorageRelocationReads Reads { get; }
    public IReadOnlyList<StorageRootInventory> Roots { get; }
    public ContentCatalogSnapshot Catalog { get; }
    public IReadOnlyList<Guid> AffectedContentIds { get; }
    public IReadOnlyList<string> AffectedPosterIds { get; }
    public string Fingerprint { get; }
    public long FileCount => Request.Inventories.Sum(x => x.FileCount);
    public long ByteCount => Request.Inventories.Sum(x => x.ByteCount);
}

public enum StorageRelocationPhase { Prepared, IntentDurable, Copying, TargetsVerified, IndexCommitted, CleanupPending, Completed }
public enum StorageRelocationAction
{
    ConfirmLocationChange, WriteIntent, CopyAndVerifyKeepingSources, AwaitVerification, CommitOneTransaction,
    KeepOldLocation, RepreviewKeepingSources, FinishVerifiedOldCopyCleanup, KeepNewLocationAndOldCopies, Done, PreserveAndInspect
}
public sealed record StorageRelocationCommit(Guid OperationId, string PlanFingerprint, Guid ActiveRootId, long CommittedRevision);
public sealed record StorageRelocationCheck(string CurrentPlanFingerprint, string? DurableIntentFingerprint,
    bool Cancelled, bool AllTargetsVerified, bool SourcesStillMatch, bool OwnedOldCopiesVerified,
    bool NewLocationStillVerified, Guid? ActiveRootId, StorageRelocationCommit? Commit = null)
{
    public string? ConfirmedPlanFingerprint { get; init; }
}

public static class ManagedStorageRelocationPolicy
{
    public static StorageRelocationPlan Prepare(StorageRelocationRequest request, StorageRelocationReads reads,
        IReadOnlyList<StorageRootInventory> roots, ContentCatalogSnapshot catalog)
    {
        Require(request.FormatVersion == 1 && request.OperationId != Guid.Empty && reads.SelectionVersion >= 0 &&
            reads.RootSetVersion >= 0 && reads.ContentVersion >= 0 && reads.RuntimeGeneration > 0 && Text(reads.ProfileIdentity), "Invalid migration context.");
        Require(roots.Count <= 256 && request.Inventories.Count <= ContentCatalogPolicy.MaximumEntries,
            "Migration inventory exceeds contract budget.");
        catalog = ContentCatalogPolicy.Freeze(catalog);
        roots = Array.AsReadOnly(roots.Select(r => r with { CollectionIds = Array.AsReadOnly(r.CollectionIds.Order().ToArray()) })
            .OrderBy(r => r.Root.Id).ToArray());
        request = request with { Inventories = Array.AsReadOnly(request.Inventories.Select(i => i with
            { AffectedContentIds = Array.AsReadOnly(i.AffectedContentIds.Order().ToArray()) }).OrderBy(i => i.CollectionId).ToArray()) };
        reads = reads with { PosterVersions = new ReadOnlyDictionary<string, long>(
            new SortedDictionary<string, long>(reads.PosterVersions.ToDictionary(x => x.Key, x => x.Value), StringComparer.Ordinal)) };
        Require(reads.PosterVersions.All(x => Text(x.Key) && x.Value > 0), "Invalid poster read set.");
        Require(ManagedStoragePolicy.Evaluate(request.Destination) == ManagedStorageDecision.Ready &&
            Text(request.DestinationWitness) && request.ResolvedSafe && request.TargetEmpty &&
            request.Relation == StorageDestinationRelation.Disjoint, "Destination must be verified, disjoint and empty; never overwrite or adopt foreign files.");
        Require(roots.Select(x => x.Root.Id).Distinct().Count() == roots.Count &&
            roots.Select(x => x.Root.ObjectIdentity).Distinct(StringComparer.Ordinal).Count() == roots.Count && roots.All(r =>
            ManagedStoragePolicy.Evaluate(r.Root) == ManagedStorageDecision.Ready &&
            r.OwnerProfileIdentity == reads.ProfileIdentity && Text(r.OwnershipWitness) &&
            r.Root.Id != request.Destination.Id && r.Root.ObjectIdentity != request.Destination.ObjectIdentity &&
            !string.Equals(r.Root.Locator, request.Destination.Locator, StringComparison.OrdinalIgnoreCase)), "Source roots must belong to this profile and differ from destination.");
        var managed = catalog.Collections.Where(x => x.Kind == ContentCollectionKind.Managed).Select(x => x.Id).ToHashSet();
        var registered = roots.SelectMany(r => r.CollectionIds).ToArray();
        Require(registered.Distinct().Count() == registered.Length && managed.SetEquals(registered), "All managed collections must have exactly one registered owner.");
        Require(request.Inventories.Select(i => i.CollectionId).Distinct().Count() == request.Inventories.Count &&
            request.Inventories.Select(i => i.DirectoryIdentity).Distinct(StringComparer.Ordinal).Count() == request.Inventories.Count &&
            managed.SetEquals(request.Inventories.Select(i => i.CollectionId)), "Inventory must cover every managed collection, including empty ones.");
        var itemIds = catalog.Items.Select(i => i.Id).ToHashSet();
        foreach (var inventory in request.Inventories)
        {
            Require(inventory.Complete && inventory.FileCount >= 0 && inventory.ByteCount >= 0 &&
                Text(inventory.DirectoryIdentity) && Text(inventory.TreeWitness) &&
                roots.Any(r => r.Root.Id == inventory.RootId && r.CollectionIds.Contains(inventory.CollectionId)), "Incomplete, unowned or invalid collection inventory.");
            Require(inventory.AffectedContentIds.Count <= ContentCatalogPolicy.MaximumEntries &&
                inventory.AffectedContentIds.Distinct().Count() == inventory.AffectedContentIds.Count &&
                inventory.AffectedContentIds.All(itemIds.Contains), "Invalid resolved alias/descendant set.");
            Require(catalog.Memberships.Where(m => m.CollectionId == inventory.CollectionId)
                .All(m => inventory.AffectedContentIds.Contains(m.ContentItemId)), "Managed contents cannot disappear from migration impact.");
        }
        // Alias IDs may share a physical entry, but one item cannot be assigned to two collections.
        var affected = request.Inventories.SelectMany(i => i.AffectedContentIds).ToArray();
        Require(affected.Distinct().Count() == affected.Length, "Overlapping physical inventories must be resolved before migration.");
        try { _ = request.Inventories.Sum(x => x.ByteCount); _ = request.Inventories.Sum(x => x.FileCount); }
        catch (OverflowException) { throw new ArgumentException("Inventory totals overflow."); }
        var members = catalog.Memberships.Where(m => affected.Contains(m.ContentItemId)).Select(m => m.Id).ToHashSet();
        var posters = catalog.PosterItems.Where(p => members.Contains(p.MembershipId)).Select(p => p.PosterId).Distinct().Order(StringComparer.Ordinal).ToArray();
        Require(posters.All(reads.PosterVersions.ContainsKey), "Every affected poster must participate in the read set.");
        reads = reads with { PosterVersions = new ReadOnlyDictionary<string, long>(
            new SortedDictionary<string, long>(posters.ToDictionary(p => p, p => reads.PosterVersions[p]), StringComparer.Ordinal)) };
        string fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { request, reads, roots, catalog })));
        return new(request, reads, roots, catalog, Array.AsReadOnly(affected.Order().ToArray()), Array.AsReadOnly(posters), fingerprint);
    }

    // A pure next-action proposal. The next D2 batch must supply durable receipts, hold leases,
    // perform the complete CAS and commit root/collection/alias locators in ONE transaction.
    public static StorageRelocationAction Decide(StorageRelocationPlan plan, StorageRelocationPhase phase, StorageRelocationCheck check)
    {
        if (!Enum.IsDefined(phase)) return StorageRelocationAction.PreserveAndInspect;
        if (check.Commit is { } receipt)
        {
            if (receipt.OperationId != plan.Request.OperationId || receipt.PlanFingerprint != plan.Fingerprint ||
                receipt.ActiveRootId != plan.Request.Destination.Id || receipt.CommittedRevision < 1)
                return StorageRelocationAction.PreserveAndInspect;
            // Durable commit wins even if the process died before saving its phase. Never roll
            // back an already-published root, including on cancellation or a new runtime epoch.
            if (check.ActiveRootId != receipt.ActiveRootId) return StorageRelocationAction.PreserveAndInspect;
            if (!check.NewLocationStillVerified)
                return StorageRelocationAction.KeepNewLocationAndOldCopies;
            if (phase == StorageRelocationPhase.Completed) return StorageRelocationAction.Done;
            return check.Cancelled || !check.SourcesStillMatch || !check.OwnedOldCopiesVerified
                ? StorageRelocationAction.KeepNewLocationAndOldCopies : StorageRelocationAction.FinishVerifiedOldCopyCleanup;
        }
        if (phase >= StorageRelocationPhase.IndexCommitted) return StorageRelocationAction.PreserveAndInspect;
        if (check.Cancelled) return StorageRelocationAction.KeepOldLocation;
        if (check.CurrentPlanFingerprint != plan.Fingerprint || !check.SourcesStillMatch)
            return StorageRelocationAction.RepreviewKeepingSources;
        if (check.ConfirmedPlanFingerprint != plan.Fingerprint) return StorageRelocationAction.ConfirmLocationChange;
        if (phase == StorageRelocationPhase.Prepared) return StorageRelocationAction.WriteIntent;
        if (check.DurableIntentFingerprint != plan.Fingerprint) return StorageRelocationAction.PreserveAndInspect;
        return phase switch
        {
            StorageRelocationPhase.IntentDurable or StorageRelocationPhase.Copying => StorageRelocationAction.CopyAndVerifyKeepingSources,
            StorageRelocationPhase.TargetsVerified => check.AllTargetsVerified
                ? StorageRelocationAction.CommitOneTransaction : StorageRelocationAction.AwaitVerification,
            _ => StorageRelocationAction.PreserveAndInspect
        };
    }

    private static bool Text(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 32768 && !value.Any(char.IsControl);
    private static void Require(bool condition, string reason) { if (!condition) throw new ArgumentException(reason); }
}
