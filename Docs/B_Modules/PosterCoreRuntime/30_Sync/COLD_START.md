# Poster Core Runtime 冷启动

按顺序阅读：

1. [`MODULE_PLAN.md`](../10_Plan/MODULE_PLAN.md)
2. [`C_WORKSTREAM_INDEX.md`](../00_Catalog/C_WORKSTREAM_INDEX.md)
3. [`CURRENT_HANDOFF.md`](CURRENT_HANDOFF.md)
4. C-PSS 关闭追溯：[`PosterStateStore`](../../../C_Workstreams/PosterStateStore/30_Sync/COLD_START.md)

C-PCB 与 C-PSS 已关闭，B-PCR 当前无活跃 C；C-LST 按全局路线排在 C-IBC/C-RLC 之后。控制中心 legacy 视觉参数回弹归后继 Template Platform 参数权威，不重开 C-PSS。接手后先运行 `git status --short --branch`，不得删除 legacy 来源/备份或扩大主屏单海报认证范围。
