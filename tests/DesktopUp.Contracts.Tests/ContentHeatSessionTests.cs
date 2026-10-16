using DesktopUp.Application;
using DesktopUp.Domain;
using static DesktopUp.Contracts.Tests.ContentLayoutSolverTests;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentHeatSessionTests
{
    private static ContentHeatProposal Heat(ContentLayoutDocument author, long version, double scale = 1) =>
        new(author.PosterId, author.ItemSetVersion, version, author.Placements.Select((item, index) =>
            new ContentHeatItem(item.ItemId, author.Placements.Count - index - 1, scale)).ToArray());

    [Fact]
    public void ProtectedSessionRetainsLatestAndDoesNotPublishOrPersist()
    {
        var author = Doc(3) with { AllowHeatDrivenSize = true };
        string saved = ContentLayoutPolicy.Serialize(author);
        var session = new ContentHeatSession();
        session.Reconcile(author, View());
        Assert.True(session.Offer(Heat(author, 1, .5)));
        Assert.True(session.Offer(Heat(author, 2, 1.5)));
        Assert.False(session.ApplyPending(author, View(), true).Applied);
        Assert.True(session.HasPending);
        Assert.Null(session.Projection);
        Assert.True(session.ApplyPending(author, View(), false).Applied);
        Assert.Equal(2L, session.ActiveVersion);
        Assert.False(session.HasPending);
        Assert.Equal(ContentLayoutPolicy.Serialize(ContentLayoutProposals.ResolveHeat(author, Heat(author, 2, 1.5)).Document),
            ContentLayoutPolicy.Serialize(session.Projection!));
        Assert.Equal(saved, ContentLayoutPolicy.Serialize(author));
        Assert.False(session.Offer(Heat(author, 1)));
    }

    [Fact]
    public void AuthorEditsRebaseAcceptedHeatWithoutCompoundingOrLosingOverrides()
    {
        var author = Doc(3) with { AllowHeatDrivenSize = true };
        var session = new ContentHeatSession();
        session.Reconcile(author, View());
        session.Offer(Heat(author, 1, 1.5));
        session.ApplyPending(author, View(), false);
        var edited = author with { DocumentVersion = author.DocumentVersion + 1,
            Placements = author.Placements.Select((item, index) => index == 0
                ? item with { ManualSize = true, ColumnSpan = 3, PinnedIndex = 1 } : item).ToArray() };
        session.Reconcile(edited, View());
        string first = ContentLayoutPolicy.Serialize(session.Projection!);
        session.Reconcile(edited, View());
        Assert.Equal(first, ContentLayoutPolicy.Serialize(session.Projection!));
        Assert.Equal(3, session.Projection!.Placements[0].ColumnSpan);
        Assert.Equal(1, session.Projection.Placements[0].PinnedIndex);
        Assert.Equal(6, session.Projection.Placements[1].ColumnSpan);
        var manual = ContentLayoutEditing.Apply(edited, new(ContentLayoutEditKind.Mode)
            { Mode = ContentLayoutMode.Manual }, View()).Document;
        session.Reconcile(manual, View());
        Assert.Equal(ContentLayoutPolicy.Serialize(manual), ContentLayoutPolicy.Serialize(session.Projection!));
    }

    [Fact]
    public void RejectedProposalKeepsLastGoodAndConsumesItsVersion()
    {
        var author = Doc(2);
        var session = new ContentHeatSession();
        session.Reconcile(author, View());
        session.Offer(Heat(author, 1));
        session.ApplyPending(author, View(), false);
        var accepted = session.Projection;
        session.Offer(Heat(author, 2));
        var result = session.ApplyPending(author, View(width: 10), false);
        Assert.False(result.Applied);
        Assert.Equal(2L, result.RejectedVersion);
        Assert.NotNull(result.Error);
        Assert.Same(accepted, session.Projection);
        Assert.Equal(1L, session.ActiveVersion);
        Assert.False(session.HasPending);
        Assert.False(session.Offer(Heat(author, 2)));
        Assert.True(session.Offer(Heat(author, 3)));
        Assert.True(session.ApplyPending(author, View(), false).Applied);
    }

    [Fact]
    public void FailedAuthorRebaseDropsOnlyProjectionAndCanRecover()
    {
        var author = Doc(2);
        var session = new ContentHeatSession();
        session.Reconcile(author, View());
        session.Offer(Heat(author, 1));
        session.ApplyPending(author, View(), false);
        string accepted = ContentLayoutPolicy.Serialize(session.Projection!);
        session.Reconcile(author, View(width: 10));
        Assert.Null(session.Projection);
        Assert.Equal(1L, session.ActiveVersion);
        session.Reconcile(author, View());
        Assert.Equal(accepted, ContentLayoutPolicy.Serialize(session.Projection!));
    }

    [Fact]
    public void MembershipPreviewDoesNotInvalidateUntilCommittedReconciliation()
    {
        var author = Doc(2);
        var session = new ContentHeatSession();
        session.Reconcile(author, View());
        session.Offer(Heat(author, 1));
        session.ApplyPending(author, View(), false);
        var accepted = session.Projection;
        session.Offer(Heat(author, 2));
        var changed = author with { ItemSetVersion = author.ItemSetVersion + 1,
            DocumentVersion = author.DocumentVersion + 1, Placements = author.Placements.Take(1).ToArray() };
        Assert.Same(changed, session.ProjectEdit(changed));
        Assert.Same(accepted, session.Projection);
        Assert.True(session.HasPending);
        session.Reconcile(changed, View());
        Assert.Null(session.Projection);
        Assert.Null(session.ActiveVersion);
        Assert.False(session.HasPending);
        Assert.False(session.Offer(Heat(author, 3)));
        Assert.True(session.Offer(Heat(changed, 1)));
    }

    [Fact]
    public void ClosedSessionCannotBeRevivedByLateWork()
    {
        var author = Doc(2);
        var session = new ContentHeatSession();
        session.Reconcile(author, View());
        session.Offer(Heat(author, 1));
        session.Close();
        session.Close();
        session.Reconcile(author, View());
        Assert.False(session.Offer(Heat(author, 2)));
        Assert.False(session.ApplyPending(author, View(), false).Applied);
        Assert.Null(session.Projection);
        Assert.Null(session.ActiveVersion);
        Assert.False(session.HasPending);
        Assert.Same(author, session.ProjectEdit(author));
    }
}
