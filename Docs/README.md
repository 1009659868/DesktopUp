# Desktop-Up V2 文档入口

七份需求已经用户确认冻结，并于 2026-08-28 经用户反馈继续修订为外框/内容分权、稳定 resize capture 与连续 Item viewport。QTS-04-04 生命周期矩阵已通过；R4.2 已按用户决定实现 Qt Quick GridView/Flickable renderer adapter 与 Host staged-present，Release 14/14 和真实 Runtime smoke 已通过。当前只剩真实桌面 reflow flash 人工关闭门；历史候选矩阵与工作集失败继续保留。
## 当前状态

```text
需求基线：FROZEN — USER REVISION 2026-08-28
技术选型：QT ROUTE SELECTED BY USER — HISTORICAL MATRIX 2 PASS + 1 FAIL + 34 UNKNOWN + 4 NOT RUN RETAINED
系统描述蓝图：FROZEN — AXIOM CORE / AX-BP-01～06 COMPLETE
完整生产架构：FROZEN — QTS-02 / HYBRID QT PHYSICAL BOUNDARIES
Qt 方案计划：ACTIVE — QTS-04-04 R4.2 AUTO PASS / MANUAL REFLOW FLASH GATE PENDING
代码落地：REAL DESKTOP HOST + NIGHT OPS + SPLIT INPUT ISLANDS + FILTERED SHELL EVENTS + DISPLAY REPROJECTION + LOW-LATENCY RELOCATION + FRAME/CONTENT LAYOUT SEPARATION / RELEASE 13 OF 13 CTEST PASS
```

## 唯一阅读路径

1. [需求索引](00_Catalog/REQUIREMENTS_INDEX.md)
2. [V2 完整需求画像](A_Project/10_Requirements/DESKTOPUP_V2_REQUIREMENTS_PROFILE_V1.md)
3. [海报画面与交互规范](A_Project/10_Requirements/POSTER_VISUAL_AND_INTERACTION_V1.md)
4. [控制中心、托盘与海报运行关系](A_Project/10_Requirements/RUNTIME_SURFACES_AND_TRAY_V1.md)
5. [内容 Item 活跃度与动态排版](A_Project/10_Requirements/CONTENT_ITEM_ACTIVITY_LAYOUT_V1.md)
6. [自定义主题平台与 AI 接入](A_Project/10_Requirements/THEME_PLATFORM_AND_AI_V1.md)
7. [风险、反例与待定项](A_Project/10_Requirements/REQUIREMENTS_RISKS_AND_OPEN_QUESTIONS.md)
8. [需求迁移追踪表](A_Project/10_Requirements/REQUIREMENTS_MIGRATION_TRACEABILITY_V1.md)
9. [完整需求一致性审计证据](A_Project/10_Requirements/REQUIREMENTS_CONSISTENCY_AUDIT_2026-08-20.md)
10. [技术选型阶段索引](A_Project/20_Technical_Selection/TECHNICAL_SELECTION_INDEX.md)
11. [技术选型章程](A_Project/20_Technical_Selection/TECHNICAL_SELECTION_CHARTER_V1.md)
12. [技术候选登记表](A_Project/20_Technical_Selection/CANDIDATE_REGISTER_V1.md)
13. [统一候选证据矩阵](A_Project/20_Technical_Selection/UNIFIED_CANDIDATE_EVIDENCE_MATRIX_V1.md)
14. [TS-01 候选事实审查](A_Project/20_Technical_Selection/TS01_CANDIDATE_FACT_REVIEW_2026-08-20.md)
15. [Desktop Overlay Host 探针章程](A_Project/20_Technical_Selection/DESKTOP_OVERLAY_HOST_PROBE_CHARTER_V1.md)
16. [TS-H01-D 生命周期完整门执行计划](A_Project/20_Technical_Selection/TS_H01_D_EXECUTION_PLAN_V1.md)
17. [TS-H01-D3 分辨率、DPI、工作区与边界矩阵](A_Project/20_Technical_Selection/TS_H01_D3_MATRIX_V1.md)
18. [TS-H01-E 接口冻结计划](A_Project/20_Technical_Selection/TS_H01_E_DESKTOP_HOST_INTERFACE_FREEZE_PLAN_V1.md)
19. [TS-H01-E2 合同审计](A_Project/20_Technical_Selection/TS_H01_E2_CONTRACT_AUDIT_2026-08-21.md)
20. [TS-H01-E3 生命周期合同](A_Project/20_Technical_Selection/TS_H01_E3_LIFECYCLE_CONTRACT_2026-08-21.md)
21. [TS-H01-E4 几何/命中/render 原子合同](A_Project/20_Technical_Selection/TS_H01_E4_GEOMETRY_HIT_RENDER_CONTRACT_2026-08-21.md)
22. [TS-H01-E5 诊断事件与一致性夹具](A_Project/20_Technical_Selection/TS_H01_E5_DIAGNOSTIC_CONFORMANCE_FIXTURE_2026-08-21.md)
23. [TS-H01-E6 候选交接规则](A_Project/20_Technical_Selection/TS_H01_E6_CANDIDATE_HANDOFF_2026-08-21.md)
24. [DesktopHost 原型接口](A_Project/20_Technical_Selection/DESKTOP_HOST_PROTOTYPE_INTERFACE_V1.md)
25. [TS-H01-E 完成审计](A_Project/20_Technical_Selection/TS_H01_E_COMPLETION_AUDIT_2026-08-21.md)
26. [TS-02 renderer adapter/P0 执行计划](A_Project/20_Technical_Selection/TS_02_RENDERER_ADAPTER_P0_EXECUTION_PLAN_V1.md)
27. [TS-C01 Qt Quick 进入门审计](A_Project/20_Technical_Selection/TS_C01_QT_QUICK_ENTRY_AUDIT_2026-08-24.md)
28. [TS-C01 R4-D4 当前用户生命周期计划](A_Project/20_Technical_Selection/TS_C01_R4_D4_CURRENT_USER_LIFECYCLE_PLAN_V1.md)
29. [TS-C01 R5 证据收敛与裁决准备计划](A_Project/20_Technical_Selection/TS_C01_R5_EVIDENCE_CONVERGENCE_AND_DECISION_PREP_PLAN_V1.md)
30. [TS-C01 R5-01 原始证据盘点](A_Project/20_Technical_Selection/TS_C01_R5_01_EVIDENCE_INVENTORY_2026-08-26.md)
31. [TS-C01 R5-02 统一矩阵映射](A_Project/20_Technical_Selection/TS_C01_R5_02_UNIFIED_MATRIX_MAPPING_V1.md)
32. [TS-C01 Candidate Evidence](A_Project/20_Technical_Selection/Evidence/TS-C01/README.md)
33. [TS-C01 R5-04 候选处置审计](A_Project/20_Technical_Selection/TS_C01_R5_04_CANDIDATE_DISPOSITION_AUDIT_V1.md)
34. [TS-C01 R5-05 R6 交接输入](A_Project/20_Technical_Selection/TS_C01_R5_05_R6_HANDOFF_INPUT_V1.md)
35. [TS-C01 R6 Qt 实施路线决定](A_Project/20_Technical_Selection/TS_C01_R6_QT_IMPLEMENTATION_DECISION_2026-08-26.md)
36. [选型决策记录](A_Project/20_Technical_Selection/SELECTION_DECISION_RECORD_V1.md)
37. [Qt 方案总执行计划](A_Project/30_Architecture/QT_SOLUTION_EXECUTION_PLAN_V1.md)
38. [QTS-01-01 内存测量合同](A_Project/30_Architecture/QTS_01_01_MEMORY_MEASUREMENT_CONTRACT_V1.md)
39. [QTS-01-01 内存基线审计](A_Project/30_Architecture/QTS_01_01_MEMORY_BASELINE_AUDIT_2026-08-26.md)
40. [QTS-01 混合 Qt 优化方向](A_Project/30_Architecture/QTS_01_HYBRID_QT_OPTIMIZATION_DIRECTION_2026-08-26.md)
41. [QTS-02 生产物理架构](A_Project/30_Architecture/QTS_02_PRODUCTION_PHYSICAL_ARCHITECTURE_V1.md)
42. [QTS-02 一致性审计与 QTS-03 交接](A_Project/30_Architecture/QTS_02_CONSISTENCY_AUDIT_AND_QTS_03_HANDOFF_2026-08-26.md)
43. [AX-BP 蓝图索引](A_Project/30_Architecture/AXIOM_BLUEPRINT_INDEX.md)
44. [Axiom Core 程序蓝图](A_Project/30_Architecture/AXIOM_CORE_PROGRAM_BLUEPRINT_V1.md)
45. [AX-BP-01 Core 语义映射](A_Project/30_Architecture/AX_BP_01_AXIOM_CORE_SEMANTIC_MAPPING_V1.md)
46. [AX-BP-02 整体程序拓扑](A_Project/30_Architecture/AX_BP_02_PROGRAM_TOPOLOGY_V1.md)
47. [AX-BP-03 模块与端口冻结](A_Project/30_Architecture/AX_BP_03_MODULE_PORT_FREEZE_V1.md)
48. [AX-BP-04 调用、数据与组合关系冻结](A_Project/30_Architecture/AX_BP_04_FLOW_COMPOSITION_FREEZE_V1.md)
49. [AX-BP-05 物理执行边界冻结](A_Project/30_Architecture/AX_BP_05_PHYSICAL_EXECUTION_BOUNDARIES_V1.md)
50. [AX-BP-06 一致性审计与交接](A_Project/30_Architecture/AX_BP_06_CONSISTENCY_AUDIT_AND_HANDOFF_V1.md)
51. [Axiom 蓝图类型夹具](../prototype/axiom-desktopup-blueprint/README.md)
52. [QTS-03 生产骨架与合同夹具审计](A_Project/30_Architecture/QTS_03_PRODUCTION_SKELETON_AND_CONTRACT_FIXTURE_AUDIT_2026-08-27.md)
53. [QTS-03 代码锚点](A_Project/30_Architecture/QTS_03_CODE_ANCHORS_V1.md)
54. [QTS-03 实施日志](A_Project/30_Architecture/QTS_03_IMPLEMENTATION_LOG_2026-08-27.md)
55. [QTS-03 → QTS-04 交接](A_Project/30_Architecture/QTS_03_HANDOFF_TO_QTS_04_2026-08-27.md)
56. [QTS-04-01 真实 DesktopHost 与重连审计](A_Project/30_Architecture/QTS_04_01_REAL_DESKTOP_HOST_AND_RECONNECT_AUDIT_2026-08-27.md)
57. [QTS-04-01 代码锚点](A_Project/30_Architecture/QTS_04_01_CODE_ANCHORS_V1.md)
58. [QTS-04-01 实施日志](A_Project/30_Architecture/QTS_04_01_IMPLEMENTATION_LOG_2026-08-27.md)
59. [QTS-04-01 → QTS-04-02 交接](A_Project/30_Architecture/QTS_04_01_HANDOFF_TO_QTS_04_02_2026-08-27.md)
60. [QTS-04-02 NIGHT OPS renderer 审计](A_Project/30_Architecture/QTS_04_02_NIGHT_OPS_QPAINTER_RENDERER_AUDIT_2026-08-27.md)
61. [QTS-04-02 代码锚点](A_Project/30_Architecture/QTS_04_02_CODE_ANCHORS_V1.md)
62. [QTS-04-02 实施日志](A_Project/30_Architecture/QTS_04_02_IMPLEMENTATION_LOG_2026-08-27.md)
63. [QTS-04-02 用户桌面视觉门](A_Project/30_Architecture/QTS_04_02_MANUAL_VISUAL_GATE_V1.md)
64. [QTS-04-02 → QTS-04-03 交接](A_Project/30_Architecture/QTS_04_02_HANDOFF_TO_QTS_04_03_2026-08-27.md)
65. [QTS-04-03 split input islands 审计](A_Project/30_Architecture/QTS_04_03_SPLIT_INPUT_ISLANDS_AUDIT_2026-08-27.md)
66. [QTS-04-03 代码锚点](A_Project/30_Architecture/QTS_04_03_CODE_ANCHORS_V1.md)
67. [QTS-04-03 实施日志](A_Project/30_Architecture/QTS_04_03_IMPLEMENTATION_LOG_2026-08-27.md)
68. [QTS-04-03 用户单海报输入门](A_Project/30_Architecture/QTS_04_03_MANUAL_INPUT_GATE_V1.md)
69. [QTS-04-04 生命周期矩阵计划](A_Project/30_Architecture/QTS_04_04_LIFECYCLE_MATRIX_PLAN_V1.md)
70. [QTS-04-04 代码锚点](A_Project/30_Architecture/QTS_04_04_CODE_ANCHORS_V1.md)
71. [QTS-04-04 实施日志](A_Project/30_Architecture/QTS_04_04_IMPLEMENTATION_LOG_2026-08-28.md)
72. [QTS-04-04 用户生命周期矩阵](A_Project/30_Architecture/QTS_04_04_MANUAL_LIFECYCLE_GATE_V1.md)
73. [QTS-04-04 R2 外框与内容布局分离修订](A_Project/30_Architecture/QTS_04_04_FRAME_CONTENT_LAYOUT_REVISION_V1.md)
74. [QTS-04-04 R3 V1 连续缩放与 Item 溢出对齐](A_Project/30_Architecture/QTS_04_04_V1_PARITY_RESIZE_OVERFLOW_R3.md)
75. [QTS-04-04 R4 连续 Item viewport](A_Project/30_Architecture/QTS_04_04_CONTINUOUS_ITEM_VIEWPORT_R4.md)
76. [QTS-04-04 R4.1 底部裁切与 Renderer 组件审计](A_Project/30_Architecture/QTS_04_04_R4_1_CLIP_AND_COMPONENT_AUDIT_2026-08-28.md)
77. [QTS-04-04 R4.2 Qt Quick Adapter 与 Host 分阶段提交](A_Project/30_Architecture/QTS_04_04_R4_2_QTQUICK_ADAPTER_AND_HOST_TRANSACTION_2026-08-28.md)
## 权威顺序

发生冲突时按以下顺序判断：

1. 用户最新明确决策；
2. `A_Project/10_Requirements/` 下的冻结需求；
3. `A_Project/20_Technical_Selection/` 下已冻结的 DesktopHost 合同与仍在执行的候选证据；
4. `A_Project/30_Architecture/AXIOM_BLUEPRINT_INDEX.md` 及 AX-BP-01～06 中已冻结的框架中立系统描述边界；
5. `Reference/V1_Design_Source/` 中的只读历史来源。

旧代码、旧二进制、旧验证结果和已删除的实施文档不能覆盖当前需求，也不能证明某项技术已被选定。

## 下一阶段的建立条件

QTS-04-04 生命周期矩阵 1～6 已通过；R1～R4 历史证据保留。R4.1 已用 IntersectClip 和像素门关闭 card-local clip 泄漏。R4.2 已实现 Qt Quick renderer adapter 与 Host staged-present，并通过 Release 14/14 与真实进程 smoke；当前执行真实桌面 reflow flash 最小人工门，通过前不把单海报 P0 扩写为完整 PASS。
