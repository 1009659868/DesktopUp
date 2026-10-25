using System.Security.Cryptography;
using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using DesktopUp.Windows;

namespace DesktopUp;

public partial class App
{
    // Review-only composition probe. Provider acceptance is simulated in a worker;
    // all consumption, production, safe-point publication and surface readback are real.
    private async void RunRealActivityRuntimeProbe(string dataRoot)
    {
        IDisposable? lease = null;
        string stage = "fixture-guard";
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        try
        {
            string repo = AppContext.BaseDirectory;
            while (!File.Exists(Path.Combine(repo, "DesktopUp.sln")))
                repo = Path.GetDirectoryName(repo) ?? throw new IOException("Repository fixture required.");
            string profile = Path.GetFullPath(dataRoot), root = Path.GetDirectoryName(profile)!;
            string allowed = Path.GetFullPath(Path.Combine(repo, "out", "drop-d3"));
            if (!string.Equals(Path.GetDirectoryName(root), allowed, StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(profile) != "profile" ||
                Environment.GetEnvironmentVariable("DESKTOPUP_V3_TEST_DATA_ROOT") != dataRoot ||
                !File.Exists(Path.Combine(root, ".heat-runtime-fixture")) ||
                File.ReadAllText(Path.Combine(root, ".heat-runtime-fixture")) != "owned")
                throw new IOException("New owned out/drop-d3/<unique>/profile fixture required.");

            lease = _ledger!.Acquire("fixture.real-activity", "journal-to-production-publication");
            _stopAutoSmoke = () => cancellation.Cancel();
            await _transferDiscovery;
            void Require(bool value, string message)
            {
                if (!value) throw new IOException(message);
            }
            async Task Until(Func<bool> ready)
            {
                while (!ready()) await Task.Delay(50, cancellation.Token);
            }
            await Until(() => _autoReady && _spatialRuntime is { IsSpatialBusy: false });
            string id = BuiltInStateIdentities.PrimaryPosterId;
            var spatial = _spatialRuntime!;
            var window = spatial.BackdropWindowForProbe(id);
            await window.CaptureRenderIdentityForProbe();
            Require(spatial.ReadContentEditor(id) is { CanEdit: true } &&
                _stateCoordinator!.Refresh().PostersById[id].Poster is { IsCollapsed: false, IsLocked: false },
                "An expanded editable primary poster is required; use the owned cross-poster fixture.");

            stage = "reference-registration";
            string filesRoot = Path.Combine(root, "activity-sentinels");
            Require(!Directory.Exists(filesRoot), "Activity sentinels already exist; use a new fixture.");
            Directory.CreateDirectory(filesRoot);
            string[] files = [Path.Combine(filesRoot, "cold-reference.txt"), Path.Combine(filesRoot, "hot-reference.txt")];
            foreach (string path in files)
            {
                using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
                file.Write("Owned activity probe source; no native open is dispatched.\n"u8);
            }
            string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
            var hashes = files.Select(Hash).ToArray();
            File.WriteAllText(Path.Combine(root, "activity-runtime-created-files.json"),
                JsonSerializer.Serialize(files.Select((path, i) => new { path, sha256 = hashes[i] })));
            var inputs = files.Select(path => LocalContentReferenceProbe.Inspect(path)).ToArray();
            Require(inputs.All(x => x.Item is not null), "Could not inspect owned reference files.");
            var current = _stateCoordinator!.Refresh();
            int countBefore = spatial.ReadContentEditor(id)!.Document.Placements.Count;
            var add = _stateCommands!.Execute(new ContentReferenceCommand(
                "activity-reference-" + Guid.NewGuid().ToString("N"), StateCommandSource.ControlCenter,
                current.Revision, StateReadSet.ForPoster(id, current.PostersById[id].EntityVersion) with
                { ContentVersion = current.ContentVersion }, id,
                inputs.Select(x => new ContentReferenceEntry(x.Item!, ContentReferenceChoice.CreateIndependent)).ToArray()));
            Require(add.Succeeded, "Reference registration failed: " + add.Error);
            await Until(() => !spatial.IsSpatialBusy && spatial.ReadContentEditor(id) is { CanEdit: true } editor &&
                editor.Document.Placements.Count == countBefore + files.Length);
            stage = "desktop-standard-size";
            double standardIcon = window.ReadStandardContentIconSizeForProbe();
            Require(Math.Abs(standardIcon - 48) < .001, $"Standard icon area was {standardIcon}, expected 48 DIP.");

            // An explicit settings edit is a normal safe-point trigger. Membership updates alone
            // are not treated as successful opens, and this probe never offers a heat proposal.
            stage = "settings-and-initial-production";
            var options = new ContentActivityOptions(.75, 2.5, 1.25, 40);
            long beforeSettingsVersion = spatial.ReadContentProposalForSmoke(id).ActiveHeatVersion ?? 0;
            Require(window.EditContent(new(ContentLayoutEditKind.Activity) { Activity = options, Enabled = true }) is null,
                "Could not save activity settings through the production editor.");
            await Until(() => !spatial.IsSpatialBusy && spatial.ReadContentProposalForSmoke(id) is
                { ActiveHeatVersion: { } version, HeatPending: false, AuthorIntent.Activity: var saved } &&
                version > beforeSettingsVersion && saved == options &&
                spatial.ReadContentSurfaceForSmoke(id) is { Transition: false, CanvasMatches: true, InputMatches: true });
            // Let any already queued configuration publication settle before opening content.
            await Task.Delay(150, cancellation.Token);
            var initial = spatial.ReadContentProposalForSmoke(id);
            long initialVersion = initial.ActiveHeatVersion ?? throw new IOException("Initial activity publication disappeared.");
            var initialSurface = spatial.ReadContentSurfaceForSmoke(id);
            var author = initial.AuthorIntent;
            string authorJson = ContentLayoutPolicy.Serialize(author);
            Require(author.Mode == ContentLayoutMode.Automatic && author.AllowHeatDrivenSize,
                "Automatic activity sizing was not enabled.");
            var catalog = _stateCoordinator.Refresh().ContentCatalog;
            var ownedIds = inputs.Select(x => x.Item!.Id).ToArray();
            string LayoutId(Guid contentId) => catalog.PosterItems.Single(p => p.PosterId == id &&
                ContentRelationPolicy.Resolve(catalog, id, p.LayoutItemId)!.Item.Id == contentId).LayoutItemId;
            string coldId = LayoutId(ownedIds[0]), hotId = LayoutId(ownedIds[1]);
            var beforeUsage = _composition!.ActivityStore.Read(ownedIds);
            Require(!beforeUsage.HasPending && ownedIds.All(item => beforeUsage.Items[item].TotalLaunches == 0),
                "New reference objects must begin with zero successful opens.");

            void VerifySavedSettings()
            {
                var reread = new SqliteStateCommandRepository(_composition.Store).Read();
                string? raw = reread.Catalog.Posters.Single(p => p.Poster.PosterId == id).Poster.ContentLayoutJson;
                Require(ContentLayoutPolicy.TryRead(raw, id, out var stored, out _) && stored!.Activity == options &&
                    stored.AllowHeatDrivenSize && ContentLayoutPolicy.Serialize(stored) == authorJson,
                    "Durable settings or author sizes changed across statistics/publication.");
            }
            VerifySavedSettings();

            stage = "simulated-provider-facts";
            Require(spatial.SetContentEditing(id, true) is null, "Cannot protect the active edit.");
            // Entering edit mode adds its own border and changes the available viewport.
            // Compare against that accepted geometry, not the preceding everyday-mode frame.
            var protectedBasis = spatial.ReadContentProposalForSmoke(id);
            var protectedSurface = spatial.ReadContentSurfaceForSmoke(id);
            var journal = new SqliteContentOpenJournal(_composition.Store);
            var worker = new ActivityRuntimeProbeWorker();
            var actions = new ContentActionService(() => _stateCoordinator.Refresh().ContentCatalog,
                worker, journal, _composition.Activity);
            const int successes = 3;
            for (int i = 0; i < successes; i++)
            {
                var target = new ContentActivationTarget(id, hotId, author.ItemSetVersion, author.DocumentVersion,
                    "review-simulated-provider-acceptance");
                var intent = new ContentOpenIntent(target, Guid.NewGuid(), ContentOpenOrigin.Enter);
                Require((await actions.OpenAsync(intent, cancellation.Token)).Feedback.Succeeded, "Successful open failed.");
                Require(!(await actions.OpenAsync(intent, cancellation.Token)).Feedback.Succeeded, "Duplicate open was accepted.");
            }
            await _composition.Activity.WhenIdleAsync().WaitAsync(cancellation.Token);
            var usage = _composition.ActivityStore.Read(ownedIds);
            Require(!usage.HasPending && usage.Items[ownedIds[0]].TotalLaunches == 0 &&
                usage.Items[ownedIds[1]].TotalLaunches == successes, "Production statistics did not consume facts exactly once.");
            await Task.Delay(250, cancellation.Token);
            var deferred = spatial.ReadContentProposalForSmoke(id);
            Require(deferred.ActiveHeatVersion == protectedBasis.ActiveHeatVersion && !deferred.HeatPending &&
                ContentLayoutPolicy.Serialize(deferred.EffectiveIntent) == ContentLayoutPolicy.Serialize(protectedBasis.EffectiveIntent) &&
                spatial.ReadContentSurfaceForSmoke(id).GeometryFingerprint == protectedSurface.GeometryFingerprint,
                "Successful opens changed displayed geometry during an active edit.");
            Require(worker.Calls == successes, "Duplicate activation dispatched another provider operation.");
            Require(spatial.SetContentEditing(id, false) is null, "Could not release the edit.");
            stage = "production-publication";
            await Until(() => !spatial.IsSpatialBusy && spatial.ReadContentProposalForSmoke(id) is
                { HeatPending: false, ActiveHeatVersion: { } version } && version > initialVersion &&
                spatial.ReadContentSurfaceForSmoke(id) is { Transition: false, CanvasMatches: true, InputMatches: true });
            var published = spatial.ReadContentProposalForSmoke(id);
            var surface = spatial.ReadContentSurfaceForSmoke(id);
            var hot = published.EffectiveIntent.Placements.Single(p => p.ItemId == hotId);
            var cold = published.EffectiveIntent.Placements.Single(p => p.ItemId == coldId);
            Require(hot.Order < cold.Order && hot.ColumnSpan > cold.ColumnSpan && hot.RowSpan > cold.RowSpan,
                "Production activity did not independently rank and resize the hot reference.");
            Require(surface.Handle == initialSurface.Handle && surface.CanvasMatches && surface.InputMatches && surface.Clipped,
                "Published canvas/input are not from the accepted layout on the retained native surface.");
            VerifySavedSettings();
            Require(ContentLayoutPolicy.Serialize(published.AuthorIntent) == authorJson,
                "Derived activity overwrote author layout intent.");
            Require(files.Select(Hash).SequenceEqual(hashes), "Owned reference contents changed.");

            stage = "unprotected-live-open";
            long firstPublishedVersion = published.ActiveHeatVersion!.Value;
            var liveTarget = new ContentActivationTarget(id, hotId, author.ItemSetVersion, author.DocumentVersion,
                "review-simulated-provider-acceptance");
            Require((await actions.OpenAsync(new(liveTarget, Guid.NewGuid(), ContentOpenOrigin.Enter), cancellation.Token))
                .Feedback.Succeeded, "Unprotected open failed.");
            await Until(() => !spatial.IsSpatialBusy && spatial.ReadContentProposalForSmoke(id) is
                { HeatPending: false, ActiveHeatVersion: { } version } && version > firstPublishedVersion &&
                spatial.ReadContentSurfaceForSmoke(id) is { Transition: false, CanvasMatches: true, InputMatches: true });
            Require(_composition.ActivityStore.Read(ownedIds).Items[ownedIds[1]].TotalLaunches == successes + 1 &&
                worker.Calls == successes + 1, "Live open was not consumed exactly once.");
            Require(!_stateCoordinator.Refresh().PostersById[id].Poster.IsCollapsed,
                "Live publication must not require collapsing the poster.");
            VerifySavedSettings();

            _log!.Write("info", "contract.real_activity_runtime_passed", new
            {
                providerConfirmation = "SIMULATED: worker acceptance; production open service; no external program opened",
                standardIconDip = standardIcon, overallScaleMinimum = PosterContentPreferencePolicy.MinimumScale,
                overallScaleMaximum = PosterContentPreferencePolicy.MaximumScale,
                nativeMouse = false, processRestart = false, configurationReread = "new repository read of SQLite",
                successes, duplicateCompletionsIgnored = successes, coldLaunches = usage.Items[ownedIds[0]].TotalLaunches,
                hotLaunches = usage.Items[ownedIds[1]].TotalLaunches, usage.Position,
                initialVersion, publishedVersion = published.ActiveHeatVersion,
                deferredDuringEditing = true, updatedWithoutCollapse = true,
                unprotectedPublishedVersion = spatial.ReadContentProposalForSmoke(id).ActiveHeatVersion,
                surface.CanvasMatches, surface.InputMatches, surface.Clipped,
                surface.Handle, surface.GeometryFingerprint, options,
                hot = new { hot.ItemId, hot.Order, hot.ColumnSpan, hot.RowSpan },
                cold = new { cold.ItemId, cold.Order, cold.ColumnSpan, cold.RowSpan },
                filesUnchanged = true, profile
            });
        }
        catch (Exception error)
        {
            Environment.ExitCode = 1;
            _log?.Write("error", "contract.real_activity_runtime_failed", new { stage, error.Message, error.StackTrace });
        }
        finally
        {
            _stopAutoSmoke = null;
            lease?.Dispose();
            ExitApplication();
        }
    }

    private sealed class ActivityRuntimeProbeWorker : ILocalContentWorker
    {
        internal int Calls { get; private set; }
        public ValueTask<LocalContentResponse> RunAsync(LocalContentRequest request, CancellationToken cancellation)
        {
            Calls++;
            return ValueTask.FromResult(new LocalContentResponse(request.RequestId, ContentAvailability.Available, "probe accepted")
            { Outcome = ContentOpenOutcome.Succeeded, Evidence = ContentOpenEvidence.ProviderConfirmed });
        }
    }
}
