# Collection and Launch 工作流索引

| C 工作流 | 责任 | 依赖 | 状态 |
| --- | --- | --- | --- |
| [`CollectionItemModelProjection`](../../../C_Workstreams/CollectionItemModelProjection/10_Plan/WORKSTREAM_PLAN.md) (`C-CIM`) | 正式 Item 契约、SQLite 仓库、CRUD 和数据驱动海报投影 | C-PSS, C-PSI | `ADVANCE PLAN READY — AFTER C-PSI` |
| Read-only Discovery (`C-ROD`) | 用户主动触发的只读解析、标准化、图标和去重；只产出候选项 | C-CIM | `PLANNED` |
| Preview and Add-only (`C-PAA`) | 预览、冲突提示和仅添加，经正式 Item 仓库进入海报 | C-ROD | `PLANNED` |
| Real Launch (`C-LCH`) | 真实启动、失效入口、错误反馈和结果记录 | C-PAA | `PLANNED` |
| Safe Vault Transaction (`C-SVT`) | 保险库、账本、安全收纳、撤销和故障注入 | C-PAA | `PLANNED` |
| Recovery and Uninstall Interface (`C-RCV`) | 异常恢复、卸载恢复接口和失败保留保险库 | C-SVT, C-LCH | `PLANNED` |

`C-CIM` 只使用隔离夹具和应用自有 SQLite，不扫描桌面；`C-LCH` 只启动已登记目标，不依赖保险库。真实文件移动不得早于 C-SVT，并必须逐批确认。
