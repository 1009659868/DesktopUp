using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using static DesktopUp.Contracts.Tests.ContentReferencePersistenceTests;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentActionServiceTests
{
    [Fact]
    public async Task CancelledBeforeAdmissionDoesNotClaimOrDispatch()
    {
        using var fixture = Prepared();
        var worker = new Worker((_, _) => throw new InvalidOperationException("Must not dispatch."));
        var service = Service(fixture, worker);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await service.OpenAsync(Intent(fixture), cancellation.Token);

        Assert.Equal(ContentOpenOutcome.Cancelled, result.Feedback.Outcome);
        Assert.Null(result.Observation);
        Assert.Equal(0, worker.Calls);
        Assert.Equal(0L, fixture.Sql("SELECT count(*) FROM content_open_requests"));
    }

    [Fact]
    public async Task RelationChangesAfterAdmissionCompleteWithoutDispatch()
    {
        using var fixture = Prepared();
        var worker = new Worker((_, _) => throw new InvalidOperationException("Must not dispatch."));
        int reads = 0;
        var service = new ContentActionService(
            () => ++reads == 1 ? fixture.Current.ContentCatalog : ContentCatalogSnapshot.Empty,
            worker, new SqliteContentOpenJournal(fixture.Store));

        var result = await service.OpenAsync(Intent(fixture), CancellationToken.None);

        Assert.Equal(ContentOpenOutcome.Unavailable, result.Feedback.Outcome);
        Assert.Null(result.Observation);
        Assert.Equal(0, worker.Calls);
        Assert.Equal(1L, fixture.Sql("SELECT completed FROM content_open_requests"));
        Assert.Equal(0L, fixture.Sql("SELECT count(*) FROM content_open_facts"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ForeignResponseOrWorkerExceptionDoesNotCreateFactOrRetry(bool throws)
    {
        using var fixture = Prepared();
        var worker = new Worker((request, _) => throws
            ? throw new IOException("fixture")
            : ValueTask.FromResult(Success(request) with { RequestId = Guid.NewGuid() }));
        var service = Service(fixture, worker);
        var intent = Intent(fixture);

        var result = await service.OpenAsync(intent, CancellationToken.None);

        Assert.Equal(ContentOpenOutcome.Failed, result.Feedback.Outcome);
        Assert.False(result.Feedback.Succeeded);
        Assert.Equal(ContentOpenEvidence.None, result.Observation!.Response.Evidence);
        Assert.Equal(0L, fixture.Sql("SELECT count(*) FROM content_open_facts"));
        Assert.Equal(1L, fixture.Sql("SELECT completed FROM content_open_requests"));
        Assert.False((await service.OpenAsync(intent, CancellationToken.None)).Feedback.Succeeded);
        Assert.Equal(1, worker.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfirmedHandoffSurvivesRemovalOrRepairWithoutStaleViewUpdate(bool remove)
    {
        using var fixture = Prepared();
        var completion = new TaskCompletionSource<LocalContentResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        LocalContentRequest? dispatched = null;
        var worker = new Worker((request, _) =>
        {
            dispatched = request;
            return new(completion.Task);
        });
        var views = new ReferenceContentRuntime(fixture.Current.ContentCatalog, fixture.Main, 1, worker);
        var actions = new ReferenceContentActions(Service(fixture, worker), views);
        var intent = Intent(fixture);
        var pending = actions.OpenAsync(intent, CancellationToken.None).AsTask();
        Assert.NotNull(dispatched);
        var current = fixture.Current;

        if (remove)
        {
            var detached = fixture.Coordinator.Execute(new ContentDetachCommand(Guid.NewGuid().ToString(), StateCommandSource.ControlCenter,
                current.Revision, new(new Dictionary<string, long>
                { [fixture.Main] = current.PostersById[fixture.Main].EntityVersion }, ContentVersion: current.ContentVersion),
                fixture.Main, [intent.Target.ItemId], true));
            Assert.True(detached.Succeeded, detached.Error);
        }
        else
        {
            fixture.Commit(fixture.AddCommand(fixture.Main, Entry("repaired.txt")) with
                { RepairContentId = current.ContentCatalog.Items.Single().Id });
        }
        views.Reconcile(fixture.Current.ContentCatalog);
        long version = views.Version;
        completion.SetResult(Success(dispatched!) with { Name = "stale-name.txt" });

        Assert.True((await pending.WaitAsync(TimeSpan.FromSeconds(5))).Succeeded);
        Assert.Equal(1L, fixture.Sql("SELECT count(*) FROM content_open_facts"));
        Assert.Equal(version, views.Version);
        ContentLayoutPolicy.TryRead(fixture.Current.PostersById[fixture.Main].Poster.ContentLayoutJson,
            fixture.Main, out var layout, out _);
        var visible = views.Read(layout!);
        if (remove) Assert.Empty(visible);
        else Assert.Equal("repaired.txt", Assert.Single(visible).Name);
        Assert.False((await actions.OpenAsync(intent, CancellationToken.None)).Succeeded);
        Assert.Equal(1, worker.Calls);
    }

    [Fact]
    public async Task CompletionStorageFailurePropagatesWithoutObservationOrSecondDispatch()
    {
        using var fixture = Prepared();
        var worker = new Worker((request, _) => ValueTask.FromResult(Success(request) with { Name = "changed.txt" }));
        var views = new ReferenceContentRuntime(fixture.Current.ContentCatalog, fixture.Main, 1, worker);
        var actions = new ReferenceContentActions(Service(fixture, worker), views);
        long version = views.Version;
        var intent = Intent(fixture);
        fixture.Sql("CREATE TRIGGER reject_service_fact BEFORE INSERT ON content_open_facts BEGIN SELECT RAISE(ABORT,'fixture'); END;");

        await Assert.ThrowsAnyAsync<Exception>(() => actions.OpenAsync(intent, CancellationToken.None).AsTask());

        Assert.Equal(version, views.Version);
        Assert.Equal(0L, fixture.Sql("SELECT count(*) FROM content_open_facts"));
        Assert.Equal(0L, fixture.Sql("SELECT completed FROM content_open_requests"));
        Assert.False((await actions.OpenAsync(intent, CancellationToken.None)).Succeeded);
        Assert.Equal(1, worker.Calls);
    }

    private static Fixture Prepared()
    {
        var fixture = new Fixture();
        fixture.Commit(fixture.AddCommand(fixture.Main, Entry()));
        return fixture;
    }

    private static ContentOpenIntent Intent(Fixture fixture) => new(
        new(fixture.Main, fixture.Current.ContentCatalog.PosterItems.Single().LayoutItemId, 1, 1, "service-fixture"),
        Guid.NewGuid(), ContentOpenOrigin.Enter);

    private static ContentActionService Service(Fixture fixture, ILocalContentWorker worker) =>
        new(() => fixture.Current.ContentCatalog, worker, new SqliteContentOpenJournal(fixture.Store));

    private static LocalContentResponse Success(LocalContentRequest request) =>
        new(request.RequestId, ContentAvailability.Available, "accepted", Evidence: ContentOpenEvidence.ShellAccepted);

    private sealed class Worker(Func<LocalContentRequest, CancellationToken, ValueTask<LocalContentResponse>> run)
        : ILocalContentWorker
    {
        internal int Calls { get; private set; }
        public ValueTask<LocalContentResponse> RunAsync(LocalContentRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            return run(request, cancellationToken);
        }
    }
}
