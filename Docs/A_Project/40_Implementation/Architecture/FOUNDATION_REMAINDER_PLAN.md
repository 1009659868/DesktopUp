# 基础架构余项实施清单与退出标准

2026-10-16。状态：**基础架构整改 IN PROGRESS；R1 文件动作、拖放两片均 USER PASS / 切片收口；R1 其余入口及 R2～R4 未完成；真实热度准入 HOLD。**

工作分支：`impl/architecture-foundation`。R1 源码/测试/图源检查点 `b3bfdc6` 已推送；本次登记用户确认的两组拖放验收并同步交接，不修改产品、不合入 develop。拉取后的接续顺序见第10节；此前 impl/main 安装验证被迁移 SQL 换行哈希兼容问题阻断，发布同步未完成、问题未修复，单独见[发布记录](../V3-04/PREVIEW_INSTALLER_DELIVERY.md#2026-10-16-发布同步阻断与现场保全)。不把此发布阻断混作 R1 拖放失败，也不因拖放通过补记自启 PASS。

## 1. 总目标与已完成范围

本轮目标是让**已有业务**具备[目标架构](../../20_Architecture/ArchitectureReview/target.architecture.json)中薄界面、应用用例、稳定海报内核、平台/存储适配的职责边界，再决定进入真实热度。不是先完成 AI、模板平台和更新器，也不是仅为热度抽出一个类就宣布架构整体完成。

已通过成果继续保留：

- A：真实打开相关目录归组、Bootstrap 入口、产品与验收代码编译隔离。
- B：打开/定位执行服务退出视图，成功打开事实沿原账本完成。
- C 的热度切片：ContentHeatSession 管理提案缓冲和派生状态；收展图标闪烁已修复并人工通过。

原 C 目标包含会话控制、Presenter 和几何/输入发布。此前 C CLOSED 只适用于热度切片及闪烁修订，**不关闭原 C 的其余边界，也不关闭整体架构整改**。后续记录分别标识“切片已验”“基础架构出口”“后续能力完成”，不能互相替代。

本计划是余项状态与证据的唯一详细入口；[原始审查](../../20_Architecture/ARCHITECTURE_EVOLUTION_REVIEW_2026-10-16.md)保留目标依据，[旧 C 记录](C_HEAT_RUNTIME.md)保留已验事实。当前图只反映现状，不能用图表校验通过代替代码边界通过。

## 2. 目标边界与待办对应

| 目标边界 | 当前缺口及代码入口 | 必须完成的余项 |
| --- | --- | --- |
| 薄界面层 | FileActions及拖放生产入口已经应用端口/用例；窗口不再自建文件业务或使用Infra回执；其余窗口/中心业务仍待迁出 | R1 迁出内容操作编排；R2/R3 迁出会话策略和呈现状态。UI 保留控件、菜单/选择器、输入采集及反馈 |
| 应用用例层 | FileActions/Incoming/NativeExit已接应用用例；ContentReturn、映射同步及中心等仍与具体存储/业务服务耦合 | R1 将现有内容、配置、存储位置及生命周期入口收敛到类型化用例；沿用已有命令和事务 |
| 稳定海报内核 | ContentProposals 的交互保护/调度、Content.cs 的作者/草稿/派生/卡片状态仍共享窗口私有字段 | R2 明确会话与操作所有者；R3 明确派生帧、绘制与命中发布边界 |
| 存储与平台适配 | 业务运行器持有 SqliteStateStore；ContentWorker 链接编译 Infrastructure 源码 | R1 分离业务编排和原子持久化；R4 收清组装及 Worker 隐式依赖 |
| 能力接入的现有部分 | 已有热度/主题提案、版本和交互安全点，但仍由窗口串接 | R2/R3 形成可用的类型化提案入口与同代发布；不先造通用插件总线或空 AI 接口 |

保留六个生产程序集，不强制一个逻辑模块对应一个新项目。App 在组合根引用 Infrastructure/Windows 是合法依赖；要消除的是**业务运行代码和视图**持有具体存储、直接执行文件业务，并非机械禁止 App 的所有实现引用。

## 3. 实施顺序与完成清单

R1 → R2 → R3 → R4 → 集中验收与热度准入审查。它们是内部实施批，不要求用户逐小步骤验收。文件动作和拖放切片均已验收；下一片覆盖映射创建/同步，再覆盖其余既有内容与生命周期入口。未覆盖项继续留在 R1，不提前整体关单。

### R1 现有业务用例与存储解耦

**实施清单**

- [ ] 盘点入口到副作用的调用链：普通/映射海报、控制中心、托盘和卸载模式都纳入；记录相同用例的重复编排。
- [x] USER PASS：新建、原生新建回执登记、重命名、真实删除、属性/Win 菜单：UI 只采集意图与呈现反馈，目标关系/身份校验和执行交给应用用例。行内命名、新建后进入命名、系统必要提示保持。用户本轮确认集中验证通过，首片收口，不重跑其人工清单。
- [ ] 外部拖入、原生拖出、跨海报交接、映射与最小外部同步：迁出业务顺序、来源/目标见证、结果登记和状态刷新编排；原生 OLE、跟手图像及 Shell 落点继续由原适配器完成。
  - [x] 外部受管拖入、原生拖出观察、跨海报交接、映射拖入：CODE/AUTO 与两组集中人工均 PASS，见第9节；不重复安排这两组人工。
  - 映射创建、周期扫描/最小外部同步与启动收尾仍待迁出；不能因拖放切片通过而勾选整项。
- [ ] 删除海报/卸载归还、启动收尾、默认收纳位置与迁移：复用相同用例入口；来源不可用回桌面、映射目录不动、取消停止及文件保留规则不变，不增加恢复任务流程。
- [ ] 配置/布局/图标与海报管理入口复用已有 IStateCommandService/StateProjectionCoordinator 等命令投影路径；补齐仍绕入具体实现的入口，不再新建第二套配置服务或事实库。
- [ ] 将持久化所需结果/请求类型放回应用边界，禁止 UI 或 Application 依赖 Infrastructure 中的业务 DTO；具体 SQL、文件身份、防覆盖、Shell 执行留适配层。

**拆分方法**

以真实用例所需的读取、准备、原子提交和结果观察定义窄端口。现有账本、事务、CAS、执行租约和故障收尾是必须保留的实现，不改 schema 或语义。不能把一个原子事务拆成数次独立写，也不能把“任意 SQL/回调”塞进通用 Repository 逃避分层。若发现必须改变事务或文件安全语义，先单独列出变化，不混入结构整理。

**退出标准**

1. 在本计划所列生产入口中，视图/运行器不再持有 SqliteStateStore、new 具体文件业务服务或自行调用 LocalTransferFiles 执行业务；Bootstrap 可组装实现。
2. 应用用例在无 WinUI 窗口条件下可执行，依赖请求/结果和窄端口；适配器不回调窗口私有状态。取消、版本和操作身份显式传递。
3. 不是仅包装原 Infrastructure 大服务：业务执行次序与决策有明确应用层所有者，Infrastructure 保留原子持久化及系统操作实现；源码调用链与生产接线都能证明这一点。
4. 同一操作只有一套账本、一次副作用执行和既有提交语义。重复回调、提交后回应丢失、取消/退出不重放已完成的文件操作。

**定向验证**

只选本次迁出用例对应的既有 ContentCreation/Deletion/NativeAction、DirectoryMapping、CrossPosterTransfer、PosterContentReturn、StorageRelocation 等相关用例，不一次跑齐整个列表。首次纵向切片用无视图服务执行及实际 UI 接线证明边界；事务迁移只补受影响的失败/取消/重启点，不给每个包装方法新增测试。

### R2 海报会话与交互状态归属

**实施清单**

- [ ] 在现有 PosterSessionRegistry、WinUiPosterSession、PosterSpatialRuntime、ContentHeatSession 基础上，划清应用级协调、单海报会话、可重建窗口/呈现资源三个生命周期。
- [ ] 从窗口迁出“是否可以执行/何时可以发布”的策略及状态所有权，收敛编辑草稿、内部拖动、外部悬停/交接、框选、菜单/命名、收展等交互保护。视图保留原生事件捕获和控件事实，通过明确的开始/结束通知或作用域租约交接。
- [ ] 提案缓冲、来源版本、会话代次、待发布工作和取消统一归属；复用 ContentHeatSession，不另复制热度缓冲。主题提案沿同一会话边界接线，但不制作模板平台。
- [ ] Dispatcher、时钟与原生捕获继续由宿主适配；会话决定调度时机，宿主执行调度。多海报/中心操作互斥在应用协调层，不放进单张海报的全局静态状态。
- [ ] 明确关闭、重建、挂起、恢复时哪些状态保留、哪些资源释放；在途文件用例的事实完成不因视图消失而丢失。

**退出标准**

1. 每类状态只有一个权威所有者，窗口不再通过访问其他 partial 的私有字段拼装业务保护条件；新能力接入不需要继续新增一组窗口 Busy 字段。
2. 会话策略和热度/主题提案处理可在无 XAML/SQLite/原生 HWND 环境验证；平台工作通过注入适配执行，不能移到 Application 后又引用 WinUI。
3. 迟到回调经操作/会话代次和来源见证拒绝；退出不重新激活关闭会话，取消不会误撤已确认事实。
4. 原交互保护、自动收展、中心草稿优先和多海报互不吞操作保持；不能为了解耦变更手势或新增确认框。

**定向验证**

复用 ContentHeatSession、已有手势/自动收展/会话生命周期测试中受影响的条目；只补单一所有者和迟到回调缺口。用现有隔离夹具观察保护期不发布、解除后仅最新有效候选生效和关闭后不再调度。

### R3 增量呈现与同代命中边界

**实施清单**

- [ ] 将作者布局、草稿、热度派生、视口与空间校验的组合放入纯布局/会话边界；输出不可变呈现快照。沿用 ContentLayoutSnapshot、ContentSpacePublication 和现有求解器，不新造第二套布局引擎。
- [ ] 将 XAML 卡片树、CardPaint 差异应用、选中/命名的视觉映射收敛为 Presenter；它接收快照/差异，发出类型化用户意图，不执行文件业务或读库。
- [ ] 图像需求、缓存、解码图像和订阅具有明确生命周期所有者；Presenter 持有 UI 资源，资源提供方负责加载。保持稳定卡片 identity、有界缓存、来源代次检查和收起保留暖图。
- [ ] 布局、裁剪、标题/动作区、内容命中和拖入区由同一代快照驱动；平台适配只转换坐标和应用区域。发布失败不能让新画面配旧命中。
- [ ] Lamp/Rain 与公共背景、标题继续走已有场景端口；只隔离现有边界，不重写动效、不引入异形或离散窗口。

**退出标准**

1. 窗口宿主不再同时承担业务状态派生、卡片差异算法和输入几何决策；Presenter 不持有 SqliteStateStore、文件用例实现或领域状态的第二份权威副本。
2. 同一帧身份绑定绘制和命中，候选失败沿用上一有效发布状态；当前场景布局、拖放和窗口边界不变。
3. 无关 Item 对象/暖位图在单项改动与收展中保持；不能因抽取恢复全量重建、无界缓存或重复加载。
4. 可用既有提案入口替换一个合法候选，不需修改窗口内部算法；不以虚构评分器或新模板功能证明此边界。

**定向验证**

复用 ContentSpacePublication、ContentLayoutProposal、IncrementalContentRaster 和 ContentAssetCache 的受影响项；复用同帧/增量/收展夹具，不另建一套探针。只有确实改变的路径需要重新运行；旧候选的通过不能替代修改后的呈现证据。

### R4 组装与平台依赖收口

**实施清单**

- [ ] Bootstrap 统一组装用例、会话、Presenter 和系统适配，明确启动/停止顺序；AppLifetime 只编排生命周期，不包含单次文件业务。
- [ ] 清除 R1～R3 生产入口的具体数据库、文件执行、Shell 客户端构造旁路；WinUI 宿主保留线程、原生窗口和系统回调适配。
- [ ] 处理 ContentWorker 链接 LocalContentReferenceProbe 的隐式跨项目依赖：按实际系统探测职责落位，复用协议而非复制实现，不将 OS 探测塞入纯 Domain/Application。
- [ ] 补齐职责目录/源码入口；纯移动单列清单，构造依赖和行为变化另列。命名空间/XAML资源不做无关全量改名。
- [ ] 维护少量可执行依赖规则，检查完整生产 partial 集合和实际调用链，不再只检查几个主文件。合法组合根/平台适配例外精确到文件与职责，不放行整个 App。
- [ ] 更新“现状与目标差异”清单。现有能力所需边界不得用“以后按需抽取”结项；后续新能力未制作仍明确标识。

**退出标准**

1. R1～R3 的依赖方向在实际生产编译和接线中成立；没有 ServiceLocator、万能总线、循环引用、重复账本或空接口掩盖原耦合。
2. 普通产品不编入独立验收执行器；Review 模式仅增加验收内容，不能靠不同业务接线使测试通过。构建/打包入口不因目录变化缺文件。
3. 原代码及资源释放路径可追踪到唯一所有者，旧路径已退役或有精确合法用途；不能新旧路径同时写状态。
4. 当前图/源码入口/本清单一致。绘图工具故障单列，不冒充架构失败，也不拿渲染通过代替代码验收。

**定向验证**

复用 ArchitectureTests 与 check-architecture.ps1；修正其覆盖范围而不是机械增加每文件断言。目录/编译接线有变化时验证正常产品构建与必要 Review 编译集合；不默认再跑安装卸载矩阵或全量合同。

## 4. 进入真实热度的准入标准

**当前结论：HOLD。** 原“C 热度切片收口后直接进入 D/V3-06”的顺序由本计划替代。以下条件全部满足后，才在交接中明确给出“可进入真实热度”；任一项未达到仍保持 HOLD，不因分支合并或局部人工通过自动放行。

- [ ] R1 现有用例的生产调用链已收敛，视图/运行器不存在本清单范围内的具体存储与文件业务旁路。
- [ ] R2 会话状态、交互保护、调度与关闭边界有明确所有者，能无 UI 验证。
- [ ] R3 Presenter 与同代绘制/命中发布已接真实生产路径；稳定 Item、暖图及缓存边界保持。
- [ ] R4 依赖、组合根、Worker 边界及产品/验收编译通过；剩余例外逐项说明且不破坏上述基础职责。
- [ ] 最终代码的定向证据与一次集中人工结论齐备，影响范围无未关闭回归；给出目标边界到实际文件/端口/测试的对应表。
- [ ] 证明热度可以只新增“事实扫描/消费统计、纯策略、提案输出及组装接线”，不再让窗口查询 SQLite、持有评分状态或执行文件动作。使用现有提案注入验证此接点，不提前实现真实评分。

准入通过后，才进入成功事实幂等消费/稳定游标 → 默认评分与新项保护 → 原提案入口 → 既定发布节奏。真实热度的“打开只标记待更新，下次展开/明确刷新发布”是后续产品行为，不与本次结构整理混改。

本准入只证明**现有业务的基础架构边界**。目标图中的动态模板、AI、独立更新器仍为未实现；全目标架构继续保持分阶段状态，不能因此宣布所有目标模块完成。

## 5. 精简验证与集中人工

文件动作及拖放切片的定向范围分别见第8、9节；本次仅登记人工结论与同步交接，不重跑构建、合同测试或进程矩阵。发布线的独立安装失败另记，不扩大为本次架构人工清单。

后续每次实现先写一句影响面，再选对应测试。R1～R4 的测试清单是选取范围，不是每批全部重跑；复用同一最终源码的有效结果，失败只扩大相关邻域。不得以测试数量、移动文件数、类名数量或新增接口数作为完成标准。

集中人工默认一轮、两个自然体验点，最终按实际改动缩减：

1. **文件用例连续操作**：隔离样例完成新建即命名、重命名/打开、一次跨海报或拖出及删除海报归还中受影响的路径；检查没有新增确认、重复执行或文件丢失。不把所有历史文件场景重新列一遍。
2. **海报会话与呈现**：内部移动/外部悬停让位、框选、收展及中心切换的一次连续使用；检查无关 Item 不闪、暖图保留、命中正确、关闭后不复活。无需人工验证尚未实现的评分。

只有确有不同风险且自动检查不足时才追加体验点，并说明原因。真实文件、旧配置/候选、安装/自启设置保持；样例默认在本批 out 隔离目录，外部落点须记录并收尾。原自启登录、长测、多屏/DPI、真实满盘等携带项不补 PASS，也不自动扩成本批前置。

## 6. 执行状态与下一动作

| 范围 | 当前状态 | 完成证据 |
| --- | --- | --- |
| A、B 打开切片、C 热度切片及闪烁修订 | 已验收并本地集成 | A/B/C 既有记录，不重复验收 |
| 本轮余项盘点与退出标准 | 已记录 | 本计划、代码入口核对、分支/资料保全 |
| R1 现有业务用例与存储解耦 | 文件动作、拖放两片 USER PASS；其余入口未完成 | 第8～9节；不关闭 R1 总项 |
| R2 海报会话与交互状态 | 待实施 | 同上 |
| R3 增量呈现与同代命中 | 待实施 | 同上 |
| R4 组装与平台依赖 | 待实施 | 同上 |
| 真实热度准入 | HOLD | 等待第4节全部条件 |

下一动作：继续 R1 的映射创建、周期扫描/最小外部同步及其启动接线，再收清归还/卸载、配置与收纳位置入口；R2/R3/R4 和真实热度不提前进入。不按方法或内部子步骤安排人工；此次授权只保存并推送架构进度，不等于开始下一批或合入 develop。接续要点见第10节。

前一轮计划阶段切换前将涉及分支差异的两份记录备份到 out/architecture-resume-20261016-01，切换后恢复并核对哈希一致；其他未提交图源/资料原位保留。该计划阶段没有产品源码、用户文件或系统设置变化；当前 R1 实现见第8节。

前一轮计划阶段检查：新增/修改 Markdown 中53个本地文件链接均可解析，git diff --check通过；与impl/develop比较，产品/模块/脚本/打包/测试范围仅src/README.md有文档变化，生产源码与构建入口未变。没有执行产品构建或行为测试。

## 7. 现有代码锚点

以下是余项盘点入口；本次已实施的文件动作入口/端口见第8节，其余不据此标记完成。

- [打开用例](../../../../src/DesktopUp.Application/Features/Content/Open/ContentActionService.cs)与[热度会话](../../../../src/DesktopUp.Application/Features/Content/Layout/ContentHeatSession.cs)：已完成切片。
- [窗口文件动作](../../../../src/DesktopUp.App/PosterVisualWindow.FileActions.cs)、[运行器文件用例](../../../../src/DesktopUp.App/PosterSpatialRuntime.FileActions.cs)、[跨海报编排](../../../../src/DesktopUp.Application/Features/Content/Transfers/CrossPosterTransferUseCase.cs)：R1已实施切片。
- [收纳位置](../../../../src/DesktopUp.Infrastructure/ManagedStorageService.cs)、[内容归还](../../../../src/DesktopUp.Infrastructure/PosterContentReturn.cs)、[卸载入口](../../../../src/DesktopUp.App/App.Uninstall.cs)：R1 生命周期。
- [会话注册](../../../../src/DesktopUp.Application/PosterSessionRegistry.cs)、[WinUI 会话](../../../../src/DesktopUp.App/WinUiPosterSession.cs)、[窗口保护/提案调度](../../../../src/DesktopUp.App/PosterVisualWindow.ContentProposals.cs)：R2。
- [卡片与布局状态](../../../../src/DesktopUp.App/PosterVisualWindow.Content.cs)、[空间发布](../../../../src/DesktopUp.App/PosterVisualWindow.ContentSpace.cs)、[图像生命周期](../../../../src/DesktopUp.App/PosterVisualWindow.ContentAssets.cs)：R3。
- [组装](../../../../src/DesktopUp.App/Bootstrap/App.Services.cs)、[Worker 项目](../../../../src/DesktopUp.ContentWorker/DesktopUp.ContentWorker.csproj)、[现有依赖测试](../../../../tests/DesktopUp.Contracts.Tests/ArchitectureTests.cs)：R4。

## 8. R1 文件动作纵向切片 2026-10-16

状态：**USER PASS，首片收口；R1 整体仍 IN PROGRESS，真实热度 HOLD。** 用户本轮确认集中验证通过；本批保持行内命名、新建即命名、原生属性/Win 菜单、实际删除、系统必要提示和单一归属规则。没有新增确认框、schema、第二套状态库或插件接口。

### 实际调用边界

| 调用链 | Application 所有者 | Infrastructure 保留职责 |
| --- | --- | --- |
| 新建与默认名称 | ContentCreationUseCase：名称/类型验证、默认名冲突选择、映射/受管分流、准备→完成→收尾、失败反馈 | SqliteContentCreationStorage：现有执行租约、版本/保护检查、生成暂存对象与持久意图准备；复用 AutomaticTransferCompletion；仅身份/字节仍匹配的自建对象可清理 |
| 重命名与启动观察 | ContentRenameUseCase：目标关系、改名意图、执行/回应丢失/有限回退、只观察不重放；ContentFileActionPlans.Rename：原关系更新规则 | SqliteContentRenameJournal：原 content_renames、CAS、索引＋观察＋关闭回执同一事务；LocalContentActionFiles：文件身份、目录锁、精确改名 |
| 真实删除 | ContentDeletionUseCase：父子项排斥、逐项执行、取消/部分成功、释放执行租约后观察；不自动重试 | SqliteContentDeletionStorage：原 NativeShellExitJournal 授权及真实结果观察；ShellDirectoryTransfer 仍负责系统删除、回收站/系统必要提示 |
| 属性/Win 菜单与新建回执 | ContentFileActionService：映射/关系见证、身份、一次 Shell 调用、已创建事实登记与失败反馈；ContentFileActionPlans.ShellCreated：单一归属及幂等登记策略 | ShellUiClient 原独立 STA 辅助进程与协议不变；ShellCreatedContentService 保留文件锁、身份/保护检查、映射原子同步和登记提交 |

窗口保留控件、Popup、坐标采集与反馈；PosterSpatialRuntime.FileActions 只经 IContentFileActionService 接线，保留原宿主调度、忙态和投影通知。**已接受的 Shell 新建事实即使窗口消失也登记并刷新投影**；新建后的行内编辑失败不删除文件、不重放系统命令。忙态/会话所有权的进一步迁移属于 R2，本批不把它伪装成已完成。

组合根在 App/Bootstrap/App.ContentServices.cs 组装真实端口。NewContentLocation、NewContentKind、RegisteredManagedStorage、MappedNewContent、ContentRenameIntent、删除结果及 Shell 删除接口回到 Application；UI/用例不引用 Infrastructure DTO。原 ContentCreationService/ContentRenameService/ContentDeletionService 仅保留已有夹具/启动入口兼容转接，不保留第二份业务实现。

独立移动项：CreateMappedPoster 方法原文移至 PosterSpatialRuntime.MappedPosterCreation.cs，方法体未改；映射海报创建仍依赖具体存储，明确留待 R1 余项。其余代码为结构提取/接线，不算“纯移动”。后续若获 Git 授权，移动与行为/结构变更分别提交。

### 定向证据与边界

- 最终定向：108/108，通过范围为 ContentCreationTests、ContentDeletionTests、ContentNativeActionTests、ShellNewMenuTests、DirectoryMappingTests、ContentFileActionUseCaseTests，以及 ArchitectureTests 的核心依赖与本片入口检查。TRX：out/architecture-r1/tests/r1-file-actions-final.trx。没有执行累计全量。
- 复用既有失败覆盖：生成暂存内容被外改/提交失败、改名冲突/对象替换/回退/重启观察、删除取消/假成功/部分成功/登记失败、Shell 新建幂等/失效身份/映射登记。新增7例只验证无窗口、无数据库/文件 I/O 的执行顺序、取消及迟到成功，不按每个方法增设合同。
- 正常产品、Review、夹具 Release 构建均0警告/0错误。编译集合：正常100源/0验收源；Review166源/66验收源；Preview 与 Review 互斥通过。真实运行入口只使用正常产品，不包含验收执行器。
- 实际 WinUI 行内控件冒烟：复用 VerifyInlineNamingForProbe，新建即命名、取消命名保留默认对象、改名冲突仍可编辑、离开编辑提交均通过。仅为控件回调/真实生产服务接线，不称原生键鼠验收；没有顺带跑旧拖放套件。证据：out/drop-d3/architecture-r1-probe01/profile/logs/desktopup.jsonl，contract.r1_file_actions_passed，PID26004退出 activeResources=0。
- 当前环境日志 isElevated=true，沿用本机环境且未改 UAC/token；不把本批结论当成普通权限、UIPI或其他设备认证。真实登录自启、长测、多屏/DPI和真实满盘仍按原记录未验。

### 集中人工候选

- 入口：[start-architecture-file-actions-review.ps1](../../../../scripts/start-architecture-file-actions-review.ps1)；Open/Prepare/Status 为人工包，Probe 为独立回调冒烟。
- 正常产品：out/architecture-r1/product01；当时启动 PID6728，现已退出。配置及全部样例：out/drop-d3/architecture-r1-review01；A/B 普通收纳、C/D 映射目录。旧 C 包、配置及收纳文件原位保留。
- 启动器绑定 App/Application/Infrastructure 三份产物哈希，拒绝混用重建包。DesktopUp.dll：7AD178F8E36476576668DC384A223C6C368712B91C6A150C468E4B72DE98669B。
- 没有安装、启动项或桌面样例变化；自建文件和自动验证证据均在 out。人工删除只操作本批新建样例。

以下两组已由用户确认通过，不重复安排：

1. A 普通收纳空白处新建文本文档，直接命名→再次重命名→删除；原文件和卡片同步，无新增应用确认框，系统必要提示保留。
2. 检查 A 文件的属性/Win 菜单，以及 C 映射空白区的目录属性/Win 菜单；从 C 的 Win 菜单新建后可直接命名，真实对象仍在 mapping-C，未移入普通收纳库。

### 下一实施范围

本片通过不关闭整体 R1。继续迁出外部拖入/原生拖出/跨海报、映射同步和归还/卸载/位置迁移/配置的既有编排；保留当前安全执行器，不向窗口或具体存储新增热度功能。之后才是 R2 会话、R3 Presenter 与同代命中、R4 组合根/Worker 依赖；满足第4节全部出口才审查真实热度准入。

## 9. R1 拖放用例切片 2026-10-16

状态：**CODE/AUTO PASS、USER PASS / 本切片 CLOSED**。用户已明确确认下述两组验收符合预期；源码检查点 `b3bfdc6` 已推送，本次另同步验收记录，不合入 develop。没有改变手势、Windows落点、单一归属、原路归还规则；不新增确认框或恢复任务。

### 边界与保留项

| 入口 | Application 所有者 | 原适配器职责 |
| --- | --- | --- |
| 外部→普通海报 | ManagedIngressUseCase：逐项准备、晚到准入检查、先记账再执行、部分成功/取消、事实发布与后续落点推进 | SqliteManagedIngressStorage：冻结现有预览；原SqliteTransferJournal和AutomaticTransferCompletion完成事务/文件安全执行 |
| 外部→映射海报 | MappedIngressUseCase：执行作用域、逐项Shell调用、结果登记、释放后观察/发布 | MappedDirectoryIngress实现窄attempt端口：身份/容量/保护检查、目录锁、原账本与原子目录同步 |
| 海报→海报 | CrossPosterTransferUseCase：先核对既有回执、逐项交接、同目录仅换归属、登记结果→提交、取消及落点推进 | CrossPosterTransfer实现窄attempt端口：执行租约、读集/路径见证、原有原子Commit和只观察Reconcile |
| 海报→外部 | NativeShellExitUseCase：先记录意图、区分交接/返回/外部结果、有限观察、仅原失效卡片松手后确认移除 | NativeShellExitJournal保留原身份观察、回执及移除事务；原NativeShellDragSource继续独占OLE/系统落点 |

[用例目录](../../../../src/DesktopUp.Application/Features/Content/Transfers)、[端口/回执](../../../../src/DesktopUp.Application/Ports/Content/ContentTransferPorts.cs)、[组装](../../../../src/DesktopUp.App/Bootstrap/App.ContentServices.cs)为生产调用链。运行器Incoming/NativeExit及窗口Transfers不再引用Infrastructure DTO、具体存储或LocalTransferFiles；旧CrossPosterTransfer.Execute / MappedDirectoryIngress.Execute只兼容转接到同一用例，没有保留第二份循环实现。

宿主仍拥有UI线程、几何/窗口存活事实、投影发布、原忙态和源拖动结束前的同步OLE交接标记。普通拖入在调用方同步上下文检查交互，文件工作继续在后台；跨海报/映射的Shell回调只使用线程安全的取消事实和持久读集，不读取XAML窗口。会话归属的进一步抽取留在R2，未提前标完成。

原CrossPoster Commit/Reconcile、NativeShellExitJournal从MigrationSql起的既有实现、ShellDirectoryTransfer原生执行体、映射ValidateTree逐段与改前比对一致（仅换行归一化）。账本JSON字段、迁移/schema、执行/观察租约与原子事务不变。本轮是结构抽取和接线，没有纯文件移动或功能行为修改。

仍未覆盖：映射海报创建与周期扫描、最小外部同步、启动收尾、归还/卸载/收纳位置及配置入口。PosterSpatialRuntime.Transfers/Outgoing旧确定性路径目前只有Review调用者，仍待R4去除产品编译旁路；本片没有把这些例外包装成整体解耦完成。

### 精简验证与实际运行

- 定向75/75：CrossPosterTransfer、MappedDirectoryIngress、NativeShellExit、IncomingTransferIntegration、DirectTransferPreparation既有受影响测试65项；新增7项无UI/SQLite/文件I/O用例检查，以及核心依赖2项、拖放入口依赖1项。最终TRX：out/architecture-r1-transfers/tests/r1-transfer-final.trx。未执行累计全量。
- 新增检查仅覆盖晚到准入拒绝、不撤销已提交事实、非外部结束不观察/不弹窗、NONE结果仍核实、仅原失效卡片可确认移除、取消带已完成结果先登记再释放/观察。
- 正常产品、Review、SessionFixture均Release构建通过，0警告/0错误；编译集合正常101源/0验收源，Review167源/66验收源，Preview互斥通过。
- 复用IncomingProbe及CrossPosterProbe，新增早退参数--r1-transfers-probe，只跑本片：4项普通拖入→排队跨海报交接→模拟外部移动后的原生结束观察→实际Shell映射拖入。独立进程PID41976，contract.r1_transfers_passed，退出activeResources=0。证据：out/drop-d3/architecture-r1-transfers-probe01/profile/logs/desktopup.jsonl。
- 上述为真实生产接线/窗口回调冒烟，nativeMouse=false；外部拖出的文件移动由隔离夹具模拟，映射拖入实际调用Shell。不冒充原生鼠标、桌面落点或视觉验收。本机仍高完整性环境，未改UAC/token；未验环境、自启登录、长测、满盘继续携带。
- 所有新增样例、搬移目标及证据在本批out内；不使用桌面/真实用户文件，不改安装/启动项。旧文件动作包及配置原位保留。

### 集中人工（仅两组）

已验候选当时 PID11416，用户随后已正常退出；正常产品out/architecture-r1-transfers/product01不编入Review执行器。入口：[start-architecture-transfer-review.ps1](../../../../scripts/start-architecture-transfer-review.ps1)，配置/样例out/drop-d3/architecture-r1-transfers-review01。启动器绑定App/Application/Infrastructure哈希，拒绝混用重建包；DesktopUp.dll：650F05A8C1B03CEA2EE76CB3B583CC9BA588DBA06A50F8A41B94DB9AEDA73D26。

1. **普通收纳连续拖放**：incoming-files中的“外部拖入.txt”→A空白→B空白→outgoing-target文件夹。应一次按住完成、图标跟手、落点/让位保持、文件和卡片无重复；有效对象无新增应用确认。
2. **映射与取消**：incoming-files中的mapping-incoming.txt→C空白，核对真实文件在mapping-C；C→A，核对来源卡片消失；再拖动一项按Esc取消，应文件/卡片保持且随后可正常拖动。

**2026-10-16 用户验收：两组均 USER PASS。** 第1组图标跟手、落点与让位正常，文件/卡片不重复，无新增确认框；第2组文件进入 mapping-C，C→A 正常，Esc 取消保持文件/卡片且随后能继续拖动。此结论来自用户实际操作，不将此前 nativeMouse=false 的自动冒烟改写为原生鼠标测试。仅关闭本切片，未验设备/环境和自启登录照常携带。

## 10. 拉取后的接续入口

本次同步只包含开发源码、测试/夹具、计划、图源与验收记录；out 候选、数据库、日志、安装包及收纳文件不上传。源码检查点为 b3bfdc6，本段验收/交接由其后的文档提交补齐；以同名远端分支最新 HEAD 为准。

1. 在实际 .NET/WinUI 仓库检查 `git status --short --branch`，确认无未保存改动后 fetch，切换 `impl/architecture-foundation` 并 `git pull --ff-only origin impl/architecture-foundation`。不要误用旧 DesktopUp/Tauri 仓库，也不要从尚无 R1 的 develop 重新开工。
2. 阅读本计划第6、8、9节及[当前交接](../V3-04/HANDOFF.md)、[源码职责](../../../../src/README.md)。下一片从 `PosterSpatialRuntime.MappedPosterCreation.cs`、`App.Transfers.cs`、`ManagedContentSynchronizer` / `ManagedContentMonitor` 的创建→扫描→结果发布链推进；沿用现有窄端口、原子提交和观察规则，不复制第二套业务循环。
3. 映射/同步完成后，继续 R1 归还/卸载、收纳位置迁移、配置/海报管理入口；然后 R2 会话、R3 呈现/同代命中、R4 组装/Worker。真实热度仍按第4节 HOLD，AI/模板平台/更新器不是当前前置实现。
4. 换机首次使用 `scripts/setup.ps1` 准备项目工具链；需要构建时使用 `scripts/build.ps1 -Configuration Release -OutputDirectory out/architecture-next/product01`（选择未使用的输出目录）。没有缓存时不要直接加 Offline，不要依赖系统 dotnet 版本。
5. 旧 review 脚本绑定历史候选路径/哈希，只在原候选仍存在时用于重开；新拉取没有 out 是正常状态。下一批按实际改动重建独立候选/夹具并绑定新哈希，不能改旧哈希冒充已验二进制，也不需要为“拉取接续”重跑两组已通过人工。测试仍按影响选择。
6. 发布线阻断单列保留，尚未获实施修复结论；不得重置旧配置或直接改迁移账本使校验通过。本次不推进该修复、不更新 main、不把发布精简反向合入开发线；确需继续发布验证时先处理[阻断记录](../V3-04/PREVIEW_INSTALLER_DELIVERY.md#2026-10-16-发布同步阻断与现场保全)。
