# Poster Core Runtime 冷启动

按顺序阅读：

1. [`MODULE_PLAN.md`](../10_Plan/MODULE_PLAN.md)
2. [`C_WORKSTREAM_INDEX.md`](../00_Catalog/C_WORKSTREAM_INDEX.md)
3. [`CURRENT_HANDOFF.md`](CURRENT_HANDOFF.md)
4. C-PSS 关闭追溯：[`PosterStateStore`](../../../C_Workstreams/PosterStateStore/30_Sync/COLD_START.md)
5. C-LST 关闭追溯：[`LargeSurfaceTransparency`](../../../C_Workstreams/LargeSurfaceTransparency/30_Sync/COLD_START.md)
6. C-PSI 当前入口：[`PosterStateInteraction`](../../../C_Workstreams/PosterStateInteraction/30_Sync/COLD_START.md)

C-PCB、C-PSS、C-IBC、C-RLC、C-LST 与 C-PSI-01 已关闭。B-PCR/C-PSI-02 当前活动，只实现冻结审计矩阵中的状态权威缺口；C-PSI 关闭后按 `C-CIM → C-ROD → C-PAA → C-LCH → C-PCRG` 推进，其中 C-PCRG 必须使用正式 Item 仓库和真实启动链。控制中心 legacy 视觉参数回弹归后继 Template Platform 参数权威，不重开 C-PSS。接手后先运行 `git status --short --branch`，不得删除 legacy 来源/备份或扩大主屏单海报认证范围。
