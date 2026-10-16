using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using DesktopUp.Windows;

namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    internal async Task R1TransfersProbe(string root)
    {
        var repository = new SqliteStateCommandRepository(_transferStore!);
        if (repository.Read().ContentCatalog.PosterItems.Count != 0) throw new IOException("Fresh owned fixture required.");
        string sourceDirectory = Path.Combine(root, "sources"); Directory.CreateDirectory(sourceDirectory);
        string Make(string name)
        {
            string path = Path.Combine(sourceDirectory, name);
            using var writer = new StreamWriter(new FileStream(path, FileMode.CreateNew));
            writer.Write("R1 owned transfer sample"); return path;
        }
        var paths = Enumerable.Range(1, 4).Select(i => Make($"sample-{i}.txt")).ToArray();
        await IncomingProbe(paths);
        if (repository.Read().ContentCatalog.PosterItems.Count != 4 || paths.Any(File.Exists))
            throw new IOException("Application ingress did not publish four committed items.");
        await CrossPosterProbe(false); // Existing source-lifetime / queued OLE handoff checks.

        var current = repository.Read();
        var card = current.ContentCatalog.PosterItems.GroupBy(p => p.PosterId).Single(g => g.Count() == 1).Single();
        var source = _windows[card.PosterId];
        var intent = BeginNativeExit(source, [card.LayoutItemId]) ?? throw new IOException("Exit admission failed.");
        string returned = Path.Combine(root, "outgoing-target"); Directory.CreateDirectory(returned);
        string destination = Path.Combine(returned, Path.GetFileName(intent.Items.Single().Path));
        File.Move(intent.Items.Single().Path, destination); // Simulates external Shell result, not native pointer coverage.
        CompleteNativeExit(source, intent, new(true, false, 0, default));
        await _fileTransferTask.WaitAsync(TimeSpan.FromSeconds(15));
        if (repository.Read().ContentCatalog.PosterItems.Any(p => p.Id == card.Id) || !File.Exists(destination))
            throw new IOException("Native completion did not observe the external result.");

        string mapped = Path.Combine(root, "mapping"); Directory.CreateDirectory(mapped);
        string poster = await CreateMappedPoster(mapped, CancellationToken.None);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (!_windows.TryGetValue(poster, out var ready) || !ready.IncomingProbeReady)
        { if (watch.Elapsed.TotalSeconds > 15) throw new IOException("Mapped window not ready."); await Task.Delay(100); }
        var target = _windows[poster]; string external = Make("mapped-incoming.txt");
        var ticket = target.IncomingProbeTicket([external], false);
        ReceiveFiles(target, ticket, target.IncomingProbeLanding(ticket));
        await _fileTransferTask.WaitAsync(TimeSpan.FromSeconds(30));
        if (File.Exists(external) || !File.Exists(Path.Combine(mapped, Path.GetFileName(external))) ||
            repository.Read().ContentCatalog.PosterItems.Count(p => p.PosterId == poster) != 1)
            throw new IOException("Mapped ingress did not publish the observed Shell result.");
        log.Write("info", "contract.r1_transfers_passed",
            new { nativeMouse = false, externalExitSimulated = true, mappedShellExecuted = true, items = 4 });
    }

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
