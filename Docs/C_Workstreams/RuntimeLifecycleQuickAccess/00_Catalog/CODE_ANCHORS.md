# Runtime Lifecycle and Quick Access 代码锚点

## 现有基线

- [`src-tauri/src/runtime_lifecycle.rs`](../../../../src-tauri/src/runtime_lifecycle.rs)：`runtime-lifecycle-v1` 的 Starting/Running/Exiting、运行模式、generation/eventCount 和退出粘滞状态。
- [`src-tauri/src/control_center_lifecycle.rs`](../../../../src-tauri/src/control_center_lifecycle.rs)：`control-center-lifecycle-v1` 的 absent/hidden/visible、显式 ready、generation/eventCount、严格有序的 ready 前命令队列和释放/重建状态机。
- [`src-tauri/src/lib.rs`](../../../../src-tauri/src/lib.rs)：控制中心串行懒创建/显式 ready/释放重建、托盘菜单/双击、原生 runtime 动作、Poster State Store CAS、单实例回调和托盘退出的统一组合入口。
- [`src-tauri/src/host_topology.rs`](../../../../src-tauri/src/host_topology.rs)：单桌面宿主、零或一控制中心的表面拓扑诊断。
- [`src-tauri/src/desktop_host.rs`](../../../../src-tauri/src/desktop_host.rs)：桌面 Shell 识别、宿主挂载/回退、有界恢复，以及不进入系统移动循环的桌面层拖动事务、松手后 DWM/窗口区域恢复。
- [`src-tauri/src/taskbar_recovery.rs`](../../../../src-tauri/src/taskbar_recovery.rs)：`taskbar-recovery-v1` 状态机、`tray_icon_app` 原生 `TaskbarCreated` 子类监听、去抖/有界重试、degraded/手动恢复与退出移除。
- [`src-tauri/src/runtime_monitor.rs`](../../../../src-tauri/src/runtime_monitor.rs)：前台、锁屏、显示器和 Shell 表面的事件驱动暂停输入。
- [`src-tauri/src/autostart_contract.rs`](../../../../src-tauri/src/autostart_contract.rs)：`autostart-contract-v1`、严格正式路径/命令、`autostart-diagnostics-v1`、`autostart-change-v1`、串行协调器及只触达 HKCU Run `DesktopUp` 的 Win32 后端。
- [`src-tauri/src/launch_intent.rs`](../../../../src-tauri/src/launch_intent.rs)：`launch-intent-v1` 的首/次实例参数表、结构化拒绝、分流 disposition 与单调诊断快照。
- [`src-tauri/src/autostart_test_gate.rs`](../../../../src-tauri/src/autostart_test_gate.rs)：仅在 `autostart-test-root` Cargo feature 下编译的临时路径实测门；请求由第二实例转交第一实例执行，并只向 OS 临时目录写结构化报告。
- [`src-tauri/src/performance_gate.rs`](../../../../src-tauri/src/performance_gate.rs)：内部 `--cdrp-performance-gate` 参数的单一常量权威；只允许首实例诊断运行，不属于产品启动意图。
- [`src-tauri/src/install_paths.rs`](../../../../src-tauri/src/install_paths.rs)：正式安装根、当前可执行文件和自启路径门所需的单一路径解析。
- [`src-tauri/tauri.conf.json`](../../../../src-tauri/tauri.conf.json)：冷启动桌面宿主、控制中心按需创建和 current-user NSIS 配置。
- [`app/src/main.js`](../../../../app/src/main.js)：先注册监听、读取原生快照并完成首帧，再显式标记控制中心 ready；前端 runtime-control 只消费设置/Agent 路由，不再执行编辑/锁定/主题权威动作。
- [`app/src/poster-editor-geometry.js`](../../../../app/src/poster-editor-geometry.js)：桌面矩形到控制中心视口的等比投影、拖动/缩放逆投影和主工作区约束；不得成为原生宿主几何权威。
- [`app/src/poster-state-store.js`](../../../../app/src/poster-state-store.js)：正式默认 `NIGHT OPS` 身份、旧 revision 0 完整签名识别、expected-revision/CAS 恢复及失败保留原状态。
- [`app/src/autostart-control.js`](../../../../app/src/autostart-control.js)：自启原生快照到控制中心 checked/disabled/status 的纯投影和结果提示。

## 现有验证

- [`tests/runtime-surface.test.mjs`](../../../../tests/runtime-surface.test.mjs)：冷启动表面、控制中心懒创建、托盘、退出、桌面重连，以及禁止系统拖动提升和拖动后样式/区域恢复的源码契约。
- [`tests/control-center-lifecycle.test.mjs`](../../../../tests/control-center-lifecycle.test.mjs)：显式 ready、命令有序排队、释放/重建、托盘原生动作与 Poster State Store CAS 的 C-RLC-04 源码契约。
- [`tests/taskbar-recovery.test.mjs`](../../../../tests/taskbar-recovery.test.mjs)：TaskbarCreated 原生入口、唯一托盘、宿主恢复、退出顺序及禁止 TOPMOST 的 C-RLC-05 源码契约。
- [`tests/launch-intent.test.mjs`](../../../../tests/launch-intent.test.mjs)：C-RLC-02 版本化参数表、初始化顺序、第二实例分流和只读诊断源码契约。
- [`tests/autostart-contract.test.mjs`](../../../../tests/autostart-contract.test.mjs)：HKCU exact-value、正式路径、严格 `--autostart`、最小写入 API 和托盘/控制中心统一快照边界。
- [`tests/autostart-control.test.mjs`](../../../../tests/autostart-control.test.mjs)：开发路径禁用、matching/absent、pending/unreadable 和读回失败的控制投影。
- [`tests/poster-editor-geometry.test.mjs`](../../../../tests/poster-editor-geometry.test.mjs)：近全高/legacy/收起态预览、移动逆投影、缩放和工作区约束。
- [`tests/poster-editor-surface.test.mjs`](../../../../tests/poster-editor-surface.test.mjs)：控制中心投影、正式坐标提交和原生表面 CSS 隔离源码契约。
- [`tests/poster-state-store.test.mjs`](../../../../tests/poster-state-store.test.mjs)：干净默认、精确旧默认恢复、任意用户改动保护和恢复写失败保留的前端状态门。
- [`tests/runtime-dependency-handoff.test.mjs`](../../../../tests/runtime-dependency-handoff.test.mjs)：C-IBC 到 C-RLC 的依赖和 owner 交接门。
- [`tests/poster-state-recovery-gate.test.mjs`](../../../../tests/poster-state-recovery-gate.test.mjs)：恢复探针必须先于 Tauri 与单实例初始化退出，避免干扰正常进程。
- [`scripts/probe-poster-cdp.mjs`](../../../../scripts/probe-poster-cdp.mjs)：在可用 CDP 会话中读取 presentation、runtime、launch-intent、autostart 与正式 poster state 只读诊断。

## C-RLC 实施落点

- C-RLC-02 已通过 [`launch_intent.rs`](../../../../src-tauri/src/launch_intent.rs) 收敛 [`lib.rs`](../../../../src-tauri/src/lib.rs) 的进程参数解析与 `tauri_plugin_single_instance` 回调；精确 `--autostart` 不会打开控制中心。
- C-RLC-03 已关闭：正式 `%LOCALAPPDATA%` 合同未改，`autostart-test-root` 只允许精确 `G:\A_\Tools\Test\DesktopUp.exe` 作为临时 eligible 目标；真实 enable/noChange/`--autostart`/普通二次启动/disable/readback 与其他 Run 值不变均已通过。
- C-RLC-04 已实现：`MainWindowCoordinator` 串行管理 `control-center-lifecycle-v1`，关闭优先释放 WebView；托盘编辑直接修改原生 runtime，锁定与主题通过正式仓库 CAS；控制中心显式 ready 前的设置/Agent 命令按序且只投递一次，不建立第二套前端权威。
- C-RLC-05 已实现：`TaskbarCreated` 监听复用 tray-icon 消息窗口，事件经 `taskbar-recovery-v1` 合并并有界重试；托盘验证失败才执行单 ID 回退重建，宿主通过既有恢复协调器重连，Exiting 先拆监听与恢复队列且禁止旧 TOPMOST 轮询。

- C-RLC-06 已封板：上述锚点由全量 Node/Rust、Release/NSIS、多进程/注册表探针及 C-RLC-03..05 用户人工矩阵共同覆盖；后继不得复制 launch intent、托盘、控制中心、自启或 TaskbarCreated 权威。

本文件只登记锚点。目标行为以 [`RUNTIME_LIFECYCLE_MATRIX_V1.md`](../10_Plan/RUNTIME_LIFECYCLE_MATRIX_V1.md) 为唯一矩阵权威。
