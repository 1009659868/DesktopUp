# 2026-08-06 C-DHF-06 性能基线实现记录

状态：`COMPLETE`

## 目标

为 C-DHF-06 提供四态性能采集、采样记录和 Desktop-Up 内简单性能数值可视化。

## 实现内容

### Rust 侧

- 新增 `get_performance_snapshot`：返回内存、系统进程数、海报 FPS、进程 CPU 时间、暂停状态和运行时长；
- 使用正式的 `PROCESS_MEMORY_COUNTERS` 与 `K32GetProcessMemoryInfo`，不再手写结构体偏移；
- 新增 `report_poster_fps`：接收海报页 FPS 上报，并限制在 0..240；
- 新增 `save_performance_baseline`：将采样数组写入 `%APPDATA%/com.allmight.desktopup/performance-baseline-v1.json`；保存时按 `mode` 合并，避免后续模式覆盖前序模式；
- `RuntimeSnapshot` 新增 `posterFps` 与 `performanceMode`，暂停状态由自动模式或采样模式推导。

### 前端

- 控制中心新增 `perf-chip`，实时显示内存与 FPS；
- 新增性能面板，可切换 `自动 / 静态 / 30 FPS / 暂停 / 停止` 五种模式；
- 新增“采集当前状态 10s”按钮，每 2 秒采样一次并保存基线；
- 海报页 FPS 遥测仅在 `activeAnimated` 且未暂停、页面可见时运行；
- `activeStatic`、`paused`、`stopped` 下停止 `requestAnimationFrame` 并上报 `0 FPS`。

## 自动化验证

- Node / 契约测试：31/31 通过；
- Rust 测试：16/16 通过；
- `pnpm run build`：通过；
- `cargo build --release`：通过；
- `cargo fmt --check`：通过；
- `cargo tauri build --no-bundle`：通过。

## 真实采样完成（2026-08-06）

采样环境：`Microsoft Windows 11 专业版 10.0.26100`，`Intel(R) Core(TM) i7-14700`（20C/28T），`31.7 GB` 内存。

已在本机 Release 控制中心依次执行：

1. `activeStatic`：5 个样本，`posterFps=0`，内存约 `116.6 MB`；
2. `activeAnimated`：5 个样本，后续 4 个样本 `posterFps=60`，内存约 `116.6 MB`；
3. `paused`：5 个样本，`posterFps=0`，内存约 `116.7 MB`；
4. `stopped`：5 个样本，`posterFps=0`，内存约 `35.8 MB`。

基线文件：`%APPDATA%\com.allmight.desktopup\performance-baseline-v1.json`

文件当前包含 `20` 个样本，四种模式各 `5` 个；每种模式按 `2s` 间隔连续采集，共约 `10s`。

## 收口核对（2026-08-06）

`performance-baseline-v1.json` 已补充机器规格与采样方法元数据：

- `machine.os`：`Microsoft Windows 11 专业版`
- `machine.osVersion`：`10.0.26100`
- `machine.cpu`：`Intel(R) Core(TM) i7-14700`
- `machine.totalMemoryBytes`：`34000846848`
- `sampling.method`：Release 控制中心，每种模式每 `2000ms` 采集 1 次，共 5 次约 `10s`
- `sampling.modes`：`activeStatic / activeAnimated / paused / stopped`

Release 实时性能数值可视化已通过 UIA 读取确认：`perf-chip` 可显示内存与 FPS 文本，进程保持 `Responding=True`，未发现崩溃或数值消失。

## 结论

- C-DHF-06 采集与可视化实现完成，四态真实基线数据已生成；
- 已修复 `save_performance_baseline` 覆盖旧模式的缺陷，改为按 `mode` 合并；
- 已核对机器规格元数据、采样方法记录、Release 实时数值可视化和自动化验证；
- C-DHF-06 标记为 `COMPLETE`。
