# Desktop-Up V2 文档入口

七份需求已经用户确认冻结。技术选型仍在执行且没有 renderer/UI 赢家；仅使用 Axiom Core 的系统描述蓝图 AX-BP-01～06 已经完成并冻结，Axiom 已退出执行主线。旧技术选型、旧架构尝试、旧制作计划、旧工作流、旧交接和旧实现日志不具有执行权威。

## 当前状态

```text
需求基线：FROZEN — 2026-08-20
技术选型：ACTIVE — TS-C01 R2 PASS / R3 AUTO PASS / R4-A USER-ACCEPTED 1/20 / R4-B MANUAL SMOKE AWAITING USER CONFIRMATION / P0 NOT RUN / NO WINNER
系统描述蓝图：FROZEN — AXIOM CORE / AX-BP-01～06 COMPLETE
完整生产架构：NOT FROZEN — PHYSICAL ADAPTERS UNSELECTED
制作计划与 B/C 工作流：NOT STARTED
代码落地：TECHNICAL PROBES + AXIOM TYPE FIXTURE ONLY
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
28. [选型决策记录](A_Project/20_Technical_Selection/SELECTION_DECISION_RECORD_V1.md)
29. [AX-BP 蓝图索引](A_Project/30_Architecture/AXIOM_BLUEPRINT_INDEX.md)
30. [Axiom Core 程序蓝图](A_Project/30_Architecture/AXIOM_CORE_PROGRAM_BLUEPRINT_V1.md)
31. [AX-BP-01 Core 语义映射](A_Project/30_Architecture/AX_BP_01_AXIOM_CORE_SEMANTIC_MAPPING_V1.md)
32. [AX-BP-02 整体程序拓扑](A_Project/30_Architecture/AX_BP_02_PROGRAM_TOPOLOGY_V1.md)
33. [AX-BP-03 模块与端口冻结](A_Project/30_Architecture/AX_BP_03_MODULE_PORT_FREEZE_V1.md)
34. [AX-BP-04 调用、数据与组合关系冻结](A_Project/30_Architecture/AX_BP_04_FLOW_COMPOSITION_FREEZE_V1.md)
35. [AX-BP-05 物理执行边界冻结](A_Project/30_Architecture/AX_BP_05_PHYSICAL_EXECUTION_BOUNDARIES_V1.md)
36. [AX-BP-06 一致性审计与交接](A_Project/30_Architecture/AX_BP_06_CONSISTENCY_AUDIT_AND_HANDOFF_V1.md)
37. [Axiom 蓝图类型夹具](../prototype/axiom-desktopup-blueprint/README.md)

## 权威顺序

发生冲突时按以下顺序判断：

1. 用户最新明确决策；
2. `A_Project/10_Requirements/` 下的冻结需求；
3. `A_Project/20_Technical_Selection/` 下已冻结的 DesktopHost 合同与仍在执行的候选证据；
4. `A_Project/30_Architecture/AXIOM_BLUEPRINT_INDEX.md` 及 AX-BP-01～06 中已冻结的框架中立系统描述边界；
5. `Reference/V1_Design_Source/` 中的只读历史来源。

旧代码、旧二进制、旧验证结果和已删除的实施文档不能覆盖当前需求，也不能证明某项技术已被选定。

## 下一阶段的建立条件

需求和 AX-BP 系统描述蓝图已经冻结；Axiom Core 只作为架构方法与追溯输入保留，axiom-runtime 明确排除。TS-C01 的 Qt 6.8.3 R2 真实 Explorer 六项人工门已由用户确认 PASS，R3 自动预检也已 PASS；当前进入 R4 人工 P0 门，候选仍 P0 NOT RUN，未创建 Candidate Evidence。renderer/UI、StateStore、生产 runtime、进程与安装架构仍须通过统一候选证据和用户批准后才能冻结。
