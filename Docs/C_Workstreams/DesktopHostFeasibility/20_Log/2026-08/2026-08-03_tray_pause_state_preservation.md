# C-DHF-05 托盘前台切换暂停状态修复

日期：2026-08-03

状态：`FIX IMPLEMENTED / USER RETEST PENDING`

## 人工矩阵失败

用户执行“将 Codex 窗口最大化，再右键 Desktop-Up 托盘”时，托盘仍显示“运行状态：静态待机”，不符合“最大化覆盖，自动暂停”的预期。因此 C-DHF-05 继续保持进行中，不能以此前自动化基础验证代替人工通过。

## 原因

旧前台归一化逻辑把以下 Shell 表面归为同一种“回到桌面”事件：

- 真正桌面：`Progman`、`WorkerW`；
- 临时系统界面：`Shell_TrayWnd`。

右键通知区域时，前台短暂切换到 `Shell_TrayWnd`。状态机因此清除了此前最大化/全屏窗口的覆盖结果，托盘菜单打开后显示“静态待机”。

## 修复

- `Progman`、`WorkerW` 仍视为真正回到桌面并清除覆盖暂停；
- `Shell_TrayWnd`、副任务栏、托盘溢出区、Windows 组合系统弹层和原生菜单类改为临时 Shell 表面；
- 临时 Shell 表面继承上一个有效前台窗口的覆盖类型和类名，托盘打开期间继续显示最大化/全屏自动暂停；
- 前台 HWND 暂时为空、不可见、属于 Desktop-Up 自身或几何读取短暂失败时，同样不再错误清除已有暂停；
- 跨显示器前台和同屏普通小窗口仍会明确清除覆盖暂停；
- 新增 `FOREGROUND_PROBE_PRESERVED` 诊断位，表示本次结果来自临时前台继承；
- Release 入口同时补充 `windows_subsystem = "windows"`，消除 Windows Terminal 命令窗口，Debug 构建仍保留控制台。

## 验证

```text
pnpm run check
  16 tests passed
  42 Markdown files verified（补写本日志前）

cargo test --locked --manifest-path src-tauri/Cargo.toml
  8 tests passed

cargo check --locked --manifest-path src-tauri/Cargo.toml
  PASS

cargo tauri build --no-bundle
  PASS
```

- Rust 回归测试证明 `Shell_TrayWnd` 会继承上一个最大化覆盖，`Progman/WorkerW` 会清除覆盖；
- Release PE `SubsystemValue=2 / WINDOWS_GUI`；
- 修复版 Release PID `71836` 已启动，`Responding=True`；
- 自动化工具会话的 `GetForegroundWindow` 返回空 HWND，无法替代用户真实桌面完成 Codex 最大化与托盘右键测试。

## 待用户复测

1. 保持 Desktop-Up 运行；
2. 将 Codex 或其他同屏普通窗口最大化；
3. 右键 Desktop-Up 托盘；
4. 应显示“运行状态：最大化覆盖，自动暂停”；
5. 回到桌面后再次打开托盘，应恢复“运行状态：静态待机”；
6. 启动期间不应再出现 Windows Terminal 命令窗口。
