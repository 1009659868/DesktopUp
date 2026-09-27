# R5：UI C 与已确认操作意图对齐

2026-09-27当前结论（DEC-029）：UI/动画获认可，用户确认滚动大致可用并延期停顿续滚BUG-026；方向审阅结束，批准接续。**R5 CORE CLOSED**，逐项证据与代码/计划差异见[收口审计](LR_R5_CLOSEOUT_AND_PLAN_AUDIT.md)。下方UI缺口表与候选3～17为历史追溯，不再表示当前未实现；循环拖动/被动恢复仍归R6，模块/Rain排期归[主计划§8](../../30_Plans/V3_04_L_REMEDIATION_REVIEW_PLAN.md#8-当前出口和最近下一步)。

最新实测与候选状态见[输入报告§6](LR_R5_POINTER_AND_MODULE_REVIEW.md#6-候选21默认黑底与主题资源隔离)：用户对其他样式、灯光、图钉交互给出正向反馈；候选21修复默认黑底。本文件下方候选3～17等均为历史阶段，不单独裁决当前状态。

日期：2026-09-27。状态：**候选14六组同包验证通过；菜单排序已修，命中受遮挡，待必要人工体验；R5 IN PROGRESS，R6 NOT STARTED**。本文是原 R5 的差异与实施追溯，不新增产品路线或替代需求。阶段出口仍归[主计划 §4/§4.2.1](../../30_Plans/V3_04_L_REMEDIATION_REVIEW_PLAN.md)。§1～5保留审查起点，§6为第一轮，当前实施见§7。

## 1. 用户反馈和本轮证据边界

用户先对上一轮早期操作核对回复“人工体验通过”，随后明确指出“UI与操作意图与设计稿中UI c原型差距较远”，并提醒“文档中有做过操作意图确认的测试的，你目前还未完整对齐”。前一条保留为该组基础操作的人工反馈，不能推导为 UI C、E1 全任务生产版或 R5 整体通过；也不要求用户重新说明已确认的意图。

当前审查对象为工作区 R5 源码及 `out/lr-r5-evidence/candidate3-app-local/` 的既有截图/证据；App.dll SHA256 为 `925DF8591A0C085366671E2983FF4A09E2EDBAA23D9189640B55C4B42BAF40F2`。本轮未改生产/测试/原型、未构建或启动/关闭 App、未操作体验数据库。已实际查看 `content-fields-03/data/content-fields-wide-span.png`、`lamp-regression-01/data/lamp-preview-center.png` 和海报 XAML 图。C/E1 的 HTML、实际继承 CSS、交互 JS 和人工记录已阅读；旧 C 临时截图未找到，没有声称重跑浏览器或取得新的桌面像素。

原有 1921 项合同、协议/字段/配置重启结果保留其范围，详见[核心交互记录](LR_R5_CORE_INTERACTION.md)。它们不证明用户可以发现入口、预判影响、看到结果并恢复。此前以“保留四个 Pivot 索引/原有控件”为样板约束，可以解释实施路径，却不能成为产品界面沿用旧分组的依据。

## 2. 必须合并理解的权威入口

1. [体验记录 §1、§3～4、§6.1～6.2](../../20_Architecture/CONTENT_LAYOUT_EXPERIENCE_REVIEW_V1.md)：六任务的意图、风险和用户结果。01～05 符合预期；06a 取消和 06b 关闭中心继续拖动随后单独通过；06c 是提示后恢复成功，首次入口未找到的事实保留。不能读到 §6.1 的历史“待反馈”就停下。
2. [DEC-017～025](../../10_Requirements/REQUIREMENTS_RISKS_AND_OPEN_QUESTIONS.md)：C 视觉已采纳；两模式/直接拖动；局部让位与适配构图承接；退出 A；被动受限双轴；草稿仲裁/旧配置保真；新建纵向且循环关闭。后续明确决定替代原型里的旧入口，不推翻全部 C。
3. [UI C §1～3](../../20_Architecture/CONTENT_TEMPLATE_SCHEMA_UI_C.md)及[原型 HTML](../../20_Architecture/Designs/content-layout-controls-c.html)：控制中心信息分组、样片视觉选择、双层收纳、并列宏观调节、作用范围与参数表达。右侧评审舞台不是产品预览。
4. [手动专项 §3、§5～6](../../20_Architecture/CONTENT_LAYOUT_MANUAL_COMPOSITION_REVIEW_V1.md)、[图钉专项 §2/§6](../../20_Architecture/CONTENT_LAYOUT_PIN_DRAG_REVIEW_V1.md)、[BM-01～12](../../20_Architecture/CONTENT_LAYOUT_COMBINATION_AUDIT_V1.md#2-ui-c入口--行为状态--命令--验收)：把上述意图落实到预览、影响范围、双入口及正式验证。

E1 是意图确认试验，不能照搬其试验操作器、A/B 选项、模拟空间隐藏或结果填写表成为产品 UI。生产回归应验证相同意图，不让用户重新做已通过的原型比选。

## 3. 六任务到当前生产的追溯

| 已确认意图 | 当前代码与既有证据 | 尚未对齐的生产体验 / 验证要求 |
| --- | --- | --- |
| **01 找到与打开**：单击选择；双击/有真实焦点的 Enter 打开一次；拖动释放不打开 | [ContentActivationSession](../../../../src/DesktopUp.Application/ContentActivationSession.cs)、[ClickContent/DispatchContentOpen](../../../../src/DesktopUp.App/PosterVisualWindow.ContentActivation.cs)及协议门覆盖去重和拖后不误开；当前仅 DEMO 意图 | `DispatchContentOpen` 只计数、日志、赋 `_contentNotice` 并通知中心；海报结果区没有闲置态的打开结果绑定。中心关着时用户能否识别成功，不能由 log 通过推导。补本地可见 DEMO 反馈，不接真实 Shell、不强制用户先点专用键盘按钮才可理解选中状态。 |
| **02 移动与恢复**：空位/占位直接拖，局部让位可预判，不满意一次撤销 | [ContentDragPolicy.Map](../../../../src/DesktopUp.Domain/ContentDragPolicy.cs)、[ContentLayoutEditing.Apply/PushManualPeers](../../../../src/DesktopUp.Domain/ContentLayoutEditing.cs)和 [HandleContentInput](../../../../src/DesktopUp.App/PosterVisualWindow.ContentEditing.cs)已接同起点预览、单笔保存/历史 | 当前普通手动提示只有“松手保存位置”；没有受影响/屏外项范围表达。桌面撤销主要混在海报菜单，未建立清晰的当前 Item 管理路径；取消/成功/失败结果须在中心关闭时仍可读。验证局部让位与撤销的可发现性，不能只断言文档值。 |
| **03 自动与固定**：让一个项目靠前，其他未固定项继续自动，再解除固定 | 同一编辑模型生成顺序图钉；[CreateContentCard](../../../../src/DesktopUp.App/PosterVisualWindow.Content.cs)有独立 pin 标识；[AddContentMenu](../../../../src/DesktopUp.App/PosterVisualWindow.ContentEditing.cs)及中心有解除入口 | 保留“固定顺序而非坐标/大小”的可读状态和就近解除；核对右键目标身份，不能作用于上次选择。当前 R5 没有以完整用户路径证明“观察变化→解除→继续排序”；DEMO 热度/处理器证据不等于真实使用统计。对应 BM-03/05、PD-06/16。 |
| **04 缩框后再编辑**：不编辑放宽还原；有效编辑承接整张当前构图；一次撤销恢复旧意图而非宽度 | [ContentLayoutEditing.Apply](../../../../src/DesktopUp.Domain/ContentLayoutEditing.cs)已有完整捕获与 `AdoptsProjection`；[手动投影测试](../../../../tests/DesktopUp.Contracts.Tests/ContentManualProjectionTests.cs)和 R4 事务证据覆盖模型范围 | **明确实现缺口**：App 未消费 `AdoptsProjection`，松手前没有说明整图/屏外恢复位置会改变，与 DEC-020、E1 §4.1 不符。必须在实际编辑时给短影响提示、可取消；不加第二次按下或确认弹窗。还需实际窄→编辑→宽→撤销连续链，不能用纯模型替代。 |
| **05 模式来回切换**：每次手动从眼前完整画面开始；回自动恢复原自动图钉/尺寸；再次手动不召回旧手动图 | R4 保存 `SavedAutomaticMode/State`，R5 协议有进入完整画面和退出 A；中心具备两模式 | 模式选择在第四页，结果解释主要是常驻段落；应在统一工作区明确当前模式及切换影响，桌面也能看懂当前操作含义。用有原图钉/尺寸且手动已修改的样例核对整条往返/撤销；不能用空覆盖样例掩盖差别。不增加退出 B 产品开关。 |
| **06a/b 中断与继续**：Esc/失捕取消不写；随后首次动作正常；中心关闭仍可继续 | R5 handler、owner/CAS/取消及迟到释放反例已覆盖，用户对早期操作组有正向反馈 | [DrawContentSnapshot](../../../../src/DesktopUp.App/PosterVisualWindow.Content.cs)仅拖动中把 `_contentNotice` 写入 footer；手势结束先清 gesture，取消又未设置新的结果文案，存在旧“松手保存”残留/被泛提示覆盖的路径。补完整反馈生命周期及关闭中心后的恢复入口，系统输入矩阵仍按原范围记录。 |
| **06c 受限与恢复**：空间不足可继续安全访问，恢复后退出 | E1 只模拟条件，用户提示后完成；DEC-022 随后明确产品双轴只读例外；`ContentSpacePolicy` 目前为纯模型 | 属 R6 已计划工作，生产未接。不得拿 E1 的隐藏占位/还原按钮替代。必须看得见受限原因、找到浏览路径，保尺寸/原构图，按最新状态恢复；首次不可发现的教训要进入 OS-03/06/10 的验收。 |

桌面结果缺口的具体链：[ContentEditing](../../../../src/DesktopUp.App/PosterVisualWindow.ContentEditing.cs)先结束 `_contentGesture` 后提交；[Content.cs](../../../../src/DesktopUp.App/PosterVisualWindow.Content.cs)的 footer notice 绑定限定 `IsDragging`；[PosterVisualWindow.UpdateVisualState](../../../../src/DesktopUp.App/PosterVisualWindow.cs)另写“拖动排列 · 双击打开”。这是代码路径事实；最终残留/覆盖的实机时序尚未重测，不能写成每次都发生同一种画面故障。

进一步核实的入口缺口：

- [PosterInputIslandManager.HandleMessage](../../../../src/DesktopUp.Windows/PosterInputIslandManager.cs)未路由内容右键；[HandleInvocation](../../../../src/DesktopUp.App/PosterVisualWindow.cs)只由标题 MoreButton 打开海报菜单。当前有解除能力，但没有 E1 §3 的 Item 右键/Shift+F10 管理入口，不能称为已完成同项就近管理，也不能在没有事件的情况下捏造“右键串写”。
- `AddContentMenu` 的撤销/重做仅按只读状态使能，`Action` 丢弃 `UndoContent` 的错误返回；无历史或锁定时直接返回，未转换成可见提示。需要正确可用状态和统一结果反馈。
- `ContentKeyboard_KeyDown` 仅在中心 `ContentKeyboardPad` 实际聚焦时处理 Enter；没有桌面 Item 的真实焦点键盘路径。完善可发现的焦点入口时仍遵守 NOACTIVATE/既有焦点合同，不通过全局键盘监听或抢焦点掩盖缺口；不能把双击通过标成桌面 Enter 通过。

## 4. UI C 信息和视觉结构差异

| C 中已采纳的表达 | 当前偏差 / 修改入口 | 纠偏边界 |
| --- | --- | --- |
| 紧凑外观工作区：样片域外保留尺度/疏密、方向/循环；行为另组 | [ControlCenterWindow.xaml](../../../../src/DesktopUp.App/ControlCenterWindow.xaml)中外观纵向堆叠滑块，方向/循环在第四个 Pivot | 恢复共同调节路径，并纳入已决两模式；高级数值/诊断按需进入，不以保留旧页签索引为验收。 |
| 样片图＋名称＋选中状态，折叠后当前摘要 | [ControlCenterWindow.Editing](../../../../src/DesktopUp.App/ControlCenterWindow.Editing.cs)仍用文本 ComboBox 选模板 | 为实际支持的模板提供真实对应样片选择；模板册完整管理、导入/另存/热装入仍 V3-07，不放假入口。 |
| 选中对象就近操作，中心可选精调 | XAML 常驻跨度/顺位/位置多行输入与应用按钮、专用键盘区 | 按当前选择/模式渐进显示，常用动作直接操作/小步调整；需要完整输入的成组数值仍保留一次提交、取消、非法草稿与唯一所有者。不能把“少按钮”实现成逐字符写库。 |
| 模板参数分组、色块、紧凑调节、值反馈及当前模板复位 | [TemplateParameters](../../../../src/DesktopUp.App/ControlCenterWindow.TemplateParameters.cs)为六行 Lamp 表单，颜色要求手写 hex，缺可视选择/复位 | 已支持参数回到可视操作；折叠不重置、单海报单模板隔离、复位走同一事务。任意 schema 参数平台不提前混入。 |
| 轻量页签/分段按钮/缩略标记/样片纸张层次；中心配色独立 | 原生仅复用纸色和大框，默认 Pivot/ComboBox/宽按钮主导；全局动效混入海报行为页 | 对照 C 实际组件尺寸与层级做原生表达；明确当前海报/全局范围。中心完整主题编辑能力与首批可用范围分开记录，不为补入口编造持久化能力。 |
| 短状态＋按需帮助，日常不必学习工程字段 | 常驻长说明与 DEMO 构图按钮占主路径 | 信息以当次动作/影响/恢复为中心；隐藏诊断细节不等于隐藏错误或兼容原因。 |

代表模板目前只有静态 Lamp 的绑定/六参数证据；指针跟随、雨云等未全部交付。原计划允许先验证一款，不能据此声称 C 场景已完整还原，也不能以完整模板平台延期解释中心基本选择方式的缺失。透明光池问题仍按[核心记录 §6](LR_R5_CORE_INTERACTION.md)先核实原生合成，不凭透明截图预览更改颜色。

## 5. 推进次序与验收

具体分步和里程碑统一维护在[主计划 §4.2.1](../../30_Plans/V3_04_L_REMEDIATION_REVIEW_PLAN.md)。本次完成的是六任务/决定/原型/代码/证据的差异追溯；尚未修复上述生产缺口。

- 先补 E1 01～05、06a/b 的影响与结果反馈、正确对象和恢复入口，再按 C 落地同一原生工作区；二者都属于当前 R5，不能把缺口全部甩给 R7。
- 每个改动注明对应 E1/BM/PD/MC 条款；验证既看状态/事务，也看当时可见文案、控件位置、目标身份和取消后的下一次操作。
- 以新独立候选、相同版本完成宽/窄中心及真实海报的可见状态检查、操作回归和重启；只请求必要的修改后产品体验，不重做 E1 规则确认或 UI 风格比选。
- R5 达到“首个可操作的 UI C 版本”后再实施 R6；06c、横向/循环组合仍按 R6/OS/CA 范围验，不提前标通过。R7/R8 不取消。

协作：root 持所有文档、公共合同、App 集成和串行构建；本次 r1_contract_audit 审查意图/出口，r2_review 审查 C 结构，r2_portable_audit 审查桌面结果路径，均只读。后续写文件前重新明确归属，不依据旧任务分工并发编辑。

## 6. 授权后的纠偏实施与验证

用户随后明确“ok，请开始吧”，按主计划§4.2.1步骤2～5实施。以下是代码状态；原生、人工出口分别记录，不回写此前基础体验为新UI通过。

- E1 01/02/03/06a/b：新增 `PosterContentContextRequest`、`PosterInputRouter.ResolveContext` 及 input island 右键路由。按当前session、layout、generation、frame及实际释放命中核对目标，App选择本次Item后打开就近菜单；打开仍是DEMO意图。`PosterMenuActionGate`阻止已关闭/消费的菜单回调复用；Undo/Redo依实际历史、锁定、草稿可用性启用，失败不丢弃。
- E1 02/04/05：`ContentInteractionFeedbackPolicy.Describe`仅从现有完整author/presented/edit/result派生整图承接、让位、屏外项、最终pin和自动返回态，未加第二solver或持久状态。拖动先提示，提交后发布结果；中心合法数值草稿用同一编辑器和effective热度预检显示应用前影响，无写入。
- 桌面反馈由 `PosterVisualWindow.ContentFeedback` 放在既有footer非内容命中区，避免提示覆盖卡片但底下仍可点击。两行短提示与中心完整说明并存，成功/取消定时清理，失效计时器不擦新错误；挂起清临时结果、恢复重绘兼容原因。当前排布模式显示在闲置footer，未增加全局键盘监听或夺焦点。
- UI C：`ControlCenterWindow.UiCWorkspace`/XAML建立外观、行为两页；三款实际支持模板的示意样片、并排尺度/疏密、域外模式/方向/循环、所选Item常用操作及按需精调。高级/诊断收纳；全局动效与当前海报设置分开。中心配色跟随系统，完整模板平台仍V3-07。
- Lamp：`ControlCenterWindow.LampColors`提供色块与ColorPicker、应用/取消、当前模板复位。色板打开即保护目标，旧非法草稿不被吞掉；关闭/换目标清实例身份。仍为静态效果，指针跟随和桌面透明合成最终视觉未据此通过。

复审修正：旧菜单仅验版本不足、原生激活false缺提示、内容区toast命中遮挡、恢复未重绘及临时反馈挂起后常驻，均已落实代码。中心数值预览也检查effective热度冲突，避免作者候选合法就误报可保存。

本轮初步证据（均为项目 `out/lr-r5-evidence/`）：

| 证据 | 已执行结果与边界 |
| --- | --- |
| `alignment-model-first/` | 74/74相关合同通过；没有原生输入含义 |
| `alignment-contracts-01/` | 全量1960，1959通过、1失败：既有 `PosterCatalogTests.ConcurrentFirstRunSeedsTheLegacyDefaultOnlyOnce` SQLite表锁；不改写为首轮全绿 |
| `alignment-catalog-recheck-01/` | 上述单项独立复跑1/1通过；不是并发缺陷已修 |
| `alignment-build-01/` | 首构建发现WinUI Thickness二参不支持及后继XAML错误，已保留失败日志 |
| `alignment-build-02/` | 修正后solution Release/x64构建0警告/0错误；尚不是最终候选包或产品验收 |

并发SQLite问题在[历史验证记录](VERIFICATION.md)已有同名失败；本轮Infrastructure/该测试未改。两个Store共享缓存并发初始化可能读失败；App正常单例Store路径和配置丢失不能由此直接推断。独立列为既有可靠性风险，不通过删测试、延timeout或伪造重试结果处理。

当前分工：r1路由/一次菜单合同与native右键driver、r2_review全部中心文件已冻结交回root；r2_portable当前仅新增整图承接原生probe及脚本；root负责其余集成、文档和唯一构建队列。R5出口仍需同候选运行与修改后的必要产品体验；R6尚未开始。

## 7. 用户截图反馈后的实际C核对与第二轮纠偏

用户要求继续未完成任务，并指出样片夹、文字、行为归属、海报更多菜单、双标题及模板/AI预留的偏差。此前以控件接线与配置测试为主的核对不足以证明C还原。本次实际在本地浏览器打开C、展开样片夹与专属参数，查看用户两张截图，并对照当前原生截图；原型文件未修改。原型的已取代“整理内容”前置仍不复活。

| 用户问题 / 权威依据 | 当前实现与证据入口 |
| --- | --- |
| 锁定、收纳/展开归行为 | `ControlCenterWindow.xaml`把Lock/Collapse/Visibility放入BehaviorScroll，保持原命令；自动收展在同页，外观保留宏控/浏览/两模式。实际行为页截图归`alignment-v11-ui/data/ui-c-behavior.png`。 |
| 样片夹及文字远离C | `UiCWorkspace`恢复NIGHT PLAY. / AFTER DARK. / MAKE ROOM.两行画面字、108 DIP示意、旋转纸卡、选中胶带、家族/使用中状态；XAML纸页签、当前摘要、收起/展开文案与真实模板册。只列实际支持3款，不伪造13款或雨夜可用。 |
| 海报⋯被塞入中心设置 | `PosterVisualWindow.ShowPosterMenu`移除基线网格、跨度提示、模式、所选Item图钉/历史和未接文件占位；只保留锁定、外框调整、打开中心、删除海报。网格/跨度/模式在中心；Item右键的打开/图钉/恢复尺寸/撤销独立保留。 |
| 双title | 保留原生`Window.Title`，明确`ExtendsContentIntoTitleBar=false`，移除自绘DesktopUp品牌栏；中心与全局移到导航右侧。已关闭InfoBar与空定位消息不再占位。XAML截图仅捕获客户区，系统caption最终由人工核对。 |
| 参数/AI入口及底层 | `TemplateParameterSchema`描述稳定模板/能力版本、key、binding、类型、标签/分组/提示、范围和默认；`TemplateParameterBindingAdapter`按binding接已有Lamp实例类型、草稿、正式存储。六控件实际按声明生成与复位。`TemplateRecipe`提供真实三模板册、默认/实例分开的只读配方和可复制AI说明；不调用网络AI、不假装可导入。 |

### 7.1 仍保持的边界与差异

- C的DEC-016/017方向保留：声明→能力校验→表单→当前海报实例→受控绘制。现在实现的是受信任内置Color/Range声明基础；未知绑定拒绝。新key/中文标签/顺序不再要求修改表单语义代码，新增宿主未知效果仍需版本化能力。
- 不新建数据库权威，不修改schema11或presentation schema3，不把调参覆盖写回定义默认。合同包括重命名key/label仍按binding保真、不可写默认、非法颜色草稿保护、两海报隔离和真实SQLite重开。
- 完整包导入、另存注册、资源/版本兼容、安全边界切换、last-good回退仍V3-07，联网AI/确认执行仍V3-08。入口明确当前是本地说明；没有将原型内存热装入当成生产完成。
- 原生仍支持night-ops/basic-box/静态lamp；雨夜、指针跟随、云/雨/风动、中心自定义配色尚未完整交付。夜航生产已有蓝色默认与C绿样片仍有差异，样片取实际生产主题来源，不通过缩略图假画绿或静默改旧配置。高对比/深色/真实高DPI和桌面透明合成没有据当前浅色XAML图通过。

### 7.2 同包验证与保留的失败

纠偏中间候选`out/lr-r5-evidence/candidate11-app-local/`：412份源码指纹、401产物记录；App.dll SHA256=`7F84414726B77DBF8D0D7762BD44EAD23B3A4E4FDF0695FA19D370569F704CD4`。`source-before-alignment-13.json`与`manifest.json`绑定；`app-local-alignment-13`构建0警告/错误。全量`alignment-extensions-full`1984/1984，新增`alignment-extensions-first`24/24；后续仅App/UI/probe调整，Domain/Application/Contracts源未再改变。冻结后仅review启动脚本补读旧UI证据格式的退出日志，未修改包；仍要求唯一退出事件且资源0，不将缺字段视为0。

- 候选5：`alignment-fields-02`337项及4图，`alignment-protocol-02`13组/13图及同库重启，`alignment-lamp-02`26步/9图通过；这些保留为中间证据。
- `alignment-composition-01`揭示真实中心宽度动画误用等宽收展合同。现新增`PosterGeometryTransitionSurface`双轴容量，只让中心resize走responsive lease，每帧消费同一prepared Content/Viewport并核对实际Canvas、发布命中、clip及native allocation。原等宽收展合同保持；测试覆盖宽窄/宽高/DPI/取消作者恢复。
- 候选6 `alignment-composition-02`的三段真实宽窄动画通过，probe按住后却等待包含自身手势的IsSpatialBusy，60秒正常退出而无完成记录。仅修正probe stage3自等待与期限；不把该次写成通过。候选7 `alignment-composition-03`完成全链及重启。
- `alignment-ui-02`新检查误读尚未连接的行为页子控件Parent；改为核对声明Children。`alignment-ui-03`五状态通过后实际看图发现页签被裁切；为页签留正向布局行，补统一文件夹边框。候选8→9最后仅此XAML视觉修正。
- 第一轮`alignment-contracts-01`1959/1960的SQLite并发首启锁失败保留；随后1960/1960和本轮1984/1984通过不能证明竞态已修。详见KNOWN_ISSUES独立风险。

候选9的六组中间证据保留。随后实际窄图发现footer第二行裁切：候选10及后续诊断记录分别保存于`alignment-v10-composition`、`alignment-feedback-diagnostic`、`alignment-v11-composition`、`alignment-feedback-render`；修正两行高度/行距，并缩短为“屏外94·让位99  Esc取消”。最后在实际截图后校验`IsTextTrimmed=false`及完整两行边界，`alignment-feedback-fit`完成100项缩窄→预告→单笔承接→放宽→单次撤销→同库进程重启。未放宽裁切断言或隐藏屏外影响。

候选11的六组证据为`alignment-v11-{ui,fields,protocol,rejected,lamp}`及`alignment-feedback-fit`：UI五态/5图、精调338项/4图、协议13组/13图及重启、非法热度拒绝及重启、Lamp26步/9图、整图承接5图及重启均通过。最后一组先在原构建目录运行，App/fixture全部产物逐项与冻结候选相同，review启动脚本已核对六组完整绑定；不是只比较主DLL。

补充`alignment-final-menu`未通过：弹层已创建，但`DesktopMenuInteraction.ActivatePopup`报告`Desktop menu order read-back failed.`后安全关闭，原生输入恢复、退出资源0；不能将本次归为鼠标遮挡或声称四项菜单已通过实机检查。另一次候选9 `alignment-v9-native-mouse`是遮挡导致零输入停止，保留两种不同失败。后续诊断、修复及当前原生边界见§7.3。

R5的产品人工出口未借前一轮“人工体验通过”覆盖，R6仍未启动。已实际查看候选11样片夹、100项承接/撤销截图；新的模板册/配方/AI说明弹窗还需实际操作核对。系统键鼠、深色/高对比、实际高DPI、桌面合成及雨夜完整效果没有由XAML/状态门获得通过。

### 7.3 当前交付候选14：菜单修复集成，同包回归完成

§7.2是中间候选与失败过程；当前固定候选为`out/lr-r5-evidence/candidate14-app-local/`，App.dll SHA256=`B42B923EFE35C869FE7C8E1A14BE0BD5817A1BCA5801BE89A700AF91513D7221`，manifest SHA256=`6560962C272AFC8B2B33A939DE03D1D5B2BCD9F3CC1ACFEDA2555A57CD6D372C`。`source-before-menu-fix-01.json`绑定412份源码/配置；401产物记录，冻结后源码无差异。`app-local-menu-fix-01.log`构建0警告/错误；菜单针对32/32、全量`alignment-menu-full` **2001/2001**、0跳过。定向首跑有一条xUnit断言风格警告，改用Assert.Contains后全量与构建无警告；未删除反例。

菜单诊断及修复证据：

1. candidate12 / `alignment-menu-diagnostic-01`明确SetWindowPos返回error5，Popup和owner均本进程同UI线程、非topmost；还未进入点击采样。该事实不证明具体系统权限/band根因，也不以提权或取消校验解决。
2. candidate13 / `alignment-menu-own-anchor-01`只在显式隔离probe中尝试一次：以自己的input作锚点成功，外部窗口和自身表面的相对序均未改变，关闭后恢复/资源0；`productAccepted=false`。实验分支已经从正式源码移除，原包和日志保留。
3. 正式`DesktopMenuInteraction.PlaceAndVerify`仅排列自己Popup和最前input两步。每步比较除Popup外的完整可见序列与Topmost位，最终仍要求原`IsOrdered`；仅第二步确切input/anchor/不动尺寸组合可改Z序，finally清许可。原生层再次核对进程/UI线程/owner/input类名。失败、异常、外部变化均关闭恢复，无重试/置顶/修改外部窗。独立只读复审未发现阻塞缺陷。
4. candidate14 / `alignment-v14-menu`实际两步成功，`ordered=true`、输入透明、除Popup外全序未变，实际命令恰为四项。随后15/15命中点均被外进程遮挡，六状态完整门未通过；退出0/资源0/输入恢复。**修复后的原生排序读回通过，不等于原生命中或菜单产品验收通过**。已请求用户露出桌面；未获新环境条件前不反复重跑或自动最小化其他窗口。

同候选六组均在`alignment-v14-{ui,fields,protocol,rejected,lamp,composition}`：UI五态/5图，字段338检查/4图，协议13组/13图及进程重启，非法热度拒绝及重启，Lamp26步/9图，100项缩窄/整图承接/放宽/撤销及重启/5图。各组退出0、无错误、资源0、全产物不变；`alignment-v14-summary.json`和`alignment-v14-review-check.json`登记完整绑定及源码一致性。已实际查看候选14宽屏样片夹、行为页和窄海报预告，提示未裁切；客户区图仍不等于桌面系统合成截图。

体验入口：仓库根目录运行`scripts/start-lr-r5-review.ps1`，CandidateDirectory取上述candidate14；ProtocolEvidence/FieldsEvidence/RejectedHeatEvidence/UiEvidence/LampEvidence/CompositionEvidence分别取对应六目录，DataRoot取新隔离`out/lr-r5-evidence/human-review-02/data`。先`-CheckOnly`已通过，再启动；保留旧`human-review-01`和全部失败库。体验数据只有DEMO，不执行真实Shell或文件动作。

本轮必要人工核对：外观/行为与单标题；展开纸样片夹、切深夜书桌/调参数/取消或复位；打开模板册、配方和AI说明再关闭；关闭中心后拖动，确认影响预告、Esc取消、一次撤销可理解。它是修改后生产体验，不重做E1规则比选。完整菜单命中、系统键鼠、实际高DPI/深色/高对比/桌面合成、雨夜/指针跟随和完整模板平台仍按各自门记录；R5 IN PROGRESS，R6 NOT STARTED。所有子agent已冻结交回root。

## 8. 2026-09-27 六项实机反馈：候选16已构建，原生验证待旧实例退出

本节取代§7.3中静态Lamp、只读配方及无主题入口的交付边界，产品决定归DEC-026；历史候选14及其通过范围不改。已查看用户七张截图，重新读取C的HTML、model/library/scenes及设计说明。浏览器工具本次初始化因沙箱ACL失败，未声称重新操作过浏览器。

| 用户要求 | 当前代码（待新候选运行证明） | 验证入口 |
| --- | --- | --- |
| 日常无高亮描边/右下锚点，仅编辑态有 | `PosterVisualWindow.Content.cs`按IsEditing绘制和发布handle；`ContentEditing.cs`拒绝非编辑态旧handle，普通卡片仍直接拖 | Daily/Protocol各14场景，读真实Border/14pxhandle/已发布input表 |
| 新建两辅助默认关；中心整体统一风格 | `ContentLayout.cs`两默认false、旧显式值保留；`ControlCenterWindow.xaml`通用纸面Button/TextBox/ComboBox/Expander、所选项目与分页；主题资源统一 | 5项SQLite保真case；UI宽窄、参数、配方截图待跑 |
| 海报下方提示仅测试启用 | `ContentFeedback.cs`日常不创建callout、隐藏footer；`Start-DesktopUpFixtureProcess`显式子进程开关，普通review强制0；运行信息/DEMO构图不在日常中心显示 | PS5.1与PS7父环境×显式开关各4项通过；Daily与诊断Protocol分别待跑 |
| 装入、留副本、分页及完整弹窗，删除AI说明 | `ControlCenterWindow.TemplateRecipe.cs`按C纸面流程接JSON文件/粘贴→检查→装入并使用；副本用当前材质/参数作默认，保存不自动切换；真实模板册/筛选/分页 | `probe-template-recipes.ps1`真实按钮与控件、7张PNG及同库第二进程待跑 |
| Lamp跟随与离开轻摆、配方声明动态参数 | `TemplateParameterSchema`增加scene.motion/lamp.follow/lamp.sway/lamp.speed，通用Color/Range/Boolean控件；`LampScene`/`Runtime.LampMotion`共享一个33ms帧源、被动指针采样 | 纯模型/paint-only合同通过；33步Lamp实际帧/处理器输入/暂停/资源门待跑。闲置轻摆来自本次用户新增，C原型仅离开回中心 |
| 中心主题入口 | `ControlCenterWindow.Theme.cs`纸白/墨色/深海/跟随系统，现有Settings命令与SQLite保存；只改中心 | theme不改海报/重启合同通过，实际控件与重启门待跑 |

数据链：`TemplateRecipeCodec`有界解析→`RegisterTemplateCommand`完整读集CAS→同事务注册+可选选中→`PosterSetting.TemplateParameters`→逐海报/逐模板覆盖→`ResolvedPosterPresentation.TemplateParameters`绘制。SQLite v12增加模板册和中心主题；原始内置Lamp覆盖与custom实例分离。`TemplatePresentationRefreshPolicy`确认仅参数变化时直接更新绘制，定义/结构变化仍重建。失败、重复ID、过期候选、草稿取消不写；旧命令收据的默认settings哈希保持。完整数据库备份可携新字段；现便携格式遇custom配方或非默认中心主题会明确拒绝，尚未扩其格式，不静默丢数据。

当前支持minimal@1/lamp@1的新ID声明配方，Color/Range/Boolean，最多20参数/64模板、单配方64KiB。没有提供雨场景renderer、select参数、在线AI、目录watcher、同ID版本替换或任意渲染脚本；装入示例为可运行的暖夜书桌，不用未支持的薄荷雨样例假冒可用。模板数按真实3张内置及用户装入/副本显示，不伪造13张。

构建与证据：`out/lr-r5-evidence/candidate16-ui-c-feedback`，App.dll SHA256=`70705C6B5E3525DFA37DF154AB6A77CD113706C223A6E99D819E109D7CA23432`；manifest=`9A373589390511897C11A55E1C81DA791C4BB0F57ADC38FCC0E0D1255977B72C`。427源码/配置指纹与401产物逐项一致，`ui-c-feedback-full-final-01` **2065/2065**，最终App/fixture构建0警告/0错误；`ui-c-feedback-build-summary-v16.json`汇总。首次定向141/142的降版夹具duplicate-column、首次App编译错误保留原日志，最终修正通过；不覆盖失败材料。

用户候选14体验PID9460仍运行，已请求正常退出或允许代退出，尚未收到答复；没有关闭进程、迁移体验库或运行新GUI门。下一步只在旧实例正常退出后，用候选16及全新隔离DataRoot跑Daily、Protocol、fields、rejected、UI、Lamp、composition、recipes/restart，查看真实PNG再交付。所有agent已冻结交root。R5仍IN PROGRESS，R6未开始；不能把编译/2065合同写成UI C或NativeMouse通过。

### 8.1 候选17同包运行与体验交付

上述等待状态为历史。用户确认旧实例已关闭，root确认进程0后串行运行。v16实际暴露两处检查问题：日常Border的0.8 DIP经XAML读回为0.800000011920929，严格double等号误拒；Lamp共享timer误记在单海报ledger，真实帧推进但根ledger计数0。前者只改诊断容差与先写readback，锚点计数不放松；后者把共享timer/系统设置订阅改为应用级根ledger，零需求仍释放资源，重开保留依赖。v16全部结果保持原样。

当前为`out/lr-r5-evidence/candidate17-ui-c-feedback`，App SHA256 `4BBBC824BCD4A19D93EB985E0742D44B655FDC2A1CCB63CB58CCC91C35F6637A`、manifest `4E275E30EF7D26DD2E4E98D451607EF79817ADB9C98172371BE6B1A48F179FE0`。427源码/401产物复核一致，App构建0警告/错误。2065/2065合同沿用最终通过材料，此后只有App/probe/启动脚本7文件变动，合同输入不变。汇总为`alignment-v17-summary.json`。

| 六项要求 | 当前取得的证据 |
| --- | --- |
| 日常/编辑标记分离 | Daily和Protocol各14case/14图+重启；真实读回日常厚度0.8、可见与发布handle均0→编辑厚度2、handle各1→退出回0；普通卡片拖动仍直接可用 |
| 两辅助默认关、统一中心风格 | 既有5项SQLite默认/显式值保真；新UI五态和字段337检查/4图；实际查看宽窄样片夹、所选项目与精调样式 |
| 提示仅测试 | Daily不创建callout、footer不可见；Protocol/构图显式开启诊断；普通review启动强制0，父环境不泄漏 |
| 装入/副本/分页，无AI说明 | 实际按钮/控件驱动导入校验/新ID注册与选中、重复/非法拒绝、留副本不自动切换、模板册搜索/分页；7图+同库第二进程保真，原生picker尚未驱动 |
| Lamp动效与声明参数 | 33步/9图，10参数；共享帧211→225且timer恰1，生产paint处理器追光与离开轻摆、实例开关、编辑/全局暂停/收纳/挂起资源释放通过；处理器输入不冒充系统鼠标 |
| 独立中心主题 | 纸白→墨色真实控件与截图，主题不改海报，第二进程保持；完整桌面高对比/多DPI未验 |

非法有效预览拒绝、100项整图构图也有同包第二进程保真。八组通过记录共13进程exit0、错误0、资源0。构图第一次`alignment-v17-composition`因原生`GetWindowRgn=0/window-region-read-failed`安全取消；`alignment-v17-composition-recheck`独立通过不替代失败。登记[BUG-021](KNOWN_ISSUES.md#v3-bug-021尺寸动画窗口区域读回间歇失败)，具体原因未明，不能称所有几何动画稳定。

已实际查看PNG。样片是原生绘制的实际三款，纸页签/胶带/画面文案/真实页码/动作归属已调整；仍不是HTML逐像素复刻，也没有把缺少的雨场景或13张素材假充已交付。XAML不包含系统背板与窗口标题，最终桌面合成和原生输入仍需相应检查。

PS5.1与PS7八证据绑定CheckOnly通过，启动器同时验证Daily与Recipe；历史六参数调用仍兼容。已用PS5.1启动`human-review-03/data`，PID26864，正常权限、控制中心已创建/显示、启动错误0。入口`out/lr-r5-evidence/start-ui-c-feedback-review.ps1`可在关闭当前实例后复用；`human-review-02`未改。当前实例留给用户，不再运行会冲突的自动门。

R5保持IN PROGRESS、R6 NOT STARTED。新UI体验、原生命中与文件选择器、BUG-021处置未完成；雨renderer/select、版本替换/watch/在线AI仍不在已交付范围。全部agent冻结交root，没有Git提交/推送。
