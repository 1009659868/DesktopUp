using System.Diagnostics;
using DesktopUp.Application;
using DesktopUp.Windows;
using Microsoft.UI.Dispatching;

namespace DesktopUp;

public partial class App
{
    private AutoPostureScheduler? _autoPosture;
    private readonly AutoPostureEvidence _autoEvidence = new();
    private readonly Stopwatch _autoClock = Stopwatch.StartNew();
    private DispatcherQueueTimer? _autoTimer;
    private IDisposable? _autoTimerLease;
    private long _autoEnvironment = 1, _autoPointerSequence;
    private bool _autoReady;

    private void StartAutoPosture()
    {
        _autoPosture = new(() => _autoClock.ElapsedMilliseconds, ReadAutoPosture,
            action => _dispatcher?.TryEnqueue(() => { if (Volatile.Read(ref _exiting) == 0) action(); }) == true,
            ArmAutoPosture, (request, done) => _spatialRuntime!.StartAutomatic(request, done),
            request => _spatialRuntime?.CancelAutomatic(request),
            (name, data) => _log?.Write("info", "auto_posture." + name, data));
        if (_spatialRuntime is ITitleRuntimeReadback titleRuntime && _stateCommands is ITitleSpatialCommitter titleCommitter)
            titleCommitter.BindTitleRuntime(titleRuntime);
        _spatialRuntime!.ReadTitleEvidence = session =>
        {
            var pointer = ReadPassivePointer(++_autoPointerSequence, _autoEnvironment);
            var observed = pointer.Posters.FirstOrDefault(p => p.Session == session);
            return !_autoReady || observed is null ? null : new(_autoEnvironment, observed.Pointer);
        };
        _spatialRuntime.TitleEnvironmentEpoch = () => _autoEnvironment;
        _spatialRuntime.TitleRearm += OnTitleRearm;
        _spatialRuntime.TitleExpanded += OnTitleExpanded;
        _spatialRuntime.ActivityChanged += ObserveAutoPosture;
        _spatialRuntime.ManualIntent += OnAutoManualIntent;
        _controlCenter!.RemovalInteractionChanged += ObserveAutoPosture;
        _controlCenter.SettingsInteractionChanged += OnCenterSettingsInteractionChanged;
    }

    private void OnTitleRearm(TitleAutoRearmSuppression suppression)
    {
        if (suppression.EnvironmentEpoch == _autoEnvironment) _autoEvidence.CaptureTitleSuppression(suppression);
    }
    private void OnTitleExpanded(PosterInputSessionKey session) =>
        _autoEvidence.CaptureManualCollapse(session, new(AutoPosturePresence.Unknown), false);

    private void OnAutoManualIntent(string id, bool? collapse)
    {
        _autoEvidence.ManualIntent(id);
        ObserveAutoPosture();
        if (collapse is { } desired && _passivePointer?.Current.Posters.FirstOrDefault(p => p.Session.PosterId == id) is { } observed)
            _autoEvidence.CaptureManualCollapse(observed.Session, observed.Pointer, desired);
    }

    private void ObserveAutoPosture() => _autoPosture?.Observe();

    private void OnCenterSettingsInteractionChanged()
    {
        _spatialRuntime?.NotifyCenterContentInteractionChanged();
        ObserveAutoPosture();
    }

    private AutoPostureSample ReadAutoPosture()
    {
        if (_posterSessions?.GloballyHidden == true) return new([], false, AutoPostureProtection.Recovery);
        var pointer = ReadPassivePointer(++_autoPointerSequence, _autoEnvironment);
        var targets = _spatialRuntime?.ReadPointerTargets() ?? [];
        _autoEvidence.RetainSessions(targets.Select(t => t.Session));
        if (_stateCoordinator?.TryGetCurrent(out var current) != true || current is null)
            return new([], false, AutoPostureProtection.Recovery);
        var observations = new List<AutoPostureObservation>();
        foreach (var target in targets)
        {
            if (!current.PostersById.TryGetValue(target.Session.PosterId, out var committed)) continue;
            var observed = pointer.Posters.FirstOrDefault(p => p.Session == target.Session);
            observations.Add(new(target.Session, committed.EntityVersion, _autoEnvironment, committed.AutoPosture,
                committed.Poster.IsCollapsed, _autoReady && target.Ready, target.Visible,
                observed?.Pointer ?? new(AutoPosturePresence.Unknown),
                _autoEvidence.ManualVersion(target.Session.PosterId), _autoEvidence.SpaceVersion,
                observed?.Protection ?? AutoPostureProtection.Recovery,
                observed is null ? null : _autoEvidence.ManualCollapseExit(target.Session, observed.Pointer)));
        }
        return new(observations, _spatialRuntime?.IsSpatialBusy == true, pointer.SharedProtection);
    }

    private void ArmAutoPosture(int? milliseconds)
    {
        if (milliseconds is null || Volatile.Read(ref _exiting) != 0)
        {
            if (_autoTimer is null) return;
            _autoTimer.Stop(); _autoTimer.Tick -= AutoPostureTick; _autoTimer = null;
            _inputRouter.PointerActivity -= ObserveAutoPosture;
            DesktopMenuInteraction.ActivityChanged -= ObserveAutoPosture;
            _autoTimerLease?.Dispose(); _autoTimerLease = null;
            return;
        }
        if (_autoTimer is null)
        {
            _autoTimer = _dispatcher!.CreateTimer(); _autoTimer.IsRepeating = false;
            _autoTimer.Tick += AutoPostureTick;
            _inputRouter.PointerActivity += ObserveAutoPosture;
            DesktopMenuInteraction.ActivityChanged += ObserveAutoPosture;
            _autoTimerLease = _ledger!.Acquire("timer.auto-posture", "shared-observer-deadline");
        }
        _autoTimer.Stop(); _autoTimer.Interval = TimeSpan.FromMilliseconds(milliseconds.Value); _autoTimer.Start();
    }

    private void AutoPostureTick(DispatcherQueueTimer sender, object args)
    {
        if (ReferenceEquals(sender, _autoTimer)) ObserveAutoPosture();
    }

    private void SuspendAutoPosture()
    {
        _autoReady = false; _autoEnvironment++;
        _autoEvidence.ResetPointerBaseline();
        ObserveAutoPosture();
        ArmAutoPosture(null);
    }

    private void StopAutoPosture()
    {
        _autoPosture?.Dispose(); _autoPosture = null;
        if (_spatialRuntime is not null)
        {
            _spatialRuntime.ActivityChanged -= ObserveAutoPosture;
            _spatialRuntime.ManualIntent -= OnAutoManualIntent;
            _spatialRuntime.TitleRearm -= OnTitleRearm;
            _spatialRuntime.TitleExpanded -= OnTitleExpanded;
            _spatialRuntime.ReadTitleEvidence = null;
            _spatialRuntime.TitleEnvironmentEpoch = null;
        }
        if (_controlCenter is not null) _controlCenter.RemovalInteractionChanged -= ObserveAutoPosture;
        if (_controlCenter is not null) _controlCenter.SettingsInteractionChanged -= OnCenterSettingsInteractionChanged;
        _passivePointer = null;
    }
}
