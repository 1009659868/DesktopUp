# C-RLC-03 自启显式 opt-in 与状态同步

日期：2026-08-13

状态：`C-RLC-03 IN VALIDATION`

## 目标

在正式安装路径门之后实现用户显式开机自启启用、漂移修复与禁用，让托盘和控制中心只消费同一个原生写后读回快照；开发/便携 Release 必须保持零写入。

## 实施

- 在 [`autostart_contract.rs`](../../../../../src-tauri/src/autostart_contract.rs) 增加 `autostart-change-v1` 和串行 `AutostartCoordinator`；
- 启用只写 HKCU Run 的 `DesktopUp` / `REG_SZ` / 冻结正式命令，禁用只删除该值，不删除父键或其他应用值；
- 所有 mutation 均先读资格和状态，完成后再次读回；只有读回 matching/absent 才返回成功；
- ineligible、unreadable、Win32 写失败和读回不一致均失败关闭，不乐观翻转 UI；
- 托盘和控制中心共享 `autostart-state-changed`，控制中心新增纯投影 [`autostart-control.js`](../../../../../app/src/autostart-control.js)；
- 开发/便携版入口显示“仅正式安装版”并禁用；正式路径中的 drifted 可由用户明确启用操作修复。

## 自动与 Release 证据

- 自启目标 Node 契约/投影 9/9 通过；
- Rust 自启隔离矩阵 12/12 通过；全量 Node 139/139、Rust 122/122、151 份 Markdown 链接通过；
- production UI、Release 与 NSIS 构建通过；
- 最终 Release `DesktopUp.exe` SHA-256：`AE5F25A94DB799F1F511EB1E0FA4F33D4AA9631BCC971E6930C556EE76B168E6`；
- 最终安装器 `DesktopUpSetup.exe` SHA-256：`A791C587AAA7AB331104817F1E65F79F9E559A504054ACAA609954943D7FA5AE`；
- 本机正式目标 `%LOCALAPPDATA%\Programs\DesktopUp\DesktopUp.exe` 不存在；开发路径 Release 启动前后真实 Run `DesktopUp` 均为 absent，证明冷启动不会自行注册；
- 本轮没有安装/卸载，也没有通过开发版触发真实 HKCU 写入。

## 待人工验证

必须从最新 NSIS 安装后的正式 `DesktopUp.exe` 验证：首次关闭状态、托盘/控制中心任一入口启用与跨端同步、退出后 `--autostart` 只显示海报和托盘、禁用后值消失，以及核心海报/控制中心无回退。该矩阵通过前 C-RLC-03 不关闭、不推进 C-RLC-04。
