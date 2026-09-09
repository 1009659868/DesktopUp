# M07 菜单输入修复 R1

日期：2026-09-09。状态：IMPLEMENTED / CONTRACT & BUILD PASS / REPORTED MENU INTERACTION MANUAL PASS；最终原生6场景完整读回仍未完成。M07 仍 OPEN。

最新反馈：用户明确确认“现在没问题了，‘…’的交互没问题了”，原菜单交互故障记为人工通过，不要求重复证明同一故障；这不补写全部菜单边界和 M07 S3～S5。另报 C 展开后再展开 B、收起 C 时 B 不归位，作为[独立空间策略缺口](M07_NESTED_POSTURE_RETURN_DIAGNOSIS.md)登记；不是菜单修复失败。下方两次遮挡采样保留为历史未通过证据。

## 1. 依据与范围

用户已授权修复。[根因证据](M07_MENU_INPUT_DIAGNOSIS.md)确认菜单前方存在本海报输入岛和邻海报视觉/输入表面，B 菜单左侧的 down/up 实际派发到底层 GHOST SIGNAL。不是改文字坐标或撤销 CAS 能解决的问题。

本次保留 WinUI MenuFlyout、稀疏输入岛、DesktopHost owner、透明视觉、空间模型、SQLite 与命令校验。未切换渲染框架、未重新制作菜单控件、未接真实文件能力、未改菜单样式。修改默认运行路径，诊断仍须显式开关；所有新夹具仅使用隔离配置。

## 2. 实现合同

| 阶段 | 实现行为 | 边界 |
| --- | --- | --- |
| 打开前 | 共享 `DesktopMenuInteraction` 会话结束已有 capture，保存所有当前海报输入岛的透明位，临时设置 layered 输入岛的 `WS_EX_TRANSPARENT` | 一次仅一个菜单交互会话；普通状态的 HRGN/坐标/布局 revision 不变。菜单打开期间整组输入岛让位，不支持同时拖动另一海报 |
| 打开中 | 保持各海报已验证的桌面 Z 序，不在 owner 激活或 bottom reassert 时连带把 Popup 压回输入岛后方 | 不移除正常状态的 MA_NOACTIVATE/bottom guard；保留海报非置顶 |
| Popup 已打开 | 只定位当前 UI 线程、当前 owner 的 WinUI Popup；安装有生存期的 subclass，把 Popup 放到最前海报之前、原先在桌面组前方的普通窗口之后，并读回 | 不使用 HWND_TOPMOST；保留 NOACTIVATE/NOOWNERZORDER，不移动/缩放菜单、不改普通程序窗口；无前序非置顶窗口时仅 Popup 可用 HWND_TOP |
| 原生路由 | 输入透明负责让鼠标真正到达 Popup；原路由另外拒绝菜单期间排队的海报输入 | 仅丢弃路由不能算修复，仍需真实 hover/click 验证 |
| 关闭/取消 | 恢复各输入岛原有透明位、恢复各海报的 desktop-bottom、移除 Popup subclass、释放菜单资源租约 | 幂等；重复 Opened 不重复挂 subclass；回滚失败单独 error，不冒充 clean |
| 状态/环境变化 | 应用新输入布局、实际重挂、暂停、会话销毁或新增海报时取消菜单会话 | 无变化的 Reattach 健康检查不取消菜单；新布局/实体版本仍受原 MenuCurrent/CAS 保护 |

临时透明使用 Windows layered-window 命中语义；层级采用 [SetWindowPos](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos) 的 NOACTIVATE/NOOWNERZORDER，并排除 topmost HWND 作为插入锚点。视觉窗口不改透明方式，普通应用遮挡菜单时不得通过临时置顶绕过。

原生桥类名 `Microsoft.UI.Content.PopupWindowSiteBridge` 来自本机已取证的 WinUI 版本，不宣称为跨版本稳定公共 API。未发现、发现多个、挂钩失败或层级读回失败均关闭并恢复，不猜测 HWND，也不修改别的进程。以后升级 WinUI/增加嵌套子菜单需重新验证此边界。

## 3. 代码与参考归属

- [DesktopMenuInteraction](../../../../src/DesktopUp.Windows/DesktopMenuInteraction.cs)：共享临时交互会话、插入锚点、层级读回、失败回滚及资源台账。
- [NativeDesktopMenuPlatform](../../../../src/DesktopUp.Windows/NativeDesktopMenuPlatform.cs)：本进程 UI 线程窗口校验、原生透明位/Popup subclass/只读命中采样。subclass delegate 保持引用至移除成功或窗口销毁，避免回调悬空。
- [PosterInputIslandManager](../../../../src/DesktopUp.Windows/PosterInputIslandManager.cs)、[DesktopEnvironmentWatcher](../../../../src/DesktopUp.Windows/DesktopEnvironmentWatcher.cs)：菜单期间输入/层级保护；正常路径保留。
- [PosterVisualWindow](../../../../src/DesktopUp.App/PosterVisualWindow.cs)：MenuFlyout 的打开/关闭与共享会话成对绑定，保留原身份/实体/布局校验。
- [合同回归](../../../../tests/DesktopUp.Contracts.Tests/DesktopMenuInteractionTests.cs)：13项新增（含参数化计数）；[原生夹具](../../../../src/DesktopUp.App/PosterSpatialRuntime.MenuSmoke.cs)仅由 `--menu-smoke` 加隔离数据根开启。

DeskBox 参考为本地 commit `64f27e1aa0f16867abe20074f35ea05cd7b12ce7`，GPL-3.0-only；`Views/ContentWidgetWindow.Commands.cs:973` 的 `ShowFlyoutWithInteraction` 成对 Begin/End，以及 `WidgetWindowBase.Interaction.cs` 菜单期间延后恢复的生命周期意图。**未照搬其临时 TOPMOST pulse**。这里的跨海报输入岛让位、原生 Popup 桌面内排序与故障夹具是 DesktopUp 的独立集成补强；不把 DeskBox 使用成熟等同本修复通过。原来源登记与项目 GPL-3.0-only 基线继续有效。

## 4. 本轮实际验证与缺口

| 检查 | 结果 |
| --- | --- |
| Windows 组件首次构建 | 0警告0错误 |
| 合同首跑、最终复跑 | 433/433、0skip；`out/verify/v3-04-m07-menu-r1/results/menu-repair-first.trx`、`menu-repair-final.trx` |
| 最终 App Release | `out/verify/v3-04-m07-menu-r1/app/`；0警告0错误 |
| Windows PowerShell 5.1 | 5.1.26100.8875 的新入口 CheckOnly 通过；没有更换 Shell 或权限硬门 |
| 原 M07 产物 | 原 `scripts/m07-build.json` 的6个指纹全部一致；原包与原诊断包不覆盖 |
| 中间产物原生样本1 | `out/manual/v3-04-m07-menu-r1/20260909-145404-b8c44c/`，PID44128；Popup层级读回与三输入岛让位成功，但15点均未命中Popup，整项不通过 |
| 中间产物原生样本2 | `20260909-145709-e5af52/`，PID23332；补充接收者后确认15点全部是外部进程 HWND `0xD0C74`，不是B/C输入岛；属于测试区域被普通程序遮挡，不能算菜单命中PASS |
| 上述两次退出 | 均由原应用退出入口结束，存在application.stopped；台账clean=true、资源0。没有强杀 |
| 最终产物原生6场景 | 等待主屏右上区域不被普通程序遮挡后再执行；不把中间产物的部分读回等同最终全项通过 |
| 真实鼠标与视觉 | 用户确认“…”交互已正常，原故障人工 PASS；未逐项确认的完整视觉/层级/退出边界不自动补齐 |

首轮 App 夹具少了 Application 命名空间，造成CS0246及次生XAML错误；已补齐。原生回调闭包的空值分析警告已修正。失败构建/被遮挡样本没有改写为PASS。

原生夹具通过应用本身打开真实 MenuFlyout，将三海报临时排列到主屏右上且相邻，依次检查三张×Using/Editing；每张菜单取原生窗口内部3列×5行点位进行 WindowFromPoint 查询，并校验关闭后真实透明位恢复。**查询不是实际鼠标事件**；即使这6场景通过，仍需要用户真实操作。只改变隔离运行的物理布局，不把此布局写回日常配置，也不采集外部窗口标题/文件内容。测性能时不得使用此诊断模式。

遮挡归属补核：只读查询既知接收 HWND `0xD0C74` 的类名/进程，确认是 `Chrome_RenderWidgetHostHWND`、`Code` 进程 PID9796、顶层根`0x10A0B5E`，不是Explorer桌面。没有读取该窗口文本或文件，没有移动/最小化外部程序。当前已请求用户显示桌面后继续，未在相同遮挡状态下反复运行无效采样。

收尾：固定R1六指纹接入后，PS5.1 CheckOnly再次通过；6份交接/来源文档的132个本地链接无缺失，git diff --check通过（仅既有换行转换提示）。最终采样准备前没有DesktopUp活动进程。

## 5. 启动与最短复测

先从托盘退出旧版；Windows PowerShell 5.1 可直接运行：

```powershell
& 'G:\A_\Tools\DesktopUp_V2\scripts\start-m07-menu-repair.ps1'
```

每次新建隔离三海报配置；无自动到期。启动前核对 [6个固定产物指纹](../../../../scripts/m07-menu-r1-build.json)，不会误启动旧诊断版。保留诊断日志以便真实失败/成功事件继续关联。不要同时运行原版或资源测量。

优先复测用户原来的B/C相邻位置：B菜单的图标、文字左/中/右均可高亮与单击；C不得遮住B菜单；进入编辑后“完成编辑/锁定/打开控制中心”仍可操作。随后关闭菜单，确认两张海报拖动、缩放、滚动恢复。删除仅用隔离演示海报并走确认；检查普通窗口依旧在海报之上、退出无残留。S3～S5和M07既有未确认格不自动关闭。

自动原生夹具（约16秒，须先露出主屏右上桌面区域）：

```powershell
& 'G:\A_\Tools\DesktopUp_V2\scripts\start-m07-menu-repair.ps1' -Smoke
```

它会正常退出并检查 smoke 结果、error/critical 与资源台账；外部程序遮挡导致的采样失败应记录环境原因，不应取消层级保护来通过测试。旧进程检测、日常配置保护和单实例规则都保留。

## 6. 交付边界

修复前检查点仍为远程 `11e7f1a`。本轮无commit/push，没有升级依赖、操作桌面文件、修改日常数据库或系统权限。当前提交候选包含上一轮诊断和本轮修复；必须完成最终真实命中与人工格后才能将菜单缺陷关闭，M07未关闭。
