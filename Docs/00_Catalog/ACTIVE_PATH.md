# 当前活跃文档路径

最后检查：2026-08-02

| 层级 | 当前节点 | 权威入口 | 状态 |
| --- | --- | --- | --- |
| A | Desktop-Up 产品主线 / A0 可行性与体验定型 | `A_Project/10_Plan/PROJECT_MAINLINE.md` | `ACTIVE` |
| B | Desktop Runtime Foundation / 桌面运行时基础 | `B_Modules/DesktopRuntimeFoundation/10_Plan/MODULE_PLAN.md` | `ACTIVE` |
| C | Desktop Host Feasibility / 桌面宿主可行性验证 | `C_Workstreams/DesktopHostFeasibility/10_Plan/WORKSTREAM_PLAN.md` | `ACTIVE` |

## 当前接手链

1. `A_Project/30_Sync/COLD_START.md`
2. `B_Modules/DesktopRuntimeFoundation/30_Sync/COLD_START.md`
3. `C_Workstreams/DesktopHostFeasibility/30_Sync/COLD_START.md`

## 当前决策门

A0 不直接进入完整功能开发。桌面宿主原型必须先验证以下硬门：

- Windows 11 桌面层挂载和恢复可行；
- Win+D、Explorer 重启、多显示器和 DPI 切换可恢复；
- 海报区域可交互，空白区域可穿透；
- 全屏/最大化应用出现时能够事件驱动暂停；
- 静态状态不持续刷新。

未通过桌面宿主硬门前，主题市场、游戏平台深度扫描和脚本主题保持 `PLANNED`。
