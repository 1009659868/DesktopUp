# 文档登记表

最后检查：2026-08-21

## 当前权威文档

| 文档 | 职责 | 状态 |
| --- | --- | --- |
| `README.md` | 仓库入口、阶段边界和后续顺序 | `ACTIVE` |
| `Docs/README.md` | 文档入口与权威顺序 | `ACTIVE` |
| `Docs/DOCUMENT_STRUCTURE.md` | 需求重启后的阶段治理 | `ACTIVE` |
| `Docs/00_Catalog/REQUIREMENTS_INDEX.md` | 完整需求集唯一索引 | `FROZEN` |
| `Docs/00_Catalog/ACTIVE_PATH.md` | 冻结需求与当前技术选型入口 | `ACTIVE` |
| `Docs/A_Project/10_Requirements/DESKTOPUP_V2_REQUIREMENTS_PROFILE_V1.md` | 完整需求画像与全局验收基线 | `FROZEN REQUIREMENTS PROFILE` |
| `Docs/A_Project/10_Requirements/POSTER_VISUAL_AND_INTERACTION_V1.md` | 海报视觉与交互需求 | `FROZEN REQUIREMENTS CONTRACT` |
| `Docs/A_Project/10_Requirements/RUNTIME_SURFACES_AND_TRAY_V1.md` | 运行表面、托盘和生命周期需求 | `FROZEN REQUIREMENTS CONTRACT` |
| `Docs/A_Project/10_Requirements/CONTENT_ITEM_ACTIVITY_LAYOUT_V1.md` | Item、活跃度与动态排版需求 | `FROZEN REQUIREMENTS CONTRACT` |
| `Docs/A_Project/10_Requirements/THEME_PLATFORM_AND_AI_V1.md` | 主题平台、AI 与安全需求 | `FROZEN REQUIREMENTS CONTRACT` |
| `Docs/A_Project/10_Requirements/REQUIREMENTS_RISKS_AND_OPEN_QUESTIONS.md` | 历史反例、统一待定项和暂缓范围 | `FROZEN REQUIREMENTS SUPPORT` |
| `Docs/A_Project/10_Requirements/REQUIREMENTS_MIGRATION_TRACEABILITY_V1.md` | V1 → V2 迁移追溯 | `FROZEN EVIDENCE` |
| `Docs/A_Project/20_Technical_Selection/TECHNICAL_SELECTION_INDEX.md` | 技术选型阶段入口与任务状态 | `ACTIVE` |
| `Docs/A_Project/20_Technical_Selection/TECHNICAL_SELECTION_CHARTER_V1.md` | 选型原则、决策域和阶段门 | `ACTIVE` |
| `Docs/A_Project/20_Technical_Selection/CANDIDATE_REGISTER_V1.md` | 候选家族与事实卡 | `ACTIVE / FACT CHECKED` |
| `Docs/A_Project/20_Technical_Selection/UNIFIED_CANDIDATE_EVIDENCE_MATRIX_V1.md` | 统一硬门、评分与证据格式 | `ACTIVE / NOT RUN` |
| `Docs/A_Project/20_Technical_Selection/TS01_CANDIDATE_FACT_REVIEW_2026-08-20.md` | TS-01 官方与本机事实审查、静态筛选结果 | `COMPLETE` |
| `Docs/A_Project/20_Technical_Selection/DESKTOP_OVERLAY_HOST_PROBE_CHARTER_V1.md` | 公共原生桌面宿主探针合同与阶段门 | `TS-H01-E COMPLETE WITH RECORDED D EXCEPTIONS` |
| `Docs/A_Project/20_Technical_Selection/SELECTION_DECISION_RECORD_V1.md` | 选择、淘汰、敏感性和架构输入 | `NOT READY / NO WINNER` |
| `Docs/A_Project/20_Technical_Selection/TS_H01_D_EXECUTION_PLAN_V1.md` | D1/D2/D3 生命周期完整门执行顺序与独立判定 | `USER-ACCEPTED COMPLETE WITH RECORDED EXCEPTIONS` |
| `Docs/A_Project/20_Technical_Selection/TS_H01_D3_MATRIX_V1.md` | 主屏分辨率、DPI、工作区、非客户区和交互边界矩阵 | `D3-3 PASS / D3 USER-ACCEPTED COMPLETE WITH EXCEPTIONS` |
| `Docs/A_Project/20_Technical_Selection/TS_H01_E_DESKTOP_HOST_INTERFACE_FREEZE_PLAN_V1.md` | TS-H01 证据汇总、接口冻结和候选交接顺序 | `COMPLETE / E1-E6 FROZEN` |
| `Docs/A_Project/20_Technical_Selection/TS_H01_E2_CONTRACT_AUDIT_2026-08-21.md` | E2 需求/探针/证据一致性问题、裁决与 E3-A1 追踪 | `COMPLETE / E2 FROZEN WITH E3-A1` |
| `Docs/A_Project/20_Technical_Selection/TS_H01_E3_LIFECYCLE_CONTRACT_2026-08-21.md` | E3 surface、attach、恢复、竞态、幂等、detach 与 exiting 合同 | `COMPLETE / FROZEN` |
| `Docs/A_Project/20_Technical_Selection/TS_H01_E4_GEOMETRY_HIT_RENDER_CONTRACT_2026-08-21.md` | E4 几何、命中、render 与 gesture 原子合同 | `COMPLETE / FROZEN` |
| `Docs/A_Project/20_Technical_Selection/TS_H01_E5_DIAGNOSTIC_CONFORMANCE_FIXTURE_2026-08-21.md` | E5 公共事件、manifest、validator 与证据边界 | `COMPLETE / FIXTURE SELF-TEST PASS` |
| `Docs/A_Project/20_Technical_Selection/TS_H01_E6_CANDIDATE_HANDOFF_2026-08-21.md` | E6 候选 adapter 进入、证据隔离和退出规则 | `COMPLETE / FROZEN` |
| `Docs/A_Project/20_Technical_Selection/DESKTOP_HOST_PROTOTYPE_INTERFACE_V1.md` | 实现中立 DesktopHost 能力、状态、命中、诊断与错误合同 | `TS-H01-E COMPLETE / FROZEN` |
| `Docs/A_Project/20_Technical_Selection/TS_H01_E_COMPLETION_AUDIT_2026-08-21.md` | E1-E6 完成、历史例外和退出门审计 | `COMPLETE` |
| `prototype/desktop-host-win32/ts-h01-e5/` | 候选共用诊断事件 validator 与正反样本 | `FIXTURE SELF-TEST PASS / NO CANDIDATE RESULTS` |

## 治理与历史材料

| 路径 | 职责 | 状态 |
| --- | --- | --- |
| `Docs/Rules/` | 文档、Git、版本及发布治理底线 | `ACTIVE` |
| `Docs/Reference/V1_Design_Source/` | V1 原文快照与来源追溯 | `HISTORICAL / READ-ONLY` |
| `Docs/A_Project/10_Requirements/REQUIREMENTS_CONSISTENCY_AUDIT_2026-08-20.md` | 七份需求一致性审计证据，不产生第八份需求 | `COMPLETED / NON-NORMATIVE` |

## 明确不存在的当前文档

架构设计、总体制作计划、B 模块、C 工作流、当前交接和实现日志尚未激活，不应出现在权威登记表中。技术选型已激活，但没有候选获选。
