# M07 遗留菜单命中问题：只读诊断

后续状态：用户已授权并完成[R1代码修复](M07_MENU_REPAIR_R1.md)，433项/构建通过，最终原生与人工仍待验证。以下“修复未实施”均为根因取证时的历史结论；原始证据保留，不以代码实现覆盖未通过的验收格。

日期：2026-09-09。状态：OPEN / 本次 B 菜单样本根因已确认，修复未实施。真实消息证明菜单被本海报输入岛抢占命中、被邻海报遮挡；M07 未关闭。此前“待复现/根因未确认”内容保留为历史，不覆盖以下新证据。

## 最新：真实输入路径与根因确认（2026-09-09）

### 1. 复现样本与结论范围

用户截图 `codex-clipboard-b6c78d3a-2e37-4b6c-8545-d2b7604ae44a.png`：B 左、C 右且相邻；B 菜单的“图标＋进入”不能正常点击，靠“编辑”处才可交互，C 同时遮住菜单右部。本轮只读取用户真实操作产生的日志，没有自动点击、激活、重排窗口或停止进程，也没有实施产品修复。

原始证据：[本次隔离日志](../../../../out/manual/v3-04-m07-menu-diagnostic/20260909-141423-b14013/logs/desktopup.jsonl)，PID `18716`，UI 线程 `37872`。下文时间均为北京时间，行号按本次读取的 JSONL 顺序计；读取时日志仍有追加，不把它声明为已封存或退出清理 PASS。`out/` 不跟踪入 Git，关键字段在本文保留。

- B：`poster-4933a8dbef714d76b9f04ac145ba1157`，epoch 2；视觉 HWND `0x2580BAA`，输入 HWND `0x40E10`；外框 `(1960,0,300,360)`。
- B 失败菜单 trace：`b9704031fa5d4d8fa54e6b439d24b7e9`；菜单 Popup HWND `0x720C76`，原生屏幕框 `[2152,2310) × [33,284)`。
- C：`poster-70ff3c7446ca4ce1bd1123d25b646edf`，epoch 3；视觉 HWND `0x1B0DFA`，输入 HWND `0x5E0D0C`；外框 `(2260,0,300,360)`。

已确认的直接原因：**同一菜单视觉范围内，部分点先命中位于 Popup 前方的透明输入岛，随后被派发为底层 Item；菜单未收到该次点击。** 这不是把文字或图标位置写偏了，也不是该次命令被 revision/CAS 拒绝。此结论针对实测样本，不推称每个历史失败都已逐一复现。

### 2. 真实按下、抬起、业务派发的闭环

| 日志行 | 时间 | 实际证据 |
| --- | --- | --- |
| 1734 | 14:16:37.843 | `(2196,90)` 的 `WM_LBUTTONDOWN / 0x0201` 实际进入 B 输入岛 `0x40E10` 的 WndProc，不是仅做 WindowFromPoint 查询 |
| 1735 | 14:16:37.844 | 原路由开始 capture，目标 B、layout 18、`Item / index 1`；局部命中框 `(156,56,116,72)` |
| 1736 | 14:16:37.971 | 同点 `WM_LBUTTONUP / 0x0202` 仍由 B 输入岛收到 |
| 1738 | 14:16:37.975 | 原路由派发 `Invoked → Item / index 1`，`cancelled=false`、`owns=true` |
| 1739 | 14:16:37.976 | B 实际记录 `poster.test_item_invoked = GHOST SIGNAL` |
| 1766～1814 | 14:16:39.759～40.145 | `(2231,88)` 的另外三组按下/抬起也全部派发给 B 的 Item 1 |
| 1826 | 14:16:41.700 | 移至 `(2234,90)`，菜单“进入编辑”实际收到 `PointerEntered`；菜单局部点 `(71,16)`，查询 HWND 为 Popup `0x720C76` |
| 1828 | 14:16:41.946 | 移至 `(2248,91)`，菜单收到 `PointerExited`，查询重新落到 B 输入岛 |

失败链的 `menuEntity=2`、当前实体版本 2、`menuLayout=18`、applied layout 18、`current=true`。这组失败没有进入菜单 Click/command_guard，不能解释成“命令被版本检查挡下”。本轮 B 这条 trace 直接观测到的是右侧 hover 恢复；不把 hover 冒充 B 点击成功。

独立成功对照来自 **C**，不能混作 B：trace `cd8783db84f14824845d17ac1000d1c3` 中，301～307 行的 `(2473,93)` 被 C 输入岛派发为 Item 1；362 行的 `(2539,100)` 实际进入菜单 `PointerPressed`，364 行 `edit accepted=true`，随后进入 Editing。相同菜单功能可执行，失败与命中所经过的原生表面有关。

### 3. “图标＋进入”与“编辑”的分界为何恰好如此

本次 DPI 为 96、XAML scale 为 1。B 外框屏幕 X=1960，Item 1 输入框的局部 X 范围为 `[156,272)`，所以其屏幕范围为 **`[2116,2232) × [56,128)`**。失败点 `(2196,90)`、`(2231,88)` 都在其中。

右侧滚动条输入带由 `BuildInputLayout` 登记，宽 12 像素，因此在本次外框中为 **`[2248,2260)`**。菜单行对应的这段高度内，卡片与滚动输入带之间只留下 **`[2232,2248)` 的约 16 像素空隙**。实测 x=2233、2234、2246、2247 可产生菜单 hover，x=2231 派发给卡片，x=2248 查询又回到输入岛，边界与代码一致。文字“编辑”只是恰好落入空隙，文字并未被拆成两个点击控件。

菜单 PointerEntered 的屏幕点 `(2234,90)` 减去局部点 `(71,16)`，与同 trace 多条 entered 记录交叉得到菜单行原点 `(2163,74)`；结合实际行尺寸 136×36，可核对失败点确在同一菜单行内，而不是用户点到了菜单外面。

**采集坐标的限制：**诊断字段 `screenBounds` 原样来自 AutomationPeer，但本样本返回 `(207,76,128,32)`，与真实 Popup 的屏幕位置不相符；它不能在这个桌面 owner 场景下直接作为物理屏幕框使用。`rootBoundsDip=(203,74,136,36)` 也只是 XAML 根坐标。本次结论使用实际 WndProc、原生 Popup 框、菜单 Pointer 的屏幕点/局部点交叉验证，没有用错误的字段标签推出“产品 DPI 偏移”，也不准备把这个差值硬编码进修复。正式回归的坐标观测需校验物理来源。

### 4. 同一失败时刻的窗口顺序

1734 行同时记录本进程窗口的前后顺序；按从顶层窗口向后遍历所得 zIndex，数值越小越靠前，不是高度或坐标。前后语义见 [GetWindow / GW_HWNDNEXT](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindow)。此表只保留相关本进程窗口。

| zIndex | 表面 | HWND | 与问题的关系 |
| --- | --- | --- | --- |
| 440 | C 输入岛 | `0x5E0D0C` | 位于 B 菜单之前 |
| 441 | C 视觉窗口 | `0x1B0DFA` | x≥2260 的菜单溢出部分被 C 画面遮挡，与截图一致 |
| 446 | B 输入岛 | `0x40E10` | 位于 B 菜单之前；看起来透明，但登记区域可接收鼠标 |
| 447 | B 菜单 Popup | `0x720C76` | 高于 B 画面，却低于 B 输入岛和 C |
| 452 | B 视觉窗口 | `0x2580BAA` | 菜单因此仍可见，不代表它能先收到鼠标 |

B 菜单 owner 是 B 视觉窗口；B/C 输入岛与视觉窗口的 owner 均是桌面句柄 `0x101B2`。本次不是一个普通 XAML 面板内的子元素排序问题，而是多个独立原生表面的顺序没有统一覆盖 Popup。

反例边界也保留：C 的 Editing trace `aa7916e8392d4bc6a0c4ea9dc393c324` 在392行中，菜单处于 z14、输入岛处于 z447，`(2427,93)` 的菜单左侧也收到 PointerEntered，后续“完成编辑”命令通过。因此“进入 Editing 必然全菜单失灵”并不成立；整面 EditSurface 只在 Popup 落于输入岛之后时放大故障。此成功样本 Popup 甚至位于控制中心 z15 前方，也不能把“可点击”当作桌面层级合同已通过。

### 5. 源码根因与未证实事项

1. [PosterVisualWindow](../../../../src/DesktopUp.App/PosterVisualWindow.cs) 的 `ReassertDesktopBottom`（475行）只按“输入岛 bottom → 视觉窗口 bottom”维护二元窗口组；没有描述临时 Popup 应在所有海报画面/输入岛之前的关系。
2. 同文件 `ShowPosterMenu`（1344行）直接 `MenuFlyout.ShowAt`，保留 Click 内的身份/版本检查，但打开期间没有共享菜单交互会话，也没有让相邻海报参与保护；Closed 才恢复本窗口组层级。
3. [PosterInputIslandManager](../../../../src/DesktopUp.Windows/PosterInputIslandManager.cs) 的 `BuildWindowRegion`（540行附近）合并登记区域；`ResolveRoute`（460行）只按海报视觉窗口排序并查询海报区域，没有活动 Popup 优先级。收到菜单覆盖范围内的点击后仍能解析到底层 Item。
4. 输入岛 `WM_MOUSEACTIVATE`（287行）和视觉 guard 会重申 bottom，`ConstrainZOrder`（593行）也只处理自身的桌面层规则，没有菜单生命周期例外。[DesktopLayerAttachment](../../../../src/DesktopUp.Windows/DesktopLayerAttachment.cs) 的157行方法检查 owner/style/non-topmost，不验证 Popup 与输入岛的相对顺序。

因此确认的集成根因是：**现有“透明视觉＋独立输入岛”的桌面窗口组合同没有纳入 WinUI Popup 的显示层级和输入优先级。** 实测是“B 输入岛 > B Popup > B 视觉”，并且 C 也在 Popup 前方。邻近海报或靠边会改变重叠范围，是触发/放大场景，不是需要用户绕开的使用条件。

本次未给每一条 WinUI/Shell 内部重排消息建立完整因果链，不声称已锁定是哪一次系统内部调用首先移动了 Popup。也未把所有历史靠屏边/DPI场景判为同一已复现案例。无需这些额外推断，现有实际接收窗口、派发和正反边界已经足以确认本样本根因及修复责任边界。

### 6. 拟定最小修复范围（待实施，非已通过）

1. 建立共享的临时菜单交互会话，绑定 poster/session/Popup 生存期；从打开准备到关闭，统一协调 Popup、本海报与相邻海报的视觉/输入表面，不能只处理 B 自己。
2. 菜单实际可交互期间，其可见区域必须由菜单收到 hover/down/up；相关输入岛不得在该区域抢先 capture 或派发底层 Item/拖缩。仅在 router 丢弃点击仍可能让菜单不高亮，不算完成。
3. 原生层级同时保证菜单不被其他海报遮住，且不将整个海报置顶、不侵入普通应用/全屏的前方。保留正常桌面 bottom 保护；具体 Popup 协调方式必须以小范围实测验证，不能把单次 ShowAt 或“非 topmost”当作已经满足相对层级。
4. 菜单关闭、取消、切目标、海报隐藏/删除、Explorer generation 变化及退出均释放同一会话并恢复输入；保留 MenuCurrent、实体 CAS 和删除确认，不通过删除安全校验掩盖问题。
5. 回归必须包含 B/C 紧邻及反向菜单、左右屏边、Using/Editing、图标/文字左中右/空白点击、邻海报 Item/滚动条交叠、关闭后正常拖缩滚动。菜单打开时底层 Item 派发必须为零；普通程序/文件选择器与 Win+D/刷新/Explorer 既有层级门不能倒退。

本轮仅更新诊断与交接，没有重建或改修复逻辑。沿用前轮隔离诊断420/420的构建证据，不将其写成修复 PASS；M07 继续 OPEN。修复前远程检查点仍为 `11e7f1a`，本地诊断增量未提交或推送。

## 修复前取证门（以下为准备阶段历史，2026-09-09）

用户要求严格按“现象复现 → 实际输入路径取证 → 根因确认 → 修复方案”推进。修复前原样快照 `11e7f1a9881c2ed3e1486a1a36b83ca355ce0658` 已同步 `origin/impl/poster-redesign-V3`。本轮只增加默认关闭的诊断代码、测试和隔离入口；未实施菜单修复，未改命令条件、窗口样式、层级、输入区域或消息返回值。

### 阶段与证据边界

- 现象：用户已报告“不高亮/点击无效，偶发进入编辑后整组菜单失灵”。当前尚未在诊断版再次复现。
- 现场工具：computer-use 初始化及 reset 后重试均返回 `CreateProcessWithLogonW 1385`，未执行任何 UI 操作。未转用 PowerShell UI Automation；下一步由用户执行真实鼠标动作，App 内部只读取自己的事件和窗口数据。
- 当前原版进程 PID48408 仍运行，未强杀、未并开诊断版。已请求正常退出；诊断版真实启动、菜单采样与清理均待执行，不能以构建成功替代。
- 自动：最终420/420（既有411＋9项诊断边界检查）、0skip；App/SessionFixture Release均0警告0错误；PowerShell5.1.26100.8875 `-CheckOnly`通过。结果在 `out/verify/v3-04-m07-menu-diagnostic/results/m07-menu-diagnostic-final.trx`。
- 原M07六个固定产物与 `scripts/m07-build.json`全部一致；本次输出独立目录，不覆盖原测试包。诊断App DLL SHA256 `F3AA62DF5286A9431BE1EF4D703F7870EBAFE23F01FF291A588A8964678BBE09`；Windows DLL `E76F50D58241D3460B02429BB8CB7D1238F0790DB7603D1F1BE0D6B7E7AC45E9`。

### 取证内容与安全边界

1. App 内的 `MenuInputDiagnostic` 记录每项 IsEnabled/IsHitTestVisible、屏幕矩形、XAML根坐标/DPI、PointerEntered/Exited/Pressed/Released；不设置 Handled。屏幕矩形取自 [AutomationPeer.GetBoundingRectangle](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.automation.peers.automationpeer.getboundingrectangle?view=windows-app-sdk-1.8)，不能把 [TransformToVisual(null)](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.uielement.transformtovisual?view=windows-app-sdk-1.8) 的根坐标当屏幕坐标。矩形读取失败单列，不凭估算补齐。
2. 原生输入岛现有WndProc入口记录收到的消息/接收HWND；已有路由选中后记录实际目标海报、目标区域、capture开始和派发。视觉guard仅记录收到的消息，不改既有HTTRANSPARENT/MA_NOACTIVATE。
3. 原MenuCurrent条件完整保留；额外记录 lock/edit/control-center/remove 的 accepted、打开时和当前实体/布局版本。菜单关闭后排队的删除校验也可记录，不执行任何新增删除行为。
4. 附带鼠标当前屏幕点、队列消息点、capture及本进程可见窗口的矩形/样式/Z序。`WindowFromPoint`只是查询，不等于实际消息接收证据；WM_NCHITTEST也不等于点击。以WM_LBUTTONDOWN/UP、路由派发、菜单Pointer/guard形成同一trace的证据链。
5. 仅显式 `DESKTOPUP_V3_MENU_DIAGNOSTICS=1` 且使用非日常配置根时启用。不开全局鼠标/键盘钩子，不记录其他应用标题/路径/矩形，不传输日志。菜单打开才订阅消息、250ms采样，单菜单最多1200条，关闭/退出清理；诊断会有额外开销，不能拿来测性能或时延。

### 本次最短复现入口

先从托盘正常退出旧版，然后在现有Windows PowerShell5.1执行：

```powershell
& 'G:\A_\Tools\DesktopUp_V2\scripts\start-m07-menu-diagnostic.ps1'
```

每次创建全新三海报演示配置，日志位置由启动输出的LogPath给出，位于 `out/manual/v3-04-m07-menu-diagnostic/<独立运行>/logs/desktopup.jsonl`。不复制日常配置或修改现有M07人工库，未设自动退出。不要并行运行原版或测量。

1. 先在原来容易失败的位置（右屏边或相邻海报）打开“…”；在“进入编辑”文字上停约2秒，再单击一次。记录海报A/B/C、是否高亮、是否成功。
2. 若无法进入编辑，可从控制中心切同一海报进入编辑，再打开同一菜单；停在“完成编辑”约2秒并单击一次。失败时截图，先不要连续乱点；无需确认删除。
3. 报告失败步骤/海报和LogPath；用同一trace关联实际矩形、接收窗口、路由和guard。必要时再与居中且不相邻位置对照，不先要求重跑完整M07。

### 根因判定与条件性修复方向（尚未裁决）

| 同一失败点击的证据 | 能支持的根因方向 | 证实后才考虑的修复 |
| --- | --- | --- |
| 真实屏幕点在可用菜单项矩形内；输入岛收到DOWN/UP并派发；该项无Pointer/guard | Popup没有取得该点的输入，底层输入岛仍接收/路由 | 协调实际Popup输入表面与所有相关海报输入岛的交互生命周期；不能只丢弃底层命令，更不能整张海报置顶 |
| 菜单项Pointer/Click已到；guard accepted=false，并能定位变更的版本/布局 | 命令过期或布局刷新使校验失效 | 修对应版本/布局失效时机，保留目标实体/CAS保护 |
| 菜单矩形、实际视觉和屏幕坐标不一致，DPI/根坐标转换有直接差异 | 几何/坐标映射问题 | 修统一坐标源，不补硬编码位移 |
| Pointer/guard均通过但状态不变 | 命令执行/投影阶段问题 | 沿request/revision检查拒绝或投影，不改输入层掩盖问题 |

只有实际样本满足对应链条，才能将某方向写为已确认根因。现有“菜单打开期间底层Item被点击”的历史日志不够，420项通过也不关闭菜单人工门或M07。

## 以下为取证版之前的只读审计历史

## 用户反馈与验证状态

用户确认上一轮列出的1～2项通过（S1基础交互/视觉和S2桌面生命周期）；同时报告海报右侧靠屏幕或其他海报时，“…”中“进入编辑”等选项部分难以交互、疑似视觉/命中位置不一致。保留通过结论，菜单缺陷单独OPEN；不将本次报告误写为所有菜单交互通过，S3～S5仍待确认。

补充确认：失败时鼠标放在“进入编辑”文字上不高亮、点击无效；多点偶尔能进入编辑，随后“完成编辑”“锁定”“打开控制中心”“删除”也难以响应。这将问题进一步收敛到菜单hover/hit-test输入路径，不能用仅在Click回调内执行的MenuCurrent拒绝来解释“不高亮”。

## 编辑模式放大机制（本次补充诊断）

- `PosterVisualWindow.cs:443`的SetEditing会重建并应用输入布局；1211～1217行在Editing且未锁定时添加`EditSurface=(0,0,frame.Width,frame.Height)`，覆盖整张海报。
- `PosterInputIslandManager.cs:533`将所有登记区域做RGN_OR并裁至外圆角。因此普通模式的离散按钮/卡片输入面，在编辑模式下变成整面输入；菜单覆盖范围若没有单独输入保护，将更容易全面落入底层输入岛。这与从部分不灵变为整组不灵高度吻合，但尚未取得实际Popup与输入HWND相对顺序，不能宣称每一次偶发点击成功的原生原因已测定。
- `DesktopEnvironmentWatcher.cs:208`对海报视觉窗口的WM_NCHITTEST固定返回HTTRANSPARENT；加上既有WS_EX_TRANSPARENT，当前视觉窗口不为菜单开放临时切换输入策略。不能只在router中丢弃底层事件就宣称修复：还必须确认菜单自身实际能收到hover/click，且不泄漏到桌面。
- 最新进程PID48408：12:48:40.002实际进入Editing（applied=true）；12:49:54.648再次打开Editing菜单后，到12:50:00前同一海报记录13次底层Item调用，菜单未关闭、无模式变化、无state_projection.published或geometry_committed。这与用户重复点击但菜单不响应的描述一致。仍缺鼠标坐标/菜单矩形，保留该证据边界。
- 已按computer-use技能尝试只读实时检查，初始化和reset后重试均因工具`CreateProcessWithLogonW 1385`失败，未执行点击、激活窗口或键盘输入。没有改系统权限，也未转用PowerShell UI Automation绕过；代码与日志诊断继续完成。

当前结论：高置信度为菜单与原生输入岛之间的输入隔离/优先级缺口，编辑整面输入是已确认的放大条件。命令版本校验不是“不高亮”的解释；屏幕边缘/相邻海报仍只是可能改变重叠范围的触发条件，不作根因定论。修复应协调菜单实际输入表面、所有海报输入岛在菜单区域内的让位，以及关闭/切换/退出的成对恢复；保留海报非置顶、桌面空白穿透和版本保护。用户本轮仍在补充诊断信息，尚未修改实现。

## 已取得的证据

来自当前固定M07版本，PID48072，日志`out/manual/v3-04-m07/interaction/logs/desktopup.jsonl`；下表为北京时间，同一B海报、同一次菜单开放区间：

| 时间 | 日志事件 | 含义 |
| --- | --- | --- |
| 12:27:21.105 | poster.more_menu_opened（1416行） | B菜单打开 |
| 12:27:22.221 | poster.test_item_invoked（1429行） | B的GHOST SIGNAL卡片收到点击 |
| 12:27:24.685 | poster.test_item_invoked（1442行） | B的LAST ORBIT卡片收到点击 |
| 12:27:26.365 / 28.181 | poster.test_item_invoked | 同一卡片又收到两次点击 |
| 12:27:28.494 | poster.removal_confirmation_opened | 删除确认入口能响应 |
| 12:27:28.521 | poster.more_menu_closed（1482行） | B菜单随后关闭 |

该区间没有geometry_committed、state_projection.published或input_stale事件；本次进程没有more_menu_failed或error/critical。A海报在12:24:05成功记录mode_changed=Editing、applied=true，说明编辑功能并非整体未接通。进程已于12:29:55正常退出，检查时没有可直接测量的活动菜单。

这些证据确认“菜单开着时底层海报输入仍会执行”，但日志没有鼠标屏幕点、菜单项实际矩形、接收HWND或MenuCurrent拒绝原因，不能单凭事件顺序断言四次点击全部落在菜单视觉矩形内，也不能断言唯一原因就是右屏边/DPI偏移。

## 代码层缺口与优先判断

1. `PosterVisualWindow.cs:249`把WinUI视觉窗口按inputTransparent=true挂载；`DesktopLayerAttachment.cs:94`设置WS_EX_TRANSPARENT及WS_EX_LAYERED。其下用独立原生输入岛承接海报按钮、Item和拖缩。微软文档明确此组合会让鼠标消息穿过该layered窗口，见[Layered Windows](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features#layered-windows)。这说明不能把“视觉盖住”当作“输入也盖住”。
2. `PosterVisualWindow.cs:1343`创建原生MenuFlyout，并在1434行ShowAt(_moreGlyph)。没有手算菜单项坐标，也没有注册菜单输入保护范围或建立共享菜单交互会话；关闭时才ReassertCurrentClusterBottom。
3. `PosterInputIslandManager.cs:448`的ResolveRoute只枚举各海报视觉窗口并交给PosterInputRouter；路由器只知道海报区域和捕获，没有活动菜单/Popup排除或菜单优先级。相邻海报输入岛也没有菜单开放门。若鼠标被原生层送至这些输入岛，它仍会执行底层Item/按钮命令。
4. 输入岛WM_MOUSEACTIVATE无条件重申cluster bottom，视觉窗口也有桌面层保护；它们未与菜单开放生命周期协同。不能直接放宽整个海报置顶规则来解决菜单，否则会回归已通过的层级门。
5. MenuCurrent会核对实体版本和layout revision，失败时静默返回。这是另一个需要诊断的分支，不应删除其保护。上述时间段没有已记录的状态/几何提交，因此它不是当前最强解释，但无日志的布局刷新仍不能排除。

优先判断：菜单Popup与“点击穿透的视觉窗口＋独立输入岛”之间缺少输入隔离和交互生命周期管理，比单纯坐标偏移更符合现有证据。靠边/邻近海报可能使菜单与输入岛重叠范围变化，放大症状；此因果关系仍需实际命中快照验证，未认定为根因。

## DeskBox参考与未完成步骤

本地DeskBox的`Views/ContentWidgetWindow.Commands.cs:973`在ShowFlyoutWithInteraction里成对Begin/EndWidgetInteraction、Begin/EndCompactInteraction，并在Closed恢复层级；`WidgetWindowBase.cs:219`检查打开的Popup，`WidgetWindowBase.Interaction.cs`在菜单开放时延后恢复层级。应吸收这种明确的交互生命周期，但不能直接照搬其临时抬层策略，也不能假定它解决了本项目额外输入岛的问题。

下一步应在隔离诊断中同时记录：鼠标屏幕坐标、菜单及各项实际矩形、实际接收窗口/样式/Z序、MenuCurrent通过或拒绝原因；对比居中、贴右屏边、右邻海报三种场景，并核对目标行是否高亮。证实后再实现最小菜单输入协调，保留桌面空白穿透、海报非置顶、实例身份/版本保护。已有纯路由测试没有菜单Popup参与，不能拿411项PASS替代这一真实交互验证。
