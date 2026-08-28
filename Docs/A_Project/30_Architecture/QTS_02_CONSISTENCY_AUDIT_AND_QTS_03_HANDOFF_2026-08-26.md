# QTS-02 一致性审计与 QTS-03 交接（2026-08-26）

状态：`COMPLETE — PHYSICAL ARCHITECTURE FROZEN / QTS-03 READY`

关联架构：[QTS-02 生产物理架构](QTS_02_PRODUCTION_PHYSICAL_ARCHITECTURE_V1.md)

## 1. 审计目的

确认 QTS-02 的物理决定：

1. 没有破坏七份冻结需求；
2. 没有绕过 AX-BP 的单一权威、端口和 PEB；
3. 没有把 Qt Quick 候选的观察冒充 QPainter 生产 renderer 结果；
4. 没有丢失历史失败、TBD 或 DEFER；
5. 足以让 QTS-03 建立正式源码骨架，不再需要实现者猜测进程、StateStore、IPC 或依赖边界。

## 2. 关键裁决及证据

| 裁决 | 输入证据 | 审计结论 |
| --- | --- | --- |
| Runtime 常驻、Control Center 独立按需进程 | RP-INV-06、Runtime surfaces §4、QTS-01 混合方向 | CONSISTENT |
| Runtime 禁用 Qt Quick/QML | QTS-01 QtGui 26.453 MiB、minimal Quick 63.454 MiB | DIRECTION SUPPORTED / PRODUCT BUDGET NOT YET PASS |
| QPainter/QImage 首发 renderer | QtGui 基线、Qt 6.8.3 路线、renderer 可替换不变量 | ARCHITECTURE BASELINE / P0 NOT RUN |
| Host-owned layered visual + split islands | TS-C01 R2 已验证 transport 类别、DesktopHost 合同 | TRANSPORT CATEGORY SUPPORTED / PRODUCTION ADAPTER NOT YET PASS |
| SQLite direct C API 单写者 | RP-INV-04/05、PEB-02、CAS 闭环 | CONSISTENT |
| named pipe + per-user/session mutex | LIFE-01～06、PEB-13、AX-BP IPC 约束 | CONSISTENT |
| renderer-neutral Theme/Item/AI projection | RP-INV-08/10、AX-BP 禁止边 | CONSISTENT |

## 3. 候选证据隔离

以下结论严格分开：

- Qt Quick candidate 的 R2 transport 和人工输入通过，证明 `OFFSCREEN_FRAME_SOURCE / HOST ownership` 类别在当前环境可行；
- candidate 的完整单海报 Working Set 约 136 MiB，50 MiB 门继续 FAIL；
- candidate 使用 QQuick/QML/Scene Graph 和 readback，不能代表 QPainter 生产 renderer 的资源或视觉结果；
- QPainter production renderer 当前为 `NOT IMPLEMENTED / P0 NOT RUN`；
- QTS-04 必须重新执行白条/灰框、Explorer 输入、普通窗口层级、Win+D、Explorer 重建、显示变化、主题故障和退出矩阵。

## 4. 单一权威审计

合法路径仍只有：

~~~text
Command -> Runtime Core -> SQLite CAS -> accepted snapshot -> projections
~~~

审计结果：

- Control Center 无 SQLite 连接；
- QML property 和 draft 不持久化为第二权威；
- Runtime 只接受 StateStore CommitAccepted 的新 revision；
- Host physical bounds、renderer frame 和 tray menu state 只作 observation/projection；
- AI ChangePlan 仍走普通命令；
- IPC 不是第二业务 API，Runtime 仍执行最终验证。

结果：PASS。

## 5. 依赖与故障隔离审计

| 场景 | 预期边界 | 结果 |
| --- | --- | --- |
| 关闭控制中心 | 独立 Qt Quick 进程退出，Runtime/Poster/Tray 保持 | FROZEN |
| 控制中心崩溃 | draft 丢弃，可重新打开；业务状态不变 | FROZEN |
| StateStore 故障 | 保留原文件和 accepted copy，拒绝写入 | FROZEN |
| Renderer 故障 | fence SurfaceEpoch，有界重建，不改 z-order | FROZEN |
| Explorer 重建 | Host degraded/reconnect，不创建 TopMost fallback | FROZEN |
| Runtime fatal | crash-safe store；无隐藏 watchdog 重启环 | FROZEN |
| 显式退出 | 控制中心、jobs、store、renderer、host、tray、diagnostics 有序结案 | FROZEN |

## 6. 开放项处置

| 项 | 处置 |
| --- | --- |
| V2-TBD-001 | 由 QTS-02 关闭物理基线；冻结需求原文不回写 |
| V2-TBD-002/003 | QTS-06 冻结算法与阈值 |
| V2-TBD-004/005/012 | QTS-07 冻结主题视觉、扩展安全与包表达 |
| V2-TBD-006 | QTS-08 冻结 AI 低风险确认策略 |
| V2-TBD-007 | QTS-06 前冻结 EDITING 双击 |
| V2-TBD-011 | QTS-06 前冻结 LaunchSuccessGate |
| V2-TBD-008/009 | 保持产品待决，不阻塞 QTS-03 |
| V2-TBD-010、V2-DEFER-001～004 | 保持延期，端口不封死但不激活资源 |
| 50 MiB Working Set | FAIL 保留；QTS-01-02～04 最迟 QTS-09 前恢复 |

## 7. QTS-03 必须建立的正式源码边界

建议正式源码拓扑；最终目录名可机械调整，但依赖方向不得改变：

~~~text
src/
  apps/
    runtime/                 persistent executable / composition root
    control-center/          on-demand Qt Quick executable
  contracts/                 framework-free DTO, IDs, errors, ports
  core/                      reducer, validation, projection, lifecycle
  domains/
    item/
    activity-layout/
    theme/
    ai/
    file-safety/
  adapters/
    state-store-sqlite/
    desktop-host-win32/
    renderer-qpainter/
    tray-shell-win32/
    runtime-ipc-win32/
    diagnostics/
  tests/
    contracts/
    architecture/
    lifecycle/
~~~

QTS-03 不要求实现完整 Item、主题或 AI 功能，但必须建立不允许反向依赖的 target 边界。

## 8. QTS-03 最小合同夹具

### 8.1 Build/Dependency

- Runtime Release 构建不依赖 QtQuick/Qml/QuickControls；
- Control Center 构建不依赖 SQLite/DesktopHost adapter；
- contracts/core 不含 Qt/Win32/sqlite/provider SDK 对象；
- axiom-runtime 全仓依赖为零。

### 8.2 Core/Store

- NoData 与 Corrupt 分离；
- InitializeState 也经过 commit；
- expected revision accepted/conflict；
- stale store result 不倒退 accepted revision；
- Store worker 不阻塞 RuntimeCoordinator。

### 8.3 IPC/Control Center

- primary 与 ordinary/autostart secondary 分流；
- protocol/version/capability handshake；
- 最大消息、未知必需消息和 peer ACL fail closed；
- accepted snapshot 单调订阅；
- Control Center 关闭后进程退出，Runtime 保持；
- disconnect 后 draft 不提交。

### 8.4 Host/Renderer

- RendererPort 和 DesktopHostPort 分离；
- fake QPainter frame 具备 revision/epoch/bounds；
- stale frame 被拒绝；
- first-frame gate 前 visual/input 不发布；
- BeginExit 后迟到 frame 不创建资源。

### 8.5 Exit/Diagnostics

- sticky exiting；
- 每个 worker/adapter 有终态；
- resource ownership ledger 归零；
- stable error code、RequestId、Revision、SurfaceEpoch 可导出；
- 敏感字段脱敏。

## 9. QTS-03 退出门

QTS-03 完成需同时满足：

1. 正式源码树和 CMake/Release 构建建立；
2. Runtime 与 Control Center 两个部署单元可独立构建；
3. Runtime 在没有 Control Center 时启动并正常退出；
4. named pipe handshake 和 secondary forwarding 夹具通过；
5. SQLite load/NoData/CAS/conflict 夹具通过；
6. fake Host/Renderer 同 revision/epoch gate 通过；
7. 依赖禁止门自动通过；
8. 退出后测试进程、pipe、临时文件和自有资源为零；
9. 不把候选 demo 卡片、旧 schema 或候选 installer 当作生产实现；
10. 更新代码锚点、日志、交接和 QTS-04 输入。

## 10. QTS-02 审计结论

| 检查 | 结果 |
| --- | --- |
| 七份需求无冲突 | PASS |
| AX-BP MOD/flow/PEB 无旁路 | PASS |
| 进程、线程、StateStore、IPC、renderer、host 已明确 | PASS |
| Qt Quick 资源边界可确定释放 | PASS BY ARCHITECTURE |
| 候选与生产证据未混淆 | PASS |
| 50 MiB FAIL 与历史例外保留 | PASS |
| TBD/DEFER 处置完整 | PASS |
| QTS-03 无需再猜测物理边界 | PASS |

QTS-02 可以关闭，下一动作是 `QTS-03：生产工程骨架与合同夹具`。
