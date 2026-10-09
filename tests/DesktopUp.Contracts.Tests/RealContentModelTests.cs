using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class RealContentModelTests
{
    [Fact]
    public void OneContentCanBeShownByTwoPostersWithDifferentLayoutIds()
    {
        var c = RealContentFixtures.Catalog();
        Assert.Single(c.Items);
        Assert.NotEqual(c.PosterItems[0].LayoutItemId, c.PosterItems[1].LayoutItemId);
        foreach (var p in c.PosterItems)
        {
            Assert.Null(ContentLayoutPolicy.Validate(new(p.PosterId, [new(p.LayoutItemId, 0)])));
            Assert.Equal(c.Items[0].Id, ContentRelationPolicy.Resolve(c, p.PosterId, p.LayoutItemId)!.Item.Id);
        }
    }

    [Fact]
    public void DetachPlanOnlyRemovesLocalRelationAndPreservesSourceAndOtherPoster()
    {
        var c = RealContentFixtures.Catalog();
        var before = ContentCatalogCodec.Write(c);
        var resolved = ContentRelationPolicy.Resolve(c, "A", c.PosterItems[0].LayoutItemId)!;
        var plan = ContentRelationPolicy.PrepareDetach(c, Guid.NewGuid(), resolved.Witness);
        Assert.NotNull(plan);
        var next = ContentCatalogPolicy.Freeze(c with { PosterItems = c.PosterItems.Where(x => x.Id != plan.ReadSet.PosterItemId).ToArray() });
        Assert.Equal(before, ContentCatalogCodec.Write(c)); // pure proposal, no hidden store
        Assert.Equal(c.Items, next.Items);
        Assert.Equal(c.Collections, next.Collections);
        Assert.Equal(c.Memberships, next.Memberships);
        Assert.Single(next.PosterItems);
        Assert.Equal("B", next.PosterItems[0].PosterId);
        Assert.Null(ContentRelationPolicy.PrepareDetach(next, Guid.NewGuid(), resolved.Witness));
    }

    [Theory]
    [InlineData("item")] [InlineData("member")] [InlineData("collection")]
    [InlineData("binding")] [InlineData("poster")]
    public void EveryReadVersionParticipatesInRelationCas(string changed)
    {
        var c = RealContentFixtures.Catalog();
        var witness = ContentRelationPolicy.Resolve(c, "A", c.PosterItems[0].LayoutItemId)!.Witness;
        var next = changed switch
        {
            "item" => c with { Items = [c.Items[0] with { Version = 2 }] },
            "member" => c with { Memberships = [c.Memberships[0] with { Version = 2 }] },
            "collection" => c with { Collections = [c.Collections[0] with { Version = 2 }] },
            "binding" => c with { Bindings = [c.Bindings[0] with { Version = 2 }, c.Bindings[1]] },
            _ => c with { PosterItems = [c.PosterItems[0] with { Version = 2 }, c.PosterItems[1]] }
        };
        Assert.False(ContentRelationPolicy.IsCurrent(next, witness));
    }

    [Fact]
    public void UnrelatedPosterRevisionDoesNotConflictWithA()
    {
        var c = RealContentFixtures.Catalog();
        var witness = ContentRelationPolicy.Resolve(c, "A", c.PosterItems[0].LayoutItemId)!.Witness;
        Assert.True(ContentRelationPolicy.IsCurrent(c with
        {
            PosterItems = [c.PosterItems[0], c.PosterItems[1] with { Version = 2 }]
        }, witness));
    }

    [Fact]
    public void ArgumentsWorkingDirectoryAndOpaqueIdentityAreNotNormalizedOrMerged()
    {
        var original = RealContentFixtures.Item() with
        { Kind = ContentKind.Application, Target = new(ContentTargetKind.Application, "app-id", "--profile A", @"C:\One") };
        foreach (var target in new[] {
            original.Target with { Arguments = "--profile B" },
            original.Target with { Arguments = "--profile A " },
            original.Target with { WorkingDirectory = @"C:\Two" },
            original.Target with { Locator = "APP-ID" } })
        {
            var candidate = original with { Id = Guid.NewGuid(), Target = target };
            Assert.Equal(ContentDuplicateHint.SameSource, ContentIdentityPolicy.Compare(original, candidate));
        }
        Assert.Equal(ContentDuplicateHint.SameSource | ContentDuplicateHint.SameOpenSemantics,
            ContentIdentityPolicy.Compare(original, original with { Id = Guid.NewGuid() }));
        Assert.Equal(ContentDuplicateHint.SameOpenSemantics, ContentIdentityPolicy.Compare(original,
            original with { Source = original.Source with { Key = "STABLE-ID" } }));
    }

    [Fact]
    public void RenameAndTemporaryUnavailabilityKeepStableIdentityAndRelations()
    {
        var c = RealContentFixtures.Catalog();
        var changed = c.Items[0] with
        { Version = 2, Availability = ContentAvailability.Missing, Target = c.Items[0].Target with { Locator = @"Z:\renamed.txt" } };
        var next = ContentCatalogPolicy.Freeze(c with { Items = [changed] });
        Assert.Equal(c.Items[0].Id, next.Items[0].Id);
        Assert.Equal(c.PosterItems, next.PosterItems);
    }

    [Fact]
    public void SnapshotCopiesMutableInputCollections()
    {
        var c = RealContentFixtures.Catalog();
        var list = c.Items.ToList();
        var frozen = ContentCatalogPolicy.Freeze(c with { Items = list });
        list.Clear();
        Assert.Single(frozen.Items);
    }

    [Theory]
    [InlineData("item-id")] [InlineData("member")] [InlineData("poster")] [InlineData("binding")]
    [InlineData("empty")] [InlineData("null")] [InlineData("foreign")]
    public void InvalidGraphIsRejected(string variant)
    {
        var c = RealContentFixtures.Catalog();
        var invalid = variant switch
        {
            "item-id" => c with { Items = [c.Items[0], c.Items[0]] },
            "member" => c with { Memberships = [c.Memberships[0] with { ContentItemId = Guid.NewGuid() }] },
            "poster" => c with { PosterItems = [c.PosterItems[0], c.PosterItems[0] with { Id = Guid.NewGuid() }] },
            "binding" => c with { Bindings = [c.Bindings[0], c.Bindings[0]] },
            "empty" => c with { Items = [c.Items[0] with { Id = Guid.Empty }] },
            "null" => c with { Items = [null!] },
            _ => c with { PosterItems = [c.PosterItems[0] with { PosterId = "foreign" }] }
        };
        Assert.Throws<ArgumentException>(() => ContentCatalogPolicy.Freeze(invalid));
    }

    [Theory]
    [InlineData(ContentCollectionKind.Reference, ContentStorageOwnership.None, false, ContentMembershipOrigin.User)]
    [InlineData(ContentCollectionKind.Mapped, ContentStorageOwnership.Borrowed, true, ContentMembershipOrigin.Source)]
    [InlineData(ContentCollectionKind.Managed, ContentStorageOwnership.Managed, true, ContentMembershipOrigin.Source)]
    public void CollectionOwnershipAndMembershipOriginAreExplicit(ContentCollectionKind kind,
        ContentStorageOwnership owner, bool hasStorage, ContentMembershipOrigin origin)
    {
        var c = RealContentFixtures.Catalog();
        var next = c with
        {
            Collections = [c.Collections[0] with { Kind = kind, Ownership = owner, StorageLocator = hasStorage ? @"Z:\fixture" : null }],
            Memberships = [c.Memberships[0] with { Origin = origin }]
        };
        ContentCatalogPolicy.Freeze(next);
        Assert.Throws<ArgumentException>(() => ContentCatalogPolicy.Freeze(next with
        { Memberships = [next.Memberships[0] with { Origin = origin == ContentMembershipOrigin.User ? ContentMembershipOrigin.Source : ContentMembershipOrigin.User }] }));
        Assert.Throws<ArgumentException>(() => ContentCatalogPolicy.Freeze(next with
        { Collections = [next.Collections[0] with { Ownership = owner == ContentStorageOwnership.None ? ContentStorageOwnership.Managed : ContentStorageOwnership.None }] }));
    }

    [Theory]
    [InlineData(ContentOperation.Copy)] [InlineData(ContentOperation.Move)]
    [InlineData(ContentOperation.Rename)] [InlineData(ContentOperation.Recycle)]
    public void InstalledApplicationCannotAcquireFileMutationFromAdvertisedFlags(ContentOperation operation)
    {
        var item = RealContentFixtures.Item() with { Kind = ContentKind.Application };
        Assert.Equal(ContentCapabilityDenial.ProtectedApplication, ContentCapabilityPolicy.Evaluate(item, operation).Denial);
    }

    [Theory]
    [InlineData(ContentOperation.Open)] [InlineData(ContentOperation.Reference)]
    [InlineData(ContentOperation.Move)] [InlineData(ContentOperation.Copy)] [InlineData(ContentOperation.Link)]
    public void VirtualSourceRequiresExplicitMaterialization(ContentOperation operation)
    {
        var item = RealContentFixtures.Item() with { Kind = ContentKind.VirtualStream,
            Target = new(ContentTargetKind.Virtual, "provider-lease"), Capabilities = ContentCatalogPolicy.AllCapabilities };
        Assert.False(ContentCapabilityPolicy.Evaluate(item, operation).Allowed);
        Assert.True(ContentCapabilityPolicy.Evaluate(item, ContentOperation.Materialize).Allowed);
    }

    [Theory]
    [InlineData(ContentTransferRole.CopySource, true)]
    [InlineData(ContentTransferRole.MoveSource, false)]
    [InlineData(ContentTransferRole.Destination, false)]
    [InlineData(ContentTransferRole.None, true)]
    public void TransferReadAndMutationProtectionAreSeparate(ContentTransferRole role, bool canOpen)
    {
        var item = RealContentFixtures.Item();
        Assert.Equal(canOpen, ContentCapabilityPolicy.Evaluate(item, ContentOperation.Open, role).Allowed);
        Assert.Equal(role == ContentTransferRole.None, ContentCapabilityPolicy.Evaluate(item, ContentOperation.Rename, role).Allowed);
    }

    [Theory]
    [InlineData(ContentAvailability.Missing)] [InlineData(ContentAvailability.Unavailable)] [InlineData(ContentAvailability.AccessDenied)]
    public void StableUnavailableReferenceDoesNotGrantOpenOrDelete(ContentAvailability availability)
    {
        var item = RealContentFixtures.Item() with { Availability = availability };
        Assert.True(ContentCapabilityPolicy.Evaluate(item, ContentOperation.Reference).Allowed);
        Assert.False(ContentCapabilityPolicy.Evaluate(item, ContentOperation.Open).Allowed);
        Assert.False(ContentCapabilityPolicy.Evaluate(item, ContentOperation.Recycle).Allowed);
    }

    [Fact]
    public void ShortcutKeepsOriginalShellEntryNotItsResolvedExe()
    {
        var item = RealContentFixtures.Item() with { Kind = ContentKind.Shortcut,
            Target = new(ContentTargetKind.Shortcut, @"Z:\fixture-only\Game.lnk", "--opaque", @"Z:\work") };
        var request = RealContentFixtures.Request(RealContentFixtures.Catalog(item));
        Assert.Equal(item.Target, request.Target);
        Assert.EndsWith(".lnk", request.Target.Locator);
    }

    [Theory]
    [InlineData(ContentAvailability.RestrictedProtocol)] [InlineData(ContentAvailability.ParseFailed)]
    [InlineData(ContentAvailability.Unsupported)]
    public void UnsafeOrUnparsedSourceDoesNotBecomeReferenceThroughFallback(ContentAvailability state)
    {
        Assert.False(ContentCapabilityPolicy.Evaluate(RealContentFixtures.Item() with { Availability = state }, ContentOperation.Reference).Allowed);
    }

    [Fact]
    public void UnknownTypeCapabilityAndOperationFailClosed()
    {
        var item = RealContentFixtures.Item() with { Kind = ContentKind.Unknown, Target = new(ContentTargetKind.Unknown, "opaque") };
        Assert.Equal(ContentCapabilityDenial.UnknownType, ContentCapabilityPolicy.Evaluate(item, ContentOperation.Open).Denial);
        Assert.Throws<ArgumentException>(() => ContentCatalogPolicy.ValidateItem(item with { Capabilities = (ContentCapability)4096 }));
        Assert.False(ContentCapabilityPolicy.Evaluate(RealContentFixtures.Item(), (ContentOperation)999).Allowed);
        Assert.False(ContentCapabilityPolicy.Evaluate(RealContentFixtures.Item() with { Capabilities = ContentCapability.None }, ContentOperation.Open).Allowed);
    }
}
