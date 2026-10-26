using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;

// Isolated diagnostic, not a product sampler or a wall-time assertion in the contract suite.
internal static class JointModelCostFixture
{
    internal static async Task Run(string requested)
    {
        string root = Path.GetFullPath(requested), repo = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(repo, "DesktopUp.sln")))
            repo = Path.GetDirectoryName(repo) ?? throw new IOException("Repository fixture required.");
        if (Path.GetDirectoryName(root) != Path.Combine(repo, "out", "drop-d3") || Directory.Exists(root) || File.Exists(root))
            throw new IOException("A new direct child of out/drop-d3 is required.");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, ".joint-cost-fixture"), "owned");
        var results = new List<object>();
        try
        {
            foreach (int count in new[] { 1, 3, 6 })
            foreach (int items in new[] { 20, 100 })
            {
                string area = Path.Combine(root, $"posters-{count}-items-{items}");
                using var store = new SqliteStateStore(new UserDataPaths(Path.Combine(area, "profile")));
                var initialized = store.Initialize();
                if (!initialized.Succeeded) throw new IOException(initialized.Error);
                var repository = new SqliteStateCommandRepository(store); repository.EnsureInitialized();
                ReadObservationCostFixture.SeedMappings(store, area, count, items);
                var state = repository.Read();
                var authors = state.Catalog.Posters.Select(p =>
                {
                    if (!ContentLayoutPolicy.TryRead(p.Poster.ContentLayoutJson, p.Poster.PosterId, out var document, out var error))
                        throw new IOException(error);
                    return document! with { AllowHeatDrivenSize = true };
                }).ToArray();
                if (authors.Length != count || authors.Any(a => a.Placements.Count != items)) throw new IOException("Unexpected fixture membership.");
                var stats = new SqliteContentActivityStore(store);
                var faults = new ConcurrentQueue<Exception>();
                var consumer = new ContentActivityConsumer(stats, faults.Enqueue);
                var registry = ContentModelRegistry.BuiltIns();
                var producer = new ContentActivityProducer(stats, consumer, registry);
                var viewport = new ContentLayoutViewport(900, 700);
                var now = DateTimeOffset.UtcNow;
                var files = new LocalContentObservationFiles();
                var observation = new ContentObservationService(
                    new(new SqliteManagedContentObservationStorage(store), files),
                    new(new SqliteMappedDirectoryStorage(store), files), new SqliteContentWatchTargetSource(store));
                try
                {
                    await Measure("full-state-read", () => { _ = repository.Read(); return Task.CompletedTask; });
                    await Measure("relations-legacy-reference", () =>
                    {
                        foreach (var author in authors)
                        foreach (var placement in author.Placements)
                            if (ContentRelationPolicy.Resolve(state.ContentCatalog, author.PosterId, placement.ItemId) is null)
                                throw new IOException("Missing relation.");
                        return Task.CompletedTask;
                    });
                    await Measure("relations-current", () =>
                    {
                        foreach (var author in authors)
                            if (ContentRelationPolicy.ResolveMany(state.ContentCatalog, author.PosterId,
                                author.Placements.Select(p => p.ItemId).ToArray()).Any(r => r is null))
                                throw new IOException("Missing batched relation.");
                        return Task.CompletedTask;
                    });
                    await Measure("statistics-read", () =>
                    {
                        foreach (var author in authors)
                        {
                            var ids = state.ContentCatalog.PosterItems.Where(p => p.PosterId == author.PosterId)
                                .Select(p => state.ContentCatalog.Memberships.Single(m => m.Id == p.MembershipId).ContentItemId).ToArray();
                            if (stats.Read(ids).Items.Count != items) throw new IOException("Missing usage.");
                        }
                        return Task.CompletedTask;
                    });
                    foreach (string model in new[] { ContentModelProtocol.BuiltInId, ContentModelProtocol.RecentId })
                    {
                        var selected = authors.Select(a => a with { Activity = a.Activity with { ModelId = model } }).ToArray();
                        var inputs = selected.Select(a => new ContentModelInput(a, a.Placements.Select((p, i) =>
                            new ContentActivityInput(p.ItemId, i, new(Guid.NewGuid(), now.AddDays(-80), i + 1, now.AddHours(-i))))
                            .ToArray(), viewport, now, 1)).ToArray();
                        await Measure(model + "/model-session", async () =>
                        {
                            var proposals = await Task.WhenAll(inputs.Select(i => registry.EvaluateAsync(i, default)));
                            for (int i = 0; i < selected.Length; i++) Apply(selected[i], proposals[i]);
                        });
                        await Measure(model + "/producer-session", async () =>
                        {
                            var proposals = await Task.WhenAll(selected.Select(a => producer.ProduceAsync(a, state.ContentCatalog, 1, now, default, viewport)));
                            for (int i = 0; i < selected.Length; i++) Apply(selected[i], proposals[i] ?? throw new IOException("No proposal."));
                            if (faults.TryPeek(out var failure)) throw new IOException("Consumer failed.", failure);
                        });
                    }
                    await Measure("watch-targets", () => { _ = observation.WatchTargets(); return Task.CompletedTask; });
                    await Measure("unchanged-reconcile", () =>
                    {
                        var result = observation.Reconcile();
                        if (result.Changed || result.Pending || result.Notices.Count > 0) throw new IOException("Unexpected observation change.");
                        return Task.CompletedTask;
                    });
                    if (repository.Read().Revision != state.Revision) throw new IOException("Measurement modified authored state.");
                }
                finally { await consumer.StopAsync(); }
                Console.WriteLine($"Joint model cost: {count} posters x {items} items completed.");

                void Apply(ContentLayoutDocument author, ContentHeatProposal proposal)
                {
                    var session = new ContentHeatSession(); session.Reconcile(author, viewport);
                    if (!session.Offer(proposal) || !session.ApplyPending(author, viewport, false).Applied)
                        throw new IOException("Proposal admission failed.");
                    var layout = ContentLayoutSolver.Resolve(session.Projection!, viewport);
                    if (layout.IsLayoutReadOnly || layout.Items.Count != items) throw new IOException("Invalid layout.");
                    session.CompletePublication(true); session.Close();
                }
                async Task Measure(string stage, Func<Task> operation)
                {
                    for (int i = 0; i < 2; i++) await operation();
                    var times = new List<double>(); var bytes = new List<long>();
                    for (int i = 0; i < 8; i++)
                    {
                        long allocation = GC.GetTotalAllocatedBytes(true), start = Stopwatch.GetTimestamp();
                        await operation();
                        times.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                        bytes.Add(GC.GetTotalAllocatedBytes(true) - allocation);
                    }
                    var ordered = times.Order().ToArray();
                    results.Add(new { Posters = count, ItemsPerPoster = items, Stage = stage, Milliseconds = times,
                        MedianMs = (ordered[3] + ordered[4]) / 2, MaxMs = ordered[^1], MeanAllocatedBytes = bytes.Average() });
                    Save();
                }
            }
        }
        catch (Exception error) { File.WriteAllText(Path.Combine(root, "first-failure.txt"), error.ToString()); throw; }
        finally { Save(); }
        void Save() => File.WriteAllText(Path.Combine(root, "cost.json"), JsonSerializer.Serialize(new {
            Runtime = Environment.Version.ToString(), OS = Environment.OSVersion.ToString(), Processors = Environment.ProcessorCount,
            Notes = "8 samples after 2 warmups; process-wide allocations include workers; real isolated SQLite/files. No HWND, UI/FPS, long-run, SQL-count or lock-cause claim.",
            Results = results }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
