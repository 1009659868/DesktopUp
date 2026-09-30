# LR-R1～R8 里程碑执行与多 agent 交接

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
