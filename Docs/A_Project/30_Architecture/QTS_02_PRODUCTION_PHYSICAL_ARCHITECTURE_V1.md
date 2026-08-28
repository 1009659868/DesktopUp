# Desktop-Up V2 QTS-02 生产物理架构 V1

状态：`FROZEN BASELINE / CONTROLLED R4.2 RENDERER REVISION ACTIVE`

冻结日期：2026-08-26

实施分支：`impl/qt-solution`

2026-08-28 受控修订：用户在 QTS-04-04 R4.1 组件审计后正式选择 Qt Quick/QML + GridView/Flickable + QAbstractListModel + QQuickRenderControl 作为常驻海报 renderer adapter。该最新决定覆盖本文原 QTS02-D04/D05 的 QPainter-only 实施基线；DesktopHost、renderer-neutral scene、进程隔离、IPC、安全与状态权威边界不变。修订证据见 `QTS_04_04_R4_2_QTQUICK_ADAPTER_AND_HOST_TRANSACTION_2026-08-28.md`。

## 1. 文档职责

本文把七份冻结需求、DesktopHost E2～E6 合同、AX-BP-01～06 逻辑蓝图、Qt 6.8.3 路线决定、QTS-01-01 内存证据和用户接受的混合 Qt 方向，绑定到可落地的生产物理架构。

本文冻结：

- 常驻与按需进程；
- 线程/执行域及队列方向；
- StateStore 技术、单写者和 CAS 所有权；
- 本地 IPC、单实例和控制中心生命周期；
- 常驻海报 renderer、DesktopHost transport 与 frame 发布边界；
- Qt Quick 的允许部署域；
- 模块、端口和 PEB 的物理占据者；
- 资源所有权、启动、恢复、退出和故障隔离；
- QTS-03 工程骨架必须实现的依赖门。

本文不宣称生产代码、正式 renderer、SQLite schema、IPC schema 或安装包已经实现或通过 P0；这些属于 QTS-03 以后。QTS-01-01 的 50 MiB Working Set FAIL 继续有效。

## 2. 权威输入

1. 七份冻结需求与 2026-08-20 一致性审计；
2. [DesktopHost 原型接口](../20_Technical_Selection/DESKTOP_HOST_PROTOTYPE_INTERFACE_V1.md)及 E2～E6；
3. [AX-BP-02 程序拓扑](AX_BP_02_PROGRAM_TOPOLOGY_V1.md)；
4. [AX-BP-03 模块与端口](AX_BP_03_MODULE_PORT_FREEZE_V1.md)；
5. [AX-BP-04 调用、数据与组合关系](AX_BP_04_FLOW_COMPOSITION_FREEZE_V1.md)；
6. [AX-BP-05 物理执行边界](AX_BP_05_PHYSICAL_EXECUTION_BOUNDARIES_V1.md)；
7. [AX-BP-06 一致性审计与交接](AX_BP_06_CONSISTENCY_AUDIT_AND_HANDOFF_V1.md)；
8. [Qt 方案总执行计划](QT_SOLUTION_EXECUTION_PLAN_V1.md)；
9. [QTS-01-01 内存基线审计](QTS_01_01_MEMORY_BASELINE_AUDIT_2026-08-26.md)；
10. [QTS-01 混合 Qt 优化方向](QTS_01_HYBRID_QT_OPTIMIZATION_DIRECTION_2026-08-26.md)。

发生冲突时，用户最新明确决定与冻结需求优先；候选原型只提供证据，不是生产架构来源。

## 3. 冻结决定摘要

| ID | 决定 | 状态 |
| --- | --- | --- |
| QTS02-D01 | 使用一个常驻 Runtime 主进程，拥有 Core、StateStore、托盘、DesktopHost 和常驻海报 renderer | FROZEN |
| QTS02-D02 | 控制中心是独立、按需创建的 Qt Quick 进程；关闭后退出进程而非后台隐藏 | FROZEN |
| QTS02-D03 | 普通第二实例是短命 broker，只向 Runtime 转发启动意图后退出 | FROZEN |
| QTS02-D04 | 常驻 Runtime 基础依赖保持 QtCore/QtGui；renderer adapter 受控允许 QtQuick/QtQml software offscreen，仍禁止 QtQuickControls、WebView 与不受限外部 QML/JS | CONTROLLED REVISION R4.2 |
| QTS02-D05 | 首发生产海报 renderer 改为 Qt Quick GridView/Flickable + QAbstractListModel + QQuickRenderControl，输出 `QImage::Format_ARGB32_Premultiplied`；QPainter 保留为回归/fallback 基线 | CONTROLLED REVISION R4.2 / AUTO PASS / MANUAL GATE PENDING |
| QTS02-D06 | 沿已验证类别 `OFFSCREEN_FRAME_SOURCE / HOST ownership` 发布；Host-owned layered visual 与 split input islands 保持 DesktopHost 所有权 | FROZEN |
| QTS02-D07 | renderer 与 DesktopHost 同进程但保持独立端口、执行域、资源台账和 SurfaceEpoch | FROZEN |
| QTS02-D08 | StateStore 使用本地 SQLite 3 直接 C API、专用单写者线程和唯一连接所有权 | FROZEN |
| QTS02-D09 | Runtime 与控制中心使用当前用户、当前会话受限的 Windows named pipe | FROZEN |
| QTS02-D10 | 跨进程消息使用长度前缀、版本化 DTO envelope；像素、HWND、数据库连接和 provider SDK 对象禁止进入通用 IPC | FROZEN |
| QTS02-D11 | Runtime 内部使用类型化值与有界队列；CoreDecisionDomain 保持单一有序序列 | FROZEN |
| QTS02-D12 | 主题、Item、布局和 AI ChangePlan 先生成 renderer-neutral 投影；QML 不是业务状态或主题权威 | FROZEN |
| QTS02-D13 | 不加载任意第三方 renderer DLL、原生主题代码或不受限 QML/JS | FROZEN |
| QTS02-D14 | Runtime 崩溃不由隐藏 watchdog 无限拉起；Explorer/renderer 恢复在 Runtime 内有界执行 | FROZEN |
| QTS02-D15 | 安装目录与用户数据目录分离；自定义安装目录不改变 StateStore 位置 | FROZEN |
| QTS02-D16 | axiom-runtime 使用量保持为零；Axiom Core 蓝图不进入调度、IPC 或 lifetime | FROZEN |

## 4. 生产进程拓扑

~~~text
ordinary/autostart launch
        |
        v
DesktopUp Runtime executable
  primary? ---------------- no ---> short-lived intent broker ---> named pipe ---> primary
     |
    yes
     v
+------------------------------------------------------------------+
| Persistent Runtime Process                                      |
|                                                                  |
|  RuntimeIngress -> ApplicationCore -> ProjectionGraph            |
|         |              |                 |                       |
|         |              |                 +-> Tray projection      |
|         |              |                 +-> Control projection   |
|         |              |                 +-> Poster scene/host    |
|         |              |                                         |
|         |              +<-> SQLite StateStore worker              |
|         |                                                        |
|  Win32 Tray + DesktopHost + split input islands                  |
|  Qt Quick software offscreen adapter -> immutable frame -> Host   |
|  IPC server + bounded jobs + diagnostics                          |
+------------------------------------------------------------------+
       |
       | user explicitly opens home/settings/AI
       v
+------------------------------------------------------------------+
| On-demand Control Center Process                                |
| Qt Quick/QML/Quick Controls UI + local draft + preview           |
| accepted snapshots <-> versioned commands over named pipe        |
+------------------------------------------------------------------+
~~~

不存在第三个常驻 renderer、helper、watchdog 或 AI 进程。后续若安全证据要求额外隔离，必须通过架构变更记录新增，不能由实现暗中产生后台进程。

## 5. 部署单元

### 5.1 Runtime 主程序

逻辑名称：`DesktopUp.Runtime`。正式文件名由 QTS-03 工程骨架登记；本文不改变品牌 TBD。

职责：

- 解析启动意图并决定 primary/broker；
- 拥有唯一 CoreDecisionDomain 和 accepted snapshot 工作副本；
- 拥有唯一 StateStore adapter；
- 拥有唯一托盘图标与菜单；
- 拥有 DesktopHost、宿主 HWND、命中岛和 Shell 恢复；
- 拥有常驻 Qt Quick software offscreen renderer adapter；
- 启动/监管唯一控制中心子进程；
- 执行 Item/Shell、文件安全、主题包、AI Provider 和诊断 adapter 的协调；
- 执行粘滞退出和资源台账核对。

依赖允许：Qt 6.8.3 Core/Gui/Quick/Qml（仅内嵌、受限、software offscreen renderer）、SQLite 3、C++ 标准库、明确登记的 Win32/Shell/DWM API。

依赖禁止：Qt Quick Controls、WebView、浏览器 runtime、axiom-runtime、任意第三方主题/renderer 原生插件，以及未验证或从用户主题包直接载入的外部 QML/JS。

Runtime 不创建普通主页窗口；它可以在没有任何普通窗口的情况下运行。

### 5.2 Control Center

逻辑名称：`DesktopUp.ControlCenter`，独立可执行部署单元。

职责：

- 使用 Qt Quick/QML/Quick Controls 构建普通可聚焦管理窗口；
- 显示 runtime 提供的 accepted projection；
- 保存 navigation、selection 和本地短命 draft；
- 生成带 expected revision 的命令；
- 使用独立预览 adapter 呈现同一 renderer-neutral scene；
- 关闭时取消 draft/preview、断开 IPC 并正常退出进程。

禁止：

- 直接打开 SQLite；
- 直接操作真实 Poster HWND、DesktopHost 或托盘；
- 保存第二份持久业务状态；
- 向 Runtime 发送像素或 QML 对象；
- 控制中心崩溃时要求 Runtime/Poster/Tray 一并退出。

独立进程的裁决理由：它把 QML/Scene Graph 内存从常驻路径移出，关闭后由进程终止保证资源回收，并把控制中心故障与桌面海报隔离。代价是增加一个本地 IPC 边界；该代价由小型版本化 DTO 和单客户端协议限制。

### 5.3 Secondary intent broker

普通第二次启动不创建第二套 Runtime。启动器发现 primary 后：

- 普通启动发送 `OPEN_CONTROL_CENTER`；
- 精确 `--autostart` 发送 `NO_UI / ALREADY_RUNNING` 或静默结束；
- 未知/组合参数结构化拒绝并非零退出；
- broker 在收到 accepted/rejected 终态或 deadline 后退出；
- ready 前意图由 primary 的有界启动队列按 RequestId 去重。

## 6. 单实例与本地 IPC

### 6.1 Primary identity

- Runtime 使用每用户、每登录会话命名的 Windows mutex；
- 名称包含稳定产品命名空间、当前用户 SID 的不可逆短哈希和 Session ID；
- mutex 只证明候选 primary，随后必须通过 named pipe 握手确认活性；
- 遗留 mutex/pipe 不得造成无限等待，deadline 后进入有界 stale-primary 裁决。

### 6.2 Named pipe

Runtime 是唯一 pipe server。pipe ACL 只允许当前用户 SID、当前会话和 SYSTEM；拒绝匿名/网络访问。Control Center 与 broker 是客户端。

通用 envelope 至少包含：

~~~text
ProtocolEnvelope {
  protocol_schema_version
  message_kind
  request_id
  runtime_instance_id
  source_role
  expected_revision?
  session_id?
  payload_length
  payload
}
~~~

规则：

- 长度前缀、消息上限、超时和背压必须显式；
- 未知必需版本/能力 fail closed；
- 每个 mutation 返回 accepted/no-change/rejected/conflict/failed/exiting 中一个终态；
- snapshot 推送只允许 revision 单调前进；
- reconnect 后客户端先重新订阅当前 accepted snapshot，不重放未确认 draft；
- Runtime 不信任客户端声称的成功、权限或 revision；
- Control Center 同时只允许一条已认证会话；新窗口请求由 Runtime 合并/聚焦现有实例；
- 日志不得记录敏感 payload。

### 6.3 敏感凭据

AI Key 不进入普通 snapshot。控制中心的凭据提交使用单独标记的敏感 command：只在受限 pipe 内短暂传输，Runtime 立即交给受保护 credential adapter，禁止记录、回显或落入 SQLite；完成后两端尽快清除可控缓冲。具体 Windows 凭据技术由 QTS-08 在本边界内实现。

## 7. Runtime 执行域与线程

| Domain | 物理落点 | 顺序/阻塞规则 |
| --- | --- | --- |
| RuntimeCoordinator | Runtime 主线程；Qt/Win32 event loop | 归一化输入、Core 决策、lifecycle 排序；不得阻塞 I/O |
| DesktopHostDomain | Runtime 主线程 | HWND、Shell、托盘、命中岛和 frame 发布；仅由 coordinator 调度 |
| CoreDecisionDomain | Runtime 主线程上的唯一串行队列 | 一次只执行一个 reducer transition |
| StateStoreDomain | 专用单线程 worker | 独占 SQLite connection；结果异步返回 RuntimeIngress |
| RendererDomain | 专用单线程 worker | QImage/QPainter 离屏绘制；最新未发布 scene 可合并，终态不可丢 |
| IpcDomain | 一个受限 pipe I/O worker | framing/auth/backpressure；不执行业务 reducer |
| JobDomain | 固定上限 worker pool | discovery/theme/AI 等可取消任务；不阻塞 Core/UI |
| DiagnosticsDomain | 专用有界 writer | 先脱敏后写入；错误/cleanup 终态不可采样丢弃 |
| ControlCenterUiDomain | Control Center 主线程 | Qt Quick UI；不执行 store/file/network 工作 |
| ControlCenterRenderDomain | Qt Quick 自有 scene graph 线程模型 | 只服务按需预览，进程关闭后释放 |

队列都必须有界。只有同一 Gesture Session 的中间 Update、未发布旧 projection 和性能样本允许按合同合并；StateStore 结果、End/Cancel、错误、cleanup 和退出确认不能被替换。

## 8. StateStore 生产边界

### 8.1 技术与路径

- 技术：SQLite 3 direct C API；不经 QtSql，也不由 UI/renderer 打开；
- 路径：`%LOCALAPPDATA%\DesktopUp\Data\desktopup.db`；正式品牌变化由迁移合同处理，不因用户选择安装目录而改变；
- Runtime 的 StateStore worker 是唯一连接所有者和唯一 writer；
- 控制中心、托盘、renderer、诊断和 installer 均无数据库连接。

### 8.2 事务与 CAS

- schema 有独立 `schema_version`；
- authoritative snapshot 有单调 `revision`；
- mutation 在一个事务中检查 expected revision、写入全部关联表并产生新 revision；
- commit 成功后才返回完整 authoritative snapshot；
- conflict 返回 actual revision，不自动重试覆盖；
- `NoData`、`Corrupt`、`UnknownVersion`、`IoFailure` 必须分离；
- 仅 `NoData` 允许走专用 InitializeState commit；
- corruption 不得用默认数据覆盖。

### 8.3 可靠性

- 使用 WAL 或经 QTS-03 实测证明等价的 crash-safe journal；正式选择为 WAL，`synchronous=FULL` 作为首发安全基线；
- foreign keys 与完整性检查开启；
- migration 在事务中幂等执行，原始 legacy 数据先只读保留；
- recovery ledger 与业务数据共用同一 authority，但文件备份由 FileSafety 边界持有；
- 退出 flush 有终态；失败保留文件和诊断，不执行广泛删除；
- 数据备份、LKG 和恢复策略在 QTS-03/QTS-05 形成 schema 级合同。

## 9. 常驻海报 renderer

### 9.1 首发实现基线（R4.2 受控修订）

首发 renderer 采用 Qt 6.8.3 Qt Quick software offscreen adapter：

- 输入是 renderer-neutral `PosterScene`、资源快照、Revision 和 SurfaceEpoch；
- NightOpsItemModel 以 QAbstractListModel 向内嵌 QML 投影 Item；
- GridView/Flickable 管理固定 Item metrics、动态列数、delegate 生命周期与连续 contentY；
- QQuickRenderControl 和隐藏 QQuickWindow 使用 software Scene Graph 离屏绘制 `QImage::Format_ARGB32_Premultiplied`；
- 禁止 QWidget、Qt Quick Controls、WebView、任意外部 QML/JS 和 QML 业务状态权威；
- 静态状态只在 scene、geometry、theme、DPI 或可见性变化时重绘；
- 动画由显式 timeline 驱动，PAUSED 时停止并保留最后有效帧；
- 图片在预算内按目标尺寸解码，缓存有所有者、上限和失效策略；
- 输出是带 `(poster_id, revision, surface_epoch, frame_sequence, pixel_bounds)` 的不可变 frame handle。

选择理由：QPainter 已证明基础视觉可行，但连续 viewport、动态列数、delegate clip/scroll/reflow 和输入映射持续产生手写状态机缺陷。Qt Quick 标准组件把这些职责交给统一 scene graph；通过 IRenderer 端口，DesktopHost 与业务权威不感知 renderer 框架。

限制：R4.2 已实现最小 adapter 并取得 Release 14/14 与真实进程零资源退出证据，但人工 reflow flash 门仍待用户确认。QTS-01-01 已测得的 Qt Quick 工作集风险继续有效；性能/资源优化和受限主题热更新不在本轮完成范围。

### 9.2 Scene 合同

`PosterScene` 是不可含 QML/Qt object 的稳定逻辑值，至少表达：

- logical canvas、clip、opacity、rounded geometry；
- shape、text、image、icon 和受限 effect primitive；
- z-order、transform、resource ID、semantic role；
- layout 输出与 HitRegionSet 的同源语义标识；
- theme token 和 fallback reason；
- Revision/SurfaceEpoch。

ThemeDomain、ActivityLayout 和 ItemDomain 生成 scene 所需值；renderer 不计算业务排名、改写主题参数或生成命中语义。

### 9.3 Frame handoff

Renderer 与 DesktopHost 同进程，frame 使用只读共享所有权对象直接交接，不经过 named pipe 或业务消息序列化。队列深度为 latest-wins 单槽加当前已发布帧：

- 未开始绘制的旧 scene 可以被新 scene 替换；
- 已提交的 frame 以 revision/epoch 裁决；
- Host 发布完成前 frame 内存不可复用；
- stale frame 只诊断，不显示；
- 退出/epoch fence 后迟到 frame 不得复活 surface。

## 10. DesktopHost 与 surface transport

生产 transport 冻结为：

~~~text
QtQuick/QML software OFFSCREEN_FRAME_SOURCE
  -> immutable premultiplied frame
  -> Host-owned layered visual
  + Host-owned split input islands
  -> raised Progman safe desktop role
~~~

该类别继承 TS-C01 R2 已验证的边界，不整体复制候选实现。

DesktopHost 独占：

- topology discovery 与 safe target；
- parent/owner/z-order；
- visual HWND、interaction-island HWND、窗口 region 和物理 bounds；
- geometry/hit 投影；
- ShellGeneration、LifecycleEpoch、SurfaceEpoch；
- attach/reconnect/detach/BeginExit；
- frame 发布与首帧门。

Renderer 独占像素内容，不得调用 SetParent、SetWindowPos、ShowWindow、TopMost 或 Shell discovery。Control Center 不获得真实 HWND。

首次或新 epoch 发布必须同时满足：

1. 当前 accepted Revision；
2. 当前 HostProjectionRevision；
3. 当前 HitRegionRevision；
4. 当前 SurfaceEpoch；
5. frame bounds 与物理 target 匹配；
6. HostState 为 `ATTACHED_SAFE`；
7. first frame 已实际发布。

Host visual 不承担整窗输入；只由显式 input islands 命中 Item、标题栏、按钮和编辑边缘。视觉空白继续由 Explorer 处理。

## 11. 投影、命令与权威闭环

唯一合法写入路径保持：

~~~text
Poster / Tray / Control Center / AI ChangePlan
  -> CommandEnvelope(expected_revision, request_id, source)
  -> RuntimeIngress
  -> ValidationPolicy + ApplicationCore
  -> StoreCommitIntent
  -> StateStore worker / SQLite CAS
  -> CommitAccepted(authoritative snapshot R+1)
  -> RuntimeIngress monotonic accept
  -> ProjectionGraph
  -> Poster / Tray / Control / Host / Renderer projections R+1
~~~

运行时 observation、Control Center draft、Qt property、tray checkbox、HWND bounds 和 renderer cache 都不是权威。任何 rejected/conflict/failed command 保留当前 accepted snapshot，并把结果显式返回来源表面。

## 12. 模块到部署域映射

| MOD | 物理占据者 | 备注 |
| --- | --- | --- |
| MOD-01 RuntimeIngress | RuntimeCoordinator | pipe/OS/worker 回调先值化再进入 |
| MOD-02 ApplicationCore | RuntimeCoordinator | 单序列 reducer |
| MOD-03 ProjectionGraph | Runtime shared core library | 纯函数；Runtime 生成全部 accepted projection |
| MOD-04 ValidationPolicy | shared contract/core library | Runtime 执行最终验证；Control 可预检但不具权威 |
| MOD-05 StateStorePort | Runtime StateStore worker | SQLite 唯一 writer |
| MOD-06 PosterSurfacePort | Runtime Poster adapter | Host visual + input islands + renderer projection |
| MOD-07 ControlCenterPort | Control Center process | Runtime 中仅有 supervisor/IPC adapter |
| MOD-08 TraySurfacePort | Runtime Win32 Tray adapter | 唯一托盘 |
| MOD-09 DesktopHostPort | Runtime Win32 DesktopHost adapter | 唯一 z-order/host owner |
| MOD-10 RendererPort | Runtime QPainter adapter | 可替换端口，首发静态链接 |
| MOD-11 ItemDomain | shared core library / Runtime | 纯规则与 effect intents |
| MOD-12 ActivityLayout | shared core library / Runtime | 纯算法 |
| MOD-13 ThemeDomain | shared core library / Runtime | 结构化主题；Control 只呈现草稿 |
| MOD-14 AiDomain | Runtime core + on-demand provider job | Control 只发用户意图/显示计划 |
| MOD-15 FileSafety | Runtime core + JobDomain/File adapter | ledger 经 StateStore |
| MOD-16 LifecycleDomain | RuntimeCoordinator | sticky exit 与 epoch owner |
| MOD-17 ShellIntegrationPort | Runtime Win32/Shell adapter | launch、picker、events、broker forwarding |
| MOD-18 Diagnostics | Runtime DiagnosticsDomain | Control 只消费脱敏 projection |
| MOD-19 DistributionPort | installer/uninstaller + Runtime settings adapter | 不进入普通业务写入旁路 |
| MOD-20 DesktopUpProgram | compile-time composition root | 无全局万能 Service Locator |

## 13. PEB 绑定

| PEB | 生产边界 |
| --- | --- |
| PEB-01 Surface/System | Runtime Win32 input + Control Center IPC；值化后进入 ingress |
| PEB-02 StateStore | 同进程跨线程 SQLite request/result |
| PEB-03 DesktopHost | 同进程 Runtime 主线程 Win32 adapter |
| PEB-04 Renderer | 同进程跨线程 QPainter adapter + 直接 frame handoff |
| PEB-05 Gesture | input islands/Control draft；SessionId 有序流，End 进入 CAS |
| PEB-06 Discovery/FileSafety | Runtime bounded JobDomain + file adapter |
| PEB-07 Item launch | Runtime Shell adapter；产品成功门仍待 QTS-06 前冻结 |
| PEB-08 Theme package | Runtime bounded job + staging；格式留给 QTS-07 |
| PEB-09 AI provider | Runtime on-demand job/provider adapter；无 Key 不创建 |
| PEB-10 Tray/System | Runtime Win32 Tray/System adapter |
| PEB-11 Diagnostics | Runtime bounded writer 与导出 adapter |
| PEB-12 Distribution | current-user installer/uninstaller；QTS-09 完整冻结 |
| PEB-13 Single instance | per-user/session mutex + named pipe forwarding |
| PEB-14 Time | Runtime injectable monotonic/wall-clock adapter |
| PEB-15 Power/session | Runtime Win32 event adapter |

## 14. 启动时序

### 14.1 Primary normal/autostart

1. 解析精确启动意图；
2. 建立/确认 per-user/session primary identity；
3. 启动 RuntimeCoordinator、Diagnostics 和 IPC endpoint；
4. StateStore worker 打开 SQLite，区分 Loaded/NoData/Failed；
5. 只有 Loaded 或专用 InitializeState commit 成功后接受 snapshot；
6. 创建唯一托盘；
7. DesktopHost 发现 topology、协商 OFFSCREEN_FRAME_SOURCE；
8. ProjectionGraph 生成 Host/Hit/Scene；
9. QPainter renderer 生成当前 Revision/SurfaceEpoch 首帧；
10. Host 原子发布 frame 与 input islands；
11. 标记 RuntimeReady，投递启动期间排队命令。

普通启动和 autostart 均不创建 Control Center。Store/Host 不安全时进入明确 degraded；不创建普通顶层或 TopMost fallback。

### 14.2 打开 Control Center

1. 用户通过托盘/开始菜单/普通第二实例发出 Open intent；
2. Runtime 合并重复请求并创建一次性 control session token；
3. 启动 Control Center 子进程；
4. 子进程通过受限 pipe 握手；
5. Runtime 推送当前 accepted projection 与 capability/health；
6. Control Center 创建窗口并报告 Ready；
7. 只有显式打开意图允许聚焦普通窗口；海报 z-order 不变。

关闭时 Control Center 先结束 draft/preview，再断开并退出。Runtime 收到正常退出或 pipe disconnect 后释放 session；Poster/Tray/Core 不变。

## 15. 恢复与显示变化

### Explorer/Shell 重建

- DesktopHost 递增 LifecycleEpoch，fence 旧 SurfaceEpoch；
- 立即撤销旧 input/presentation，保留 accepted snapshot；
- 清理旧 generation 自有资源；
- 有界发现新 safe target，创建新 SurfaceEpoch；
- 用当前 accepted snapshot 重投影并等待首帧；
- 只发布匹配新 epoch 的 host/hit/frame；
- 预算耗尽保持 degraded，托盘提供重连；禁止 TopMost fallback。

### Renderer fault

- 只 fence Renderer/SurfaceEpoch，不修改业务 snapshot；
- 若 Host 仍安全，可保留最后有效帧或关闭输入，具体按 frame 健康裁决；
- 有界重建 renderer 资源并重新发布；
- 与 Shell loss 竞争时 Shell cleanup 优先。

### DPI/分辨率/工作区

- 系统事件先值化为 display observation；
- 从持久化 DIP/normalized anchor 重算物理 bounds、hit 和 scene；
- 生成同一业务 Revision 下的新 HostProjectionRevision/SurfaceEpoch 策略；
- 旧 DPI frame 不发布；
- 不把临时物理像素反写成业务几何。

完整多显示器、混合 DPI、负坐标和热插拔认证继续 DEFERRED，但 DTO 和 display identity 不得封死扩展。

## 16. 故障隔离矩阵

| 故障 | 保留 | 物理处理 | 禁止行为 |
| --- | --- | --- | --- |
| Control Center crash/hang | Runtime、Poster、Tray、accepted state | 断开 session、丢弃 draft、可再次启动 | 退出 Runtime、提交 draft |
| IPC disconnect | accepted state | 客户端重连后重新订阅 | 重放未知 mutation |
| StateStore I/O/corrupt | 文件、LKG、accepted copy | 拒绝 mutation、read-only/degraded、诊断 | 默认空状态覆盖 |
| Renderer fault | accepted state、Host contract | fence epoch、有界重建 | renderer 修改 z-order |
| DesktopHost/Shell loss | accepted state、托盘/Core | degraded、有界 reconnect | 顶层/TopMost fallback |
| Tray Explorer rebuild | Core/Poster | 幂等重建唯一托盘 | 第二 primary |
| Job/theme/AI fault | 手动核心产品 | cancel/fail terminal、隔离错误 | 阻塞海报/状态循环 |
| Diagnostics sink fault | 业务操作 | 有界内存缓冲/降级 | 成为业务失败或权威 |
| Runtime fatal crash | SQLite crash-safe state | 进程退出；下次显式/自启正常恢复 | 无限 watchdog 拉起 |

## 17. 资源所有权与释放

| 资源 | 唯一所有者 | 释放门 |
| --- | --- | --- |
| SQLite connection/transaction/WAL | StateStore worker | flush/result + close |
| accepted snapshot working copy | ApplicationCore | Runtime exit |
| host visual/input HWND/regions/hooks | DesktopHost adapter | detach ledger zero |
| QImage/frame/cache/font/image decode | Renderer adapter | RendererReleased + frame refs zero |
| tray icon/menu callbacks | Tray adapter | Shell notify remove + callback fence |
| pipe handles/session/token | IPC adapter | Close/Disconnect + session tombstone |
| Qt Quick/QML/Scene Graph | Control Center process | process normal exit |
| draft/preview | Control Center session | cancel/close/disconnect |
| file scan/move handles | File adapter job | job terminal + ledger result |
| theme staging | Theme package adapter | publish/rollback + staging cleanup |
| provider credentials/requests | credential/provider adapter | request cancel + protected handle close |
| diagnostic files/buffer | Diagnostics adapter | drain/deadline + close |

Runtime 组合根只能保存 adapter ownership container 和端口连接，不能暴露跨模块任意访问。

## 18. 显式退出顺序

1. `BeginExit` 进入 sticky exiting；
2. 关闭 IPC 新连接并拒绝新 mutation；
3. 向 Control Center 发出 shutdown，等待有界 ack；必要时只终止由 Runtime 创建且身份匹配的子进程；
4. cancel/fence discovery、theme、AI 和其他 JobEpoch；
5. StateStore flush，核对关键 recovery ledger；
6. release renderer，等待 frame 引用归零；
7. DesktopHost detach，核对 visual/island/hook 零资源；
8. 移除托盘与系统回调；
9. 排空必要诊断终态并关闭 sink；
10. 核对 ResourceOwnershipLedger 后结束 Runtime。

迟到 pipe、Shell、frame、job 或 store 事件只进入诊断，不能离开 exiting 或创建资源。

## 19. 主题、Item、布局与 AI 的扩展边界

### Item/布局

- Item、Activity 和 Layout 是 shared core 纯值/算法；
- 200 Item 计算在 Core/Job 边界完成，renderer 只消费逻辑 scene；
- V2-TBD-002/003 和 LaunchSuccessGate 不由 QTS-02 擅自关闭；
- QTS-06 前必须冻结启动成功门。

### 主题

- built-in、imported 和 AI draft 最终都转成受验证的 ThemeSpec/ThemeInstance；
- Theme compiler 输出 renderer-neutral token/resource/scene 值；
- Qt Quick 预览与 QPainter 海报消费同源 projection，但分别拥有资源；
- 主题不包含任意 QML、JS、DLL、网络或文件执行权限；
- 包格式、扩展名和受限脚本是否首发留给 QTS-07/V2-TBD-012。

### AI

- AI 是 on-demand provider job，不是常驻 UI 条件；
- Provider 输出只生成 ChangePlan；
- ChangePlan 经普通 Validation、preview、confirmation 和 CAS；
- 无 Key 时不创建 provider、credential handle 或网络连接；
- QTS-02 不裁决 R0/R1/R2 免确认范围。

## 20. 构建和依赖硬门

QTS-03 必须建立自动检查：

1. Runtime 二进制依赖中不存在 QtQuick/Qml/QuickControls/WebView；
2. Control Center 不链接 SQLite、DesktopHost 或真实 poster adapter；
3. shared contract/core 库不包含 HWND、QWindow、QML、sqlite3 connection 或 provider SDK 类型；
4. axiom-runtime 依赖和引用为零；
5. Renderer 不能链接或调用 DesktopHost 私有 z-order API；
6. StateStore 只由一个 adapter target 链接；
7. IPC DTO 有版本、大小限制和 unknown-required fail-closed 测试；
8. Release 启动/退出后资源台账可检查。

## 21. 性能边界

- 常驻 Runtime 不加载 Qt Quick/QML/Scene Graph；
- 静态海报无持续帧循环；
- frame queue、cache、diagnostics 和 IPC 都有界；
- Control Center cold open/close 单独测量，关闭后进程应消失；
- 6/200 Item、主题资源和 renderer transport 仍须恢复 QTS-01-02～04 测量；
- 50 MiB Working Set 门继续 FAIL，不能用 process trimming 或 Private Working Set 偷换；
- 性能支线最迟在 QTS-09 入口前恢复。

## 22. 保留的开放项和证据状态

### 本阶段已关闭的架构项

- V2-TBD-001 的 renderer/UI/StateStore/process/IPC 物理基线由本文关闭；如改变需走架构变更控制。

### 继续开放或延期

- V2-TBD-002/003：活跃度公式和布局阈值；
- V2-TBD-004：最终默认主题名称/视觉；
- V2-TBD-005/006/007/008/009/011/012；
- V2-TBD-010 与 V2-DEFER-001～004；
- installer、自更新、签名和公开分发材料的最终技术；
- QPainter 生产 renderer 的全部 P0 与性能结果。

QTS-02 决定不修改冻结需求原文；关闭状态在架构追踪中登记。

## 23. 生产状态声明

| 对象 | 当前状态 |
| --- | --- |
| Qt 6.8.3 实施路线 | USER SELECTED |
| 混合 Qt 方向 | USER ACCEPTED |
| 生产物理架构 | FROZEN BY QTS-02 |
| Runtime/Control Center/IPC/Store source code | NOT STARTED |
| QPainter production renderer | SELECTED BASELINE / NOT IMPLEMENTED / P0 NOT RUN |
| 已有 Qt Quick candidate | HISTORICAL EVIDENCE WITH RETAINED EXCEPTIONS |
| 50 MiB Working Set | FAIL RETAINED |
| 多屏/混合 DPI完整认证 | DEFERRED |

## 24. QTS-02 退出门

| 检查 | 结果 |
| --- | --- |
| 常驻与按需进程唯一 | PASS |
| Qt Quick 不进入常驻 runtime | PASS |
| StateStore 技术、路径、单写者与 CAS 所有权明确 | PASS |
| DesktopHost/Renderer transport 与责任分离 | PASS |
| 单实例、IPC、版本、认证与背压边界明确 | PASS |
| MOD-01～20 有物理占据者 | PASS |
| PEB-01～15 有物理落点 | PASS |
| 启动、控制中心、恢复、显示变化和退出时序明确 | PASS |
| 资源台账和故障隔离完整 | PASS |
| Theme/Item/AI 保持 renderer-neutral 与单一权威 | PASS |
| 12 TBD/4 DEFER 未被无授权关闭 | PASS |
| 候选结果未冒充生产通过 | PASS |
| 50 MiB FAIL 与延期性能检查点保留 | PASS |
| axiom-runtime 排除 | PASS |

## 25. 下一动作

进入 `QTS-03：生产工程骨架与合同夹具`。QTS-03 只建立可构建的 Runtime、Control Center、shared contracts/core、StateStore、DesktopHost/Renderer 接口、IPC handshake、诊断与零资源退出夹具；不得提前把 Night Ops 演示卡或候选原型整体迁入生产源码。
