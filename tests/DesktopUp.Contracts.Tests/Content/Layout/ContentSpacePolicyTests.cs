using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentSpacePolicyTests
{
    private static readonly Guid Surface = Guid.Parse("abc70000-0000-0000-0000-000000000001");
    private static readonly Guid Runtime = Guid.Parse("abc70000-0000-0000-0000-000000000002");

    [Theory] [InlineData(ContentFlowAxis.Vertical)] [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void OnlyAnObservedPhysicalChangeGrantsRestrictedBrowsing(ContentFlowAxis axis)
    {
        var normal = Observation(axis, 300);
        var published = ContentSpacePolicy.Evaluate(normal, normal.Witness);
        Assert.Equal(ContentSpaceDisposition.Publish, published.Disposition);
        Assert.False(published.IsReadOnly);
        var small = Observation(axis, 180, ContentSpaceCause.PassivePhysicalEnvironmentChange, generation: 2, displayGeneration: 2);
        var restricted = ContentSpacePolicy.Evaluate(small, small.Witness, published);
        Assert.True(restricted.IsRestricted); Assert.True(restricted.IsReadOnly); Assert.True(restricted.CancelContentGesture);
        Assert.Equal(ContentSpaceReason.PassivePhysicalShortage, restricted.Reason);
        Assert.True(small.Snapshot!.LoopScroll);

        var claimedChange = small with { Witness = small.Witness with { Environment = normal.Witness.Environment } };
        var rejected = ContentSpacePolicy.Evaluate(claimedChange, claimedChange.Witness, published);
        Assert.Equal(ContentSpaceReason.UnqualifiedShortage, rejected.Reason);
        Assert.Null(rejected.BrowseMode);
    }

    [Theory]
    [InlineData(ContentSpaceCause.ActiveResize, ContentSpaceReason.ActiveResizeBelowMinimum)]
    [InlineData(ContentSpaceCause.HeatProposal, ContentSpaceReason.HeatProposalBelowMinimum)]
    [InlineData(ContentSpaceCause.PresentationProposal, ContentSpaceReason.PresentationProposalBelowMinimum)]
    public void ProposalsBelowMinimumKeepThePublishedFrameWithoutAcquiringEmergencyEligibility(ContentSpaceCause cause, ContentSpaceReason reason)
    {
        var normal = Observation();
        var published = ContentSpacePolicy.Evaluate(normal, normal.Witness);
        var proposal = Observation(cross: 180, cause: cause, generation: 2, displayGeneration: 2);
        var rejected = ContentSpacePolicy.Evaluate(proposal, proposal.Witness, published);
        Assert.Equal(ContentSpaceDisposition.RejectCandidate, rejected.Disposition);
        Assert.Equal(reason, rejected.Reason); Assert.True(rejected.RetainPublishedFrame);
        Assert.Null(rejected.BrowseMode); Assert.Null(rejected.Offsets);
    }

    [Fact]
    public void OuterCollisionAndLegacyConstraintsCannotBorrowThePhysicalShortageException()
    {
        var collision = Observation(cross: 180, cause: ContentSpaceCause.OuterCollision);
        var deferred = ContentSpacePolicy.Evaluate(collision, collision.Witness);
        Assert.Equal(ContentSpaceDisposition.DeferToOuterOwner, deferred.Disposition);
        Assert.Equal(ContentSpaceReason.OuterSpaceConstraint, deferred.Reason);
        Assert.Null(deferred.BrowseMode);
        var legacy = collision with { Cause = ContentSpaceCause.ColdStartPhysicalShortage,
            Snapshot = collision.Snapshot! with { SpaceStatus = ContentLayoutSpaceStatus.LegacyConstraintConflict } };
        var result = ContentSpacePolicy.Evaluate(legacy, legacy.Witness);
        Assert.Equal(ContentSpaceReason.LegacyConstraintConflict, result.Reason);
        Assert.False(result.IsRestricted); Assert.Null(result.Offsets);
    }

    [Fact]
    public void ColdStartRequiresExplicitQualificationAndNeverNeedsAnOldRamLayout()
    {
        var small = Observation(cross: 180);
        Assert.Equal(ContentSpaceReason.UnqualifiedShortage, ContentSpacePolicy.Evaluate(small, small.Witness).Reason);
        var qualified = small with { Cause = ContentSpaceCause.ColdStartPhysicalShortage };
        var restricted = ContentSpacePolicy.Evaluate(qualified, qualified.Witness);
        Assert.True(restricted.IsRestricted);
        Assert.Equal(ContentSpaceReason.ColdStartPhysicalShortage, restricted.Reason);
        Assert.NotNull(restricted.Offsets);
    }

    [Fact]
    public void RecoveryWaitsForASafePointWithoutPassingANormalSnapshotToOverflowFlow()
    {
        var small = Observation(cross: 180, cause: ContentSpaceCause.ColdStartPhysicalShortage);
        var restricted = ContentSpacePolicy.Evaluate(small, small.Witness);
        var recovered = Observation(cross: 300, cause: ContentSpaceCause.PassivePhysicalEnvironmentChange,
            generation: 2, displayGeneration: 2) with { AtSafePoint = false };
        var pending = ContentSpacePolicy.Evaluate(recovered, recovered.Witness, restricted);
        Assert.Equal(ContentSpaceDisposition.PendingSafePoint, pending.Disposition);
        Assert.True(pending.RecoveryEligible); Assert.True(pending.RetainPublishedFrame);
        Assert.Null(pending.BrowseMode); Assert.Null(pending.Offsets);
        var mistakenPublication = ContentSpacePolicy.Evaluate(recovered, recovered.Witness, pending);
        Assert.Equal(ContentSpaceReason.InvalidObservation, mistakenPublication.Reason);
        Assert.Null(mistakenPublication.BrowseMode);
        var published = ContentSpacePolicy.Evaluate(recovered with { AtSafePoint = true }, recovered.Witness, restricted);
        Assert.Equal(ContentSpaceReason.Recovered, published.Reason);
        Assert.Equal(ContentBrowseMode.SingleAxis, published.BrowseMode);
        Assert.False(published.IsReadOnly);
    }

    [Fact]
    public void RecoveryUsesLatestMembersAndTheSameItemsInternalPointInsteadOfReplayingOldGeometry()
    {
        var small = Observation(cross: 180, cause: ContentSpaceCause.ColdStartPhysicalShortage);
        small = small with { Snapshot = small.Snapshot! with { ScrollYDip = 360 }, Viewport = small.Viewport! with { ScrollYDip = 360 } };
        var restricted = ContentSpacePolicy.Evaluate(small, small.Witness);
        var tile = Assert.Single(ContentLayoutFlow.ProjectTiles(small.Snapshot!, small.Viewport!, ContentBrowseMode.ReadOnlyOverflow));
        var anchor = ContentLayoutFlow.CaptureAnchor(small.Snapshot!, small.Viewport!, tile, 20, 70, ContentBrowseMode.ReadOnlyOverflow);
        Assert.Equal("p:second", anchor.ItemId); Assert.Equal(30, anchor.InsideYDip);

        var latest = Observation(cross: 300, generation: 2, displayGeneration: 2);
        latest = latest with
        {
            Witness = latest.Witness with { DocumentVersion = 2, ItemSetVersion = 2 },
            Snapshot = latest.Snapshot! with { DocumentVersion = 2, ItemSetVersion = 2, ExtentHeightDip = 740,
                Items = [latest.Snapshot.Items[1] with { ReadOrder = 0, Bounds = new(0, 500, 240, 240) }] },
            Anchor = anchor
        };
        var restored = ContentSpacePolicy.Evaluate(latest, latest.Witness, restricted);
        Assert.Equal(ContentSpaceReason.Recovered, restored.Reason);
        Assert.Equal(new ContentBrowseOffsets(0, 460), restored.Offsets!.Value);
        Assert.Equal(2, restored.Witness.ItemSetVersion);
        Assert.Single(latest.Snapshot!.Items);
        Assert.Equal(500, latest.Snapshot.Items[0].Bounds.Y);
    }

    [Theory] [InlineData("document")] [InlineData("members")] [InlineData("surface")]
    [InlineData("environment")] [InlineData("viewport")]
    public void LateResultsCannotPublishAgainstANewerWitness(string changed)
    {
        var request = Observation();
        var current = changed switch
        {
            "document" => request.Witness with { DocumentVersion = 2 },
            "members" => request.Witness with { ItemSetVersion = 2 },
            "surface" => request.Witness with { SurfaceSessionId = Guid.NewGuid() },
            "environment" => request.Witness with { Environment = request.Witness.Environment with { DisplayGeneration = 2 } },
            _ => request.Witness with { ViewportGeneration = 2 }
        };
        var result = ContentSpacePolicy.Evaluate(request, current);
        Assert.Equal(ContentSpaceDisposition.Stale, result.Disposition);
        Assert.Null(result.BrowseMode); Assert.Null(result.Offsets);
    }

    [Theory] [InlineData("snapshot-owner")] [InlineData("snapshot-document")] [InlineData("snapshot-members")]
    [InlineData("snapshot-metrics")] [InlineData("viewport-generation")] [InlineData("viewport-size")]
    public void MatchingOuterWitnessCannotLaunderAnUnrelatedLayoutOrViewport(string mismatch)
    {
        var request = Observation();
        request = mismatch switch
        {
            "snapshot-owner" => request with { Snapshot = request.Snapshot! with { PosterId = "foreign" } },
            "snapshot-document" => request with { Snapshot = request.Snapshot! with { DocumentVersion = 2 } },
            "snapshot-members" => request with { Snapshot = request.Snapshot! with { ItemSetVersion = 2 } },
            "snapshot-metrics" => request with { Snapshot = request.Snapshot! with { ThemeMetricsVersion = 2 } },
            "viewport-generation" => request with { Viewport = request.Viewport! with { Generation = 2 } },
            _ => request with { Viewport = request.Viewport! with { WidthDip = 900 } }
        };
        var result = ContentSpacePolicy.Evaluate(request, request.Witness);
        Assert.Equal(ContentSpaceReason.InvalidObservation, result.Reason);
        Assert.Null(result.Offsets);
    }

    [Fact]
    public void HiddenMissingUnsafeOrUnparseableSurfacesNeverProduceAUsableWindow()
    {
        var request = Observation();
        var hidden = request with { Witness = request.Witness with { IsVisible = false } };
        Assert.Equal(ContentSpaceReason.HiddenOrRemoved, ContentSpacePolicy.Evaluate(hidden, hidden.Witness).Reason);
        var unsafeSurface = request with { Witness = request.Witness with { HasSafeSurface = false } };
        Assert.Equal(ContentSpaceReason.UnsafeSurface, ContentSpacePolicy.Evaluate(unsafeSurface, unsafeSurface.Witness).Reason);
        var outside = request with { Witness = request.Witness with { EffectiveFrame = new(4000, 0, 320, 240) } };
        Assert.Equal(ContentSpaceReason.UnsafeSurface, ContentSpacePolicy.Evaluate(outside, outside.Witness).Reason);
        Assert.Equal(ContentSpaceReason.InvalidObservation, ContentSpacePolicy.Evaluate(request with { Snapshot = null }, request.Witness).Reason);
    }

    [Fact]
    public void EmptyContentUsesTheExistingSafeSurfaceWithoutInventingAnItemMinimum()
    {
        var request = Observation(cross: 180);
        request = request with { Snapshot = request.Snapshot! with
            { Items = [], ExtentWidthDip = 0, ExtentHeightDip = 0, MinimumCrossAxisDip = 0, SpaceStatus = ContentLayoutSpaceStatus.Normal } };
        var result = ContentSpacePolicy.Evaluate(request, request.Witness);
        Assert.Equal(ContentSpaceReason.Normal, result.Reason);
        Assert.Equal(new ContentBrowseOffsets(0, 0), result.Offsets!.Value);
        Assert.False(result.IsRestricted);
    }

    [Fact]
    public void PhysicalSurfaceValidationUsesCurrentDpiOnEachAxis()
    {
        var request = Observation();
        var scaled = request.Witness with { Environment = request.Witness.Environment with { DpiX = 144, DpiY = 120 } };
        Assert.Equal(ContentSpaceReason.UnsafeSurface, ContentSpacePolicy.Evaluate(request with { Witness = scaled }, scaled).Reason);
        scaled = scaled with { EffectiveFrame = new(0, 0, 480, 300) };
        Assert.Equal(ContentSpaceDisposition.Publish, ContentSpacePolicy.Evaluate(request with { Witness = scaled }, scaled).Disposition);
    }

    private static ContentSpaceRequest Observation(ContentFlowAxis axis = ContentFlowAxis.Vertical, double cross = 300,
        ContentSpaceCause cause = ContentSpaceCause.AuthoritativeRefresh, long generation = 1, long displayGeneration = 1)
    {
        bool vertical = axis == ContentFlowAxis.Vertical;
        double width = vertical ? cross : 200, height = vertical ? 200 : cross;
        var environment = new SpatialEnvironment("DISPLAY1", new(0, 0, 2000, 1500), 96, 96, displayGeneration, 1, Runtime);
        var frame = new SpatialRect(0, 0, (int)width + 20, (int)height + 40);
        var witness = new ContentSpaceWitness("p", Surface, 1, 1, 1, environment, frame, "metrics-1", 1, generation, width, height);
        var viewport = new ContentLayoutViewport(width, height, Generation: generation);
        var snapshot = new ContentLayoutSnapshot("p", 1, 1, generation, 1, 10, 24, 20,
            vertical ? 240 : 640, vertical ? 640 : 240, 0, 0,
            [new("p:first", 0, 0, 0, 0, 8, 8, new(0, 0, 240, 240), default),
             new("p:second", 1, 1, 0, 0, 8, 8, new(vertical ? 0 : 400, vertical ? 400 : 0, 240, 240), default)], [])
        {
            FlowAxis = axis, LoopScroll = true, MinimumCrossAxisDip = 240, AvailableCrossAxisDip = cross,
            SpaceStatus = cross < 240 ? ContentLayoutSpaceStatus.InsufficientCrossAxis : ContentLayoutSpaceStatus.Normal
        };
        return new(witness, snapshot, viewport, cause);
    }
}
