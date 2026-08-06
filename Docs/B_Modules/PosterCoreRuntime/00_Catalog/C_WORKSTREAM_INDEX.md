# Poster Core Runtime 工作流索引

| C 工作流 | 责任 | 依赖 | 状态 |
| --- | --- | --- | --- |
| Poster Contract Baseline (`C-PCB`) | 状态源、几何所有权、默认快照、自制标题栏和行为测试 | C-DHF | `PLANNED NEXT` |
| Large Surface and Transparency (`C-LST`) | 右侧大幅面、透明背景和位置恢复 | C-PCB, C-DRP | `PLANNED` |
| Poster State and Interaction (`C-PSI`) | 状态机、锁定、拖动、海报框拉伸与尺寸限制、命中和暂停同步 | C-LST | `PLANNED` |
| Poster Core Release Gate (`C-PCRG`) | Release、Windows 实机矩阵和性能封板 | C-PSI | `PLANNED` |

只在满足依赖并由 `ACTIVE_PATH.md` 激活时创建对应完整 C 目录。
