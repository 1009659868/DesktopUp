# B 模块索引

最后检查：2026-08-18

状态：`B-RND ACTIVE — C-QTP`

| ID | 候选能力模块 | 未来责任 | 当前依赖状态 | 状态 |
| --- | --- | --- | --- | --- |
| B-RND | Renderer Selection | 渲染宿主选定：候选原型、证据矩阵、淘汰与选定决策 | 用户裁决路线甲 | `ACTIVE — C-QTP` |
| B-FND | Product Foundations | 统一状态、命令、修订、生命周期、诊断、安全和发布公共契约 | 需求已迁移，模块未设计 | `PENDING` |
| B-SRF | Runtime Surfaces | 桌面海报、控制中心、托盘、设置和后台核心的运行关系 | 技术选型未开始 | `PENDING` |
| B-ITM | Content Item Model | 内容发现、身份、启动、安全收纳、统计和失效处理 | Item 需求已迁移 | `PENDING` |
| B-LAY | Activity and Layout | 活跃度、排序、缩放、固定、分栏、弹性流和爆发排版 | 精确衰减公式待定 | `PENDING` |
| B-VIS | Poster Visual System | Night Ops、Neon HUD、Cinema Lightbox、响应式构图和交互命中 | 默认正式主题待定 | `PENDING` |
| B-THM | Theme Platform | 模板包、实例、参数、导入导出、隔离、回退和兼容 | 安全声明格式待定 | `PENDING` |
| B-AI | AI Assistance | BYOK、上下文、ChangePlan、工具权限、确认、撤销和错误隔离 | 自动执行边界待定 | `PENDING` |
| B-RLS | Install and Release | 单实例、自启、安装、升级、卸载、恢复、发布认证 | 实现阶段未开始 | `PENDING` |

## 说明

- 此表只说明未来需要哪些能力，不规定先开发哪一个；
- 技术候选比较已建立独立模块 B-RND（Renderer Selection），当前激活 C-QTP 原型；
- V1 的 Rust、SQLite、托盘、WebView 或其他代码只可作为行为和经验参考，不构成 V2 实现约束；
- 激活 B 模块时必须建立模块计划、依赖图、验收门和 C 工作流索引，并同步 `ACTIVE_PATH.md`。
