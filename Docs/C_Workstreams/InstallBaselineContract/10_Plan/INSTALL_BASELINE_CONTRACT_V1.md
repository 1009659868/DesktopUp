# Install Baseline Contract V1

状态：`FROZEN — C-IBC-01`

稳定标识：`install-baseline-v1`

## 路径与所有权

| 角色 | 路径 | 当前状态 | 所有权 |
| --- | --- | --- | --- |
| 程序安装根 | `%LOCALAPPDATA%\Programs\DesktopUp` | 目标 | 安装器；运行时只读 |
| 目标数据根 | `%LOCALAPPDATA%\DesktopUp` | 尚未切换 | 主程序/后继迁移器 |
| 当前正式根 | `%APPDATA%\com.allmight.desktopup` | C-UGM 前继续可写 | 当前 Release |

目标数据布局：

```text
%LOCALAPPDATA%\DesktopUp\
├─ data\poster-state-v1.sqlite3
├─ backups\
├─ vault\shortcuts\
├─ themes\
├─ cache\
├─ logs\
└─ recovery\
```

任何目标路径切换必须由 C-UGM 提供版本、备份、原子切换和失败回滚。C-IBC-01 不创建目标目录，不读取或改写用户数据。

## 安装与权限

- `scope=currentUser`；
- `elevationRequired=false`；
- 安装根禁止可变运行时写入；
- 最低目标内容为 `DesktopUp.exe`、`uninstall.exe`、`resources/`；当前分发范围为 `privateUse`，`licensePayloadRequired=false`；
- 当前 Tauri 产物是否已经满足正式命名和独立卸载能力，由 C-IBC-03/C-URV 继续审计，不能因 NSIS 构建成功提前宣称完成。

## 自启

- 默认关闭；
- 必须由用户显式开启；
- 当前用户范围，候选机制为 HKCU Run；
- 只能引用已安装的正式可执行文件；
- 实现归 C-RLC，卸载清理归 C-URV；
- 精确值、命令、单实例和清理语义已由 [`AUTOSTART_CLEANUP_CONTRACT_V1.md`](AUTOSTART_CLEANUP_CONTRACT_V1.md) 在 C-IBC-04 冻结。

## 运行时依赖

- Windows 11 x64；
- Evergreen WebView2；
- Tauri Windows installer `webviewInstallMode=downloadBootstrapper`；
- Windows MSVC 静态 VC Runtime；
- `rusqlite` bundled SQLite；
- WebView2 已随系统存在时不额外下载；缺失时 bootstrapper 可能需要网络，离线发布策略留到 C-RCG 决策。
