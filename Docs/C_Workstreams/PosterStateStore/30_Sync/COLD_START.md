# Poster State Store 冷启动

按顺序阅读：

1. [`WORKSTREAM_PLAN.md`](../10_Plan/WORKSTREAM_PLAN.md)
2. [`TASK_INDEX.md`](../00_Catalog/TASK_INDEX.md)
3. [`CURRENT_HANDOFF.md`](CURRENT_HANDOFF.md)
4. [`CODE_ANCHORS.md`](../00_Catalog/CODE_ANCHORS.md)
5. 最新日志：[`2026-08-12_cpss05_release_recovery_gate_activation.md`](../20_Log/2026-08/2026-08-12_cpss05_release_recovery_gate_activation.md)

接手后先运行 `git status --short --branch`、`pnpm run check` 与 Rust 测试。C-PSS-04 已关闭，当前只推进 C-PSS-05 隔离恢复矩阵；下一轮先按用户要求完成 Git 同步。不得在无恢复副本时损坏真实数据库，不删除 legacy 读取能力、备份或原始来源，也不把主屏单海报以外的能力视为已认证。
