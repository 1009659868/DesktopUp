# Poster Core Runtime 工作流索引

| C 工作流 | 责任 | 依赖 | 状态 |
| --- | --- | --- | --- |
| [`PosterContractBaseline`](../../../C_Workstreams/PosterContractBaseline/10_Plan/WORKSTREAM_PLAN.md) (`C-PCB`) | 状态 schema、几何所有权、默认快照、自制标题栏和行为测试 | C-DRP | `COMPLETE` |
| [`PosterStateStore`](../../../C_Workstreams/PosterStateStore/10_Plan/WORKSTREAM_PLAN.md) (`C-PSS`) | SQLite schema、仓库接口、迁移和 JSON 桥接替换 | C-PCB | `COMPLETE — USER RELEASE RECOVERY PASS` |
| Large Surface and Transparency (`C-LST`) | 右侧大幅面、透明背景和位置恢复 | C-PSS, C-DRP | `PLANNED AFTER C-IBC/C-RLC` |
| Poster State and Interaction (`C-PSI`) | 状态机、锁定、拖动、海报框拉伸与尺寸限制、命中和暂停同步 | C-LST | `PLANNED` |
| Poster Core Release Gate (`C-PCRG`) | Release、Windows 实机矩阵和性能封板 | C-PSI | `PLANNED` |

C-PCB 与 C-PSS 已关闭。B-PCR 当前无活跃 C；按全局路线先进入 C-IBC/C-RLC，之后才激活 C-LST。
