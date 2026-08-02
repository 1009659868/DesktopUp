# C-DHF-05 全屏、锁屏与显示器关闭自动暂停基础

日期：2026-08-02

状态：`IN PROGRESS — EVENT FOUNDATION PASS / REAL MATRIX PENDING`

## 目标

- 将前台全屏/最大化覆盖、系统锁定和显示器关闭归一化到同一暂停状态机；
- 保留用户手动暂停，并以 `effectivePaused = manuallyPaused || automaticallyPaused` 合并结果；
- 只消费 Windows 事件和一次性启动复核，不通过高频轮询维持状态；
- 海报进入暂停后冻结全部 CSS 主题动画，恢复桌面可见后继续。

## Windows 事件源

- 前台切换：`SetWinEventHook(EVENT_SYSTEM_FOREGROUND)`；
- 当前前台窗口在不切换焦点时改变最大化/全屏尺寸：`EVENT_OBJECT_LOCATIONCHANGE`，只接收 `OBJID_WINDOW` 且 HWND 等于当前前台窗口的事件；
- 会话锁定/解锁：`WTSRegisterSessionNotification` + `WM_WTSSESSION_CHANGE`；
- 显示器关闭/恢复：`RegisterPowerSettingNotification(GUID_CONSOLE_DISPLAY_STATE)` + `WM_POWERBROADCAST/PBT_POWERSETTINGCHANGE`；
- 启动时已有全屏应用：海报页面加载后立即重评估，并在启动后 1500ms 进行一次性复核；该复核只执行一次，不形成轮询。

## 前台覆盖规则

- 只判断与海报位于同一显示器的前台窗口；
- 排除 Desktop-Up 自身、`Progman`、`WorkerW` 和 `Shell_TrayWnd`；
- 窗口覆盖显示器完整区域时分类为 `Fullscreen`；
- `IsZoomed=true` 或覆盖显示器工作区时分类为 `Maximized`；
- 普通小窗口分类为 `None`，不触发自动暂停；
- WinEvent 回调把事件自带 HWND 直接送入状态机，避免事件发生后再次查询前台窗口造成竞态。

## 状态与前端

- `RuntimeSnapshot` 新增 `automaticallyPaused`、`effectivePaused`、`runtimeVisibility` 和 `runtimeMonitor`；
- `runtimeVisibility` 记录前台覆盖类型、窗口类名、锁屏/熄屏状态、暂停原因和只读判定阶段；
- 后端通过 `runtime-pause-state` 同步主页预览与独立海报 WebView；
- 海报 `is-runtime-paused` 会冻结海报自身和主题氛围动画；
- 托盘新增只读“运行状态”，区分静态待机、手动暂停、全屏、最大化、锁屏和显示器关闭。

## 黑盒诊断

为避免重新使用窗口标题，海报 HWND 暴露三项无界面的只读属性：

- `DesktopUp.RuntimePauseFlags`：自动、全屏、最大化、锁屏、熄屏、手动暂停位；
- `DesktopUp.ForegroundProbeFlags`：前台判定经过的阶段；
- `DesktopUp.RuntimeMonitorCapabilities`：前台、会话、电源三类监视器注册位。

属性不落盘、不产生计时循环，仅在状态事件变化时更新。

## 验证结果

```text
pnpm run check
  14 tests passed
  41 Markdown files verified

cargo test --locked --manifest-path src-tauri/Cargo.toml
  6 tests passed

cargo check --locked --manifest-path src-tauri/Cargo.toml
  PASS

cargo tauri build --no-bundle
  PASS
```

- 当前 Release PID：`74264`；
- 进程启动正常，单次观测工作集约 `32.1 MB`；
- HWND 能力位为 `7`，即前台 WinEvent、WTS 会话通知和显示器电源通知均注册成功；
- 自动化工具所在桌面会话中，启动复核看到的前台 HWND 仍是海报自身，判定阶段停在 `ForegroundProbe=1`，因此没有把当前游戏画面误记为全屏自动暂停通过；
- 外部合成 `NotifyWinEvent` 未被该桌面会话的系统钩子转发，不作为真实切换证据。

## 待人工矩阵

1. 同屏普通小窗口前台：保持静态待机；
2. 同屏最大化普通应用：进入最大化自动暂停，回桌面后恢复；
3. 无边框/独占全屏游戏：进入全屏自动暂停，退出后恢复；
4. `Win+L` 锁定再解锁：锁定期间暂停，解锁后按当前前台状态恢复；
5. 显示器关闭/睡眠再唤醒：关闭期间暂停，恢复后重评估前台；
6. 第二显示器全屏、海报仍在另一显示器：不得误暂停；
7. 手动暂停与自动暂停叠加：自动原因消失后仍保持手动暂停。

## 结论

C-DHF-05 的事件驱动实现基础、统一状态与诊断已经通过编译和自动化验证；真实全屏、锁屏、显示器关闭及多屏人工矩阵尚未完成，因此状态保持 `IN PROGRESS`，不能进入 C-DHF-06 正式性能封板。
