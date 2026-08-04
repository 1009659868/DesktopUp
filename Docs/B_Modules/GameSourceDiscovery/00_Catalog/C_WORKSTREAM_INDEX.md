# Game Source Discovery 工作流索引

| C 工作流 | 责任 | 依赖 | 状态 |
| --- | --- | --- | --- |
| Steam and Epic Discovery (`C-SED`) | Steam/Epic 只读发现和证据 | C-ROD | `PLANNED` |
| Xbox and Store Discovery (`C-XSD`) | Xbox/Store 发现和不可解析项 | C-ROD | `PLANNED` |
| Source Dedup and Manual Correction (`C-SDM`) | 多来源去重、优先级和人工修正 | C-SED, C-XSD | `PLANNED` |

来源适配器只读，不移动平台库或游戏本体。