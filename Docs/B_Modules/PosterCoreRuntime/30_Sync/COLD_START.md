# Poster Core Runtime 冷启动

按顺序阅读：

1. [`MODULE_PLAN.md`](../10_Plan/MODULE_PLAN.md)
2. [`C_WORKSTREAM_INDEX.md`](../00_Catalog/C_WORKSTREAM_INDEX.md)
3. [`CURRENT_HANDOFF.md`](CURRENT_HANDOFF.md)
4. 当前 C 工作流 [`PosterStateStore`](../../../C_Workstreams/PosterStateStore/30_Sync/COLD_START.md)

接手后先运行 `git status --short --branch`。C-PCB 与 C-PSS-04 已关闭，C-PSS-05 是当前唯一活跃 C；Git 同步检查点 `b447f1f` 已创建，本轮推送后用隔离副本推进恢复矩阵。门关闭前不删除 legacy 读取能力、备份或原始来源。
