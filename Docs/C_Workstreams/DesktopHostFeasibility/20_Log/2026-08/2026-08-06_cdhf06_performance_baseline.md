# 2026-08-06 C-DHF-06 性能基线实现记录

状态：`IN PROGRESS — 采集与可视化实现完成，真实基线数据待采样`

## 目标

为 C-DHF-06 提供四态性能采集、采样记录和 Desktop-Up 内简单性能数值可视化。

## 实现内容

### Rust 侧

- 新增 `get_performance_snapshot`：返回内存、系统进程数、海报 FPS、进程 CPU 时间、暂停状态和运行时长；
- 使用正式的 `PROCESS_MEMORY_COUNTERS` 与 `K32GetProcessMemoryInfo`，不再手写结构体偏移；
- 新增 `report_poster_fps`：接收海报页 FPS 上报，并限制在 0..240；
- 新增 `save_performance_baseline`：将采样数组写入 `%APPDATA%/com.allmight.desktopup/performance-baseline-v1.json`；
- `RuntimeSnapshot` 新增 `posterFps` 与 `performanceMode`，暂停状态由自动模式或采样模式推导。

### 前端

- 控制中心新增 `perf-chip`，实时显示内存与 FPS；
- 新增性能面板，可切换 `自动 / 静态 / 30 FPS / 暂停 / 停止` 五种模式；
- 新增“采集当前状态 10s”按钮，每 2 秒采样一次并保存基线；
- 海报页 FPS 遥测仅在 `activeAnimated` 且未暂停、页面可见时运行；
- `activeStatic`、`paused`、`stopped` 下停止 `requestAnimationFrame` 并上报 `0 FPS`。

## 自动化验证

- Node / 契约测试：31/31 通过；
- Rust 测试：15/15 通过；
- `pnpm run build`：通过；
- `cargo build --release`：通过；
- `cargo fmt --check`：通过。

## 真实采样待执行

以下操作需要用户在本机 Release 环境中逐项执行：

1. 切换到 `activeStatic`，点击“采集当前状态 10s”；
2. 切换到 `activeAnimated`，点击“采集当前状态 10s”；
3. 切换到 `paused`，点击“采集当前状态 10s”；
4. 切换到 `stopped`，点击“采集当前状态 10s”；
5. 确认 `performance-baseline-v1.json` 生成并包含四组样本。

## 结论

- C-DHF-06 实现部分完成，真实基线数据仍待采样；
- 在真实采样完成前，不将 C-DHF-06 标记为 `COMPLETE`。
