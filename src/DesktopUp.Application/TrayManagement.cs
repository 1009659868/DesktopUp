namespace DesktopUp.Application;

public enum TrayAction { Create, OpenCenter, Visibility, LockAll, Motion, Exit }
public sealed record TrayMenuEntry(TrayAction Action, string Text, bool Enabled, bool? Checked = null);

// Only global/catalog actions; no mutable control-center selection or per-poster tray target.
public static class TrayManagement
{
    public static IReadOnlyList<TrayMenuEntry> Menu(TrayProjection? projection, bool hidden, bool environmentPending) =>
    [
        new(TrayAction.Create, "新增海报", projection is not null && !environmentPending),
        new(TrayAction.OpenCenter, "打开控制中心", true),
        new(TrayAction.Visibility, hidden ? "恢复显示海报" : "隐藏全部海报", projection is not null, hidden),
        new(TrayAction.LockAll, projection?.AllPostersLocked == true ? "解锁全部海报" :
            projection?.LockSummary == PosterLockSummary.Some ? "锁定全部海报（部分已锁定）" : "锁定全部海报",
            projection?.PosterCount > 0, projection?.AllPostersLocked == true),
        new(TrayAction.Motion, projection?.DynamicEffectsPaused == true ? "恢复提示动效" : "暂停提示动效",
            projection is not null, projection?.DynamicEffectsPaused == true),
        new(TrayAction.Exit, "退出 DesktopUp", true)
    ];

    public static ControlCenterCatalogRequest Create(DesktopUpProjection snapshot, string requestId) =>
        new(requestId, snapshot.Revision, new StateReadSet(snapshot.Posters.ToDictionary(p => p.Poster.PosterId,
            p => p.EntityVersion), CatalogVersion: snapshot.CatalogVersion).Freeze(), null, "新海报")
        { Source = StateCommandSource.Tray };

}
