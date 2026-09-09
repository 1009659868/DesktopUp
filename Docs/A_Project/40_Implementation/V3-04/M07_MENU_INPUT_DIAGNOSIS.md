# M07 遗留菜单命中问题：只读诊断

日期：2026-09-09。状态：OPEN / 已定位输入隔离缺口，尚未取得失败点的原生命中快照。用户要求先排查，本轮没有修改业务代码、构建或测试配置，也未启动/操控App。

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
