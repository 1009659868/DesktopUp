using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;

namespace DesktopUp;

public partial class App
{
    // One focused composition check. No native input synthesis or file operations.
    private async void RunContentHeatRuntimeProbe(string dataRoot, bool postureOnly = false)
    {
        IDisposable? lease = null;
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            string repo = AppContext.BaseDirectory;
            while (!File.Exists(Path.Combine(repo, "DesktopUp.sln")))
                repo = Path.GetDirectoryName(repo) ?? throw new IOException("Repository fixture required.");
            string root = Path.GetDirectoryName(Path.GetFullPath(dataRoot))!;
            if (!LocalTransferFiles.Contains(Path.Combine(repo, "out", "drop-d3"), root) ||
                Path.GetFileName(dataRoot) != "profile" ||
                Environment.GetEnvironmentVariable("DESKTOPUP_V3_TEST_DATA_ROOT") != dataRoot ||
                !File.Exists(Path.Combine(root, ".heat-runtime-fixture")) ||
                File.ReadAllText(Path.Combine(root, ".heat-runtime-fixture")) != "owned")
                throw new IOException("Owned isolated heat-runtime fixture required.");

            lease = _ledger!.Acquire("fixture.heat-runtime", "derived-projection");
            _stopAutoSmoke = () => cancellation.Cancel();
            await _transferDiscovery;
            async Task Until(Func<bool> ready)
            {
                while (!ready()) await Task.Delay(50, cancellation.Token);
            }
            void Require(bool value, string message)
            {
                if (!value) throw new IOException(message);
            }
            await Until(() => _autoReady && _spatialRuntime is { IsSpatialBusy: false });
            string id = BuiltInStateIdentities.PrimaryPosterId;
            var spatial = _spatialRuntime!;
            var window = spatial.BackdropWindowForProbe(id);
            await window.CaptureRenderIdentityForProbe();
            bool presentationProbe = _startupIntent.Arguments.Contains("--r3-presentation-probe");
            if (presentationProbe)
            {
                window.VerifyContentPublicationForProbe();
                if (window.EditContent(new(ContentLayoutEditKind.Mode) { Mode = ContentLayoutMode.Manual }) is { } error)
                    throw new IOException(error);
                await window.VerifyIncrementalRenderForProbe();
            }
            if (postureOnly || presentationProbe)
            {
                await window.VerifyPostureRastersForProbe(async collapsed =>
                {
                    var current = _stateCoordinator!.Refresh();
                    var poster = current.PostersById[id];
                    var done = new TaskCompletionSource<StateCommandResult>();
                    spatial.SetCollapsed(new("raster-posture-" + Guid.NewGuid().ToString("N"),
                        StateCommandSource.ControlCenter, PosterStateCommandKind.SetCollapsed,
                        current.Revision, id, poster.Poster with { IsCollapsed = collapsed })
                        { ReadSet = StateReadSet.ForPoster(id, poster.EntityVersion) }, result => done.TrySetResult(result));
                    var result = await done.Task.WaitAsync(cancellation.Token);
                    Require(result.Succeeded, "Posture command failed: " + result.Error);
                });
                return;
            }
            var author = spatial.ReadContentEditor(id)!.Document;
            long revision = _stateCoordinator!.Refresh().Revision;
            string raw = ContentLayoutPolicy.Serialize(author);
            Require(author.Mode == ContentLayoutMode.Automatic && author.Placements.Count >= 2,
                "Automatic multi-item fixture required.");

            ContentHeatProposal Heat(long version, bool reverse) => new(id, author.ItemSetVersion, version,
                author.Placements.Select((item, index) =>
                    new ContentHeatItem(item.ItemId, reverse ? author.Placements.Count - index - 1 : index, 1)).ToArray());

            Require(spatial.SetContentEditing(id, true) is null, "Cannot enter protection.");
            var before = spatial.ReadContentSurfaceForSmoke(id);
            var render = await window.CaptureRenderIdentityForProbe();
            Require(spatial.OfferContentHeatForSmoke(id, Heat(1, false)) &&
                spatial.OfferContentHeatForSmoke(id, Heat(2, true)), "Proposal admission failed.");
            await Task.Delay(100, cancellation.Token);
            var protectedState = spatial.ReadContentProposalForSmoke(id);
            Require(protectedState.HeatPending && protectedState.ActiveHeatVersion is null &&
                spatial.ReadContentSurfaceForSmoke(id).GeometryFingerprint == before.GeometryFingerprint,
                "Protected proposal changed the displayed geometry.");
            window.RequireRetainedRenderForProbe(render);

            Require(spatial.SetContentEditing(id, false) is null, "Cannot release protection.");
            await Until(() => spatial.ReadContentProposalForSmoke(id).ActiveHeatVersion == 2);
            var applied = spatial.ReadContentProposalForSmoke(id);
            var surface = spatial.ReadContentSurfaceForSmoke(id);
            Require(!applied.HeatPending && surface.CanvasMatches && surface.InputMatches &&
                surface.Handle == before.Handle, "Published canvas and input do not share the accepted layout.");
            Require(ContentLayoutPolicy.Serialize(applied.EffectiveIntent) ==
                ContentLayoutPolicy.Serialize(ContentLayoutProposals.ResolveHeat(author, Heat(2, true)).Document),
                "The latest proposal was not derived from author intent.");
            window.RequireRetainedRenderForProbe(render);
            Require(!spatial.OfferContentHeatForSmoke(id, Heat(1, false)) &&
                !spatial.OfferContentHeatForSmoke(id, Heat(3, false) with { ItemSetVersion = author.ItemSetVersion + 1 }),
                "A stale proposal was accepted.");

            Require(spatial.OfferContentHeatForSmoke(id, new(id, author.ItemSetVersion, 3, [])), "Neutral restore failed.");
            await Until(() => spatial.ReadContentProposalForSmoke(id).ActiveHeatVersion == 3);
            var restored = spatial.ReadContentProposalForSmoke(id);
            Require(ContentLayoutPolicy.Serialize(restored.EffectiveIntent) == raw &&
                ContentLayoutPolicy.Serialize(restored.AuthorIntent) == raw &&
                _stateCoordinator.Refresh().Revision == revision, "A derived projection changed author state.");
            surface = spatial.ReadContentSurfaceForSmoke(id);
            Require(surface.CanvasMatches && surface.InputMatches, "Neutral restoration did not publish coherently.");
            window.RequireRetainedRenderForProbe(render);
            if (_startupIntent!.Arguments.Contains("--r2-session-probe", StringComparer.Ordinal))
            {
                _controlCenter!.Show();
                await Until(() => _controlCenter.SettingsWindowForContractProbe?.Content.XamlRoot is not null);
                var center = _controlCenter.SettingsWindowForContractProbe!;
                center.SelectSettingsTargetForProbe(id);
                center.SelectContentItemForProbe(author.Placements[0].ItemId);
                center.PrepareContentFieldsForProbe(980, 1040);
                center.SetContentFieldsForProbe("span", "bad");
                Require(spatial.HasCenterDraft(id), "The invalid raw center draft did not acquire protection.");
                var baseline = window.ReadCenterProposalWakeForProbe();
                var latestTheme = baseline.ThemeParameters with
                { Theme = baseline.ThemeParameters.Theme with { Accent = "#AABBCC" } };
                Require(window.OfferContentHeat(Heat(4, false)) && window.OfferContentHeat(Heat(5, true)) &&
                    window.OfferContentTheme(new(1, latestTheme)), "Center-protected proposals were refused.");
                await Until(() => window.ReadCenterProposalWakeForProbe().WaitingForCenterRelease);
                var waiting = window.ReadCenterProposalWakeForProbe();
                Require(waiting.ActiveHeatVersion == 3 && waiting.ThemeParameters == baseline.ThemeParameters &&
                    waiting.GeometryFingerprint == baseline.GeometryFingerprint, "Center draft did not protect publication.");
                center.CancelContentFieldsForProbe("span");
                await Until(() => window.ReadCenterProposalWakeForProbe().ActiveHeatVersion == 5 &&
                    window.ReadCenterProposalWakeForProbe().ThemeParameters == latestTheme);
                Require(!spatial.HasCenterDraft(id) && _stateCoordinator.Refresh().Revision == revision,
                    "Release wrote author state or retained center ownership.");
                var settled = window.ReadCenterProposalWakeForProbe();
                await Task.Delay(100, cancellation.Token);
                Require(window.ReadCenterProposalWakeForProbe().ViewportGeneration == settled.ViewportGeneration,
                    "Settled session kept scheduling reflows.");
                _log!.Write("info", "contract.r2_center_release_passed", new { latest = 5, persisted = false, polling = false });
            }
            _log!.Write("info", "contract.heat_runtime_passed", new
            {
                nativeMouse = false, nativeKeyboard = false, latest = 2, restored = 3,
                surface.CanvasMatches, surface.InputMatches, retainedCardsAndRasters = true,
                persisted = false
            });
        }
        catch (Exception error)
        {
            Environment.ExitCode = 1;
            _log?.Write("error", "contract.heat_runtime_failed", new { error.Message, error.StackTrace });
        }
        finally
        {
            _stopAutoSmoke = null;
            lease?.Dispose();
            ExitApplication();
        }
    }
}
