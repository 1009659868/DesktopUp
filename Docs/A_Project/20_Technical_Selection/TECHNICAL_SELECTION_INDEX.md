# Desktop-Up V2 技术选型阶段索引

状态：`QT IMPLEMENTATION ROUTE SELECTED BY USER / HISTORICAL MATRIX RETAINED / HANDOFF TO QTS ACTIVE`

建立日期：2026-08-20

## 1. 阶段目标

本阶段从已经冻结的七份产品需求反推技术候选，通过同一套可复现、可证伪的证据矩阵，选择能够承载 Desktop-Up V2 的技术组合。

本阶段不把任何历史原型、个人熟悉度、框架流行度或单张截图当作选型结论。Qt、原生 Win32、WinUI、WPF、Avalonia、WebView2、Slint 及后续新增候选均从未评估状态开始。

### 1.1 正交的系统描述输入

用户于 2026-08-24 明确将 Axiom Core 放在架构方法与技术研究层，用于描述整体拓扑、模块端口、关系和物理执行边界。[AX-BP-01～06](../30_Architecture/AXIOM_BLUEPRINT_INDEX.md) 已完成并冻结；该输入不选择 renderer/UI、StateStore、生产 runtime、进程或安装技术，也不改变 TS-C01 至 TS-C07 的 P0 未运行状态。

## 2. 冻结输入

1. [需求索引](../../00_Catalog/REQUIREMENTS_INDEX.md)
2. [完整需求画像](../10_Requirements/DESKTOPUP_V2_REQUIREMENTS_PROFILE_V1.md)
3. [海报视觉与交互](../10_Requirements/POSTER_VISUAL_AND_INTERACTION_V1.md)
4. [运行表面与托盘](../10_Requirements/RUNTIME_SURFACES_AND_TRAY_V1.md)
5. [内容 Item、活跃度与动态排版](../10_Requirements/CONTENT_ITEM_ACTIVITY_LAYOUT_V1.md)
6. [主题平台与 AI](../10_Requirements/THEME_PLATFORM_AND_AI_V1.md)
7. [需求风险与统一待定项](../10_Requirements/REQUIREMENTS_RISKS_AND_OPEN_QUESTIONS.md)
8. [迁移追踪证据](../10_Requirements/REQUIREMENTS_MIGRATION_TRACEABILITY_V1.md)
9. [一致性审计证据](../10_Requirements/REQUIREMENTS_CONSISTENCY_AUDIT_2026-08-20.md)
10. [AX-BP 蓝图索引](../30_Architecture/AXIOM_BLUEPRINT_INDEX.md)
11. [AX-BP-06 一致性审计与交接](../30_Architecture/AX_BP_06_CONSISTENCY_AUDIT_AND_HANDOFF_V1.md)

需求在本阶段是输入，不因某个候选难以实现而被静默修改。若发现需求本身必须变更，先暂停选型，形成需求变更决策并重新评估所有候选。

## 3. 当前文档

| 顺序 | 文档 | 职责 | 状态 |
| --- | --- | --- | --- |
| 1 | [技术选型章程](TECHNICAL_SELECTION_CHARTER_V1.md) | 范围、原则、决策域、阶段门和证据规则 | `ACTIVE` |
| 2 | [候选登记表](CANDIDATE_REGISTER_V1.md) | 候选家族、支撑技术域、事实卡和进入/退出规则 | `ACTIVE / FACT CHECKED` |
| 3 | [统一候选证据矩阵](UNIFIED_CANDIDATE_EVIDENCE_MATRIX_V1.md) | 所有候选共用的硬门、评分项、环境和证据格式 | `ACTIVE` |
| 4 | [TS-01 候选事实审查](TS01_CANDIDATE_FACT_REVIEW_2026-08-20.md) | 官方资料、本机工具链、公共宿主事实和静态筛选结果 | `COMPLETE` |
| 5 | [Desktop Overlay Host 探针章程](DESKTOP_OVERLAY_HOST_PROBE_CHARTER_V1.md) | 图标上方/普通程序下方宿主、Win+D、Explorer 与输入实验合同 | `TS-H01-E COMPLETE WITH RECORDED D EXCEPTIONS` |
| 6 | [TS-H01-D 执行计划](TS_H01_D_EXECUTION_PLAN_V1.md) | D1 显示桌面、D2 Explorer 重建、D3 显示边界的独立顺序与门 | `USER-ACCEPTED COMPLETE WITH RECORDED EXCEPTIONS` |
| 7 | [TS-H01-D3 边界矩阵](TS_H01_D3_MATRIX_V1.md) | 主屏四档 DPI、分辨率、工作区、非客户区与交互边界执行合同 | `D3-3 PASS / D3 USER-ACCEPTED COMPLETE WITH EXCEPTIONS` |
| 8 | [TS-H01-E 接口冻结计划](TS_H01_E_DESKTOP_HOST_INTERFACE_FREEZE_PLAN_V1.md) | 汇总宿主证据并冻结候选共用接口与一致性夹具 | `COMPLETE / E1-E6 FROZEN` |
| 9 | [TS-H01-E2 合同审计](TS_H01_E2_CONTRACT_AUDIT_2026-08-21.md) | 需求、探针、证据与接口草案的冲突检查和裁决 | `COMPLETE` |
| 10 | [TS-H01-E3 生命周期合同](TS_H01_E3_LIFECYCLE_CONTRACT_2026-08-21.md) | 所有权、首帧门、恢复、竞态、幂等、迟到事件和退出合同 | `COMPLETE / FROZEN` |
| 11 | [TS-H01-E4 原子投影合同](TS_H01_E4_GEOMETRY_HIT_RENDER_CONTRACT_2026-08-21.md) | 几何、命中、render、gesture、收起与显示变化的同 revision 合同 | `COMPLETE / FROZEN` |
| 12 | [TS-H01-E5 诊断夹具](TS_H01_E5_DIAGNOSTIC_CONFORMANCE_FIXTURE_2026-08-21.md) | 事件 schema、场景 manifest、validator 与证据边界 | `COMPLETE / FIXTURE SELF-TEST PASS` |
| 13 | [TS-H01-E6 候选交接](TS_H01_E6_CANDIDATE_HANDOFF_2026-08-21.md) | adapter 进入门、证据隔离、fail-fast 与退出规则 | `COMPLETE / FROZEN` |
| 14 | [DesktopHost 原型接口](DESKTOP_HOST_PROTOTYPE_INTERFACE_V1.md) | 实现中立能力、生命周期、几何、命中、诊断和错误语义 | `TS-H01-E COMPLETE / FROZEN` |
| 15 | [TS-H01-E 完成审计](TS_H01_E_COMPLETION_AUDIT_2026-08-21.md) | E1-E6 产出、例外、夹具自测与退出门核对 | `COMPLETE` |
| 16 | [TS-02 renderer adapter/P0 执行计划](TS_02_RENDERER_ADAPTER_P0_EXECUTION_PLAN_V1.md) | 候选顺序、R0～R6、公共不变量、证据与裁决 | `ACTIVE / TS-C01 R5-01～05 COMPLETE / FAIL RECOMMENDED / R6 READY` |
| 17 | [TS-C01 Qt Quick 进入门审计](TS_C01_QT_QUICK_ENTRY_AUDIT_2026-08-24.md) | Qt 版本、工具链、surface/ownership 和进入缺口 | `6.8.3 / R5 COMPLETE / FAIL RECOMMENDED / USER DECISION PENDING` |
| 18 | [TS-C01 R4 人工 P0 手册](TS_C01_R4_MANUAL_P0_RUNBOOK_V1.md) | R4-A～D 完整矩阵、受控会话、真实夹具缺口和 fail-fast | `CLOSED / COUNT + WORKING-SET + AUTOSTART + DEVICE-RESTART EXCEPTIONS RETAINED` |
| 19 | [TS-C01 R4-D4 当前用户生命周期计划](TS_C01_R4_D4_CURRENT_USER_LIFECYCLE_PLAN_V1.md) | user-level 安装、单实例、自启、状态、重启与卸载清理 | `CLOSED / EXECUTED SCOPE PASS / AUTOSTART DEFERRED / DEVICE RESTART SKIPPED` |
| 20 | [TS-C01 R5 证据收敛与裁决准备计划](TS_C01_R5_EVIDENCE_CONVERGENCE_AND_DECISION_PREP_PLAN_V1.md) | 证据盘点、矩阵映射、缺口收敛和候选处置准备 | `COMPLETE / R5-01～05` |
| 21 | [TS-C01 R5-01 原始证据盘点](TS_C01_R5_01_EVIDENCE_INVENTORY_2026-08-26.md) | 原始文件、哈希、构建血缘和可采性边界 | `COMPLETE / P0 NOT RUN` |
| 22 | [TS-C01 R5-02 统一矩阵映射](TS_C01_R5_02_UNIFIED_MATRIX_MAPPING_V1.md) | 41 项硬门的严格结果、证据和缺口 | `COMPLETE / 2 PASS / 1 FAIL / 34 UNKNOWN / 4 NOT RUN` |
| 23 | [TS-C01 Candidate Evidence](Evidence/TS-C01/README.md) | 正式证据索引、哈希、保留策略和确定结果记录 | `R5-03 COMPLETE` |
| 24 | [TS-C01 R5-04 候选处置审计](TS_C01_R5_04_CANDIDATE_DISPOSITION_AUDIT_V1.md) | E6 PASS/FAIL/CONDITIONAL/WITHDRAWN 审计 | `COMPLETE / FAIL RECOMMENDED` |
| 25 | [TS-C01 R5-05 R6 交接输入](TS_C01_R5_05_R6_HANDOFF_INPUT_V1.md) | 用户处置选项、禁止路径和下一候选入口 | `COMPLETE / R6 READY` |
| 26 | [选型决策记录](SELECTION_DECISION_RECORD_V1.md) | 最终比较、敏感性分析、淘汰理由和架构输入 | `QT ROUTE SELECTED / SUPPORT DOMAINS PENDING` |
| 26a | [R6 Qt 实施路线决定](TS_C01_R6_QT_IMPLEMENTATION_DECISION_2026-08-26.md) | 用户选择、历史证据边界、工作集处理和实施入口 | `APPROVED` |
| 26b | [Qt 方案总执行计划](../30_Architecture/QT_SOLUTION_EXECUTION_PLAN_V1.md) | Qt 生产路线、阶段门与立即动作 | `ACTIVE / QTS-01 NEXT` |
| 27 | [证据目录规则](Evidence/README.md) | 原始数据、视频、截图、日志和复现材料的组织方式 | `ACTIVE / TS-C01 FORMAL INDEX PRESENT` |

## 4. 阶段任务

| ID | 任务 | 产出 | 状态 |
| --- | --- | --- | --- |
| TS-00 | 冻结需求并激活技术选型 | 冻结状态、章程、候选表、统一矩阵 | `COMPLETE` |
| TS-01 | 候选事实审查 | 官方支持范围、版本、许可证、构建与部署事实卡 | `COMPLETE` |
| TS-02 | 公共 Desktop Overlay Host 探针，再接入海报渲染候选 | 先证明精确层级、Win+D、Explorer 重建和输入；再做同构渲染适配器 | `CLOSED BY USER QT IMPLEMENTATION DECISION / MATRIX EXCEPTION RETAINED` |
| TS-03 | 控制中心、核心、状态、IPC、主题和安装支撑域审查 | 各域短名单与兼容约束 | `HANDED TO QTS-02/QTS-05/QTS-07/QTS-09` |
| TS-04 | 统一矩阵复测 | 同设备、同夹具、同脚本的完整证据包 | `DEFERRED UNTIL QT PRODUCT SLICES EXIST` |
| TS-05 | 选择与交接 | 决策记录、被淘汰理由、残余风险、架构输入清单 | `USER DECISION COMPLETE / QTS HANDOFF ACTIVE` |

## 5. 当前下一动作

R6 已由用户决定选择 Qt 实施路线，历史矩阵和 `HG-PERF-02` 失败继续保留。技术选型主线交给 [Qt 方案总执行计划](../30_Architecture/QT_SOLUTION_EXECUTION_PLAN_V1.md)；下一动作是 `QTS-01-01` 工作集测量合同与分层基线。

## 6. 阶段退出门

只有同时满足以下条件，技术选型才可标记 `COMPLETE`：

- 每个保留候选均有完整事实卡；
- 每个进入实测的候选使用相同夹具和矩阵；
- 所有 P0 硬门均有原始证据，未知不能当作通过；
- 任一淘汰均有可复现失败或权威资料依据；
- 通过硬门的候选才进入加权评分；
- 完成权重敏感性分析，结论不依赖单个主观分值；
- 选型决策记录明确“选择什么、为什么、拒绝什么、仍有什么风险”；
- 输出架构设计阶段所需约束，但不在本阶段伪造最终架构。
