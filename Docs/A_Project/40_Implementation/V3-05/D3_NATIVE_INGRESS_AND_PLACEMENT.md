# D3 全区域拖入与实际落点接线

2026-10-12。**D3组件化已接生产：最终合同/同包门通过，原生连续拖动复验待桌面解锁，人工未开始。** 用户授权按§7连续推进至一轮人工验收；留白接收及第2～5项USER PASS保留，普通点击穿透FAIL未关闭。整改仅涉及原生接收、输入归属与组件生命周期，已通过的文件、账本和落点业务保持，不进入D4。D2-04已限定收口。D3归[工作流§6.5](../../30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md#65-桌面与海报的剪切式拖放主路径)，需求与交互边界归[专项方案§2～4](../../20_Architecture/DROP_SURFACE_AND_STORAGE_PROPOSAL.md#2-用户看到的操作)。原接线与自动证据见§4.2，不替代人工失败。

## 1 内部实施顺序与交付出口

按[精简实施与验证流程](../../../Rules/IMPLEMENTATION_AND_VALIDATION.md)，下表子批是内部依赖顺序，不是独立交付或逐批人工确认点。02～04连续推进到完整拖入链路可运行；开发中做影响范围内定向验证，最终集中全量、必要集成和人工验收。高风险文件路径在相关安全验证通过前仍不得生产启用。

| 子批 | 制作内容 | 当前状态与出口 |
| --- | --- | --- |
| D3-01 接收会话与坐标 | 有界全矩形OLE能力、普通输入仍稀疏；Shell目标图像四回调；不可变载荷、最终松手点、代次校验；内外拖动共用屏幕换算 | 已接生产。隔离证据见§4.1，生产跨进程穿透待人工 |
| D3-02 实际落点与事务 | 从当前内容投影建立落点；自动顺位图钉、手动网格/局部让位、标题按规则加入；批量稳定顺序，逐项成功才发布关系和布局 | 已实现并通过相关测试；复用既有排序/局部让位，单项单revision，不再追加独立落点事务 |
| D3-03 逐项跨卷安全执行 | 在既有逐项账本中接保留源的目录/文件准备、身份回执、目标证明、索引切换与安全清源；共享引用一致 | 已实现新入库通道；真实C→G文件/目录、复制中断/索引失败/重启收尾通过。旧拖出范围不扩大 |
| D3-04 生产接线与同包验收 | 完整接通默认位置、近旁反馈和普通成功不拉起控制中心；来源缺图时的系统文档图标兜底；原生输入/生命周期/部分成功联合门 | 首版用户第2～5项及留白接收通过，普通点击穿透失败；§7组件修订合同/同包门通过，最终原生及人工待验 |

D3不制作任意Explorer拖出、不撤备用添加入口；D4负责连续拖出，D5完成联合收口。所有自动文件验证只用自建隔离目录。真实满盘按用户决定延期；Medium、跨屏DPI、锁屏等未验项继续携带，不继承D1独立探针或D2设置的人工结论。

## 2 接收与操作规则

一次OLE Enter建立一个载荷快照，Over只做内存预览，不读文件内容、不创建收纳目录。普通Move可接收；Ctrl/Alt/右键或仅Copy/Link能力不得被偷偷解释为移动。拒绝时图像生命周期仍要完整结束，不能靠丢失图标提示拒绝。

松手重读载荷，按顺序比较；使用最终物理屏幕点。落点依据包含海报/运行身份、实体版本、布局版本、投影代次、当前可见外框、DPI、内容内边距、滚动和循环周期。几何或导航变化必须先发布新的Over预览；未展示的新依据不得在Drop时静默接受。海报/运行身份更换直接结束会话，不能凭一次Over将旧会话转交新实例。

回调在调用消费者前消费会话。重复Drop、过期载荷、Leave后Drop、退出后回调都不产生第二次提交。只传不可变路径/落点数据，不让IDataObject跨越await。应用负责自己的记账移动；Drop返回NONE，不以MOVE再授权Shell删除来源。该规则仍需结合后续文件完成事实验收，不把effect当文件回执。

内容点先做屏幕→DIP→去内容内边距，再加当前单轴滚动，最后按现有ContentLayoutFlow折回循环坐标。循环副本号只用于瞬时定位，不入持久布局。标题、装饰区和收起海报没有内容网格点，返回“按当前规则加入”，不裁成第一个格。IncomingContentPlacement复用原插入顺位与局部让位算法；靠跨轴边缘时将新卡片限制在可容纳的格内。批量保持源载荷顺序，仅成功项推进下一个落点，不留下失败卡片。

## 3 原生输入与图像边界

下述全矩形输入岛方案是已发生点击穿透回归的历史实现，由§7整改替代；不能作为新实现准入依据。

PosterInputIslandManager增加显式全区域能力，复用当前海报输入HWND和现有层级/收展/销毁通道；不新增全屏透明窗，不靠全局左键检测外部拖动。OLE窗口区域为当前可见矩形，普通鼠标仍按原稀疏逻辑和圆角返回命中，内容留白返回HTTRANSPARENT。暂停时仍清空原生区域。能力默认关闭，由生产AttachFileDrop注册成功后显式启用；启用失败撤销注册，不留下半接通入口。

HTTRANSPARENT的官方保证针对同线程下层窗口，不能由D1同进程底板穿透推导跨进程桌面必过；生产桌面层必须单独复验。[Microsoft WM_NCHITTEST](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-nchittest)。D1 A方案继续作为候选，不暗中改回用户否定的WS_EX_TRANSPARENT方案。

NativeFileDropTarget新增接收完整ticket的构造入口，使用NativeIncomingDropSession和ShellDropImages。Enter/Over/Leave/Drop成对转发给Shell图像辅助接口，拒绝、异常、重入和Dispose都清理图像所有者。旧路径数组构造入口暂保留原来无辅助图像的兼容行为，避免在新路径未完工时改变当前文件支持范围。[Microsoft IDropTargetHelper](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-idroptargethelper)。

真实接口隔离检查发现：只有CF_HDROP、缺少源图像私有格式时，辅助接口可能返回DV_E_FORMATETC。D3-04已针对这个错误接入系统文档图标兜底，沿屏幕坐标跟随并在所有终止路径释放；不读取源文件内容或执行Shell扩展。它是通用文档符号，不保证无源图像时仍还原原始文件图标。有源图像时继续使用Shell图像；源辅助对象须保持到拖动结束。实际层级、清晰度及桌面合成仍需人工验证。

## 4 实现与验证记录

### 4.1 D3-01历史检查点

以下“未接生产/缺图拒绝”描述当时范围，由§4.2替代当前状态；旧包和proof原样保留。

代码入口：

- `ContentScreenCoordinates.cs`：内/外拖动共用物理屏幕到内容视口的换算，拒绝非有限DPI或超预算坐标。
- `IncomingDropSession.cs`：纯接收会话、完整预览依据、松手消费和标题/内容分类；无文件操作。
- `PosterDropSurfacePolicy.cs`与`PosterInputIslandManager.cs`：有界OLE/稀疏鼠标策略、显式默认关闭的全区域能力。
- `NativeIncomingDropSession.cs`、`ShellDropImages.cs`、`NativeFileDropTarget.cs`：原生适配、图像生命周期、注册/撤销；新入口尚未接文件执行器。
- `PosterVisualWindow.ContentEditing.cs`：现有内部拖动只复用屏幕换算，不改变抓取偏移、循环导航、布局命令或提交语义。
- `IncomingDropSessionTests.cs`、`IncomingDropNativeTests.cs`：纯会话、真实GDI区域、内存CF_HDROP、真实STA注册/撤销与图像格式检查；不操作用户文件，不模拟真人鼠标通过。

本轮 **D3-01 ISOLATED PASS / D3 IN PROGRESS**。新增39项，完整`out/drop-d3/ingress-full01/contracts.trx`为**3236通过，0失败/跳过**。真实原生部分仅覆盖GDI区域、隐藏测试HWND的RegisterDragDrop/撤销/重注册、STA图像辅助对象及其格式生命周期；不是物理鼠标、图像像素或跨进程桌面穿透通过。

`ingress-app01`及`ingress-fixture01`均Release x64构建0警告/错误。相同App运行`empty`/`three`各自首次和重启共4个内容门：PID33884/22544/34132/24356，检查数39/35/287/43，退出全部0、资源0；包括原内容拖动最新松手/反向/取消/边缘和空海报，均为生产命令/Canvas程序调用，不是原生鼠标验收。结果为`out/drop-d3/ingress-gates01/content-gates-be67226adbdc4d5884db3af325287d73/results.json`。

`out/drop-d3/proof-ingress01.json`绑定671输入、130个App/夹具产物、最终TRX/测试程序集及4进程结果；四个核心DLL与全量测试完全一致。没有启动新的人工候选，结束时无DesktopUp进程；未搬用户文件、未修改schema19、未Git提交推送。D2原包/数据/proof保留；原严格脚本因源码推进而拒绝旧指纹是预期，不修改旧proof以强行放行。

过程记录保留：targeted01为70项通过；targeted02/native03发现新增真实接口测试只有CF_HDROP、没有图像格式却断言可显示，返回DV_E_FORMATETC。增加有/无源图像分支后，targeted04有图分支仍失败，原因是夹具在Enter前释放源图像辅助对象；保持它至拖动结束，native05两项通过，最终完整门通过。无图分支保持明确拒绝，兜底未实现；没有放宽产品检查来冒称全来源支持。

### 4.2 D3完整接线与集中验证

实际落点入口为`IncomingContentPlacement`及`SqliteTransferJournal.IndexPlan`：添加成员后复用ContentLayoutEditing，关系和布局一次提交；空海报不再因首次绑定重置手动模式。旧无落点请求指纹保持兼容。逐项成功才推进后续位置；旧布局撤销不能撤掉已收纳关系，文件移动本身不冒充布局撤销。

跨卷入库使用`IncomingTransferCopies`，复用D2句柄/复制原语但把逐项创建身份记录到既有账本receipt；索引提交前保留并验证源，提交后受限清源。提交前失败只收尾已证明属于本次的副本；替换对象、外来文件及无法证明唯一完整副本的情况保留路径。启动不重做复制。schema19不变，旧请求/旧拖出支持矩阵不扩张。本批只接既有本地普通文件、快捷方式本体与受限目录树；链接/重解析点、特殊属性与超预算对象继续拒绝，不能理解为任意文件系统对象。

`PosterVisualWindow.Transfers`与`PosterSpatialRuntime.Incoming`接全区域ticket、近旁落点标记、顺序执行、默认位置及短结果。普通新文件不弹中心确认；旧DEMO替换、已有共享引用受影响仍保留明确确认。接收方自己执行记账移动，Shell返回NONE以避免二次清源。活跃目标不接内部编辑/收展，环境暂停/退出取消未提交复制；其他海报保持原有隔离。全局文件执行队列仍串行，不承诺多批同时搬移。

验证只用隔离文件：

- 定向126项通过；扩展241项中1项发现删除原海报后的恢复查询使用Single，改成允许缺失后定向108项通过。不是删除或放宽失败测试；过程记录保留。
- 最终`out/drop-d3/combined-final01/final.trx`：**3246通过，0失败/跳过**。本批新增10项，含横纵贴边、自动单revision/幂等、手动让位、真实跨卷文件夹与空目录、复制各阶段中断/重开库收尾、SQLite提交失败后可重新拖入。中断为故障注入后重开库，不冒称断电测试。
- `combined-app03` Release x64构建0警告/错误，四个核心DLL与最终测试运行时一致。同包`combined-gates02/results.json`首次PID3228、重启PID49052均通过，退出0/资源0；覆盖空海报标题加入、中间同名项失败后继续、跨卷目录手动落点、无中心弹出、收起/展开接收准入与重启四关系保留。均为实际生产通道程序调用，**NativeMouse=false**。
- 人工入口`scripts/start-incoming-review.ps1`，识别清单`out/drop-d3/candidate.json`只绑定候选/夹具/只读环境检查器产物，不重复绑定全源码。独立`out/drop-d3/human01/profile`，初始PID18560。环境HighIntegrityMatched，不补Medium通过；旧包/配置/proof未改，未操作用户文件或Git提交推送。

<a id="5-下一步的具体制作任务"></a>

## 5 首版人工验收与结果（保留历史）

运行`& .\scripts\start-incoming-review.ps1`可在正常退出后重开同一配置，不重置数据。同级`sources`为六个自建文本，`cross-source.txt`记录C盘自建目录位置；**仅使用这些可丢弃文件**，本次会真实移动。也可先将其中的测试文件移到桌面，再测试桌面拖入。不要用唯一原件。

1. **全区域与穿透**：空海报留白上普通点击仍能操作下方桌面；从Explorer/桌面拖测试文件到留白，未松手始终有跟手图标，松手直接收纳、源文件移走，不要求选址或弹出控制中心。
2. **实际落点**：自动模式拖入已有Item前后/留白，按提示顺位加入并固定；切到手动后拖到指定格或已有Item处，新项到落点，受影响项局部让位。将浏览方向切横向、或开启循环后再试一次边缘落点，不产生重复卡片。
3. **取消与接收范围**：拖到海报内按Esc，来源保留且无新卡；立即重新拖入可成功。收起时只在可见标题范围接收，隐藏内容原范围不接收；有前台窗口遮挡时不抢该窗口的拖放。
4. **批量与跨卷**：多选两份测试文件拖入，位置依次安排；将C盘`cross-folder`拖入G盘候选，目录及child.txt/empty完整保留在收纳位置、来源目录移除，可正常打开。来源父目录保留属正常。
5. **跟手与重启**：切换深夜书桌/雨夜窗边检查拖入图像不藏到装饰层下；正常退出，再用同一启动器打开，文件/关系/实际位置保留。拖入中关闭/取消的底层安全已隔离验证，不要求用重要文件人工制造中断。

用户最新反馈：第1项的留白接收通过，但普通点击穿透失败；第2～5项均符合预期。保留这些人工结论，只对改变的输入机制做组合复验。D3未整体收口；修复第1项后再进入**D4：连续拖出与真实目标协议**，然后D5联合收口。

## 6 普通点击穿透回归与窄范围机制验证

本节为整改前调查快照；其中“未改生产／等待探针”已由§7替代，不再让用户重复运行旧窄探针。

**V3-BUG-022 REOPENED。** 本轮没有改生产代码、没有重建产品包；combined-app03及已验配置/旧proof保留。当前矩形输入窗口在透明留白仍有非零alpha，只在WM_NCHITTEST返回HTTRANSPARENT。该返回值的保证仅针对同线程下层窗口；原D1 TargetForm.underlay与TargetSurface同线程，“源窗口独立进程”并不使点击底板也跨进程。D1点击PASS不能证明Explorer穿透，现人工失败暴露了这项验证缺口。[Microsoft WM_NCHITTEST](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-nchittest)。这是已确认的机制限制，具体系统消息路径不写成已完整跟踪。

下一步先用同一块**独立进程底板**同时检查普通点击与OLE首次留白接收。`tests/DesktopUp.NativeDragProbe/CrossProcessClickProbe.cs`复用既有Shell图像/IDropTarget；A为当前HTTRANSPARENT反例，E为颜色键透明留白候选。E只在探针中存在，不表示已选定或已修好。WinEvent仅旁路记录外部捕获/拖放通知，不据此改变接收范围；微软明确拖放开始/结束事件由应用负责发送，不能假设涵盖所有来源。[事件常量](https://learn.microsoft.com/en-us/windows/win32/winauto/event-constants)、[分层窗口透明命中](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features#layered-windows)。

检查只问两个结果：蓝色留白普通左/右点击是否增加独立底板计数；从Explorer或独立源直接拖入同处是否增加Enter/Drop。不执行文件移动、不改系统设置、不注入Explorer、不模拟点击转发。若E点击通过但拖入失败，淘汰该候选；不能改用人工开关或悄悄退回只在卡片上接收来冒称满足原需求。

`click-probe02`构建零警告/错误；`click-smoke01`父进程18960与独立底板22492、两组注册及正常关闭通过，日志明确NativeMouse=false。没有重跑未改动的3246项全量。初版编译的Area方法/属性笔误已修正，不涉及产品。`scripts/start-incoming-click-probe.ps1`已打开交互探针47844，独立目录`out/drop-d3/click-review-1a75304e5e1c4d03b00b3569435ffc52`，HighIntegrityMatched；当前等待真实计数结果。正常关闭主探针会请求其底板正常退出，不强杀进程。

当前出口：取得跨进程组合证据→选择机制并修订生产输入→只复验受影响的第1项及取消/结束后恢复穿透。未获该证据前不声称修复、不进入D4。已通过的2～5项不撤销；若改动影响其具体子边界，仅定向补验。

## 7 输入与拖放组件化整改

2026-10-12用户批准。普通点击穿透是海报基础约束，全区域拖入是增量能力；不能靠破坏前者实现后者。此前同线程底板不能证明真实桌面穿透，独立底板探针显示也不清楚，停止将其作为用户下一项待办。

### 职责与不变量

- 原生宿主唯一管理窗口、层级、可见几何与生命周期；普通输入区域只由标题、按钮、Item等有效区域组成。模板与拖放业务不得直接覆盖其窗口区域。
- 拖放组件管理注册、会话及跟手图像，只向既有消费者提交不可变ticket。组件安装、停止、异常和释放均不得遗留拦截、捕获或图像。
- 普通指针与OLE接收有不同命中需求，须在原生适配层同时成立；不能仅用两个布尔策略冒充系统隔离。不得依赖HTTRANSPARENT的跨线程保证、仅左键按下推断OLE、注入Explorer、人工接收开关或模拟转发用户点击。
- 保留IncomingDropSession、IncomingContentPlacement、默认存储、复制/账本/单事务提交和共享关系规则；不变更schema19、不增加文件支持范围、不重做D2。

### 连续实施顺序

1. 用受控独立进程底板和无文件动作OLE源定位可行的原生命中机制；加入真实跨进程组合检查，不把创建窗口或回调程序调用称为鼠标通过。
2. 将验证过的原生机制收进宿主拥有的接收组件，移除业务直接调用TrySetFullAreaDropReception的权限；普通输入岛恢复单一职责。同步处理几何、暂停、收展、前台遮挡和销毁。
3. 接回原有ticket与业务通道，开发中定向验证取消、释放、区域恢复及已通过业务不变；最终一次全量合同、必要同包集成与候选构建。
4. 一轮人工集中检查：空海报留白普通左右点击/桌面框选与全区域首次拖入并存；跟手图像、Esc后立即重拖；收起/遮挡/模板切换及结束后穿透恢复。原2～5项只复验受影响组合，不整套重做。

### 验收边界

本计划不预选未经验证的窗口技巧，也不以拆分文件数量衡量完成。若Windows机制不能同时满足需求，先记录具体反例与支持边界，不静默降级为卡片接收或扩大常驻输入区。真实满盘、Medium、跨屏/DPI和锁屏等已有未验项继续如实携带。旧包/配置/证据保留，无Git提交推送授权。

### 7.1 已实施的组件边界

- `PosterInputIslandManager`保持稀疏HRGN；移除`TrySetFullAreaDropReception`、全矩形分支和以HTTRANSPARENT补救跨进程点击的分支。业务只能调用`InstallDropReceiver`取得可释放注册，不能拿HWND改基础输入区域。安装失败、暂停、卸载均不影响原稀疏输入。
- `PosterDropReception`是宿主拥有的独立有界窗口，默认空区域，不常驻拦截留白；仅识别到OLE捕获时临时覆盖当前可见海报范围。与原输入窗口分别管理命中，几何、收展、重挂载、层级及销毁仍由原生宿主统一接线。生产窗口不置顶、不扩大为全桌面遮罩、不转发模拟点击。
- `NativeOleDragMonitor`每UI线程共享一个进程外WinEvent订阅。捕获通知只触发核验：来源线程的实际捕获窗口、类、菜单/移动状态及输入桌面均需满足；普通按键和普通窗口捕获不会启用拖放。只在活跃OLE期间30ms检查结束，空闲无此定时器；最后订阅释放时撤销hook/定时器。
- 窗口捕获结束与OLE协议结束分开：立即撤掉接收区域，已经进入的会话仍可收到最终Drop；最多500ms后清理未结束的悬停/图像。新一轮捕获重置已完成标记，避免“Drop晚于释放捕获”使下一次拖入被拒。暂停/退出立即取消，文件执行权仍只来自有效ticket。
- `NativeFileDropTarget`继续沿用`NativeIncomingDropSession`与单个Shell图像所有者；helper改为独立RCW，防止释放一个实例导致另一个实例COM包装失效。`PosterVisualWindow.Transfers`只持有注册、读取当前表面并消费原ticket；入库、默认目录、布局、复制账本、schema19均未改。

**兼容边界**：捕获类`CLIPBRDWNDCLASS`来自本机Windows 11 26100实际OLE日志，是Windows实现细节，不是公开OLE兼容承诺。未知来源/环境核验失败时拒绝启用该增量接收层，保持普通点击穿透；不得宣称所有Windows版本和拖放提供方都已认证，也不能把普通权限、Explorer真实拖入或锁屏恢复自动补PASS。后续Windows版本变化的修订集中在该适配器，不扩散到业务与普通输入。

### 7.2 证据与当前验证范围

原生机制对照使用独立点击底板、独立OLE源、真实`DoDragDrop`及接收窗口。仅在自建窗口发送测试输入；生产组件分支使用不存在的诊断路径计数，没有执行器，不读写用户文件。测试夹具为保证自己的底板可见使用局部置顶，不能外推为生产桌面层级验收。

- `out/drop-d3/routing-run05`排除静态技巧：原全矩形/HTTRANSPARENT能接OLE但跨进程点击不通；颜色键留白、alpha0、透明样式或稀疏窗口能穿透但首次留白OLE不到达。
- `routing-run07`证实捕获核验后的动态接收可并存；`routing-run08`暴露捕获早于Drop释放，修订后`routing-run09`生产组件提交一次、取消零提交、暂停零提交、结束后左右点击恢复，组件资源均0。属于受控跨进程原生输入，不是用户Explorer验收。
- 定向142项通过；并行native测试曾发现Shell helper共享RCW释放问题，已修复而非忽略失败。`component-final01/final.trx`完整3248项通过、0失败/跳过。
- `component-app01`与夹具Release x64零警告/错误；`component-gates01/results.json`首次/重启均通过、退出0、资源0，验证旧生产入库通道与重启保留，`NativeMouse=false`。
- 收尾增加同一接收组件连续两轮拖动，修正已完成标记的跨轮复位；最终候选为`component-app02`/`component-native02`。最终全量、同包门和原生重复拖动复验记录在本节下方，不能用上一个build的结果代替。
- `component-routing01`因交互桌面不可读立即停止，未发送测试输入、无文件动作；该次是环境阻断，不是PASS。不绕过锁屏、不改系统设置。

最终同构建记录：`component-app02`/`component-native02`均Release x64零警告/错误；`component-final03/final.trx` **3248通过、0失败/跳过**。四个核心DLL与测试程序集运行目录一致，原生夹具的Windows组件也一致。`component-gates02/results.json`首次PID49572、重启PID34268，退出0/资源0、`NativeMouse=false`。过程`component-final02`有1项既有故障夹具目录清理AccessDenied（Dispose，不是恢复断言失败）；未改该夹具/生产复制逻辑，保留TRX，失败同组10项定向复验通过，再跑final03完整通过。暂不能断言外部短暂占用的具体来源。

`component-routing02`对最终build再次核验交互桌面仍不可用，启动即正常结束测试、退出1，无发送输入；停止重复尝试，等待用户解锁。`out/drop-d3/component-candidate.json`已绑定候选/夹具/环境检查器，状态为`AUTO_PASS_NATIVE_REPEAT_PENDING`，不会被启动器误认为最终原生已过。`scripts/start-incoming-review.ps1 -ComponentRevision -PrepareOnly`已建独立`component-human01/profile`及自建sources，未启动产品；旧无参数入口仍绑定旧候选，不覆盖数据。

**恢复工作时**：不改源码、不重跑已过全量。使用`component-native02 --routing-auto <新的out/drop-d3诊断目录>`（目录先创建`d1-probe-owned.marker`），核对10组`pass=true`、进程正常结束及生产分支资源0；最终3组连续两轮预期分别为2/0/0次提交，左右点击各轮恢复，普通捕获不启用接收。若通过，再将该原生结果记入新manifest、状态改为`AUTO_PASS_USER_PENDING`，以`start-incoming-review.ps1 -ComponentRevision`打开下面三组集中人工。若失败只定位该原生路径，不将静态门PASS掩盖失败。

### 7.3 一轮人工验收（替代旧窄探针待办）

使用独立组件修订候选；旧`combined-app03`、`human01`和证据保留。仅使用新`sources`自建文件，收纳会真实移动这些文件。

1. **同处点击与首次拖入**：空海报留白上左/右点击、桌面框选仍作用于下面桌面；从Explorer直接把测试文件拖到同处，图标持续跟手，松手直接收纳。再拖第二个文件，不能出现隔次失效。
2. **取消与连续恢复**：另一个测试文件拖到留白后Esc，来源保留；立刻在留白点击仍能穿透，再拖同文件成功。已有Item选择/内部拖动、标题拖动和收展按钮仍正常。
3. **范围与装饰组合**：切深夜书桌/雨夜窗边后重复一次留白接收；收起后只接可见标题范围，前台应用遮挡处不抢接收。操作结束及正常退出/重启后，点击穿透仍在。

以上集中验收通过后才关闭BUG-022和D3，随后进入D4。原已通过2～5项继续保留，不额外重做完整文件安全矩阵；本轮不开放新的拖出目标。
