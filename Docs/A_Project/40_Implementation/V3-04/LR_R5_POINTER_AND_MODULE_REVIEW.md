# R5 实机反馈：稀疏输入、图钉与场景模块

日期：2026-09-27。当前状态：**R5 CORE CLOSED，DEC-029恢复推进；模块B/C/D已批准但未实施，Rain在其后**，出口与当前差异见[收口审计](LR_R5_CLOSEOUT_AND_PLAN_AUDIT.md)。用户对图钉/灯光及后续UI/动画正向反馈保留；候选25后停顿续滚反馈另列BUG-026并延期。本文§1～9按对应候选记录，旧“待审/R5未收口”是历史，既有未验原生边界仍有效，不将局部反馈转授完整产品验收。

## 1. 用户要求与结论

| 反馈 | 已确认事实 | 当前处置 |
| --- | --- | --- |
| 灯光不能跟随，怀疑与闲置轻摆冲突 | `LampSceneMotion.Resolve`同帧if/else互斥；实际鼠标已进入Item后，NVIDIA Overlay全屏alpha=0窗口被误当成遮挡，阻止进入Following | 仅排除原生API明确证明整体alpha=0的窗口；候选20真实进入两个点跟随、离开恢复轻摆通过，见§4.2 |
| 删除深夜书桌代码后宿主仍能运行 | 通用App/绘制/参数模型仍直接引用Lamp；有动态配方不等于模块可移除 | [解耦方案](../../20_Architecture/CONTENT_SCENE_MODULE_DECOUPLING_PROPOSAL.md)已由DEC-029批准，排在R6后、Rain前，尚未实施 |
| 图钉改图标，右键解除 | 原先显示“钉N” | 固定22 DIP图标；同一图标内按下/抬起右键经原有版本、锁定、草稿与事务检查解除，支持撤销；旧硬位置锁不当成顺序图钉解除 |
| 空白应可操作桌面 | ContentViewport被整体加入输入岛原生HRGN，透明绘制不等于输入透明 | 原生命中和逻辑路由共同排除ContentViewport；保留其几何元数据/被动观察。Item和操作控件仍命中，显式编辑表面保持 |

## 2. 穿透回归的证据链

历史权威依据：[M04人工G3](M04_MANUAL_VALIDATION.md)已有普通空白下桌面框选、菜单、图标操作USER PASS；[M04输入确认](M04_INPUT_CONFIRMATION.md)要求实际原生区域失效，不能只在回调忽略；[M08被动观察§2](M08_POINTER_OBSERVATION.md#2-判定规则)明确“观察整个可见轮廓”不扩张输入岛。

当前链路为`PosterVisualWindow.Content.cs`发布ContentViewport → `PosterInputIslandManager.BuildWindowRegion`将所有区域合并 → 路由同样把Viewport当接收者。`git show HEAD`已存在该区域，`git log -- src/DesktopUp.App/PosterVisualWindow.Content.cs`可追到内容布局基础提交`9607d4e`及检查点`bd148fb`。因此可以确认属于内容输入区域引入后的回归，不能仅凭本次曝光就归因于最新Lamp或网格绘制。

修正入口：`PosterInputGeometry.CapturesPointer`统一决定输入资格；原生HRGN、本地HitTest、Resolve/Begin/ExpansionZone均采用同一规则。没有增加全局hook、第二层输入网格或为追光恢复整面拦截；非交互空白的滚轮也交给实际底层窗口。

修复前新测试3项中2失败，证明空白抢到前海报、GDI区域包含空白；修复后实际GDI区域与路由测试通过。实际App `GetWindowRgn`读回中243个空白点排除、216个交互点包含；`WindowFromPhysicalPoint`查询空白没有本海报自身接收者。**这是原生区域与接收者证据，不等于已经实际执行桌面框选/右键/双击。**

## 3. 图钉与真实追光验证边界

图钉绘制/命中共享`ContentPinPresentationPolicy`，不新增窗口区域、不改变左键拖动和Item滚动边界。22 DIP图标裁切到当前可见tile；跨图标边界的右键手势取消；缺按下见证的旧调用只允许普通菜单。原生输入管理器保存真实右键按下点，失效/释放时清理；正式解除使用`EditContent(Pinned,false)`。测试覆盖循环副本、剪裁、旧锁、模型事务及撤销；实际WinUI处理器验证仅写一次、无打开/菜单、旧请求被拒绝、两级撤销还原。尚未取得OS真实右键交付证据。

追光新门：`App.LampPointerProbe`只经外部受保护驱动移动真实指针，不调用PaintLampMotion或合成hover。请求两个A海报Item点，再到B海报Item离开A；要求同次native位置/接收者/遮挡/保护/presence与场景跟随坐标一致，离开后真实帧/吊灯角/光位变化。诊断为有界内存最后一帧，仅显式probe写日志，不增加生产逐帧日志或输入权限。

`pointer-v18-real`停在第一点(198,334)：指定Input HWND不是该点实际接收窗口，驱动Status=Occluded，MouseMoves/LeftDowns/RightDowns均0。已询问用户露出左上/中上桌面，未操作其他窗口。该次采样的鼠标在海报外，故不能据此判定用户追光问题就是外窗遮挡。后续须在露出桌面后运行同包新证据目录；若已真实移动但未跟随，按诊断reason/occluder/protection定位，不放松遮挡检查。

## 4. 候选与证据

分支`impl/poster-redesign-V3-wb`，HEAD `bd148fb4641bccb18d75777ae6645149e3ff9cfc`，保留全部既有未提交工作。候选`out/lr-r5-evidence/candidate18-pointer-feedback`：434源码指纹、401产物；App SHA256 `B71FC337862497E51EF6EF182D30070DADC7C166DC33B5518AB7DFB919A30CCE`；manifest SHA256 `8C6CB2E8F895F046E8691312498BAB63DEF49E074241CB69C6B47182F4DD8674`。

| 检查 | 证据目录（均在out/lr-r5-evidence） | 结果与范围 |
| --- | --- | --- |
| 全量合同 | pointer-feedback-full-v18 | 2093/2093，0跳过 |
| App / fixture构建 | app-local-pointer-feedback-v18-02.log / pointer-feedback-fixture-v18 | 0警告、0错误；首次Symbol.Lock编译失败保留，已改为字体图标 |
| 日常 / 测试反馈处理器与重启 | pointer-v18-daily / pointer-v18-protocol | 各15场景通过；含新增图钉事务和实际稀疏HRGN读回；不是OS输入 |
| 字段 / 非法热度预览 / UI五态 | pointer-v18-fields / pointer-v18-rejected / pointer-v18-ui | 通过，同包稳定；XAML/状态/处理器范围 |
| Lamp参数/资源 | pointer-v18-lamp | 33步通过；合成跟随处理器和实际闲置帧，不是实际鼠标跟随 |
| 真实指针 | pointer-v18-real | BLOCKED，0输入，未通过；失败证据保留 |

候选17用户已正常退出，本轮各GUI探针串行使用新DataRoot；日常库及human-review-03不改写，没有Git提交/推送。截图已实际查看图钉日常状态；PNG是XAML渲染证据，不是桌面合成截图。原BUG-019/020/021没有因本轮局部通过自动关闭。

### 4.1 复审补正：候选19

独立复审发现，原生右键DOWN/UP虽保存两个点，却都读取处理时的当前光标；UI线程积压可能把“正文按下、图钉抬起”误记成两点都在图钉。本轮补正为`ContentContextMessagePoint.TryRead`解析各条消息的signed lParam并ClientToScreen；原生frame不匹配则拒绝，当前光标仅作附加接收者保护，不覆盖事件点。实际路由、按下见证与解除使用消息坐标。只改右键链，不扩大左键范围。

新增隐藏自有HWND测试验证负屏坐标、两消息跨图标取消、同图标解除、旧frame拒绝；不移动系统鼠标、不激活窗口，不将其称为真实排队消息验收。复审确认P2解决，无新增阻塞。

`out/lr-r5-evidence/candidate19-pointer-feedback`：436源码指纹、402运行产物；App SHA256 `158F72459466C8A5DC92495F5673ABDB02BC9115F11CD73AE9CE538D875882C0`，manifest `A2C2E870213A9E187CBF0BFADC1B2F0CABE6AA3D724AE148D982DC7CC0835721`。v18的manifest按其自身登记集合保留，不移用到v19。

全量`pointer-feedback-full-v19`为2094/2094、0跳过；App/fixture `*-v19.log`均0警告/错误。八组同包运行`pointer-v19-{daily,protocol,fields,rejected,ui,lamp,recipes,composition}`全部通过相应脚本门、产物稳定；日常/协议各15场景及重启，字段、拒绝、UI、Lamp参数资源、配方主题及重启、100项构图及重启边界同上表。实际查看v19日常PNG确认图钉图标，桌面合成及产品视觉仍未认证。

真实指针没有重试未变化的遮挡环境，v18阻挡证据仅用于说明上一次0输入，**v19真实追光尚未通过**。体验入口`out/lr-r5-evidence/start-pointer-feedback-review.ps1`绑定v19八组证据与全新human-review-04，PS5.1/PS7两种Shell的CheckOnly均通过；本轮未打开常驻体验占用后续真实指针门。最终436源码hash仍与冻结清单一致，`git diff --check`通过，DesktopUp进程0。

### 4.2 真实复现与修复：候选20

用户再次露出桌面后，`pointer-v19-real-visible-wait-02`首次实际移动到Item点(198,334)，指定Input确为接收者，但同次观察仍为`native-window-occludes`、Outside、Following=false。只读核对遮挡HWND `0x101A8`：PID10732、`NVIDIA Overlay.exe`、`CEF-OSC-WIDGET`、2560×1440、visible=true、cloaked=0；`GetLayeredWindowAttributes`成功，`LWA_ALPHA`且alpha=0。这与之前Codex挡住目标、完全零输入的环境失败不同，已形成真实故障证据。

根因是`NativeDesktopPointerPlatform.CoversPoint`只看可见标记、矩形和HRGN，没有排除原生API已证明整体全透明的分层窗口。现在每次采样仅在`WS_EX_LAYERED`、属性读取成功、含`LWA_ALPHA`、alpha恰为0时判定不遮挡。不按进程/类名放行；alpha=1、色键独用、属性未知仍保守阻挡，同一窗口改回非零alpha立即重新遮挡。未修改NVIDIA窗口、设置、接收者校验、输入区域或保护策略。API边界依据[微软GetLayeredWindowAttributes说明](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getlayeredwindowattributes)。

`candidate20-pointer-alpha`：436源码指纹、402运行产物；App SHA256 `354FD9B152B8273172C1C8C8D945EDB949EC3B4D3818C50CFA37846C97AFCC90`；manifest SHA256 `E35C6407C31E9B8C38778C2B33E14A61FD5660A17470F1760896C267A22F1F4B`。构建与冻结材料均在`out/lr-r5-evidence`，旧包不覆盖。

| 检查 | 证据 | 结果与范围 |
| --- | --- | --- |
| 新原生窗口红→绿 | pointer-alpha-red / pointer-alpha-green | 红测9项中3失败；修正后相关102/102；含alpha0/1/255、disabled、色键、未知属性、同HWND透明度切换 |
| 全量与构建 | pointer-alpha-full-v20 / app-local-pointer-alpha-v20.log / pointer-alpha-fixture-v20.log | 2102/2102、0跳过；App/fixture 0警告、0错误 |
| 真实追光 | pointer-v20-real | 3次真实移动、0按钮；两个Item点Following=true且光位随点变化，移出到另一海报后Following=false、实际帧/角度/光位轻摆变化；退出0、资源0、产物稳定 |
| 真实鼠标条目链 | pointer-v20-native-mouse | 单击、双击、自动拖动固定顺序、非法落点取消、手动移动、编辑缩放与撤销重做、条目右键菜单、重启保真通过；不是图钉图标直接右击或桌面空白手势认证 |
| 八组同包回归 | pointer-v20-{daily,protocol,fields,rejected,ui,lamp,recipes,composition} | 全部脚本门通过、运行产物稳定；日常/协议各15场景，实际HRGN/图钉处理器，字段、拒绝预览、UI五态、Lamp33步、配方主题、100项构图及相应进程重启；不替代人工审美 |

真实追光点：(198,334)→局部光位(85,243)、角度-9.3333；(318,523)→(205,432)、角度-1.3333；移到另一海报(944,334)后`outside-published-frame`并恢复轻摆。独立审查确认窄范围修正，无阻塞发现。BUG-023可登记CODE+REAL POINTER修复，产品视觉人工验收仍待用户复测；R5与BUG-019/020/021不自动关闭。

体验命令：`& .\out\lr-r5-evidence\start-pointer-alpha-review.ps1`（项目根目录）。它绑定候选20八组证据和全新`human-review-05/data`；PowerShell 5.1与7的`-CheckOnly`均通过，避免旧候选/证据混用。启动脚本不会宣称真实桌面手势已经通过；本轮未启动常驻体验、旧human-review与日常库未改。已实际查看新日常图钉PNG，仅作为XAML渲染证据。

最终核对：436个冻结源码指纹仍一致；29份改动/新增文档的853个本地文件链接无缺失（不等于所有历史章节锚点认证）；`git diff --check`通过。NativeMouse两进程均退出0、资源0；无DesktopUp常驻进程。没有Git提交/推送，工作区既有改动保留。

## 5. 下一步与协作交接

以下候选19的零输入失败为修复前历史；最新实测及下一步以§4.2和本节末清单为准。

最新续测（2026-09-27，用户“已露出请继续”）：候选19新增`pointer-v19-real-exposed-02`、`pointer-v19-real-exposed-delayed`、`pointer-v19-real-after-uncovered`、`pointer-v19-real-visible-wait`，均停在第一点(198,334)。实际HRGN包含该Item点，但WindowFromPoint/PhysicalPoint读到PID8964的Codex/ChatGPT.exe窗口（Chrome_RenderWidgetHostHWND），不是指定Input HWND；MouseMoves/全部按钮均0，各候选退出0、资源0、运行产物稳定。窗口只读复核为主屏2560×1440、Codex最大化覆盖、visible=true/iconic=false/cloaked=0；不能将该次阻挡说成用户所报Lamp故障的根因。

等待脚本曾在三个测试位置连续3秒不被Codex占用后启动海报，但首次输入检查又读到Codex；这里只证明窗口接收状态变化，没有确定是窗口启动/桌面显示机制还是用户切回导致。未擅自归因或更改桌面层级。尝试启动后等待10秒仍被阻挡，20次等待均仅重做零输入可见性检查，不重放已执行动作。

为此仅修改测试脚本`probe-lamp-pointer.ps1`增加可选`WaitForUnoccludedMilliseconds`（0～10000，默认0）和`PreflightAttempts`证据；仅Occluded、未执行、零移动/按钮且清理成功才等待，当前原生接收守卫保持。`DesktopUpContentNativeDriver.cs`此前新增只读StartHitDiagnostic，实际运行脚本hash由本次artifacts.json单独绑定；这两个脚本已不同于候选19构建时的source-manifest，App/fixture二进制没有改动，不再声称整个当前工作区与构建时源码清单完全一致。

已准备`out/lr-r5-evidence/run-lamp-pointer-check.ps1`：从终端运行，20秒后启动新隔离数据，启动后最多等10秒露出桌面，结果保存到唯一`pointer-v19-manual-*`目录。它尚未取得成功运行证据；用户需最小化Codex和终端直到测试海报退出，无需切回聊天回复。没有通过工具自动最小化Codex：本机computer-use技能关联guidance明确禁止自动操作ChatGPT桌面UI，Codex窗口由ChatGPT.exe承载，按该限制处理。生产追光仍OPEN，未进入模块B/C/D。

1. 候选20真实移动→跟随→离开轻摆已通过；交付同包体验复核实际动效，不再重复候选19零输入环境检查或以旧合成结果替代新证据。
2. 核验真实图钉右键以及空白下桌面框选/菜单/图标；编辑模式、下层海报、循环接缝不应回退。前述自动结果不关闭人工门。
3. 用户审阅[模块方案B→C→D](../../20_Architecture/CONTENT_SCENE_MODULE_DECOUPLING_PROPOSAL.md#3-实施里程碑)后再拆宿主/模块；物理排除模块构建、重开保真、恢复模块为必需出口。
4. R5保持IN PROGRESS，R6尚未开始。daily visuals agent与lamp motion agent已交文件并冻结；root负责公共路由/native、集成与Docs；review agent仅只读复审。后续不得沿旧分工并发覆盖文件。

可复制交接：当前候选20/§4.2 SHA，真实追光及NativeMouse条目链通过；根因为全透明NVIDIA覆盖窗口被误判遮挡，现按成功读取的整体alpha=0排除。普通空白HRGN与图钉已有代码及自动证据，右键使用消息事件坐标；真实图钉右击、桌面空白手势与产品视觉仍待人工。权威为DEC-026/027及本报告；模块可移除是已要求的目标，B/C/D具体拆分方案待审阅。全部历史失败、旧体验数据和dirty工作保留，不将局部门算R5完成。

## 6. 候选21：默认黑底与主题资源隔离

用户新反馈：控制中心的“更多／全局”、颜色弹窗“应用颜色／取消”等默认黑底，悬停才正常；同时明确“其他的样式，灯光，图钉交互等……都很不错”。后者登记为对应范围的用户正向体验，不据此补写空白桌面框选/右键/双击逐项通过或R5全部通过。本轮只修主题，不改海报输入、Lamp运动、图钉事务或已认可外观布局。

实际复现：`theme-v21-red-system/data/center-theme-system.json`中ActualTheme=Light，但更多按钮与弹窗按钮Normal背景为`#FF2A2D30`、前景为`#E4000000`；PointerOver背景为`#80F9F9F9`，回Normal再次深灰。纸白单独首启测试未复现，切换“跟随系统”后复现，红测与截图全部保留。另一深色状态探针`theme-v21-red-modes`暴露未统一的框架按钮状态资源；不能将该深色探针本身说成用户浅色黑底复现。

根因：`ControlCenterWindow.Theme.cs/RenderCenterTheme`通过`ResourceDictionary.TryGetValue`查到的资源可来自框架回退，直接改`SolidColorBrush.Color`会改到共享画刷。system模式依次写Light/Default，后者的深色值污染当前浅色默认按钮资源，悬停却使用另一套未改资源。只有显式UiC背景的按钮躲过；DropDownButton和新建弹窗动作尤其明显。

修正：只修改资源字典实际拥有的画刷，缺失时新建本地画刷；XAML预先声明完整按钮状态资源，下拉按钮显式使用UiC样式；动态色块/复位/颜色弹窗动作统一UiCAction，弹窗携带独立主题资源作用域和当前主题。真实色块仍表示已配置的海报底色/卡片色，深色值不会被错误“修成浅色”。

候选`out/lr-r5-evidence/candidate21-center-theme`：437源码指纹、402运行产物；App SHA256 `6FAACCE76E645FA942F1DB09FAAC99075820A32278AE0DCB07BF8FE31DB7926A`；manifest SHA256 `8C869C756E1C5ED98BB57ED9950F3E53EF5BD005C17BE549BB7D90B25F5B3A43`。`app-theme-final-v21.log`构建0警告错误；`theme-v21-full`为2102/2102、0跳过。

`theme-v21-recipes`正式控件/配方及两进程重启通过；新增`ControlCenterWindow.ThemeProbe`检查9个真实按钮×6个状态步骤（默认、悬停、按下、恢复、禁用、恢复），覆盖初始纸白、跟随系统、墨色、深海、切回纸白、最终system与保存后重启system，共378项状态读回，错误0。颜色弹窗取消不写配置，主题不改海报参数；仍使用真实生产表单/命令。`VisualStateManager`仅驱动控件视觉状态，不宣称本轮发送了OS鼠标事件。已实际查看修复后中心及浅/深色弹窗PNG，XAML捕获不等于桌面合成或高对比/系统实时主题切换认证。

最新交接：普通样式、灯光和图钉交互已有用户正向反馈，默认黑底已在同路径修复。图钉边界/空白手势的未逐项人工范围、BUG-019/020/021与模块B/C/D提案继续独立记录；R5仍IN PROGRESS，R6未开始。旧候选、human-review和日常库保留，无Git提交/推送。

最终同包检查：`theme-v21-{recipes,daily,protocol,fields,rejected,ui,lamp,composition}`八组均通过、产物稳定；437源码指纹无漂移，实际查看保存system重启后的中心PNG。体验命令`& .\out\lr-r5-evidence\start-center-theme-review.ps1`，绑定候选21/八组证据/独立`human-review-06/data`；PS5.1与7 CheckOnly通过。探针均正常退出，本轮结束无常驻DesktopUp实例。

## 7. 候选23：半透明收展灰底与系统动画策略

2026-09-27，用户报告海报A标题下整片浅灰、追光及闲置轻摆均停止。运行包为候选21，体验库为`human-review-06/data`。先用SQLite只读在线备份保存`poster-background-motion-report-v22/user-state-copy.sqlite3`和原日志；用户从托盘退出后才运行独立副本。未改原库、Windows设置或其他应用。

### 7.1 背景：原生复现与修正

A保存的Lamp底色`#17221E`、不透明度25%，追光/动效开、闲置强度100；配置没有变成灰色。新增隔离`--backdrop-probe`使用Windows.Graphics.Capture只捕获自身视觉HWND，包含原生合成背景，补足过去RenderTargetBitmap不含SystemBackdrop的盲区。

旧生产路径的`reproduce-02/data`对照：initial正常；两次collapsed→expanded均在60px标题以下变灰；ordinary-redraw仍灰；相同HWND、尺寸和画刷下，erase-redraw恢复。侧边无内容的24个像素对照中18点失败，最大通道差191。原PNG、日志、配置与`pixel-red-green.json`全部保留。这证明终态清底遗漏，不能再用`VerifyWindowBackdrop=true`宣称像素正常。

原因链：R4在旧HRGN后预备更大原生容量；WinUIEx 2.9.3 TransparentTintBackdrop的GDI清底受旧区域裁切，后续扩大HRGN不会自行清理全部新露出区域。原`DesktopLayerAttachment.RequestFrameRedraw`只有INVALIDATE/ALLCHILDREN/UPDATENOW，未要求WM_ERASEBKGND；近白底层透过25%深绿画刷形成约`#C5C8C7`。正式修正只在现有FinalRestore收尾重绘加`RDW_ERASE`，位置在最终HRGN提交之后；未增加每帧重绘、改输入区域、改底色或不透明度。

平台依据：[RedrawWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-redrawwindow)、[DC可见区域](https://learn.microsoft.com/en-us/windows/win32/gdi/window-regions)、[锁定版本TransparentTintBackdrop](https://github.com/dotMorten/WinUIEx/blob/72f2975d2a237c0d7ad1113fe617d5894e66feb6/src/WinUIEx/TransparentTintBackdrop.cs)。实测范围是稳定终态；没有采集逐帧GPU轨迹，不宣称已证明动画全过程无闪烁或所有DPI环境。

### 7.2 灯光：系统策略导致双停，并非两个分支冲突

正常桌面会话首次读取`UISettings.AnimationsEnabled=false`、`AdvancedEffectsEnabled=true`。候选21独立`lamp-system-policy/observation.json`进一步实测`ReducedMotion=true / StaticOnly=true / MotionFrames=0 / timers=0 / GlobalMotionPaused=false`；每海报参数仍允许动效。`PosterSpatialRuntime.LampMotion.SetLampMotionDemand/ArmLampMotion`遵循系统动画偏好，故追光与闲置共用计时器都停止；`LampSceneMotion.Resolve`的跟随/闲置互斥，无竞争证据。本次没有`lamp.motion_stopped`异常。

期间系统读值自行恢复true（没有由本任务修改设置，触发者未确定）；`windows-ui-settings-desktop.json`保存后次正常会话读值。候选22后次Lamp门实测`ReducedMotion=false`、1个计时器、221帧，跟随/闲置处理器均通过。两个进程的策略分支证据不能扩称同实例实时切换或本轮OS鼠标追光认证。最早`windows-ui-settings.json`来自受限shell、AdvancedEffectsEnabled=false，不能与正常会话混用；其后正常会话和App证据优先。

保留现行尊重系统动画设置的行为；没有静默强开Windows动画，也没有新增越过系统偏好的产品选项。若以后决定模板动效独立于系统，需要另行明确交互及减少动态效果的规则。此处不重开BUG-023的alpha遮挡根因，也不改变模块B/C/D待审状态。

### 7.3 候选、回归和交接

最终候选`candidate23-backdrop`，App SHA256 `85DD19495DB28892655B085916E6B0D42430C0B4D13BFE873565E98AE7D2F41B`，manifest `F69555706A776329143584C065E050AE3D91A4926F71FC32A8C3D4C3382CBCF2`，439源码/402运行产物。生产修正之外，仅新增自身HWND背景探针/脚本与主题探针连接状态读回；捕获超时使用同步frame移交及未领取frame释放，不留async事件尾部位图。合同输入未再变化：`poster-background-motion-report-v22/contracts/backdrop-v22.trx`为2102/2102；App/fixture构建0警告错误。

中间候选22四组原生终态全部通过；八门中的recipes蓝色弹窗Pressed/Disabled出现框架Light颜色，原探针没有持续记录是否仍加载，不能断言是产品错误或外部关闭。最终探针补逐状态`connected/actualTheme`并拒绝检查已卸载弹窗；候选23 recipes及重启通过。旧失败保留，不改写为PASS、不据此修改产品主题。

最终`backdrop-v23-{daily,protocol,rejected,fields,ui,lamp,recipes,composition}`八组同包门全部通过。`poster-background-motion-report-v22/final-v23-{user-state-copy,opacity-0,opacity-100,cold-collapsed-25}`四组原生检查共90点、最大差值0，源快照/产物不变、退出0/账本0；已实际查看展开后PNG。最后源码/运行产物指纹复核一致。只认证指定条件下收展稳定终态，不认证逐帧闪烁、HDR/混合DPI或当前OS鼠标追光。

体验入口：项目根目录`& .\out\lr-r5-evidence\start-backdrop-review.ps1`，PS5.1/7 CheckOnly通过。`human-review-07/data`已从故障时只读快照准备独立副本，保留A/B的底色、透明度、尺寸和内容；原human-review-06与日常库保留。本轮结束无常驻DesktopUp。R5 IN PROGRESS / R6 NOT STARTED；BUG-019/020/021、桌面空白手势、模块拆分待审与逐帧/混合DPI范围保持。没有Git提交推送。

## 8. 候选24：展开过程灰底与A/B不透明度对照

### 8.1 用户反例与证据边界纠正

用户指出候选23的A在**展开过程中**仍灰，B相同模板却正常，并提出不透明度差异。候选23仅在FinalRestore清底、只验证稳定终态；该结果不能当作动画过程已通过。用户从托盘退出后，先将human-review-07库以SQLite只读备份保存到`out/lr-r5-evidence/poster-animation-background-v24/user-state-copy.sqlite3`，全部实验使用新副本，原体验库保留。

故障时A、B均为Lamp、底色均`#17221E`；A不透明度25%、B100%。两者灯光颜色、轻摆强度及速度等也不同，所以增加仅交换不透明度的副本，其他参数不改。配置与旧包动画配置一致性记录在该目录`configuration-at-report.json`、`investigation.json`；节奏为maximumStepPixels=4、frameIntervalMs=8、durationMs=160、ease-in-out。

新增探针在原生提交之后保持初/中/末三个严格中间HRGN，使用Windows.Graphics.Capture捕获自身海报窗口，并验证采样前后实际区域、背景ARGB不变。**这是分阶段原生像素采样（phase-paced），不是原速逐帧录像。** 原速完整动画、全部GPU帧、HDR/混合DPI和用户最终观感仍未认证。

| 同一生产行为对照 | 原生背景色全程读回 | 修复前中间像素最大差 | 最终候选中间像素最大差 |
| --- | --- | --- | --- |
| A原配置25% | `#4017221E` | 191，灰底复现 | 0 |
| B原配置100% | `#FF17221E` | 0 | 0 |
| A仅改100% | `#FF17221E` | 0 | 0 |
| B仅改25% | `#4017221E` | 191，灰底随透明度转移 | 0 |

修复前使用`red-app`（候选23生产呈现行为，增加诊断入口；并非冒用候选23原DLL）；结果为`red-A25/B100/A100/B25`。最终结果为`final-A25/B100/A100/B25`。A采样高145/267/388，B181/362/539；每次左右两个新露出的无内容点，共24点均与同次显式ERASE终态参照一致。四组最终源副本/产物稳定、错误0、退出0、资源0。已实际查看修复前/后的中段PNG，红测只因像素不符失败，未把探针执行失败当成产品复现。

### 8.2 正式修正与恢复

结果支持用户关于A/B透明度差异的判断，但不是动画漏传25%参数：ARGB在全过程采样中保持25%。问题是准备容量时被旧HRGN裁掉的底面没有在扩大可见区域时清理；半透明画刷透出旧白底，100%背景遮住了它。此前的末帧ERASE来得太晚。

新增`DesktopRoundedRegion`及`IDesktopLayerAttachment.ClearExposedVisualBackground`。在`PosterVisualWindow.CommitSpatialFrames`确认窗口容量、HRGN和原生读回后，计算新旧圆角区域差集，使用`RDW_INVALIDATE | RDW_ERASE | RDW_NOCHILDREN | RDW_ERASENOW`同步请求清理新露出的底面；普通`ApplyFrame`也在原生尺寸完成后调用。只允许已注册的透明视觉HWND，不接触输入岛；无新区域时快退，不按模板、颜色或不透明度硬编码。保留FinalRestore完整ERASE恢复；不是GPU原子提交或present fence承诺。

只读复审发现清底失败后区域缓存可能导致同尺寸重试快退，已补失败时缓存失效，下次重断言区域并清全部可见底面。三个临时GDI区域始终释放。合同新增覆盖输入/外来/已卸载句柄拒绝、混合宽高变化、圆角变化、错误传播、同区域快退和非法范围；此处失败缓存恢复是代码复审结论，未宣称已注入真实Win32擦底失败。

### 8.3 候选与交接

最终候选`candidate24-frame-backdrop-final`：App SHA256 `0F02DA495BFB72DC18F94CA8B347F9EDDC6AF5BB541C1356C3135F7DFE47BF94`，manifest SHA256 `4A4E09F7069FF0BBDD64FD3BC88B628E6D1D98127134A8377E0B76F1F6E20F94`，441源码/402运行产物；源码清单`source-frame-backdrop-v24-final.json`。中间`candidate24-frame-backdrop`与red/green包保留，不作为最终体验入口。

`full-contracts/backdrop-motion-v24.trx`为2105/2105、0跳过；App/fixture构建0警告错误。最终四组原生阶段检查已通过；`frame-backdrop-v24-{daily,protocol,rejected,fields,ui,lamp,recipes,composition}`八组同包门全部通过，包含相应重启、100项尺寸组合及灯光/主题处理器，退出0/资源0、产物稳定。这些自动结果不替代OS鼠标/空白桌面手势或人工审美验收。

体验命令：项目根目录`& .\out\lr-r5-evidence\start-frame-backdrop-review.ps1`。PS5.1与7 CheckOnly均通过，441源码指纹与402运行产物无漂移；`human-review-08/data`已从故障快照复制并写入最终候选绑定，保留A25%/B100%的原参数，原human-review-07和日常库未覆盖。所有隔离测试已退出，当前无DesktopUp常驻实例。用户应重点原速反复收展A，再对照B；当前未获该候选人工通过。

协作：root负责公共合同/原生层/视觉接线/测试/Docs及串行GUI；lamp_motion仅负责App.BackdropProbe.cs、Posture探针钩子及新脚本，交付冻结后只读审查。R5保持IN PROGRESS，R6未开始；BUG-019/020/021与桌面空白手势未自动关闭，Lamp模块B/C/D仍待方案审阅。未改变系统动画偏好、Lamp参数或交互模型，无Git提交推送。

## 9. 候选25：循环起点反向滚动与主线审阅节点

### 9.1 用户反馈与根因

用户认可当前UI、动画表现，另报开启纵向循环后，向下正常、起点向上被挡。用户要求修正后先同步文档、计划和项目进度，再给出后续系列计划供判断，**不直接开始主线**；该边界已登记DEC-028。既有候选24的UI/动画记录为当前体验认可，不外推到所有DPI/GPU帧或未逐项验证的原生手势。

根因在`PosterVisualWindow.Content.cs/ScrollItems`，并非Domain循环求解缺少反向能力：两轴滚轮都先计算`Math.Max(0, offset + delta)`，再做循环取模；从0向上本应产生负位移，却先变成0。每次`Math.Round(... / step)`还丢弃不足半格的小增量。既有Domain的负偏移/正反循环合同已通过，但旧生产滚轮没有保留该输入，因此那些模型PASS不能证明实际滚轮路径正确。

本轮将输入映射收敛到`ContentWheelScrollPolicy.Advance`，使用现有`ContentLayoutFlow.NormalizeOffsets/IsLooping/AdvanceScrollX`和原始布局快照；普通一格滚轮仍对应一个网格pitch。循环先保留有符号增量再取模，有限浏览才在两端钳制，小增量保留在RAM偏移中。空/短内容不实际循环、不生成副本，保存的Loop偏好不清。生产仍经相同`HandleContentInput→ScrollItems→EnsureContentSnapshot/Reclip→DrawContentSnapshot→BuildInputLayout`，没有新增滚动计时器、持久化偏移或第二布局权威。

### 9.2 红绿与证据范围

证据目录`out/lr-r5-evidence/loop-wheel-v25`。新增10项纯合同先按旧算式复现8项失败（两轴起点、微量累计、长序列及关Loop后的反例），修正后相关47/47、全量2115/2115、0跳过。纯合同覆盖每轴1000次正反混合累计、可见副本身份、有限边界及0/1项。

旧生产ScrollItems保留构建为`red-app`，新增同一隔离探针；`red-browse`第一次+120事件即失败：应到phase2970，实际仍0。此包的纯策略虽已修正但旧处理器未接入，专门证明接线缺口。修正后的`green-browse`实际调用当前已发布ContentItem区域的Scrolled处理器，共12场景、102次事件、984个检查通过：

- 纵/横起点反向露出末项和首段副本，反向一格回0，连续正反均可返回；6次20单位等价一次120，反向抵消。
- 关闭循环后起点/终点有限钳制，在接缝位置关Loop后回到合法范围；单项短内容偏移0且只有一份卡片。
- 每次滚动核对真实XAML的TileKey、卡片位置/大小/裁切与已发布Input区域及同一ItemId；布局原文、成员集、revision和entity/document version均未改变。
- 四张XAML截图为纵/横起点及反向后，已实际查看纵向末项97～100与首段同屏的接缝图。它不包含SystemBackdrop，不拿该截图复验已认可的背景/动画。

该探针直接调用生产共享处理器，没有发送Windows滚轮消息或OS鼠标；`NativeWheel=false`。它证明本次错误所在的应用输入映射和呈现/命中发布链，不代替用户实际滚轮复看，也不认证循环副本的直接拖动、跨接缝抓取点或R6被动恢复。短内容配置通过正式命令在独立夹具中建立，不是滚动动作改写用户配置。

### 9.3 候选与进度

最终候选`candidate25-loop-wheel`，App SHA256 `1FE34E3121EBDF3104753510294768D282F160517511A83C64D9D3E4BDA9D6CA`；manifest SHA256 `EE463C5898F8C7A9345BACEA3212616FB8AD39A2694F937CA5DE958C539F2C59`。445源码/402运行产物，源码清单`source-loop-wheel-v25.json`；App/fixture构建0警告错误。`green-browse`与最终包逐文件指纹一致；`loop-wheel-v25-{daily,protocol,rejected,fields,ui,lamp,recipes,composition}`八组既有同包门全部通过，相关重启/主题/灯光/100项几何组合无回退，正常退出资源0。

体验入口：项目根目录`& .\out\lr-r5-evidence\start-loop-wheel-review.ps1`。PS5.1/7 CheckOnly通过；该入口同时验证循环探针与八组既有证据的完整产物绑定。新human-review-09从原human-review-08的快照复制，保留用户参数；原库hash未变。445源码指纹无漂移，无常驻DesktopUp。用户可用实际滚轮从纵向循环起点向上复看，不要求重新确认已认可的整套UI/动画；该候选的实际滚轮人工确认尚未取得。

原human-review-08已在无DesktopUp进程且无有效WAL时复制到本轮`user-state-copy.sqlite3`，源/副本hash记录在`source-profile.json`；原库不覆盖、旧包不替换。root负责生产/集成/Docs，lamp_motion仅交付新浏览探针及脚本，均已冻结。本轮只回补BUG-008中的滚轮输入问题，不开始R6；BUG-019/020/021及空白桌面手势范围保留。

后续顺序与各里程碑统一在[主计划§8](../../30_Plans/V3_04_L_REMEDIATION_REVIEW_PLAN.md#8-当前出口和最近下一步)。建议R5确认→R6组合→待批准的最小模块B/C/D→R7保真/可靠性→R8接入交接→V3-05真实内容闭环。模块位置是待审建议；本轮交付后主线暂停，等待用户判断，无Git提交推送。
