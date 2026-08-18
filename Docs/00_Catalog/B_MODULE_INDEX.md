# B 模块索引

最后检查：2026-08-18

| ID | B 模块 | 责任 | 依赖 | 状态 |
| --- | --- | --- | --- | --- |
| B-RND | Renderer Selection | V2-P0 渲染宿主选定实验:**优先原生 Direct2D/DirectComposition** 候选最小实现、§7.2 矩阵验证、选定决策 | 无(先行) | `PENDING — V2-P0 待建立` |
| B-PCR | Poster Core Runtime | 以选定渲染器重建桌面海报宿主,接入状态/托盘/生命周期 | B-RND | `PENDING` |
| B-COL | Collection and Launch | V1 的 Item/导入/启动/收纳路线(以 V1 文档为参考全重写) | B-PCR | `PENDING` |
| B-TPL / B-LAR / B-THA / B-AIA / B-GSD / B-DLC | 模板/布局/主题作者/AI/平台来源/分发 | 沿 V1 路线,依赖对应前序 | — | `PENDING` |

V1 已交付能力(Rust 核心、SQLite、托盘、单实例、生命周期)是 V2 的行为需求与参考实现;代码全重写,不设迁移模块(用户决策 2026-08-18)。
