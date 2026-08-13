# Distribution Lifecycle 工作流索引

| C 工作流 | 责任 | 依赖 | 状态 |
| --- | --- | --- | --- |
| Install Baseline Contract (`C-IBC`) | 安装/数据目录、权限、自启和依赖契约 | C-DRP | `COMPLETE` |
| Upgrade and Migration (`C-UGM`) | 应用、数据库、模板和配置迁移/回滚 | C-PSS 与相关 schema 稳定 | `PLANNED` |
| Uninstall and Recovery (`C-URV`) | 独立卸载器、恢复和失败保留保险库 | C-RCV, C-UGM | `PLANNED` |
| Release Candidate Gate (`C-RCG`) | 净机、签名、许可证、安全和安装卸载矩阵 | 全部 V1 工作流 | `PLANNED` |

C-PSS 与 C-IBC 已关闭；B-DLC 暂无活跃 C。C-IBC 只建立早期契约和交接证据，不能替代最终发布矩阵。
