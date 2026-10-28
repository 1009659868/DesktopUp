using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class ManagedStorageRelocationPolicyTests
{
    private const string Profile = "profile-directory-id";
    private static readonly Guid ManagedId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid ManagedMemberId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid ExtraItemId = Guid.Parse("10000000-0000-0000-0000-000000000003");
    private static ManagedStorageRoot Root => ManagedStorageDropContractTests.Root();
    private static ContentCatalogSnapshot Catalog()
    {
        var original = ManagedStorageDropContractTests.Snapshot().Catalog;
        return original with
        {
            Items = original.Items.Append(ManagedStorageDropContractTests.Item() with { Id = ExtraItemId, Source = new("local", "outside"), Target = new(ContentTargetKind.FileSystem, "C:/outside-reference") }).ToArray(),
            Collections = original.Collections.Append(new(ManagedId, "owned", ContentCollectionKind.Managed, "D:/owned/collection", ContentStorageOwnership.Managed)).ToArray(),
            Memberships = original.Memberships.Append(new(ManagedMemberId, ManagedId, original.Items[0].Id, ContentMembershipOrigin.Source)).ToArray()
        };
    }
    private static StorageRelocationReads Reads => new(Profile, 3, 4, 5, 1, new Dictionary<string, long> { ["A"] = 10, ["B"] = 20, ["C"] = 30 });
    private static StorageRootInventory[] Roots => [new(Root, Profile, "root-owner-v1", [ManagedId])];
    private static StorageRelocationRequest Request => new(Guid.Parse("20000000-0000-0000-0000-000000000001"),
        Root with { Id = Guid.Parse("20000000-0000-0000-0000-000000000002"), Locator = "E:/new-storage", ObjectIdentity = "destination-id" },
        "destination-witness-1", StorageDestinationRelation.Disjoint, true, true,
        [new(ManagedId, Root.Id, "collection-identity", "complete-tree-v1", 1, 12, [ManagedStorageDropContractTests.ItemId], true)]);
    private static StorageRelocationPlan Plan() => ManagedStorageRelocationPolicy.Prepare(Request, Reads, Roots, Catalog());
    private static StorageRelocationCheck Check(StorageRelocationPlan p) => new(p.Fingerprint, p.Fingerprint, false, true, true, true, true, Root.Id) { ConfirmedPlanFingerprint = p.Fingerprint };
    private static StorageRelocationCommit Commit(StorageRelocationPlan p) => new(p.Request.OperationId, p.Fingerprint, p.Request.Destination.Id, 19);

    [Fact]
    public void ProposalIncludesSharedPostersButPreservesRelationsIconsAndUnmanagedReferences()
    {
        var catalog = Catalog(); var plan = ManagedStorageRelocationPolicy.Prepare(Request, Reads, Roots, catalog);
        Assert.Equal(new[] { "A", "B" }, plan.AffectedPosterIds);
        Assert.Equal(new[] { ManagedStorageDropContractTests.ItemId }, plan.AffectedContentIds);
        Assert.Equal(catalog.Items, plan.Catalog.Items); Assert.Equal(catalog.Collections, plan.Catalog.Collections);
        Assert.Equal(catalog.Memberships, plan.Catalog.Memberships); Assert.Equal(catalog.Bindings, plan.Catalog.Bindings);
        Assert.Equal(catalog.PosterItems, plan.Catalog.PosterItems);
        Assert.NotNull(plan.Catalog.PosterItems[0].IconAssetId);
        Assert.DoesNotContain("C", plan.Reads.PosterVersions.Keys);
        Assert.Equal(1, plan.FileCount); Assert.Equal(12, plan.ByteCount);
    }

    [Fact]
    public void PreviewOwnsDeeplyFrozenInputs()
    {
        Guid[] ids = [ManagedStorageDropContractTests.ItemId]; Guid[] collections = [ManagedId];
        var request = Request with { Inventories = [Request.Inventories[0] with { AffectedContentIds = ids }] };
        var roots = Roots; roots[0] = roots[0] with { CollectionIds = collections };
        var versions = new Dictionary<string, long>(Reads.PosterVersions);
        var plan = ManagedStorageRelocationPolicy.Prepare(request, Reads with { PosterVersions = versions }, roots, Catalog());
        ids[0] = Guid.NewGuid(); collections[0] = Guid.NewGuid(); versions["A"] = 999;
        Assert.Equal(ManagedStorageDropContractTests.ItemId, plan.Request.Inventories[0].AffectedContentIds[0]);
        Assert.Equal(ManagedId, plan.Roots[0].CollectionIds[0]); Assert.Equal(10, plan.Reads.PosterVersions["A"]);
    }

    [Fact]
    public void ExplicitMigrationIncludesAllOldRootsEvenAnEmptyCollection()
    {
        Guid otherCollection = Guid.NewGuid();
        var otherRoot = Root with { Id = Guid.NewGuid(), ObjectIdentity = "second-root", Locator = "F:/second-root" };
        var roots = Roots.Append(new(otherRoot, Profile, "second-owner", [otherCollection])).ToArray();
        var catalog = Catalog();
        catalog = catalog with { Collections = catalog.Collections.Append(new(otherCollection, "empty", ContentCollectionKind.Managed, "F:/second-root/empty", ContentStorageOwnership.Managed)).ToArray() };
        Assert.Throws<ArgumentException>(() => ManagedStorageRelocationPolicy.Prepare(Request, Reads, roots, catalog));
        var request = Request with { Inventories = Request.Inventories.Append(new(otherCollection, otherRoot.Id, "empty-id", "empty-witness", 0, 0, [], true)).ToArray() };
        var plan = ManagedStorageRelocationPolicy.Prepare(request, Reads, roots, catalog);
        Assert.Equal(2, plan.Roots.Count); Assert.Equal(2, plan.Request.Inventories.Count);
        Assert.Equal(1, plan.FileCount); Assert.Equal(12, plan.ByteCount);
    }

    [Fact]
    public void ResolvedAliasesAndDescendantsAreIncludedWithoutCreatingNewCards()
    {
        var request = Request with { Inventories = [Request.Inventories[0] with { AffectedContentIds = [ManagedStorageDropContractTests.ItemId, ExtraItemId] }] };
        var plan = ManagedStorageRelocationPolicy.Prepare(request, Reads, Roots, Catalog());
        Assert.Equal(2, plan.AffectedContentIds.Count);
        Assert.Equal(Catalog().PosterItems, plan.Catalog.PosterItems);
    }

    [Theory]
    [InlineData(StorageRelocationPhase.Prepared)]
    [InlineData(StorageRelocationPhase.IntentDurable)]
    [InlineData(StorageRelocationPhase.Copying)]
    [InlineData(StorageRelocationPhase.TargetsVerified)]
    public void RelocationAlwaysRequiresConfirmationBoundToTheWholePlan(StorageRelocationPhase phase)
    {
        var p = Plan();
        Assert.Equal(StorageRelocationAction.ConfirmLocationChange, ManagedStorageRelocationPolicy.Decide(p, phase, Check(p) with { ConfirmedPlanFingerprint = null }));
        Assert.Equal(StorageRelocationAction.ConfirmLocationChange, ManagedStorageRelocationPolicy.Decide(p, phase, Check(p) with { ConfirmedPlanFingerprint = "older-choice" }));
    }

    [Theory]
    [InlineData(StorageDestinationRelation.SameLocation)]
    [InlineData(StorageDestinationRelation.InsideSource)]
    [InlineData(StorageDestinationRelation.ContainsSource)]
    [InlineData(StorageDestinationRelation.Unknown)]
    public void NonDisjointDestinationIsNotAMigration(StorageDestinationRelation relation) =>
        Assert.Throws<ArgumentException>(() => ManagedStorageRelocationPolicy.Prepare(Request with { Relation = relation }, Reads, Roots, Catalog()));

    [Theory]
    [InlineData("occupied")]
    [InlineData("unsafe")]
    [InlineData("read-only")]
    [InlineData("future")]
    [InlineData("identity")]
    public void TargetFailureNeverBecomesAnOverwrite(string fault)
    {
        var request = fault switch
        {
            "occupied" => Request with { TargetEmpty = false },
            "unsafe" => Request with { ResolvedSafe = false },
            "read-only" => Request with { Destination = Request.Destination with { Writable = false } },
            "future" => Request with { FormatVersion = 2 },
            _ => Request with { Destination = Request.Destination with { ObjectIdentity = Root.ObjectIdentity } }
        };
        Assert.Throws<ArgumentException>(() => ManagedStorageRelocationPolicy.Prepare(request, Reads, Roots, Catalog()));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("incomplete")]
    [InlineData("unowned")]
    [InlineData("omit-item")]
    [InlineData("unknown-item")]
    [InlineData("negative-bytes")]
    [InlineData("duplicate")]
    public void IncompleteInventoryCannotLeaveFilesOrReferencesBehind(string fault)
    {
        var i = Request.Inventories[0];
        var inventories = fault switch
        {
            "missing" => Array.Empty<StorageCollectionInventory>(),
            "incomplete" => [i with { Complete = false }],
            "unowned" => [i with { RootId = Guid.NewGuid() }],
            "omit-item" => [i with { AffectedContentIds = [] }],
            "unknown-item" => [i with { AffectedContentIds = [Guid.NewGuid()] }],
            "negative-bytes" => [i with { ByteCount = -1 }],
            _ => new[] { i, i }
        };
        Assert.Throws<ArgumentException>(() => ManagedStorageRelocationPolicy.Prepare(Request with { Inventories = inventories }, Reads, Roots, Catalog()));
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("offline")]
    [InlineData("missing-registration")]
    [InlineData("missing-poster")]
    public void OwnershipAndCompleteReadSetAreMandatory(string fault)
    {
        var roots = Roots;
        if (fault == "foreign") roots[0] = roots[0] with { OwnerProfileIdentity = "copied-profile" };
        if (fault == "offline") roots[0] = roots[0] with { Root = Root with { Available = false } };
        if (fault == "missing-registration") roots[0] = roots[0] with { CollectionIds = [] };
        var reads = fault == "missing-poster" ? Reads with { PosterVersions = new Dictionary<string, long> { ["A"] = 10 } } : Reads;
        Assert.Throws<ArgumentException>(() => ManagedStorageRelocationPolicy.Prepare(Request, reads, roots, Catalog()));
    }

    [Fact]
    public void EmptyProfileStillRequiresDurableIntentAndVerifiedRootSwitch()
    {
        var plan = ManagedStorageRelocationPolicy.Prepare(Request with { Inventories = [] }, Reads, [], ContentCatalogSnapshot.Empty);
        Assert.Equal(0, plan.FileCount); Assert.Empty(plan.AffectedContentIds);
        Assert.Equal(StorageRelocationAction.WriteIntent, ManagedStorageRelocationPolicy.Decide(plan, StorageRelocationPhase.Prepared, Check(plan)));
        Assert.Equal(StorageRelocationAction.AwaitVerification, ManagedStorageRelocationPolicy.Decide(plan, StorageRelocationPhase.TargetsVerified, Check(plan) with { AllTargetsVerified = false }));
    }

    [Theory]
    [InlineData("settings")]
    [InlineData("roots")]
    [InlineData("content")]
    [InlineData("runtime")]
    [InlineData("poster")]
    [InlineData("inventory")]
    [InlineData("target")]
    [InlineData("owner")]
    public void EveryChangedWitnessInvalidatesPrecommit(string changed)
    {
        var old = Plan(); var request = Request; var reads = Reads; var roots = Roots;
        if (changed == "settings") reads = reads with { SelectionVersion = 4 };
        if (changed == "roots") reads = reads with { RootSetVersion = 5 };
        if (changed == "content") reads = reads with { ContentVersion = 6 };
        if (changed == "runtime") reads = reads with { RuntimeGeneration = 2 };
        if (changed == "poster") reads = reads with { PosterVersions = new Dictionary<string, long> { ["A"] = 11, ["B"] = 20 } };
        if (changed == "inventory") request = request with { Inventories = [request.Inventories[0] with { TreeWitness = "changed" }] };
        if (changed == "target") request = request with { DestinationWitness = "replaced-target" };
        if (changed == "owner") roots[0] = roots[0] with { OwnershipWitness = "changed-marker" };
        var fresh = ManagedStorageRelocationPolicy.Prepare(request, reads, roots, Catalog());
        Assert.NotEqual(old.Fingerprint, fresh.Fingerprint);
        Assert.Equal(StorageRelocationAction.RepreviewKeepingSources, ManagedStorageRelocationPolicy.Decide(old,
            StorageRelocationPhase.TargetsVerified, Check(old) with { CurrentPlanFingerprint = fresh.Fingerprint }));
    }

    [Fact]
    public void UnrelatedPosterEditDoesNotBlockRelocation()
    {
        var reads = Reads with { PosterVersions = new Dictionary<string, long> { ["A"] = 10, ["B"] = 20, ["C"] = 99 } };
        Assert.Equal(Plan().Fingerprint, ManagedStorageRelocationPolicy.Prepare(Request, reads, Roots, Catalog()).Fingerprint);
    }

    [Theory]
    [InlineData(StorageRelocationPhase.Prepared)]
    [InlineData(StorageRelocationPhase.IntentDurable)]
    [InlineData(StorageRelocationPhase.Copying)]
    [InlineData(StorageRelocationPhase.TargetsVerified)]
    public void CancellationBeforeCommitAlwaysKeepsOriginalLocation(StorageRelocationPhase phase)
    {
        var plan = Plan(); Assert.Equal(StorageRelocationAction.KeepOldLocation, ManagedStorageRelocationPolicy.Decide(plan, phase, Check(plan) with { Cancelled = true }));
    }

    [Theory]
    [InlineData(StorageRelocationPhase.IntentDurable)]
    [InlineData(StorageRelocationPhase.Copying)]
    [InlineData(StorageRelocationPhase.TargetsVerified)]
    public void NoIntentOrWrongIntentCannotCopyOrSwitch(StorageRelocationPhase phase)
    {
        var p = Plan();
        Assert.Equal(StorageRelocationAction.PreserveAndInspect, ManagedStorageRelocationPolicy.Decide(p, phase, Check(p) with { DurableIntentFingerprint = null }));
        Assert.Equal(StorageRelocationAction.PreserveAndInspect, ManagedStorageRelocationPolicy.Decide(p, phase, Check(p) with { DurableIntentFingerprint = "other" }));
    }

    [Theory]
    [InlineData(StorageRelocationPhase.IntentDurable)]
    [InlineData(StorageRelocationPhase.Copying)]
    public void CopyStagesNeverAuthorizeSourceCleanup(StorageRelocationPhase phase)
    {
        var p = Plan(); Assert.Equal(StorageRelocationAction.CopyAndVerifyKeepingSources, ManagedStorageRelocationPolicy.Decide(p, phase, Check(p)));
    }

    [Fact]
    public void PartialCopyOrChangedSourceCannotPublishEvenWithVerifiedPhaseLabel()
    {
        var p = Plan();
        Assert.Equal(StorageRelocationAction.AwaitVerification, ManagedStorageRelocationPolicy.Decide(p, StorageRelocationPhase.TargetsVerified, Check(p) with { AllTargetsVerified = false }));
        Assert.Equal(StorageRelocationAction.RepreviewKeepingSources, ManagedStorageRelocationPolicy.Decide(p, StorageRelocationPhase.TargetsVerified, Check(p) with { SourcesStillMatch = false }));
        Assert.Equal(StorageRelocationAction.CommitOneTransaction, ManagedStorageRelocationPolicy.Decide(p, StorageRelocationPhase.TargetsVerified, Check(p)));
    }

    [Theory]
    [InlineData(StorageRelocationPhase.IndexCommitted)]
    [InlineData(StorageRelocationPhase.CleanupPending)]
    [InlineData(StorageRelocationPhase.Completed)]
    public void PhaseLabelWithoutCommitReceiptCannotAuthorizeCleanup(StorageRelocationPhase phase)
    {
        var p = Plan(); Assert.Equal(StorageRelocationAction.PreserveAndInspect, ManagedStorageRelocationPolicy.Decide(p, phase, Check(p)));
    }

    [Theory]
    [InlineData("operation")]
    [InlineData("fingerprint")]
    [InlineData("root")]
    [InlineData("revision")]
    public void ForgedOrMismatchedCommitReceiptIsNotAccepted(string fault)
    {
        var p = Plan(); var commit = fault switch
        {
            "operation" => Commit(p) with { OperationId = Guid.NewGuid() },
            "fingerprint" => Commit(p) with { PlanFingerprint = "other" },
            "root" => Commit(p) with { ActiveRootId = Root.Id },
            _ => Commit(p) with { CommittedRevision = 0 }
        };
        Assert.Equal(StorageRelocationAction.PreserveAndInspect, ManagedStorageRelocationPolicy.Decide(p, StorageRelocationPhase.CleanupPending, Check(p) with { Commit = commit, ActiveRootId = p.Request.Destination.Id }));
    }

    [Theory]
    [InlineData("cancelled")]
    [InlineData("changed-old-copy")]
    [InlineData("unowned-old-copy")]
    [InlineData("target-unavailable")]
    public void PostcommitFailuresNeverRollBackThePublishedLocation(string fault)
    {
        var p = Plan(); var check = Check(p) with { Commit = Commit(p), ActiveRootId = p.Request.Destination.Id };
        check = fault switch
        {
            "cancelled" => check with { Cancelled = true },
            "changed-old-copy" => check with { SourcesStillMatch = false },
            "unowned-old-copy" => check with { OwnedOldCopiesVerified = false },
            _ => check with { NewLocationStillVerified = false }
        };
        Assert.Equal(StorageRelocationAction.KeepNewLocationAndOldCopies, ManagedStorageRelocationPolicy.Decide(p, StorageRelocationPhase.CleanupPending, check));
    }

    [Fact]
    public void CommittedReceiptSurvivesCrashBeforePhaseUpdateAndNewRuntime()
    {
        var p = Plan(); var check = Check(p) with { Commit = Commit(p), ActiveRootId = p.Request.Destination.Id, CurrentPlanFingerprint = "next-runtime" };
        Assert.Equal(StorageRelocationAction.FinishVerifiedOldCopyCleanup, ManagedStorageRelocationPolicy.Decide(p, StorageRelocationPhase.TargetsVerified, check));
        Assert.Equal(StorageRelocationAction.Done, ManagedStorageRelocationPolicy.Decide(p, StorageRelocationPhase.Completed, check));
        Assert.Equal(StorageRelocationAction.PreserveAndInspect, ManagedStorageRelocationPolicy.Decide(p, StorageRelocationPhase.CleanupPending, check with { ActiveRootId = Guid.NewGuid() }));
        Assert.Equal(StorageRelocationAction.PreserveAndInspect, ManagedStorageRelocationPolicy.Decide(p, (StorageRelocationPhase)999, check));
    }
}
