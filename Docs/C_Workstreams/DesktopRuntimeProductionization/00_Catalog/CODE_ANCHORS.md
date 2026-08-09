# Desktop Runtime Productionization 代码锚点

最后检查：2026-08-09

## C-DRP-01 稳定契约锚点

- [`src-tauri/src/host_contract.rs`](../../../../src-tauri/src/host_contract.rs)：`host-contract-v1` Rust 权威 DTO 与验证；包含 HostSurfaceRequest/Snapshot、DisplayTopology、InteractionRegion、placement intent、physical/virtual geometry 和非法输入测试。
- [`app/src/host-contract.js`](../../../../app/src/host-contract.js)：前端 contractVersion、请求归一化、非法值拒绝和 HostSurface snapshot 兼容判断。
- [`src-tauri/src/lib.rs`](../../../../src-tauri/src/lib.rs)：唯一 canonical `sync_host_surface` 命令、revision 串行协调、DisplayTopology 采集、原生尺寸/位置/命中应用；旧 `resize_poster_surface` 与 `set_poster_interaction_region` 仅构造请求并进入同一内部函数。
- [`app/src/poster-runtime.js`](../../../../app/src/poster-runtime.js)：海报业务对象到 HostSurfaceRequest 的单向适配器；宿主与 interaction region 宽高来自目标业务状态，DOM 只提供表面偏移；持久海报快照不写入运行时宿主字段。
- [`app/src/poster-surface.js`](../../../../app/src/poster-surface.js)：生产页面只调用一次 `sync_host_surface`，从 RuntimeSnapshot 续接 revision，避免 WebView 重载后的陈旧请求；完整同步链路有可恢复异常边界。
- [`tests/host-contract.test.mjs`](../../../../tests/host-contract.test.mjs)：前端版本、默认 preserve、Windows 显示器 ID、非法区域/比例、adapter、native geometry owner，以及旧展开测量下的收起/展开尺寸回归测试。
- [`tests/poster-runtime.test.mjs`](../../../../tests/poster-runtime.test.mjs)：RuntimeSnapshot/HostSurface 传递、持久/临时字段分离和 canonical adapter 测试。

## 已保留的 A0 输入锚点

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

## C-DRP-02 剩余证据

- 真实负坐标副屏与混合 DPI 跨屏；
- 主屏切换、热插拔和跨屏重启恢复后的比例/锚点矩阵。
