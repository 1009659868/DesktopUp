using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using DesktopUp.Windows;

namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    // Explicit owned-data application probe. Exercises the real source lifetime, admission,
    // receiver and renderer; does not synthesize OS mouse input or claim OLE pointer coverage.
    internal async Task CrossPosterProbe(bool restart)
    {
        var repository = new SqliteStateCommandRepository(_transferStore!);
        var before = repository.Read();
        if (restart)
        {
            if (before.Catalog.Posters.Count != 2 || before.ContentCatalog.PosterItems.GroupBy(p => p.PosterId).Count() != 2 ||
                before.ContentCatalog.PosterItems.Count != 4) throw new IOException("Cross-poster ownership did not survive restart.");
        }
        else
        {
            string sourceId = before.Catalog.Posters.Single().Poster.PosterId;
            var coordinator = new StateProjectionCoordinator(repository);
            coordinator.EnsureInitialized();
            var create = coordinator.Execute(new CreatePosterCommand(Guid.NewGuid().ToString(), StateCommandSource.ControlCenter, before.Revision,
                StateReadSet.ForCatalog(before.Catalog.CatalogVersion), PosterConfigurationFactory.NewBasicBox("B / Cross-poster probe", "DISPLAY1") with { HorizontalAnchor = .6 }));
            if (!create.Succeeded) throw new IOException(create.Error);
            state.Refresh();
            string targetId = repository.Read().Catalog.Posters.Single(p => p.Poster.PosterId != sourceId).Poster.PosterId;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            while (!_windows.TryGetValue(targetId, out var ready) || !ready.IncomingProbeReady)
            { if (timer.Elapsed.TotalSeconds > 15) throw new IOException("Target window not ready."); await Task.Delay(100); }
            var source = _windows[sourceId]; var target = _windows[targetId];
            var selected = before.ContentCatalog.PosterItems.First(p => p.PosterId == sourceId);
            var intent = BeginNativeExit(source, [selected.LayoutItemId]) ?? throw new IOException("Native exit admission failed.");
            if (CanReceiveInto(sourceId) || !CanReceiveInto(targetId)) throw new IOException("Cross-poster admission is not isolated.");
            var ticket = target.IncomingProbeTicket(intent.Items.Select(i => i.Path).ToArray(), false);
            var handoff = PreparePosterDropHandoff(targetId, ticket.Paths) ?? throw new IOException("Drop handoff failed.");
            ReceiveFiles(target, ticket, target.IncomingProbeLanding(ticket), handoff); // Queue before source unwind.
            CompleteNativeExit(source, intent, new NativeShellDragResult(true, false, 0, default));
            timer.Restart();
            while (repository.Read().ContentCatalog.PosterItems.All(p => p.PosterId != targetId) || !TransfersAvailable)
            { if (timer.Elapsed.TotalSeconds > 20) throw new IOException("Queued cross-poster handoff timed out."); await Task.Delay(100); }
            var after = repository.Read();
            if (after.ContentCatalog.PosterItems.Count != 4 || after.ContentCatalog.PosterItems.Any(p => p.Id == selected.Id) ||
                ContentSingleOwnerPolicy.Validate(after.ContentCatalog) is not null) throw new IOException("Cross-poster handoff duplicated/lost ownership.");
            // An ordinary external target still remains eligible after the handoff.
            if (!CanReceiveInto(sourceId) || !CanReceiveInto(targetId)) throw new IOException("Transfer admission did not reset.");
        }
        log.Write("info", "contract.cross_poster_passed", new { restart, nativeMouse = false, posters = 2, items = 4 });
    }
}
