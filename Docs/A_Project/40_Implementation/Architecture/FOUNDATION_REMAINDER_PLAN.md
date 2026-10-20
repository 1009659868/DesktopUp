# 基础架构余项实施清单与退出标准

2026-10-20。状态：**本轮基础架构整理已按用户授权正式收口（USER ACCEPTED / CLOSED）；R1～R8、P1～P3均已验。** 最终复审见第30节，正式收口与当前架构图见第31节。收口仅限现有业务职责与已验优化范围，不认证规模性能或未实现平台。真实热度实施HOLD，不提交/推送/合并；历史锁冲突未定因继续保留。

工作分支：`impl/architecture-foundation`，本次复审为HEAD `87b81a3`及现有未提交R5～R8/P1～P3工作区，不只审查HEAD。此前提交/推送检查点和第10节接续快照保留为历史，不代表本轮已同步。待用户授权后才进行集成，不自动开工下一能力。迁移/编码修复证据见[修复记录](../V3-04/PREVIEW_INSTALLER_DELIVERY.md#2026-10-19-迁移指纹与统一编码修复)，不代替架构验收。发布同步仍暂停，安装/真实登录自启未复验。P1验收见第26节，P2/P3见第28/29节，当前复审及后续处理计划见第30节。

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
| 应用用例层 | R5映射余项与R6操作生命周期均已人工通过；旧适配入口及活动字段已删除 | R7/R8及P1～P3已收口；第30节复审确认业务编排与适配实现分属明确，不另建事实管线 |
| 稳定海报内核 | R2/R3已将会话保护、布局缓存、卡片/暖图分属明确所有者；宿主只完成原生提交 | R2/R3已收口：会话保护、纯布局、Presenter和同代发布已接线 |
| 存储与平台适配 | R1产品运行器已无SqliteStateStore；ContentWorker已引用Windows平台实现，不再链接编译Infrastructure源码 | R4已收口：显式组合根、唯一资源所有者、无跨项目源码链接 |
| 能力接入的现有部分 | R2会话拥有提案准入/缓冲/保护，R3拥有帧与呈现；窗口仅作线程/宿主接线 | 现有热度/主题接点已落地；通用权限、插件装载与模板包验证仍未制作，不以空接口补齐目标图 |

保留六个生产程序集，不强制一个逻辑模块对应一个新项目。App 在组合根引用 Infrastructure/Windows 是合法依赖；要消除的是**业务运行代码和视图**持有具体存储、直接执行文件业务，并非机械禁止 App 的所有实现引用。

## 3. 实施顺序与完成清单

R1～R4原切片功能验收保留。2026-10-20复审发现第4节原整体准入结论范围过宽，按第21节继续R5～R8；不把功能通过或依赖检查等同于所有业务职责均已收清。

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

**当前结论：现有业务职责边界通过最终复审；真实热度实施仍HOLD，等待用户决定下一步。** R5已补映射新建编排、R6已补操作收尾、R8已核清目录/旧入口，P1～P3也已人工通过；不再用已解决的R5缺口解释HOLD。第30节取代第20/21节阶段性整体结论，区分结构准备、规模性能与产品实施授权。没有证据要求为目标图继续无边界拆类，但也不宣布全部目标平台或规模成本完成。

- [x] R1/R5调用链已复核：映射新建的准备、创建、登记与失败收尾由应用用例编排，存储端口不包完整业务；实际副作用/事务仍由适配层执行。
- [x] R2 会话状态、交互保护、调度与关闭边界有明确所有者，能无 UI 验证。
- [x] R3 Presenter 与同代绘制/命中发布已接真实生产路径；稳定 Item、暖图及缓存边界保持。
- [x] R4组合根、Worker及编译隔离已验；R5/R8旧入口清理与最终生产编译集合复核通过。启动兼容桥仍有明确合法用途，不冒充所有内部构造均已迁到Bootstrap。
- [x] R1～R8/P1～P3相关自动证据与人工齐备，最终源码/成本对照见第30节。历史锁冲突首因未知、发布/自启与长期规模验证仍单列，不用新成功覆盖，也不拿R4旧图代表当前全部代码。
- [x] 现有提案注入证明消费者可输出ContentHeatProposal，经PosterContentSession和同代帧发布；无需窗口查询SQLite、持有评分或执行文件。复用R2真实宿主提案注入、R3同代/增量探针及R4依赖证据（见第20节）；事实读取端口/游标适配和纯评分策略仍需下一批实现。

获得下一功能授权后，才规划成功事实只读分页/幂等消费/稳定游标 → 默认评分与新项保护 → 原提案入口 → 既定发布节奏；不需要先实现AI/动态模板平台。真实热度的“打开只标记待更新，下次展开/明确刷新发布”是后续产品行为，不与本次复审混改。结构准备通过不代表这些生产者已经实现，也不代替成本预算与版本/取消策略设计。

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
| R5残余用例、R6操作生命周期 | USER PASS / CLOSED | 第21～22节；R6单次未移动未定因仍携带 |
| R7读取与观察成本 | USER PASS / CLOSED | 第23节，不把局部改善写成全部成本已解决 |
| R8目录与最终复审 | USER PASS / CLOSED | 第24节，67份纯移动、测试入口清理及目标对照 |
| P1归因与同轮安全共享读取 | USER PASS / CLOSED | 第25～26节；两组集中人工已确认，复用最终candidate02自动证据 |
| P2目标发现与无变化投影 | USER PASS / CLOSED | 第28节；本次用户明确确认此前已通过，保留11项定向及原始对照 |
| P3按目录观察与有界公平调度 | USER PASS / CLOSED | 第29节；本次用户确认通过，保留19项定向及原始对照 |
| 最终架构与成本复审 | REVIEW COMPLETE；现有业务职责边界通过，规模性能未认证 | 第30节；当前调用链/依赖门与已验候选证据，不等同全部目标能力完成 |
| 本轮基础架构整理正式收口 | USER ACCEPTED / CLOSED | 第31节；用户授权收口，当前图与证据清单已生成，图的浏览器/视觉复验未完成 |
| 后续能力与集成 | 热度实施HOLD，Git等待授权 | 未实现能力、未定因锁冲突及按条件成本计划分列，发布/真实登录自启仍暂停 |

下一动作：基础架构本轮已正式收口；Git集成或下一功能仍须单独授权，不再要求用户重复确认P2/P3或架构阶段是否收口。图的浏览器/视觉复验另列未完成，不影响已验业务结论，也不能补记为通过。剩余成本先按触发条件测量，不默认开启P4；不自动推进真实热度或提交/推送/合并。

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

本节是2026-10-19阶段结论；2026-10-20第21节复审已纠正整体准入判断，R1～R4原功能人工结果不变。[当前架构与原目标对照](../../20_Architecture/ArchitectureReview/R4-Closed/README.md)给出逐项映射；原目标图保持不变，R4待验图保留为历史快照。

热度接点证据：R2 Session探针实际注入合法/过时/错ItemSet版本提案，验证编辑保护期间不发布、解除后只采用最新有效提案、派生结果不写作者布局；中心草稿解除由通知唤醒，不新增轮询。R3 Presentation探针覆盖同代发布拒绝、失败保留、增量卡片与暖图。R4的Application/Domain与R3字节相同；R4改动是组装和平台探测依赖，正常/Review共用组合根，定向构建/测试及本次人工已通过。这里是组合证据与源码链路核对，不声称R4重新执行了全部R2/R3探针。

下一能力可以通过事实读取端口/游标存储适配、纯评分策略、类型化提案生产者及Bootstrap接线增量接入；宿主保留调度和通知适配，不承接评分算法或SQL。策略热替换、通用能力权限/版本路由、热加载模板与异形/离散海报、AI接入、独立更新器仍各需自己的实现与验收。

OPT-CONTENT-SYNC-01的周期扫描成本尚待研究；本轮未改变策略，也未测得或承诺整体性能提速。发布同步和真实登录自启仍暂停，长测、多屏/DPI与真实满盘继续按原记录携带。本次不扩为新的人工清单。

## 21. 复审修订与R5至R8实施计划

2026-10-20，审查基线impl/architecture-foundation@87b81a3。用户授权先更新结论，再实施R5至集中人工；不合并、不推进真实热度。R1～R4的真实用例、会话、Presenter、同代命中及Worker进展继续保留，但原“所有现有业务基础出口PASS”不足以覆盖以下源码事实。

### 87b81a3基线审查结论

- 映射新建经ContentCreationUseCase.CreateMapped回到MappedDirectoryService.CreateNew；状态检查、创建、扫描、登记和失败清理仍由Infrastructure串联。R5迁出这些顺序，不拆开原子提交、文件锁、身份/字节证明。
- PosterSpatialRuntime.FileActions的CreateContent在finally先Refresh再释放忙态；Refresh异常可能跳过释放。静态风险已确认，尚未注入复现，归R6单独修订，不混入R5结构变更。
- StateProjectionCoordinator.Refresh先完整Read再判断Revision；ManagedContentMonitor每配置一个2秒循环、全局脏标记和约30秒兜底，非每海报一个计时器。成本来源已确认，耗时未量化；OPT-CONTENT-SYNC-01仍先测量、不直接改调度。
- ManagedContentSynchronizer、PosterContentReturn等包装主要服务测试/验收；ContentRenameService仍有启动与schema核验调用，须迁移调用者而非直接删除。旧键盘入口有真实调用，不按菜单删减推定死代码。
- 现有check-architecture仍PASS（108产品源/176 Review源、额外68），只证明所列编译/依赖规则，不证明接口后无完整业务编排。

### 接续批次与出口

| 批次 | 工作 | 出口 |
| --- | --- | --- |
| R5 残余用例与旧入口 | 映射新建、关联映射校验/登记归应用用例；测试专用包装退出产品编译；迁出仍需的校验和精确清理后删除旧入口 | 一套生产执行链，无完整业务流程藏在CreateMapped存储端口；原事务/取消/未知提交及只清理自建未变对象语义保持 |
| R6 操作生命周期 | 登记、取消、退出等待、忙态释放的有限归组；Refresh异常修复单列行为变化 | 异常不遗留忙态；关闭可等待收尾，不重放副作用；不引入通用任务框架 |
| R7 读取与观察成本 | 测量空闲/单项/多海报查询、耗时和分配；按结果优化重复读取，另行决定观察调度 | 有前后证据，不牺牲一致性/暖图；不新增第二份权威状态，不盲删兜底 |
| R8 目录与最终复审 | 剩余目录/命名/压缩代码、少量依赖门、目标对照与实际调用链 | 无用包装/重复状态清理，允许的平台职责明确；源码和结论一致后再请求整体确认 |

### R5实施约束与验证选择

影响面仅映射内容新建与其校验/登记，以及旧测试适配入口的接线；不改手势、窗口忙态、扫描节奏、数据库schema、归还语义或成功事实。先保留纯移动前后字节证据，再单列结构接线修改；没有实际纯移动则明确记录，不能将方法抽取称为纯移动。

复用映射新建/原生新建/映射拖入与受影响测试适配的定向条目；新增仅覆盖应用层次序及提交回执不明、取消/对象被替换时的清理边界。正常/Review构建确认产品与验收接线，复用隔离候选入口；不跑全量合同、安装卸载或旧R1～R4人工矩阵。最终人工默认一组映射新建连续操作，只有实际另有影响才追加一组。

### R5实现与清理

状态：2026-10-20用户确认集中验证通过，R5 USER PASS / CLOSED。工作区基于87b81a3，未提交/推送/合并；以下原子保护、手势和界面语义保持。

- [MappedContentCreationUseCase](../../../../src/DesktopUp.Application/Features/Content/FileActions/MappedContentCreationUseCase.cs)拥有映射新建的版本/锁定判定、创建→扫描→锁定已建对象→二次扫描→登记及失败判定。ContentCreationUseCase显式路由，IContentCreationStorage.CreateMapped已移除，不再回到存储层整段执行。
- [MappedDirectoryUseCase.Actions](../../../../src/DesktopUp.Application/Features/Content/Observation/MappedDirectoryUseCase.Actions.cs)归属位置解析、失效/替代对象校验和拖入/原生新建结果登记。IMappedDirectoryStorage仍保留原事务及身份账本；IContentObservationFiles只提供平台事实。
- [LocalOwnedContentCreationFiles](../../../../src/DesktopUp.Infrastructure/ContentActions/LocalOwnedContentCreationFiles.cs)保留创建见证、固定句柄、字节/空目录核验及精确清理。未知提交先读回登记；读回失败或已有卡片时不清理、不重试文件操作。未新增schema/账本，也未改变前后读集或扫描次数。
- 删除生产MappedDirectoryService。生产ContentCreationService、ContentRenameService、ManagedContentSynchronizer、PosterContentReturn退出编译；测试需要的构造与故障注入归DesktopUp.Testing下对应Fixture，不复制用例实现。Review归还探针直接复用已注入ContentReturnUseCase。启动改名核对直接组装原用例，schema核验归SqliteContentRenameJournal，精确清理归LocalOwnedContentCreationFiles。
- 直接调用者已同步到新用例。CrossPosterTransfer/文件动作适配器持有同一个无观察调度用途的映射用例，减少原循环中重复构造；映射观察的settling会话和监视器仍独立，不共享其可变扫描状态。不据此宣称整体性能提速。
- R6的Refresh忙态异常、R7读投影和扫描策略本轮未改；没有删键盘入口、生产账本或原安全证据。

### 文件移动与结构变化分离

四个包装先以apply_patch原字节移至测试目录；git hash-object --no-filters与HEAD blob逐个一致，之后才单列命名/namespace/接线调整。它们最终成为ContentCreationFixture、ContentRenameFixture、ManagedContentSyncFixture及ContentReturnFixture。以下是纯移动检查点，不是最终修改后摘要：

| 原生产文件 | 原字节与移动后共同Git blob |
| --- | --- |
| ManagedContentSynchronizer.cs | f610f2ee0b37c067e245eb6147272684cb437a63 |
| PosterContentReturn.cs | 53d9b5a222145fac0f60fc4e2bc7c6ca500d8099 |
| ContentCreationService.cs | 2ba40550278fb0dd1e246f67087e7c3457dbae5a |
| ContentRenameService.cs | 5d706bc8d6f51c0485ddf7dad30acc6ac8dfe928 |

映射方法改为端口、拆分创建见证及调用接线属于结构修改，不冒充纯移动。本轮未授权Git提交，所以不建立移动/实现提交；后续获授权时按上述边界组织。启动脚本编辑后仅对该文件恢复UTF-8 BOM/CRLF，未格式化全仓库。

### 定向证据与候选

- 三次定向共130个不同条目、0失败：首轮映射及既有用例33项；映射创建6种内存端口情形、创建/原生菜单/映射拖入/改名及旧夹具接线89项；最后映射适配复用的跨海报4路文件/目录8项。TRX在out/architecture-r5-content/tests。没有跑累计全量合同。
- 新增仅6个映射用例情形及1条产品程序集边界断言；其余复用已有用例。内存端口确认次序/取消/对象变化/提交失败/回执丢失/读回失败，物理防误删继续用既有映射故障测试。
- 最终正常产品、Review及SessionFixture Release均0警告/0错误；check-architecture完整108产品/176 Review源和Preview隔离PASS；源文本951文件/25份冻结迁移PASS。
- 复用IncomingProbe与InlineNamingProbe，仅增加R5限定入口。PID19124实际映射新建/行内命名/取消默认名/冲突继续编辑/失焦保存及卡片登记PASS，退出activeResources=0，无error。nativeMouse=false、keyboardDispatch=false，不冒充真实鼠标认证。证据out/drop-d3/architecture-r5-content-probe01/profile/logs/desktopup.jsonl。
- 独立候选out/architecture-r5-content/candidate01/app，Review在同级review，fixture在同级fixture；旧候选和旧配置保留。正常App SHA256=40CCFA171E2C7BC5B53D10B338FB4E3838D267149F5E55E2A7751EFBB20A7C52；Review App=4885FC812B69F7ADD349A3AD7FA28CA68BF7B24C24851C63A3451105FDAA4E4E。两者Application/Infrastructure/Domain哈希相同，启动器绑定candidate-hashes.json。
- 正常候选已打开PID53060；自建A/B普通收纳、C/D映射及样例仅在out/drop-d3/architecture-r5-content-review01。未安装、未改启动项、未写桌面、未操作真实文件。人工数据留待验收，自动样例均在隔离目录。

### R5集中人工 仅一组 USER PASS

启动/重开：powershell.exe -NoProfile -File scripts/start-architecture-file-actions-review.ps1 -Batch R5 -Action Open；Status只读查看路径。使用C映射海报，不需要外部Explorer拖入。

在C空白区新建文件夹和文本：直接进入行内命名；确认后改名一次，尝试同名应保持可编辑、不覆盖；再新建文本后按Esc，应保留默认名称的真实文件和卡片。到本批mapping-C核对名称/内容一致，正常退出后重开确认仍在。没有新增确认窗、重复卡片或文件丢失即符合预期。不重复既有拖放/热度/安装卸载验收。

用户确认本组符合预期，复用本节最终候选和自动证据收口，不重跑R5矩阵。现在继续R6；R7扫描调度仍先测量，R8最终对照后再请求整体确认。当前真实热度HOLD，不合并。

## 22. R6 操作生命周期与异常安全（2026-10-20）

状态：2026-10-20用户确认两组均通过，R6 USER PASS / CLOSED；另有一次批量拖入未移动的待查记录继续保留，见下。影响面是宿主文件操作的活动归属、停止令牌、完成等待及已登记UI操作的退出排空；不改文件执行/账本/原子提交，也不调整扫描、热度或手势协议。

- 将分散的忙态/海报ID/文件任务/停止令牌归一个内容操作生命周期所有者；文件操作有明确的取得与释放范围。保留窗口会话内的编辑/原生手势保护，它与全局文件操作互斥职责不同。
- 保持退出先停止文件操作、再关闭会话/Worker、最后排空UI回调和释放数据库的顺序。登记的是完整UI操作，不只后台IO；已失败的操作仍可排空，不在退出阶段重放文件动作。
- 新建后的Refresh异常可能阻止忙态释放：作为单独的行为修复，以故障注入确认释放并能再次操作。入场通知、收尾通知异常也不能遗留活动所有权。
- 原生拖出跨越同步OLE与异步收尾；跨海报接收必须等源端收尾后重新检查准入。只整合生命周期，不改变路径/落点/共享规则或增加确认。
- 删除被替代字段、临时完成任务和重复观察器；本批不计划目录搬移。方法抽取/接线不是纯文件移动，行为修复与机械替换分别记录。
- 验证只选生命周期异常/取消/排空，以及现有跨海报交接定向链；隔离Review验证实际新建故障后重试与退出。正常/Review构建后集中人工最多两组（连续使用、取消/退出重开），不重跑安装/卸载、旧矩阵或全量合同。

出口：活动释放不依赖Refresh成功；退出不提前关闭被任务使用的数据库；跨海报队列和取消可继续正常工作；旧状态/等待旁路清除；无通用作业框架、无新增轮询/额外确认。

### R6实现与清理

- ContentOperationLifetime是内容运行时唯一的全局文件活动/停止/完成登记所有者；FileOperation记录目标与可选源海报，释放幂等，旧范围不会释放后来的范围。这里只管理寿命，不执行I/O，也不包业务事务。
- 删除原_fileTransferBusy、_fileTransferPoster、_crossPosterSource、_fileTransferStopping、_fileTransferTask和_realContentOperations；原生退出的独立TCS改为同一活动范围，删除新建/删除两份ObserveForShutdown及归还临时done任务。全UI任务仍登记，新建/命名编辑器和错误窗不被缩成后台IO等待。
- BeginFileOperation负责入场失败释放；FinishFileOperation在finally释放后再发活动通知。新建后Refresh抛错不会卡住后续操作；底层已提交文件不回滚、不重放，故障仍返回调用者。
- 原生拖出完成从async void改为登记的Task；按意图ID和完成状态拒绝重复回调。源范围结束后才唤醒跨海报接收，目标重新检查准入；原始路径、一次按住、Shell落点和确认规则不变。
- 停止先关文件准入再取消，等待当前文件范围收尾；宿主仍先关会话/Worker再排空完整UI任务，最后释放数据库。排空等待包含已登记回调产生的子任务，失败记录而不打断退出或重试；无新定时器/轮询。
- 窗口仍保留会话级FileAction/手势保护，进入停止状态后不再接新的命名/新建/删除/Win菜单/打开；入场通知异常会撤回本窗口FileAction标记。每海报保护与文件执行互斥不是两份内容状态。
- 本批没有文件移动/目录调整。机械替换与上述异常语义修订分别列明；R5此前纯移动证据仍在§21。未改存储事务/SQL、扫描、热度、安装、自启或发布分支。

### R6验证与候选

- 定向11项通过：ContentOperationLifetimeTests四项（范围互斥/旧释放、异常重入/停止等待、退出期子任务/失败、取消回调与诊断异常），复用ContentTransferUseCaseTests七项。新增不按旧矩阵扩张；TRX：out/architecture-r6-lifecycle/tests/r6-lifetime.trx。首轮测试编译因Assert.Throws重载歧义失败，明确Action后通过；非产品缺陷。
- 正常、Review、SessionFixture Release构建均0警告/0错误。check-architecture：生产108源、Review177源（额外69），Preview隔离通过；源文本954文件/25冻结迁移通过；旧字段/ObserveForShutdown搜索无残留。
- 正常候选out/architecture-r6-lifecycle/candidate01/app；Review同级review，fixture同级fixture。正常App SHA256=E3C0C54D7D129158B50F0C587260568A11DA70839DA87BD6B6513412E4A5152B；最终Review App=FC62B5A6E14CFEFEF7BD6E0AAB097EC6461B5C0C9B83A3B23B6830EFAC0FCBEB。Application/Infrastructure/Domain相同，四项正常候选摘要绑定candidate-hashes.json；仅Review含故障端口替身，不在产品埋测试开关。
- 最终隔离探针probe03/PID47384：现有真实用例接线的批量拖入、跨海报源端排空/目标接收、模拟外部Shell结果及实际映射Shell拖入通过；新增真实创建后Refresh失败、入场通知失败、已取消请求、重新发起操作、原生取消与重复完成回调通过。每个原生意图只完成一次，退出资源0，无error。鼠标输入未自动验证，nativeMouse=false，不冒充原生指针验收。probe01为加入重复回调检查前的历史通过记录。
- 正常包PID2924烟测：四海报ready，定时正常退出0、activeResources=0、无error。人工数据out/drop-d3/architecture-r6-lifecycle-review01；四张自建海报和文件仅在本批out，旧候选/旧配置保留，未安装、未写桌面、未操作真实文件。复用start-architecture-file-actions-review.ps1扩展-Batch R6，R1/R5默认与数据不变。

### 单次未移动记录（携带，不掩盖失败）

probe02/PID22576在进入新增异常检查前，旧R1批量拖入链的第四项sample-4.txt返回NotMoved，前三项成功；该次整组失败。SQLite账本phase=7并自动收尾，错误为“原文件未移动；Source still exists; preserve both paths for review.”。只读核对源SHA256与原计划一致、目标不存在；退出资源0。未自动重试该文件、未删除失败样例、未改账本或放宽安全保护。

最终同包probe03在全新独立样例上通过，但这不能证明probe02的初始中止原因。原始底层异常被后续收尾描述覆盖，现有证据不足以归因于文件占用、观察竞争或本轮接线；保留out/drop-d3/architecture-r6-lifecycle-probe02及待查事项，不能记“从未失败”或已修复此项。后续若复现应先保留第一失败阶段/原始异常，再决定修复，不能靠重复跑测掩盖问题。

### R6集中人工（两组，USER PASS）

正常候选已启动PID53620，四海报ready、无error；不含Review执行器，配置与R5分离。

启动/重开：powershell.exe -NoProfile -File scripts/start-architecture-file-actions-review.ps1 -Batch R6 -Action Open。

1. **连续操作与取消**：在A中新建文本并命名，将它拖到B，再拖动按Esc取消，然后再正常拖回A。可顺手删除本次新建样例。预期无额外确认、无重复/丢失，取消或完成后可立即继续新建/拖动，没有残留忙态。
2. **带编辑状态退出**：在C中新建文本、保留行内命名状态，从托盘正常退出，再用同命令重开。预期退出不卡住，默认名或已确认名称的文件与卡片一致，继续新建/命名正常。只用本批隔离样例，不重做安装卸载、外部权限或旧人工矩阵。

用户确认两组符合预期，R6收口，复用本节最终候选/定向证据，不重跑R6矩阵。probe02未定因事项不随人工通过自动关闭。现在进入R7，R8再做最终职责对照；真实热度HOLD，不提交/推送/合并。

## 23. R7 读取与观察成本（2026-10-20）

状态：USER PASS / CLOSED。用户确认本节两组验证均符合预期；复用最终candidate02，不重跑矩阵。已先完成基线，再实施单事务共享读取。自建1/6/12映射海报，每张20项；记录空闲投影、监听目标发现、无变化核对和实际监听变化的耗时、分配及端口调用次数。不是SQL语句计数，也不是UI帧时长或CPU占用。测量夹具与产品分离，同一场景/工具链做前后对照。

决策边界：只有实测显示重复读取成本明显，才选择窄读取或消除重复扫描；保留同事务一致性、现有schema/数据校验、目录身份、二次枚举和CAS。不能只看Revision就跳过完整性保护，也不先建第二份权威缓存。2秒事件合并/约30秒兜底/失联重试暂不改；先量化其实际成本。

出口：可重跑的前后数据；真实改变/取消/失联仍按原规则；被替代入口清理；无不受限缓存、额外轮询或新任务框架。只做受影响读取/观察定向验证和一次隔离接线检查，集中人工最多两组。不扩到热度/模板/发布，不合并。

### R7决策与清理

- 最终只改监听目标发现：新增IContentWatchTargetSource读模型端口，SqliteContentWatchTargetSource在一次现有ExecuteRead内读完整快照及受管登记，分别生成递归受管/非递归映射目标。两类目标同事务，不再各读一份完整状态。ManagedContentSyncUseCase仅供旧发现入口使用的ActivePaths已删除；MappedDirectoryUseCase.ActivePaths仍被跨海报身份检查使用，保留。
- SqliteTransferJournal.ReadStorageLocations提取为事务内共享读取。公开StorageLocations仍读取完整快照，所有调用者的解析/登记容量保护保留；根记录的严格解析不会因只取路径被省略。不新增缓存、任务层或定时器。
- 曾探索窄目录表读取和ReadIfChanged，但审查发现VerifyCurrentSchema对部分空间数据只验表结构，ReadSnapshot另有解析/语义校验。不能用相同Revision或schema验证冒充这些保护。上述试验接口、条件读取及窄表帮助方法全部撤回，不留兼容入口。candidate01/after01/after02仅是中间实验，不是交付或最终收益；after02还与构建重叠，不采用其耗时作最终对比。
- 最终保留投影Refresh、2秒事件合并、约30秒兜底、全局dirty、失联重试、目录身份、二次扫描及CAS。ReadSnapshot/存储验证未因本批减弱；不更改文件动作、暖图、布局或退出逻辑。本批没有文件移动，只有有限行为/读取接线变化。
- 实测仍显示Reconcile随目录数增长、重复整体验证与全目录核对成本明显。单次读取合并不解决全局dirty、失联频繁重试或每访问完整校验问题；这些留在OPT-CONTENT-SYNC-01，由R8明确边界与后续决策，不宣称整体性能已解决。暂不实施目录脏集合或校验缓存。

### R7验证口径

SessionFixture新增--architecture-cost只在新建out/drop-d3子目录运行；同进程加入上一版双读取的测试用对照表达式，不编入产品。每操作2次预热、12次同步样本（核对6次），记录中位数/P95及当前线程分配；不是全机内存峰值。6张样例观察33秒空闲+7秒真实文件事件，检查最终新增项确实入库。旧入口表达式仅作为可重跑的性能基线保留在测量夹具，生产没有旧通道开关。

定向范围为7项ReadObservationCostTests（含保留的空间连续状态语义校验反例）及5项既有变化/失联/Watcher/CAS/同代投影检查。初次夹具3项失败分别是损坏样例先被SQLite CHECK拦截、删除样例缺CatalogVersion；修正测试构造后复验，不隐藏失败，不因它们修改产品保护。既有11项探索版本通过不替代最终12项记录。

### R7最终测量（candidate02 / final01）

同一次进程/同一批数据，上一版双读取表达式与产品当前路径分别预热后对照。MB为十进制当前线程分配，非工作集；12个计时样本不能代表长期尾延迟。6→12场景跨过运行时预热/分层编译，不能用跨行耗时倒推出规模变大更快，只比较同场景旧/新路径。

| 映射海报 / 项数 | 监听发现中位ms（旧→新） | P95 ms（旧→新） | 平均分配MB（旧→新） |
| --- | --- | --- | --- |
| 1 / 20 | 8.06 → 3.90 | 11.06 → 4.70 | 1.016 → 0.507 |
| 6 / 120 | 18.44 → 8.90 | 19.26 → 10.42 | 4.793 → 2.401 |
| 12 / 240 | 14.08 → 7.05 | 15.08 → 8.28 | 9.453 → 4.729 |

- 每次发现旧逻辑端口调用为Locations + ReadState，最终为一次IContentWatchTargetSource.Read。计数来自夹具端口包装，不是SQL条数；完整验证仍会读取内容表。
- 6张/33秒空闲：17次发现、2次核对、0项变化；再新增一个样例，累计40秒20次发现、5次核对、1次变更入库。与最初基线次数一致，无新增轮询或并发核对。
- 未改变的无变化Reconcile：1/6/12场景分别58.57/214.80/293.69ms、5.90/48.37/144.11MB；最后一项仍是明显成本，不列为本批改善。投影Refresh保持原实现，12张暖态6.77ms/4.72MB；不能靠跳过语义校验优化。
- 原始基线out/drop-d3/architecture-r7-cost-baseline01/cost.json；最终数据out/drop-d3/architecture-r7-cost-final01/cost.json。基线执行器保留out/architecture-r7-cost/baseline/fixture；最终SessionFixture可在任何全新out/drop-d3目录以--architecture-cost重跑同进程对照。中间after01/after02不作为最终数字。

### R7最终候选与证据

- candidate02的正常/Review/SessionFixture Release均0警告/0错误；12项定向TRX：out/architecture-r7-cost/tests/r7-final-candidate02.trx。源码文本957文件/25冻结SQL通过；架构检查生产108、Review177（额外69）、Preview隔离通过。撤回的ReadIfChanged/窄表帮助方法及Managed.ActivePaths搜索无残留；Mapped.ActivePaths保留真实调用方。
- 正常包out/architecture-r7-cost/candidate02/app。App SHA256=037C9BE249B706D6BF9CCA84D580442B6FE0ADD8176D5D326805AD090C9E1F6A；Application=54C5D29B016DEECA2ABC0B11C6E25B0424FFD346BAC4D9074993707B55407CC5；Infrastructure=7485432C4D60DC6E2FF2F9FAA87118C81BB19B870038C077DA41A2155C727271；Domain=1BDA105A09F1A7AC1D12C8AEA9B3FAFE33D8E42768B829167C46957B6AB4046E。摘要绑定同级candidate-hashes.json；Review App=17D0DEAF76695DFE76320515883132F9B5AFC9220FD0C478CB5D5C73869352D7，核心程序集与正常/测量一致。
- 最终probe02（out/drop-d3/architecture-r7-cost-probe02）复用现有R1观察接线，不新增执行器：PID47952在线映射创建/新增/改名/失联恢复、普通受管外部移出通过；PID45624离线新增后的启动观察通过。实际生产monitor/Dispatcher，不强制Invalidate；两段退出资源0、无error。nativeMouse=false、picker=false，不冒充原生鼠标/选择器人工验收。probe01是撤回试验的中间结果。
- 正常包PID32464烟测会话ready、退出0、资源0、无error；随后最终人工候选PID35156已启动并ready。人工数据out/drop-d3/architecture-r7-cost-review01。旧R6用户已正常退出，所有新样例留在本批out，未安装、未写桌面/注册表、未处理真实文件。脚本只增加-Batch R7，默认R1路径/配置不变。

### R7集中人工（两组，USER PASS / CLOSED）

启动/重开：powershell.exe -NoProfile -File "G:\A_\Tools\DesktopUp_V2\scripts\start-architecture-observation-review.ps1" -Batch R7 -Action Open。

仅用out/drop-d3/architecture-r7-cost-review01内样例：

1. **普通收纳**：在当前海报新建一个文本文件；从内容的“在资源管理器中定位”进入实际存放位置，再用资源管理器剪切到本批external-target。预期原海报卡片数秒后自动消失、目标文件保留；不需要点击刷新。
2. **映射观察**：控制中心“新增海报”创建独立目录映射，选择本批mapping-input；在资源管理器把其中mapped-sample.txt改名。预期映射卡片自动改名且不重复，普通海报不受影响；随后正常收起/展开，已有图标保持正常。

候选在当前宿主权限下启动（isElevated=true）；本批不补普通权限原生拖放验收，也不改变UAC/UIPI。人工第一组用新建即可，不要求跨权限拖动。

不重跑卸载、原生拖放全矩阵或R6退出异常；这些不是本批变更。本轮用户确认后收口R7，进入R8最终职责/目录/性能边界复审；OPT-CONTENT-SYNC-01及R6旧probe02初因仍携带。整体架构IN PROGRESS、真实热度HOLD，不自动合并。

## 24. R8 目录整理与最终架构复审（2026-10-20）

状态：USER PASS / CLOSED。用户确认本节集中验证符合预期，复用candidate01及最终自动证据，不重跑R8矩阵；在impl/architecture-foundation继续，不提交/推送/合并，不进入真实热度。

### 实施清单与退出标准

1. **先纯移动**：PosterSpatialRuntime及WinUiPosterSession归App/Runtime/Posters，PosterVisualWindow归App/Views/Poster；基础SQLite状态仓储与事务账本归Infrastructure/Persistence/State、Transfers。保留类型/命名空间、程序集、XAML路径、资源名、SQL与实际执行逻辑；移动前后逐文件SHA256一致。现有Presentation、Bootstrap和内容用例不重复再包一层。
2. **再独立清理**：删除仅测试使用的生产ContentDeletionService包装，以及CrossPosterTransfer/MappedDirectoryIngress上的测试专用Execute/Checkpoint便捷入口（保留真实存储适配类型）；测试直接组装同样用例和端口，不改变Shell删除/缺失确认/账本规则。源码锚点机械更新；递归依赖检查不能因目录下沉漏查。没有证据的“无用代码”不删除。
3. **职责复审**：逐条核对目标图的现有业务链、所有权、异常停止与依赖方向，明确合法的原生宿主/事务适配职责。不把partial目录归组说成拆分了运行类型，不把已有模板接点说成动态平台完成。
4. **成本后续计划**：根据R7证据区分完整校验、投影读取、真实枚举与调度放大，列出先测量/后修改的切片、保留的安全语义和出口；本批不更改观察频率、不添加缓存/轮询层。
5. **精简验证**：架构/路径锚点与删除用例定向集合；正常/Review构建及生产编译集合；一次独立正常包启动/退出。纯文件移动不要求重验所有文件业务；如发现行为变化，另行说明并定向验证。

退出：纯移动证据可核对；生产无旧删除包装；移动后的门仍覆盖真实源文件；正常与Review资源/编译不退化；对照结论明确“已落实、有限遗留、未来能力”。用户已确认本批收口，成本与旧probe02未定因不自动变成PASS。

### R8实际修改与分阶段证据

- 第一阶段只移动67份文件：App/Runtime/Posters 23份、App/Views/Poster 31份、Infrastructure/Persistence/State 7份、Persistence/Transfers 6份。以包含已通过R5～R7未提交内容的当前工作树为基准，**不是Git HEAD**；逐份移动前后SHA256完全一致，原路径均消失。[R8_MOVES.json](R8_MOVES.json)保留映射和摘要。XAML、命名空间、程序集、内置模块、25份SQL迁移资源及资源名称均未改。不是把partial拆成新运行对象，也没有多一层转发。
- 第二阶段删除生产ContentDeletionService（只服务测试的包装）；CrossPosterTransfer/MappedDirectoryIngress保留真实存储适配，但删除只有测试调用的Execute、可选Shell构造参数及Checkpoint字段。测试直接组装现有Application用例，故障注入仍走其既有checkpoint参数。生产Bootstrap早已直接组装这些用例，本批没有改变执行路径。没有保留旧别名或替代生产包装。
- ArchitectureTests、MenuInputDiagnosticTests、SpatialAnimationTests只更新文件锚点；运行宿主检查显式指向新目录并断言非空，Worker检查改为递归并排除bin/obj，防止以后下沉目录漏查。正常/Review实际编译集合门不依赖旧根目录。
- App根级CS由90降至36，Infrastructure由54降至40；仍保留与XAML对应的控制中心partial以及真实平台/存储辅助类，不为“根目录清零”移动资源或增加碎片。紧凑事务代码仍有可读性债，后续只随热点用例整理命名/局部方法，不做无证据全库格式化或扩张接口。
- src/README与当前CODE_ANCHORS更新为实际路径；旧R4图源、历史日志和A_MOVES是当时快照，不改写为今天的证据。本批未生成新架构图。

### 目标架构逐链复审

对照对象为[原目标图源](../../20_Architecture/ArchitectureReview/target.architecture.json)，不是把R4现状图当成目标。下表判断针对现有业务，不能推导未来平台能力已完成。

| 边界 | 真实调用链与所有者 | 结论 / 允许保留的责任 |
| --- | --- | --- |
| 薄界面与文件用例 | Views/Poster菜单/命名 → Runtime/Posters文件动作 → ContentFileActionService → 创建/命名/删除用例 → 文件、Shell、SQLite端口；打开事实由ContentActionService管理 | 已接线。窗口保留交互和Dispatcher，不持有具体数据库；属性/Win菜单宿主句柄仍是平台输入，不另造业务服务 |
| 拖放与文件安全 | Runtime/Posters原生手势 → Managed/MappedIngress、CrossPosterTransfer、NativeShellExit用例 → scoped attempt / journal / Shell | 循环、取消和提交顺序在Application；适配层保留固定句柄、文件身份、原子CAS、受管关系/结果见证与事务内验证。这些不拆成窗口回调，不重复执行物理结果 |
| 生命周期/配置 | 删除海报/卸载 → ContentReturnUseCase / UninstallContentUseCase；位置设置 → StorageLocationUseCase / StorageRelocationUseCase；引用修复/便携/图标 → 对应Application端口 | 已有用例为唯一流程入口；SqliteContentStartupSteps仍作为原恢复执行器桥接，调用既有归还/迁移/命名核对，不另有策略。桥接不能继续承载新业务决策 |
| 会话与瞬态状态 | 会话注册表 → WinUiPosterSession → PosterContentSession；全局文件活动由ContentOperationLifetime统一登记，停止先关准入再排空 | 一次会话代际、一套编辑/提案保护；全局文件互斥与每海报手势保护不是重复事实库。原生资源仍随宿主释放 |
| 布局、呈现与命中 | ContentPresentationLayout准备帧/ContentInputGeometry → Views/Poster同代原生准入 → PosterContentPresenter/ContentRasterBinding呈现 | 已有帧缓存、稳定卡片、暖图和迟到准入；窗口保留实际绘制/提交/回退，不拥有另一个布局求解器。普通内容帧与窗口几何批次各保留原有失败处理 |
| 组装与Worker | Bootstrap/ApplicationComposition及5个精确组装文件 → 唯一store/Worker；App.Lifetime按操作→会话→Worker→UI任务→数据库收尾 | 无运行时ServiceProvider；App允许在组合根引用Infrastructure；Worker仅引用Application/Windows，无存储实现链接。六程序集不增加 |
| 读取与观察 | ManagedContentMonitor → ContentObservationService → managed/mapped用例与IContentWatchTargetSource → SQLite/files | 职责分离成立，**规模成本未闭环**。R7只合并发现读取；调度放大、完整校验和无变化投影仍待P1～P3 |
| 后续扩展 | 已有热度/主题提案以海报身份、项集合版本、会话保护进入内核；Lamp/Rain仍是构建内模块 | 只有现有接点。真实评分/策略热替换、任意形状与离散模板、动态插件/AI权限、独立更新器均未制作，不能由接口名称判PASS |

复审结论：现有业务的主要责任边界已有可执行链路与测试约束，R8删除了最后本轮确认的三处测试便捷入口；未发现需要为本批再新建项目/通用容器的依据。仍有较大的宿主partial和事务适配类，目录归组改善导航而不是自动降低复杂度；“没有发现”不等于证明全库无死代码。用户已确认本轮**现有业务职责边界**整理，不宣称全部目标能力或规模化性能已完成。

增量开发约束：新规则先落Domain/Application现有用例或纯策略，必须有清楚的输入/结果/版本/取消语义；只有新的外部副作用才增端口和适配，在Bootstrap接一次；窗口只增加必要交互绑定。动态模板/AI未来通过提案准入，不直接写库或改窗口字段；发布更新另以进程退出/数据兼容边界设计。不得为了图形对称给每个类配一层接口。

### 剩余读取与观察成本：处理顺序及退出标准

OPT-CONTENT-SYNC-01保持OPEN。推荐在重新评估真实热度前先完成P1证据及安全读边界；P2/P3依据测量分别决策，不在R8顺便改调度。它们是明确的后续计划，不代表本次已实现或已获后续自动开工授权。

| 顺序 / 影响面 | 下一批要做什么 | 可验证出口与不可牺牲的规则 |
| --- | --- | --- |
| P1 读取/校验归因与最小共享读模型（先做） | 在现有测量夹具分别记录VerifyCurrentSchema、ReadSnapshot、投影拼装、枚举与提交的耗时/分配及真实SQL调用；不能再用端口次数冒充SQL数。绘出本操作所需验证清单后，优先合并同事务重复读取；再决定是否值得细分读取/校验职责 | 同机独占1/6/12相同数据，至少3轮同场景旧/新对比，保留中位/P95、分配与读取次数；无稳定收益就撤回。schema、内容关系、空间连续性、根登记解析及相同Revision下损坏反例均不得漏验；不增加第二份权威缓存。不能只以Revision不变跳过ReadSnapshot |
| P2 目标发现与投影无变化路径（P1后按收益选择） | 评估监听目录随配置/位置/绑定变更失效更新；稳定空闲不每2秒重读整库。投影是否可复用已验证快照需先定义权威来源和外部改库失效条件，不能让未经验证的数据变成缓存 | 记录每分钟空闲完整读取次数确实下降；绑定变更、迁移、启动离线变化、根失联/恢复、漏事件仍能最终收敛；旧卡片/暖图无回退。实现不能以跳过保护换性能，不能引入新轮询层 |
| P3 按目录核对与串行公平预算（P1归因支持时） | 将全局dirty评估为去重的目录脏集合；受管递归/映射直接子项保持不同规则。一次高频目录不拖住其余目录；失联重试带退避与重新出现检测；保留启动/Watcher溢出/低频兜底 | 用“单目录突发+其它目录单变更”和“失联后恢复+执行中取消”两个自动场景测反馈P95、排队和退出耗时；无无界队列、并发整批扫描或事件逐条执行。完整扫描和二次见证未完成不能删卡片；文件操作租约/唯一归属/CAS保持 |

现有基线：12映射/240项无变化核对293.69ms、144.11MB当前线程分配；投影Refresh6.77ms、4.72MB。它们不是整机CPU/内存或UI帧时长，P1必须定位占比，不直接承诺某个未经测量的毫秒目标。先以稳定下降和原安全反例为最小出口，UI反馈/取消预算在归因报告中确定后才落实调度方案。

携带项：R6 probe02第四项未移动的首因未知，成功复验不抹去现场；若再次复现先保留第一失败阶段/原异常，不自动重试掩盖。真实登录自启、发布同步、真实满盘及长期多屏/DPI仍按原范围未验，不混成本批架构余项或补PASS。

### R8验证与集中确认

- 最终定向71项PASS，TRX：out/architecture-r8-review/tests/r8-final.trx。是本批架构/移动锚点、删除规则及8个受便捷入口清理影响的既有拖放/归还样例，不跑完整合同矩阵，不新增人工测试项堆叠。首轮63项只覆盖目录+删除清理，最终71项覆盖全部清理；不能把首轮当最终结果。
- 正常、Review及SessionFixture Release均0警告/0错误；源文本956文件/25冻结迁移通过。check-architecture生产108、Review177（额外69），编译集合与Preview隔离均PASS。
- 正常候选out/architecture-r8-review/candidate01/app；App SHA256=D1C526B32A3D63836CC3C37F924A1A810BAE00A8272058F8C160DED2BE248436；Infrastructure=C44376BB46CBBC21AEC7D90E10A6676F7EEA01AEE1B9EC369D1FA4BF1ABAC728。Application/Domain与R7最终候选一致，四项摘要绑定同级candidate-hashes.json；Review/fixture核心程序集与正常一致。
- 正常包PID37216：四海报ready，退出码0、activeResources=0、无error。数据与样例只在out/drop-d3/architecture-r8-review01；未安装、未写桌面/注册表、未改旧配置和真实文件。人工启动复用start-architecture-file-actions-review.ps1 -Batch R8 -Action Open，默认R1/R5/R6行为不变；R8不开放旧Probe重跑。
- 人工正常候选已启动PID22136，四海报ready、无warning/error；保留烟测后同一隔离配置，未使用Review执行器。当前宿主权限沿用既有环境，本批不宣称普通权限原生拖放已复验。
- 本批集中确认只需**一组**：在独立候选里切换海报并收起/展开，选一张海报新建文本、行内命名；确认显示/选择/控制中心正常、没有新增弹窗或整体闪烁，再正常退出。无需重验安装、卸载、跨卷或全部拖放。架构结论则以本节对照和成本清单一并确认。
- 用户本轮确认符合预期，R8收口；确认范围是现有业务职责整理，不包含未来平台能力与规模性能闭环。未提交/推送/合并，真实热度HOLD。候选启动不等于人工通过；本批未实际测量新性能收益，不将目录移动计为性能改善。

## 25. P1 读取与校验成本归因（2026-10-20）

状态：读取归因结论及下一方向已获用户确认；R8已USER PASS / CLOSED。本轮授权继续同轮安全共享读取，实施/验收见第26节；此前测量没有产品优化。独立提交探针一次锁失败继续保留为未定因事项。P2/P3、真实热度、Git提交/推送/合并均不在本轮。

实施：扩展隔离测量夹具，复用1/6/12映射、每张20项的R7场景；至少三轮独立进程，分别测端到端、完整schema校验、快照物化、投影构造、文件枚举与一次真实变更提交。SQL计数采自SQLite执行回调，另跑计数阶段，不把回调采集开销混入性能数字。正常产品不加入采样框架或运行开关。私有阶段由测试反射绑定原方法，绑定不符立即失败，不复制实现。

出口：保留原始样本、阶段口径及不可相加的说明；明确真正热点、未验安全假设和是否值得优化；仅维护测量入口和少量测量自检，已有产品功能不变则不新建人工业务矩阵。若选择优化，再单独列明被替代入口与语义反例后实施，不拿无效计数或省略校验后的数字作改善证据。

### P1测量实现与边界

- 只改tests/DesktopUp.SessionFixture：新增--architecture-attribution、ReadValidationCostFixture及SqliteExecutionCounter；从R7夹具提取共享映射样例创建，删除原内联重复准备段；复用CostPortProxy，只为P1单独归因阶段添加可选时间/分配统计。没有生产计时接口、运行开关、缓存或新定时器。
- 原VerifyCurrentSchema、ReadSnapshot、BuildProjection与打开连接方法由测试反射绑定成委托，签名不符即失败；不复制产品实现，也不因测量绕过正常产品访问。组件测量显式只测某阶段：完整校验不含连接打开；快照含自身读事务、不含之前校验/连接打开；投影在已读好的快照上构造。它们不是端到端内部精确分摊，不能相加当总耗时。
- 端到端无变化操作直接走未包装的生产服务；端口归因另跑三次，记录包含反射/端口调用的耗时与当前线程分配。文件名字枚举和带元数据/身份的ScanMapping分别测量，不能把二者混成纯文件枚举。
- SQL另跑未计时的一次调用，通过本进程SQLite[自动连接回调](https://www.sqlite.org/c3ref/auto_extension.html)挂接[STMT追踪](https://www.sqlite.org/c3ref/c_trace.html)。记录普通开始、注释/子程序事件及总事件数；不是唯一SQL文本数、不是端口调用次数，也不假定注释类事件都是触发器。所有回调退出前解绑，仅注销自己；不留SQL文本/路径/参数，不改数据库逻辑或用户进程。自检两条SELECT及现有/后续连接解绑。
- round01～03完整通过，其计时与普通语句开始数有效，但旧字段TriggerEntries把注释类回调标得过窄；保留原数据，不将108个注释事件称为108次触发器执行。fixture02只修正标签/说明，final01完整通过、数值同量级；final02在12映射首次真实提交失败，final03未执行。不能把未完成的复测称为三轮全过。
- 三个独立进程、串行执行，未同时构建/测试/运行DesktopUp；子进程固定DOTNET_TieredCompilation=0以消除不同规模进入分层JIT阶段的干扰，不改正常应用环境。每阶段预热3次、计时12次；无变化核对6次。三轮分别保存原始样本和P95；这是暖态同步微测量，**不是UI帧时长、CPU占用、进程内存或长期P95**，不能与R7不同JIT条件直接算优化百分比。
- 每场景另创建3个自建文件，走现有两次见证/提交流程；首见证和虚拟时间推进不在计时内，提交样本包含首次调用成本、条目数逐个增加。该组用于定位提交与整轮的区别，不当作纯稳态读基线。数据库、文件和日志只在本批out/drop-d3目录。

### P1结果：重复整库读取是当前主热点

原始完整三轮：out/drop-d3/architecture-p1-round01～03/attribution.json，PID45492/53920/29752。每轮1/6/12映射、每张20个自建文本，无实际受管文件树；下表列三轮中位数区间、各轮P95区间及每次调用分配（十进制MB）。不能外推深目录、网络盘、云文件、实际UI或全机负载。

| 无变化核对 | 中位 ms | P95 ms（每轮6样本） | 当前线程分配 MB | 完整校验 / 新连接 | 普通SQL开始 / 注释类事件 |
| --- | --- | --- | --- | --- | --- |
| 1张 / 20项 | 35.62～37.18 | 36.88～40.67 | 5.80 | 14 / 14 | 1308 / 42 |
| 6张 / 120项 | 144.72～151.10 | 147.22～152.45 | 47.77 | 24 / 24 | 2483 / 72 |
| 12张 / 240项 | 346.99～354.97 | 351.01～360.84 | 143.13 | 36 / 36 | 4157 / 108 |

12张的36次完整校验还包含900次迁移账本查询（25×36）与36次foreign_key_check。次数来自单独执行回调；总STMT事件为4265，不是4157个不同SQL，也不是108个触发器。固定前置读取12次＋每映射ValidateRoot/Evidence各一次，形成12＋2N次完整校验。每个端口又会反复解码全配置内容；这解释了分配随目录数与总内容量双重放大的风险，不是单纯“每张增加一小段枚举”。

| 12张阶段微测量 | 三轮中位 ms | 每次分配 MB | 解释 |
| --- | --- | --- | --- |
| 打开/配置/关闭连接 | 0.63～0.77 | 0.0035 | 不是全部数据库成本 |
| VerifyCurrentSchema | 5.47～5.70 | 3.28 | 包含内容关系/布局等语义验证，不只检查表头 |
| ReadSnapshot含读事务 | 1.77～1.89 | 1.41 | 不含前述校验和连接 |
| BuildProjection纯拼装 | 0.0014 | 0.0026 | 已物化快照上的工作，不代表UI绘制 |
| 仅枚举文件名 | 0.18～0.19 | 0.058 | 不含身份/元数据探测 |
| ScanMapping完整元数据 | 57.35～58.85 | 2.14 | 文件探测另有真实成本，不能全归数据库 |
| 完整状态读取 | 8.59～10.44 | 4.69 | 137次普通SQL开始，一次完整校验 |
| 无变化投影Refresh | 8.66～9.08 | 4.69 | 拼装虽小，获取/校验快照仍付成本 |

这些阶段是独立调用，不能相加或乘36当精确端到端分摊。另跑的端口包围计时，每轮12次ValidateRoot约110～114ms/56.46MB、12次Evidence约80～82ms/39.67MB；扫描约59～68ms/2.14MB。主要重复读取证据一致，优先处理本轮共享读取，不先缓存投影或删除quick_check。

真实提交与读取基线分开：首三轮12映射整轮约411～556ms，Commit端口约24.77～169.60ms；首次提交存在明显长尾，包含首次执行成本及写事务，不能把最大值删掉或称为稳定写入耗时。计数阶段43次完整校验、5746普通开始/129注释事件，其中764次写语句。提交调优不是本轮优先项，未做统计稳态或锁归因结论。

### P1失败现场与测量工具收尾

- final02（PID44336）在mapped-12首次p1-commit-0.txt进入提交时，AcquireObservationExecutor打开transfer-executor.lock得到共享冲突；用例返回Pending/notice，夹具停止、退出非零，没有重试。1/6张结果已保存，12张读取虽执行但旧夹具未保存，**不作为报告证据**；final03未开始。
- 现场out/drop-d3/architecture-p1-final02保持。事后只读核对SQLite：quick_check=ok、仍240个内容项、没有p1-commit条目；新文件15字节留在directory-0，没有物理搬移。进程已结束，未取得当时锁持有者；静态路径中的lease使用using释放，没有足够证据断言是自身泄漏或外部程序。不得将事后成功、R6旧probe02或杀毒软件猜测当作原因。
- 不改锁策略、不关闭保护、不自动重试该文件。若再现，先捕获第一次失败的阶段/占用者，再决定局部修复；下批共享读取上线前保留定向提交复验门。该失败不推翻前三轮无变化读取归因，也不能被后来的独立成功覆盖。
- fixture03补齐“先保存读取结果，再执行提交探针；逐样本保存，失败保存原异常/样例名并重新抛出”。保留同一失败即停止的规则，没有新增生产异常重试、日志框架或恢复流程。所有测试文件均留在out/drop-d3，无桌面/注册表遗留。

### P1工具验证、复跑差异与集中确认

- fixture03 Release构建0警告0错误，源码文本958份/25冻结迁移通过；SessionFixture SHA256=9DBA87B25697EB6122588546C594E82E35B22A80B887495ABD3A91C48D7F3A26。Application、Infrastructure、Domain、Windows四个核心DLL与R8正常候选逐一SHA256相同。无需再发相同功能的应用验收包，未改App/生产库/SQL/配置/打包入口。
- 只复跑7项ReadObservationCostTests（全过，out/architecture-p1-cost/tests/p1-read-guards.trx）：无变化投影、外部提交、同Revision损坏及根登记严格解析。没有扩大为全套合同测试；这7项不是新增7个人工任务。
- 最终工具在全新out/drop-d3/architecture-p1-report01执行一次（PID42976、退出0）：计数自检、三规模读取及每规模3次实际提交/另一次计数提交完成。没有重新操作final02失败样例，不将独立通过当作该锁异常修复。失败保存分支本轮未另做故障注入，尚不声称已覆盖所有测量工具异常。
- 必须保留复跑差异：fixture02/final01三个规模的核对中位为37.52/143.75/351.87ms；fixture03/report01为110.51/544.79/1135.10ms，明显更慢，原因未通过调度/CPU/I/O追踪确定。完整校验次数仍14/24/36，线程分配约5.81/47.88/143.37MB，重复读热点结论一致。本任务没有同时跑构建/测试/DesktopUp，但未控制整机其他进程与电源/缓存状态；不能将早期稳定三轮作为稳定时延上界，也不能只丢掉慢轮。下批旧/新比较要交错测量并记录运行环境，优先看调用/分配下降，再判断墙钟收益。
- 重跑入口：按项目toolchain.ps1进入离线.NET环境，Release构建tests/DesktopUp.SessionFixture；在独立子进程设置DOTNET_TieredCompilation=0，运行DesktopUp.SessionFixture.exe <全新out/drop-d3子目录> --architecture-attribution。必须退出DesktopUp，不并发构建/其它测量；已有输出目录会拒绝覆盖。报告中Read baselines、InclusivePortCosts、Commits/CommitSql分开，不混用。
- **本批集中验收只有一组报告确认**：确认“重复整库读取优先处理；本轮产品没有变快；下批同轮上下文不省安全检查；锁异常与时延波动继续携带”。无需手工重验新建/拖放/卸载。本组报告已获用户确认；当时没有产品修改，后续实现与人工出口见第26节。

### P1不能删掉或混为同一种的验证

| 保护 | 当前真实位置 / 为什么不能省 |
| --- | --- |
| 数据库完整性、应用身份、迁移身份 | SqliteStateStore.VerifyCurrentSchema/VerifySchemaVersion：quick_check、application_id/user_version、冻结迁移账本、状态头与schema一致；没有以“曾经打开过”或Revision不变证明其长期有效 |
| 内容解析与关系一致性 | SqliteContentRepository.Read/Verify：JSON严格字段与字节预算、目录/成员/卡片关系、绑定与布局项一致性；不是只检查表存在 |
| 快照额外语义 | SqliteStateCommandRepository.ReadSnapshot还解析空间证明/连续状态等；R7已经证明schema检查不能替代这些读取校验，同Revision损坏反例继续保留 |
| 收纳登记与映射观察证据 | ReadStorageLocations的根严格解析/容量界限；MappedDirectoryStore.Read的缺失、大小、反序列化及根身份。批量读取不能把坏目录的一项证据错误扩大为全部目录不可用，也不能悄悄用空数组代替坏证据 |
| 文件/提交时新鲜性 | 根与对象身份、完整扫描、二次见证、执行互斥、最终写事务内校验/CAS；不能用本轮开始时的上下文替代提交前新鲜检查，不在文件扫描期间长期占用读写事务 |

### P1决定：下一实现优先同轮读取复用，不先改全局校验政策

本批到归因与方案选择为止，**产品行为零修改**；没有“性能已优化”的声明。选择下一实施切片为P1的安全共享读取：一次有界读事务内准备本轮映射核对需要的已验证状态、收纳登记和逐目录证据，关闭事务后再扫描文件；避免每个目录重新读/解码整库。上下文只活一轮，不跨轮、不跨写复用，不成为第二份权威缓存。

具体实施/退出要求：

1. 新读端口只返回数据和逐目录错误，不把扫描、两次见证或提交顺序搬回Infrastructure；Application继续持有核对流程。先处理ValidateRoot/Evidence的重复读，固定前置状态检查是否合并再按收益选择，不批量重写所有仓储。
2. 保留目录级错误隔离：一项观察证据损坏时其它目录的行为不得改变；映射根替换、扫描不完整或配置/迁移变化时保留卡片。最终提交仍重新校验和CAS，不允许用旧上下文提交。
3. 实施后用本批同条件三轮对比完整读取次数、线程分配与中位/P95；预期每目录的整库校验/物化不再线性重复，但不提前承诺毫秒或百分比。若无稳定收益，撤回新入口。
4. 只补该读取范围需要的损坏/并发/缺失证据反例，复用已有ReadObservationCostTests与映射观察检查；清理被替代的Reconcile专用读取调用，仍有文件动作/提交调用的入口保留。集中人工最多普通外部同步和映射变化两类自然操作，不重跑安装/卸载/全量拖放。
5. 完成该切片后再决定P2目标发现/无变化投影与P3目录脏集合/预算/退避。暂不删30秒兜底、不放宽2秒稳定见证，不新建第二个定时器；真实热度继续HOLD。

## 26. P1 同轮安全共享读取（2026-10-20）

状态：**USER PASS / CLOSED**。用户本轮确认验证符合预期，普通外部同步与映射变化两组集中人工通过；第25节归因已确认，同轮安全共享读取收口。以下保留最终candidate02自动证据，不因登记验收重跑构建或回归。锁冲突未定因继续保留，不推进热度、不合并分支。

- 仅收敛映射核对的准备读取：沿现有ExecuteRead执行完整校验，再在其短读事务中获取快照、受管根登记与当前有效映射的逐目录证据/错误；读事务结束后才持目录句柄和扫描。上下文为调用局部值，不驻留服务、不跨轮。
- 保留每目录错误隔离、两秒见证、取消、执行锁及二次扫描；提交前重新检查根登记并读取观察证据，写事务保留完整校验及CAS。自己提交成功后重建读取上下文，不把写前数据用于后续目录。
- 复用原解析/边界，删除Reconcile每目录重复读取路径；创建、文件动作、提交仍使用新鲜读取入口，不能因重名误删。没有目录移动或SQL迁移，不混入观察调度/缓存策略。
- 测量：保留fixture03旧实现，与新夹具在全新隔离样例上交错跑三对；计数/分配先决，墙钟保留全部慢轮及环境差异。测试只选新读取边界、目录错误、版本/文件证据变化和真实提交；集中人工最多普通同步与映射变化两组。
- final02锁冲突与R6旧probe02首因未明继续OPEN，不自动重试原样例、不以新样例通过覆盖。若再现，先保存首个异常；不改锁策略来制造通过。

### 实际边界与清理

1. IMappedDirectoryStorage.ReadObservation返回一次调用的MappedObservationRead：已读快照＋逐目录MappedObservationEntry（证据或局部错误）。SqliteMappedDirectoryStorage沿原ExecuteRead做完整校验，ReadSnapshot、ReadStorageLocations与逐目录证据在同一短读事务内物化；事务结束后返回数据，不暴露连接/委托/运行服务，不跨轮缓存。
2. Reconcile删除逐目录的ValidateRoot/Evidence重复读取，直接使用本轮条目；没有并存的旧分支。两个新鲜入口仍服务创建、文件动作及最终提交，因此保留，不为减少文件/方法数量而误删。Root关系检查复用一个局部方法，取消重复的owner查找；没有文件移动、组装容器或额外程序集。
3. 真正提交时，执行租约、RequireIdle、根登记新鲜检查、第二次文件扫描、CommitObservation新鲜Evidence、写事务完整校验及原Revision/ContentVersion/实体版本CAS全部保留。新增的提交前根重验避免准备阶段登记变更被忽略；成功后重新获取观察上下文，下一目录不跨写复用。并发解除的绑定不继续访问其旧证据。
4. ReadObservation只为有效绑定映射准备数据；保留既有快照/登记数量与字节限制、单目录512×1024字符记录上限（旧规则按json.Length，不是UTF-8字节），并逐目录检查取消。按目录返回缺失/格式/超限错误；全局schema/快照/根登记损坏仍整体拒绝，不能当作某个目录的空证据。观察记录对象形状错误或数组null成员统一为局部IOException，避免JsonException/NullReferenceException打断无关目录。合法记录的解析结果不变。
5. 上下文短期保留当前映射的证据数组，直到本轮或下一次提交替换，不是零额外存活内存。当前1/6/12映射场景测的是累计线程分配，不是大规模峰值承诺；极端规模的总预算/分片仍属P3。没有放宽扫描完整性/二次见证，没有新增重试、计时器或永久缓存。
6. 测量沿原夹具；追加首个端口异常堆栈保留，避免生产用例转notice后只留下断言异常。scripts/measure-architecture-shared-read.ps1使用旧fixture03和新候选交错三对，记录每次PID、程序集摘要、运行环境与整夹具CPU/峰值；整夹具数据包含样例准备，不当作单轮核对的成本。所有输出只写全新out/drop-d3子目录，失败即停止、不重试。

### 最终测量与收益边界

最终对照：out/architecture-p1-shared/comparison-paired02.json，顺序旧1→新1→新2→旧2→旧3→新3；每轮原始数据在out/drop-d3/architecture-p1-paired02-{before|after}{1|2|3}/attribution.json。Windows 26100、28逻辑处理器、平衡电源、子进程DOTNET_TieredCompilation=0；本任务不并发构建/应用/其它测试，但不宣称控制了整机负载。每张20项，只有映射内容；不能推导任意混合受管集合也固定12次读取。

| 映射规模 | 无变化核对完整校验 旧→新 | 普通SQL开始 旧→新 | 当前线程分配MB 旧→新 | 三对中位ms（旧→新，按轮号） |
| --- | --- | --- | --- | --- |
| 1张/20项 | 14→12 | 1308→1115 | 5.85→4.98 | 44.74→42.30；73.41→67.09；45.18→32.28 |
| 6张/120项 | 24→12 | 2483→1200 | 48.37→23.77 | 164.94→108.08；176.72→165.81；149.63→97.56 |
| 12张/240项 | 36→12 | 4157→1302 | 144.89→47.91 | 402.43→186.25；510.39→278.65；351.72→169.47 |

12张每轮分配约降67%，同轮完整校验减少24次；三对中位耗时各下降约45%～54%。各轮P95旧410.36/678.95/357.72ms，新218.63/291.57/220.63ms（每轮6个样本，非长期尾延迟）。1张收益较小，6张第二对受明显慢轮影响；保留全部数字，不只选择最快一轮。统计仅同步用例墙钟与线程累计分配，不是UI帧率/整机CPU/常驻内存；不同场景路径长度也影响绝对分配，不能与第25节不同目录的数值直接算百分比。

每种实现/每轮/每规模的3次真实提交＋一次计数提交均完成，未出现新的锁失败。计数提交完整校验旧21/31/43次，新均20次；新鲜Evidence、根登记与最终写事务未被绕过。此组成功不关闭历史final02或R6 probe02。初次candidate01/paired01为中间证据，之后补齐null成员隔离及局部可读性整理，**以candidate02/paired02为最终比较**。

### 最终构建、接线与人工候选

- 22项定向全过：新共享读边界/缺失、错误形状、null、超限的目录隔离、同Revision新鲜证据、根变化、版本冲突、连续自身提交与受控锁阻塞；复用原读取损坏保护、根失联、二次扫描和取消检查。TRX：out/architecture-p1-shared/tests/p1-shared-final.trx。首轮21项是中间结果；没有扩大全量合同矩阵。
- 正常/Review/SessionFixture Release均0警告0错误；960份源码文本/25冻结SQL通过；生产108、Review177（额外69）与Preview隔离通过。App无新增生产源码/依赖，四个核心程序集在正常、Review、测量包间摘要一致。
- candidate02/app：App SHA256=F5336EE82FB7B7640D4C7B246D9A634181536D9803B95C0EAA5EED44A0CCD845；Application=42837F7809BEDFF6F5864C370BB9ECB0DBFB24003FE7739B1298928DBF803925；Infrastructure=A360C0C50A8B1E00048A7C52FAEC7B1F050A33AB09644DA44668B216C74E0DD4；Domain=1BDA105A09F1A7AC1D12C8AEA9B3FAFE33D8E42768B829167C46957B6AB4046E。candidate-hashes.json绑定启动入口。
- 实际生产monitor/Dispatcher接线：out/drop-d3/architecture-p1-shared-probe01，PID52276在线创建/新增/改名/失联恢复/普通受管外部移出；PID52860离线新增后重启，两段PASS，无error。失联阶段一条预期external_sync_deferred警告保留，不伪称无warning。没有强制Invalidate，nativeMouse=false、picker=false，不冒充鼠标/选择器人工验收。
- 正常包PID46856：海报ready、退出0、activeResources=0，无error；独立样例out/drop-d3/architecture-p1-shared-review01。未安装、不写桌面或注册表、不改旧包/旧配置/真实文件。未改变普通权限/UIPI，原生拖放不是本批复验范围。

### 集中人工（两组，USER PASS）

交付时正常候选PID54116已打开，海报ready且无warning/error；不是Review执行器。保留烟测后的本批隔离配置，不使用日常配置。该PID是交付记录，不表示本轮检查过进程仍在运行。

入口：powershell.exe -NoProfile -File "G:\A_\Tools\DesktopUp_V2\scripts\start-architecture-observation-review.ps1" -Batch P1 -Action Open。R1/R7原默认入口和样例保留。只操作本批review01样例：

1. **普通外部同步**：海报内新建文本并行内命名，从“在资源管理器中定位”找到实际文件，剪切到本批external-target。预期文件保留，原卡片数秒后自动消失，不需刷新/额外确认。
2. **映射变化**：控制中心新增独立映射，选本批mapping-input；在资源管理器改名mapped-sample.txt并新建一个文本。预期卡片自动更新且不重复，普通海报不受影响；收起/展开已有图标正常。完成后正常退出。

2026-10-20用户反馈“验证通过”，上述两组均记为USER PASS，安全共享读取切片收口。不重验安装/卸载/全量拖放，未修改产品代码或再次启动测试；本批样例和证据仍在隔离out目录，未执行桌面或真实文件清理。OPT-CONTENT-SYNC-01仍OPEN，所有锁冲突未定因和未验环境继续携带。

## 27. P1收口后的系列计划（2026-10-20）

本节为P1收口时的计划快照；后续P2授权、实现与当前验收状态见第28节。

当前在impl/architecture-foundation@87b81a3及R5～R8/P1未提交工作区；本次只更新验收/计划记录，未提交、推送、合并或开始新实现。R1～R8关闭的是现有业务职责边界，P1关闭的是归因与映射同轮准备读取；不等于观察成本、全部目标平台或真实热度准入已经完成。后续仍先测量，选择有收益的最小改动，不为架构图增加层次。

| 顺序 | 目标与范围 | 退出依据 |
| --- | --- | --- |
| P2：目标发现与无变化投影路径（下一建议，未开始） | 先测稳定空闲的整库读取、目标发现与投影成本，明确配置/位置/映射绑定、外部改库及启动的失效边界。优先减少每2秒重复目标发现；投影复用仅在有证据且权威/失效规则明确时实施，不直接缓存跨轮数据 | 同条件每分钟读取次数/分配下降；绑定变更、迁移、离线变化、根失联恢复与漏事件仍收敛，旧卡片/暖图不回退。保留新鲜提交检查、版本保护及错误隔离；不能仅凭Revision跳过语义校验 |
| P3：按目录观察与有界公平调度（P2后按测量决定） | 评估去重的脏目录集合、串行预算、失联退避，避免单目录频繁变化触发全配置核对；保留启动、Watcher溢出和低频兜底，不另加轮询层 | 用目录突发与另一目录变化、失联恢复与取消验证反馈/排队/退出成本；队列有界，完整扫描/二次见证及文件操作优先、唯一归属/CAS不弱化。没有稳定收益则不引入新复杂度 |
| 基础架构与成本最终复审 | 对照目标图复核现有职责、旧入口清理、可读性、实测成本及未验事项，明确哪些优化保留、哪些有依据延期 | 分别给出职责闭环、规模成本和真实热度准入结论；锁冲突不以成功复跑销项。仅在用户另行授权后执行Git同步/集成 |
| V3-06：真实热度（HOLD） | 准入结论与用户授权后，再沿现有Application/Domain及会话/呈现边界接入真实打开事实、评分策略和自动布局 | 不向大窗口或具体存储叠加规则；不把AI、动态模板或独立更新器全部完成作为前置 |

每批保持已通过的功能，替代实现同步清理旧入口；文件移动与行为变更分开。只做影响面定向验证，能合并的自然操作集中验收，不重列累计回归矩阵。本轮计划不是P2/P3自动开工或合并授权。

继续携带：P1旧final02执行锁冲突与R6旧probe02首因未知，现场/证据保留；再现先捕获首次异常及持锁信息，不自动重试掩盖。发布同步与真实登录自启仍暂停/未复验，真实满盘、长期多屏/DPI仍未验。海报风格/动态模板平台、AI与发布热更新属于以后独立能力，不冒充已实现，也不混入当前读取优化。

## 28. P2目标发现与无变化投影成本（2026-10-20）

状态：**USER PASS / CLOSED**。2026-10-20用户明确说明“P2验证通过了的”，本次补齐人工通过记录。下文是原交付实现与证据；没有重复测试、改动产品或合并分支，历史锁冲突未定因保留。

首轮基线：P1最终fixture在out/drop-d3/architecture-p2-before01重新测量，6映射空闲33秒调用17次WatchTargets、2次Reconcile；文件变化后正常入库。12映射一次目标发现分配约4.705MB，无变化Refresh约4.701MB；已有P1最终归因中纯BuildProjection约0.0016ms/2640字节。代码也确认AdoptLocked复用未变投影，主要成本是前置新鲜读取，不是投影重建。因此本批不增加投影缓存、不削弱Refresh的完整验证。

选择最小方案：复用既有2秒worker与30秒兜底，只让目标发现于启动、显式失效、监听错误或独立周期校验时重新读取。已验证目标列表仅用于配置监听器，不能作为文件归属或提交依据；周期不能被持续内容事件/Pending饿死。内容事件只标内容脏；新建/解除映射、收纳位置与内容版本的投影发布保守地通知目标失效。缺失监听器仍逐tick尝试挂接，不反复读库；读取失败不替换上一次有效目标并阻止该轮核对，下一tick重试。最终文件操作与Reconcile的根/证据/版本校验原样保留。

验证范围：同场景新旧63秒空闲及一次真实目录变化，记录实际调用/线程分配/耗时（不外推FPS/CPU）；少量调度定向覆盖显式失效、执行中失效、目录重现/递归方式变化、发现失败与取消；复用同Revision损坏反例。接线沿用真实monitor/Dispatcher映射和离线变化探针，集中人工最多两组，不重验安装/全量拖放。若没有确定性读取减少或失效链不能成立，撤回该方案；P3目录调度不混入本批。

### 实际实现与边界

- ManagedContentMonitor替换每tick的WatchTargets调用：启动/显式Invalidate/Watcher错误/独立15-tick计数才读新目标；普通文件事件和Pending只设置内容脏位。仍为一个worker、一个2秒PeriodicTimer，不新增后台任务/永久连接或权威缓存；Reconcile用例、文件提交和读取验证政策不改。
- 保留的Dictionary仅为监听路径及递归选项，不能用于删卡片、判断文件归属或提交。每次目标读取仍全量验证；失败保留原有效监听且该轮不执行Reconcile，下一tick重试。交换失效位发生在读取前，读取中到来的新失效不丢失。
- 未挂接路径每tick尝试创建watcher，不为了缺失路径重读整库；路径删除/递归模式变化替换旧watcher，Watcher.Error失效后重建监听。删除每tick构建路径集合、逐路径Single查找及原单一脏位混用路径，没有并存的旧生产分支。
- App沿既有ApplyStateProjection通知：CatalogChanged或ContentVersion变化保守失效目标；新建/解除映射、受管集合首次建立、收纳迁移及内容变更均覆盖，单纯姿态/样式不触发目标刷新。启动时monitor自行全读，系统恢复沿原Invalidate接线。直接外部改库/漏掉通知由周期新鲜读取兜底，不能仅相信版本或旧路由数据。迁移界面仍通过既有_refreshTransfers→state.Refresh发布；没有从窗口增加SQLite依赖。
- 独立目标计数不被内容Pending重置，避免忙目录饿死目标重新发现；15个tick在空闲时约30秒，不是严格时间上界，慢扫描仍会拉长实际间隔。目录级脏集合、扫描预算、缺失退避与峰值存活内存仍属P3，未声称本批解决。
- 不新增投影缓存：AdoptLocked已有未变对象复用，纯BuildProjection占比很小。Refresh的完整读和同Revision损坏拒绝保留；外部数据失效没有被版本快捷判断掩盖。本批仅对实际发起目标发现的频率做优化，不修改真实热度或模板能力。

### 测量与定向证据

旧/新使用同一修改后的诊断夹具、相同1/6/12映射×20项场景，子进程DOTNET_TieredCompilation=0，先旧后新，不并发运行构建/应用/其它测试；整机其它负载未受控。本批一对63秒实际timer观测及4项调度边界测试证明确定性的调用减少，不把单对墙钟视为长期性能保证。既有33秒基线只辅助定位，不与63秒直接计算比例。

| 6映射空闲63秒（包含启动） | 旧实现 | P2 |
| --- | --- | --- |
| WatchTargets调用 | 32 | 3 |
| 目标发现路径累计线程分配（十进制MB） | 76.762 | 7.226 |
| 目标发现累计墙钟ms | 654.44 | 51.79 |
| Reconcile次数 | 3 | 3 |
| Reconcile累计线程分配MB | 71.226 | 71.147 |
| Reconcile累计墙钟ms | 653.37 | 827.01 |

目标发现调用减少约90.6%，其累计分配约减90.6%；Reconcile未减少、墙钟本轮反而较慢，不能宣称整条链或UI提升同样比例。随后新增一个真实文件，旧/新均最终提交一次，累计Reconcile均6次；新目标读取仍3次，旧35次。夹具不含App投影回调，产品在真实内容提交发布后可能额外发现一次，不能将3次当所有应用场景的硬上限。

- 原始测量：out/drop-d3/architecture-p2-idle-before01/cost.json、architecture-p2-idle-after01/cost.json；只写全新自有目录。保留旧二进制out/architecture-p2-discovery/before/fixture，最终candidate01/fixture提供--architecture-discovery-cost，原--architecture-cost保留33秒默认。两份夹具含同一测量代码；不同的是本批生产程序集。
- 11项定向PASS：ContentMonitorDiscoveryTests四项实际worker/FSW检查＋ReadObservationCostTests七项已验证读取/同Revision损坏/根登记保护。TRX：out/architecture-p2-discovery/tests/p2-discovery.trx；没有重跑累计合同矩阵。历史锁冲突未关闭，本次没有重试原失败样例或改执行锁。
- 正常/Review/SessionFixture Release均0警告0错误；961份源码文本/25冻结SQL通过。生产108/Review177（69额外）及Preview隔离通过；无新生产类/端口或目录移动，App仍只依赖IContentMonitor的既有Invalidate。
- 真实生产monitor/Dispatcher探针：out/drop-d3/architecture-p2-discovery-probe01，PID31208在线映射创建/新增/改名/失联恢复/普通受管外部移出，PID46728重启离线新增；均PASS、退出0、activeResources=0，无error。一次预期失联warning保留。nativeMouse=false、picker=false、forcedInvalidation=false，不冒充人工原生鼠标结果。
- 正常包烟测PID48012：海报ready，退出0、资源0，无warning/error。正常/Review/fixture的Application、Infrastructure、Domain摘要一致；正常App摘要及核心摘要存candidate01/candidate-hashes.json并由启动脚本核对。没有安装、注册表或桌面测试文件写入；旧包/配置/真实数据不动。

### 集中人工（原两组，已由用户确认通过）

正常候选out/architecture-p2-discovery/candidate01/app，隔离配置/样例out/drop-d3/architecture-p2-discovery-review01；已启动PID46648。入口：powershell.exe -NoProfile -File "G:\A_\Tools\DesktopUp_V2\scripts\start-architecture-observation-review.ps1" -Batch P2 -Action Open。R1/R7/P1原入口和数据保留。

1. **运行中新建映射**：控制中心新增独立映射海报，选本批mapping-input；在资源管理器改名mapped-sample.txt并新建文本。预期几秒内自动更新、不重复，无需重启或手动刷新，收起/展开无整体图标闪烁。
2. **收纳位置变更后的观察**：在普通收纳海报新建文本；全局→收纳位置→更改并迁移选择本批空storage-target（沿用既有迁移确认）。迁移后对该文本“在资源管理器中定位”，将实际文件剪切到本批external-target。预期文件保留、对应卡片自动移除，映射海报内容不受影响；完成后正常退出。

以上候选、PID与步骤为交付时记录，不代表进程目前仍运行。2026-10-20补录用户确认：本批验证通过，P2 CLOSED；没有以P3自动结果替代P2人工。P3现状见第29节，真实热度仍未开工，Git无本轮提交/推送/合并。P1 final02、R6 probe02未定因及发布/登录/满盘/长期多屏DPI未验继续携带。

## 29. P3按目录观察与有界公平调度（2026-10-20）

状态：**USER PASS / CLOSED**。2026-10-20用户明确确认“P3验证也通过”，按原集中范围记录通过；本次同时补录用户明确确认的P2通过，不是从自动结果推定。

以下实现、测量与候选为P3交付时证据；本次验收收口后完成第30节最终复审，不改产品或追加验收包。本轮不合并、不推进热度，不以后续成功覆盖P1 final02/R6 probe02锁冲突首因未知。

先在12映射×20项隔离数据测量单目录突发、另一目录一次变化，以及单根失联对其他目录的扫描放大。随后按证据实施：将全局内容脏位替换为按已验证路由去重的待核对目录；同一worker每轮最多4个目录，串行先进先出，失联延迟重试。保留一个2秒timer与独立周期全目录入队，不增加轮询层。路由只用于筛选任务，每次读取及提交仍核实真实登记、证据、归属和版本。

退出标准：突发与失联不再每轮扫描所有健康目录；队列大小不超过已知路由数，重复事件不挤掉较早等待目录；扫描期间事件不丢失；目录失败保持卡片并可恢复；退出取消不提交未完成扫描。完整扫描、两秒稳定见证、提交前第二次完整扫描、新鲜根校验、执行锁和CAS保持。同步发布只重新发现路由，不将自己的结果扩散成全量脏事件；系统恢复仍完整入队。替代旧生产脏位，无双路径开关，无目录移动。

精简验证：同夹具前后测量、调度/范围安全定向测试、真实monitor接线与正常包烟测；人工合并为最多两组。慢单目录完整扫描仍受现有文件预算约束，不声称抢占扫描、严格30秒或UI帧率保证。若成本未降低或安全边界不能成立，调整/撤回方案并记录证据。

### 实施边界

- Infrastructure新增内部DirectoryObservationQueue，替代monitor的全局内容脏位；只接受已发现路径，每路径一个槽，重复事件去重且保留原排队位置，最多4根/轮，已有等待者先于连续新事件。扫描时的新事件可再次入队，完成回调不能抹掉或推迟它。删除/改递归路由时清理旧槽，未知事件路径不增长队列。
- 失联/证据不可核实的目录分别按2/4/8/16/30秒延迟，真实事件/重新挂接可立即唤醒；缺失watcher注册同样退避。仍仅一个2秒timer，独立15-tick全目录入队、启动/系统恢复/Watcher错误完整入队，目录内仍完整扫描。根目录移走不保证发出子项事件，因此依赖周期兜底和有限排队；不承诺所有失联在2秒反馈。
- Application两个观察用例接收可选目录范围，每次从新鲜登记/绑定中匹配，未知路径不能变成扫描或文件操作权限；默认null仍完整核对，服务不重复业务实现。全局待文件操作仍阻止提交；逐目录返回重试提示，失联不当空目录。SQLite映射准备读取只装载本批目录证据，但全局语义校验、全部根关系保护不变。成功提交后仍重新读，第二扫描/新鲜证据/根检查/CAS没有省略。
- IContentMonitor区分RefreshTargets与Invalidate：App的内容投影只刷新目标；目标未变不会再次扩散全量核对，新路由仍入队。系统恢复仍Invalidate。没有重置ContentVersion见证或缓存权威内容来让提交更快；不断发生的全局版本变化仍可能推迟稳定提交，公平性指调度而非绕开CAS。
- 不再保留旧全局内容脏位/逐tick缺失watcher重试生产路径；完整核对仍是周期安全兜底，不是旧实现开关。无文件移动/目录重排，无真实文件移动规则修改。12根启动需要分3批，这会延后最后一批初次观察，但不延后已有卡片加载；全库读取/校验、单目录完整扫描与全局写版本仍是剩余成本，未声称全部解决。

### 同条件测量与自动证据

最终对照：out/drop-d3/architecture-p3-before02/cost.json、architecture-p3-after02/cost.json；12映射×20项，全新隔离数据，先旧后新、无并发构建/应用/测试，DOTNET_TieredCompilation=0。二者使用同一诊断DLL（SHA256 46B3413AAF501C4BE20327311EE62BAD4EB800DC49440C18A16DDA6631242E6A）；before02配保留的P2核心，candidate01为P3核心。没有把生产旧路径留下开关。初轮before01/after01是11秒失联窗口，不能证明漏事件兜底，故只保留作探索证据，最终采用35秒窗口。

| 同一隔离场景（启动扫描不计） | P2基线 | P3 |
| --- | --- | --- |
| A连续变化、B新增一次：完整ScanMapping调用总数 | 49 | 7 |
| B新增文件可见时间（从突发开始计） | 5487.74ms | 7598.67ms |
| C失联35秒、D新增一次：健康目录完整扫描总数 | 200 | 17 |
| D新增、C失联反馈/恢复 | 通过 | 通过 |

扫描调用减少约85.7%/91.5%；可见延迟本次更慢，保留原值，不宣称延迟/FPS/CPU/整体内存改善。计数是到达ScanMapping的完整扫描调用，失联根在LockDirectory拒绝，故不计其失败尝试；退避时序由队列定向测试验证。墙钟只有一对、整机背景负载未控制，不能当性能保证。P3分批可能增加启动/全兜底的全局准备读取次数，本批没有缓存校验结果；后续如要改应另行测归因，不能据扫描减少推算全库读取同比下降。

- 19项定向PASS，TRX：out/architecture-p3-directory/tests/p3-directory.trx。范围仅新队列/真实monitor、两个用例范围筛选、同Revision损坏与新鲜证据/根关系/CAS、扫描不完整/最终扫描竞态。已有用例复用，无全套回归。纯队列以确定性时钟覆盖大量重复/未知事件、FIFO、扫描期间事件、2/4/8/16/30秒退避、事件唤醒与路由替换；真实worker验证发现失败、周期不饥饿、挂接、重绑定、发布不扩散、运行中取消。
- 正常/Review/fixture Release均0警告0错误；964份源码文本/25冻结SQL、生产108/Review177（69额外）及Preview隔离通过。检查脚本故意用失败的子命令检验PreviewGuard，外层最初误将该LASTEXITCODE当整体失败；脚本四门实际均PASS，按脚本异常/回执核对后重跑通过，未改产品或绕过门禁。
- 同一核心三个包摘要一致。正常App SHA256 D7132FB43A657DE3985EBE7ECDEA53CCD0B0A3EF926CF06F71E338C99FFCD985；Application AF6648630CD99065E28644C810B5F8B0792D84FB1E6852DB481505F01EAED87A；Infrastructure 29CC76B3CD905874472445F32D65F75F133B10676CCE038630708280F2F2A289。完整清单为candidate01/candidate-hashes.json，启动脚本核对正常包。
- 实际生产monitor/Dispatcher probe01：PID26132在线映射创建/新增/改名/根失联恢复/受管外部移出，PID27492重启离线新增，均PASS、退出0、activeResources=0。一条预期失联warning，无error；nativeMouse=false、picker=false、forcedInvalidation=false，不当成人工原生交互验收。目录恢复保留卡片身份。
- 正常包烟测PID51516 ready、退出0、资源0，无warning/error。随后正常候选PID11980启动供集中人工。无安装/注册表/桌面样例写入；旧包、旧配置与真实文件不动，测量文件保留于自有out证据目录。

### 集中人工：原两组，已由用户确认通过

入口：powershell.exe -NoProfile -File "G:\A_\Tools\DesktopUp_V2\scripts\start-architecture-observation-review.ps1" -Batch P3 -Action Open。正常候选out/architecture-p3-directory/candidate01/app；样例out/drop-d3/architecture-p3-directory-review01。已启动PID11980。R1/R7/P1/P2入口不变。

1. **两张映射互不拖累**：控制中心新增两张独立映射海报，分别选择本批mapping-input与second-mapping。资源管理器连续给前者新增/改名几项，同时给后者新增或改名一项。预期两处都自动更新、卡片不重复、不要求手动刷新；收起/展开无整体图标闪烁。只用隔离样例，不重复整个拖放矩阵。
2. **迁移后外部同步与退出**：普通收纳海报新建文本；全局收纳位置迁移至本批空storage-target，沿既有确认。定位迁移后的文本，将其剪切至本批external-target。预期文件保留、对应卡片自动移除，两个映射海报不受影响；正常退出并重开，状态保持。此组覆盖P2待确认的迁移后监听入口，不另加一轮重复验收。

2026-10-20用户确认以上集中验证通过，P3收口；上述PID仅为交付记录。根失联/恢复、漏事件周期兜底与取消复用既有定向证据，不额外堆人工项；真实高频长期/UI尾延迟、多屏/DPI、真实满盘未验保留。P1 final02与R6 probe02锁冲突未定因仍OPEN，成功新数据不销项。最终复审、剩余成本与后续授权边界见第30节，不自动进入下一功能或合并。

## 30. P2/P3收口后的最终架构与成本复审（2026-10-20）

### 30.1 范围、证据与结论

用户本次明确确认P2此前已验收通过、P3也通过，两批均记USER PASS / CLOSED。复审对象是 `impl/architecture-foundation@87b81a3` 加当前未提交工作区，不是只看提交树或R4旧图。对照[原目标图源](../../20_Architecture/ArchitectureReview/target.architecture.json)，核对文件动作/拖放、映射观察、归还/位置迁移、会话/呈现及启动退出的现有调用链和编译依赖。

**结论：现有业务基础职责整理可以收口，P1～P3限定范围的优化已验；规模性能和完整目标平台不能据此宣告完成。** 本轮没有发现需要继续阻塞整个架构阶段的具体职责缺口。这个判断不是“完全没有问题”的证明：下面列出合法兼容边界、可读性债务、尚未测清的成本和未知异常，不把它们隐藏到“已完成”里。真实热度实施仍HOLD、发布同步仍暂停，Git需另行授权。

本轮只改文档，复用已验源码的有效构建/测试/人工证据；没有新建验收包、运行交互探针或追加人工清单。只读重新执行check-architecture.ps1：ProductionComposition 108、normal Compile 108（0 Review文件）、Review Compile 177（69额外）、PreviewGuard均PASS。核对P3原TRX为19/19通过、最终正常候选四份DLL摘要与清单相符；原始cost.json重新读取得到下表数值。不是本轮重新运行19项测试或性能基准。

### 30.2 目标职责逐项对照

| 目标职责 | 当前落点与真实接线 | 判断及限制 |
| --- | --- | --- |
| 薄界面/意图入口 | App Views/Runtime收集菜单、拖动与命名意图；[App.ContentServices](../../../../src/DesktopUp.App/Bootstrap/App.ContentServices.cs)组装应用用例；窗口不持有SqliteStateStore或自行构造文件业务实现 | 已落实。Dispatcher、HWND、Shell菜单桥和控件适配合法留在宿主，不为行数好看再造转发层 |
| 业务用例 | Application/Features/Content下FileActions、Transfers、Observation、Lifecycle、Storage；[MappedContentCreationUseCase](../../../../src/DesktopUp.Application/Features/Content/FileActions/MappedContentCreationUseCase.cs)拥有新鲜读取、准备/创建/登记及失败收尾，不只是转调存储大方法 | 已落实。原子事务、身份/防覆盖和原生系统执行留适配层，不能为“纯粹”分层拆散原子提交 |
| 会话与操作生命周期 | [PosterContentSession](../../../../src/DesktopUp.Application/Features/Content/Layout/PosterContentSession.cs)拥有草稿/交互保护/提案与票据；[ContentOperationLifetime](../../../../src/DesktopUp.Application/Features/Content/ContentOperationLifetime.cs)负责宿主在途任务；App.Lifetime按停止接入、释放会话、停Worker、排空操作、释放存储的顺序收尾 | 已落实。会话关闭不等同文件事实回滚，不增加第二套Busy字段/账本；退出屏障和迟到回调保护保持 |
| 纯布局、增量呈现与同代命中 | [ContentPresentationLayout](../../../../src/DesktopUp.Application/Features/Content/Layout/ContentPresentationLayout.cs)准备帧；[PosterContentPresenter](../../../../src/DesktopUp.App/Presentation/PosterContentPresenter.cs)持有卡片/XAML/暖图；[ContentPublication](../../../../src/DesktopUp.App/Views/Poster/PosterVisualWindow.ContentPublication.cs)检查代次并完成原生命中和绘制提交 | 已落实。原生提交仍在宿主；Presenter不读库/搬文件/另建布局引擎。帧提交失败停用错误路由，稳定卡片/缓存不因抽层重建 |
| 存储、平台与组装 | Domain无外部项目依赖；Application→Domain；Windows→Application/Domain；Infrastructure→Application/Domain/Windows；App组合根引用实现；ContentWorker→Application/Windows，不再链接Infrastructure源码 | 已落实为六程序集模块化单体。App→Worker仅构建/部署依赖，非窗口直接调用Worker内部实现；不强制每模块拆新项目 |
| 调度和业务核对分离 | [ManagedContentMonitor](../../../../src/DesktopUp.Infrastructure/ManagedContentMonitor.cs)和[DirectoryObservationQueue](../../../../src/DesktopUp.Infrastructure/ContentObservation/DirectoryObservationQueue.cs)只决定何时/哪些已知目录待核对；ContentObservationService及两个用例从新鲜存储证据判断实际变更 | 已落实。每配置一个worker/timer，队列按路由有界；监听通知不授权删除，保留全局保护、完整扫描、提交前复核与CAS |
| 未来可替换能力 | 成功打开事实由ContentActionService经原IContentOpenJournal登记；既有ContentHeatSession/PosterContentSession已提供提案消费、保护和同代发布 | 只完成既有接点。事实只读分页/消费游标、生产评分策略、通用能力准入、动态模板/AI/独立更新器均未实现；不能把编译期Lamp/Rain模块说成运行时模板平台 |

以上不仅核对using：抽查用例内实际顺序、生产组装、旧入口退役和异常/释放路径。架构门覆盖实际生产partial集合；它能阻止已声明的依赖回流，但不能证明所有性能或第三方Shell行为。

**有意保留而非遗漏的桥**：[SqliteContentStartupSteps](../../../../src/DesktopUp.Infrastructure/ContentObservation/SqliteContentStartupSteps.cs)仍组合既有持久收尾执行器，内部有构造调用；总体顺序归Application的ContentStartupUseCase。它不是万能组合根，不可承接新启动业务政策。旧CrossPosterTransfer/MappedDirectoryIngress保留的中断收尾用途不等于已退役Execute入口仍在执行正常业务。后续若修改这些收尾协议，再同批收敛内部依赖；本轮不为消灭所有new表达式重写已验恢复语义。

可读性仍有改进空间：部分用例/适配器语句紧密、窗口partial较多。职责目录、窄端口和唯一所有者改善了导航，但不能写成“已无冗余/难读代码”。后续以实际改动链为单位整理局部命名与长方法，同时删掉被替代实现；纯移动/格式与行为改动分开，不再追加仅转发或按文件数量拆分的层。

### 30.3 成本结论：确定减少了什么，没有证明什么

| 同批可比较的场景 | 已有证据 | 不能外推的结论 |
| --- | --- | --- |
| P1：12映射一次无变化核对，同轮共享准备读取 | 完整校验36→12；该轮线程分配约144.89→47.91MB，交错三对测量与真实提交证据见第26节 | 仍有完整读/校验；不是跨轮权威缓存，也不是全进程内存下降同一比例 |
| P2：6映射空闲63秒，含启动 | 目标发现32→3次，该路径线程分配76.762→7.226MB；Reconcile仍3次 | Reconcile墙钟653.37→827.01ms，本次更慢；夹具无App投影回调，不能宣称产品始终仅3次或全部链更快 |
| P3：12映射，A突发+B一次新增 | 完整ScanMapping调用49→7；B可见5487.74→7598.67ms | 扫描放大显著减少，但本次可见更慢；只有一对墙钟，不能定因到调度或宣称尾延迟改善 |
| P3：一根失联35秒，另一根新增 | 健康根完整扫描200→17；新增/失联反馈/恢复均成功 | 失联根在LockDirectory拒绝，不计入ScanMapping；退避由定向测试证明，不从该数值推算失败尝试或SQL次数 |

各行场景/规模/计数口径不同，不能连乘为“整体提速”。P3的原始对照在out/drop-d3/architecture-p3-before02与after02，包含原始慢值；启动扫描不在该表计数中。没有UI帧时间、UI等待尾延迟、整进程CPU/峰值内存、长期高频和大单根目录的完整认证。

**计时器真实现状**：不是每张海报一个，而是每配置一个2秒PeriodicTimer，文件事件按目录入队，最多4根/轮，独立15-tick目标发现/全目录兜底。兜底不能删除，因为根目录移动/监听丢失不保证有可用事件。4根是数量预算，不是毫秒预算；完整扫描和二次见证仍串行，慢根会延长实际间隔，不能承诺严格30秒或“公平队列即可保证及时提交”。

### 30.4 剩余成本和未定因项的处理计划

以下是明确携带项，不默认开启另一串架构改造。先按场景归因，达到触发条件才形成小任务；未证实瓶颈前不引入永久读连接、跨轮权威缓存、并发文件核对或取消安全验证。

| 优先关注 | 具体剩余点/证据入口 | 触发与最小后续动作 | 必须保持的边界 |
| --- | --- | --- | --- |
| 1：全局读取/校验及分批代价 | SqliteStateStore.ExecuteRead/Write每次经_gate打开连接、VerifyCurrentSchema；受管核对仍读取全局待操作/根/状态。12根分3批可能增加准备读取，P3未测全链空闲分配 | 在下一次性能工作或出现空闲卡顿/大型配置反馈时，先对比相同12根启动、空闲和突发的完整校验次数、线程分配、连接/锁等待及UI等待；区分批次数与单次成本，再决定是否做同轮共享读取 | 不凭ContentVersion跳过新鲜语义验证，不用长期缓存决定归属/删卡；提交前根/身份/CAS照旧 |
| 2：受管无变化证据写入 | ManagedContentSyncUseCase对observed调用SaveEvidence，SqliteManagedContentObservationStorage逐项Put，即使最终无卡片变化也可能进入写事务 | 先量出无变化写事务/更新行数/等待占比；占比确实显著才设计“只写变化证据”。不能仅凭方法名断言磁盘实际写了多少 | 新旧证据必须属于同一ContentVersion，首次基线及身份修复不得漏写，最终提交仍新鲜 |
| 3：慢根和全局版本争用 | 单根完整Inventory/ScanMapping及提交前第二扫描不可抢占；稳定见证包含全局ContentVersion，别处持续提交仍可能使本根重等 | 出现稳定更新延迟问题或扩大规模前，分测队列等待、单根扫描、稳定等待、CAS重试和发布；先验证一次突发+另一根持续更新，必要时再设计预算/作用域版本 | 不把不完整扫描当空目录；不削弱文件动作优先权，不将调度公平误写为提交期限 |
| 4：可读性与兼容桥 | 紧密语句、较多partial及上述启动桥仍存在；不等于生产旧执行链双写 | 相关功能触及这些文件时，以一次真实调用链收整、删除替代代码；只有明确遗漏/重复执行才升级为架构阻断 | 不为新层抽象复制业务、事实库或事务，不把纯移动与行为混成一个变化 |
| 未定因：历史锁冲突 | P1旧final02与R6旧probe02的失败现场/记录保留，后者首次异常曾被后续收尾覆盖 | 若再现，先保存第一次异常、执行租约/文件句柄持有者和时间线，再分类判断；未复现前继续OPEN，不自动重试原失败样例 | 不能把SqliteStateStore的_gate、FSW或并行测试当已证实根因；新成功不能销项 |

成本任务的退出条件应是：**在同输入、同安全语义下减少明确热点，并且更新可见性/取消/异常保留不退化**。出现墙钟改善主张时至少复核多轮配对并保留离群值；没有变化的路径复用既有证据，不重跑全量合同。此处只登记后续测量方案，本轮未实施任何成本策略变化。

发布/真实登录自启、真实满盘、长期多屏/DPI继续按原状态携带；它们属于发布或环境验证范围，不补记本轮PASS。未知锁冲突在最终交付和集成说明中必须可见，不宜在未定因情况下承诺全环境稳定。

### 30.5 后续顺序与增量开发约束

1. **本轮结束于复审交付**：记录P2/P3通过，修正总索引和准入清单的旧R1～R4/R5结论；不新增人工矩阵，不默认启动P4。
2. **集成决策单独授权**：用户确认基础架构整理完成后，再决定提交/推送/合入impl/develop；本轮不操作分支、不推进impl/main或发布。已有未提交成果、候选和证据原位保留。
3. **真实热度只在另行授权后设计**：复用成功打开事实与既有提案消费链，补只读分页/稳定游标/幂等消费、纯策略、策略版本与取消/预算；输出提案不改作者布局。端口放Application，SQL游标实现留Infrastructure，评分/布局规则不进窗口，实例在组合根装配。现在不写实现，也不先造模板/AI通用平台。
4. **成本携带项按证据处理**：如下一能力会放大已知全局读取，应在那次实现中先测该相邻热点；没有触发证据则保持现状，避免无限架构整理。新文件业务仍走原账本/安全提交；新呈现仍走会话→纯布局帧→Presenter/同代命中，不向大窗口重新叠加业务。

本节是最终复审结论；第31节记录后续用户授权的正式收口与当前架构图。第20/21/24节和R4图源保留历史含义。未来目标图仍是方向，不是完成清单，也不是通过拆更多类即可达到的验收标准。

## 31. 本轮基础架构正式收口与当前架构图（2026-10-20）

用户明确要求“收口本轮基础架构整理，给出目前的架构图”，本轮记 **USER ACCEPTED / CLOSED**。R1～R8、P1～P3人工通过及第30节复审结果保留；不再为现有业务边界安排下一架构批次。本次只更新文档、图源与生成产物，不修改产品、真实数据、配置或安装状态，不提交/推送/合并。

### 31.1 图与源码依据

- [当前架构 HTML](../../../../out/architecture-foundation-closed-20261020/current.architecture.html)：9个职责节点，以用户意图→应用用例→海报会话/纯布局→增量呈现/同代命中为主线，旁列组装、观察、内置场景与存储/系统适配。
- [当前图源](../../20_Architecture/ArchitectureReview/Foundation-Closed/current.architecture.json)与[交付回执/24份源码摘要](../../20_Architecture/ArchitectureReview/Foundation-Closed/delivery.receipt.json)：对应impl/architecture-foundation@87b81a3及当前未提交工作区。没有把新文件伪装成旧提交的远程源码链接。
- [原目标图源](../../20_Architecture/ArchitectureReview/target.architecture.json)保持不变。R4图仍为历史快照，不能作为本轮现状图。

图中箭头表达主要运行协作，不是全部程序集引用：状态投影由App/StateProjectionCoordinator接入会话；用例不直接持有窗口。Infrastructure适配负责存储及文件/系统端口，Windows适配与独立Worker为系统能力聚合，不能理解为它们都在同一进程。六程序集精确依赖仍见第30.2节及src/README.md。

现有启动兼容桥未隐藏：总体顺序归ContentStartupUseCase，既有安全收尾留SqliteContentStartupSteps；不能承接新业务政策。真实热度生产链、动态模板、AI及独立更新器只列“尚未实现”，不画成现有运行节点。已有Lamp/Rain是编译期内置模块，不是运行时热加载平台。

### 31.2 图稿验证与明确限制

按Archify skill生成architecture类型、静态classic/中文查看器。最终validate与deliver均成功：**showcase 9/9、0错误、0警告**；图源5205字节、HTML715218字节，SHA-256见交付回执。首次源码链接诊断因工作区未提交而改用旁侧证据清单；按诊断调整节点尺寸、标签位置和水平间距，未修改Skill代码或升级Skill。

浏览器证据 **failed**，视觉复验 **skipped**，不得合并为“图稿已全面验证”：

1. packaged visual-check未自动发现Chrome；指定本机已有Edge（Chromium）后，DevTools检查进程提前退出，未产生四尺寸测量或深浅主题截图，最新回执为[浏览器检查失败记录](../../../../out/architecture-foundation-closed-20261020/current.architecture.visual-check.json)。
2. 补充内置浏览器读取本地HTML被file://安全策略拒绝；未换协议、另开服务或改用底层控制绕过。没有渲染截图可供图像审阅，因此不能记录首屏适配、主题视觉或查看器交互通过。
3. 确定性HTML产物仍可交付，未因浏览器失败而改写或覆盖；可由用户自行打开查看。若后续获得允许的浏览器证据，再单独补记，不要求重验已通过的业务或修改图来伪造浏览器通过。

### 31.3 收口后的边界

- 本轮职责整理关闭；剩余全局读取/无变化证据写入/慢目录与版本争用按第30.4节条件触发，不为抽象对齐无限拆层。
- P1 final02及R6 probe02锁冲突仍未定因；发布/真实登录自启、真实满盘和长期多屏DPI未验不补PASS。
- 分支仍impl/architecture-foundation，HEAD仍87b81a3，已有未提交源码/测试保持。用户本次没有授权Git集成或真实热度实施，后续另行决定。
