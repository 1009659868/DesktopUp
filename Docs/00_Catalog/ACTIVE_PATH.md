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

- 需求画像与四份设计合同已冻结；技术选型已完成：**Qt 6 Quick 路线甲选定成立(闸口 A 8/8 PASS)**;
- 渲染选定决策已定稿(`B_Modules/RendererSelection/30_Sync/SELECTION_DECISION_DRAFT.md`);
- 当前推进:V2-P1 宿主重建(核心真身已完成路线中立部分;渲染器生产化 C-V2P1-03 已解锁) → 闸口 B;
- V1 白条/层级/编辑失效保持为防回归门，任何候选必须逐项证明不复现。

## 当前阶段完成条件(闸口 B)

- 主屏单海报八项:C-V2P1-06 证据矩阵(冷启动/20 轮拖动/穿透/层级/Win+D/Explorer/托盘/资源复测);
- 渲染器生产化(hello_ack 配置注入、命中语义化、参数校验)随 V2-P1-03 落地;
- 用户闸口 B 实机验收。
