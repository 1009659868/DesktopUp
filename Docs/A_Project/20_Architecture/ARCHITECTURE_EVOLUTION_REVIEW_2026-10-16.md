# DesktopUp 架构审查与增量演进建议

原五张图保留4ae822a基线/目标提案，下文“本轮”指原审查轮。2026-10-20 当前：**本轮基础架构整理 USER ACCEPTED / CLOSED；R1～R8、P1～P3均已验。** 用户已授权正式收口，[当前架构图、收口范围与交付限制](../40_Implementation/Architecture/FOUNDATION_REMAINDER_PLAN.md#31-本轮基础架构正式收口与当前架构图2026-10-20)已记录。图稿showcase 9/9通过，浏览器/视觉复验未完成；不与业务验收混记。剩余规模成本和历史锁冲突继续携带，动态模板/AI/更新器等未实现。当前impl/architecture-foundation及未提交成果保留，本轮不改产品、不提交/推送/合并；真实热度实施HOLD，发布/登录自启仍暂停。下方为阶段历史。

日期：2026-10-16。审查基线：`impl/develop@4ae822a`。**这是源码审查与设计提案，不是已实施重构，也不是性能测量报告。** 本轮不修改产品代码、数据库、用户配置或 Git 分支，不提交/推送。

## 1. 先看结论和图

建议采用“模块化单体＋明确端口＋少量必要进程隔离”。保留已验的状态/文件安全、布局求解、原生交互和增量呈现；先把职责从大窗口类与具体数据库服务中抽出来，再接热度和模板。仅移动文件夹不能解决耦合，也没有证据要求推翻 WinUI 或拆成微服务。

| 阅读顺序 | 图 | 它回答什么 |
| --- | --- | --- |
| 1 | [现状架构](../../../out/architecture-review-20261016/current.architecture.html) | 哪些分层真的存在，职责集中在哪里 |
| 2 | [现有打开流程](../../../out/architecture-review-20261016/current-open.workflow.html) | 打开事实如何落库，热度链缺在哪一段 |
| 3 | [建议目标架构](../../../out/architecture-review-20261016/target.architecture.html) | 稳定内核与热度、模板、AI、更新器如何分工 |
| 4 | [建议能力热替换流程](../../../out/architecture-review-20261016/target-extension.workflow.html) | 如何校验、预览、等安全点、局部切换、失败保留旧版 |
| 5 | [建议应用更新流程](../../../out/architecture-review-20261016/target-release.workflow.html) | 为什么发布更新不能与模板热换混为一谈 |

HTML 为本机生成物，放在被忽略的 out/，不混入安装包；可维护图源在 [ArchitectureReview](ArchitectureReview/README.md) 下。现状架构的16处来源绑定固定提交；目标图全部明确标为建议。图中箭头表示调用/运行职责，不是完整程序集引用图。现状图中的 Windows 适配虽用“外部系统”符号表示平台边界，实际仍有大量代码运行于主进程，不能据此推断成独立进程。

## 2. 当前架构：有分层，但执行边界不够收敛

### 2.1 实际程序集依赖

根据六个生产 csproj，而非目录名称推测：

```text
DesktopUp.App
 ├─ Application ── Domain
 ├─ Infrastructure ── Application + Domain
 ├─ Windows ── Application + Domain
 ├─ Domain
 └─ ContentWorker（构建/部署依赖；不引用其输出程序集）
       └─ Application ── Domain
       ＋链接编译 Infrastructure/LocalContentReferenceProbe.cs

Modules/Lamp、Modules/Rain
 └─ 通过 Directory.Build.targets 分别编入 Domain/Application/App
```

Application 没有直接 ProjectReference 到 Infrastructure；App 作为组合入口引用实现本来合理。问题在于组合之外的会话、窗口、内容操作仍持续持有具体 SqliteStateStore，并直接 new 多种业务服务。基础程序集统一继承 Windows TFM；“纯规则”指代码职责，不代表目前已经是跨平台构建。

| 位置 | 现有职责 | 应保留 | 需要整理的边界 |
| --- | --- | --- | --- |
| App | WinUI、托盘、会话、空间运行、菜单、恢复、服务组装 | UI线程与Windows生命周期的宿主 | 组合入口与业务运行分离；窗口不再当跨模块调度中心 |
| Application | 命令、读集版本校验、投影、协议、纯行为与端口 | 状态提交路径、类型化结果 | 业务用例集中归属，避免只剩“合同库”而实现散在外层 |
| Domain | 内容关系、布局/空间规则、模板参数和提案 | 纯函数、稳定身份、作者布局意图 | 按领域归组；不引入XAML、SQLite和外部网络 |
| Infrastructure | SQLite、文件、账本、Worker客户端，同时含搬运/归还用例 | 已验事务、文件身份和防覆盖实现 | 将“做什么”的编排从“怎样访问存储/系统”中分开 |
| Windows | HWND、桌面层、DPI、命中、OLE、菜单桥接 | 已验原生交互 | 与纯策略/呈现快照通过小端口通信 |
| ContentWorker | Shell打开、图标、属性、原生菜单 | 隔离容易卡住的Shell扩展；不访问业务库 | 协议独立维护；消除跨项目链接源码的隐式依赖 |
| Modules | Lamp/Rain声明、计算、WinUI场景实例 | 装饰能力不直接拥有文件/窗口控制权 | 目前是可信编译期模块，不是运行时模板平台 |
| packaging/scripts/tests | 发布、开发、验收工具 | 已有测试与安全夹具 | 发布入口和验收入口分区；不靠发布分支手工逐项删探针 |

### 2.2 可读性与耦合证据

统计范围为 Git 跟踪的 src/、Modules/ 下 .cs/.xaml，含注释和空行，不含 bin/obj；静态规模只帮助定位，不证明性能瓶颈。

- 431个源码/XAML文件，约55,018行。
- App目录149个文件、22,590行；Infrastructure 62个文件、9,122行。
- 按文件前缀聚合：PosterVisualWindow 44个文件、7,341行；App.* 35个文件、6,536行；ControlCenterWindow 28个文件、4,460行；PosterSpatialRuntime 30个文件、2,820行。拆成partial并没有拆开私有状态、构造依赖或生命周期。
- Probe/Smoke后缀源码65个文件、10,292行，位于生产源码/模块目录。它们有验证价值，不应简单删除；但应逐步从默认产品编译/运行路径隔离。
- 大量操作依靠多字段、委托、Busy布尔值、窗口查询和生命周期取消协作；读者必须跨多个partial拼接一次操作。应让这些约束成为有明确所有者的会话/租约，而不是删掉安全检查。
- 具体例子：App.xaml.cs:143将SqliteStateStore交给ControlCenterService；PosterSpatialRuntime.Transfers.cs:9保存具体存储；CrossPosterTransfer.cs:13直接围绕存储组织交接业务。部分基础设施内部还承担SQL、原生操作和用例判定。
- ContentOpenContract.cs:26/45仍有“future/DEMO”时期注释，但真实打开已经由ReferenceContentActions和SqliteContentOpenJournal接线。注释和当前路径的落差也是阅读障碍，应按实际调用核对后清理，而非凭注释判断功能缺失。

### 2.3 不是从零开始

已有基础应成为新边界内部的实现，而非重新制作：

1. `StateProjectionCoordinator`：版本/读集校验、事务后的投影发布，并已提供ChangedPosterIds。
2. `PosterVisualWindow.Content`：稳定卡片字典、按CardPaint差异更新和图像缓存；不能改回每次重建所有Item。
3. `ContentLayoutProposals`：热度派生、量化/迟滞、手工布局保护、最新提案缓冲；已有交互保护和安全点。
4. `ReferenceContentActions + SqliteContentOpenJournal`：成功交接事实持久化、ActivationId去重；定位不计打开。
5. `IPosterSceneInstance`：场景装饰、共享时钟、异常边界；宿主仍拥有输入。
6. 原生拖放、单一引用、外部最小同步、归还账本和不重放文件操作原则均已验，不因结构调整而重写。

## 3. 目标模块与职责

第一阶段仍保留现有六个生产程序集，先用目录、命名空间、构造依赖与可执行依赖检查建立边界。只有确需独立生命周期/分发/风险隔离时才新增程序集或进程；不要让图上的每个方框都变成一个项目。

| 逻辑模块 | 独占职责 | 对外产物/端口（建议名） | 禁止越界 |
| --- | --- | --- | --- |
| Bootstrap / ApplicationLifetime | 组装服务、启动退出顺序、资源释放 | 明确的启动/停止阶段 | 不处理单次拖放、重命名或评分 |
| ContentUseCases | 打开、新建、重命名、删除、拖放、映射、归还 | 有类型的请求/结果；复用已有命令和日志端口 | 不持有WinUI控件；不直接拼SQL |
| PosterSessionController | 每海报会话状态、交互保护、工作调度 | 会话/交互租约；当前有效帧 | 不实现Shell文件操作 |
| Layout / Presentation | 作者意图＋热度＋模板约束生成派生帧 | `PosterFrameSnapshot`、可见项差异 | 不将热度派生位置写回作者布局 |
| Geometry / InputRegions | 形状、内容槽、标题拖区、动作区与命中同源 | `RegionSet`＋坐标变换＋FrameEpoch | 模板不得直接操控HWND或抢占桌面输入 |
| Heat | 消费已确认打开事实；评分、衰减、新项保护 | `IHeatPolicy` → 现有ContentHeatProposal | 不操作窗口/文件，不把点击当成功 |
| Templates | 包验证、能力描述、资产解析、候选与回退 | `TemplateDescriptor`、受限场景描述 | 不接受任意C#/DLL/脚本；不污染其他海报 |
| AI | 受控上下文、Provider适配、生成/审阅计划 | 有版本、有目标、有允许操作集的ChangePlan | 不直接调用Shell、SQL或通用文件系统 |
| Persistence / OS Adapters | SQL、文件身份、OLE、Shell、日志、网络 | 实现应用层定义的窄端口 | 不反向依赖窗口或AI业务 |
| ReleaseUpdater | 分发、信任校验、安全停机、切换、健康门与回退 | 独立更新进程与版本清单 | 不当作模板加载器，不删除/回滚用户真实内容 |

“能力接入与验证”是共同规范＋各功能的类型化适配，不建议造一个能执行任意命令的万能插件总线。权威配置变化仍沿现有事务命令；短期热度只更新派生运行态；两者不能伪装成同一种数据库写操作。

### 3.1 状态必须分成四类

| 类别 | 所有者 | 是否持久化 | 新能力如何使用 |
| --- | --- | --- | --- |
| 作者/业务权威 | 状态库及命令服务 | 是 | 海报、关系、手动布局、选定模板与参数；按版本提交 |
| 操作事实/账本 | 内容用例及对应持久化端口 | 是 | 打开事实、文件交接/归还结果；不得重复执行副作用 |
| 可再生派生结果 | Heat / Layout / Template候选 | 可缓存，但非权威 | 排名、尺寸等级、布局快照；能由输入重建 |
| 运行资源 | 每海报会话/宿主 | 否 | HWND、命中、计时、位图、取消令牌、当前拖动 |

用明确的类型和所有者保持这四类边界，比单纯把代码分进更多文件夹更重要。

## 4. 建议目录：先原程序集内归组

下面是目标，不是本轮已迁移的目录。省略未变化文件；名称可在实施时根据实际边界调整。

```text
src/
  DesktopUp.Domain/
    Content/  Posters/  Layout/  Templates/
  DesktopUp.Application/
    Features/
      Content/      # Open、Rename、Transfer、Mapping、Return等用例
      Posters/      # 创建/删除、配置、选择与展示意图
      Heat/         # 事实消费、策略切换、提案输出
      Templates/    # 验证、预览、应用、回退
      AI/           # 上下文与ChangePlan准入（后续）
    Runtime/
      Sessions/  Scheduling/  Presentation/  Geometry/
    Ports/          # 真正跨模块的窄端口，不做杂物桶
  DesktopUp.App/
    Bootstrap/
    Views/          # Poster、ControlCenter、Tray
    Presenters/     # XAML增量映射与UI线程适配
    Hosting/        # WinUI场景和桌面会话宿主
  DesktopUp.Infrastructure/
    Persistence/    # SQLite、迁移、Repositories
    Files/          # 身份、防覆盖、受管存储
    External/       # Worker客户端、后续AI HTTP适配
    Packages/       # 模板/图标资产验证与读取
    Diagnostics/
  DesktopUp.Windows/
    Desktop/  Input/  DragDrop/  Shell/  Lifecycle/
  DesktopUp.ContentWorker/
    Protocol/  Shell/  Icons/
  DesktopUp.Updater/ # 直到应用更新阶段才建立
Modules/             # 可信内置能力；明确不是外部模板包目录
  Lamp/  Rain/
tests/
  DesktopUp.Contracts.Tests/   # 先按功能目录归组，不立即拆更多测试项目
  ...已有原生/会话夹具...
tools/
  review/             # 人工验收入口与探针
scripts/               # build/test/dev/package等少量稳定入口
packaging/              # 安装/卸载/发布清单
Docs/                   # 当前入口＋唯一任务记录，旧进度归档
```

不要一次“搬完全部文件＋改所有namespace＋重写行为”。先选一个纵向功能抽取；文件移动与逻辑修改分开提交。XAML生成代码、partial私有访问、Modules编译链接、打包来源清单、PowerShell相对路径都是实际迁移约束，必须同步处理。

代码约定建议：一处声明一个主要职责；公共API和异常/取消语义清晰；避免一行多语句与缩写布尔字段；注释解释约束和原因，不堆历史阶段；partial仅用于真正共用生命周期的UI实现。文件长度用于触发审查，不作为机械拆分指标。

## 5. 为四项后续能力铺路

### 5.1 热度模型接入与热替换

现状链：打开意图 → 有效关系 → 请求去重 → Worker交接 → 成功事实。真实消费/评分尚缺，但事实与布局提案两端已存在。

建议下一步只补中段：

1. 新增只读事实扫描端口；按稳定游标消费，统计更新与消费进度同事务提交。事件只负责唤醒，不是唯一可靠来源；重启可补算事实，**不能重放外部打开/文件移动**。
2. 默认策略继续遵循A§15已有权重、最近性/频率/新项保护、确定性同分顺序，不趁重构偷偷换评分语义。
3. 评分输入包含明确的时刻、统计快照和策略版本；输出沿用ContentHeatProposal，并补策略版本/事实水位等可解释来源。
4. 参数或内置策略选择热换：构造新不可变策略 → 影子计算 → 校验 → 安全点切换 → 只发布受影响海报。失败保留旧策略；旧异步结果按代次丢弃。
5. 手动模式、图钉和手动尺寸继续优先；每次从作者意图生成，避免反复缩放叠乘。
6. 沿A§15.3，打开后仅标记待更新，在下次展开/明确刷新等节奏发布，不能在点击后立刻把卡片挪走。图中的“安全点”只是必要条件，不代替该产品节奏。
7. 第一版不增加高频计时器、不做任意算法DLL热载入。若未来真的需要算法代码在线更换，再评估隔离进程/受限运行时和版本协议。

### 5.2 自定义风格、模板平台与异形海报

当前参数化模板和装饰场景是可复用起点，但不足以覆盖“AI即时生成任意外形”。

建议先分离四件事：外观Paint、内容布局槽LayoutSlots、形状Shape、交互区域InteractionRegions。圆形、异形和离散区域都从一个海报坐标系生成，输入和绘制共享同代快照：

- 视觉区域、可拖标题区、内容区、动作区和外部拖入区分别表达，不能仅凭某像素不透明就推断其业务角色。
- 离散区域先按单一PosterId/会话/坐标变换组织为RegionSet；是否需要多个HWND属于后续原生试验结果，暂不写死。
- 当前“可见海报内部不做普通点击穿透”的决定保留；异形之外/区域之间如何落到桌面，要与区域边界一致，不恢复已经取消的整框空白穿透方案。
- 形状/布局变化先构造候选，校验可用内容空间、DPI变换、拖放命中、窗口/后台层行为，再同时发布绘制和命中；不能先换画面后补命中。
- 模板包先采用受限声明＋资产，不绑定尚未确认的容器/扩展名；能力清单、契约版本、依赖、资源预算、路径/类型/压缩校验、上一有效版本必须具备。
- 不重启能改变的范围由宿主已有原语决定。新渲染原语/原生能力仍可能需要软件更新；“任意风格”不是执行任意代码的许可。
- 复用现有场景异常边界及基础收纳fallback；迁移Lamp/Rain时先包装既有实现，不同时重写动效。

### 5.3 AI 接入

AI 是计划提供者，不是新的状态权威。它只能看到经裁剪的当前目标与匿名内容信息；Key由后续当前用户安全凭据适配保存，无Key不联网。

链路：用户意图 → 最小上下文 → Provider → ChangePlan → schema/目标版本/能力校验 → 预览与所需批准 → 既有命令/模板服务。操作权限与预览免确认边界仍按A§18冻结；现有人工拖放无需额外确认，不代表AI获得文件搬运授权。

模型不能直接改XAML对象、文件路径、SQL、注册表或执行Shell。旧版本计划遇上拖动/删除/改名时应冲突返回或重新生成，不做最后写入覆盖。Provider超时、取消和坏输出不影响正常打开/拖放。

### 5.4 发布版本更新

必须把“数据/能力热换”和“应用更新”分开：

| 更新对象 | 建议首版方式 | 是否可不重启 |
| --- | --- | --- |
| 热度参数、内置策略选择 | 校验候选＋安全点切换 | 可以 |
| 受支持的模板声明/素材 | 临时实例＋原子激活＋旧版保留 | 可以，受宿主能力范围限制 |
| 新算法/渲染原语代码 | 正常应用更新；以后再评估隔离插件 | 不先承诺 |
| EXE/DLL、WinUI或原生依赖 | 独立更新器＋旁路安装＋受控重启 | 首版需要重启 |

现有安装器只做安装/卸载，并拒绝同版本直接覆盖；不是已实现升级器。建议先确定可信签名与版本清单、数据目录独立、版本化程序目录、更新独占租约、停机时文件任务收尾、备份和兼容窗口。健康门期间不允许新业务写入，成功后才开放；否则简单还原旧数据库可能丢掉新操作。

程序回退不能回滚/删除用户已搬动的真实文件。未知或破坏性schema变更须单独设计备份/降级策略；不能只画一个“回滚”框就假定有事务。

## 6. 如何真正做增量开发

下表保留原始阶段分解：A首批、B打开切片和C热度切片已实施并验收，不等于表中所有职责完成。当前继续原impl/architecture-foundation，按余项计划完成既有业务基础边界后再审查D准入；E/F/G仍为后续能力，不是本轮前置。

| 批次 | 最小交付 | 有意不做 | 影响面验证/退出条件 |
| --- | --- | --- | --- |
| A：边界整理 | 目录/命名约定、组装入口、探针发布隔离、消除明显旧注释 | 不重写业务，不全库改namespace | 同构建入口可运行；产品构建不依赖验收夹具；文件搬移与行为变更分开 |
| B：抽出内容用例 | 从一个真实打开/文件操作切片抽端口与执行服务；App只接请求/结果 | 不一次搬走所有账本和SQL；禁止临时双写 | 重复/取消/失败保持；相同输入结果等价；原安全原语不复制 |
| C：收敛海报运行边界 | SessionController、Presenter、Geometry/Input publication；复用稳定卡片 | 不同时制作异形模板 | 拖动/悬停让位/选择不闪烁；输入和画面同代；资源有唯一释放者 |
| D：V3-06热度最小闭环 | 成功事实消费→默认策略→现有提案端口，参数策略可切换 | 不先建通用插件市场 | 去重可重启；手工控制保持；交互中不跳位，失败回旧策略 |
| E：模板运行时 | 受限模板包、候选/回退、先常规后异形/离散RegionSet试验 | 不开放任意脚本或原生插件 | 坏包不影响基础收纳；切换/布局/命中共同生效 |
| F：AI计划接入 | Provider、脱敏、校验、预览、受控执行 | 不授予通用系统权限 | 旧计划冲突可解释；取消/坏输出不影响手工流程 |
| G：应用更新 | 独立更新器、签名、健康门、schema兼容/回退 | 不宣称二进制原地热换 | 用隔离安装验证失败恢复与用户数据保留；不重复整套内容人工 |

2026-10-16最新顺序：原先“热度前只做B/C直接需要的部分”的建议已被用户调整。先完成余项R1～R4所列现有业务用例、会话、呈现/命中及组装边界，再按明确准入标准决定进入真实热度；不要求先实现模板、AI或更新器。已验切片不重做，完整目标模块与基础架构出口分开记录。

每新增一个能力的固定动作只有：声明输入/输出和权限 → 实现纯策略/适配 → 在组合入口注册 → 接现有类型化用例/提案 → 定向验证失败与回退 → 集中人工（确有新体验才需要）。不每批叠加全部历史测试，不为每个方法制造新合同。

依赖检查建议复用现有测试项目做少量结构规则：Domain禁外部IO/平台依赖；Application不引用WinUI/SQLite实现；Presenter不持有SqliteStateStore；模块不能访问文件系统/窗口原语；生产包不包含探针入口。先记录例外清单，迁移一处减一处，不用新增大量空接口掩盖旧依赖。

## 7. 证据定位与审查边界

- [工程引用与模块编译](../../../Directory.Build.targets)：Modules/*按层编入现有程序集。
- [宿主组装](../../../src/DesktopUp.App/App.xaml.cs)：143～185附近可见具体存储、Worker和运行器接线。
- [文件操作耦合](../../../src/DesktopUp.App/PosterSpatialRuntime.Transfers.cs)：9～24附近具体存储与委托；[CrossPosterTransfer](../../../src/DesktopUp.Infrastructure/CrossPosterTransfer.cs)含用例编排与事务。
- [投影增量](../../../src/DesktopUp.Application/StateProjectionCoordinator.cs)：187～207附近ChangedPosterIds；[卡片差异呈现](../../../src/DesktopUp.App/PosterVisualWindow.Content.cs)：144～231。
- [真实打开](../../../src/DesktopUp.Application/Features/Content/Open/ContentActionService.cs)：B批已从ReferenceContentRuntime文件抽为独立执行服务；[成功事实](../../../src/DesktopUp.Infrastructure/Persistence/Content/SqliteContentOpenJournal.cs)：67～86，存储实现不变。图中固定提交仍指原审查基线。
- [Worker进程协议](../../../src/DesktopUp.Infrastructure/LocalContentWorkerClient.cs)、[Worker入口](../../../src/DesktopUp.ContentWorker/Program.cs)：有界标准输入输出/另有Shell UI模式，不访问业务数据库。
- [热度/模板提案](../../../src/DesktopUp.Domain/ContentLayoutProposals.cs)、[现有安全点](../../../src/DesktopUp.App/PosterVisualWindow.ContentProposals.cs)。
- [场景端口](../../../src/DesktopUp.App/Scenes/IPosterSceneInstance.cs)、[编译期注册](../../../src/DesktopUp.App/Scenes/SceneModuleRegistry.cs)、[场景异常边界](../../../src/DesktopUp.App/PosterVisualWindow.SceneHost.cs)。
- [现安装器](../../../packaging/preview/installer.nsi)：268附近已安装拒绝路径；没有据此推断自动升级已具备。
- 产品约束参考[A需求§15/17/18/19/24](../10_Requirements/DESKTOPUP_V2_REQUIREMENTS_PROFILE_V1.md)与[B既有模块目录](DESKTOPUP_V2_V3_MODULE_CATALOG_V1.md)。旧DEMO、共享关系、空态提示等历史文字如与后续明确决定冲突，不作为本轮回退依据。

此次是聚焦入口、依赖、打开/持久化、布局/输入、场景与发布的静态审查，不是逐行安全审计，也没有重新测量CPU/GPU/内存。已验功能不被本报告撤销；自启动真实登录暂缓未验继续携带。目标API命名/包格式/原生异形承载方式都不是冻结合同。

## 8. 图表交付验证

由Archify 2.17生成。五张图各自通过showcase 9/9检查，0组合错误、0警告；现状图16处来源在固定提交核验。使用本机Edge Chromium完成1440×900、1600×1000、1920×1080、2048×1320检查，均无横向或纵向溢出；明暗两色已截图，另作图像目视审查。结构校验、浏览器证据、目视审查分别记录，不等同产品功能验收。

完整规格/HTML SHA-256、字节数、浏览器状态与目视范围见 [交付回执](ArchitectureReview/delivery-receipt.json)。图表源可重生成，生成物不更改产品或发布线。
