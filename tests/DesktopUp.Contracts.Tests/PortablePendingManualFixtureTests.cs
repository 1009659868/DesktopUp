using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Testing;

namespace DesktopUp.Contracts.Tests;

public sealed class PortablePendingManualFixtureTests
{
    private static PortableProfileDocument Document() => PortableProfileCodec.Parse(
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "portable-p4-pending.desktopup.json")));
    private static DesktopStateSnapshot State(params PosterDefinition[] posters) => new(
        new(12, 4, true, Array.AsReadOnly(posters.Select(p =>
            new PosterStateSnapshot(12, 11, p, DateTimeOffset.UnixEpoch, 7)).ToArray())),
        new(12, 10, new(false, DesktopUpPerformanceMode.Static, false), DateTimeOffset.UnixEpoch, 5));
    private static DisplayTopologySnapshot Topology(int width = 2560, int height = 1392) => new(
        "p4-manual-96dpi", Array.AsReadOnly(new[] { new DisplayMonitorSnapshot(@"\\.\DISPLAY1", true,
            new(0, 0, width, height), new(0, 0, width, height), 96, 96) }), 1, DateTimeOffset.UnixEpoch);

    [Fact]
    public void Checked_in_file_is_accepted_and_blocked_by_the_actual_single_poster_seed()
    {
        var document = Document();
        var source = Assert.Single(document.Posters);
        Assert.Equal(2480, source.WidthDip);
        Assert.Equal(1320, source.ExpandedHeightDip);
        Assert.True(source.IsLocked);
        Assert.False(source.IsCollapsed);
        var blocker = MeasurementFixtureData.Create(0, 1, "expanded");
        var plan = PortableProfilePlanner.Plan(document, State(blocker), Topology(), PortableImportMode.Append, false);
        var pending = Assert.Single(plan.Creates);
        Assert.Equal(new[] { pending.PosterId }, plan.PendingPosterIds);
        Assert.True(pending.IsVisible);
        Assert.True(pending.PlacementPending);
        Assert.Empty(plan.Updates);
        Assert.Empty(plan.Removes);
        Assert.Null(plan.Settings);
        var retry = PortableProfilePlanner.TryPlacePending(State(blocker, pending), Topology());
        Assert.Empty(retry.Updates);
        Assert.Equal(plan.PendingPosterIds, retry.PendingPosterIds);
    }

    [Fact]
    public void Removing_only_the_blocker_allows_retry_without_changing_identity_or_intent()
    {
        var imported = PortableProfilePlanner.Plan(Document(),
            State(MeasurementFixtureData.Create(0, 1, "expanded")), Topology(), PortableImportMode.Append, false);
        var pending = Assert.Single(imported.Creates);
        var retry = PortableProfilePlanner.TryPlacePending(State(pending), Topology());
        var placed = Assert.Single(retry.Updates);
        Assert.Empty(retry.PendingPosterIds);
        Assert.Empty(retry.Creates);
        Assert.Empty(retry.Removes);
        Assert.False(placed.PlacementPending);
        Assert.Equal(pending.PosterId, placed.PosterId);
        Assert.Equal(pending.ThemeInstanceId, placed.ThemeInstanceId);
        Assert.Equal(pending.LayoutParametersJson, placed.LayoutParametersJson);
        Assert.Equal(2480, placed.WidthDip);
        Assert.Equal(1320, placed.ExpandedHeightDip);
        Assert.True(placed.IsVisible);
        Assert.True(placed.IsLocked);
        Assert.False(placed.IsCollapsed);
        Assert.False(placed.AutoCollapseEnabled);
    }

    [Fact]
    public void Smaller_display_keeps_sample_pending_instead_of_silently_shrinking_it()
    {
        var plan = PortableProfilePlanner.Plan(Document(), State(), Topology(1920, 1080), PortableImportMode.Append, false);
        var pending = Assert.Single(plan.Creates);
        Assert.True(pending.PlacementPending);
        Assert.Equal(2480, pending.WidthDip);
        Assert.Equal(1320, pending.ExpandedHeightDip);
    }
}
