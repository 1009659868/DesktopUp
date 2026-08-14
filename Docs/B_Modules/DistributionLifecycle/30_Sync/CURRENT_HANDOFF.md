# Distribution Lifecycle 当前交接

最后更新：2026-08-13

状态：`INACTIVE — C-IBC COMPLETE / C-UGM PLANNED`

## 已建立

- 当前用户、无提权安装和程序/数据分离已进入 `install-baseline-v1`；
- 当前 roaming SQLite authority 保持不变，目标 LocalAppData 迁移归 C-UGM；
- 自启默认关闭且实现归 C-RLC；
- WebView2、静态 VC Runtime、bundled SQLite 与 NSIS current-user 基线已显式冻结。
- C-IBC-01 的目录/依赖契约、C-IBC-02 的只读路径诊断、C-IBC-03 的双层内容/权限探针和 C-IBC-04 的自启/清理契约均已完成；
- C-IBC-03 的 117 项前端/契约、104 项 Rust、137 份文档链接与 Release/NSIS 构建均通过，SQLite 哈希和目标目录状态未改变；
- current-user、无提权和 NSIS 生成卸载入口已获得证据；安装根、主程序名、resources、licenses 和最终安装器名五项差距已归 C-RCG；
- HKCU Run 默认关闭、正式路径/严格参数门和 C-URV 精确值清理已冻结；实际注册未启用，本机值始终不存在；
- C-IBC-04 的 122 项前端/契约、111 项 Rust、139 份文档链接与 Release/NSIS 构建均通过。
- C-IBC-05 的依赖证据门通过，后续个人自用包装维护已使结构化缺口清零；`DesktopUp.exe`、`%LOCALAPPDATA%\Programs\DesktopUp`、resources、`DesktopUpSetup.exe` 与精确自启清理均对齐；当前仍未通过 C-RCG 净机/升级/卸载恢复/签名认证；
- C-IBC-01..05 已关闭，四方输入以 [`C_IBC_HANDOFF_INPUTS_V1.md`](../../../C_Workstreams/InstallBaselineContract/10_Plan/C_IBC_HANDOFF_INPUTS_V1.md) 为准。

## 下一步

B-DLC 暂无活跃 C。全局当前活跃链为 B-DRF / C-RLC，C-RLC-03 已实现并处于正式安装版人工验证门；C-UGM、C-URV 与 C-RCG 继续等待各自依赖，不得并行提前实施。
