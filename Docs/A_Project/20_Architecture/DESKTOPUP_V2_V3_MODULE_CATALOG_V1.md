# DesktopUp V2 V3 模块分类与端口目录 V1

状态：`DESIGN BASELINE — MODULE RESPONSIBILITIES ALIGNED 2026-09-16`（布局职责待L01冻结，不把设计类型记为现存代码）

建立日期：2026-09-03

## 0. 多实例新增/调整端口

设计细节见 [通用多海报合同](GENERAL_CONTENT_MULTI_POSTER_CONTRACT_V1.md)。M01 已实现目录/实体/schema v3；M02 已将 D11/D12 职责落实到现有 StateProjectionCoordinator、StateCommandPolicy 和 IStateCommandRepository/SqliteStateCommandRepository，并追加 schema v4。不是新增平行权威服务或要求同名类；M03 的 A07 对应 Application/PosterSessionRegistry + App/WinUiPosterSessionFactory；A08 对应 App 中唯一共享恢复调度、隐藏环境监听锚点及版本门，并非强制另建同名类。其他端口按计划实现，已有 DesktopHost 不重写。

| ID | 模块 | 所有权与端口 | 任务 |
| --- | --- | --- | --- |
| A07 | PosterSessionRegistry | PosterId → Session；List/Create/Close/Dispose，晚到回调校验 session generation | V3-04-M03 |
| A08 | DesktopEnvironmentCoordinator | 唯一环境发现/恢复调度，逐实例结果；不保存单张业务状态 | V3-04-M03 |
| A09 | PosterSpatialCoordinator（由PosterSpatialRuntime承接） | 完整占位/障碍读集、候选推让、条件归位因果、异构几何CAS与呈现协调；纯求解放Domain，复用现有命令权威 | V3-04-M04-02..05 |
| B13 | PosterStackCoordinator | 正常布局无重叠；保留异常重叠命中防御和cluster顺序；不得以抬层消除碰撞或盖住普通程序 | V3-04-M04-06 |
| D11 | PosterCatalogApplicationService | Create/CloneConfig/Rename/Remove/List，profile 初始化与零实例 | V3-04-M01/02 |
| D12 | ScopedCommandCoordinator | Entity/ReadSet CAS、全局提交序列、批量状态事务和 NotFound | V3-04-M02 |
| D13 | PortableProfile职责（现有代码承接） | Application/PortableProfileCodec、Planner及StateProjectionCoordinator.Portable：一致快照/严格解析/确定性映射/完整CAS；App/PortableProfileController及中心全局入口：选择/预览/确认；Infrastructure/PortableProfileFile和StateBackupRecovery：原子文件及离线恢复，均不形成第二权威 | V3-04-06已接入，剩余验证归C；L03扩展布局载荷/映射，完整主题素材仍V3-07-04 |
| E14 | ContentCapabilities | 文件/应用/Shell/虚拟流能力和 DropIntent；不越权搬运 | V3-05-01/07/13 |
| E15 | PosterThemeParameterContract（M05最小实现已接入） | Domain/PosterPresentationPolicy解析v1参数/旧兼容/安全fallback，Application/PosterPresentationDraft与SetPresentation复用实体CAS；每实例Presentation.Metrics供renderer/输入/展开/恢复，不强制固定NIGHT OPS；完整manifest/主题加载仍V3-07 | V3-04-M05、V3-07 |
| F13 | CollectionSubscriptionHub | 共享来源 watcher 引用计数、取消、事件合并，更新多个关联视图 | V3-05-11 |
| D07/D08 扩展 | Existing Theme/AI Services | 共享服务但每次计划带目标 IDs/实体版本，实例覆盖与草稿独立 | V3-07/08 |

已实现状态以代码锚点为准。App/PosterVisualWindow 中尚集中存在的协调和演示逻辑，在对应项逐步提取；模块目录不把计划中的类当作现有代码。

04-06新增持久PlacementPending由Domain/仓储负责，投影计算有效可见，A07会话注册/恢复过滤待处理项；空间命令拒绝直接移缩/收展绕过安置。D13只产出本机配置变更，不操作真实Item文件。内部备份恢复与便携导入两条通道分开，最终交付范围见[本轮记录](../40_Implementation/V3-04/PORTABLE_PROFILE_DELIVERY.md)。

M05实际边界见[参数合同](../40_Implementation/V3-04/M05_PRESENTATION_PARAMETERS.md)：几何、内容参数与主题实例身份分离；外框缩放不改iconSize/字号，参数提交不擅自移动外框。M05当时仅有中心摘要；后续04-04已接常用/高级参数编辑与自动提交，不能继续标作未实施。L05待做的是Item跨格编辑，不是再造一次控制中心参数编辑器。

M06为[多实例合同夹具](../40_Implementation/V3-04/M06_MULTI_INSTANCE_FIXTURES.md)，复用上述模块而非新增产品服务；测试专用共享数据、故障注入及隔离App探针不进入日常启动路径。该阶段自动/进程证据保留，后续集中人工与资源证据已归入M07，不再把M07列为待启动任务。

M07是外部测量/安全门，不新增生产服务或状态权威；当前已[收口](../40_Implementation/V3-04/M07_CLOSEOUT.md)，资源预算差距和未认证环境仍保留。具体计数、产物、人工结论只引用C层证据，本模块目录不重复维护滚动测试数字或下一步指令。M08已增加Application纯行为模型及逐海报设置/迁移合同，原生观察及生产共享调度/空间事务已接，最小设置UI已由M08-05接入，M08-06已补共享系统通知/多原因暂停及隔离生命周期回归，完整体验门与最终自动环境复测仍待补，现有空间、输入和恢复通道继续复用。

### M08标题交互职责补充（TI-01/02证明及TI-03接线已建，实机门待验）

按V2-DEC-003及[TI合同](../40_Implementation/V3-04/M08_TITLE_INTERACTION_CONTRACT.md)，Windows输入层只负责命中、capture和阈值事件；Application负责按下阶段固定、点击/双类拖动仲裁及抑制；A09既有PosterSpatialRuntime是唯一组几何运行器。Domain继续复用SpatialContinuitySolver/SpatialExpansionPolicy；D12保留完整读集和原权威事务。R4负责整组容量/预布局/提交/恢复，不因快展再建一条像素写入路径。

现有自动StartAutomatic和手动ToggleCollapsed均调用SetCollapsed，来源区别只在触发/权限/反馈，不在空间策略。新标题快展只改变呈现节奏；收起态拖动按TI-01/02模型证明、TI-03原通道接管执行。旧ExpansionDrag增长实验及专用事务口已在TI-01退役；Application/TitleInteractionModel只输出暂停/快展/双类拖动意图，TitleAutoRearmSuppression复用原自动模型的手动退出证据，不新增几何或状态权威。TI-02新增TitleSpatialKernel/Session/Commit：复用原碰撞/归位/细分、接受已呈现提案、重放并核对终态/因果，使用原协调器与SQLite单revision完整CAS。详见[TI-02报告](../40_Implementation/V3-04/M08_TI02_SPATIAL_TRANSACTION.md)；[TI-03](../40_Implementation/V3-04/M08_TI03_PRODUCTION_TITLE_ADAPTER.md)已接原输入岛TitleInput→PosterSpatialRuntime.Title→TitlePresentationDriver→原R4组呈现；ITitleSpatialCommitter通过一次绑定的可信宿主见证复用原CAS，不新增持久权威或第二套收展求解。

[TI-04分段证明](../40_Implementation/V3-04/M08_TI04_ISOLATED_GATES.md)：Application在当前租约内独立重放有界尾段后封存不透明checkpoint，保留原Press/因果，不逐帧写库；原Title运行器仅在完整proposal接受后请求封段，最终仍完整CAS/单revision。组件故障夹具只测R4 API和共享恢复，不是第二个生产输入或新的状态权威。

## 1. 目的

2026-09-16补充（V2-DEC-007/008，04-L01 READY）：C02/D06/E03/E04/E07的跨格占位、编辑和连续视口增量归[04-L](../30_Plans/V3_04_L_CONTENT_LAYOUT_PLAN.md)，现已前移为主线、位于真实Item前。LayoutDocument/Placement/Snapshot是待L01冻结的合同职责，由Domain/Application承接；UI复用现有视口、输入岛/R4，SQLite与PortableProfile承接版本化持久化，不另建权威。G类主题只提交声明和度量，不自建几何；E06真实热度仍V3-06。现有宏观度量/呈现/事务/生命周期入口见[代码锚点](../40_Implementation/V3-04/CODE_ANCHORS.md)，不把已有单项尺寸接口当作完成跨格求解或编辑器。04-06余验收和04-07/08收口的延期归C计划，不删相应模块职责。

本文把 V3 总体架构拆成可独立实现、测试和替换的模块。模块名表达职责，不等同于最终程序集数量；首发可在一个解决方案和主进程中实现，只有高风险 Shell、主题代码和更新流程需要物理隔离。

## 2. 建议工程结构

```text
DesktopUp.sln
src/
├─ DesktopUp.App/                 WinUI 入口、Poster、Control Center、Tray
├─ DesktopUp.Application/         用例、命令、查询、事务与投影
├─ DesktopUp.Domain/              纯模型和策略
├─ DesktopUp.Infrastructure/      SQLite、文件、主题、AI、日志
├─ DesktopUp.Windows/             HWND、Explorer、Shell、DPI、拖放
├─ DesktopUp.ShellProxy/          可选隔离进程
├─ DesktopUp.ThemeHost/           可选受限主题进程
└─ DesktopUp.Updater/             可选更新进程
native/
└─ desktopup-native/              可选 Rust/C ABI
tests/
├─ DesktopUp.Domain.Tests/
├─ DesktopUp.Application.Tests/
├─ DesktopUp.Windows.Tests/
├─ DesktopUp.Integration.Tests/
└─ DesktopUp.ManualGates/
```

首阶段只建立前五个项目和对应测试；辅助进程与 Rust 模块在需求进入相应阶段时创建。

## 3. A 类：进程、启动与生命周期

| ID | 模块 | 职责 | 主要端口 | 阶段 |
| --- | --- | --- | --- | --- |
| A01 | `AppBootstrap` | 组合依赖、建立数据目录、加载配置、启动 UI dispatcher | `IAppComposition` | V3-01 |
| A02 | `SingleInstanceCoordinator` | 主实例互斥、第二实例意图转发、拒绝重复托盘/海报 | `IActivationRouter` | V3-01 |
| A03 | `RuntimeLifecycleCoordinator` | 冷启动、普通启动、退出、崩溃恢复和终态顺序 | `ILifecycleParticipant` | V3-01 |
| A04 | `StartupIntentParser` | 区分普通、自启、协议、通知、设置和测试启动 | `StartupIntent` | V3-01 |
| A05 | `ResourceOwnershipLedger` | 登记 HWND、hook、timer、watcher、native handle 和释放结果 | `IResourceLedger` | V3-01 |
| A06 | `BackgroundWorkScheduler` | 有界后台任务、取消、退避和退出 drain | `IBackgroundWorkScheduler` | V3-01 |

DeskBox 对应经验：单实例、启动到托盘、桌面图标宿主稳定后挂接、隐藏 UI 资源回收。DesktopUp 首发去除天气、音乐、通知等无关启动任务。

## 4. B 类：桌面宿主与窗口系统

| ID | 模块 | 职责 | 主要端口 | 阶段 |
| --- | --- | --- | --- | --- |
| B01 | `ExplorerDesktopLocator` | 查找已存在 `SHELLDLL_DefView`，稳定采样并产生 generation | `IDesktopShellLocator` | V3-02 |
| B02 | `DesktopLayerAttachment` | 保存/设置/验证 owner，原子 bottom/show、no-show bottom reassert、恢复原 owner并管理 desktop generation | `IDesktopLayerAttachment` | V3-02 |
| B03 | `WindowStylePolicy` | 无边框、无标题栏、no-activate、非 topmost、`WM_MOUSEACTIVATE`/`MA_NOACTIVATE` 与任务切换可见性 | `IWindowStylePolicy` | V3-02 |
| B04 | `PosterWindowCluster` | 管理 VisualWindow 与全部 InputIsland 的共同生命周期 | `IPosterSurfaceHost` | V3-02 |
| B05 | `PosterVisualWindow` | WinUI 半透明海报视觉与首帧门 | `IPosterVisualSurface` | V3-02 |
| B06 | `PosterInputIslandManager` | 建立/复用标题、按钮、Item、滚动、缩放输入区 | `IInputRegionHost` | V3-03 |
| B07 | `WindowZOrderPolicy` | 维持普通程序 > 海报 > 图标 > 壁纸；首次显示原子置底，指针消息前同步置底，不抢前台 | `IZOrderController` | V3-02 |
| B08 | `WindowGeometryTransaction` | 原子准备和提交外框、输入岛、裁剪与视觉 revision | `IGeometryCommitter` | V3-03 |
| B09 | `DisplayTopologyService` | 显示器身份、工作区、DPI、归一化锚点和稳定窗口 | `IDisplayEnvironment` | V3-02 |
| B10 | `PointerGestureSession` | 拖动/缩放 capture、帧节流、取消、完成和回滚 | `IGestureSession` | V3-03 |
| B11 | `CollapseStateMachine` / `AutoPostureModel` | expanded/collapsed bounds、边界锚点、动画与持久化；M08纯模型提供自动资格/保护/仲裁/取消协议；已有按需观察及AutoPostureScheduler共享调度，复用空间运行器 | `ICollapseController`；新增AutoPostureFrame/State/Step纯输入输出 | V3-03；M08-01～06模型/观察/调度/设置/生命周期接线；完整验收后续 |
| B12 | `DesktopRecoveryCoordinator` | Explorer 重建、Win+D、桌面刷新、会话/电源恢复 | `IDesktopRecovery` | V3-02/03 |

关键规则：B 类只管理窗口和输入，不读取 Item 排名、不解释主题包、不执行文件移动。

2026-09-11 [M08边界](../30_Plans/V3_04_M08_AUTO_POSTURE_PLAN.md)：自动行为纯模型及调度属于Application，Windows仅提供被动指针/保护信号，App组合共享服务并复用空间运行器；配置沿用Domain/Repository/Command/Projection，现有中心只补最小开关/秒数入口。当前已实现AutoPostureModel，以及Domain秒数/schema7/SetAutoPosture/草稿/投影，见[设置合同](../40_Implementation/V3-04/M08_AUTO_POSTURE_SETTINGS.md)；[M08-03](../40_Implementation/V3-04/M08_POINTER_OBSERVATION.md)已实现PosterPointerTracker、PassivePosterPointerObserver及App当前会话采集端口，[M08-04](../40_Implementation/V3-04/M08_SHARED_SCHEDULER.md)已接AutoPostureScheduler/AutoPostureEvidence、App共享计时及IAutomaticPostureCommitter，[M08-05](../40_Implementation/V3-04/M08_CONTROL_CENTER_SETTINGS.md)已接ControlCenterAutoPostureEditor与真实控件，latest/basis分离并复用CAS；[M08-06](../40_Implementation/V3-04/M08_LIFECYCLE_REGRESSION.md)已接SystemLifecycleGate、共享WTS/电源分类与原恢复版本门；完整体验及输入桌面环境复测待补，不宣称整个M08已完成。

### 4.1 V3-02 恢复职责补强

| 模块 | 直接吸收 DeskBox | DesktopUp V3 补强责任 |
| --- | --- | --- |
| B01 `ExplorerDesktopLocator` | `TaskbarCreated`、缓存失效、只查既有 DefView、稳定采样 | 产生包含 DefView/容器/Explorer PID 的单调 `DesktopShellGeneration`，拒绝旧快照 |
| B02 `DesktopLayerAttachment` | owner 保存/设置/读回、失败回滚、non-topmost/no-activate、bottom 与 `SWP_SHOWWINDOW` 原子提交 | 维护每个 cluster 的 `attachedGeneration`；同一 HWND 集原地重挂，提供 no-show bottom reassert，禁止 hide/show、激活和复制窗口 |
| B03/B07 激活与层级策略 | `SetWindowSubclass`、`WM_MOUSEACTIVATE`、`MA_NOACTIVATE`、Activated 后恢复 desktop bottom | 普通指针永不取得层级 lease；失败进入 B12，不使用周期抢层或临时 topmost；未来文本输入另设显式键盘 lease |
| B09 `DisplayTopologyService` | 原生消息、低频轮询签名、DPI/work area、稳定窗口 | 产生独立 `DisplayTopologyGeneration`，输出不可变稳定快照，不持久化暂态拓扑 |
| B12 `DesktopRecoveryCoordinator` | 信号合并、有界延迟和恢复重试 | 单航班协调两个 generation；迟到结果作废；提交后统一验证 owner、样式、层级和 bounds |

B01/B02 的 Shell generation 与 B09 的 topology generation 必须分离。两者同时变化时，B12 捕获版本对并等待各自稳定；任何一个版本在提交前后改变，本轮恢复不得记为成功。

## 5. C 类：用户界面表面

| ID | 模块 | 职责 | 主要输入/输出 | 阶段 |
| --- | --- | --- | --- | --- |
| C01 | `PosterShellView` | 受保护标题栏、按钮、内容 viewport 和底栏结构 | `PosterProjection` / `PosterCommand` | V3-02/03 |
| C02 | `ItemViewportView` | 虚拟化 Item、连续裁剪、滚动与稳定 identity | `LayoutSnapshot` / `ItemCommand` | V3-05 接真实内容时推进 V3-06-01..04 |
| C03 | `ItemCardView` | 图标、标题、状态、选择、启动和拖放反馈 | `ItemProjection` | V3-05 |
| C04 | `ControlCenterWindow` | 海报列表、编辑、Item 管理、主题和设置 | `ControlCenterProjection` | V3-04 起 |
| C05 | `TraySurface` | DEC-006六类整体入口：新增、中心、临时全局显隐、全部锁定、动效、退出；无单张选择/子菜单 | 全局/目录命令、`TrayProjection`及运行期显隐摘要；不依赖中心SelectedPosterId | V3-01/04 |
| C06 | `DialogCoordinator` | 确认、冲突、失败、文件/文件夹选择和系统对话框 owner | `IDialogService` | V3-04/05 |
| C07 | `FallbackSurface` | 主题、数据库或渲染故障时的最小恢复 UI | `RecoveryProjection` | V3-04/07 |

C 类不得直接写数据库或调用 Shell API。所有交互转换成 Application command。

2026-09-15旧UI实现基线：[04-04编辑合同](../40_Implementation/V3-04/CONTROL_CENTER_EDIT_CONTRACT.md)及[02～06接线](../40_Implementation/V3-04/CONTROL_CENTER_DELIVERY.md)。Application/ControlCenterPosterEditor负责目标草稿，ControlCenterWindow.Editing已接预览/几何/目录/参数，M08专用编辑器仍是应用/取消。ControlCenterGeometryRequest交原空间运行器，ControlCenterSpatial组织碰撞/完整CAS证明，App复用R4与恢复；用户反馈几何/预览失败，不能整体标验收通过。

V2-DEC-005的[可用性设计](CONTROL_CENTER_USABILITY_DESIGN_V1.md)已由04-04 UX接入：C04宏观控件/真实海报定位，Application目标化预览/自动提交与ContentMetrics映射，Domain参数/相对层级/布局输入分层，App目标会话的短时定位和清理。目录状态刷新闪烁另按BUG-004记录，不能把接线完成写成所有体验问题已解决。持久化沿用原命令/仓储，空间仍归原运行器；旧桌面缩略预览专用入口已退役，共享有效几何读回保留。M08仅设置适配变化，不重写自动调度。

## 6. D 类：应用用例与投影

| ID | 模块 | 职责 | 主要命令/查询 | 阶段 |
| --- | --- | --- | --- | --- |
| D01 | `PosterApplicationService` | 创建、加载、移动、缩放、锁定、收起和可见性 | `UpdatePoster*` | V3-03/04 |
| D02 | `ProjectionCoordinator` | 从同一 revision 生成 Poster/Control/Tray 投影 | `GetProjection` | V3-04 |
| D03 | `ItemApplicationService` | 添加、移除、启动、失效处理和元数据刷新 | `AddItem`, `RemoveItem`, `LaunchItem` | V3-05 |
| D04 | `CollectionApplicationService` | 新建映射/受管集合、改变目录、清理孤儿目录 | `CreateCollection`, `DetachCollection` | V3-05 |
| D05 | `FileTransferApplicationService` | dry-run、恢复日志、执行、验证和补偿 | `PlanTransfer`, `ExecuteTransfer` | V3-05 |
| D06 | `LayoutApplicationService` | 排名、固定、模式切换、重排和撤销 | `ReflowItems`, `PinItem`, `UndoLayout` | V3-06 |
| D07 | `ThemeApplicationService` | 导入、验证、预览、应用、回退和热更新 | `ApplyTheme`, `ReloadTheme` | V3-07 |
| D08 | `AIApplicationService` | provider 选择、上下文裁剪、ChangePlan 与确认执行 | `GenerateChangePlan`, `ApplyApprovedPlan` | V3-08 |
| D09 | `SettingsApplicationService` | 用户设置、性能模式、自启 opt-in 和同步 | `UpdateSettings` | V3-04/09 |
| D10 | `RecoveryApplicationService` | 最近可用状态、孤儿文件、失败操作和诊断恢复 | `RecoverOperation`, `RestoreBackup` | V3-04/05/09 |

D13 的 PortableProfile 是跨机习惯配置，不替代 D10 的完整数据库灾难恢复。删除确认由中心/海报菜单共用呈现服务，核心负责目标/版本/影响校验；导入与主题包校验复用既有服务，不允许 UI 直接写 SQLite。

Application 层拥有用例顺序和事务，不拥有 WinUI 控件或 HWND。

## 7. E 类：领域模型与纯策略

| ID | 模块 | 职责 | 不允许依赖 | 阶段 |
| --- | --- | --- | --- | --- |
| E01 | `PosterModel` | 海报身份、标题、状态、几何引用和主题引用 | WinUI/Win32/SQLite | V3-04 |
| E02 | `FrameGeometry` | DIP 几何、显示器身份、锚点、约束和收起配对 | WinUI/Win32 | V3-03 |
| E03 | `ContentMetrics` | 字体、Item 尺寸、间距、密度和用户参数 | UI 控件 | V3-04-M05 合同，V3-06 扩充 |
| E04 | `ResponsiveLayoutPolicy` | 列数、行流、viewport、裁剪和可达性 | HWND/数据库 | V3-06 |
| E05 | `ContentItemModel` | 类型、来源、目标、状态、排序和活跃度 | Shell API | V3-05 |
| E06 | `ActivityScoringPolicy` | recency/frequency/newness、衰减和稳定排序 | 网络/UI | V3-06 |
| E07 | `ManualLayoutPolicy` | 自动、锚定+自动、全手动与冲突偏移 | UI 控件 | V3-06 |
| E08 | `CollectionModel` | reference/mapped/managed 所有权、能力和删除语义 | 文件系统 | V3-05 |
| E09 | `FileTransferPlan` | 来源、目标、动作、冲突、回滚和逐项结果 | IFileOperation | V3-05 |
| E10 | `ThemeManifest` | 主题身份、版本、资源、能力和权限 | XAML loader | V3-07 |
| E11 | `ThemeValidationPolicy` | schema、资源范围、受保护交互与兼容性 | 网络/文件写入 | V3-07 |
| E12 | `AIChangePlan` | 有限变更操作、风险级别、diff 和确认状态 | provider SDK | V3-08 |
| E13 | `LockPermissionPolicy` | USING/EDITING/LOCKED 下允许的命令 | WinUI | V3-03 |

E 类优先使用确定性单元测试和性质测试；它们是后续 UI 或原生实现更换时的稳定核心。

## 8. F 类：状态、文件与 Windows Shell 基础设施

| ID | 模块 | 职责 | 主要端口 | 阶段 |
| --- | --- | --- | --- | --- |
| F01 | `SqliteStateStore` | schema、事务、revision/CAS、迁移与备份 | `IStateStore` | V3-04 |
| F02 | `RecoveryJournalStore` | 文件操作预写日志、提交、恢复和压缩 | `IRecoveryJournal` | V3-05 |
| F03 | `ContentRepository` | Poster、Collection、Item 和统计仓储 | `IContentRepository` | V3-04/05 |
| F04 | `FolderInventoryService` | 枚举、规范化、重复/失效识别 | `IFolderInventory` | V3-05 |
| F05 | `FolderWatcherAdapter` | FileSystemWatcher/StorageFolder 变化、合并与退避 | `IFolderWatcher` | V3-05 |
| F06 | `ShellFileOperationAdapter` | 复制、移动、重命名、删除、进度和取消 | `IFileOperations` | V3-05 |
| F07 | `NativeDropTargetAdapter` | HDROP、StorageItem、URL、图片及 drop effect | `IDropTargetPort` | V3-05 |
| F08 | `ShortcutAdapter` | `.lnk` 读取、创建、解析和修复 | `IShortcutService` | V3-05 |
| F09 | `ItemLaunchAdapter` | 文件、文件夹、快捷方式、应用和协议启动 | `IItemLauncher` | V3-05 |
| F10 | `IconThumbnailAdapter` | 图标/缩略图提取、缓存和失效 | `IVisualAssetProvider` | V3-05 |
| F11 | `ShellContextMenuAdapter` | 有 owner 的系统菜单和属性 | `IShellMenuService` | V3-05/09 |
| F12 | `LocalDataPathService` | 数据、日志、主题、缓存与受管文件根目录 | `IDataPathService` | V3-01 |

F06/F07/F08/F11 可先用 C# 适配器，遇到 AOT、COM 稳定性或第三方扩展风险时转移到 Rust DLL 或 ShellProxy；上层端口不变。

## 9. G 类：主题、AI 与外部集成

| ID | 模块 | 职责 | 安全边界 | 阶段 |
| --- | --- | --- | --- | --- |
| G01 | `ThemeCatalog` | 内置/本地主题发现、版本和启停 | 只读主题根 | V3-07 |
| G02 | `ThemePackageLoader` | manifest、资源和模板加载 | 路径规范化、大小上限 | V3-07 |
| G03 | `ThemePreviewHost` | 隔离候选主题并产生预览结果 | 不触碰正式状态 | V3-07 |
| G04 | `ThemeHotReloadWatcher` | 监听变更、去抖、验证和触发重载 | 不直接切换正式主题 | V3-07 |
| G05 | `ThemeFallbackCoordinator` | 最近可用主题和 minimal fallback | 始终可恢复 | V3-07 |
| G06 | `AIProviderRegistry` | OpenAI/DeepSeek/OpenAI-compatible 选择 | 无 Key 不初始化 | V3-08 |
| G07 | `AIContextRedactor` | 只提供必要主题/布局上下文 | 默认移除私密路径/文件名 | V3-08 |
| G08 | `AIPlanValidator` | schema、权限、风险和受保护交互检查 | 不执行计划 | V3-08 |
| G09 | `SecretStoreAdapter` | BYOK 凭据保存与删除 | 不进入日志/导出 | V3-08 |

## 10. H 类：诊断、发布与隔离进程

| ID | 模块 | 职责 | 阶段 |
| --- | --- | --- | --- |
| H01 | `StructuredLogger` | 有界结构日志、相关 operation/revision/generation | V3-01 |
| H02 | `DiagnosticsBundleService` | 隐私过滤后的诊断导出 | V3-09 |
| H03 | `HealthMonitor` | StateStore、desktop host、watcher、theme host 状态 | V3-04 起 |
| H04 | `PerformanceSampler` | CPU、Working Set、GPU、句柄、首帧和交互延迟 | V3-02 起 |
| H05 | `ShellProxyProcess` | 隔离第三方 Shell 扩展和缩略图 | V3-05 启用扩展前；V3-09 完善退出/发布 |
| H06 | `ThemeHostProcess` | 未来受限用户代码主题 | V3-07 后半 |
| H07 | `UpdaterProcess` | 主进程退出后的版本替换与回滚 | V3-09 |
| H08 | `InstallerLifecycle` | 当前用户安装、路径选择、自启 opt-in、卸载恢复 | V3-09 |

## 11. 状态所有权矩阵

| 状态 | 唯一写入者 | 持久化位置 | UI 权限 |
| --- | --- | --- | --- |
| Poster 业务状态 | D01 | F01 SQLite | 只发命令 |
| FrameGeometry | D01 | F01 SQLite | 编辑预览后提交 |
| EffectiveExpansion（R2已接入） | SpatialExpansionPolicy＋EffectivePosterLayouts＋PosterSpatialRuntime | 会话内可重建，无新持久字段；不得覆盖FrameGeometry意图 | 中心/海报同源有效Bounds及受限说明；托盘同源数量；独立InsufficientSpace结果 |
| Item/Collection | D03/D04 | F01 SQLite | 只发命令 |
| 文件传输执行状态 | D05 | F02 journal + F01 结果 | 观察/取消 |
| 活跃度原始统计 | D03 | F01 SQLite | 只读 |
| LayoutSnapshot | D06 | 可重建；必要字段进 F01 | 只读 |
| 主题选择 | D07 | F01 SQLite | 只发命令 |
| 主题资源 | G02 | 主题目录 | 预览/选择 |
| AI Key | G09 | 系统凭据存储 | 仅设置/删除 |
| HWND/owner/generation | B01–B12 | 运行期内存 | 不可直接写 |

## 12. 依赖规则

允许方向：

```text
Presentation → Application → Domain
Infrastructure → Application/Domain 端口实现
Windows → Application/Domain 端口实现
Bootstrap → 所有组合根
```

禁止方向：

- Domain 引用 WinUI、Win32、SQLite、HTTP 或文件系统；
- ViewModel 直接 `File.Move`、执行 SQL 或设置 HWND owner；
- Theme/AI 直接调用文件转移、进程启动或窗口层级；
- DesktopLayerAttachment 读取业务数据库或主题资源；
- Watcher 回调直接更新 XAML 集合；
- 第二份 JSON/localStorage 成为业务权威；
- 辅助进程持有无法由主进程撤销的唯一状态。

## 13. DeskBox 复用映射

| DesktopUp 模块 | DeskBox 可参考区域 | 复用判断 |
| --- | --- | --- |
| B01/B02/B07/B12 | `Services/WidgetLayerService.cs`、相关 layer/recovery tests | 高；保留已验证 cluster，扩展按 PosterId 管理多个实例 |
| B03/B05/B09/B10 | `Views/WidgetWindowBase.*`、Win32Helper、显示器策略 | 高；去除无关 Widget 类型 |
| B11 | `WidgetWindowBase.Collapse.cs`、`WidgetCompact*` policies | 中高；复用尺寸意图/可行性分离；完整尺寸方向回退后按R2最小高度临时展开，不照搬抬层或自动缩小持久尺寸 |
| A01–A06/C05 | App 启动、Tray、bounded scheduler、runtime policy | 中高；重新命名并删去无关服务 |
| D04/D05/F02 | Desktop organization、managed storage、recovery store/transaction | 高；必须符合 DesktopUp recovery ledger |
| F04–F11 | FileService、FolderWatcher、NativeDrop、Shortcut、Shell tests | 高；逐能力移植并保留许可证 |
| C02/E04 | DeskBox 文件格子/叠放布局与 DPI tests | 中；DesktopUp 另有连续 viewport 与活跃度要求 |
| G01–G05 | DeskBox appearance/resource dictionary/backdrop | 中低；DesktopUp 需要独立主题包与热更新事务 |
| G06–G09 | 无直接完整对应 | 新建 |
| B06 | DeskBox 完整矩形交互不足以满足透明空白穿透 | 新建并优先探针 |

## 14. 首批冻结端口

M04-06增量：Application的PosterInputRouter/PosterRemovalConfirmation承接共享路由及单次确认；Windows只提供实际HWND顺序和原生输入，App绑定独立会话并在唯一中心承载确认。旧状态命令/CAS/Host沿用，无新schema。313/313及实际确认/退出/链式回归通过，见 [输入确认合同](../40_Implementation/V3-04/M04_INPUT_CONFIRMATION.md)。

M04-03..05增量：Domain求解→Application手势/收展事务合同→App的PosterSpatialRuntime共享协调→Windows整批移动视觉/输入岛；既有命令权威/宿主保护保留。Infrastructure追加v5布局代次/因果表。288/288及真实三海报空间smoke通过，见 [空间运行合同](../40_Implementation/V3-04/M04_SPATIAL_RUNTIME.md)。M04-02纯计算证据保留在其阶段合同，不再代表当前运行边界。

V3-01/02 首先冻结以下接口，避免窗口代码再次侵入业务层：

- `IDesktopShellLocator`
- `IDesktopLayerAttachment`
- `IPosterSurfaceHost`
- `IInputRegionHost`
- `IDisplayEnvironment`
- `IGeometryCommitter`
- `IStateStore`
- `IRecoveryJournal`
- `IFileOperations`
- `IItemLauncher`
- `IThemeRuntime`
- `IAIProvider`

端口只描述能力、输入、输出、错误和取消语义；不得把某个具体 DeskBox 类名直接暴露为公共合同。
