# TS-H01-E3 DesktopHost 生命周期与恢复合同

状态：`COMPLETE — E3 FROZEN / TS-H01-E COMPLETE`

日期：2026-08-21

## 1. 文档职责

本文冻结候选渲染器接入 `DesktopHost` 时共同遵守的生命周期语义：surface 协商、attach、首帧门、Shell 世代丢失、degraded、自动/显式恢复、renderer/device loss、detach 和 exiting。

本文不选择进程模型、线程库、IPC、渲染 API 或 UI 框架，也不把 TS-H01-D2 探针中的 HWND、类名、采样周期和固定次数写成生产架构。

## 2. 权威输入与证据边界

| 输入 | 可支持的结论 | 不可扩大解释 |
| --- | --- | --- |
| 冻结需求 Runtime §7–13、Profile RP-INV-02/04/05/08/11 | 生命周期、失败保留、单一权威、层级和退出边界 | 不决定进程、语言 ABI 或具体窗口框架 |
| TS-H01-D2 静态门 | 无 Shell 变化时唯一安全 attachment、正常退出零残留 | 不证明 10 次 Explorer 自动恢复 |
| TS-H01-D2 单次恢复失败证据 | 先清旧表面、进入 degraded、稳定发现后只建一组新表面在一次重建中可行 | 1/10 不能写成 H4 完整 PASS |
| TS-H01-D1/D3 | Win+D 不触发重挂，当前主屏 DPI/工作区范围可保持结构 | 不证明 traditional WorkerW、多屏、混合 DPI 或未测档 |
| E2 冻结合同 | surface 协商、修订分层、正交状态、RequestId 和结构化错误 | 不自动给任何候选 surface transport 赋 PASS |

E3 冻结的是候选必须实现和接受测试的语义，不是已验证能力。每个候选仍须按统一证据矩阵独立执行生命周期硬门。

## 3. E2 受控修订 E3-A1

E3 审计发现 E2 方法集中没有显式进入粘滞 `EXITING` 的入口，仅有 `HostState.EXITING`。若由实现私下切换，跨进程候选无法证明退出拒绝、单飞取消和迟到事件裁决一致。

因此登记以下最小修订：

- 新增 `BeginExit(RequestId) -> HostResult<ExitResult>`；
- 新增 `HostSessionId`、`LifecycleEpoch`、`SurfaceEpoch` 和 `RecoveryCycleId`，用于跨线程/跨进程迟到事件 fencing；
- 新增与退出、幂等冲突、epoch 过期、超时、预算耗尽和清理不完整对应的稳定错误码；
- 不改变 E2 已冻结的状态权威、修订分层、surface 协商或正交状态语义。

该修订只补齐 E3 无法绕开的生命周期入口和身份，不构成需求或架构变更。

## 4. 生命周期身份与正交状态

```text
HostSessionId   = 每次 DesktopHost 进程/服务实例唯一；重启后必须改变
LifecycleEpoch  = 每次会改变资源归属或有效事件集合的操作递增
SurfaceEpoch    = 每次实际 surface binding 实例唯一并单调递增
RecoveryCycleId = 每次自动或用户显式恢复周期唯一

SurfaceState = UNBOUND | BINDING | READY_NO_FRAME | PRESENTED |
               LOST | RELEASING | RELEASED

RecoveryCause = INITIAL_NO_SAFE_HOST | SHELL_GENERATION_CHANGED |
                SAFE_TARGET_INVALID | ATTACHMENT_RESOURCE_LOST |
                SURFACE_DEVICE_LOST | SURFACE_TRANSPORT_LOST |
                EXPLICIT_RECONNECT | SYSTEM_RESUME |
                ATTACH_OR_FRAME_TIMEOUT | INVARIANT_VIOLATION
```

`HostState`、`SurfaceState` 与 E2 的 `RuntimeMode/UserVisibility/SystemAvailability/InputAvailability` 相互正交：

- `HostState` 描述桌面宿主关系；
- `SurfaceState` 描述候选渲染表面的绑定与实际首帧；
- `RuntimeMode` 描述产品暂停/停止意图，不能冒充 renderer fault；
- `UserVisibility` 与系统不可见、Shell degraded 不合并；
- `HostSessionId + AttachmentId + ShellGeneration + LifecycleEpoch + SurfaceEpoch` 共同决定回调是否仍有效。

## 5. 资源所有权台账

| 资源 | 默认所有者 | 创建阶段 | 释放责任 | 禁止事项 |
| --- | --- | --- | --- | --- |
| Shell 目标发现快照 | Host | DISCOVERING | Host 丢弃 | 候选缓存 raw HWND 作为永久目标 |
| parent/owner/z-order 角色 | Host | ATTACHING | Host 撤销 | 候选直接改 parent、owner、TopMost 或激活 |
| host-owned visual slot/child | Host | ATTACHING | Host 销毁 | adapter 越权销毁或重挂 |
| 交互岛、命中窗口、hook、timer | Host | ATTACHING/COMMIT | Host 逆序销毁 | renderer 用整窗命中替代 |
| adapter-owned surface/device | Adapter | BINDING | Adapter 释放 | Host 在合同外关闭未知 handle |
| shared surface | SurfaceBindingPlan 明示 | BINDING | 按 plan 的 release handshake | 模糊共享、双方都假定对方清理 |
| recovery/backoff/cancellation | Host | RECOVERING | Host 取消 | detach/exit 后继续调度 |
| RequestId 结果与 attachment tombstone | Host | 首次请求/清理 | 有界保留后淘汰 | 用过期结果复活资源 |

每个 attachment 必须有可导出的资源台账。所有权未明确的 transport 协商失败，不允许“先创建再决定谁释放”。

## 6. 逻辑执行域与线程规则

1. 每个 `DesktopHost` 实例必须有一个逻辑串行的 `HostCoordinatorQueue`，所有状态迁移、资源台账和事件序号只在该排序域提交；它不要求特定线程库或进程模型。
2. Win32/框架对象仍在其要求的 owning thread 执行；候选不能因此绕过统一排序域。
3. renderer、Shell、timer 和 IPC 回调可来自任意线程，但只能复制为不可变事件并排队，不能直接修改生命周期状态。
4. Host 调用 adapter 时不得持有会阻止 adapter 回调排队的内部锁；同步重入一律转为排队事件。
5. 同进程和分进程使用同一语义：传输允许重复投递，但 `RequestId` 保证副作用最多一次；事件按 `HostSessionId + HostEventSequence` 排序。
6. 发现事件缺口或 HostSession 改变时，消费者调用 `ObserveHostState` 重新取快照，不猜测缺失迁移。

## 7. 总体状态机

```text
DETACHED
  -> DISCOVERING
     -> DEGRADED_NO_DESKTOP_HOST                  (没有唯一安全目标)
     -> SURFACE_NEGOTIATING
        -> FAULTED                                (无共同 transport)
        -> ATTACHING
           -> ATTACHED_SAFE                       (结构安全；是否可见由首帧门决定)
           -> DEGRADED_NO_DESKTOP_HOST            (目标在 attach 中失效)
           -> FAULTED                             (surface/adapter 失败)

ATTACHED_SAFE | ATTACHING | FAULTED
  -> SHELL_GENERATION_LOST
  -> DEGRADED_NO_DESKTOP_HOST
  -> RECOVERING
  -> DISCOVERING -> SURFACE_NEGOTIATING -> ATTACHING -> ATTACHED_SAFE

ATTACHED_SAFE
  -> FAULTED                                      (surface/device loss，Shell 仍安全)
  -> surface-only rebind -> ATTACHED_SAFE

任意非 EXITING 状态
  -> DETACHING -> DETACHED

任意状态
  -> EXITING                                      (粘滞终态；内部完成全部 detach/cleanup)
```

`FAULTED` 表示当前 attachment 或 surface 无法安全呈现，但不等于 `DEGRADED_NO_DESKTOP_HOST`。只有 Shell 目标不存在或不唯一时使用 degraded。任何非法迁移都返回结构化错误并保留诊断。

## 8. 有效呈现与输入门

```text
host_safe = HostState == ATTACHED_SAFE
            && attached_shell_generation == current_shell_generation

wants_visible = UserVisibility == SHOWN
                && SystemAvailability == AVAILABLE
                && RuntimeMode != STOPPED

may_present = host_safe
              && wants_visible
              && SurfaceState == PRESENTED
              && frame.surface_epoch == current_surface_epoch
              && frame.host_projection_revision == current_host_projection_revision

may_receive_input = may_present
                    && InputAvailability == ENABLED
                    && current HitRegionSet contains an enabled matching region
```

- `PAUSED` 仍可显示最后有效帧并保留按钮输入；
- 用户隐藏、锁屏、显示器关闭、STOPPED、fault、degraded、detach 和 exit 必须先关闭有效输入；
- Win+D、焦点变化、桌面右键和普通窗口覆盖不是显隐或恢复事件，不改变上述状态；
- 未达到首帧门前 host visual 与交互岛保持不可见/不可输入，禁止显示未初始化白底或透明错误帧。

## 9. Negotiate 与 Attach 事务

### 9.1 NegotiateSurface

- 纯能力协商，不创建长期资源；
- plan 绑定 candidate/build、HostSession、capability snapshot 和适用 Shell 拓扑范围；
- ShellGeneration 改变后 plan 默认失效，重新协商；只有将来明确证明 generation-independent 的 transport 才能例外；
- 协商失败进入 `FAULTED` 或保持 `DETACHED`，不得创建普通顶层 fallback。

### 9.2 Attach 顺序

1. 校验 Host 未 EXITING、RequestId、plan、初始 projection 和 source revision；
2. 进入新 `LifecycleEpoch`，为本次 attempt 分配新的 `AttachmentId`；
3. 再次确认唯一安全 Shell 目标和当前 `ShellGeneration`；
4. 建立资源台账；先创建不可见、不可输入的 host-owned 结构；
5. 若当前 projection 需要准备呈现，则根据 plan 请求/绑定 adapter surface 并分配新 `SurfaceEpoch`；用户隐藏、系统不可用或 STOPPED 可保持 `UNBOUND`；
6. 应用初始 geometry、projection 与 hit-region，但保持交互岛关闭；
7. 对已经开始的 binding 接受匹配 epoch 的 `NotifySurfaceReady`，进入 `READY_NO_FRAME`；
8. 只有准备呈现时才要求匹配当前 projection/render revision 与实际边界的 `NotifyFramePresented`；
9. 重新确认 Shell 目标、边界与层级不变量；
10. 只有 `may_present` 成立时原子开放 visual 与命中；否则保持安全隐藏但 attachment 可进入 `ATTACHED_SAFE`；
11. 发布完成快照和首帧事件；
12. 任一步失败都逆序回滚资源台账，旧/部分 surface 不进入可见状态。

`Attach` 成功只表示 attachment 事务被建立并可观察；`ATTACHED_SAFE` 表示桌面角色结构安全，不等同于用户当前可见。隐藏、系统不可用或 STOPPED 的 attachment 可以在 `UNBOUND/READY_NO_FRAME` 下保持安全隐藏；以后转为需要显示时必须先完成 surface binding 和首帧门，不能直接开放 visual 或输入。

Attach 期间允许接受当前 attachment 的更高 source revision，但 Host 只保留一份最新有效 desired projection；不得形成无界命令队列。旧 attachment、旧 epoch 或旧 host revision 的 commit 返回 stale 错误。

## 10. Shell 世代丢失与桌面恢复

### 10.1 丢失判定

满足任一条件即进入新的 `LifecycleEpoch`：

- Explorer PID/已验证角色树改变；
- parent、DefView、icon view 或安全关联失效；
- attachment 自有结构丢失；
- 当前目标不再唯一或不再满足已证拓扑合同。

类名变化、重复通知或单次临时枚举空结果不能单独伪造新 generation；判定条件和稳定窗口必须记录。

### 10.2 丢失处理顺序

1. 发布 `SHELL_GENERATION_LOST`，对旧 generation 建立 fence；
2. 立即使 `may_present/may_receive_input` 为 false；
3. 取消较低优先级的 attach、commit、surface recovery 和 timer；
4. 撤销旧回调接受权，旧 frame/ready/commit 只能得到 stale 结果；
5. 按资源台账逆序清理旧 host-owned 资源，并向 adapter 发出有界 release；
6. release 超时的 adapter/shared 资源进入 quarantine/tombstone，不得阻塞到无限期，也不得复用；
7. 核对自有可枚举窗口、交互岛、hook、timer 和恢复任务为零；
8. 进入 `DEGRADED_NO_DESKTOP_HOST`；
9. 事件驱动开始一个有界 `RecoveryCycleId`，寻找连续稳定且唯一的安全目标；
10. 找到新目标后生成新 `ShellGeneration`，重新协商 plan，并以新 `AttachmentId/SurfaceEpoch` attach 最新权威 snapshot；
11. 找不到目标或预算耗尽时保持 degraded，并暴露“重新连接桌面”，绝不升为普通顶层或 TopMost。

旧 Shell 签名不能被当成“新 generation”。同一 generation 同时最多一个 live attachment；显式 detach 完成后的同 generation 重连也必须生成新的 AttachmentId/LifecycleEpoch，不能复活 tombstone。

### 10.3 自动与显式重连

- 自动恢复和 `RequestReconnect` 进入同一状态机；
- 同一 `RecoveryCycleId` 只允许一个 in-flight discover/attach；
- 自动恢复进行中收到显式请求时加入现有 operation，返回同一最终结果，不创建第二组表面；
- 预算耗尽后，只有用户显式动作、ShellGeneration 真实变化或系统 resume 才能开启新周期；
- reconnect 不能携带 raw HWND 或跳过安全发现。

D2 的稳定 32 个 8 ms 样本只作为当前探针证据。E3 只要求稳定窗口有界、可配置、可记录并在候选间使用同一测试预算。

## 11. Renderer/device loss

1. `NotifySurfaceFault` 必须携带当前 AttachmentId、ShellGeneration、SurfaceEpoch、原因和可恢复性；
2. 先将 `SurfaceState` 置为 `LOST` 并关闭输入，旧 epoch 的 frame 立即失效；
3. 若 Shell 目标仍安全，不执行 Shell detach/re-discover，不改变 ShellGeneration，也不抬升层级；
4. 在同一 attachment 内启动一个有界 surface-only recovery，分配新 SurfaceEpoch；
5. adapter-owned/shared 旧资源按 plan 释放或 quarantine，host-owned桌面角色可保留为安全隐藏结构；
6. 新 surface 重新经过 Ready 与首帧门后恢复 `ATTACHED_SAFE`；
7. 若期间发生 Shell loss，Shell loss 立即抢占 surface recovery，后者的所有迟到事件失效；
8. 连续 surface fault 超出预算时保持 `FAULTED`、停止自动重试并提供诊断/手动重连；不得把 `RuntimeMode` 改写为 PAUSED 或 STOPPED 掩盖故障。

主题/模板 fallback 属于上层 renderer 与状态合同；DesktopHost 只保证失败 surface 不越权显示、不接收不可见输入且不污染权威业务状态。

## 12. Detach 合同

`Detach` 对相同 RequestId 幂等，对已经完成的 attachment 返回同一 `DetachResult`。顺序固定为：

1. 进入新 LifecycleEpoch 和 `DETACHING`；
2. 关闭输入与有效呈现；
3. 取消该 attachment 的 attach/recovery/backoff/commit；
4. revoke adapter 回调和旧 epoch；
5. 请求 adapter 停止绘制并按 plan 有界释放；
6. 逆序销毁交互岛、host visual、hook、timer 和其他 host-owned 资源；
7. 核对零自有表面、零恢复任务和零可接受迟到事件；
8. 写入 attachment tombstone，进入 `DETACHED`。

adapter release 失败返回 `DETACH_INCOMPLETE/CLEANUP_INCOMPLETE` 并 quarantine 其身份；Host 仍必须完成可控的自有资源清理，不能因为第三方未响应而无限阻塞退出或重挂旧 surface。

## 13. BeginExit 与粘滞 EXITING

`BeginExit(RequestId)` 是 Host 范围的高优先级、幂等操作：

1. 立即把 Host 标记为粘滞 `EXITING`；
2. 拒绝新的 negotiate、attach、commit、reconnect 和 surface recovery；
3. 取消未完成的发现、backoff 和恢复周期；
4. 对全部 attachment 执行等价于 Detach 的输入 fence 和清理；
5. 等待有界 release，超时资源进入 quarantine 并登记；
6. 返回含每个 attachment 清理结果的 `ExitResult`；
7. 退出中的 Shell、显示、托盘和 renderer 迟到事件只记诊断，不得创建资源。

`EXITING` 不返回普通运行状态；只有新的 HostSession 才能再次启动。全应用仍按冻结需求由外层生命周期协调器先取消 AI/扫描/导入并刷新账本，再调用 Host BeginExit，最后移除托盘并结束进程。

## 14. 幂等、单飞与抢占优先级

### 14.1 RequestId

- 相同 RequestId、相同 operation 和相同 payload：返回同一个进行中句柄或终态结果，不重复副作用；
- 相同 RequestId、不同 operation/payload：`REQUEST_ID_REUSE_MISMATCH`；
- 传输失败导致结果未知时，调用方只能用相同 RequestId 重试；
- ledger 至少保留到 operation 终态和相关 attachment tombstone 到期；HostSession 改变后必须重新观察状态。

### 14.2 优先级

| 优先级 | 操作 | 抢占规则 |
| ---: | --- | --- |
| 1 | `BeginExit` | 抢占全部；禁止后续恢复 |
| 2 | `Detach` | 抢占该 attachment 的恢复、attach 和 commit |
| 3 | Shell generation loss cleanup | 抢占 surface recovery、attach 和 commit |
| 4 | 自动/显式桌面恢复 | 同一 RecoveryCycle 单飞，二者合流 |
| 5 | surface/device recovery | 只在 Shell 仍安全时运行 |
| 6 | initial attach / projection commit | 被以上操作 fencing |

抢占不等于同步杀死线程；它递增 LifecycleEpoch、撤销旧操作提交权并等待有界清理。迟到完成只能记录为 `OPERATION_PREEMPTED`。

## 15. 迟到事件裁决矩阵

| 事件 | 接受条件 | 不满足时 |
| --- | --- | --- |
| `NotifySurfaceReady` | HostSession、Attachment、ShellGeneration、LifecycleEpoch、SurfaceEpoch 全匹配且 state=BINDING | 返回/记录 stale，不改变状态 |
| `NotifyFramePresented` | 上述身份匹配，且 projection/render/bounds 符合当前期望 | 旧帧丢弃；未来未知 revision 拒绝并诊断 |
| `NotifySurfaceFault` | 当前 SurfaceEpoch 匹配 | 旧 fault 只记诊断，不触发新恢复 |
| `CommitProjection` | 当前 attachment/generation，expected host revision 匹配，Host 非 DETACHING/EXITING | 返回当前 revision 与明确 retry disposition |
| 重复 Shell 通知 | 与当前已处理 generation/cycle 相同 | 合并计数，不创建新 attachment |
| reconnect 完成 | RecoveryCycle、LifecycleEpoch 仍当前，未被 detach/exit 抢占 | 清理其部分资源，不发布成功 |
| adapter release ack | 匹配 live operation 或 tombstone | tombstone 只更新清理诊断，不复活资源 |
| 任意事件在 EXITING | 仅 cleanup ack/诊断可记录 | 不创建、不显示、不恢复 |

## 16. 有界策略参数

E3 冻结以下字段必须存在、使用单调时钟、写入证据，但不冻结生产数值：

```text
discovery_stability_window
negotiate_deadline
attach_deadline
surface_ready_deadline
first_frame_deadline
adapter_release_deadline
max_shell_recovery_attempts_per_cycle
max_surface_recovery_attempts
bounded_backoff_schedule
recovery_cooldown
request_result_retention
attachment_tombstone_retention
```

- 任何预算不得为无限；
- timeout 不能触发 TopMost、激活或普通顶层 fallback；
- 候选同构测试使用相同预算；若框架要求不同预算，必须作为候选限制与证据披露；
- 静态/暂停稳定态不得靠高频 polling 维持层级或呈现。

## 17. 失败分类与稳定错误

| 分类 | 目标状态 | 自动重试 |
| --- | --- | --- |
| 没有唯一安全 Shell 目标 | `DEGRADED_NO_DESKTOP_HOST` | 有界、事件驱动 |
| surface transport 不支持 | `FAULTED` 或 DETACHED | 不重试；重新选择候选/plan |
| attach/首帧超时 | `FAULTED`；若目标同时失效则 degraded | 有界 |
| renderer/device loss | `FAULTED` + SurfaceState.LOST | surface-only 有界恢复 |
| Shell generation loss | degraded -> recovering | 有界、单飞 |
| detach cleanup 不完整 | DETACHED + tombstone 诊断 | 不复活；可在退出报告 |
| recovery 预算耗尽 | 保持 degraded/faulted | 等用户或真实外部状态变化 |
| EXITING 中请求 | `EXITING` | 永不重试当前 HostSession |

新增稳定错误：`HOST_EXITING`、`REQUEST_ID_REUSE_MISMATCH`、`STALE_LIFECYCLE_EPOCH`、`STALE_SURFACE_EPOCH`、`OPERATION_PREEMPTED`、`ATTACH_TIMEOUT`、`SURFACE_READY_TIMEOUT`、`FIRST_FRAME_TIMEOUT`、`RECOVERY_BUDGET_EXHAUSTED`、`CLEANUP_INCOMPLETE`。

## 18. E3 非协商不变量

1. 任一 generation 同时最多一个 live attachment；
2. 任一 attachment 同时最多一个 live SurfaceEpoch；
3. Ready 不等于 Presented，首帧前不可显示或开放输入；
4. Shell loss 先 fence 与清旧代，再发现和创建新代；
5. device loss 不伪造成 Shell loss，Shell loss 可抢占 device recovery；
6. 自动和显式恢复共用同一单飞状态机；
7. detach/exit 后旧事件永不复活资源；
8. EXITING 粘滞，当前 HostSession 不返回运行态；
9. 任何失败不产生 TopMost、激活、普通顶层 fallback、无限轮询或无限重试；
10. Host 不写回业务权威状态，恢复始终读取最新正式 snapshot；
11. 用户隐藏、系统不可用、PAUSED、STOPPED、fault 与 degraded 不互相覆盖原因；
12. traditional WorkerW、多屏和未验证 surface transport 继续保持 UNKNOWN/NOT_RUN。

## 19. 候选生命周期 P0 义务

每个 TS-C01 至 TS-C07 adapter 至少必须证明：

- cold attach 与 hidden attach 均无未初始化可见帧；
- Ready/first-frame 分离且 revision/epoch 可诊断；
- Shell loss 期间零普通顶层、零 TopMost、零重复 attachment；
- 自动恢复与连续显式重连不会创建第二组 surface；
- 注入 device loss 后不重挂 Shell、不接受旧 frame；
- detach、控制中心关闭、Host exit 后资源台账归零；
- 同进程/分进程方案均能以 RequestId 安全重试未知结果；
- 迟到 ready/frame/fault/release/Shell 事件全部按矩阵裁决；
- 超时和预算耗尽进入可诊断 degraded/faulted，不无限重试。

这些义务是后续 E5 夹具输入，不代表公共宿主已经替候选通过。

## 20. 残余风险

- D2 Explorer 重建自动遥测只有 1/10，E3 顺序来自需求、单次证据和保守 fencing，仍需候选独立执行 10 轮；
- renderer/device loss 尚无公共实际 surface adapter，当前为合同冻结、证据状态 `NOT_RUN`；
- 分进程断连、进程崩溃和共享 surface release 尚无实现证据，候选必须报告；
- traditional WorkerW、多屏、混合 DPI、负坐标和热插拔不因 E3 冻结获得支持；
- quarantine/tombstone 是语义要求，实际 OS/GPU 资源能否及时回收仍由候选验证。

## 21. 移交 E4/E5

E4 接收：

- 首帧门与有效呈现/输入公式；
- HostProjectionRevision、HitRegionRevision、RenderRevision 与 SurfaceEpoch 的原子关联；
- attach/recovery 中 latest desired projection 的有界规则；
- Shell/DPI/工作区变化时几何重投影不能越过 lifecycle fence。

E5 接收：

- HostSession、LifecycleEpoch、SurfaceEpoch、RecoveryCycleId 和 HostEventSequence；
- 每次 operation 的 RequestId、优先级、开始/抢占/完成、超时和 cleanup 结果；
- 资源台账、tombstone、quarantine、首帧和迟到事件计数；
- 候选同预算、同注入步骤、同 P0 判定的 runner。

## 22. 结论

E3 已冻结生命周期顺序、所有权、串行化、首帧门、恢复优先级、幂等、单飞、超时和迟到事件语义。该冻结不选择候选，也不补写 D2 缺失证据。E4-E6 已依次完成原子投影、诊断夹具和候选交接；本文件继续作为这些后续合同的生命周期权威输入。
