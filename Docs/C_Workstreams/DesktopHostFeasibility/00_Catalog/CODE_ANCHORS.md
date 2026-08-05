# Desktop Host Feasibility 代码锚点

状态：`ACTIVE IMPLEMENTATION / M1 RESET BASELINE / FOUNDATION HARDENING`

## 前端 Demo

- [`app/index.html`](../../../../app/index.html)：Demo 页面和主界面结构。
- [`app/src/main.js`](../../../../app/src/main.js)：交互绑定、本地演示状态，以及手动/自动暂停状态的控制中心预览同步；海报标记来自 `poster-core`，状态动作来自 `poster-runtime`。
- [`app/src/model.js`](../../../../app/src/model.js)：海报、图标、主题和布局的纯状态模型。
- [`app/src/poster-core.js`](../../../../app/src/poster-core.js)：海报状态快照、共享渲染标记与存储契约；`main.js` 与 `poster-surface.js` 都从这里读取。
- [`app/src/poster-runtime.js`](../../../../app/src/poster-runtime.js)：窗口几何所有权、USING/EDITING 推导、锁定/收起动作和原生同步参数构造的统一运行时层；与 Rust `RuntimeSnapshot` 使用同一份 camelCase 字段契约。
- [`app/src/styles.css`](../../../../app/src/styles.css)：Windows 11 风格控制中心与桌面海报视觉。
- [`app/poster.html`](../../../../app/poster.html)：独立透明海报宿主页面。
- [`app/src/poster-surface.js`](../../../../app/src/poster-surface.js)：独立海报状态同步、锁定、原生拖动、收起交互、运行时自动暂停，以及 WebView DPI/无动画布局区域上报；海报标记来自 `poster-core`，状态动作来自 `poster-runtime`。
- [`app/src/poster-surface.css`](../../../../app/src/poster-surface.css)：独立宿主透明背景与满窗海报适配。
- [`tests/model.test.mjs`](../../../../tests/model.test.mjs)：状态模型单元测试。
- [`tests/poster-core.test.mjs`](../../../../tests/poster-core.test.mjs)：共享海报渲染与存储契约行为测试。
- [`tests/poster-runtime.test.mjs`](../../../../tests/poster-runtime.test.mjs)：窗口几何、USING/EDITING、锁定/收起和原生同步参数行为测试。
- [`tests/runtime-surface.test.mjs`](../../../../tests/runtime-surface.test.mjs)：Tauri 冷启动窗口契约、Release GUI 子系统、共享海报存储/渲染契约的行为测试。
- [`scripts/build-ui.mjs`](../../../../scripts/build-ui.mjs)：零额外依赖静态前端构建。
- [`scripts/capture-demo.mjs`](../../../../scripts/capture-demo.mjs)：浏览器交互冒烟与截图脚本。

## Tauri 外壳

- [`src-tauri/src/main.rs`](../../../../src-tauri/src/main.rs)：原生应用入口；Release 使用 Windows GUI 子系统，不创建控制台或 Windows Terminal 窗口。
- [`src-tauri/src/lib.rs`](../../../../src-tauri/src/lib.rs)：主页/海报双窗体生命周期、宿主诊断状态、单实例、原生拖动/收起、WebView DPI 到物理窗体尺寸换算、通知区域菜单、后台状态快照到主线程 UI 的无锁调度，以及海报原生标题强制置空；`RuntimeSnapshot` 顶层契约包含 `pauseReasons` 等前端对齐字段。
- [`src-tauri/src/desktop_host.rs`](../../../../src-tauri/src/desktop_host.rs)：隔离的 WorkerW 发现/挂载、超时保护、统一 DPI 的原生窗口区域命中、显式 DWM 非客户区/边框抑制、无装饰 `ToolWindow` 自动回退，以及基于 `EVENT_SYSTEM_FOREGROUND` 的 Win+D/Shell 恢复监视器。
- [`src-tauri/src/placement.rs`](../../../../src-tauri/src/placement.rs)：版本化海报位置快照、300ms 合并写盘和用户应用配置目录持久化。
- [`src-tauri/src/runtime_monitor.rs`](../../../../src-tauri/src/runtime_monitor.rs)：前台全屏/最大化、WTS 锁屏和显示器电源事件归一化，托盘/系统弹层临时前台继承，确定性自动暂停状态、一次性启动复核与 HWND 黑盒诊断。
- [`src-tauri/tauri.conf.json`](../../../../src-tauri/tauri.conf.json)：主页冷启动隐藏、透明海报默认可见、海报原生标题为空，以及 Release/NSIS 构建配置。
- [`src-tauri/capabilities/default.json`](../../../../src-tauri/capabilities/default.json)：主页与海报窗口的最小能力声明。
- [`src-tauri/icons/app-icon.svg`](../../../../src-tauri/icons/app-icon.svg)：A0 应用图标矢量源。
- [`src-tauri/Cargo.lock`](../../../../src-tauri/Cargo.lock)：已验证的 Rust 依赖锁文件。

当前 V1 基线中尚未建立模板加载器、模板运行时或 `webview2.rs`；这些文件只有在对应契约、测试和稳定 API 路径明确后才重新加入。

## 尚未建立的锚点

- WebView2 `DefaultBackgroundColor` 的稳定、可验证接入路径；不得通过猜测子 HWND `GWLP_USERDATA` 并解引用未知 COM 指针实现；
- M1 模板加载器、单根模板 DOM 契约、参数转换和行为测试；
- Explorer 重启后的完整人工验证（Shell 创建/显示事件恢复已接入，`TaskbarCreated` 托盘恢复属于 C-DHF-07）；
- 多海报交互区域合并和人工桌面输入矩阵（单海报原生圆角区域已经通过）；
- C-DHF-05 托盘菜单一致性、手动+自动暂停叠加、恢复后视觉确认待补；真实游戏全屏、Win+L、显示器关闭/唤醒已由用户实机确认；
- `TaskbarCreated` 通知区域恢复（A0 快捷主题菜单已建立）；
- Explorer/显示器恢复协调器；
- 性能探针与原生集成测试。
