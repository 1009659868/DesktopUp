# Desktop-Up V2

Desktop-Up V2 是面向 Windows 11 的桌面海报、内容入口与个性化主题平台重构项目。

当前仓库阶段是“需求与设计迁移基线”：把 V1 已讨论、已确认和仍待确认的产品设计完整迁入 V2，并建立可追溯的权威文档。当前阶段只处理文档，不迁移实现代码，不提前选定 Qt、WebView、DirectComposition 或其他渲染与窗口技术。

文档入口：[Docs/README.md](Docs/README.md)

当前本地分支：`impl/Refactor`

## 当前边界

- 已纳入：海报画面与交互、控制中心/托盘/海报关系、内容 Item 活跃度与动态排版、主题平台与 AI 接入、状态和发布底线。
- 未开始：技术选型、架构验证、代码重构、安装包制作和发布验证。
- V1 设计原文保存在 `Docs/Reference/V1_Design_Source/`，只用于追溯；V2 当前需求以 `Docs/A_Project/10_Plan/` 下的 V2 文档为准。
