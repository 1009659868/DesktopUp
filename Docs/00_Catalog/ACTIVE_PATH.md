# 当前活跃文档路径

最后检查：2026-08-18

| 层级 | 当前节点 | 权威入口 | 状态 |
| --- | --- | --- | --- |
| A | Desktop-Up V2 / 技术选型与原型验证 | `A_Project/10_Plan/TECH_SELECTION_CANDIDATES_V1.md` | `ACTIVE — TECH SELECTION` |
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
- 当前唯一实现工作流为 C-QTP 原型；未通过证据矩阵前不启动产品功能实现；
- V1 白条/层级/编辑失效保持为防回归门，任何候选必须逐项证明不复现。

## 当前阶段完成条件（技术选型与原型验证）

- C-QTP 五任务逐项验收，8 项证据矩阵逐项记录；
- 通过 → 选定决策文档；未通过 → 淘汰记录并激活 C-D2D 保底；
- 未经验证不宣布任何渲染技术已被选定。
