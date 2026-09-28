using System.Diagnostics;
using DesktopUp.Application;
using DesktopUp.Windows;
using Microsoft.UI.Dispatching;
using Windows.UI.ViewManagement;

namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    private readonly HashSet<PosterVisualWindow> _lampMotionSurfaces = [];
    private readonly Stopwatch _lampMotionClock = new();
    private DispatcherQueueTimer? _lampMotionTimer;
    private DispatcherQueue? _lampMotionDispatcher;
    private IResourceLedger? _lampMotionLedger;
    private IDisposable? _lampMotionTimerLease, _lampMotionSettingsLease;
    private PassivePosterPointerObserver? _lampPointer;
    private UISettings? _lampUiSettings;
    private long _lampPointerSequence;
    private bool _lampAnimationsEnabled, _lampMotionFaulted;

    internal void ConfigureLampMotionLedger(IResourceLedger resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        if (_lampMotionLedger is not null && !ReferenceEquals(_lampMotionLedger, resources))
            throw new InvalidOperationException("The shared motion ledger is already configured.");
        _lampMotionLedger = resources;
    }

    // All scenes share one read-only sample and one bounded frame clock. Auto-posture keeps
    // its own deadlines/evidence; decorative motion never publishes input or issues commands.
    internal void SetLampMotionDemand(PosterVisualWindow window, bool needed,
        PosterInputRouter router)
    {
        if (!needed) _lampMotionSurfaces.Remove(window);
        else
        {
            _lampMotionSurfaces.Add(window);
            _lampMotionDispatcher = window.SpatialDispatcher;
            _lampPointer ??= new(router);
            if (_lampUiSettings is null && !_lampMotionFaulted)
            {
                UISettings? settings = null;
                bool subscribed = false;
                try
                {
                    if (_lampMotionLedger is null) throw new InvalidOperationException("The shared motion ledger is not configured.");
                    settings = new UISettings();
                    _lampAnimationsEnabled = settings.AnimationsEnabled;
                    settings.AnimationsEnabledChanged += LampAnimationsEnabledChanged;
                    subscribed = true;
                    _lampMotionSettingsLease = _lampMotionLedger.Acquire("subscription.lamp-motion-settings", "shared-reduced-motion-observer");
                    _lampUiSettings = settings;
                }
                catch (Exception error)
                {
                    if (subscribed)
                        try { settings!.AnimationsEnabledChanged -= LampAnimationsEnabledChanged; }
                        catch (Exception cleanup) { log.Write("warning", "lamp.motion_settings_cleanup_failed", new { cleanup.Message }); }
                    _lampAnimationsEnabled = false;
                    _lampMotionFaulted = true;
                    log.Write("warning", "lamp.motion_settings_unavailable", new { error.Message });
                }
            }
        }
        try { ArmLampMotion(); }
        catch (Exception error) { StopFailedLampMotion("arming", error); }
    }

    private void LampAnimationsEnabledChanged(UISettings sender, UISettingsAnimationsEnabledChangedEventArgs args) =>
        _lampMotionDispatcher?.TryEnqueue(() =>
        {
            if (!ReferenceEquals(sender, _lampUiSettings)) return;
            try
            {
                _lampAnimationsEnabled = sender.AnimationsEnabled;
                if (!_lampAnimationsEnabled)
                    foreach (var window in _lampMotionSurfaces.ToArray()) window.PaintLampMotion(0, null, false);
                ArmLampMotion();
            }
            catch (Exception error) { StopFailedLampMotion("accessibility-change", error); }
        });

    private void ArmLampMotion()
    {
        bool running = !_suspended && !LampGlobalMotionPaused && _lampMotionSurfaces.Count > 0 &&
            _lampAnimationsEnabled && !_lampMotionFaulted;
        if (!running)
        {
            if (_lampMotionTimer is not null)
            {
                var timer = _lampMotionTimer; _lampMotionTimer = null;
                try { timer.Stop(); timer.Tick -= LampMotionTick; }
                finally { _lampMotionTimerLease?.Dispose(); _lampMotionTimerLease = null; }
            }
            _lampMotionClock.Stop();
        }
        else if (_lampMotionTimer is null)
        {
            _lampMotionTimer = _lampMotionDispatcher!.CreateTimer();
            _lampMotionTimer.Interval = TimeSpan.FromMilliseconds(33);
            _lampMotionTimer.IsRepeating = false; _lampMotionTimer.Tick += LampMotionTick;
            _lampMotionTimerLease = _lampMotionLedger!.Acquire("timer.lamp-motion", "shared-decorative-frame");
            _lampMotionClock.Start(); _lampMotionTimer.Start();
        }
        if (_lampMotionSurfaces.Count != 0 && !_lampMotionFaulted) return;
        var settings = _lampUiSettings; _lampUiSettings = null;
        try { if (settings is not null) settings.AnimationsEnabledChanged -= LampAnimationsEnabledChanged; }
        catch (Exception error) { log.Write("warning", "lamp.motion_settings_cleanup_failed", new { error.Message }); }
        finally { _lampMotionSettingsLease?.Dispose(); _lampMotionSettingsLease = null; }
        _lampPointer = null; _lampPointerSequence = 0; _lampMotionClock.Reset();
        // The ledger belongs to the application, not the poster that happened to register
        // first. Keep that dependency across zero-demand/reopen cycles; only leases expire.
        _lampMotionDispatcher = null;
        if (_lampMotionSurfaces.Count == 0) _lampMotionFaulted = false;
    }

    private void StopFailedLampMotion(string stage, Exception error)
    {
        // A native/XAML observation failure must not turn decoration into a crashing input
        // path or leave a resource-ledger lease attached to an unarmed timer.
        _lampMotionFaulted = true; _lampAnimationsEnabled = false;
        foreach (var window in _lampMotionSurfaces) window.StopLampMotionFrame();
        try { ArmLampMotion(); }
        catch (Exception cleanup) { log.Write("warning", "lamp.motion_cleanup_failed", new { cleanup.Message }); }
        log.Write("warning", "lamp.motion_stopped", new { stage, error.Message });
    }

    private void LampMotionTick(DispatcherQueueTimer sender, object args)
    {
        if (!ReferenceEquals(sender, _lampMotionTimer) || _lampPointer is null) return;
        try
        {
            var owners = _lampMotionSurfaces.ToArray();
            var sessions = owners.Select(window => window.ReadPointerTarget().Session).ToHashSet();
            var targets = ReadPointerTargets().Select(target => target with { Enabled = sessions.Contains(target.Session) }).ToArray();
            var sample = _lampPointer.Sample(++_lampPointerSequence, 1, targets, PointerProtection);
            foreach (var window in owners)
            {
                var observed = sample.Posters.FirstOrDefault(p => p.Session == window.ReadPointerTarget().Session);
                bool animate = !_suspended && !LampGlobalMotionPaused && _lampAnimationsEnabled && observed is not null &&
                    (sample.SharedProtection | observed.Protection) == AutoPostureProtection.None;
                var point = animate && observed!.Pointer.Presence == AutoPosturePresence.Inside ? _lampPointer.LastPosition : null;
                window.PaintLampMotion(_lampMotionClock.Elapsed.TotalSeconds, point, animate);
            }
            if (ReferenceEquals(sender, _lampMotionTimer)) sender.Start();
        }
        catch (Exception error) { StopFailedLampMotion("frame", error); }
    }

    private void RefreshLampMotionSurfaces()
    {
        foreach (var window in _windows.Values.ToArray()) window.RefreshLampScene();
        ArmLampMotion();
    }

    internal bool LampMotionRunning => _lampMotionTimer is not null;
    internal PosterPointerSampleDiagnostic? ReadLampPointerDiagnosticsForProbe() => _lampPointer?.LastDiagnostics;
    internal bool LampReducedMotion => !_lampAnimationsEnabled;
    internal bool LampGlobalMotionPaused => !state.TryGetCurrent(out var projection) || projection!.Tray.DynamicEffectsPaused;
}
