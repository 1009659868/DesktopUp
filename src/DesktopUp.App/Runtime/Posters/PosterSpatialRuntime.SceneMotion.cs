using System.Diagnostics;
using DesktopUp.Application;
using DesktopUp.Windows;
using Microsoft.UI.Dispatching;
using Windows.UI.ViewManagement;

namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    private readonly HashSet<PosterVisualWindow> _sceneMotionSurfaces = [];
    private readonly Stopwatch _sceneMotionClock = new();
    private DispatcherQueueTimer? _sceneMotionTimer;
    private DispatcherQueue? _sceneMotionDispatcher;
    private IResourceLedger? _sceneMotionLedger;
    private IDisposable? _sceneMotionTimerLease, _sceneMotionSettingsLease;
    private PassivePosterPointerObserver? _scenePointer;
    private UISettings? _sceneUiSettings;
    private long _scenePointerSequence;
    private bool _sceneAnimationsEnabled, _sceneMotionFaulted;

    internal void ConfigureSceneMotionLedger(IResourceLedger resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        if (_sceneMotionLedger is not null && !ReferenceEquals(_sceneMotionLedger, resources))
            throw new InvalidOperationException("The shared motion ledger is already configured.");
        _sceneMotionLedger = resources;
    }

    // All scenes share one read-only sample and one bounded frame clock. Auto-posture keeps
    // its own deadlines/evidence. A trusted title-motion proposal is published by its host's
    // title-only native path; the scene still never owns input or issues commands.
    internal void SetSceneMotionDemand(PosterVisualWindow window, bool needed,
        PosterInputRouter router)
    {
        if (!needed) _sceneMotionSurfaces.Remove(window);
        else
        {
            _sceneMotionSurfaces.Add(window);
            _sceneMotionDispatcher = window.SpatialDispatcher;
            _scenePointer ??= new(router);
            EnsureAnimationSettings(window.SpatialDispatcher);
        }
        try { ArmSceneMotion(); }
        catch (Exception error) { StopFailedSceneMotion("arming", error); }
    }

    // Content transitions share the accessibility observer, not scene pointer/timer demand.
    // A basic-box poster has no animated scene and must still read the real system preference.
    internal bool CanAnimateContent(DispatcherQueue dispatcher)
    {
        EnsureAnimationSettings(dispatcher);
        return _sceneAnimationsEnabled && !_sceneMotionFaulted && !SceneGlobalMotionPaused;
    }

    private void EnsureAnimationSettings(DispatcherQueue dispatcher)
    {
        _sceneMotionDispatcher ??= dispatcher;
        if (_sceneUiSettings is not null || _sceneMotionFaulted) return;
        UISettings? settings = null;
        bool subscribed = false;
        try
        {
            if (_sceneMotionLedger is null) throw new InvalidOperationException("The shared motion ledger is not configured.");
            settings = new UISettings();
            _sceneAnimationsEnabled = settings.AnimationsEnabled;
            settings.AnimationsEnabledChanged += SceneAnimationsEnabledChanged;
            subscribed = true;
            _sceneMotionSettingsLease = _sceneMotionLedger.Acquire("subscription.scene-motion-settings", "shared-reduced-motion-observer");
            _sceneUiSettings = settings;
        }
        catch (Exception error)
        {
            if (subscribed)
                try { settings!.AnimationsEnabledChanged -= SceneAnimationsEnabledChanged; }
                catch (Exception cleanup) { log.Write("warning", "scene.motion_settings_cleanup_failed", new { cleanup.Message }); }
            _sceneAnimationsEnabled = false;
            _sceneMotionFaulted = true;
            log.Write("warning", "scene.motion_settings_unavailable", new { error.Message });
        }
    }

    private void SceneAnimationsEnabledChanged(UISettings sender, UISettingsAnimationsEnabledChangedEventArgs args) =>
        _sceneMotionDispatcher?.TryEnqueue(() =>
        {
            if (!ReferenceEquals(sender, _sceneUiSettings)) return;
            try
            {
                _sceneAnimationsEnabled = sender.AnimationsEnabled;
                if (!_sceneAnimationsEnabled)
                    foreach (var window in _sceneMotionSurfaces.ToArray()) window.PaintSceneMotion(0, null, false);
                ArmSceneMotion();
            }
            catch (Exception error) { StopFailedSceneMotion("accessibility-change", error); }
        });

    private void ArmSceneMotion()
    {
        bool running = !_suspended && !SceneGlobalMotionPaused && _sceneMotionSurfaces.Count > 0 &&
            (_sceneAnimationsEnabled || _sceneMotionSurfaces.Any(w => w.ObservesCanvasPointer || w.HasActiveCanvasScripts)) && !_sceneMotionFaulted;
        if (!running)
        {
            if (_sceneMotionTimer is not null)
            {
                var timer = _sceneMotionTimer; _sceneMotionTimer = null;
                try { timer.Stop(); timer.Tick -= SceneMotionTick; }
                finally { _sceneMotionTimerLease?.Dispose(); _sceneMotionTimerLease = null; }
            }
            _sceneMotionClock.Stop();
        }
        else if (_sceneMotionTimer is null)
        {
            _sceneMotionTimer = _sceneMotionDispatcher!.CreateTimer();
            _sceneMotionTimer.Interval = TimeSpan.FromMilliseconds(33);
            _sceneMotionTimer.IsRepeating = false; _sceneMotionTimer.Tick += SceneMotionTick;
            _sceneMotionTimerLease = _sceneMotionLedger!.Acquire("timer.scene-motion", "shared-decorative-frame");
            _sceneMotionClock.Start(); _sceneMotionTimer.Start();
        }
        if (_sceneMotionSurfaces.Count != 0 && !_sceneMotionFaulted) return;
        var settings = _sceneUiSettings; _sceneUiSettings = null;
        try { if (settings is not null) settings.AnimationsEnabledChanged -= SceneAnimationsEnabledChanged; }
        catch (Exception error) { log.Write("warning", "scene.motion_settings_cleanup_failed", new { error.Message }); }
        finally { _sceneMotionSettingsLease?.Dispose(); _sceneMotionSettingsLease = null; }
        _scenePointer = null; _scenePointerSequence = 0; _sceneMotionClock.Reset();
        // The ledger belongs to the application, not the poster that happened to register
        // first. Keep that dependency across zero-demand/reopen cycles; only leases expire.
        _sceneMotionDispatcher = null;
        if (_sceneMotionSurfaces.Count == 0) _sceneMotionFaulted = false;
    }

    private void StopFailedSceneMotion(string stage, Exception error)
    {
        // A native/XAML observation failure must not turn decoration into a crashing input
        // path or leave a resource-ledger lease attached to an unarmed timer.
        _sceneMotionFaulted = true; _sceneAnimationsEnabled = false;
        foreach (var window in _sceneMotionSurfaces.ToArray()) window.StopSceneMotionFrame();
        try { ArmSceneMotion(); }
        catch (Exception cleanup) { log.Write("warning", "scene.motion_cleanup_failed", new { cleanup.Message }); }
        log.Write("warning", "scene.motion_stopped", new { stage, error.Message });
    }

    private void SceneMotionTick(DispatcherQueueTimer sender, object args)
    {
        if (!ReferenceEquals(sender, _sceneMotionTimer) || _scenePointer is null) return;
        try
        {
            var owners = _sceneMotionSurfaces.Where(w => _sceneAnimationsEnabled || w.ObservesCanvasPointer || w.HasActiveCanvasScripts).ToArray();
            var sessions = owners.Select(window => window.ReadPointerTarget().Session).ToHashSet();
            var targets = ReadPointerTargets().Select(target => target with { Enabled = sessions.Contains(target.Session) }).ToArray();
            var sample = _scenePointer.Sample(++_scenePointerSequence, 1, targets, PointerProtection);
            foreach (var window in owners)
            {
                var observed = sample.Posters.FirstOrDefault(p => p.Session == window.ReadPointerTarget().Session);
                bool admitted = observed is not null && ScenePointerAdmissionPolicy.CanAnimate(
                    !_suspended && !SceneGlobalMotionPaused, observed.Pointer.Presence,
                    sample.SharedProtection | observed.Protection);
                var point = admitted && observed!.Pointer.Presence == AutoPosturePresence.Inside &&
                    (_sceneAnimationsEnabled || window.ObservesCanvasPointer) ? _scenePointer.LastPosition : null;
                window.PaintSceneMotion(_sceneMotionClock.Elapsed.TotalSeconds, point, admitted && _sceneAnimationsEnabled);
            }
            if (ReferenceEquals(sender, _sceneMotionTimer)) sender.Start();
        }
        catch (Exception error) { StopFailedSceneMotion("frame", error); }
    }

    private void RefreshSceneMotionSurfaces()
    {
        foreach (var window in _windows.Values.ToArray()) window.RefreshScene();
        ArmSceneMotion();
    }

    internal bool SceneMotionRunning => _sceneMotionTimer is not null;
    internal PosterPointerSampleDiagnostic? ReadScenePointerDiagnosticsForProbe() => _scenePointer?.LastDiagnostics;
    internal bool SceneReducedMotion => !_sceneAnimationsEnabled;
    internal bool SceneGlobalMotionPaused => !state.TryGetCurrent(out var projection) || projection!.Tray.DynamicEffectsPaused;
}
