# 当前活跃文档路径

最后检查：2026-08-04

| 层级 | 当前节点 | 权威入口 | 状态 |
| --- | --- | --- | --- |
| A | DesktopUP V1 落地计划 / V1-P0 宿主硬门 | `A_Project/10_Plan/DESKTOPUP_V1_IMPLEMENTATION_PLAN.md` | `ACTIVE` |
| B | Desktop Runtime Foundation / 单全局桌面运行时 | `B_Modules/DesktopRuntimeFoundation/10_Plan/MODULE_PLAN_V1.md` | `ACTIVE` |
| C | Desktop Host Feasibility / 桌面宿主可行性验证 | `C_Workstreams/DesktopHostFeasibility/10_Plan/WORKSTREAM_PLAN.md` | `ACTIVE` |

## 当前接手链

1. `A_Project/30_Sync/COLD_START.md`
2. `B_Modules/DesktopRuntimeFoundation/30_Sync/COLD_START.md`
3. `C_Workstreams/DesktopHostFeasibility/30_Sync/COLD_START.md`

## 当前决策门

V1-P0 不直接进入海报核心和真实收纳开发。当前 C-DHF 必须先关闭以下硬门：

- Windows 11 桌面层挂载和恢复可行；
- Win+D、Explorer 重启、多显示器和 DPI 切换可恢复；
- 海报区域可交互，空白区域可穿透；
- 全屏/最大化应用出现时能够事件驱动暂停；
- 静态状态不持续刷新。

当前顺序为 `C-DHF-04 + C-DHF-05 → C-DHF-06 / C-DHF-07 → C-DHF-08`。关闭后激活 `C-PCB Poster Contract Baseline`；在此之前不得把设计冻结或契约草案标记为实现通过。
