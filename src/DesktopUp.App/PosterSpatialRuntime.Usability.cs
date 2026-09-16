using DesktopUp.Application;
namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    public string LocateControlCenterPoster(string id)
    {
        foreach (var peer in _windows.Values) peer.StopLocate();
        if (_suspended || IsSpatialBusy) return "桌面正在恢复或交互，暂不定位。";
        if (!state.TryGetCurrent(out var current) || current!.PostersById.GetValueOrDefault(id) is not { } target) return "目标已不存在。";
        if (target.Poster.PlacementPending) return "此海报等待安置；请在配置与恢复中查看并重试。";
        if (!target.Poster.IsVisible) return "此海报已隐藏；定位不会强行显示。";
        if (ReadControlCenterGeometry(id) is null || !_windows.TryGetValue(id, out var window) || !window.ShowLocate()) return "海报尚未就绪。";
        return "已在桌面短时描边；被其他窗口遮挡时不会抬层。";
    }
    public bool PreviewControlCenterSetting(PosterSettingEdit edit)
    {
        if (_suspended || IsSpatialBusy || edit.Setting is PosterSetting.AutoDelay or PosterSetting.AutoEnabled ||
            !state.TryGetCurrent(out var current) || current!.PostersById.GetValueOrDefault(edit.PosterId)?.EntityVersion != edit.Basis.EntityVersion ||
            !_windows.TryGetValue(edit.PosterId, out var target)) return false;
        foreach (var peer in _windows.Values) if (!ReferenceEquals(peer, target)) peer.ClearCenterPresentation();
        return target.PreviewCenterPresentation(edit);
    }
    public void CancelControlCenterVisuals()
    {
        foreach (var window in _windows.Values) { window.StopLocate(); window.ClearCenterPresentation(); }
    }
}
