# Desktop-Up V2 文档入口

七份需求已经用户确认冻结。当前正式进入全新的技术选型阶段；所有候选从未评估状态开始，旧技术选型、架构尝试、制作计划、工作流、交接和实现日志不具有执行权威。

## 当前状态

```text
需求基线：FROZEN — 2026-08-20
技术选型：ACTIVE — TS-H01-E COMPLETE WITH RECORDED D EXCEPTIONS / CANDIDATE ADAPTERS NEXT / NO WINNER
架构设计：NOT STARTED
制作计划：NOT STARTED
实施工作流：NOT STARTED
生产代码落地：NOT STARTED（当前仅有技术选型探针）
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
26. [选型决策记录](A_Project/20_Technical_Selection/SELECTION_DECISION_RECORD_V1.md)

## 权威顺序

发生冲突时按以下顺序判断：

1. 用户最新明确决策；
2. `A_Project/10_Requirements/` 下的冻结需求；
3. `A_Project/20_Technical_Selection/` 下的选型章程、候选与统一证据矩阵（仅约束选型过程，不改写需求）；
4. `Reference/V1_Design_Source/` 中的只读历史来源。

旧代码、旧二进制、旧验证结果和已删除的实施文档不能覆盖当前需求，也不能证明某项技术已被选定。

## 下一阶段的建立条件

需求已经冻结，技术选型已激活。只有候选通过统一 P0 硬门、加权比较、敏感性分析和用户批准后，才建立架构设计；架构冻结后才编制总体制作计划和 B/C 工作流；生产代码实现必须由已激活工作流授权。
