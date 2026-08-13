# Install Baseline Contract 工作流计划

日期：2026-08-13

状态：`COMPLETE — C-IBC-01..05 CLOSED`

## 1. 目的

在继续运行生命周期和海报核心前，冻结 Windows 11 x64 的安装范围、程序/数据目录所有权、权限、自启和运行时依赖契约，避免后续安装器、迁移器、卸载器和主程序各自推导路径。

## 2. 范围

本工作流负责：

- 当前用户、无管理员权限的安装基线；
- 安装目录与可变数据目录严格分离；
- 当前漫游配置目录到目标本地数据目录的兼容边界；
- 自启默认值、注册范围和实现责任；
- WebView2、VC Runtime、SQLite 与 NSIS 构建基线；
- 向 C-RLC、C-UGM、C-URV 和 C-RCG 输出稳定契约。

本工作流不负责：立即搬迁现有 SQLite、真正启用自启、最终独立卸载器、签名、升级回滚或净机发布认证。

## 3. 冻结决策

1. V1 默认按当前用户安装，不要求管理员权限；目标安装根为 `%LOCALAPPDATA%\Programs\DesktopUp`。
2. 安装根只保存程序、静态资源与卸载入口；运行时不得在安装根写数据库、缓存、日志或用户内容。当前为个人自用范围，不要求许可证载荷；公开分发时重开审计。
3. 目标可变数据根为 `%LOCALAPPDATA%\DesktopUp`，内部按 `data/vault/themes/cache/backups/logs/recovery` 分责。
4. 当前 Release 的 `%APPDATA%\com.allmight.desktopup` 仍是迁移前正式可写权威；C-IBC 不移动、不复制、不双写。切换、来源保留和回滚归 C-UGM。
5. 自启默认关闭，只允许用户显式开启；注册范围为当前用户，正式实现归 C-RLC，恢复失败时卸载仍必须清除自启。
6. 当前依赖基线为 Windows 11 x64、Evergreen WebView2、`downloadBootstrapper` 安装策略、静态 VC Runtime 和 bundled SQLite。WebView2 缺失时可能需要网络。
7. C-IBC 只冻结早期契约；最终 `DesktopUpSetup.exe` 归 C-RCG，独立 `uninstall.exe` 归 C-URV。

## 4. 任务

### C-IBC-01：目录、权限与依赖契约

结果：`install-baseline-v1` Rust DTO、校验与源码契约测试通过；NSIS current-user、静态 VC Runtime 和 WebView2 provisioning 变为显式配置。

状态：`COMPLETE — CONTRACT TESTS PASS`

### C-IBC-02：运行时路径解析与兼容诊断

结果：统一解析安装根、目标数据根、当前正式根和各数据角色；诊断明确当前 authority 与迁移状态，不改变 C-PSS 权威。

状态：`COMPLETE — RELEASE READ-ONLY DIAGNOSTICS PASS`

### C-IBC-03：安装目录、权限与内容探针

结果：Release/NSIS 探针验证当前用户范围、无需提权、程序目录不可承载可变数据，并核对安装最低内容和卸载入口现状。

状态：`COMPLETE — NON-DESTRUCTIVE PROBES PASS / RELEASE GAPS RECORDED`

### C-IBC-04：自启边界与清理契约

结果：冻结 HKCU 注册、显式 opt-in、已安装可执行文件校验、单实例参数和卸载清理接口；实际 UI/注册切换仍由 C-RLC 实现。

状态：`COMPLETE — READ-ONLY HKCU DIAGNOSTICS PASS`

### C-IBC-05：运行时依赖与交接门

结果：Release/NSIS 构建、WebView2/VC/SQLite 依赖证据、个人自用许可证范围和后继输入清单收敛，工作流交接 C-RLC。

状态：`COMPLETE — DEPENDENCY EVIDENCE PASS / ASSIGNED-GAP HANDOFF READY`

## 5. 完成门

- 程序目录和可变数据目录没有双重所有权；
- 当前正式数据在 C-UGM 前不会被静默搬迁或双写；
- 自启默认关闭且不引用开发路径；
- 缺失运行时依赖时不会生成“可安装但不可运行”的无诊断产物；
- C-RLC/C-UGM/C-URV/C-RCG 能引用同一版本化契约；
- Release/NSIS 构建与对应自动检查通过。

## 6. 已知限制

本工作流不宣称最终安装包、独立卸载器、签名、升级、净机或多显示器矩阵已经通过。
