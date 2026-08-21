# Desktop-Up V2 需求索引

状态：`FROZEN — 2026-08-20`

最后检查：2026-08-20

本索引只登记产品需求、验收边界、风险与来源追溯。它不包含技术选型、架构决定、制作计划或实现工作流。

| 顺序 | 权威文档 | 职责 |
| --- | --- | --- |
| 1 | [`DESKTOPUP_V2_REQUIREMENTS_PROFILE_V1.md`](../A_Project/10_Requirements/DESKTOPUP_V2_REQUIREMENTS_PROFILE_V1.md) | 产品目标、范围、全局不变量、数据、安全、Windows 集成、性能和总体验收 |
| 2 | [`POSTER_VISUAL_AND_INTERACTION_V1.md`](../A_Project/10_Requirements/POSTER_VISUAL_AND_INTERACTION_V1.md) | 海报视觉、构图、几何、状态、命中、拖动、缩放、锁定与收起 |
| 3 | [`RUNTIME_SURFACES_AND_TRAY_V1.md`](../A_Project/10_Requirements/RUNTIME_SURFACES_AND_TRAY_V1.md) | 海报、控制中心、托盘、设置、后台核心及生命周期关系 |
| 4 | [`CONTENT_ITEM_ACTIVITY_LAYOUT_V1.md`](../A_Project/10_Requirements/CONTENT_ITEM_ACTIVITY_LAYOUT_V1.md) | Item 模型、启动语义、活跃度、排序、缩放、固定和动态排版 |
| 5 | [`THEME_PLATFORM_AND_AI_V1.md`](../A_Project/10_Requirements/THEME_PLATFORM_AND_AI_V1.md) | 主题定制、导入导出、隔离、回退、BYOK AI 与变更权限 |
| 6 | [`REQUIREMENTS_RISKS_AND_OPEN_QUESTIONS.md`](../A_Project/10_Requirements/REQUIREMENTS_RISKS_AND_OPEN_QUESTIONS.md) | 历史反例、必须证伪的风险、未冻结产品问题和暂缓范围 |
| 7 | [`REQUIREMENTS_MIGRATION_TRACEABILITY_V1.md`](../A_Project/10_Requirements/REQUIREMENTS_MIGRATION_TRACEABILITY_V1.md) | V1 到 V2 的来源映射、冲突归一和遗漏检查 |

## 审计证据

[2026-08-20 完整需求一致性审计](../A_Project/10_Requirements/REQUIREMENTS_CONSISTENCY_AUDIT_2026-08-20.md) 记录重复、矛盾、遗漏和实现假设的修订过程。该文件是非规范性证据，不构成第八份需求权威。

## 权威边界

- 上述七份文档共同组成 2026-08-20 用户确认冻结的完整需求基线。
- 任何需求变更必须形成显式决策，并重评受影响的技术候选、证据和下游阶段。
- 技术名称只能作为需求文档中的历史说明或待比较对象，不能据此认定已经选型。
- 后续必须依次建立技术选型、架构设计、制作计划与工作流；在相应阶段正式激活前，不存在任何默认技术路线或实施任务。
- `Docs/Reference/V1_Design_Source/` 仅用于来源追溯，不能覆盖 V2 当前需求。
