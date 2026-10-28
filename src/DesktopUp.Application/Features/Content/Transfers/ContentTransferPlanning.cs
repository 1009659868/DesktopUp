using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DesktopUp.Domain;

namespace DesktopUp.Application;

// Facts must be resolved by the future filesystem adapter under the transfer lease.
// No Path.GetFullPath/string-prefix comparison here can authorize a disk operation.
public sealed record TransferSourceFact(string ObjectIdentity, string Locator, string VersionWitness,
    IReadOnlyList<string> AncestorIdentities, IReadOnlyList<Guid> DescendantContentIds,
    long ImpactContentVersion, bool SafeToMove);
public sealed record TransferDestinationFact(string ParentIdentity, string ParentVersionWitness,
    string Locator, IReadOnlyList<string> AncestorIdentities, string? ExistingObjectIdentity,
    bool IsActualDesktop, bool SafeToWrite);
public sealed record ContentTransferEntry(ContentItem Item, TransferSourceFact Source,
    TransferDestinationFact Destination, Guid? OriginPosterItemId = null);
public sealed record ContentTransferRequest(Guid OperationId, long RuntimeGeneration,
    string PosterId, ContentDropRoute Route, ContentDropModifiers Modifiers,
    ManagedStorageRoot? Root, IReadOnlyList<ContentTransferEntry> Entries, int FormatVersion = 1)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IncomingContentPlacement? Placement { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool KeepSourceUntilCommit { get; init; }
}
public sealed record ContentTransferSnapshot(ContentCatalogSnapshot Catalog, long ContentVersion,
    IReadOnlyDictionary<string, long> PosterVersions, bool PosterAcceptsTransfer, bool TargetContainsDemo = true);
public enum TransferPlanStatus { Ready, Invalid, Unsupported, Conflict, Collision, Unsafe, Unavailable }
public sealed record TransferPlanResult(TransferPlanStatus Status, ContentTransferPlan? Plan = null, string? Reason = null);
public sealed record PlannedContentTransfer(ContentTransferEntry Entry, bool SameLocation,
    IReadOnlyList<Guid> AffectedContentIds, IReadOnlyList<string> AffectedPosterIds, bool RequiresConfirmation);
public sealed class ContentTransferPlan
{
    internal ContentTransferPlan(ContentTransferRequest request, StateReadSet readSet,
        IReadOnlyList<PlannedContentTransfer> entries, string fingerprint)
    { Request = request; ReadSet = readSet; Entries = entries; Fingerprint = fingerprint; }
    public ContentTransferRequest Request { get; }
    public StateReadSet ReadSet { get; }
    public IReadOnlyList<PlannedContentTransfer> Entries { get; }
    public string Fingerprint { get; }
    public bool RequiresConfirmation => Entries.Any(x => x.RequiresConfirmation);
}
public enum TransferAdmission { ReadyForJournal, Repreview, ConfirmationRequired }
public sealed record TransferRelationDelta(IReadOnlyList<Guid> RelocateContentIds,
    IReadOnlyList<Guid> DetachPosterItemIds, IReadOnlyList<Guid> PreservePosterItemIds);

public static class ContentTransferPlanning
{
    public const int MaximumBatch = 100;
    public static TransferPlanResult Prepare(ContentTransferRequest request, ContentTransferSnapshot state)
    {
        try { return PrepareCore(request, state); }
        catch (ArgumentException ex) { return new(TransferPlanStatus.Invalid, Reason: ex.Message); }
    }

    private static TransferPlanResult PrepareCore(ContentTransferRequest request, ContentTransferSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(state);
        if (request.FormatVersion != 1 || request.OperationId == Guid.Empty || request.RuntimeGeneration < 1 ||
            request.Route is not (ContentDropRoute.IntoManagedStorage or ContentDropRoute.ToDesktop) ||
            state.ContentVersion < 0 || state.PosterVersions is null ||
            !state.PosterVersions.TryGetValue(request.PosterId, out long posterVersion) || posterVersion < 1 ||
            request.Entries is null || request.Entries.Count is < 1 or > MaximumBatch)
            return Reject(TransferPlanStatus.Invalid, "Unknown format or invalid transfer context.");
        if (!state.PosterAcceptsTransfer)
            return Reject(TransferPlanStatus.Unavailable, "Poster is locked, hidden, read-only or otherwise unavailable for this gesture.");
        var catalog = ContentCatalogPolicy.Freeze(state.Catalog);
        var binding = catalog.Bindings.SingleOrDefault(b => b.PosterId == request.PosterId);
        if (binding is not null && catalog.Collections.Single(c => c.Id == binding.CollectionId).Kind != ContentCollectionKind.Reference)
            return Reject(TransferPlanStatus.Unsupported, "First batch keeps reference views separate from managed storage membership.");
        var entries = request.Entries.Select(Freeze).ToArray();
        request = request with { Entries = Array.AsReadOnly(entries) };
        if (entries.Select(e => e.Item.Id).Distinct().Count() != entries.Length ||
            entries.Select(e => e.Source.ObjectIdentity).Distinct(StringComparer.Ordinal).Count() != entries.Length ||
            entries.Select(e => e.Destination.Locator).Distinct(StringComparer.OrdinalIgnoreCase).Count() != entries.Length)
            return Reject(TransferPlanStatus.Invalid, "Duplicate physical source or destination; group aliases before planning.");
        // A selected directory and its selected descendant cannot be moved twice in one batch.
        if (entries.Any(e => entries.Any(other => !ReferenceEquals(e, other) &&
            e.Source.AncestorIdentities.Contains(other.Source.ObjectIdentity, StringComparer.Ordinal))))
            return Reject(TransferPlanStatus.Unsafe, "Overlapping sources must be normalized before planning.");
        var planned = new List<PlannedContentTransfer>();
        var requiredPosters = new HashSet<string>(StringComparer.Ordinal) { request.PosterId };
        foreach (var entry in entries)
        {
            var decision = ContentDropIntentPolicy.Resolve(entry.Item, request.Route, request.Modifiers,
                request.Root, entry.Destination.IsActualDesktop);
            if (!decision.Allowed) return Reject(TransferPlanStatus.Unsupported, decision.Denial.ToString());
            var source = entry.Source; var destination = entry.Destination;
            if (!source.SafeToMove || !destination.SafeToWrite ||
                source.ImpactContentVersion != state.ContentVersion ||
                source.Locator != entry.Item.Target.Locator)
                return Reject(TransferPlanStatus.Unsafe, "Unresolved or stale provider facts.");
            if (destination.ParentIdentity == source.ObjectIdentity ||
                destination.AncestorIdentities.Contains(source.ObjectIdentity, StringComparer.Ordinal))
                return Reject(TransferPlanStatus.Unsafe, "A directory cannot be moved into itself.");
            if (request.Route == ContentDropRoute.IntoManagedStorage &&
                destination.ParentIdentity != request.Root!.ObjectIdentity &&
                !destination.AncestorIdentities.Contains(request.Root.ObjectIdentity, StringComparer.Ordinal))
                return Reject(TransferPlanStatus.Unsafe, "Destination is outside the resolved managed root.");
            bool same = StringComparer.OrdinalIgnoreCase.Equals(source.Locator, destination.Locator);
            // Two paths to the same object (including hard links) are not evidence that a move happened.
            if (same && destination.ExistingObjectIdentity != source.ObjectIdentity)
                return Reject(TransferPlanStatus.Conflict, "Same path no longer identifies the source.");
            if (!same && destination.ExistingObjectIdentity is not null)
                return Reject(TransferPlanStatus.Collision, "Choose and confirm a new vacant destination; never overwrite.");

            var known = catalog.Items.SingleOrDefault(i => i.Id == entry.Item.Id);
            // Move is an ephemeral provider capability, verified again by the disk adapter.
            if (known is not null && known != entry.Item &&
                (known with { Capabilities = known.Capabilities | ContentCapability.Move }) != entry.Item)
                return Reject(TransferPlanStatus.Conflict, "Content changed since payload capture.");
            if (request.Route == ContentDropRoute.ToDesktop)
            {
                var relation = catalog.PosterItems.SingleOrDefault(p => p.Id == entry.OriginPosterItemId);
                if (known is null || relation is null || relation.PosterId != request.PosterId ||
                    catalog.Memberships.Single(m => m.Id == relation.MembershipId).ContentItemId != entry.Item.Id)
                    return Reject(TransferPlanStatus.Conflict, "The original card/relationship no longer exists.");
            }
            else if (entry.OriginPosterItemId is not null)
                return Reject(TransferPlanStatus.Invalid, "External input cannot impersonate an internal card.");

            // Same-source independent entities also share physical movement consequences.
            var affected = catalog.Items.Where(i => i.Source == entry.Item.Source ||
                i.Source.ProviderId == entry.Item.Source.ProviderId &&
                string.Equals(i.Target.Locator, entry.Item.Target.Locator, StringComparison.OrdinalIgnoreCase)).Select(i => i.Id)
                .Concat(source.DescendantContentIds).Distinct().Order().ToArray();
            if (source.DescendantContentIds.Count > 0 && entry.Item.Kind != ContentKind.Folder ||
                source.DescendantContentIds.Any(id => catalog.Items.All(i => i.Id != id)))
                return Reject(TransferPlanStatus.Invalid, "Invalid folder descendant impact.");
            var memberIds = catalog.Memberships.Where(m => affected.Contains(m.ContentItemId)).Select(m => m.Id).ToHashSet();
            var affectedPosters = catalog.PosterItems.Where(p => memberIds.Contains(p.MembershipId))
                .Select(p => p.PosterId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            requiredPosters.UnionWith(affectedPosters);
            bool alreadyManaged = catalog.Memberships.Any(m => m.ContentItemId == entry.Item.Id &&
                catalog.Collections.Any(c => c.Id == m.CollectionId && c.Kind == ContentCollectionKind.Managed));
            bool legacyReference = entry.OriginPosterItemId is not null && !alreadyManaged;
            // Shared aliases/descendants must be visible even if they are in the same poster.
            bool shared = affected.Length > 1 || catalog.PosterItems.Count(p => memberIds.Contains(p.MembershipId)) > 1;
            planned.Add(new(entry, same, Array.AsReadOnly(affected), Array.AsReadOnly(affectedPosters), false)); // DEC-047: drop expresses intent.
        }
        if (requiredPosters.Any(id => !state.PosterVersions.TryGetValue(id, out long version) || version < 1))
            return Reject(TransferPlanStatus.Conflict, "Missing affected poster version.");
        var reads = new StateReadSet(new ReadOnlyDictionary<string, long>(
            requiredPosters.ToDictionary(id => id, id => state.PosterVersions[id], StringComparer.Ordinal)),
            ContentVersion: state.ContentVersion).Freeze();
        return new(TransferPlanStatus.Ready, new(request, reads, planned.AsReadOnly(), Fingerprint(request, reads, planned)));
    }

    // A pure admission result is only permission to prepare a durable journal, never to move files.
    public static TransferAdmission Revalidate(ContentTransferPlan preview, ContentTransferRequest current,
        ContentTransferSnapshot state, string? confirmedFingerprint)
    {
        var refreshed = Prepare(current, state);
        if (refreshed.Plan is not { } plan || plan.Fingerprint != preview.Fingerprint) return TransferAdmission.Repreview;
        return preview.RequiresConfirmation && confirmedFingerprint != preview.Fingerprint
            ? TransferAdmission.ConfirmationRequired : TransferAdmission.ReadyForJournal;
    }

    // Result proposal only; future repository must re-check read set and record per-item receipts.
    // All aliases keep stable IDs. No layout/icon mutation is part of this delta.
    public static TransferRelationDelta DescribeVerifiedMove(ContentTransferPlan plan, int index, ContentCatalogSnapshot catalog)
    {
        var entry = plan.Entries[index];
        if (entry.SameLocation) throw new InvalidOperationException("No-op needs a separate explicit detach command.");
        var members = catalog.Memberships.Where(m => entry.AffectedContentIds.Contains(m.ContentItemId)).Select(m => m.Id).ToHashSet();
        Guid[] detach = plan.Request.Route == ContentDropRoute.ToDesktop && entry.Entry.OriginPosterItemId is { } id ? [id] : [];
        return new(entry.AffectedContentIds, Array.AsReadOnly(detach),
            Array.AsReadOnly(catalog.PosterItems.Where(p => members.Contains(p.MembershipId) && !detach.Contains(p.Id)).Select(p => p.Id).ToArray()));
    }

    private static ContentTransferEntry Freeze(ContentTransferEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ContentCatalogPolicy.ValidateItem(entry.Item);
        ArgumentNullException.ThrowIfNull(entry.Source);
        ArgumentNullException.ThrowIfNull(entry.Destination);
        var s = entry.Source; var d = entry.Destination;
        if (new[] { s.ObjectIdentity, s.Locator, s.VersionWitness, d.ParentIdentity, d.ParentVersionWitness, d.Locator }
            .Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 32768 || x.Contains('\0')) ||
            s.AncestorIdentities is null || d.AncestorIdentities is null || s.DescendantContentIds is null ||
            s.AncestorIdentities.Count > 1024 || d.AncestorIdentities.Count > 1024 ||
            s.DescendantContentIds.Count > ContentCatalogPolicy.MaximumEntries ||
            s.AncestorIdentities.Concat(d.AncestorIdentities).Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 8192 || x.Contains('\0')))
            throw new ArgumentException("Invalid/budget-exceeding resolved identities.");
        return entry with { Source = s with { AncestorIdentities = Array.AsReadOnly(s.AncestorIdentities.ToArray()),
            DescendantContentIds = Array.AsReadOnly(s.DescendantContentIds.ToArray()) },
            Destination = d with { AncestorIdentities = Array.AsReadOnly(d.AncestorIdentities.ToArray()) } };
    }

    private static string Fingerprint(ContentTransferRequest request, StateReadSet reads, IEnumerable<PlannedContentTransfer> entries)
    {
        var text = new StringBuilder();
        void Add(object? value) { string s = Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""; text.Append(s.Length).Append(':').Append(s); }
        Add(request.FormatVersion); Add(request.OperationId); Add(request.RuntimeGeneration); Add(request.PosterId); Add(request.Route); Add(request.Modifiers);
        if (request.KeepSourceUntilCommit || request.Placement is not null)
        { Add("incoming-placement-v1"); Add(request.KeepSourceUntilCommit); Add(StateRequestFingerprint.Compute(request.Placement)); }
        Add(request.Root is not null);
        if (request.Root is { } root)
        { Add(root.Id); Add(root.Locator); Add(root.ObjectIdentity); Add(root.Version); Add(root.FormatVersion); Add(root.Location); Add(root.Available); Add(root.Writable); }
        Add(reads.ContentVersion);
        Add(reads.Posters.Count);
        foreach (var pair in reads.Posters) { Add(pair.Key); Add(pair.Value); }
        Add(request.Entries.Count);
        foreach (var p in entries)
        {
            var e = p.Entry; var i = e.Item; var s = e.Source; var d = e.Destination;
            Add(i.Id); Add(i.Version); Add(i.Source.ProviderId); Add(i.Source.Key); Add(i.Kind); Add(i.Target.Kind);
            Add(i.Target.Locator); Add(i.Target.Arguments); Add(i.Target.WorkingDirectory); Add(i.DisplayName); Add(i.Capabilities); Add(i.Availability);
            Add(e.OriginPosterItemId); Add(s.ObjectIdentity); Add(s.Locator); Add(s.VersionWitness); Add(s.ImpactContentVersion);
            Add(d.ParentIdentity); Add(d.ParentVersionWitness); Add(d.Locator); Add(d.ExistingObjectIdentity); Add(d.IsActualDesktop);
            Add(s.AncestorIdentities.Count); foreach (var x in s.AncestorIdentities) Add(x);
            Add(d.AncestorIdentities.Count); foreach (var x in d.AncestorIdentities) Add(x);
            Add(p.AffectedContentIds.Count); foreach (var x in p.AffectedContentIds) Add(x);
            Add(p.SameLocation); Add(p.RequiresConfirmation);
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }
    private static TransferPlanResult Reject(TransferPlanStatus status, string reason) => new(status, Reason: reason);
}

