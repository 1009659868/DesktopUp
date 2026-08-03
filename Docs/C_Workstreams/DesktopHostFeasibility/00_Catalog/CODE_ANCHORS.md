# Desktop Host Feasibility 代码锚点

状态：`ACTIVE IMPLEMENTATION`

## 前端 Demo

- [`app/index.html`](../../../../app/index.html)：Demo 页面和主界面结构。
- [`app/src/main.js`](../../../../app/src/main.js)：交互绑定、渲染、本地演示状态，以及手动/自动暂停状态的控制中心预览同步。
- [`app/src/model.js`](../../../../app/src/model.js)：海报、图标、主题和布局的纯状态模型。
- [`app/src/styles.css`](../../../../app/src/styles.css)：Windows 11 风格控制中心与桌面海报视觉。
- [`app/poster.html`](../../../../app/poster.html)：独立透明海报宿主页面。
- [`app/src/poster-surface.js`](../../../../app/src/poster-surface.js)：独立海报状态同步、锁定、原生拖动、收起交互、运行时自动暂停，以及 WebView DPI/无动画布局区域上报。
- [`app/src/poster-surface.css`](../../../../app/src/poster-surface.css)：独立宿主透明背景与满窗海报适配。
- [`tests/model.test.mjs`](../../../../tests/model.test.mjs)：状态模型单元测试。
- [`tests/runtime-surface.test.mjs`](../../../../tests/runtime-surface.test.mjs)：冷启动仅海报可见、Release GUI 子系统、第二实例/托盘显式呼出、主页关闭隐藏，以及后台状态线程不得持锁同步托盘的运行表面契约测试。
- [`scripts/build-ui.mjs`](../../../../scripts/build-ui.mjs)：零额外依赖静态前端构建。
- [`scripts/capture-demo.mjs`](../../../../scripts/capture-demo.mjs)：浏览器交互冒烟与截图脚本。

## Tauri 外壳

- [`src-tauri/src/main.rs`](../../../../src-tauri/src/main.rs)：原生应用入口；Release 使用 Windows GUI 子系统，不创建控制台或 Windows Terminal 窗口。
- [`src-tauri/src/lib.rs`](../../../../src-tauri/src/lib.rs)：主页/海报双窗体生命周期、宿主诊断状态、单实例、原生拖动/收起、WebView DPI 到物理窗体尺寸换算、通知区域菜单、后台状态快照到主线程 UI 的无锁调度，以及海报原生标题强制置空。
- [`src-tauri/src/desktop_host.rs`](../../../../src-tauri/src/desktop_host.rs)：隔离的 WorkerW 发现/挂载、超时保护、统一 DPI 的原生窗口区域命中、显式 DWM 非客户区/边框抑制、无装饰 `ToolWindow` 自动回退，以及基于 `EVENT_SYSTEM_FOREGROUND` 的 Win+D/Shell 恢复监视器。
- [`src-tauri/src/placement.rs`](../../../../src-tauri/src/placement.rs)：版本化海报位置快照、300ms 合并写盘和用户应用配置目录持久化。
- [`src-tauri/src/runtime_monitor.rs`](../../../../src-tauri/src/runtime_monitor.rs)：前台全屏/最大化、WTS 锁屏和显示器电源事件归一化，托盘/系统弹层临时前台继承，确定性自动暂停状态、一次性启动复核与 HWND 黑盒诊断。
- [`src-tauri/tauri.conf.json`](../../../../src-tauri/tauri.conf.json)：主页冷启动隐藏、透明海报默认可见、海报原生标题为空，以及 Release/NSIS 构建配置。
- [`src-tauri/capabilities/default.json`](../../../../src-tauri/capabilities/default.json)：主页与海报窗口的最小能力声明。
- [`src-tauri/icons/app-icon.svg`](../../../../src-tauri/icons/app-icon.svg)：A0 应用图标矢量源。
- [`src-tauri/Cargo.lock`](../../../../src-tauri/Cargo.lock)：已验证的 Rust 依赖锁文件。

- [`src-tauri/src/webview2.rs`](../../../../src-tauri/src/webview2.rs)：WebView2 DefaultBackgroundColor 透明化入口（当前为占位桩，等待 Tauri 稳定 API 后接入）

## 尚未建立的锚点

- `webview2.rs` 中的 `ICoreWebView2Controller2::SetDefaultBackgroundColor` 实际调用——当前为安全占位桩，等 Tauri 提供稳定 WebView 控制器访问路径后接回第三条透明层
- Explorer 重启后的完整人工验证（Shell 创建/显示事件恢复已接入，`TaskbarCreated` 托盘恢复属于 C-DHF-07）；
- 多海报交互区域合并和人工桌面输入矩阵（单海报原生圆角区域已经通过）；
- C-DHF-05 真实全屏、锁屏、显示器关闭及多屏人工矩阵（事件驱动状态机已经建立）；
- `TaskbarCreated` 通知区域恢复（A0 快捷主题菜单已建立）；
- Explorer/显示器恢复协调器；
- 性能探针与原生集成测试。
