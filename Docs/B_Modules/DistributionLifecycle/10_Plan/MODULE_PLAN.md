# Distribution Lifecycle 模块计划

最后检查：2026-08-13

状态：`INACTIVE — C-IBC COMPLETE / C-UGM PLANNED`

上层计划：[`DESKTOPUP_V1_IMPLEMENTATION_PLAN.md`](../../../A_Project/10_Plan/DESKTOPUP_V1_IMPLEMENTATION_PLAN.md)

## 1. 责任

拥有安装目录、数据目录、权限、自启、运行时依赖、升级迁移、回滚、恢复型卸载、签名、许可证和净机发布矩阵。

## 2. C 工作流

| C 工作流 | 结果 | 依赖 | 状态 |
| --- | --- | --- | --- |
| Install Baseline Contract (`C-IBC`) | 安装/数据目录、权限、自启和依赖契约 | C-DRP | `COMPLETE` |
| Upgrade and Migration (`C-UGM`) | 应用、数据库、模板和配置迁移/回滚 | C-PSS 与相关 schema 稳定 | `PLANNED` |
| Uninstall and Recovery (`C-URV`) | 独立卸载器、快捷方式恢复和失败保留保险库 | C-RCV, C-UGM | `PLANNED` |
| Release Candidate Gate (`C-RCG`) | 净机、签名、许可证、安全和安装卸载矩阵 | 全部 V1 必需工作流 | `PLANNED` |

## 3. 模块完成信号

- 生成 `DesktopUpSetup.exe` 和安装目录内独立 `uninstall.exe`；
- 升级失败可回滚；
- 卸载优先恢复用户快捷方式，失败时保留可定位保险库；
- 净机、权限、依赖、签名和许可证矩阵通过。

`C-IBC` 已在 V1-P1 冻结契约、依赖证据和四方交接，不宣称已交付最终安装包；真实 `DesktopUpSetup.exe` 与独立 `uninstall.exe` 仍由 C-RCG/C-URV 封板。B-DLC 暂无活跃 C。
