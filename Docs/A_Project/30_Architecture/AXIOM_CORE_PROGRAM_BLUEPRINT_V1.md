# DesktopUp V2 Axiom Core 程序蓝图 V1

状态：FROZEN — AX-BP-01～06 COMPLETE / PHYSICAL TECHNOLOGIES UNSELECTED

建立日期：2026-08-24

## 1. 文档职责

本文把 DesktopUp V2 已冻结需求、DesktopHost E2–E5 合同与 Axiom 0.3 的编译期核心组合为一份显式程序蓝图。它回答：

1. DesktopUp 作为一个完整程序由哪些稳定边界组成；
2. 哪些关系属于编译期定义，哪些对象只在运行期激活；
3. 单一状态权威、命令、效果、投影和适配器如何流动；
4. Axiom Core 能负责什么，不能负责什么；
5. 在尚未选出海报 renderer、控制中心 UI、存储和安装技术时，哪些架构边界可以冻结；
6. 后续技术选型和完整架构如何接收这些边界而不改写业务语义。

本文是用户于 2026-08-24 明确要求“基于 Axiom Core 继续开发并显式定义程序蓝图”后的架构研究输入。Axiom 仅位于架构方法与技术研究层；本项目只使用 Axiom Core，不采用、不推荐、不依赖处于 unstable 状态的 axiom-runtime。

配套的可编译类型夹具位于：

- prototype/axiom-desktopup-blueprint/

## 2. 权威输入与冲突顺序

发生冲突时按以下顺序裁决：

1. 用户最新明确决策；
2. Docs/A_Project/10_Requirements/ 下的冻结需求；
3. DesktopHost E2 数据合同、E3 生命周期、E4 原子投影和 E5 诊断合同；
4. 本程序蓝图；
5. Axiom 的正式规范与本地已验证实现；
6. adapter、UI、renderer、存储和安装实现。

Axiom 不能借“类型正确”修改 DesktopUp 的产品语义；具体框架也不能借平台限制修改本蓝图中的状态权威、命中、层级和恢复边界。

## 3. Axiom 输入基线

本轮读取并核对的 Axiom 输入：

| 字段 | 值 |
| --- | --- |
| crate | axiom 0.3.0 |
| local source | axiom/ |
| upstream | https://github.com/ghwinter/axiom.git |
| exact commit | 890cc75680e150d98cbbe4133976dfcf35e8e871 |
| core toolchain floor | Rust 1.85 / edition 2024 |
| license | MIT |
| core verification | 21 unit + 5 closed-boundary + 6 topology tests PASS |
| runtime scope | axiom-runtime 明确排除：unstable，不作为依赖、证据或候选 |

本地 axiom/ 当前是独立 Git checkout。它如何进入 DesktopUp 的长期依赖图（固定 git revision、submodule、vendor 或已发布 crate）仍需单独冻结；在此之前不得把嵌套仓库静默当作普通源目录提交。

## 4. 对 Axiom Core 的准确理解

### 4.1 五个构造概念

DesktopUp 采用以下 Axiom 构造概念：

1. PortCell：有类型输入、输出、独占状态和一次原子全转移的端口体；
2. 对偶组合：上游输出类型必须与下游输入类型匹配；
3. 组合封闭：组合后的子图仍是 PortCell；
4. 代换绑定：固定接口的型位由编译期实现或运行期实现填充；
5. 激活：运行时向已经定义的图输入事件，使状态和效果发生。

### 4.2 定义平面与实例平面

定义平面包含：

- 稳定消息类型；
- Cell 输入、输出与状态边界；
- 允许的组合；
- adapter 型位；
- 编译期可检查的接线；
- 静态纯算法子图。

实例平面包含：

- 当前 Poster、Item、ThemeInstance 和 revision；
- HWND、Shell generation、surface、frame、DPI 和物理矩形；
- 队列、线程、文件、数据库连接、网络请求和取消令牌；
- 当前 Item 数量、显示器数量和实际连接数量；
- 当前 adapter 实例以及其故障状态。

Item 数量变化、状态修改、海报显隐和主题参数变化属于静态结构下的运行实例活动，不需要把程序拓扑改成运行时 JSON 图。

### 4.3 定义不等于激活

Blueprint 只证明一张类型图被定义。真正的窗口创建、数据库打开、帧呈现、网络请求和文件操作只能在受控 adapter 被激活后发生。

因此：

- 定义 DesktopHostSlot 不会创建 HWND；
- 定义 RendererSlot 不会创建 surface；
- 定义 AiProviderSlot 不会联网；
- 定义 StateStoreSlot 不会创建默认业务数据；
- 未配置 AI 时对应型位可以永不激活，产品其余部分仍完整工作。

### 4.4 物理执行边界不属于 Axiom Core

Axiom Core 只帮助描述“某条调用或数据关系在此跨出纯定义平面”。线程、队列、背压、取消、合并、调度、IPC、共享表面和进程隔离都属于后续物理实现技术，不由 Axiom Core 提供。

本项目明确排除 axiom-runtime：

- 不加入 DesktopUp workspace 依赖；
- 不把其示例、测试或 API 作为生产证据；
- 不从其 Carrier 或调度实现推导 DesktopUp 的线程模型；
- 不在 AX-BP 中实现队列、线程、异步 runtime 或 IPC；
- 只在蓝图中冻结“这里需要何种物理执行语义”，供后续技术选型和实现架构使用。

因此，蓝图中的 PhysicalExecutionBoundary 是说明性边界，不是 Axiom runtime 类型，也不是待实现的 Axiom 组件。

## 5. Axiom 能力适配结论

| DesktopUp 范围 | Axiom 适配度 | 采用方式 |
| --- | --- | --- |
| 命令校验、权限和纯 reducer | 高 | PortCell + Chain |
| revision/CAS 前置判断 | 高 | 纯 Cell；实际事务由 StateStore adapter |
| Snapshot 到各表面投影 | 高 | Broadcast/Diamond 或等价封闭组合 |
| Item 排名、缩放与布局 | 高 | 纯、确定、可注入时钟的静态子图 |
| 生命周期状态机与效果计划 | 高 | 纯 reducer；OS 操作外置 |
| adapter 接口 | 高 | Slot + Conforms；运行时存在绑定 |
| 外部 I/O、Win32、Shell、数据库 | 部分 | 只定义 typed seam；由后续获选的物理 adapter 实现 |
| Renderer 帧传输 | 部分 | 控制消息类型化；像素/visual 不经过通用消息盒 |
| 主题包与用户素材 | 部分 | 数据经校验后进入 ThemeCompiler；不是任意代码 Cell |
| AI Provider | 部分 | 请求/结果接口固定；网络在受控 adapter |
| 动态插件和任意脚本 | 不采用 | 首发禁止通用原生插件和无限权限脚本 |

## 6. 诚实边界

1. PortCell trait 不能从类型系统证明业务函数纯、无阻塞或无副作用，必须由代码边界、审查和测试保证；
2. Conforms 只能证明 Rust 类型可连接，不能证明 DesktopHost 层级、Win+D、透明、命中或清理语义正确；
3. Blueprint 和 Static 证明定义为零大小类型，不证明完整程序零内存、零线程或零运行成本；
4. Axiom Core 只提供系统描述方法，不提供 DesktopUp 的生产 runtime；
5. axiom-runtime 为 unstable 且明确排除，不属于推荐方案、依赖候选或验证证据；
6. 外部事件、线程、队列、取消、超时、优先级和资源清理由后续物理架构与技术选型负责；
7. Renderer、控制中心、StateStore、主题表达、进程、IPC 和安装技术仍没有赢家；
8. Axiom 类型蓝图不能替代 DesktopHost E5、候选 P0 和人工 Release 验证。

## 7. 顶层程序蓝图

~~~text
                       DesktopUp V2 定义平面

  Poster / Tray / Control Center / OS / Jobs / AI adapter events
                              │
                              ▼
                    RuntimeIngress Cell
                  统一身份、来源与事件类型
                              │
                              ▼
               ApplicationReducer PortCell
       ┌─────────────────────────────────────────┐
       │ accepted snapshot cache                 │
       │ lifecycle/runtime state                 │
       │ pending request / cancellation fences   │
       │ pure permission and revision decisions  │
       └─────────────────────────────────────────┘
                 │                         │
                 │ EffectIntent[]          │ ProjectionSource?
                 ▼                         ▼
          Effect Dispatcher         Snapshot Projection Graph
                 │                  ┌────────┼─────────┐
                 │                  ▼        ▼         ▼
                 │               Poster   Control    Tray/Diag
                 │               model    Center
                 ▼
  ┌──────── Adapter typed seams / physical execution boundaries ────┐
  │ StateStore │ DesktopHost │ Renderer │ Shell │ Files │ AI │ Tray │
  └──────────────────────────────┬───────────────────────────────────┘
                                 │ result/event feedback
                                 └──────────────► RuntimeIngress
~~~

蓝图的运行形态是一个受控反馈系统：

- Core 只产生 EffectIntent，不直接做副作用；
- adapter 完成或拒绝副作用后，把结构化结果作为新 RuntimeInput 返回；
- 只有 StateStore 成功返回的新 accepted snapshot 才能成为业务状态；
- 投影由 accepted snapshot 和当前运行技术状态共同派生；
- UI 草稿、宿主投影和 renderer frame 都不能反向成为第二份业务权威。

## 8. 顶层 Cell

### 8.1 RuntimeIngress

输入：RuntimeInput。

输出：CoreEvent。

职责：

- 把 Poster、Tray、Control Center、系统、存储、DesktopHost、Renderer、Job 和 AI 事件归一化；
- 保留 source、request ID、revision、epoch 和取消身份；
- 拒绝未知或身份不完整的事件；
- 不读数据库，不改窗口，不调用网络。

### 8.2 ApplicationReducer

输入：CoreEvent。

输出：CoreTransition。

状态：CoreState。

职责：

- 持有最近已接受 snapshot 的运行缓存；
- 持有生命周期、宿主、renderer 和 pending request 的技术状态；
- 校验 expected revision、权限、锁定、退出和当前生命周期门；
- 产生 MutationPlan、EffectIntent、拒绝或 no-change；
- 接受 StateStore 的 commit result 后才更新 accepted snapshot；
- 对旧 revision、旧 epoch 和迟到结果作稳定裁决。

CoreState 默认值必须是 BOOTSTRAPPING + no accepted snapshot，绝不能把空白 Poster 当作“默认业务状态”。只有 StateStore 明确返回 NoData 且迁移完成后，才允许由专门命令创建初始状态。

### 8.3 EmissionProjector

输入：CoreTransition。

输出：ProgramEmission。

职责：

- 保留 effect intents；
- 当 transition 携带 ProjectionSource 时，驱动静态 SnapshotProjectionGraph；
- 保证 Poster、Control Center、Tray 和诊断投影共享同一个 source revision；
- 不保存第二份状态。

### 8.4 DesktopUpProgramGraph

类型形状：

~~~text
Chain<
  RuntimeIngress,
  Chain<
    ApplicationReducer,
    EmissionProjector
  >
>
~~~

该类型是零大小定义；运行时内存只来自组合 State，主要是 ApplicationReducer 的 CoreState。

## 9. Snapshot 投影图

投影使用一份 ProjectionSource：

~~~text
ProjectionSource
  ├─ AcceptedSnapshot
  ├─ HostRuntimeObservation
  ├─ RendererRuntimeObservation
  ├─ visibility / pause / selection
  └─ diagnostic summary
~~~

静态投影图：

~~~text
SnapshotIdentity
      ├────────────► PosterProjector ───────────┐
      └────────────► AuxiliaryProjector ───────┤
                                                  ▼
                                         ProjectionBundle
~~~

其中 AuxiliaryProjector 产生 ControlCenterProjection、TrayProjection 和 DiagnosticProjection。后续可以继续按组合封闭拆成更多分支，但所有分支必须携带同一 source snapshot revision。

### 9.1 PosterProjection

至少包含：

- source snapshot revision；
- Poster 持久字段；
- Item/Layout/Theme 的派生快照；
- HostProjection 意图；
- HitRegionSet；
- RenderScene/RenderRevision 期望；
- presentation、interaction、runtime、visibility、availability 和 lock；
- 可操作 degraded/error 状态。

### 9.2 ControlCenterProjection

至少包含：

- 当前 accepted revision；
- 可编辑 snapshot；
- draft 与已保存值的明确区别；
- 当前权限和冲突状态；
- 预览坐标变换输入；
- adapter/host/renderer 诊断；
- 不含 HWND、API Key 和第二份持久业务状态。

### 9.3 TrayProjection

至少包含：

- poster shown/hidden；
- locked；
- animation paused；
- control center visible；
- autostart observed state；
- ready/degraded/exiting；
- AI configured boolean；
- 当前快速主题身份。

### 9.4 DiagnosticProjection

至少包含脱敏后的：

- source/host/hit/render/display revisions；
- lifecycle/surface epoch；
- host/surface/resource 状态；
- 最近命令结果和稳定错误码；
- 资源、恢复、取消和超时摘要。

## 10. 单一状态权威闭环

唯一允许的业务写入闭环：

~~~text
CommandEnvelope(expected_revision)
  -> ApplicationReducer
  -> MutationPlan
  -> StateStoreDispatch
  -> transactional validate + CAS + persist
  -> StoreCommitResult(accepted snapshot or structured failure)
  -> ApplicationReducer
  -> ProjectionBundle
~~~

规则：

1. reducer 在 StoreCommitResult 成功前不乐观替换 accepted snapshot；
2. Store 是持久业务权威，CoreState 只是最近 accepted snapshot 的运行缓存；
3. Control Center draft、主题 preview 和拖动 live geometry 是临时状态；
4. live geometry 可产生 HostProjectionRevision，但 EndGesture 只提交一次业务 CAS；
5. commit 失败保留 accepted snapshot 和用户未提交意图；
6. 旧 snapshot、旧 store result、旧 host frame 都不得覆盖新 revision；
7. HWND、DPI、physical bounds、hit regions、paused reason 和 performance sample 不持久化为业务字段。

## 11. 命令与效果模型

### 11.1 CommandEnvelope

每条命令必须包含：

- request ID；
- command source；
- target identity；
- expected source revision；
- command payload；
- risk/confirmation evidence（适用时）；
- cancellation/deadline identity（长任务适用时）。

### 11.2 CoreTransition

结果只能是：

- AcceptedPendingEffect：已接受为副作用计划，尚未成为业务状态；
- Applied：StateStore 已接受并返回新 snapshot；
- NoChange；
- Rejected：权限、锁定、revision、校验、宿主未就绪或 exiting；
- Failed：adapter/I/O/timeout 等结构化失败。

### 11.3 EffectIntent

EffectIntent 是“请求物理世界执行某事”的值，不是已经发生的事实。至少分为：

- StoreEffect；
- DesktopHostEffect；
- RendererEffect；
- ControlCenterEffect；
- TrayEffect；
- ShellLaunchEffect；
- Discovery/ShortcutEffect；
- ThemePackageEffect；
- CredentialEffect；
- AiProviderEffect；
- DiagnosticEffect；
- ExitEffect。

每个 effect 只能被对应 adapter 消费。通用 Shell、任意文件系统、任意网络和任意原生 IPC 不作为公开 effect。

## 12. Adapter 型位

每个外部边界拆成“出站 dispatch”和“入站 event”两侧，避免把异步操作伪装成同步函数返回。

| 型位 | 出站请求 | 入站事件 | 状态 |
| --- | --- | --- | --- |
| StateStore | StoreRequest | StoreEvent | 技术未选 |
| DesktopHost | DesktopHostRequest | DesktopHostEvent | E2–E5 合同冻结，adapter 未选 |
| Renderer | RenderRequest/control | RenderEvent/frame observation | TS-C01–C07 未选 |
| ControlCenter | projection/lifecycle request | user command/window event | UI 未选 |
| Tray | TrayProjection/menu command | tray action/system rebuild | 技术未选 |
| ShellLauncher | LaunchRequest | LaunchResult | 成功统计门 TBD |
| ShortcutInspector | inspect/discover request | parsed candidate/result | 技术未选 |
| SafeCollection | ledgered transaction request | progress/result/recovery | 技术未选 |
| ThemePackage | validate/install/switch request | result/progress | 格式未选 |
| CredentialStore | credential command | configured/result | backend 未选 |
| AiProvider | minimized request | provider result | provider transport 未选 |
| Clock | time query/snapshot | monotonic/wall clock sample | 可测试注入 |
| Diagnostics | event batch | sink acknowledgement | 格式与落盘策略未选 |

对每个型位，Rust 类型接线只是进入门。运行实现仍要通过语义、错误、资源、性能和安全验证。

## 13. DesktopHost 与 Renderer 关系

DesktopHost 和 Renderer 是两个独立型位：

~~~text
Application Core
  -> HostProjectionIntent -> DesktopHost
  -> RenderSceneIntent    -> Renderer

Renderer
  -> Ready / FramePresented / Fault
  -> DesktopHost coordinator
  -> Host observation
  -> Application Core
~~~

冻结规则：

- DesktopHost 拥有 parent/owner/z-order、桌面角色、命中岛和 lifecycle fencing；
- Renderer 拥有候选特定绘制资源；
- surface transport 通过 E2 offer/plan 协商；
- geometry、hit、render expectation 由一个 HostProjectionRevision 原子关联；
- 像素帧或 composition visual 不经过通用 Box<dyn> 事件总线；
- 控制消息需要“有界、可靠、可取消”的物理执行语义；具体机制由后续选型决定；
- frame 通知必须带完整 session/attachment/epoch/revision/bounds；
- adapter 不能绕过 DesktopHost 自行 TopMost、重挂或恢复。

## 14. Item、活跃度与布局子图

正式 Item 子图是纯计算，适合静态定义：

~~~text
ItemSnapshot + ClockSnapshot + LayoutConfig
  -> ItemValidityProjection
  -> ActivityFeatureBuilder
  -> RankScoreCalculator
  -> StableTieBreak
  -> ScaleQuantizer
  -> PinnedManualResolver
  -> LayoutEngine
  -> LayoutSnapshot
~~~

不变量：

- 0/1/N Item 是实例数量变化，不使用 Rep<N> 写死；
- 同输入、同时间快照和同配置必须得到同输出；
- 时间由输入注入，Cell 内不读取系统时钟；
- 排名计算不写回真实启动次数；
- 30 天保护公式在 V2-TBD-002 关闭前只允许夹具/候选实现，不进入权威生产 Cell；
- 全手动、锚定和自动模式由明确状态决定；
- 200 Item 计算不得进入 UI 线程阻塞接缝；
- 输出只含逻辑布局，不含 HWND 或物理显示器像素。

## 15. Item 启动与统计子图

~~~text
Poster ItemAction
  -> InteractionPolicy
  -> LaunchIntent
  -> ShellLauncher adapter
  -> LaunchResult
  -> LaunchSuccessGate
  -> StateStore mutation
  -> accepted snapshot
~~~

在 V2-TBD-011 关闭前，LaunchSuccessGate 必须保持显式型位/策略，UI 和 Shell adapter 不得自行增加 total_launches。

## 16. 发现、仅添加与安全收纳子图

~~~text
UserInitiatedSourceSelection
  -> Discovery adapter
  -> CandidateParser
  -> IdentityAndDuplicateAnalysis
  -> PreviewProjection
  -> UserConfirmation
  -> AddOnlyPlan | ManagedCollectionPlan
  -> ledgered physical adapter
  -> StateStore commit
~~~

安全收纳是事务型物理流程，不放进纯 PortCell 内执行文件移动。纯 Cell 只构造和校验计划；adapter 必须执行：

~~~text
PENDING ledger
-> verified temporary copy
-> atomic vault copy
-> source move
-> MANAGED commit
~~~

失败结果回流 Core，保留 ledger、vault 和上一 accepted snapshot。

## 17. 主题子图

~~~text
TemplatePackage + ThemeInstance + PosterSnapshot
  -> PackageAndSchemaValidation
  -> DefaultsPresetOverrideMerge
  -> ProtectedStructureValidation
  -> ThemeCompilation
  -> RenderScene
~~~

边界：

- 模板和用户主题主要是受限数据，不是任意 PortCell 插件；
- 内置 renderer/theme compiler 可以是编译期实现；
- 第三方主题包不得借任何扩展钩子获得通用 Rust/原生代码执行；
- 参数 preview 与 saved override 分开；
- 热切换在临时资源中验证，成功后原子发布；
- fallback 是运行投影，不写回覆盖用户模板选择；
- 主题不能拥有命中、桌面层级、状态仓库或程序启动权限。

## 18. AI 子图

~~~text
UserPrompt
  -> MinimalContextBuilder
  -> ConfirmationPolicy
  -> AiProvider adapter
  -> ToolCallDecoder
  -> ToolContractValidator
  -> ChangePlanBuilder
  -> Preview
  -> User/Policy Confirmation
  -> normal CommandEnvelope
~~~

AI 不是第二条写入路径。所有修改最终回到普通 CommandEnvelope、revision/CAS、预览、确认和撤销链。

无 Key 时：

- AiProvider 型位不激活；
- 不激活网络连接或任何物理执行机制；
- 不读取凭据；
- 其余蓝图继续激活。

## 19. 生命周期子图

生命周期 reducer 接收：

- process start intent；
- singleton routing；
- store open/migrate/recover result；
- shell generation；
- DesktopHost lifecycle event；
- renderer/device event；
- tray rebuild；
- display/session/power event；
- control center lifecycle；
- detach/exit；
- timeout/cancellation。

输出有序 EffectIntent。优先级沿 E3：

~~~text
BeginExit
> Detach
> Shell loss cleanup
> desktop recovery
> surface recovery
> attach / projection commit
~~~

同一逻辑 CoordinatorQueue 串行裁决状态；回调只入队，不直接修改 host/core 状态。

## 20. 启动蓝图

~~~text
ParseStartIntent
-> AcquireSingleInstance
-> OpenStateStore
-> ValidateSchema / Migrate / Recover
-> AcceptInitialSnapshot
-> CreateTray
-> QueryHostCapabilities
-> DiscoverTopology
-> NegotiateSurface
-> Attach hidden/input-disabled
-> Renderer ready
-> Matching first frame
-> Publish poster + matching hit regions
-> Runtime ready
~~~

约束：

- 普通启动和 --autostart 都不创建控制中心；
- StateStore 失败不能以空默认覆盖；
- 找不到安全桌面宿主时进入 degraded，不创建普通顶层 fallback；
- first frame 前不可暴露白底、旧 frame 或输入；
- 相同启动/恢复请求幂等。

## 21. 控制中心蓝图

~~~text
OpenControlCenter effect
-> on-demand adapter create/show
-> request current accepted snapshot
-> render scaled projection
-> local draft interaction
-> CommandEnvelope(expected_revision)
-> store/core result
-> accepted snapshot or structured conflict
~~~

关闭控制中心：

- 释放预览 renderer、动画和页面资源；
- 不隐藏海报；
- 不销毁托盘或 Core；
- 不改变 DesktopHost z-order；
- 不保留能覆盖新 snapshot 的长期乐观副本。

## 22. 退出蓝图

~~~text
BeginExit(sticky)
-> reject new commands
-> cancel AI/import/scan/theme jobs
-> fence host and renderer callbacks
-> flush state and ledgers
-> close control center
-> detach/destroy poster surfaces
-> remove tray
-> verify zero owned resources
-> process exit
~~~

迟到的 Shell、display、frame、store 或 provider 事件只形成诊断，不得复活资源。

## 23. 物理执行边界声明

本节只冻结边界语义，不选择或实现 runtime、线程、队列、IPC、调度器或 Carrier。

| 边界 ID | 跨越关系 | 必须保留的物理语义 | AX-BP 是否实现 |
| --- | --- | --- | --- |
| PEB-01 | UI/Tray/系统事件 → Coordinator | 有序、可追踪、不得静默丢失命令和生命周期终态 | 否，只冻结要求 |
| PEB-02 | Coordinator ↔ StateStore | commit/result 成对、CAS、超时可诊断、结果回到唯一协调序列 | 否，只冻结要求 |
| PEB-03 | Coordinator ↔ DesktopHost | surface epoch、幂等 attach/recover/detach、迟到事件隔离 | 否，只冻结要求 |
| PEB-04 | Coordinator ↔ Renderer | frame revision、ready/fault/release 终态、像素不走通用消息盒 | 否，只冻结要求 |
| PEB-05 | Gesture → live projection | Update 可合并，End/Cancel 可靠且顺序不可破坏 | 否，只冻结要求 |
| PEB-06 | Item scan/import/theme/AI jobs | 可取消、带 job epoch、结果不能越过新状态 | 否，只冻结要求 |
| PEB-07 | Diagnostics | 有界、脱敏、错误和清理结论不可丢 | 否，只冻结要求 |
| PEB-08 | Control Center process/thread boundary | 关闭不影响 Poster/Core；草稿不成为第二权威 | 否，只冻结要求 |

“有界”“可取消”“合并”“可靠”等词是后续技术候选必须满足的合同，不指向 axiom-runtime，也不预设具体实现。

## 24. 模块边界

建议的逻辑模块不是对最终 crate、DLL 或进程数量的预判，而是必须保持的职责边界：

| 逻辑模块 | 负责 | 明确不负责 |
| --- | --- | --- |
| desktopup-contracts | ID、revision、命令、事件、投影、错误和 adapter 接口 | OS 调用、数据库、绘制 |
| desktopup-core | Axiom Cell、状态转移、验证、效果意图和投影 | 阻塞 I/O、线程、HWND、GPU |
| desktopup-layout | Item 活跃度、排序、网格与稳定性算法 | 扫描文件、启动程序 |
| desktopup-theme-model | ThemeSpec、token、能力声明和验证 | 任意脚本执行、直接 GPU API |
| desktopup-coordinator | 程序激活顺序、revision/epoch 与物理执行边界要求 | 线程、队列、runtime 和候选特定绘制 |
| desktopup-state-store | schema、迁移、原子写、CAS、恢复和账本 | UI 草稿和乐观权威 |
| desktopup-desktop-host | E2–E5 DesktopHost 合同的 adapter | 海报视觉语义、业务状态 |
| desktopup-renderer-* | 候选特定 surface、frame 和资源 | z-order、WorkerW 生命周期 |
| desktopup-control-center-* | 候选特定管理 UI 和预览 | 成为第二状态权威 |
| desktopup-shell | 托盘、单实例、启动意图、程序启动和系统通知 | 业务规则旁路 |
| desktopup-theme-runtime | 受限主题编译、资源预算和降级 | 非白名单原生代码 |
| desktopup-ai-provider-* | provider 调用、授权、审计和 ChangePlan 生成 | 直接修改 StateStore |
| desktopup-diagnostics | 结构化事件、脱敏、证据导出和清理审计 | 偷偷修正状态 |
| desktopup-app | 组合根、依赖注入和正式入口 | 跨越接口直接访问内部实现 |

禁止形成一个同时拥有业务状态、HWND、renderer、数据库连接和托盘回调的全局 AppState。技术对象必须由 adapter 拥有；Core 只保存可序列化业务状态和必要的技术观察摘要。

## 25. 进程与线程边界

Axiom Core 不决定进程拓扑。正式选型前只冻结以下不变量：

1. Core 转移必须是单写者、确定且严格有序；
2. StateStore commit 结果必须回到同一协调序列后才成为 accepted snapshot；
3. DesktopHost 与 Renderer 回调必须携带 surface epoch 和相关 revision；
4. 控制中心可按需创建和释放，不能拥有海报运行寿命；
5. AI、扫描、导入和主题编译不能阻塞协调序列；
6. Explorer、显示和 DPI 回调可以并发到达，但必须先归一化为 RuntimeInput；
7. 退出进入 sticky exiting 后，任何迟到回调都不能重新激活资源；
8. 线程或进程崩溃不能导致未经提交的 UI 草稿成为正式状态。

候选拓扑仍包括单进程多线程、核心/宿主与控制中心分进程等方案。最终选择必须比较启动时间、内存、IPC 复杂度、故障隔离、GPU 资源共享、安装体积和可诊断性，不能由 Axiom 的类型图倒推出答案。

## 26. 定义、装载与激活

三者必须严格分开：

~~~text
Definition
  Rust types + Axiom graph + Slot contracts
  zero HWND / zero thread / zero store / zero frame

Installation
  validated adapters occupy fixed slots
  resources may be prepared but are not yet published

Activation
  startup intent enters coordinator
  adapters perform bounded effects
  accepted snapshot drives synchronized projections
~~~

对任一 adapter，装载条件至少包括：

- 接口版本匹配；
- capability manifest 可读取；
- 所需线程模型可满足；
- 取消、deadline、错误和释放语义完整；
- 诊断事件 schema 兼容；
- 安全与许可证边界通过；
- 候选自己的 P0 证据仍在适用环境内有效。

运行期代换只允许发生在蓝图声明的 Slot 上。Core、StateStore schema、revision 语义和安全政策不能作为普通插件热替换。

## 27. 性能与资源合同

本蓝图先冻结测量点，不伪造尚未验证的绝对数值：

| 路径 | 必须测量 | 失败表现 |
| --- | --- | --- |
| RuntimeIngress → reducer | 队列深度、排队时间、单步时间 | 命令延迟或生命周期竞态 |
| accepted snapshot → projection | revision、计算时间、合并次数 | 各表面状态不同步 |
| projection → first frame | frame revision、ready 时间、丢帧 | 白条、旧帧或首次闪烁 |
| gesture | Update 合并率、End/Cancel 延迟 | 拖动卡死、位置回弹 |
| StateStore | load/commit/CAS/恢复时间 | UI 回弹、状态丢失 |
| DesktopHost | discover/attach/recover/detach 时间 | Win+D、Explorer、层级回归 |
| Renderer | CPU/GPU/内存、surface 重建 | 卡顿、资源泄漏 |
| Control Center | 冷开、重开、关闭后资源 | 海报受影响或后台常驻浪费 |
| Item scan/import | 吞吐、取消、ledger 完整性 | 桌面快捷方式风险 |
| AI/theme | provider、编译、预算和降级 | UI 阻塞或不受控资源 |

禁止用无限队列掩盖吞吐不足。任何“保留最新值”策略必须标注可被替换的消息类别，并保证 mutation、commit result、End/Cancel、Ready/Fault/Release 等终态不可丢。

## 28. 安全与信任边界

### 28.1 Item 与启动

- Item 只保存规范化目标、显示元数据和受控统计；
- 启动必须经过 ShellLaunch adapter，不执行任意拼接命令；
- 导入 native code、脚本和危险参数必须拒绝或进入显式审批；
- 桌面快捷方式移动前必须有 recovery ledger；
- 不把 Explorer 图标坐标当作权威布局数据；
- 默认发现只能添加，不能静默移动、删除或覆盖用户文件。

### 28.2 Theme

- ThemeSpec 是数据或受限 DSL，不是任意 JavaScript、DLL 或 shell；
- 资源需要 MIME、尺寸、数量、解码预算和路径约束；
- token 只能映射到公开的 Theme Capability；
- renderer adapter 必须声明支持集和降级结果；
- 导入、验证、编译、预览、应用和回滚是不同阶段；
- 主题失败不能破坏当前 accepted snapshot。

### 28.3 AI

- provider 凭据不进入业务快照、日志或主题包；
- AI 只能生成结构化建议或 ChangePlan；
- ChangePlan 必须经过 schema、安全、能力、revision 和用户确认策略；
- AI 最终通过普通 CommandEnvelope 进入同一 CAS 路径；
- provider 超时、拒绝、离线或撤权不会影响本地核心功能；
- AI 输出不能绕过 Theme、Item、Launch 和 StateStore 边界。

### 28.4 Diagnostics

- 日志默认脱敏，不记录凭据、完整私人路径和不必要内容；
- 错误必须保留 request ID、revision、surface epoch、adapter 和阶段；
- 诊断只观察，不得成为补偿性状态权威；
- 证据导出必须可追溯到场景、版本、环境和原始事件。

## 29. 冻结需求追溯

| 冻结需求域 | 蓝图入口 | 主要证明层 |
| --- | --- | --- |
| 海报视觉与交互 | PosterProjection、PEB-03/04/05、DesktopHost/Renderer 双槽 | 类型夹具 + renderer P0 + DesktopHost E5 + 人工门 |
| 普通程序 > 海报 > 图标 > 壁纸 | DesktopHostSlot 与 HostProjectionIntent | DesktopHost 合同与平台证据，Axiom 不证明 |
| Win+D / Explorer 恢复 | LifecycleReducer、surface epoch、fence | DesktopHost 自动/人工矩阵 |
| 控制中心/托盘/海报一致 | accepted snapshot → ProjectionGraph | 类型夹具 revision 测试 + UI 集成门 |
| 单一状态权威 | CommandEnvelope → Store CAS → accepted snapshot | reducer 单元测试 + store 故障矩阵 |
| 位置/尺寸/锁定/收起恢复 | mutation、validation、Store commit | 核心/存储/Release 回归 |
| Item 活跃度排版 | ItemSnapshot → Activity → Layout | 纯算法性质测试 + 视觉基准 |
| Item 启动统计 | LaunchIntent → LaunchResult → mutation | adapter 集成与失败测试 |
| 仅添加与安全收纳 | discovery/import/ledger 子图 | 文件夹沙盒与恢复演练 |
| 自定义主题 | ThemeSpec → validate/compile/project | schema、预算、安全和 renderer 兼容测试 |
| AI 接入 | provider → ChangePlan → CommandEnvelope | provider 契约、权限和审计测试 |
| 托盘与生命周期 | StartIntent、TrayProjection、BeginExit | 生命周期场景矩阵 |
| 安装/自启/清理 | Shell/Installer adapter 和 release contract | 安装路径、注册表、卸载实测 |

追溯表的“蓝图入口”只说明责任落点。平台行为仍必须由对应适配器证据证明，不能用 Rust 编译成功代替用户可见验收。

## 30. 当前明确不做的技术结论

本蓝图不决定：

- TS-C01 至 TS-C07 哪个 renderer/UI 候选获胜；
- 控制中心是否与海报使用同一 UI 框架；
- DesktopHost 的最终 Win32 实现细节；
- StateStore 使用 SQLite、其他嵌入式数据库或专用文件格式；
- 单进程还是多进程；
- IPC 库与序列化格式；
- GPU API、合成方式和文本栈；
- 安装器、自更新、签名和打包工具；
- Axiom 采用 crates.io、固定 git revision、submodule 还是 vendor；
- 生产 runtime、线程、队列和 IPC 如何实现；axiom-runtime 已排除，不进入候选。

这些项仍须进入统一候选证据矩阵或后续专门决策。不得因为类型夹具存在就把它们标成已实现。

## 31. AX-BP 当前可冻结范围

在 renderer/UI 等候选未选出前，AX-BP 只允许形成研究与架构定义产物：

1. 冻结领域 ID、revision、CommandEnvelope、StoreRequest/Event 和错误语义；
2. 描述 validation、activity、layout、theme capability 与 ChangePlan 的逻辑 Cell 边界；
3. 冻结状态权威、CAS、过期事件与投影一致性关系；
4. 把 DesktopHost E2–E5 合同映射为逻辑端口与物理执行边界；
5. 冻结顺序、可靠性、取消、deadline 和终态约束，但不选择实现机制；
6. 保留可编译的非生产类型夹具，证明 Core 描述可以闭合；
7. 向 TS-C01 至 TS-C07 提供统一的 adapter 约束，不为任何候选背书。

AX-BP 当前禁止：

- 用未获选框架写生产海报或控制中心；
- 把探针代码直接提升为正式 DesktopHost；
- 把 Axiom 示例 thread-per-call flow 当成生产调度；
- 创建未经选型的数据库 schema 并宣称权威存储已定；
- 把本地 axiom 嵌套仓库静默提交为普通目录；
- 建立安装器、升级器或发布包并宣称架构冻结；
- 把 AX-BP 继续扩张为生产实现工作流。

## 32. AX-BP：Axiom Core 程序蓝图计划

AX-BP 只属于架构方法与技术研究层。它使用 Axiom Core 描述系统，完成蓝图冻结后退出，不承担任何生产实现。

| ID | 任务 | 主要产物 | 退出条件 | 当前状态 |
| --- | --- | --- | --- | --- |
| AX-BP-01 | Axiom Core 语义审计与 V2 映射 | Core 概念映射、适用/不适用边界、禁止项 | 明确只用 Core；runtime 排除；概念无误用 | COMPLETE |
| AX-BP-02 | V2 整体程序拓扑 | 顶层程序图、子系统关系、定义/激活分离 | 七份需求全部有拓扑落点 | COMPLETE |
| AX-BP-03 | 模块与端口冻结 | 模块职责、输入、输出、状态、错误、禁止访问 | 端口完整、无重复职责、无隐式旁路 | COMPLETE |
| AX-BP-04 | 调用、数据与组合关系冻结 | 命令流、状态流、投影流、效果流、组合关系 | 单一权威、revision 与因果关系闭合 | COMPLETE |
| AX-BP-05 | 物理执行边界冻结 | 纯/物理边界、进程线程候选约束、I/O 与资源所有权 | 只冻结约束，不选择或实现 runtime | COMPLETE |
| AX-BP-06 | 一致性审计、蓝图冻结与交接 | 审计报告、冻结清单、技术选型/完整架构输入 | 无遗漏、矛盾、重复或实现假设；用户可审阅 | COMPLETE / FROZEN |

### 32.1 AX-BP-01～06 完成证据

- 已完整阅读 Axiom Core 的 foundations、core 文档、Cell 实现和闭边界测试；
- Axiom Core 的 21 unit、5 closed-boundary、6 topology 测试通过；
- DesktopUp 类型夹具只依赖 axiom Core，未依赖 axiom-runtime；
- 已证明 Blueprint/Static 图为零大小定义；
- 已用夹具表达无伪造默认状态、CAS 后接受状态和同 revision 投影；
- 已明确 Conforms 只证明类型兼容，不证明平台与业务语义；
- 已冻结 20 个逻辑角色/模块、九类关系和 15 个物理执行边界；
- 已逐项保留 12 个 TBD 与 4 个 DEFER；
- 已完成重复、矛盾、遗漏、实现假设和 Core-only 审计；
- 已明确 Axiom 位于架构方法与技术研究层，并在 AX-BP-06 后退出。

### 32.2 冻结产物入口

以 [AXIOM_BLUEPRINT_INDEX](AXIOM_BLUEPRINT_INDEX.md) 为入口阅读 AX-BP-01～06；最终审计和后续交接见 [AX_BP_06_CONSISTENCY_AUDIT_AND_HANDOFF_V1](AX_BP_06_CONSISTENCY_AUDIT_AND_HANDOFF_V1.md)。

冻结范围不包含产品 DTO 细节、renderer/UI、数据库、线程、队列、HWND 或网络连接；这些仍由后续技术选型和完整架构决定。

## 33. AX-BP 总体退出门

| 检查项 | 目标 |
| --- | --- |
| 只使用 Axiom Core | 必须 PASS |
| axiom-runtime 依赖、推荐与证据 | 必须为零 |
| 七份冻结需求均有蓝图落点 | 必须 PASS |
| 模块职责和端口无重复/遗漏 | 必须 PASS |
| StateStore 单一业务权威无旁路 | 必须 PASS |
| 调用、数据、组合和反馈关系闭合 | 必须 PASS |
| 纯计算与物理执行边界明确 | 必须 PASS |
| renderer/UI/store/runtime/IPC/安装未被偷偷选定 | 必须 PASS |
| AX-BP 产物可直接交给后续技术选型与完整架构阶段 | 必须 PASS |

## 34. AX-BP 与后续阶段的关系

~~~text
AX-BP-01..06
  Axiom Core architecture method
  program blueprint + frozen boundaries
                 |
                 v
Technical Selection / Full Architecture
  choose renderer, UI, StateStore, runtime, process, IPC, installer
                 |
                 v
B/C Implementation Workflows
  production code and verification
~~~

AX-BP 不与生产实现并行，也不为任何候选背书。候选发现的事实可以反馈为蓝图约束，但不能把候选私有类型写入 Core 蓝图。

## 35. 当前接手入口

1. 七份冻结需求与一致性审计；
2. DesktopHost E2–E6 合同；
3. 本 Axiom Core 程序蓝图；
4. prototype/axiom-desktopup-blueprint/README.md；
5. prototype/axiom-desktopup-blueprint/src/lib.rs；
6. 技术候选统一证据矩阵与部分决策记录。

AX-BP 已关闭。当前下一动作返回 TS-C01～TS-C07 renderer adapter/P0 证据；Axiom Core 只保留为已冻结的系统描述与追溯方法。
