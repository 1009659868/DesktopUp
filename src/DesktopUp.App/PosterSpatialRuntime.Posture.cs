using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using System.Diagnostics;

namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    private PostureRun? _posture;
    // Off for every normal launch; enabled only by the existing isolated spatial probe.
    private bool _measurePostureFrames;
    public event Action? RecoveryRequired;

    public void ToggleCollapsed(PosterVisualWindow window)
    {
        if (!state.TryGetCurrent(out var current) || current?.PostersById.GetValueOrDefault(window.PosterId ?? "") is not { } target) return;
        SetCollapsed(new("posture-" + Guid.NewGuid().ToString("N"), StateCommandSource.Poster, PosterStateCommandKind.SetCollapsed,
            current.Revision, target.Poster.PosterId, target.Poster with { IsCollapsed = !target.Poster.IsCollapsed })
            { ReadSet = StateReadSet.ForPoster(target.Poster.PosterId, target.EntityVersion) }, result =>
                log.Write(result.Succeeded ? "info" : "warning", "spatial.posture_result", new { result.RequestId, result.Status, result.Error }));
    }

    public void SetCollapsed(PosterStateCommand request, Action<StateCommandResult> completed)
    {
        if (HasCenterDraft(request.PosterId))
        { completed(Failure(request, "控制中心存在未完成修改，请先完成或取消。")); return; }
        if (request.Source != StateCommandSource.System) BeforeManualIntent(request.PosterId, request.DesiredState.IsCollapsed);
        if (_title is not null || _posture is not null || _owner is not null || !_windows.TryGetValue(request.PosterId, out var owner) ||
            !TrySnapshot(owner, out var scene, out var projection))
        { completed(Failure(request, "空间环境尚未就绪或另一个拖缩/收展正在进行。")); return; }
        var target = projection!.PostersById[request.PosterId];
        if (request.ReadSet?.Posters.GetValueOrDefault(request.PosterId) != target.EntityVersion)
        { completed(Failure(request, "海报状态已变化，请重试。") with { Status = StateCommandStatus.Conflict }); return; }
        if (request.DesiredState != (target.Poster with { IsCollapsed = request.DesiredState.IsCollapsed }))
        { completed(Failure(request, "收展命令不能同时修改其他参数。")); return; }
        if (request.DesiredState.IsCollapsed == target.Poster.IsCollapsed)
        { completed(CommitPosture(request)); return; }
        int compactHeight = Math.Max(1, (int)Math.Round(PosterFrameGeometryPolicy.CollapsedHeightDip * scene!.Environment.DpiY / 96d));
        var source = scene.Posters.Single(p => p.PosterId == request.PosterId);
        var continuous = SpatialContinuitySolver.Plan(scene, request.PosterId, request.DesiredState.IsCollapsed,
            compactHeight, request.RequestId, target.Presentation.Metrics, projection.SpatialContinuities.FirstOrDefault(s =>
                s.Environment.WorkspaceId == scene.Environment.WorkspaceId));
        var expansion = !request.DesiredState.IsCollapsed ? continuous : null;
        if (expansion?.Outcome == SpatialExpansionOutcome.InsufficientSpace)
        {
            if (request.Source != StateCommandSource.System) owner.ShowInsufficientSpaceFeedback();
            log.Write("info", "spatial.expansion_insufficient_space", new { request.PosterId, expansion.MinimumHeight, expansion.EffectiveHeight });
            completed(Failure(request, "空间不足：无法容纳完整标题、一行内容及页脚；海报和邻居保持原位。")
                with { Status = StateCommandStatus.InsufficientSpace });
            return;
        }
        var group = continuous.Group;
        var plan = group.Plan;
        if (!plan.Succeeded)
        { completed(Failure(request, "收展未执行（不是普通空间不足）：" + string.Join(", ", plan.Issues.Select(i => i.Reason)))); return; }
        SpatialAnimationSchedule schedule;
        try { schedule = SpatialAnimationSchedule.Create(scene, group.Frames, animationOptions); }
        catch (ArgumentException exception)
        { completed(Failure(request, "动画轨迹未提交：" + exception.Message)); return; }
        var final = plan.Changes.FirstOrDefault(p => p.PosterId == request.PosterId)?.After ?? source.Bounds;
        var desired = SpatialGestureCommitPolicy.Capture(target.Poster, final, scene.Environment, target.Poster.ExpandedHeightDip)
            with { IsCollapsed = request.DesiredState.IsCollapsed };
        var reads = new StateReadSet(projection.Posters.ToDictionary(p => p.Poster.PosterId, p => p.EntityVersion),
            CatalogVersion: projection.CatalogVersion).Freeze();
        var command = request with { ExpectedRevision = projection.Revision, DesiredState = desired, ReadSet = reads,
            SpatialPosture = new(scene, desired.IsCollapsed, compactHeight, null) { AllowPush = true, UseContinuity = true,
                ContentMetrics = target.Presentation.Metrics } };
        var timer = owner.SpatialDispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(100); // Cancellation/stall watchdog, never an animation clock.
        timer.IsRepeating = true;
        var participants = plan.Changes.Select(p => _windows[p.PosterId]).ToArray();
        var run = new PostureRun(owner, command, scene, schedule, animationOptions, participants, timer, completed) { Changes = plan.Changes };
        _posture = run;
        timer.Tick += WatchPosture;
        run.RenderingHandler = (_, _) =>
        {
            try { AdvancePosture(run); }
            catch (Exception exception)
            {
                log.Write("error", "spatial.animation_failed", new { run.Command.RequestId, exception.Message });
                CancelPosture("animation-exception");
            }
        };
        try
        {
            foreach (var window in participants)
            {
                var change = plan.Changes.Single(c => c.PosterId == window.PosterId);
                var before = scene.Posters.Single(p => p.PosterId == window.PosterId).Bounds;
                var expanded = change.After.Height > before.Height ? change.After : before;
                window.BeginPosturePreview(command.RequestId, Pixels(expanded), window == owner);
            }
            var surfaces = participants.Select(p => p.PrepareSpatialFrame(Pixels(scene.Posters.Single(s => s.PosterId == p.PosterId).Bounds))).ToArray();
            if (surfaces.Any(p => p is null) || !owner.CommitSpatialFrames(surfaces.Select(p => p!).ToArray(), DesktopFramePhase.SurfacePreparation))
                throw new InvalidOperationException("Clipped native surface preparation failed.");
        }
        catch (Exception exception)
        {
            log.Write("error", "spatial.transition_prepare_failed", new { command.RequestId, exception.Message });
            CancelPosture("content-preparation-failed");
            return;
        }
        log.Write("info", "spatial.posture_started", new { command.RequestId, command.PosterId,
            participants = participants.Select(p => p.PosterId), frames = run.Frames.Count, group.ConsumeOperationId,
            keyframes = group.Frames.Count, animation = animationOptions, nominalDurationMs = schedule.NominalDurationMs,
            collapsed = desired.IsCollapsed,
            model = "continuity-v1", outcome = expansion?.Outcome.ToString(), preferredHeight = source.ExpandedHeight,
            effectiveHeight = final.Height, minimumHeight = expansion?.MinimumHeight,
            initialBounds = source.Bounds, finalBounds = final,
            downGrowth = expansion is null ? (long?)null : final.Bottom - source.Bounds.Bottom,
            upGrowth = expansion is null ? (int?)null : source.Bounds.Y - final.Y,
            simultaneousFrames = run.Frames.Count(f => f.Changes.Any(c => c.PosterId == command.PosterId) &&
                f.Changes.Any(c => c.PosterId != command.PosterId)),
            returnOnlyFrames = run.Frames.Count(f => f.Changes.All(c => c.PosterId != command.PosterId)) });
        run.Clock.Start();
        CompositionTarget.Rendering += run.RenderingHandler;
        timer.Start();
    }

    private void WatchPosture(DispatcherQueueTimer timer, object args)
    {
        if (_posture is not { } run || !ReferenceEquals(run.Timer, timer)) return;
        if (run.Clock.Elapsed.TotalMilliseconds > Math.Min(120000, 5000 + run.Schedule.NominalDurationMs + run.Frames.Count * 100d))
        { CancelPosture("rendering-stalled"); return; }
        if (!TrySnapshot(run.Owner, out var scene, out var current) || scene!.Environment != run.Initial.Environment || !run.ReadsCurrent(current!))
        { CancelPosture("environment-or-read-set-stale"); return; }
    }

    private void AdvancePosture(PostureRun run)
    {
        if (!ReferenceEquals(_posture, run)) return;
        if (!TrySnapshot(run.Owner, out var scene, out var current) || scene!.Environment != run.Initial.Environment || !run.ReadsCurrent(current!))
        { CancelPosture("environment-or-read-set-stale"); return; }
        // Only the explicit isolated backdrop probe installs this observer. Keep the native
        // frame stable while its own-HWND capture completes; the existing watchdog remains on.
        if (run.BackdropObservation is { } observation)
        {
            if (!observation.IsCompleted) return;
            run.BackdropObservation = null;
            observation.GetAwaiter().GetResult();
        }
        if (!run.Cursor.Complete)
        {
            if (run.AnimationOriginMs is null)
            {
                // Let the real HWND/WinUI size update finish behind the unchanged visible HRGN.
                // Consecutive callbacks are a preparation gate, not a claim of GPU presentation.
                run.ReadyCallbacks = run.Participants.All(p => p.VerifyTransitionSurfaceReady()) ? run.ReadyCallbacks + 1 : 0;
                if (run.ReadyCallbacks < 2) return;
                run.AnimationOriginMs = run.Clock.Elapsed.TotalMilliseconds;
                log.Write("info", "spatial.surfaces_ready", new { run.Command.RequestId, run.ReadyCallbacks, preparationMs = run.AnimationOriginMs });
                return;
            }
            if (!run.Cursor.TryTake(run.Clock.Elapsed.TotalMilliseconds - run.AnimationOriginMs.Value, out var frame)) return;
            long probeStart = _measurePostureFrames ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            var prepared = frame!.Changes.Select(p => _windows[p.PosterId].PrepareSpatialFrame(Pixels(p.To))).ToArray();
            bool applied = prepared.All(p => p is not null) && run.Owner.CommitSpatialFrames(prepared.Select(p => p!).ToArray(), DesktopFramePhase.Animation);
            // A live window backdrop is a separate check from HWND/XAML geometry. This does
            // not claim to measure presented pixels; it prevents a silent return to XAML-only fill.
            bool? backdropsReady = _measurePostureFrames
                ? prepared.All(p => p is not null && p.Owner.VerifyWindowBackdrop()) : null;
            if (_measurePostureFrames) log.Write("info", "spatial.probe_frame", new { run.Command.RequestId, index = run.Cursor.Index,
                applied, backdropsReady, layoutsStable = prepared.All(p => p is not null && p.Owner.VerifyTransitionLayout(true)),
                surfacesCoverVisible = prepared.All(p => p is not null && p.Owner.VerifyTransitionSurfaceReady()),
                qpc = System.Diagnostics.Stopwatch.GetTimestamp(), frequency = System.Diagnostics.Stopwatch.Frequency,
                maximumEdgeDelta = frame.Changes.Select(c => SpatialAnimationSchedule.EdgeDistance(c.From, c.To)).DefaultIfEmpty().Max(),
                elapsedMs = run.Clock.Elapsed.TotalMilliseconds,
                prepareAndCommitMs = System.Diagnostics.Stopwatch.GetElapsedTime(probeStart).TotalMilliseconds });
            if (!applied)
                CancelPosture("native-batch-failed");
            else
            {
                run.AcceptedFrames++;
                if (BackdropFrameObserverForProbe is { } observer &&
                    prepared.FirstOrDefault(p => p!.Owner == run.Owner) is { } source)
                {
                    var initial = run.Initial.Posters.Single(p => p.PosterId == run.Owner.PosterId).Bounds;
                    var final = run.Changes.Single(p => p.PosterId == run.Owner.PosterId).After;
                    run.BackdropObservation = observer(new(run.Command.RequestId, run.Owner.PosterId!,
                        run.Command.DesiredState.IsCollapsed, run.AcceptedFrames, run.Frames.Count,
                        Pixels(initial), Pixels(final), source.Bounds));
                }
            }
            return;
        }
        run.Timer.Stop();
        StateCommandResult result = CommitPosture(run.Command);
        if (_posture != run) return;
        FinishPosture(run, result);
    }

    private void CancelPosture(string reason, StateCommandStatus status = StateCommandStatus.Failed)
    {
        if (_posture is not { } run) return;
        log.Write("warning", "spatial.posture_cancelled", new { run.Command.RequestId, reason });
        FinishPosture(run, Failure(run.Command, "收展已取消并恢复权威布局：" + reason) with { Status = status });
    }

    private void FinishPosture(PostureRun run, StateCommandResult result)
    {
        run.Timer.Stop(); run.Timer.Tick -= WatchPosture;
        CompositionTarget.Rendering -= run.RenderingHandler;
        run.Clock.Stop();
        if (result.Succeeded && result.Projection is { } projection)
            foreach (var change in run.Changes)
                if (projection.PostersById.TryGetValue(change.PosterId, out var target))
                    _effective.Set(target, change.After, run.Initial.Environment);
        bool restored = RestorePresentedGroup(run.Owner, run.Participants, run.Command.RequestId);
        _posture = null;
        EffectiveLayoutChanged?.Invoke();
        if (!restored)
        {
            _suspended = true;
            log.Write("error", "spatial.physical_recovery_required", new { run.Command.RequestId, result.CurrentRevision });
            result = result with { Status = StateCommandStatus.Failed, Error = "物理布局应用失败，正在按 committed revision 恢复；未将显示失败当作成功。" };
            RecoveryRequired?.Invoke();
        }
        run.Completed(result);
    }

    // Both posture and title owners finish through this exact R4 release/readback path.
    private bool RestorePresentedGroup(PosterVisualWindow owner, IReadOnlyList<PosterVisualWindow> participants, string requestId)
    {
        var restorations = new List<PosterVisualWindow.PreparedSpatialFrame>();
        bool restored = true;
        foreach (var participant in participants)
        {
            try
            {
                var frame = participant.PreparePostureRestore();
                if (frame is not null) restorations.Add(frame);
                else if (participant.WindowHandle != 0) restored = false;
            }
            catch (Exception exception)
            {
                restored = false;
                log.Write("error", "spatial.transition_restore_failed", new { participant.PosterId, exception.Message });
            }
        }
        try
        {
            if (restorations.Count > 0) restored &= owner.CommitSpatialFrames(restorations, DesktopFramePhase.FinalRestore);
        }
        catch (Exception exception)
        {
            restored = false;
            log.Write("error", "spatial.transition_final_commit_failed", new { exception.Message });
        }
        bool layoutsReleased = true;
        foreach (var participant in participants.Where(p => p.WindowHandle != 0))
        {
            try { layoutsReleased &= participant.VerifyTransitionLayout(false); }
            catch (Exception exception)
            {
                layoutsReleased = false;
                log.Write("error", "spatial.transition_release_check_failed", new { participant.PosterId, exception.Message });
            }
        }
        restored &= layoutsReleased;
        log.Write(restored ? "info" : "warning", "spatial.presentation_restored", new { requestId, restored, layoutsReleased });
        return restored;
    }

    private StateCommandResult Failure(PosterStateCommand command, string message)
    {
        state.TryGetCurrent(out var current);
        return new(StateCommandStatus.Failed, command.RequestId, command.ExpectedRevision, current?.Revision ?? 0, current, message);
    }

    private sealed class PostureRun(PosterVisualWindow owner, PosterStateCommand command, SpatialSnapshot initial,
        SpatialAnimationSchedule schedule, PosterAnimationOptions options, IReadOnlyList<PosterVisualWindow> participants,
        DispatcherQueueTimer timer, Action<StateCommandResult> completed)
    {
        public PosterVisualWindow Owner { get; } = owner;
        public PosterStateCommand Command { get; } = command;
        public SpatialSnapshot Initial { get; } = initial;
        public SpatialAnimationSchedule Schedule { get; } = schedule;
        public IReadOnlyList<SpatialBatchFrame> Frames => Schedule.Frames;
        public SpatialAnimationCursor Cursor { get; } = new(schedule, options);
        public Stopwatch Clock { get; } = new();
        public int ReadyCallbacks { get; set; }
        public int AcceptedFrames { get; set; }
        public Task? BackdropObservation { get; set; }
        public double? AnimationOriginMs { get; set; }
        public EventHandler<object>? RenderingHandler { get; set; }
        public IReadOnlyList<PosterVisualWindow> Participants { get; } = participants;
        public DispatcherQueueTimer Timer { get; } = timer;
        public Action<StateCommandResult> Completed { get; } = completed;
        public IReadOnlyList<SpatialChange> Changes { get; init; } = [];
        public bool ReadsCurrent(DesktopUpProjection p) => p.CatalogVersion == Command.ReadSet!.CatalogVersion &&
            p.Posters.Count == Command.ReadSet.Posters.Count && Command.ReadSet.Posters.All(pair =>
                p.PostersById.TryGetValue(pair.Key, out var entry) && entry.EntityVersion == pair.Value);
    }
}
