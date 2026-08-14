## 2026-08-14 — C-RLC-06 Release 生命周期封板

状态：`COMPLETE — B-DRF / C-RLC CLOSED`

C-RLC-05 用户 Explorer/退出竞态矩阵通过。最终 Node/文档 159/159、Rust 132/132、Release/NSIS、安装内容/依赖、多进程和注册表探针通过；生命周期矩阵封存，C-RLC-01..06 全部关闭。B-DRF 完成并交回 B-PCR；C-LST 是待用户激活的下一候选。
# Desktop Runtime Foundation 模块日志

## 2026-08-14 — C-RLC-05 TaskbarCreated 恢复与退出顺序

状态：`ACTIVE — C-RLC-05 IMPLEMENTED / RELEASE MANUAL PENDING`

C-RLC-04 用户人工矩阵已通过并关闭。C-RLC-05 已接入 tray-icon 消息窗口的 TaskbarCreated，形成唯一托盘验证/回退重建、宿主有界恢复、degraded/手动重连和不可逆 Exiting 顺序。Node/文档 159/159、Rust 132/132、Release/NSIS 与定向五事件探针通过；当前等待真实 Explorer 重启三轮和退出竞态人工门。
## 2026-08-14 — C-RLC-04 控制中心与托盘快捷入口收敛

状态：`ACTIVE — C-RLC-04 IMPLEMENTED / RELEASE MANUAL PENDING`

`control-center-lifecycle-v1` 已统一显式 ready、有序命令队列和控制中心释放/重建；托盘编辑使用原生 runtime，锁定与主题使用正式 SQLite CAS。Node 156/156、Rust 127/127、Release/NSIS 与资源释放门通过；可见控制中心约 352.47 MiB 峰值单独记录，关闭稳定后进程树约 222.87 MiB。当前只等待用户 Release 人工矩阵，C-RLC-05 尚未激活。

## 2026-08-14 — 完成 C-RLC-03 自启显式 opt-in 与状态同步

状态：`ACTIVE — C-RLC-03 COMPLETE / C-RLC-04 NEXT`

用户授权的 feature-scoped `G:\A_\Tools\Test\DesktopUp.exe` 已通过真实 HKCU enable/noChange/disable、`--autostart` 冷启动和首/次实例分流；最终自启值 absent，其他 Run 值不变。正式安装合同与默认 Release 未改。C-RLC-04“控制中心与托盘快捷入口收敛”为唯一下一任务。

## 2026-08-14 — 修复控制中心桌面几何直接落入编辑画布

状态：`ACTIVE — C-RLC-03 VALIDATION`

- 只读 HWND/SQLite/placement 证据确认近全高海报是合法正式默认状态，不是原生窗口或持久状态损坏；
- 新增独立编辑视口投影和拖动/缩放逆投影，控制中心窗口变化只重绘，不改真实海报；
- 无头 Edge 证明默认海报完整位于控制中心画布，原生海报 CSS 隔离保持；
- Rust 123/123、production UI、Release/NSIS 和依赖门通过，等待最新版 Release 组合人工复测。

## 2026-08-14 — C-RLC-03 拖动层级维护进入复测

状态：`ACTIVE — C-RLC-03 VALIDATION`

- 替换 Windows 系统窗口移动循环，使用既有低级鼠标钩子驱动桌面层拖动；
- 拖动中保持普通应用之后和 NOACTIVATE，松手后重申无标题栏/DWM/窗口区域；
- Node 140/140、Rust 123/123、Release/NSIS 和依赖门通过；
- 当前等待拖动白条/Z-order 人工复测，并区分 G 盘 ineligible 结果与正式目标自启证据。


## 2026-08-13 — C-RLC-03 进入验证门

状态：`ACTIVE — C-RLC-03 VALIDATION`

- 完成正式路径限定的 HKCU Run `DesktopUp` enable/repair/disable 和串行写后读回；
- 托盘与控制中心只消费同一原生自启快照，开发/便携 Release 保持禁用和零写入；
- 隔离矩阵与 Release/NSIS 构建通过；正式安装版真实启用/重启/禁用待用户确认。

## 2026-08-13 — 完成 C-RLC-02

状态：`ACTIVE — C-RLC-02 COMPLETE / C-RLC-03 NEXT`

- 新增 `launch-intent-v1`，统一首/次实例的 interactive/autostart/internal diagnostic/invalid 决策；
- 第二普通实例只呼回唯一控制中心，第二精确 `--autostart` 零 UI 变化，非法组合以 64 退出；
- Node 135/135、Rust 117/117、Release 构建与真实多进程矩阵通过；本轮未写 HKCU；
- C-RLC-03 自启显式 opt-in 与状态同步成为唯一下一任务。

## 2026-08-13 — 激活 C-RLC

状态：`ACTIVE — C-RLC-01 COMPLETE / C-RLC-02 NEXT`

- C-IBC-01..05 关闭后，用户明确激活 Runtime Lifecycle and Quick Access；
- C-RLC-01 建立权威工作流文档并冻结 `runtime-lifecycle-matrix-v1`；本切片没有修改运行时代码或注册表；
- C-RLC-02 启动意图与单实例分流成为唯一下一任务。

## 2026-08-02 — 模块建立

状态：`COMPLETE`

- 建立 Desktop Runtime Foundation 的责任、边界、里程碑和风险回退。
- 激活 `DesktopHostFeasibility` 作为首个 C 工作流。
- 当前没有运行时代码或性能结论；不得把文档设计视为已验证实现。

