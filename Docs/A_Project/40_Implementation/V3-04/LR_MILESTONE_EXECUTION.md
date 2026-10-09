# LR-R1～R8 里程碑执行与多 agent 交接

当前接续（2026-10-09）：V3-04/R7/R8历史证据仍归本页；新主线实施日志、代码锚点与交接转[V3-05-04实施](../V3-05/NON_DESTRUCTIVE_REFERENCE_ADD.md)。§14是实施前审计，不覆盖正式引用生产接线已完成的事实，也不代表真实打开/文件执行已经完成。

2026-10-09当前：**V3-05-05／06 已接生产／人工待验**。[真实显示和使用](../V3-05/REAL_CONTENT_DISPLAY_AND_ACTIONS.md)已接真实Shell图标、有界隔离worker、打开/定位、Ctrl/Shift选择及仅移除关联；SQLite14持久打开收据和成功事实。05-04添加、复用/共享修复、重启和选择器退出已获用户确认，PID42288正常退出，旧包/库保留。本轮独立候选与自动证据以C记录为准；先收本轮人工，再05-02/03集合能力，真实文件移动须先05-08账本。R7携带/C2后置不变；未Git提交推送，下方旧状态为历史快照。

2026-10-08当前：用户允许标题显隐完成后进入R7，范围核对和工作包已登记[§11](#11-r7配置保真与可靠性启动2026-10-08)。标题增量工程证据归[模块§17](LR_SCENE_MODULE_EXECUTION.md#17-标题显隐与r7准入2026-10-08)；R7可靠性尚未验收，不承接旧候选的人工结论。

更新：2026-09-30（DEC-031）。当前：**R1～R4出口保持，R5/R6 CORE CLOSED；最小模块B/C/D自动门通过；Rain NEEDS REVISION，整改计划已补齐但未实施**。按[模块记录§7.5](LR_SCENE_MODULE_EXECUTION.md#75-分批推进与出口)安排RR-01～06，最近下一步RR-01反例基线与唯一标题合同；本轮仅文档，不开始新实现/运行门。历史自动门及[R6§9携带清单](LR_R6_CONTINUOUS_OPERATIONS.md#9-2026-09-30-r6收口决定与携带项)保留原范围，R7未开始。主agent持有本轮全部修改，旧子agent分工无效；不重做历史R6阶段，不把自动检查当作原生输入或桌面像素验收。

用户已在手势/三组核对后要求扩大推进跨度、建立goal，授权方案、文档、代码和测试推进及必要子agent辅助。Q1/Q2按用户随后“ok，准备开始推进”承接前轮建议；新建默认由用户另外明确选择纵向＋循环关闭。权威决定见[DEC-024/025](../../10_Requirements/REQUIREMENTS_RISKS_AND_OPEN_QUESTIONS.md)，阶段出口仍唯一维护在[主计划§4](../../30_Plans/V3_04_L_REMEDIATION_REVIEW_PLAN.md)。本文件记录执行事实、分工和证据，不替代需求或重新编号计划。

## 1. 持续推进方式

1. 开始一个实施单元：记录所属LR阶段、目标行为/反例ID、文件所有者、读写边界、依赖及最小验收。
2. 先核对现有代码与基线，必要缺陷先补会失败的回归；修复后运行针对性测试，再执行受影响正式门。产品选择未决时提出具体反例与选项，其他独立工作继续。
3. 接口、schema、共同文件由主agent统筹；子agent只修改明确分配文件，不自行扩大、回退或覆盖他人改动。无不同意图的第二solver/权威状态。不同单元不要同时运行会覆盖同一bin/obj的构建。
4. 主agent检查合并后的差异、运行统一验证，按源码状态、环境、日志及产物hash登记证据。测试数、提案Accepted、Win32返回值与人工视觉是不同证据，不相互替代。
5. 每个里程碑核对主计划出口，更新工作流、HANDOFF、VERIFICATION及必要CODE_ANCHORS/KNOWN_ISSUES。未过项保持OPEN；进入下一阶段不需要重复申请已授权的普通开发，但不能跳过依赖门。
6. 用户需要裁决产品范围/行为或提供人工验收时，报告具体结果再提问。没有授权Git提交/推送/发布；不迁移日常库、不执行真实文件搬移。App验证使用明确隔离DataRoot，启动前检查已有进程/单实例风险。

## 2. 当前实施单元与归属

### 2026-09-27 当前：R5收口、Bug延期与代码/计划对齐

root负责全部文档与统一复核；r5_close_audit、layout_plan_audit仅只读审计，已交回，无在写文件。登记BUG-026并遵循用户延期决定；DEC-029批准最小B/C/D、解耦后Rain及恢复推进。审计修正旧Bug机制状态、schema12/10参数/配方已交付、Portable能力边界及R6真缺口；未改生产/测试或用户库。

445源码与候选25零漂移，启动器CheckOnly复核成功；既有2115合同、八组13进程门的证据不重复伪称本次重跑。R5有限核心路径关闭，遗留风险及用户反馈范围归收口报告。下一实施为§9的R6循环抓取/导航，后续文件分派须重新明确；所有旧agent写入分工失效。R6实现尚未开始，模块/Rain亦未开始。

下面各“最新”标题是对应候选的历史记录，其中“主线待审/模块待批准/R5未通过”不再作为当前指令；失败与未验事实仍保留。

### 2026-09-27 最新：循环起点反向滚动与主线审阅

用户认可当前UI/动画，同时报告纵向循环起点向上被挡。root负责两轴wheel映射、合同/接线、集成候选/Docs；lamp_motion仅新增独立App.ContentBrowseProbe.cs与probe-content-browse.ps1，已交付冻结，无并行GUI。源码根因是取模前Math.Max(0)和逐事件Round；独立定向红测8/10失败，旧App首个反向事件也失败。修正后47相关、2115全量、生产handler/XAML/命中12场景102事件及八组同包门通过，候选25/启动绑定见[报告§9](LR_R5_POINTER_AND_MODULE_REVIEW.md#9-候选25循环起点反向滚动与主线审阅节点)。真实滚轮人工复看仍待用户；本单元不代表开始R6复杂组合。

当前资料同步包括DEC-028、已知缺陷分项、主计划§8及工作流。所有新验证用独立DataRoot，原human-review-08关闭后已只读复制，用户配置不回退。修复和交接完成后暂停主线，等待用户判断建议步骤与模块B/C/D插入位置。

### 2026-09-27 最新：A/B透明度与展开过程补验

候选23末帧修正未覆盖用户新报的展开中灰底。仅交换A/B不透明度确认灰底跟随25%；候选24在共同视觉提交后清新旧HRGN差集，四组三阶段原生像素通过、合同2105。root拥有合同/native/窗口接线/测试/Docs；lamp_motion只改隔离BackdropProbe、Posture钩子、新motion脚本，交付冻结后只读复审。缓存恢复缺口已按审查修正；所有GUI串行、新数据根，原human-review-07保留。最终同包与交接状态见[报告§8](LR_R5_POINTER_AND_MODULE_REVIEW.md#8-候选24展开过程灰底与ab不透明度对照)。R5未整体通过，R6未开始，模块拆分仍待审阅。

### 2026-09-27 最新：半透明灰底与系统动效诊断

用户新报告A背景及追光/闲置双停；root持有生产/诊断/文档，lamp_motion只读交叉审查，无并行GUI。候选23修正收展末帧透明底面清理；2102合同、四组原生终态90点、八组同包门通过。系统曾报告动画关闭并由App实证，后恢复true时计时/处理器正常；未改系统偏好或扩张模块拆分。新human-review-07保留故障配置副本，来源/指纹/旧失败与范围见[报告§7](LR_R5_POINTER_AND_MODULE_REVIEW.md#7-候选23半透明收展灰底与系统动画策略)。R5未整体关闭，R6不提前开始。

### 2026-09-27 最新实测：输入/图钉/真实追光及模块评估

候选20后用户认可其他样式、灯光与图钉交互，同时报告控制中心默认黑底；本轮聚焦资源隔离，不扩大改动已认可的交互。候选21复现system模式共享画刷污染并修复，实际控件378状态与保存重启通过；root继续持有App/Docs/集成，详见输入报告§6，模块拆分状态不变。

候选20已将真实鼠标问题从环境阻挡推进到复现/修复：全透明NVIDIA覆盖窗口误挡被动观察，窄范围alpha判定修正后真实两点跟随/离开轻摆及NativeMouse条目链通过；全量2102/2102。证据与人工边界见输入报告§4.2。当前A单元仍保留桌面空白手势、图钉直接右击和视觉复验，B/C/D模块方案待审阅。

依据DEC-027。root持有公共路由/原生区域/按下见证、App集成、全部Docs、候选和串行验证；everyday_visuals完成图标及解除策略后交root冻结；lamp_motion完成同次指针诊断/真实门后交root冻结；r2_review只读复核。审查发现右键两阶段误读处理时光标的问题，由root改为消息事件坐标并验证，不因原合同PASS忽略。

具体进展见[输入报告](LR_R5_POINTER_AND_MODULE_REVIEW.md)，长期仍R5 IN PROGRESS。用户已正常退出候选17并允许隔离测试，未授权关闭其他窗口；所有新验证使用新DataRoot。可移除Lamp尚不受当前框架支持，[拆分方案](../../20_Architecture/CONTENT_SCENE_MODULE_DECOUPLING_PROPOSAL.md)供审阅，未实施B/C/D，不将方案成文视作完成。下方候选17运行PID及旧文件归属均为历史。

### 2026-09-27 六项实机反馈：第三轮C对齐

本单元当前交付状态：用户确认旧实例退出后完成候选17同包运行；八组门有通过记录，首次构图窗口区域读回失败独立登记BUG-021，复查不等于修复。日常/编辑显示、配方/主题重启、Lamp共享帧及资源门已取得代码与自动证据，UI产品和原生出口尚未关闭。2065/2065合同输入未变，427源码/401产物绑定已核对；新体验PID26864留给用户。事实、失败及精确边界见[对齐记录§8.1](LR_R5_UI_C_INTENT_ALIGNMENT.md#81-候选17同包运行与体验交付)。三个agent再次冻结，root持有全部文件；下面表格出口不因局部门通过自动算完整产品完成。

当前用户明确要求取代此前“静态Lamp/只读配方即可、装入留待V3-07”的有限交付范围。依据C声明与配方流程实施受控新ID装入/副本/重启持久化；不实施watcher、同ID替换、在线AI或任意执行。原型浏览器工具本轮初始化失败（沙箱ACL错误）；已查看用户七张截图并读取C HTML、说明和依赖源码，不能声称新浏览器实操已通过。

| 小里程碑 | 所有者及文件边界 | 出口 |
| --- | --- | --- |
| 日常/编辑显示分离 | everyday_visuals：Content绘制/反馈、默认辅助及针对性测试；共享主文件交root | 日常选择无描边/锚点；编辑有；直接拖动保持；新建辅助关、旧配置保留；测试提示仅显式probe启用 |
| 配方数据合同 | recipe_core：Domain/Application/Infrastructure及合同测试 | 有界声明校验、新ID装入、副本默认、逐海报逐模板覆盖、原子保存与重启；失败/取消/过期不写 |
| 灯光行为 | lamp_motion：LampScene/新增局部文件，共享生命周期交root | 海报范围内跟随，离开轻摆；声明参数生效；减少动效/隐藏/编辑/关闭正确暂停与释放 |
| 中心完整路径 | root：全部ControlCenter/App接线及Docs | 装入/检查/使用、留存副本、真分页、模板册/参数、独立主题及统一纸面组件；无AI说明、无测试内容常驻 |
| 同包交付核对 | root串行构建/测试/原生探针 | 新候选及绑定证据；显示/参数/失败/两海报/重启与资源清理验证；未完成项明确保留，不借旧2001合同冒称新功能通过 |

用户体验候选14/PID9460及`human-review-02/data`保留，不自动关闭或重建。旧agent已冻结；本轮仅以上三agent可写分配文件，所有构建/GUI由root串行。主题及配方共享存储由recipe_core和root先明确接口。

| 单元 | 所属阶段 / 所有者 | 范围 | 状态 / 出口 |
| --- | --- | --- | --- |
| R5纠偏：桌面结果/目标菜单/集成 | R5步骤2～3 / root | PosterVisualWindow各partial、App probes、scripts及全部Docs；串行构建/同包验证 | 用户2026-09-27明确“ok，请开始吧”；IN PROGRESS，未宣称R5通过 |
| R5纠偏：原生Item右键路由 | R5步骤2 / r1_contract_audit实现，已交root | PosterInputIslandManager、PosterInputRouter、新PosterContentContextRequest及对应router测试；App消费归root | 已冻结；无左键capture/激活副作用，按session/当前命中见证拒绝旧请求；生产handler通过不代表系统右键通过 |
| R5纠偏：影响描述/整图probe/参数基础 | R5步骤3～4 / r2_portable_audit实现，已交root | ContentInteractionFeedback、100项构图probe、TemplateParameterSchema/BindingAdapter及15项合同；无新solver/数据库权威 | 已冻结；候选11对应合同、实际缩窄/承接/撤销/重启及参数门通过 |
| R5纠偏：原生UI C工作区 | R5步骤4 / r2_review实现，已交root | ControlCenterWindow.xaml与全部ControlCenterWindow.*.cs（含中心probes）；root后续页签/配方入口/集成 | 两页、纸样片、行为、单标题与声明参数已集成；五态截图通过；后续配方三入口只读复审未发现阻塞，弹窗实操仍待验 |
| R5 UI/意图对齐审查 | R5 / root；三子agent复核 | E1六任务/后续确认、UI C、DEC到源码/证据；root编辑Docs | 追溯和生产纠偏已实施，当前证据归§10及对齐记录§7，不再沿用早期“待实施”状态 |
| R5补充原生菜单修复 | R5步骤2/5 / r1_contract_audit实现，已交root | DesktopMenuInteraction.cs、NativeDesktopMenuPlatform.cs及DesktopMenuInteractionTests；其他Windows文件无新授权 | 已冻结；candidate14两步仅排自身窗口、严格原生层级读回通过，32定向/2001全量通过；完整命中因遮挡未验，见对齐记录§7.3 |
| R1合同收口 | R1 / root，r1_contract_audit只读复核 | DEC、兼容能力矩阵、草稿仲裁、默认值、BM/CA/CB/CC到正式门映射；全部Docs由root编辑 | DESIGN CONTRACT READY；不表示代码实现 |
| 基线与工具链 | R2 / root | 独立源码/未跟踪材料快照、恢复核验、SDK和依赖、统一测试/构建 | BASELINE READY，见§3；不是原生验收 |
| BUG-014保真 | R2 / r2_portable_audit实现，root运行，r2_review独立审查 | Portable codec、非空留白/非法载荷/真实SQLite回归、ReservedSpace空Name验证 | FIXED / CODE+AUTO；25/25、全1664/1664；无阻塞审查发现 |
| R3 A/B统一几何 | R3 / root实现，r1_contract_audit测试/只读审查 | ContentLayout.cs、ContentLayoutSolver.cs及Solver/Stress测试；纯派生空间状态、轴归一化占用，不改持久schema | 完成并交回root；原实现agent传输中断前未落源码，不将其草稿当交付 |
| R3 C浏览 | R3 / r2_portable_audit实现，root接App | ContentLayoutFlow.cs、Flow测试及独立浏览模型文件；有限/循环边界、tile与锚 | 完成并交回root；App按完整度量键重新求解，按实际tile捕获/恢复锚 |
| UI C中心原生样板 | R3 / r2_review实现，root修复/观测/App接入 | ControlCenterWindow.xaml/.xaml.cs/.UiCSample.cs；root持有App.UiCSampleProbe、隔离启动脚本 | 四状态真实XAML/Canvas观测完成；未验鼠标/滚轮/键盘或桌面合成像素 |
| 手动投影用例 | R3 D / root实现，r2_review测试 | 新ContentManualProjectionTests.cs；通过同一Solver API验证文档算例 | 17例，先11失败后全部通过；保原图、普通留白和读序 |
| 被动受限资格 | R3 E / r2_portable_audit | 新ContentSpacePolicy.cs及独立测试；原因/有效环境见证/安全恢复 | 23例通过，纯模型完成；原生输入/环境适配接线归R6 |
| R4编辑/返回意图 | R4 / r1_contract_audit实现及独立事务回归，root集成 | ContentLayout/Editing/Solver/FlowPolicy；schema3完整自动返回态、尺寸来源、读序图钉、手动整图承接和局部推让 | 已移交root，合同及真实事务回归通过 |
| R4逐操作兼容 | R4 / r2_portable_audit实现/只读复审，root命令校验 | ContentLayoutCapabilities、Portable v4；已知只读布局可保真存档，不可静默实例化 | 已移交root；源/目标双侧、SQLite回归通过 |
| R4代表模板 | R4 / r2_review，root负责已有App挂接 | LampTemplateParameters/参数schema3、新Scene/TemplateParameters partial；六参数单海报绑定 | 静态深夜书桌原生六参数/隔离/取消通过；指针跟随和最终视觉仍待R5～R7 |
| R4草稿/事务接线 | R4 / root，r2_portable_audit只读审查 | ControlCenterSettingsSession、中心/桌面实际操作入口、CommandPolicy和生命周期 | 合同及原生控件草稿/清理通过；真实鼠标/键盘仍待R5 |

root负责App/Application其他文件、基础设施/脚本、集成、所有阶段状态。子agent新增需求、公共数据结构或其他文件修改必须先同步；同一个用户Item在不同海报的覆盖/历史必须隔离。子agent“完成”消息是交接，不自动等于里程碑通过。

## 3. R2基线与环境证据

- 起点：分支`impl/poster-redesign-V3-wb`，HEAD `bd148fb4641bccb18d75777ae6645149e3ff9cfc`；此前12份跟踪文档修改、组合核对新文件及6个未跟踪目录均保留。
- 原始材料快照：`out/lr-r2-baseline-20260926-203107/`。`workspace-materials.zip`含552文件/10,474,210字节，527个tracked文件全部覆盖；在`restore-verification/`解压后逐项SHA256相同。archive SHA256=`7e64ff3755154b7666b1f23224e5eb4ecdb974d790259a64c8666d6028f8ab92`。
- Git只读备份：同目录`repository.bundle`通过`git bundle verify`，保存全部refs；另存索引/工作区binary patch、原始状态及文件清单。bundle SHA256=`359d6f281eef3fd0fa33500ccd9d9bc49fa85c141314cd4e17dec1a22b46300a`。未改branch/HEAD/index，未提交。
- `node_modules`、`.pnpm-store`、`src-tauri/target`依赖/构建缓存原位保留，不含于恢复包；新`out`工具/证据区排除。`.claude`、`Docs/C_Workstreams`、`dist`、src-tauri/gen及已有`.desktop-up`日志已随材料复制。不能称作整盘或缓存的完整灾备。
- 当时没有`%LOCALAPPDATA%/DesktopUp/V3`数据目录、没有DesktopUp进程、没有旧out候选包；不伪造旧库/包备份。保真验证使用人工建立的隔离配置/数据库副本。
- 系统只有SDK10.0.301；保留`global.json`10.0.103/latestPatch，通过[微软发布清单](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json)选兼容10.0.112，官方zip的SHA512核对后安装至`out/toolchains/dotnet-10.0.112`，`dotnet --version`输出10.0.112。未改系统SDK或仓库版本。官方清单、选择、压缩包保存在`out/lr-tools/`。
- CLI home/NuGet packages/http cache均设到out；还原日志`out/lr-r2-evidence/restore.log`。首次SDK启动输出生成开发证书提示，未执行trust；后续禁用自动证书生成。恢复、红绿测试、完整构建结果持续登记于[VERIFICATION](VERIFICATION.md)。
- BUG-014对照：新增10例先全部失败（原15例通过），修复后25/25通过；真实SQLite追加/替换各一revision、请求重放无重复写、store销毁重开后完整字段保持。完整合同1664/1664、0跳过，Release解决方案构建0警告/0错误；独立审查无阻塞项。日志/TRX均在`out/lr-r2-evidence/`；store重开不是App进程重启或断电认证。
- 独立R2构建副本`out/lr-r2-evidence/app/`共73文件/49,451,534字节；`app-manifest.json`、`source-manifest.json`（355源码/配置文件）和`workspace-at-r2.patch`绑定该次源码。App.exe SHA256=`99bbe7cfae81e1505c22953e30c88af827c3963e9a0c8bd8f7426e81da20fd20`；App.dll=`2b60087e043bc35856cbf4cbaa389766ac12d05f8a296754a6317d36eac496fb`；Application.dll=`a61e9e5944c958f9b45de97a097a5fc47df2ccda47c1f05b230ff9ca975a2bea`。未启动，不替换旧候选manifest或R7同包门。

恢复核验只在新目录进行。需要恢复时先保全新的工作：在独立目录clone `repository.bundle`并checkout上述HEAD，再按归档覆盖当前材料并核对`files.json`；索引差异用所存index patch重建。不要把恢复脚本直接应用于仍有新改动的当前工作树，不删除原始目录。

## 4. R1冻结的首批兼容能力矩阵

此为Q1保真原则的实施范围，R4须逐项证明后才能开放能力；没有一项仅因下表成文变成现版PASS。

| 组合 | 首批能力与保真边界 |
| --- | --- |
| 新两模式、普通单区域、无旧硬约束 | 支持全链编辑/取消/撤销/保存/重启及导入导出；自动图钉/尺寸独立，手动保位置/普通留白 |
| 已知v1/v2普通Automatic、合法PinnedIndex的Anchored | 保留来源/全部字段；仅在无损解释及同一意图映射已证明后开放对应两模式能力，不删除Anchored或误当坐标锁 |
| 旧Manual普通坐标 | 保留原构图/普通留白，可证明无损的同模式操作允许；原自动图钉/尺寸来源无法恢复时，相关模式转换拒绝，不编造历史覆盖 |
| FixedPosition | 当前约束合法可原样呈现；改变硬位置含义/跨轴越界的操作拒绝，不将其自动改名成图钉，不抬高新布局统一最小值 |
| 多区域 | 全部区域身份、顺序、偏移、宽度及Item归属原样保留；只有等价性证明后放行适配/轴变换，否则拒绝受影响操作 |
| Explicit ReservedSpaces | 保留位置/跨度/Kind/Name并参与碰撞；不裁小/删除，不等同普通空白；不支持创建编辑器不等于能丢字段 |
| Decorative ReservedSpaces | 无损保存，不参与碰撞，不阻止落点 |
| 未知schema/engine/必需字段、重复字段/损坏载荷 | 保原文与明确原因，拒绝相关写入，不用默认布局覆盖；未知格式不假称可移植导入 |
| 旧游戏单击/EDITING-USING预设 | 保留来源，不静默套新手势；真实Item阶段作显式映射，当前不开放未经解释的激活能力 |

“支持”按具体操作能力判定，不是见到旧字段就永久禁用整张海报。无法证明影响范围时拒绝整个内容布局写入，安全观察/无损备份仍可用；若无法保证无损导出则明确拒绝。几何冲突不冒充DEC-022的被动C<M；无法合法展示时在既有管理入口说明兼容原因/保原文。显式转换另议，不在本轮默认执行。

Q2冻结同海报聚合单所有者：当前有效草稿保留；冲突操作先完成/取消，不排队/隐式合并/自动保存。正常调节结束一次提交，中心打开/焦点不长期占锁；关闭中心只取消自己的未提交内容；提交阶段由回执决定结果；旧输入/环境代次失效不重放。

默认冻结：新建纵向有限，循环关；方向与循环独立，切横向不自动开启Loop；旧显式值保持。空/短/恰一屏时offset归零、不补副本，Loop偏好不清除，内容增长后重新判资格。实际容差和接缝算法归R3/R6工程证明。

## 5. R3出口与下一节点

R3的A～E纯模型及最小原生中心样板出口已满足，按主计划进入R4。新持久schema、模式覆盖/旧坐标映射、当前空间编辑权限、草稿仲裁、逐操作兼容与真实SQLite事务归R4；代表性模板效果、绑定/参数及资源清理仍需实测。R3不提供整套原生交互、配置全链或产品验收PASS。

新增纯派生合同：Snapshot报告Normal / InsufficientCrossAxis / LegacyConstraintConflict及C/M；不足不自行批准临时二维，旧硬约束冲突不冒充物理不足。保留既有Resolve/Reclip和schema；canonical authoring按轴对称验证并拒绝非Normal结果。按DEC-011，最小边界单列/单行一项，作者跨度不缩。公共字段先协商再实现，全部构建由root串行运行。

## 6. R3证据与后续约束

- A/B/C首轮红测：Solver+Flow共65项，45通过/20失败；新增8项几何与12项浏览失败均已实际复现，`out/lr-r3-evidence/abc-red/`保留日志/TRX。Flow的新NaN/Infinity三例中原负Infinity已通过，不把13条新增浏览输入全部算失败。D红测17项中11失败，见`d-red/`。
- UI C中心样板保留全部55个原具名控件/事件和4个Pivot索引；复用真模板选择/尺度/疏密/目标/定位。折叠模板域只是视图状态，灯/雨与两模式实际手势仍未实现。`out/lr-r3-evidence/ui-compile/build.log`仅为App `--no-dependencies`静态编译成功，不是全量集成包或原生验证。
- 隔离观测入口`--ui-c-sample-probe`及`scripts/probe-ui-c-sample.ps1`：绑定新app/fixture输出，独立新DataRoot，四种宽窄/折叠的真实RenderTargetBitmap和Canvas/input/clip读取，逐步核对revision/配置指纹不变。DIP转原生像素并断言Compact，避免高DPI假覆盖。已执行成功，原生鼠标/键盘/滚轮、桌面GPU合成和UI C产品验收仍明确为false。
- SessionFixture不在solution中，另行离线还原。前两次因NuGet读取用户配置权限失败，日志保留；授权提升后从项目内既有包缓存和空离线源还原成功，`fixture-restore-authorized/restore.log`。未改用户NuGet配置或安装系统运行时。
- R3时遗留：`ValidateAuthoring`拒绝派生只读结果，Portable/Profile/Clone当时复用写入校验而阻止部分已知旧配置无损存档。**后续R4已以`ContentLayoutCapabilities.Inspect`区分存档与实例化/编辑，并有ArchiveCapability合同**；不再列为当前未实现。未知格式和R5配方/中心主题的Portable能力边界另见收口审计，不放松写入门。
- App最小安全接线提前消费新的只读状态，并将height加入几何失效键；暂不授予受限双轴，不把普通缩高称为物理环境事件。完整交互/恢复仍归R5/R6。

- 集成：`abcde-integration/`为241/243；修复实际C与配置容量混淆、更新超64交叉轴的旧反例、补硬位置/全局图钉/Reclip见证回归后，`abcde-reviewed/`248/248。完整`full/full.trx`为1758通过/0失败/0跳过。`integrated-build/build.log`及`fixture-build/build.log`均0警告/0错误。只读复审未发现新的纯模型阻塞项。
- 原生前置与失败保留：`candidate1`为普通框架依赖包；本机仅CBS 1.6/2.2，没有所需2.4，bootstrap返回`0x80670016`；`ui-native-01/02`启动对话框停留，无项目日志，尝试正常关闭未退出后仅终止确认为本次新建的失败进程。`candidate2-app-local`由命令行`WindowsAppSDKSelfContained=true`内置Windows App SDK；沙箱下`ui-native-03`退出`0xC000027B`，正常桌面权限的`ui-native-04`暴露根Grid局部背景资源引用过早。移动背景到资源定义后的子Border，保留全部失败记录。
- 最终样板：`candidate3-app-local/`绑定363源码文件；`app-local-build-ui2.log`0警告/0错误。没有安装系统运行时，也未改csproj默认部署；此包仍依赖.NET10。source-manifest SHA256=`902ec83328086e53c4423508e31844da69f68989edf528d88414e09288b2971a`，App.exe=`21d75ea7a25074731fde8a79dcac47648f5e6544353a6f909f4f18ded1705e4f`，App.dll=`f9356e6b55fd6f8b09c6d6e1a03aa2c373abf5167d682aeb00759d04997aba58`；含全产物manifest及工作区patch。最后一次源码改变仅为上述XAML背景，Domain/Application/全部测试与1758通过版本一致。
- `ui-native-05/observation.json`：PID36976，exit0；revision始终1、配置指纹不变、产物hash前后一致、error0、退出activeResources0；四张PNG均实际查看。中心客户区964×681和664×781、模板域展开/折叠，水平边界正确。海报6成员、当帧4可见/命中，Canvas/Input/Clip一致；末项当帧不可见，未滚动到末尾，不宣称已验证原生全部可达。展开状态下下方参数需要现有ScrollViewer，尚未进行真实滚轮验证。仅浅色截图，深色/高对比/实际高DPI未测。
- R3时遗留：编辑/热度提案当时主要依赖Resolve抛异常。**后续R4/R5已补`ContentLayoutEditing.Apply`只读门和App `RequireWritableContentProjection`，R5 rejected门验证非法有效投影零写入及重启**；来源能力也按R4检查。R6仍需真正接被动资格/双轴导航，不将安全拒绝等同完整受限恢复。

## 7. R4实施记录（SAVE CONTRACT + SAMPLE READY）

- 格式变更：内容schema3为手动模式保存`SavedAutomaticMode`和全成员`SavedAutomaticState`；尺寸覆盖以`AutomaticSizeBasis`保留原自动跨度。进入手动捕获当前完整呈现，返回信息只取作者意图；退出A恢复该信息。旧手动布局没有可证明来源时拒绝模式转换，旧人工尺寸没有来源时拒绝恢复自动尺寸，不猜默认4×4。旧Automatic中的休眠pin不随升级隐式生效。
- Portable v4完整映射新字段；旧v1～3不得夹带schema3。能力检查区分“可无损存档”和“可实例化/编辑”；已知可呈现Legacy冲突可备份导出，Planner及执行重规划仍拒绝应用，未知/非法载荷拒绝。未改SQLite schema11或历史迁移指纹。
- 草稿由既有session/input owner仲裁。同海报第二操作不自动保存/覆盖/排队；非法输入保留且可取消。中心关闭/切目标不再取消桌面手势；中心焦点本身不占所有权。菜单、标题、外框、收展、内容提交分别接入保护；Esc取消中心当前草稿不写revision。此处为代码状态，尚无NativeMouse/Keyboard证明。
- 深夜书桌为独立`lamp`，不重命名night-ops。六参数为灯光颜色、柔和度、强度、底色、卡片色、背景不透明度；写入既有presentation schema3 `templates.lamp`。静态灯具/光束/光池/卡片渐变均为非命中装饰；透明度只作用背景。纯Lamp更新保持卡片和布局几何；指针跟随和完整视觉验收归R5/R6/R7，不把静态样板宣称为完整C效果。
- 已执行证据均在`out/lr-r4-evidence/`：`editing-red`13项中12失败；`archive-red`13项失败（部分旧负例在构造阶段失败，随后改为合法基线再注入非法值）；`ownership-red`11项中4失败，`ownership-green`68/68；`transition-red`3项中2失败；`lamp-red`18项失败（负例初期有构造NRE，后补有效源断言）；第一次`editing-archive-transition-green`71/71。
- `integration-red`全1844项，1829通过/15失败：包括三条新增命令绕过反例及被DEC-024/退出A替代的旧测试预期，正在分别修复；不删除失败覆盖。`app-integration-first`因两个C#集成错误失败并引发后续XAML编译错误；修复控件名/局部变量后`app-integration-second`解决方案Release构建0警告/0错误。此构建还不是最终候选或R4验收。
- 完整回归最终为`contracts-reviewed-final` 1868/1868、0跳过；兼容字段来源、休眠pin语义、手动尺寸返回态、真实Undo/Redo与热度尺寸开关反例均保留。配置逐字段映射/能力/证据见[保真报告](LR_R4_CONFIGURATION_FIDELITY.md)。
- 原生`lamp-native-01`在步骤10失败：颜色TextChanged晚于同帧其他控件/保存，非法输入未及时进入session，导致Intensity91合法旧草稿提交。隔离库revision4→5，失败包/日志保留。改为同步TextChanging；`lamp-native-02`通过，随后补取消清理滑杆令牌和无草稿颜色失焦no-op。
- 最终`candidate3-app-local`：376源码/401产物记录，App.dll SHA256=`DFCE3BCF4FB22147FADD4D3C7C2A1D15C9E5AA171E977A37D99A64AAEAB38716`，构建0警告/0错误。`lamp-native-03/observation.json` PID19140/exit0/产物不变/错误0/退出资源0；六参数、零光照/背景alpha0、取消、非法草稿与第二入口冲突、模板往返、双海报隔离、中心关开、取消后迟到回调均通过。11条状态观测，revision2→8仅六次有效操作；6张PNG均实际查看。
- 该门证明原生控件/状态/对象绑定：Canvas、卡片身份、几何/命中/clip未因纯Lamp参数改变。不是NativeMouse/Keyboard/Wheel、人工作业、App进程重启或桌面SystemBackdrop像素验收。XAML截图光池有亮黄环状边缘，记录为待核对视觉问题；R5/R7须辨明捕获与实际合成效果，不能把现图当UI C最终视觉通过。
- R4出口为**SAVE CONTRACT + SAMPLE READY**；报告列明store重开与进程重启的区别。后者及配置完整链继续在R5/R7同包验证。R5开始核心界面/首次直接操作；R6～R8未实施。


## 8. R5当前实施单元

- 目标：BM-01～11、PD基础直接操作/MC承接；纵向有限优先，保留横向/循环配置但复杂组合门归R6。根agent持有App、Windows、文档及唯一构建队列。
- 首按复用现有5物理像素防抖和native capture；移除整理前置，冻结完整按下画面/viewport，自动落到最终读序图钉，手动局部让位。无效松手不得提交上一次合法候选或触发打开。
- 单击只选择，双击同有效身份才打开；Enter仅在已有真实焦点的内容键盘操作区路由并抑制repeat，不全局监听或抢前台。宿主NOACTIVATE保持，未承诺无焦点桌面Enter。
- 中心选择不得取消桌面草稿；纯选择/辅助展示不写布局。UI C整理按钮退役、模式/方向/当前Item细调与独立图钉反馈接入。协议/字段提交及同库进程重启已由候选3证明；系统首次拖动、取消及早期人工仍未验收。

- R5 实现与逐包证据见[核心交互记录](LR_R5_CORE_INTERACTION.md)。Application 激活模型及协议 probe 由 r1_contract_audit 交回 root；Domain 拖动策略/测试由 r2_portable_audit 交回；中心字段/UI/probe 由 r2_review 交回；所有构建和文档仍由 root 负责。
- 复审修复当前归属：r2_portable_audit 仅负责 `PosterVisualWindow.ContentProposals.cs`、`PosterSpatialRuntime.Content.cs` 和独立 proposal probe；root 持其余生产/集成/probe 文件。Native driver 已补键盘目标所有权并交回 root，无实际输入执行。

## 9. R6 接线准备（R5核心已收口，实现尚未开始）

2026-09-28更新：保留本节标题/锚点及以下实施前地图用于追溯；第2/3项第一批、第5项2A及第4/5项2B核心已接线并取得合同/托管App证据，详见[R6记录§6](LR_R6_CONTINUOUS_OPERATIONS.md#6-第二批2b被动空间不足与恢复2026-09-28)。最近下一步为第三批第6/7项及完整组合；真实系统显示/DPI、原生输入与像素尚未验收，不能把分批结果算为R6出口通过。

以下地图已按候选25复核，DEC-029授权接续，R5核心前置已满足；不把定位或已有模型当R6实现完成。当前所有子agent文件交回root，后续修改重新分配，构建/GUI串行。

1. 统一浏览发布：候选25已由`ContentWheelScrollPolicy`去除循环前非负夹紧/逐事件取整，保留小增量；12场景102事件通过，不重复排期。继续复用`NormalizeOffsets/ProjectTiles/CaptureAnchor/RestoreAnchor`，让`Reclip`显式携带`ContentBrowseMode`以保留受限交叉轴offset。停顿续滚BUG-026按用户决定延期，不与已修机制混算。
2. 循环拖动：在现冻结 snapshot/viewport/实际 tile/capture 上增加按下内点、相位及有界未折返导航位移；仍输出单身份读序/合法 canonical 坐标，不保存副本周期。先写 CA-01 的 extent980/view300/period1000/offset950、副本70→95/抓点10反例，再替换当前拒绝 active loop 的门。
3. 边缘导航及结束：`FlushContentMotion` 改用累计导航位移，显示走统一 tile；提交/取消/无效释放从实际抓取项交接浏览锚，不能默认换成首个可见项。验证 CA-01/02/07、PD-10、两轴和零位移。
4. 被动资格：App 尚未消费 `ContentSpacePolicy`。经现 `PrepareSpatialFrame/CommitSpatialFrames` 携带同代 browse mode/空间见证，previous 只取最后成功发布态；环境使空间不足时先取消旧 capture/草稿，恢复读取最新作者和成员。按 DEC-022 验 CA-03～06、OS-02/05/06/08/10。
5. 主动最小值：现外框主要用宿主最小尺寸，须把最大完整 Item 的 `MinimumCrossAxisDip`＋chrome 传原空间 owner；不能主动缩小后只读来替代 DEC-011。合格受限态才开有限双轴与明确导航，普通/旧约束冲突不得冒充物理环境不足。
6. 资源/假源：资产 overscan 目前按 canonical 纵向矩形，需统一 tile 可见集按 ItemId 去重，保留现缓存预算。沿 `IContentItemViewSource/IContentAssetProvider/ContentOpenIntent` 接可替换假源，成员增删走现正式事务；覆盖迟到、失效恢复、同源多海报隔离、重复激活。
7. 观测器：旧`ContentSmoke.ReadContentSurfaceForSmoke`仍按canonical ItemId取卡片；候选25新`App.ContentBrowseProbe`已按TileKey，Transition按tile读回。复用新浏览见证补接缝拖动/受限/像素场景，保留副本有界/单身份/同代次；旧finite门不能替代这些组合。

这些入口来自当前代码只读审查，未构建或验证R6实现；没有发现需要用户重新选择产品方向的问题。首单元为第2/3项冻结抓点、累计导航及结束锚，随后第4/5项空间准入恢复，再接资源/假源及完整连续验证；具体反例与验证范围见收口报告§4。

历史顺序修正：用户此前指出R5 UI/意图偏离，曾要求先按§4.2.1纠偏；现R5已有收口记录，该等待已解除。不重评已定规则、不把R6的06c倒塞进R5；本次仍只完成文档/只读复核，无R6代码改动。

## 10. R5用户截图后的纠偏交接（2026-09-27）

§8～9末尾的旧任务分配/未实施说明为历史。本轮已实际浏览C展开样片夹和参数，与两张用户截图及原生图对照：锁定/收展归行为，保留一套原生标题，纸页签/卡片/画面字/胶带与三款真实样片接入，海报菜单移除中心设置。声明参数沿受控能力生成六控件，默认与实例隔离；真实模板册、只读配方、本地AI说明有入口。完整导入/热装入、雨夜/跟随动效、在线AI仍未交付。

候选11的源码/产物绑定、1984全量合同、六组UI/字段/协议/非法预览/Lamp/100项构图及重启见[对齐记录§7](LR_R5_UI_C_INTENT_ALIGNMENT.md#7-用户截图反馈后的实际c核对与第二轮纠偏)与[VERIFICATION](VERIFICATION.md)。几何动画错误及窄提示裁切已修，失败材料保留。补充native菜单未过：候选12仅加诊断后确认本线程非topmost Popup排序返回error5，安全关闭/恢复，不能当普通遮挡。随后仅自身窗口的两步排列已集成candidate14，32针对/2001全量及六组同包通过；原生排序读回/四命令正确，完整命中因遮挡未过。当前事实见对齐记录§7.3，不让旧PASS替代出口。

R5保持IN PROGRESS，R6未开始；用户此前“人工体验通过”只属于旧基础组。本次新的必要产品核对包括样片夹/行为/单标题、参数与三个弹窗、关闭中心后拖动预告/取消/撤销；系统键鼠/菜单和配置可靠性出口分别记录。root持所有Docs/集成/验证；每次新候选不覆盖旧包或旧数据，不Git提交/推送。

## 11. R7配置保真与可靠性启动（2026-10-08）

当前：**R7-A/B实现及自动门完成，整体R7未关闭**。R7-0历史基线见§11.4；本轮便携v5合同与同包final04证据归§11.5～11.6，2396合同/4门/14个应用进程通过，下一步R7-C长时资源与连续可靠性，再R7-D人工/真实环境。依据用户“标题显隐添加之后就可以进入R7”，不以云效果未逐项反馈阻塞当前阶段，也不替其填写PASS。root负责本轮，无Git提交推送、不操作日常库、不搬移真实文件。

### 11.1 已核对的差距

以下为R7启动时盘点；019/021的后续修复及完整自动门以§11.4为准，便携缺口仍未实施。

1. PortableProfileCodec.Version=4，便携参数仅接收presentation v1/v2；Export会拒绝模板目录、非light中心主题、模板实例，标题色v5与显隐v6也拒绝。这是明确功能缺口，不以“拒绝时没丢数据”冒充完整导出。名称、布局/自动手动/图钉及两轴等已有便携合同需保留。
2. 当前数据库保存和普通重启具备新设置路径；它们不等于跨机导入、版本兼容、覆盖/追加冲突、备份故障和损坏恢复均通过。新便携版本须与内容schema、presentation版本、声明renderer版本分别验证，不能混为单一版本号。
3. BUG-019并发首次初始化、BUG-020原生菜单命中余项、BUG-021窗口区域瞬时读失败、BUG-022空白桌面手势仍有证据缺口；Rain §15的同类窗口区域瞬态失败及资源分配离群样本原样携带。重试通过不能关闭它们。

   本轮title-visibility/final01/R4新增一个可定位样本：handoff-reuse的SurfacePreparation发生窗口240×480、可见区域240×60，native边界/input成功，GetWindowRgn=0。PosterVisualWindow.ApplyVisualRegion在缓存requested相同时直接返回true，SurfacePreparation未强制重装；缓存是否在原生容量变化后失效是**待证假设**，不是已确认因果。不跳过InspectVisualSurface、不把Region=null当成功；后续应比较容量变更/同尺寸复用/强制FinalRestore和消息回调时间线，再决定修复。上一版及本版独立8例都通过，原失败及两次对照仍保留。
4. R6-CARRY-01～04：真实显示/DPI/工作区、不足冷启动、原生输入生命周期、桌面合成像素仍需最终候选范围证据。BUG-026、031与OPT-MOTION-02按原延期，先评估核心承诺影响，不擅自重开所有动效优化。

### 11.2 近期工作包与出口

| 顺序 | 具体工作 | 通过条件 |
| --- | --- | --- |
| R7-0 可重复可靠基线（AUTO COMPLETE） | 019锁时序反例/Private cache，021原生区域丢失注入/边界重装，探针生命周期分离；见§11.4 | candidate02/final02完整16门/2362合同/42进程通过；未把工程基线等同完整R7或原生长时认证 |
| R7-A 配置合同与反例（CODE/AUTO COMPLETE） | 逐字段列出本地保存/副本/备份/便携/跨机含义；v5携带中心主题、模板目录、逐海报实例、标题色/显隐及内容布局；身份与机器专属字段分离 | §11.6先红后绿、旧v1～4不改义、缺模块不执行、确定性副本/原文保留通过；未知必需格式拒绝 |
| R7-B 接原持久化与恢复通道（CODE/AUTO COMPLETE） | 原codec/planner/事务及中心通道；追加/替换、模板碰撞、取消/过期、SQL/文件失败、零海报和待安置 | final04同包2396合同、4门/14应用进程；单权威/单提交、重启/再编辑及物理缺模块往返通过。真实选择器与跨机人工仍归D |
| R7-C 可靠性（C1 AUTO COMPLETE，见§11.9；C2已转交） | 区域副本读回路径修订；final09完整2424合同、0/1/3/6张20轮、17进程及R4 12案例通过 | 旧final05失败保留；不宣称OS内部原因或长时认证，C2 NOT RUN；当前进入D人工 |
| R7-D 最终候选与人工 | 固定源码/产物/验证manifest；补真实系统环境、未覆盖原生手势、桌面像素与云增量/标题开关的人工范围 | 支持范围、通过/未验/延期逐项对应；阻断核心链的问题不放行；满足出口再R8，而非测试数够多就收口 |

启动时先实施R7-0，随后完成R7-A/B自动范围（§11.6）和C1（§11.9）。最近下一步为R7-D人工，原C2移至基础功能完成后、不再阻塞R7阶段收口；其他出口仍须完成。不重选UI、不扩成模板商店，AI配方不变成任意脚本/DLL执行。同ID异定义按DEC-032保留两份，详见§11.5；无界重试、放宽读回和静默覆盖仍禁止。

### 11.3 配置矩阵起点

- 必须保真：名称/副标题、显隐/锁定/收展/自动延迟、保存尺寸和位置意图；两轴/循环、手动构图/留白、自动图钉/尺寸覆盖/模式恢复、辅助显示；尺度/疏密、标题色/显隐；模板声明、参数、未选模板实例；中心主题和全局动效设置。
- 保留但不可执行：未来/缺失模块配置、未知配方声明；严禁通过宽松JSON读取后删字段“修复”。不能安全携带的值须明确拒绝且原库不变。
- 不复制机器权限和运行态：HWND/会话/revision/捕获/动画位置、原机进程ID、自动启动授权、实际文件移动；跨机位置走现有拓扑映射与待安置策略，不推动本机已有海报。
- 既有完整数据库备份与跨机便携仍是不同操作；备份不替代新便携交付，便携也不替代原库灾难恢复。

### 11.4 R7-0可靠基线实施（2026-10-08）

本轮只修公共可靠性边界，未改浏览/拖动产品意图，也未把便携版本提前升级。实施前的63份已改源码/文档/未跟踪实现和tracked.patch保存在`out/r7-baseline/before-20261008`；不含`.workbuddy`、日常配置或Git写操作。

**BUG-019：证据与修复**

- 两份Store各有实例锁，但读写连接原先都使用Shared cache＋WAL。读取多张表的快照与首次写入交错时，共享缓存表锁阻塞，并非CAS冲突。固定时序测试分别保持未提交seed和已读catalog快照；无sleep、无碰运气循环，旧实现两项均在5秒有界等待后报SQLite Error6。`red019/bug019-red.trx`保留。
- 改用Private cache，保留WAL、完整快照隔离、写事务/单revision、原5秒等待和CAS；没有增加重试、全局进程锁或read_uncommitted。Microsoft明确不推荐Shared cache与WAL混用；[连接串说明](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/connection-strings#basic)和[SQLite表锁说明](https://www.sqlite.org/sharedcache.html#table_level_locking)支持机制分析。
- 新反例验证：写入未提交时读到旧快照；写者提交后已有读快照不混revision；SQL回滚不暴露半数据/不消耗operation；32轮独立新库双Store竞争每库仅一个seed/一个revision。原ConcurrentFirstRun测试保留。25项初步绿门后，新增四项纳入全量2362项通过。
- 范围：数据库已初始化后的并发首次seed/正常快照读写，不宣称两个进程同时执行首次schema迁移或断电恢复已认证。

**BUG-021：证据与修复**

- 在真实WinUI窗口的native batch之后、裁剪提交之前，注入清除自有HWND区域。旧实现确实出现`regionApplied=true`但`GetWindowRgn=0`，窗口240×480、逻辑可见240×60，和原final01失败边界相同。`red021b/data` PID6848保留；原八场景先通过，第九场景失败，恢复退出资源0。
- `red021/data` PID40780先暴露探针自身将IntPtr写入JSON的错误；改为记录宽高后才得到上述有效反例。该轮不是产品根因证据，不删除、不包装成产品失败。
- `SurfacePreparation`（含复用/增长交接）和`FinalRestore`现在都在整批原生位置提交后重装一次HRGN；正常Animation仍可复用同尺寸缓存。底层安装失败清空缓存。原起点校验、window/client/clip严格读回、共享失败恢复和事务守卫保留，没有读回重试，也不把null区域当成功。
- R4增加`prepare-region-reset`、`handoff-reset-reuse`、`handoff-reset-grow`，必须11个不同案例及3次实际注入全部通过。三轮同候选分别PID21580/37036/36996，正常退出、资源0。注入只操作本探针的视觉HWND，不注入鼠标、不操作Explorer、不写数据库。
- 因果边界：确认了“原生区域丢失后仍信任旧缓存”的应用缺口；未证明自然发生时究竟由哪条WinUI/系统消息清除区域，也不把这一修复当作GPU白条BUG-031已修好。若准备前起点已失效或后续真实读回失败，仍安全取消，不强行接管未知状态。

**固定候选及放行条件**

- `out/r7-baseline/candidate01`，App SHA256 `86CEFA3522D98994B1C33701252E72AE3B4B8FED2F160C42528103D695976873`；App/两夹具Release构建0警告错误。
- `contracts01/full.trx`2362/2362；`r4-1`～`r4-3`各11场景通过。candidate01的完整门`final01`在Recipes停止，没有封存。所有路径均位于`out/r7-baseline`。
- 更新：`final01`在Recipes的`verify-theme-system`停止（PID384），未封存。`center-theme-system.json`前Normal/PointerOver/Pressed均连接且颜色正确，Disabled时弹层关闭：旧探针同时禁用宿主色块锚点和弹层按钮，却要求弹层继续加载。现仅调整探针为先验证中心全部状态、再打开弹层验证其全部状态；不自动重开弹层、不跳过断连/颜色断言、不改产品交互。
- 新`candidate02` App SHA256 `29128899CC51BDBA174AEF019AEE75DCE22C04CB0CAB7FFAEC12E22D02D3E0D0`；`recipes02`实际控件及重启通过（PID38424/42932），七份主题观察各54条、断连/错误0。完整同包门在全新`final02`从头执行并封存：2362/2362合同、16门、42进程（含4缺模块变体）全部通过，error/退出资源0；538源码脚本和161运行产物前后指纹一致。它不是把旧失败改PASS。
- `final02/r4/result.json` PID21572的11案例含三次区域重置全部通过；最终全部日志未再出现`spatial.batch_readback_failed`或起点区域拒绝。严格人工入口`start-lr-rain-review.ps1 -EvidenceRoot out/r7-baseline/final02 -DataRoot out/r7-baseline/review02 -CheckOnly`通过，但未启动或创建人工配置。当前无DesktopUp残留进程。
- 复用`verify-rr06.ps1`，名称保持避免重做入口，seal Scope明确仅工程回归基线，不等于完整R7或人工验收。源码/脚本/产物运行期间不变；失败不拼接不同轮次证明。
- R7-0结束时的下一步是R7-A/B，现已推进至§11.6；本节保留对应包的历史可靠性证据，不将其16门认证直接转授新便携候选。R7-C/D资源长测、原生/显示环境及人工余门仍待完成。

### 11.5 R7-A/B完整配置往返合同

便携主版本升至5；presentation v1～6、content v1～3、recipe/renderer各自版本独立，不更改旧包语义。不是把SQLite原样搬机，也不建立第二个权威配置源。实施与证据见§11.6；本节定义合同，不替代人工认证。

| 配置域 | v5携带/应用边界 | 必须失败的反例 |
| --- | --- | --- |
| 海报意图 | 名称/副标题/用途、保存尺寸/锚点、显隐/锁定/收展/自动延迟/层序；新建本机Poster/ThemeInstance身份 | 取消、CAS过期、空间不足不得偷偷重置尺寸或移动原有海报 |
| 内容布局 | 两轴/循环、手动位置/留白、自动顺序图钉/尺寸覆盖、保留自动状态、辅助显示；沿用内容身份映射 | 模式切回丢旧自动设置、留白被清空、重复成员/非法跨度、受限临时投影被当原构图导出 |
| 通用外观 | 原presentation数据含scale/spacing、标题/符号色、showTitle；旧缺省仍显示 | false被当缺省丢失、旧v1～4包伪装携带新增必需字段、导入后改尺寸误删外观 |
| 每海报模板实例 | 当前及未选模板的声明与覆盖值完整携带；Lamp/Rain专属参数不转通用字段 | 切模板/重启后雨量、云尺寸、风、闪电间隔、灯色/不透明度或未选实例丢失 |
| 自定义模板册 | 携带已支持的声明式定义；实例的内嵌定义不因目录中同ID内容不同而被替换 | 同ID异定义静默覆盖本机模板/既有海报；未选用模板被遗漏；目录上限溢出半写 |
| 中心主题/全局设置 | 受既有“导入全局设置”选择控制；不勾选时保留本机主题/动效/性能。模板依赖独立处理，不靠勾选全局设置才能正确显示导入海报 | 不勾选却改本机主题，或为保留主题丢掉导入海报需要的模板 |
| 兼容/安全 | 已支持数据但缺模块时保留、只读、不执行；未知必需格式版本明确拒绝并保留原材料。旧v1～4无新字段时保留本机新增设置 | 缺模块静默降级并覆盖原参数、从未知JSON删除字段伪装成功、运行脚本/下载依赖 |

“完整”指包内已支持的用户配置语义一致，不要求原机器坐标、内部revision、操作receipt、HWND或导出字节一致。显式数据库备份维持独立灾难恢复职责；自动启动请求/权限和真实文件不迁移。未知外层版本拒绝不等于宣称支持其完整往返。

**用户已裁决：同ID异定义模板保留两份（DEC-032）。** 本机不变，导入模板另建副本并重映射导入海报引用；预览/提交使用同一冻结映射，CAS重检时不能生成另一组随机身份。完全相同声明可考虑复用，必须完整定义比较，不能只看名字或版本；模板实例内嵌定义保持其原语义。Replace当前含义是替换海报，不自动等同删除本机全部模板册。现RegisterTemplate仍严格拒绝重复ID；完整配置导入须在原planner中先完成映射，再以原单事务提交，不放宽普通模板注册的覆盖限制。

实现顺序：先固定碰撞合同/旧包兼容矩阵，再用包含未选实例和false开关的单库→导出→新库导入→重启→再次编辑测试打红；扩展纯codec与确定性planner；复用ExecutePortable完整读集、备份和单事务；最后补真实控制中心导出/导入与物理缺模块往返。中间阶段不把“库中保存正常”或“导出拒绝无损”计为完整便携PASS。

### 11.6 R7-A/B实施与验证（2026-10-08）

当前：CODE / AUTO COMPLETE，USER PENDING，整体R7未关闭。唯一证据out/r7-portable/final04，下一步R7-C；不接真实文件，不提交推送，不覆写日常库。开始前69份已有修改保存在out/r7-portable/before。

**版本与字段**

- v5要求显式settings.centerTheme和templateCatalog；旧v1～4没有这些字段时保持本机新增设置，伪装在旧版本下的新字段即便null也拒绝。presentation版本1～6与content版本1～3独立检验。
- 原presentation原文作为数据携带，含标题显隐false、颜色、宏观尺度/间距、Lamp旧字段、当前/未选配方声明及全部覆盖值。导入不通过重新序列化渲染投影来删除旧值或提前迁移配方。
- 包上限仍4 MiB、128张海报、JSON深度32；v5单海报presentation上限2 MiB，旧v1～4仍32 KiB；单配方64 KiB、模板册2 MiB/总64条继续受控。超限明确拒绝，不截断。
- 中心主题/全局动效/性能只由“导入全局设置”控制；模板册及模板依赖独立合并。启动权限、内部revision/收据、HWND、临时投影及真实文件不迁移。完整数据库备份仍独立。

**身份、冲突与原子性**

- 新增PortableTemplateMerge为纯计划组件，不另建权威状态。按完整声明比较，不只看名称/版本。复制ID由完整定义摘要与冻结本机占用集合确定，已有同定义副本复用；预览/提交CAS重算相同映射，警告也参与比较。
- 同ID异定义保留本机目录和已有海报；为导入另建副本。逐海报内嵌定义优先于来源模板册，若两者不同，两份均保留，不拿目录版本覆盖实际实例。原普通RegisterTemplate严格重复拒绝不变。
- 精确受信内置模板及已登记前驱仍复用内置身份；缺模块的孤立内嵌定义用安全导入身份收录，避免恢复模块后同名内置模板撞库。缺模块只读保留，不扫描/下载/加载脚本；合并时为缺失能力恢复预留目录容量，放不下则整次拒绝。
- 合并目录和海报创建/替换仍走ExecutePortable、完整读集与同一SQLite事务；不勾选全局设置也要校验settings版本。取消预览不写；过期、篡改映射、SQL任一阶段失败均不半写、不消耗失败收据。控制中心保留导入前备份、文件哈希和原生占位守卫。

**验证进度与失败留痕**

- red/red.trx：5项新反例在v4全部失败，原因均为现有导出拒绝；green01恢复5项通过。
- full01保留升级后30项旧合同夹具不匹配：旧版本测试错误携带v5字段、旧断言仍期待拒绝导出等。定向修订夹具明确生成真正旧包，没有删除安全断言。related02另保留一次测试定位错误，related03为203项全部通过。
- full02/full02.trx：2396/2396通过；覆盖完整模板、标题false、旧包兼容、单revision/通知、回放、重启后再次编辑、故障回滚、缺模块只读、容量、未知声明/字段及原内容布局/文件故障合同。
- candidate01新诊断代码将接口误当具体协调器导致编译失败；candidate02修正后App/两夹具Release构建0警告错误。portable02六进程（0/1/3张各往返与重启）通过，资源归零，原生选择器/鼠标/真实另一主机仍未验。
- final03：合同及完整portable六进程通过，缺Lamp便携导入在新夹具尚未完成Profile初始化时被NotReady正确拒绝；该轮无seal。修正夹具为原EnsureInitialized→显式删除种子形成零海报状态，不放宽生产守卫。
- candidate04/final04：App与两夹具Release构建0警告错误；2396/2396合同、Contracts/PortableFull/RemoveLamp/RemoveRain四门通过。0/1/3张各一次完整往返和正常重启，共6应用进程；两模块各4次原生生命周期，共8应用进程，合计14；所有进程error/退出资源0。每模块另有5段console检查：有/无模块导出逐字节一致→缺模块导入→重启只读→模块恢复可编辑，保留参数及目录语义。
- App SHA256为C7E7D302FC072751E8BBC75CA038140AF389844C31F2BCFD0122E94727D5290A；不能只凭App哈希代表所有依赖。544源码/脚本及161候选文件指纹前后不变，完整candidate副本/各门proof/processes与r7-portable.json封存。入口scripts/verify-r7-portable.ps1只认证R7-A/B自动范围，不代替R7-C/D或声称本轮重跑了R7-0的16门。
- 当前无DesktopUp残留，本轮未启动人工候选。审批服务一次只读请求超时后重试成功，没有降级校验或绕过授权。原失败/测试/构建包及用户既有修改保留，无Git操作。

**后续门**

1. R7-C：按§11.7先做C1必要短时连续可靠性；原C2长时资源已按DEC-033移交基础功能完成后的联合验证，不重开所有延期动效优化。
2. R7-D：固定最终候选后人工核对真实选择器、导入预览副本说明、勾选/不勾选全局设置、重新打开/再编辑，以及真实显示/DPI和空间不足；未验不补PASS。
3. 满足调整后的R7阶段出口并显式转交原C2未验项后，进入R8→V3-05非破坏性真实Item→账本安全门后的真实收纳→基础功能联合长测。RR-06保持关闭，BUG-031等延期边界不变，不把阶段收口当长期稳定认证。

### 11.7 R7-C长时资源与连续可靠性（2026-10-09）

本节为排期决定，实施状态以§11.8为准：C1已实施但六海报连续门FAIL，原C2 MOVED / NOT RUN。DEC-033将长测后置到基础功能（含真实收纳）完成后联合执行，责任归[总计划§14.1](../../30_Plans/DESKTOPUP_V2_V3_IMPLEMENTATION_PLAN_V1.md#141-基础功能完成后的联合长时验证原r7-c2)，复用V3-10-01/02/06，无需等待完整AI/主题平台。不因长测后置跳过C1发现的核心短时问题，当前R7未关闭。

| 排期 | 工作 | 证据/限制 |
| --- | --- | --- |
| R7-C1：AUTO COMPLETE | 核查019/021反例及020/022携带；同包20轮连续门、正常重启、生命周期注入、R4恢复与组隔离，结果见§11.9 | final09通过；不要求40分钟露出桌面，不把内部命令/模拟信号当原生手势或系统变化 |
| 原R7-C2：已移交，未执行 | 基础功能完成后，将1/3/6张海报、Rain/Lamp/混合与真实Item加载/缓存/watcher/文件突发一起做长时资源及稳定性验证 | 不再阻塞R7收口或V3-05开发；移交状态不等于PASS，最终功能候选重新绑定、复核长测矩阵 |
| R7-D：阶段验收与交接 | C1后固定阶段候选、完成其余范围内的人工/真实环境检查，逐项说明支持与未验边界 | A/B、C1及D其余必要出口满足后，可携带原C2未验项关闭R7并进入R8；该携带已由DEC-033明确，无须再为同一长测取舍重复询问，不豁免其他核心缺陷/文件安全 |

**长测移交与阶段边界**

1. R7只交付可继续接真内容的布局/配置与短时可靠性基础，不声称完整收纳、长期稳定或资源预算达标。原长测方案输入和预算统一转交总计划§14.1，不保留两套互相阻塞的排期。
2. 基础功能完成条件以V3-05功能验收及支持矩阵确认；真实图标/引用最小闭环不是完整收纳已完成。05-08账本、补偿、失败夹具仍先于05-07/10实际磁盘变更，P0安全测试随开发执行，不后置到长测。
3. 原20轮收展/隐藏恢复/中心关开归C1短时组合，按风险用已有测试与增量复验，避免为等待长测新增不必要UI打磨。BUG-020/022及真实系统/原生输入的其余范围仍归D；核心缺陷不得因本次长测迁移自动放行。
4. 后续长测使用基础功能最终候选，测实际Item/文件和装饰共同负载；旧DEMO短样本保留为历史参照，不能继承为新包长期PASS。排期调整当时未启动定时任务/测试/新候选；随后C1实施结果见§11.8，仍没有启动长测。

**其他边界**：测试机动效失效排查同时按用户此前要求暂缓，启动路径仅为待核对假设、不标修复。本轮不统一启动入口、不改系统动画/电源/锁屏、不接真实文件、不Git同步；R7-A/B既有证据和延期BUG-031等范围不变。

### 11.8 R7-C1短时连续可靠性实施（2026-10-09）

历史状态（candidate05）：**IMPLEMENTED / PARTIAL AUTO PASS / CONTINUOUS-6 FAIL**。后续定点处置及当前阶段归§11.9；本节失败证据不改写为PASS。

**实现与方法**

- `App.ContinuousLifetimeProbe.cs`经原隔离lifetime入口运行，0/1/3/6张各20轮：中心开→依次收起→全隐藏→中心关→重显→逆序展开。使用原StateCommand/CAS、会话/R4与中心窗口，不重建生产逻辑、不发送OS鼠标输入；20轮自动开关关闭，原automatic/manual组探针另验重叠，结束时启用300秒策略供正常重启核对。
- 核对每个成功命令单revision、外框精确归位、用户内容/模板配置不变、会话epoch不复用、隐藏后轮询/场景时钟释放、中心关闭资源释放；全局设置按设置值/实体版本核对，不把总revision变化误判为设置修改。首轮仅允许测试夹具比例坐标物化为本机坐标，此后完整定义逐轮相等。
- 发现并修正验证器假阴性：旧`VerifyPresentation`用record比较模板字典/属性列表引用；diagnostic03实际/期望完整JSON相同仍失败。`PresentationReadbackEquality`逐值比较全部声明及选中/未选实例，17项正反例通过；不修改生产绘制、R4区域验证或事务。
- `verify-r7-continuity.ps1`绑定源码/候选/合同/过程证明；失败立即停止主套件，不生成`r7-continuity.json`成功manifest，保留原始失败。没有重试直到变绿或把独立补验拼为整套通过。

**候选和实测**

候选`out/r7-continuity/candidate05`，App SHA256 `C695E9D573AC29CC478957B08E84EC7BEF9A89F0C0C413A6348BE52FA6DA82C5`。主套件目录`out/r7-continuity/final05`虽然含final字样，**本次未通过、未封存成功**。其548源码/脚本、119产物哈希在主跑和独立补验后均复核未变。

| 门 | 结果与证据 |
| --- | --- |
| 合同（含019固定锁时序/32轮初始化反例） | 2413/2413、0失败0跳过；`final05/contracts/c1-full.trx` |
| 0/1/3张连续→重启→生命周期 | 各20轮通过，重启完整海报JSON/revision一致；`final05/continuous-{0,1,3}`与`signals-{0,1,3}`。0张只验中心/空目录，不声称注入了锁屏消息 |
| 6张连续 | 前10轮通过，第11轮第三张收起FinalRestore失败，PID7196；`final05/continuous-6/logs/desktopup.jsonl`，不得记PASS |
| 六张独立启用退出/重启、生命周期注入 | `remaining05/enabled-six`两进程及`signals-six`通过；这是新隔离配置，不是失败连续链的重启认证 |
| 独立/相交收展组及组合 | `remaining05/groups`与`combinations`四进程通过，包括自动A/手动B、他项编辑、删除及晚回调等原生产端口检查，不是OS手势 |
| R4原故障门 | `remaining05/surface.json`11案例/3个真实HRGN清除注入通过；不能替本次自然发生时序判通过 |

candidate05主套件及独立补验合计18应用进程：17个无error，失败PID7196有2条error（物理恢复请求及探针失败）。全部正常退出/退出登记资源0；这不证明长时无泄漏，未采集长期预算。preflight05单张20轮通过，run01/diagnostic02/03的集合引用假阴性和run04的夹具坐标假设失败均另存，未计入主套件PASS。

**当前阻断与下一步**

1. BUG-021新增自然样本：第三张是极简模板（非Rain专属），revision253，窗口/客户区均300×60；native边界、input、region设置均成功，但FinalRestore的GetWindowRgn=0、Region=null，严格恢复门返回失败。代码已在FinalRestore强制重装，故不能简单重用“旧缓存没重装”的根因结论；具体哪个原生调用/消息时序导致失败仍未证明。
2. 下一步先补这条**最终恢复**边界的定点观测/可重复反例，区分区域安装与随后的实际读回，并核对共享恢复能否回到committed revision。只有证据支持才小范围修复；不接受null区域、不无限重试、不跳过失败/改动画掩盖。
3. 修复后新候选重新跑完整C1同包门，再进入R7-D：020菜单命中、022空白桌面手势、R6携带的真实显示/DPI/不足冷启动/原生生命周期与桌面像素、配置选择器及跨机范围。已有PASS不冒充新候选真实环境证明。长测仍移交总计划§14.1；R7/R8当前均不据本轮强行收口。

无日常配置/真实文件变更，无系统锁屏/动画/电源设置变更，无新人工候选常驻，无Git提交推送。全部测试进程已正常退出，旧包/配置/日志保留。

### 11.9 BUG-021读回路径整改与R7-D准入（2026-10-09）

当前：**C1 AUTO COMPLETE / D1、D2、D4已询问范围USER PASS / D3 NOT RUN（USER DEFERRED）**。candidate09完整同包复验通过；review09最后一组人工反馈已收到，退出现场差异仍需核清。不是整个D矩阵或R7全部通过，C2按DEC-033继续后置。

人工增量（同日）：用户分别回复 **“D1 全部通过”“D2 全部通过”**。对应已询问范围：三模板展开/收起时菜单可点且不误触Item、标题/云可拖、透明留白/圆角外可操作桌面；A收展中操作B、重复收展/隐藏重显/中心关开互不吞操作、不异常归位，中心关闭仍可用。只认证这些问项，不把下表中的低透明度专项、Item Esc或退出重启自动并入，不重做D1/D2。

后续人工反馈：D3用户选择“暂不具备条件，先保留未验”，记**NOT RUN / USER DEFERRED**，不能用C1注入消息代替。D4用户回复“配置往返符合预期”，按问项记录两模板私有参数、标题/中心主题导出导入、原本机海报不覆盖和全局主题勾选语义通过；同ID异定义副本的专项冲突情形/跨机不据此推定。最后一组用户回复**“两项均通过，已正常退出”**，据此记录显式备份→改参→恢复及选择器打开时托盘退出为USER PASS；不将报告与现场核查混成同一证据。

**最后一组的现场核对（2026-10-09 11:58 +08）**：`review09/logs/desktopup.jsonl`记录11:54:51本机备份创建（revision260）；原PID8796于11:55:18、离线恢复PID32876于11:55:32均`application.stopped`且activeResources=0。恢复后PID39792于11:55:32启动，命令行指向同一封存候选、同一review09目录；核查时仍存活，日志最后事件为11:56:13的auto_posture.phase，无该PID的exit_requested/stopped或选择器取消完成记录，三进程目前error均0。已询问用户是否退出测试后又重开；未强杀/启动应用，也不据此认定BUG-007复发。当前只能写“退出体验USER PASS，最终运行/清理状态待核对”，不能写所有人工进程退出资源0。原binding及封存证据不追写成全项通过。

**定位结论与不确定性**

- 原final05是最终恢复失败；本轮diagnostic07在六海报第5轮展开中再次自然复现同类读回错误，PID44072，源海报Rain，原生容量300×360、可见区域300×150。第一次`GetWindowRgn`返回0，随后同一线程、没有区域重设/窗口恢复/消息泵重入的只读补查返回3（COMPLEXREGION）。原失败判定保留，探针依旧FAIL；之后原共享恢复restored=true，正常退出资源0。
- 这反驳了“返回0必然表示HRGN已被清除”，定位到**复制区域的读回路径不可靠、错误被当成收展失败**；并不证明Windows内部为什么第一次调用失败，也不能倒推每一份旧失败都是同一底层原因。R7-0强制重装仍保留，实际丢失区域仍必须拒绝。
- 新验证直接调用一次`GetWindowRgnBox(HWND)`取得实际已安装区域的最小边界，无临时HRGN、无副本、无重设、无重试。窗口/客户区/可见区域三边界严格相等规则不变；ERROR、空区域、偏移或尺寸不符仍FAIL。新日志`RegionReadMethod=GetWindowRgnBox`与旧证据区分。其API语义参见[Microsoft说明](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowrgnbox)。这里只认证边界，未宣称完成GPU/桌面合成像素验证。
- 诊断06为逐消息额外读回，两个六张20轮通过不能关闭故障；低干扰诊断07捕获上述反例。诊断08补直接边界观察但两次未再现，不能声称已在该自然失败点抓到直接边界值。诊断代码/二进制和原始日志保存在`out/r7-continuity/diagnostic08/source`及diagnostic06～08；临时探针已从生产源码退役。
- 另修脚本截止时间冲突：通用`--exit-after-ms`硬上限60秒，曾提前取消六海报诊断06。连续门改用原探针175秒可取消截止、脚本185秒等待；不放宽20轮/事务/资源判据，不修改生产退出上限。该超时与BUG-021分开。

**回归与封存**

- `VisualRegionNativeReadbackTests`新增11项：容量/可见区分离；实际HRGN删除、空、偏移、错误尺寸/窗口均严格失败且不偷偷修复；确定性无效复制目标只用于证明“复制失败≠区域不存在”，不冒充自然故障的确切触发器。架构用例要求单次直接查询、无复制/重试；旧路径10通过1失败（red.trx），新路径及相关原生合同40/40（green02.trx）。green.trx的失败仅为源码断言误匹配注释，已保留并改为匹配函数调用。
- 共享最终恢复新增`final-restore-region-reset`：在原生批移动之后、重装之前真实清除HRGN，要求原共享恢复、布局释放和revision不变。R4共12案例、4次原生区域清除注入，仍不是原生鼠标输入认证。
- 新候选`out/r7-continuity/candidate09`，App SHA256 `FDC0C5B40AFBE164A4B168835EFC8A3C1A50F3A5A5870DC4F0631608E28D9931`。唯一完整复验目录`out/r7-continuity/final09`，已生成成功`r7-continuity.json`，SHA `F299A6C247355E2BB0EEC8C963EA69607EC6800BFBA609455426C9896D245F0E`。2424/2424合同，0/1/3/6张各20轮＋正常重启＋生命周期、四个组/组合进程、R4 12案例/4次清除全部通过；17个应用进程无error，正常退出/资源0。550源码及119产物指纹前后不变，18份proof封存。没有用诊断成功或旧A/B证据拼接本轮PASS。
- 人工入口CheckOnly正例与6项反例通过：未知schema、缺最终恢复proof、路径逃逸、产物哈希变化、源码哈希变化、人工目录落入证据目录均拒绝。反例仅修改独立`guard09`副本，结果`guard09-results.json`，未改final09。已用封存candidate启动`out/r7-continuity/review09`，初始PID8796；实际窗口/进程以现场为准，不沿用历史PID。

**R7-D人工分批（不是已验结论）**

人工入口`scripts/start-r7-review.ps1`校验C1的18份proof、完整候选文件集/哈希、源文件指纹、17个清理进程、最终恢复注入；只能复制已关闭的三海报DEMO数据库到新的独立目录。既有人工目录须绑定同包；不覆盖旧包/原C1证据，不碰日常数据。配置内Rain/Lamp/极简共存，自动收展已启用、离开延迟300秒，可在中心改短进行人工验证。

| 批次 | 人工操作与通过标准 | 未验/携带边界 |
| --- | --- | --- |
| D1 原生入口/空白命中 | 三模板的展开、收起、低不透明度状态下使用标题/云拖动及“…”；菜单能点到、不会触发下方Item。透明留白/圆角外在桌面可框选、右键；控制中心关闭后仍可操作海报 | 对应BUG-020/022；不能用命令探针或固定坐标代填PASS |
| D2 连续组合 | A自动收展中操作B；重复收起/展开、隐藏/重显、中心关开、退出重启；互不吞点击，无位置/内容漂移或无法继续操作。Item拖动Esc取消 | 延期拖影/BUG-031白条单独记录，不冒称本轮修复；BUG-026原延期保持 |
| D3 真实环境 | 用户实际调整显示器/DPI/工作区或锁屏恢复；真实不足时临时二维浏览、原构图保留、恢复空间回归；极端不足冷启动与合成画面核对 | R6-CARRY-01～04的对应范围逐项记录；未具备环境就标NOT RUN，不能用消息注入代替 |
| D4 配置/原生选择器 | 调整两模板私有参数、标题显隐/颜色、布局和中心主题；导出→修改→导入核对，测试同ID异定义另建副本/全局设置勾选边界；显式备份恢复；文件选择器打开时从托盘退出应取消选择器并正常退出 | A/B自动历史证据不转授本轮真实操作；只用本独立DEMO目录，跨机未验单列 |

最近下一步：核清用户退出报告与PID39792现场状态的差异，再做R7阶段出口审查；不重复已经通过的D1/D2/D4问项。审查须分别列出已验核心链、尚未单列的低透明度菜单/Item Esc/人工重启/同ID异定义及跨机、用户延期的D3、已移交的C2，判定必要补验与可携带范围，不因最后一组通过自动标整个R7完成。R8之后才是V3-05真实Item接入，真实磁盘执行仍以前置恢复账本为门。本轮仅更新验收/交接，未改源码、未运行新回归、未提交推送Git。

## 12. R7出口审查与运行差异收清（2026-10-09）

**结论：R7核心已验范围可交接开发，完整验收仍OPEN；R8接入交接已执行，见§13。** 用户本轮明确要求“核清差异→审查R7收口与携带项→衔接R8”。这不是把D3或所有未问人工项改为通过，也不是稳定发布放行。原R7-A/B、C1和D问项的证据范围保持，不重复已经通过的操作、不重做UI。

### 12.1 进程差异的最终事实

- 原验收PID8796→离线恢复PID32876→恢复后PID39792，是同一review09的恢复重启链；上轮现场的PID39792确实尚存活，不能倒写成上轮已退出。
- 新日志记录39792在**12:08:35.750 +08**收到退出请求，随后portable.cancel_requested（picker=false、busy=false）→cancel_completed→**12:08:35.840 application.stopped，activeResources=0**。12:08:54现场已无DesktopUp进程；未强杀、未发退出消息，也未重新启动这份验收配置。
- 三个人工进程均有正常停止记录，error0、退出资源0。现在“还有运行进程”的差异已消除；无法由这些日志还原用户之前是否操作过选择器后重开，亦无需猜测。
- **证据分层**：选择器打开时退出仍按用户“两项均通过”记USER PASS；本次最后退出明确picker=false，不能伪装成该情形的自动复验。原BUG-007历史原生验证保持，不据当前日志新增复发结论。
- 人工日志位置为`out/r7-continuity/review09/logs/desktopup.jsonl`，停止后SHA256 `6533005F16E48F75B0E1D8713E0A776D548109C81C20E20D95DD33B84EDAB218`。旧binding及C1 seal不回填人工PASS。

### 12.2 出口逐项审查

| 出口 | 已有证据及当前结论 | 仍不承诺 |
| --- | --- | --- |
| 配置语义/失败保留 | A/B final04的便携v5、旧版本、同ID异定义副本、事务/失败和缺模块门；当前final09全量合同2424保持，D4配置往返及本机备份恢复USER PASS | 当前同包的所有模板碰撞原生操作、跨机/断电/所有损坏环境；普通备份恢复不是这些测试 |
| 短时连续与区域恢复 | C1 final09：0/1/3/6张各20轮、正常重启/生命周期、组隔离/R4，17进程；D2已询问组合通过 | 长时资源、真实显示/DPI/锁屏恢复和GPU合成全帧；没有证明Win32内部偶发复制失败原因 |
| 原生命中与日常入口 | D1三模板展开/收起菜单、云/标题拖动、透明留白桌面操作通过；D2中心关开与多海报操作通过 | 所有不透明度/层级、每种鼠标键盘输入组合；BUG-001不得因这一反馈自动关闭 |
| 配置选择器及退出 | D4用户反馈通过；本次三进程正常停止及资源0已核对 | picker=true退出的本次日志证明缺失，原生体验与日志证据分别保留 |
| Item直接操作与假源 | R6已确认首次拖动、反向、Esc、撤销及循环拖缩；§13补当前候选假源/资源处理器三组 | 不将旧候选的人手操作直接转授全部新候选；当前Item Esc/接缝handle等尚未单列人工范围保留 |
| 发布条件 | 可继续端口交接及非破坏性真实内容开发准备 | **R7整体全PASS、稳定版本、跨机/普通权限/长期运行认证均未取得** |

未发现此次R7修订需要另建布局权威、重写solver或回滚已采纳操作意图的证据。当前可继续R8的理由是配置/事务/短时恢复及假源通道有分层证据，R8不触及真实文件与Shell执行；不是用“已延期”宣称整个产品稳定。若后续出现配置丢失、已提交数据覆盖、确定性无法继续核心操作，必须回到对应阻断门，不能以本次交接豁免。

### 12.3 携带清单（未验不改PASS，不创建重复Bug）

| ID | 范围/当前状态 | 后续负责人及触发点 |
| --- | --- | --- |
| R7-CARRY-01 | D3及R6-CARRY-01/02：真实显示/DPI/工作区、不足恢复、极端不足冷启动、真实锁屏；NOT RUN / USER DEFERRED | R7-D环境验收；具备条件时用届时候选补验，承诺跨环境稳定/发布之前须收清；不冒充已纳入自动C1 |
| R7-CARRY-02 | D1/D2未单列的低透明度菜单、Item Esc/人工重启、接缝handle/完整键盘输入及部分动画终态像素 | 后续影响输入/视口的V3-05/06增量验收与R7-D回归；旧R6人工及新处理器证据分别引用，不声称本轮全部复验 |
| R7-CARRY-03 | 同ID异定义UI专项、跨机导入、普通权限与另一测试机启动动效 | 配置/部署支持矩阵；自动合同通过但对应新人工未验。测试机动效问题按用户决定暂缓，启动路径差异仍是假设 |
| R7-CARRY-04 | 原C2长测及动态标题分配成本，NOT RUN / MOVED | 总计划§14.1/V3-10-01/02/06，基础功能（含真实收纳）完成后联合执行；不重复索要已作出的DEC-033裁决 |
| 既有Bug/优化 | BUG-001标题拖动回退、004列表闪烁、006中心位置、026循环停顿、031白条、035云风感/尺寸余项；OPT-MOTION-02拖影 | 原清单保留：001/026可影响特定操作，004/006/031/035影响反馈/体验；不把没有新报告当修复，不在本轮自动重启全部排查。稳定发布前重新评估核心承诺影响 |

本节不是额外扩大既有延期授权；D3/C2沿用户原决定，其他未验项仍为开放验收责任。**开发交接可以推进，完整可靠性关闭另行核对**。无Git提交推送，无日常配置或系统设置变更。

## 13. R8真实Item接入交接（2026-10-09）

状态：**HANDOFF COMPLETE（现有端口审查＋当前候选假源复验）；V3-05 NOT STARTED**。本轮不新增平行内容引擎、不扫描/打开/移动真实文件、不冻结正式实体schema。保持UI C、单击选中/双击或Enter打开、按住移动即拖、手动与热度自动的已定规则。

### 13.1 可复用端口与接线边界

| 职责/现代码 | 当前合同与已验行为 | V3-05必须补齐 |
| --- | --- | --- |
| 身份与布局：Domain/ContentLayout.cs，ContentLayoutDocument/ContentPlacement | PosterId＋布局ItemId、文档/成员/placement版本；顺位/跨度/留白/图钉独立，源返回顺序不改布局 | 正式ContentItem/Collection/Membership/PosterItem映射；不能把文件路径、AssetKey或DEMO的Poster前缀ID当全局稳定身份 |
| 元数据：IContentItemViewSource/IObservableContentItemViewSource；Application/ContentSourcePorts.cs | 版本检查、非法/缺失项fallback；Changed合并到UI队列；源变化取消旧草稿，不偷偷增删成员 | 可信宿主adapter应提供快速、线程安全的内存快照；Read/Version不在UI线程做目录扫描/Shell调用。真实watcher/取消/共享所有权归05-02/11 |
| 成员更新：ContentLayoutCommand、原stateCommands/CAS | 成员变更必须走事务/版本校验；迟到松手不复活删除项；A的布局不写B | 内容实体及关系与布局的单权威事务、迁移/去重/移除关系；不存在“收到源事件就直接改Placements”的捷径 |
| 资源：ContentAssetDemand/ContentAssetCache、PosterVisualWindow.ContentAssets.cs | 按实际可见tile＋有限预取请求；循环副本去重；完整身份/版本/DPI/尺寸键；每表面4并发、64项/16MiB缓存、单载荷4MiB；迟到结果拒绝 | 真图标提取、图像解码/像素预算、真正可终止的取消/超时/隔离与跨海报全局预算。当前只计受控载荷接受，**没有把图像贴到Item的解码链** |
| 打开：ContentActivationSession、IContentOpenHandler、PosterVisualWindow.ContentActivation.cs | capture/重复Enter去重、呈现见证复核、失效不分发；DEMO handler无副作用 | 当前Open是同步void，不能直接塞阻塞Shell或当成功事实。05-01/06定义异步受理/结果/ActivationId幂等，成功才计热度；失败/取消明确反馈 |
| 生命周期：PosterSpatialRuntime.ContentSource.cs、PosterVisualWindow.ContentSource.cs | adapter绑定随海报目录存在，旧窗口释放订阅，重显重绑；R4中先拒绝旧资源，保留预制树至原恢复安全点 | 真实source/提取服务的共享订阅与Dispose/后台任务所有权，不能因窗口隐藏重建重复启动无限任务；元数据不可执行任意主题/脚本 |

上表的“布局ItemId”是现端口中的名字，不预先裁决05-01数据库列名；共享fake SourceKey也不等于正式同一ContentItem身份已实现。适配新实体允许在宿主边界增加映射/打开结果通道，不需把文件职责塞入solver。

### 13.2 当前候选复验与复现入口

沿用final09封存候选，App SHA256 `FDC0C5B40AFBE164A4B168835EFC8A3C1A50F3A5A5870DC4F0631608E28D9931`。运行前后用`start-r7-review.ps1 -CheckOnly`核查原550源码/119产物/18 proof；只校验、不打开人工包。原manifest/binding保持不变。

| 本轮新证据 | 结果 |
| --- | --- |
| out/r8-handoff/source-one-01 | 1张，PID3332，5场景/29检查PASS |
| out/r8-handoff/source-3-01 | 3张，PID19872，5场景/31检查PASS |
| out/r8-handoff/source-6-01 | 6张，PID9360，5场景/34检查PASS |
| 退出与绑定 | 三进程exit0、error0、activeResources0；每组121份候选/脚本绑定前后不变 |
| 既有合同核查（不是本轮重跑） | C1的2424项中，ContentActivationSessionTests 23、ContentAssetCacheTests 23、ContentResourceDemandTests 16，共62项通过；原TRX及哈希保留 |

五场景分别为：来源20次变化仍受真实占用槽限制；失效/恢复与旧手势取消/打开去重；显式成员CAS/迟到松手/多海报隔离；收展中源通知仅在R4恢复安全点接纳；隐藏后的迟到资源无效、重显绑定正确。只调用实际生产托管处理器，不合成OS鼠标，也不执行真实文件或bitmap decoder。

复现入口仍为`scripts/probe-content-source.ps1`，传`-AppDirectory out/r7-continuity/final09/candidate/app -FixtureDirectory out/r7-continuity/final09/candidate/fixture -ExpectedAssemblySha256 <上述SHA> -PosterCount 1|3|6 -OutputRoot <新的独立目录>`；先正常退出其他DesktopUp，绝不复用已有证据目录。汇总和逐份证明哈希见`out/r8-handoff/r8-handoff.json`。未来改源码后须重新构建/绑定，不能借旧hash认证新代码。

### 13.3 接下来按原主线推进

1. **V3-05-01：实体/能力与打开结果合同**。先把稳定内容身份、海报内关系身份、集合所有权、来源能力和失效状态分开；明确同一文件不同启动参数、同Item多海报引用、删除关系不删源及DEMO兼容。输出schema/迁移合同与纯测试，暂不触碰实际用户文件。
2. **V3-05-04：非破坏性引用添加**。隔离fixture→添加预览/去重/逐项失败→原单权威事务，原文件不动；不以DEMO数量增长替代真实关系验证。
3. **V3-05-05/06＋06-01..04：真图标/打开/定位/移除关联及重启闭环**。补解码/提取隔离与预算、异步打开事实和版本准入，复用现视口/布局/输入，按R7携带项进行影响范围复验。
4. **集合与实际收纳**：随后02/03；05-08预写账本/取消/故障恢复先于05-07/10任何真实磁盘执行。真实收纳基础完成后再按DEC-033联合长测。

最近下一步是05-01的合同与纯验证，不是立即移动桌面文件。R8的接入交接通过不代表ContentItem/Shell适配/真实收纳已经完成，不豁免§12携带项及未来发布门。

## 14. 文档源码对齐审计与05-01准备（2026-10-09）

范围：活动ABC入口、冷启动提示、当前主计划/任务正文、与V3-05-01直接相关的架构/模板/内容合同，以及现源码与R7/R8绑定证据。不是全库历史文档重写；原型HTML、旧失败/人工反馈、旧包/配置及源码均保留。用户本轮要求的是同步与准备，本节不把下一步模型或测试登记为已完成。

### 14.1 差异与处置

| 差异 | 现代码/证据事实 | 文档处置 |
| --- | --- | --- |
| 冷启动仍指向RR-06待解锁、不进R7；任务正文仍写R7/R8未开始 | RR-06已收口；R7核心已验、完整验收OPEN；R8现候选假源门/接入交接完成 | README、ACTIVE_PATH、冷启动可复制提示、需求导航、总计划/工作流/LR计划改为当前入口；旧节点标历史，不删除证据 |
| 模块/UI C说明写无Rain、Lamp未解耦 | Modules/Lamp、Modules/Rain与SceneModuleRegistry、Directory.Build.targets已接可选可信源码模块；参数表单由声明驱动 | 修正模块与UI C实现映射；没有宣称支持用户DLL/任意脚本、完整目录热替换或在线AI |
| 仍以Portable v4的拒绝范围描述现在的配置能力 | PortableProfileCodec.Version=5，R7-A/B已补声明/实例/主题携带与冲突副本映射 | v4限制转历史；跨机、同ID专项人工等仍按§12携带，不能用实现代替人工认证 |
| 架构写“顶部锚点不动” | SpatialExpansionPolicy先下后上及条件归位，复用唯一空间/R4通道 | 修正架构表，与已定需求一致，不重做solver |
| “05-03取消”省略上级编号 | 取消的是V3-04-05-03托盘单张子菜单；V3-05-03受管集合仍是主线需求 | 补全编号，避免下一阶段误删Managed Collection |
| 后续任务仍要求复用ScrollViewer、三种布局模式、默认游戏单击 | 现ContentLayoutFlow＋Canvas可见tile/连续offset；两模式及DEC-023手势已定 | 工作流05-06/06-01/06-08沿用现引擎/基线；可选游戏预设另评审，不悄改默认 |
| R8基础容易误读为正式内容已实现 | ContentItemView/布局局部ID、SourceKey、假源资源端口已存在；正式ContentItem/Collection/Membership/PosterItem未落代码/存储 | 通用合同及模块职责明确“现实现/目标设计”，由05-01建立映射，不将路径或DEMO ID当正式全局身份 |
| “打开/资源加载”容易被当成真实能力 | ContentSourcePorts.Demo的Open为void/no-op；ContentAssets只接受有界载荷，无bitmap decoder；演示反馈带demo标记 | 列为05-01结果合同、05-05/06真实适配任务；假源PASS不是文件已打开或图标已显示 |
| 热度注释落后于纯提案能力 | ContentLayout.cs的AllowHeatDrivenSize旧注释称未实现排序；ResolveHeat已按注入Rank排序并可映射尺寸，手动不改；真实统计/公式仍未接 | 本节记录局部注释债，后续改该模型时同步修正；本轮不为注释改动使封存源码绑定漂移，也不宣称真实热度完成 |

代码事实入口：

- [版本与布局身份](../../../../src/DesktopUp.Domain/ContentLayout.cs)、[热度提案](../../../../src/DesktopUp.Domain/ContentLayoutProposals.cs)；注入提案不等于真实使用统计。
- [打开端口](../../../../src/DesktopUp.Application/ContentSourcePorts.cs)、[激活意图](../../../../src/DesktopUp.Application/ContentActivationSession.cs)、[DEMO分发](../../../../src/DesktopUp.App/PosterVisualWindow.ContentActivation.cs)、[资源接纳](../../../../src/DesktopUp.App/PosterVisualWindow.ContentAssets.cs)。
- [便携格式](../../../../src/DesktopUp.Application/PortableProfileCodec.cs)、[数据库](../../../../src/DesktopUp.Infrastructure/SqliteStateStore.cs)、[呈现参数](../../../../src/DesktopUp.Domain/PosterPresentationParameters.cs)。现版本分别为portable **5**、SQLite **12**、content layout **3**、presentation **6**，属于不同版本域，不提前冻结V3-05的新版本号。
- 架构图中的ShellProxy/ThemeHost/Updater及正式文件恢复账本仍是目标模块，不以建议名称证明已落地；现SQLite状态事务日志不能冒充真实磁盘搬移账本。

### 14.2 收敛结论与下一步

不是简单地“代码全面超前”或“计划全面落后”：旧阶段描述落后于已落地的宿主、布局、模块与配置；另一方面，架构中的正式内容与文件安全仍确实超前于实现，需保留为计划。当前没有发现应在本轮撤销生产功能的依据，也没有把待验状态补成PASS。

下一步入口为[工作流§6.1：V3-05-01实施准备](../../30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md#61-v3-05-01实施准备实体能力与打开结果)：依次做实体身份、能力/兼容、打开结果、纯验证。当前仅准备，不运行用户库迁移，不做真实Shell/文件操作；R7完整验收OPEN、§12携带责任及DEC-033后置长测保持。


本次验证只检查文档差异/链接及当前封存绑定，具体结果归VERIFICATION本轮记录；没有重跑2424合同或应用进程门，不新增原生人工结论，无Git提交/推送。
