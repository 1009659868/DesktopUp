# TS-H01-E4 几何、命中与渲染原子合同

状态：`COMPLETE — E4 CONTRACT FROZEN`

日期：2026-08-21

## 1. 目的与边界

本合同把 D3 已证明的单主屏几何行为转换为候选无关的 `DesktopHost` 语义，确保可见矩形、命中岛和 renderer target 永远属于同一个宿主投影。它不冻结 Win32 API 调用顺序、渲染框架、进程边界或多屏算法，也不把未测 DPI、多屏、mixed-DPI、负坐标和热插拔写成已支持。

权威事实保持不变：125% DPI、任务栏工作区变化和当前 raised Progman 路径有完整通过证据；100% 只有历史/静态的部分证据；150%/200%、traditional WorkerW 和多屏仍是 `UNKNOWN / DEFERRED`；D3-2 的 8/10 自动遥测例外继续存在。

## 2. 坐标空间与权威

只允许以下空间：

```text
TARGET_WORK_AREA_DIP  业务 snapshot 的逻辑位置和尺寸；唯一持久化几何
POSTER_LOCAL_DIP      命中形状；原点为当前海报可见矩形左上角
PHYSICAL_PX           宿主投影、renderer target 与诊断；只读观测
```

- `DisplayTarget + LogicalGeometryDip + NormalizedAnchor + GeometryConstraintsDip` 来自权威 snapshot。
- 物理 `window/client/extended-frame/render-target` 不得写回业务状态，也不得成为下一次拖动的权威起点。
- DPI、分辨率或工作区改变只触发重投影；Win+D、焦点、右键、文件选择器和普通窗口覆盖不属于几何事件。
- 禁止固定顶部条带、边框或阴影像素补偿；若候选无法使可见框、客户区和 render target 对齐，该候选在对应矩阵项失败。

## 3. 原子投影事务

```text
ProjectionTransaction {
  request_id
  attachment_id
  host_session_id
  lifecycle_epoch
  expected_host_projection_revision
  source_snapshot_revision
  display_environment_revision
  display_target
  logical_geometry
  normalized_anchor
  geometry_constraints
  presentation_mode
  interaction_mode
  runtime_mode
  visibility_and_availability
  is_locked
  hit_region_set
  expected_render_revision
}
```

接受事务时产生一个新的 `HostProjectionRevision`。该 revision 同时标识：

1. 当前逻辑几何和物理投影；
2. 当前命中集合及裁剪；
3. renderer 必须呈现的目标尺寸和内容 revision；
4. presentation、interaction、lock 与有效输入门。

宿主在变更资源前后都检查 `HostSessionId + AttachmentId + LifecycleEpoch`。任一身份、expected revision、约束或 display environment 已过期，整个事务拒绝；不得只更新窗口、命中或 renderer 中的一部分。迟到 frame、旧命中集合和旧 projection 只能记录 `STALE_IGNORED`，不能重新可见。

同一 attachment 最多保留一个尚未应用的“最新期望投影”。新事务可显式取代旧 pending 事务，但不得重排已提交 revision。Attach/recovery 期间合并 pending 只合并意图，不借此跳过首帧门。

## 4. DIP、锚点与物理投影

当前 presentation 的高度记为 `h`，work area 为 `W × H`：

```text
availableW = max(0, W - width)
availableH = max(0, H - h)
anchorX    = availableW == 0 ? edgeFallbackX : left / availableW
anchorY    = availableH == 0 ? edgeFallbackY : top  / availableH
left       = clamp(anchorX * availableW, safeLeft, availableW - safeRight)
top        = clamp(anchorY * availableH, safeTop,  availableH - safeBottom)
```

`edgeFallback` 由已保存的 `edge_affinity` 决定；没有 affinity 时取 0，且必须写入诊断。约束无法同时满足时按 `safe insets -> min size -> max size -> work area` 的固定优先级得出一个可解释的 clamp 结果并发出 `geometry_constraint_clamped`，不得生成负尺寸或隐式像素偏移。

DIP 转像素按四条边分别投影和舍入，再由 `right-left`、`bottom-top` 得到尺寸，避免独立舍入位置与宽高造成漂移。D3 的观测容差仅允许边界投影舍入误差不超过 1 px；容差不是允许白条、灰框或非客户区的视觉豁免。

每次提交后必须满足：

```text
window_rect == client_screen_rect == extended_frame_bounds == render_target_bounds
```

若平台 API 无法保证逐像素恒等，候选必须在证据中记录所有原始矩形和差值，并由矩阵门裁决，不能只报最终海报尺寸。

## 5. 拖动与缩放会话

连续交互使用有界的 `GeometryGestureSession`：

```text
BeginGeometryGesture(session_id, kind, pointer_id, start_revision)
UpdateGeometryGesture(session_id, logical_delta_or_edges)
EndGeometryGesture(session_id, final_source_snapshot_revision)
CancelGeometryGesture(session_id, reason)
```

- Begin 捕获当前逻辑几何、约束、pointer 和 lifecycle fence；同一 attachment 只允许一个活动会话。
- 每个 live update 都生成新的 host projection revision，仍须原子更新 visual/hit/render；不能只移动 HWND 后等待业务层补齐。
- End 只向业务权威提交一次最终命令/CAS；宿主不是第二份持久化状态。
- CAS 失败、锁定、detach、Shell generation/lifecycle epoch 改变或 pointer capture 丢失时取消会话，并以一个原子 revision 恢复开始时或最新权威几何。
- resize 明确活动边/角；未拖动边保持固定，尺寸受 min/max/safe inset 约束。`COLLAPSED` 不接受外部 resize。
- 可见交互不得因为一次拖动而失去后续拖动、resize 或进入编辑模式的能力；会话结束必须释放捕获并输出终态事件。

## 6. 收起、展开与锁定

- 收起/展开保持 `left/top/width` 不变，只改变 `height/bottom`；顶部标题栏、锁定与收起按钮的屏幕位置不得移动。
- `expanded_height` 始终保留，收起态不能用当前高度覆盖它。
- 收起态仅保留明示控件命中；被隐藏 Item 的区域必须在同 revision 从命中集合移除。
- 锁定拒绝来自标题栏、resize edge/corner 和外部布局的移动/缩放，但不禁用 Item、解锁、收起/展开和允许的上下文动作。
- 锁定、收起或 interaction mode 改变不允许重新创建普通顶层表面或改变桌面层级。

## 7. 命中集合合同

`HitRegionSet` 必须与当前 `HostProjectionRevision` 同时提交：

- 零区域：全视觉穿透，Explorer 原生接收空白输入；
- 一区域：只命中登记形状；
- 多区域：逐形状命中，不得扩为总包络矩形；
- 重叠：先按显式 priority 选语义角色，再对同一所有权区域求并集；
- 所有形状裁剪到当前可见 poster-local bounds；禁用或不可见区域不得保留窗口遮挡；
- 命中结果记录 region id、role、revision 和 pointer 坐标，不记录或推断 Explorer 图标坐标。

`HitRegionRevision` 必须能反查唯一的 `HostProjectionRevision`。若 visual 已移动而 hit 尚未准备，宿主宁可暂时关闭该 revision 的输入，也不得暴露旧命中。

## 8. 首帧与可见发布

Attach、surface rebind、恢复、尺寸或像素目标改变时执行：

1. 在不可见且不可输入状态准备 host projection 与 target；
2. 提交 geometry/hit-disabled/render expectation；
3. renderer 以相同 HostSession、Attachment、LifecycleEpoch、SurfaceEpoch、HostProjectionRevision 和 physical bounds 回报 Ready/FramePresented；
4. coordinator 在同一串行 turn 发布该 frame 并启用同 revision 的命中；
5. 任何身份或 bounds 不匹配均丢弃，不显示上一个尺寸的拉伸帧。

隐藏、STOPPED 或系统不可用可停在 `UNBOUND/READY_NO_FRAME`；以后变为可见时仍要通过首帧门。PAUSED 可保留最后一个有效 frame，但 presentation/geometry 改变后必须获得匹配新 revision 的静态帧。连续拖动允许 adapter 使用已协商的低延迟更新模型，但每个被发布的 frame 仍须对应唯一 revision，不能用旧 frame 填白或让 hit 先行。

## 9. 显示环境变化

- `DisplayEnvironmentRevision` 在 DPI、分辨率、work area、显示目标有效性变化时递增。
- 当前 V1 只认证主屏；目标丢失时按 `PRIMARY_ONLY_V1` 重投影，并记录 `display_target_fallback`，不推导多屏能力。
- 重投影使用逻辑几何与归一化锚点；禁止从上一次 physical rect 逐次缩放。
- display change 与 Shell generation change 分开处理；前者不应触发 detach/re-attach，后者遵循 E3 degraded/recovery。
- 变化期间只允许最新 display revision 发布；旧 DPI/旧工作区 frame 不得闪现。

## 10. 错误与诊断

至少返回或记录：

```text
STALE_HOST_REVISION
STALE_SOURCE_SNAPSHOT
STALE_LIFECYCLE_EPOCH
STALE_DISPLAY_ENVIRONMENT
GEOMETRY_CONSTRAINT_INVALID
GEOMETRY_GESTURE_CONFLICT
GEOMETRY_GESTURE_CANCELLED
LOCKED_OPERATION_REJECTED
HIT_REGION_INVALID
RENDER_REVISION_MISMATCH
RENDER_BOUNDS_MISMATCH
FIRST_FRAME_TIMEOUT
```

事件必须含 request/session/attachment/lifecycle/surface/projection/display revisions、逻辑几何、四组物理矩形、DPI、命中 revision、render revision、clamp/cancel reason 和单调时间。

## 11. 候选一致性义务与退出门

每个 TS-C01 至 TS-C07 候选必须使用同一 Night Ops fixture 和 E5 诊断 schema，独立证明：

1. 冷启动、恢复、显示与 resize 不出现白条、灰框、裁切或旧尺寸帧；
2. 20 次移动、20 次缩放、20 次收起/展开后 visual/hit/render revision 一致；
3. 锁定与零/一/多/重叠命中矩阵满足原生桌面输入；
4. DPI、分辨率、工作区变化按已纳入 P0 的范围重投影；
5. stale frame、CAS 冲突、gesture cancel 和 lifecycle change 能被夹具识别。

公共 TS-H01 的通过和用户接受例外不能借给候选。E4 的完成只表示合同可交接，不表示任何 adapter 已实现或通过。

## 12. 结论

E4 已冻结。几何、命中与 renderer 不再是三个可独立“随后修正”的通道，而是一个可 fencing、可观测、可整体拒绝的 `HostProjectionRevision`。下一工作包 E5 以此合同建立机器可验证事件 schema 与跨候选一致性夹具。
