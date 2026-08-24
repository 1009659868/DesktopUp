# 当前活跃文档路径

最后检查：2026-08-24

| 层级 | 当前节点 | 权威入口 | 状态 |
| --- | --- | --- | --- |
| A | Desktop-Up V2 完整需求基线 | `REQUIREMENTS_INDEX.md` | `FROZEN — 2026-08-20` |
| A | Desktop-Up V2 技术选型 | `../A_Project/20_Technical_Selection/TECHNICAL_SELECTION_INDEX.md` | `ACTIVE — TS-C01 R4-A USER-ACCEPTED 1/20 / R4-B MANUAL SMOKE AWAITING USER CONFIRMATION / P0 NOT RUN / NO WINNER` |
| A | Axiom Core 系统描述蓝图 | `../A_Project/30_Architecture/AXIOM_BLUEPRINT_INDEX.md` | `FROZEN — AX-BP-01～06 COMPLETE / AXIOM EXITED` |
| B | 无 | 无 | `NOT STARTED` |
| C | 无 | 无 | `NOT STARTED` |

## 当前接手链

1. `REQUIREMENTS_INDEX.md`（冻结输入）；
2. `../A_Project/20_Technical_Selection/TECHNICAL_SELECTION_INDEX.md`；
3. `../A_Project/20_Technical_Selection/TECHNICAL_SELECTION_CHARTER_V1.md`；
4. `../A_Project/20_Technical_Selection/TS01_CANDIDATE_FACT_REVIEW_2026-08-20.md`；
5. `../A_Project/20_Technical_Selection/CANDIDATE_REGISTER_V1.md`；
6. `../A_Project/20_Technical_Selection/DESKTOP_OVERLAY_HOST_PROBE_CHARTER_V1.md`；
7. `../A_Project/20_Technical_Selection/TS_H01_D_EXECUTION_PLAN_V1.md`；
8. `../A_Project/20_Technical_Selection/TS_H01_D3_MATRIX_V1.md`；
9. `../A_Project/20_Technical_Selection/TS_H01_E_DESKTOP_HOST_INTERFACE_FREEZE_PLAN_V1.md`；
10. `../A_Project/20_Technical_Selection/TS_H01_E2_CONTRACT_AUDIT_2026-08-21.md`；
11. `../A_Project/20_Technical_Selection/TS_H01_E3_LIFECYCLE_CONTRACT_2026-08-21.md`；
12. `../A_Project/20_Technical_Selection/TS_H01_E4_GEOMETRY_HIT_RENDER_CONTRACT_2026-08-21.md`；
13. `../A_Project/20_Technical_Selection/TS_H01_E5_DIAGNOSTIC_CONFORMANCE_FIXTURE_2026-08-21.md`；
14. `../A_Project/20_Technical_Selection/TS_H01_E6_CANDIDATE_HANDOFF_2026-08-21.md`；
15. `../A_Project/20_Technical_Selection/DESKTOP_HOST_PROTOTYPE_INTERFACE_V1.md`；
16. `../A_Project/20_Technical_Selection/TS_H01_E_COMPLETION_AUDIT_2026-08-21.md`；
17. `../A_Project/20_Technical_Selection/UNIFIED_CANDIDATE_EVIDENCE_MATRIX_V1.md`；
18. `../A_Project/20_Technical_Selection/TS_02_RENDERER_ADAPTER_P0_EXECUTION_PLAN_V1.md`；
19. `../A_Project/20_Technical_Selection/TS_C01_QT_QUICK_ENTRY_AUDIT_2026-08-24.md`；
20. `../A_Project/20_Technical_Selection/SELECTION_DECISION_RECORD_V1.md`；
21. `../A_Project/30_Architecture/AXIOM_BLUEPRINT_INDEX.md`；
22. `../A_Project/30_Architecture/AXIOM_CORE_PROGRAM_BLUEPRINT_V1.md`；
23. `../A_Project/30_Architecture/AX_BP_01_AXIOM_CORE_SEMANTIC_MAPPING_V1.md`；
24. `../A_Project/30_Architecture/AX_BP_02_PROGRAM_TOPOLOGY_V1.md`；
25. `../A_Project/30_Architecture/AX_BP_03_MODULE_PORT_FREEZE_V1.md`；
26. `../A_Project/30_Architecture/AX_BP_04_FLOW_COMPOSITION_FREEZE_V1.md`；
27. `../A_Project/30_Architecture/AX_BP_05_PHYSICAL_EXECUTION_BOUNDARIES_V1.md`；
28. `../A_Project/30_Architecture/AX_BP_06_CONSISTENCY_AUDIT_AND_HANDOFF_V1.md`；
29. `../../prototype/axiom-desktopup-blueprint/README.md`。

## 当前冻结边界

- 七份需求已由用户确认冻结；12 个 TBD 与 4 个 DEFER 继续按登记状态存在；
- Axiom Core 系统描述蓝图 AX-BP-01～06 已完成并冻结；Axiom 已退出执行主线；
- Qt、原生 Win32、WinUI、WPF、Avalonia、WebView2、Slint 和组合候选均没有 renderer/UI 赢家；
- 公共宿主目标固定为“普通/全屏程序 > 海报 > 桌面图标 > 壁纸”；Lively 的图标下方壁纸路径不满足本项目；
- 旧技术选型、架构尝试和原型不具有默认优先级，只能作为历史风险输入；
- 决策记录仍是 `NO RENDERER WINNER`，Axiom 决策不能替代候选 P0；
- 完整生产架构、制作计划、B/C 工作流和正式产品代码仍未激活。

## 下一动作

TS-C01 已受控修订为 Qt 6.8.3 / Qt Quick / MSVC 2022 x64。R0、R1 已通过；R2 的修订版 OFFSCREEN_FRAME_SOURCE / HOST 路线已由用户完成六项真实 Explorer 人工门并确认 PASS；R3 自动预检已完成双清洁可复现构建、部署、E5 正反例、stale/late frame、故障注入、性能与零资源清理并 PASS。当前进入 R4 人工 P0 门；候选状态仍 P0 NOT RUN，不创建或借用 P0 Evidence。
