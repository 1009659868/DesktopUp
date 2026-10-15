# DesktopUp V2 文档入口

2026-10-15 当前：05-12原路径记录与删海报归还四组集中人工 **USER PASS / 本批CLOSED**；右键问题按用户决定不修订输入代码，详见[直接拖放§15.3](A_Project/40_Implementation/V3-05/DIRECT_TRANSFER_DELIVERY.md#153-人工通过与右键问题处置2026-10-15)。当前 `impl/05-12-content-lifecycle` 未提交/合回/推送，不改impl/main；本轮仅更新记录和[后续系列计划](A_Project/30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md#2026-10-15-05-12本批收口后的系列计划建议)。完整卸载归还尚未接线；下方待验/下一步05-12为历史。

2026-10-15 最新：跨海报拖放四组集中人工 **USER PASS / CLOSED**。本轮授权提交/推送功能分支并合入、同步 `impl/develop`，再从集成点派生05-12分支。下一批记录原始来源，删除普通收纳海报时归还文件，原路径不可用则回桌面；映射海报只解除映射，不引入恢复任务流程。Git结果见当前交接；本轮不更新 `impl/main`，下方待验/禁止同步为历史。

2026-10-15 当前：`impl/05-07-cross-poster-drag` 从develop 55582a7派生，推进跨海报直接拖放至集中人工候选。唯一详细记录为[本批C§14](A_Project/40_Implementation/V3-05/DIRECT_TRANSFER_DELIVERY.md#14-v3-05-07-跨海报直接拖放2026-10-15进行中)；未提交/合并/推送，不改发布线。下方外观UI和发布同步已完成。

2026-10-15 当前：控制中心外观UI的1～3项集中人工 **USER PASS / CLOSED**，既有参数和提交逻辑保持。功能0a75494、develop集成d66265f及精简发布f80765e已推送；20261015.1成品构建/安装运行/外观配置保真/卸载限定验证通过，已返回develop，交接随文档提交同步。验收见[外观与布局修订](A_Project/40_Implementation/V3-04/CONTROL_CENTER_USABILITY_DELIVERY.md#2026-10-15-人工结果与集成)，发布证据与边界见[发布记录](A_Project/40_Implementation/V3-04/PREVIEW_INSTALLER_DELIVERY.md)。下方“待验/不合并/不更新发布线”均为此前任务。

2026-10-15 最新：用户确认candidate04本轮1～3项通过，并授权当前功能分支提交/推送、合入及同步 `impl/develop`。本轮修订按[人工记录](A_Project/40_Implementation/V3-05/CONTENT_NATIVE_ACTIONS.md#2026-10-15-人工通过与集成授权)收口；历史长测/DPI等携带项不补PASS。随后从集成点创建并切换 `impl/control-center-ui-optimization`，只准备“外观与布局”的背景/标题/收纳栏优化，方案范围见[下一步](A_Project/30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md#2026-10-15-下一步控制中心ui优化准备)。不更新 `impl/main`。下方此前“当前”均为阶段历史。

2026-10-15 最新：N4修复Win菜单的“新建”子菜单通知与创建结果接线；Item/空白区入口统一“Win菜单”。candidate04/review04为新候选，candidate03及数据保留。定向78项通过，集中人工仍待确认；无本轮Git提交/推送/合并。详见[本轮C记录](A_Project/40_Implementation/V3-05/CONTENT_NATIVE_ACTIONS.md#2026-10-15-win菜单新建修订)。下方候选均为阶段历史。

2026-10-15 当前：N4继续修订真实删除、属性页生命周期及控制中心新增映射入口。旧candidate02已由用户正常退出；新candidate03/review03独立保留。实施/证据/集中验收以[本轮C记录](A_Project/40_Implementation/V3-05/CONTENT_NATIVE_ACTIONS.md#2026-10-15-真实删除属性生命周期与映射入口)为准；本轮不提交/推送/合回，未增加USER PASS。下方2026-10-14 Git同步及候选均为历史。

2026-10-14 Git检查点：用户授权提交并推送当前 `impl/05-content-native-actions`，**暂不合入 `impl/develop`**。源码/测试/验收入口提交 `8ded3aa`，本批文档随相邻提交保存；仅同步同名远端功能分支，不改集成/发布线。N4仍待人工复验，既有失败/未验项保持；本地out候选、配置和日志不上传。下方“不提交/推送”指此前制作阶段，当前边界见[交接](A_Project/40_Implementation/V3-04/HANDOFF.md)。

2026-10-14 N4反馈修订：属性/原生菜单失败已定位为错误的worker启动目录；新建/重命名改为直接创建＋Item名称行编辑。当前交付修订候选，待复验，不是N4已通过。启动脚本切到candidate02/review02，旧包/数据保留。唯一[实施记录](A_Project/40_Implementation/V3-05/CONTENT_NATIVE_ACTIONS.md)包含失败证据与新验收项；仍不提交/合回/推送。下方“只等N4”是本次反馈前状态。

2026-10-14 当前：`impl/05-content-native-actions` 已完成N3收尾（映射目录拖入目标、实际落点与失联/恢复反馈），进入N4集中人工，尚无本批USER PASS。独立入口 `scripts/start-content-native-review.ps1`；实现、证据、四组验收与未验边界统一见[真实内容基础操作](A_Project/40_Implementation/V3-05/CONTENT_NATIVE_ACTIONS.md)。构造卡片延期，不提交/合回/推送；下方为历史进度。

2026-10-14 当前：[海报增量呈现与外部拖入悬停让位](A_Project/40_Implementation/V3-05/POSTER_INCREMENTAL_RENDERING.md)两批人工分别通过，用户授权优化分支本地合回并切换 `impl/develop`，本轮不推送。准确集成结果见[当前交接](A_Project/40_Implementation/V3-04/HANDOFF.md)。下一主线05-04-V3构造卡片，须从集成后的develop另开分支；本轮不制作。首图偶发等待超时及其他未验项保持，下方为阶段历史。

2026-10-14 最新：单卡图标更换/重启保留/恢复默认三项人工通过，用户授权将 `impl/05-05-v2-card-icons` 合入并切换 `impl/develop`，本轮不推送或更新发布线。`impl/develop` 是基础能力闭环基线；后续新功能从它派生功能分支，集中验收通过后再合回。见[当前交接](A_Project/40_Implementation/V3-04/HANDOFF.md)和[V2实施](A_Project/40_Implementation/V3-05/CONTENT_ICON_ASSETS.md)。下一制作项 05-04-V3 构造卡片，本轮不实施、不沿旧 V3 分支开发。

2026-10-14 当前分支约定：V3 功能开发 → `impl/develop` 完整开发集成 → `impl/main` 精简发布源码。此次同步图标提取与安装快捷方式修订，发布验证/最终提交见[当前交接](A_Project/40_Implementation/V3-04/HANDOFF.md)，职责见[Git策略](Rules/GIT_STRATEGY.md)。下方双分支流程为建立集成线之前的历史；下一主线仍 05-05-V2。

2026-10-13发布同步：开发资料/测试继续保留在本分支；精简`impl/main`已完成独立构建及20261013.2自选路径安装、全新启动、登记卸载验证。公共打包修复两线一致，证据与未验边界见[发布同步记录](A_Project/40_Implementation/V3-04/PREVIEW_INSTALLER_DELIVERY.md#2026-10-13-发布同步)。本轮有Git同步授权，下方“无Git同步”为历史；下一主线仍05-05-V2。

2026-10-13最新增量：按DEC-048完成[05-14六项界面收敛§13](A_Project/40_Implementation/V3-05/DIRECT_TRANSFER_DELIVERY.md#13-05-14界面减法与通用背景2026-10-13)：空海报/底部提示减法、模板按装配显示参数、通用背景颜色/不透明度与菜单去重。先交付本轮查看版，再衔接05-05-V2；独立三组件装配器未制作，D4-N既有结论保持。

2026-10-13当前：D4-N四组USER PASS，D4-N/BUG-037已收口；[05-14入口收口§12](A_Project/40_Implementation/V3-05/DIRECT_TRANSFER_DELIVERY.md#12-05-14入口收口)CODE/AUTO完成：撤阶段性添加入口、修复并入所选项目管理菜单，保留底层引用和拖放安全。下一步05-05-V2图标替换/恢复默认，未实施；下方待D4-N人工为历史，未验环境继续携带，无Git同步。

当前（2026-10-13，DEC-047）：D4-N已接真实Shell拖出；取消有效拖放的额外确认和共享引用，失效卡片仅松手后确认移除。下一步是同包集中人工验收，不重新验D5既有结论。实施、证据与入口见[直接拖放§11](A_Project/40_Implementation/V3-05/DIRECT_TRANSFER_DELIVERY.md#11-dec-047单一引用与直接原生拖放)。下方“新接线尚未实施”是本轮前的历史状态；无本轮Git同步。

当前（2026-10-13，D5反馈）：[四组人工全部通过](A_Project/40_Implementation/V3-05/DIRECT_TRANSFER_DELIVERY.md#95-d5人工结果)，BUG-036关闭；用户同时要求桌面落点走真正Windows原生拖放。下一步[D4-N原生Shell拖出整改](A_Project/40_Implementation/V3-05/DIRECT_TRANSFER_DELIVERY.md#10-d4-n原生shell拖出整改2026-10-13)，已对照DeskBox核清当前捕获松手搬文件的缺口，新接线尚未实施。整改集中验收后再05-14入口收口→图标UI/构造，暂保留备用添加入口。旧包/配置/未验环境保留，无本轮Git同步；下方待D5为历史。

当前（2026-10-13，05-11-A）：公共关系移除与最小外部同步已实施，最终3268合同、同包首启/重启通过；BUG-036人工剪切复验保留至D5，D3既有人工结论不变。详情见[直接拖放§8](A_Project/40_Implementation/V3-05/DIRECT_TRANSFER_DELIVERY.md#8-05-11-a最小外部同步与公共移除2026-10-13)。下一步空白框选规则收敛/实施 → D4 → D5，再图标UI/构造；右键/模板架构暂定。无Git同步，旧配置不动；下方为历史阶段。

Git同步检查点（2026-10-12）：本次用户授权保存当前D1～D3累计工作，源码`10b2afc`及相邻文档提交，目标仅`origin/impl/poster-redesign-V3`；不合入`impl/main`，不上传候选/配置/日志。验证与携带项见[当前交接](A_Project/40_Implementation/V3-04/HANDOFF.md)，D3最终原生复验及人工仍待完成；下方“不Git同步”是此前实施边界。

当前（2026-10-13，DEC-043）：用户取消普通点击穿透。D3改为可见全框输入＋同HWND OLE注册，移除生产专用捕获监测/额外接收窗；文件业务和原2～5项人工结论保留。BUG-022以需求取消退役，不补PASS，不再推进旧穿透探针。当前验证及集中人工归[D3§8](A_Project/40_Implementation/V3-05/D3_NATIVE_INGRESS_AND_PLACEMENT.md#8-取消普通点击穿透并收敛接收组件)；D4/D5未开始，普通权限/满盘等携带不变，未Git同步。

2026-10-10 Git 分支决定：开发继续在 `impl/poster-redesign-V3`，每个完成的里程碑再整合到精简发布源码线 `impl/main`；规则见 [开发与发布分支](Rules/GIT_STRATEGY.md)。发布精简不反向合入开发，不改变未验项结论。

DEC-039已确认：先把基础收纳交互做稳定，再做图标更换UI／构造卡片。桌面↔海报默认剪切式移动；旧引用拖出也移动原对象，且事前明确确认源路径。

当前文档核对（2026-10-09）：[原审计§14](A_Project/40_Implementation/V3-04/LR_MILESTONE_EXECUTION.md#14-文档源码对齐审计与05-01准备2026-10-09)保留当时范围；正式关系持久化、非破坏性添加与共享修复已同步到[工作流§6.2](A_Project/30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md#62-v3-05-04-当前实施与验收)。

2026-10-10当前（DEC-041）：**失败自动收尾＋基础原生拖放，准备人工验收。** 旧恢复任务界面退役，§10.4四项人工不再执行、不补PASS。普通失败直接重新拖动；内部账本、身份／防覆盖保护和schema18保留。[当前实施与验收](A_Project/40_Implementation/V3-05/DIRECT_TRANSFER_DELIVERY.md)记录自动收尾、CF_HDROP拖入、原捕获手势拖回真实桌面与支持矩阵。本批同卷本机文件／目录／快捷方式，跨卷及任意Explorer目标未开放；真实满盘继续延期。旧包和证据保留，未操作用户文件、未Git提交推送。下方旧阶段只作历史。

## 历史检查点（不覆盖页首当前入口）

Git同步检查点（2026-10-08，本次用户授权）：源码`f2f6804`保存尚未同步的RR-06/Rain/标题修订、R7-0可靠基线和R7-A/B完整配置往返；相邻文档提交保存计划、证据与交接。同步目标仅`origin/impl/poster-redesign-V3`，远端结果以实际Git核对；不合入发布分支。本次2396合同复跑通过，原final04的544源码/161产物/4份proof哈希复核一致，详情见[交接](A_Project/40_Implementation/V3-04/HANDOFF.md)。下一步仍为R7-C，再R7-D；下方“无Git提交推送/无授权”为各次实施时的历史快照，不改变原验收范围。本地笔记、配置、候选及日志均不上传。

2026-10-08当前：**R7-A/B实现及自动门完成，整体R7未关闭**。便携v5携带模板册、当前/未选模板实例、标题显隐/颜色、内容布局和中心主题；DEC-032同ID异定义保留两份，导入副本确定性映射，原单事务/CAS/备份守卫保持。唯一同包证据为`out/r7-portable/final04`：2396合同、4项门、14个应用进程及Lamp/Rain各5段缺模块便携检查通过，error/退出资源0，544源码/161产物指纹未变。原始失败及旧包/配置保留，详见[R7-A/B合同与实施](A_Project/40_Implementation/V3-04/LR_MILESTONE_EXECUTION.md#116-r7-ab实施与验证2026-10-08)。下一步R7-C长时资源与连续可靠性，再R7-D真实显示/DPI、原生选择器/手势与人工验收；不把本轮当整体R7关闭，不进入R8。本轮未启动新人工包、无Git提交推送。下方R7-0或更早“尚未实现/下一步A/B”等是历史范围，不转授旧证据给当前源码。

最新续做（2026-10-08）：用户追加云尺寸及更强起伏/倾斜，转[模块§16](A_Project/40_Implementation/V3-04/LR_SCENE_MODULE_EXECUTION.md#16-云尺寸参数与增强起伏倾斜2026-10-08)。review28已正常退出，旧包/库保留；新参数0.60～1.10倍、默认1，绘制/命中共用缩放与边界拟合，风力提升升降/转角占比。rain-size candidate05已通过final04完整16门、2338合同与42进程，536源码/161产物指纹不变，error/退出资源0。严格入口已打开独立review04（初始PID3276，中心显示/启动日志正常），当前AUTO GATES COMPLETE / USER PENDING。参数与风摆准入分离、失败证据及短时资源限制见§16；不继承旧人工结论，确认后再R7，RR-06仍关闭、BUG-031延期，无Git操作。下方§15及更早候选是历史范围。

最新增量（2026-10-08）：candidate25云风感/裁切用户未通过，贯穿闪电形态认可；当前按[模块§15](A_Project/40_Implementation/V3-04/LR_SCENE_MODULE_EXECUTION.md#15-ui-c风感对齐整周期防裁切与随机闪电2026-10-08)对齐UI C复合风摆、防裁切及可调间隔/随机落点。candidate28已通过final28b完整同包16门、2327合同及42进程；严格入口已启动独立review28（初始PID42520），当前AUTO GATES COMPLETE / USER PENDING。失败重试与资源短样本限制保留在§15，不代填真实鼠标/桌面合成PASS。RR-06仍CLOSED，BUG-031延期，人工确认后再R7；下方§14及更早候选为历史。

最新（2026-10-08收口）：**RR-06 CLOSED / USER PASS**。用户确认标题无底板、独立颜色及射灯交互跟随通过，BUG-030/034按该范围关闭。先完成本次“设置夹＋Rain风纹/闪电”增量修订，再进入R7；当前证据和入口见[模块§13](A_Project/40_Implementation/V3-04/LR_SCENE_MODULE_EXECUTION.md#13-rr-06收口与r7前增量修订2026-10-08)。BUG-031仍延期未修，无Git授权；下方待验文字均为历史。

当前（2026-10-08）：**RR-06同包自动门完成，人工待验**。最终candidate12的2274合同、16项门、42进程及严格人工入口通过；补修删除恢复BUG-033，白条BUG-031仍延期未修。续做[模块§11](A_Project/40_Implementation/V3-04/LR_SCENE_MODULE_EXECUTION.md#11-rr-06同包自动门完成进入人工验收2026-10-08)与[交接](A_Project/40_Implementation/V3-04/HANDOFF.md)。本轮不进R7、不Git同步；下方candidate07待解锁等为历史快照。

Git同步检查点（2026-09-30，本次用户授权）：源码`5f753e1`及相邻文档提交保存R6后续、模块/Rain和RR整改，目标`origin/impl/poster-redesign-V3`；Release构建及2274合同复验通过。阶段不变：RR-06待补验、白条延期，详见[交接](A_Project/40_Implementation/V3-04/HANDOFF.md)。本地笔记/配置/产物不上传，下方“无Git授权/未提交”是此前快照。

当前（2026-09-30）：[RR整改实施§10](A_Project/40_Implementation/V3-04/LR_SCENE_MODULE_EXECUTION.md#10-rr整改生产接线与同包验证2026-09-30)。唯一云标题/可读按钮、安全提速、受影响收展组隔离已接生产；candidate07全量2274及双组WinUI/R4/模块移除恢复通过。白条BUG-031按用户要求延期，未修复；RR-06同包余门和人工待解锁继续，不能整体标完成，不进R7。下方各次“最新/下一步RR-02”均为历史。

最新入口（2026-09-30，DEC-031）：[Rain整改计划§7](A_Project/40_Implementation/V3-04/LR_SCENE_MODULE_EXECUTION.md#7-rain人工反馈修订入口2026-09-30)已补齐，当前仅文档；下一步RR-01反例基线与唯一标题合同，后续标题/对比度→白条→安全提速→组隔离→同包复验。Rain仍NEEDS REVISION，不直接进入R7。下方为此前工程/验收范围。

当前实施（2026-09-30，DEC-030）：**R6 CORE CLOSED（用户批准携带未验项），最小模块B/C/D自动门通过；雨夜窗边生产接线与最终自动门通过，当前人工发现四项问题，待修订**。收口边界见[R6记录§9](A_Project/40_Implementation/V3-04/LR_R6_CONTINUOUS_OPERATIONS.md#9-2026-09-30-r6收口决定与携带项)，当前工程见[模块实施记录](A_Project/40_Implementation/V3-04/LR_SCENE_MODULE_EXECUTION.md)。真实显示/DPI等不补记PASS，转R7携带；以下同日/更早R6未关闭及下一步描述均为历史。

当前同步（2026-09-28）：按用户授权保存 R6 第一批/2A、可配置 Preview 打包及相关文档到当前 `impl/poster-redesign-V3`；源码检查点 `c815015`，2175 合同复验通过。提交/远端结果以 [最新交接](A_Project/40_Implementation/V3-04/HANDOFF.md)和实际 Git 为准；本地笔记、配置、安装产物及运行证据不上传。下一步仍为 R6 2B，不增加人工验收结论；以下未提交/未推送描述为历史。

按需维护（2026-09-28）：[Preview一键打包／自选安装路径／默认深夜书桌25%](A_Project/40_Implementation/V3-04/PREVIEW_INSTALLER_DELIVERY.md)。旧安装器已移入回收站，当前交付脚本与配置；本机自动验证通过，目标机与普通权限待验。R6下一步仍为2B，未提交推送。

最新实施（2026-09-28）：**R6 IN PROGRESS**。第一批循环抓取/导航/结束锚及第二批2A主动缩框下限已接生产；candidate03构建、2158合同及隔离App回归通过，原生人工未验。下一步2B被动空间准入/只读二维/恢复，见[R6记录](A_Project/40_Implementation/V3-04/LR_R6_CONTINUOUS_OPERATIONS.md)。下方同日“仅对齐、尚未实施”属于开始R6之前的历史。

新agent从[冷启动提示词](00_Catalog/AGENT_COLD_START.md)开始：包含可复制入口、当前基线和分层核对步骤；实际阶段以当前决定/计划/交接为准。

2026-09-28本机对齐：实际工作区`G:\A_\Tools\DesktopUp_V2`，当前`impl/poster-redesign-V3@98e0204`已合入远端wb，领先其跟踪分支4个提交、未推送。当前阶段仍是R5核心收口、R6就绪未实施；本轮仅核对新增内容、更新本地记忆和接手导航，不启动R6。UI C核心已是生产实现，不再沿用“只有HTML原型”的旧认知；本机启动预览及证据边界见[最新交接](A_Project/40_Implementation/V3-04/HANDOFF.md)。

2026-09-27最新（DEC-029）：R5核心已收口，用户延期BUG-026并批准继续、最小解耦及解耦后的雨夜窗边。先读[R5收口与代码/计划审计](A_Project/40_Implementation/V3-04/LR_R5_CLOSEOUT_AND_PLAN_AUDIT.md)，后续里程碑看[LR计划§8](A_Project/30_Plans/V3_04_L_REMEDIATION_REVIEW_PLAN.md#8-当前出口和最近下一步)，候选/验证看[HANDOFF](A_Project/40_Implementation/V3-04/HANDOFF.md)。R6就绪但实现未开始，模块/Rain/R7/R8及真实Item未完成；下方较早阶段为历史。

历史节点（2026-09-26）：R1合同齐全、R2基线完成；R3统一几何/浏览/手动投影/空间资格模型及UI C最小原生样板完成，248项相关/1758项全量回归通过，R4安全保存合同及代表模板原生绑定完成（1868项合同）；当时进入R5首次直接操作。阶段证据与分工见[执行记录](A_Project/40_Implementation/V3-04/LR_MILESTONE_EXECUTION.md)。

## 历史入口（2026-09-26，LR里程碑goal启动；不覆盖上方当前状态）

用户已授权按[LR-R1～R8计划](A_Project/30_Plans/V3_04_L_REMEDIATION_REVIEW_PLAN.md)建立goal并推进方案/文档/代码/测试及必要子agent协作。DEC-024/025收清保真兼容、单草稿冲突和新建纵向/循环关闭；R1实施合同齐全，R2～R4完成、R5实施中。当前分工、基线及验证入口见[里程碑执行记录](A_Project/40_Implementation/V3-04/LR_MILESTONE_EXECUTION.md)，阶段状态归细化工作流。

DEC-021：先用[整体体验评审/E1](A_Project/20_Architecture/CONTENT_LAYOUT_EXPERIENCE_REVIEW_V1.md)验证基础手势、适配整图承接和模式退出覆盖；现有决定暂不推翻，控件细化后置。E1六任务反馈已收清（06c提示后恢复成功）；DEC-023采用double/退出A，DEC-022批准被动极端不足时临时双轴。[操作/空间方案](A_Project/20_Architecture/CONTENT_LAYOUT_OPERATION_SPACE_REVIEW_V1.md)已成文；旧41项原型证据不验证新双轴，不是生产通过。

DEC-019：纯手动/热度自动两种模式，均在桌面海报按住Item并移动即拖，无整理按钮/长按时间前置；纯手动支持指定网格位置和主动留白，控制中心另提供精细微调。自动拖动仍按DEC-018固定顺序位，不能把手动位置也降为序号。

[UI C](A_Project/20_Architecture/CONTENT_TEMPLATE_SCHEMA_UI_C.md)视觉方向保持采纳；原型里的强制“整理”入口已落后于新决定，需定向调整，不重选整套风格。UI C原稿保留；此前新增独立E1体验试验，未改生产。

DEC-020手动局部让位/当前构图/有效编辑承接保持；[组合核对](A_Project/20_Architecture/CONTENT_LAYOUT_COMBINATION_AUDIT_V1.md)12条行为映射和19条轨迹作为R3～R7实施入口。几何候选与原生效果须分别证明，R1合同齐全不代替这些门。

目标仍是稳定、好用的桌面内容收纳。当前状态归[工作流](A_Project/30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md)，缺陷修复证据归[清单](A_Project/40_Implementation/V3-04/KNOWN_ISSUES.md)与VERIFICATION；真实Item未开工，旧候选证据不转授当前dirty源码。

## 历史阶段摘要

最新执行（2026-09-17）：[04-L](A_Project/30_Plans/V3_04_L_CONTENT_LAYOUT_PLAN.md)的L01～L06实现和自动门完成：1630合同、最终R4同包35进程门及1/3/6资源基线通过；25指纹/35门人工入口已在PS5.1预检。当前进入L07人工验收，真实输入/像素尚未确认。能力、限制与操作见[交付](A_Project/40_Implementation/V3-04/CONTENT_LAYOUT_DELIVERY.md)。旧延期和BUG处置不变，不补PASS。 最新L07反馈要求单轴/横向循环并简化整理，当前NEEDS REVISION；按DEC-009先讨论交互修订，尚未进入05-01。

历史阶段（2026-09-16，04-06首批）：[04-06专项计划](A_Project/30_Plans/V3_04_06_PORTABLE_PROFILE_PLAN.md)的A/B/C生产接线与D1自动门已通过；完整1452/1452合同、同包portable6＋既有回归12＋离线恢复窗口3门通过，退出资源均为0。分批人工范围见[交付/人工矩阵](A_Project/40_Implementation/V3-04/PORTABLE_PROFILE_DELIVERY.md)，真实另一主机尚未验证。空间不足按V2-TBD-021保留全部配置为“等待安置”，不改显隐意图或尺寸/收展。旧layout包不能验收04-06。

历史入口（2026-09-16，04-06开始前）：05-05托盘人工G1～G8用户确认通过；[布局统一修订](A_Project/40_Implementation/V3-04/LAYOUT_UNIFICATION_DELIVERY.md)完成schema8兼容修复，1315合同/12同包门及旧配置副本迁移通过，外观三项USER PASS。旧layout包/入口与配置保留作证据；当时04-L仅设计、下一步04-06-A，现由DEC-008的L01入口替代，不再按该旧顺序开工。

历史记录（04-04滑块交付，2026-09-15）：[紧凑滑块与轨道预设点](A_Project/40_Implementation/V3-04/CONTROL_CENTER_SLIDER_REFINEMENT.md)1283合同/新同包六组门PASS，真实点选/微调待复验。旧PID30652已退出；当时要求先排查列表、暂不进入05，后续用户已允许BUG-004非阻塞并进入05。旧候选入口不冒充新托盘验证。

历史记录（04-04 UX交付）：1265合同/六组进程门及旧人工反馈见[UX实施记录](A_Project/40_Implementation/V3-04/CONTROL_CENTER_USABILITY_DELIVERY.md)；旧02～06证据见对应交付，不再将旧PID或“下一步UX-05”作为当前指令。M08余门不补PASS。

历史检查点：2026-09-17的04-L自动交付/L07待人工；当时分支impl/poster-redesign-V3，HEAD记录b118936。2026-09-23审查时两本地分支均为9607d4e，差异来自wb未提交工作。2026-09-24用户授权保存该工作，将impl/poster-redesign-V3-wb合入并切换至impl/poster-redesign-V3，再推送两分支；后续工作入口为impl/poster-redesign-V3。该开发检查点不改变LR-R1状态、不继承旧候选PASS；实际提交/远端结果以Git记录为准。

## 常用导航

- [当前执行路径](00_Catalog/ACTIVE_PATH.md)：A → B → C 与最近步骤。
- [当前交接](A_Project/40_Implementation/V3-04/HANDOFF.md)、[代码锚点](A_Project/40_Implementation/V3-04/CODE_ANCHORS.md)、[验证日志](A_Project/40_Implementation/V3-04/VERIFICATION.md)。
- [04-L任务卡](A_Project/30_Plans/V3_04_L_CONTENT_LAYOUT_PLAN.md)保留原计划；当前先看[整改评审计划](A_Project/30_Plans/V3_04_L_REMEDIATION_REVIEW_PLAN.md)。[旧交付/人工矩阵](A_Project/40_Implementation/V3-04/CONTENT_LAYOUT_DELIVERY.md)仅适用对应包，不直接用于验收当前未提交修订。
- [白条修复与清理证据](A_Project/40_Implementation/V3-04/M07_ANIMATION_R4.md)；[同类问题排查方法](A_Project/20_Architecture/PRESENTATION_DIAGNOSIS_PLAYBOOK_V1.md)。
- 历史稳定动画入口：`scripts/start-m07-animation.ps1`；[启动配置](../config/README.md)。R2/R3/独立诊断入口已退出活动目录，旧动画包不能验收04-06。

## 权威顺序

1. 用户最新明确决定。
2. [七份冻结需求](00_Catalog/REQUIREMENTS_INDEX.md)。
3. [总体架构](A_Project/20_Architecture/DESKTOPUP_V2_V3_ARCHITECTURE_V1.md)、[模块目录](A_Project/20_Architecture/DESKTOPUP_V2_V3_MODULE_CATALOG_V1.md)。
4. [V3 制作计划](A_Project/30_Plans/DESKTOPUP_V2_V3_IMPLEMENTATION_PLAN_V1.md)、[细化工作流](A_Project/30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md)。
5. 对应阶段的实现合同、验证和交接；历史失败记录不能覆盖当前状态。

## ABC归属与历史记录

ABC是逻辑分层，不要求搬迁现有目录。A维护产品需求、全局约束/架构；B维护模块职责、端口及代码映射；C维护任务顺序、实现/验证证据和交接。

- 产品决定/未决/延期以[统一索引](A_Project/10_Requirements/REQUIREMENTS_RISKS_AND_OPEN_QUESTIONS.md)为准；C层只引用决定ID及安排实施，不另行裁决产品行为。
- [细化工作流](A_Project/30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md)维护当前子项状态；总计划维护阶段依赖，HANDOFF是最新交接摘要，登记表只导航，不各自维护另一套待办。
- 验证日志和旧版任务卡保留当时状态/产物。HISTORICAL不等于无用，PLANNED不等于已实现；旧“待验”不能覆盖后续证据，后续PASS也不能扩大到未经验证的环境。

按步推进的每次交付须明确：当前步骤及验证范围、对应计划/证据文档链接、下一步计划；没有实机验证时不将纯模型通过描述为产品交互已修好。

## 产品与边界

正式方向：通用桌面内容收纳、多海报、一个控制中心、一个托盘、独立风格主题、热度排版、AI 辅助定制。游戏图标海报是特色起点，不限制通用文件/目录/应用/Shell 内容。
技术路线沿用 DeskBox 的 Windows 原生经验与 WinUI；不恢复旧 Qt/WebView/Axiom 试验。当前内容仍是 DEMO ITEMS，04-L支持异尺寸样例与有界压力数据，未实现真实文件收纳。

已有基础：V3-01～03、海报多实例/输入/空间、M08自动收展、管理/托盘/恢复及LR布局均有分项实现证据；不再把M08最小设置/观察标为当前待开发。历史阶段出口不代表所有原生/环境细项已通过，现携带项归执行§12和KNOWN_ISSUES。
当前顺序：R8交接完成 → [V3-05-01合同/纯验证准备](A_Project/30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md#61-v3-05-01实施准备实体能力与打开结果) → 引用/展示/真实打开 → 文件安全闭环。取消的是V3-04-05-03托盘单张子菜单，不是V3-05-03 Managed Collection。04-07/08延期和M08原未验项保持，不自动重启标题缺陷循环。
[按需优化登记](A_Project/30_Plans/ON_DEMAND_OPTIMIZATIONS.md)按需执行；C2长测按DEC-033放在基础功能（含真实内容）完成后，不启动40分钟测试，不据此宣布稳定发布。

需求保留：空配置首次仅一张默认海报；中心/托盘新增，中心/海报菜单删除并确认后果；单文件跨机习惯配置。配置迁移按 04-06-A..D，自定义素材按 07-04，不以内部 SQLite 备份冒充便携配置。
历史 V3_CURRENT 可视化是当时单海报实现快照，不是多海报当前实现证明。
