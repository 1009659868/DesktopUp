# C-DRP-02 冷启动顶部 DWM 白条修复

日期：2026-08-09

状态：`FIX VERIFIED`

## 现象

海报以展开状态首次启动时，顶部稳定出现一条 Windows/DWM 浅色非客户区；执行一次收起后白条消失，再展开也不会复现。DOM 尺寸、透明背景、无装饰配置和 HostSurface 比例均正常，因此不能按普通 CSS 裁切问题处理。

## 根因

首次启动时原生窗口过早可见，而且最终一次 `SWP_FRAMECHANGED` 可能发生在圆角 window region 写入之后。Windows 11 DWM 因而保留一帧陈旧的非客户区边框；收起操作会再次按“窗口尺寸 → window region”顺序写入，所以缺陷被动消失。

## 修复

- Tauri 中的 `poster` 原生窗口改为冷启动隐藏；
- `POSTER_VISIBLE_INTENT` 默认关闭，Shell 恢复线程不得在 HostSurface 完成前抢先显示窗口；
- 新增 `PosterSurfaceLifecycle` 与 RuntimeSnapshot `hostSurfaceReady` 诊断；
- 前端在收到 canonical `sync_host_surface` 结果、应用响应式 CSS 变量并稳定两个 animation frame 后，调用 `mark_poster_surface_ready`；
- 原生 ready 收口严格执行“宿主样式/`SWP_FRAMECHANGED` → 最终物理尺寸 → 最终圆角 region → 显示”；
- 托盘显隐读取运行时可见意图，避免把初始化隐藏状态误判为用户主动隐藏。

## 验证

- `pnpm run check`：45 项 Node/契约测试通过；加入本记录后，92 份文档链接复检通过；
- `cargo test --manifest-path src-tauri/Cargo.toml`：31 项通过；
- `cargo tauri build --no-bundle`：正式 Release 构建通过；
- 2560×1440、WebView2 DPR 1.2、responsive 1.074 环境，以持久化展开状态启动全新 Release 进程；
- 首次可见时 `hostSurfaceReady=true`、HostSurface `657×531`、圆角 interaction region 已完成；
- 未点击收起的冷启动实屏截图确认顶部白条消失。

## 结论

该缺陷已在当前主显示器环境关闭。C-DRP-02 仍仅因真实副屏/混合 DPI 物理矩阵保持 `IN PROGRESS`。
