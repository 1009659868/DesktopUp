# Poster Core Runtime 冷启动

按顺序阅读：

1. [`MODULE_PLAN.md`](../10_Plan/MODULE_PLAN.md)
2. [`C_WORKSTREAM_INDEX.md`](../00_Catalog/C_WORKSTREAM_INDEX.md)
3. [`CURRENT_HANDOFF.md`](CURRENT_HANDOFF.md)
4. 当前 C 工作流 [`PosterContractBaseline`](../../../C_Workstreams/PosterContractBaseline/30_Sync/COLD_START.md)

接手后先运行 `git status --short --branch`。C-DRP 已在“当前发布基线只认证主屏单海报；多显示器、混合 DPI、负坐标副屏、主屏切换、跨屏重启和热插拔未认证”的明确限制下关闭。当前只推进 C-PCB，不提前实现 C-PSS 的 SQLite 仓库，也不改写已经通过人工验证的 Release 视觉与桌面交互。
