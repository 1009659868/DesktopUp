using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentActivitySizingTests
{
    private static ContentHeatProposal Heat(ContentLayoutDocument author, double scale = 2.7, long version = 1) =>
        new(author.PosterId, author.ItemSetVersion, version,
            author.Placements.Select((p, i) => new ContentHeatItem(p.ItemId, author.Placements.Count - i - 1, scale)).ToArray(),
            ContentLayoutProposals.ContinuousMappingVersion) { AuthorDocumentVersion = author.DocumentVersion };

    [Theory]
    [InlineData(ContentFlowAxis.Vertical)]
    [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void Frame_caps_and_restores_target_without_compounding_or_distorting_cards(ContentFlowAxis axis)
    {
        var author = ContentLayoutSamples.Equal("A", 1) with { AllowHeatDrivenSize = true, FlowAxis = axis };
        author = author with { Placements = [author.Placements[0] with { ColumnSpan = 8, RowSpan = 4 }] };
        var wide = new ContentLayoutViewport(1000, 1000, Scale: 1.3, Spacing: .8);
        var tight = axis == ContentFlowAxis.Vertical ? wide with { WidthDip = 210 } : wide with { HeightDip = 130 };
        var proposal = Heat(author);
        var session = new ContentHeatSession(); session.Reconcile(author, wide);
        Assert.True(session.Offer(proposal)); Assert.True(session.ApplyPending(author, wide, false).Applied);
        var full = ContentLayoutSolver.Resolve(session.Projection!, wide).Items.Single().Bounds;
        session.Reconcile(author, tight);
        var capped = ContentLayoutSolver.Resolve(session.Projection!, tight);
        Assert.False(capped.IsLayoutReadOnly);
        Assert.Equal(full.Width / full.Height, capped.Items[0].Bounds.Width / capped.Items[0].Bounds.Height, 8);
        Assert.Equal(axis == ContentFlowAxis.Vertical ? 210 : 130,
            axis == ContentFlowAxis.Vertical ? capped.Items[0].Bounds.Width : capped.Items[0].Bounds.Height, 7);
        Assert.Equal(1, session.ActiveVersion);
        session.Reconcile(author, wide);
        Assert.Equal(full, ContentLayoutSolver.Resolve(session.Projection!, wide).Items.Single().Bounds);
        Assert.Equal(8, author.Placements[0].ColumnSpan);
        Assert.Equal(4, author.Placements[0].RowSpan);
        var lower = ContentHeatSizing.LowerBound(author, tight);
        Assert.True(ContentLayoutEditing.SameState(lower, ContentHeatSizing.LowerBound(author, wide)));
        Assert.Throws<ContentLayoutException>(() => ContentHeatSizing.Project(author, proposal, new(20, 20)));
    }

    [Fact]
    public void Manual_sizes_and_mode_win_while_pins_only_fix_order_and_stale_proposals_do_not_apply()
    {
        var author = ContentLayoutSamples.Equal("A", 3) with { AllowHeatDrivenSize = true };
        author = author with { Placements = author.Placements.Select((p, i) => p with { ManualSize = i == 1, PinnedIndex = i == 0 ? 1 : null }).ToArray() };
        var viewport = new ContentLayoutViewport(900, 900);
        var projected = ContentHeatSizing.Project(author, Heat(author), viewport);
        Assert.Equal(author.Placements[1].ColumnSpan, projected.Placements[1].ColumnSpan);
        Assert.True(projected.Placements[0].ColumnSpan > author.Placements[0].ColumnSpan);
        Assert.Equal(author.Placements[0].ItemId, ContentLayoutSolver.Resolve(projected, viewport).Items[0].ItemId);
        var manual = ContentLayoutEditing.Apply(author, new(ContentLayoutEditKind.Mode) { Mode = ContentLayoutMode.Manual }, viewport).Document;
        Assert.Equal(manual, ContentHeatSizing.Project(manual, Heat(manual), viewport));
        var session = new ContentHeatSession(); session.Reconcile(author, viewport);
        Assert.True(session.Offer(Heat(author) with { AuthorDocumentVersion = 99 }));
        Assert.False(session.ApplyPending(author, viewport, false).Applied);
        Assert.Null(session.ActiveVersion);
    }

    [Fact]
    public void Old_integer_author_documents_get_a_fractional_projection_without_rewriting_history()
    {
        var old = ContentLayoutSamples.Equal("A", 1) with { SchemaVersion = 3, AllowHeatDrivenSize = true };
        var projection = ContentHeatSizing.Project(old, Heat(old, 1.571), new(150, 500));
        Assert.Equal(4, projection.SchemaVersion);
        Assert.Equal(3, old.SchemaVersion); Assert.Equal(4, old.Placements[0].ColumnSpan);
        Assert.False(ContentLayoutSolver.Resolve(projection, new(150, 500)).IsLayoutReadOnly);
        var legacyPin = old with { SchemaVersion = 2, Placements = [old.Placements[0] with { PinnedIndex = 1 }] };
        Assert.Throws<ContentLayoutException>(() => ContentHeatSizing.Project(legacyPin, Heat(legacyPin), new(500, 500)));
    }
}
