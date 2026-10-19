# 基础架构余项实施清单与退出标准

2026-10-19。状态：**R1～R4 USER PASS / CLOSED；现有业务基础架构出口通过，可进入真实热度的下一批规划，尚未开始实现。动态模板、AI与独立更新器不是本轮完成范围。**

工作分支：`impl/architecture-foundation`。本轮按用户授权提交并推送cff657e（纯编码整理）与b90768b（R1～R4、迁移/编码校验、测试及图文）；远端同名分支已核对，未合入develop。待用户确认整体架构优化完成后再考虑集成，不自动开工下一能力。迁移/编码修复证据见[修复记录](../V3-04/PREVIEW_INSTALLER_DELIVERY.md#2026-10-19-迁移指纹与统一编码修复)，不代替架构验收。发布同步仍暂停，安装/真实登录自启未复验。当前下一动作见第6、20节；第10节为此前拉取接续快照。

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
| 薄界面层 | 文件/拖放/配置等现有业务经应用用例；会话、布局与卡片资源已有独立所有者 | R1～R4已收口；UI保留控件、菜单/选择器、输入采集、Dispatcher和原生提交，不以文件行数替代边界判断 |
| 应用用例层 | 文件/拖放/映射/观察/生命周期/迁移/配置/图标/来源修复均已验，R1已收口 | 继续复用类型化用例与现有命令/事务，不另建事实管线 |
| 稳定海报内核 | R2/R3已将会话保护、布局缓存、卡片/暖图分属明确所有者；宿主只完成原生提交 | R2/R3已收口：会话保护、纯布局、Presenter和同代发布已接线 |
| 存储与平台适配 | R1产品运行器已无SqliteStateStore；ContentWorker已引用Windows平台实现，不再链接编译Infrastructure源码 | R4已收口：显式组合根、唯一资源所有者、无跨项目源码链接 |
| 能力接入的现有部分 | R2会话拥有提案准入/缓冲/保护，R3拥有帧与呈现；窗口仅作线程/宿主接线 | 现有热度/主题接点已落地；通用权限、插件装载与模板包验证仍未制作，不以空接口补齐目标图 |

保留六个生产程序集，不强制一个逻辑模块对应一个新项目。App 在组合根引用 Infrastructure/Windows 是合法依赖；要消除的是**业务运行代码和视图**持有具体存储、直接执行文件业务，并非机械禁止 App 的所有实现引用。

## 3. 实施顺序与完成清单

R1～R4均已获人工确认并收口。第4节现有业务基础架构准入已完成证据核对；不等同于动态模板、AI、更新器等整个目标平台完成。

### R1 现有业务用例与存储解耦

**实施清单**

- [x] 盘点入口到副作用的调用链：普通/映射海报、控制中心、托盘和卸载模式均纳入，第16节代码审计与人工均完成。
- [x] USER PASS：新建、原生新建回执登记、重命名、真实删除、属性/Win 菜单：UI 只采集意图与呈现反馈，目标关系/身份校验和执行交给应用用例。行内命名、新建后进入命名、系统必要提示保持。用户本轮确认集中验证通过，首片收口，不重跑其人工清单。
- [x] 外部拖入、原生拖出、跨海报交接、映射与最小外部同步：迁出业务顺序、来源/目标见证、结果登记和状态刷新编排；原生 OLE、跟手图像及 Shell 落点继续由原适配器完成。
  - [x] 外部受管拖入、原生拖出观察、跨海报交接、映射拖入：CODE/AUTO 与两组集中人工均 PASS，见第9节；不重复安排这两组人工。
  - [x] 映射创建、周期扫描/最小外部同步与启动编排：第11节两组人工已通过。归还等服务内部仍归第12节及后续R1，不混作完成。
- [x] 删除海报/卸载归还、启动收尾、默认收纳位置与迁移：复用相同用例入口；来源不可用回桌面、映射目录不动、取消停止及文件保留规则不变，不增加恢复任务流程。
  - [x] 归还/卸载及其启动核对：第12节两组人工USER PASS。
  - [x] 默认位置与迁移：第13节两组人工USER PASS，本切片CLOSED。
- [x] 配置/布局/图标与海报管理入口复用已有 IStateCommandService/StateProjectionCoordinator 等命令投影路径；第15节配置/图标及第16节来源修复均USER PASS。未新建第二套配置服务或事实库。
- [x] 将本清单持久化请求/结果放回应用边界；第16节已迁出最后的来源预览DTO，生产窗口/运行器无具体存储旁路。SQL、文件身份、防覆盖、Shell执行仍在适配层；不因此提前关闭R4的宿主组装与Worker依赖工作。

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

- [x] CODE/AUTO：Registry维持可见代次；WinUi会话拥有单一PosterContentSession；应用协调与可重建原生资源仍分属SpatialRuntime和窗口。
- [x] CODE/AUTO：草稿/历史、提案保护与发布策略迁入会话；原生拖动、悬停、框选、菜单/命名、收展在既有开始/结束点通知，控件及捕获资源留宿主。
- [x] CODE/AUTO：复用ContentHeatSession，主题缓冲/版本、来源绑定与待发布票据归会话；删除窗口中的对应所有者与重复gesture.Proposal。
- [x] CODE/AUTO：Dispatcher/时钟/原生捕获留宿主；会话仅决定是否排队/接纳。控制中心原草稿拥有者和多海报协调不复制，不增加静态Busy或计时器。
- [x] CODE/AUTO：挂起使旧票据失效、保留待处理提案；关闭终止缓冲与订阅合并器；隐藏重建获得新对象/代次。在途文件完成登记仍由既有应用用例/运行器持有。第17节人工已获用户确认，R2收口。

**退出标准**

1. 每类状态只有一个权威所有者，窗口不再通过访问其他 partial 的私有字段拼装业务保护条件；新能力接入不需要继续新增一组窗口 Busy 字段。
2. 会话策略和热度/主题提案处理可在无 XAML/SQLite/原生 HWND 环境验证；平台工作通过注入适配执行，不能移到 Application 后又引用 WinUI。
3. 迟到回调经操作/会话代次和来源见证拒绝；退出不重新激活关闭会话，取消不会误撤已确认事实。
4. 原交互保护、自动收展、中心草稿优先和多海报互不吞操作保持；不能为了解耦变更手势或新增确认框。

**定向验证**

复用 ContentHeatSession、已有手势/自动收展/会话生命周期测试中受影响的条目；只补单一所有者和迟到回调缺口。用现有隔离夹具观察保护期不发布、解除后仅最新有效候选生效和关闭后不再调度。

### R3 增量呈现与同代命中边界

**实施清单**

- [x] CODE/AUTO： 将作者布局、草稿、热度派生、视口与空间校验的组合放入纯布局/会话边界；输出不可变呈现快照。沿用 ContentLayoutSnapshot、ContentSpacePublication 和现有求解器，不新造第二套布局引擎。
- [x] CODE/AUTO： 将 XAML 卡片树、CardPaint 差异应用、选中/命名的视觉映射收敛为 Presenter；它接收快照/差异，发出类型化用户意图，不执行文件业务或读库。
- [x] CODE/AUTO： 图像需求、缓存、解码图像和订阅具有明确生命周期所有者；Presenter 持有 UI 资源，资源提供方负责加载。保持稳定卡片 identity、有界缓存、来源代次检查和收起保留暖图。
- [x] CODE/AUTO： 布局、裁剪、标题/动作区、内容命中和拖入区由同一代快照驱动；平台适配只转换坐标和应用区域。发布失败不能让新画面配旧命中。
- [x] CODE/AUTO： Lamp/Rain 与公共背景、标题继续走已有场景端口；只隔离现有边界，不重写动效、不引入异形或离散窗口。

**退出标准**

1. 窗口宿主不再同时承担业务状态派生、卡片差异算法和输入几何决策；Presenter 不持有 SqliteStateStore、文件用例实现或领域状态的第二份权威副本。
2. 同一帧身份绑定绘制和命中，候选失败沿用上一有效发布状态；当前场景布局、拖放和窗口边界不变。
3. 无关 Item 对象/暖位图在单项改动与收展中保持；不能因抽取恢复全量重建、无界缓存或重复加载。
4. 可用既有提案入口替换一个合法候选，不需修改窗口内部算法；不以虚构评分器或新模板功能证明此边界。

**定向验证**

复用 ContentSpacePublication、ContentLayoutProposal、IncrementalContentRaster 和 ContentAssetCache 的受影响项；复用同帧/增量/收展夹具，不另建一套探针。只有确实改变的路径需要重新运行；旧候选的通过不能替代修改后的呈现证据。

### R4 组装与平台依赖收口

**实施清单**

- [x] CODE/AUTO： Bootstrap 统一组装用例、会话、Presenter 和系统适配，明确启动/停止顺序；AppLifetime 只编排生命周期，不包含单次文件业务。
- [x] CODE/AUTO： 清除 R1～R3 生产入口的具体数据库、文件执行、Shell 客户端构造旁路；WinUI 宿主保留线程、原生窗口和系统回调适配。
- [x] CODE/AUTO： 处理 ContentWorker 链接 LocalContentReferenceProbe 的隐式跨项目依赖：按实际系统探测职责落位，复用协议而非复制实现，不将 OS 探测塞入纯 Domain/Application。
- [x] CODE/AUTO： 补齐职责目录/源码入口；纯移动单列清单，构造依赖和行为变化另列。命名空间/XAML资源不做无关全量改名。
- [x] CODE/AUTO： 维护少量可执行依赖规则，检查完整生产 partial 集合和实际调用链，不再只检查几个主文件。合法组合根/平台适配例外精确到文件与职责，不放行整个 App。
- [x] CODE/AUTO： 更新“现状与目标差异”清单。现有能力所需边界不得用“以后按需抽取”结项；后续新能力未制作仍明确标识。

**退出标准**

1. R1～R3 的依赖方向在实际生产编译和接线中成立；没有 ServiceLocator、万能总线、循环引用、重复账本或空接口掩盖原耦合。
2. 普通产品不编入独立验收执行器；Review 模式仅增加验收内容，不能靠不同业务接线使测试通过。构建/打包入口不因目录变化缺文件。
3. 原代码及资源释放路径可追踪到唯一所有者，旧路径已退役或有精确合法用途；不能新旧路径同时写状态。
4. 当前图/源码入口/本清单一致。绘图工具故障单列，不冒充架构失败，也不拿渲染通过代替代码验收。

**定向验证**

复用 ArchitectureTests 与 check-architecture.ps1；修正其覆盖范围而不是机械增加每文件断言。目录/编译接线有变化时验证正常产品构建与必要 Review 编译集合；不默认再跑安装卸载矩阵或全量合同。

## 4. 进入真实热度的准入标准

**当前结论：现有业务基础架构准入 PASS，可进入真实热度的下一批规划；实现仍待用户指示。** 原“C 热度切片收口后直接进入 D/V3-06”的顺序由本计划替代。此结论来自以下边界、最终定向证据与人工验收的联合核对，不由图表通过或分支合并推定。

- [x] R1 现有用例的生产调用链已收敛，视图/运行器不存在本清单范围内的具体存储与文件业务旁路。第16节人工确认后收口；R4宿主组装例外仍单独处理。
- [x] R2 会话状态、交互保护、调度与关闭边界有明确所有者，能无 UI 验证。
- [x] R3 Presenter 与同代绘制/命中发布已接真实生产路径；稳定 Item、暖图及缓存边界保持。
- [x] R4 依赖、组合根、Worker 边界及产品/验收编译通过；剩余例外逐项说明且不破坏上述基础职责。
- [x] 最终定向证据与R4两组集中人工齐备，本批影响范围无已知未关闭回归；[目标对照与源码证据](../../20_Architecture/ArchitectureReview/R4-Closed/README.md)逐项说明实际文件、端口与验证，不把发布/自启携带项补记通过。
- [x] 现有提案注入证明消费者可输出ContentHeatProposal，经PosterContentSession和同代帧发布；无需窗口查询SQLite、持有评分或执行文件。复用R2真实宿主提案注入、R3同代/增量探针及R4依赖证据（见第20节）；事实读取端口/游标适配和纯评分策略仍需下一批实现。

准入通过后，才进入成功事实幂等消费/稳定游标 → 默认评分与新项保护 → 原提案入口 → 既定发布节奏。真实热度的“打开只标记待更新，下次展开/明确刷新发布”是后续产品行为，不与本次结构整理混改。

本准入只证明**现有业务的基础架构边界**。目标图中的动态模板、AI、独立更新器仍为未实现；全目标架构继续保持分阶段状态，不能因此宣布所有目标模块完成。

## 5. 精简验证与集中人工

文件动作及拖放切片的定向范围分别见第8、9节；2026-10-16登记人工结论时没有重跑构建、合同测试或进程矩阵。2026-10-19只选迁移/备份/文本门的定向验证，不扩大为新的架构人工清单。

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
| R1 现有业务用例与存储解耦 | USER PASS / CLOSED | 第8～16节 |
| R2 海报会话与交互状态 | USER PASS / CLOSED | 第17节 |
| R3 增量呈现与同代命中 | USER PASS / CLOSED | 第18节 |
| R4 组装与平台依赖 | USER PASS / CLOSED | 第19节；本次用户确认两组均通过 |
| 迁移身份/统一编码横切修复 | CODE/AUTO完成，已随b90768b同步当前功能分支；发布仍暂停 | 25份固定资源、共同构建门、旧库副本及反例验证；不代替R1～R4 |
| 现有业务基础架构出口／真实热度准入 | PASS；真实热度尚未开工 | 第4、20节及当前/目标对照 |

下一动作：继续审阅收口后的现状/目标对照，等待用户确认整体架构优化完成，再考虑合入impl/develop。当前源码检查点已同步远端，仍保持impl/architecture-foundation，不合并、不自动推进真实热度，不重复旧切片验收。

前一轮计划阶段切换前将涉及分支差异的两份记录备份到 out/architecture-resume-20261016-01，切换后恢复并核对哈希一致；其他未提交图源/资料原位保留。该计划阶段没有产品源码、用户文件或系统设置变化；当前 R1 实现见第8节。

前一轮计划阶段检查：新增/修改 Markdown 中53个本地文件链接均可解析，git diff --check通过；与impl/develop比较，产品/模块/脚本/打包/测试范围仅src/README.md有文档变化，生产源码与构建入口未变。没有执行产品构建或行为测试。

## 7. 现有代码锚点

以下是余项盘点入口；本次已实施的文件动作入口/端口见第8节，其余不据此标记完成。

- [打开用例](../../../../src/DesktopUp.Application/Features/Content/Open/ContentActionService.cs)与[热度会话](../../../../src/DesktopUp.Application/Features/Content/Layout/ContentHeatSession.cs)：已完成切片。
- [窗口文件动作](../../../../src/DesktopUp.App/PosterVisualWindow.FileActions.cs)、[运行器文件用例](../../../../src/DesktopUp.App/PosterSpatialRuntime.FileActions.cs)、[跨海报编排](../../../../src/DesktopUp.Application/Features/Content/Transfers/CrossPosterTransferUseCase.cs)：R1已实施切片。
- [收纳位置用例](../../../../src/DesktopUp.Application/Features/Content/Storage/StorageLocationUseCase.cs)、[内容归还](../../../../src/DesktopUp.Application/Features/Content/Lifecycle/ContentReturnUseCase.cs)、[卸载入口](../../../../src/DesktopUp.App/App.Uninstall.cs)：R1 生命周期。
- [会话注册](../../../../src/DesktopUp.Application/PosterSessionRegistry.cs)、[WinUI 会话](../../../../src/DesktopUp.App/WinUiPosterSession.cs)、[窗口保护/提案调度](../../../../src/DesktopUp.App/PosterVisualWindow.ContentProposals.cs)：R2。
- [卡片与布局状态](../../../../src/DesktopUp.App/PosterVisualWindow.Content.cs)、[空间发布](../../../../src/DesktopUp.App/PosterVisualWindow.ContentSpace.cs)、[图像生命周期](../../../../src/DesktopUp.App/PosterVisualWindow.ContentAssets.cs)：R3。
- [组装](../../../../src/DesktopUp.App/Bootstrap/ApplicationComposition.cs)、[Worker 项目](../../../../src/DesktopUp.ContentWorker/DesktopUp.ContentWorker.csproj)、[现有依赖测试](../../../../tests/DesktopUp.Contracts.Tests/ArchitectureTests.cs)：R4。

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
2. 阅读本计划第6、8、9、11～12节及[当前交接](../V3-04/HANDOFF.md)、[源码职责](../../../../src/README.md)。第11节创建→扫描→结果发布链已USER PASS；当前归还/卸载片见第12节，不重验已通过的观察两组。后续沿用现有窄端口、原子提交和观察规则，不复制第二份循环。
3. 第12节归还/卸载人工通过后，继续 R1 收纳位置迁移、配置/海报管理入口；然后 R2 会话、R3 呈现/同代命中、R4 组装/Worker。真实热度仍按第4节 HOLD，AI/模板平台/更新器不是当前前置实现。
4. 换机首次使用 `scripts/setup.ps1` 准备项目工具链；需要构建时使用 `scripts/build.ps1 -Configuration Release -OutputDirectory out/architecture-next/product01`（选择未使用的输出目录）。没有缓存时不要直接加 Offline，不要依赖系统 dotnet 版本。
5. 旧 review 脚本绑定历史候选路径/哈希，只在原候选仍存在时用于重开；新拉取没有 out 是正常状态。下一批按实际改动重建独立候选/夹具并绑定新哈希，不能改旧哈希冒充已验二进制，也不需要为“拉取接续”重跑两组已通过人工。测试仍按影响选择。
6. 发布线阻断的迁移身份问题已于2026-10-19在本架构分支修复并隔离验证，但尚未提交/同步发布线；不得重置旧配置或直接改迁移账本。发布同步、安装和真实登录验证仍暂停；恢复发布时单独携带[修复变更及必要构建文件](../V3-04/PREVIEW_INSTALLER_DELIVERY.md#2026-10-19-迁移指纹与统一编码修复)，不要顺带合入未完成R1，也不要反向引入发布精简。

## 11. R1 映射创建、观察同步与启动接线（2026-10-19）

状态：**CODE/AUTO PASS，USER PASS / 本切片 CLOSED**。用户确认两组验证均符合预期；本片收口，不关闭整个R1。当前impl/architecture-foundation@548486f加工作区修改；未提交/推送/合并，发布同步仍暂停。先前迁移身份/编码修复原样保留，不改schema、旧迁移账本或真实用户文件。

### 本片职责与退出标准

| 所有者 | 实际迁出的职责 | 保持的边界 |
| --- | --- | --- |
| Application/Features/Content/Observation | MappedDirectoryUseCase创建顺序、两秒稳定等待/复核、映射提交决策；MappedDirectoryCreation在事务当前状态上生成初始计划 | 初次关系与身份凭证同事务；失联、替代、超限不作空目录 |
| ManagedContentSyncUseCase | 基线/身份匹配、缺失与改名决策、两次完整枚举核对、关系移除/更新计划 | 不搜索外部目录、不执行文件搬移；排队文件操作优先、CAS失败不猜测 |
| ContentObservationService | 监视目标及递归范围、先受管后映射的结果聚合 | 每配置单一串行扫描，失联时原卡片保留 |
| ContentStartupUseCase | 位置收尾→文件账本收尾→单一归属→改名→归还→跨海报→映射拖入→原生拖出观察的既有顺序 | 不重放搬移；已产生通知及时记录；后续步骤不越过失败 |
| Infrastructure/ContentObservation | 窄SQLite端口、原子关系/凭证提交、所有权/执行租约；LocalContentObservationFiles限额只读枚举/身份读取 | 不把SQL或任意事务回调暴露给应用用例，不拆原事务 |
| ManagedContentMonitor / App | 监听器和2秒定时器、15轮兜底、停止释放；宿主派发投影/通知；Bootstrap组装 | 监视器不持有SQLite；映射运行入口及App.Transfers不引用Infrastructure |

原MappedDirectoryService.Create/Reconcile与ManagedContentSynchronizer只转接同一应用用例，没有复制第二份扫描循环；原新建/拖入的映射发布也复用该提交路径。映射动作的身份校验、其余生命周期实现仍由既有适配器承担。Bootstrap保留共享ManagedStorageService给尚未迁出的设置入口；本片只抽启动编排，不声称归还/卸载/位置迁移内部已完成R1。

本轮没有搬迁既有生产文件；新适配器归入ContentObservation，命名空间保持。迁出逻辑与新组装不作为“纯文件移动”验收；没有叠加新产品行为。

### 精简验证与证据

- 65/65定向通过：既有DirectoryMappingTests、ManagedContentSyncTests、MappedDirectoryIngressTests 58项；新增启动顺序/失败、扫描后取消与租约释放4项；核心依赖2项、新切片接线约束1项。最终：out/architecture-r1-observation/tests/r1-observation-final02.trx。不执行累计全量。
- 正常product02、Review review02和SessionFixture均Release构建0警告/0错误。正常编译102源/0验收源，Review169源/67验收源，Preview互斥通过；共同文本门906文件/25冻结迁移通过。
- 实际生产接线：复用控制中心映射创建/取消回调，正常定时器/Watcher自动发现新增/改名，目录失联保留卡片/恢复，受管文件外部移出移除卡片；停机新增后再次启动自动发现，原映射卡片ID保持。没有直接调用Reconcile或强制Invalidate，没有生成桌面文件。
- 两次Review进程20520/40052均contract.r1_observation_passed，退出activeResources=0。证据：out/drop-d3/architecture-r1-observation-probe02/profile/logs/desktopup.jsonl；nativeMouse=false、picker=false，**不代替原生鼠标/选择器/视觉人工**。本机仍高完整性，不改UAC，不称普通权限或跨环境认证。
- 首次probe01因新夹具错误假设每内容仅一条membership而中止；实际有存储与海报关系。已改为通过ContentRelationPolicy查卡片，再用新数据probe02复验通过；不是修改产品归属规则，失败证据保留在out。初编缺少枚举适配器Domain using已补齐，最终构建/测试全绿。
- 开机真实登录、长测、多屏/DPI及满盘仍按原记录未验；安装/发布同步继续PAUSED。本批“启动”指普通应用初始化/重启，不是开机登录认证。

### 集中人工：仅两组

正常产品候选已打开：PID31028，out/architecture-r1-observation/product02；配置与样例out/drop-d3/architecture-r1-observation-review01。入口：[start-architecture-observation-review.ps1](../../../../scripts/start-architecture-observation-review.ps1)，Open可重开原配置、Prepare/Status只准备/查看，Probe使用另一份数据。绑定App/Application/Infrastructure哈希；App为4223312FC1482039CE5387683CFEBEDFF4176FD105EC274725F4B6F03F9C01B0。旧候选/配置原位保留，自动样例与证据只在out。

1. **映射与外部变化**：控制中心“新增海报→目录映射”选择本批mapping-input。在Explorer内新增/改名文件，海报随后自动跟随且原卡片不重复；将整个mapping-input临时改名为mapping-input-offline，海报应提示失联而保留卡片；改回后恢复。变化通常数秒；无事件时约30秒触发兜底，但不是端到端延迟上限（扫描耗时另计），不要求立即刷新。
2. **普通收纳与重启**：A空白区新建一个文本文件；从其右键“在资源管理器中定位”进入实际目录，改名后海报跟随，再剪切到本批external-target，卡片随后消失且目标文件保留。托盘正常退出，用上述脚本Open重开：映射内容保持，移出卡片不重现，没有新确认框或自动搬回。

2026-10-19用户明确确认两组均USER PASS：映射创建、外部变化/失联恢复，以及普通收纳外部修改/移出/重启均符合预期。验收登记不重跑本片测试。下一片归还/卸载见第12节；真实热度仍HOLD。

<a id="r1-lifecycle"></a>

## 12. R1归还／卸载用例解耦（2026-10-19，人工通过）

用户授权连续推进至集中人工，暂不合并。本轮影响面为删海报归还、卸载内容处理及两者共用的单项归还/重启观察，不改文件动作、扫描策略、安装器或清理清单。

质量约束：目标架构是手段，不以接口/文件数量作为成绩。优先可读的执行顺序、明确命名和单一实现；保留同步/后台线程边界、现有查询/事务次数，不引入重复读库、轮询、缓存或额外任务队列。发现可疑权宜实现先记录事实、代价和改进条件，不在行为保持批中顺带重写。

计划：迁出两类批次的决策及共用“来源→目的地→意图→Shell→回执→提交”流程；SQLite适配保留身份/归属防护、原子关系移除及账本。卸载宿主只处理控件、退出码和生命周期，应用用例持有保留/归还/静默限制与位置清单编排；报告文件和配置可用性检查由适配器执行。原入口兼容调用同一用例，不能保留两套循环。

退出标准：生产删海报、删除前核对、启动核对和维护卸载入口均经过应用用例；源不可用回桌面、映射不搬动、部分取消保留剩余、重启只观察不重放、保留模式不枚举离线磁盘保持。沿用已有归还/卸载测试，只补新边界；独立进程证明正常与维护接线。人工最多两组，不重跑旧安装卸载矩阵，不创建桌面测试遗留，不恢复发布同步。

后台观察性能另记[OPT-CONTENT-SYNC-01](../../30_Plans/ON_DEMAND_OPTIMIZATIONS.md)，本轮不调整。

### 实现与行为保持

状态：**CODE/AUTO PASS，USER PASS / 本切片 CLOSED**。用户确认删海报归还/映射保留、维护冲突取消/再次归还两组均符合预期。仍在impl/architecture-foundation@548486f加工作区修改，未提交/推送/合并；整个R1不因此关闭。

- Application/Features/Content/Lifecycle：ContentReturnUseCase拥有删除读集确认、逐项归还、部分取消、删除海报及重启只核对；UninstallContentUseCase拥有库存复核、逐项归还和最终空库核查，两者共用ReturnOne；UninstallPreparation拥有保留/归还/静默限制、进度和位置清单编排。
- Ports/Content/ContentLifecyclePorts：返回事实、原子存储端口、目标可用性与现有配置/清单端口；没有通用SQL、任意事务回调、额外消息总线或任务队列。
- Infrastructure/ContentLifecycle：SqliteContentReturnStorage保留归属/身份核验、执行租约、库存及事务；LocalContentReturnFiles保留原目的地限制；LocalUninstallProfile保留既有库打开与位置清单落盘。原PosterContentReturn只兼容转发，没有第二套循环；原Uninstall分部合入适配器/用例，没有删除用户文件。
- Bootstrap/App.ContentLifecycle统一组装；运行器、中心删除前核对、启动核对和App.Uninstall均接用例。App.Uninstall只留控件/句柄、取消/退出码与日志；未启动配置仍不创建默认库/海报。控制中心其他位置/配置入口仍是后续R1，不以本片替代。
- SourceCurrent、Commit、Inventory、UninstallSourceCurrent、CommitUninstall与改前主体比对一致（忽略格式空白）；原检查/查询次序、回执JSON字段、原子提交、schema及文件动作不变。没有增加定时器、扫描、缓存或执行队列，也未宣称性能实测改善。
- 本片为用例抽取和接线，非纯文件移动；兼容外观与文件行为不变。不要把接口数量或文件数量当作退出标准。

### 精简验证

- 定向39/39：原归还/卸载33项；新增卸载准备的离线保留/静默拒绝/失败清单与再次归还3项；核心依赖2项、生产接线边界1项。最终out/architecture-r1-lifecycle/tests/lifecycle-final02.trx，未跑累计全量。初轮新增测试一条xUnit写法警告已修；最终无警告。
- 正常candidate01/app、Review candidate01/review、SessionFixture均Release 0警告/0错误；正常103源/0验收源，Review170源/67验收源，Preview互斥PASS。文本门915文件/25冻结迁移PASS；启动脚本Windows PowerShell 5.1语法/执行与git diff --check通过。
- 正常候选的维护入口五进程PASS：保留7056/exit0；静默归还拒绝32032/32；交互归还9540/0；再次空归还17952/0；首次未启动45336/0。复用scripts/smoke-uninstall-content.ps1，证据out/drop-d3/architecture-r1-lifecycle-maintenance01/results.json。没有执行安装器、卸载器或修改登记。
- 既有ContentReturnProbe生产接线：PID27964正常拖入→原路径登记→运行器归还删海报，PID29440重启后无卡片复活/无重复搬移。contract.content_return_passed，nativeMouse=false；证据out/drop-d3/architecture-r1-lifecycle-probe01/profile/logs/desktopup.jsonl。自动回调不替代原生鼠标或视觉人工。
- 本批所有样例/目标均在out；人工夹具复用原ContentReturnReviewFixture但启用architecture模式，保留B原目录、不创建未知来源样例，避免回桌面遗留。生产“原路径不可用回桌面”逻辑保持，已有测试用隔离假桌面覆盖。
- 旧候选/配置及发布stash不动；没有安装、修改启动项或搬移真实用户文件。真实登录、长测、多屏/DPI、满盘等既有未验项仍携带，不重跑或补PASS。

### 集中人工：仅两组

入口：[start-architecture-lifecycle-review.ps1](../../../../scripts/start-architecture-lifecycle-review.ps1)。正常产品out/architecture-r1-lifecycle/candidate01/app已启动PID18424，独立样例out/drop-d3/architecture-r1-lifecycle-review01。Open重开、Prepare只准备、Status查看；Keep/Return只调用该配置的卸载前内容处理，不实际卸载软件。旧包/数据不覆盖，绑定App/Application/Infrastructure SHA256；App为04707F51176C6A2011442105C2A5333983D8F5E55CCC66CEB3AC79ACC9253E96。

1. **删海报归还与映射保留**：删除“A · 原路返回”，origin-A应出现A-return.txt与A-folder/child.txt，A海报消失；删除“D · 映射不搬动”，mapping-D/D-mapped.txt保持。请只操作本批样例，不改名/删除origin目录，不用真实桌面文件。
2. **维护归还的冲突取消／再次归还**：先托盘正常退出，用该脚本 -Action Return。C-collision.txt的Windows冲突提示中取消；关闭维护窗口后 -Action Open，应仍可启动、未完成卡片仍在，已完成文件不搬回。再次退出；将本批origin-C/C-collision.txt占位文件改名为C-existing.txt以保留它，再 -Action Return；剩余内容应归还，profile/uninstall-reports留下位置清单。最终Open确认无旧卡片复活。此过程不执行实际卸载。

2026-10-19用户明确确认两组均USER PASS。本片收口，不重跑这两组；继续第13节位置迁移，再配置/海报管理，整体目标架构及真实热度仍未放行。

<a id="r1-storage-location"></a>

## 13. R1收纳位置与迁移用例解耦（2026-10-19，人工通过）

范围：全局收纳位置查看/旧根选择、首次默认创建、主动更改预览/确认、保留源复制→核实→一次索引切换→受限收尾、启动仅观察既有工作。用户界面、确认语义、默认路径、元数据与文件支持范围保持，不改schema、迁移清单、扫描调度或安装器。暂不合并分支。

职责：Application拥有位置选择/创建顺序、预览种类、迁移阶段与失败收尾调用、结果和启动通知；Infrastructure保留原生文件复制/身份锁、证据核验与受限清理这一完整安全操作，以及原子SQLite提交。不得把原子证明拆成多次独立写，或让UI提供“复制已完成”的布尔证明；被替代旧服务和执行入口直接删除；仅测试夹具通过组合真实用例注入故障，不保留产品兼容壳或第二套循环。控制中心只持有用例/只读异常位置端口，组装放Bootstrap。

质量边界：沿用已冻结纯策略和实际事务；按内聚职责拆分，不按每个私有方法造接口。保持既有查询/枚举顺序，不增加轮询、全量刷新、任务队列或无界缓存；本轮不为满足目标图顺带调整观察性能。

退出标准：首次收纳、中心迁移和启动收尾使用同一个应用级位置服务；取消前后及未知副本保留规则不变；查看空配置不创建目录，明确迁移才改变已有位置。精简验证选ManagedStorageService与受影响StorageRelocationFiles既有测试（含实际故障/取消/重启），只补执行次序/依赖缺口；复用StorageSettingsProbe中的迁移与重启，加一个退出/取消生命周期点，不重跑旧安装卸载或全量合同。独立正常候选集中最多两组：确认迁移/重开保持；取消选择或拒绝非空目标后仍可继续使用。样例与目标只放out，保留上一批已验包。

### 实现与旧代码退出

状态：**CODE/AUTO PASS，USER PASS / 本切片 CLOSED**。用户确认下述两组符合预期。本批在impl/architecture-foundation@548486f加工作区修改完成，不提交/推送/合并。前一片归还/卸载的两组USER PASS已记录；R1整体、R2～R4和真实热度准入均未关闭。

- Application/Features/Content/Storage：StorageLocationUseCase集中首次创建/旧位置选择、预览/确认、结果和启动通知；StorageRelocationUseCase集中准备→核实登记→一次提交→按已观察事实收尾的顺序。UI取消及失败仍沿原语义，启动只观察，不重放复制。
- Ports/Storage：迁移/位置请求结果回到应用边界；IStorageLocationStorage提供原子存储/所有权事实，IStorageRelocationExecution提供阶段执行；只读异常位置投影不暴露账本控制权。没有通用Repository、任意SQL/事务回调或新任务管理流程。
- Infrastructure/StorageLocation：SqliteStorageLocationStorage承接原查询、目录声明/身份和预览清单；LocalStorageRelocationExecution使用原真实验证器；SqliteTransferLocationNotices只投影既有最后30条异常位置。文件复制、身份锁、证明和原子事务保持凝聚，StorageRelocationFiles.Recovery主体与本片前完全一致。
- Bootstrap组装一个位置用例供首次收纳、中心与启动共享；ControlCenterWindow.Transfers及ControlCenterService不再引用Infrastructure/SQLite。不新建计时器、扫描、缓存或队列；既有查询/枚举次序不变，未宣称性能已测得提升。
- **已删除**原ManagedStorageService整文件（185行，含迁出的旧DTO/入口）、StorageRelocationFiles.Execute旧循环、ControlCenterWindow中的直接账本筛选与SqliteTransferJournalNotice辅助类、无调用者使用的forceChoose参数。Provision/LocationState/Result旧声明移至Application后，原定义删除。
- 既有ManagedStorageServiceTests保留历史文件名但已经直接组合新用例；StorageRelocationFilesTests及进程故障夹具也走同一应用执行循环。不存在“生产走新入口、测试只覆盖旧入口”的双路径。先前其他已验切片仍有必要兼容转发，不在本批跨范围删除。
- 本次是责任迁出和旧入口退役，不是纯目录移动；新增端口/适配仍有代码成本，不以净行数或接口数量证明质量。实际安全底座不是“无用旧代码”，不会为缩行数而删掉。

### 精简验证与候选

- 74/74定向PASS：复用位置22项、复制/收尾46项；新增编排次序/错误确认/意图前后失败3项和接线约束1项；核心依赖2项。证据out/architecture-r1-storage/tests/r1-storage-01.trx，未跑累计全量。
- 正常candidate01/app、Review candidate01/review、SessionFixture均Release零警告/零错误；正常103源/0验收源，Review170源/67验收源，Preview互斥通过。文本门最终921文件/25冻结SQL通过；Windows PowerShell 5.1入口通过。
- 复用既有StorageSettingsProbe的3个实际进程：迁移verify PID48576、中心关闭并重开close PID33024、启动restart PID10536，全部PASS/exit0/退出activeResources=0。证据out/drop-d2/architecture-r1-storage-smoke01/results.json；这些为生产回调接线，不代替鼠标、选择器和视觉人工（nativeMouse=false）。
- 独立正常候选已启动PID41000；App SHA256为6275703FE1E13CCF434BA16A2679FE5D2533E0DD45B43E900F8F198521B8838A。入口[start-architecture-storage-review.ps1](../../../../scripts/start-architecture-storage-review.ps1)绑定App/Application/Infrastructure；Open重开原配置、Prepare只准备、Status只查看，不运行安装器。
- 人工数据out/drop-d3/architecture-r1-storage-review01：一个基础收纳海报，return-file.txt及return-folder/child.txt两张真实内容卡片，storage为初始收纳；empty-target-A/B为空目的地，occupied-target/keep.txt为自建占位。复用既有受管文件/文件夹夹具，仅增加标题选项，不复制一套建库流程。sample-manifest.json记录初始路径/哈希，所有内容只在out，旧包/配置不动，无桌面遗留。
- 本机候选日志isElevated=true（asInvoker继承当前启动环境）；未改变UAC或完整性等级，不将本轮进程回调/人工候选扩称普通权限或跨环境认证。
- 发布同步仍PAUSED；没有安装/卸载、改启动项或schema。真实登录、长测、多屏/DPI、满盘及观察性能风险继续携带；不得借本批结构验证补PASS。

### 集中人工：仅两组

只操作本批自建样例：

1. **迁移与重开**：控制中心“全局→收纳位置→更改并迁移”，选empty-target-A，确认原有预览后完成。两张卡片及文件内容保持，打开/定位指向新位置；托盘退出后用上述脚本Open重开，位置与卡片保持、不重复。
2. **取消／非空目标保护与再迁移**：更改时取消目录选择器，现位置不变；再选occupied-target，应拒绝使用且keep.txt不变。随后改到empty-target-B应仍可完成，卡片及内容正常。不重复上一批拖放/归还/卸载人工矩阵。

2026-10-19用户确认“符合预期”，按本次集中清单记录两组均USER PASS：迁移/打开定位/重开保持，以及取消/非空目标保护/再次迁移均符合预期。本切片CLOSED，不关闭R1整体或目标架构。此次只登记人工结论并更新计划，复用本节有效自动证据，不重跑构建/测试，不改源码、不结束运行进程、不清理旧包或隔离证据，不提交/推送/合并。

<a id="next-series-after-storage"></a>

## 14. 位置迁移收口后的系列计划（2026-10-19）

当前本批人工已通过；下面是接续顺序，不表示本轮已开始实施。继续使用impl/architecture-foundation，不自动合入develop；Git集成另按用户授权执行。

| 顺序 | 工作范围 | 退出标准 |
| --- | --- | --- |
| 1. R1最后一批：配置与海报管理 | 核清配置导入/导出、备份/离线恢复、布局/图标命令和新增/修改/删除海报的调用链。复用既有命令/投影/原子提交，只迁出仍留在窗口/具体实现中的业务编排，删除被替代路径 | 窗口采集意图与反馈；业务不直接持有具体存储；配置保真、取消、原事务和已验收手势不变。已满足边界的入口不再包一层 |
| 2. R1整体出口审查 | 对生产入口、DTO归属、剩余兼容转发及实际调用者逐项核清；更新职责/例外清单 | 不存在重复执行或具体存储旁路；无用旧入口移除，有必要的适配保留并说明。不把各切片USER PASS自动等同于R1完成 |
| 3. R2海报会话与交互状态 | 整理草稿、拖动/框选/命名/菜单/收展保护、提案缓冲、迟到回调和关闭释放的唯一所有者；沿用ContentHeatSession | 窗口不再拼接业务Busy字段；会话策略可无UI验证，多海报互不干扰，关闭后不复活 |
| 4. R3增量呈现与同代命中 | 收敛布局快照、Item差异绘制、图像需求/缓存与几何命中发布；复用已有求解器和场景端口 | 画面与命中属于同一代；稳定Item、暖图和增量刷新保持，不重新引入闪烁/全量重建 |
| 5. R4组装与平台依赖收口 | Bootstrap统一组装/停止；清理剩余构造旁路、Worker隐式链接和误入产品的验收入口；目录移动单独记录 | 实际生产依赖方向清晰，资源有唯一所有者，构建/打包边界成立，现状图与源码一致 |
| 6. 基础架构出口与真实热度准入 | 对照第4节审查现有业务职责边界、精简验证及集中人工；通过后才规划V3-06实现 | 成功打开事实幂等消费/游标→评分与新项保护→既有提案发布入口；未达标仍HOLD，不继续往大窗口或具体存储叠加评分 |

下一批的代码核对入口：App/PortableProfileController与App.Portable、StateRecoveryWindow、ControlCenterWindow.ContentIcons、ControlCenterService及PosterSpatialRuntime.ControlCenter。只读核对已见图标视图直接使用LocalContentReferenceProbe、恢复窗口持有StateBackupRecovery、中心图标分支依赖具体StateProjectionCoordinator；其中哪些属于用例编排、哪些属于合法组合/宿主适配需按实际调用者判断，不能仅凭using或类名机械迁出。业务接口已有覆盖时直接复用。

每批均以可读性、执行成本、唯一职责和增量开发便利为标准；旧路径退役与新接线一起完成，不追求接口/文件数量。继续按影响选用现有测试，人工通常1～2组，不把历史回归库全部带入，也不按内部小步骤逐次验收。当前记录轮没有新增产品或测试代码。

后台扫描/计时器的规模化优化继续归OPT-CONTENT-SYNC-01：先测量，再单独决定事件合并、增量范围和兜底策略，不在结构保持批中顺改。真实登录自启、长测、多屏/DPI和满盘等携带项保持原状态；发布同步仍暂停。动态模板、AI接入、热度模型热替换和发布热更新属于后续能力，本轮只铺已有业务需要的职责边界，不先制作全部目标功能。
<a id="r1-configuration-icons"></a>

## 15. R1 配置与图标入口整理（2026-10-19，USER PASS / 本切片CLOSED）

本批接续已验收的收纳位置迁移；暂不合并。范围是配置导入/导出、备份/离线恢复和卡片图标入口，审查海报管理调用链但不改空间交互行为。

- 将 App 中 PortableProfileController 的预览、源文件复核、备份和提交顺序迁到 Application。复用现有命令协调器、规划器和状态仓库；文件 IO 由基础设施实现端口，旧控制器删除，不留转发壳。
- 图标文件检查/解码走 Application 入口；窗口只持有选择器、预览、选择有效性与取消生命周期。移除对具体协调器的类型转换，复用命令接口。
- 离线恢复的候选/结果与调用端口归 Application；现有数据库租约、日志、替换和回滚保持一个基础设施原子实现。不把其内部步骤拆成多个可被误调用的端口，也不增加纯转发恢复用例。
- 海报新增/复制与布局仍复用既有命令和纯规划；运行时读回、显示环境、占位/交互守卫属于后续 R2/R3，不为迁出 using 而复制其业务。

退出标准：产品窗口不直接构造配置文件/恢复存储、不读取图标文件元数据、不强转具体命令协调器；导入的文件变更检查、前置备份、二次环境守卫与一次提交顺序不变；图标取消/过期预览、恢复安全保护不弱化；退役旧控制器并更新所有调用者。按影响运行配置、恢复、图标相关验证及正常/Review 构建，复用现有进程探针；集中人工仅配置/备份与图标两组。整项 R1 仍需后续调用链与旧入口审计，不能据此标记目标架构完成。

### 实现与证据（2026-10-19）

- Application/Features/Configuration/PortableProfileUseCase承接原控制器的业务顺序；IPortableProfileFiles由既有PortableProfileFile实现，提交复用IStateCommandService.ExecutePortable，不另建事实库或配置命令管线。原App/PortableProfileController.cs整文件删除；预览说明文字留在视图，进入离线恢复的重启请求归宿主。
- ContentIconImport.LoadAsync集中后台来源检查及原Worker解码，LocalContentIconSource复用原本地检查；中心用IStateCommandService提交图标命令，不再强转StateProjectionCoordinator。选择器、过期选择检查、取消与预览对话框保持原流程，无额外轮询或重复解码。
- IStateBackupRecovery及结果类型归Application；StateRecoveryWindow只依赖端口，Bootstrap/App.Configuration组合具体实现。StateBackupRecovery内部验证/租约/日志/替换/回滚主体未改，不为架构形状拆散安全操作。
- 海报新增/复制经既有命令、空间规划与运行时占位证明；本批不动显示读回、布局草稿与交互守卫，后两者仍属R2/R3。不能把这些合法宿主职责当成待删旧代码。
- 116项定向通过（含新增6个导入顺序场景、1个接线检查），再补2项来源检查取消/拒绝不启动解码，共118项；其余复用既有配置文件/事务、恢复、图标解码/持久化/预览验证，未跑全历史套件。证据out/architecture-r1-configuration/tests/r1-configuration-01.trx和r1-configuration-cancellation.trx。初次测试编译暴露移动DTO后的测试using遗漏，已修正，不保留旧命名空间兼容壳。
- 正常产品、Review、SessionFixture Release构建均0警告/错误；compile-set正常103/Review0，验证170/Review67，Preview隔离门通过。文本门926文件/25冻结迁移通过。
- 仅复用1海报配置全参数进程探针：PID14100导入/拒绝变更等53检查，PID42516重启18检查；results位于out/architecture-r1-configuration/probes/gates-d6a856cfcc5e45b68c16d17b6c841473。离线恢复健康/损坏/待处理三个窗口探针PID5136/13664/31536通过，证据probes/recovery-gates-passed.json。五进程退出资源均0；窗口探针不执行实际恢复，NativePicker/NativeConfirmation/NativeMouse均非人工证据。
- 正常候选out/architecture-r1-configuration/candidate01/app已启动PID13764，已见application.started、poster_session.ready且无error/fatal。日志确认asInvoker继承当前高完整性；不声称普通权限拖放已复验。旧候选、真实配置/文件、发布stash不动，未安装、未调整自启、未提交/推送/合并。

### 集中人工：两组，USER PASS

2026-10-19用户明确反馈“验证通过，请继续”。配置/恢复与图标两组均记USER PASS，本切片收口；不重跑这两组、不据此关闭整项R1。

入口：scripts/start-architecture-configuration-review.ps1，Open/Prepare/Status；默认Configuration，另用-Scenario Icons。绑定正常候选App/Application/Infrastructure三个SHA256；App为71B39BB79635D65898B0208580C7B07D81C83D5BE666230CD801E743C60F0487。数据全部在out/drop-d3/architecture-r1-configuration-review01。两个场景互相隔离，切换前托盘正常退出，不覆盖已备数据。

1. **配置与恢复入口**：当前空海报修改标题/一个外观参数，导出到本场景exports，再追加导入，检查参数保留、原海报未覆盖。创建本机备份后改标题，通过“从本机备份恢复”进入离线窗口，恢复该备份并正常启动，标题回到备份状态。不要在本场景拖入真实文件；既有真实引用不允许便携导出的边界不变。
2. **图标入口**：托盘退出后执行脚本-Scenario Icons。选择return-file.txt卡片，在控制中心更换为本场景sample-icon.png，确认只改图标、真实来源和另一卡片不变；重开后仍保留，再恢复默认图标。全部文件为自建样例，不用桌面真实内容。

### 当时的下一片与仍未关闭的边界（来源修复已于第16节处理）

R1来源修复/遗留入口清理仍需做：ControlCenterWindow.References直接调用LocalContentReferenceProbe、持有LocalReferencePreview；EditReferences(false)添加分支目前未找到产品调用者，需核清后退役。RemoveSelectedReference仍有ControlCenterWindow.ContentLayout键盘调用，不能仅凭菜单已移除就判为死代码删除。先收清这一调用链，再做R1完整出口审计；不提前跳到R2或真实热度。扫描/计时器OPT-CONTENT-SYNC-01、R2～R4及发布/自启携带项原状保留。

<a id="r1-reference-cleanup"></a>

## 16. R1 来源修复与遗留执行入口清理（2026-10-19，USER PASS / R1 CLOSED）

用户明确确认本组通过并要求收口R1；结合前六片已验结果和下方出口审计，R1关闭，不等于整体架构完成。补充只读排查：本批为samples目录中的外部引用，未建立受管根；全局收纳位置“打开”灰显来自尚未首次收纳，不是修复清空路径。数据库无受管根/集合/迁移，修复已提交且三个样例哈希保持；本轮不修改该行为。

- 修复预览、元数据复核、版本见证和命令提交归Application；窗口保留选择器、确认/跳过、交互及关闭守卫。继续使用同一个ContentReferenceCommand和事务，不新增事实库，不移动真实文件。
- 删除无调用的EditReferences(false)添加/DEMO转换/批量分支；保留活跃的修复入口、ReferenceDialog及Delete键移除关联语义。
- 原PosterSpatialRuntime.Outgoing仅CombinedOutgoingProbe调用；先按字节搬入tools/review，再单独整理接线。删除无调用RunTransfer和旧确认回调，具体测试存储只在Review组装；正常Transfers保留原繁忙/停止/启动守卫。既有原生Incoming/NativeExit/跨海报业务不改。
- 退出：生产窗口不持有基础设施预览DTO，运行器不持有SqliteStateStore或旧文件执行器；来源复核→宿主守卫→一次原命令提交顺序与取消/版本拒绝不变。静态检查调用者、按影响跑修复/隔离验证和构建，集中人工仅一组修复/取消/重启；不带入已验配置与图标清单。

R2/R3运行时会话/布局与R4其余组装/Worker边界不在本片顺改；定时观察策略仅记录。仍不提交、推送或合并。

### 已实施与退役内容

- Application/Features/Content/FileActions/ContentReferenceRepairUseCase冻结当前关系版本、列出影响范围、后台检查来源、复核元数据，随后在宿主上下文执行交互/关闭守卫并调用同一ContentReferenceCommand。守卫与提交之间不再await；真实文件不移动，SQL/CAS及单一归属规则不变。
- LocalReferencePreview移入应用端口IContentReferenceSource；LocalContentReferenceSource只适配既有本地检查。窗口不持有基础设施DTO或读取文件元数据；选择器、确认/跳过和提示保留。原EditReferences(bool)中的无调用添加/多项/DEMO转换分支删除，活跃RepairSelectedReference和键盘移除保留。
- 先将PosterSpatialRuntime.Outgoing原文件逐字节移动到tools/review/App/PosterSpatialRuntime.LegacyOutgoingProbe.cs，前后SHA256均8DE5A48969F49178CEDE1B9F81D1F8D119B721C95C2896F8D7D7CAAE4A1DE4C7；之后单独改Review组装与字段。该探针历史模拟执行器不进入正常产品，不能代表原生鼠标拖出验证。
- 删除生产RunTransfer/TransferFiles、无用_confirmTransfer/ConfirmDirectTransferAsync及恒返回false的ConfirmOutgoingAsync。正常运行器只保留传输启动/忙态/停止与现有原生入口；原来因验证代码持有的SqliteStateStore迁到Review专用字段。原生失效引用确认窗口不删。

### 定向验证与候选

- 44/44通过：ContentReferenceRepairTests、ContentReferencePersistenceTests、LocalContentReferenceTests及两项相关接线检查。其中新增7个修复场景和1个隔离检查，其余复用；未跑全量。覆盖来源变化/丢失、取消、宿主忙态、版本过期、一次提交/重复拒绝、原文件与布局保持及单一归属。TRX：out/architecture-r1-references/tests/r1-references.trx。
- 正常、Review及SessionFixture Release均0警告错误；编译集合正常102项/Review0，验证170项/Review68，Preview隔离门通过；文本门932文件/25冻结迁移通过。测试/夹具初次编译的目标类型推断和using遗漏已修正，不影响最终通过结论。
- 复用已有R1TransfersProbe，仅一进程PID9032通过，退出资源0；入站/跨海报/映射接线及模拟外部Shell完成可用，不重跑历史完整矩阵。证据out/drop-d3/architecture-r1-references-probe01/results.json；NativeMouse=false，外部拖出为模拟事实，不记新一轮原生人工通过。
- 正常候选out/architecture-r1-references/candidate01/app已启动PID48172，独立数据out/drop-d3/architecture-r1-references-review01；三个自建文件及哈希见sample-manifest.json。入口scripts/start-architecture-reference-review.ps1提供Open/Prepare/Status，Probe仅独立自建验证。App SHA256=30374E8A5BF8E13BEF5142042A2F92D39737D658756D3A5E9025669CFD1ABC9A，另绑定Application/Infrastructure。旧包/配置不覆盖，不写桌面样例，不安装或改自启；当前分支未提交/推送/合并。

### R1出口代码审计

| 生产调用链 | 当前职责边界 | 结论与携带 |
| --- | --- | --- |
| 新建/重命名/删除/Win菜单 | 窗口采集，ContentFileActionService及专用用例编排；SQL/Shell原子步骤在适配器 | 已USER PASS，本轮不重验 |
| 拖入/拖出/跨海报 | Incoming/NativeExit只调应用端口；旧非原生执行器已退出产品 | 既有人工保持，本轮接线探针通过 |
| 映射/扫描/启动、归还/卸载、位置迁移 | 各应用用例共享原账本/事务，宿主只组装/调度 | 已USER PASS；OPT-CONTENT-SYNC-01仍仅记录 |
| 配置/备份恢复/图标 | Application编排/端口，原离线恢复作为完整原子适配器 | 第15节已USER PASS |
| 来源修复/引用与海报管理 | 本批修复用例；其余海报/布局通过既有命令与纯规划 | 代码/定向通过，待下方一组人工 |

静态扫描ControlCenter/PosterSpatialRuntime/PosterVisualWindow：无Infrastructure using、SQLite、直接File/Directory业务IO；具体组合在Bootstrap/App生命周期宿主。App.xaml中启动配置读取/资源停止、中心自启适配器的组装位置以及Worker链接依赖仍属于R4，不据此宣布“App无平台依赖”。会话草稿/忙态/布局及命中发布仍待R2/R3。本审计确认R1本计划覆盖调用链的代码出口，不替代第16节人工或R2～R4验收。

### 集中人工：仅一组，待用户结果

使用当前“R1 · 来源修复”海报，文件均在本批samples目录：

1. 控制中心选择original.txt →“…”→“修复所选引用…”，通过文件选择器选择replacement.txt；预览先取消，原卡片不变。
2. 再次选择并确认修复。当前卡片应改为replacement.txt，另一张untouched.txt与原布局保持；samples/original.txt和replacement.txt均仍在原处，没有搬移/删除。正常退出后用脚本Open重开，修复结果仍保留。

失效路径/来源中途变化/版本过期等已用定向自动验证覆盖，不追加到人工清单。上述一组已获用户确认通过，R1最终登记完成，继续第17节；未获授权不合并。

<a id="r2-session-boundary"></a>
## 17. R2 会话状态、保护与安全点（2026-10-19，USER PASS / CLOSED）

影响面：海报的瞬态内容草稿、热度/主题候选、交互保护和待发布工作归属；不改文件事务、扫描频率、布局算法、原生拖放协议、手势或确认流程。

- Registry继续拥有可见会话代次和生命周期；每个WinUi会话拥有一个纯应用内容会话，视图只借用。关闭使未执行工作失效；挂起保留已接受派生状态但禁止发布；新会话不继承旧回调。
- 内容会话复用ContentHeatSession，统一草稿优先级、主题版本、安全点和合并调度；不复制缓冲，不新增计时器。窗口保留Dispatcher、原生捕获/菜单/命名控件及图像资源，通过明确开始/结束更新保护状态。
- 控制中心草稿/多海报互斥仍由现有应用协调者拥有，按现有通知刷新各自会话保护；不加入静态全局Busy，不让单张海报获得其他海报状态。
- 来源订阅的在途刷新使用绑定身份和会话关闭检查，合并刷新不因旧订阅迟到回调误清新订阅的待办。来源检查与文件事实不迁入视图。
- 删除被替代的窗口草稿/提案版本/排队/关闭字段与判断；原有原生资源释放、文件操作完成登记和稳定卡片/暖图机制保留。R3布局/绘制/命中发布不顺改。

退出与验证：会话策略无XAML/SQLite可测；保护期间不发布、解除后仅最新有效候选生效；关闭/挂起/旧绑定回调不复活或丢新工作。只跑受影响的会话/提案和生命周期测试，加现有隔离会话探针；正常/Review构建后集中人工最多两组自然连续操作。不重复R1文件完整矩阵，不提交/推送/合并。

### 实际职责、清理与行为边界

| 所有者 | 本批实际职责 | 不承担的职责 |
| --- | --- | --- |
| PosterSessionRegistry（沿用） | 可见集合、会话代次、取消/重建和环境恢复准入 | 不持有卡片树或文件事务 |
| PosterSpatialRuntime / 原控制中心会话（沿用） | 多海报互斥、空间交互、中心草稿及应用级在途文件操作；向会话通知中心保护 | 不复制每张海报热度/主题缓冲 |
| PosterContentSession（新增纯应用类） | 单会话草稿/历史、模式与交互保护、既有ContentHeatSession、主题版本、合并发布票据、来源版本和安全点 | 不读库、不调Shell、不创建Dispatcher或计时器、不持有控件 |
| WinUiPosterSession / PosterVisualWindow | 前者创建并关闭内容会话；后者持有捕获、菜单/命名控件、Dispatcher、图像和绘制资源，在现有边界通知状态 | 窗口不再拥有第二套提案/草稿/排队策略 |

- 删除窗口的_contentDraft/_contentDraftProjection、_contentHistory、_contentHeatSession、_contentThemeBuffer、最高主题版本、发布队列/代次/等待中心字段、_shellUiBusy、模式/命令/挂起字段，以及ContentGesture.Proposal重复草稿。生产和Review读取同一会话；无旧字段兼容转发或第二套执行分支。
- 发布使用PosterInputSessionKey＋单调票据；挂起失效旧票据但保留候选，关闭清空并拒绝迟到工作。宿主只TryEnqueue/应用结果，正常排队仍至多一份，不增加轮询。
- 来源绑定使用独立PosterSourceRefresh（原子idle/queued/closed三态）替代跨绑定共用的整型标志。旧订阅的迟到回调不能清掉新绑定的通知；原图片缓存/并发预算和来源版本见证不变。
- 业务决策迁出与接线属于本批行为保持型整理，无纯目录搬移。布局求解/卡片差异/同代绘制与命中仍在原宿主，明确留给R3；未制作真实热度、模板平台、AI或更新器，也未修改OPT-CONTENT-SYNC-01扫描策略。

### 验证与交付

- 67个不同的定向测试最终通过：PosterContentSessionTests新增5项、既有ContentHeatSessionTests、PosterSessionRegistryTests及既有架构边界项。初跑65通过/2失败均为新样例错误（同色主题被正确判为无变化、B海报使用A项目ID）；只修样例并重跑5项通过，原62项证据复用。TRX为out/architecture-r2-session/tests/r2-session.trx及r2-session-fixed.trx，不把初跑报全绿。
- 正常产品、Review及SessionFixture Release构建最终0警告/0错误；Review初编译暴露两个探针还读已删Proposal字段，改为读取同一会话后通过，不恢复旧字段。正常102/Review170编译集合及Preview隔离检查通过。
- 两个限定进程：Session PID10260验证编辑保护、最新候选、卡片/暖图保留、中心草稿阻止发布→取消显式唤醒→稳定后不反复排队；Source PID22640复用原来源探针，覆盖有界加载/通知合并、来源变化取消手势、成员变化、收展安全点以及隐藏迟到结果/重建。均PASS、退出资源0、无error；NativeMouse=false，不扩展为原生鼠标认证。证据分别位于out/drop-d3/architecture-r2-session-probe-session01/results.json与architecture-r2-session-probe-source01/results.json。
- 正常候选out/architecture-r2-session/candidate01/app，已启动PID43380，四海报Ready、无error。App SHA256=8945CF3E008AE02CC764520B6236E1CF3A628537131B775F3480A4DE1161465F；Application=CA37EB208BE911CFD41A5655B2AB455D2D6FE23EB9F2D85A92FCA0EBDAE381F3。
- 入口scripts/start-architecture-session-review.ps1，Open/Prepare/Status使用out/drop-d3/architecture-r2-session-review01；Probe Session/Source各用独立数据，不覆盖旧证据。复用已验收的四海报自建文件夹具，样例清单sample-manifest.json；不改用户配置，不安装，不写桌面文件。候选继承当前高完整性，人工不要求跨权限Explorer拖入。

### 集中人工（两组，USER PASS）

1. **连续交互**：A内移动卡片、空白框选；把A一项拖到B上方停留后Esc取消，再重复收起/展开。预期跟手/让位正常、取消后原文件/卡片不变、能继续操作，无关卡片不闪；B不被A的局部操作异常锁住。不重复R1文件迁移完整矩阵。
2. **中心与生命周期**：在控制中心给A的内容布局字段留下未提交草稿，确认A的冲突操作被保护、B仍可使用；取消后A可继续。随后关闭/重开中心、隐藏/重显海报，正常退出后脚本Open重启；没有残留忙态、旧草稿复活或布局丢失。真实热度未实施，不要求人工验证评分。

两组已由用户确认通过，R2已收口；继续第18节R3，本轮暂不合并。R1 CLOSED仅覆盖已审计现有用例，整体架构仍IN PROGRESS，发布/真实登录自启继续暂停。

### R2 人工收口

用户确认本次两组验证通过，R2 USER PASS / CLOSED。保留上述同包自动证据，不重跑R1/R2矩阵；当前分支暂不合并。

<a id="r3-presentation-boundary"></a>
## 18. R3 呈现与同代命中（2026-10-19，USER PASS / CLOSED）

按第3节R3退出标准执行，先迁出职责，再验证同代发布；不重写布局求解器、场景动画、文件业务或轮询策略。

1. 纯布局边界拥有求解缓存、代次、视口裁剪与空间候选组合；保留既有Solver/ContentSpacePublication。输出一份内容布局帧，不复制会话草稿或作者权威状态。
2. WinUI Presenter拥有卡片树、差异绘制、命名/选择视觉和暖位图；资源需求/加载/订阅单独归属，继续64项/4并发预算及来源身份校验。窗口仅提供当前事实、场景绘制适配和线程生命周期。
3. 输入几何从同一布局帧生成；明确准备、原生应用、绘制发布次序，失败不得发布新画面配旧命中。保留空间批次的既有读回与恢复，不另造布局或恢复引擎。
4. 迁出即删除窗口对应算法/字段和无用入口，Review探针读取新所有者，不用兼容字段保留两套状态。

仅验证直接影响面：布局/空间发布/资源缓存既有定向测试，加必要同代失败测试；正常/Review构建，复用现有增量/收展/同帧探针。集中人工最多两组连续操作；不提交、推送或合并。R4待做，整体IN PROGRESS、真实热度HOLD。

### 实际职责与旧路径清理

| 所有者 | 当前职责 |
| --- | --- |
| Application/ContentPresentationLayout | 既有Solver的缓存/代次、锚点/视口裁剪、空间候选组合；不可变ContentPresentationFrame供绘制与输入使用，ContentSpacePublication仍是空间连续性的唯一发布依据 |
| Application/ContentInputGeometry | 标题/动作、编辑边缘、卡片/循环副本、留白与受限导航的区域决策；输入布局携带同一内容代次，平台不重新解读布局 |
| App/Presentation/PosterContentPresenter | 稳定XAML卡片、CardPaint差异、选择/命名外观、解码位图集合与场景材质回调；不执行文件用例、读库或解算布局 |
| App/Presentation/ContentRasterBinding | 有界可见需求、缓存、并发请求、订阅/迟到准入及清理；Provider继续提供字节，Presenter持有UI位图，宿主仅提供当前生命周期事实和Dispatcher |
| PosterVisualWindow及原空间运行器 | 线程/原生窗口、视图事实适配、原始输入转既有类型化意图、实际区域应用及批次读回/恢复；场景端口、手势与文件业务不重写 |

- 删除窗口的CardPaint、卡片/位图字典、求解缓存/代次/视口/空间候选所有者及输入区域算法；Review和Lamp/Rain探针直接读取新所有者，不增加兼容影子字段。布局帧不成为第二份作者状态，仍从R2会话的Effective入口接收草稿/派生结果。
- 普通内容更新先应用同代命中，成功后才绘制；取消了ApplyFrame和过渡准备中的重复绘制。失败保留上一画面并暂停不可信路由，空间批次沿原恢复路径重建；不增加恢复流程或轮询。绘制异常也先停路由再尝试还原上一帧。
- 资源预算仍为64项/4并发；收起不是资源销毁边界；来源身份、绑定代次和窗口关闭仍拒绝迟到结果。常规图像完成只更新已有Image，不触发布局或中心刷新。指标指纹按presentation引用缓存，不在每次命中查询重复计算。
- 本批是职责迁出与接线，非无行为差异的纯文件移动：明确新增同代标签/拒绝和失败保留保护。未做文件业务、轮询策略、真实热度、模板平台、AI或发布更新。非交互式Presenter不新造事件总线；原生输入继续由既有类型化意图入口处理。

### 定向验证与候选

- 75个不同定向最终通过：布局/空间发布/提案/增量栅格/缓存共74项（其中新增纯布局5项），再补一个实际职责边界项。边界项初次字符串断言误匹配合法场景回调ApplyTemplateCardPaint，改为精确禁止旧记录/字典后单项通过；不修改生产接线迎合断言。TRX：out/architecture-r3-presentation/tests/r3-presentation.trx、r3-boundary-fixed.trx；初次失败证据保留。
- 正常产品、Review及SessionFixture最终Release构建0警告/0错误。中途修正新类型与已有框架/输入工具重名、Review旧字段引用及一处遗漏的方法调用；没有恢复旧所有者。编译隔离105/173（Review新增68）和Preview保护PASS。
- 最终Presentation探针PID12588：错代输入拒绝、实际原生输入尺寸拒绝、部分批次失败保留及恢复同代通过；40次内部预览保留3张卡片/暖图且0新增请求；1/3项外部悬停及40次重复Over保留卡片、0新增请求；6轮收展保留暖图、0新增请求。最终Source探针PID33220复用来源变更/并发预算/迟到结果与隐藏重建检查。均PASS、退出资源0、无error，NativeMouse=false，不冒充原生鼠标人工结果。
- 最终证据：out/drop-d3/architecture-r3-presentation-probe-presentation02/results.json及architecture-r3-presentation-probe-source02/results.json。01保留为清理前通过记录；02验证最终接线。Review App SHA256=70D8358ACAE243296FA0E380A7C97A8ED3AA9FAF3DDF2BD7BCC7490B6B598675。
- 正常候选out/architecture-r3-presentation/candidate01/app，独立数据out/drop-d3/architecture-r3-presentation-review01；PID26812已打开。App SHA256=3BEB8E8AA1994FEA97113705A6C897118D3444FEFCC3BDF71B069C1614F172F1，Application=D8BDF0ABF990011D42998C5F5041849C230CA1A2516C718B248E3BF9A7EEFB46；启动入口另锁定Domain。入口scripts/start-architecture-presentation-review.ps1，Open/Prepare/Status；Probe支持独立Run编号且禁止覆盖证据。PS5.1解析通过。
- 所有样例在本批out目录，sample-manifest.json记录原始样例哈希；不写桌面、不安装、不改真实配置/收纳文件、不覆盖R2候选。当前继承高完整性，不要求跨权限Explorer拖入。扫描策略OPT-CONTENT-SYNC-01仍仅记录。分支未提交/推送/合并。

### 集中人工（仅两组，USER PASS）

1. **增量与收展**：在A内连续拖动/框选，拖到B悬停让位后按Esc取消，再连续收起/展开A。无关卡片不闪，图像不退回占位，取消后文件/卡片保持，随后仍能继续操作。
2. **布局与命中**：调整A宽度或内容缩放，随后点选/框选/拖动可见卡片，操作标题按钮；画面与命中一致，B不受影响。正常退出后用脚本Open重开，布局和图标保持。

用户确认以上两组均通过，R3收口；保留最终同包自动证据，不重跑R1～R3矩阵。继续R4，不提交、推送或合并，整体架构与真实热度准入仍未完成。

<a id="r4-composition-boundary"></a>
## 19. R4 组装与 Worker 依赖（2026-10-19，USER PASS / CLOSED）

本批只改变现有依赖装配与所有权，不改文件业务、会话/呈现语义、扫描周期或发布行为。

1. 清理 App 运行路径的 ServiceProvider 查询：组合根负责构造并显式交付所需端口；主入口保持启动/退出编排。卸载/离线恢复不提前初始化正常运行资源，数据库、Worker、扫描和原生资源依原顺序关闭。
2. 用例/适配器只在 Bootstrap 装配，Window/Runtime 不自行创建具体文件业务；窗口、Dispatcher、原生输入和场景材质适配继续保留正确宿主，不为形式一致增加万能工厂。
3. LocalContentReferenceProbe 的原实现先纯移动至 Windows 平台层并核对字节；再独立修改命名空间/项目引用/调用者，取消 Worker 对 Infrastructure 源码的链接编译。Worker 不依赖 SQLite 或持久库。
4. 完整生产 partial 集合与项目依赖做少量架构检查，正常/Review 编译集合和打包载荷验证；只复用对应来源/生命周期探针，不重跑文件/安装矩阵。
5. 同步现状图、目标差异和唯一资源释放清单。集中人工最多两组连续体验；只有此轮通过后才审查基础架构出口，未实现的 AI/模板/热更新仍单列。

纯移动与依赖修改分开记录；移除被替代的服务查询、旧组装入口和链接编译，不保留影子兼容层。OPT-CONTENT-SYNC-01仍为待研究记录，本轮不调整。

### 已实现接线与清理

- 以Bootstrap/ApplicationComposition替代BuildServices/ServiceProvider：具体依赖按需显式构造，只有一个数据库所有者和一个Worker客户端；App运行路径只使用已知端口，无Resolve/GetRequiredService。删除DI依赖包和未使用的IPosterRepository/IStateProjectionRepository注册。
- Bootstrap/App.Startup是正常/维护启动组装入口；App.Lifetime只协调停止，App.DesktopEnvironment保留原生环境与时钟适配。原OnLaunched/退出/环境方法先原样提取，再修改依赖接线，未全量改名。控制中心自启动注册适配也从视图服务构造迁入Bootstrap。
- LocalContentReferenceProbe先从Infrastructure纯移动到Windows/Content，移动前后SHA256均B2F69E86668210CCC05AFAF6DD7C179390881E3E185302BE241ACFB609B2B17F；随后才改命名空间、引用和调用者。取消Worker的Compile链接；Infrastructure与Worker均直接复用Windows平台程序集，无复制探测实现/旧类型转发，无新增项目或数据库依赖。
- Windows仍只依赖Application/Domain。Infrastructure新增Windows依赖用于共享只读元数据探测；Worker只引用Application/Windows，实际content-worker载荷不含Infrastructure/SQLite。图标协议、源身份、预算与执行语义不变，不声称尚未测量的速度提升。
- 所有生产partial（含内置模块）的实际编译集合都检查组合根边界，允许构造的文件仅列六个Bootstrap文件。WinUiPosterSessionFactory保留每会话窗口/会话装配，Presenter/栅格绑定保留每窗口UI资源装配，App.DesktopEnvironment保留原生监听器；这些是有明确寿命的宿主适配，不是文件业务旁路。
- Review探针改读同一组合根，未恢复旧容器或保留两套接线。复用既有start-architecture-presentation-review.ps1，增加-Batch R4及已有Offline探针入口，未复制一份完整验收框架；R3默认入口和旧数据保持。

### 生命周期单一所有者

| 范围 | 创建时机／所有者 | 退出顺序 |
| --- | --- | --- |
| 配置/SQLite | 组合根按需创建；离线恢复不打开库 | 内容和回调排空后，组合根释放唯一store |
| 内容观察 | 原StartContentDiscovery；监听/计时器仍ManagedContentMonitor | 停止新操作后等待启动发现，再DisposeAsync |
| 海报会话 | Registry经WinUiPosterSessionFactory创建 | 先关闭内容会话票据，再释放窗口/原生资源 |
| Worker客户端 | 原内容组装点，组合根StartWorker | 会话关闭后StopWorkerAsync，随后等待内容完成登记 |
| 环境监听/恢复 | App.DesktopEnvironment、原调度节奏 | 停监听、取消恢复，等待在途任务后释放剩余宿主 |
| 中心/托盘/单实例 | Bootstrap启动，App.Lifetime协调 | 取消交互、释放窗口/托盘/实例，记录资源0，再关数据库 |

离线恢复/卸载准备仍在正常服务之前分流，不建海报/托盘/Worker或默认收纳目录。OPT-CONTENT-SYNC-01扫描策略未变；统一编码和25份冻结迁移未变。

### 定向证据

- 58个不同测试通过：ArchitectureTests、LocalContentReferenceTests、RealContentNativeTests。包含真实Worker元数据/图标、独立自建目标成功交接；非用户应用。重命名组合文件后只重跑一个所有权断言通过，不重复57项。TRX：out/architecture-r4-composition/tests/r4-boundaries.trx、r4-final-owner.trx。
- 正常产品、Review、SessionFixture最终Release均0警告/0错误；Worker独立publish成功，输出published-worker。初次正常构建暴露具体Worker字段仍留主文件，改归组合根；初次Review构建暴露旧探针DI依赖，同步清除后通过；没有为通过测试恢复旧代码。
- check-architecture检查108个生产源，Review176（额外68）；Preview保护PASS。初版检查大小写不敏感误报注释中的SQLite，修为C#类型名大小写后通过，不放宽文件例外。文本门947文件/25迁移；git diff --check PASS。
- 最终Review App SHA256=0B0D7E73B1BDADE6029061F82E1D071A78C7F7DCFBA873470C677C750CA244E2。Source PID53920复用有界资源、来源变化和隐藏重建；Offline PID53828证明离线恢复不启动正常消费者、不创建数据库。均PASS、退出资源0、无error，NativeMouse=false。证据out/drop-d3/architecture-r4-composition-probe-source01/results.json及probe-offline01/results.json。
- 正常产品烟测PID24400四海报Ready、退出0/资源0、无error；随后同包Open已启动PID18480供人工。正常候选out/architecture-r4-composition/candidate01/app；App SHA256=0EE1DCF737690285D31B765DCE6B31FCC27D955FECE0BA5F4C998170F80A529D。Application/Domain与R3已验字节相同，七项主程序/Worker依赖哈希锁在candidate01/candidate-hashes.json。独立数据out/drop-d3/architecture-r4-composition-review01，样例清单sample-manifest.json；不覆盖旧包、不安装、不写真实桌面。
- Archify现状图源与源码对应表见[本轮图与边界说明](../../20_Architecture/ArchitectureReview/R4/README.md)，HTML在out/architecture-r4-composition/current.architecture.html。9/9 showcase、0错误警告；Edge四档桌面尺寸检查通过，最大尺寸浅/深主题目视通过。图不是生产行为认证。

### 集中人工（仅两组，USER PASS）

启动：powershell.exe -NoProfile -File scripts/start-architecture-presentation-review.ps1 -Batch R4 -Action Open。仅使用本批自建A/B/C/D海报，不需要跨完整性Explorer拖入。

1. **连续使用**：A空白区新建文本并行内命名，重命名，再将其一次拖到B；随后内部编排/收展。预期图标正常、文件与卡片各一份、没有额外确认、无关卡片不闪。
2. **生命周期**：关闭/重开控制中心，隐藏/重显海报；从托盘退出，再用同命令Open重开。预期没有卡死/残留忙态、布局与内容保持，原生菜单仍可用。

用户确认以上两组均通过，R4 USER PASS / CLOSED。复用本节最终候选和自动证据；本次仅记录与制图，未重跑构建或行为测试，未改产品源码，未提交/推送/合并。安装/卸载、自启登录和历史事务不重复验收，已有携带项不补PASS。

## 20. 基础架构出口与目标对照（2026-10-19）

R1～R4的已有业务职责边界已验收收口，不再将“基础架构未完”与“未来能力未实现”混为一个状态。[当前架构与原目标对照](../../20_Architecture/ArchitectureReview/R4-Closed/README.md)给出逐项映射；原目标图保持不变，R4待验图保留为历史快照。

热度接点证据：R2 Session探针实际注入合法/过时/错ItemSet版本提案，验证编辑保护期间不发布、解除后只采用最新有效提案、派生结果不写作者布局；中心草稿解除由通知唤醒，不新增轮询。R3 Presentation探针覆盖同代发布拒绝、失败保留、增量卡片与暖图。R4的Application/Domain与R3字节相同；R4改动是组装和平台探测依赖，正常/Review共用组合根，定向构建/测试及本次人工已通过。这里是组合证据与源码链路核对，不声称R4重新执行了全部R2/R3探针。

下一能力可以通过事实读取端口/游标存储适配、纯评分策略、类型化提案生产者及Bootstrap接线增量接入；宿主保留调度和通知适配，不承接评分算法或SQL。策略热替换、通用能力权限/版本路由、热加载模板与异形/离散海报、AI接入、独立更新器仍各需自己的实现与验收。

OPT-CONTENT-SYNC-01的周期扫描成本尚待研究；本轮未改变策略，也未测得或承诺整体性能提速。发布同步和真实登录自启仍暂停，长测、多屏/DPI与真实满盘继续按原记录携带。本次不扩为新的人工清单。
