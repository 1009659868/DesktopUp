# Install Baseline Contract 代码锚点

## C-IBC-01

- [`src-tauri/src/install_baseline.rs`](../../../../src-tauri/src/install_baseline.rs)：`install-baseline-v1` 的目录、权限、数据角色、自启、依赖和后继责任 DTO；`validate` 拒绝范围漂移、默认自启、WebView2 skip、重复或越界数据路径。
- [`src-tauri/tauri.conf.json`](../../../../src-tauri/tauri.conf.json)：显式 `currentUser` NSIS 与 `downloadBootstrapper` WebView2 provisioning。
- [`.cargo/config.toml`](../../../../.cargo/config.toml)：通过锁定 `tauri-build 2.6.3` 支持的 `STATIC_VCRUNTIME=true` 构建环境冻结静态 VC Runtime。
- [`src-tauri/Cargo.toml`](../../../../src-tauri/Cargo.toml)：bundled SQLite 与当前 Tauri/Windows 依赖。
- [`src-tauri/src/lib.rs`](../../../../src-tauri/src/lib.rs)：公开 install baseline 模块；当前 `app_config_dir()` SQLite authority 保持不变。
- [`tests/install-baseline.test.mjs`](../../../../tests/install-baseline.test.mjs)：配置/源码契约，约束当前用户安装、目标/当前数据根分离、自启仍禁用及依赖不使用 skip。

## C-IBC-02

- [`src-tauri/src/install_paths.rs`](../../../../src-tauri/src/install_paths.rs)：单一运行时路径解析器；从 Tauri `local_data_dir()`、`app_config_dir()` 和当前可执行文件生成安装根、目标数据根、当前 authority、七个目标角色路径及 `install-path-diagnostics-v1`。
- [`src-tauri/src/lib.rs`](../../../../src-tauri/src/lib.rs)：setup 统一消费 `RuntimePathResolution` 打开 placement、legacy bridge 和当前 SQLite；只读 `install_path_diagnostics` 命令返回托管快照。
- [`tests/install-paths.test.mjs`](../../../../tests/install-paths.test.mjs)：约束诊断命令注册、当前 authority 保留、目标根未激活和解析器无文件系统写入。

## C-IBC-03

- [`src-tauri/src/install_probe.rs`](../../../../src-tauri/src/install_probe.rs)：`install-content-probe-v1` 仅通过 metadata 读取识别开发/便携 Release、目标安装根和四项最低内容；冻结 current-user、无提权、运行时安装根零写入和结构化 gap。
- [`src-tauri/src/lib.rs`](../../../../src-tauri/src/lib.rs)：注册只读 `install_content_probe` Tauri 命令并复用 `RuntimePathResolution`。
- [`scripts/probe-install-artifacts.mjs`](../../../../scripts/probe-install-artifacts.mjs)：只读审计 Tauri 配置、本机构建工具的 NSIS 模板、Release/NSIS 哈希和发布差距；不解包、不安装、不卸载、不提权。
- [`tests/install-probe.test.mjs`](../../../../tests/install-probe.test.mjs)：覆盖差距夹具、只读副作用、NSIS 检查限制和 Tauri 命令注册。

## C-IBC-04

- [autostart_contract.rs](../../../../src-tauri/src/autostart_contract.rs)：autostart-contract-v1、autostart-diagnostics-v1、严格正式路径/参数门、只读 HKCU Run 查询与清理责任契约。
- [lib.rs](../../../../src-tauri/src/lib.rs)：注册只读 autostart_diagnostics 命令；托盘自启项继续禁用，单实例实际参数分流留给 C-RLC。
- [Cargo.toml](../../../../src-tauri/Cargo.toml)：只增加 Win32_System_Registry 读取 API 特性。
- [autostart-contract.test.mjs](../../../../tests/autostart-contract.test.mjs)：约束 HKCU exact-value 所有权、零变更 API、正式路径、严格参数和 C-URV 清理边界。
- [AUTOSTART_CLEANUP_CONTRACT_V1.md](../10_Plan/AUTOSTART_CLEANUP_CONTRACT_V1.md)：C-RLC/C-URV 可直接消费的冻结契约。

## C-IBC-05

- [probe-runtime-dependencies.mjs](../../../../scripts/probe-runtime-dependencies.mjs)：只读组合 Release/NSIS 哈希、PE 头/导入表、Cargo/Tauri 配置、生成 NSIS 安装根/资源载荷、个人自用范围与自启清理值名。
- [installer.nsi](../../../../src-tauri/nsis/installer.nsi)：固定 tauri-bundler 2.9.4 模板，按契约覆盖 current-user 安装根与精确 `DesktopUp` 自启清理值。
- [finalize-release-artifacts.mjs](../../../../scripts/finalize-release-artifacts.mjs)：把版本化 NSIS 字节一致地收口为最终入口 `DesktopUpSetup.exe`。
- [INSTALLATION_SCOPE.txt](../../../../src-tauri/resources/INSTALLATION_SCOPE.txt)：确保静态资源目录形成真实安装载荷并声明零可变数据写入边界。
- [runtime-dependency-handoff.test.mjs](../../../../tests/runtime-dependency-handoff.test.mjs)：覆盖 x64/GUI PE 解析、完整就绪夹具、已分配缺口、零副作用与 C-RLC/C-UGM/C-URV/C-RCG owner 冻结。
- [package.json](../../../../package.json)：公开 `probe:runtime-dependencies` 并把脚本语法检查和门禁测试纳入全量 `check`。
- [C_IBC_HANDOFF_INPUTS_V1.md](../10_Plan/C_IBC_HANDOFF_INPUTS_V1.md)：冻结依赖证据、原十项处理结果、零缺口包装门及四方后继输入。

C-IBC 已关闭。`contractPackagingReady` 不得被解释为 C-RCG 净机、升级、卸载恢复或签名认证完成。
