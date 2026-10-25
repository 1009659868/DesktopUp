using DesktopUp.Application;
using DesktopUp.Domain;
namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    public string? ValidateControlCenterSetting(PosterSettingEdit edit)
    {
        if (_suspended || IsPosterSpatialBusy(edit.PosterId)) return "海报正在恢复或交互，请先完成或取消当前操作。";
        if (!state.TryGetCurrent(out var current) || current!.PostersById.GetValueOrDefault(edit.PosterId)?.EntityVersion != edit.Basis.EntityVersion)
            return "海报版本已变化，请重新开始这次调节。";
        if (!_windows.TryGetValue(edit.PosterId, out var target)) return null;
        if (edit.Setting != PosterSetting.Scale) return target.ValidateCenterPresentation(edit);
        var command = edit.Command("scale-preview-validation");
        if (!TryPlanScale(command, target, out var plan, out _, out _, out var error)) return error;
        return target.ValidateCenterPresentation(edit, ScalePreviewBounds(target, plan!));
    }
    public string LocateControlCenterPoster(string id)
    {
        foreach (var peer in _windows.Values) peer.StopLocate();
        if (_suspended || IsPosterSpatialBusy(id)) return "桌面正在恢复或交互，暂不定位。";
        if (!state.TryGetCurrent(out var current) || current!.PostersById.GetValueOrDefault(id) is not { } target) return "目标已不存在。";
        if (target.Poster.PlacementPending) return "此海报等待安置；请在配置与恢复中查看并重试。";
        if (!target.Poster.IsVisible) return "此海报已隐藏；定位不会强行显示。";
        if (ReadControlCenterGeometry(id) is null || !_windows.TryGetValue(id, out var window) || !window.ShowLocate()) return "海报尚未就绪。";
        return "已在桌面短时描边；被其他窗口遮挡时不会抬层。";
    }
    public bool PreviewControlCenterSetting(PosterSettingEdit edit)
    {
        if (_suspended || IsPosterSpatialBusy(edit.PosterId) || edit.Setting is PosterSetting.AutoDelay or PosterSetting.AutoEnabled ||
            !state.TryGetCurrent(out var current) || current!.PostersById.GetValueOrDefault(edit.PosterId)?.EntityVersion != edit.Basis.EntityVersion ||
            !_windows.TryGetValue(edit.PosterId, out var target)) return false;
        foreach (var peer in _windows.Values.ToArray()) if (!ReferenceEquals(peer, target)) peer.ClearCenterPresentation();
        if (edit.Setting != PosterSetting.Scale) return target.PreviewCenterPresentation(edit);
        var command = edit.Command("scale-preview");
        return TryPlanScale(command, target, out var plan, out _, out _, out _) &&
            target.PreviewCenterPresentation(edit, ScalePreviewBounds(target, plan!));
    }
    private static DesktopPixelRect ScalePreviewBounds(PosterVisualWindow target, ControlCenterGeometryPlan plan) =>
        plan.Changes.Count == 0 ? target.CenterPresentationBaseline!.Value : Pixels(plan.Changes[0].After);

    private bool TryPlanScale(PosterStateCommand command, PosterVisualWindow target, out ControlCenterGeometryPlan? plan,
        out SpatialSnapshot? scene, out DesktopUpProjection? current, out string? error)
    {
        plan = null; scene = null; current = null;
        error = "桌面、海报版本或空间已变化，本次尺度未保存。";
        if (_suspended || IsSpatialBusy || target.CenterPresentationBaseline is not { } baseline ||
            !TrySnapshot(target, out var observed, out current) ||
            current!.PostersById.GetValueOrDefault(command.PosterId) is not { } basis ||
            command.ReadSet?.Posters.GetValueOrDefault(command.PosterId) != basis.EntityVersion) return false;
        // Native readback above is fresh; only our own transient frame is replaced by its
        // original same-version/generation bounds. Peers remain their actual current geometry.
        scene = new(observed!.Environment, observed.Posters.Select(p => p.PosterId == command.PosterId
            ? p with { Bounds = Rect(baseline) } : p));
        plan = ContentScaleExpansion.Plan(basis.Poster, command.DesiredState, scene, out error, target.ActiveContentHeat);
        return plan is not null;
    }

    public StateCommandResult ApplyControlCenterPresentation(PosterStateCommand command)
    {
        if (!state.TryGetCurrent(out var current) ||
            current!.PostersById.GetValueOrDefault(command.PosterId) is not { } basis ||
            !ContentScaleExpansion.ChangesScale(basis.Poster, command.DesiredState) ||
            !_windows.TryGetValue(command.PosterId, out var target)) return commands.Execute(command);
        try
        {
            if (!TryPlanScale(command, target, out var plan, out var scene, out current, out var error))
                return Failure(command, error!);
            if (!target.PreviewCenterPresentation(command, ScalePreviewBounds(target, plan!)))
                return Failure(command, "无法安全撑开海报，已恢复原尺度。");
            if (plan!.Changes.Count != 0)
                command = command with
                {
                    DesiredState = plan.Desired,
                    ReadSet = command.ReadSet! with
                    {
                        Posters = current!.Posters.ToDictionary(p => p.Poster.PosterId, p => p.EntityVersion),
                        CatalogVersion = current.CatalogVersion
                    },
                    ContentScaleExpansion = new(scene!, command.DesiredState, target.ActiveContentHeat)
                };
            return commands.Execute(command);
        }
        finally { target.ClearCenterPresentation(); }
    }
    public void CancelControlCenterVisuals()
    {
        foreach (var window in _windows.Values.ToArray()) { window.StopLocate(); window.ClearCenterPresentation(); }
    }
    internal void CenterPresentationRestoreFailed(PosterVisualWindow window)
    {
        _suspended = true;
        window.SuspendInput();
        log.Write("error", "control_center.preview_restore_failed", new { window.PosterId });
        RecoveryRequired?.Invoke();
    }
}
