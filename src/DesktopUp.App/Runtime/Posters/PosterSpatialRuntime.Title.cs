using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using System.Diagnostics;

namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    private TitleRun? _title;
    public Func<PosterInputSessionKey, TitlePointerEvidence?>? ReadTitleEvidence { get; set; }
    public Func<long>? TitleEnvironmentEpoch { get; set; }
    public event Action<TitleAutoRearmSuppression>? TitleRearm;
    public event Action<PosterInputSessionKey>? TitleExpanded;

    public void BeginTitle(PosterVisualWindow owner, TitleNativeInputEventArgs input)
    {
        if (owner.PosterId is { } id && HasCenterDraft(id)) return;
        if (_suspended || _title is not null || _owner is not null ||
            commands is not ITitleSpatialCommitter port || !owner.OwnsTitleCapture(input.Capture)) return;
        // A different source's group retains ownership. Never graft an unrelated animation
        // prefix onto this title's intent, and never fall through to legacy cancellation.
        if (_postures.Values.Any(r => r.Owner != owner)) return;
        try
        {
            // A presented endpoint is Expanded/Compact, even before the next rendering callback.
            if (_posture is { } endpoint && endpoint.AcceptedFrames == endpoint.Frames.Count)
                AdvancePosture(endpoint);
            if (!TrySnapshot(owner, out var actual, out var projection) ||
                ReadTitleEvidence?.Invoke(input.Capture.Press.Session) is not { } evidence) return;
            var paused = _posture;
            var initial = paused?.Initial ?? actual!;
            var target = projection!.PostersById[owner.PosterId!];
            if (!target.AutoPosture.Enabled || !owner.IsTitleSessionCurrent(input.Capture.Press.Session)) return;
            var reads = new StateReadSet(projection.Posters.ToDictionary(p => p.Poster.PosterId, p => p.EntityVersion),
                CatalogVersion: projection.CatalogVersion).Freeze();
            if (paused is not null && !paused.ReadsCurrent(projection)) return;
            // Every initial rectangle was resolved from current intent by the existing effective
            // layout path. The original posture lease retains this same pre-animation snapshot.
            if (initial.Posters.Any(p => _effective.Find(projection.PostersById[p.PosterId]) is not { } cached ||
                !cached.Matches(projection.PostersById[p.PosterId].Poster, initial.Environment,
                    projection.PostersById[p.PosterId].Presentation.Metrics.Version) || cached.Bounds != p.Bounds))
                throw new InvalidOperationException("Initial effective-layout provenance is unavailable.");
            var stage = paused is null
                ? target.Poster.IsCollapsed ? TitlePresentedPosture.Compact : TitlePresentedPosture.Expanded
                : paused.Command.DesiredState.IsCollapsed ? TitlePresentedPosture.Collapsing : TitlePresentedPosture.Expanding;
            var press = new TitlePress("title-" + Guid.NewGuid().ToString("N"), input.Sequence,
                evidence.EnvironmentEpoch, input.Capture, input.Position, stage, evidence.Pointer, IsLocked: target.Poster.IsLocked);
            var start = new TitleSpatialStart(initial, press, animationOptions, paused is null ? null :
                new(paused.Command.RequestId, paused.Command.DesiredState.IsCollapsed, paused.AcceptedFrames));
            var session = new TitleSpatialSession(start, target.Presentation.Metrics,
                projection.SpatialContinuities.FirstOrDefault(c => c.Environment.WorkspaceId == initial.Environment.WorkspaceId));
            if (!session.Presented.Posters.All(p => actual!.Posters.Single(q => q.PosterId == p.PosterId).Bounds == p.Bounds))
                throw new InvalidOperationException("Accepted prefix does not match native readback.");
            var participants = initial.Posters.Select(p => _windows[p.PosterId]).ToArray();
            var timer = owner.SpatialDispatcher.CreateTimer();
            timer.Interval = TimeSpan.FromMilliseconds(100); timer.IsRepeating = true;
            var run = new TitleRun(owner, session, reads, projection.Revision, participants, timer);
            // Dispatcher-atomic owner transfer: invalidate old callbacks before notifying observers.
            if (paused is not null)
            {
                paused.Timer.Stop(); paused.Timer.Tick -= WatchPosture;
                CompositionTarget.Rendering -= paused.RenderingHandler;
                paused.Clock.Stop(); _postures.Remove(paused.Command.RequestId);
            }
            _title = run;
            if (paused is not null)
            {
                _automaticCancellation = _automatic is null ? null : AutoPostureOutcome.Cancelled;
                paused.Completed(Failure(paused.Command, "Title capture took ownership of the accepted group frame."));
            }
            ManualIntent?.Invoke(owner.PosterId!, null);
            run.Lease = port.OpenTitle(start, reads) ??
                throw new InvalidOperationException("Live title start attestation was rejected.");
            run.Clock.Start();
            foreach (var window in participants)
            {
                var physical = session.Presented.Posters.Single(p => p.PosterId == window.PosterId);
                int capacity = window == owner
                    ? Math.Max(physical.Bounds.Height, Math.Min(initial.Environment.WorkArea.Height, physical.ExpandedHeight))
                    : physical.Bounds.Height;
                window.BeginTitlePreview(press.OperationId, Pixels(physical.Bounds with { Height = capacity }), window == owner);
            }
            var surfaces = participants.Select(p => p.PrepareSpatialFrame(Pixels(run.Driver.Physical[p.PosterId!]))).ToArray();
            if (!TitleCurrent(run) || surfaces.Any(p => p is null) ||
                !owner.CommitSpatialFrames(surfaces.Select(p => p!).ToArray(), DesktopFramePhase.SurfacePreparation))
                throw new InvalidOperationException("Title R4 surface preparation failed.");
            RequestTitleFrame(run);
            timer.Tick += WatchTitle; timer.Start();
            log.Write("info", "spatial.title_started", new { press.OperationId, owner.PosterId, stage,
                acceptedPrefix = paused?.AcceptedFrames, press.Capture.Id, press.Capture.Press.Frame,
                initial.Environment, readCount = reads.Posters.Count });
            ActivityChanged?.Invoke();
        }
        catch (Exception e)
        {
            log.Write("warning", "spatial.title_rejected", new { owner.PosterId, e.Message });
            CancelTitle("start-or-preparation-failed");
        }
    }

    public void TitleInput(PosterVisualWindow owner, TitleNativeInputEventArgs input)
    {
        if (_title is not { } run || run.Owner != owner || run.Finishing ||
            input.Capture != run.Session.Start.Press.Capture) return;
        try
        {
            bool interrupted = input.Kind == TitleNativeInputKind.Cancel;
            if (interrupted)
            {
                run.NativeEnd = input.EndReason;
                if (!TitleNativeEndPolicy.AllowsSafeStop(input.EndReason))
                { CancelTitle("native-" + input.EndReason); return; }
            }
            var kind = interrupted ? TitlePointerEventKind.Interrupt :
                input.Kind == TitleNativeInputKind.Release ? TitlePointerEventKind.Release : TitlePointerEventKind.Move;
            var evidence = kind is TitlePointerEventKind.Release or TitlePointerEventKind.Interrupt
                ? ReadTitleEvidence?.Invoke(input.Capture.Press.Session) : null;
            if (interrupted && evidence is null) { CancelTitle("interrupt-evidence-unavailable"); return; }
            if (evidence is not null && evidence.EnvironmentEpoch != run.Session.Intent.Token.EnvironmentEpoch)
            { CancelTitle("pointer-environment-changed"); return; }
            bool queued = run.Driver.Queue(new(run.Session.Intent.Token, input.Sequence, kind, input.Position,
                input.CanInvoke, evidence?.Pointer));
            if (queued) RequestTitleFrame(run);
            if (interrupted && queued)
            {
                // Native capture has ended. All other session/generation/whole-group/read-set
                // checks stay live, including the independent recheck inside SQLite commit.
                if (!TitleCurrent(run)) { CancelTitle("interrupt-runtime-stale"); return; }
                log.Write("info", "spatial.title_interrupt_requested", new { run.NativeEnd,
                    input.Sequence, run.Driver.AcceptedFrames, run.Driver.HasPendingPresentation });
            }
        }
        catch (Exception e)
        {
            log.Write("error", "spatial.title_input_failed", new { e.Message });
            CancelTitle("input-failed");
        }
    }

    private static bool TitleReadsCurrent(StateReadSet reads, DesktopUpProjection p) =>
        reads.CatalogVersion == p.CatalogVersion && reads.Posters.Count == p.Posters.Count &&
        reads.Posters.All(pair => p.PostersById.TryGetValue(pair.Key, out var entry) && entry.EntityVersion == pair.Value);

    // Called by the bound coordinator at lease issuance AND inside the existing SQLite transaction.
    public bool ValidateTitleStart(TitleSpatialStart start, StateReadSet reads) =>
        _title is { Finishing: false } run && run.Session.Start == start &&
        run.Reads.CatalogVersion == reads.CatalogVersion && run.Reads.Posters.Count == reads.Posters.Count &&
        run.Reads.Posters.All(p => reads.Posters.TryGetValue(p.Key, out var value) && p.Value == value) && TitleCurrent(run);

    private bool TitleCurrent(TitleRun run)
    {
        if (!ReferenceEquals(_title, run) || _suspended || run.Finishing ||
            !run.Owner.IsTitleSessionCurrent(run.Session.Intent.Token.Session) ||
            (!run.Driver.InputEnded && !run.Owner.OwnsTitleCapture(run.Session.Start.Press.Capture)) ||
            !TrySnapshot(run.Owner, out var scene, out var current) || !TitleReadsCurrent(run.Reads, current!) ||
            !run.Session.ValidateCurrent(scene!.Environment, run.Session.Intent.Token with
                { EnvironmentEpoch = TitleEnvironmentEpoch?.Invoke() ?? -1 })) return false;
        // Compare current native readback to the last successful batch, not just the last model
        // endpoint: a release proposal can contain several still-uncommitted presentation frames.
        return scene.Posters.Count == run.Driver.Physical.Count &&
            scene.Posters.All(p => run.Driver.Physical.TryGetValue(p.PosterId, out var bounds) && bounds == p.Bounds);
    }

    private void RequestTitleFrame(TitleRun run) => RequestDragFrame(run.Owner, () =>
    {
        if (!ReferenceEquals(_title, run) || run.Finishing) return;
        try
        {
            AdvanceTitle(run);
            if (ReferenceEquals(_title, run) && !run.Finishing &&
                (!run.Ready || run.Driver.HasPendingWork)) RequestTitleFrame(run);
        }
        catch (Exception e)
        {
            log.Write("error", "spatial.title_render_failed", new { e.Message });
            CancelTitle("render-exception");
        }
    });

    private void AdvanceTitle(TitleRun run)
    {
        if (!TitleCurrent(run)) { CancelTitle("environment-capture-or-read-set-stale"); return; }
        if (!run.Ready)
        {
            run.ReadyCallbacks = run.Participants.All(p => p.VerifyTransitionSurfaceReady()) ? run.ReadyCallbacks + 1 : 0;
            if (run.ReadyCallbacks < 2) return;
            run.Ready = true; run.LastProgressMs = run.Clock.Elapsed.TotalMilliseconds;
        }
        double now = run.Clock.Elapsed.TotalMilliseconds;
        int before = run.Driver.AcceptedFrames;
        run.Driver.Tick(() => TitleCurrent(run), frame =>
        {
            var prepared = frame.Changes.Select(p => _windows[p.PosterId].PrepareSpatialFrame(Pixels(p.To))).ToArray();
            return prepared.All(p => p is not null) &&
                run.Owner.CommitSpatialFrames(prepared.Select(p => p!).ToArray(), DesktopFramePhase.Animation);
        });
        if (before != run.Driver.AcceptedFrames) run.LastProgressMs = now;
        if (run.Session.Cancelled) { CancelTitle(run.Driver.FailureReason ?? "model-rejected"); return; }
        if (run.Session.NeedsCheckpoint && !run.Driver.HasPendingPresentation &&
            !((ITitleSpatialCommitter)commands).CheckpointTitle(run.Session, run.Lease!))
        { CancelTitle("checkpoint-verification-failed"); return; }
        if (!run.Session.Complete) return;
        if (!TitleCurrent(run)) { CancelTitle("precommit-generation-stale"); return; }
        var command = new TitleSpatialCommand(run.Session.Intent.Token.OperationId, StateCommandSource.Poster,
            run.Revision, run.Reads, run.Session.Freeze());
        var result = ((ITitleSpatialCommitter)commands).ExecuteTitle(command, run.Lease!);
        if (ReferenceEquals(_title, run)) FinishTitle(run, result);
    }

    private void WatchTitle(DispatcherQueueTimer timer, object args)
    {
        if (_title is not { Finishing: false } run || !ReferenceEquals(run.Timer, timer)) return;
        if (!TitleCurrent(run)) { CancelTitle("watchdog-stale"); return; }
        // A stationary held title has no timer-driven behavior. Only an unfinished presentation
        // or ended input transaction is subject to the stalled-rendering safety deadline.
        if ((!run.Ready || run.Driver.InputEnded || run.Driver.HasPendingPresentation) &&
            run.Clock.Elapsed.TotalMilliseconds - run.LastProgressMs > 5000) CancelTitle("rendering-stalled");
    }

    private void CancelTitle(string reason)
    {
        if (_title is not { Finishing: false } run) return;
        run.Driver.Cancel(reason);
        state.TryGetCurrent(out var projection);
        FinishTitle(run, new(StateCommandStatus.Failed, run.Session.Intent.Token.OperationId, run.Revision,
            projection?.Revision ?? run.Revision, projection, "Title cancelled: " + reason));
    }

    private void FinishTitle(TitleRun run, StateCommandResult result)
    {
        run.Finishing = true;
        run.Timer.Stop(); run.Timer.Tick -= WatchTitle;
        CancelDragFrame(run.Owner);
        run.Clock.Stop(); run.Lease?.Dispose();
        run.Owner.CancelTitleCapture();
        if (result.Succeeded && result.Projection is { } projection)
        {
            foreach (var physical in run.Session.Presented.Posters)
                if (projection.PostersById.TryGetValue(physical.PosterId, out var target))
                    _effective.Set(target, physical.Bounds, run.Session.Start.Initial.Environment);
            if (run.Session.Intent.Rearm is { } suppression) TitleRearm?.Invoke(suppression);
            else if (!run.Session.Presented.Posters.Single(p => p.PosterId == run.Owner.PosterId).IsCollapsed)
                TitleExpanded?.Invoke(run.Session.Intent.Token.Session);
        }
        bool restored = RestorePresentedGroup(run.Owner, run.Participants, result.RequestId);
        _title = null;
        EffectiveLayoutChanged?.Invoke();
        if (result.Succeeded && restored && run.Driver.WasFastExpand && run.Session.Start.Press.Posture == TitlePresentedPosture.Compact &&
            !run.Session.HasUserMoved && run.Session.Intent.Rearm is null &&
            run.Session.Presented.Posters.Single(p => p.PosterId == run.Owner.PosterId).IsCollapsed)
            run.Owner.ShowInsufficientSpaceFeedback();
        if (!restored)
        {
            _suspended = true;
            RecoveryRequired?.Invoke();
        }
        log.Write(result.Succeeded && restored ? "info" : "warning", "spatial.title_finished", new
        {
            result.RequestId, result.Status, result.Error, restored, run.Driver.AcceptedFrames,
            run.NativeEnd, run.Driver.Interrupted, run.Driver.FailureReason,
            samples = run.Session.SampleCount, run.Session.SealedSegments, run.Session.HasUserMoved, elapsedMs = run.Clock.Elapsed.TotalMilliseconds
        });
        ActivityChanged?.Invoke();
    }

    private sealed class TitleRun(PosterVisualWindow owner, TitleSpatialSession session, StateReadSet reads,
        long revision, IReadOnlyList<PosterVisualWindow> participants, DispatcherQueueTimer timer)
    {
        public PosterVisualWindow Owner { get; } = owner;
        public TitleSpatialSession Session { get; } = session;
        public TitlePresentationDriver Driver { get; } = new(session);
        public StateReadSet Reads { get; } = reads;
        public long Revision { get; } = revision;
        public IReadOnlyList<PosterVisualWindow> Participants { get; } = participants;
        public DispatcherQueueTimer Timer { get; } = timer;
        public TitleRuntimeLease? Lease { get; set; }
        public Stopwatch Clock { get; } = new();
        public bool Ready { get; set; }
        public bool Finishing { get; set; }
        public TitleNativeEndReason NativeEnd { get; set; }
        public int ReadyCallbacks { get; set; }
        public double LastProgressMs { get; set; }
    }
}
