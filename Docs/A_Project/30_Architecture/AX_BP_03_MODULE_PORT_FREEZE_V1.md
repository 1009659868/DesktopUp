# AX-BP-03：DesktopUp V2 模块与端口冻结

状态：FROZEN — AX-BP-06 PASS

完成日期：2026-08-24

## 1. 文档职责

本文以 AX-BP-02 整体程序拓扑为输入，冻结逻辑模块职责和端口语义。

冻结不等于选择 crate、DLL、进程、线程或框架。后续实现可以合并或拆分部署单元，但不能合并职责、绕过端口或改变状态权威。

## 2. 端口通用规则

所有跨模块关系必须满足：

1. 输入和输出是不可含候选框架对象的稳定逻辑值；
2. 请求与最终结果分成两个方向，不伪装成同步完成；
3. 每个 mutation 带 RequestId 和 expected Revision；
4. 每个 surface 结果带 SurfaceEpoch 和相关 Revision；
5. 每个长任务结果带 JobEpoch；
6. 每个编辑手势带 SessionId；
7. 错误使用稳定分类，不用 UI 文案代替；
8. 端口不包含 HWND、GPU handle、数据库连接、线程句柄或 provider SDK 对象；
9. 未知字段/事件的兼容策略由后续公共合同阶段冻结；
10. Axiom Slot 只描述端口型位，不提供调用或调度实现。

## 3. 公共身份与版本概念

| 概念 | 职责 | 禁止替代 |
| --- | --- | --- |
| RequestId | 关联一次命令和其外部结果 | 时间戳 |
| Revision | 标识 accepted business snapshot | UI render count |
| SurfaceEpoch | 隔离 DesktopHost/Renderer 资源世代 | Revision |
| JobEpoch | 隔离扫描、导入、主题、AI 长任务 | RequestId |
| SessionId | 隔离拖动、缩放和编辑草稿会话 | PointerId |
| ItemId | Item 稳定身份 | 绝对路径 |
| ThemeId | 用户选择的主题实例身份 | fallback 名称 |
| AdapterId | 诊断实现占据者 | 模块逻辑身份 |
| SchemaVersion | 持久化/包/消息 schema 版本 | 应用展示版本 |

这些概念不得相互复用。

## 4. 端口类别

| 类别 | 方向 | 例子 |
| --- | --- | --- |
| CommandIn | Surface/外部 → Core | SetLocked、MovePoster、OpenControlCenter |
| SystemEventIn | OS/adapter → Core | ShellLost、DisplayChanged、FrameReady |
| StoreEventIn | StateStore → Core | Loaded、CommitAccepted、CommitRejected |
| QueryIntentOut | Core → adapter | LoadState、DiscoverTopology |
| EffectIntentOut | Core → adapter | CommitState、LaunchItem、AttachSurface |
| ProjectionOut | Core → Surface/adapter | PosterProjection、TrayProjection |
| DiagnosticOut | 任意模块 → Diagnostics | structured health/error event |
| DraftLocal | ControlCenter 内部 | ThemeDraft、GeometryDraft；不能成为 accepted state |

## 5. 模块总表

| Module ID | 名称 | 唯一职责 | 权威状态 |
| --- | --- | --- | --- |
| MOD-01 | RuntimeIngress | 归一化和分类外部输入 | 无业务权威 |
| MOD-02 | ApplicationCore | 唯一有序业务决策与接受点 | accepted snapshot 工作副本 |
| MOD-03 | ProjectionGraph | 从 snapshot 生成同 revision 投影 | 无 |
| MOD-04 | ValidationPolicy | 纯 schema、权限、能力和不变量验证 | 无 |
| MOD-05 | StateStorePort | 描述唯一正式持久化权威边界 | 正式业务状态由占据者拥有 |
| MOD-06 | PosterSurfacePort | 描述持续海报表面 | 无 |
| MOD-07 | ControlCenterPort | 描述按需管理/预览表面 | 本地短命草稿 |
| MOD-08 | TraySurfacePort | 描述托盘表面 | 无 |
| MOD-09 | DesktopHostPort | 描述桌面宿主能力边界 | 技术资源世代由占据者拥有 |
| MOD-10 | RendererPort | 描述画面生成能力边界 | 绘制资源世代由占据者拥有 |
| MOD-11 | ItemDomain | Item 身份、有效性、启动和统计规则 | 纯规则，无正式持久化 |
| MOD-12 | ActivityLayout | 评分、排序、人工控制和逻辑布局 | 纯算法状态 |
| MOD-13 | ThemeDomain | ThemeSpec、能力、草稿和 fallback 规则 | 纯规则/草稿 |
| MOD-14 | AiDomain | 上下文、ChangePlan、风险和确认规则 | 纯规则/会话摘要 |
| MOD-15 | FileSafety | 发现、仅添加、收纳计划、账本语义 | 账本正式数据经 StateStore/File 边界 |
| MOD-16 | LifecycleDomain | 启动、单实例、恢复、暂停和退出决策 | 逻辑 lifecycle state |
| MOD-17 | ShellIntegrationPort | 托盘、启动、选择器和系统通知边界 | 技术资源由占据者拥有 |
| MOD-18 | Diagnostics | 事件归一、脱敏、健康投影和审计 | 诊断缓冲非业务权威 |
| MOD-19 | DistributionPort | 安装、自启、升级、卸载边界 | 安装资源由占据者拥有 |
| MOD-20 | DesktopUpProgram | 组合根和依赖型位定义 | 不拥有运行资源 |

## 6. MOD-01 RuntimeIngress

职责：

- 接收 Surface command、Store event、Host event、Renderer event、System event 和 Job result；
- 检查 envelope 基本完整性；
- 归一化为 RuntimeInput；
- 保留 source、request/revision/epoch 元数据；
- 把不可识别输入转成结构化错误。

输入端口：

- SurfaceCommandIn；
- StoreEventIn；
- HostEventIn；
- RendererEventIn；
- SystemEventIn；
- JobResultIn。

输出端口：

- RuntimeInputOut；
- InvalidInputDiagnosticOut。

拥有状态：

- 仅允许短期去重/解析状态描述；具体实现机制未定。

禁止：

- 修改业务 snapshot；
- 调用 adapter；
- 决定 UI；
- 执行重试；
- 吞掉未知终态。

## 7. MOD-02 ApplicationCore

职责：

- 维护最后 accepted snapshot 工作副本；
- 串行裁决命令、store result 和 lifecycle event；
- 检查 expected revision、surface/job/session epoch；
- 产生 MutationPlan、EffectIntent、ProjectionSource 或拒绝；
- 保持 exiting sticky；
- 保持 StateStore 单一权威闭环。

输入：

- RuntimeInput。

输出：

- CoreTransition；
- EffectIntent list；
- ProjectionSource；
- CoreDiagnostic。

拥有状态：

- accepted snapshot 工作副本；
- lifecycle logical state；
- live surface/job/session epoch 摘要；
- in-flight request identity 摘要。

禁止：

- 直接数据库、文件、网络、Win32 或 GPU I/O；
- 在 Store commit 成功前接受 proposed state；
- 创建默认空状态掩盖失败；
- 保存 UI widget、HWND、线程或连接对象；
- 选择 adapter 技术。

## 8. MOD-03 ProjectionGraph

职责：

- 从一个 accepted snapshot 生成 Poster、ControlCenter、Tray、Diagnostics 投影；
- 保证同源 revision；
- 将业务状态与技术观察分离；
- 生成逻辑 Scene/Host/Hit intent。

输入：

- AcceptedSnapshot；
- HostRuntimeObservation；
- RendererRuntimeObservation；
- RuntimeHealth。

输出：

- PosterProjection；
- ControlCenterProjection；
- TrayProjection；
- DiagnosticProjection；
- HostProjectionIntent；
- VisualSceneIntent；
- HitRegionIntent。

状态：

- 无业务状态；允许纯计算缓存描述但不得成为权威。

禁止：

- 写 StateStore；
- 读取 UI 草稿作为 accepted state；
- 根据 renderer 私有能力静默改写需求语义；
- 自行发布物理 frame。

## 9. MOD-04 ValidationPolicy

职责：

- 命令 schema；
- mutation 范围；
- 权限；
- Theme capability；
- AI ChangePlan；
- Item identity；
- geometry/layout 不变量；
- 安全政策。

输入：

- ValidationRequest tagged union。

输出：

- ValidationAccepted(normalized value)；
- ValidationRejected(stable reason)。

状态：

- 纯规则和只读 capability snapshot。

禁止：

- 自动修复并提交；
- 网络/文件 I/O；
- 依赖 UI 文案；
- 在 TBD 未冻结时自行选策略。

## 10. MOD-05 StateStorePort

职责：

- 描述正式状态 load、migrate、recover、commit/CAS、ledger 和 flush 边界；
- 区分 NoData、Corrupt、UnknownVersion、Conflict、IoFailure；
- 返回 authoritative result。

请求端口：

- LoadStateRequest；
- CommitStateRequest(expected_revision, proposed)；
- Load/CommitLedgerRequest；
- FlushRequest；
- MigrationRequest。

事件端口：

- StateLoaded；
- NoData；
- StateLoadFailed；
- CommitAccepted(new_snapshot)；
- CommitRejected(conflict/error)；
- LedgerResult；
- FlushResult。

正式权威：

- StateStore adapter 占据者拥有持久化业务状态。

禁止：

- UI 直接调用；
- 自动用默认值覆盖损坏；
- JSON/localStorage 成为正式写入源；
- 返回成功但无新 revision；
- 把技术异常折叠成 NoData。

## 11. MOD-06 PosterSurfacePort

职责：

- 显示 PosterProjection；
- 把有限交互转成 CommandEnvelope；
- 报告实际表面交互状态；
- 保留透明空白的 Windows 桌面行为。

输入：

- PosterProjection；
- SurfaceAvailability；
- InteractionPolicyProjection。

输出：

- PosterCommand；
- GestureEvent；
- ItemGesture；
- SurfaceHealthObservation。

状态：

- 不拥有业务状态；只允许输入会话的短命技术状态。

禁止：

- 直接写 StateStore；
- 自行改变 z-order；
- 整窗捕获输入；
- 自行缓存并覆盖新 snapshot；
- 创建第二真实 Poster。

## 12. MOD-07 ControlCenterPort

职责：

- 按需显示管理 UI；
- 显示 accepted projection；
- 管理短命 draft 和预览；
- 提交带 expected revision 的命令；
- 关闭时释放自己的资源。

输入：

- ControlCenterProjection；
- CommandResult/Conflict；
- CapabilityProjection；
- DiagnosticProjection。

输出：

- ControlCommand；
- DraftPreviewIntent；
- Open/CloseObservation。

拥有状态：

- 本地 draft、selection、navigation；全部短命且非权威。

禁止：

- 启动时默认创建；
- 关闭时退出程序；
- 直接操纵真实 Poster HWND；
- 用 draft 覆盖 accepted snapshot；
- 成为后台 Core。

## 13. MOD-08 TraySurfacePort

职责：

- 显示 TrayProjection；
- 双击请求打开控制中心；
- 右键发出菜单命令；
- 呈现状态和健康。

输入：

- TrayProjection；
- RuntimeHealth。

输出：

- TrayCommand；
- TrayLifecycleObservation。

禁止：

- 单击执行默认动作；
- 直接改 StateStore/Renderer/DesktopHost；
- 用本地勾选缓存当权威；
- 创建第二托盘实例。

## 14. MOD-09 DesktopHostPort

职责由 E2–E6 冻结：

- capability/topology discovery；
- negotiate/attach；
- desktop role 与 z-order；
- geometry 和 hit regions；
- projection commit；
- Shell/display recovery；
- detach/exit；
- surface epoch 和诊断。

请求：

- QueryCapabilities；
- DiscoverTopology；
- NegotiateSurface；
- Attach；
- CommitHostProjection；
- RequestReconnect；
- Detach；
- BeginExit。

事件：

- Capability/Topology result；
- Attached/ProjectionCommitted；
- HostStateObservation；
- ShellLost/Recovered；
- SurfaceInvalidated；
- Detached/ExitResult；
- StableHostError。

禁止：

- 修改业务状态；
- 决定视觉内容；
- 使用普通 TopMost fallback；
- 让 renderer 或 UI 获得 z-order 所有权；
- 宣称类型通过等于 P0 通过。

## 15. MOD-10 RendererPort

职责：

- 接收逻辑 scene 和主题资源投影；
- 产生与 revision/epoch 关联的 frame observation；
- 报告 ready/fault/device loss/release；
- 按候选能力返回显式降级。

请求：

- PrepareRenderer；
- ApplyScene；
- ApplyThemeResources；
- RenderFrame；
- ReleaseRenderer。

事件：

- RendererReady；
- FrameReady(revision, epoch)；
- RenderFault；
- DeviceLost；
- RendererReleased；
- CapabilityFallback。

禁止：

- 操纵 DesktopHost parent/owner/z-order；
- 接收 HWND 以外的职责扩张由本文决定；
- 直接读取 StateStore；
- 改写 ThemeSpec；
- 把像素塞进通用业务消息；
- 在 AX-BP 中选择具体 renderer。

## 16. MOD-11 ItemDomain

职责：

- 稳定身份与去重；
- 类型和有效性；
- 发现候选归一；
- 启动意图；
- 启动成功门占位；
- 使用统计 mutation；
- 失效和恢复语义。

输入：

- ItemSnapshot；
- CandidateResult；
- ItemGesture；
- LaunchResult。

输出：

- NormalizedCandidate；
- ItemMutation；
- LaunchIntent；
- UsageMutation；
- ItemDiagnostic。

禁止：

- 路径作为唯一 ID；
- 双击重复启动；
- 在 success gate 前更新统计；
- 自动删除失效 Item；
- 直接移动文件。

## 17. MOD-12 ActivityLayout

职责：

- 特征、评分、稳定排序；
- 动态缩放；
- auto/anchored/full-manual overlay；
- 逻辑网格与跳变能力占位；
- 确定性和撤销语义。

输入：

- ItemSnapshot；
- ActivityPolicy；
- ManualControlState；
- LayoutConstraints。

输出：

- RankedItems；
- LogicalLayout；
- LayoutDiagnostic。

状态：

- 纯算法状态和明确的 manual control data；持久化仍经 StateStore。

禁止：

- 高频后台计时器假设；
- 在 V2-TBD-002/003 未决时冻结公式/阈值；
- 锁定时停止内部自动排名；
- 用 Rep<N> 表达动态 Item。

## 18. MOD-13 ThemeDomain

职责：

- TemplatePackage/ThemeInstance/minimal-fallback 逻辑；
- schema、capability、protected structure；
- 参数/素材/结构草稿；
- migration、preview、apply/rollback mutation；
- 包安全政策。

输入：

- ThemeSnapshot；
- PackageResult；
- ThemeDraftCommand；
- ThemeCapability；
- AiThemeDraft。

输出：

- ThemeValidation；
- ThemeDraftProjection；
- ThemeMutation；
- ThemeResourceIntent；
- ThemeDiagnostic。

禁止：

- 任意 native code 或不受限脚本；
- fallback 写回覆盖用户选择；
- 主题污染控制中心/其他实例；
- 直接文件/网络/进程/注册表/IPC；
- 在 V2-TBD-012 前冻结包格式。

## 19. MOD-14 AiDomain

职责：

- provider-independent request；
- 最小化/脱敏上下文；
- untrusted response parsing；
- ChangePlan schema/capability/risk validation；
- preview、confirmation 和撤销意图；
- provider error isolation。

输入：

- AiUserRequest；
- AiSettingsProjection；
- ProviderResult；
- CapabilitySnapshot。

输出：

- ProviderRequestIntent；
- ChangePlanDraft；
- ConfirmationRequest；
- normal CommandEnvelope；
- AiDiagnostic。

禁止：

- 直接写 StateStore；
- 直接执行 launch/file/theme install；
- 保存明文凭据；
- 在 V2-TBD-006 前自行免确认；
- 使无 AI 模式不可用。

## 20. MOD-15 FileSafety

职责：

- read-only discovery；
- add-only plan；
- .lnk/.url eligibility；
- backup/ledger/confirm/move/restore 语义；
- conflict 和 retry。

输入：

- DiscoveryCommand；
- CandidateFileResult；
- OrganizeCommand；
- FileOperationResult；
- PendingLedgerSnapshot。

输出：

- FileReadIntent；
- SafetyPlan；
- LedgerMutation；
- FileMove/RestoreIntent；
- ItemMutation；
- SafetyDiagnostic。

禁止：

- 移动普通文件；
- 无账本先移动；
- 覆盖同名目标；
- 未确认自动整理；
- 失败后删除保险库。

## 21. MOD-16 LifecycleDomain

职责：

- StartIntent；
- single instance；
- visibility/pause/lock ownership；
- Shell/display/renderer recovery；
- lifecycle priority；
- sticky exiting；
- owned-resource release order。

输入：

- Start/System/Host/Renderer/Tray lifecycle events。

输出：

- LifecycleTransition；
- Host/Renderer/Tray/Control/Store effect intents；
- LifecycleDiagnostic。

状态：

- logical lifecycle state；
- request/surface/job epoch 摘要；
- exiting flag。

禁止：

- Win+D 时 hide/restore 补救；
- exit 后重新激活；
- 自动启动控制中心；
- 用焦点或激活换层级。

## 22. MOD-17 ShellIntegrationPort

职责：

- item launch；
- file/folder picker；
- tray OS integration；
- system notifications；
- second-instance forwarding；
- Windows event acquisition。

请求：

- LaunchRequest；
- PickerRequest；
- TrayRequest；
- NotificationRequest；
- ForwardIntent。

事件：

- LaunchResult；
- PickerResult；
- TrayEvent；
- SystemEvent；
- ForwardResult。

禁止：

- 自行定义 launch success product gate；
- 直接修改业务状态；
- 自动移动文件；
- 让第二实例继续常驻。

## 23. MOD-18 Diagnostics

职责：

- normalize、redact、classify；
- runtime health projection；
- presentation health；
- evidence export；
- cleanup audit。

输入：

- DiagnosticEvent from all modules。

输出：

- DiagnosticProjection；
- RedactedDiagnosticBatchIntent；
- EvidenceManifest。

状态：

- 非业务诊断缓冲和健康摘要。

禁止：

- 记录凭据和不必要私人路径；
- 修复业务状态；
- 静默丢错误、cleanup result；
- 用“窗口存在”代替呈现事实。

## 24. MOD-19 DistributionPort

职责：

- install/upgrade/uninstall；
- autostart explicit opt-in；
- package/resource/path contract；
- state/ledger preservation；
- owned-resource cleanup。

请求：

- Install/Upgrade/Uninstall/Autostart intent。

事件：

- DistributionResult；
- CleanupInventory；
- StableDistributionError。

禁止：

- 删除不属于 DesktopUp 的文件/注册表；
- 静默启用自启；
- 覆盖损坏数据；
- 在 AX-BP 中选择安装器。

## 25. MOD-20 DesktopUpProgram

职责：

- 声明模块种类；
- 声明端口连接和 Slot；
- 声明静态纯子图；
- 声明 PhysicalExecutionBoundary；
- 输出整体 Blueprint。

禁止：

- 保存运行对象；
- 创建资源；
- 依赖 axiom-runtime；
- 成为全局 Service Locator；
- 绕过模块端口。

## 26. 状态所有权矩阵

| 状态 | 唯一正式所有者 | 可持有副本 | 禁止所有者 |
| --- | --- | --- | --- |
| Business snapshot | StateStore | ApplicationCore accepted copy、projections | UI/Renderer/Host |
| UI draft | ControlCenter | preview projector | StateStore 直到 commit |
| Poster geometry business value | StateStore | Core/Host projection | Host observation |
| physical window geometry | DesktopHost adapter | diagnostic observation | Renderer |
| frame resources | Renderer adapter | 无业务副本 | Core/UI |
| Item stats | StateStore | Item projection | ShellLaunch |
| Theme selection/params | StateStore | Theme/Surface projections | fallback |
| Credentials | protected provider settings boundary | redacted availability only | Core/log/theme |
| Recovery ledger | StateStore/FileSafety boundary | diagnostic summary | UI local state |
| Lifecycle logical state | ApplicationCore/LifecycleDomain | projections | adapter |
| Surface resource state | DesktopHost/Renderer adapters | Core observation summary | StateStore |
| Diagnostic buffer | Diagnostics | exported redacted evidence | business snapshot |

## 27. 错误族

| Error family | 产生者 | Core 处理 |
| --- | --- | --- |
| ValidationError | ValidationPolicy | reject/no effect |
| RevisionConflict | StateStore/Core | retain accepted, explicit conflict |
| StateUnavailable/Corrupt | StateStore | retain data, degraded/recovery |
| HostError | DesktopHost | fence epoch, degraded/recover |
| RenderError | Renderer | retain previous if valid, report health |
| LaunchError | ShellIntegration | no usage mutation |
| FileSafetyError | FileSafety/File boundary | retain source/backup/ledger |
| ThemeError | ThemeDomain | temporary fallback, no selection overwrite |
| AiError | Ai/provider | isolate, manual product stays usable |
| LifecycleError | LifecycleDomain | bounded degraded or exit |
| DistributionError | Distribution | retain installed/data state where possible |
| ProtocolError | RuntimeIngress | reject and diagnose |

错误必须携带 stable code 和适用 identity，不以本地化文本为机器语义。

## 28. Adapter Slot 冻结

| Slot | Request type family | Event type family |
| --- | --- | --- |
| StateStoreSlot | StoreRequest | StoreEvent |
| DesktopHostSlot | HostRequest | HostEvent |
| RendererSlot | RenderRequest | RenderEvent |
| PosterSurfaceSlot | PosterProjection | PosterCommand/Observation |
| ControlCenterSlot | ControlProjection | ControlCommand/Observation |
| TraySurfaceSlot | TrayProjection | TrayCommand/Observation |
| ShellIntegrationSlot | ShellRequest | ShellEvent |
| ThemePackageSlot | ThemeIoRequest | ThemeIoEvent |
| AiProviderSlot | ProviderRequest | ProviderEvent |
| DiagnosticsSinkSlot | DiagnosticBatch | DiagnosticSinkEvent |
| DistributionSlot | DistributionRequest | DistributionEvent |

每个异步 Slot 必须有 request 和 event 两个方向。不得把 request → final result 表达为同步 PortCell。

## 29. 端口版本与兼容边界

AX-BP 冻结以下原则，具体序列化由后续架构决定：

- 公共逻辑类型有 schema version；
- 新增可选字段不得改变既有语义；
- 未知必需能力必须 fail closed；
- 未知事件不得静默当成功；
- stable error code 与展示文案分离；
- adapter 私有数据留在 adapter 内；
- persistence schema、IPC schema 和 Rust 内部类型不是同一个版本面；
- Axiom 类型不进入外部包格式。

## 30. 禁止全局对象

禁止出现同时拥有以下三类以上能力的全局对象：

- business snapshot mutation；
- StateStore connection；
- HWND/DesktopHost；
- Renderer/GPU；
- ControlCenter widgets；
- Tray callback；
- provider credentials；
- file move；
- installer state。

组合根只能持有端口装配关系，不能成为绕过合同的万能对象。

## 31. AX-BP-03 退出门

| 检查 | 结果 |
| --- | --- |
| 20 个逻辑模块有唯一职责 | PASS |
| 输入/输出/状态/禁止项完整 | PASS |
| 公共身份不混用 | PASS |
| 状态所有权唯一 | PASS |
| 11 个 adapter Slot 双向表达 | PASS |
| 错误族稳定 | PASS |
| UI/adapter 无直接状态旁路 | PASS |
| 无候选框架类型 | PASS |
| axiom-runtime 使用量为零 | PASS |

## 32. 交接 AX-BP-04

AX-BP-04 必须以本文端口为节点，冻结命令流、状态流、投影流、效果流、观察流和反馈关系，并说明每条关系使用的 Axiom Core 组合形状。
