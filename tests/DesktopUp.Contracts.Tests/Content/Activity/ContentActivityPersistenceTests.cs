using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using static DesktopUp.Contracts.Tests.ContentReferencePersistenceTests;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentActivityPersistenceTests
{
    private static ContentOpenRequest Request(Fixture f)
    {
        var card = f.Current.ContentCatalog.PosterItems.First();
        var target = new ContentActivationTarget(card.PosterId, card.LayoutItemId, 1, 1, "activity-test");
        return ContentOpenContract.Prepare(f.Current.ContentCatalog, new(target, Guid.NewGuid(), ContentOpenOrigin.Enter), target).Request!;
    }

    private static ContentOpenRequest Success(Fixture f, DateTimeOffset when)
    {
        var journal = new SqliteContentOpenJournal(f.Store);
        var request = Request(f);
        Assert.Equal(ContentOpenRequestAdmission.Ready, journal.TryStart(request));
        var completion = new ContentOpenCompletion(request.ActivationId, request.Source.ProviderId,
            ContentOpenOutcome.Succeeded, ContentOpenEvidence.ShellAccepted, when);
        Assert.NotNull(journal.Complete(request, completion));
        Assert.Null(journal.Complete(request, completion));
        return request;
    }

    [Fact]
    public void Incremental_statistics_survive_restart_and_never_inherit_by_path()
    {
        using var f = new Fixture(); var entry = Entry(); f.Commit(f.AddCommand(f.Main, entry));
        var activity = new SqliteContentActivityStore(f.Store);
        var added = activity.Read([entry.Item.Id]).Items[entry.Item.Id];
        Assert.Equal(0, added.TotalLaunches);
        var last = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        Success(f, last); Success(f, last.AddDays(-1));
        long revision = f.Store.ReadHead().Revision;
        var first = activity.ConsumeNext(1);
        Assert.Equal(1, first.Consumed); Assert.True(first.HasMore);
        using var reopened = new SqliteStateStore(f.Paths);
        Assert.True(reopened.Initialize().Succeeded);
        var next = new SqliteContentActivityStore(reopened);
        Assert.Equal(1, next.ConsumeNext(1).Consumed);
        Assert.Equal(0, next.ConsumeNext().Consumed);
        var current = next.Read([entry.Item.Id, Guid.NewGuid()]);
        var usage = Assert.Single(current.Items).Value;
        Assert.Equal(2, usage.TotalLaunches); Assert.Equal(last, usage.LastLaunchedAt);
        Assert.Equal(added.AddedAt, usage.AddedAt);
        Assert.False(current.HasPending); Assert.Equal(revision, reopened.ReadHead().Revision);
        // A distinct object at the exact same source is not a statistics key alias.
        var readded = Entry();
        Assert.Equal(entry.Item.Source, readded.Item.Source);
        Assert.Empty(next.Read([readded.Item.Id]).Items);
    }

    [Fact]
    public void Projection_failure_keeps_success_facts_and_rolls_back_counts_history_and_position_together()
    {
        using var f = new Fixture(); var entry = Entry(); f.Commit(f.AddCommand(f.Main, entry));
        Success(f, DateTimeOffset.UtcNow); Success(f, DateTimeOffset.UtcNow);
        var activity = new SqliteContentActivityStore(f.Store);
        f.Sql("CREATE TRIGGER reject_activity BEFORE UPDATE ON content_activity_state BEGIN SELECT RAISE(ABORT,'fixture'); END;");
        Assert.ThrowsAny<Exception>(() => activity.ConsumeNext());
        var before = activity.Read([entry.Item.Id]);
        Assert.Equal(0, before.Position); Assert.Equal(0, before.Items[entry.Item.Id].TotalLaunches);
        Assert.True(before.HasPending);
        Assert.Equal(2L, f.Sql("SELECT count(*) FROM content_open_facts"));
        Assert.Equal(0L, f.Sql("SELECT count(*) FROM content_activity_recent"));
        f.Sql("DROP TRIGGER reject_activity;");
        Assert.Equal(2, activity.ConsumeNext().Consumed);
        Assert.Equal(0, activity.ConsumeNext().Consumed);
        Assert.Equal(2, activity.Read([entry.Item.Id]).Items[entry.Item.Id].TotalLaunches);
    }

    [Fact]
    public void Schema26_facts_are_backfilled_once_without_replaying_opens_or_rewriting_history()
    {
        using var f = new Fixture(); var entry = Entry(); f.Commit(f.AddCommand(f.Main, entry));
        Success(f, DateTimeOffset.UtcNow.AddDays(-80));
        var original = f.Sql("SELECT fact_json FROM content_open_facts");
        f.Sql(StorageSchemaFixture.Drop27 + "UPDATE state_head SET schema_version=26; PRAGMA user_version=26;");
        var result = f.Store.Initialize();
        Assert.True(result.Succeeded, result.Error); Assert.Equal(26, result.PreviousSchemaVersion);
        Assert.Equal(27, result.Head!.SchemaVersion); Assert.NotNull(result.PreMigrationBackupFileName);
        var activity = new SqliteContentActivityStore(f.Store);
        Assert.Equal(1, activity.ConsumeNext().Consumed);
        Assert.True(f.Store.Initialize().Succeeded);
        Assert.Equal(0, activity.ConsumeNext().Consumed);
        Assert.Equal(original, f.Sql("SELECT fact_json FROM content_open_facts"));
        Assert.Equal(1L, f.Sql("SELECT count(*) FROM content_open_requests"));
        // Older versions did not record addition time. Migration starts a conservative
        // protection window; it does not invent a file creation time as product history.
        Assert.True(activity.Read([entry.Item.Id]).Items[entry.Item.Id].AddedAt > DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task Open_feedback_and_durable_fact_survive_consumer_failure_and_retry_does_not_reopen()
    {
        using var f = new Fixture(); var entry = Entry(); f.Commit(f.AddCommand(f.Main, entry));
        var activity = new SqliteContentActivityStore(f.Store);
        var errors = new List<Exception>(); var consumer = new ContentActivityConsumer(activity, errors.Add);
        var changed = new List<Guid>();
        consumer.StatisticsChanged += ids =>
        {
            // Notification follows the transaction: its reader already sees committed counts.
            Assert.False(activity.Read(ids).HasPending);
            Assert.Equal(1, activity.Read(ids).Items[entry.Item.Id].TotalLaunches);
            changed.AddRange(ids);
        };
        var worker = new Worker();
        var service = new ContentActionService(() => f.Current.ContentCatalog, worker, new SqliteContentOpenJournal(f.Store), consumer);
        f.Sql("CREATE TRIGGER reject_activity BEFORE UPDATE ON content_activity_state BEGIN SELECT RAISE(ABORT,'fixture'); END;");
        var request = Request(f);
        var target = new ContentActivationTarget(f.Main, f.Current.ContentCatalog.PosterItems[0].LayoutItemId, 1, 1, "activity-test");
        var response = await service.OpenAsync(new(target, request.ActivationId, ContentOpenOrigin.Enter), CancellationToken.None);
        Assert.True(response.Feedback.Succeeded);
        await consumer.WhenIdleAsync(); Assert.Single(errors);
        Assert.Empty(changed);
        Assert.True(activity.Read([entry.Item.Id]).HasPending);
        f.Sql("DROP TRIGGER reject_activity;");
        consumer.RequestRefresh(); await consumer.WhenIdleAsync();
        Assert.Equal(new[] { entry.Item.Id }, changed);
        consumer.RequestRefresh(); await consumer.WhenIdleAsync();
        Assert.Equal(new[] { entry.Item.Id }, changed); // Empty drains do not cause refresh loops.
        Assert.Equal(1, activity.Read([entry.Item.Id]).Items[entry.Item.Id].TotalLaunches);
        Assert.Equal(1, worker.Calls);
        await consumer.StopAsync(); consumer.RequestRefresh(); await consumer.WhenIdleAsync();
        Assert.Equal(1, worker.Calls);
    }

    private sealed class Worker : ILocalContentWorker
    {
        public int Calls { get; private set; }
        public ValueTask<LocalContentResponse> RunAsync(LocalContentRequest request, CancellationToken cancellation)
        {
            Calls++;
            return ValueTask.FromResult(new LocalContentResponse(request.RequestId, ContentAvailability.Available, "opened")
                { Outcome = ContentOpenOutcome.Succeeded, Evidence = ContentOpenEvidence.ShellAccepted });
        }
    }
}
