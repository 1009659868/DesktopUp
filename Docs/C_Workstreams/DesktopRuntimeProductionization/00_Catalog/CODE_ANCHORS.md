# Desktop Runtime Productionization 代码锚点

最后检查：2026-08-11

## C-DRP-01 稳定契约锚点

- [`src-tauri/src/host_contract.rs`](../../../../src-tauri/src/host_contract.rs)：`host-contract-v1` Rust 权威 DTO 与验证；包含 HostSurfaceRequest/Snapshot、DisplayTopology、InteractionRegion、placement intent、physical/virtual geometry 和非法输入测试。
- [`app/src/host-contract.js`](../../../../app/src/host-contract.js)：前端 contractVersion、请求归一化、非法值拒绝和 HostSurface snapshot 兼容判断。
- [`src-tauri/src/lib.rs`](../../../../src-tauri/src/lib.rs)：唯一 canonical `sync_host_surface` 命令、revision 串行协调、DisplayTopology 采集、原生尺寸/位置/命中应用；旧 `resize_poster_surface` 与 `set_poster_interaction_region` 仅构造请求并进入同一内部函数。
- [`app/src/poster-runtime.js`](../../../../app/src/poster-runtime.js)：海报业务对象到 HostSurfaceRequest 的单向适配器；宿主与 interaction region 宽高来自目标业务状态，DOM 只提供表面偏移；持久海报快照不写入运行时宿主字段。
- [`app/src/poster-surface.js`](../../../../app/src/poster-surface.js)：生产页面只调用一次 `sync_host_surface`，从 RuntimeSnapshot 续接 revision，避免 WebView 重载后的陈旧请求；完整同步链路有可恢复异常边界。
- [`tests/host-contract.test.mjs`](../../../../tests/host-contract.test.mjs)：前端版本、默认 preserve、Windows 显示器 ID、非法区域/比例、adapter、native geometry owner，以及旧展开测量下的收起/展开尺寸回归测试。
- [`tests/poster-runtime.test.mjs`](../../../../tests/poster-runtime.test.mjs)：RuntimeSnapshot/HostSurface 传递、持久/临时字段分离和 canonical adapter 测试。

## 已保留的 A0 输入锚点

- [`src-tauri/src/lib.rs`](../../../../src-tauri/src/lib.rs) 的 preserve placement 分支：首次同步按持久化锚点恢复；后续收起/展开在 resize 前捕获物理左上角并在 resize 后恢复，保证顶部栏屏幕坐标不随高度变化。

- [`src-tauri/src/desktop_host.rs`](../../../../src-tauri/src/desktop_host.rs)：WorkerW/ToolWindow、桌面 Z 序、原生命中和 Shell 恢复适配层。
- [`src-tauri/src/runtime_monitor.rs`](../../../../src-tauri/src/runtime_monitor.rs)：前台覆盖、WTS 和显示器电源事件归一化。
- [`src-tauri/src/placement.rs`](../../../../src-tauri/src/placement.rs)：A0 单窗体位置持久化；C-DRP 只保留宿主坐标能力，海报业务几何随后交给 C-PCB/C-PSS。
- [`src-tauri/src/poster_snapshot.rs`](../../../../src-tauri/src/poster_snapshot.rs)：过渡 JSON 海报快照桥接；不是 C-DRP 的正式状态仓库。
- [`tests/runtime-surface.test.mjs`](../../../../tests/runtime-surface.test.mjs)：现有运行表面行为测试入口。

## C-DRP-02 已建立锚点

- [`src-tauri/src/host_topology.rs`](../../../../src-tauri/src/host_topology.rs)：唯一桌面宿主标签/数量不变量、HostTopologySnapshot、effective logical work area、响应比例纯函数和分辨率/DPI 矩阵。
- [`src-tauri/src/host_contract.rs`](../../../../src-tauri/src/host_contract.rs)：DisplayTopology scalePolicy、monitor/renderer/effective 比例、目标显示器与 HostSurfaceSnapshot 的版本化诊断字段。
- [`src-tauri/src/lib.rs`](../../../../src-tauri/src/lib.rs)：目标显示器选择、renderer scale 注入、响应宿主尺寸、同尺度 interaction region、DPI 变化重同步和 RuntimeSnapshot 拓扑发布。
- [`app/src/host-presentation.js`](../../../../app/src/host-presentation.js)：原生 HostSurfaceSnapshot 到设计逻辑尺寸/前端视觉比例的纯映射。
- [`app/src/poster-surface.css`](../../../../app/src/poster-surface.css) 与 [`app/src/poster-surface.js`](../../../../app/src/poster-surface.js)：固定设计画布、响应等比变换、目标显示器诊断和 canonical sync 呈现。
- [`scripts/probe-poster-cdp.mjs`](../../../../scripts/probe-poster-cdp.mjs)：仅测试启动参数下的 Release DOM/RuntimeSnapshot 与展开/收起往返探针；普通 Release 不开放 CDP。
- [`tests/host-presentation.test.mjs`](../../../../tests/host-presentation.test.mjs)、[`tests/runtime-surface.test.mjs`](../../../../tests/runtime-surface.test.mjs) 与 Rust `host_topology` tests：单宿主、响应矩阵、renderer/monitor 分层和宿主/命中同尺度保护。
- [`src-tauri/src/lib.rs`](../../../../src-tauri/src/lib.rs) 的 `PosterSurfaceLifecycle`/`mark_poster_surface_ready` 与 [`app/src/poster-surface.js`](../../../../app/src/poster-surface.js) 的 ready 握手：冷启动先隐藏宿主，按宿主样式、最终尺寸、最终 region、CSS 稳定帧顺序完成后再显示，防止 Windows 11 DWM 顶部白条。
- [`src-tauri/src/desktop_host.rs`](../../../../src-tauri/src/desktop_host.rs) 的 `WM_STYLECHANGING`/`strip_non_client_decoration_styles`：在 Win32 消息边界阻止 Tauri/tao 后续生命周期重新写回 `WS_CAPTION` 等非客户区 style；一次性初始化顺序不能替代该不变量。

## C-DRP-03 完成锚点

- [`src-tauri/src/host_contract.rs`](../../../../src-tauri/src/host_contract.rs)：`surfaceRegistry v1` 中每个 SurfaceRegistration 明确拆分完整 `visualRegion` 与零到多个 `interactionRegions`；当前生产和验收只登记一个海报表面，结构性多表面容量不代表当前已具备多海报产品能力。
- [`app/src/host-contract.js`](../../../../app/src/host-contract.js)：与 Rust 对齐的 visual/input region 规范化、重复 ID/越界/版本拒绝和 legacy `interactionRegion` 兼容入口。
- [`app/src/poster-runtime.js`](../../../../app/src/poster-runtime.js) 与 [`app/src/poster-surface.js`](../../../../app/src/poster-surface.js)：生产适配器提交一张海报的完整视觉轮廓；首次布局测量会映射到目标视觉宽度并裁切边界、过滤隐藏零尺寸卡片，避免旧原生窗口尺寸导致首次 sync 失败；页面同时阻止 WebView 默认右键菜单。
- [`src-tauri/src/desktop_host.rs`](../../../../src-tauri/src/desktop_host.rs)：`apply_surface_regions` 只把完整视觉轮廓通过 GDI/`SetWindowRgn` 写入窗口形状；独立输入命中图按相同 effective scale 换算。`WH_MOUSE_LL` 事件钩子在空白与命中区之间切换 `WS_EX_TRANSPARENT | WS_EX_LAYERED`，不轮询；消息级扩展样式不变量持续移除 `APPWINDOW` 并恢复 `TOOLWINDOW/NOACTIVATE`。
- [`src-tauri/src/lib.rs`](../../../../src-tauri/src/lib.rs)：canonical sync、显示变化 resync、旧 resize 兼容、宿主重配置和 ready 握手共用 visual/input 两张区域图与同一 revision；折叠时视觉轮廓被收敛到新逻辑尺寸。
- [`tests/host-contract.test.mjs`](../../../../tests/host-contract.test.mjs)、[`tests/poster-runtime.test.mjs`](../../../../tests/poster-runtime.test.mjs)、[`tests/runtime-surface.test.mjs`](../../../../tests/runtime-surface.test.mjs) 与 Rust tests：覆盖首次窗口/目标海报尺寸不匹配、隐藏卡片、单表面多命中区、视觉/输入分离、APPWINDOW 回写、圆角命中和 DPI 单次换算。
## C-DRP-04 完成锚点

- [`src-tauri/src/desktop_host.rs`](../../../../src-tauri/src/desktop_host.rs)：`HostRecoveryTrigger`/`HostRecoveryRequest`、140ms 尾沿合并、互斥待处理快照和单工作器；显示/DPI/工作区消息只生成恢复请求。Shell 前台事件还会启动有界 Z 序防护，但不改写位置、尺寸或窗口 region。
- [`src-tauri/src/lib.rs`](../../../../src-tauri/src/lib.rs)：`HostRecoveryCoordinator` 与 `recover_desktop_host` 串行处理所有恢复；Shell-only 请求优先执行 `shellRepair`，只有挂载失效或显示/DPI/工作区/手动重连才执行宿主重配、唯一 `PlacementStore` 位置恢复和 HostSurface 新 revision 重同步。
- [`src-tauri/src/desktop_host.rs`](../../../../src-tauri/src/desktop_host.rs) 的 `guard_tool_window_during_shell_transition` / `release_tool_window_shell_z_order_guard` / `repair_shell_host`：Progman 前台切换期间以 48ms `TOPMOST | NOACTIVATE` 防住 Shell 第二次无事件 Z 序重排，普通应用恢复前台或超时后立即 `NOTOPMOST` 并重锚；常规 `shellRepair` 仍不移动、不缩放、不使用 `SWP_FRAMECHANGED`，WorkerW 只校验父窗口。
- [`src-tauri/src/desktop_host.rs`](../../../../src-tauri/src/desktop_host.rs) 的 `DesktopHostDiagnostics`：恢复次数、代数、真实合并事件数、最近触发原因、`lastRecoveryAction` 与最近错误可通过 RuntimeSnapshot/诊断命令观察；普通宿主重配保留历史恢复诊断。
- [`tests/runtime-surface.test.mjs`](../../../../tests/runtime-surface.test.mjs) 与 Rust `recovery_tests`：约束入口收敛、尾沿稳定判断、原因合并、事件计数、Shell-only 在完整 configure 前返回和诊断序列化。
- [`2026-08-10_cdrp04_recovery_coordinator_first_slice.md`](../20_Log/2026-08/2026-08-10_cdrp04_recovery_coordinator_first_slice.md)：审计结论、第一切片实现、自动验证与剩余单显示器人工矩阵。

## C-DRP-05 完成锚点

- [`src-tauri/src/runtime_lifecycle.rs`](../../../../src-tauri/src/runtime_lifecycle.rs)：`runtime-lifecycle-v1` 纯归约器；定义 starting/running/exiting、activeStatic/activeAnimated/paused/stopped、generation/eventCount、退出粘性和安全优先组合测试。
- [`src-tauri/src/lib.rs`](../../../../src-tauri/src/lib.rs)：RuntimeSnapshot 暴露 `runtimeLifecycle`；`mutate_runtime_state` 只在锁内修改/克隆，`publish_runtime_snapshot` 在锁外发布诊断、托盘与 `runtime-state-changed`。显隐、ready、FPS、暂停和退出共用该入口。
- [`src-tauri/src/runtime_monitor.rs`](../../../../src-tauri/src/runtime_monitor.rs)：前台覆盖、WTS 锁屏与显示电源事件合并和去重；托盘/Shell 临时前台保持上一覆盖状态，不产生高频轮询。
- [`app/src/poster-runtime.js`](../../../../app/src/poster-runtime.js)：版本化 lifecycle 归一化、generation + eventCount 乱序拒绝、统一 `runtimeMode` 和本地 document.hidden 安全停绘。
- [`app/src/main.js`](../../../../app/src/main.js) 与 [`app/src/poster-surface.js`](../../../../app/src/poster-surface.js)：只监听 `runtime-state-changed`；不再从 visibilitychange 重建原生状态。控制中心持续性能轮询已删除，海报只有 activeAnimated 才保持 rAF。
- [`tests/poster-runtime.test.mjs`](../../../../tests/poster-runtime.test.mjs)、[`tests/runtime-surface.test.mjs`](../../../../tests/runtime-surface.test.mjs) 与 Rust lifecycle tests：覆盖组合、重复/乱序、暂停安全优先、显隐/ready 恢复、退出、状态优先原生调用、锁外发布和无持续轮询。
- [`2026-08-10_cdrp05_lifecycle_interface_first_slice.md`](../20_Log/2026-08/2026-08-10_cdrp05_lifecycle_interface_first_slice.md)：审计、实现、自动验证、Release 产物与用户确认 5/5 通过的人工矩阵。


## C-DRP-02 延期至 C-DRP-06 的证据

- 真实负坐标副屏与混合 DPI 跨屏；
- 主屏切换、热插拔和跨屏重启恢复后的比例/锚点矩阵。

## C-DRP-06 当前实现锚点

- [`src-tauri/src/performance_gate.rs`](../../../../src-tauri/src/performance_gate.rs)：`performance-gate-v3` 的主进程私有工作集 50 MiB、完整后代进程树私有工作集 256 MiB、120 秒稳定态、四态 10 秒窗口、采集完整性、Release/Host Contract 一致性和失败代码测试；当前静态产品允许动画调度状态为 0 FPS，但任何非零帧仍受 30 FPS 上限约束。
- [`src-tauri/src/lib.rs`](../../../../src-tauri/src/lib.rs)：ToolHelp 后代枚举及树内存归集、PerformanceSnapshot 的主进程/进程树诊断字段、`performance-baseline-v4.json`、显式 `--cdrp-performance-gate` 原生四态探针及 0/2 退出码；`MainWindowCoordinator` 负责控制中心首次请求创建、ready 前事件排队和并发去重。
- [`src-tauri/tauri.conf.json`](../../../../src-tauri/tauri.conf.json)：冷启动只静态创建 `poster`；`main` 控制中心不再占用冷启动 WebView。
- [`app/src/main.js`](../../../../app/src/main.js)、[`app/src/performance-monitor.js`](../../../../app/src/performance-monitor.js) 与 [`app/src/poster-surface.js`](../../../../app/src/poster-surface.js)：6 点真实 10 秒采样、主进程/进程树完整样本字段、50/256 MiB 双预算 UI 状态和最高 30 FPS 的 timer+rAF 调度。
- [`scripts/run-performance-gate.mjs`](../../../../scripts/run-performance-gate.mjs)：启动 Release 原生性能门、拒绝陈旧 `performance-baseline-v4.json` 并输出最终版本化 Gate；[`tests/runtime-surface.test.mjs`](../../../../tests/runtime-surface.test.mjs) 与 Rust tests 约束懒创建、显式诊断入口、双预算边界和采集完整性。
- [`2026-08-11_cdrp06_release_performance_gate_first_slice.md`](../20_Log/2026-08/2026-08-11_cdrp06_release_performance_gate_first_slice.md)：审计、两轮实测、产物哈希、稳定接口、兼容移除窗口与剩余物理门。
- [`2026-08-11_cdrp06_stable_working_set_correction.md`](../20_Log/2026-08/2026-08-11_cdrp06_stable_working_set_correction.md)：推翻早期窗口 PASS 的稳定态曲线、用户双预算决策、v3/v4 最终 PASS、懒创建进程树证据与最新版产物哈希。
