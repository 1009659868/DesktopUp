# V3 当前已知缺陷

本文件登记现版缺陷与处置，不替代需求合同或历史验证日志。暂停修复不等于问题解决，也不等于发布验收通过。

## 发布观察 2026-10-20：一次卸载中断（首因未定）

2026-10-24文档核对：后续即时取证与唯一安装交付均已验收，但本问题仍OPEN。10月23日单安装隔离检查另记录`app/content-worker/System.Web.HttpUtility.dll` / Win32 32 / 退出24、即时持有者0，保全后显式卸载成功；详见[唯一安装记录](SINGLE_INSTALL_MIGRATION.md)。不能据重试成功销项，也不能把它与历史升级退出41、P1/R6锁冲突直接判为同因；各次首轮证据保持。后续诊断代码已存在，下一次按实际失败阶段取证，不重做整套安装功能。

2026-10-21：新分支四次隔离复现成功，仍未获得历史失败的Win32码或占用者。失败路径诊断和测试执行码捕获已补强，受控故障/安全再执行/普通卸载自动与集中人工均通过，本批诊断交付收口；**本条仍OPEN，不记根因已修复**。见[本轮排查及人工收尾](UNINSTALL_INTERRUPTION.md)。

OPEN / 原因未确定。最终20261020.1包在smoke02的一次自动卸载中移除730/806项声明文件后停止，首个剩余`app/wuceffectsi.dll`；未捕获首次返回码，不推测占用者。原卸载器重试完成，数据摘要保持；同包smoke03全流程通过。保留原始失败和重试证据，不把复跑成功写为根因修复。下一次复现优先捕获卸载子进程退出码、首个失败文件及占用证据；不强删或放宽卸载保护。与P1/R6历史锁冲突分开记录，见[发布记录](PREVIEW_INSTALLER_DELIVERY.md#2026-10-20-架构已验成果集成与发布同步)。

## 原发布阻断 2026-10-16：迁移 SQL 换行形式导致旧配置校验失败

2026-10-20更新：固定资源/精确历史指纹与统一编码门已进入开发及发布线，最终包内组件对保留旧库的副本完成21→25及再次打开，旧账本与原库摘要未改。此已定位问题的限定修复/复验完成；真实登录自启仍未验。以下为阻断时历史。

OPEN / 已定位、未修。正常 main 候选20261016.101启动原 Preview schema21 配置时，在schema13发生迁移账本哈希不匹配；只读比对证明新包LF SQL转换为CRLF后与原记录精确相同，数据库前后哈希未变。不是数据丢失证据，不重置库、不改写旧账本绕过保护。修复范围、精确来源及现场保全见[发布记录](PREVIEW_INSTALLER_DELIVERY.md#2026-10-16-发布同步阻断与现场保全)。阻断安装环境自启动验证，main未提交/推送；不撤销另一独立候选的R1拖放人工通过结论。

## V3-BUG-038：局部操作触发整组图标失效与视觉重建

2026-10-14 / CLOSED（本轮USER PASS范围）。用户确认内部拖动/批量拖入/拖出再拖回三项均符合预期；布局/像素代次解耦、成员对账和定点呈现整改收口。后续外部悬停让位三项亦单独通过，现获合入develop授权。一次独立首图等待超时原因未定、重跑未复现，仍按[增量呈现记录](../V3-05/POSTER_INCREMENTAL_RENDERING.md)携带，不因人工通过冒称该瞬时失败已解决。

## V3-BUG-037：拖出到桌面未交接原生落点

2026-10-13收口：**CLOSED / USER PASS**。用户确认D4-N四组全部符合预期，按app05/human01本机原生拖放范围关闭，见[§11.4](../V3-05/DIRECT_TRANSFER_DELIVERY.md#114-d4-n人工通过与收口)。Windows自动排列/对齐仍由系统决定；不扩大为未验环境或跨海报直接OLE。下方USER PENDING/OPEN为历史。

2026-10-13最新：**CODE / TARGETED AUTO COMPLETE，USER PENDING**。DEC-047取消额外确认/共享，真实Shell源、跟手格式、捕获交接与源核实收尾已接线。原生鼠标落点/冲突/往返集中人工未通过前不关闭，见[直接拖放§11](../V3-05/DIRECT_TRANSFER_DELIVERY.md#11-dec-047单一引用与直接原生拖放)。下方“未修/未改代码”为本轮前背景。

2026-10-13 / **OPEN，机制核清、未修**。用户确认D5四组通过，但指出拖出不能决定桌面落点。源码事实：HandleDesktopTransfer仅用坐标判断桌面，完成后调用自有Outgoing将文件搬到DesktopDirectory；Explorer没有收到这一文件的真实拖放事务/落点。NativeContentDragImage只是视觉，不是OLE源。已有D5通过保留限定范围，此项不算通过。

按DEC-046／D4-N前置整改：参考DeskBox的真实Shell载荷，Explorer接收Drop并执行外部文件动作；本应用核实逐项结果并更新共享关系，禁止旧执行器二次移动/清源。尊重系统自动排列/网格，不以强设Explorer图标坐标补丁替代原生接线。实现顺序、确认/目标证据风险与集中验收见[直接拖放§10](../V3-05/DIRECT_TRANSFER_DELIVERY.md#10-d4-n原生shell拖出整改2026-10-13)。本轮未改代码或新增测试。

## V3-BUG-036：受管对象被外部剪切移出后海报卡片残留

2026-10-13最新：**CLOSED / USER PASS**。D5第4组外部改名/剪切、重启不复活与退出获用户通过；结合原同包自动证据，按最小受管同步范围关闭，见[人工结果§9.5](../V3-05/DIRECT_TRANSFER_DELIVERY.md#95-d5人工结果)。不扩展为任意外部目标定位或完整watcher通过；下方OPEN/USER PENDING是此前阶段。

2026-10-13修订：**CODE/AUTO COMPLETE / USER PENDING**。最小外部同步及与拖出共用的关系移除已接线；正式状态/真实应用界面/冷重启自动通过，证据归[直接拖放§8](../V3-05/DIRECT_TRANSFER_DELIVERY.md#8-05-11-a最小外部同步与公共移除2026-10-13)。外部Explorer剪切人工留至D5合包；未关闭BUG或冒充D3旧人工覆盖。下方为最初登记背景。

2026-10-13 / OPEN，用户报告。用户在实际收纳目录中将对象剪切到桌面，海报未移除对应图标；同次D3第1～3组人工通过，不因此撤销D3已有结论。源码核对：ReferenceContentRuntime基于持久成员输出卡片，Refresh/Observe仅刷新名称/图标/可用性，明确不更改成员或布局；尚无受管目录外部变化到成员事务的接线。此为已知能力缺口，本轮未复现原生操作或修改代码。

按DEC-045前置05-11-A最小同步：可信核对外部移出后同步受管成员、卡片和布局；离线/权限失败不清空，重命名/自身转移不误删，共享关系准确更新，重启不复活。不得再移动/删除桌面对象、猜测全盘新路径或按失效统一清空普通引用。范围/验证归[主计划§6.5.1](../../30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md#651-d3收口后的基础交互推进2026-10-13)。未实现/未验，关闭须有同包自动证据及用户外部剪切复验。

## Bug清单

2026-10-13 / DEC-043：**V3-BUG-022 RETIRED / REQUIREMENT WITHDRAWN**。用户取消普通点击穿透，留白归海报接收输入。原穿透失败及探针未通过事实保留，不标修复或人工PASS，不再阻塞D3。隐藏释放输入、框外不拦截和前台遮挡仍是有效约束；新实现与组合验收见[D3§8](../V3-05/D3_NATIVE_INGRESS_AND_PLACEMENT.md#8-取消普通点击穿透并收敛接收组件)。

2026-10-09出口审查：R7-D已询问D1/D2/D4均USER PASS，review09三进程正常停止/error0/退出资源0；最新退出picker=false，不新增选择器打开退出的日志认证。R7完整验收仍OPEN，D3及未单列原生/跨机范围归[§12携带表](LR_MILESTONE_EXECUTION.md#123-携带清单未验不改pass不创建重复bug)，R8假源交接完成不关闭001/004/006/026/031/035，也不证明另一测试机动效问题的根因。下面保留各Bug原始证据；仅定点校正011/016落后的“尚未接线”状态。

2026-10-08 R7-0更新：BUG-019有确定锁时序反例、修复及全量自动证据，按首次seed/快照读写范围关闭；BUG-021已修复并验证准备/交接缓存失效子类，长时原生范围继续观察。candidate02/final02的2362合同、16门、42进程通过，失败记录保留，见[R7§11.4](LR_MILESTONE_EXECUTION.md#114-r7-0可靠基线实施2026-10-08)。这不关闭白条031、云体验035或R7全部可靠性余项；下方“R7未开始”为历史。

2026-10-08收口：candidate15标题无底板/独立颜色与射灯交互跟随用户通过，BUG-030/034关闭，RR-06 CLOSED；新增BUG-035为Rain标题遮住可见动效，按[模块§13](LR_SCENE_MODULE_EXECUTION.md#13-rr-06收口与r7前增量修订2026-10-08)解决后再R7。BUG-031仍延期；下方早期待验描述为历史。

2026-09-30最新范围：BUG-031白条用户延期、未解决；BUG-029/030已接唯一云标题/独立动作底板，BUG-032核心组隔离已取得实际WinUI/事务证据，原生人工和完整矩阵待验。最新候选及边界归[模块§10](LR_SCENE_MODULE_EXECUTION.md#10-rr整改生产接线与同包验证2026-09-30)，下方源码审查段是修订前历史。

最后核对：2026-09-27，R5收口审计。BUG-008～017的历史机制已与R3～R5现源码及证据逐项对照，不能把原审查OPEN直接当成现状；修复层级及剩余范围见[R5审计§3](LR_R5_CLOSEOUT_AND_PLAN_AUDIT.md#3-代码与计划的差异审计)。新增BUG-026按用户要求延期，不进行本轮修复。原始反例见[布局审查](CONTENT_LAYOUT_REVIEW_2026-09-23.md)§3/4；旧Bug和失败材料保留。

| 编号 | 问题 | 状态 | 影响 / 当前处置 |
| --- | --- | --- | --- |
| V3-BUG-001 | 拖动中海报回到起点；左键仍按住但继续移动无效，须松开重新按住 | 未解决 / 用户要求暂缓修复 | 自动收展与标题拖动组合受影响；根因及稳定复现条件未确认。保留代码、日志和候选；回归主线，不自动重启排查 |
| V3-BUG-002 | 控制中心初始几何字段与真实海报不符，X/Y把fallback当当前值 | OPEN / UX-02自动通过，待人工对照 | X/Y静态机制已确认；宽高分别待核对保存意图/有效大小/单位，不一并判同因。仅隐藏字段不能关闭 |
| V3-BUG-003 | 海报贴屏右缘但预览看似处于中部，画布工作区边界不清 | OPEN / 定位自动通过，待UX-05人工验收 | 等比居中与整画布同色留白解释该错觉；按DEC-005旧预览专用代码已退役，定位原生不变性和到期自动门已通过，人工体验尚未验证，不提前关闭 |
| V3-BUG-004 | 收展、隐藏及自动收展使控制中心左侧列表闪烁 | OPEN / 已确认整表重绑定触发机制，尚未修复 | 状态摘要变化导致整份ItemsSource替换；默认过渡是高置信视觉原因，具体空白帧需对照验证 |
| V3-BUG-005 | 新增海报模板与外观滑块禁用 | CLOSED / CODE AUTO + USER PASS（2026-09-16） | 新建统一adaptive-grid，v8窄范围兼容修复；1315合同/12进程及真实旧库副本迁移通过，见布局统一交付 |
| V3-BUG-006 | 拖动控制中心后关闭再打开，窗口不恢复到上次位置 | OPEN / DEFERRED BY USER（2026-09-16） | 用户明确仅登记、暂不修复；不阻塞P9其他项，不扩大为跨进程位置持久化承诺 |
| V3-BUG-007 | 原生文件选择器打开时，托盘退出须等用户手动关闭选择器才继续 | CLOSED / CODE AUTO + USER PASS（2026-09-16） | 独立R3：1462合同、8真实原生及21既有同包门通过；用户随后确认复验通过，按已发步骤关闭退出缺陷，不扩大到未验环境 |
| V3-BUG-008 | 有限/循环滚动边界与轴向混用，短内容偏移、反向接缝及小增量异常 | PARTIAL FIX / WHEEL CODE + APP HANDLER PASS（候选25） | 起点反向预夹紧/逐事件Round已修；47相关/2115全量、两轴12场景102处理器事件/XAML/命中及八组同包门通过。用户确认大致可用，新报停顿续滚另列026延期；循环抓取/被动恢复归R6，不写成全场景已修，见[报告§9](LR_R5_POINTER_AND_MODULE_REVIEW.md#9-候选25循环起点反向滚动与主线审阅节点) |
| V3-BUG-009 | 横向区域局部坐标误碰撞，容量与纵向不对称 | FIXED / SOURCE + MODEL（R3） | 现solver按主/交叉轴求解并转换区域全局坐标；两轴1000个最大跨度及旧区域语义合同通过。原100项非对称反例为历史；App横向组合仍归R6/R7 |
| V3-BUG-010 | 横向只改变高度时宿主遗漏布局失效 | PARTIAL FIX / SOURCE + MODEL | EnsureContentSnapshot已有HeightDip失效键与锚恢复，Reclip拒绝横向旧高度见证；原遗漏已修。横向单改高度与R4动画/终态实际连续证据归R6 |
| V3-BUG-011 | 自动未固定项纯模型可移动，生产鼠标编辑门仍拒绝 | R5/R6 CODE + AUTO；R6已问拖动/循环USER PASS | 原循环拒绝门已解除；R6§8.1已确认两模式首拖/反向/再次拖、Esc、撤销及循环拖缩，不能继续写全部人工未验。当前R8假源失效/迟到松手复验通过；新候选接缝handle/完整键盘等细项仍归R7-CARRY-02，不扩称全场景通过 |
| V3-BUG-012 | 旧“恢复自动”清位置/尺寸/序号的预期与结果不一致 | SUPERSEDED / DEC-020、023、024；R4新合同已实现 | 退出A恢复保存的自动来源，解除图钉和恢复尺寸独立；不能恢复“全清”旧预期。旧Manual无返回来源明确拒绝退出A，旧硬约束不静默转换；不是所有旧数据均可自由转换 |
| V3-BUG-013 | Manual切方向提案版本+2被真实命令拒绝，并包含清位置固定的转换 | FIXED / POLICY + CONTRACT；APP WIRED | SetFlowAxis现只改方向与版本+1，保留模式/loop/坐标/返回态，冲突拒绝；App沿正式提交。动作专属原生/SQLite连续验收不冒填，归R6/R7 |
| V3-BUG-014 | 便携导出/导入静默清空ReservedSpaces | FIXED / CODE+AUTO（2026-09-26） | R2独立包：10新红测→定向25/25、全1664/1664；非空留白经真实SQLite/store重开保真。见[验证记录](VERIFICATION.md)；R7复验另列，非App重启或文件丢失声明 |
| V3-BUG-015 | 热度尺寸关闭标记未控制ResolveHeat投影 | FIXED / INJECTED MODEL + R4 AUTO | ResolveHeat现遵循AllowHeatDrivenSize及ManualSize，手动模式不改；R4红绿及1868合同支持。真实统计/热度公式仍未接入 |
| V3-BUG-016 | 真实可用空间不足时的准入/裁切/导航未闭环 | R6 APP WIRED + AUTO；真实环境未验 | R6 2A/2B已接ContentFrameMinimumPolicy、ContentSpacePolicy及ReadOnlyOverflow，不能再写App未接线。模型/隔离空间证据见[R6记录](LR_R6_CONTINUOUS_OPERATIONS.md)；D3真实显示/DPI/不足冷启动由用户保留未验，归R7-CARRY-01，不把主动缩框或消息注入当真实环境通过 |
| V3-BUG-017 | 旧布局人工候选manifest与实际DLL不匹配 | HISTORICAL CANDIDATE INVALID / REPLACEMENT VERIFIED | 旧CheckOnly失败与禁用保留，不修改hash绕过。后续候选25已有新同包证据/445源码/402产物，启动预检复核成功；旧包不继承新PASS，当前不再被旧失配阻塞 |
| V3-BUG-018 | 中心缩窄/增宽动画误用等宽收展表面，导致安全取消 | FIXED / CODE+AUTO（2026-09-27） | `alignment-composition-01`原生失败；新geometry lease同步prepared Content/Input/Clip，`alignment-v9-composition`104帧全部一致，100项缩窄/承接/放宽/撤销及重启通过；取消/宽高/DPI另有9项合同，非全环境人工认证 |
| V3-BUG-019 | 两个Store并发首次初始化偶发SQLite table locked | CLOSED / FIXED + AUTO PASS（首次seed与快照读写） | R7固定读/写锁时序旧实现两项Error6，改Private cache＋WAL，保留原事务、CAS与5秒等待；快照/回滚及32轮新库并发通过。candidate02/final02全量2362与16门通过；旧失败保留。不是并发schema迁移/断电认证，详见[R7§11.4](LR_MILESTONE_EXECUTION.md#114-r7-0可靠基线实施2026-10-08) |
| V3-BUG-020 | 海报原生菜单排列返回error5并立即安全关闭 | FIX + D1 USER PASS（本次问项范围） | 2026-10-09 review09用户确认三模板展开/收起的原生“…”菜单可点击且不误触Item、标题/云可拖；旧foreign锚点和被遮挡的坐标失败证据保留。不将本次反馈扩展为任意层级/全部透明度状态认证；见[R7§11.9](LR_MILESTONE_EXECUTION.md#119-bug-021读回路径整改与r7-d准入2026-10-09) |
| V3-BUG-021 | 预分配/最终恢复间歇读不到窗口区域并安全取消 | COPY-READ PATH FIX / C1 AUTO PASS / D1+D2 USER PASS | diagnostic07捕获复制初读0、无重设补查3；改单次GetWindowRgnBox直接读边界，实际缺失/错误仍拒绝。candidate09完整2424合同、17进程及R4 12案例/4清除通过，用户D1/D2询问范围通过；Win32内部原因及真实显示环境不据此推定。旧final05 FAIL保留，见[§11.9](LR_MILESTONE_EXECUTION.md#119-bug-021读回路径整改与r7-d准入2026-10-09) |

本轮新增：

| Bug ID | 问题 | 当前状态 | 依据与边界 |
| --- | --- | --- | --- |
| V3-BUG-022 | 内容Viewport整幅HRGN截获日常空白下的桌面操作 | RETIRED / REQUIREMENT WITHDRAWN（DEC-043，2026-10-13） | 普通点击穿透需求取消，不补修复PASS；全框接收成为预期行为。隐藏/退出后残留输入仍属缺陷，不包含在退役范围。见[D3§8](../V3-05/D3_NATIVE_INGRESS_AND_PLACEMENT.md#8-取消普通点击穿透并收敛接收组件) |
| V3-BUG-023 | 真实鼠标进入Item后仍不能驱动Lamp跟随 | FIXED / CODE + REAL POINTER（候选20，2026-09-27） | v19真实移动复现：NVIDIA Overlay整体alpha=0仍被当成遮挡；v20按成功原生读取排除全透明窗口，两个点实际跟随、离开轻摆通过，2102合同及NativeMouse通过。未知/非零alpha仍阻挡；产品视觉待人工，不扩大为全环境认证 |
| V3-BUG-024 | 控制中心默认按钮深色、悬停才恢复主题 | FIXED / CODE + CONTROL STATES（候选21，2026-09-27） | system浅色模式写到框架共享画刷，真实红测复现Normal深灰/PointerOver浅色；本地资源隔离、完整状态与弹窗样式修复。四主题及保存system重启共378状态读回通过；不是OS hover或高对比认证，见输入报告§6 |
| V3-BUG-025 | 半透明Lamp展开过程及终态标题下浅灰 | FIXED / NATIVE PHASE PIXELS + USER ACCEPTED UI/ANIMATION（候选24） | 候选23仅修终态，候选24在每次HRGN扩大后清新区域，四组三阶段24点差值0。用户随后明确“目前的UI，动画等都做得挺不错了”，登记当前体验认可；不扩为原速全部GPU帧/HDR/混合DPI认证，见[报告§8](LR_R5_POINTER_AND_MODULE_REVIEW.md#8-候选24展开过程灰底与ab不透明度对照) |
| V3-BUG-026 | 纵向循环在原顶部/底部停顿后继续滚动可能失效 | OPEN / USER-REPORTED / DEFERRED BY USER（2026-09-27） | 用户说明持续滚动正常、停下再滚偶发失效，当前大致可用并明确后续再修；不阻塞R5收口，不计为已修或循环全场景通过，详见下节 |
| V3-BUG-027 | 热度投影后新成员集合被旧item-set预校验阻断 | FIX IMPLEMENTED / MANAGED CONTINUOUS VERIFIED（2026-09-30） | R6第三批红例layout-05，按owner/member版本匹配热度后candidate10完整链与重启通过；不放宽CAS/兼容门，原生/真实文件不外推 |
| V3-BUG-028 | 新建SQLite备份经只读验证后产生WAL/SHM，显式恢复拒绝 | FIX IMPLEMENTED / CONTRACT + PHYSICAL-MODULE RESTORE（2026-09-30） | D physical-03确认；仅在新备份未发布副本中封存DELETE journal，重复只读验证不改字节且可恢复。未删旧sidecar，未放宽恢复安全门；见下节 |
| V3-BUG-029 | 雨云没有可拖动入口，用户期望按住云朵移动海报 | CLOSED / candidate12 AUTO + USER PASS（2026-10-08） | 用户确认原§11.4第1项；云为唯一标题，原命中/捕获/R4保持。后续只调绘制，不把本结论扩成所有标题拖动缺陷均已修 |
| V3-BUG-030 | 透明背景上收展/更多按钮难以辨认 | CLOSED / candidate15 AUTO + USER PASS（2026-10-08） | 用户确认无底板及独立标题/符号色方案通过；不承诺任意壁纸下自动对比度保护 |
| V3-BUG-031 | Rain及深夜书桌展开途中偶发底部闪白条，结束消失 | DEFERRED BY USER（2026-09-30）；ROOT CAUSE UNCONFIRMED | 用户要求先列入BUG清单，不阻塞RR-01～06其余项。预布局/固定原生容量已存在；Rain/Lamp各5轮、927张桌面局部图未复现，用户同轮亦未见，不是修复PASS。保留原报告、候选及取证入口，后续凭反例再判因，不能用动画提速冒充修复。 |
| V3-BUG-032 | A收展时B手动收展被拒绝，须A完成后再次点击 | CLOSED FOR REPORTED CASE / candidate12 AUTO + USER PASS（2026-10-08） | 用户确认原§11.4第3项，独立/相关组按合同处理。标题/外框仍全局协调，catalog变更仍可安全取消其他活动组，不宣称所有操作并发 |
| V3-BUG-033 | 收展中删除参与海报，已删除窗口被误判为物理恢复失败并触发全局恢复 | FIX IMPLEMENTED / MANAGED R4 RED→GREEN（2026-10-08）；完整同包门与人工状态见最新记录 | candidate11真实组合门出现`spatial.physical_recovery_required`；candidate12只对committed中已删除的成员释放布局并停用输入，仍存在成员/状态不可读继续严格失败。活动与排队回调终止、迟到回调无效、已提交B不变、环境代次不增通过；不证明删除时其他活动组仍能继续。详见下节 |
| V3-BUG-034 | 指针仍在海报内，点击/按住/透传交互使深夜书桌射灯回默认位 | CLOSED / candidate15 AUTO + USER PASS（2026-10-08） | 用户确认本次按键保护/刷新重置修订通过；遮挡、恢复、减少动效保护不放宽 |
| V3-BUG-035 | Rain云风感/裁边与模板参数增量 | AUTO GATES COMPLETE / USER PENDING，携带R7 | candidate25云横移/裁边用户未通过；§15闪电形态认可及candidate28自动证据保留，未获得所有人工项PASS。云尺寸/增强摇摆见[模块§16](LR_SCENE_MODULE_EXECUTION.md#16-云尺寸参数与增强起伏倾斜2026-10-08)，final04完整16门/2338合同/42进程通过。用户最新允许标题显隐完成后进入R7（§17），因此风感/首拖体验余项转R7而不是本轮补PASS；原生保护失败、资源限制和旧review04保留。不重开RR-06，不关闭白条BUG-031 |

具体输入链、图钉边界、候选与未过项见[输入与模块报告](LR_R5_POINTER_AND_MODULE_REVIEW.md)。模块可移除是DEC-027目标及独立方案，不把“当前不能删Lamp”伪装成已经修复的Bug。

候选20后续用户反馈：其他样式、灯光、图钉交互“都很不错”，对应范围登记用户正向体验，BUG-023补充USER POSITIVE；未将这句话扩大为BUG-022全部桌面手势或所有图钉反例的逐项确认。

后续新增记录继续分配稳定编号；关闭须写明候选、证据和复测范围，不以一次未复现或纯模型PASS关闭。

## V3-BUG-034：射灯在点击/按住时回默认位（2026-10-08）

- 用户反例：指针仍在海报内，点击/按住/透传交互让Lamp回默认位置。
- 源码原因：场景准入复用收展保护，PointerButton/PointerCapture/Gesture使canAnimate=false；UpdateScene刷新无条件传入0/null/false；内容/外框手势另被作为场景暂停原因。不是“真实离开”。
- 修订：独立只读场景准入，复用最近共享样本；不更改原生输入、自动收展或遮挡/恢复/减少动效保护。代码/托管处理器证据归模块§12；2026-10-08用户确认该修订通过，按报告范围关闭。

## V3-BUG-033：删除成员与结束恢复分类（2026-10-08）

RR-06补组合门时发现，不是用户新报的视觉问题。红例保留在`out/lr-rain-rr/rr06-20261008/combinations11/independent.json`（PID19780）：删除A后，A的收展与排队回调虽然退休，但产生error级`spatial.physical_recovery_required`并进入全局恢复，不能因退出资源0就计PASS。

根因：`App.ApplyStateProjection`先通知空间协调器取消，再由session registry销毁已删除窗口；此时HWND尚在，而`PreparePostureRestore`因committed中无该海报而没有可恢复几何。原共享R4恢复把这一组合与存活窗口恢复失败混为一谈。

修订仅限`PosterSpatialRuntime.Posture.RestorePresentedGroup`：调用原Prepare释放布局后，明确查询committed已删除时停用其输入，等待原registry正常销毁；未知状态及仍存在的成员不放宽，布局租约释放检查保留。没有重建窗口、跳过存活成员恢复或改变R4提交/白条清底逻辑。

candidate12 App SHA256 `DEA1F59B7AD7BBAF518CD087942C5D1F9CFDA44478E3C3918E32BB224E758D3C`，`combinations12`两进程通过，删除例不再进入全局恢复；真实WinUI/SQLite和托管输入协议范围，不是OS鼠标。完整同包门、最终候选及人工结论跟随[模块执行记录](LR_SCENE_MODULE_EXECUTION.md)，本条不提前授予RR-06通过。目录变更仍会使其他活动组的完整read set失效并安全取消；这里只证明已提交B不被回滚，不宣称任意删除完全局部化。

## V3-BUG-029～032：Rain人工反馈与跨海报共享门

用户在`rain-candidate08`、`out/lr-rain-review/data01`体验中提出四项问题，并明确：按钮辨识/白条目前仅测试Rain；B的收展没有延迟执行，必须A结束后重新点。对其他操作仅是用户怀疑，不能登记为全部已经原生复现。

源码及日志核对：

- `PosterVisualWindow.SceneHost.cs`将scene root设为不可命中；`BuildInputLayout`仅追加顶部TitleBar。云朵的几何/风动并未注册标题拖动语义。这原本符合“装饰不接输入”的旧范围，但不满足本次要求；DEC-031进一步要求云朵融入唯一可模板化标题组件，不能保留旧标题条后另造云拖动逻辑。宿主统一解析命中/动作并复用原capture，模块不得自行拖HWND或写状态。
- `CreateCircleGlyph`使用固定浅灰TextBlock，无独立底板；背景透明使桌面颜色直接影响对比度。当前没有调整整窗或按钮Opacity的链路，不把“看不见”误报为功能控件消失。
- `CommitSpatialFrames`仍在HRGN提交/表面读回后调用ClearExposedVisualBackground，完成时还有FinalRestore清底。调用成功、几何一致及XAML截图都不保证曝光帧无白条；需先留原速像素反例，未确定是视觉HWND、输入岛、原生底面或提交时序。
- `PosterSpatialRuntime.Posture.cs`的单一`_posture`及SetCollapsed全局忙拒绝，与验收日志中多次`spatial.posture_result`失败“另一个拖缩/收展正在进行”吻合（2026-09-30 15:56，UTC+08）。不是B参数被A覆盖，也不是已安排延后执行。
- 单纯把`_posture`换字典不足：PostureRun.ReadsCurrent校验全部海报版本；BeforeManualIntent会取消任何当前自动任务；`IsSpatialBusy`还被内容激活、拖动、内容编辑/菜单、定位/中心编辑等调用。源代码风险已经确认，但逐项原生症状仍待矩阵验证。导入恢复/全局环境变更等真正全局操作需要继续受保护，不能一律改为单张放行。

整改范围与分层出口归[模块记录§7](LR_SCENE_MODULE_EXECUTION.md#7-rain人工反馈修订入口2026-09-30)。保留candidate08的自动PASS证据，但Rain人工状态改为NEEDS REVISION，不进入R7；当前尚未修改生产代码或宣称任何一项已修。

后续补充：用户要求加快收展。只读日志最近六次单参与者成功收展136帧、nominal约1088ms、started至result约2325～2364ms；源码存在细分帧最小间隔和每次Rendering只取一帧的时长放大机制。提速单列RR-04，不并入BUG-031根因，也不能靠更快遮住白条；本轮仅制定计划。

## V3-BUG-028：备份只读验证生成sidecar，随后恢复被拒

D移除模块全链中的physical-03复现：BackupDatabase把源库WAL journal头带入目标；备份目标关闭后，公开只读验证仍创建-wal/-shm。恢复原有“standalone closed snapshot”门因此正确拒绝。此问题不是模块参数丢失，也不是BUG-019并发初始化。

修订只操作新建、未发布、由程序独占的临时备份：checkpoint(TRUNCATE)→journal_mode=DELETE→关闭并确认无sidecar→原校验→原子发布。源库、用户文件、既有备份及其sidecar均不清理；有sidecar的旧备份仍拒绝，不绕安全门。

红例为out/lr-module-cd-evidence/contracts/module-d-backup-red.trx；绿例module-cd-full-05.trx 2233通过；physical-04证明缺模块时创建备份→公开验证→实际恢复→再启动→装回参数逐字不变、退出资源0。原physical-03失败材料保留。仅认证新备份链，不承诺自动修复已有不独立备份。
## V3-BUG-027：热度投影后合法成员替换被旧版本预校验拒绝

2026-09-30在R6第三批同包连续门发现：热度提案已接受→结束保护→替换为新成员集合时，`ProjectContentEdit`仍拿旧item-set的heat校验新作者意图，抛出stale/different item set，合法CAS未执行。原配置未丢失，但成员更新被阻断。

已修：仅当owner/item-set一致才派生heat；新集合先验证新作者布局，CAS失败仍保留旧last-good，成功后由原成员重置流程失效旧heat。未放宽CAS或旧区域/硬位置兼容门。红证据`out/lr-r6-batch3-evidence/layout-05`；candidate10三海报完整链446检查及同库重启通过。状态为**FIX IMPLEMENTED / MANAGED CONTINUOUS VERIFIED**，不外推真实文件接入或全部原生体验。

## V3-BUG-026：纵向循环停顿后继续滚动可能失效

- 来源：候选25交付后的用户体验反馈。浏览方向纵向、循环开启，在原本的顶部或底部停下后继续滚动可能无效；持续滚动反而正常。
- 当前只确认用户报告的表现；停顿时长、续滚方向、指针位置、恢复条件和发生频率未量化，根因未验证。不能据此断言是输入区域、相位或滚轮硬件问题。
- 与BUG-008已修的取模前非负夹紧/逐事件取整分开登记，保留候选25红绿证据，不回退修复。本轮未新增复现、测试或代码改动。
- 用户明确“大致能用，这个小bug后续再修”，按DEC-029延期；R5按原核心有限路径收口。后续原生滚轮回归/R7支持范围审查必须携带此项；不能因延期宣称纵向循环完全可靠，也不据此自动重开修复。

## V3-BUG-021：尺寸动画窗口区域读回间歇失败

2026-10-09本轮处置：**复制读回假阴性路径已修订 / 完整C1 AUTO PASS / D真实环境待验**。diagnostic07 PID44072第5轮展开自然失败，GetWindowRgn=0后没有区域重设或恢复、同线程只读补查得到3；不能把复制API的0直接等同于HRGN不存在，也不能据此解释所有旧失败或Win32内部原因。几何验证现在只调用一次GetWindowRgnBox取得实际区域边界，无临时副本/重试/修复；真实缺失、空区域和错位仍失败。R7-0安装守卫保留。

candidate09/final09完整2424合同、0/1/3/6各20轮及17进程通过，12故障案例含4次原生HRGN清除（新增FinalRestore），退出error/资源0。证据、读回语义和边界见[里程碑§11.9](LR_MILESTONE_EXECUTION.md#119-bug-021读回路径整改与r7-d准入2026-10-09)。本轮不宣称所有环境永久消除此类错误；若直接查询也失败，仍沿严格失败/共享恢复路径保留现场。人工review09待反馈，C2仍后置；以下为修订前历史，final05不改记PASS。

2026-10-09最新：C1连续0/1/3张各20轮通过，6张第11轮第三张极简模板收起FinalRestore失败。`out/r7-continuity/final05/continuous-6/logs/desktopup.jsonl`，PID7196，App SHA `C695E9D573AC29CC478957B08E84EC7BEF9A89F0C0C413A6348BE52FA6DA82C5`，request `c1-posture-1d7d208a781942edb20729c5b671dca0`，revision253。窗口/客户区300×60，native/matches/input/region设置true，surfaceReadback为window-region-read-failed、GetWindowRgn=0。FinalRestore已强制重装，不能直接断言是原缓存分支；具体原生消息/时序尚待证明。原R4故障门在同候选独立11案例/3区域清除注入仍通过。C1未通过，不进D；下一步定点观测安装到读回边界及committed恢复，再小范围修复/完整复验。失败进程正常退出/资源0不代表恢复成功或缺陷已解决。

2026-10-08历史：R7-0已确认并修正“表面准备/交接后仍信任同尺寸旧HRGN缓存”的应用缺口；真实区域清除注入先红后绿，完整candidate02/final02已封存。保持严格起点/终态读回，不接受null区域，不增加无限重试。短时余项继续C1，真实环境归D，长测按DEC-033移至基础功能完成后；以下candidate17等原始观察保留，完整证据归[R7§11.4](LR_MILESTONE_EXECUTION.md#114-r7-0可靠基线实施2026-10-08)。

- 2026-09-27，App SHA256 `4BBBC824BCD4A19D93EB985E0742D44B655FDC2A1CCB63CB58CCC91C35F6637A`，`alignment-v17-composition/data/logs/desktopup.jsonl` PID18068。basic-box/100项夹具，无Lamp动效，因此没有证据把故障归于本次Lamp根ledger修正。
- 540×720准备及两次ready回调成功，逻辑缩到528×720时尺寸、输入、区域设置均返回成功；预分配surface实际GetWindowRgn=0，`window-region-read-failed`，不放松表面读回门。取消后`spatial.presentation_restored`的restored/layoutsReleased均true，未见几何命令提交，退出资源0。
- 同二进制在全新`alignment-v17-composition-recheck`通过全部宽窄/承接/撤销与第二进程；仅说明独立复核成功，不证明并发/时序根因消除。保留失败与成功证据；不增加自动重试掩盖、不凭未再现关闭。R5核心收口明确携带本风险，未宣称尺寸链完全可靠；R7最终出口必须有处置/支持范围结论，破坏核心链则阻塞。

## V3-BUG-006：控制中心重开未保留最后位置

- 来源：2026-09-16 P9第1项用户反馈，App SHA256 `395914F0204CDF5614639D7B181C1F371EB9B8FB7E85A1B303B3C03ACE02D67C`，独立lifecycle，PID12380。
- 复现：拖动中心到新位置→关闭→双击托盘打开；回到固定初始位置，而非刚才拖动后的位置。其他反复打开/取消/海报交互按用户反馈通过。
- 已确认代码机制：ControlCenterService.OnClosed释放并清空_window；下一次Show新建ControlCenterWindow。InitializeView只设置980×640初始大小，没有读取/恢复上次位置，服务也未缓存关闭前位置。不能将应用未保存的坐标归咎于系统恢复失败。
- 用户明确本轮只记录、暂不修复；不修改窗口关闭模式、设置schema、坐标或启动位置。不把“当前进程内关开记忆”扩展成已承诺跨机/跨进程持久化。
- 后续重启修复时再明确保存范围、最大化/正常窗体位置、多屏/DPI变化和离屏回收策略；当前无新增测试或修复。

## V3-BUG-007：文件选择器未结束阻塞托盘退出

最新状态（2026-09-16）：R3 App C0D2873C…及1462合同、8原生/21既有同包门通过后，用户已明确反馈复验通过，BUG-007按已发步骤CLOSED / USER PASS。独立STA持有选择器、核验本实例HWND请求关闭、清理完成后释放等待的修订保留。两轮失败证据及SDK子原因未知边界不删；见[BUG007完整记录](BUG007_NATIVE_PICKER.md)。BUG-006未修，无Git同步。下文诊断和“下一步”均为修订前历史，不作当前指令。

- 来源：同一P9候选、lifecycle/PID12380。用户报告原生导入文件选择器弹出后点托盘退出无可见反应，手动关闭选择器才正常退出。P9第2项说明弹窗关闭、第3项层级USER PASS不能覆盖此失败。
- 原日志保留：`out/manual/v3-04-06/lifecycle/logs/desktopup.jsonl`。16:00:42.137141+08:00已有application.exit_requested；16:01:07.1175219开始desktop_environment.monitoring_stopped；16:01:07.1797596停止，activeResources=0。请求到停止约25.04秒；等待期间仍有桌面层级事件，无对应error/fatal。当前进程已退出，无强杀。
- 已排除“点击完全未送达”：QueueTrayAction的Exit分支直接调用ExitApplication并return，不写普通tray.action_dispatched；缺少Exit action记录不是未到达的证据，exit_requested是正面证据。
- 已定位应用阻塞区间：ExitApplication在释放海报/中心/托盘之前await ControlCenterService.CancelPortableAndWaitAsync；该方法转发窗口，后者设置_stopping、Hide内容弹窗、调用_picker.Cancel()并等待_portableCompletion。completion只在PortableAction的finally释放；PickPortableFile本身await原生选择器操作。
- 当前高置信解释：取消请求未让本次原生选择器/等待任务及时结束，应用因而等到用户关窗才续行。现有日志没有Cancel调用前后、IAsyncInfo.Status或completion时间点，尚不能断言是Cancel内部等待、SDK未及时处理取消还是完成通知链未释放；不将具体SDK缺陷写成已证实根因。
- SDK依据：项目引用Microsoft.WindowsAppSDK 2.4.0；微软说明[IAsyncInfo.Cancel用于请求取消](https://learn.microsoft.com/en-us/archive/msdn-magazine/2018/june/c-effective-async-with-coroutines-and-c-winrt)。这是通用异步语义参考，不证明当前二进制内部实现；不能把调用Cancel视为原生窗口已经关闭。
- 验证缺口：App.PortableSmoke明确不驱动原生picker/确认，21项自动进程门不覆盖本次组合；最终资源0只说明用户关窗后能清理，不能将P9-4记为PASS。
- 下一步：针对当前SDK建立可证实完成的picker取消/关闭通道，区分选择器等待和真实文件IO；覆盖打开前/期间取消、重复退出、关闭中心/退出应用、导入与导出、迟到完成和资源释放。不能用跳过await、超时强杀、关闭无关窗口或提前释放仍在IO的存储来掩盖问题。生产修订前先补窄范围原生取消证据；不改BUG-006或空间/输入/R4/数据库合同。
- 本轮仅只读定位和登记，无生产代码改动、无新包、无自动复测或Git同步。P9-4保持USER FAIL，先处理本项再排P5。

## V3-BUG-005：新增布局标识与外观合同不一致

2026-09-16用户确认layout候选三项外观复验通过，按该范围关闭BUG-005；完整证据见[布局统一交付](LAYOUT_UNIFICATION_DELIVERY.md)及验证日志。下方修复前诊断和原待验文字为历史，不影响本次关闭。

最新修订（2026-09-16）：用户授权实施后统一新建工厂，追加v8迁移，仅修复有来源回执且参数原文匹配的已知错误记录，保留备份/身份/几何/状态；未知数据仍只读并明确解释。1315合同/12同包进程及旧验收配置副本迁移通过，原库v7保留；[交付与三项人工复验](LAYOUT_UNIFICATION_DELIVERY.md)。未收到新包用户反馈前不关闭。以下为修复前诊断事实。

- 2026-09-16截图672ae9de显示“新海报2”模板与两个滑块禁用；用户同时确认当前托盘人工验证通过。两项分开记录，不撤销已验托盘行为，不将新增后外观链路标为通过。
- 根因：App/PosterSpatialRuntime.ControlCenter.cs:109～113生成合法basic-box参数，却将LayoutKind写成grid；中心与托盘新增共用此处。Domain/PosterPresentationParameters.cs:51只接受adaptive-grid，否则Fallback令CanEdit=false；App/ControlCenterWindow.AutoPosture.cs:164据此禁用模板和两个滑块。不是锁定或权限问题。
- 实际证据：SQLite mode=ro/query_only只读核对out/manual/v3-04-05-global/three/state/desktopup.sqlite3，A/B/C为adaptive-grid，“新海报2”（poster-7214b9b15b574f24a862c24e39ed2c6e）为grid/basic-box，schemaVersion=1且参数合法。v1受支持，不是缺少v2 preferences导致。
- 未知布局仍可用basic-box兜底显示，但为防覆盖未知数据禁止外观编辑。界面未在外观区解释只读原因，仍提示“已保存”，造成误导。
- 测试缺口：App.TraySmoke新增后验数量/revision/窗口及全局行为，没验新实体CanEdit或新建→外观编辑→重启；既有外观夹具预置adaptive-grid。原1302/12项证据保留，但不宣称覆盖此组合链路。
- 建议修复：统一受支持的新建布局工厂；对已确认由此错误产生且参数合法的旧记录受控、幂等修复，保留身份/几何/状态/参数与备份，不放开任意未知布局；明确只读原因；补中心/托盘新增后编辑保存重启和旧记录恢复测试。
- 历史诊断轮：当时仅只读登记，未改源码/数据库/包或运行新测试；后续用户授权实施及证据见上方最新修订。未自动提交/推送。

## V3-BUG-004：状态刷新触发整表重绑定

- 2026-09-15后续用户补充：属性变化似乎不闪烁，状态变化才闪烁；与Summary/RuntimeStatus参与行相等性、宏观属性不参与的路径一致。用户允许本缺陷不阻塞主线并明确进入04-05。状态仍OPEN / NON-BLOCKING；本轮未实施稳定行修复，也未取得新的逐帧视觉证据。

- 用户截图4a4cd292显示列表文字短暂空白而选中条/右侧目标页仍在；用户说明这是旧问题，手动收展、显隐及自动收展均触发。不是本轮滑块引入，滑块候选未改列表生产逻辑。
- 已确认：Application/ControlCenterSelection.cs:5的record相等性包含Summary/RuntimeStatus；BuildView:44～60由IsCollapsed/IsLocked/IsVisible及会话状态生成摘要。App/ControlCenterWindow.xaml.cs:61～64在任何行不相等时替换整个ItemsSource，而不只是更新该行文字。同ID目录的状态更新因此被当成集合替换。
- 视觉放大机制：当前安装WinUI 2.3.6的generic.xaml:8967～8990默认ListView带AddDeleteThemeTransition、ContentThemeTransition、ReorderThemeTransition和EntranceThemeTransition；PosterSelector未覆盖这些过渡。整表重绑定与默认过渡组合是闪烁的高置信原因，但本轮未逐帧记录容器透明度，不能量化空白时长或称已排除一切其他呈现因素。
- 实际旧日志：out/manual/v3-04-04-ux/three/logs/desktopup.jsonl，PID30652。SetVisible在revision38～42均Committed；自动请求revision55在15:28:34.8207099/.8245734/.8337701（+08:00）三次中心投影，目录始终3、选中poster-main、Runtime均Ready。全日志178次中心投影不是178次集合重置：相等行会被SequenceEqual拦住；不要误称动画每帧重建或数据库丢目录。
- 重复刷新来源：App.ApplyStateProjection先ApplyProjection、再ApplySessions；ShowCommandResult还可再次投影同revision。它增加构建/比较次数，但根本问题是“状态改变→整表替换”，单做节流或禁止自动动画不能根治。
- 建议下一步：UI适配层保留按PosterId稳定的集合和行对象，状态/标题原位通知；仅新增/删除/实际重排改变集合结构；只在目标真的变化时更新选中项。不得只比较ID而漏掉状态文字更新，也不以关闭全部过渡掩盖整表重建。
- 验证出口：连续收展/显隐/自动操作时ItemsSource及未删除行身份稳定、摘要及时、选中/键盘焦点/滚动不跳；新增/复制/删除/零目录正常；再做相同操作的真实帧画面对照。此轮只排查、登记，不实施修复；不改变CAS/权威revision、宿主或R4。

## V3-BUG-002：初始几何字段的语义与实际位置错配

- 来源：2026-09-15用户04-04人工反馈与截图341574d7：选择海报时X/Y为0，用户报告宽度、保存展开高度也与实际不符。本轮未获得运行包指纹/完整原生尺寸记录，不补造。
- 已确认代码机制：Application/ControlCenterEditContracts.cs的PosterGeometryText.From直接读取FallbackXDip/YDip；App/PosterSpatialRuntime.Effective.cs启动占位还经过锚点、工作区投影和空间恢复。测试seed中fallback为0不意味着实际海报在0,0；控件却将它作为“当前显示器工作区位置”。
- 派生风险：旧请求总携带四字段；只改宽高可能把未触碰的陈旧0,0作为移动意图提交。这是代码可推导的风险，不冒充用户已复现位置跳变。
- 尚待区分：WidthDip/ExpandedHeightDip是保存意图，当前受限高度/窗口物理像素/DIP不是同一值。用户报告的宽高问题保留，不认定均由fallback造成，也不先判用户误解。
- 处置：UX-01建立真实几何基准＋字段意图合同，UX-02接同一可信运行快照、坐标换算、未改字段保留、完整空间/CAS。高级几何入口仍须真实可用。
- 关闭门：初次打开/切目标、锚点恢复后实际位置正确；只改宽或高不移动，位置单改不覆盖尺寸；受限高度、锁定、失效工作区/实体、重启均不静默改意图。新候选纯验证与实际画面对照后才关闭。

## V3-BUG-003：桌面缩略预览无法清晰表达真实工作区

- 来源：同日截图f11451f7，C实际位于屏幕右上角而预览看似在中部。
- 静态机制：Application/ControlCenterDesktopPreview.Fit等比适配并居中；App/ControlCenterWindow.Editing.cs使用固定160高且整块DimGray的Canvas，未单独标示有效工作区边界。例如2560×1392工作区缩至160高只占约294宽，宽画布两侧同色留白会被看作桌面空间。贴缩略工作区右边不等于贴画布右边。
- 证据边界：该机制解释截图误导，但未逐帧对照所有运行读回，不据此宣布不存在其他投影陈旧/坐标问题。与BUG-002分开。
- 处置：用户选择撤掉现有预览，UX-03移除其专用UI/死映射，改真实海报定位；不继续美化旧画布，也不删除共享几何读口。
- 关闭方式：新候选确认旧入口退役、定位/选择真实可用且无焦点/层级/资源回归后，可标“由替代交互消除”；不能写成“旧预览坐标算法已修复”。UX专用入口已退役并替换定位，状态仍OPEN，等待同包实机/人工门。

## V3-BUG-001：拖动回到起点并丢失本次长按交互

- 状态：OPEN / DEFERRED BY USER，2026-09-15。
- 影响：交互中断；用户要求本轮先记录、暂停继续修复，回归主线开发准备。没有承诺恢复修复的日期。
- 范围：M08 自动收展与标题拖动组合，主要反馈在快速拖动时发生；确切必要条件和复现概率尚未确认。不能据此认定其他拖动路径一定不受影响。

### 现象与复现线索

1. 在此前开启自动收展的场景中，鼠标进入收起海报标题，自动展开开始后按住左键拖动。
2. 同一次长按已能开始移动；拖动过程中，特别是快速移动时，海报偶发回到本次拖动起点。
3. 左键尚未松开，但继续移动不再拖动海报；必须松开并再次按住才能恢复拖动。
4. 用户于2026-09-15再次明确反馈问题仍存在。未提供本次运行PID、包指纹或新故障日志，因此不补造具体运行实例；此前推荐入口为safe-stop候选。

预期：有效手势持续跟手；单纯断触且会话仍有效时按已冻结合同保留最后可信位置并安全结束；真正代次/读集/原生提交失效按安全恢复合同处理。需要确认触发机制，不能把所有恢复到权威状态都改成无条件保存屏幕坐标。

### 已知证据与尚未确认的根因

- 首次接管时把逻辑可见区误当原生容量的独立缺陷已有RED/GREEN证据；用户后续确认第一次按住能拖动。它不等于本条“拖动中回起点”已解决。
- latest优先、已呈现前缀、取消分类与安全中止已落地；1152项合同、8项R4组件与隔离生命周期通过，范围见[TI-04报告§7](M08_TI04_ISOLATED_GATES.md#7-最新位置与安全中止2026-09-14)。本次人工失败明确不能被这些自动证据覆盖。
- 旧PID14408有拖动3帧后surfaceApplied=false并恢复的样本，其余native/matches/input/region布尔项为true；具体window/client/HRGN失败及其与本次反馈的因果关系未确认。
- 不把“鼠标太快”“失去焦点”“捕获丢失”写成已确认根因；也不宣称数据库状态丢失或文件损坏，本次没有相应证据。
- 已保留细分读回诊断及原恢复保护；按用户决定，本轮不继续取证、不修改生产代码、不增加新实验。

### 证据保留与暂时使用建议

- 旧日志：out/manual/v3-04-m08-07-ti04-handoff/chain/logs/desktopup.jsonl。
- 最近候选：out/verify/v3-04-m08-07-ti04-safe-stop；其指纹快照为该目录ti04-candidate.json；不要覆盖旧候选、旧配置或失败日志。
- 暂时可尝试关闭该海报自动收展，或等待完全展开后再拖动；依据此前用户对照描述，仅是规避建议，不是本轮已验证的修复。发生后松开再按住是用户确认的恢复操作。
- 不自动关闭用户已有设置，不回退现有源码，不更新正式M08/R1绑定，不做Git提交/推送。

### 重新启动修复与关闭条件

用户重新安排本缺陷，或后续主线改动触及同一输入/呈现边界时，先检查该记录；若出现数据不一致、无法恢复或新功能被直接阻断，立即重新评估，不用本次延期掩盖新的故障。

关闭须同时有：同一候选的真实连续拖动/快速反向/长按中断分类验证；最后可信位置与权威事务的一致性；自动ON/OFF及完全展开对照、邻居条件归位和资源释放。纯模型、组件PASS或一次未复现均不足以独立关闭本条。

### 对主线的影响

[V2-DEC-004](../../10_Requirements/REQUIREMENTS_RISKS_AND_OPEN_QUESTIONS.md)允许继续V3-04-04开发准备，不表示M08完成或有缺陷版本可直接发布。M08相关人工失败、自动回归Unavailable和资源0/36仍保留；主线不再自动回到本缺陷循环，也不继续TI-05集中验收。下一项见[控制中心计划](../../30_Plans/V3_04_04_CONTROL_CENTER_PLAN.md)。
