# C-DRP-04 Shell 临时置顶回归修复

日期：2026-08-13

状态：`COMPLETE — USER RELEASE MATRIX PASS`

## 1. 用户报告

已关闭的 C-DRP-04 在普通 Shell 操作中暴露新的可见回归：

1. 桌面右键后在任意位置左键；
2. 桌面右键后点击刷新；
3. 任意程序打开 Windows 文件选择器。

上述操作会让海报瞬时覆盖所有普通程序，随后立即回到桌面层，表现为一次明显闪烁。

## 2. 根因

问题不是 WebView 重绘、SQLite 状态刷新或 `shellRepair` 的 resize。C-DRP-04 为抑制 Win+D 后约 10–45ms 的第二次无事件 Progman Z 序重排，曾在任意 `Progman/WorkerW` 前台或窗口生命周期事件中执行：

```text
HWND_TOPMOST
→ 等待 48ms
→ HWND_NOTOPMOST
→ 相对 Progman 重锚
```

桌面菜单关闭、桌面刷新以及文件选择器相关 Shell 窗口活动会进入同一通用回调，因此海报确实在短时间内成为系统顶层窗口。旧源码契约测试还明确要求 `HWND_TOPMOST/NOTOPMOST` 存在，说明这是既有设计的回归边界，而不是偶发绘制异常。

## 3. 实施修复

- 删除 `SHELL_Z_ORDER_GUARD_ACTIVE`、`guard_tool_window_during_shell_transition`、`release_tool_window_shell_z_order_guard` 及 `HWND_TOPMOST/NOTOPMOST` 导入和调用；
- 新增 `is_desktop_shell_window`：事件必须对应真实 `Progman`，或带 `SHELLDLL_DefView` 的桌面 Shell 窗口；仅凭 `WorkerW` 类名不再足以触发恢复；
- 新增 `repair_tool_window_during_shell_transition`：只调用既有 Progman 相对重锚，不移动、不缩放、不激活、不使用 `SWP_FRAMECHANGED`；
- 恢复工作器在一次 Shell 事件后执行有界重锚突发：4–48ms 每 4ms 一次，随后在 64ms、96ms 收尾，并在 140ms 原协调窗口结束后继续 `shellRepair`；
- 新事件到达时通过既有 generation 合并机制废弃旧突发并从新一代重新计时；该逻辑是事件触发、时间有界的修复，不是持续轮询；
- 更新 `runtime-surface.test.mjs`：要求首轮重锚先于延迟协调、要求真实桌面窗口过滤和有界时间表，并明确禁止生产宿主源码出现 `HWND_TOPMOST/NOTOPMOST`。

相对 Progman 的插入点始终位于普通应用之后、桌面之前，因此即使普通 Shell 事件仍触发轻量修复，也不会把海报提升到文件对话框或其他普通程序之上。

## 4. 自动验证与产物

- `cargo fmt --check`：通过；
- `pnpm run check`：105/105 通过；
- `cargo test --manifest-path src-tauri/Cargo.toml`：89/89 通过；
- `pnpm run build`：通过；
- `pnpm run tauri:build`：Release 与 NSIS 通过；
- Release：`src-tauri/target/release/desktop-up.exe`；
- NSIS：`src-tauri/target/release/bundle/nsis/Desktop-Up_0.0.1_x64-setup.exe`；
- Release SHA-256：`19431138135D4750BBED872CC688F68752C56B73E208A9EEC12FA3714D0C21FD`；
- 最终 Release 已启动，当次 PID 为 `52480`。
- 原生 Progman 前台探针：`SetForegroundWindow(Progman)=true`，80 个约 2ms 样本中 Progman 前台 29/80、海报 `WS_EX_TOPMOST` 0/80，最终扩展样式 `0x80800A0`；证明最新二进制的 Shell 前台回调执行窗口内没有临时置顶采样。

## 5. 待人工确认

自动测试只能证明生产代码不再进入 TOPMOST 和恢复时序没有结构性回退，不能替代人的视觉观察。使用 PID 52480 对应的新 Release 验证：

1. 桌面右键打开菜单，再在桌面或普通程序任意位置左键关闭：海报不得闪到普通程序上方；
2. 桌面右键并点击刷新，连续执行至少 5 次：海报不得覆盖普通程序或出现明显闪烁；
3. 从任意普通程序打开 Windows 文件选择器，并关闭或取消，连续执行至少 3 次：对话框及原程序始终位于海报上方；
4. 连续按 Win+D 至少 10 轮：海报回到桌面时保持稳定，不消失、不重复、不出现旧的隐藏/再显示闪烁；
5. 复核海报空白穿透、卡片、拖动、锁定和收起/展开没有回退。

## 6. 用户验收

用户确认上述验证项全部通过：桌面右键关闭、桌面刷新和 Windows 文件选择器不再触发海报覆盖普通程序；连续 Win+D 保持稳定；空白穿透、卡片、拖动、锁定和收起/展开无回退。

本维护门因此关闭。C-DRP 的主屏单海报范围和多显示器未认证限制保持不变。
