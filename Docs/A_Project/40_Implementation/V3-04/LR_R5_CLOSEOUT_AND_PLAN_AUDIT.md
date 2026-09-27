# LR-R5 收口与代码 / 计划对照

日期：2026-09-27。依据：用户认可当前UI、动画及此前基础操作，确认滚动大致可用、停顿问题延期，并要求继续R5收口；授权登记为[DEC-029](../../10_Requirements/REQUIREMENTS_RISKS_AND_OPEN_QUESTIONS.md#31-已明确的近期优先级)。本报告是阶段证据与差异清单，不另建产品路线。

## 1. 收口结论与基线

**LR-R5 CORE CLOSED / 带明确遗留项的阶段收口**：原定“首个可操作的UI C版本”已具备中心调参、纵向有限两模式直接操作、正式保存、中心关开及同库进程重启证据；UI/动画已有用户认可。此结论允许进入R6，不等于R7产品可靠性验收、所有循环操作或所有原生环境通过。

- 工作树：`impl/poster-redesign-V3-wb`，HEAD `bd148fb4641bccb18d75777ae6645149e3ff9cfc`，保留既有未提交改动。本轮只改文档，不提交/推送、不改生产或测试代码、不操作体验库。
- 候选：`out/lr-r5-evidence/candidate25-loop-wheel`；App SHA256 `1FE34E3121EBDF3104753510294768D282F160517511A83C64D9D3E4BDA9D6CA`；manifest SHA256 `EE463C5898F8C7A9345BACEA3212616FB8AD39A2694F937CA5DE958C539F2C59`。
- `source-loop-wheel-v25.json`记录445源码，manifest记录402运行产物；本次复核源码零漂移，`start-loop-wheel-review.ps1 -CheckOnly`成功。未新建候选、未重跑2115合同或八组GUI进程门；下列测试结果均为已存在的绑定证据。
- 自动报告中的`UiCProductAccepted=false` / `MilestoneAccepted=false`保持原样：探针不能替用户或阶段评审作决定。本报告把代码、自动、真实输入及用户反馈分别列出，不补改旧证据旗标。

## 2. 原R5出口逐项核对

候选25证据根为`out/lr-r5-evidence/`。详细运行过程和旧失败归[输入报告§9](LR_R5_POINTER_AND_MODULE_REVIEW.md#9-候选25循环起点反向滚动与主线审阅节点)，E1意图追溯归[UI C对齐记录](LR_R5_UI_C_INTENT_ALIGNMENT.md)。

| 原出口 | 已有实现与证据 | 结论及不能外推的范围 |
| --- | --- | --- |
| 中心选择 / 调参 / UI C核心工作区 | `loop-wheel-v25-ui`五态；`fields`338检查；`recipes`覆盖装入新ID、留副本、分页、四主题及重启；用户认可当前UI/动画 | 核心界面已交付。不是原生文件选择器、高对比、混合DPI认证；不要求重选C |
| 首次直接操作 / 两模式 / 取消撤销 | `daily`、`protocol`各15场景，自动首拖读序图钉、手动位置、跨度、撤销重做、返回A、原位与非法松手；早期人工基础组通过 | R5有限路径成立。候选20另有真实NativeMouse链和图钉/灯光正向体验；候选25未重新执行OS鼠标门，循环副本拖动仍归R6 |
| 保存 → 中心关开 / 同库进程重启 | `fields`取消/重开及已保存字段；`daily/protocol/rejected/recipes/composition`有第二进程，八组门合计13进程通过且退出资源0 | 有实际进程重启证据。`fields`自身没有进程重启测试，不把各组结果拼成所有配置全链已通过 |
| 草稿、冲突与目标隔离 | `fields`非法/过期文本、唯一草稿与延后提案；`rejected`无效有效布局零写入与重启；`lamp/recipes`双海报隔离 | 失败不覆盖及独立目标有证据。DEC-026规定日常不显示测试提示，不恢复调试文案充当体验反馈 |
| E1 04/05构图影响和恢复 | `composition`100项缩窄→不编辑放宽恢复→整图/屏外影响预告→松手一次承接→放宽→一次撤销→重启 | 正常路径成立；BUG-021区域读回偶发失败及安全取消保留，不能由成功重跑关闭 |
| 代表模板 / 动效与资源 | `lamp`33步骤、10参数、9张图；编辑/暂停/收纳/挂起及关闭清理；真实追光见候选20；灰底过程原生证据见候选24；用户认可灯光和当前动画 | Lamp已超出R4静态六参数样板。Rain尚未实现；不称所有GPU帧、HDR或多屏已验 |
| 普通循环浏览补修 | 候选25旧算式8/10失败→47相关/2115全量通过；两轴12场景102共享Scrolled事件/984检查，卡片与发布命中一致 | 起点反向预夹紧/小增量问题有红绿证据。用户新报停顿后失效另列BUG-026并延期，不称循环完全可靠 |

本阶段保留BUG-019/020/021/022等明确限制，进入R7时逐项按核心承诺审查；不是默许发布。R5原先“所有界面均未对齐”“只读配方/静态Lamp”“主线待方向审阅”的描述已有后续事实或DEC-029取代，下文历史记录不删除。

## 3. 代码与计划的差异审计

审计范围：LR内容模型、App输入/布局/绘制、模板配方与设置持久化、便携边界、代表测试和当前导航。未对全仓所有旧模块做安全/性能审计，也未新增原生测试。

| 发现 | 当前代码 / 证据锚点（项目相对路径） | 对齐处理 |
| --- | --- | --- |
| 文档落后：BUG-009仍称两轴容量不对称 | `src/DesktopUp.Domain/ContentLayoutSolver.cs`的Axis与区域全局坐标；`ContentLayoutSolverTests`的`Both_axes_support_one_thousand_maximum_span_items_without_squeezing`和旧区域两轴用例 | 登记SOURCE+MODEL修复，App横向组合另验，不重复排R3模型修复 |
| 文档落后：BUG-010仍称遗漏高度键 | `src/DesktopUp.App/PosterVisualWindow.Content.cs/EnsureContentSnapshot`检查HeightDip并恢复锚；`ContentLayoutSolver.Reclip`横向高度见证 | 改为部分修复；单改高度与R4连续原生链仍归R6 |
| 文档落后：BUG-011/013/015旧机制仍OPEN | `PosterVisualWindow.ContentEditing.HandleContentInput`已允许自动未钉项；`ContentLayoutFlowPolicy.SetFlowAxis`版本+1且保留约束；`ContentLayoutProposals.ResolveHeat`遵循尺寸开关/人工覆盖 | 按有限App路径、命令合同、注入热度模型分别标证据；不扩大成循环、全部OS或真实统计通过 |
| 旧Bug预期失效：BUG-012要求恢复自动时全清 | `ContentLayoutEditing`保存/恢复SavedAutomaticState；`ControlCenterWindow.ContentLayout`独立图钉/尺寸恢复 | 依DEC-020/023/024改记SUPERSEDED；旧Manual无来源及硬约束仍保留/拒绝，不能为了“修Bug”清数据 |
| 文档落后：R3存档能力/只读写入限制仍写未完成 | `ContentLayoutCapabilities.Inspect`与ArchiveCapabilityTests；`ContentLayoutEditing.Apply`只读门及`RequireWritableContentProjection`；R5 rejected门 | R4/R5已解决相应能力和安全写入门；原R3描述标为历史 |
| 已授权前移：装入/留副本与动态Lamp | `ControlCenterWindow.TemplateRecipe`、`TemplateRecipeCommands`、`TemplateParameterBindingAdapter`、`LampSceneMotion`；DEC-026及R5 recipes/lamp门 | 将R4“静态六参数”、早期R5“只读/无导入”限定为历史；当前10参数、新ID配方、独立中心主题已交付，不算越权超前 |
| 文档落后：SQLite写成schema11，Portable被泛称完整往返 | `src/DesktopUp.Infrastructure/SqliteStateStore.cs/CurrentSchemaVersion=12`；`src/DesktopUp.Application/PortableProfileCodec.cs/Export` | R4报告加版本范围：当前模板目录/中心主题已持久化；自定义目录、非light中心主题或RecipeInstances存在时Portable v4明确拒绝，完整数据库备份为保留途径。R7前须补受支持新字段的版本化携带；拒绝是安全底线，不代替完整往返验收 |
| R6真缺口：运行中循环拖动/边缘导航 | `ContentDragPolicy.Map`拒绝全部实际循环副本（含cycle0）；`ContentEditing.FlushContentMotion`仍有限钳制/偏移差 | R5只关有限核心门；R6需接冻结抓点、未折返累计导航、单身份提交及取消/保存锚恢复，不能只移除拒绝条件 |
| R6真缺口：被动不足 / 主动最小值 | `ContentSpacePolicy`有模型；App未消费该策略和ReadOnlyOverflow，普通Reclip仍单轴；外框未接最大完整Item的MinimumCrossAxisDip | BUG-016记PARTIAL；R6分别接主动准入、被动有限双轴只读、恢复与最后成功发布见证 |
| R6/R8真缺口：可见资源与假源 | `PosterVisualWindow.ContentAssets.SyncContentAssets`按canonical纵向overscan；当前`DemoContentItemViewSource` / `NullContentAssetProvider`；`ContentActivation.DispatchContentOpen`仅DEMO意图 | R6补按tile去重与假源变化；R8交接端口；真实打开仍归V3-05，不将DEMO当已接真实文件 |
| 观测器部分已前进 | 旧`ContentSmoke.ReadContentSurfaceForSmoke`按ItemId取控件；新`App.ContentBrowseProbe`按TileKey，Transition亦按tile | 候选25浏览门可复用，旧finite观察器需按R6场景补齐；不重复发明全新探针，也不把旧门泛用到接缝拖动 |
| BUG-017指向旧失配候选，不能当当前全部候选失效 | 原布局候选失败材料保留；候选25的manifest/八组证据和445源码复核通过 | 旧入口继续禁用，登记替代候选已验证；不修改旧hash或把新证据转授旧包 |
| 最小解耦已批准但未实现 | `TemplateRecipeInstance`直接存LampTemplateParameters；`PosterVisualWindow`直接调用Lamp生命周期，Backdrop依赖CurrentLamp；codec仅minimal/lamp | B/C/D负责通用参数、能力注册和可物理删除模块；在隔离副本排除Lamp仍编译/启动才算完成 |
| 雨夜窗边已排期但未实现 | UI C HTML/`content-layout-controls-c-model.js`、`-scenes.js`有rain原型；生产codec无rain能力 | 排在解耦之后作第二个实际模块样例；不把原型样片或新增计划算成生产模板 |

审计未发现需要撤销的未授权功能；发现的是DEC-026之后的文档滞后、旧需求被取代，以及明确属于R6/模块/Rain/R7/R8的未完成项。保持代码和原型，本轮对齐计划与事实，不以改文档掩盖真正缺口。

## 4. 遗留项与下一实施入口

- **用户延期**：BUG-026停顿后续滚失效，不阻塞R5，不在本轮修；BUG-001/004/006原处置保持。
- **R7前明确审查**：BUG-019并发初始化、020菜单完整命中边界、021尺寸动画读回、022桌面空白真实手势；当前证据层级见[Bug清单](KNOWN_ISSUES.md)。不能用本报告关闭它们。
- **R6首单元**：CA-01/02/07、PD-10的两轴循环抓取及取消/保存。修改入口为`ContentDragPolicy`、`ContentEditing`、统一Flow及tile观察；先覆盖extent980/view300/period1000/offset950、副本70→95/抓点10反例，随后生产链验证。浏览BUG-026继续延期，接缝拖动不得偷用修该Bug作为前置。
- **后续顺序**：R6组合→最小模块B/C/D→雨夜窗边→R7→R8→V3-05，里程碑唯一维护在[主计划§8](../../30_Plans/V3_04_L_REMEDIATION_REVIEW_PLAN.md#8-当前出口和最近下一步)。模块/Rain已获授权，尚未制作，不需要重复询问已定方向。
- **共享工作边界**：root持有全部文件及唯一构建/GUI队列；本轮两名审计agent只读、已交回，无在写文件。后续分派要先指定文件与接口，不继承历史分工。复用唯一状态权威/CAS/R4/输入岛；新候选、隔离DataRoot，不覆盖旧包/用户库。

可复制交接：DesktopUp是桌面海报式收纳入口，当前仍DEMO。基线`impl/poster-redesign-V3-wb@bd148fb4641bccb18d75777ae6645149e3ff9cfc`加既有dirty；候选25指纹见本报告§1。先读DEC-029→LR计划§8→本报告/HANDOFF。R5核心已收口，R6实现未开始；模块解耦获准、雨夜在其后。BUG-026按用户决定延期；019/020/021/022及Portable能力缺口带入后续验收。保留配置/旧证据，不自动Git同步或执行真实文件搬移。
