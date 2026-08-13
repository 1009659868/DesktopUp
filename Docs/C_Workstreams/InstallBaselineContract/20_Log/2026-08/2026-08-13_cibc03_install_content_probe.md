# C-IBC-03 安装目录、权限与内容探针

日期：2026-08-13

状态：COMPLETE — C-IBC-04 NEXT

## 1. 结果

- 新增运行时 install-content-probe-v1，只通过 symlink_metadata 读取目标安装根、当前可执行文件和最低内容类型，不创建目录、不写入、不安装、不卸载、不提权。
- 探针复用 RuntimePathResolution，明确区分 developmentOrPortable 与 targetInstalled，并通过只读 Tauri 命令 install_content_probe 返回结构化 readiness 和 gaps。
- 新增构建侧 install-artifact-probe-v1，只读审计 tauri.conf.json、Cargo 元数据、本机构建工具 NSIS 模板及 Release/NSIS 哈希；压缩 payload 未独立提取，报告中保留该限制。
- package.json 已接入 probe:install-artifacts 和全量语法/测试检查。

## 2. 权限与内容结论

已确认：

- NSIS installMode 为 currentUser；
- 本次 tauri-cli 2.11.4 构建环境的模板对 current-user 使用 RequestExecutionLevel user，无管理员提权；
- 同一模板会在安装时生成真实 uninstall.exe；该模板不是项目 Cargo.lock 直接锁定的依赖；
- 运行时安装根写入、目录创建、安装/卸载和提权副作用全部为 false；
- SQLite 正式 authority 仍位于当前 roaming 根，C-IBC-03 不迁移、不复制、不双写。

当前产物仍不是最终发布包，结构化记录五项 C-RCG 差距：

1. installRootMismatch：计划根为 %LOCALAPPDATA%\Desktop-Up，而契约要求 %LOCALAPPDATA%\Programs\DesktopUp；
2. mainExecutableNameMismatch：当前为 desktop-up.exe，契约要求 DesktopUp.exe；
3. resourcesDirectoryNotBundled：bundle 未包含 resources/；
4. licensesDirectoryNotBundled：bundle 未包含 licenses/；
5. finalInstallerNameMismatch：当前为 Desktop-Up_0.0.1_x64-setup.exe，最终入口要求 DesktopUpSetup.exe。

uninstall.exe 的恢复型卸载行为仍归 C-URV；本轮只确认 NSIS 生成入口，不宣称恢复/清理矩阵通过。

## 3. 验证

- C-IBC-03 Node 定向：4/4；
- C-IBC-03 Rust 定向：4/4；
- 前端/契约全量：117/117；
- Rust 全量：104/104；
- 文档链接：137 份；
- UI production、Release 和 NSIS 构建：通过；
- Release 启动烟测：通过，最终 PID 43240；
- SQLite 烟测前后 SHA-256：DA63A52BDF62B3FC863995D79F497584BE3BA97C1DE4C55D1D301AFBAFAB6CFC；
- 目标 %LOCALAPPDATA%\DesktopUp 与 %LOCALAPPDATA%\Programs\DesktopUp 均未创建；
- Release SHA-256：9C100B63289BD9BC299F152CCE3A7A01F548CAF287CB70A6E81378DCBBD18DF8；
- NSIS SHA-256：4B1D95BD8036EA86530758DB9B64DCD214672341910F6505F9DFBEB239B2D92C，大小 2,966,787 bytes。

## 4. 下一步

C-IBC-04 冻结 HKCU 当前用户自启、显式 opt-in、正式安装路径校验、单实例启动参数和卸载清理接口。本轮仍不得实际注册自启，也不得引用开发目录 Release。
