# 当前活跃文档路径

最后检查：2026-08-18

| 层级 | 当前节点 | 权威入口 | 状态 |
| --- | --- | --- | --- |
| A | Desktop-Up V2 / 长程落地推进 | `A_Project/10_Plan/DESKTOPUP_V2_IMPLEMENTATION_PLAN.md` | `ACTIVE — V2-P0` |
| B | B-RND Renderer Selection | `B_Modules/RendererSelection/30_Sync/CURRENT_HANDOFF.md` | `ACTIVE — C-QTP` |
| C | C-QTP Qt Quick Prototype | `C_Workstreams/QtQuickPrototype/30_Sync/CURRENT_HANDOFF.md` | `ACTIVE` |

## 当前接手链

1. `A_Project/30_Sync/COLD_START.md`
2. `A_Project/10_Plan/DESKTOPUP_V2_REQUIREMENTS_PROFILE_V1.md`
3. `A_Project/10_Plan/POSTER_VISUAL_AND_INTERACTION_V1.md`
4. `A_Project/10_Plan/RUNTIME_SURFACES_AND_TRAY_V1.md`
5. `A_Project/10_Plan/CONTENT_ITEM_ACTIVITY_LAYOUT_V1.md`
6. `A_Project/10_Plan/THEME_PLATFORM_AND_AI_V1.md`
7. `A_Project/10_Plan/REQUIREMENTS_MIGRATION_TRACEABILITY_V1.md`
8. `A_Project/30_Sync/CURRENT_HANDOFF.md`
9. `A_Project/30_Sync/KNOWN_ISSUES.md`

## 当前决策门

- 需求画像与四份设计合同已冻结；技术选型已启动并完成用户裁决；
- 用户裁决：路线甲（Qt 6 Quick 优先、原生 D2D 保底）、内存预算原型实测后放宽、C++ 渲染侧车进程 + IPC；
- C-QTP 原型自动化证据 5/8 PASS(矩阵 4/5/7/8),矩阵 1/2/3 待用户闸口 A 实机;选定决策草稿已建(`B_Modules/RendererSelection/30_Sync/SELECTION_DECISION_DRAFT.md`);
- V2-P1 宿主重建计划已提前准备(`C_Workstreams/V2P1HostRebuild/10_Plan/WORKSTREAM_PLAN.md`),待闸口 A 解锁;
- V1 白条/层级/编辑失效保持为防回归门，任何候选必须逐项证明不复现。

## 当前阶段完成条件(闸口 A)

- 用户实机验证三项:穿透(空白点图标/标题栏命中)、拖动 20 轮零白条、不打扰(Win+D/Explorer);
- 全过 → 选定决策定稿 → 激活 C-V2P1;任一失败 → 淘汰记录 → 激活 C-D2D 保底。
