# QTS-03 代码锚点 V1

状态：`HISTORICAL BASELINE — QTS-04-01 CODE ANCHORS SUPERSEDE HOST DETAILS`

## 构建入口

| 路径 | 作用 |
| --- | --- |
| `CMakeLists.txt` | 根级生产 CMake 入口、Qt 6.8 依赖和 CTest |
| `CMakePresets.json` | Windows x64 Release / VS 2026 + v143 / Qt 6.8.3 预设 |
| `src/CMakeLists.txt` | 正式 target、允许依赖和 9 项合同夹具 |

## 合同与核心

| 路径 | 作用 |
| --- | --- |
| `src/contracts/types.h` | Revision、SurfaceEpoch、RequestId、ErrorCode、Snapshot、FrameDescriptor |
| `src/contracts/ports.h` | `IStateStore`、`IRenderer`、`IDesktopHost` |
| `src/contracts/ipc_protocol.h` | versioned named-pipe wire header、capability、intent、snapshot event |
| `src/core/runtime_core.*` | AcceptedState、FrameGate、SnapshotCursor |

## 可执行单元与 adapters

| 路径 | 作用 |
| --- | --- |
| `src/apps/runtime/main.cpp` | Runtime composition root、primary/secondary、store/render/host/IPC/exit 顺序 |
| `src/apps/control-center/main.cpp` | 独立 Qt Quick 进程、IPC open intent、close-exits |
| `src/apps/control-center/Main.qml` | 最小生产控制中心窗口，不是候选 A0 页面 |
| `src/adapters/state-store-sqlite/` | Windows SQLite direct C API、CAS 和单写者 worker |
| `src/adapters/runtime-ipc-win32/` | current-user ACL、peer SID、session mutex/pipe、handshake/intent client-server |
| `src/adapters/renderer-qpainter/` | ARGB32 premultiplied QPainter frame source |
| `src/adapters/desktop-host-win32/` | QTS-03 Host/epoch 合同夹具起点；真实 adapter 见 QTS-04-01 代码锚点 |
| `src/adapters/diagnostics/` | 资源账本和诊断脱敏 |
| `src/adapters/tray-shell-win32/` | QTS-05 前的 adapter 占位边界 |

## 自动验证

| 路径 | 作用 |
| --- | --- |
| `src/tests/contracts/contract_tests.cpp` | revision/frame/snapshot 单调性 |
| `src/tests/contracts/state_store_tests.cpp` | SQLite NoData/Corrupt/CAS/worker |
| `src/tests/contracts/ipc_tests.cpp` | mutex、handshake、intent、fail-closed、pipe cleanup |
| `src/tests/lifecycle/lifecycle_tests.cpp` | renderer/host/exit/ledger/redaction |
| `src/tests/lifecycle/verify_process_roles.ps1` | 真实 primary/secondary/Control Center 多进程合同 |
| `src/tests/lifecycle/verify_exit_ledger.ps1` | 零资源和临时文件门 |
| `src/tests/architecture/verify_boundaries.ps1` | 禁止依赖和 axiom-runtime 零依赖 |

## QTS-04 修改纪律

- 保留 `contracts/core` framework-free；
- 真实 WorkerW/HWND 只能进入 `desktop-host-win32`；
- QPainter 视觉实现只能进入 renderer，不取得 z-order/input 所有权；
- Runtime 仍不得链接 QtQuick/QML；
- 任何真实海报发布都必须通过同 revision/SurfaceEpoch/bounds first-frame gate；
- 不从 `prototype/renderer-adapters/ts-c01-qt-quick` 复制 composition root、schema 或 installer。
