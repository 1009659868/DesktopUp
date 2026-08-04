# Game Source Discovery 模块计划

最后检查：2026-08-04

状态：`PLANNED`

上层计划：[`DESKTOPUP_V1_IMPLEMENTATION_PLAN.md`](../../../A_Project/10_Plan/DESKTOPUP_V1_IMPLEMENTATION_PLAN.md)

## 1. 责任

拥有 Steam、Epic、Xbox/Store 等本地来源适配器、只读证据、来源归一化、多来源去重和人工修正。

不负责移动平台库或游戏本体。

## 2. C 工作流

| C 工作流 | 结果 | 依赖 | 状态 |
| --- | --- | --- | --- |
| Steam and Epic Discovery (`C-SED`) | Steam/Epic 只读发现与来源证据 | C-ROD | `PLANNED` |
| Xbox and Store Discovery (`C-XSD`) | Xbox/Store 发现与不可解析项处理 | C-ROD | `PLANNED` |
| Source Dedup and Manual Correction (`C-SDM`) | 多来源去重、优先级和人工修正 | C-SED, C-XSD | `PLANNED` |

## 3. 模块完成信号

- 来源适配器只读、可禁用、可诊断；
- 同一游戏的多来源记录可解释合并；
- 用户可修正错误匹配；
- 不依赖在线账户或云同步。
