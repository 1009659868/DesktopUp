# AX-BP-04：DesktopUp V2 调用、数据与组合关系冻结

状态：FROZEN — AX-BP-06 PASS

完成日期：2026-08-24

## 1. 文档职责

本文以 AX-BP-02 整体拓扑和 AX-BP-03 模块/端口为输入，冻结：

- 命令流；
- 权威状态流；
- 投影流；
- 效果请求与结果反馈流；
- 观察与诊断流；
- 草稿与预览流；
- Axiom Core 组合形状；
- revision、epoch、session 的因果规则；
- 禁止的环和暗链。

本文不实现事件循环、线程、队列、runtime 或 IPC。

## 2. 流类别

| Flow ID | 类别 | 方向 | 可靠性语义 |
| --- | --- | --- | --- |
| FT-CMD | Command | Surface/System → Core | 不静默丢失，显式接受/拒绝 |
| FT-STORE | Authority | Core ↔ StateStore | commit/result 成对，CAS |
| FT-PROJ | Projection | Core → Surface/Host/Renderer | 同 revision，可替换未发布旧投影 |
| FT-EFFECT | Effect intent | Core → External boundary | 请求不是事实，必须有结果 |
| FT-OBS | Observation | Adapter/System → Core | 带 epoch/revision，可能过期 |
| FT-DRAFT | Local draft | ControlCenter internal | 非权威，关闭/取消可丢弃 |
| FT-DIAG | Diagnostic | Any → Diagnostics | 错误和终态不可静默丢失 |
| FT-JOB | Long job | Core ↔ scan/theme/AI boundary | 带 JobEpoch，可取消，迟到隔离 |
| FT-GESTURE | Gesture session | Surface ↔ Core | Update 可合并，End/Cancel 可靠 |

## 3. Axiom Core 组合映射

| 组合 | V2 关系 | 约束 |
| --- | --- | --- |
| Chain | RuntimeIngress → ApplicationCore → ProjectionGraph | 同步纯定义顺序，不代表线程 |
| Merge | 多事件归一器 → RuntimeIngress | 仲裁和调度留给物理边界 |
| Broadcast | AcceptedSnapshot → 多投影 | 输入 snapshot 不可被分支修改 |
| Diamond | snapshot 分投影后以 revision 一致性汇合 | 汇合是逻辑一致性，不代表 frame 已发布 |
| Choice | Command/Event tagged family | 分支必须穷尽或显式 Unknown |
| Opt | AI/主题覆盖等可选分支 | None 不影响核心路径 |
| Feedback | EffectResult → later RuntimeInput | 必须跨物理边界/时间，不允许即时自递归 |
| Slot | External adapter request/event seam | 类型兼容不等于行为通过 |
| Static | validation/activity/layout 等纯子图 | 不用于 OS、I/O 或 dynamic adapter |

## 4. 顶层因果链

~~~text
ExternalInput
  -> [F-001] Normalize
  -> [F-002] RuntimeInput
  -> [F-003] ApplicationDecision
      -> [F-004] no-change/reject
      -> [F-005] ProjectionSource
      -> [F-006] EffectIntent
  -> [F-007] ProjectionGraph
  -> [F-008] Surface/Adapter projection
  -> [F-009] ExternalResult/Observation
  -> [F-010] RuntimeInput feedback
~~~

F-009 → F-010 是后续输入，不是 F-006 的同步返回值。

## 5. 核心边登记

| Edge | From | To | Value | 组合/类型 |
| --- | --- | --- | --- | --- |
| F-001 | Surface/System adapter | RuntimeIngress | raw typed event | Slot boundary |
| F-002 | RuntimeIngress | ApplicationCore | RuntimeInput | Chain |
| F-003 | ApplicationCore state | Core decision | CoreTransition | PortCell step |
| F-004 | Core decision | source surface/Diagnostics | Reject/NoChange | Choice |
| F-005 | ApplicationCore | ProjectionGraph | AcceptedSnapshot + observations | Chain |
| F-006 | ApplicationCore | EffectBoundary | EffectIntent list | Broadcast/Choice |
| F-007 | ProjectionSource | projectors | same snapshot | Broadcast/Diamond |
| F-008 | projectors | Surface/Host/Renderer Slots | typed projections | Slot |
| F-009 | adapter occupant | RuntimeIngress | Event/Result | Slot event side |
| F-010 | RuntimeIngress | ApplicationCore | normalized feedback | Chain |

## 6. 命令 envelope

所有可改变正式业务状态的命令：

~~~text
CommandEnvelope {
  request_id
  source
  expected_revision
  command
  optional session_id
}
~~~

规则：

- mutation 必须有 expected_revision；
- lifecycle-only 命令可没有 business revision，但必须有 request identity；
- source 用于权限和诊断，不用于决定权威；
- UI 不能自行生成“已成功”结果；
- 同 RequestId 的重复输入必须幂等或稳定拒绝；
- stale command 不能自动覆盖新状态。

## 7. 权威状态闭环

~~~text
F-S01 SurfaceCommand
  -> F-S02 CommandEnvelope
  -> F-S03 Validation
  -> F-S04 MutationPlan
  -> F-S05 StoreCommitIntent(expected R)
  -> F-S06 StateStore physical boundary
  -> F-S07 CommitAccepted(snapshot R+1) / Rejected
  -> F-S08 RuntimeIngress
  -> F-S09 Core monotonic acceptance
  -> F-S10 ProjectionGraph
~~~

不变量：

1. F-S04 不改变 accepted snapshot；
2. F-S05 是意图，不是已提交事实；
3. 只有 F-S07 Accepted 才能推进 revision；
4. R+1 只是示意，实际 revision 分配由 StateStore 合同决定；
5. 非递增或过期结果拒绝；
6. rejection 保留 R；
7. 所有表面只消费 F-S10；
8. 没有 Surface → StateStore 直接边。

## 8. 初始加载与 NoData

~~~text
LoadIntent
  -> StateStore
  -> one of:
       Loaded(snapshot R)
       NoData
       Corrupt
       UnknownVersion
       IoFailure
~~~

- Loaded 进入正常接受；
- NoData 允许专门的 InitializeState 决策；
- Corrupt/Unknown/Io 不得走 NoData；
- InitializeState 仍通过正式 commit；
- 迁移来源只读，原始数据先保留。

## 9. 多投影一致性

~~~text
AcceptedSnapshot R
  -> Broadcast
      -> PosterProjection R
      -> ControlCenterProjection R
      -> TrayProjection R
      -> DiagnosticProjection R
      -> HostProjectionIntent R
      -> VisualSceneIntent R
~~~

任何投影不得带 R 的业务内容却标 R+1。Host/Renderer 技术观察可有自己的 epoch，但不能伪装成业务 revision。

## 10. 呈现发布关系

~~~text
PosterProjection R
  -> Host geometry/hit request (R,E)
  -> Renderer scene request (R,E)
HostCommitted(R,E) + FrameReady(R,E)
  -> PresentabilityDecision
  -> PublishSurface(R,E)
~~~

必要条件：

- E 是当前 live SurfaceEpoch；
- geometry、hit、frame 同 R；
- Host 和 Renderer 均未 fault；
- 旧 R/E 结果只诊断，不发布；
- previous valid presentation 可按后续架构策略保留，但不能显示一半新状态。

## 11. 控制中心草稿关系

~~~text
AcceptedProjection R
  -> DraftSession S
  -> DraftUpdate*
  -> Preview(S,R)
  -> one of:
       Cancel(S) -> discard
       Commit(S, expected R) -> normal command flow
       Conflict(S, actual R2) -> reload/merge/discard choice
~~~

草稿关系是局部 Opt/Choice，不进入 Broadcast 的 accepted source。

禁止环：

~~~text
Draft -> Preview -> Draft
~~~

可以在 ControlCenter 内作为局部交互，但不得连接到 ApplicationCore State，除非经过 Commit。

## 12. Gesture 会话

~~~text
GestureBegin(Session S, base R)
  -> LiveUpdate(S,n)*
  -> End(S,final) / Cancel(S)
~~~

- 每个 S 只有一个终态；
- Update(n) 单调；
- 旧 S 的 Update 在新 S 开始后失效；
- Cancel 不产生正式 geometry mutation；
- End 产生一个带 expected R 的命令；
- 锁定时 Begin 稳定拒绝；
- collapse/expand 不改变顶部锚点语义；
- physical pixel movement observation 不直接写 business geometry。

## 13. Item 发现与添加关系

~~~text
DiscoveryCommand
  -> JobEpoch J
  -> FileReadIntent
  -> CandidateEvent(J)*
  -> Normalize/Deduplicate
  -> Preview
  -> AddOnlyCommand(expected R)
  -> authority loop
~~~

- 旧 J 结果不能加入新扫描；
- 取消后结果只诊断；
- preview 不创建 Item；
- stable identity 决定去重，路径不是唯一身份。

## 14. 安全收纳关系

~~~text
OrganizeCommand
  -> SafetyPlan
  -> LedgerPrepare
  -> BackupVerified
  -> Confirmation
  -> FileMoveIntent
  -> FileMoveResult
  -> LedgerCommit
  -> ItemMutation
~~~

序列约束严格，不得交换：

- LedgerPrepare 在 Move 前；
- BackupVerified 在 Move 前；
- Confirmation 在 Move 前；
- ItemMutation 在文件和账本结果明确后；
- failure 保留 ledger/backup/source；
- restore conflict 不能覆盖。

## 15. 启动与统计关系

~~~text
ItemGesture
  -> LaunchIntent
  -> Shell boundary
  -> LaunchResult
  -> LaunchSuccessGate
  -> UsageMutation
  -> authority loop
~~~

V2-TBD-011 保留为 LaunchSuccessGate Slot。任何 surface、Shell adapter 或 ItemDomain 都不能自行跳过该 Slot。

## 16. 活跃度和布局关系

~~~text
ItemSnapshot R
  -> Eligibility
  -> ActivityFeatures
  -> ActivityPolicy
  -> StableSort
  -> ManualOverlay
  -> LayoutPolicy
  -> LogicalLayout R
~~~

组合：

- Chain：Eligibility → Features → Score；
- StableSort 是纯 Cell；
- ManualOverlay 可视为 Choice/merge of auto + anchored + manual；
- LayoutPolicy 是可代换静态策略位，但当前未选公式/阈值；
- dynamic Item collection 是值，不使用 Rep。

## 17. 主题关系

~~~text
ThemePackageEvent(J)
  -> SafetyValidation
  -> SchemaMigration
  -> CapabilityValidation
  -> ThemeDraft
  -> Preview
  -> ApplyCommand(expected R)
  -> authority loop
  -> ThemeProjection R2
~~~

failure：

~~~text
ThemeProjection failure
  -> temporary minimal-fallback presentation
  -> Diagnostic
  - no ThemeSelection mutation
~~~

Fallback 是呈现分支，不是状态写回边。

## 18. AI 关系

~~~text
AiRequest
  -> ContextPolicy
  -> ProviderIntent(J)
  -> ProviderResult(J)
  -> ChangePlanParse
  -> ValidationPolicy
  -> RiskChoice
  -> Preview/Confirmation
  -> CommandEnvelope(expected R)
  -> authority loop
~~~

ProviderResult 是不可信输入。AI 路径在 Opt 分支中；None/disabled/failed 均回到正常手动产品路径。

禁止：

- ProviderResult → StateStore；
- ProviderResult → ShellLaunch；
- ProviderResult → FileMove；
- ProviderResult → ThemeInstall；
- ProviderResult → DesktopHost。

## 19. Lifecycle 关系

~~~text
LifecycleEvent
  -> Normalize
  -> PriorityChoice
  -> LifecycleTransition
  -> EffectIntents
  -> AdapterResults
  -> RuntimeInput feedback
~~~

优先级固定：

1. BeginExit；
2. Detach；
3. ShellLost/RendererLost；
4. Reconnect；
5. normal projection。

Exiting 是吸收状态：

~~~text
Exiting + any late result -> Diagnostic only
~~~

## 20. Shell/Renderer 恢复关系

~~~text
Loss(E)
  -> fence E
  -> mark not publishable
  -> retain accepted R
  -> request recovery E+1
  -> observations(E+1,R)
  -> matching publication
~~~

旧 E 的 Attached、FrameReady、CommitProjection 或 DeviceReady 都不能复活旧资源。

## 21. 显示变化关系

~~~text
Display/DPI/WorkArea event
  -> normalize environment observation
  -> recompute logical-to-physical projection
  -> new geometry/hit/frame intent with same or newer R
  -> atomic presentability gate
~~~

显示变化不修改用户业务几何，除非明确产生经过 CAS 的归一化业务 mutation。具体策略由后续完整架构决定。

## 22. 暂停、可见和锁定关系

| 概念 | 命令路径 | 影响 |
| --- | --- | --- |
| visible | normal command/CAS | 是否发布海报，不销毁业务状态 |
| paused | normal command/CAS | 暂停允许暂停的动态效果/任务，不破坏按钮 |
| locked | normal command/CAS | 冻结外部几何和人工重排，不停内部排名 |
| collapsed | normal command/CAS | 改内容区域，保持顶部锚点 |
| stopped/deactivated | lifecycle command | 释放适用资源，语义区别于 pause |

这些状态不得由 Surface 本地 toggle 直接成为事实。

## 23. Distribution 关系

~~~text
DistributionIntent
  -> external result
  -> reconcile installed inventory / state migration / autostart state
  -> diagnostics
~~~

Distribution 不进入 BusinessSnapshot mutation，除非有明确的迁移/设置命令经 StateStore。卸载清理使用 owned inventory，不扫描删除未知目标。

## 24. 诊断关系

~~~text
Any module event
  -> DiagnosticNormalizer
  -> Redaction
  -> HealthProjection
  -> external sink intent
~~~

诊断流可以 Broadcast，不反馈修改业务状态。只有用户或系统产生的正式命令能触发修复路径。

## 25. Feedback 合法性

合法 Feedback 必须同时满足：

- 输出先跨一个 PhysicalExecutionBoundary；
- 后续结果具有新的事件身份；
- 重新进入 RuntimeIngress；
- Core 可按 revision/epoch 判断过期；
- 环有终止、取消或有界恢复条件；
- exiting 能吸收迟到结果。

不合法：

- ApplicationCore step 内立即调用自身；
- Surface ↔ Surface 状态同步环；
- Renderer ↔ DesktopHost 私有重挂环；
- StateStore callback 直接修改 UI；
- Diagnostics 自动修复状态；
- retry 无上限。

## 26. 顺序和替换规则

| 值 | 可否被更新值替换 | 说明 |
| --- | --- | --- |
| uncommitted projection R | 可 | 新 R 到达且旧 R 未发布 |
| Gesture Update | 可合并 | 同 Session，End/Cancel 不可替换 |
| Diagnostic performance sample | 可采样 | Error/Cleanup 不可丢 |
| lifecycle terminal result | 不可 | Ready/Fault/Detached/Exit |
| StateStore result | 不可 | 每个 RequestId 必须结案 |
| File ledger result | 不可 | 影响恢复 |
| AI partial streaming text | 可 | ChangePlan final/failed 不可丢 |
| Host/Renderer old epoch observation | 不发布 | 保留诊断，可丢弃业务处理 |

这些是物理实现必须满足的语义，不选择具体机制。

## 27. Revision 与 Epoch 关系

~~~text
Business Revision R
  independent from
SurfaceEpoch E
  independent from
JobEpoch J
  independent from
SessionId S
~~~

有效呈现键：

~~~text
PresentationKey = (poster_id, revision R, surface_epoch E)
~~~

有效 job result：

~~~text
JobKey = (job_kind, job_epoch J, base_revision optional R)
~~~

有效 gesture：

~~~text
GestureKey = (poster_id, session S, base_revision R)
~~~

不得用单个全局计数器替代所有概念。

## 28. 组合闭包

静态纯子图：

- CommandValidationGraph；
- ProjectionGraph；
- ItemActivityGraph；
- LayoutGraph；
- ThemeValidationGraph；
- ChangePlanValidationGraph；
- DiagnosticRedactionGraph。

这些子图可以继续 Chain/Diamond/Choice 组合。

外部 Slot 不被吸收到 Static 子图，保证技术边界可替换。

## 29. 禁止边登记

| Forbidden edge | 风险 |
| --- | --- |
| ControlCenter → StateStore | 双重权威、回弹 |
| Tray → DesktopHost | 生命周期旁路 |
| Poster → Renderer private API | renderer 绑定 |
| Renderer → z-order | 覆盖普通窗口 |
| DesktopHost → accepted snapshot | 技术观察污染业务 |
| AI → EffectBoundary | 绕过命令/确认 |
| Discovery → FileMove | 未确认整理 |
| LaunchIntent → UsageMutation | 未验证成功 |
| Fallback → ThemeSelection | 覆盖用户选择 |
| Diagnostics → Mutation | 观察者变权威 |
| axiom-runtime → any module | unstable runtime 进入生产 |

## 30. AX-BP-04 退出门

| 检查 | 结果 |
| --- | --- |
| 9 类流定义完整 | PASS |
| 10 条顶层边完整 | PASS |
| 权威状态闭环无旁路 | PASS |
| 多投影同 revision | PASS |
| effect result 只经 RuntimeIngress 返回 | PASS |
| gesture/job/surface 过期隔离 | PASS |
| Feedback 有物理边界与终止条件 | PASS |
| 禁止边登记完整 | PASS |
| Axiom 组合只用于描述 | PASS |
| 未实现 runtime/调度/IPC | PASS |

## 31. 交接 AX-BP-05

AX-BP-05 必须逐条接收 FT-*、F-* 和外部 Slot，标注纯计算边界、I/O 类型、资源所有权、可能部署域、顺序/可靠性要求和后续技术选型归属，但不得选择具体物理机制。
