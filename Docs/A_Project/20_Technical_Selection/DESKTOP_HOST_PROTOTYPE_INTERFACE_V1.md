# DesktopHost 原型接口 V1

状态：`TS-H01-E COMPLETE — E2-E5 CONTRACTS FROZEN / CANDIDATE ADAPTERS NEXT`

日期：2026-08-21

## 1. 文档角色

本文件定义不同海报渲染候选接入 Windows 桌面宿主时必须共享的语义合同。它是技术选型夹具接口，不是生产架构、语言 ABI 或某个框架封装。内部可以使用 Win32 HWND 和合成对象，但候选只能依赖本文公开的能力与事件。
E2 冻结依据与草案修正见 `TS_H01_E2_CONTRACT_AUDIT_2026-08-21.md`；E3 生命周期、恢复、退出、线程与竞态合同见 `TS_H01_E3_LIFECYCLE_CONTRACT_2026-08-21.md`；E4 原子投影见 `TS_H01_E4_GEOMETRY_HIT_RENDER_CONTRACT_2026-08-21.md`；E5 诊断夹具见 `TS_H01_E5_DIAGNOSTIC_CONFORMANCE_FIXTURE_2026-08-21.md`；候选交接见 `TS_H01_E6_CANDIDATE_HANDOFF_2026-08-21.md`。任何后续候选若要改变这些冻结项，必须重新打开对应工作包并登记对全部候选的影响。


## 2. 已验证适用范围

| 维度 | 状态 | 范围 |
| --- | --- | --- |
| raised Progman layered visual + split input islands | `PASS` | 当前 Windows 11 x64 / 当前 Explorer 拓扑 |
| 层级、桌面输入、键盘与任务栏 Win+D | `PASS` | 当前单主屏环境 |
| Explorer 重建 | `USER-ACCEPTED` | 自动证据 1/10，例外保留 |
| 100% / 96 DPI | `PARTIAL` | 旧构建交互 PASS、当前构建静态 PASS |
| 125% / 120 DPI | `PASS` | 当前构建 5/5 自动与人工 PASS |
| 150% / 200% | `UNKNOWN` | 用户跳过 |
| 分辨率改变 | `USER-ACCEPTED` | 人工 PASS，自动只识别 8/10 且混入交互 |
| 任务栏工作区变化 | `PASS` | 3,802/3,802 样本、11 次变化、人工 PASS |
| traditional WorkerW、多屏、混合 DPI、负坐标、热插拔 | `UNKNOWN / DEFERRED` | 不在当前结论内 |

候选接入不得把 `USER-ACCEPTED`、`PARTIAL`、`UNKNOWN` 或 `DEFERRED` 映射成 `PASS`。

## 3. 语言中立类型模型

```text
SourceSnapshotRevision = monotonic u64 from the authoritative application core
HostProjectionRevision = monotonic u64 local to one attachment
HitRegionRevision      = monotonic u64, committed with HostProjectionRevision
RenderRevision         = monotonic u64, observed for the presented frame
ShellGeneration        = monotonic u64 within one host process
HostEventSequence      = monotonic u64 within one host process
HostSessionId          = opaque token unique to one host process/service session
LifecycleEpoch         = monotonic u64 fencing resource-changing operations
SurfaceEpoch           = monotonic u64 for each concrete surface binding
RecoveryCycleId        = opaque token for one bounded recovery cycle
AttachmentId           = opaque stable token
SurfacePlanId          = opaque negotiation token
RegionId               = opaque stable token
RequestId              = opaque idempotency token

HostTopology       = RAISED_PROGMAN | TRADITIONAL_WORKERW | NO_SAFE_HOST | UNKNOWN
HostState          = DETACHED | DISCOVERING | SURFACE_NEGOTIATING |
                     ATTACHING | ATTACHED_SAFE | SHELL_GENERATION_LOST |
                     DEGRADED_NO_DESKTOP_HOST | RECOVERING |
                     DETACHING | EXITING | FAULTED
PresentationMode   = EXPANDED | COLLAPSED
InteractionMode    = USING | EDITING
RuntimeMode        = ACTIVE_STATIC | ACTIVE_ANIMATED | PAUSED | STOPPED
UserVisibility     = SHOWN | HIDDEN
SystemAvailability = AVAILABLE | SESSION_LOCKED | DISPLAY_OFF |
                     SHELL_UNAVAILABLE | EXITING
InputAvailability  = ENABLED | DISABLED_BY_SYSTEM
RegionRole         = TITLE_DRAG | ITEM_ACTION | ITEM_CONTEXT |
                     LOCK_BUTTON | COLLAPSE_BUTTON | POSTER_CONTEXT |
                     RESIZE_EDGE | RESIZE_CORNER | CUSTOM_ACTION
EvidenceDisposition = VERIFIED_PASS | USER_ACCEPTED_WITH_EXCEPTION |
                      PARTIAL_EVIDENCE | UNKNOWN | DEFERRED |
                      FAILED | NOT_RUN
SurfaceState       = UNBOUND | BINDING | READY_NO_FRAME | PRESENTED |
                     LOST | RELEASING | RELEASED
RecoveryCause      = INITIAL_NO_SAFE_HOST | SHELL_GENERATION_CHANGED |
                     SAFE_TARGET_INVALID | ATTACHMENT_RESOURCE_LOST |
                     SURFACE_DEVICE_LOST | SURFACE_TRANSPORT_LOST |
                     EXPLICIT_RECONNECT | SYSTEM_RESUME |
                     ATTACH_OR_FRAME_TIMEOUT | INVARIANT_VIOLATION
```

`SourceSnapshotRevision` 与 `HostProjectionRevision` 绝不互换：前者来自单一业务权威并用于防止旧 snapshot 覆盖新状态；后者只证明该 attachment 已应用哪次宿主投影。宿主不持久化第二份 Poster 真相。

`HostTopology` 仅用于诊断与能力范围，候选不得按固定 HWND、类名或枚举序号实现业务分支。所有支持声明同时携带 `EvidenceDisposition`、环境范围和 Evidence ID。

## 4. 核心数据

### 4.1 能力与 surface 协商

```text
SurfaceTransportKind = HOST_OWNED_NATIVE_CHILD_SLOT |
                       ADAPTER_OWNED_NATIVE_CHILD |
                       COMPOSITION_VISUAL |
                       OFFSCREEN_FRAME_SOURCE
SurfaceOwnership     = HOST | ADAPTER | SHARED_BY_EXPLICIT_CONTRACT

SurfaceOffer {
  candidate_id
  supported_transport_kinds[]
  required_pixel_formats[]
  alpha_mode
  size_update_model
  ownership_capabilities[]
}

SurfaceBindingPlan {
  plan_id: SurfacePlanId
  selected_transport_kind
  ownership
  host_requirements
  adapter_requirements
  evidence_disposition
}

HostCapability {
  capability_id
  evidence_disposition
  environment_scope
  evidence_ids[]
  limitations[]
}

HostCapabilities {
  topology_capabilities[]
  surface_transport_capabilities[]
  input_capabilities[]
  geometry_capabilities[]
  recovery_capabilities[]
}
```

E2 只冻结协商字段和语义，不宣称任一 transport 已适配所有候选。公共探针证明 host-owned Windows 桌面子表面可以满足当前层级/输入合同；Qt Quick、WinUI、WPF、Avalonia、WebView2、Slint 和实际 Direct2D/DirectComposition renderer 的绑定仍为 `NOT_RUN`。协商失败返回 `SURFACE_TRANSPORT_UNSUPPORTED`，不得偷偷创建普通顶层或 TopMost fallback。

### 4.2 显示目标与几何

```text
DisplayTarget {
  stable_display_id
  fallback_policy: PRIMARY_ONLY_V1
}

LogicalGeometryDip {
  left, top, width, expanded_height, collapsed_height
  coordinate_space: TARGET_WORK_AREA_DIP
}

NormalizedAnchor { x: 0..1, y: 0..1, edge_affinity }
GeometryConstraintsDip { safe_insets, min_size, max_size, resize_origin }
PhysicalBounds { monitor_px, work_area_px, expected_px, window_px,
                 client_screen_px, extended_frame_px, render_target_px }
```

逻辑几何、显示目标和归一化锚点来自权威 snapshot；物理边界是宿主投影和诊断。显示器、工作区或 DPI 改变时重投影，不把物理像素写回业务状态，不加入固定顶部/边框补偿。V1 的 `fallback_policy` 只允许当前主屏；稳定显示身份是结构预留，不构成多屏认证。

### 4.3 命中集合

```text
HitRegion {
  id: RegionId
  role: RegionRole
  shape_dip: RECT | ROUNDED_RECT | POLYGON
  clip_to_visual_bounds
  priority
  enabled
  action_payload_ref
}

HitRegionSet {
  revision: HitRegionRevision
  coordinate_space: POSTER_LOCAL_DIP
  regions[]
  overlap_policy: EXPLICIT_PRIORITY_THEN_UNION
}
```

零区域表示全穿透；一/多区域只命中登记形状；重叠区域先按显式 priority 裁决语义角色，再对相同所有权区域求并集，不得扩大成包络矩形或产生额外不可见遮挡。区域必须裁剪到 visual bounds，可见控件与实际命中面积一致。未覆盖的视觉空白保持 Explorer 原生桌面框选、右键和快捷方式输入；不得用整窗 UI 命中或合成转发鼠标消息冒充原生行为。

### 4.4 正交投影状态与原子提交

```text
HostProjection {
  source_snapshot_revision: SourceSnapshotRevision
  display_target
  logical_geometry
  normalized_anchor
  geometry_constraints
  presentation_mode
  interaction_mode
  runtime_mode
  user_visibility
  system_availability
  input_availability
  is_locked
  hit_region_set
}

ProjectionCommit {
  request_id: RequestId
  attachment_id: AttachmentId
  expected_host_revision: HostProjectionRevision
  projection: HostProjection
}
```

呈现、交互、运行时、用户显隐、系统可用性、输入可用性和锁定保持正交。手动 `PAUSED` 只停止动画/持续绘制并保留按钮；系统不可用才禁用输入；`STOPPED` 允许 renderer 释放可选资源；用户隐藏与 Shell degraded 必须可区分。Win+D、焦点变化、右键、文件选择器和普通程序覆盖不修改这些意图。

一次 commit 必须把 geometry、anchor、presentation、interaction、lock、hit-region set 和 renderer target 的期望版本作为一个投影意图接受或整体拒绝。可见 frame 的 `RenderRevision` 与 `HitRegionRevision` 必须能够关联到同一 `HostProjectionRevision`。冲突返回当前 source/host revisions，不静默覆盖新状态，也不形成预览与真实海报分叉。

## 5. 方法合同

```text
QueryCapabilities() -> HostResult<HostCapabilities>
DiscoverTopology() -> TopologySnapshot
NegotiateSurface(SurfaceOffer) -> HostResult<SurfaceBindingPlan>
Attach(AttachRequest) -> HostResult<AttachmentSnapshot>
CommitProjection(ProjectionCommit) -> HostResult<AttachmentSnapshot>
NotifySurfaceReady(SurfaceReady) -> HostResult<AttachmentSnapshot>
NotifyFramePresented(FramePresented) -> HostResult<AttachmentSnapshot>
NotifySurfaceFault(SurfaceFault) -> HostResult<AttachmentSnapshot>
ObserveHostState(AttachmentId?) -> HostResult<HostObservation>
RequestReconnect(AttachmentId?, observed_shell_generation, RequestId)
  -> HostResult<RecoveryResult>
Detach(AttachmentId, RequestId) -> HostResult<DetachResult>
BeginExit(RequestId) -> HostResult<ExitResult>
```

### QueryCapabilities

返回逐能力证据状态和可协商 surface transport；不得用“平台支持”代替本候选已实测。调用方据此拒绝不可满足的适配，而不是进入运行后再静默 fallback。

### DiscoverTopology

返回所有候选 Shell 角色、唯一安全目标、Explorer PID/世代和证据化拓扑结论。类名不能单独决定角色；结果必须结合父子关系、进程、可见性和 icon view 关联。没有唯一安全目标时返回 `NO_SAFE_HOST`，不创建海报。

### NegotiateSurface

在创建 attachment 前选择双方都明确支持的 transport、所有权和尺寸更新模型。`SurfaceBindingPlan` 只对当前 candidate/build 和 host capability snapshot 有效；Shell generation 改变不自动证明 surface 仍可复用。

### Attach

消费有效的 `SurfaceBindingPlan` 和初始 `HostProjection`。宿主拥有 parent/owner/z-order 与交互岛语义；surface 资源按 plan 的 ownership 清理。Attach 不激活窗口、不设 TopMost、不创建普通顶层 fallback。相同 RequestId 必须幂等，成功返回 HostSession、AttachmentId、ShellGeneration、LifecycleEpoch、SurfaceEpoch、source/host revisions、surface 状态和边界快照。

Attach 是一项可回滚事务：再次验证安全目标，创建不可见/不可输入的 host 结构，应用初始 projection；只有当前 projection 需要准备呈现时才绑定 surface、等待匹配 epoch 的 Ready 与首帧，再按有效呈现/输入门开放。隐藏、系统不可用或 STOPPED 可保持 `UNBOUND/READY_NO_FRAME`；以后显示前仍必须补过首帧门。任一步失败均逆序清理，禁止暴露未初始化白底、透明错误帧或不可见命中区。

### CommitProjection

提交一个不可分割的运行投影。业务层先以 expected source revision/CAS 得到权威 snapshot，宿主只拒绝 stale host revision 或非法投影，不反向成为业务状态权威。锁定阻止外部移动/缩放但不禁用 Item、解锁和收起；收起只改变底边/高度，顶部、水平位置、宽度和按钮位置保持不变。

### Surface notifications

`NotifySurfaceReady`、`NotifyFramePresented` 和 `NotifySurfaceFault` 将 renderer 生命周期与 host attachment 分开。所有通知必须携带 HostSession、Attachment、ShellGeneration、LifecycleEpoch 和 SurfaceEpoch；Ready 不等于 frame 已呈现，frame 必须携带 host/render revision 和实际 pixel bounds。旧 epoch 通知只能得到 stale 结果，fault 不允许宿主用 TopMost、激活或普通顶层替代物掩盖。

### ObserveHostState

返回只读一致快照，含正交意图、有效状态、surface/host revisions、Shell generation、边界和诊断。观察不得顺带调用 `SetWindowPos`、hide/show 或重挂来修复差异。

### RequestReconnect

用户入口或内部协调器可以请求一次受控重连，但不能指定 raw HWND 或强迫 unsafe attach。Shell 世代丢失后的自动恢复和显式重连共享同一个 `RecoveryCycleId` 和单飞状态机：旧 generation 先 fenced/清理并进入 degraded，发现唯一稳定安全的新目标后才重新协商并创建一组新表面；不明确或预算耗尽时保持 degraded。

### Detach

幂等清理 attachment 所有 host-owned visual/islands/hook/timer 和恢复任务，并按 SurfaceBindingPlan 有界释放或归还 surface。顺序为输入/呈现 fence、取消 operation、revoke callback、adapter release、host-owned 资源逆序销毁、零资源核对、写入 tombstone。第三方 release 超时进入 quarantine 并返回结构化不完整结果，不得复活旧 surface 或无限阻塞。

### BeginExit

Host 范围的粘滞退出入口。首次调用进入 `EXITING`，拒绝新的 negotiate/attach/commit/reconnect，取消恢复并对全部 attachment 执行有界清理。相同 RequestId 返回同一结果；当前 HostSession 不得从 EXITING 返回运行态。迟到 Shell/显示/renderer 事件只记诊断，不创建资源。

## 6. 生命周期状态机

完整规范见 `TS_H01_E3_LIFECYCLE_CONTRACT_2026-08-21.md`。接口级状态图冻结为：

```text
DETACHED
  -> DISCOVERING
     -> DEGRADED_NO_DESKTOP_HOST
     -> SURFACE_NEGOTIATING -> ATTACHING -> ATTACHED_SAFE
                              \-> FAULTED

ATTACHED_SAFE | ATTACHING | FAULTED
  -> SHELL_GENERATION_LOST
  -> DEGRADED_NO_DESKTOP_HOST
  -> RECOVERING -> DISCOVERING -> ... -> ATTACHED_SAFE

ATTACHED_SAFE
  -> FAULTED (surface/device loss while Shell remains safe)
  -> surface-only rebind -> ATTACHED_SAFE

any non-EXITING -> DETACHING -> DETACHED
any state       -> EXITING (sticky terminal for this HostSession)
```

### 6.1 有效呈现与输入

只有当前 HostSession/Attachment/ShellGeneration/LifecycleEpoch/SurfaceEpoch 全匹配、HostState 为 `ATTACHED_SAFE`、SurfaceState 为 `PRESENTED`、frame revision 对应当前 projection，且用户/系统状态允许时才可显示。输入还必须满足 InputAvailability 和当前 HitRegionSet。`PAUSED` 保留最后有效帧及按钮；Win+D、焦点和普通窗口覆盖不改变意图或触发重挂。

### 6.2 Shell loss 与 renderer loss

Shell loss 先递增 LifecycleEpoch、关闭呈现/输入、撤销旧事件、清理旧 generation，再进入 degraded 和有界恢复；找不到安全目标时不显示替代物。renderer/device loss 只使当前 SurfaceEpoch 失效，在 Shell 仍安全时执行 surface-only recovery；若同时发生 Shell loss，Shell cleanup 抢占 surface recovery。

### 6.3 串行化、单飞与迟到事件

所有迁移在一个逻辑 `HostCoordinatorQueue` 排序；回调只能排队，不直接改状态。优先级为 `BeginExit > Detach > Shell loss cleanup > desktop recovery > surface recovery > attach/commit`。相同恢复周期单飞；相同 RequestId/相同 payload 复用结果，相同 ID/不同 payload 拒绝。epoch 不匹配的 ready/frame/fault/release/reconnect 事件不得改变状态或复活资源。

### 6.4 有界恢复与清理

发现稳定窗口、negotiate/attach/ready/first-frame/release deadline、恢复次数、backoff、结果/tombstone 保留均为必须记录的有限策略字段，生产数值不在 E3 冻结。预算耗尽保持 degraded/faulted 并等待用户或真实外部状态变化；禁止无限 polling、无限重连和通用顶层 fallback。

## 7. 错误语义

```text
HostResult<T> = Success<T> | Failure<HostError>
RetryDisposition = NEVER | AFTER_STATE_CHANGE | BOUNDED_BACKOFF | USER_ACTION

HostError {
  stable_code
  operation
  request_id
  attachment_id?
  shell_generation?
  source_snapshot_revision?
  host_projection_revision?
  retry_disposition
  sanitized_reason
  diagnostic_ref?
}

NO_SAFE_HOST
AMBIGUOUS_TOPOLOGY
UNSUPPORTED_TOPOLOGY
SURFACE_TRANSPORT_UNSUPPORTED
SURFACE_PLAN_STALE
STALE_HOST_GENERATION
STALE_HOST_REVISION
STALE_SOURCE_SNAPSHOT
INVALID_GEOMETRY
INVALID_HIT_REGIONS
ATTACH_FAILED
PROJECTION_REJECTED
SURFACE_NOT_READY
SURFACE_FAULTED
RECOVERY_INCOMPLETE
DETACH_INCOMPLETE
DIAGNOSTIC_UNAVAILABLE
HOST_EXITING
REQUEST_ID_REUSE_MISMATCH
STALE_LIFECYCLE_EPOCH
STALE_SURFACE_EPOCH
OPERATION_PREEMPTED
ATTACH_TIMEOUT
SURFACE_READY_TIMEOUT
FIRST_FRAME_TIMEOUT
RECOVERY_BUDGET_EXHAUSTED
CLEANUP_INCOMPLETE
```

错误必须结构化区分业务 source revision、host revision、Shell generation、surface readiness 和 I/O/诊断故障。相同 RequestId 重试不得重复创建 attachment 或动作。任何错误都不得触发隐式 TopMost、激活、普通顶层 fallback、无限重连或清空权威状态。

## 8. 事件与诊断

每个候选至少输出：

- monotonic timestamp、candidate/build hash、HostSession、OS/Explorer generation；
- topology、host/surface state、recovery reason/cycle；
- lifecycle/surface epoch、HostEventSequence、in-flight operation/priority；
- surface/attachment identity、parent、owner、相邻 z-order；
- styles、visible、iconic、cloaked、active、TopMost；
- logical geometry、anchor、monitor/work area、window/client/DWM/render bounds、DPI；
- source snapshot、host projection、hit-region、render revision；
- presentation/interaction/runtime/user visibility/system availability/input availability/lock；
- attach/detach/recover/hide/show/recreate/preempt/late-event counts；
- first frame、commit、render fault、timeout/budget、resource ledger、tombstone/quarantine 和 cleanup result。

日志必须保留事件序列，不能只输出最终“正常”。

## 9. 候选一致性门

候选适配器必须证明：

1. 只通过 `DesktopHost` 改变桌面角色；
2. 使用同一 Night Ops 最小视觉与命中夹具；
3. 空白原生输入和交互岛同时成立；
4. Win+D 不触发 hide/show、detach/re-attach、TopMost 或激活；
5. 普通程序、文件选择器和全屏程序始终在海报之上；
6. 几何/命中/render revision 同步；
7. Shell 丢失时进入 degraded 而非顶层回退；
8. 退出后零自有表面；
9. 按统一候选证据矩阵独立记录结果，不能借用公共宿主 PASS 填充候选渲染项。
10. Ready/first-frame 分离，旧 SurfaceEpoch 的回调不能恢复可见或输入；
11. Shell loss、device loss、显式 reconnect、detach 和 exit 竞态遵守 E3 优先级且无重复资源。

## 10. TS-H01-E 冻结结论

E2 已冻结，并通过受控修订 E3-A1 补入 BeginExit 与 lifecycle fencing 身份：

- 能力必须带 EvidenceDisposition、环境范围和证据 ID；
- surface 通过 offer/plan 协商，任何 transport 都不能借公共探针结果预填候选 PASS；
- source snapshot、host projection、hit-region 和 render revision 分层；
- 呈现、交互、运行时、显隐、系统可用性、输入可用性和锁定正交；
- display target、逻辑 DIP、归一化锚点、约束与物理观测分离；
- 命中集合明确零/一/多/重叠、裁剪与 priority；
- 方法使用 RequestId 幂等和结构化 HostResult/HostError；
- E3-A1 的新增项不改变业务权威、修订分层或候选能力状态。

E3 已冻结：

- NegotiateSurface/Attach 的资源台账、首帧门和逆序回滚；
- Shell generation loss 与 renderer/device loss 的独立恢复通道及抢占顺序；
- automatic recover、显式 RequestReconnect、Detach 和 BeginExit 的幂等、单飞和迟到事件规则；
- 同进程/分进程 adapter 的逻辑串行队列、RequestId 副作用最多一次和事件缺口重同步；
- timeout/backoff/retry/tombstone/quarantine 的有界语义；
- traditional WorkerW、多屏和未测 transport 继续保持 UNKNOWN/NOT_RUN。

E4 已冻结：

- `TARGET_WORK_AREA_DIP` 是唯一持久化几何，物理像素只作边缘投影和诊断；
- `HostProjectionRevision` 原子关联 geometry、hit regions、render target、presentation、interaction 和 lock；
- drag/resize 使用有界 gesture session，live update 仍逐 revision 原子发布，结束时只向业务权威提交一次 CAS；
- 收起保持 top/left/width 与顶部控件位置，锁定只拒绝外部移动/缩放；
- display environment 变化从逻辑 anchor 重投影，旧 DPI/旧 frame 不得发布；
- 首帧、stale frame、bounds mismatch 和 lifecycle fence 有结构化裁决。

E5 已冻结：

- 公共事件 schema 为 `desktopup.desktop-host.event.v1`，sequence/time/session/build/candidate 身份可验证；
- E2-E4 的 lifecycle/surface/projection/display/hit/render revisions 和资源账本进入机器硬门；
- `prototype/desktop-host-win32/ts-h01-e5/` 提供 manifest、validator 与正反样本；
- validator 自测通过只表示夹具可工作，不能替代视频、人工输入/层级裁决或候选 P0。

E6 已冻结候选 adapter 的进入门、最小交付包、证据隔离、fail-fast、一次有界 CONDITIONAL 修正和退出状态。TS-H01-E 至此完成；TS-C01 至 TS-C07 仍全部是 `P0 NOT RUN`，TS-C08 在至少一个海报候选通过 P0 前保持 deferred，当前没有赢家。
