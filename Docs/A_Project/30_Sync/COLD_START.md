# A 层冷启动

## 阅读顺序

1. `Docs/README.md`
2. `Docs/00_Catalog/ACTIVE_PATH.md`
3. `Docs/A_Project/10_Plan/DESKTOPUP_V2_REQUIREMENTS_PROFILE_V1.md`
4. `Docs/A_Project/10_Plan/POSTER_VISUAL_AND_INTERACTION_V1.md`
5. `Docs/A_Project/10_Plan/RUNTIME_SURFACES_AND_TRAY_V1.md`
6. `Docs/A_Project/10_Plan/CONTENT_ITEM_ACTIVITY_LAYOUT_V1.md`
7. `Docs/A_Project/10_Plan/THEME_PLATFORM_AND_AI_V1.md`
8. `Docs/A_Project/10_Plan/REQUIREMENTS_MIGRATION_TRACEABILITY_V1.md`
9. `Docs/A_Project/10_Plan/TECH_SELECTION_CANDIDATES_V1.md`
10. `Docs/A_Project/30_Sync/CURRENT_HANDOFF.md`
11. `Docs/A_Project/30_Sync/KNOWN_ISSUES.md`
12. `Docs/B_Modules/RendererSelection/30_Sync/COLD_START.md`
13. `Docs/C_Workstreams/QtQuickPrototype/30_Sync/COLD_START.md`

仅当需要追溯结论来源或核对迁移遗漏时，再阅读 `Docs/Reference/V1_Design_Source/`；不得把其中旧架构、旧公式或旧任务顺序直接当作 V2 决策。

## 关键背景

- V2 源于 V1 透明桌面海报在顶部条带、窗口层级、编辑交互等方面长期出现难以稳定根治的问题。
- 需求迁移已冻结并获用户确认；技术选型已按用户裁决进入路线甲（Qt 优先），当前唯一实现工作流为 C-QTP 原型。
- 当前 V2 仓库没有实现权威；渲染技术尚未选定，候选按证据矩阵验证后决定。
- 本轮迁移不是“摘要 V1”，而是把产品需求重新表述为独立、可验证、技术中立的 V2 合同。

## 接手约束

- 原型实现需用户明确批准；未经批准不写产品功能代码、不迁移 V1 代码。
- 不删除 V1 历史证据；发现冲突时在迁移追踪表和问题台账中登记。
- 任何未来技术方案都必须证明满足需求画像中的桌面层级、透明度、命中、动态 Item、主题隔离、AI 权限和可诊断性要求。
