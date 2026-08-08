# 当前活跃文档路径

最后检查：2026-08-08

| 层级 | 当前节点 | 权威入口 | 状态 |
| --- | --- | --- | --- |
| A | DesktopUP V1 落地计划 / V1-P0 宿主硬门 | `A_Project/10_Plan/DESKTOPUP_V1_IMPLEMENTATION_PLAN.md` | `ACTIVE — V1-P0 COMPLETE / NEXT V1-P1` |
| B | Desktop Runtime Foundation / 单全局桌面运行时 | `B_Modules/DesktopRuntimeFoundation/10_Plan/MODULE_PLAN_V1.md` | `ACTIVE — C-DRP NEXT` |
| C | Desktop Host Feasibility / 桌面宿主可行性验证 | `C_Workstreams/DesktopHostFeasibility/10_Plan/WORKSTREAM_PLAN.md` | `COMPLETE — NEXT C-DRP / C-PCB` |

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

当前推进顺序（2026-08-05）：

```text
C-DHF-04/05 剩余物理矩阵（COMPLETE）
→ C-DHF-06 性能基线 + Desktop-Up 内简单性能数值可视化（COMPLETE）
→ C-DHF-07/08（COMPLETE）
→ 激活 C-PCB Poster Contract Baseline
```

`C-DHF-01..08` 已关闭，`V1-P0` 宿主硬门结论具备；下一步激活 `C-DRP` 与 `C-PCB`，不得把 M1 标记为实现通过。

2026-08-08 在 `impl/poster-redesign-V1-fix` 完成关闭后维护修复并合入 `impl/poster-redesign-V1`：统一 Dev/Release 海报快照，修正锁定与尺寸包络、托盘快捷入口、任务栏契约、桌面 Z 序和顶部亮带；不改变下一工作流选择，也不把 M1 标记为完成。
