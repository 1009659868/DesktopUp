# Poster Contract Baseline 冷启动

按顺序阅读：

1. [`WORKSTREAM_PLAN.md`](../10_Plan/WORKSTREAM_PLAN.md)
2. [`TASK_INDEX.md`](../00_Catalog/TASK_INDEX.md)
3. [`CURRENT_HANDOFF.md`](CURRENT_HANDOFF.md)
4. [`CODE_ANCHORS.md`](../00_Catalog/CODE_ANCHORS.md)
5. 最新日志：[`2026-08-11_cpcb05_contract_seal_release_candidate.md`](../20_Log/2026-08/2026-08-11_cpcb05_contract_seal_release_candidate.md)

接手后先运行 `git status --short --branch`、`pnpm run check` 与 Rust 测试。C-PCB-01..04 已关闭，C-PCB-05 自动门禁、C-PSS 输入和冷启动 Z 序探针已通过，当前只等待主屏单海报 Release 人工回归；不要提前激活 C-PSS、实现 SQLite、Shadow DOM 或模板安装。
