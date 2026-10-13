using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class ManagedStorageDropContractTests
{
    internal static readonly Guid ItemId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    internal static readonly Guid CollectionId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    internal static readonly Guid MemberId = Guid.Parse("00000000-0000-0000-0000-000000000003");
    internal static readonly Guid CardA = Guid.Parse("00000000-0000-0000-0000-000000000004");
    internal static readonly Guid CardB = Guid.Parse("00000000-0000-0000-0000-000000000005");
    internal static ContentItem Item(ContentKind kind = ContentKind.File) => new(ItemId, kind, new("local", "object-1"),
        new(kind switch { ContentKind.Shortcut => ContentTargetKind.Shortcut, ContentKind.Application => ContentTargetKind.Application,
            ContentKind.ShellItem => ContentTargetKind.Shell, ContentKind.VirtualStream => ContentTargetKind.Virtual,
            ContentKind.Unknown => ContentTargetKind.Unknown, _ => ContentTargetKind.FileSystem }, "C:/fixture/source"),
        "source", ContentCatalogPolicy.AllCapabilities);
    internal static ManagedStorageRoot Root() => new(Guid.Parse("00000000-0000-0000-0000-000000000006"),
        "D:/owned", "root", 1, ManagedRootLocation.UserPersistent, true, true);
    internal static ContentTransferSnapshot Snapshot(ContentItem? item = null) => new(ContentCatalogPolicy.Freeze(new(
        [item ?? Item()], [new(CollectionId, "references", ContentCollectionKind.Reference)],
        [new(MemberId, CollectionId, ItemId)], [new("A", CollectionId), new("B", CollectionId)],
        [new(CardA, "A", MemberId) { IconAssetId = ContentIconAsset.Create(1, 1, [0, 0, 0, 255]).Id }, new(CardB, "B", MemberId)])),
        5, new Dictionary<string, long> { ["A"] = 10, ["B"] = 20, ["C"] = 30 }, true);
    internal static ContentTransferRequest Request(ContentDropRoute route = ContentDropRoute.ToDesktop, ContentItem? item = null) =>
        new(Guid.Parse("00000000-0000-0000-0000-000000000007"), 1, "A", route, ContentDropModifiers.None, Root(),
        [new(item ?? Item(), new("object-1", "C:/fixture/source", "v1", ["source-parent"], [], 5, true),
            new(route == ContentDropRoute.ToDesktop ? "desktop" : "root", "d1", "D:/target/source", [],
                null, route == ContentDropRoute.ToDesktop, true), route == ContentDropRoute.ToDesktop ? CardA : null)]);
    internal static ContentTransferRequest Edit(ContentTransferRequest r, Func<ContentTransferEntry, ContentTransferEntry> change) =>
        r with { Entries = [change(r.Entries[0])] };
    internal static ContentTransferPlan Plan(ContentTransferRequest? r = null, ContentTransferSnapshot? s = null)
    { var result = ContentTransferPlanning.Prepare(r ?? Request(), s ?? Snapshot()); Assert.Equal(TransferPlanStatus.Ready, result.Status); return Assert.IsType<ContentTransferPlan>(result.Plan); }

    [Theory]
    [InlineData(ManagedRootLocation.Install)]
    [InlineData(ManagedRootLocation.Cache)]
    [InlineData(ManagedRootLocation.Temporary)]
    [InlineData(ManagedRootLocation.Unknown)]
    [InlineData((ManagedRootLocation)999)]
    public void NonpersistentRootIsNeverAccepted(ManagedRootLocation location) =>
        Assert.Equal(ManagedStorageDecision.UnsafeLocation, ManagedStoragePolicy.Evaluate(Root() with { Location = location }));

    [Fact]
    public void MissingOfflineReadOnlyAndFutureRootRemainDistinct()
    {
        Assert.Equal(ManagedStorageDecision.ChooseRoot, ManagedStoragePolicy.Evaluate(null));
        Assert.Equal(ManagedStorageDecision.Unavailable, ManagedStoragePolicy.Evaluate(Root() with { Available = false }));
        Assert.Equal(ManagedStorageDecision.Unavailable, ManagedStoragePolicy.Evaluate(Root() with { Writable = false }));
        Assert.Equal(ManagedStorageDecision.UnknownFormat, ManagedStoragePolicy.Evaluate(Root() with { FormatVersion = 99 }));
        Assert.Equal(ManagedStorageDecision.Invalid, ManagedStoragePolicy.Evaluate(Root() with { ObjectIdentity = "" }));
    }

    [Fact]
    public void ManagedRegistrationPreservesLegacyReferencesAndIsIdempotent()
    {
        var before = Snapshot().Catalog;
        var collection = new Collection(Guid.NewGuid(), "owned", ContentCollectionKind.Managed, "D:/owned/collection", ContentStorageOwnership.Managed);
        var result = ManagedCollectionRegistrationPolicy.Prepare(before, Root(), collection, "root");
        Assert.Equal(before.Items, result.Catalog.Items);
        Assert.Equal(before.Bindings, result.Catalog.Bindings);
        Assert.Equal(before.PosterItems, result.Catalog.PosterItems);
        Assert.Equal(before.Memberships, result.Catalog.Memberships);
        Assert.Equal(ContentCollectionKind.Reference, result.Catalog.Collections[0].Kind);
        Assert.Equal(2, ManagedCollectionRegistrationPolicy.Prepare(result.Catalog, Root(), collection, "root").Catalog.Collections.Count);
        Assert.Throws<ArgumentException>(() => ManagedCollectionRegistrationPolicy.Prepare(before, Root(), collection with { Id = CollectionId }, "root"));
        Assert.Throws<ArgumentException>(() => ManagedCollectionRegistrationPolicy.Prepare(before, Root(), collection, "outside"));
    }

    [Theory]
    [InlineData(true, true, true, ContentDropPayload.PhysicalPaths)]
    [InlineData(false, true, true, ContentDropPayload.VirtualFiles)]
    [InlineData(false, true, false, ContentDropPayload.ShellApplication)]
    [InlineData(false, false, false, ContentDropPayload.Unsupported)]
    public void PhysicalFormatWinsWithoutDoubleImport(bool physical, bool app, bool virtualFiles, ContentDropPayload expected) =>
        Assert.Equal(expected, ContentDropPayloadPolicy.Classify(physical, app, virtualFiles));

    [Theory]
    [InlineData(ContentKind.File)]
    [InlineData(ContentKind.Folder)]
    [InlineData(ContentKind.Shortcut)]
    public void PhysicalObjectsUseMoveWithoutResolvingShortcutTarget(ContentKind kind)
    {
        var item = Item(kind);
        Assert.True(ContentDropIntentPolicy.Resolve(item, ContentDropRoute.IntoManagedStorage, ContentDropModifiers.None, Root()).Allowed);
        Assert.Equal(ContentDropAction.Move, ContentDropIntentPolicy.Resolve(item, ContentDropRoute.ToDesktop, ContentDropModifiers.Shift, desktopResolved: true).Intent);
        Assert.Equal("C:/fixture/source", Plan(Request(item: item), Snapshot(item)).Entries[0].Entry.Source.Locator);
    }

    [Theory]
    [InlineData(ContentKind.Application)]
    [InlineData(ContentKind.ShellItem)]
    [InlineData(ContentKind.VirtualStream)]
    [InlineData(ContentKind.Unknown)]
    public void NonphysicalPayloadNeverMovesEvenIfItAdvertisesEveryCapability(ContentKind kind) =>
        Assert.Equal(ContentDropDenial.UnsupportedSource, ContentDropIntentPolicy.Resolve(Item(kind), ContentDropRoute.IntoManagedStorage, ContentDropModifiers.None, Root()).Denial);

    [Theory]
    [InlineData(ContentDropModifiers.Control, ContentDropAction.Copy)]
    [InlineData(ContentDropModifiers.Alt, ContentDropAction.Link)]
    [InlineData(ContentDropModifiers.Control | ContentDropModifiers.Shift, ContentDropAction.Link)]
    public void ReservedModifiersDoNotSilentlyMove(ContentDropModifiers keys, ContentDropAction action)
    {
        var decision = ContentDropIntentPolicy.Resolve(Item(), ContentDropRoute.ToDesktop, keys, desktopResolved: true);
        Assert.Equal(action, decision.Intent); Assert.Equal(ContentDropDenial.OperationNotImplemented, decision.Denial);
        Assert.Equal(ContentDropAction.Layout, ContentDropIntentPolicy.Resolve(Item(), ContentDropRoute.InternalLayout, keys).Intent);
    }

    [Fact]
    public void UnsupportedTargetsAndCapabilitiesNeverFallBackToReferenceOrCopy()
    {
        Assert.Equal(ContentDropDenial.MissingCapability, ContentDropIntentPolicy.Resolve(Item() with { Capabilities = ContentCapability.Copy }, ContentDropRoute.ToDesktop, 0, desktopResolved: true).Denial);
        Assert.Equal(ContentDropDenial.ChooseStorage, ContentDropIntentPolicy.Resolve(Item(), ContentDropRoute.IntoManagedStorage, 0).Denial);
        Assert.Equal(ContentDropDenial.TargetUnavailable, ContentDropIntentPolicy.Resolve(Item(), ContentDropRoute.ToDesktop, 0).Denial);
        Assert.Equal(ContentDropDenial.UnsupportedRoute, ContentDropIntentPolicy.Resolve(Item(), ContentDropRoute.OtherPoster, 0).Denial);
        Assert.Equal(ContentDropDenial.Invalid, ContentDropIntentPolicy.Resolve(Item(), ContentDropRoute.InternalLayout, (ContentDropModifiers)128).Denial);
    }

    [Fact]
    public void LegacyAliasReadSetIsStillCompleteButDropRequiresNoExtraConfirmation()
    {
        var state = Snapshot(); var request = Request(); var plan = Plan(request, state);
        Assert.Equal(new[] { "A", "B" }, plan.Entries[0].AffectedPosterIds);
        Assert.Equal(new[] { "A", "B" }, plan.ReadSet.Posters.Keys);
        Assert.False(plan.RequiresConfirmation);
        Assert.Equal(TransferAdmission.ReadyForJournal, ContentTransferPlanning.Revalidate(plan, request, state, null));
        Assert.Equal(TransferAdmission.ReadyForJournal, ContentTransferPlanning.Revalidate(plan, request, state, plan.Fingerprint));
        var delta = ContentTransferPlanning.DescribeVerifiedMove(plan, 0, state.Catalog);
        Assert.Equal(new[] { ItemId }, delta.RelocateContentIds);
        Assert.Equal(new[] { CardA }, delta.DetachPosterItemIds);
        Assert.Equal(new[] { CardB }, delta.PreservePosterItemIds);
        Assert.Equal(ContentIconAsset.Create(1, 1, [0, 0, 0, 255]).Id, state.Catalog.PosterItems[0].IconAssetId);
    }

    [Fact]
    public void IndependentSameSourceIdentityAndFolderDescendantsAreIncluded()
    {
        var state = Snapshot(); var alias = Item() with { Id = Guid.NewGuid() };
        state = state with { Catalog = ContentCatalogPolicy.Freeze(state.Catalog with { Items = state.Catalog.Items.Append(alias).ToArray() }) };
        Assert.Contains(alias.Id, Plan(s: state).Entries[0].AffectedContentIds);
        var folder = Item(ContentKind.Folder);
        var child = Item() with { Id = Guid.NewGuid(), Source = new("local", "child") };
        state = Snapshot(folder);
        state = state with { Catalog = ContentCatalogPolicy.Freeze(state.Catalog with { Items = [folder, child] }) };
        var request = Edit(Request(item: folder), e => e with { Source = e.Source with { DescendantContentIds = [child.Id] } });
        Assert.Contains(child.Id, Plan(request, state).Entries[0].AffectedContentIds);
    }

    [Fact]
    public void SamePathIsNoOpAndNeverImplicitlyDetaches()
    {
        var request = Edit(Request(), e => e with { Destination = e.Destination with { Locator = e.Source.Locator, ExistingObjectIdentity = e.Source.ObjectIdentity } });
        var plan = Plan(request);
        Assert.True(plan.Entries[0].SameLocation);
        Assert.Throws<InvalidOperationException>(() => ContentTransferPlanning.DescribeVerifiedMove(plan, 0, Snapshot().Catalog));
        Assert.Equal(TransferPlanStatus.Conflict, ContentTransferPlanning.Prepare(Edit(request, e => e with { Destination = e.Destination with { ExistingObjectIdentity = "replaced" } }), Snapshot()).Status);
    }

    [Fact]
    public void CollisionNeedsNewVacantCandidateAndAnewConfirmation()
    {
        var before = Request(); var plan = Plan(before);
        var collision = Edit(before, e => e with { Destination = e.Destination with { ExistingObjectIdentity = "foreign" } });
        Assert.Equal(TransferPlanStatus.Collision, ContentTransferPlanning.Prepare(collision, Snapshot()).Status);
        var renamed = Edit(before, e => e with { Destination = e.Destination with { Locator = "D:/target/source (2)" } });
        Assert.Equal(TransferAdmission.Repreview, ContentTransferPlanning.Revalidate(plan, renamed, Snapshot(), plan.Fingerprint));
    }

    [Theory]
    [InlineData("source")]
    [InlineData("destination")]
    [InlineData("generation")]
    [InlineData("root")]
    [InlineData("content")]
    [InlineData("affected-poster")]
    [InlineData("source-path")]
    public void AnyRelevantChangeExpiresThePreview(string change)
    {
        var request = Request(); var state = Snapshot(); var plan = Plan(request, state);
        switch (change)
        {
            case "source": request = Edit(request, e => e with { Source = e.Source with { VersionWitness = "v2" } }); break;
            case "destination": request = Edit(request, e => e with { Destination = e.Destination with { ParentVersionWitness = "d2" } }); break;
            case "generation": request = request with { RuntimeGeneration = 2 }; break;
            case "root": request = request with { Root = Root() with { Version = 2 } }; break;
            case "content": state = state with { ContentVersion = 6 }; break;
            case "affected-poster": state = state with { PosterVersions = new Dictionary<string, long>(state.PosterVersions) { ["B"] = 21 } }; break;
            case "source-path": request = Edit(request, e => e with { Source = e.Source with { Locator = "C:/other" } }); break;
        }
        Assert.Equal(TransferAdmission.Repreview, ContentTransferPlanning.Revalidate(plan, request, state, plan.Fingerprint));
    }

    [Fact]
    public void UnrelatedPosterChangeDoesNotExpireReadSet()
    {
        var request = Request(); var state = Snapshot(); var plan = Plan(request, state);
        state = state with { PosterVersions = new Dictionary<string, long>(state.PosterVersions) { ["C"] = 99 } };
        Assert.Equal(TransferAdmission.ReadyForJournal, ContentTransferPlanning.Revalidate(plan, request, state, plan.Fingerprint));
    }

    [Fact]
    public void MutableCallerListsCannotRewriteAcceptedPreview()
    {
        var ancestors = new List<string> { "source-parent" };
        var request = Edit(Request(), e => e with { Source = e.Source with { AncestorIdentities = ancestors } });
        var plan = Plan(request); ancestors.Add("changed");
        Assert.Single(plan.Entries[0].Entry.Source.AncestorIdentities);
        Assert.Equal(TransferAdmission.Repreview, ContentTransferPlanning.Revalidate(plan, request, Snapshot(), plan.Fingerprint));
    }

    [Fact]
    public void UnknownOrMalformedRequestPreservesAllExistingRelationships()
    {
        var state = Snapshot();
        Assert.Equal(TransferPlanStatus.Invalid, ContentTransferPlanning.Prepare(Request() with { FormatVersion = 2 }, state).Status);
        Assert.Equal(TransferPlanStatus.Invalid, ContentTransferPlanning.Prepare(Request() with { Entries = [] }, state).Status);
        Assert.Equal(TransferPlanStatus.Conflict, ContentTransferPlanning.Prepare(Edit(Request(), e => e with { OriginPosterItemId = Guid.NewGuid() }), state).Status);
        Assert.Equal(2, state.Catalog.PosterItems.Count);
        Assert.Equal(ContentCollectionKind.Reference, state.Catalog.Collections[0].Kind);
    }

    [Theory]
    [InlineData("unsafe-source")]
    [InlineData("unsafe-destination")]
    [InlineData("outside-root")]
    [InlineData("self-child")]
    public void ResolvedSafetyFactsAreRequired(string change)
    {
        var request = Request(ContentDropRoute.IntoManagedStorage);
        request = Edit(request, e => change switch
        {
            "unsafe-source" => e with { Source = e.Source with { SafeToMove = false } },
            "unsafe-destination" => e with { Destination = e.Destination with { SafeToWrite = false } },
            "outside-root" => e with { Destination = e.Destination with { ParentIdentity = "outside" } },
            _ => e with { Destination = e.Destination with { AncestorIdentities = ["object-1", "root"] } }
        });
        Assert.Equal(TransferPlanStatus.Unsafe, ContentTransferPlanning.Prepare(request, Snapshot()).Status);
    }


    [Fact]
    public void ValidLegacyReferenceNeedsNoExtraSourceConfirmation()
    {
        var state = Snapshot();
        state = state with { Catalog = ContentCatalogPolicy.Freeze(state.Catalog with { PosterItems = [state.Catalog.PosterItems[0]] }) };
        Assert.False(Plan(s: state).RequiresConfirmation);
    }

    [Fact]
    public void RealManagedOwnershipIsIndependentOfTheReferenceView()
    {
        var state = Snapshot();
        var owned = new Collection(Guid.NewGuid(), "storage", ContentCollectionKind.Managed, "D:/owned/items", ContentStorageOwnership.Managed);
        state = state with { Catalog = ContentCatalogPolicy.Freeze(state.Catalog with
        {
            Collections = state.Catalog.Collections.Append(owned).ToArray(),
            Memberships = state.Catalog.Memberships.Append(new(Guid.NewGuid(), owned.Id, ItemId, ContentMembershipOrigin.Source)).ToArray(),
            PosterItems = [state.Catalog.PosterItems[0]]
        }) };
        var plan = Plan(s: state);
        Assert.False(plan.RequiresConfirmation);
        Assert.Equal(CollectionId, state.Catalog.Bindings.Single(b => b.PosterId == "A").CollectionId);
        Assert.Equal(new[] { CardA }, ContentTransferPlanning.DescribeVerifiedMove(plan, 0, state.Catalog).DetachPosterItemIds);
    }

    [Fact]
    public void MappedViewIsNotSilentlyTreatedAsManaged()
    {
        var state = Snapshot();
        state = state with { Catalog = ContentCatalogPolicy.Freeze(state.Catalog with
        {
            Collections = [state.Catalog.Collections[0] with { Kind = ContentCollectionKind.Mapped, Ownership = ContentStorageOwnership.Borrowed, StorageLocator = "C:/borrowed" }],
            Memberships = [state.Catalog.Memberships[0] with { Origin = ContentMembershipOrigin.Source }]
        }) };
        Assert.Equal(TransferPlanStatus.Unsupported, ContentTransferPlanning.Prepare(Request(), state).Status);
    }

    [Fact]
    public void RemovedSharedCardMissingVersionAndNewRelationExpireThePlan()
    {
        var request = Request(); var state = Snapshot(); var plan = Plan(request, state);
        var removed = state with { ContentVersion = 6, Catalog = ContentCatalogPolicy.Freeze(state.Catalog with { PosterItems = [state.Catalog.PosterItems[0]] }) };
        Assert.Equal(TransferAdmission.Repreview, ContentTransferPlanning.Revalidate(plan, request, removed, plan.Fingerprint));
        var versions = new Dictionary<string, long>(state.PosterVersions); versions.Remove("B");
        Assert.Equal(TransferPlanStatus.Conflict, ContentTransferPlanning.Prepare(request, state with { PosterVersions = versions }).Status);
        Assert.Equal(TransferAdmission.Repreview, ContentTransferPlanning.Revalidate(plan, request, state with { ContentVersion = 6 }, plan.Fingerprint));
    }

    [Fact]
    public void PosterBecomingUnavailableCannotUseAnEarlierConfirmation()
    {
        var request = Request(); var state = Snapshot(); var plan = Plan(request, state);
        Assert.Equal(TransferAdmission.Repreview, ContentTransferPlanning.Revalidate(plan, request,
            state with { PosterAcceptsTransfer = false }, plan.Fingerprint));
    }

    [Fact]
    public void HardlinkAliasDestinationIsNotMisreportedAsSamePathNoOp()
    {
        var request = Edit(Request(), e => e with { Destination = e.Destination with { ExistingObjectIdentity = e.Source.ObjectIdentity } });
        Assert.Equal(TransferPlanStatus.Collision, ContentTransferPlanning.Prepare(request, Snapshot()).Status);
    }

    [Fact]
    public void UnboundPosterUsesDefaultStorageWithoutExtraConfirmation()
    {
        var state = Snapshot() with { Catalog = ContentCatalogSnapshot.Empty };
        var request = Request(ContentDropRoute.IntoManagedStorage);
        var plan = Plan(request, state);
        Assert.False(plan.RequiresConfirmation);
        Assert.Empty(ContentTransferPlanning.DescribeVerifiedMove(plan, 0, state.Catalog).DetachPosterItemIds);
        Assert.Empty(state.Catalog.Bindings);
    }

    [Fact]
    public void BatchRejectsDuplicateAndNestedPhysicalSources()
    {
        var request = Request(ContentDropRoute.IntoManagedStorage); var entry = request.Entries[0];
        Assert.Equal(TransferPlanStatus.Invalid, ContentTransferPlanning.Prepare(request with { Entries = [entry, entry] }, Snapshot()).Status);
        var child = entry with { Item = entry.Item with { Id = Guid.NewGuid(), Source = new("local", "child"), Target = entry.Item.Target with { Locator = "C:/fixture/child" } },
            Source = entry.Source with { ObjectIdentity = "child", Locator = "C:/fixture/child", AncestorIdentities = ["object-1"] },
            Destination = entry.Destination with { Locator = "D:/target/child" } };
        Assert.Equal(TransferPlanStatus.Unsafe, ContentTransferPlanning.Prepare(request with { Entries = [entry, child] }, Snapshot()).Status);
    }
}

