# Desktop Runtime Productionization 冷启动

按顺序阅读：

1. [`WORKSTREAM_PLAN.md`](../10_Plan/WORKSTREAM_PLAN.md)
2. [`TASK_INDEX.md`](../00_Catalog/TASK_INDEX.md)
3. [`CURRENT_HANDOFF.md`](CURRENT_HANDOFF.md)
4. [`CODE_ANCHORS.md`](../00_Catalog/CODE_ANCHORS.md)
5. 最近一份 [20_Log](../20_Log/2026-08/2026-08-13_cdrp04_shell_topmost_regression_fix.md)

接手后先运行 `git status --short --branch`。C-DRP-01..06 及 2026-08-13 无 TOPMOST 维护矩阵均已由用户确认通过。该维护不重开多屏范围；用户接受“当前发布基线只认证主屏单海报”的平台限制，多显示器、混合 DPI、负坐标副屏、主屏切换、跨屏重启和热插拔未认证。当前活动链已进入 [`InstallBaselineContract`](../../InstallBaselineContract/30_Sync/COLD_START.md)。
