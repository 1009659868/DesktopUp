using DesktopUp.Application;

namespace DesktopUp;

// Linked only by DesktopUpReviewTools=true; original probe order and guards are preserved.
public partial class App
{
    partial void RunReviewTools()
    {
        if (_composition is null || _log is null || _dispatcher is null ||
            _controlCenter is null || _spatialRuntime is null || _ledger is null) return;
        if(_startupIntent.Arguments.Contains("--real-activity-probe",StringComparer.Ordinal))
        { RunRealActivityRuntimeProbe(_composition.Paths.Root);return; }
        if(_startupIntent.Arguments.Contains("--content-raster-posture-probe",StringComparer.Ordinal))
        { RunContentHeatRuntimeProbe(_composition.Paths.Root, postureOnly: true);return; }
        if(_startupIntent.Arguments.Contains("--content-heat-runtime-probe",StringComparer.Ordinal))
        { RunContentHeatRuntimeProbe(_composition.Paths.Root);return; }
        if(_startupIntent.Arguments.Contains("--incoming-probe",StringComparer.Ordinal))
        { RunIncomingProbe(_composition.Paths.Root);return; }
        if(_startupIntent.Arguments.Contains("--storage-settings-probe",StringComparer.Ordinal))
        {
            RunStorageSettingsProbe(_composition.Paths.Root);
            return;
        }
        string[] smokeFlags = ["--content-reference-probe", "--posture-groups-probe", "--rain-resource-probe", "--rain-scene-probe", "--rain-scene-restart", "--scene-module-probe", "--content-source-probe", "--content-browse-probe", "--backdrop-probe", "--lamp-pointer-probe", "--template-recipe-probe", "--template-recipe-restart", "--content-composition-probe", "--content-fields-probe", "--content-rejected-heat-probe", "--content-protocol-probe", "--content-native-probe", "--content-native-mouse-probe", "--content-interaction-restart", "--lamp-parameter-probe", "--ui-c-sample-probe", "--content-layout-smoke", "--content-layout-restart", "--picker-smoke", "--portable-smoke", "--tray-smoke", "--control-center-smoke", "--title-surface-smoke", "--auto-measurement", "--auto-lifetime-smoke", "--auto-settings-smoke", "--auto-posture-smoke", "--continuity-smoke", "--continuity-arrange", "--menu-smoke", "--presentation-smoke", "--spatial-smoke", "--input-confirmation-smoke", "--multi-instance-smoke", "--multi-instance-restart"];
        if (_startupIntent.Arguments.Count(a => smokeFlags.Contains(a, StringComparer.OrdinalIgnoreCase)) > 1)
        {
            _log.Write("error", "contract.smoke_refused", new { reason = "one-smoke-per-process" });
            return;
        }
        if (_startupIntent.Arguments.Any(a => a is "--content-reference-probe" or "--posture-groups-probe" or "--rain-resource-probe" or "--rain-scene-probe" or "--rain-scene-restart" or "--scene-module-probe" or "--content-source-probe" or "--content-browse-probe" or "--backdrop-probe" or "--lamp-pointer-probe" or "--template-recipe-probe" or "--template-recipe-restart" or "--content-composition-probe" or "--content-fields-probe" or "--content-rejected-heat-probe" or "--content-protocol-probe" or "--content-native-probe" or "--content-native-mouse-probe" or "--content-interaction-restart" or "--lamp-parameter-probe" or "--ui-c-sample-probe" or "--content-layout-smoke" or "--content-layout-restart" or "--picker-smoke" or "--portable-smoke" or "--tray-smoke" or "--control-center-smoke" or "--title-surface-smoke" or "--auto-posture-smoke" or "--auto-settings-smoke" or "--auto-lifetime-smoke" or "--auto-measurement"))
        {
            string? testRoot = Environment.GetEnvironmentVariable("DESKTOPUP_V3_TEST_DATA_ROOT");
            string normalRoot = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopUp", "V3"));
            string actual = Path.GetFullPath(_composition.Paths.Root);
            if (string.IsNullOrWhiteSpace(testRoot) || actual.Equals(normalRoot, StringComparison.OrdinalIgnoreCase) ||
                actual.StartsWith(normalRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                _log.Write("error", "auto_posture.smoke_refused", new { reason = "isolated-profile-required" });
#if SCENE_RAIN
            else if (_startupIntent.Arguments.Contains("--rain-resource-probe", StringComparer.Ordinal)) RunRainResourceProbe();
            else if (_startupIntent.Arguments.Contains("--rain-scene-probe", StringComparer.Ordinal)) RunRainSceneProbe(actual, false);
            else if (_startupIntent.Arguments.Contains("--rain-scene-restart", StringComparer.Ordinal)) RunRainSceneProbe(actual, true);
#endif
            else if (_startupIntent.Arguments.Contains("--posture-groups-probe", StringComparer.Ordinal))
            {
                if (_startupIntent.Arguments.Contains("--rr-combinations")) RunPostureCombinationProbe(_startupIntent.Arguments.Contains("--rr-chain"));
                else RunPostureGroupsProbe(_startupIntent.Arguments.Contains("--rr-chain"));
            }
            else if (_startupIntent.Arguments.Contains("--scene-module-probe", StringComparer.Ordinal)) RunSceneModuleProbe(actual);
            else if (_startupIntent.Arguments.Contains("--content-reference-probe", StringComparer.Ordinal)) RunContentReferenceProbe(actual);
            else if (_startupIntent.Arguments.Contains("--content-source-probe", StringComparer.Ordinal)) RunContentSourceProbe(actual);
            else if (_startupIntent.Arguments.Contains("--content-browse-probe", StringComparer.Ordinal)) RunContentBrowseProbe(actual);
#if SCENE_LAMP
            else if (_startupIntent.Arguments.Contains("--backdrop-probe", StringComparer.Ordinal)) RunBackdropProbe(actual);
#endif
#if SCENE_LAMP
            else if (_startupIntent.Arguments.Contains("--lamp-pointer-probe", StringComparer.Ordinal)) RunLampPointerProbe(actual);
#endif
            else if (_startupIntent.Arguments.Contains("--template-recipe-probe", StringComparer.Ordinal)) RunTemplateRecipeProbe(actual, false);
            else if (_startupIntent.Arguments.Contains("--template-recipe-restart", StringComparer.Ordinal)) RunTemplateRecipeProbe(actual, true);
            else if (_startupIntent.Arguments.Contains("--content-composition-probe", StringComparer.Ordinal)) RunContentCompositionProbe(actual);
            else if (_startupIntent.Arguments.Contains("--content-fields-probe", StringComparer.Ordinal)) RunContentFieldsProbe(actual);
            else if (_startupIntent.Arguments.Contains("--content-protocol-probe", StringComparer.Ordinal)) RunContentInteractionProbe(actual,"protocol");
            else if (_startupIntent.Arguments.Contains("--content-rejected-heat-probe", StringComparer.Ordinal)) RunContentInteractionProbe(actual,"rejected-heat");
            else if (_startupIntent.Arguments.Contains("--content-native-probe", StringComparer.Ordinal)) RunContentInteractionProbe(actual,"native");
            else if (_startupIntent.Arguments.Contains("--content-native-mouse-probe", StringComparer.Ordinal)) RunContentInteractionProbe(actual,"native-mouse");
            else if (_startupIntent.Arguments.Contains("--content-interaction-restart", StringComparer.Ordinal)) RunContentInteractionProbe(actual,"restart");
#if SCENE_LAMP
            else if (_startupIntent.Arguments.Contains("--lamp-parameter-probe", StringComparer.Ordinal)) RunLampParameterProbe(actual);
#endif
            else if (_startupIntent.Arguments.Contains("--ui-c-sample-probe", StringComparer.Ordinal)) RunUiCSampleProbe(actual);
            else if (_startupIntent.Arguments.Any(a => a is "--content-layout-smoke" or "--content-layout-restart")) RunContentLayoutSmoke(actual);
            else if (_startupIntent.Arguments.Contains("--picker-smoke", StringComparer.Ordinal)) RunPickerSmoke(actual);
            else if (_startupIntent.Arguments.Contains("--portable-smoke", StringComparer.Ordinal)) RunPortableSmoke(actual);
            else if (_startupIntent.Arguments.Contains("--tray-smoke", StringComparer.Ordinal)) RunTraySmoke(actual);
            else if (_startupIntent.Arguments.Contains("--control-center-smoke", StringComparer.Ordinal)) RunControlCenterSmoke(actual);
            else if (_startupIntent.Arguments.Contains("--title-surface-smoke", StringComparer.Ordinal))
            {
                var lease = _ledger.Acquire("fixture.title-surface", "isolated-r4-component-faults");
                var stop = _spatialRuntime.RunTitleSurfaceSmoke(_dispatcher, () =>
                {
                    lease.Dispose(); _stopAutoSmoke = null; ExitApplication();
                });
                _stopAutoSmoke = () => { try { stop(); } finally { lease.Dispose(); _stopAutoSmoke = null; } };
            }
            else if (_startupIntent.Arguments.Contains("--auto-measurement", StringComparer.Ordinal))
            {
                var modes = _startupIntent.Arguments.Where(a => a.StartsWith("--measurement-mode=", StringComparison.Ordinal)).ToArray();
                string mode = modes.Length == 1 ? modes[0]["--measurement-mode=".Length..] : "";
                if (mode is not ("off-expanded" or "on-expanded" or "off-collapsed" or "on-collapsed" or "off-trigger" or "on-trigger"))
                {
                    _log.Write("error", "auto_measurement.refused", new { reason = "invalid-mode" });
                    ExitApplication();
                }
                else RunAutoMeasurement(mode);
            }
            else if (_startupIntent.Arguments.Contains("--auto-lifetime-smoke", StringComparer.Ordinal))
            {
                var modes = _startupIntent.Arguments.Where(a => a.StartsWith("--lifetime-mode=", StringComparison.Ordinal)).ToArray();
                string mode = modes.Length == 0 ? "exercise" : modes[0]["--lifetime-mode=".Length..];
                if (modes.Length > 1 || mode is not ("exercise" or "enabled-exit" or "restart" or "continuous"))
                {
                    _log.Write("error", "auto_lifetime.smoke_refused", new { reason = "invalid-mode" });
                    ExitApplication();
                }
                else if (mode == "continuous") RunContinuousLifetimeProbe();
                else RunAutoLifetimeSmoke(mode);
            }
            else if (_startupIntent.Arguments.Contains("--auto-settings-smoke", StringComparer.Ordinal)) RunAutoSettingsSmoke(actual);
            else RunAutoPostureSmoke();
        }
        if (_startupIntent.Arguments.Any(a => a is "--multi-instance-smoke" or "--multi-instance-restart"))
        {
            string? testRoot = Environment.GetEnvironmentVariable("DESKTOPUP_V3_TEST_DATA_ROOT");
            string normalRoot = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopUp", "V3"));
            string actual = Path.GetFullPath(_composition.Paths.Root);
            if (string.IsNullOrWhiteSpace(testRoot) || actual.Equals(normalRoot, StringComparison.OrdinalIgnoreCase) ||
                actual.StartsWith(normalRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                _log.Write("error", "multi_instance.smoke_refused", new { reason = "isolated-profile-required" });
            else RunMultiInstanceSmoke(_startupIntent.Arguments.Contains("--multi-instance-restart", StringComparer.Ordinal));
        }
        if (_startupIntent.Arguments.Any(a => a is "--continuity-smoke" or "--continuity-arrange"))
        {
            string? testRoot = Environment.GetEnvironmentVariable("DESKTOPUP_V3_TEST_DATA_ROOT");
            string normalRoot = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopUp", "V3"));
            string actual = Path.GetFullPath(_composition.Paths.Root);
            if (string.IsNullOrWhiteSpace(testRoot) || actual.Equals(normalRoot, StringComparison.OrdinalIgnoreCase) ||
                actual.StartsWith(normalRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                _log.Write("error", "continuity.smoke_refused", new { reason = "isolated-profile-required" });
            else _spatialRuntime.RunContinuitySmoke(_dispatcher, _startupIntent.Arguments.Contains("--continuity-arrange", StringComparer.Ordinal));
        }
        if (_startupIntent.Arguments.Contains("--menu-smoke", StringComparer.OrdinalIgnoreCase))
        {
            string? testRoot = Environment.GetEnvironmentVariable("DESKTOPUP_V3_TEST_DATA_ROOT");
            string normalRoot = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopUp", "V3"));
            string actual = Path.GetFullPath(_composition.Paths.Root);
            if (string.IsNullOrWhiteSpace(testRoot) || actual.Equals(normalRoot, StringComparison.OrdinalIgnoreCase) ||
                actual.StartsWith(normalRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                _log.Write("error", "menu.smoke_refused", new { reason = "isolated-profile-required" });
            else _spatialRuntime.RunMenuSmoke(_dispatcher);
        }
        if (_startupIntent.Arguments.Contains("--presentation-smoke", StringComparer.OrdinalIgnoreCase))
        {
            string? testRoot = Environment.GetEnvironmentVariable("DESKTOPUP_V3_TEST_DATA_ROOT");
            string normalRoot = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopUp", "V3"));
            string actual = Path.GetFullPath(_composition.Paths.Root);
            if (string.IsNullOrWhiteSpace(testRoot) || actual.Equals(normalRoot, StringComparison.OrdinalIgnoreCase) ||
                actual.StartsWith(normalRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                _startupIntent.Arguments.Any(a => a is "--spatial-smoke" or "--input-confirmation-smoke"))
                _log.Write("error", "presentation.smoke_refused", new { reason = "exclusive-isolated-profile-required" });
            else _spatialRuntime.RunPresentationSmoke(_dispatcher);
        }
        if (_startupIntent.Arguments.Contains("--spatial-smoke", StringComparer.OrdinalIgnoreCase))
        {
            string? testRoot = Environment.GetEnvironmentVariable("DESKTOPUP_V3_TEST_DATA_ROOT");
            string normalRoot = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopUp", "V3"));
            string actual = Path.GetFullPath(_composition.Paths.Root);
            if (string.IsNullOrWhiteSpace(testRoot) || actual.Equals(normalRoot, StringComparison.OrdinalIgnoreCase) ||
                actual.StartsWith(normalRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                _log.Write("error", "spatial.smoke_refused", new { reason = "isolated-profile-required" });
            else _spatialRuntime.RunIsolatedSmoke(_dispatcher, _startupIntent.Arguments
                .FirstOrDefault(a => a.StartsWith("--expect-expansion=", StringComparison.Ordinal))?.Split('=')[1],
                _startupIntent.Arguments.Contains("--spatial-smoke-leave-expanded", StringComparer.Ordinal));
        }
        if (_startupIntent.Arguments.Contains("--input-confirmation-smoke", StringComparer.OrdinalIgnoreCase))
        {
            string? testRoot = Environment.GetEnvironmentVariable("DESKTOPUP_V3_TEST_DATA_ROOT");
            string normalRoot = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopUp", "V3"));
            string actual = Path.GetFullPath(_composition.Paths.Root);
            if (string.IsNullOrWhiteSpace(testRoot) || actual.Equals(normalRoot, StringComparison.OrdinalIgnoreCase) ||
                actual.StartsWith(normalRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                _log.Write("error", "input_confirmation.smoke_refused", new { reason = "isolated-profile-required" });
            else _controlCenter.RunIsolatedConfirmationSmoke(_dispatcher,
                _startupIntent.Arguments.Contains("--exit-with-confirmation", StringComparer.OrdinalIgnoreCase));
        }
    }

    partial void StopReviewTools() => _stopAutoSmoke?.Invoke();
    partial void StartOfflineRecoveryReview() => StartOfflineRecoverySmokeIfRequested();
}

internal sealed partial class PosterSpatialRuntime
{
    partial void StopContinuityReview() => _continuityProbeStop?.Invoke();
}

public sealed partial class ControlCenterWindow
{
    internal bool VerifyProjection(DesktopUpProjection expected) => _view?.Projection.Revision == expected.Revision &&
        PosterSelector.ItemsSource is IReadOnlyList<ControlCenterPosterRow> rows &&
        rows.Select(row => row.PosterId).SequenceEqual(expected.Posters.Select(p => p.Poster.PosterId)) &&
        PosterTitleText.Text == (_view.SelectedPoster?.Poster.Title ?? "暂无海报") && VerifyMoreMenuForProbe();
}
