# Collection and Launch 模块计划

最后检查：2026-08-14

状态：`PLANNED — C-CIM ADVANCE PLAN READY`

上层计划：[`DESKTOPUP_V1_IMPLEMENTATION_PLAN.md`](../../../A_Project/10_Plan/DESKTOPUP_V1_IMPLEMENTATION_PLAN.md)

## 1. 责任

拥有 `CollectionItem`/`PosterItem` 正式契约、SQLite 仓库和数据驱动海报投影，以及 `.lnk/.url/.exe` 只读发现、解析和去重、导入预览、仅添加、安全收纳、保险库、两阶段恢复账本、启动、撤销、异常恢复和卸载恢复接口。

## 2. C 工作流

| C 工作流 | 结果 | 依赖 | 状态 |
| --- | --- | --- | --- |
| [`Collection Item Model and Projection`](../../../C_Workstreams/CollectionItemModelProjection/10_Plan/WORKSTREAM_PLAN.md) (`C-CIM`) | 正式 Item 契约、SQLite 仓库、CRUD、空/失效状态与数据驱动海报投影；演示卡片退出正式路径 | C-PSS, C-PSI | `ADVANCE PLAN READY — AFTER C-PSI` |
| Read-only Discovery (`C-ROD`) | 用户主动触发的只读解析、标准化、图标提取和去重；只产出候选项 | C-CIM | `PLANNED` |
| Preview and Add-only (`C-PAA`) | 逐项预览、冲突提示和仅添加闭环，经正式 Item 仓库进入海报 | C-ROD | `PLANNED` |
| Real Launch (`C-LCH`) | 真实启动、失效入口、错误反馈和启动结果记录 | C-PAA | `PLANNED` |
| Safe Vault Transaction (`C-SVT`) | 保险库、两阶段账本、安全收纳、撤销和故障注入 | C-PAA | `PLANNED` |
| Recovery and Uninstall Interface (`C-RCV`) | 异常事务恢复、卸载恢复接口和失败保留保险库 | C-SVT, C-LCH | `PLANNED` |

## 3. 模块完成信号

- 硬编码 `GAME_CATALOG` 不再作为正式 Item 数据源，零/一/多/失效 Item 均由仓库快照驱动；
- 不静默扫描，不移动 `.exe`、游戏本体、文件夹或普通文件；
- “仅添加”和“安全收纳”由用户逐批选择；
- 故障注入不丢失、不覆盖快捷方式；
- V1-P3 按“Item 契约/仓库/投影 → 只读发现 → 仅添加 → 真实启动 → Poster + Item 整合 Release 门”推进，全程不移动用户文件；
- V1-P4 再通过安全收纳、撤销、恢复和卸载接口，形成可追溯闭环。
