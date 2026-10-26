using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    private ContentActivityProducer? _activityProducer;
    internal IReadOnlyList<ContentModelDescriptor> ActivityModels => _activityProducer?.Models ?? [];
    internal void ConfigureActivity(ContentActivityProducer producer)
    {
        if (_activityProducer is null) ActivityChanged += WakeActivitySafePoints;
        _activityProducer = producer;
    }

    private void WakeActivitySafePoints()
    {
        // Busy release wakes every waiting poster, including peers of a growing poster.
        // The window drains existing requests only; this does not create new rankings.
        foreach (var window in _windows.Values.ToArray())
            window.SpatialDispatcher.TryEnqueue(window.NotifyActivitySafePoint);
    }

    internal void NotifyActivityStatisticsChanged(IReadOnlyList<Guid> contentIds)
    {
        if (_contentOperations.IsStopping || !state.TryGetCurrent(out var current) || current is null) return;
        // Resolve current ownership on the UI thread: a successful open may finish after a move.
        var changed = contentIds.ToHashSet();
        var memberships = current.ContentCatalog.Memberships.Where(x => changed.Contains(x.ContentItemId))
            .Select(x => x.Id).ToHashSet();
        foreach (string posterId in current.ContentCatalog.PosterItems.Where(x => memberships.Contains(x.MembershipId))
            .Select(x => x.PosterId).Distinct())
            if (_windows.TryGetValue(posterId, out var window)) window.RequestActivityRefresh();
    }

    internal void RequestActivity(PosterVisualWindow owner, ContentLayoutDocument author, long version,
        long expectedEntityVersion, Action<ContentHeatProposal?> publish)
    {
        if (_activityProducer is null || _contentOperations.IsStopping ||
            !state.TryGetCurrent(out var current) || current is null) { publish(null); return; }
        var catalog = current.ContentCatalog;
        var viewport = owner.ActivityViewport;
        var layout = owner.ActivityLayout;
        _contentOperations.Track(Produce());
        async Task Produce()
        {
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            ContentHeatProposal? proposal = null;
            try
            {
                proposal = await _activityProducer.ProduceAsync(author, catalog, version, DateTimeOffset.UtcNow,
                    _contentOperations.Stopping, viewport, layout).ConfigureAwait(false);
                log.Write("info", "content.activity_produced", new { author.PosterId, version, author.DocumentVersion,
                    count = proposal?.Items.Count, elapsedMs = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds });
            }
            catch (OperationCanceledException) when (_contentOperations.IsStopping) { }
            catch (Exception error)
            { log.Write("warning", "content.activity_production_failed", new { author.PosterId, error.Message }); }
            owner.SpatialDispatcher.TryEnqueue(() =>
            {
                if (_windows.GetValueOrDefault(author.PosterId) != owner) return;
                bool currentOwner = !_contentOperations.IsStopping && !_suspended && state.TryGetCurrent(out var latest) &&
                    latest?.PostersById.GetValueOrDefault(author.PosterId)?.EntityVersion == expectedEntityVersion;
                publish(currentOwner ? proposal : null);
            });
        }
    }

    internal void ApplyActivityGrowth(PosterVisualWindow owner, ContentHeatProposal proposal, Action<bool> completed)
    {
        if (_suspended || IsSpatialBusy || !TrySnapshot(owner, out var scene, out var current) ||
            current!.PostersById.GetValueOrDefault(proposal.PosterId) is not { } target ||
            !ContentLayoutPolicy.TryRead(target.Poster.ContentLayoutJson, proposal.PosterId, out var author, out _) ||
            author!.DocumentVersion != proposal.AuthorDocumentVersion || author.ItemSetVersion != proposal.ItemSetVersion)
        { completed(false); return; }
        var plan = ContentHeatExpansion.Plan(target.Poster, proposal, scene!, out var reason);
        if (plan is null)
        {
            // Current scene is known; blocked growth falls back to the current-frame projection.
            // Its own readable-interval/layout validation can still reject an infeasible result.
            log.Write("info", "content.activity_growth_capped", new { proposal.PosterId, reason });
            completed(true); return;
        }
        if (plan.Changes.Count == 0) { completed(true); return; }
        var command = new PosterStateCommand(Guid.NewGuid().ToString(), StateCommandSource.System,
            PosterStateCommandKind.Resize, current.Revision, proposal.PosterId, plan.Desired)
        {
            ReadSet = new(current.Posters.ToDictionary(x => x.Poster.PosterId, x => x.EntityVersion),
                CatalogVersion: current.CatalogVersion),
            ContentHeatExpansion = new(scene!, proposal)
        };
        BeginPlannedGeometry(owner, command, scene!, plan, result => completed(result.Succeeded));
    }
}
