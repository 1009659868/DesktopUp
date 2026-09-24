# 04-L 内容布局审查：事实、问题与整改方向

日期：2026-09-23。状态：**REVIEW / DOCUMENTATION ONLY；未修复、未形成新验收包**。

归属：C 层实现审查证据，不是新增需求权威。产品决定统一登记于[需求风险与决定索引](../../10_Requirements/REQUIREMENTS_RISKS_AND_OPEN_QUESTIONS.md)。阅读顺序：本文 → [整改设计 V2 草案](../../20_Architecture/CONTENT_LAYOUT_REMEDIATION_DESIGN_V2.md) → [整改评审与分期计划](../../30_Plans/V3_04_L_REMEDIATION_REVIEW_PLAN.md)。Bug 状态只登记于[统一清单](KNOWN_ISSUES.md)，本文维护根因与证据，不另建关闭状态。

## 1. 审查目标和范围

目标是稳定、好用的通用桌面内容收纳工具；海报构图、跨格、热度和主题服务于收纳、查找、使用和整理，不把用户变成布局算法操作者。保留多海报、唯一控制中心和托盘；不以简化为由取消已要求的跨格、单轴、循环及两个独立辅助开关。

本次核对源码、需求/合同/计划、HTML 设计稿代码、本地 DeskBox，并运行隔离的 Domain/Application 诊断。未改生产代码、数据库、候选包、启动脚本或测试；未运行 DesktopUp GUI，未代填真实鼠标/像素验收。设计稿按 HTML/JS 实现审查，浏览器自动查看未成功，不能称本轮重新完成视觉检查。

证据等级：S = 源码直接确认；M = 当前源码的隔离纯模型/命令策略输出；N = 尚需原生进程/人工验证；D = 设计取舍而非已证实运行故障。M 不等于产品全套测试通过。

## 2. 代码与回退基线

- 仓库：G:/A_/Tools/DesktopUp_V2；当前分支 impl/poster-redesign-V3-wb。
- 当前 HEAD 与本地 impl/poster-redesign-V3 均为 9607d4e9e7fde7503b5990ea9bf327621a8bcf7b；提交标题 feat: add editable content layout foundation (04-L01..06)。两分支已提交差异为零，不能把工作区实验当成另一条已提交实现。
- 本轮整理前：26 个 tracked 文件修改；另有 4 个未跟踪代码/测试文件及 2 个 .workbuddy 笔记。后者只是历史线索，不是任务指令或验收证据。
- 主要实验涉及：布局轴/循环、编辑和恢复自动、便携格式 v3、布局 schema v2、SQLite schema11、候选指纹及相关测试。未提交文件必须随未来备份一起保存；单独保存 git diff 会遗漏未跟踪文件。
- HEAD 是可追溯代码检查点，不等于其所有交互已通过人工。旧 R4/旧 1630 合同及 35 进程门只证明对应产物和范围。
- 文稿不是源码或数据库备份。实施前需独立保存 tracked/untracked 差异、原始配置和包/日志；本轮没有执行 reset、checkout、commit、push 或覆盖旧包。

### 2.1 关键工作区指纹（SHA-256，审查输入）

| 文件 | SHA-256 |
| --- | --- |
| src/DesktopUp.Domain/ContentLayout.cs | 2428CB4B62C1398773A1F1401C07A35077A08F5B0479DF69046D25335EF42A81 |
| src/DesktopUp.Domain/ContentLayoutSolver.cs | 19A0381F3EF57EC4E41527A6B41463265B2EA961C1B7DFED1CC61DC068583508 |
| src/DesktopUp.Domain/ContentLayoutEditing.cs | 9A20F7A9F6F8649AA73FADF3B43CDF151A3394E6D6499294EA9DD97B3A6F28E6 |
| src/DesktopUp.Domain/ContentLayoutFlow.cs（未跟踪） | FD19E432AFE7A693429C67879EDA9941C00F73752E560C36220AB0C2529D6DE7 |
| src/DesktopUp.Application/ContentLayoutFlowPolicy.cs（未跟踪） | 2C402671D312BF79CCFB7D8936CA4C7E9BE3C716EF25E1A9E8152D1F887F98FD |
| src/DesktopUp.Application/PortableContentLayout.cs | 19F5258F0EAE3DFA2DC7CC5CAC484D8898A9506B9DEB1F60175893AC8F4B4F40 |
| src/DesktopUp.App/PosterVisualWindow.ContentEditing.cs | 15A091DEEB3DB9F4161166BE229CC00D84AB8D01506368C93297E64C433BC27A |

## 3. 问题清单、根因及整改方向

| 审查项 / Bug | 已确认问题与机制 | 影响 | 整改方向 |
| --- | --- | --- | --- |
| LRA-01 / BUG-008 | S/M：Reclip 用 FlowAxis==HorizontalLoop 决定不限制 X、限制 Y，未使用独立 LoopScroll；滚轮先 Max(0) 后取模、每事件取整 | 横向有限可滚空；纵向循环卡在末端；短内容可偏移；不能反向跨接缝；小增量丢失 | 单一滚动状态机，轴向与有限/循环分离；实际可循环资格统一；有符号累计；有限 clamp 与循环 normalize 互斥 |
| LRA-02 / BUG-009 | S/M：横向单独求解，忽略 region 偏移，并沿用 64 列作为全条带上限；纵向可使用 65535 行 | 合法区域误碰撞；相同数量切方向可能无法呈现 | 主轴/交叉轴统一坐标约定、区域变换、容量预算；失败保留 last-good，不能静默空白 |
| LRA-03 / BUG-010 | S/M：宿主 geometryChanged 判断宽度/scale/spacing，遗漏横向依赖的高度；只改高度走 Reclip | 旧行排列被裁掉，之后其他变动才重排；自动收展与缩高组合有风险 | 明确布局缓存键和 R4 中间帧租约：平时交叉轴变化重算，动画中稳定预布局、终态一次恢复；不能简单每帧重排 |
| LRA-04 / BUG-011 | S：纯 Move 可自动转位置固定；实际 canEdit 仍要求 Manual/Fixed，旧提示要求先进入手动 | 鼠标与中心键盘/纯模型行为不同；文案承诺“直接拖动”不可达 | 同一意图仲裁与编辑许可；整理态与持久模式解耦；真实首次按下→松手→命令链验收 |
| LRA-05 / BUG-012 | S/M：Anchored→Automatic 只变 Mode；RestoreAutomatic 清覆盖逻辑仅在退出 Manual 时执行 | “恢复自动”仍保留位置、尺寸和序号约束；再次执行可能无效 | 明确恢复范围；独立语义命令，不借旧模式转换模拟；所选项/整张分别提示和撤销 |
| LRA-06 / BUG-013 | S/M：手动切轴先转换顺序锚并版本+1，再切轴版本+1；原 command 要求恰好+1。提案同时清坐标固定 | 提案成功但真实命令拒绝；若只修版本还会丢构图意图 | 一次语义提案一次版本；先评审切轴约束迁移，不借机全量解除固定；预览和持久提交共用结果 |
| LRA-07 / BUG-014 | S/M：便携 Export/Import 无条件把 ReservedSpaces 清空 | 已有留白配置经导出/导入失真；不等于真实文件丢失 | 保留受支持原文或显式迁移/拒绝；UI 暂缺不能成为静默丢数据理由；分离装饰和硬留白 |
| LRA-08 / BUG-015 | S/M：AllowHeatDrivenSize=false 未被 ResolveHeat 检查，注入提案仍改跨度 | 接入真实热度后可能违背开关与人工意图；目前没有真实统计提供者 | 排序和尺寸资格分别裁决，关闭尺寸热度只禁尺寸提案；保留原始统计；注入→生产安全点→投影联测 |
| LRA-09 / BUG-016 | S/M：最小高度只在求解器内部抬高，真实 viewport 不变；有效跨度压缩与 L08“不夹窄”承诺不同 | 可读下限不等于可达；首选5格显示4格；横纵/固定项规则不对称 | 拆分主动外框缩放、宏观尺度、单项跨度、被动受限；计算完整度量最小值及冲突结果；产品选择先于算法 |
| LRA-10 / BUG-017 | S：候选脚本的指纹与当前目录二进制不匹配；CheckOnly 实际拒绝 | 无法把当前源码、新测试、旧35门、人工入口视为同包证据 | 独立候选目录，构建后一次封存 manifest；后续不覆盖；历史 PASS 不转授 |
| LRA-11 / DESIGN | FixedPosition、PinnedIndex、ManualSize、三模式及整理许可混合；固定提示依附跨度标签 | 用户不知图钉保护位置、顺序还是尺寸；关闭辅助可能丢失状态提示 | 唯一主语义、逐项覆盖与清楚标记；旧顺序锚只兼容不静默改义；见设计 V2 待决表 |
| LRA-12 / DESIGN+N | 浏览锚为纵向；绘制有循环副本，抓取输入不完整携带副本上下文；资源预取仍按原始矩形 | 接缝拖动/内容变动可能跳位；未来图片副本可能缺预取，尚未作原生复现 | 浏览、命中、选择、可见素材共享投影；整理时冻结循环，抓取保留稳定ID/代次/位置见证 |
| LRA-13 / DESIGN | 同时要求固定位置、保尺寸、不交叉滚动和任意缩小，未定义不可满足时优先级 | 靠补丁切换压缩/拒绝/裁切，用户无法预测 | 约束优先级及主动拒绝/被动安全视图；不为内部排版推动其他海报 |
| LRA-14 / PROCESS | 测试部分断言只验证辅助函数；文档 L08/09 未开始但已有实现，旧 schema/Shift+wheel 仍作现状描述 | 单元绿灯遮蔽操作链断裂；难以回退和判断完成 | 测试按用户链和中间状态组织；追溯问题→模型→命令→包→人工；旧文字明确标日期/适用产物 |

### 3.1 代码证据索引

- LRA-01/02/09：[Solver](../../../../src/DesktopUp.Domain/ContentLayoutSolver.cs) 的 ResolveHorizontalLoop / Reclip；[Flow](../../../../src/DesktopUp.Domain/ContentLayoutFlow.cs) 的 IsLooping / ProjectTiles；[内容宿主](../../../../src/DesktopUp.App/PosterVisualWindow.Content.cs) 的 ScrollItems / EnsureContentSnapshot。
- LRA-04/05：[输入编辑](../../../../src/DesktopUp.App/PosterVisualWindow.ContentEditing.cs) 的 HandleContentInput / EditContent；[纯编辑](../../../../src/DesktopUp.Domain/ContentLayoutEditing.cs) 的 Move / Mode；[中心入口](../../../../src/DesktopUp.App/ControlCenterWindow.ContentLayout.cs) 的 ContentRestoreAuto_Click。
- LRA-06：[FlowPolicy](../../../../src/DesktopUp.Application/ContentLayoutFlowPolicy.cs) 的 SetFlowAxis；[命令校验](../../../../src/DesktopUp.Application/ContentLayoutCommand.cs) 的 Prepare。提案中的意图丢失与命令拒绝是两层证据，不能宣称该提案已在真实库落盘。
- LRA-07：[PortableContentLayout](../../../../src/DesktopUp.Application/PortableContentLayout.cs)；LRA-08：[ResolveHeat](../../../../src/DesktopUp.Domain/ContentLayoutProposals.cs) 与 [安全点](../../../../src/DesktopUp.App/PosterVisualWindow.ContentProposals.cs)。
- LRA-12：[素材预取](../../../../src/DesktopUp.App/PosterVisualWindow.ContentAssets.cs) 与 AddContentInputRegions；目前 DEMO 没有真实 AssetKey，不把未来素材风险写成已观测丢图。
- LRA-14：[FlowTests](../../../../tests/DesktopUp.Contracts.Tests/ContentLayoutFlowTests.cs)、[PolicyTests](../../../../tests/DesktopUp.Contracts.Tests/ContentLayoutFlowPolicyTests.cs)。

## 4. 隔离诊断结果（2026-09-23，上一审查轮实测）

方法：.NET SDK 10.0.103；临时 console 链接当前 Domain/Application 的 .cs，排除 bin/obj；不调用数据库、WinUI、Shell 或文件收纳。临时项目位于 C:/Users/allmightwen/AppData/Local/Temp/desktopup-layout-audit-20260923，临时路径不保证长期存在；下表保留可重建输入和输出，不将它注册成正式测试入口。

共同默认：PosterId=audit、cell24/gap6、Automatic、4×4项、视口300×300；ID为 audit:序号。下列15项是诊断记录数量，不是15项产品 PASS。

| 用例 | 变化输入 / 调用 | 输出 / 判断 |
| --- | --- | --- |
| P01 axis_real_command | 1项 Manual，位置(4,3)、Fixed=true；SetFlowAxis(Horizontal) 后 Prepare，匹配读集/版本 | 提案 Accepted=true，DocumentVersion 1→3；命令拒绝 exactly one semantic version |
| P02 horizontal_finite_end | 10项、Horizontal、Loop=false、ScrollX=10000 | ExtentWidth594，合法有限末端294；实际ScrollX10000，可见tile0 |
| P03 vertical_loop_seam | 10项、Vertical、Loop=true；Reclip 请求 period−30 | period600，请求570，实际294（有限末端） |
| P04 horizontal_100_items | 100项、Horizontal、300×300 | 抛异常 exhausted bounded column range |
| P05 vertical_100_items | 同一100项、Vertical | 成功返回100项（对照） |
| P06 restore_automatic | 1项 Anchored，Fixed=true、ManualSize=true、PinnedIndex=1；Mode Automatic + RestoreAutomatic | Accepted=true；三项覆盖仍保留 |
| P07 heat_disabled | 1项，AllowHeatDrivenSize=false；ResolveHeat(RelativeScale=3) | 原跨度4，投影跨度12；不是已接真实热度的证明 |
| P08 portable_keepout | 1项＋Explicit留白(20,0,4,4)；Export→Import | 留白数量1→0→0 |
| P09 axis_change_pinned_manual | Manual固定项(4,3)，调用SetFlowAxis | 提案取消Fixed和坐标，写PinnedIndex=1；不代表已提交 |
| P10 squeeze_wide_card | 首选5×5，viewport114×300 | 有效4×5，bounds114×144，squeeze-single-column |
| P11 short_horizontal_loop | 1项，Horizontal、Loop=true；使用生产滚轮公式前进一格后Reclip | IsLooping=false，但ScrollX30；114宽卡片只可见84 |
| P12 region_local_coordinates | main偏移0、side偏移5，各1项局部(0,0)、Manual、Horizontal | 合法区域局部位置被当成相同全局位置，报冲突 |
| P13 viewport_below_min | 1项、Horizontal、viewport300×20 | Bounds.Height54，Clip.Height20，却记录viewport-height-clamped:54 |
| P14 production_wheel_arithmetic | 原点反向−30 DIP；每次wheelDelta幅度30（1/4格） | 前者被Max归零；后者每次Round归零，未累计 |
| P15 height_change_reclip_vs_resolve | 10项Horizontal；高度300→120，比Reclip与重新Resolve | 旧rows交替0/4、extentHeight234；新rows全0、extentHeight114；宿主遗漏高度时保留旧排列 |

### 4.1 实际候选检查

执行 scripts/start-content-layout-manual.ps1 -CheckOnly：失败于 out/verify/v3-04-l-r4/app/DesktopUp.Application.dll 指纹不一致；未启动 GUI。先前审计还记录 app/DesktopUp.dll 及 fixture/DesktopUp.Application.dll 不一致；不修改 manifest 去绕过拒绝。

### 4.2 为什么已有测试没有拦住

- ShortContentIsNeverDuplicated 没开启 LoopScroll，不能覆盖循环开启时的短内容偏移。
- NarrowingWrapsCardsInsteadOfShrinkingOrHidingThem 只核对 authored span 未变，不核对 effective span 保持。
- HorizontalAxisClampsToMinimumUsableRows 检查 Bounds，不检查实际 Clip 是否完整可达。
- ContinuousScrollingNeverDrifts 只测取模函数，未经过 ScrollItems→Reclip→ProjectTiles。
- 手动切轴用例只检提案，不检 Prepare / repository 的单版本提交；也没有带原Fixed=true检查保真。
- 图钉和拖动测试须覆盖真正 Press→Preview→Release，而非直接调用编辑命令后读回。

## 5. 当前行为和操作链（AS-IS，含断点）

| 用户意图 | 当前实际链 | 不一致/边界 |
| --- | --- | --- |
| 管理海报 | 中心/托盘新增、中心或海报菜单确认删除→目标化目录命令→CAS→会话创建/释放→三端投影 | 多海报/零海报路径已有；删除海报不等于删除源文件；具体历史通过范围按原交付 |
| 移动、锁定或显隐海报 | 标题拖动/菜单/中心→原空间或状态命令→输入权限/窗口投影；全局隐藏保留逐张显隐意图 | 不属于内部Item排布；锁定障碍、碰撞、推动/归位沿原规则；标题BUG-001未修 |
| 中心/托盘使用 | 双击托盘打开唯一中心；中心选目标/定位/调参数；托盘只六类整体入口→原命令或生命周期通道 | 不新增托盘单张编辑器；中心位置记忆BUG-006仍延期 |
| 浏览 | 输入岛Scrolled→ScrollItems按轴换算/取整→EnsureContentSnapshot→Resolve或Reclip→ProjectTiles→Canvas/输入表 | 有限/循环分裂；透明装饰穿透、键盘和精细滚轮不能由纯模型测试代验 |
| 整理卡片 | 中心或海报菜单进入IsEditing→选项→canEdit门→16ms最新移动/边缘滚动→ContentLayoutEditing→草稿→松手命令→CAS→投影 | 整理态不等于Manual；自动未固定项被鼠标门挡住，但中心Nudge可走新Move模型 |
| 放大某项 | 选中→右下把手或中心跨度/预设→Resize→ManualSize=true→草稿/命令 | 单项跨度、宏观scale与外框resize是不同数据；当前受限投影仍可夹窄 |
| 固定/解除 | 位置固定设置FixedPosition/坐标；顺序锚另有PinnedIndex；两者受Mode影响 | 一种图钉心智面对多种隐藏状态；解除固定不解除尺寸覆盖 |
| 恢复自动 | 中心ContentRestoreAuto→Mode(Automatic,RestoreAutomatic) | Manual退出才清覆盖；Anchored只切枚举；已Automatic可能no-op |
| 切方向/循环 | 中心→FlowPolicy→CommitContentDocument→原命令/CAS→宿主重绘 | 手动切轴提案版本+2被拒；循环开关持久化成功也不代表滚动链正确 |
| 改海报外框 | 原空间运行器碰撞/事务/R4→内容viewport变化→Solve/Reclip | 内部不应写外框；高度失效/固定横溢出/最小边界未统一 |
| 收展/自动收展 | 原手动/自动意图→同一空间策略→组呈现R4→终态恢复 | 本次不重写该策略；内容高度改变与布局/浏览锚需要新组合验证；BUG-001仍延期 |
| 改宏观外观 | 中心滑块预览→目标化参数提交→度量→内容投影 | 应保持人工跨度与热度分层；当前缓存/可达边界影响体验 |
| 导出/导入/恢复 | 便携Codec/Planner→确认→原事务；本机备份是独立恢复链 | 本工作区便携留白失真；不把便携当完整文件/素材备份 |
| 打开/拖入真实内容 | 当前仍DemoContentItemViewSource、Null素材provider | 未接真实文件引用、启动、移动或统计；不能说布局修好即实现收纳 |

## 6. 参考项目和设计稿的可用证据

本地 DeskBox 参考提交64f27e1（未查询远端最新状态）：FileSurfaceContent.IconSizing.cs 累计小滚轮增量并按Item恢复布局后锚；ScrollBars.cs 按活动显示/闲置隐藏；xaml.cs 明确拖动插入预览；KeyboardNavigation.cs 查找后滚入目标。参考的是输入累计、可定位、反馈和恢复，不照搬普通等尺寸网格来证明跨格/循环可行，也不复制与本项目无关的部件。

[HTML设计稿](../../20_Architecture/Designs/content-layout-v1.html)目前拖放调整顺序后重求解，固定列数，外层overflow滚动，网格/跨度为联动展示；不是生产固定坐标、双独立开关、有限视口、循环及冲突的完整交互模拟。保留作视觉构图参考，后续制作针对行为算例的原型，避免用艺术稿代替约束验证。

## 7. 处置结论

保留：SQLite权威状态/full CAS/单revision、稳定ID、宿主与输入岛、R4容量/预布局/结束恢复、空间推动/条件归位、会话及资源账本。整改：内部内容意图、统一轴模型、浏览状态、编辑仲裁、数据保真与测试链。不得通过取消CAS、扩大捕获、强行抬层、直接写库或隐藏失败来让表面“能用”。

本次文稿不冻结新语义；历史需求中关于三模式、图钉序号等与建议冲突的部分保留并集中待决。整改总顺序和实施门见[计划](../../30_Plans/V3_04_L_REMEDIATION_REVIEW_PLAN.md)，待用户评审后才决定制作。
