using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using DesktopUp.Windows;
using Fixture = DesktopUp.Contracts.Tests.ManagedTransferPersistenceTests.Fixture;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentActivityIdentityTests
{
    [Fact]
    public void Detached_reference_readded_from_same_path_gets_a_new_activity_identity()
    {
        using var f = new Fixture();
        string source = f.File("readded.txt");
        var original = LocalContentReferenceProbe.Inspect(source).Item!;
        f.AddReference(original);
        RecordOpen(f);
        var before = f.State;
        var card = Assert.Single(before.ContentCatalog.PosterItems);
        var reads = StateReadSet.ForPoster(f.Main, before.Catalog.Posters.Single().EntityVersion) with
            { ContentVersion = before.ContentVersion };
        var detach = new ContentDetachCommand(Guid.NewGuid().ToString(), StateCommandSource.ControlCenter,
            before.Revision, reads, f.Main, [card.LayoutItemId], true);
        var result = f.Repository.Execute(new(detach.RequestId, StateRequestFingerprint.Compute(detach), before.Revision, reads),
            state => ContentDetachPolicy.Prepare(detach, state));
        Assert.Equal(StateCommandStatus.Committed, result.Status);
        Assert.Empty(f.State.ContentCatalog.PosterItems);
        Assert.Contains(f.State.ContentCatalog.Memberships, m => m.ContentItemId == original.Id);

        // Exercise the production incoming preparation, not a fabricated replacement GUID.
        var preview = DirectTransferPreparation.Incoming(f.State, f.Main, [source], new(f.Root, f.Collection));
        var readded = Assert.Single(preview.Plan.Entries).Entry.Item;
        Assert.NotEqual(original.Id, readded.Id);
        Assert.Equal(original.Source, readded.Source);
        var statistics = new SqliteContentActivityStore(f.Store).Read([original.Id, readded.Id]);
        Assert.Equal(1, statistics.Items[original.Id].TotalLaunches);
        Assert.False(statistics.Items.ContainsKey(readded.Id));
        Assert.Empty(f.Journal.Read());
        Assert.Equal("owned-content", File.ReadAllText(source));
    }

    [Fact]
    public void Moving_a_live_reference_to_another_poster_keeps_the_same_activity_identity()
    {
        using var f = new Fixture();
        string source = f.File("moving.txt");
        var original = LocalContentReferenceProbe.Inspect(source).Item!;
        f.AddReference(original);
        RecordOpen(f);
        var before = f.State;
        var card = Assert.Single(before.ContentCatalog.PosterItems);
        var target = PosterConfigurationFactory.NewBasicBox("destination", "DISPLAY1") with
            { PosterId = Guid.NewGuid().ToString(), ThemeInstanceId = Guid.NewGuid().ToString() };
        var operation = Guid.NewGuid().ToString();
        var created = f.Repository.Execute(new(operation, operation, before.Revision,
            StateReadSet.ForCatalog(before.Catalog.CatalogVersion)), _ => new() { Creates = [target] });
        Assert.Equal(StateCommandStatus.Committed, created.Status);

        var preview = DirectTransferPreparation.Incoming(f.State, target.PosterId, [source], new(f.Root, f.Collection));
        Assert.Equal(original.Id, Assert.Single(preview.Plan.Entries).Entry.Item.Id);
        Assert.Equal(1, new SqliteContentActivityStore(f.Store).Read([original.Id]).Items[original.Id].TotalLaunches);
        Assert.Equal(card, Assert.Single(f.State.ContentCatalog.PosterItems));
        Assert.Empty(f.Journal.Read());
        Assert.Equal("owned-content", File.ReadAllText(source));
    }

    private static void RecordOpen(Fixture f)
    {
        var state = f.State;
        var card = Assert.Single(state.ContentCatalog.PosterItems);
        var target = new ContentActivationTarget(card.PosterId, card.LayoutItemId, 1, 1, "activity-identity");
        var request = ContentOpenContract.Prepare(state.ContentCatalog,
            new(target, Guid.NewGuid(), ContentOpenOrigin.Enter), target).Request!;
        var journal = new SqliteContentOpenJournal(f.Store);
        Assert.Equal(ContentOpenRequestAdmission.Ready, journal.TryStart(request));
        Assert.NotNull(journal.Complete(request, new(request.ActivationId, request.Source.ProviderId,
            ContentOpenOutcome.Succeeded, ContentOpenEvidence.ShellAccepted, DateTimeOffset.UtcNow)));
        Assert.Equal(1, new SqliteContentActivityStore(f.Store).ConsumeNext().Consumed);
    }
}
