using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using System.Text.Json;

namespace DesktopUp;

public partial class App
{
    // Entry is guarded by RunIncomingProbe's owned root/marker checks.
    // Uses the production monitor/dispatcher. Never invokes Reconcile or invalidation itself.
    private async Task RunContentObservationProbe(SqliteStateCommandRepository repository, string root, bool restart)
    {
        string mappedPath = Path.Combine(root, "center-map-probe");
        string renamedPath = Path.Combine(mappedPath, "mapped-renamed.txt");
        string proofPath = Path.Combine(root, "observation-proof.json");
        async Task Until(Func<bool> condition, string failure)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (!condition())
            {
                if (watch.Elapsed.TotalSeconds > 36) throw new IOException(failure);
                await Task.Delay(100);
            }
        }
        ContentItem? At(string path) => repository.Read().ContentCatalog.Items.SingleOrDefault(i => i.Target.Locator == path);
        Guid Card(string path)
        {
            var state = repository.Read();
            return state.ContentCatalog.PosterItems.Single(p =>
                ContentRelationPolicy.Resolve(state.ContentCatalog, p.PosterId, p.LayoutItemId)!.Item.Target.Locator == path).Id;
        }
        if (restart)
        {
            var proof = JsonSerializer.Deserialize<ObservationProof>(File.ReadAllText(proofPath))!;
            await Until(() => At(Path.Combine(mappedPath, "offline-added.txt")) is not null &&
                _spatialRuntime!.MappingState(proof.Poster)?.Available == true, "Startup monitor did not observe offline change.");
            if (Card(renamedPath) != proof.Card || repository.Read().ContentCatalog.PosterItems.Count != 2 ||
                File.ReadAllText(Path.Combine(root, "externally-returned.txt")) != "owned observation sample")
                throw new IOException("Startup changed card identity or retained a removed managed card.");
        }
        else
        {
            if (repository.Read().Catalog.Posters.Count != 1 || repository.Read().ContentCatalog.PosterItems.Count != 0)
                throw new IOException("Fresh observation fixture required.");
            string source = Path.Combine(root, "managed-source.txt");
            using (var writer = new StreamWriter(new FileStream(source, FileMode.CreateNew))) writer.Write("owned observation sample");
            await _spatialRuntime!.IncomingProbe([source]);
            var managed = repository.Read().ContentCatalog.Items.Single();
            var managedCard = Card(managed.Target.Locator);
            _controlCenter!.Show();
            await _controlCenter.WindowForPortableProbe!.VerifyMappingCreationForProbe(root);
            var state = repository.Read();
            var collection = state.ContentCatalog.Collections.Single(c => c.Kind == ContentCollectionKind.Mapped);
            string poster = state.ContentCatalog.Bindings.Single(b => b.CollectionId == collection.Id).PosterId;
            string initial = Path.Combine(mappedPath, "mapped.txt");
            using (var writer = new StreamWriter(new FileStream(initial, FileMode.CreateNew))) writer.Write("owned mapped sample");
            await Until(() => At(initial) is not null, "Monitor did not add mapped file.");
            Guid card = Card(initial);
            File.Move(initial, renamedPath);
            await Until(() => At(renamedPath) is not null, "Monitor did not rename mapped file.");
            if (Card(renamedPath) != card) throw new IOException("Mapped rename replaced card identity.");

            string unavailable = Path.Combine(root, "center-map-offline");
            async Task MoveOwnedDirectory(string from, string to)
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                while (true)
                {
                    try { Directory.Move(from, to); return; }
                    catch (IOException) when (watch.Elapsed.TotalSeconds < 5) { await Task.Delay(100); }
                }
            }
            await MoveOwnedDirectory(mappedPath, unavailable);
            await Until(() => _spatialRuntime.MappingState(poster)?.Available == false, "Missing root was not reported.");
            if (Card(renamedPath) != card) throw new IOException("Missing root removed card.");
            await MoveOwnedDirectory(unavailable, mappedPath);
            await Until(() => _spatialRuntime.MappingState(poster)?.Available == true, "Restored root did not recover.");
            if (Card(renamedPath) != card) throw new IOException("Recovery replaced card.");

            File.Move(managed.Target.Locator, Path.Combine(root, "externally-returned.txt"));
            await Until(() => !repository.Read().ContentCatalog.PosterItems.Any(p => p.Id == managedCard),
                "Monitor did not remove externally moved managed card.");
            File.WriteAllText(proofPath, JsonSerializer.Serialize(new ObservationProof(poster, card)));
        }
        _log!.Write("info", "contract.r1_observation_passed", new { restart, nativeMouse = false, picker = false,
            productionMonitor = true, forcedInvalidation = false });
    }

    private sealed record ObservationProof(string Poster, Guid Card);
}
