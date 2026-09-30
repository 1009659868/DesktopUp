using System.Text.Json;
using DesktopUp.Domain;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DesktopUp;

internal sealed record SceneModuleObservation(string Capability, bool Active, bool Connected, bool DecorativeOnly, bool Failed);
internal sealed partial class PosterVisualWindow
{
    internal SceneModuleObservation ReadSceneModuleForProbe() => new(CurrentSceneParameters.Capability, _sceneInstance is not null,
        _sceneInstance is { } instance && VisualTreeHelper.GetParent(instance.Root) is not null,
        _sceneInstance?.Root.IsHitTestVisible != true, _sceneFailure.Failure is not null);
}
internal sealed partial class PosterSpatialRuntime
{
    internal SceneModuleObservation ReadSceneModuleForProbe(string id) => _windows[id].ReadSceneModuleForProbe();
}
public sealed partial class ControlCenterWindow
{
    internal object ReadSceneModuleControlsForProbe() => new
    {
        templateEnabled = _template.IsEnabled,
        parametersEnabled = _lampParameterFolder?.IsEnabled == true,
        parametersVisible = _lampParameterFolder?.Visibility == Visibility.Visible,
        notice = _editNotice.Message
    };
}
public partial class App
{
    private void RunSceneModuleProbe(string dataRoot)
    {
        var timer = _dispatcher!.CreateTimer(); timer.Interval = TimeSpan.FromMilliseconds(100);
        var lease = _ledger!.Acquire("fixture.scene-module", "physical-module-removal");
        long deadline = _autoClock.ElapsedMilliseconds + 25000;
        bool stopped = false, ticking = false; int index = 0;
        Task? capture = null; var observations = new List<object>();
        using var expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(dataRoot, "scene-module-expected.json")));
        string expectedCapability = expected.RootElement.TryGetProperty("capability", out var cap) ? cap.GetString()! : TemplateParameterSchemas.LampCapability;
        string expectedCatalog = expected.RootElement.GetProperty("catalog").GetString()!;
        var rows = expected.RootElement.GetProperty("entries").EnumerateArray().Select(e => e.Clone()).ToArray();
        void Stop() { if (stopped) return; stopped = true; timer.Stop(); timer.Tick -= Tick; lease.Dispose(); _stopAutoSmoke = null; }
        _stopAutoSmoke = Stop;
        void Finish(bool passed, string? error)
        {
            Stop(); _log!.Write(passed ? "info" : "error", "scene.module_observed", new { passed, error, observations,
                lampAvailable = SceneCapabilityRegistry.Available(TemplateParameterSchemas.LampCapability),
                moduleAvailable = SceneCapabilityRegistry.Available(expectedCapability),
                nativeInput = false, desktopCompositionCertified = false });
            ExitApplication();
        }
        void Tick(DispatcherQueueTimer sender, object args)
        {
            if (stopped || ticking) return; ticking = true;
            try
            {
                if (_autoClock.ElapsedMilliseconds > deadline) throw new InvalidOperationException("Module observation timed out.");
                if (!_autoReady || _spatialRuntime!.IsSpatialBusy) return;
                var current = _stateCoordinator!.Refresh();
                if (current.ControlCenter.Settings.Settings.TemplateCatalogJson != expectedCatalog)
                    throw new InvalidOperationException("Catalog was rewritten.");
                if (_controlCenter!.SettingsWindowForContractProbe is not { } window) { _controlCenter.Show(); return; }
                if (window.Content.XamlRoot is null) return;
                if (capture is not null)
                {
                    if (!capture.IsCompleted) return;
                    capture.GetAwaiter().GetResult(); capture = null; index++;
                }
                if (index == rows.Length) { Finish(true, null); return; }
                var row = rows[index]; string id = row.GetProperty("id").GetString()!;
                var poster = current.PostersById[id];
                if (poster.Poster.TemplateKey != row.GetProperty("template").GetString() ||
                    poster.Poster.LayoutParametersJson != row.GetProperty("parameters").GetString() ||
                    poster.Poster.ContentLayoutJson != row.GetProperty("content").GetString())
                    throw new InvalidOperationException("Stored template/content was rewritten.");
                bool minimal = poster.Poster.TemplateKey == "basic-box";
                bool available = minimal || SceneCapabilityRegistry.Available(expectedCapability);
                if (poster.Presentation.CanEdit != available) throw new InvalidOperationException("Missing capability edit admission differs.");
                var scene = _spatialRuntime.ReadSceneModuleForProbe(id);
                if (!_spatialRuntime.BackdropWindowForProbe(id).VerifyTitleChromeForProbe())
                    throw new InvalidOperationException("Title paint/input/fallback geometry differs.");
                if (scene.Active != (!minimal && available) || scene.Failed || !scene.DecorativeOnly ||
                    scene.Active && !scene.Connected) throw new InvalidOperationException("Scene lifecycle/fallback differs.");
                window.SelectSettingsTargetForProbe(id);
                observations.Add(new { id, scene, poster.Presentation.CanEdit, poster.Presentation.Warning,
                    controls = window.ReadSceneModuleControlsForProbe(), surface = _spatialRuntime.ReadContentSurfaceForSmoke(id) });
                capture = window.CaptureSettingsForProbeAsync(Path.Combine(dataRoot, "scene-module-" + Environment.ProcessId + "-" + index + ".png"));
            }
            catch (Exception error) { Finish(false, error.ToString()); }
            finally { ticking = false; }
        }
        timer.Tick += Tick; timer.Start();
    }
}
