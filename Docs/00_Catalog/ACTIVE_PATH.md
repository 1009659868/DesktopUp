# 当前活跃文档路径

最后检查：2026-08-09

| 层级 | 当前节点 | 权威入口 | 状态 |
| --- | --- | --- | --- |
| A | DesktopUP V1 落地计划 / V1-P1 生产宿主、核心契约与数据基线 | `A_Project/10_Plan/DESKTOPUP_V1_IMPLEMENTATION_PLAN.md` | `ACTIVE — V1-P1` |
| B | Desktop Runtime Foundation / 单全局桌面运行时 | `B_Modules/DesktopRuntimeFoundation/10_Plan/MODULE_PLAN_V1.md` | `ACTIVE — C-DRP` |
| C | Desktop Runtime Productionization / 生产宿主收敛 | `C_Workstreams/DesktopRuntimeProductionization/10_Plan/WORKSTREAM_PLAN.md` | `ACTIVE — C-DRP-02 IN PROGRESS` |

## 当前接手链

1. `A_Project/30_Sync/COLD_START.md`
2. `B_Modules/DesktopRuntimeFoundation/30_Sync/COLD_START.md`
3. `C_Workstreams/DesktopRuntimeProductionization/30_Sync/COLD_START.md`

## 当前决策门

V1-P0 与 C-DHF-01..08 已关闭。当前 C-DRP 必须把验证原型固化为生产宿主：

- 运行时只有一个全局透明宿主 WebView；
- 宿主拥有原生窗口几何、显示/DPI 映射、桌面挂载、命中、暂停和恢复；
- 海报核心只通过版本化接口提交逻辑表面，不访问 Windows 私有实现；
- 多区域命中、空白穿透、不可激活和桌面 Z 序不回退；
- Release 静态/暂停性能满足量化预算。

当前推进顺序（2026-08-09）：

```text
C-DRP（ACTIVE）
→ C-PCB
→ C-PSS
→ C-IBC / C-RLC
→ C-LST / C-PSI / C-PCRG
→ C-ROD / C-PAA / C-LCH（Usable MVP）
→ C-SVT / C-RCV（Safe Collection MVP）
```

`C-DRP` 是当前唯一活跃 C；`C-PCB` 只处于 `PLANNED NEXT`，不得同时标记 `ACTIVE`。本次计划重排不把 V1-P1、M1、真实导入或安装包标记为实现通过。

2026-08-08 在 `impl/poster-redesign-V1-fix` 完成关闭后维护修复并合入 `impl/poster-redesign-V1`：统一 Dev/Release 海报快照，修正锁定与尺寸包络、托盘快捷入口、任务栏契约、桌面 Z 序和顶部亮带；这些结论作为 C-DRP 输入，不把 M1 标记为完成。
