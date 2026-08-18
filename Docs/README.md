# Desktop-Up V2 文档入口

本目录维护 Desktop-Up V2 的需求、设计、决策、计划、验证和交接信息。当前阶段只冻结“软件应当做什么、如何表现、如何验收”，不把任何具体 UI 框架、渲染器、窗口宿主或编程语言写成既定方案。

## 当前状态

```text
A：需求与设计迁移基线已建立
B：能力模块计划尚未激活
C：实现工作流尚未激活
技术选型：尚未开始，等待后续单独立项
代码迁移：尚未开始
```

## 建议阅读顺序

1. [文档结构与阅读规则](DOCUMENT_STRUCTURE.md)
2. [当前活跃路径](00_Catalog/ACTIVE_PATH.md)
3. [V2 完整需求画像](A_Project/10_Plan/DESKTOPUP_V2_REQUIREMENTS_PROFILE_V1.md)
4. [海报画面与交互规范](A_Project/10_Plan/POSTER_VISUAL_AND_INTERACTION_V1.md)
5. [控制中心、托盘与海报运行关系](A_Project/10_Plan/RUNTIME_SURFACES_AND_TRAY_V1.md)
6. [内容 Item 活跃度与动态排版](A_Project/10_Plan/CONTENT_ITEM_ACTIVITY_LAYOUT_V1.md)
7. [自定义主题平台与 AI 接入](A_Project/10_Plan/THEME_PLATFORM_AND_AI_V1.md)
8. [需求迁移追踪表](A_Project/10_Plan/REQUIREMENTS_MIGRATION_TRACEABILITY_V1.md)
9. [当前交接](A_Project/30_Sync/CURRENT_HANDOFF.md)
10. [问题、风险与待定项](A_Project/30_Sync/KNOWN_ISSUES.md)

## 当前权威需求集

| 文档 | 回答的问题 | 状态 |
| --- | --- | --- |
| [完整需求画像](A_Project/10_Plan/DESKTOPUP_V2_REQUIREMENTS_PROFILE_V1.md) | 产品是谁、为谁服务、全局不变量、范围、状态、数据和验收是什么 | `ACTIVE` |
| [海报画面与交互规范](A_Project/10_Plan/POSTER_VISUAL_AND_INTERACTION_V1.md) | Night Ops 海报应如何构图、响应、穿透、折叠、拖动和呈现 | `ACTIVE` |
| [运行表面与托盘](A_Project/10_Plan/RUNTIME_SURFACES_AND_TRAY_V1.md) | 控制中心、托盘、海报、设置和后台核心如何分工与同步 | `ACTIVE` |
| [Item 活跃度与动态排版](A_Project/10_Plan/CONTENT_ITEM_ACTIVITY_LAYOUT_V1.md) | 内容如何发现、统计、排序、缩放、布局、固定和恢复 | `ACTIVE` |
| [主题平台与 AI](A_Project/10_Plan/THEME_PLATFORM_AND_AI_V1.md) | 主题如何定制、导入、隔离、回滚，以及 AI 如何安全地产生修改 | `ACTIVE` |
| [迁移追踪表](A_Project/10_Plan/REQUIREMENTS_MIGRATION_TRACEABILITY_V1.md) | V1 设计结论迁到了哪里、哪些冲突已归一、哪些仍待定 | `ACTIVE` |

## 历史证据与权威顺序

`Reference/V1_Design_Source/` 保存迁移时引用的 V1 设计原文快照，状态统一为 `HISTORICAL / READ-ONLY`。它们用于回答“结论从哪里来”，不能覆盖 V2 当前需求。

发生冲突时，按以下顺序判断：

1. 用户最新明确决策；
2. V2 当前活跃需求与已冻结验收标准；
3. V2 当前交接和已记录决策；
4. V2 问题/待定项台账；
5. V1 只读历史证据。

后续实现代码只有在完成验证后才能反向成为事实证据；在当前“仅文档”阶段，不存在以代码覆盖需求的情况。
