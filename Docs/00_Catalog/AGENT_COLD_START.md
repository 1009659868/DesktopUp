# DesktopUp：agent 冷启动提示词

## 当前接手基线（2026-10-14）

先核对实际工作区、分支、提交与改动，再读[当前交接](../A_Project/40_Implementation/V3-04/HANDOFF.md)、[安装交付最新节](../A_Project/40_Implementation/V3-04/PREVIEW_INSTALLER_DELIVERY.md#2026-10-14-便携工具链与安装体验同步)及[Git策略](../Rules/GIT_STRATEGY.md)。本轮工作区为`D:\Project\Desktop-up`；`impl/poster-redesign-V3`保留开发资料/测试，`impl/main`为精简发布源码，不能反向合入发布删除。公共修复源`e7943af`，最终refs现场确认，勿按下方旧路径/旧HEAD切换。

复制给其他agent：项目为WinUI 3桌面海报收纳工具；D4-N原生拖入/拖出四组已人工通过，普通点击穿透已撤销，下一主线05-05-V2尚未实施。最新维护完成本地SDK/NSIS缓存、PS5.1兼容与中文输出、空海报目录启动规则和安装布局：无海报每次普通启动显示中心，有海报后台，新增基础收纳；安装依赖在app目录，非空目录允许但专属名称碰撞拒绝，卸载保留未知文件/配置/收纳。用户已要求清除历史out、构建/工具缓存及五张旧截图；旧证据路径现不可访问。根目录setup/build/package脚本会首次联网准备工具，完整缓存后才可Offline。本轮不继续产品开发；新任务依A需求/决定→B架构→C实施顺序推进，保留跨机/DPI/满盘/长时未验项；全量3304合同属于旧版本，最新验证范围以交付记录为准。

以下时间线均为历史，不覆盖本节；旧“同卷限定”“普通启动总显示中心”“旧包保留”“无Git授权”等只描述当时状态，不是本轮事实或永久指令。

DEC-039排期已经用户确认，不再重复询问：先完成剪切式拖入/拖出基础交互，再做V2图标UI／V3构造。旧引用拖出也移动原对象且先确认源路径；受管存储、账本/恢复和隔离故障门不能因前置而跳过。

2026-10-10当前（DEC-041）：**失败自动收尾＋基础原生拖放，准备人工验收。** 旧恢复任务界面退役，§10.4四项人工不再执行、不补PASS。普通失败直接重新拖动；内部账本、身份／防覆盖保护和schema18保留。[当前实施与验收](../A_Project/40_Implementation/V3-05/DIRECT_TRANSFER_DELIVERY.md)记录自动收尾、CF_HDROP拖入、原捕获手势拖回真实桌面与支持矩阵。本批同卷本机文件／目录／快捷方式，跨卷及任意Explorer目标未开放；真实满盘继续延期。旧包和证据保留，未操作用户文件、未Git提交推送。下方旧阶段只作历史。

## 历史检查点（以下时间线不作为当前任务指令）

2026-09-30 Git检查点：本次用户授权同步当前V3到同名origin，源码`5f753e1`及相邻文档提交；Release构建与2274合同复验通过。实际HEAD/远端每次现场核对。RR-06未完成、白条延期，阶段不因提交变化；本地`.workbuddy/`和`out/`不上传。下方“无Git授权/未提交”是该次同步前历史，不构成后续自动提交或推送授权。

最新实施状态：RR-01基线保全；RR-02云标题/动作、RR-04时间呈现与RR-05核心分组收展已接生产，RR-03/BUG-031用户延期。candidate07全量2274和双组WinUI/R4/模块移除恢复通过；RR-06未完成，桌面锁定使指针不可读，动效、资源和原生人工待解锁续验。见[模块§10](../A_Project/40_Implementation/V3-04/LR_SCENE_MODULE_EXECUTION.md#10-rr整改生产接线与同包验证2026-09-30)，不以旧包PASS封存新包。下方“尚未接标题/下一步RR-02”为历史。

2026-09-30最新（DEC-031）：用户要求**先补整改计划**，现已整理[Rain§7.5的RR-01～06](../A_Project/40_Implementation/V3-04/LR_SCENE_MODULE_EXECUTION.md#75-分批推进与出口)。云朵作为唯一标题组件的模板外观，复用标题输入/状态；同时安排可读按钮、原生白条、安全提速及跨海报调度隔离。下次按实际用户指令从RR-01反例基线与标题合同开始；当前只是DOCS ONLY，无新实现/运行验证。Rain NEEDS REVISION，R7未开始，旧自动PASS不等于四问题关闭。

2026-09-30当前（DEC-030）：**R6 CORE CLOSED，进入最小模块B/C/D→Rain**；B/C/D自动门已通过；Rain生产接线/最终自动门通过，当前人工发现四项问题，待修订。先读[模块实施记录](../A_Project/40_Implementation/V3-04/LR_SCENE_MODULE_EXECUTION.md)、HANDOFF及[R6收口范围§9](../A_Project/40_Implementation/V3-04/LR_R6_CONTINUOUS_OPERATIONS.md#9-2026-09-30-r6收口决定与携带项)。R6真实显示/DPI等余项转R7，不补记PASS、不重新阻塞模块。以下历史状态不覆盖本段；无Git授权，保留`.workbuddy/`及失败材料。

2026-09-28 当前 Git 同步：用户授权提交并推送当前 `impl/poster-redesign-V3` 至同名 origin 分支；源码检查点 `c81501592776a0c99779034dc0d7fd62b5b5a367`，相邻文档提交保存本轮进度。2175 合同复验通过，485 源码与已验证打包源清单零漂移；不新增原生人工认证。实际 HEAD/推送结果每次通过 Git 核对，见 [最新交接](../A_Project/40_Implementation/V3-04/HANDOFF.md)。`.workbuddy/` 和 `out/` 不上传；以下“98e0204＋未提交”“ahead 4”“不推送”为同步前历史，不是当前状态或新任务指令。

2026-09-28最新按需任务：[Preview一键打包维护](../A_Project/40_Implementation/V3-04/PREVIEW_INSTALLER_DELIVERY.md)完成，旧包已移入回收站。根Package-Preview.cmd＋集中JSON设置支持PS5.1/7、自选安装路径、初始/新增默认lamp与背景不透明度25%；不改已有配置、复制/导入/模板重置或普通构建。2175合同、本机自选目录/托盘新增/重启/卸载自动门通过；验证安装已卸载、验证安装器回收、测试配置保留，目标机/普通权限未验。当前交付脚本，不混用旧包或candidate03哈希，不推进R6 2B、不Git提交推送。

2026-09-28合并上下文：远端`impl/poster-redesign-V3-wb@fc8c920`合入本机当前`impl/poster-redesign-V3`，实际工作区为`G:\A_\Tools\DesktopUp_V2`；本次不推送。下方`D:\Project\Desktop-up`、wb分支和候选基线属于此前机器的校验快照，不是切换目录/分支指令。本地独有的[旧LR-R1提示词](../A_Project/40_Implementation/V3-04/AGENT_HANDOFF_PROMPT.md)已标历史，当前接手以本页、最新决定和实际Git为准。

维护日期：2026-10-10。用途：建立可追溯的认知与当前任务入口，不取代需求、计划或验收。历史基线不是切分支或运行旧PID的指令；详细待办维护在工作流与执行记录。

历史本机核对（2026-09-30）：当时为impl/poster-redesign-V3@e698a3e加未提交修改；R6第三批2206合同属于当时证据。当前实际Git、候选和分项证据以HANDOFF最新段及只读检查为准。

## 可直接复制的启动提示

```text
项目目录：G:\A_\Tools\DesktopUp_V2（若运行环境不同，以实际检出的项目根为准；不要误进旧G:\A_\Tools\DesktopUp）。
请读取并执行 Docs/00_Catalog/AGENT_COLD_START.md 的冷启动流程，实际核对文档、Git、源码与可用证据后再报告，不只复述摘要。
目前已知：R5/R6核心及RR-06已收口；R7核心已验、完整验收OPEN，C2按DEC-033后置；R8交接完成。V3-05已接正式关系、非破坏性添加/共享修复、Shell显示打开及移除关联；V1图标资产/迁移自动门通过、SQLite15。DEC-039已确认先做桌面↔海报剪切式拖放：先读工作流§6.5，推进05-03/05-07第一批受管存储与意图合同及纯验证，再05-08恢复安全和双向接线。V2图标UI/V3构造后移至核心稳定验收之后，不重问排期，不重做已有添加合同；账本前不搬移文件，测试先用自建隔离数据。默认基础收纳、介绍文字待人工和R7携带保持，无后续Git自动授权。
如果本次仅要求冷启动或开发准备，完成《项目开发准备摘要》后等待任务；如果本次已指定开发任务或明确要求继续主线，完成必要理解后直接在授权范围推进，不重复询问已确认的产品选择。
保留已有改动、配置、旧包和失败证据；区分需求、实现、测试与人工体验。不要凭历史记录切分支、重开延期Bug、提交推送或执行真实文件搬移。
```

## 1. 先建立真实工作基线

在实际项目根执行只读检查，记录结果；发现不一致先解释，不切回文档中的旧版本：

```powershell
Get-Location
git status --short --branch
git branch --show-current
git rev-parse HEAD
git log -3 --oneline
git remote -v
```

- 定位适用的仓库级/目录级`AGENTS.md`或同类指令；本次维护时未发现项目AGENTS文件，不代表以后一定没有。跳过`out/`、`node_modules/`、`.pnpm-store/`、`dist/`、`bin/obj/target`等生成目录的全文搜索。
- 历史远端制作基线（2026-09-27）：分支`impl/poster-redesign-V3-wb`，远端`origin/impl/poster-redesign-V3-wb`；当时HEAD为`67559b4800a939871271b29b9823baf2895baad4`，源码检查点为`e9a3320d97a0cc81812d472e9c0689ed6fe8bb8a`。当时已核对本地与远端HEAD相同；后续冷启动文档形成`fc8c920`，本机再合并为`98e0204`。启动时以Git实查为准，不回退到历史快照。
- 旧`bd148fb…＋dirty`是候选25制作时的基线；`D:\Project\Desktop-up`/wb属于此前机器，当前本机为`G:\A_\Tools\DesktopUp_V2`/V3。历史PID和分工不直接作为执行指令，Git提交不改变既有产品验收范围。
- 既有未提交/未跟踪文件视为在进行或本地保留的工作，不清理、回退或覆盖。此前同步排除了依赖、构建包、个人设置及未引用旧截图；不要用`git add .`把它们重新纳入。
- 本页和此前Git同步记录不构成每次冷启动自动提交/推送的授权。按当前用户请求及仍适用的会话授权执行；“继续主线”也不表示合并`main`或`dev/mainline`。

## 2. 按权威和依赖阅读

先读下表的当前摘要及指定章节，再展开与任务相关的细节。根据明确取代关系、适用版本和产品决定判断生效状态，不仅依据日期；同一天多份“最新”可属于不同历史候选。

| 顺序 | 必读资料 | 需要回答的问题 |
| --- | --- | --- |
| 1 | [ACTIVE_PATH](ACTIVE_PATH.md)、[HANDOFF最新段](../A_Project/40_Implementation/V3-04/HANDOFF.md) | 当前工作在哪，最近交付是什么；哪些下方记录仅为历史 |
| 2 | [需求决定索引§3.1](../A_Project/10_Requirements/REQUIREMENTS_RISKS_AND_OPEN_QUESTIONS.md#31-已明确的近期优先级)及[需求目录](REQUIREMENTS_INDEX.md) | DEC-019～034有效规则；DEC-032模板副本、033长测后置、034成功交接 |
| 3 | [LR计划§8](../A_Project/30_Plans/V3_04_L_REMEDIATION_REVIEW_PLAN.md)、[工作流§6.1](../A_Project/30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md#61-v3-05-01实施准备实体能力与打开结果) | 05-01完成范围及05-04下一步；R7未验与文件安全门不因交接取消 |
| 4 | [R5收口及代码/计划审计](../A_Project/40_Implementation/V3-04/LR_R5_CLOSEOUT_AND_PLAN_AUDIT.md)、[KNOWN_ISSUES](../A_Project/40_Implementation/V3-04/KNOWN_ISSUES.md) | 已实现/部分实现/未实现分别是什么，哪些Bug延期或仍影响后续出口 |
| 5 | [执行记录§12～14](../A_Project/40_Implementation/V3-04/LR_MILESTONE_EXECUTION.md#12-r7出口审查与运行差异收清2026-10-09)、[CODE_ANCHORS](../A_Project/40_Implementation/V3-04/CODE_ANCHORS.md)、[VERIFICATION](../A_Project/40_Implementation/V3-04/VERIFICATION.md) | 未验/延期、R8现有端口、审计和候选证据层级 |
| 6 | [总体架构](../A_Project/20_Architecture/DESKTOPUP_V2_V3_ARCHITECTURE_V1.md)、[模块目录](../A_Project/20_Architecture/DESKTOPUP_V2_V3_MODULE_CATALOG_V1.md) | 状态权威、输入/呈现/空间与各层职责；按涉及模块展开 |
| 7 | [UI C说明](../A_Project/20_Architecture/CONTENT_TEMPLATE_SCHEMA_UI_C.md)、[原型C](../A_Project/20_Architecture/Designs/content-layout-controls-c.html)、[E1已确认体验§4及§6](../A_Project/20_Architecture/CONTENT_LAYOUT_EXPERIENCE_REVIEW_V1.md)、[BM/CA组合核对](../A_Project/20_Architecture/CONTENT_LAYOUT_COMBINATION_AUDIT_V1.md) | 视觉及用户操作意图；HTML里的旧“整理内容”前置已被后续决定取代 |

涉及UI、动效或模板时，实际查看可访问的原型页面/图像/状态，并读取其真实CSS/JS依赖；不能仅看文件名或凭文字宣布视觉对齐。无法访问浏览器、图片或本机证据时说明未覆盖，不伪称已看过。空间/手动构图任务继续读[操作空间合同](../A_Project/20_Architecture/CONTENT_LAYOUT_OPERATION_SPACE_REVIEW_V1.md)及[手动构图合同](../A_Project/20_Architecture/CONTENT_LAYOUT_MANUAL_COMPOSITION_REVIEW_V1.md)；模块/Rain任务读[已批准解耦方案](../A_Project/20_Architecture/CONTENT_SCENE_MODULE_DECOUPLING_PROPOSAL.md)。

## 3. 当前项目状态快照

DesktopUp是Windows桌面海报式内容收纳入口，目标支持文件、文件夹、快捷方式与应用引用，多张海报共享一个控制中心和托盘。当前生产内容仍是DEMO；真实Shell打开、真实收纳执行、实际热度统计与在线AI尚未接入。

| 范围 | 已确认状态 / 接手边界 |
| --- | --- |
| R1～R4 | 行为合同、可恢复基线、布局/浏览模型、编辑保存合同及原生样板达到各自出口；不等于所有生产组合通过 |
| R5 | CORE CLOSED：UI C核心、有限路径两模式操作、配方与Lamp等有分项证据及用户认可；不是R7全面可靠性验收 |
| R6 | CORE CLOSED / DEC-030。三批核心及2206合同、32进程自动门和部分直接/循环/缩框人工通过；未验系统显示/DPI/冷启动等转R7，不补记PASS |
| 模块解耦 / 雨夜窗边 | B/C/D、Rain生产接线及RR-06已收口；后续云尺寸/风摆/标题显隐有各自证据，具体范围查模块记录；BUG-031等仍携带 |
| R7 / R8 / V3-05 | R7核心已验、完整验收OPEN；R8交接完成；05-01纯模型PASS，生产未接，下一步05-04；原携带与文件安全门不豁免 |
| BUG-026 | 纵向循环在原顶部/底部停顿后续滚可能失效；用户明确大致可用、后续再修。只携带记录，不自动排查修复、不阻塞R5 |
| 其余风险 | BUG-019首次seed/快照子类和021读回子类已有修复证据；其他原生/环境细项及001/004/006/026/031/035等按清单和执行§12携带，不用里程碑或Git关闭 |

当前数据边界：SQLite schema15；portable v5携带模板目录/实例/中心主题/布局，但仍拒绝真实引用（含独立图标），不得静默丢图。content layout schema3、presentation schema7独立演进；同机SQLite完整备份包含引用、打开事实和图标BLOB。跨机/环境余项仍未验，不把原schema12的DEMO边界当当前状态。

## 4. 用源码追踪至少一条真实链路

技术栈从`global.json`、`Directory.Packages.props`和工程文件核对：C#/.NET10、WinUI3、Windows App SDK、SQLite与原生Windows边界。当前`src/DesktopUp.App`为UI/组合根，Domain为模型，Application为用例/事务合同，Infrastructure为存储，Windows为原生宿主/输入；`tests/DesktopUp.Contracts.Tests`是合同测试，`DesktopUp.SessionFixture`是独立夹具。未跟踪的旧Tauri/Node生成目录不是当前生产技术路线。

可选核心追踪：`PosterInputRouter`及原生输入岛 → `PosterVisualWindow.ContentEditing.HandleContentInput/FlushContentMotion` → `ContentDragPolicy.Map`和`ContentLayoutEditing` → `CommitContentDocument` → `ContentLayoutCommandPolicy.Prepare`/权威事务 → SQLite → 状态投影 → 布局快照、实际tile、绘制和输入区域重发布。确认选择/浏览仅改临时状态，合法语义编辑才提交，冲突/非法/取消不覆盖原配置。

接续V3-05-04先追踪：ContentSourcePorts → PosterSpatialRuntime.ContentSource → ContentAssetDemand/Cache → ContentActivationSession → IContentOpenHandler。现处理器仍DEMO void/no-op，不是实际Shell打开成功；缓存为请求/载荷边界，不含真实图标解码。复用空间/CAS/R4，正式实体、能力、异步结果纯模型见V3-05合同§6；后续适配器不得绕过它直接宣称成功。

## 5. 不得丢失的产品与协作约束

- 新建默认纵向、循环关闭；切方向与循环开关独立，旧显式配置不重置，内容不足一屏不实际循环。
- 单击选择、双击/有效焦点区Enter产生打开意图；按住移动直接拖，无“整理按钮/定时长按”前置。自动拖动固定读序图钉，手动保存网格位置/留白；退出手动按A恢复保存来源，旧无来源明确拒绝，不猜测或全清。
- DEC-037澄清：仅Ctrl/Shift形成的选择集合（含单项）日常描边，普通点击/双击不描边，缩放锚点仍仅编辑时显示。真实Item无底板但逻辑命中不变；自定义图标只影响当前PosterItem、不改共享路径，尚待§6.4实现。新建网格/跨度辅助关；日常不显示测试提示。图钉用图标，右键解除走正式事务；装饰不扩大输入区域。
- UI C已采纳，不重选风格或重做已认可UI；锁定/收展归行为，基线/占比等中心设置不塞回海报“…”；只保留一套窗口标题。中心主题与逐海报模板独立。专属参数由配方声明，不按模板名称硬编码中心表单，不将AI说明/测试内容混入产品。
- 唯一状态权威、完整CAS/单revision、输入岛、空间owner、R4呈现与恢复通道继续复用；不引入第二solver、数据库或持久状态，不以未知字段清空/默认化解决兼容问题。
- 多agent协作先核对当前授权与分工。历史agent均已交回文件，旧分配不再有效；主agent重新登记各自任务、文件所有者、共享接口、依赖和验收。公共schema/接口先协调；构建/GUI由唯一队列串行，避免共同bin/obj和单实例冲突。子agent完成消息不等于里程碑验收。
- 保留原配置、旧包/旧失败；验证使用新候选与隔离DataRoot。运行前检查真实进程，关闭中心可能只是隐藏，不能沿用旧PID或擅自关闭用户实例。真实文件搬移仍须V3-05恢复账本和故障安全门。

## 6. 开发验证与证据要求

执行目录为项目根。`global.json`要求SDK10.0.103、`latestPatch`；当前机器曾使用隔离10.0.112，不据系统有10.0.301就判满足。先检查可用SDK/Windows依赖；本地工具链路径与准备方法见[执行记录§3](../A_Project/40_Implementation/V3-04/LR_MILESTONE_EXECUTION.md#3-r2基线与环境证据)，新机器不得假设`out/`已存在。

```powershell
dotnet --version
& .\scripts\build.ps1 -Configuration Release
& .\scripts\test.ps1 -Configuration Release
```

这些是已配置的构建/测试入口，不表示冷启动时已执行或必须无差别重跑。按任务先做针对性验证，再跑受影响正式门；SessionFixture不在solution中，需要相关进程门时单独准备。不要通过更改SDK约束或旧迁移指纹掩盖环境问题。

此前机器的候选25体验入口如下；本机2026-09-28核对不存在，不直接运行。若以后取得其完整配套证据，可先只读预检：

```powershell
& .\out\lr-r5-evidence\start-loop-wheel-review.ps1 -CheckOnly
```

入口依赖本地候选25和八组证据，`out/`未随Git上传；新克隆不存在时记录缺失并按任务重建新隔离候选，不伪造旧材料、不改hash绕过。当前候选`candidate25-loop-wheel`的App SHA256为`1FE34E3121EBDF3104753510294768D282F160517511A83C64D9D3E4BDA9D6CA`，445源码/402产物绑定。既有2115合同和八组13进程通过是历史实测，本次重算指纹或CheckOnly不等于重新执行它们。

必须分列：已确认需求、观察到的源码、纯模型/合同测试、App处理器/控件、真实OS输入/像素、用户人工体验、待验证推断。原型PASS、自动报告、Win32返回成功、资源归零和用户对某组正向反馈不能互相替代。失败保留；新候选绑定源码/产物/配置/场景；未测环境不写PASS。

## 7. 冷启动结束与开发交接

输出简洁的《项目开发准备摘要》，关键结论附项目相对路径及章节/符号：

1. 项目定位：用户、核心流程、范围与非目标。
2. 当前基线与进度：实际分支/HEAD/改动，目标与实现分列。
3. 文档与规则入口：权威、阅读顺序、取代关系。
4. 架构与实现地图：入口、核心业务链、依赖及可修改位置。
5. 设计与开发约束：已定行为、视觉、状态与协作边界。
6. 开发与验证方式：前置/命令、实际执行结果、未验证项。
7. 差异、风险与待确认项：来源、影响、是否阻塞；已有答案不重复询问。
8. 就绪结论：已可接任务/仅特定范围/具体阻塞，明确未覆盖范围。

末尾给其他agent的短交接须含：实际基线、当前任务/所有权、权威入口、证据、携带项和下一步。只冷启动则等待；已明确续主线则从工作流§6.1完成出口转V3-05-04非破坏性引用添加，具体请求另有限定则遵循其范围。影响范围、架构或安全的未决问题集中询问，已明确的产品选择不重复询问。

完成开发单元后按项目流程同步需求决定（如有变化）→方案/接口→计划/分工→代码与针对性测试→绑定证据→HANDOFF/VERIFICATION/CODE_ANCHORS/KNOWN_ISSUES及阶段状态。不要把本提示词维护成第二份详细任务清单，也不要仅更新聊天而漏掉共享文档。
