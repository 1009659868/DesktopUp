# C-IBC 激活与 C-IBC-01 契约基线

日期：2026-08-13

状态：`C-IBC-01 COMPLETE / C-IBC-02 NEXT`

## 1. 激活

用户确认 C-DRP-04 无 TOPMOST 维护 Release 的桌面右键关闭、刷新、Windows 文件选择器、连续 Win+D 和既有核心交互验证全部通过。维护门关闭后，按既定路线激活 C-IBC，成为唯一活跃 C。

该通过只覆盖既定主屏单海报和本轮层级矩阵，不扩张到第二显示器、混合 DPI、负坐标副屏、热插拔或跨屏重启。

## 2. 审计结论

- 规则和旧技术架构要求当前用户安装到 `%LOCALAPPDATA%\Programs\DesktopUp`，数据目标为 `%LOCALAPPDATA%\DesktopUp`；
- 当前 Tauri 已使用 NSIS `currentUser`，但 WebView2 provisioning 与静态 VC Runtime 依赖默认值，尚未显式冻结；
- 当前正式 SQLite、placement、bridge 和性能基线仍通过 `app_config_dir()` 位于 `%APPDATA%\com.allmight.desktopup`；
- 直接改用 `app_local_data_dir()` 会形成未版本化搬迁或空库，违反 C-PSS 失败保留和 C-UGM 迁移/回滚边界；
- 控制中心和托盘自启目前只是禁用占位，不能写成已实现。

## 3. 实施

- 新增 `install_baseline.rs` 和 `install-baseline-v1`；
- 冻结程序根只读、目标数据布局、当前正式根保留、C-UGM 迁移边界、自启默认关闭和后继责任；
- 通过 Cargo `STATIC_VCRUNTIME=true`、Tauri `webviewInstallMode=downloadBootstrapper` 和既有 `installMode=currentUser` 显式冻结构建基线；
- 新增 Rust 校验/单测和 Node 源码契约，明确 C-IBC-01 不创建目标目录、不搬迁或双写用户状态。

## 4. 决策依据

Tauri 2 的 `app_config_dir()` 在 Windows 使用 roaming config + identifier，`app_local_data_dir()` 使用 LocalAppData + identifier；NSIS `currentUser` 不要求管理员权限。WebView2 `downloadBootstrapper` 是默认小型安装策略，运行时缺失时可能需要网络。当前显式配置用于冻结事实，不代表最终离线发布策略已完成。

## 5. 验证

- C-IBC Node 专项：4/4 通过；
- 前端/契约全量：109/109 通过；
- Rust：94/94 通过；
- 文档链接：135 份 Markdown 通过；
- UI production build：通过；
- Release/NSIS：通过；
- 首轮全量检查识别出锁定 `tauri-build 2.6.3` 不接受较新 `build.windows.staticVCRuntime` 配置字段；改用该版本实际读取的 `STATIC_VCRUNTIME=true` Cargo 环境后，Rust、Release 与 NSIS 均通过；
- `dumpbin /dependents` 未发现 `VCRUNTIME*.dll` 或 `MSVCP*.dll` 动态依赖；保留的 `api-ms-win-crt-*` 是 Windows UCRT 系统 API 集；
- Release SHA-256：`81BABB6D867D0A3E7ABA5F62B1498A6B573D4E0409355D602799991E3B4A5B55`；
- NSIS SHA-256：`D6B268902B9D9B9F2E87947EAD7C7EB5E2FFB003ECAEC12B70DCA5E541F937C5`，大小 `2,945,611` bytes；
- 最新 Release 已启动，PID `26688`；
- C-DRP-04 用户维护矩阵：通过。

## 6. 下一步

C-IBC-02 建立单一运行时路径解析与结构化诊断，区分 install root、target data root、current authority、角色目录和 migration status；仍不切换 C-PSS authority。
