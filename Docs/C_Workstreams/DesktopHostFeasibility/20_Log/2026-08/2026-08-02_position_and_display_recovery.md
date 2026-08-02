# 原生位置持久化与显示恢复记录

日期：2026-08-02

状态：`C-DHF-04 IN PROGRESS — FOUNDATION PASS / REAL ENVIRONMENT MATRIX PENDING`

## 前置结论

- 用户已确认 C-DHF-03 鼠标命中与穿透人工矩阵通过；
- C-DHF-04 已有 Win+D/Shell 前台事件恢复补丁，本记录继续完成位置、Explorer 生命周期和显示变化恢复基础；
- 不移动或改写 Explorer 原生桌面图标位置。

## 位置持久化模型

新增 `src-tauri/src/placement.rs`，位置文件保存到当前用户应用配置目录：

```text
%APPDATA%/com.allmight.desktopup/poster-placement-v1.json
```

记录字段：

- `monitorDevice`：显示器设备名；
- `anchorX/anchorY`：海报在显示器工作区可用移动范围内的 0..1 归一化锚点；
- `fallbackX/fallbackY`：显示器消失或无法匹配时使用的物理坐标回退；
- `version`：位置格式版本。

拖动产生的连续 `Moved` 事件只更新内存快照，通过容量为 1 的信号通道合并，并在 300ms 后写盘，避免每个鼠标移动事件都访问磁盘。托盘退出和窗体销毁时主动刷新最后快照。

## 显示器与 Shell 恢复

- 使用 `EnumDisplayMonitors + MONITORINFOEXW` 获取设备名和工作区，不把包含任务栏的完整显示器矩形当成可用区域；
- 恢复时优先匹配原显示器，并按新工作区尺寸重新计算归一化锚点；原显示器不存在时选择绝对坐标最近的可用显示器；
- `SetWinEventHook` 除 `EVENT_SYSTEM_FOREGROUND` 外，增加 `EVENT_OBJECT_CREATE..EVENT_OBJECT_SHOW`，用于发现 Explorer 重建的 `Progman/WorkerW`；
- 使用 `SetWindowSubclass` 接收 `WM_DISPLAYCHANGE`、`WM_DPICHANGED` 和 `WM_SETTINGCHANGE`，事件合并后执行工作区恢复；
- WebView `resize` 事件重新同步 `devicePixelRatio`、物理窗体尺寸和原生交互区域；
- 所有恢复均使用无激活窗口定位，不使用后台轮询。

## 自动化验证

```text
pnpm run check
  13 tests passed
  40 Markdown files verified（登记本日志后）

cargo test --locked --manifest-path src-tauri/Cargo.toml
  2 tests passed

cargo check --locked --manifest-path src-tauri/Cargo.toml
  PASS

cargo tauri build --no-bundle
  PASS
```

## Release 黑盒验证

### 重启恢复

1. 初始位置 `(72,154)`；
2. 仅把 Desktop-Up 海报移到 `(120,190)`；
3. 300ms 合并写盘后，配置记录 `DISPLAY1`、归一化锚点和 `(120,190)` 回退；
4. 强制结束并重新启动 Release；
5. 海报准确恢复到 `(120,190)`，尺寸仍为 `612×494`；
6. 测试结束后恢复到 `(72,154)`，配置同步回原位置。

结论：`PASS`。

### 显示变化自动拉回

1. 临时把海报移到屏幕外 `(5000,5000)`；
2. 仅向海报窗体发送测试 `WM_DISPLAYCHANGE`，不修改系统显示设置；
3. 海报在事件合并窗口后恢复到 `(1948,898)`；
4. 当前 `DISPLAY1` 工作区为 `(0,0)-(2560,1392)`，海报 `612×494`，因此恢复位置的右下边界精确为 `(2560,1392)`；
5. 测试结束后恢复到 `(72,154)`。

结论：`PASS`。

当前 Release PID：`73960`（仅用于本次记录）。

## 尚待人工矩阵

- Win+D 显示桌面/返回应用连续测试；
- Explorer 真重启至少 10 次；
- 双屏、主屏切换和显示器热插拔；
- 100%、125%、150%、200% DPI 切换；
- 每种场景确认位置可见、Z 序正确、不抢焦点、无重复窗口和重试风暴。

在真实环境矩阵完成前，C-DHF-04 不标记为完整通过。
