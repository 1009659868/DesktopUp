# Template Platform 工作流索引

| C 工作流 | 责任 | 依赖 | 状态 |
| --- | --- | --- | --- |
| Shadow Runtime and Fallback (`C-SRF`) | Shadow DOM、Transparent Default 和 minimal-fallback | C-PCB | `PLANNED` |
| Built-in Template Suite (`C-BTS`) | Neon HUD、Cinema Lightbox 和三模板矩阵 | C-SRF, C-PSI | `PLANNED` |
| Template Package Security (`C-TPS`) | 完整模板包净化、原子安装和回滚 | C-SRF | `PLANNED` |
| Template Hot-switch Performance (`C-THP`) | 热切换、释放、暂停和性能 | C-BTS, C-TPS | `PLANNED` |

只在满足依赖并由 `ACTIVE_PATH.md` 激活时创建对应完整 C 目录。