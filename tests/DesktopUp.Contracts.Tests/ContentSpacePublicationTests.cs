using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentSpacePublicationTests
{
    private static ContentSpaceRequest Request(ContentFlowAxis axis, double cross, long generation, ContentSpaceCause cause)
    {
        var doc = ContentLayoutSamples.Equal("space", 6) with { FlowAxis = axis, LoopScroll = true };
        var view = new ContentLayoutViewport(axis == ContentFlowAxis.Vertical ? cross : 150,
            axis == ContentFlowAxis.Vertical ? 150 : cross, Generation: generation);
        var snapshot = ContentLayoutSolver.Resolve(doc, view);
        var env = new SpatialEnvironment("display", new(0, 0, 1400, 1000), 96, 96, generation, 1,
            Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var witness = new ContentSpaceWitness("space", Guid.Parse("22222222-2222-2222-2222-222222222222"),
            doc.DocumentVersion, doc.ItemSetVersion, 0, env, new(0, 0, 600, 600), "m", 1, generation, view.WidthDip, view.HeightDip);
        return new(witness, snapshot, view, cause);
    }

    [Theory]
    [InlineData(ContentFlowAxis.Vertical)] [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void FailedOrStalePublicationCannotReplaceTheRecoveryBasis(ContentFlowAxis axis)
    {
        var ledger = new ContentSpacePublication();
        var normal = Request(axis, 500, 1, ContentSpaceCause.AuthoritativeRefresh);
        var first = ContentSpacePolicy.Evaluate(normal, normal.Witness);
        Assert.True(ledger.TryPublish(first, normal.Witness, true));
        var small = Request(axis, 40, 2, ContentSpaceCause.PassivePhysicalEnvironmentChange);
        var restricted = ContentSpacePolicy.Evaluate(small, small.Witness, ledger.Published);
        Assert.True(restricted.IsRestricted);
        Assert.False(ledger.TryPublish(restricted, small.Witness, false));
        Assert.Same(first, ledger.Published);
        Assert.False(ledger.TryPublish(restricted, small.Witness with { ViewportGeneration = 3 }, true));
        Assert.True(ledger.TryPublish(restricted, small.Witness, true));
        Assert.False(ledger.TryPublish(first, normal.Witness, true));
        var latest = Request(axis, 500, 3, ContentSpaceCause.PassivePhysicalEnvironmentChange);
        var pending = ContentSpacePolicy.Evaluate(latest with { AtSafePoint = false }, latest.Witness, ledger.Published);
        Assert.False(ledger.TryPublish(pending, latest.Witness, true));
        Assert.Same(restricted, ledger.Published);
        var recovered = ContentSpacePolicy.Evaluate(latest, latest.Witness, ledger.Published);
        Assert.Equal(ContentSpaceReason.Recovered, recovered.Reason);
        Assert.True(ledger.TryPublish(recovered, latest.Witness, true));
    }

    [Theory]
    [InlineData(ContentFlowAxis.Vertical)] [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void RestrictedNavigationAndReclipKeepBothAxesFiniteWithoutSavingPreferences(ContentFlowAxis axis)
    {
        var request = Request(axis, 40, 1, ContentSpaceCause.ColdStartPhysicalShortage);
        var doc = ContentLayoutSamples.Equal("space", 6) with { FlowAxis = axis, LoopScroll = true };
        var raw = ContentLayoutPolicy.Serialize(doc);
        var snapshot = request.Snapshot!;
        var view = request.Viewport!;
        var mode = ContentBrowseMode.ReadOnlyOverflow;
        var cross = ContentWheelScrollPolicy.Advance(snapshot, view, -120000, mode, crossAxis: true);
        view = view with { ScrollXDip = cross.ScrollXDip, ScrollYDip = cross.ScrollYDip };
        var main = ContentWheelScrollPolicy.Advance(snapshot, view, -120000, mode);
        view = view with { ScrollXDip = main.ScrollXDip, ScrollYDip = main.ScrollYDip };
        var clipped = ContentLayoutSolver.Reclip(snapshot, view, mode);
        Assert.Equal(Math.Max(0, snapshot.ExtentWidthDip - view.WidthDip), clipped.ScrollXDip);
        Assert.Equal(Math.Max(0, snapshot.ExtentHeightDip - view.HeightDip), clipped.ScrollYDip);
        Assert.All(ContentLayoutFlow.ProjectTiles(clipped, view, mode), t => Assert.Equal(0, t.LoopIndex));
        Assert.Equal(snapshot.Items.Select(i => i.Bounds), clipped.Items.Select(i => i.Bounds));
        Assert.Equal(raw, ContentLayoutPolicy.Serialize(doc));
        var back = ContentWheelScrollPolicy.Advance(clipped, view, 120000, mode, crossAxis: true);
        Assert.Equal(axis == ContentFlowAxis.Vertical ? clipped.ScrollYDip : clipped.ScrollXDip,
            axis == ContentFlowAxis.Vertical ? back.ScrollYDip : back.ScrollXDip);
    }

    [Theory]
    [InlineData(ContentFlowAxis.Vertical)] [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void OwnerMustProvePhysicalCapacityRatherThanACompactFrameOrGeneration(ContentFlowAxis axis)
    {
        var small = Request(axis, 40, 2, ContentSpaceCause.AuthoritativeRefresh);
        // Same small content frame, but the monitor can fit it: collision/active/old-invalid
        // geometry is not an emergency exception, even when the display generation changed.
        Assert.Equal(ContentSpaceCause.AuthoritativeRefresh, ContentSpaceOwnerAdmission.Classify(small.Witness, small.Snapshot!, null, 800));
        bool vertical = axis == ContentFlowAxis.Vertical;
        var frame = vertical ? new SpatialRect(0, 0, 80, 250) : new SpatialRect(0, 0, 250, 80);
        var witness = small.Witness with { EffectiveFrame = frame,
            Environment = small.Witness.Environment with { WorkArea = frame } };
        Assert.Equal(ContentSpaceCause.ColdStartPhysicalShortage, ContentSpaceOwnerAdmission.Classify(witness, small.Snapshot!, null, 500));
        Assert.Equal(ContentSpaceCause.AuthoritativeRefresh, ContentSpaceOwnerAdmission.Classify(witness, small.Snapshot!, null, 80));
        var normal = Request(axis, 500, 1, ContentSpaceCause.AuthoritativeRefresh);
        var published = ContentSpacePolicy.Evaluate(normal, normal.Witness);
        Assert.Equal(ContentSpaceCause.PassivePhysicalEnvironmentChange, ContentSpaceOwnerAdmission.Classify(witness, small.Snapshot!, published, 500));
        Assert.Equal(ContentSpaceCause.AuthoritativeRefresh, ContentSpaceOwnerAdmission.Classify(small.Witness, small.Snapshot!, published, 500));
    }

    [Theory]
    [InlineData("poster")] [InlineData("surface")] [InlineData("runtime")]
    [InlineData("document")] [InlineData("members")] [InlineData("layout")]
    public void PublicationRejectsForeignOrRegressedAuthorityEvenWithANewViewport(string changed)
    {
        var request = Request(ContentFlowAxis.Vertical, 500, 1, ContentSpaceCause.AuthoritativeRefresh);
        var basis = request.Witness with { DocumentVersion = 3, ItemSetVersion = 3, LayoutGeneration = 3 };
        var first = ContentSpacePolicy.Evaluate(request, request.Witness) with { Witness = basis };
        var ledger = new ContentSpacePublication(); Assert.True(ledger.TryPublish(first, basis, true));
        var next = basis with { ViewportGeneration = 2 };
        next = changed switch {
            "poster" => next with { PosterId = "other" }, "surface" => next with { SurfaceSessionId = Guid.NewGuid() },
            "runtime" => next with { Environment = next.Environment with { RuntimeSessionId = Guid.NewGuid() } },
            "document" => next with { DocumentVersion = 2 }, "members" => next with { ItemSetVersion = 2 },
            _ => next with { LayoutGeneration = 2 } };
        Assert.False(ledger.TryPublish(first with { Witness = next }, next, true)); Assert.Same(first, ledger.Published);
    }
}
