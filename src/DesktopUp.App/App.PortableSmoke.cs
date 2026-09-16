using System.Security.Cryptography;
using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;

namespace DesktopUp;

public partial class App
{
    private sealed record PortableRestartProof(long Revision, string Fingerprint, int Count);

    // Isolated process gate only: production controller, file service, runtime guard and transaction channel.
    // Native picker/confirmation, real cross-host transfer and offline recovery remain manual/separate gates.
    private void RunPortableSmoke(string dataRoot)
    {
        bool restart = _startupIntent.Arguments.Contains("--portable-restart", StringComparer.Ordinal);
        var timer = _dispatcher!.CreateTimer(); timer.Interval = TimeSpan.FromMilliseconds(120);
        var lease = _ledger!.Acquire("fixture.portable", "isolated-production-controller");
        // Finish our own diagnostic before the shared process safety exit (capped at 60s).
        long deadline = _autoClock.ElapsedMilliseconds + 45000, revision = 0;
        string? readiness = null;
        int step = 0, initialCount = 0, checks = 0; bool ticking = false, stopped = false;
        string exportDirectory = Path.Combine(dataRoot, "exports");
        string originalPath = Path.Combine(exportDirectory, "original.desktopup.json");
        string appendPath = Path.Combine(exportDirectory, "append.desktopup.json");
        string emptyPath = Path.Combine(exportDirectory, "empty.desktopup.json");
        string changedPath = Path.Combine(exportDirectory, "changed.desktopup.json");
        string proofPath = Path.Combine(dataRoot, "portable-restart-proof.json");
        const string pendingTitle = "Portable pending sentinel";
        byte[] originalBytes = []; string originalHash = "";
        PortableProfileDocument? originalDocument = null;
        PosterDefinition[] originals = [], restoredDefinitions = []; string[] importedIds = [];
        var cases = new List<string>();
        string Fingerprint(DesktopUpProjection p) => StateRequestFingerprint.Compute(new
            { Posters = p.Posters.Select(t => t.Poster).ToArray(), Settings = p.ControlCenter.Settings.Settings });
        void Require(bool value, string message) { checks++; if (!value) throw new InvalidOperationException(message); }
        void Stop()
        {
            if (stopped) return;
            stopped = true; timer.Stop(); timer.Tick -= Tick; lease.Dispose(); _stopAutoSmoke = null;
        }
        _stopAutoSmoke = Stop;
        void Finish(bool passed, string? error)
        {
            Stop();
            _log!.Write(passed ? "info" : "error", "contract.portable_smoke", new
            { passed, error, checks, cases, step, initialCount, restart, nativeMouse = false,
                nativePicker = false, realCrossHost = false, liveDatabaseRestore = false });
            ExitApplication();
        }
        bool Ready(DesktopUpProjection p)
        {
            int expected = p.Posters.Count(t => t.IsEffectivelyVisible);
            string key = $"{step}/{_controlCenterEnvironmentPending}/{_spatialRuntime?.IsSpatialBusy}/{_inputRouter.ReadyCount}/{expected}";
            if (key != readiness)
            {
                readiness = key;
                _log!.Write("info", "contract.portable_readiness", new { step, p.Revision,
                    environmentPending = _controlCenterEnvironmentPending, spatialBusy = _spatialRuntime?.IsSpatialBusy,
                    registered = _inputRouter.Count, inputReady = _inputRouter.ReadyCount, expected,
                    sessions = _posterSessions?.Sessions.Select(s => new { s.PosterId, s.Epoch, s.Status,
                        input = _inputRouter.ReadSurface(new(s.PosterId, s.Epoch)) is not null }).ToArray() });
            }
            return !_controlCenterEnvironmentPending && _spatialRuntime is { IsSpatialBusy: false } && _inputRouter.ReadyCount == expected;
        }
        void Tick(DispatcherQueueTimer sender, object args)
        {
            if (ticking) return; ticking = true;
            try
            {
                Require(_autoClock.ElapsedMilliseconds < deadline, "portable process gate timed out");
                if (_portableController is null || !_stateCoordinator!.TryGetCurrent(out var current) || current is null || !Ready(current)) return;
                var controller = _portableController;
                switch (step)
                {
                    case 0:
                        initialCount = current.Posters.Count;
                        Require(initialCount is 0 or 1 or 3, "only isolated zero/one/three fixtures are supported");
                        Require(current.Posters.All(p => !p.AutoPosture.Enabled), "automatic posture must be off in this deterministic process fixture");
                        if (restart)
                        {
                            var proof = JsonSerializer.Deserialize<PortableRestartProof>(File.ReadAllText(proofPath))!;
                            Require(current.Revision == proof.Revision && current.Posters.Count == proof.Count && Fingerprint(current) == proof.Fingerprint,
                                "restart changed committed imported state");
                            Require(current.ProfileInitialized, "restart lost explicit initialized/empty profile");
                            cases.Add("same-root-normal-restart-preserves-imported-state"); Finish(true, null); return;
                        }
                        Require(string.Equals(Path.GetFullPath(_services!.GetRequiredService<IUserDataPaths>().Root),
                            Path.GetFullPath(dataRoot), StringComparison.OrdinalIgnoreCase), "fixture did not bind the isolated data root");
                        originals = current.Posters.Select(p => p.Poster).ToArray();
                        originalBytes = controller.Export(); originalDocument = PortableProfileCodec.Parse(originalBytes);
                        originalHash = Convert.ToHexString(SHA256.HashData(originalBytes));
                        Require(originalDocument.Posters.Count == initialCount, "export count differs from committed catalog");
                        Directory.CreateDirectory(exportDirectory);
                        controller.Save(originalPath, originalBytes, false);
                        Require(File.ReadAllBytes(originalPath).SequenceEqual(originalBytes), "atomic export changed the captured snapshot");
                        using (var parameters = JsonDocument.Parse(PosterPresentationPolicy.Serialize(PosterPresentationPolicy.Defaults("basic-box"))))
                        {
                            var oversize = new PortablePosterProfile("pending-fixture", pendingTitle, "DEMO configuration only", "general",
                                _spatialRuntime!.PortableTopology()!.PrimaryMonitor!.DeviceName,
                                PortableProfileCodec.MaximumExtentDip, PortableProfileCodec.MaximumExtentDip, 0, 0,
                                initialCount, true, true, false, false, 2,
                                PosterPresentationPolicy.AdaptiveGridLayout, parameters.RootElement.Clone(), "basic-box");
                            var appendDocument = originalDocument with { Posters = Array.AsReadOnly(originalDocument.Posters.Append(oversize).ToArray()) };
                            controller.Save(appendPath, PortableProfileCodec.Encode(appendDocument), false);
                        }
                        controller.Save(emptyPath, PortableProfileCodec.Encode(originalDocument with { Posters = Array.Empty<PortablePosterProfile>() }), false);
                        controller.Save(changedPath, originalBytes, false);
                        cases.Add("one-committed-snapshot-and-atomic-file-export");
                        revision = current.Revision;
                        var append = controller.Preview(appendPath, PortableImportMode.Append, false);
                        Require(append.Plan.Creates.Count == initialCount + 1 && append.Plan.Removes.Count == 0, "append preview impact mismatch");
                        importedIds = append.Plan.Creates.Select(p => p.PosterId).ToArray();
                        Require(!importedIds.Intersect(originals.Select(p => p.PosterId)).Any(), "import reused a local identity");
                        var applied = controller.Apply(append);
                        Require(applied.Succeeded, "append failed: " + applied.Message);
                        Require(applied.Command?.CurrentRevision == revision + 1, "append was not a single committed revision");
                        step = 1; break;
                    case 1:
                        Require(current.Revision == revision + 1 && current.Posters.Count == initialCount * 2 + 1, "append projection mismatch");
                        Require(originals.All(p => current.PostersById[p.PosterId].Poster == p), "append modified an existing poster");
                        Require(importedIds.All(id => current.PostersById.ContainsKey(id)), "append lost a created identity");
                        var sentinel = current.Posters.Single(p => p.Poster.Title == pendingTitle).Poster;
                        Require(sentinel.PlacementPending && sentinel.IsVisible && sentinel.IsLocked && !sentinel.IsCollapsed &&
                            sentinel.WidthDip == PortableProfileCodec.MaximumExtentDip, "pending import lost authored state");
                        Require(controller.PendingNames().Contains(pendingTitle), "pending poster is not named by the management controller");
                        Require(_posterSessions!.Find(sentinel.PosterId) is null, "pending poster owns a visible/input session");
                        cases.Add("append-one-revision-new-identities-pending-names-source-preserved");
                        revision = current.Revision;
                        var stale = controller.Preview(originalPath, PortableImportMode.Replace, false);
                        var settings = current.ControlCenter.Settings;
                        var changed = _stateCommands!.Execute(new SettingsStateCommand("portable-smoke-change-settings", StateCommandSource.ControlCenter,
                            SettingsStateCommandKind.SetDynamicEffectsPaused, current.Revision,
                            settings.Settings with { DynamicEffectsPaused = !settings.Settings.DynamicEffectsPaused })
                            { ReadSet = StateReadSet.ForSettings(settings.EntityVersion) });
                        Require(changed.Succeeded && changed.CurrentRevision == revision + 1, "state-change fixture failed");
                        var staleResult = controller.Apply(stale);
                        Require(!staleResult.Succeeded && _stateCoordinator.Refresh().Revision == revision + 1, "stale preview applied or changed state");
                        cases.Add("state-change-after-preview-rejected-without-partial-apply");
                        step = 2; break;
                    case 2:
                        revision = current.Revision;
                        var changedTicket = controller.Preview(changedPath, PortableImportMode.Append, false);
                        controller.Save(changedPath, originalBytes.Concat(new byte[] { 10 }).ToArray(), true);
                        var changedResult = controller.Apply(changedTicket);
                        Require(!changedResult.Succeeded && _stateCoordinator.Refresh().Revision == revision, "source file changed after preview but was applied");
                        Require(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(originalPath))) == originalHash,
                            "the original exported file was modified");
                        cases.Add("source-hash-change-rejected-original-export-unchanged");
                        var clear = controller.Preview(emptyPath, PortableImportMode.Replace, false);
                        Require(clear.Plan.Creates.Count == 0 && clear.Plan.Removes.Count == current.Posters.Count, "replace-zero preview mismatch");
                        var cleared = controller.Apply(clear);
                        Require(cleared.Succeeded && cleared.Command?.CurrentRevision == revision + 1, "replace-zero did not commit once: " + cleared.Message);
                        step = 3; break;
                    case 3:
                        Require(current.Revision == revision + 1 && current.Posters.Count == 0 && current.ProfileInitialized,
                            "replace-zero reseeded the default poster or lost profile initialization");
                        Require(_posterSessions!.Sessions.Count == 0 && _inputRouter.ReadyCount == 0, "replace-zero left native/input sessions");
                        cases.Add("replace-zero-single-revision-no-default-reseed-no-sessions");
                        revision = current.Revision;
                        var restore = controller.Preview(originalPath, PortableImportMode.Replace, true);
                        restoredDefinitions = restore.Plan.Creates.ToArray();
                        var restored = controller.Apply(restore);
                        Require(restored.Succeeded && restored.Command?.CurrentRevision == revision + 1, "restoring captured profile failed: " + restored.Message);
                        step = 4; break;
                    case 4:
                        Require(current.Revision == revision + 1 && current.Posters.Count == initialCount, "original count not restored");
                        Require(current.ProfileInitialized && controller.PendingNames().Count == 0, "restored original is uninitialized or pending");
                        Require(restoredDefinitions.All(p => current.PostersById[p.PosterId].Poster == p),
                            "restored state differs from the confirmed projection plan");
                        var finalDocument = PortableProfileCodec.Parse(controller.Export());
                        Require(finalDocument.Settings == originalDocument!.Settings, "global portable settings were not restored");
                        for (int i = 0; i < finalDocument.Posters.Count; i++)
                        {
                            var source = originalDocument.Posters[i]; var actual = finalDocument.Posters[i];
                            // Source fixtures name DISPLAY1; the native environment may name it \\.\DISPLAY1.
                            // Mapping and physical-pixel anchor rounding are previewed changes, not semantic loss.
                            Require(JsonElement.DeepEquals(actual.LayoutParameters, source.LayoutParameters) &&
                                actual with { MonitorHint = source.MonitorHint, HorizontalAnchor = source.HorizontalAnchor,
                                    VerticalAnchor = source.VerticalAnchor, LayoutParameters = source.LayoutParameters } == source,
                                "portable non-geometric preferences or authored DIP dimensions changed during restore");
                        }
                        var real = current.Posters.Where(p => p.IsEffectivelyVisible)
                            .Select(p => _spatialRuntime!.ReadControlCenterGeometry(p.Poster.PosterId)).ToArray();
                        Require(real.All(p => p is not null), "restored native geometry is missing");
                        for (int i = 0; i < real.Length; i++)
                        for (int j = i + 1; j < real.Length; j++)
                            Require(!real[i]!.Bounds.Overlaps(real[j]!.Bounds), "restored native geometry overlaps");
                        cases.Add("replace-original-restores-real-sessions-and-settings");
                        var store = _services!.GetRequiredService<IStateStore>();
                        var paths = _services!.GetRequiredService<IUserDataPaths>();
                        var backup = controller.Backup();
                        Require(backup.Succeeded && backup.FileName is not null && backup.Revision == current.Revision, "manual backup failed");
                        Require(store.ValidateBackup(Path.Combine(paths.Backups, backup.FileName!)).IsValid, "manual backup is not valid");
                        var preImports = Directory.GetFiles(paths.Backups, "*-pre-portable-import.sqlite3");
                        Require(preImports.Length >= 3 && preImports.All(path => store.ValidateBackup(path).IsValid), "pre-import backup missing or invalid");
                        cases.Add("validated-pre-import-and-manual-backups-not-live-restore");
                        File.WriteAllText(proofPath, JsonSerializer.Serialize(new PortableRestartProof(current.Revision, Fingerprint(current), current.Posters.Count)));
                        Finish(true, null); break;
                }
            }
            catch (Exception error) { Finish(false, error.ToString()); }
            finally { ticking = false; }
        }
        timer.Tick += Tick; timer.Start();
    }
}
