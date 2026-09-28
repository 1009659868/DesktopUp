using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Xaml.Media;

namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    public ControlCenterGeometryBasis? ReadControlCenterGeometry(string? posterId)
    {
        if (posterId is null || _suspended || IsSpatialBusy || !state.TryGetCurrent(out var current) ||
        !current!.PostersById.TryGetValue(posterId, out var target) || !target.IsEffectivelyVisible ||
            !_windows.TryGetValue(posterId, out var window) ||
            !window.TryReadSpatial(_runtimeId, out var environment, out var spatial, out long version)) return null;
        var basis = new ControlCenterGeometryBasis(posterId, version, spatial!.LayoutGeneration, environment!, spatial.Bounds);
        return basis.Matches(target) ? basis : null;
    }


    public void ApplyControlCenterGeometry(ControlCenterGeometryRequest request, Action<StateCommandResult> completed)
    {
        var command = new PosterStateCommand(request.RequestId, StateCommandSource.ControlCenter, PosterStateCommandKind.Move,
            request.ObservedRevision, request.Basis.Poster.PosterId, request.Basis.Poster) { ReadSet = request.ReadSet };
        BeforeManualIntent(command.PosterId);
        if (IsSpatialBusy || !_windows.TryGetValue(command.PosterId, out var owner) ||
            !TrySnapshot(owner, out var scene, out var projection))
        { completed(Failure(command, "宿主未就绪或空间正在交互，请稍后重试。")); return; }
        if (!request.ReadsCurrent(projection!))
        { completed(Failure(command, "目标或障碍已变化；请取消以载入最新值。") with { Status = StateCommandStatus.Conflict }); return; }
        var target = projection!.PostersById[command.PosterId];
        if (request.ActualBasis is not { } actual || !actual.Matches(scene!, target))
        { completed(Failure(command, "实际位置或显示环境已变化，请重新载入几何。") with { Status = StateCommandStatus.Conflict }); return; }
        var intent = actual.Resolve(target.Poster, request.Desired, request.ChangedFields);
        var plan = ControlCenterSpatial.Plan(target.Poster, scene!, intent, out var error);
        if (plan is null) { completed(Failure(command, error!)); return; }
        bool resize = intent.Width != request.Basis.Poster.WidthDip || intent.ExpandedHeight != request.Basis.Poster.ExpandedHeightDip;
        command = command with { Kind = resize ? PosterStateCommandKind.Resize : PosterStateCommandKind.Move,
            DesiredState = plan.Desired, ControlCenterGeometry = new(scene!, intent) };
        if (plan.Path.Count == 0) { completed(commands.Execute(command)); return; }
        try
        {
            var schedule = SpatialAnimationSchedule.Create(scene!, plan.Path.Select(p => new SpatialBatchFrame(new[] { p })).ToArray(), animationOptions);
            var timer = owner.SpatialDispatcher.CreateTimer();
            timer.Interval = TimeSpan.FromMilliseconds(100); timer.IsRepeating = true;
            var run = new PostureRun(owner, command, scene!, schedule, animationOptions, new[] { owner }, timer, completed) { Changes = plan.Changes };
            _posture = run; timer.Tick += WatchPosture;
            run.RenderingHandler = (_, _) =>
            {
                try { AdvancePosture(run); }
                catch (Exception ex) { log.Write("error", "control_center.geometry_frame_failed", new { ex.Message }); CancelPosture("geometry-frame-failed"); }
            };
            var before = plan.Changes[0].Before; var after = plan.Changes[0].After;
            var capacity = before with { Width = Math.Max(before.Width, after.Width), Height = Math.Max(before.Height, after.Height) };
            if (before.Width != after.Width || before.Height != after.Height)
                owner.BeginGeometryPreview(command.RequestId, Pixels(capacity));
            else
                owner.BeginPosturePreview(command.RequestId, Pixels(capacity), false);
            var prepared = owner.PrepareSpatialFrame(Pixels(before));
            if (prepared is null || !owner.CommitSpatialFrames(new[] { prepared }, DesktopFramePhase.SurfacePreparation))
                throw new InvalidOperationException("几何呈现准备失败。");
            run.Clock.Start(); CompositionTarget.Rendering += run.RenderingHandler; timer.Start();
            ActivityChanged?.Invoke();
            log.Write("info", "control_center.geometry_started", new { command.RequestId, command.PosterId, before, after, reads = command.ReadSet!.Posters.Count });
        }
        catch (Exception ex)
        {
            if (_posture?.Command.RequestId == command.RequestId) CancelPosture("geometry-prepare-failed:" + ex.Message);
            else completed(Failure(command, ex.Message));
        }
    }

    public StateCommandResult ApplyControlCenterCatalog(ControlCenterCatalogRequest request)
    {
        StateCommandResult Reject(string error, StateCommandStatus status = StateCommandStatus.Failed)
        { state.TryGetCurrent(out var p); return new(status, request.RequestId, request.Revision, p?.Revision ?? 0, p, error); }
        if (_suspended || IsSpatialBusy || _effectiveTopology?.PrimaryMonitor is not { } monitor ||
            !state.TryGetCurrent(out var current) || current is null) return Reject("桌面尚未就绪或正在交互。");
        if (current.CatalogVersion != request.Reads.CatalogVersion || current.Posters.Count != request.Reads.Posters.Count ||
            current.Posters.Any(p => request.Reads.Posters.GetValueOrDefault(p.Poster.PosterId) != p.EntityVersion))
            return Reject("对话框打开后目录或海报已变化，请重试。", StateCommandStatus.Conflict);
        var entries = new List<SpatialPoster>(); SpatialEnvironment? environment = null;
        // Global hiding preserves individual visibility intent. Reuse EXACT recovery geometry as latent occupancy,
        // never an empty scene just because HWNDs are absent. Normal mode still requires live readback.
        foreach (var target in current.Posters.Where(p => p.IsEffectivelyVisible))
        {
            if (globallyHidden?.Invoke() == true)
            {
                var mapped = _effectiveTopology.Monitors.FirstOrDefault(m =>
                    string.Equals(m.DeviceName, target.Poster.MonitorDeviceName, StringComparison.OrdinalIgnoreCase)) ?? monitor;
                if (mapped.DeviceName != monitor.DeviceName) continue;
                if (ResolveEffectiveBounds(target, mapped) is not { } latent) return Reject("隐藏海报无法安全重建占位，未新增。");
                environment ??= new(monitor.DeviceName, Rect(monitor.WorkArea), monitor.DpiX, monitor.DpiY,
                    _effectiveTopology.Generation, 0, _runtimeId);
                var bounds = Rect(latent);
                entries.Add(new(target.Poster.PosterId, bounds,
                    Math.Max(bounds.Height, (int)Math.Round(target.Poster.ExpandedHeightDip * monitor.ScaleY)),
                    true, target.Poster.IsLocked, target.Poster.IsCollapsed, target.LayoutGeneration, target.LastLayoutOperationId));
                continue;
            }
            if (!_windows.TryGetValue(target.Poster.PosterId, out var window) ||
                !window.TryReadSpatial(_runtimeId, out var env, out var peer, out long version) || version != target.EntityVersion)
                return Reject("存在未就绪海报，暂不新增，以免错误判断空位。");
            if (env!.WorkspaceId != monitor.DeviceName) continue;
            if (environment is not null && environment != env) return Reject("显示环境正在变化。");
            environment = env; entries.Add(peer!);
        }
        environment ??= new(monitor.DeviceName, Rect(monitor.WorkArea), monitor.DpiX, monitor.DpiY, _effectiveTopology.Generation, 0, _runtimeId);
        var proof = new ControlCenterPlacementProof(new(environment, entries));
        if (request.CloneSourceId is { } source)
            return commands.Execute(new ClonePosterCommand(request.RequestId, request.Source,
                request.Revision, request.Reads, source) { Placement = proof, Title = request.Title });
        var configuration = PosterConfigurationFactory.NewBasicBox(request.Title, monitor.DeviceName);
        return commands.Execute(new CreatePosterCommand(request.RequestId, request.Source,
            request.Revision, request.Reads, configuration) { Placement = proof });
    }
}
