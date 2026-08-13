# C-IBC-02 运行时路径解析与兼容诊断

日期：2026-08-13

状态：`COMPLETE — C-IBC-03 NEXT`

## 1. 结果

- 新增 `RuntimePathResolution`，统一解析 `%LOCALAPPDATA%\Programs\DesktopUp`、`%LOCALAPPDATA%\DesktopUp`、当前 `%APPDATA%\com.allmight.desktopup`、当前/目标 SQLite 和七个目标数据角色；
- 新增版本化 `install-path-diagnostics-v1`，标明当前 authority 为 `preMigrationAuthoritative`、迁移 owner 为 C-UGM、target inactive、必须保留来源并支持回滚；
- 新增只读 Tauri 命令 `install_path_diagnostics`；
- setup 统一使用解析器提供的 placement、snapshot bridge 和 SQLite 路径，当前正式根保持不变。

## 2. 安全边界

解析器：

- 不创建目录；
- 不写入、复制、重命名或删除文件；
- 不改变 authority；
- 拒绝相对路径、带 `.`/`..` 的输入、安装根内的可变数据和 C-UGM 前的目标根提前激活；
- 只把开发/便携 Release 位于目标安装根之外记录为兼容警告，不伪装成已安装状态。

## 3. 验证

- C-IBC-02 Rust 专项：6/6；
- C-IBC Node 专项：8/8；
- 前端/契约全量：113/113；
- Rust 全量：100/100；
- 文档链接：136 份；
- UI production、Release 与 NSIS：通过；
- Release 启动/退出烟雾：通过；
- SQLite 启停前后 SHA-256 均为 `DA63A52BDF62B3FC863995D79F497584BE3BA97C1DE4C55D1D301AFBAFAB6CFC`；
- `%LOCALAPPDATA%\DesktopUp` 在运行期间和退出后均不存在；
- Release SHA-256：`B9BC40E7BE22324F9249B21DD32E0715E7DFBF6F6CA48AED3B65E22E9D901585`；
- NSIS SHA-256：`FB6B40E9D93EBA9C20D3FF305B0494BF3848116773D3E95BC4F8190014C162F1`，大小 `2,947,472` bytes；
- 最新 Release PID：`53524`。

## 4. 下一步

C-IBC-03 消费同一诊断快照，建立安装目录、权限和最低内容的非破坏性探针。当前 NSIS 仍不是最终 `DesktopUpSetup.exe`/独立 `uninstall.exe` 交付。
