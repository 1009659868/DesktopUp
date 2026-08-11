# Poster Core Runtime 工作流索引

| C 工作流 | 责任 | 依赖 | 状态 |
| --- | --- | --- | --- |
| [`PosterContractBaseline`](../../../C_Workstreams/PosterContractBaseline/10_Plan/WORKSTREAM_PLAN.md) (`C-PCB`) | 状态 schema、几何所有权、默认快照、自制标题栏和行为测试 | C-DRP | `ACTIVE` |
| Poster State Store (`C-PSS`) | SQLite schema、仓库接口、迁移和 JSON 桥接替换 | C-PCB | `PLANNED` |
| Large Surface and Transparency (`C-LST`) | 右侧大幅面、透明背景和位置恢复 | C-PSS, C-DRP | `PLANNED` |
| Poster State and Interaction (`C-PSI`) | 状态机、锁定、拖动、海报框拉伸与尺寸限制、命中和暂停同步 | C-LST | `PLANNED` |
| Poster Core Release Gate (`C-PCRG`) | Release、Windows 实机矩阵和性能封板 | C-PSI | `PLANNED` |

C-PCB 已由 `ACTIVE_PATH.md` 激活；其余工作流只在满足依赖并被激活时创建完整 C 目录。
