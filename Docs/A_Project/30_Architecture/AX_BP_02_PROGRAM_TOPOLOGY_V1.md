# AX-BP-02：DesktopUp V2 整体程序拓扑

状态：FROZEN — AX-BP-06 PASS

完成日期：2026-08-24

## 1. 文档职责

本文使用 AX-BP-01 冻结的 Axiom Core 描述方法，定义 DesktopUp V2 的完整逻辑程序拓扑。

本文冻结：

- 系统上下文；
- 顶层逻辑模块；
- 定义、装载和激活关系；
- 主要运行场景子图；
- 外部世界边界；
- 七份冻结需求的拓扑落点；
- 禁止存在的旁路。

本文不冻结端口字段细节、进程线程、runtime、renderer/UI、StateStore 技术、IPC 或安装器实现。

## 2. 权威输入

1. DesktopUp V2 七份冻结需求；
2. 需求一致性审计；
3. DesktopHost E2–E6 冻结合同；
4. AX-BP-01 Axiom Core 语义映射；
5. Axiom Core-only 与 axiom-runtime 排除决定。

## 3. 系统角色

| 角色 ID | 逻辑角色 | 说明 |
| --- | --- | --- |
| SYS-01 | DesktopUpProgram | 整体定义蓝图 |
| SYS-02 | RuntimeIngress | 接收并归一化命令、结果和系统事件 |
| SYS-03 | ApplicationCore | 唯一有序业务决策和状态接受点 |
| SYS-04 | ProjectionGraph | 从 accepted snapshot 生成各表面投影 |
| SYS-05 | EffectBoundary | 把副作用意图发送至外部能力 |
| SYS-06 | StateStore | 唯一正式业务状态权威 |
| SYS-07 | PosterSurface | 唯一持续可见产品表面 |
| SYS-08 | ControlCenter | 按需管理与预览表面 |
| SYS-09 | TraySurface | 快捷入口和生命周期控制 |
| SYS-10 | DesktopHost | 桌面层级、命中、几何和恢复宿主 |
| SYS-11 | Renderer | 候选可替换的视觉呈现能力 |
| SYS-12 | ItemDomain | Item 身份、有效性、发现、启动与统计语义 |
| SYS-13 | ActivityLayout | 活跃度、排序、缩放、锚定和布局 |
| SYS-14 | ThemeDomain | 主题模型、验证、草稿、安装意图与投影 |
| SYS-15 | AiDomain | Provider 抽象、上下文、ChangePlan 和权限 |
| SYS-16 | FileSafety | 发现、仅添加、安全收纳、账本与恢复语义 |
| SYS-17 | LifecycleDomain | 启动、单实例、暂停、可见、恢复和退出 |
| SYS-18 | Diagnostics | 结构化状态、错误、呈现健康和审计 |
| SYS-19 | ShellIntegration | 外部启动、托盘、文件选择和 Windows 通知边界 |
| SYS-20 | Distribution | 安装、自启、升级、卸载和数据保留边界 |

这些是逻辑角色，不等于 crate、进程、线程、窗口或服务数量。

## 4. 系统上下文

~~~text
                         Windows Desktop / Explorer / Displays
                                      |
                                      v
User <-> PosterSurface / ControlCenter / TraySurface
                  |           |             |
                  +-----------+-------------+
                              |
                              v
                       RuntimeIngress
                              |
                              v
                       ApplicationCore
                   /          |           \
                  v           v            v
          ProjectionGraph  EffectBoundary  Diagnostics
             /  |  \           |
            v   v   v          v
        Poster Control Tray  typed adapter boundaries
                              |
         +----------+---------+---------+---------+---------+
         v          v         v         v         v         v
     StateStore DesktopHost Renderer Shell/File Theme I/O AI Provider
~~~

关键约束：

- 所有外部结果必须重新进入 RuntimeIngress；
- 任何 adapter 不得直接修改 accepted snapshot；
- 任何 UI 不得直接写 StateStore；
- PosterSurface 不拥有业务状态；
- ControlCenter 关闭不影响 Poster、Tray、Core 或 StateStore；
- Diagnostics 观察和记录，不成为状态权威。

## 5. 顶层 Axiom Core 形状

~~~text
DesktopUpProgramGraph =
  Chain<
    RuntimeIngress,
    Chain<
      ApplicationReducer,
      EmissionProjector
    >
  >

AcceptedSnapshotProjection =
  Diamond<
    SnapshotIdentity,
    PosterProjector,
    AuxiliaryProjector,
    ProjectionJoin
  >
~~~

概念用途：

| 形状 | 用途 |
| --- | --- |
| Chain | 输入归一化 → 决策 → 投影 |
| Merge | 多类已归一化事件进入 RuntimeIngress 的逻辑汇入口 |
| Broadcast/Diamond | 一个 accepted snapshot 生成多个同 revision 投影 |
| Feedback | adapter result 在后续时刻作为新 RuntimeInput 返回 |
| Choice/Opt | 可选 AI、可选主题覆盖等静态分支描述 |
| Slot | StateStore、DesktopHost、Renderer 等实现边界 |
| Blueprint | DesktopUpProgram 的零资源定义 |

Feedback 只描述因果闭环，不提供事件循环或调度实现。

## 6. 定义、绑定描述与激活

~~~text
Definition
  module kinds
  + typed logical ports
  + composition
  + physical boundary markers
             |
             v
Binding description
  required adapter capabilities
  + request/result shapes
  + ownership constraints
             |
             v
Future full architecture
  selects implementations
             |
             v
Activation
  creates real resources
~~~

AX-BP 只完成前两层。

## 7. 启动拓扑

~~~text
StartIntent
  -> IntentNormalizer
  -> SingleInstanceDecision
      -> secondary: ForwardOpenControlCenterIntent -> exit secondary
      -> primary:
           RequestStateLoad
           -> StateStoreResult
           -> Validate/Migrate/Recover decision
           -> AcceptInitialSnapshot
           -> Project Tray
           -> Discover DesktopHost topology
           -> Negotiate logical surface
           -> Request Renderer readiness
           -> Match snapshot revision + frame revision
           -> Publish Poster + HitRegions atomically
           -> RuntimeReady
~~~

不变量：

- 普通首实例启动不创建控制中心；
- autostart 是显式 opt-in，启动后只创建海报和托盘；
- StateStore 失败保留旧数据和诊断，不用空白默认覆盖；
- DesktopHost 不安全时进入 degraded，不创建普通 TopMost fallback；
- first frame、几何和命中 revision 不一致时不得发布输入；
- 相同启动意图幂等。

## 8. 权威状态与投影拓扑

~~~text
Surface/System Command
  -> CommandEnvelope(expected_revision, request_id, source)
  -> Validate
  -> MutationPlan
  -> StoreCommitIntent(expected_revision)
  -> StateStore
  -> StoreCommitResult
  -> RuntimeIngress
  -> ApplicationCore accepts newer snapshot only
  -> ProjectionGraph
      -> PosterProjection
      -> ControlCenterProjection
      -> TrayProjection
      -> DiagnosticProjection
~~~

StateStore 是正式业务状态权威。ApplicationCore 只保存最后 accepted snapshot 的工作副本；Renderer frame、Host observation、UI draft 和 Tray check state 都不是业务权威。

## 9. 海报运行拓扑

~~~text
AcceptedSnapshot
  -> PosterProjection
      -> VisualSceneIntent -> Renderer boundary
      -> HostProjectionIntent -> DesktopHost boundary
      -> HitRegionIntent -> DesktopHost boundary
      -> PosterStatusIntent -> PosterSurface
RendererResult + HostResult
  -> RuntimeIngress
  -> PresentabilityDecision
  -> publish / retain previous / degraded
~~~

PosterSurface 包含：

- 标题栏和受保护交互；
- Item 视觉和启动入口；
- 状态栏；
- 锁定、收起/展开；
- 有限命中岛；
- 透明空白穿透。

禁止：

- 整窗输入捕获；
- 浏览器默认右键；
- 临时 TopMost；
- 用控制中心预览替代真实海报；
- renderer 自行决定 z-order 或命中。

## 10. 控制中心与编辑拓扑

~~~text
OpenControlCenterCommand
  -> EffectIntent(OpenControlCenter)
  -> ControlCenter boundary
  -> RequestCurrentProjection
  -> display accepted snapshot
  -> LocalDraftSession
      -> preview-only changes
      -> cancel: discard draft
      -> commit:
           CommandEnvelope(expected_revision)
           -> normal StateStore CAS loop
  -> close:
       release preview resources
       keep Poster/Core/Tray/StateStore alive
~~~

控制中心预览：

- 是 accepted snapshot 或草稿的缩放投影；
- 不是第二个真实 Poster；
- 不拥有 DesktopHost 层级；
- 不得写回过期 snapshot；
- 冲突必须显式返回并允许重新加载/合并/放弃。

## 11. 托盘拓扑

~~~text
AcceptedSnapshot + RuntimeHealth
  -> TrayProjection
  -> TraySurface

Tray gesture
  single click -> no action
  double click -> OpenControlCenterCommand
  menu command -> normal CommandEnvelope or LifecycleCommand
~~~

托盘不直接控制 HWND、StateStore 或 Renderer。菜单勾选状态来自 TrayProjection，不由菜单内部缓存决定。

## 12. Item 发现与添加拓扑

~~~text
User starts discovery
  -> DiscoveryIntent
  -> File/Shell read-only boundary
  -> CandidateResult
  -> NormalizeCandidate
  -> DeduplicateByStableIdentity
  -> Preview
  -> user chooses:
       ignore
       add only -> ItemMutation -> StateStore CAS
       safe organize -> SafetyPlan
~~~

默认只读发现；不得自动移动、删除或覆盖用户文件。

## 13. 安全收纳拓扑

~~~text
SafetyPlan
  -> EligibilityCheck(.lnk/.url only)
  -> ConflictCheck
  -> RecoveryLedgerPrepare
  -> VerifiableBackup
  -> UserConfirmation
  -> MoveIntent
  -> FileBoundaryResult
      -> success: ledger commit + Item mutation
      -> failure: retain source/backup/ledger + retry entry
~~~

恢复与卸载：

~~~text
PendingLedger
  -> Reconcile
  -> restore without overwrite
  -> preserve conflicts and retry
~~~

普通文件永远不进入移动路径。

## 14. Item 启动与统计拓扑

~~~text
ItemGesture
  -> InteractionModeDecision
  -> LaunchIntent(item_id, target)
  -> ShellLaunch boundary
  -> LaunchResult
  -> LaunchSuccessGateDecision
      -> accepted success: UsageMutation -> StateStore CAS
      -> failure/unknown: diagnostic only
~~~

V2-TBD-011/ACT-TBD-02 仍开放。AX-BP 只冻结“统计必须等待统一成功门”，不选择 Shell 接受、进程创建或协议调度中的任何一个。

## 15. 活跃度与布局拓扑

~~~text
Accepted ItemSnapshot
  -> Eligibility
  -> ActivityFeatures
  -> ActivityScore
  -> StableSort
  -> ManualControlOverlay
      auto
      anchored
      full-manual
  -> LogicalLayout
  -> LayoutProjection
  -> PosterProjection
~~~

约束：

- 动态 Item 数是运行数据，不用 Rep<N>；
- 评分必须确定、可解释、可撤销；
- 锁定冻结外部几何和人工重排，不停止内部自动排名；
- V2-TBD-002/003 的公式和阈值继续开放；
- 全手动退出前必须取得用户选择。

## 16. 主题拓扑

~~~text
ThemeSource
  built-in / imported / AI draft
  -> PackageRead boundary
  -> SafetyValidation
  -> SchemaValidation
  -> CapabilityValidation
  -> MigrationPlan
  -> ThemeDraft
  -> PreviewProjection
  -> user apply
  -> ThemeMutation -> StateStore CAS
  -> ThemeProjection -> Renderer boundary
~~~

错误路径：

~~~text
invalid theme
  -> retain selected theme identity
  -> render minimal-fallback as temporary error projection
  -> diagnostic
  - no write-back over user selection
~~~

任意 native code 和不受限脚本没有进入拓扑的合法端口。

## 17. AI 拓扑

~~~text
User AI request
  -> PermissionScope
  -> ContextBuilder(minimal/redacted)
  -> ProviderRequest boundary
  -> ProviderResult
  -> ChangePlanParser
  -> Schema/Capability/Risk validation
  -> Preview
  -> confirmation policy
  -> normal CommandEnvelope
  -> StateStore CAS
~~~

AI：

- 不是第二命令路径；
- 不直接访问 StateStore；
- 不直接启动程序、移动文件或安装主题；
- 无 Provider 时不影响核心功能；
- 凭据只存在于受保护 provider 边界；
- R0/R1/R2 免确认范围继续作为 V2-TBD-006。

## 18. DesktopHost 与 Renderer 拓扑

~~~text
ApplicationCore
  -> HostProjectionIntent -> DesktopHost Slot
  -> VisualSceneIntent -> Renderer Slot

Renderer Slot
  -> FrameObservation(revision, surface_epoch)
  -> RuntimeIngress

DesktopHost Slot
  -> HostObservation(surface_epoch, state, geometry, hit)
  -> RuntimeIngress

PresentabilityDecision
  requires matching:
    accepted revision
    geometry revision
    hit revision
    frame revision
    live surface epoch
~~~

职责分离：

| DesktopHost | Renderer |
| --- | --- |
| parent/owner/z-order | scene drawing |
| desktop role | GPU/CPU visual resources |
| physical geometry | frame production |
| hit regions | device/surface visual feedback |
| attach/recover/detach | candidate rendering capabilities |
| surface epoch | frame revision |

Renderer 不得自行 TopMost、重挂、恢复或改变命中。

## 19. 生命周期与恢复拓扑

~~~text
SystemEvent
  ShellLost / ShellRestored / DisplayChanged / DpiChanged
  RendererLost / Suspend / Resume / ShowDesktop / SessionEnding
  -> RuntimeIngress
  -> LifecycleDecision
  -> fence old epoch
  -> retain accepted snapshot
  -> recompute projection
  -> request bounded recovery
  -> publish only matching new epoch/revision
~~~

优先级：

~~~text
BeginExit
  > Detach
  > ShellLost / RendererLost
  > Reconnect
  > Normal projection
~~~

Win+D 不是“隐藏后恢复”事件；正确 DesktopHost 必须在桌面层结构内连续存在。

## 20. 退出拓扑

~~~text
ExitCommand
  -> BeginExit(sticky)
  -> reject new mutations
  -> cancel external jobs by boundary contract
  -> fence host/renderer/provider callbacks
  -> request StateStore flush
  -> reconcile critical ledgers
  -> close ControlCenter
  -> detach/destroy Poster resources
  -> remove Tray
  -> verify owned-resource ledger empty
  -> process exit
~~~

迟到事件只进入 Diagnostics，不能复活资源。

## 21. Distribution 拓扑

~~~text
Install / Upgrade / Uninstall
  -> Distribution boundary
  -> validate package and paths
  -> preserve/migrate StateStore and ledgers
  -> explicit autostart opt-in state
  -> executable/resource/license contract
  -> cleanup owned resources only
  -> retain or remove user data according to explicit choice
~~~

具体安装器技术不在 AX-BP 中选择。

## 22. Diagnostics 拓扑

~~~text
CoreDecision
AdapterObservation
SurfaceHealth
StateStoreResult
SecurityDecision
DistributionResult
  -> DiagnosticNormalizer
  -> Redaction
  -> DiagnosticProjection
  -> bounded external diagnostic boundary
~~~

诊断必须携带适用的 request ID、revision、surface epoch、job epoch、module、phase 和 stable error code。

## 23. 外部世界边界

| 边界 | 请求方向 | 返回方向 | 不能绕过的规则 |
| --- | --- | --- | --- |
| StateStore | commit/load intent | authoritative result | 只有成功结果可形成 accepted snapshot |
| DesktopHost | attach/projection/recover/detach intent | host observation | 不得 TopMost fallback |
| Renderer | scene/resource intent | frame/ready/fault observation | 不拥有 z-order |
| ShellLaunch | launch intent | launch result | UI 不自行统计成功 |
| FileRead/Move | discovery/safety intent | candidate/move result | ledger、备份、确认 |
| ThemePackage | read/install/uninstall intent | validated result | 无任意代码 |
| AI Provider | redacted request | untrusted response | ChangePlan 验证与确认 |
| Tray/System | menu/lifecycle intent | OS event | 归一化后进入 Core |
| Distribution | install/update/remove intent | structured result | 只清理自有资源 |
| Diagnostics Sink | redacted batch intent | acceptance/fault | 不成为状态权威 |

## 24. 七份冻结需求拓扑覆盖

| 权威文档 | 主要拓扑落点 | 覆盖状态 |
| --- | --- | --- |
| 完整需求画像 | SYS-01～20、启动/权威状态/Windows/Distribution | COVERED |
| 海报视觉与交互 | Poster、Control edit、ActivityLayout、DesktopHost/Renderer | COVERED |
| 运行表面与托盘 | Poster/Control/Tray、单实例、状态同步、退出 | COVERED |
| Item 活跃度与排版 | discovery、add、launch、stats、activity、layout、manual control | COVERED |
| 主题平台与 AI | Theme、package safety、fallback、AI ChangePlan | COVERED |
| 风险与待定项 | presentation risk、authority risk、12 TBD、4 deferred | COVERED / OPEN RETAINED |
| 迁移追踪 | 四大设计主题和不继承旧实现项 | COVERED |

一致性审计作为证据输入，不构成第八需求模块。

## 25. 不变量在拓扑中的位置

| 不变量 | 拓扑保护点 |
| --- | --- |
| RP-INV-01 真实桌面优先 | DesktopHost hit boundary + limited hit islands |
| RP-INV-02 层级稳定 | DesktopHost only z-order owner |
| RP-INV-03 用户文件安全 | FileSafety + ledger + confirmation |
| RP-INV-04 失败保留 | StateStore/File/Theme failure branches |
| RP-INV-05 单一状态权威 | Store CAS closed loop |
| RP-INV-06 控制中心非主体 | on-demand ControlCenter branch |
| RP-INV-07 锁定保护边界 | geometry/manual control guards |
| RP-INV-08 renderer 可替换 | distinct Renderer Slot |
| RP-INV-09 技术选型后置 | typed logical boundaries only |
| RP-INV-10 无 AI 可用 | optional AI branch |
| RP-INV-11 呈现事实可验证 | PresentabilityDecision + Diagnostics |
| RP-INV-12 个人自用边界 | Distribution and scope constraints |

## 26. 保留的开放与延期节点

| ID | 拓扑占位 | 当前处理 |
| --- | --- | --- |
| V2-TBD-001 | Host/Renderer/process module boundaries | 交给技术选型 |
| V2-TBD-002 | ActivityScore policy slot | 只冻结性质 |
| V2-TBD-003 | LayoutPolicy slot | 只冻结能力 |
| V2-TBD-004 | Theme visual token set | 角色冻结、视觉开放 |
| V2-TBD-005 | Theme extension policy | 安全底线冻结 |
| V2-TBD-006 | AI confirmation policy | 高风险确认冻结 |
| V2-TBD-007 | Editing double-click policy | USING 已冻结 |
| V2-TBD-008 | Branding projection | 不影响拓扑 |
| V2-TBD-009 | Accessibility release gate | 接口保留 |
| V2-TBD-010 | multi-poster/multi-display activation | 数据/端口不封死，认证延期 |
| V2-TBD-011 | LaunchSuccessGate | 必须在 Item 实现前冻结 |
| V2-TBD-012 | Theme package representation | 端口保留、格式开放 |

| ID | 拓扑占位 | 当前处理 |
| --- | --- | --- |
| V2-DEFER-001 | PosterCollection capability | 首发只激活单海报；身份和集合边界不封死 |
| V2-DEFER-002 | DisplayTopology capability | 接收显示变化；完整多屏认证延期 |
| V2-DEFER-003 | ExternalThemeServices capability | 在线市场、云同步和共享无外部连接，保持 inactive |
| V2-DEFER-004 | FileOrganization automation capability | 只允许用户主动且确认的安全收纳；未经确认的自动整理无合法激活边 |

延期功能不会从拓扑中删除，但必须标记为 inactive capability，且不能以“预留”为理由产生后台效果。

## 27. 禁止旁路

不存在以下合法边：

- Surface → StateStore direct write；
- Renderer → DesktopHost z-order mutation；
- DesktopHost → business snapshot mutation；
- AI Provider → Command execution；
- Theme package → file/network/process/native IPC；
- Tray menu → HWND direct manipulation；
- ControlCenter draft → accepted snapshot overwrite；
- Discovery result → automatic file move；
- Launch request → usage stats before success gate；
- Diagnostics → state repair；
- Installer → delete unknown user files；
- axiom-runtime → DesktopUp production execution。

## 28. AX-BP-02 退出门

| 检查 | 结果 |
| --- | --- |
| 顶层系统上下文完整 | PASS |
| 20 个逻辑角色有唯一位置 | PASS |
| 启动、运行、编辑、Item、Theme、AI、恢复、退出完整 | PASS |
| 七份冻结需求有拓扑落点 | PASS |
| 12 TBD 与 4 deferred 保留 | PASS |
| 单一状态权威闭环明确 | PASS |
| Host/Renderer 分离 | PASS |
| 定义与激活分离 | PASS |
| 禁止旁路明确 | PASS |
| 未选择 runtime 或候选技术 | PASS |
| axiom-runtime 排除 | PASS |

## 29. 交接 AX-BP-03

AX-BP-03 以 SYS-01～20 和本文件关系为输入，冻结每个模块的职责、输入、输出、状态、错误及禁止访问。它不能改变本文件的拓扑或重新引入旁路。
