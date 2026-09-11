# V3-04 验证记录

本文件按日期/产物追加验证事件，保留当时结果；当前阶段与下一项查[HANDOFF](HANDOFF.md)。旧事件中的“当前/待验/PID”只描述当时观测，不是今日任务指令，也不保证进程仍运行。

## 2026-09-11 V3当前状态同步检查点

用户明确授权将当前状态提交至`origin/impl/poster-redesign-V3`。同步前fetch确认本地HEAD与远程均为53ebdb6、ahead/behind=0/0。当前源码/脚本/测试/文档共120项变更；构建产物、日志与隔离数据仍由out规则排除。提交前重新执行完整合同测试**880/880、0 skipped**，证据`out/verify/v3-04-m08-07-r2/tests/sync-checkpoint.trx`；未重跑实机鼠标或资源采样，不扩大之前的验证结论。本检查点包含进行中组合合同和纯模型，不表示生产组合交互已完成；不修改主线或强制推送。

## 2026-09-11 M08-07 R2：组合前置模型与外部点击归属

用户授权按组合目标先合同/连续测试，再接生产；不采用短停留或先回滚再拖。新增[组合合同](M08_EXPANSION_DRAG_CONTRACT.md)及28项纯模型/帧结构测试；包含两种结束顺序、普通点击、捕获/身份、不可回退高度、扫掠、锁定、4个DPI各1000轮往返。仍缺完整组合空间/因果重放、CAS和原生适配；几何安全不能授权任意邻居移动的反例已明确保留。V2-TBD-018优先跟手或优先增长已询问，未代用户决定。

R2-A单独先红后绿：`out/verify/v3-04-m08-07-r2/tests/outside-click-before.trx`4/4失败，复现deadline清空与遮挡下错误保护。只修`PassivePosterPointerObserver`原始保护归属；新9项包含连点/按住、前台遮挡、auto关闭邻居、离开后捕获、零需求/代次失效、菜单保留。未修改AutoPostureModel延迟或R1进行中取消规则。

定向`combination-model.trx`28/28；`r2-targeted.trx`89/89（最后零需求用例之前，当时构建有1项空值警告，随后修正）；最终`r2-full.trx`**880/880、0 skipped**。首轮新组合夹具有C#上下文关键字from命名/度量常量错误，修正后执行通过；不将编译错误伪报为产品回归。独立Release`out/verify/v3-04-m08-07-r2/app`构建**0警告/0错误**。未启动新包，旧R1绑定及PID25524保留；无新增实机或资源认证，无commit/push。

续：末次检查R1/PID25524日志于09:31:48.215 UTC记录`application.stopped/activeResources=0`，未强制结束。R2 fixture独立构建0警告/0错误。PS5.1运行`Test-M08NativeEnvironment.ps1`于09:52:30 UTC返回InputDesktopReadable=false/OpenError5、CursorReadable=false/CursorError5、Foreground0、ThreadName=Default。因此未启动新App、未播种回归库，不将其记为R2实际输入通过，也不推断用户锁屏或修改权限。

## 2026-09-11 M08-07：R1人工通过与R2新增诊断

用户确认R1的收起/展开途中普通点击两项均通过；新增海报外连续点击延迟重置，以及收起标题长按拖动导致自动展开回滚但拖动未建立。PID25524日志与代码证据见[R2诊断](M08_INPUT_ARBITRATION_R2.md)：前项全局原始保护反复清空Deadline已明确；后项确认取消/恢复后未建立gesture，按下中间帧与恢复后起点的具体矩形仍缺成对日志。DeskBox已复查短停留与按下撤销待展开的实现。

本轮未修改生产代码、未构建新包、未把R1的843项测试扩称R2通过。建议按交互归属、计时保留、标题待定与真正拖动接管一起修订；已询问是否接受约200ms标题停留判定，该数值未成为合同。当前仍运行R1，M08保持IN PROGRESS，无commit/push。

## 2026-09-11 M08-07 R1续：退出后的隔离回归

用户已正常退出旧PID59180，台账clean/0。当前R1指纹未变，原生输入前置已可读。新隔离目录 `out/verify/v3-04-m08-07-r1/runtime-01`：启动52912、重启52060、自动37984、链式空间56656、受限39632、阻塞53412，共6次PASS、exit0、台账资源0。两次启动DB指纹一致；原生自动4请求/3提交/1动画中策略变更Conflict恢复。完整/受限/拒绝结果均符合预期，详情见[R1§5](M08_ANIMATION_INPUT_R1.md#5-用户退出后的隔离运行2026-09-11)。

当前包的自动smoke已补验，不等于实际锁屏恢复、鼠标点击或视觉门关闭。已用PS5.1启动R1 basic人工版PID25524，保留旧场景设置，未自动注入点击；等待两种方向动画中点击反馈。有效资源基线仍0/36，没有commit/push。

## 2026-09-11 M08-07 R1：动画中按键误取消（修订交付时记录）

用户确认此前基础1～3通过，发现自动收起/展开途中普通点击会跳回原姿态。PID59180日志已证实两种方向都取消回滚，非反向请求；根因及修订边界见[R1](M08_ANIMATION_INPUT_R1.md)。

旧模型新增连续4项全部在误取消处失败。区分原始PointerButton/PointerCapture与真实Gesture后，完整843/843、Release0警告0错误；PS5.1.26100.8875的25项绑定/5场景/测量入口预检PASS。中间6个测试的虚拟截止时间误差10ms已纠正，原TRX保留。未修改空间/R4/事务或日常配置。

新包 `out/verify/v3-04-m08-07-r1`，App SHA256 BFA7A5059198FF7E07D168FC69219AA1C60B16AA020859AEE781120F1E44255A。旧PID59180尚在运行，已请求正常退出；新包隔离进程/鼠标画面未验，资源仍0/36。没有commit/push。

## 2026-09-11 M08-07：人工/增量资源入口（初始产物记录）

[07合同](M08_EXPERIENCE_AND_RESOURCE_GATE.md)已建立G1～G5及36格/18对矩阵，用户本轮授权进入07，但携带06原生余项，不代填通过。最终App SHA256 F91FFD6B28E25C887B278D396AC67C0618191D6648AD2629693ADE9D2998BE05，25项绑定，M07稳定24项未变。

新测量计数30/30，全798/798、Release0警告0错误。PS5.1.26100.8875预检/启动/同库重启PID59532/59624通过，数据库hash一致、exit0/clean0。OFF探针47664/24524/58264通过，ON58592因不可读输入明确拒绝并clean0；均不是成对外采基线。首轮脚本预检发现方法实参pipeline缺括号，修正后PS5.1入口通过。

当前工具会话仍error5/foreground0，resources-attempt-01记录BLOCKED且无样本；完整外采收尾/配对待有效环境。人工G1～G5全部PENDING、有效采样0/36。所有试验进程已退出，basic配置保留供用户从桌面PowerShell启动。未commit/push，不改变主线、用户文件或日常库。

## 2026-09-11 M08-06：生命周期与隔离回归（余门OPEN）

[实际合同](M08_LIFECYCLE_REGRESSION.md)记录新共享WTS/电源接线、DeskBox参考版本/消息常量纠偏和证据边界。新增32/32、完整768/768，Release0警告0错误；聚焦首轮55/56发现多原因部分恢复的动作判定错误，修正后完整回归PASS，保留首轮TRX。

最终App SHA256 4116ED1287B75B7945369B5256E55C925146404E6AA88EF8F8387A118D3DB3C6。生命周期3张PID42316/零目录重启18500；0/1/3/6启用退出及重启PID25952/30840、38236/7464、51140/45880、42104/37416；设置50104/50516；空间7612（4次/168帧），共13次PASS、exit0/clean0。重启完整定义/revision一致；本地通知经过生产隐藏HWND但不是实际OS休眠/锁定。

M05重检46516和M06最终自动45892均Unavailable/0请求，最终超时并clean0。只读探针07:42:57 UTC：OpenInputDesktop与GetPhysicalCursorPos均error5、foreground0，具体会话原因未确证。ObservedOutside=false，真实恢复计时、自动动画及受影响白条/层级门OPEN；不以纯模型或布局读回替代。M08-06保持IN PROGRESS。

本轮数据均在out/verify/v3-04-m08-06；M07稳定24项未变，未commit/push、未改日常库/文件或UAC，退出后无DesktopUp进程。M08-07增量资源和完整体验门未开始。

## 2026-09-11 M08-05：最小设置入口

[设置入口合同](M08_CONTROL_CENTER_SETTINGS.md)记录实际行为、命令、指纹及局限。新增27/27，全736/736，Release0警告0错误；默认OFF/3秒、逐海报1～300秒、应用/取消、原文校验、切换不串写、过期CAS冲突及关闭重开已接。窗口dirty/pending借用共享Editing保护；仅打开不保护，释放后由原调度重新判定。

最终App SHA256为6CDF89B44760BD24DC8F5834442A81AA421501B195CE0A9F553293ECDB3F8E3E。PS5.1真实控件PID43568（3张/revision6）、49852（0张/revision2）PASS；均exit0/clean0。真实Button Invoke走Click/生产命令；XAML截图已检查，不冒称鼠标或桌面动画视觉验收。

native-settings-01保留夹具事件等待失败；发现TextChanged异步，生产已同步捕获TextChanging原文并保持视觉更新异步，最终增加“输入后立即投影”断言通过。参照Microsoft文档，不在同步文本事件增删视觉树。

原自动回归PID26188在最后空目录标题修正前通过3提交＋1动画中取消；最终包PID7424/36092均Unavailable/0请求/超时后资源0，**不记PASS**。随后只读探针OpenInputDesktop拒绝访问5、光标读取失败、前台0，当前输入桌面不可读；具体锁屏/UAC原因未确认。已请求用户恢复普通桌面，最终自动门待复测，不绕过安全保护；证据保留在native-auto-release/recheck与native-input-unavailable.json。

日常数据库未打开/迁移，M07稳定24项指纹不变，无用户文件操作或Explorer/显示变更，未commit/push。M08-05设置范围PASS，M08整体仍IN PROGRESS；先补环境复测，再M08-06/07。

收尾：73份Markdown本地文件链接缺失0；git diff --check通过；44份未跟踪源/脚本/文档行尾空白0；新脚本解析错误0，PS5.1实际控件运行已通过；DesktopUp进程0，HEAD仍53ebdb6。入口、A/B实现映射、C正文/登记/交接已对齐，不删除历史未提交工作。

## 2026-09-11 M08-04：共享调度与空间接线

按用户授权接入正常App运行路径，详见[M08_SHARED_SCHEDULER](M08_SHARED_SCHEDULER.md)。一个共享计时器/观察源，输入回调只观察、效果出队重验；受限System姿态提交、完整空间CAS、现有R4动画及明确恢复后回执。设置UI仍属05，实际鼠标/视觉及增量资源门仍06/07。

新增38/38、全量709/709、0失败0跳过；独立Release0警告0错误。补充意图时ExitVersion，避免向上展开后手动收起移走标题被当成真实离开；保留无空间限次、单航班取消确认、链式事务/失败回滚、菜单通知与日志异常隔离。

最终PS5.1实际进程PID43140，native-final新隔离库：原生观察驱动3次自动收起提交；零需求timer/订阅0；旧deadline关闭无动作；第四次真实自动动画中关闭开关，Conflict回执后完成恢复、无半姿态写入；revision17、exit0、日志无error/critical、ledger clean/0。未合成指针或直接触发自动请求。中间PID48532/39028也通过并退出，原日志保留。

实际环境isElevated=true/asInvoker；未改UAC、Explorer、显示或用户文件。M07稳定24指纹保持，日常数据库未打开/迁移，旧证据和既有未提交改动保留。所有命令/最终哈希/范围限制见合同；未commit/push。XAML/尺寸读回不替代真实鼠标进入展开或无白条的视觉验收。

收尾检查：72份Markdown本地文件链接缺失0；git diff --check退出0，src/tests/Docs/scripts内37份未跟踪文件行尾空白0；新脚本解析错误0，实际PS5.1执行已通过。当前DesktopUp进程0，HEAD仍53ebdb6。入口、A/B实现映射、任务正文/登记表、代码锚点与交接已对齐M08-04；旧验证事件按原产物保留。

## 2026-09-11 M08-03：被动指针观察与交互保护

按授权完成观察适配器/API门，见[M08_POINTER_OBSERVATION](M08_POINTER_OBSERVATION.md)。新增共享按需原生采样、逻辑输入几何/圆角/热区复用、真实接收根/桌面身份/遮挡核对、跨界计数及App会话/确认读口；TME_LEAVE仅在有观察者时登记，路由提示不直接证明hover，观察回调异常不破坏原点击/拖缩。未启动生产循环、自动计时或System提交。

最终新增52/52、完整671/671，0失败0跳过；独立WinUI Release0警告0错误。除纯/假原生连续夹具外，屏幕外真实自有HWND验证输入region孔洞、300×800容量与300×360输入分离、当前接收根及禁用遮挡，20次查询前后区域/几何/配对层级和前台不变；所有自有窗口/类在finally释放。Native记录CursorProbe=True、ProtectionProbe=True。没有移动鼠标、合成点击、修改Explorer或启动DesktopUp App。

首轮原生夹具误将WindowFromPoint的禁用窗口跳过规则套用到WindowFromPhysicalPoint；本机实际返回禁用窗口。保留差异说明并验证无论接收根返回遮挡者或底下输入窗，可见遮挡都不能变成有效hover。一次新增空值编译警告已修正。最后补未知桌面根Unknown夹具，避免Shell变体/重建被误判为离开。未删除失败场景；原菜单测试仅补共享活动标志断言，旧619项保留。

结果/命令/build.binlog和五个SHA256在合同；新包out/verify/v3-04-m08-03/app仅构建未运行。schema7、日常库、原人工目录、配置及M07稳定产物不改，原24项绑定只读通过。保留此前未提交成果；未commit/push。

下一项M08-04：唯一共享观察/截止时间调度、当前模型帧组装、受限System姿态入口及精确完成取消；05设置UI，06生命周期，07实际鼠标/WinUI/动画和增量资源。快速未采样往返不伪造，真实OLE会话尚无，外部拖动的按键/capture保护不冒称全部拖放。屏幕外原生API证据不扩称实际多海报菜单、刷新、GPU或混合DPI验收；不重复已通过M07人工。

最终文档检查：71份Markdown本地文件链接缺失0（不包含所有页面锚点认证），git diff --check通过；src/tests/Docs的25份未跟踪文件尾空白0。入口、任务正文/登记、模块映射、代码锚点与交接同步到03适配器/API门；旧01/02验证事件保持当时原文，不重写历史。

## 2026-09-11 M08-02：用户规则确认、设置实体与迁移

用户明确确认手动收起后保持收起，必须真正离开再进入才自动展开；V2-TBD-017改RESOLVED，M08-01合同冻结。按授权进入M08-02，新增逐海报秒数及共享范围校验、schema7/仓储映射、SetAutoPosture目标命令与草稿、同committed revision的投影，保留旧v6请求哈希/回执。详情见[M08_AUTO_POSTURE_SETTINGS](M08_AUTO_POSTURE_SETTINGS.md)。

本次新42/42，全量619/619，0失败0跳过；独立WinUI Release0warning/0error。旧577项首轮9个失败均是当前schema期望6而实际7，只更新当前schema断言；原1..6迁移指纹/业务revision不改。新增测试覆盖旧列/表/版本与备份保留、开关真假、空库/零目录、晚期迁移与写回执故障回滚、SQL/JSON值约束、同/不同目标CAS、配置复制、重新打开数据库、删除后旧草稿及两套旧回执重放。

新包out/verify/v3-04-m08-02/app只构建未运行，build.binlog与settings/full TRX、哈希见合同。Windows/x64、SDK10.0.103；所有迁移只在测试临时库，日常库/原始人工目录未打开或迁移；没有新的鼠标/动画/原生生命周期/资源认证。M07稳定绑定只读24/24通过，旧包未覆盖。没有App/Windows窗口源码改动或用户文件操作，未commit/push。

M08-02数据/构建门PASS，下一项03被动指针与交互保护。额外事实登记到M08-04：空间运行器识别System不等于可提交，StateProjectionCoordinator.Run仍拒绝System；后续须补受控系统姿态入口及完成/取消通知，不在本步放宽权限。任务正文、模块映射、登记和交接同步到实际边界；以下01与更早日志为当时证据，其旧待确认不覆盖本次明确决定。

收尾检查：70份Markdown的本地文件链接缺失0（未宣称验证全部页面锚点）；git diff --check通过，src/tests/Docs中15份未跟踪文件尾空白0。当前入口/任务正文不再保留规则待定或下一项02的旧指令；旧验证事件保持历史原文。

## 2026-09-11 M08-01：行为合同与纯模型（AUTO PASS，规则待定）

用户授权按计划进入M08-01。新增Application/AutoPostureModel及三个AutoPosture测试文件，合同见[M08_AUTO_POSTURE_MODEL](M08_AUTO_POSTURE_MODEL.md)。默认OFF、1～300秒/默认3秒、重入撤销、手动优先草案、交互保护、当前资格仲裁、全局单航班、精确取消确认、生命周期版本失效和失败限次已纯模型化。模型不写姿态/几何/数据库，未改现有空间求解器。

本次新测试69/69，全量577/577，0失败0跳过；原508保留。除单次边界，还验证3个seed各5000步、1000次静止指针几何跨越，以及100轮自动请求与现有纯空间求解器组合的推让/归位，检查确定性、状态有界、快照不变、帧安全和无几何漂移。初轮64项及补齐长序列后的69项均通过，无删除失败场景来凑通过。

命令为`dotnet test tests/DesktopUp.Contracts.Tests/DesktopUp.Contracts.Tests.csproj -c Release -p:Platform=x64`，定向增加`--filter FullyQualifiedName~AutoPosture`；结果分别位于`out/verify/v3-04-m08-01/m08-01-pure.trx`与`m08-01-full.trx`，精确命令/哈希见合同。Windows/x64、SDK10.0.103；没有执行WinUI App构建或新产品进程。新纯模型测试本身不使用真实sleep/计时器/文件IO，全量旧合同继续使用各自临时SQLite夹具，未迁移日常数据库。

再次阅读DeskBox 64f27e1的WidgetCompactInteractionPolicy和ScheduleSmartCollapse，吸收纯判定/计时重验/交互保护；独立实现，没有复制原生代码，不改变既有许可证来源事实。源代码App/Windows/Domain/Infrastructure、配置及空间算法未改。只读Get-M07StableBuild校验24/24指纹通过；稳定App DLL仍为C699CAB63D50E224B697E2BD4F8918ED629BFB4BC7E40CBD7375113BB403BDAF。

V2-TBD-017仍OPEN：已向用户再次询问“手动收起后须真实离开再重入才自动展开”，未收到明确答复；对应夹具标注Proposed，不将测试通过冒充产品确认。M08-01尚未完全冻结，02..07仍未实施，无新增鼠标/动画/原生生命周期/资源认证。未换稳定包、未启动或停止App、不重复M07人工；保留此前文档/脚本改动，未commit/push。任务正文、登记、模块映射、代码锚点和交接同步到此范围。

本轮文档检查：69份Markdown的本地文件链接缺失0（不冒称检查了所有页面锚点）；git diff --check通过，新模型/测试/合同文件尾空白0。src/tests/config状态仅有新增的模型与三个测试文件，没有修改既有源码或配置。剩余门是用户规则确认，不是重复M07人工或重建稳定包。

## 2026-09-11 ABC文档对齐（仅文档，无主线变更）

用户授权同步任务正文/登记表、集中决策归属并标明历史范围，不重写全库或改变主线。本次修正B层M07旧计数/待验说明、C层M04待实现指令及登记表冲突；当前任务统一由细化工作流维护，M04/R2原任务卡明确为历史证据，保留原版测试与失败记录。

A层统一索引新增V2-DEC-001、V2-DEFER-005/006、V2-TBD-017；C层M08与优化登记只引用决定ID。手动收起后的自动重启条件仍OPEN，不把整理文档的授权当作产品行为确认。需求画像将“当前阶段只审计”改为本需求文档的职责说明；ABC维持逻辑分层，未搬目录。

检查：68份Markdown的本地文件链接目标无缺失，四个新ID各有且仅有一个权威定义行；已发现的M07未收口/M04待实现旧指令不再出现在当前入口、任务说明或登记表。总计划从V3-05起、工作流从原04-04起的后续正文与本轮修改前逐段相同；M08→必要管理/恢复→真实Item和文件账本安全门未变。git diff --check通过，src/tests/config无差异。

仅修改现有文档；不新增实施阶段，不删除历史证据，不改生产代码/脚本/配置，不构建或运行新产品测试，不启动/停止App，未commit/push。M07的既有PASS仍仅覆盖其明确证据，普通用户权限、多实例Explorer手工重建、多屏/混合DPI等未取得认证不补PASS。

## 2026-09-10 M07正式收口与M08计划调整（仅文档）

用户明确：完整收展减少动效、手动快速连点最后意图仅按需记录，后续有需求再制定实施计划；真实指针与计时驱动的自动收展必要，下一步先准备完成计划；稳定可用优先，不立即跳到真实Item，也不无限扩张外壳。

据此M07-04的欠项归属/顺序决策已齐，M07-01..04 PASS。F03/F06登记为无排期按需优化，F07进入[M08七项计划](../../30_Plans/V3_04_M08_AUTO_POSTURE_PLAN.md)，所有子项PLANNED；下一项M08-01行为合同/纯模型。不是将未实现功能本身记PASS，也不重复M07的S1～S5。

已同步需求优先级、架构/模块、主计划/工作流、目录/交接；DeskBox参考commit64f27e1的纯交互策略及计时重验取舍记入计划，未移植代码。手动收起后悬停抑制规则仍为待确认建议，实施前核对最新答复。

检查：本轮文档本地链接234处、缺失0；按仓库原有core.autocrlf=true执行git diff --check，exit0。临时关闭换行转换的检查曾把CRLF当尾空白产生假阳性，未据此重写文件或修改Git配置。src/tests/config差异为0；无构建/新单测，不冒用旧508计数作为M08证据。未改生产程序、脚本、日常配置；原有工作树脚本改动保留，未commit/push。

## 2026-09-10 M07 S5 与测量/人工安全子项完成

用户明确反馈海报可正常清空、控制中心可操作且正常退出。PID42800实际先删A、再删C，均removal_completed/Committed；C确认IsLastPoster=true，最终0张/revision19。日志tray.disposed、application.stopped/0，台账clean/0、error/critical=0。删除顺序与原指引不同，不影响末张/零目录验证；不虚构已逐字审核文案。

同库重启PID49016（FixtureExists=true，24指纹通过），仍0张/空ID/revision19、0海报会话、1中心/1托盘、无错误，未重建配置或补默认。S5 USER/LOG PASS，S1～S5证据齐；M07-01/02/03 PASS，M07-04仅余F03/F06/F07能力归属/顺序待决策，报告和资源差距已齐，不重复人工。当前空目录验证进程仍开放；本轮无生产代码/commit/push变更。

## 2026-09-10 M07 S4 关闭，S5 等待最后一张/零目录

用户确认按通知区退出操作执行。PID34216在16:51:02打开C确认，16:51:10出现removal_cancelled/application-exit、tray.disposed、application.stopped/activeResources=0；台账clean/0、error/critical=0、重启前进程0。C确认中关闭中心的前项证据已齐，S4交互USER PASS。

复用原removal库（FixtureExists=true）重启PID42800，24项稳定指纹通过、2会话Ready/1中心/1托盘、无错误。退出前与重启后的目录均为A/C两ID、revision16，B不复活。用户退出前有geometry-move/resize提交，revision由早先4增至16；比较实际退出前版本，不把用户额外操作误判恢复失败。S4同库重启LOG PASS。

已请用户在PID42800中心删除C，再核对最后一张提示并删除A，零张时确认中心可用后通知区退出；下一步同库重启验证仍零张。S5尚未补PASS，无生产代码/提交变更。

## 2026-09-10 M07 S4 确认中关闭通过，应用退出仍待验

用户回复“验证通过,继续下一步”。PID34216日志16:47:24出现C的removal_cancelled，reason为“控制中心已关闭，未删除海报。”，随后确认dismissed/false；因此确认中关闭中心已与人工反馈互证。A/C两张、revision4保持。

其后16:48:04确认取消、16:48:06仅control_center.closed；检查时PID仍运行、stopped=0、error/critical=0，不能宣称应用干净退出，运行台账12资源也不判泄漏。剩余需保持确认弹窗从通知区右键“退出 DesktopUp”；若该动作已执行仍无效则按退出故障排查。同库重启尚未执行，无生产代码/提交变更。

## 2026-09-10 M07 removal 顺序调整（B已主动删除）

用户反馈认为S4的1～3通过，同时说明点击“删除海报”删除了B。PID34216日志：16:39:35、16:40:04、16:40:15为B确认dismissed/confirmed=false；16:40:28仅B removal_completed/Committed，目录revision4、A/C两张仍Ready，error/critical=0。16:40:52中心关闭发生在删除之后，检查时程序仍运行、stopped=0；16:41:58记录C确认打开。

取消与B确认删除已取得证据；不把确认外的中心关闭或尚未退出当成S4其余项PASS。保留用户实际状态，不重建B；已请用户用C补确认中关闭中心、再次确认中托盘退出。下一轮同库重启应保持A/C、revision4且B不复活，再继续删除至零。S4/S5仍分项待验，无生产代码或Git同步变更。

## 2026-09-10 M07 S4 删除确认人工入口

用户正常退出blocked PID12396后，日志stopped=1、错误0、台账clean/0且无DesktopUp进程。首次创建独立removal库并启动PID34216，3会话Ready、1中心、revision3、3张、启动错误0；初始ID记入M07_CLOSEOUT。已请用户先执行取消、关闭中心、带确认托盘退出；尚不确认删除。下一步复用同库重启核对目录不变，再进入S5。本次只登记入口就绪，S4/S5不代填PASS。

## 2026-09-10 M07 S3 人工关闭

用户对blocked PID12396明确反馈“确认,验证通过,符合1～3”：不半展开、不推动上下障碍、按钮轻反馈且菜单仍可操作。日志也记录MinimumHeight=346/EffectiveHeight=292与280ms反馈，error/critical=0。加上此前chain PID37064三项，S3登记USER PASS。

本次检查PID12396仍在运行、stopped=0，已请求托盘正常退出以便切换独立removal场景；不声称已经干净退出。S4/S5仍待人工，M07不整体关闭，无生产代码或Git同步变更。下方待验记录是当时状态。

## 2026-09-10 M07 S3 空间不足人工入口

用户确认链式场景已正常退出；PID37064的application.stopped=1、error/critical=0、资源clean=true/0，启动前DesktopUp进程0。使用当前稳定入口打开blocked PID12396，3个poster_session.ready、1中心、启动错误0，App指纹C699…保持。本次启动PowerShell7.6.5、Elevated=true，不据此扩称其他环境。

人工检查对象是位于上下锁定障碍之间的A：点击展开时无半展开/无邻居位移、按钮轻反馈。当前只验证就绪，待用户画面反馈；S4/S5尚未启动。本轮未改生产代码、未commit/push。

## 2026-09-10 M07 收口自动证据

人工补充：本轮chain PID37064三海报/单中心启动后，用户对“未调整邻居归位、主动拖过不强拉回、锁定不推动”明确回复“验证通过与预期一致”。S3链式三项USER PASS；blocked反馈与S4/S5仍PENDING。已请求正常退出后切换场景。

[收口记录](M07_CLOSEOUT.md)：稳定App保持C699…/源53ebdb6，本轮仅入口、采样、验证编排和文档变更。508/508，PS5.1启动/11脚本AST/24指纹及PS7只读兼容通过；PID29280三海报/一中心/一托盘/父环境不变/clean0。
[完整资源基线](M07_RESOURCE_BASELINE_20260910.md)：36/36有效、0warning/error/重叠、全部正常退出/资源0、日志哈希匹配；单海报内存154～168MiB超50MiB目标，预算不改。资源原始根resource-20260910-155523-6baa31，未混用旧样本。
空间PID31040/38784/2564：1/3/6张、2/6/12命令、60/180/360帧，覆盖/布局/恢复通过。安全PID47056/35112/19624（第二实例16544）/29348/39136/35744/25936：七项确认、CAS/删除、重启/零目录通过，均exit0/clean0。
本轮不重启Explorer、不移动桌面文件；自动证据不替代S3～S5真实鼠标。快速最后意图、完整减少动效、自动收展触发仍按收口§5明确保留，未声称实现；M07 OPEN，未commit/push。
以下为前轮历史，最新边界以上节与HANDOFF为准。

## 2026-09-10 当前结论：R4用户确认，清理回归通过

用户确认原白条问题看起来已解决，R4 PID49284随后正常退出。人工确认范围是该故障，不补写完整M07/混合DPI/长期性能PASS。
方法论已固化，移除临时诊断partial/API/菜单/重绘、3项诊断测试和8个分叉启动/清单文件；11个原文件在本地out/archive可恢复，旧包/故障日志/日常数据保留。有效容量/视口/宿主/连续性测试不删。
清理版App/fixture Release0警告0错误，508/508、0失败0跳过；PS5.1默认240帧和自定义400帧8命令通过。上扩108帧、自适应84帧、慢速第12帧退出取消与3海报参数回归通过，六组exit0/clean/资源0。原生覆盖门不等于屏幕逐帧认证。
详细目录、PID与指纹见 [R4 §6](M07_ANIMATION_R4.md#6-清理后独立回归)。当前入口 scripts/start-m07-animation.ps1；本轮授权同步现有探索分支，不合并main/dev/mainline。
M07仍OPEN：剩余安全/完整1/3/6资源基线按HANDOFF收口。以下为按时间保留的历史日志，“最新/当前/待验”只表示当时状态，不覆盖本节；R2/R3最终是白条FAIL，R4人工PENDING已由本次反馈取代。

## R4最新：原生覆盖门PASS，屏幕PENDING

[R4证据](M07_ANIMATION_R4.md)：511/511；PID22752/38956分别240/400帧及620/1020条覆盖样本零缺口；42240上扩600、41584受限450、29956慢速第10帧正常取消、17276主题更新通过。各exit0/clean/资源0。R3用户图白条10px，与原生220/root210实测匹配，R3不再是视觉待确认而是FAIL。R4修订画面仍待人工。

## 最新：R3合同与原生回归通过，视觉PENDING

[R3](M07_ANIMATION_R3.md)：505/505（r3.trx）；PID37836默认240帧，PID24724自定义400帧，所有稳定内容读回及最终释放通过；PID15832动画第18帧正常退出并恢复；PID10140三海报参数回归通过，均exit0/clean/资源0。原诊断PID41252三次原位重绘成功但用户画面无变化，不能误记PASS。R3实际像素工具1385未取到，人工待验。

状态：`M07 IN PROGRESS — CONTRACT/BUILD PASS；CURRENT-ENVIRONMENT / MANUAL PENDING`

最后更新：2026-09-10

## 最新：诊断基础通过，非修复验收

[呈现诊断记录](M07_PRESENTATION_DIAGNOSTIC.md)：493/493，Release0警告0错误，PS5.1入口。PID43032最终8命令/2624条布局窗口样本与3次原位重绘，几何/裁切/状态版本不变，退出clean0。实际像素未自动捕获；人工PID41252待异常前后截图。D1/D2仍待像素证据，白条修复未实施；不能将下方R2原生通过当作人工PASS。

## 最新人工结果：R2白条FAIL

用户补充静止截图`7e055a5c`，明确动画完全停止后仍有底部残留；失败范围升级为“过渡帧白条＋终态视觉残留”，不再是仅待区分的瞬时现象。未新增修复或视觉PASS。

用户截图`ae175fd7`仍有10px纯白带，实际PID44360/正确R2指纹；用户另报结束后背景变化，终态分类尚待补充。自动490项、240/400帧背景连接与提交结果不变，但不能说明白条消失。当前重新诊断，未产出新的修复包；详见[R2最新记录](M07_ANIMATION_R2.md)。下方“R2尚待复测”是本次反馈前状态。

## 当前：动画R2白条修订

[完整记录](M07_ANIMATION_R2.md)。用户认可R1顺畅度，但白条人工FAIL；R2尚待白条复测。完整490/490/0skip，App/fixture Release0警告0错误。PID46520默认240帧和PID6312自定义400帧原生提交/窗口背景连接通过；PID13644三海报参数更新同HWND/同Bounds/独立背景通过；PID42988动画18帧后正常取消/exit0/台账clean0。证据位于`out/verify/v3-04-m07-animation-r2/`及`out/manual/v3-04-m07-animation-r2/`。

启动`scripts/start-m07-animation-r2.ps1`，24项指纹。没有实际逐帧屏幕像素捕获，不以连接读回声称白条已经消失；R1/旧包、日常数据不改，无commit/push。以下为历史验证。

## Git同步前复验

本轮按用户授权准备当前分支检查点，未修改业务实现。完整488/488、0失败0跳过，结果`out/verify/git-sync-20260909/sync-20260909.trx`；Windows PowerShell5.1动画入口CheckOnly通过。空白检查通过，未发现常见私钥/访问令牌标记；运行输出位于被忽略的out目录。人工视觉门保持PENDING，不以Git同步代替验收。

## 动画R1（当前优先）

详见[M07_ANIMATION_R1](M07_ANIMATION_R1.md)。新增27项，完整488/488/0skip，`out/verify/v3-04-m07-animation/results/animation-full.trx`；Release App/fixture均0警告0错误。PS5.1实际读取默认/自定义JSON与含空格路径通过。

- PID32568、`20260909-174355-0bfa4a`：默认12px/16ms/240ms，8命令、240实际/计划帧，最大变化10px、0归位尾帧、无警告错误、exit0/clean/资源0。
- PID40764、`20260909-174617-d08b4a`：6px/8ms/320ms/linear，8命令、400实际/计划帧、最大变化6px，同样无尾帧/警告错误并干净退出。
- PID47036、`exit-during-5200b8`：首次动画19帧时按限时正常退出；surface-disposed取消，exit0/clean/资源0。原8步探针的surface-removed/未完成是该中断场景预期，不冒充8步通过。
- 无真实逐帧屏幕采样证据；用户留缝/顺畅度验收PENDING。原连续性和菜单R1指纹/入口保持，未覆盖旧包、修改日常数据或commit/push。

## 连续空间模型最终验证（当前优先）

- 用户选择：新障碍导致无法完整归位时原位不动，禁止部分回收。合同/23类矩阵、域模型与同事务生产接入见[M07连续性记录](M07_SPATIAL_CONTINUITY.md)。
- 完整461/461，0失败0跳过；TRX `out/verify/v3-04-m07-continuity/results/continuity-full-final.trx`，其中新增21域/7存储测试。连续序列包含分支合流、反向、人工介入、160种子混合宽度及seed85/143反例；这不是穷尽数学证明。
- 最终App与独立fixture Release均0警告0错误；23项指纹见[scripts清单](../../../../scripts/m07-continuity-build.json)。Windows PS5.1实际预检/Smoke通过。
- 最终原生PID42012，目录`out/manual/v3-04-m07-continuity/20260909-165704-6f3bbb`：8命令、80计划/实际成功帧、0仅归位尾帧；4次收起分别3/2/2/3个参与者同步；两种关闭顺序回初始位置；正常退出exit0、clean=true、资源0。
- schema v6迁移/备份/回滚/请求重放/实体完整读集CAS及元数据一致性有独立测试；旧v5指纹保持，旧菜单R1 fixture独立且实际生成版本5，未改用户日常库。
- 人工PID47704，`20260909-165927-2d9b3b`，日志fixture_ready=true；C/B/A就绪。真实鼠标/动画观感待反馈，不能用原生帧回调代替屏幕呈现/人工PASS。M07剩余安全门与完整资源矩阵仍OPEN，无commit/push。

下节诊断时“未改代码/未修复”等是历史记录；当前实现及待验边界以上节为准。

## 最新人工反馈与嵌套收展诊断

用户确认菜单“…”交互正常：原菜单交互故障 MANUAL PASS。最终自动原生6场景仍未完整执行，其余M07门不代填PASS。新增[嵌套收展归位问题](M07_NESTED_POSTURE_RETURN_DIAGNOSIS.md)仍OPEN：PID44940日志1855/1873/1891及对应成功结果确认C展开→B展开→C收起时仅C参与；归位资格要求目标完整记录等于历史TargetAfter，B姿态改变/A后继被推均失效。根因是条件归位合同未分离姿态与位移关系。本轮无代码修改、构建或新自动测试；433/433仍是上一轮菜单修复证据，不是本问题修复PASS。下节保留反馈前验证历史。

## 菜单修复R1（最新，实现/合同通过，真实命中待验证）

用户授权后已接共享临时菜单交互、全部输入岛让位、Popup桌面内层级及成对清理，保留旧宿主/透明视觉/版本校验。新增13项，首轮与最终均433/433/0skip；最终App独立Release0警告0错误，PS5.1新入口预检通过，原M07六指纹不变。指纹、代码、来源与命令见[R1记录](M07_MENU_REPAIR_R1.md)。

两次中间原生运行PID44128/23332在20260909-145404-b8c44c及145709-e5af52：Popup层级与三输入岛透明位读回通过，但15点均被外部窗口0xD0C74遮挡，整项不算PASS；均application.stopped/clean=true/资源0。最终产物完整原生6场景与真实鼠标仍待桌面区域露出后验证，M07保持OPEN，不把代码实现当人工完成。本轮未commit/push、未触碰日常配置/桌面文件。

## 菜单真实失败取证（最新，根因已确认、未修复）

用户截图反馈 B 的“图标＋进入”无效、“编辑”附近可交互。PID18716，日志 `out/manual/v3-04-m07-menu-diagnostic/20260909-141423-b14013/logs/desktopup.jsonl`：1734/1736行实收 `(2196,90)` 的WM_LBUTTONDOWN/UP于B输入HWND `0x40E10`；1738/1739行实际派发Item1/GHOST SIGNAL。菜单版本仍current=true，无菜单guard拒绝；1826行 `(2234,90)` 则实际产生菜单PointerEntered。卡片右缘x2232与滚动输入带左缘x2248说明约16像素可交互空隙，不是文字控件被拆分。

同一失败记录Z序：C输入440/视觉441、B输入446、B Popup447、B视觉452，确认本海报输入岛抢菜单命中及邻海报遮挡。独立C样本362/364行有菜单PointerPressed/edit accepted=true，C Editing的392行Popup在前、菜单左部可hover，故不能把Editing本身当必然故障。原AutomationPeer字段不符合本样本屏幕框，结论改用原生窗口与实际Pointer局部/屏幕坐标交叉验证；不把观测字段偏移误判为产品坐标根因。

根因、完整行号/trace、保留边界与拟定修复范围见[诊断](M07_MENU_INPUT_DIAGNOSIS.md)。本轮只有只读检查与文档更新；未实施修复、未操控App、未重建或重跑测试，420/420仍是前轮诊断边界证据。M07 OPEN，不再标为“尚无实际失败点”，也不冒充修复或退出清理PASS。未commit/push。

## 菜单根因取证准备（以下为历史，未修复）

修复前基线11e7f1a已同步远程；本轮按用户要求加隔离观测日志，未调整命中、层级、窗口样式或MenuCurrent判断。最终420/420、0skip（`out/verify/v3-04-m07-menu-diagnostic/results/m07-menu-diagnostic-final.trx`），独立App/SessionFixture Release0warning/0error，Windows PowerShell5.1 `start-m07-menu-diagnostic.ps1 -CheckOnly`通过，旧M07六指纹一致。首次诊断编译出现lambda下划线参数与discard类型冲突，已改诊断参数名并重建；不属于产品缺陷修复。

computer-use初始化及reset重试仍1385，未做UI操作；原PID48408尚在运行，未强杀/并开诊断版。实际启动、菜单矩形/消息采样、失败点和退出清理仍待执行。新脚本和9项边界测试不能替代真实输入证据；根因未确认，M07保持OPEN。步骤、指纹、信息最小化与条件性修复矩阵见[诊断最新节](M07_MENU_INPUT_DIAGNOSIS.md)。本轮仅本地修改，未再次commit/push。

## M07 测量合同与前置安全门（当前，2026-09-09）

详见[M07完整合同/数据/启动说明](M07_MEASUREMENT_AND_SAFETY.md)。本轮自动411/411（新增6），TRX `out/verify/v3-04-m07/results/m07-contract.trx` SHA256 `82E4E52CB956E825275FC5713C922D86B9E8D380E6D29ECC453BF9AD4CA819C1`。Release `out/verify/v3-04-m07/app/`0warning/0error，6指纹见[scripts/m07-build.json](../../../../scripts/m07-build.json)。原4脚本预检记录保留；新5.1启动回归通过，详见下方。

历史：测量工具普通权限CreateProcessWithLogonW失败1385；管理员路径初次4格完成后Ctrl+C停止调度。前4格PID48012/47044/13772/47996有正常退出clean/0，工作集中位159.68～167.42MiB、CPU归一0～0.0482秒/10秒；第5格n1-collapsed-center0-r1中断，无stopped/measurement、旧台账clean=false/7，作为无效证据保留，不算退出PASS。该轮结束时无DesktopUp进程。`out/verify/v3-04-m07/measurements-r1/`环境Elevated=true，**不属于正式普通权限基线**。当时管理员硬门现已按用户要求取消，权限仅记录；0 CPU是计数器精度下无可见增量，不是绝对无CPU活动。

额外只读/无UI验证：六张expanded/collapsed/hidden的CLI隔离生成均revision6、6个独立ID（fixture-six-*），不等同六张窗口或性能通过。后续采样可用stop-after-current停止，避免直接中断进程。所有中断证据原样保留。

完整18格×2资源矩阵待补；当前执行改为先人工、不要求更换权限或PowerShell7.4。Windows PowerShell5.1.26100.8875启动回归PID47912通过：3会话、1中心、1托盘、exit0、无错误、台账clean/0、父环境不变，证据`out/verify/v3-04-m07/launcher-20260909-121424-23a691/result.json`。此前PID46428的测试器退出码null记录保留（程序日志正常），修正提前保留进程句柄后重跑通过。

5.1真实单海报六格采样兼容回归均Valid=true、退出完整，目录`out/verify/v3-04-m07/ps51-launcher-samples-20260909-121448`，仅一次重复，不混为完整36次。合同复跑`m07-ps51.trx`411/411、0skip。权限实际为Elevated=true，只证明当前环境；无GPU/屏幕呈现FPS/鼠标到光子数据。50MiB目标未改。

最新用户确认：上一轮1～2项通过，记S1基础交互/视觉和S2桌面生命周期USER PASS；另报“…”菜单部分选项命中异常，作为独立遗留OPEN，不把S1标为菜单全部通过。S3～S5仍MANUAL PENDING。M04既有G1～G4与收展策略认可保留，M07不关闭。

本次只读诊断：PID48072日志12:27:21.105菜单打开到12:27:28.521关闭期间，底层Item收到4次点击；期间无geometry_committed/state_projection.published/input_stale。代码中的原生输入路由没有活动Popup隔离；但日志缺少鼠标点及接收HWND，尚不能证实具体失败点击发生在菜单矩形内或仅由靠边触发。详见[菜单输入诊断](M07_MENU_INPUT_DIAGNOSIS.md)。本次仅同步反馈和诊断文档，无实现修改/构建/运行App/日常数据变更/commit/push。

追加诊断：用户确认菜单项不高亮，重复点击偶发成功，进入Editing后完成编辑/锁定/中心/删除都不能可靠操作。代码确认Editing添加整面EditSurface并合入原生输入区域；PID48408日志12:48:40.002进入Editing成功，12:49:54.648菜单打开至12:50:00前有13次底层Item响应、无菜单关闭/模式变化/状态发布/几何提交。菜单命中通路与输入岛隔离为高置信度方向，尚无原生接收窗口实测；computer-use初始化两次1385，未执行任何UI动作。只更新诊断文档，既有411项未重跑，不冒充修复PASS。

启动兼容最终回归：PowerShell7.6.5 PID41616与5.1 PID47912均3会话/1中心/1托盘、exit0、clean0；7脚本AST及固定6产物通过。5.1空间计时1/3/6张分别PID42984/37208/43192、2/6/12命令、20/60/120实际提交，全部PASS，见`out/verify/v3-04-m07/spatial-current-env-20260909-121733-7529e4`。这关闭脚本兼容问题与当前环境空间计时子门，不关闭人工或完整资源矩阵。采样单海报工作集中位138.85～165.27MiB、CPU归一最高0.13535秒/10秒，预算差距保留，不据此启动优化。测试结束进程0，git diff --check通过；人工可直接启动。

上一轮收尾记录：收尾检查：25份变更Markdown、257个本地链接、0缺失；git diff --check exit0。资源与时序入口固定6产物预检通过；当前DesktopUp进程0，standard-*普通权限目录0，普通权限矩阵尚未开始。 本轮已按用户最新决定取消权限启动门，当前状态以上方兼容回归及人工待验证为准。

## M06 多实例合同夹具（历史已完成，2026-09-09）

任务范围见[M06合同](M06_MULTI_INSTANCE_FIXTURES.md)。用户确认旧版正常退出，进程检查无DesktopUp后，先补M05参数两轮及空间回归，再执行M06 Exercise/Restart，全部PASS。M05/M06依实际证据关闭；没有强杀旧版或将测试意图转发给旧版。真实进程读回不等同人工鼠标/视觉验收。

| 验证 | 结果/证据 |
| --- | --- |
| M06新增合同 | 10项（含start/apply/recover三种故障阶段）通过；共享配置、隐藏参数、确认/CAS、晚到回调、独立恢复和零目录 |
| 完整回归 | 首轮及2026-09-09复跑均405/405，0skip；out/verify/v3-04-m06/results/m06-contract.trx、m06-resume.trx |
| 测试SHA256 | 首轮6248A8805E5C3AC06321BF5F330467C739055451B00A93F0EEA341C3D1DCE12A；复跑6C2E5529BB5DD4C38BAC917FFDF8E7AC8403A9D713C84D4DC40B3ED313CE2592 |
| Release | out/verify/v3-04-m06/app/；0warning/0error |
| 隔离配置 | out/verify/v3-04-m06/smoke/multi-instance/；初始revision3/三张，完成后A/B两条且仅A显示、C删除；不能再次直接Exercise |
| Exercise | PID44928，9阶段快照完整；A编辑、B隐藏编辑/重显、C取消/过期/确认删除及晚到命令均通过；单实例短进程PID48160 exit0 |
| 控制中心/托盘 | Exercise中心顺序关闭重开共创建2次，任一快照仅1个活动中心；托盘全程1次，三端目录/revision与原生/XAML读回一致 |
| Restart | PID47076，完整PosterDefinition与Exercise final精确一致；2条目录/1张原生海报，1中心/1托盘；隐藏/删除项不复活 |
| 退出 | 两个M06主进程exit0、无error/critical、台账clean/0；结束后无DesktopUp进程 |
| 文档/差异检查 | 本次24份变更Markdown、239个本地链接、0缺失；git diff --check exit0（仅既有换行转换提示） |
| M05尾门 | 固定M05参数PID43944/45260、空间PID45428，全部PASS，详情见下方 |

初次共享数据编译缺少Application命名空间；首轮App夹具误用命令接口读取删除预览，已改查询接口（引起的XAML编译次生错误同时消失）。最终合同/Release通过，未修改生产仓储、空间或注册表行为来掩盖测试失败。

M06最终产物SHA256：

| 产物 | SHA256 |
| --- | --- |
| DesktopUp.exe | 958D77CA81E0035094D2A603DFCD3DBBE595D048F303E574B65EF14AB32BB3A0 |
| DesktopUp.dll | 61BA4BCE0386CA2543115A439F530DDA946F920FF8D5E269D80AA8A66CFF1661 |
| DesktopUp.Domain.dll | 2C9F447DA4BCA74260784B8741303455B67155669CB1BE0B474071C58AFF2021 |
| DesktopUp.Application.dll | E27135B685CDCA1EC68BF79D5F5B2C482B4EE4714EADF12FB534075DAD614F40 |
| DesktopUp.Windows.dll | 0B4445F449483389950A34FCD789F54600A5D3402FB8756A91754AA7C0F1EE8E |
| DesktopUp.Infrastructure.dll | 01B7E50E767BFB08C494928F038D60C0C9AB9E495FB3B6F1985FAE879EEB6798 |

无commit/push、无旧测试数据覆盖、无日常库或桌面文件修改。局部失败是可重复替身会话证据，不等同Explorer重启；实际菜单/点击/视觉余项保留。M07未开始。

2026-09-09续跑未修改生产代码或替换固定Release。进程日志相对仓库位置及SHA256：

- `out/verify/v3-04-m06/smoke/multi-instance/logs/desktopup.jsonl`：`BB05075E4309C64A238A555894A8722E1D5D4AB70AFA97BD76A188BFE4E23AD9`。
- `out/verify/v3-04-m05/smoke/three-posters/logs/desktopup.jsonl`：`12B4D33506CF6E91F197785DBC7E9E029ACD7225AB4C505BE0E5EEEF280C2C21`。

上述哈希对应本次结束时日志；后续测试若追加日志不能沿用旧哈希。每项结果按PID限定，不把历史事件算入当前PASS。

## M05 最小内容/主题参数（前轮实现；2026-09-09进程尾门已补齐）

用户明确进入M05，只做真实Item接入必需参数基础；收展策略体验认可、动画延期，M04未逐项确认安全格保留。实现范围见[M05实际合同](M05_PRESENTATION_PARAMETERS.md)。分支impl/poster-redesign-V3，HEAD a6ac6cf＋既有M04 dirty及本轮增量；无commit/push。

| 检查 | 结果/证据 |
| --- | --- |
| 新域/应用合同接入、未接UI时旧回归 | 356/356；out/verify/v3-04-m05/results/m05-existing.trx |
| 接UI与新增参数夹具首轮 | 394/395；m05-first.trx，唯一失败为ArchitectureTests旧断言写死RGB；新39项全通过 |
| 最终完整合同测试 | 395/395，0skip；m05-final.trx SHA256 B5AADC8F46310CC34B57813758097964999138DF177796FBB75CA516C070AEB0 |
| 最终Release | out/verify/v3-04-m05/app/，0warning/0error；保留prepared-before-bounds和原层级/捕获实现 |
| 隔离数据准备 | out/verify/v3-04-m05/smoke/three-posters；SessionFixture创建，初始revision3/三张，未改日常配置 |
| 参数烟测首次 | PID43944，3张，启动已保存v1数0；提交后revision6，同HWND/外框、独立实例、XAML及非法草稿拒绝全部true |
| 参数烟测重启 | PID45260，启动已保存v1数3；相同读回全部true，revision仍6，无幂等额外写入 |
| 空间回归 | PID45428，4次姿态命令、56个原生帧、两次Full展开（有效高480 DIP），几何断言通过 |
| 三轮退出 | 全部exit0、台账clean/0；M05关闭，不冒充鼠标/视觉PASS |
| 文档/差异检查 | 本轮结束前23份变更Markdown、220个本地链接、0缺失；git diff --check通过 |

新增39项覆盖默认/边界/非法或重复JSON/未知模板和版本、旧数据保留、大小/深度入口、目标草稿与A/B无关CAS、同实体冲突/重复/NoChange、仓储重开持久化、克隆隔离、参数不夹带几何、目标度量/DPI与有效视口/恢复。深度16为解析器限制；不是所有安全能力都有独立攻击测试。本轮没有添加真实ContentItem或图标提取器，六张卡仍是DEMO ITEMS。

最小真实窗口验证**已通过**：2026-09-09用户正常退出旧R2后，已在同一隔离库依次执行两次（以下保留复现命令）：

```powershell
& 'G:\A_\Tools\DesktopUp_V2\scripts\smoke-presentation.ps1' `
  -ExecutablePath 'G:\A_\Tools\DesktopUp_V2\out\verify\v3-04-m05\app\DesktopUp.exe' `
  -DataRoot 'G:\A_\Tools\DesktopUp_V2\out\verify\v3-04-m05\smoke\three-posters'
```

第一次改三套独立参数并读回XAML/窗口身份/外框，第二次从已保存参数启动后复验；随后复用`scripts/smoke-spatial.ps1`在这份隔离库回归不同模板的收展，三轮均通过。进程读回不是人工视觉/鼠标PASS；窗口核心视觉安全门继续保留至M07。

最终6产物SHA256：

| 产物 | SHA256 |
| --- | --- |
| DesktopUp.exe | 958D77CA81E0035094D2A603DFCD3DBBE595D048F303E574B65EF14AB32BB3A0 |
| DesktopUp.dll | 0DA2275AC59B66362D76A3203BD22F03BA83D1990F43BDDF1DA9DE15ED40D4DB |
| DesktopUp.Domain.dll | 2C9F447DA4BCA74260784B8741303455B67155669CB1BE0B474071C58AFF2021 |
| DesktopUp.Application.dll | E27135B685CDCA1EC68BF79D5F5B2C482B4EE4714EADF12FB534075DAD614F40 |
| DesktopUp.Windows.dll | 0B4445F449483389950A34FCD789F54600A5D3402FB8756A91754AA7C0F1EE8E |
| DesktopUp.Infrastructure.dll | 01B7E50E767BFB08C494928F038D60C0C9AB9E495FB3B6F1985FAE879EEB6798 |

## M04-07 R2 向下优先修订（历史）

根因：SpatialExpansionPolicy在最大向下候选失败后，先返回可行的纯上全高候选，绕过下方剩余空隙；旧例子甚至把60..660写成正确终点。本次删除此提前返回，求下方合法容量后再上补足，复用原推让/两段路径/批次和状态CAS。三份需求、空间设计、计划与 [完整策略矩阵](../../20_Architecture/POSTER_SPATIAL_STRATEGY_MATRIX_V1.md) 同步。

- 修订前定向24项：10失败/14通过，证据`out/verify/v3-04-m04-07-r2-down-first/results/down-first-before.trx`。
- 修订后完整356/356、0跳过；`out/verify/v3-04-m04-07-r2-down-first/results/down-first-final.trx`，SHA256 `024DE5F16233B6F8C22B53554E0424258216F64553231D4981E6B3B6E3890BFA`。
- 新增9项：下方0/1/140/300像素、四DPI与负工作区原点、可推邻居余量及收起回到锚点；原80组穷举同时验证最大高度与同高最大下扩量。全部连续路径安全检查和R1归位测试继续执行。
- Release `out/verify/v3-04-m04-07-r2-down-first/app/`，0warning/0error；未改schema、Host层级、输入捕获或真实文件。旧R2包/配置不覆盖。

真实隔离进程（主屏2560×1392工作区、96 DPI），均exit0、资源clean/0；根目录`out/verify/v3-04-m04-07-r2-down-first/smoke/<场景>/`，`logs/desktopup.jsonl`为详细证据。

| 场景 | PID | 已验证结果 |
| --- | --- | --- |
| down-first | 16544 | 2次/36帧；A800..860先下扩132，再上扩408，终点392..992、高600；physical读回与共享投影一致，B不动；收起回到800..860 |
| constrained | 45480 | 2次/28帧；He450 DIP、保存Hp600，刷新/原生读回一致 |
| blocked | 37488 | 0次/0帧；InsufficientSpace，revision/物理状态不变；重复提示合并 |
| return-sync | 36364 | 4次/56帧；B收起14帧中13帧与A共同移动，归位专用尾帧0，源/邻居回到合法结果 |

首次smoke被用户正在运行的旧R2 PID32916拦截，未强杀或改其配置；用户确认正常退出后执行上表。人工fixture批量脚本第一次参数拼接错误，在参数校验处退出、未写数据；修正后另建8套全新数据。上述smoke为内置命令/原生读回，不是人工鼠标、动画观感PASS。

新版6产物SHA256（启动脚本固定）：

| 产物 | SHA256 |
| --- | --- |
| DesktopUp.exe | 958D77CA81E0035094D2A603DFCD3DBBE595D048F303E574B65EF14AB32BB3A0 |
| DesktopUp.dll | 75C2C4BB82DC11136850E4D2CB3E7CF9F1800398AF7B2D756CD45467D591846C |
| DesktopUp.Domain.dll | 92D2F76FB78858BEAF94C6168CDCA603FD71300A3E5C98302F39AA7A48B39F36 |
| DesktopUp.Application.dll | 34B8C88DB2EBB995C3C6C391D6F05BDF76889637F576F3516ED62DE60D8594E8 |
| DesktopUp.Windows.dll | F8AC596994670DA230BC4EA1FF523344877FAE3CAD061DED3088E1C562E0E65F |
| DesktopUp.Infrastructure.dll | 9C7995E1029EA5341843C3F44A5737211A6BC4CB5DC541D7233C17521B67B5D6 |

人工目录`out/manual/v3-04-m04-07-r2-down-first/`；启动参数expand-down-first与历史别名expand-up映射到同一新隔离配置，默认先验本次修正；其余7套供余项复用。G1～G4历史PASS保留，M04仍MANUAL PENDING，不提前进入M05，未commit/push。

收尾检查：9个参数入口（8套数据，expand-up为别名）全部CheckOnly PASS、Launched=false、每次固定6个二进制；脚本语法0错误；22份修改Markdown的205个本地链接0缺失；git diff --check通过，DesktopUp进程0。未执行新一轮人工点击或重启恢复，本次重启背景证据仍引用下方前轮记录，不记为新版重测PASS。

以下R2/347项和纯上进程是前一轮历史，不代表当前策略或当前启动产物。

## M04-07 R2 实现验证

R2-01～03已实现，完整347/347、0跳过，Release0warning0error。结果为out/verify/v3-04-m04-07-r2/results/r2-final-2.trx；合同与失效边界见 [R2实际合同](M04_EXPANSION_R2.md)。新的真实进程明细见下方补充；鼠标、滚动/页脚观感和按钮轻抖仍MANUAL PENDING。

初次全量337通过/1失败为既有ConcurrentFirstRunSeedsTheLegacyDefaultOnlyOnce的SQLite table locked；单测复跑和后续完整347项通过，未更改并发存储代码。R2新夹具先发现旧短海报恢复处理、恢复种子缺失键零尺寸，以及锁定邻居规划顺序问题，修复后新增对应断言；中间TRX与PID46856的恢复失败日志保留，不计PASS。

中间进程40244受限展开450/保存600、2次28帧exit0/资源0；42612保留展开后退出，下一旧构建46856恢复失败。修复后的最终Release重启PID44896通过、2次26帧，重新得到450 DIP有效高度且意图600不变。完整上下候选/不足/归位按最终Release继续验证。

### R2 最终原生回归与指纹

收尾：八套R2人工配置均CheckOnly通过（Launched=false），启动脚本语法0错误；Markdown本地文件链接0缺失，git diff --check通过。所有DesktopUp烟测已退出，无常驻实例；HEAD仍a6ac6cf，未commit/push，旧R1产物/配置保留。

所有以下进程均exit0、资源账本clean/0；均为隔离内置命令及原生读回，不是人工视觉通过。

| 场景 | PID | 结果 |
| --- | --- | --- |
| 保留受限展开后重新启动 | 44896 | 2次/26帧；新会话重算450 DIP、保存600 DIP，刷新读回一致 |
| 向上完整 | 13584 | 2次/34帧，Full600 |
| 上下组合完整 | 44824 | 2次/36帧，Full600 |
| 空间不足 | 38996 | 0次布局/0帧，revision与物理场景不变；连续反馈请求只启动1个提示 |
| 双收起同步归位 | 40248 | 4次/56帧；Full480，R1路径保留 |
| 三海报链 | 46088 | 6次/68帧；Full480/360/360 |

最终TRX SHA256：8985E3711FCF76E0CD7FE64EB81350CA0266F5DB616A8FA3BB0CA8F080E5830B。
固定脚本中的六产物SHA256：exe 958D77CA81E0035094D2A603DFCD3DBBE595D048F303E574B65EF14AB32BB3A0；App C8BE02A38435B97400692B878A531C305B9BA14B997C91EEE21435C36064586A；Domain 3B3A21696F7F804323515E8504F83FFB6463773FA3E15AE3CFE9471BBE4727BF；Application 34B8C88DB2EBB995C3C6C391D6F05BDF76889637F576F3516ED62DE60D8594E8；Windows F8AC596994670DA230BC4EA1FF523344877FAE3CAD061DED3088E1C562E0E65F；Infrastructure 9C7995E1029EA5341843C3F44A5737211A6BC4CB5DC541D7233C17521B67B5D6。

## M04-07 R2 策略修订历史（当时非软件验证）

用户确认三档展开回退、完整标题＋一行Item＋必要页脚最小值。本轮同步需求/策略/模块所有权/计划与G7-R2待实现验收，不改代码或软件产物，不运行软件测试/真实窗口；R1的324/324和进程证据不能转记为新策略AUTO PASS。

当前仍使用R1启动包。下一步先按工作流R2-01实现纯候选与度量夹具，再有效投影/反馈/集中回归；G1～G4人工通过保留，未反馈余项仍待确认。本轮不触碰日常数据、测试SQLite和Git提交/推送。

收尾检查：src/tests/scripts共105个文件前后SHA-256一致，无新增/删除代码文件；47份Markdown中的203个本地文件链接0缺失，git diff --check通过；4个策略算例算术核对一致（非软件测试）。分支impl/poster-redesign-V3、HEAD a6ac6cf未变。

## M04-07 R1 同步归位修订

- 用户明确反馈G1～G4人工通过；归位同步调整已确认。G6-R1和G5～G10未自动填PASS。
- 改前新10项时序/保护测试7失败3通过，`out/verify/v3-04-m04-07-r1/results/return-red.trx`保留；原因是先source全部收起帧再邻居回程帧。初次补丁的C#上下文关键字编译错误已修正，未将失败构建作为产物。
- 修后完整324/324、0跳过；新增11项含200组混合高度/方向/负原点链场景。逐帧证明同批、无尾帧、单步≤32px、不重叠、最终归位，以及锁定/二次调整/新障碍保持原位、未改子集仍可返回。
- Release0warning0error，目录`out/verify/v3-04-m04-07-r1/app/`；最终测试`results/m04-07-r1.trx` SHA256 `90CCF006C410B1E8880D27EC4A20CFB44F332253ECB0D80258816BEEDE025EF6`。
- 真实双收起PID42412：4次收展/56帧，B收起14帧/13个联合帧/0归位尾帧；真实链式PID43036：6次/52帧，首组收起14帧全部联合移动。两者exit0/资源0，没有批次失败。这里是内置命令、原生读回与日志证据，非真实鼠标或视觉PASS。
- 四套新版人工配置与六产物指纹检查通过，脚本语法0错误，CheckOnly均Launched=false；旧版产物/人工目录未覆盖，当前无进程常驻。无schema/真实文件改动，无commit/push。
- 下一步见 [集中人工记录表](M04_MANUAL_VALIDATION.md)：先G6-R1同步观感，再G5～G10；全部适用项通过才关闭M04、进入M05参数。

## M04-07 集中人工准备记录（历史）

固定上一轮最终Release，6个项目二进制指纹已验证。三套新夹具interaction/chain/removal均生成revision3/三张海报；人工启动脚本语法0错误、三场景CheckOnly通过（Launched=false）。没有生产改动或重新构建Release。

电脑交互初始化和重置重试失败：CreateProcessWithLogonW1385；没有取得任何窗口截图或执行输入。本轮不补写鼠标/菜单/删除点击/视觉/Win+D/Explorer人工PASS。所有G1～G10结果仍为待反馈，见 [人工记录表](M04_MANUAL_VALIDATION.md)。当前没有运行中的DesktopUp测试实例。

## M04-06 最新实现与验证

313/313 PASS（上一轮288基础新增25）；最终Release0warning0error；无新增schema。输入捕获/几何失效/顺序路由与删除ticket/真实SQLite CAS详见 [M04-06实际合同](M04_INPUT_CONFIRMATION.md)。

最终真实进程：PID19540，三张海报、三个就绪输入cluster、打开确认3次，取消/重复请求/中心关闭重开/带确认退出全部通过，revision保持3、exit0/资源0；PID24776空间回归6次/77帧，exit0/资源0。中间确认构建45548/38228通过，不替代最终产物。保留原始日志。

真实鼠标路由、菜单点击与点击删除、焦点层级、白底/闪烁为M04-07人工门，不以程序化XAML开关/日志冒充通过。没有自动确认删除；没有改用户日常配置/桌面文件，也没有commit/push。

收尾已将逻辑输入失效同步为原生空区域，防旧输入窗吞掉桌面点击；上列19540/24776已在此补强后重新执行。46份Markdown/182个本地链接0缺失，git diff --check通过；测试实例均已退出。

## M04-03..05 最新实现与验证

按顺序完成03碰撞（260/260）、04保尺寸展开（271/271）、05推让归位与v5（最终288/288）。最终Release0warning0error；链式三海报PID24596，6次收展77批次帧，三参与者与归位消费关联明确；普通三海报PID46980，4次56帧；均exit0/资源0。详见 [空间运行合同](M04_SPATIAL_RUNTIME.md)。

初次真实测试发现原生批次标志87与中间高度输入越界，已修复；故障注入发现v5提交前结构校验缺口，已修复。失败原始日志保留。未执行真实鼠标或肉眼闪烁验收，人工归M04-07；M04-01/M03既有人工待确认不代填。

收尾文档检查：45份Markdown中的177个本地链接均可解析，0缺失；git diff --check通过。普通三海报控制中心重复激活回归PID40692：仅创建1个中心、2次显示、共享监听1，exit0/资源0；没有测试实例常驻。

## M04-02 最新实现与验证

空间纯合同与67项新测试已完成；全量243/243，0失败0跳过，完整Release0warning0error。实际代码、命令、TRX/产物SHA-256及首轮编译修正记录见 [M04空间合同](M04_SPATIAL_CONTRACT.md)。

覆盖DPI保守取整/负坐标、接触与正面积相交、连续路径（非仅终点）、8向候选、三种展开、超大/历史重叠明确拒绝、不可变/确定性、归位资格与路径分离、完整障碍实体/目录幻影/环境失效、异构端点信封。500组展开与500条路径性质对照在测试内执行。旧3项固定顶边/夹取测试加Legacy标签，仍执行。

边界：运行Host/WinUI/capture和SQLite migration未修改，新合同未接生产手势；没有运行桌面smoke或人工输入，不能将此次AUTO PASS解释为碰撞/动画/连锁推让/归位已在桌面生效。M04-03接拖缩，04接展开，05接推让/归位。M04-01人工结果仍待确认。本轮无用户数据变更、无commit/push，Git基线a6ac6cf。

## M04-01 最新实现与验证

中心完整列表/稳定ID选择、所选锁定/收展/显隐、进程内重开保留、版本绑定命令和运行状态已实现。完整176/176（新增13项）；最终Release0warning0error。最后三张PID20360/零张39740：目录3/0与真实会话一致，1中心/2次show、1共享监听、exit0/resources0；首次PID26340是列表焦点优化前开窗证据。TRX、最终hash和隔离目录见 [M04合同](M04_CONTROL_CENTER_SELECTION.md)。

实际点击未执行：computer-use初始化及reset重试均因CreateProcessWithLogonW1385退出；不能据此标记人工通过。未重启Explorer或运行Win+D输入，原核心门保留。测试为elevated=true，不代表普通权限认证；无schema/用户真实数据/桌面文件变更；未commit/push。[DeskBox吸收记录](DESKBOX_SPATIAL_REFERENCE.md) 为来源/适配边界，空间算法未接生产。

## 2026-09-07 多海报空间补充（文档变更）

用户确认目前多海报可正常交互，反馈中心仅A；不可重叠、收展推让及两阶段保尺寸展开新增为需求。明确回答：锁定者不可被推、固定障碍；未再调整者条件归位。未报告的Explorer/Win+D等不记PASS。

本轮只读当前中心/投影和DeskBox源码，修改需求、空间设计、计划与导航，没有应用代码或数据变更，没有重跑build/test/smoke，没有commit/push。历史163/163和0/1/3进程证据保留，不覆盖新增空间功能。中心根因和来源证据见 [空间设计](../../20_Architecture/POSTER_SPATIAL_LAYOUT_AND_EXPANSION_V1.md)。M04仍PLANNED；集中人工出口见细化工作流§4.1。

文档检查：本次18份Markdown中的107个本地文件链接均存在；按父章节分组检查无重复标题，无冲突标记；`git diff --check -- Docs` exit0。最初全文件同名标题检查提示总计划重复“工作项/出口门”等，经父章节核对为合法复用，未改写。§5三种展开算例算术核对3/3，仅验证文档公式，非应用自动测试或动画验收。

## V3-04-01 交付

- 建立 Application 层 `IStateStore`，SQLite API 仅存在于 Infrastructure；
- 建立 schema v1 的 migration ledger、单例 state head 和 operation journal；
- 建立 expected revision CAS、幂等重放和同事务业务 mutation 入口；
- 建立旧库迁移前在线备份、手动在线备份、临时文件验证和原子改名；
- 建立 `quick_check`、application id、user version、migration checksum、head/revision 验证；
- 损坏或不兼容状态不被删除或空白默认值替换；
- 状态初始化已置于托盘、控制中心和海报创建之前。

详细合同见 [STATE_STORE_SCHEMA_V1.md](STATE_STORE_SCHEMA_V1.md)，实现位置见 [CODE_ANCHORS.md](CODE_ANCHORS.md)。

## V3-04-02 交付

- 追加 immutable schema migration v2：`posters` 与单例 `app_settings`；
- 冻结 Poster/Settings 持久字段和明确的运行时非持久字段；
- 建立 `IPosterRepository`、`ISettingsRepository`、一致 snapshot 和结构化写入结果；
- 所有 repository 写入复用 global expected revision CAS，不绕过 V3-04-01 transaction；
- 建立 Application service 边界，海报 UI 不接触 SQLite 或具体 repository adapter；该临时端口在 V3-04-03 收敛为读写分离的 `IStateCommandService`/`IStateProjectionCoordinator`；
- 海报首次启动读取/创建 `poster-main`，并从 committed snapshot 恢复几何、锁定和收起；
- 拖动/缩放只在完成时提交一次，锁定/收起写入同一 Poster snapshot；
- settings-only revision 冲突只安全重基一次；Poster 行并发变化或失败时恢复最新 committed snapshot；
- 显示拓扑重投影不写数据库，编辑模式、输入岛、HWND 和物理像素不持久化。

字段与 revision 合同见 [POSTER_SETTINGS_REPOSITORY_V2.md](POSTER_SETTINGS_REPOSITORY_V2.md)。

V3-04-02 的普通退出/重启恢复门已获用户确认并进入 V3-04-03。

## V3-04-03 交付

- 建立分离的 `IStateCommandService` 与 `IStateProjectionCoordinator` Application 端口；
- 建立单事务 `IStateProjectionRepository` 聚合查询，Poster/Settings 不再跨 transaction 拼接；
- 建立 Poster、Control Center、Tray 三份同 global revision projection；
- 建立 request/source/kind/expected revision command 与八类明确结果；
- 对 stale revision、锁定缩放、收起缩放和夹带字段执行拒绝，不覆盖正式状态；
- 同 request ID 有界幂等重放，不二次提交或广播；
- Control Center 显示 committed revision、Poster/Settings 状态，并通过 command 切换锁定、收起和暂停；
- Tray tooltip、锁定/暂停勾选消费 TrayProjection，ready 前不允许状态写入；
- Poster 消费来自其他表面的 projection；settings-only revision 不触发物理帧重建；
- 收起/展开在 Application 核心换算目标锚点，保持 normalized top 不变；
- 三端均拒绝旧 projection，控制中心关闭重开先应用保存的最新正式 projection。

完整合同见 [COMMAND_QUERY_PROJECTION_V1.md](COMMAND_QUERY_PROJECTION_V1.md)。

## 自动测试

命令：

```powershell
dotnet test "G:\A_\Tools\DesktopUp_V2\tests\DesktopUp.Contracts.Tests\DesktopUp.Contracts.Tests.csproj" -c Release -o "G:\A_\Tools\DesktopUp_V2\out\verify\v3-04-01\tests-final"
```

V3-04-01 结果：`PASS — 81/81`。

新增八项真实 SQLite 夹具覆盖：

1. fresh schema 创建及重复初始化幂等；
2. CAS commit/conflict/replay；
3. 业务 mutation、revision 和 operation journal 整体回滚；
4. 两个 StateStore 实例共享同一数据库 revision；
5. 在线备份保留已提交 revision 并通过验证；
6. legacy v0 在迁移前产生可读备份；
7. 损坏数据库初始化失败且原字节不被替换。
8. migration ledger 名称/校验值被篡改时拒绝启动。

V3-04-02 命令：

```powershell
dotnet test "G:\A_\Tools\DesktopUp_V2\tests\DesktopUp.Contracts.Tests\DesktopUp.Contracts.Tests.csproj" -c Release -o "G:\A_\Tools\DesktopUp_V2\out\verify\v3-04-02\repositories-final"
```

结果：`PASS — 87/87`。新增六项真实 SQLite repository 夹具覆盖 schema v2 默认值、默认 Poster 创建、跨连接恢复、CAS 冲突/重放、Poster/Settings 共享 revision 及非法状态不推进 revision。

V3-04-03 命令：

```powershell
dotnet test "G:\A_\Tools\DesktopUp_V2\tests\DesktopUp.Contracts.Tests\DesktopUp.Contracts.Tests.csproj" -c Release -o "G:\A_\Tools\DesktopUp_V2\out\verify\v3-04-03\tests-final"
```

结果：`PASS — 97/97`。新增 command/projection 与 v1→v2 回归覆盖：三端同 revision、Control Center Poster command、Tray Settings command、stale 冲突、重复 request、锁定权限、单事务聚合查询、收起顶部保持、UI 端口边界、源版本备份及临时边车零残留。

## Release 构建

```powershell
dotnet build "G:\A_\Tools\DesktopUp_V2\src\DesktopUp.App\DesktopUp.App.csproj" -c Release -o "G:\A_\Tools\DesktopUp_V2\out\verify\v3-04-01\app-final"
```

结果：`PASS — 0 warning / 0 error`。

V3-04-02 Release 构建：

```powershell
dotnet build "G:\A_\Tools\DesktopUp_V2\src\DesktopUp.App\DesktopUp.App.csproj" -c Release -o "G:\A_\Tools\DesktopUp_V2\out\verify\v3-04-02\app-final"
```

结果：`PASS — 0 warning / 0 error`。

V3-04-03 R1 Release 构建（2026-09-07 起替换旧开窗失败产物）：

```powershell
& "G:\A_\Tools\DesktopUp_V2\scripts\build.ps1" -Configuration Release -OutputDirectory "out\verify\v3-04-03\app-final"
```

结果：`PASS — 0 warning / 0 error`。

## 真实启动顺序 smoke

从 `out/verify/v3-04-01/app-clean/DesktopUp.exe` 以 `--exit-after-ms=5000` 启动，PID 45840。结果：

- 创建 `%LOCALAPPDATA%\DesktopUp\V3\state\desktopup.sqlite3`，大小 24576 bytes；
- 结构日志顺序为 `state_store.initialized(status=Created, schemaVersion=1, revision=0)`、`tray.created`、`poster.shown`；
- 退出记录 `application.stopped(activeResources=0)`；
- 退出后无 DesktopUp 进程残留。

## V3-04-02 人工重启门

使用 `out/verify/v3-04-02/app-final/DesktopUp.exe`：

1. 拖动并八向缩放海报后普通退出、重新启动，位置和尺寸恢复；
2. 切换锁定后普通退出、重新启动，锁定状态恢复且使用模式不能拖动；
3. 切换收起后普通退出、重新启动，收起状态及展开高度恢复；
4. 反复拖动/缩放期间无逐帧数据库写入引起的卡顿、白底或层级回退；
5. 退出后无进程或窗口残留。

结果：用户确认通过并要求进入 V3-04-03。

## V3-04-03 真实 smoke

- 首轮真实 v1 数据库迁移发现迁移前备份错误按 v2 校验；启动被安全拒绝，原数据库未被覆盖；
- 已改为迁移前备份按源 schema 校验、普通备份按当前 schema 校验，并加入 v1→v2 自动回归；
- 同时修复临时备份校验产生的 `.tmp-wal/.tmp-shm` 边车残留，并清理本轮 smoke 产生的精确临时文件；
- 修订后 PID 31072 成功创建 v1 备份并迁移至 v2；
- 最终 PID 16392 从 schema v2/revision 1 正常启动，发布 System projection revision 1，挂载 Poster 后正常退出；
- 最终 `activeResources=0`、DesktopUp 进程 0、`*.tmp*` 备份残留 0。

## V3-04-03 R1：托盘双击崩溃回归（2026-09-07）

历史故障：原人工门首项失败，双击托盘打不开控制中心并使整个进程崩溃。当时本项保持未关闭，不以 97 项合同测试替代真实开窗；后续用户人工确认见本文件末尾。

根因证据：

- Windows Application 事件 1026（2026-09-07 10:35:26，PID 5788）记录 `XamlParseException → ControlCenterWindow.InitializeComponent → ControlCenterService.Show → H.NotifyIcon.TaskbarIcon.OnMouseEvent`；
- 用 Windows SDK `makepri dump` 检查原 `app-final/DesktopUp.pri`，其 XBF 名称是 `app-finalApp.xbf`、`app-finalControlCenterWindow.xbf`；而生成的连接器查找 `ms-appx:///App.xaml`、`ms-appx:///ControlCenterWindow.xaml`；
- 实际 WinUI 依赖为 `Microsoft.WindowsAppSDK.WinUI/2.3.6`。其 `Prep_ComputeProcessXamlFiles` 使用 `$(OutputPath)` 直接拼接 XAML 相对文件名；先前 `dotnet build -o .../app-final` 的全局 OutputPath 缺末尾目录分隔符，导致 XBF 被写到输出目录旁边并以错误名字打包；
- 无参构造对照不能解决（PID 23832）；仅给输出目录补分隔符后，真实控制中心成功打开（PID 31884）。此问题不需要修改海报层级、数据库或主题样式。

修复：

- `scripts/build.ps1 -OutputDirectory` 统一解析绝对输出目录并补末尾分隔符，且检查 restore/build 退出码；
- App 工程增加 `DUV3001` 构建检查，禁止缺分隔符的自定义 OutputPath 再静默生成错误 XBF；
- 托盘和启动意图共用 UI Dispatcher 开窗入口；窗口分为 native Window 构造和 XAML 初始化，以便失败时关闭未完成窗口；
- 开窗局部异常记录阶段、HRESULT、堆栈和 restricted description，清理失败窗口，不让异常越过原生托盘回调；XAML 资源解析和控制中心应用 projection 增加日志；
- 新增 `scripts/smoke-control-center.ps1`，必须实际创建控制中心、显示 projection、再次激活复用唯一窗口、保持一张海报并零残留退出；仅进程存活不再算该 smoke 通过。

自动证据：

- 非法输出目录构建按预期失败，错误码 `DUV3001`；规范入口 Release 0 warning / 0 error；
- 修订产物 PRI 名称恢复为 `App.xbf`、`ControlCenterWindow.xbf`；
- 去掉无效构造实验后的修订版（PID 22768）创建控制中心 1 次、显示请求 2 次、应用 projection 1 次、海报 1 张，exit code 0，activeResources 0；
- 现有合同测试 `97/97 PASS`（`out/verify/v3-04-03/tests-tray-fix/`）；
- 失败隔离对照（PID 18980）仍检测到 XAML 错误，但程序不崩溃，限时正常退出且 activeResources 0；
- 用户原启动目录 `out/verify/v3-04-03/app-final/` 已重建并完成相同 smoke（PID 13440）：控制中心创建 1 次、显示请求 2 次、应用 projection 1 次、海报 1 张，exit code 0，activeResources 0；该目录 PRI 也确认是正确的 `App.xbf` 与 `ControlCenterWindow.xbf`。

运行真实开窗 smoke：

```powershell
& "G:\A_\Tools\DesktopUp_V2\scripts\smoke-control-center.ps1" -ExecutablePath "G:\A_\Tools\DesktopUp_V2\out\verify\v3-04-03\app-final\DesktopUp.exe"
```

限制：Windows computer-use 初始化两次均失败于 `CreateProcessWithLogonW 1385`，没有执行真实鼠标双击或关闭后重开。CLI 的二次激活覆盖相同开窗处理入口，但不能替代实际托盘输入。该自动修复轮次当时等待用户确认“双击打开 → 重复双击保持唯一 → 关闭后再双击恢复”；后续确认已收到，见下方用户 PASS 记录。

## V3-04-03 人工三端门（R1 复测）

使用 `out/verify/v3-04-03/app-final/DesktopUp.exe`：

1. 双击托盘打开控制中心，确认显示 committed、poster、settings revision；
2. 在控制中心切换锁定，真实海报立即不可拖缩，托盘“锁定全部海报”同步勾选；
3. 从托盘解除锁定，控制中心按钮/状态和真实海报输入权限同步恢复；
4. 在控制中心连续收起、展开三轮，真实海报顶部不跳位，控制中心状态同步；
5. 分别从控制中心和托盘切换“暂停动态效果”，两处按钮/勾选与 tooltip 同步；
6. 拖动并缩放真实海报，手势完成后控制中心几何与 revision 更新，拖动过程不逐帧增长 revision；
7. 关闭并重新打开控制中心，立即显示最新状态，不回弹到旧值；
8. 正常退出并重启，锁定、收起、暂停和几何保持；退出无进程/窗口/托盘残留。

控制中心当前是状态/命令面板；真实桌面缩略画布和预览拖缩属于 V3-04-04，不在本门判定。

## 范围边界

V3-04-03 仍没有宣称：

- 完整控制中心预览编辑和用户可理解的冲突 UI；
- 最近可用状态恢复 UI；
- 真实 ContentItem、启动统计、热度、动态缩放或排序。

这些能力依次属于 V3-04-04 至 V3-04-08、V3-05 和 V3-06。

## 下一步

2026-09-07 用户明确“人工验证通过”。按当前三端门关闭 V3-04-03（USER-REPORTED PASS；本轮未由工具重复执行鼠标操作）。随后用户确认通用多海报方向，下一步改为 V3-04-M01，多海报前置完成后再进入 V3-04-04。

## 2026-09-07 需求/架构/计划同步记录

- 原位修订七份需求：通用类型/安全收纳、多海报独立配置与主题、唯一中心/托盘、热度/AI范围；保留旧特色细节。
- 新建通用多海报设计合同与详细工作流；修正 journal/真实移动的依赖、参数合同前置、重复任务、基础可用发行包顺序。
- 仅文档，未运行新测试、未改业务代码/用户数据库/文件。多海报新门全部待实现；97/97 等数据保留为已有证据。
- 具体立即下一步、代码缺口与受影响验收见 [HANDOFF](HANDOFF.md)。

## 2026-09-07 V3-04-M01：数据/目录仓储门完成

结论：PASS（本项出口是数据/仓储合同；没有新增桌面鼠标人工门，不代表 M03..07 通过）。

- 分支 impl/poster-redesign-V3，HEAD 为 1766070＋dirty；保留上一轮未提交的托盘开窗修复，本轮未 commit/push。
- 新增 schema v3、Poster 独立显隐/用途/层序/主题关联、EntityVersion、profileInitialized/CatalogVersion。
- IPosterCatalogRepository：List/Create/CloneConfiguration/Remove/InitializeEmptyProfile；沿用同一 SqliteStateStore/global CAS；身份由核心生成、请求指纹和回执与状态事务提交。
- 旧默认入口与 Save 保留；并发初始化只播种一次；移除至零后重启或重放旧请求不会复活配置。
- 从已验证 v3-04-03/app-final 程序集提取并固定 v1/v2 指纹；迁移不改 SQL/hash/旧字段，.gitattributes 固定该 SQL 源文件 LF，避免 Windows checkout 改变原始字符串哈希。

### 自动证据

命令：

```powershell
dotnet test tests/DesktopUp.Contracts.Tests/DesktopUp.Contracts.Tests.csproj -c Release -p:Platform=x64 -o out/verify/v3-04-m01/tests/ --logger "trx;LogFileName=m01.trx" --results-directory out/verify/v3-04-m01/results
& ./scripts/build.ps1 -Configuration Release -OutputDirectory 'out/verify/v3-04-m01/app/'
```

- 测试：117/117，通过；0 失败、0 跳过；新增 PosterCatalogTests 20 项，保留此前 97 项。
- 完整 WinUI Release：0 warning、0 error，退出码 0。
- 文档校验：38 份 Markdown 的本地链接失效 0；当前入口无旧 NEXT M01；git diff --check 退出码 0；产物指纹与 TRX 117/117 已重新读回核对。
- 测试环境：本机 Windows、.NET 10/x64、Microsoft.Data.Sqlite；命令因当前工作区写权限边界获批准运行。数据库为测试创建的独立临时目录，测试结束清理；本轮没有测试普通用户窗口权限矩阵。
- 覆盖：创建/复制/移除/排序/0-1-3目录、独立身份与配置、显隐、实体与全局/目录版本、双连接冲突、并发首次初始化、跨重启幂等、改目标/请求体拒绝、缺失目标、删至零、插入和移除后故障回滚。
- v1/v2 兼容：新建真实旧 schema 起点；旧海报全部 21 列及 Settings 字段原样比较；源版本备份、当前在线备份、重复初始化、损坏数据库/账本/profile 保留。
- 故障用例一度失败：同名错误 profile 表存在时，原流程更新账本并报告 Migrated，但表结构错误。补提交前结构/profile/外键读回，确认 v3 全事务回滚、原库/源备份保留后，完整回归通过。没有把失败测试删除或改成预期成功。

| 产物 | SHA-256 |
| --- | --- |
| out/verify/v3-04-m01/app/DesktopUp.exe | 4DC0182DD7E6CD9C333474D94878358441B34C013D8014C0D7515A71401292F8 |
| out/verify/v3-04-m01/app/DesktopUp.Infrastructure.dll | 0BBED987568C964CC88C6C5EE4B82883E42FE6CA4284142F886EDF4050A660FA |
| out/verify/v3-04-m01/results/m01.trx | 2A2576AC34BCBBED791ED9B42F2675ACD65847EE535203ACDAFEA215B30E4DE4 |

### 未执行与后续

没有启动用户实际配置库或操作桌面文件。现有 smoke-control-center.ps1 使用真实 LOCALAPPDATA/V3，不是隔离配置脚本，本轮未运行它以免顺带迁移真实库；既有开窗/托盘人工 PASS 保留但不算本次新增证据。没有修改窗口/输入逻辑，不重新要求 DPI/拖缩人工矩阵。

当前运行/投影仍单海报。下一项 V3-04-M02 扩实体读集 CAS、目标化命令和目录/三端投影；M03 才建立多海报会话。实际语义和限制见 [POSTER_CATALOG_SCHEMA_V3](POSTER_CATALOG_SCHEMA_V3.md)。

## 2026-09-07 M01 后续需求澄清（DOCUMENTATION ONLY）

用户明确：首次无配置一张默认；中心/托盘新增；中心/海报“...”删除并弹出后果确认；单文件控制习惯配置以便跨主机应用。已更新画像、运行表面、视觉、主题、追溯/风险、架构、模块、总计划、细化工作流和交接。

SQLite 继续作为运行时唯一权威，PortableProfile 是待实现的显式导出/导入边界；位置/尺寸/状态与主题依赖可携带，实际文件/凭据不隐式复制。04-06-A..D 增加基础配置迁移，07-04复用自定义主题载荷，M02仍是下一项。

本次未改业务代码、测试或用户数据库，未运行新的功能测试；没有把117/117扩展为新增确认弹窗或跨机导入的PASS。

文档检查：38份 Markdown，本地链接失效0、完整章节号重复0；git diff --check退出码0。未执行commit/push。

## 2026-09-07 V3-04-M02：目标化命令、投影与实体读集 CAS

状态：AUTO PASS（代码/合同/Release/隔离实际进程 smoke）；受影响人工交互回归待用户确认，尚未关闭为 PASS。没有提前执行 M03，也不宣称多海报窗口完成。

### 本轮实现

- 生产统一 StateProjectionCoordinator → StateCommandPolicy → IStateCommandRepository → SqliteStateCommandRepository，同一 SQLite immediate transaction 内完成读集校验、写入、回执及完整快照。
- Poster/Settings EntityVersion 与 global revision 分开；额外读依赖全部校验；目录成员相关命令额外检查 CatalogVersion。无关 A/B/Settings 写入不再伪冲突；同实体旧版本、缺失实体及批量多个失败目标均明确返回。
- Create/Clone/Remove/Batch 命令及删除影响预览；确认携带原 ID/实体/目录版本，不动实际文件。首次仅一张，已初始化0或N张不补默认，晚到/重放请求不复活已删除目标。
- PostersById/有序列表/changed IDs、SettingsChanged/CatalogChanged、托盘总数/显隐/锁定汇总；三端同 revision，FIFO 发布/订阅异常隔离。移除旧内存 requestId-only 重放和混合 committed fallback。
- 追加 schema v4 的 Settings 实体版本和命令持久回执；v1..3 SQL/hash 不变；v3→v4 的源版本备份及提交前结构检查通过。
- App 与现有海报消费者显式传目标 EntityVersion，窗口只查自身 ID；托盘“锁定全部”调用完整读集批量命令；中心/托盘支持零目录；不重写 Host/拖缩/命中算法。
- smoke 加入强制隔离 DataRoot（PowerShell 7.4+）；普通用户路径不变；测试覆盖不能转发给仍运行的旧版。

### 自动执行与结果

```powershell
dotnet test tests/DesktopUp.Contracts.Tests/DesktopUp.Contracts.Tests.csproj -c Release -p:Platform=x64 -o out/verify/v3-04-m02/tests/ --logger "trx;LogFileName=m02.trx" --results-directory out/verify/v3-04-m02/results
& ./scripts/build.ps1 -Configuration Release -OutputDirectory 'out/verify/v3-04-m02/app/'
& ./scripts/smoke-control-center.ps1 -ExecutablePath 'G:\A_\Tools\DesktopUp_V2\out\verify\v3-04-m02\app\DesktopUp.exe' -DataRoot 'G:\A_\Tools\DesktopUp_V2\out\verify\v3-04-m02\smoke-data'
```

- 147/147 测试通过，0失败、0跳过。新增 ScopedStateCommandTests 27项及 v4 迁移3项；保留117项。包括双 SQLite 连接竞争同/不同实体、同时首次初始化、批量第二目标 SQL 失败全回滚、全目标成员变化冲突、NoChange 持久重放、跨旧协议请求 ID 防护、额外读依赖及坏订阅者/重入有序。
- 首轮旧测试5项只因当前 schema 期望3而实际4失败，已更新“当前版本”断言；旧 migration 指纹仍固定。新增夹具一处备份属性命名编译错误已修正。最终完整回归通过，未删除失败场景。
- 完整 WinUI Release：退出码0、0 warning、0 error。中间暴露 ControlCenterService 的可空 Poster 日志解引用，已修复；没有忽略零目录警告。
- 实际进程 smoke：旧版 PID8600 最初占用单实例；未由工具终止。其退出后启动新产物 PID40212，日志15:22:18～15:22:28（Asia/Shanghai）。1个控制中心创建，2次显示请求复用，1张海报创建；中心投影 revision1、海报revision1，schema4；退出码0，errors0，resource-ledger clean=true/resources=[]，结束后无 DesktopUp 进程。
- 环境：本机 Windows/.NET10/x64/PowerShell7.6.5；构建与测试因工作区权限边界批准运行。smoke 日志明确 isElevated=true、manifest asInvoker，因此只证明该权限环境下开窗/投影/退出，不能充当普通用户 Shell/输入认证。人工请使用普通权限终端。
- 数据：合同测试只使用临时 SQLite 夹具；实际 smoke 使用 out/verify/v3-04-m02/smoke-data，未迁移用户 LOCALAPPDATA/V3 实际库、未改用户文件/快捷方式。测试进程已正常退出。

| 产物（相对项目根） | SHA-256 |
| --- | --- |
| out/verify/v3-04-m02/app/DesktopUp.exe | 4DC0182DD7E6CD9C333474D94878358441B34C013D8014C0D7515A71401292F8 |
| out/verify/v3-04-m02/app/DesktopUp.dll | 6737DCBF19C41CA249589AA5EA2F9EDC37104BCE26EEC9AD4027782AC036461D |
| out/verify/v3-04-m02/app/DesktopUp.Application.dll | 3F26A44E826878667D7E2B0B23F7012C255365A6A8E902D1E9B625BE7C8E4B7D |
| out/verify/v3-04-m02/app/DesktopUp.Infrastructure.dll | E3F44BD7565334D1D5D7FF12EBA89774EC68707A4D1A4F31AA1443D01A307863 |
| out/verify/v3-04-m02/results/m02.trx | 6B8C8D031492282681D059DB2C76229A266596FAAB08FD6559C76529D4D7BCEF |
| out/verify/v3-04-m02/smoke-data/logs/desktopup.jsonl | B7D08FE682E9ADECC923ABB751B218D5CAFA01BCFF6D16A8C7949AA2222AE205 |
| out/verify/v3-04-m02/smoke-data/state/resource-ledger.json | 1B5523C3B79695D20AA6661364E981A44AFFD0580FBDA2068BA9ABBE998257D8 |

EXE 为 .NET 启动宿主，其 hash 可与旧版相同；实际实现变更请同时核对三个 DLL，不只比较 EXE。

### 人工门与下一项

使用普通权限 PowerShell7.4+，无旧实例时启动隔离手工配置（不会15秒自动退出）：

```powershell
Start-Process -FilePath 'G:\A_\Tools\DesktopUp_V2\out\verify\v3-04-m02\app\DesktopUp.exe' `
  -ArgumentList '--show-control-center' -WindowStyle Hidden `
  -Environment @{ DESKTOPUP_V3_TEST_DATA_ROOT = 'G:\A_\Tools\DesktopUp_V2\out\manual\v3-04-m02' }
```

1. 中心锁定/托盘解锁后，海报权限与两端显示同步；暂停状态也同步。
2. 收起/展开保持顶部位置；拖动/缩放结束后中心几何/revision更新，无白底或交互回归。
3. 关闭中心后海报继续运行，托盘重新打开显示最新状态；普通退出/重启恢复。
4. 点击/刷新、普通窗口覆盖、Win+D、退出无残留维持既有行为；不重跑全量 DPI 或安装测试。

确认后关闭 M02，进入 M03 会话注册/共享恢复。本次没有提供新增/删除按钮、多窗口 registry、真实文件收纳、便携配置/主题/热度/AI。完整合同见 [TARGETED_COMMAND_PROJECTION_V2](TARGETED_COMMAND_PROJECTION_V2.md)。

分支 impl/poster-redesign-V3，HEAD1766070+dirty；保留前轮未提交代码与文档。本轮未 commit/push。

最终文档检查：38份 Markdown（包含新 M02 合同），本地链接失效0；smoke 脚本语法错误0；按仓库原有换行配置运行 git diff --check 退出码0；再次检查 DesktopUp 运行进程数0。

## 2026-09-07 V3-04-M03：会话注册与共享恢复

状态：AUTO PASS / MANUAL PENDING。用户同意下一步M03，未据此编造M02逐项人工通过。分支impl/poster-redesign-V3，HEAD1766070＋dirty；未commit/push。

实现：PosterSessionRegistry、IPosterSession/Factory、WinUiPosterSession与按ID/epoch的资源/日志；共享环境发现/节流/版本门；零目录独立监听；按保存显示器恢复；取消旧回调、局部失败隔离、每张读回和等待异步恢复退出。DesktopLayerAttachment及既有拖缩/输入算法未重写。SQLite schema仍v4，无新迁移。

### 自动与构建

```powershell
dotnet test tests/DesktopUp.Contracts.Tests/DesktopUp.Contracts.Tests.csproj -c Release -p:Platform=x64 -o out/verify/v3-04-m03/tests/ --logger 'trx;LogFileName=m03.trx' --results-directory out/verify/v3-04-m03/results
& ./scripts/build.ps1 -Configuration Release -OutputDirectory 'out/verify/v3-04-m03/app/'
```

最终163/163，新增16项registry合同夹具，旧147项保留；TRX：out/verify/v3-04-m03/results/m03.trx。完整Release0warning/0error。旧UI架构断言从“窗口必须持有coordinator”更新为“窗口仅消费projection/command、初始化属于bootstrap”，未弱化宿主/输入断言。

### 实际 WinUI smoke（最终构建）

| 场景 | PID | 实际结果 | 隔离目录（out/verify/v3-04-m03 下） |
| --- | --- | --- | --- |
| 首次未初始化 | 34984 | 1海报、1中心、2次show、1共享监听、exit0/resources0 | smoke-fresh-final |
| 既有3张恢复/再次启动 | 40316 | 3个独立ID/visual/input、同Shell/display generation逐实例读回、1中心、2次show、1共享监听、exit0/resources0 | smoke-three |
| 已初始化0张 | 8876 | 0海报、1中心、2次show、1共享监听和恢复提交、exit0/resources0 | smoke-zero |
| 等待共享恢复时退出（500ms） | 36364 | 1次recovery_cancelled、0海报、exit0/resources0；无迟到建窗 | smoke-cancel |

各目录保留 logs/desktopup.jsonl 和 state/resource-ledger.json。三张配置由tests/DesktopUp.SessionFixture通过正式coordinator生成，A常规/B锁定/C收起；最终读回框分别为300×480、300×480、300×60且位置不同。锁定的人工手感、文案与重叠输入不由这些尺寸记录证明。

首次 smoke PID5928：窗口与退出正常，但恢复日志包含IntPtr导致Json序列化异常，因此判FAIL（smoke-fresh日志保留）。改用十六进制文本并补真实JsonLineLog夹具后通过。中间成功PID28944/24484/16456仅为迭代记录；上表才对应最终构建。

实际测试进程application.started记录isElevated=true、manifest仍asInvoker；不能作为普通用户权限认证。没有主动重启Explorer、改变分辨率/DPI或操作用户桌面文件；未执行自动像素截图/交互观察，不声称肉眼无白框/闪烁已经验证。

最终SHA256：

- DesktopUp.dll：185A3E700BE499EC0B1A5E5B1E2E1754D214B603C52D3010A201B5FBA4FD4C4C
- DesktopUp.exe：4DC0182DD7E6CD9C333474D94878358441B34C013D8014C0D7515A71401292F8
- DesktopUp.Application.dll：F92D6D19F5CCD0D279F5B487717371C7A788F920DEA0B3258AFD6670A0E8442D

剩余人工门：三张独立状态与一中心一托盘；基本点击/刷新/Win+D/普通程序覆盖；按用户许可手工Explorer重启后全部恢复、退出重启不重复。启动命令与明确后续边界见 [M03合同](M03_SESSION_RECOVERY.md)。通过后进入M04，不把M03标成完整多海报功能已交付。

收尾检查：git diff --check 退出码0；smoke脚本语法错误0；39份Markdown的本地链接失效0；DesktopUp运行进程0。未删除测试证据、未操作日常用户配置、未commit/push。
