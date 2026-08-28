# QTS-03 生产工程骨架与合同夹具审计（2026-08-27）

状态：`COMPLETE — RELEASE BUILD + 9/9 AUTOMATED FIXTURES PASS / QTS-04 READY`

关联输入：

- [Qt 方案总执行计划](QT_SOLUTION_EXECUTION_PLAN_V1.md)
- [QTS-02 生产物理架构](QTS_02_PRODUCTION_PHYSICAL_ARCHITECTURE_V1.md)
- [QTS-02 一致性审计与 QTS-03 交接](QTS_02_CONSISTENCY_AUDIT_AND_QTS_03_HANDOFF_2026-08-26.md)

## 1. 本轮完成范围

QTS-03 首次建立根级正式 CMake 工程和 `src/` 生产源码树，不再把 `prototype/` 候选程序当作产品 composition root。已建立：

1. 常驻 `desktopup-runtime` 可执行单元；
2. 独立按需 `desktopup-control-center` Qt Quick 可执行单元；
3. framework-free `contracts`、`core` 和保留的 Item/Layout/Theme/AI/FileSafety 域边界；
4. Windows SQLite C API StateStore 与专用单写者线程；
5. 当前用户 ACL + 当前会话命名的 named pipe、单实例锁、secondary intent forwarding；
6. 独立 `DesktopHostPort`、`RendererPort` 与 QPainter ARGB32 premultiplied 离屏帧夹具；
7. revision/SurfaceEpoch/bounds first-frame gate、sticky exit、资源所有权账本和脱敏诊断；
8. Release 构建预设、架构依赖门和 9 项 CTest 入口。

本轮没有实现真实 WorkerW 海报、托盘、完整控制中心业务、真实 Item、主题包或 AI；这些不得因“骨架可运行”而预填通过。

## 2. 正式目标与依赖边界

| 目标 | 物理职责 | 允许的关键依赖 | 自动边界 |
| --- | --- | --- | --- |
| `desktopup_contracts` | DTO、ID、错误、端口、IPC wire types | C++ 标准库 | 无 Qt/Win32/SQLite/provider SDK |
| `desktopup_core` | accepted state、frame gate、单调 snapshot cursor | contracts | 无 Qt/Win32/SQLite |
| `desktopup_state_store_sqlite` | SQLite load/commit/CAS、单写者 worker | contracts、`winsqlite3` | 不进入 Control Center |
| `desktopup_runtime_ipc_win32` | named pipe、ACL、session mutex、intent forwarding | contracts、Win32 | 不承载像素、HWND、SQLite handle |
| `desktopup_desktop_host_win32` | 本阶段 Host 生命周期/epoch 夹具 | contracts | 真实 WorkerW adapter 留给 QTS-04 |
| `desktopup_renderer_qpainter` | QPainter/QImage 离屏 renderer 边界 | contracts、QtGui | 无 QtQuick/QML |
| `desktopup-runtime` | 状态、Host、Renderer、IPC、退出 composition root | QtCore、QtGui、上述 runtime adapters | PE 无 QtQuick/QML/QuickControls |
| `desktopup-control-center` | 短命 Qt Quick 窗口与 IPC client | QtCore/Gui/Qml/Quick | 无 SQLite/DesktopHost |

源级依赖门还扫描 `contracts/core` 的禁止 include，并验证正式 `src/` 中 axiom-runtime 依赖为零。

## 3. StateStore 合同结果

### 3.1 唯一状态路径

~~~text
Command -> Runtime/Core -> StateStoreWorker -> SQLite CAS
        -> CommitAccepted(revision) -> AcceptedState -> projections
~~~

- 新库返回 `NoData`，不会冒充损坏；
- 无效 SQLite 文件返回 `Corrupt`，不会被空状态覆盖；
- InitializeState 使用 `expectedRevision=0` 的普通 commit，产生 revision 1；
- expected revision 相等时接受并递增；不相等时返回 `Conflict` 和当前 snapshot；
- `AcceptedState` 拒绝低 revision 的迟到结果，不能倒退内存 accepted copy；
- `StateStoreWorker` 公开 future API，SQLite connection 只驻留在单一 worker thread，并具有明确 `stopped` 终态。

当前 payload 仍是最小版本化 JSON 字符串；完整 Poster/Item/Theme schema 不属于 QTS-03。

## 4. IPC 与进程角色合同结果

- pipe DACL 只允许 `SYSTEM` 和当前用户，服务端再以客户端 PID 的 process token SID 做 fail-closed 对比；
- pipe 和 mutex 名称包含当前 Windows session；
- protocol header 冻结 magic、version、required flag 和 4096-byte 最大 payload；
- handshake 协商版本与 capability；未知 required intent、超长 payload 和不匹配 peer 均拒绝；
- 普通启动、自启和打开控制中心三类 intent 保持可区分；
- 第一 Runtime 持有 session mutex，第二 Runtime 只转发 intent 并退出；
- `SnapshotAcceptedPayload` 和 `SnapshotCursor` 建立单调订阅合同，低 revision 事件不能回滚；
- Control Center 关闭即进程退出；真实二进程夹具确认主 Runtime 仍持续运行；
- Control Center 不拥有 StateStore，因此断线 draft 没有直接 commit 通道。

完整 snapshot 广播、控制中心 draft/rebase 和命令总线业务在 QTS-05 接入；QTS-03 只冻结 wire shape、单调性和物理访问边界。

## 5. Host/Renderer 与退出合同结果

- `IRenderer` 与 `IDesktopHost` 是两个独立端口；
- QPainter 夹具创建 `QImage::Format_ARGB32_Premultiplied`，frame 带 revision、SurfaceEpoch、bounds 和 opaque buffer handle；
- revision、epoch 或 bounds 不一致时，first-frame gate/Host 均拒绝发布；
- first frame 接受前不视为已发布；
- `BeginExit` 是 sticky，迟到 render 不再分配 buffer，迟到 frame 不再发布；
- Runtime 按 `IPC -> renderer buffer -> Host -> StateStore worker -> instance lock` 收敛所有权；
- 退出账本导出 terminal state、Revision、SurfaceEpoch 和 owned resource count；测试结果为 0；
- 本地路径经诊断脱敏函数处理。

## 6. Release 证据

构建环境：Qt 6.8.3 `msvc2022_64`、Visual Studio 2026 generator + v143、Windows SDK 10.0.26100.0、CMake Release。

~~~powershell
cmake --preset windows-x64-release
cmake --build --preset windows-x64-release --parallel
ctest --preset windows-x64-release --output-on-failure
~~~

结果：`100% tests passed, 9 tests, 0 failed`。

| CTest | 覆盖 | 结果 |
| --- | --- | --- |
| `qts03.contracts` | accepted revision、stale result、frame gate、sticky exit、snapshot cursor | PASS |
| `qts03.state-store` | NoData/Corrupt、Initialize、CAS accepted/conflict、worker terminal | PASS |
| `qts03.ipc` | session mutex、三类 intent、ACL peer、max message、unknown required、pipe cleanup | PASS；IPC 单项另连续 10 轮通过 |
| `qts03.lifecycle` | QPainter frame、Host epoch、late frame、ledger、redaction | PASS |
| `qts03.architecture` | Runtime/Control Center/contract/core/Axiom 禁止依赖 | PASS |
| `qts03.runtime-exit` | 无 Control Center 启动并定时正常退出 | PASS |
| `qts03.exit-ledger` | terminal state、零自有资源、无 WAL/SHM/tmp 残留 | PASS |
| `qts03.control-center-close` | 独立 Control Center 关闭后进程退出 | PASS |
| `qts03.process-roles` | primary/secondary、Control Center handshake/close、Runtime 保持、最终退出 | PASS |

PE 依赖核对：

- `desktopup-runtime.exe`：`winsqlite3.dll`、`ADVAPI32.dll`、`Qt6Gui.dll`、`Qt6Core.dll` 及系统/VC runtime；没有 QtQuick/QML/QuickControls/WebView；
- `desktopup-control-center.exe`：QtGui/Qml/Core（Qt Quick 模块按 QML 运行路径装载）；没有 winsqlite3 或 DesktopHost adapter。

## 7. QTS-03 退出门审计

| 退出门 | 结果 |
| --- | --- |
| 正式源码树、CMake 和 Release 构建 | PASS |
| Runtime / Control Center 独立部署单元 | PASS |
| Runtime 无 Control Center 可启动退出 | PASS |
| named pipe handshake、single-instance secondary forwarding | PASS |
| SQLite load/NoData/Corrupt/CAS/conflict | PASS |
| Host/Renderer revision/epoch/bounds/first-frame gate | PASS |
| 依赖禁止门与 axiom-runtime 零依赖 | PASS |
| 退出后进程、pipe、WAL/SHM/tmp、自有资源归零 | PASS |
| 未复制 candidate demo、旧 schema 或 installer | PASS |
| 代码锚点、日志、交接与 QTS-04 输入 | PASS |

## 8. 保留风险与证据边界

1. `desktopup_desktop_host_win32` 当前是合同夹具，不是生产 WorkerW adapter；
2. QPainter 当前只证明离屏像素格式和 frame contract，不证明 NIGHT OPS 视觉、白条/灰框或性能；
3. QTS-04 必须重新执行 z-order、Explorer 命中、Win+D、Explorer 重建、DPI/显示变化和退出矩阵；
4. 50 MiB Working Set 门仍是历史 FAIL，QTS-01-02～04 的延期没有被本轮关闭；
5. snapshot wire payload 已冻结但完整订阅/草稿/CAS UI 流程在 QTS-05；
6. 托盘边界只保留目录，尚未实现。

## 9. 结论

QTS-03 可以关闭。正式产品代码已经拥有可重复的 Release build/test 入口和不会反向依赖的物理骨架；下一步进入 QTS-04，而不是继续扩展空产品或回到 Qt Quick 候选 demo。
