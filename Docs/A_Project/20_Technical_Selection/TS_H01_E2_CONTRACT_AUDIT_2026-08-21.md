# TS-H01-E2 DesktopHost 能力与数据合同审计

状态：`COMPLETE — E2 FROZEN WITH CONTROLLED AMENDMENT E3-A1 / E3 COMPLETE`

日期：2026-08-21

## 1. 审计目标

核对 `DESKTOP_HOST_PROTOTYPE_INTERFACE_V1.md` 草案是否同时符合七份冻结需求、TS-H01 探针真实能力和统一证据规则。审计不评价 UI 框架，不用用户接受的覆盖例外扩大技术结论。

## 2. 输入

- 冻结需求：项目画像、海报视觉与交互、运行表面与托盘、Item/布局、主题与 AI、风险/TBD、迁移追踪；
- TS-H01-B/C/D1/D2/D3 源码、运行器和 Evidence；
- Desktop Overlay Host 探针章程；
- 统一候选证据矩阵；
- E2 初始接口草案。

## 3. 发现与裁决

| ID | 严重度 | 草案问题 | 裁决 |
| --- | --- | --- | --- |
| E2-AUD-01 | P0 | 单一 `HostRevision` 容易与业务 snapshot revision/CAS 混用，使宿主变成第二状态权威 | 分离 `SourceSnapshotRevision`、`HostProjectionRevision`、`HitRegionRevision`、`RenderRevision`；宿主只保存运行投影 |
| E2-AUD-02 | P0 | `SetVisibilityIntent(VISIBLE \| PAUSED)` 把显隐与暂停合并，违反正交状态和手动暂停不禁用按钮 | 冻结 presentation、interaction、runtime、user visibility、system availability、input availability、lock 七个正交字段 |
| E2-AUD-03 | P0 | `NativeSurfaceRef` 含义不明确，并暗示一种 HWND transport 可以直接借给全部候选 | 改为 `SurfaceOffer -> SurfaceBindingPlan` 能力协商；每种 transport 对每个候选重新实测 |
| E2-AUD-04 | P0 | 几何没有显示目标、坐标空间和约束身份，容易把物理像素写回业务状态 | 增加 `DisplayTarget`、work-area DIP 坐标、constraints、anchor 与只读 PhysicalBounds；主屏-only 明示为范围而非多屏支持 |
| E2-AUD-05 | P0 | HitRegion 不能完整表达零/一/多/重叠、裁剪和优先级 | 冻结 `HitRegionSet`、local DIP、shape、clip、priority 和 `EXPLICIT_PRIORITY_THEN_UNION` |
| E2-AUD-06 | P0 | `PASS/USER_ACCEPTED/PARTIAL` 与统一证据状态不一致，可能隐藏 FAIL/NOT_RUN | 使用 `EvidenceDisposition`，显式包括 verified、user-accepted exception、partial、unknown、deferred、failed、not-run |
| E2-AUD-07 | P0 | Commit 没有 RequestId，重试可能重复创建/触发操作 | 所有可变操作携带 RequestId，并冻结幂等结果语义 |
| E2-AUD-08 | P0 | surface ready、首个实际 frame 和 window attached 被当作同一事实 | 分离 `NotifySurfaceReady` 与 `NotifyFramePresented`，frame 关联 render revision 和实际边界 |
| E2-AUD-09 | P1 | 错误只有 code 列表，没有 revision/generation/retry 结构 | 冻结 `HostResult<T>`、`HostError` 和 `RetryDisposition` |
| E2-AUD-10 | P1 | Recover 接口允许调用方看似可指定恢复目标 | 改为 `RequestReconnect`；调用方不能传 raw HWND，自动与显式恢复共享 E3 状态机 |
| E2-AUD-11 | P1 | runtime PAUSED/STOPPED 与 renderer fault/device loss 没有区分 | runtime intent 保留产品语义；surface fault 走独立 notification 和 E3 生命周期 |
| E2-AUD-12 | P1 | topology enum 可能诱导候选按类名/固定 HWND 分支 | topology 仅诊断；候选只消费 capability，角色发现仍由宿主负责 |

## 4. 需求追踪

| 冻结要求 | E2 合同位置 | 结论 |
| --- | --- | --- |
| RP-INV-01 / Poster §10 原生桌面输入 | HitRegionSet、未覆盖区域原生落到 Explorer | `COVERED` |
| RP-INV-02 / Runtime §10 精确层级与 Win+D | HostCapability、Attach 禁止 TopMost/activation/fallback | `COVERED WITH TS-H01 SCOPE` |
| RP-INV-05 单一状态权威 | SourceSnapshotRevision 与 HostProjectionRevision 分层 | `COVERED` |
| RP-INV-07 锁定保护边界 | HostProjection 正交 lock、interaction、presentation | `COVERED` |
| RP-INV-08 渲染器可替换 | SurfaceOffer/BindingPlan，不冻结框架 ABI | `COVERED` |
| RP-INV-11 呈现事实可验证 | SurfaceReady 与 FramePresented 分离，实际 bounds/revision | `COVERED` |
| Profile §9 / Poster §5-7 几何与收起 | DisplayTarget、DIP、anchor、constraints、presentation | `COVERED AT DATA LEVEL; ALGORITHM IN E4` |
| Profile §10 / Runtime §8 暂停、停止、显隐 | 正交 runtime/user/system/input 字段 | `COVERED` |
| Runtime §9 防回弹 | source/host revision 和 structured stale errors | `COVERED AT HOST BOUNDARY` |
| Runtime §11 Explorer 恢复 | RequestReconnect + generation 状态 | `TYPE COVERED; ORDERING IN E3` |
| Theme §6-7 宿主保留命中/拖动/锁定 | protected RegionRole 与 surface/host ownership | `COVERED` |
| 多屏、mixed DPI、traditional WorkerW | EvidenceDisposition + environment scope | `REMAINS UNKNOWN/DEFERRED` |

## 5. 探针能力与接口承诺边界

当前公共探针证明的是：在本机 raised Progman 拓扑中，host-owned layered child visual 和 split input islands 可以达到已记录的层级、输入、Win+D 与部分边界合同。它没有证明：

- Qt/WinUI/WPF/Avalonia/WebView2/Slint 的 surface 可以直接嵌入该 child；
- adapter-owned HWND、composition visual 或 offscreen source 已通过；
- 150%/200%、traditional WorkerW、多显示器、混合 DPI 或热插拔支持；
- Explorer 恢复已自动 10/10；
- 分辨率门已自动识别完整 10/10。

因此 `QueryCapabilities` 必须按 candidate/build/environment 返回证据化能力，不能从公共宿主结果预填候选 P0。

## 6. E2 冻结项

1. 能力查询及 evidence disposition；
2. surface transport offer/plan 协商字段；
3. source/host/hit/render revision 分层；
4. display target、逻辑几何、anchor、constraints 与物理观测分离；
5. HitRegionSet 的零/一/多/重叠语义；
6. 正交运行投影状态；
7. Query/Discover/Negotiate/Attach/Commit/SurfaceNotification/Observe/Reconnect/Detach 方法集合；
8. RequestId 幂等和结构化 HostResult/HostError。

## 7. 明确移交 E3-E5

- E3：surface ownership、线程/事件队列、attach/recover/detach/exiting、device loss 与 Shell loss 竞态；
- E4：几何约束公式、拖动阈值、连续 resize、收起/展开和 hit/render 同帧算法；
- E5：事件 schema、采样频率、录屏对齐、候选一致性 runner 和清理探针。

## 8. 结论

E2 经修订后不再包含已知需求冲突或把未验证 surface transport 写成通用能力的问题，状态改为 `FROZEN`。该冻结只允许继续设计 E3，不允许任何候选跳过 E3-E6 直接进入 P0。

## 9. E3-A1 受控修订记录

E3 生命周期审计发现 E2 方法集中虽然存在 `HostState.EXITING`，但没有显式、实现中立的退出入口；同时跨线程/跨进程迟到事件缺少 HostSession、LifecycleEpoch、SurfaceEpoch 和 RecoveryCycle 身份。若不补齐，E3 无法证明退出粘滞、资源 fencing、幂等和迟到事件裁决。

裁决：

- 新增 `BeginExit(RequestId) -> HostResult<ExitResult>`；
- 新增 `HostSessionId`、`LifecycleEpoch`、`SurfaceEpoch`、`RecoveryCycleId`；
- 新增对应的退出、epoch、超时、预算和清理错误码；
- E2 已冻结的业务/宿主 revision 分层、正交状态、surface offer/plan 和状态权威不变。

该修订编号为 `E3-A1`，详细理由与合同见 `TS_H01_E3_LIFECYCLE_CONTRACT_2026-08-21.md`。
