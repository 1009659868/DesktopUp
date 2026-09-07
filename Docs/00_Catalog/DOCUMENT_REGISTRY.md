# DesktopUp V2 V3 文档登记表

最后检查：2026-09-07

| 文档 | 职责 | 状态 |
| --- | --- | --- |
| `Docs/00_Catalog/REQUIREMENTS_INDEX.md` | 七份需求权威入口 | `FROZEN` |
| `Docs/A_Project/10_Requirements/DESKTOPUP_V2_REQUIREMENTS_PROFILE_V1.md` | 项目级完整需求画像 | `USER REVISION 2026-09-07` |
| `Docs/A_Project/10_Requirements/POSTER_VISUAL_AND_INTERACTION_V1.md` | 海报视觉、几何、命中与交互 | `FROZEN` |
| `Docs/A_Project/10_Requirements/RUNTIME_SURFACES_AND_TRAY_V1.md` | 海报、控制中心、托盘和生命周期 | `FROZEN` |
| `Docs/A_Project/10_Requirements/CONTENT_ITEM_ACTIVITY_LAYOUT_V1.md` | Item、活跃度与动态排版 | `FROZEN` |
| `Docs/A_Project/10_Requirements/THEME_PLATFORM_AND_AI_V1.md` | 主题平台、热更新与 AI | `FROZEN` |
| `Docs/A_Project/10_Requirements/REQUIREMENTS_RISKS_AND_OPEN_QUESTIONS.md` | 风险、待定与延期范围 | `FROZEN SUPPORT` |
| `Docs/A_Project/10_Requirements/REQUIREMENTS_MIGRATION_TRACEABILITY_V1.md` | 需求来源追溯 | `FROZEN EVIDENCE` |
| `Docs/A_Project/10_Requirements/REQUIREMENTS_CONSISTENCY_AUDIT_2026-08-20.md` | 需求一致性审计 | `NON-NORMATIVE EVIDENCE` |
| `Docs/A_Project/20_Architecture/DESKTOPUP_V2_V3_ARCHITECTURE_V1.md` | DeskBox 路线、总体拓扑、状态和运行流 | `DESIGN BASELINE 2026-09-07` |
| `Docs/A_Project/20_Architecture/DESKTOPUP_V2_V3_MODULE_CATALOG_V1.md` | 模块分类、职责、端口和依赖规则 | `DESIGN BASELINE 2026-09-07` |
| `Docs/A_Project/30_Plans/DESKTOPUP_V2_V3_IMPLEMENTATION_PLAN_V1.md` | V3 分阶段制作与验证计划 | `M03 AUTO PASS / SHORT MANUAL PENDING` |
| `Docs/A_Project/40_Implementation/V3-01/DEPENDENCY_BASELINE.md` | V3-01 工具链与依赖冻结 | `FROZEN` |
| `Docs/A_Project/40_Implementation/V3-01/DESKBOX_PROVENANCE_LEDGER.md` | DeskBox 参考/移植来源登记 | `ACTIVE` |
| `Docs/A_Project/40_Implementation/V3-01/CODE_ANCHORS.md` | V3-01 代码锚点与物理边界 | `CURRENT` |
| `Docs/A_Project/40_Implementation/V3-01/VERIFICATION.md` | V3-01 自动证据和人工门 | `COMPLETE` |
| `Docs/A_Project/40_Implementation/V3-02/CODE_ANCHORS.md` | V3-02 桌面宿主代码锚点 | `CURRENT` |
| `Docs/A_Project/40_Implementation/V3-02/DESKBOX_PROVENANCE_LEDGER.md` | V3-02 DeskBox 参考来源 | `ACTIVE` |
| `Docs/A_Project/40_Implementation/V3-02/DESKBOX_RECOVERY_REFERENCE_AND_V3_STRENGTHENING.md` | Explorer/显示恢复的 DeskBox 吸收项、V3 补强和验收合同 | `RECORDED INPUT` |
| `Docs/A_Project/40_Implementation/V3-02/VERIFICATION.md` | V3-02 自动证据和人工门 | `COMPLETE — SINGLE-POSTER BASELINE` |
| `Docs/A_Project/40_Implementation/V3-04/STATE_STORE_SCHEMA_V1.md` | V3-04-01 SQLite 状态底座合同 | `FROZEN` |
| `Docs/A_Project/40_Implementation/V3-04/POSTER_SETTINGS_REPOSITORY_V2.md` | V3-04-02 Poster/Settings schema 与 repository 合同 | `FROZEN — V3 ADDITIVE EXTENSION` |
| `Docs/A_Project/40_Implementation/V3-04/POSTER_CATALOG_SCHEMA_V3.md` | M01 多海报实体、迁移、版本与目录仓储实际合同 | `PASS — DATA CONTRACT` |
| `Docs/A_Project/40_Implementation/V3-04/COMMAND_QUERY_PROJECTION_V1.md` | V3-04-03 单海报 command/query 与 projection 历史合同 | `HISTORICAL BASELINE` |
| `Docs/A_Project/40_Implementation/V3-04/TARGETED_COMMAND_PROJECTION_V2.md` | M02 目标命令、实体读集、批量/回执、集合投影和实际边界 | `CURRENT — AUTO PASS` |
| `Docs/A_Project/40_Implementation/V3-04/M03_SESSION_RECOVERY.md` | M03 registry、共享恢复、取消/局部失败/资源合同、真实进程证据与手工入口 | `AUTO PASS / MANUAL PENDING` |
| `Docs/A_Project/40_Implementation/V3-04/CODE_ANCHORS.md` | V3-04 代码锚点 | `CURRENT` |
| `Docs/A_Project/40_Implementation/V3-04/VERIFICATION.md` | V3-04 自动证据与人工门 | `M03 AUTO PASS / SHORT MANUAL PENDING` |
| `LICENSE` | DesktopUp V3 GPL-3.0-only 正文 | `ACTIVE` |
| `THIRD_PARTY_NOTICES.md` | 第三方依赖与许可证通知 | `ACTIVE` |

## 新增细化与交接

| 文档 | 职责 | 状态 |
| --- | --- | --- |
| [GENERAL_CONTENT_MULTI_POSTER_CONTRACT_V1](../A_Project/20_Architecture/GENERAL_CONTENT_MULTI_POSTER_CONTRACT_V1.md) | 实例/共享边界、数据/CAS、生命周期、类型能力、主题与 DeskBox 来源 | M01/M02/M03 IMPLEMENTED / M03 MANUAL PENDING |
| [V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1](../A_Project/30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md) | M 前置、后续逐项输入/依赖/失败/验收与日用里程碑 | M03 AUTO PASS / SHORT MANUAL PENDING |
| [V3-04 HANDOFF](../A_Project/40_Implementation/V3-04/HANDOFF.md) | 已验证范围、未实现缺口与立即下一步 | CURRENT |
| [M04_CONTROL_CENTER_SELECTION](../A_Project/40_Implementation/V3-04/M04_CONTROL_CENTER_SELECTION.md) | 中心目录/目标选择实际合同、测试/产物、短人工与启动 | AUTO PASS / MANUAL PENDING |
| [DESKBOX_SPATIAL_REFERENCE](../A_Project/40_Implementation/V3-04/DESKBOX_SPATIAL_REFERENCE.md) | 吸附、组移动和展开计算来源/指纹/测试及适配取舍 | REFERENCE ABSORBED / SPATIAL IMPLEMENTATION PENDING |
| [POSTER_SPATIAL_LAYOUT_AND_EXPANSION_V1](../A_Project/20_Architecture/POSTER_SPATIAL_LAYOUT_AND_EXPANSION_V1.md) | DeskBox源码依据、碰撞/推让/两阶段展开/条件归位、CAS与失败边界；中心选择缺口和M04出口 | DESIGN BASELINE / IMPLEMENTATION PLANNED |
| [V3_CURRENT 图说明](../A_Project/20_Architecture/Diagrams/V3_CURRENT/README.md) | 原图为单海报代码快照，不冒充多海报设计 | HISTORICAL IMPLEMENTATION SNAPSHOT |

旧技术选型、旧架构、旧证据与实现文件已从 V3 工作树移除；仍可通过 Git 历史恢复，但不再参与当前权威判断。
