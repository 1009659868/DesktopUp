# 当前活跃文档路径

最后检查：2026-08-25

| 层级 | 当前节点 | 权威入口 | 状态 |
| --- | --- | --- | --- |
| A | Desktop-Up V2 完整需求基线 | `REQUIREMENTS_INDEX.md` | `FROZEN — 2026-08-20` |
| A | Desktop-Up V2 技术选型 | `../A_Project/20_Technical_Selection/TECHNICAL_SELECTION_INDEX.md` | `ACTIVE — TS-C01 R5-01～05 COMPLETE / 2 PASS + 1 FAIL + 34 UNKNOWN + 4 NOT RUN / FAIL RECOMMENDED / R6 USER DECISION PENDING / NO WINNER` |
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
20. `../A_Project/20_Technical_Selection/TS_C01_R4_D4_CURRENT_USER_LIFECYCLE_PLAN_V1.md`；
21. `../A_Project/20_Technical_Selection/TS_C01_R5_EVIDENCE_CONVERGENCE_AND_DECISION_PREP_PLAN_V1.md`；
22. `../A_Project/20_Technical_Selection/TS_C01_R5_01_EVIDENCE_INVENTORY_2026-08-26.md`；
23. `../A_Project/20_Technical_Selection/TS_C01_R5_02_UNIFIED_MATRIX_MAPPING_V1.md`；
24. `../A_Project/20_Technical_Selection/Evidence/TS-C01/README.md`；
25. `../A_Project/20_Technical_Selection/TS_C01_R5_04_CANDIDATE_DISPOSITION_AUDIT_V1.md`；
26. `../A_Project/20_Technical_Selection/TS_C01_R5_05_R6_HANDOFF_INPUT_V1.md`；
27. `../A_Project/20_Technical_Selection/SELECTION_DECISION_RECORD_V1.md`；
28. `../A_Project/30_Architecture/AXIOM_BLUEPRINT_INDEX.md`；
29. `../A_Project/30_Architecture/AXIOM_CORE_PROGRAM_BLUEPRINT_V1.md`；
30. `../A_Project/30_Architecture/AX_BP_01_AXIOM_CORE_SEMANTIC_MAPPING_V1.md`；
31. `../A_Project/30_Architecture/AX_BP_02_PROGRAM_TOPOLOGY_V1.md`；
32. `../A_Project/30_Architecture/AX_BP_03_MODULE_PORT_FREEZE_V1.md`；
33. `../A_Project/30_Architecture/AX_BP_04_FLOW_COMPOSITION_FREEZE_V1.md`；
34. `../A_Project/30_Architecture/AX_BP_05_PHYSICAL_EXECUTION_BOUNDARIES_V1.md`；
35. `../A_Project/30_Architecture/AX_BP_06_CONSISTENCY_AUDIT_AND_HANDOFF_V1.md`；
36. `../../prototype/axiom-desktopup-blueprint/README.md`。

## 当前冻结边界

- 七份需求已由用户确认冻结；12 个 TBD 与 4 个 DEFER 继续按登记状态存在；
- Axiom Core 系统描述蓝图 AX-BP-01～06 已完成并冻结；Axiom 已退出执行主线；
- Qt、原生 Win32、WinUI、WPF、Avalonia、WebView2、Slint 和组合候选均没有 renderer/UI 赢家；
- 公共宿主目标固定为“普通/全屏程序 > 海报 > 桌面图标 > 壁纸”；Lively 的图标下方壁纸路径不满足本项目；
- 旧技术选型、架构尝试和原型不具有默认优先级，只能作为历史风险输入；
- 决策记录仍是 `NO RENDERER WINNER`，Axiom 决策不能替代候选 P0；
- 完整生产架构、制作计划、B/C 工作流和正式产品代码仍未激活。

## 下一动作

R5-01～05 已完成并形成正式 TS-C01 Candidate Evidence。统一矩阵结果为 2 PASS、1 FAIL、34 UNKNOWN、4 NOT RUN；`HG-PERF-02` 工作集 136.332MiB 超过 50MiB，R5 按 E6 建议 FAIL，且不允许加权评分。当前进入 R6 用户处置决定：接受 FAIL 并按顺序进入 TS-C02，或先建立适用于所有候选的资源预算需求变更并全量重跑。TS-C01 尚未被用户最终标为 REJECTED，当前仍无 renderer/UI winner。
