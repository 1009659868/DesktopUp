# Poster Core Runtime 工作流索引

| C 工作流 | 责任 | 依赖 | 状态 |
| --- | --- | --- | --- |
| [`PosterContractBaseline`](../../../C_Workstreams/PosterContractBaseline/10_Plan/WORKSTREAM_PLAN.md) (`C-PCB`) | 状态 schema、几何所有权、默认快照、自制标题栏和行为测试 | C-DRP | `COMPLETE` |
| [`PosterStateStore`](../../../C_Workstreams/PosterStateStore/10_Plan/WORKSTREAM_PLAN.md) (`C-PSS`) | SQLite schema、仓库接口、迁移和 JSON 桥接替换 | C-PCB | `COMPLETE — USER RELEASE RECOVERY PASS` |
| [`LargeSurfaceTransparency`](../../../C_Workstreams/LargeSurfaceTransparency/10_Plan/WORKSTREAM_PLAN.md) (`C-LST`) | 右侧大幅面、透明背景和位置恢复 | C-PSS, C-DRP | `COMPLETE — ABSORBED AND AUDITED` |
| [`PosterStateInteraction`](../../../C_Workstreams/PosterStateInteraction/10_Plan/WORKSTREAM_PLAN.md) (`C-PSI`) | 状态机、锁定、拖动、海报框拉伸与尺寸限制、命中和暂停同步 | C-LST | `ACTIVE — C-PSI-02 AUTHORITY` |
| Poster + Item Integration Release Gate (`C-PCRG`) | 正式 Item/真实启动链下的 Release、Windows 实机矩阵和性能封板 | C-PSI, C-LCH | `PLANNED AFTER USABLE MVP` |

C-PCB、C-PSS、前置 C-RLC、C-LST 与 C-PSI-01 已关闭。当前执行 C-PSI-02；C-PSI 关闭后按 `C-CIM → C-ROD → C-PAA → C-LCH → C-PCRG` 推进。
