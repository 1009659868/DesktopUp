# M04-06 多海报输入路由与确认交互

日期：2026-09-08。状态：`AUTO PASS / MANUAL PENDING`。下一项：M04-07 集中多窗口人工回归。

本项接在 M04-03..05 上，不重写已验证的 DesktopHost、拖缩求解器、原生捕获或状态权威；没有新增 SQLite 迁移。保留上一轮未提交修改，本轮未 commit/push。

## 1. 交付行为

- 每个输入实例绑定 PosterId + session epoch；命中区域只接受已成功应用的 geometry revision。旧实例、旧几何、已取消/删除的回调不能重新生效。
- 全进程共享一个捕获租约。按下时冻结目标和起始框，拖缩中的位置/尺寸更新及换列不把 capture 转给另一海报；取消/丢失捕获/移除实例释放租约。
- 无捕获时按实际视觉 HWND 的前后顺序选择有效输入区域；不拿中心选择、默认海报别名、注册顺序或数据库 stack order 冒充物理顺序。历史异常重叠时，顶部输入空洞可以继续检查下层，最终无命中则保留桌面路由。
- 圆角和未登记的空白区域不因 router 变成全矩形输入面。正常布局的碰撞占位仍是完整外框，主题透明不允许绕过占位。
- 普通按钮按下后若布局改变或释放位置离开按钮，不触发旧按钮；连续拖缩允许同会话发布新几何版本。
- 海报“...”菜单新增“删除海报…”，控制中心所选海报也新增同一入口。只使用一个共用确认流程，取消为默认按钮，Escape/关闭中心/退出均不提交删除。
- 确认框固定海报名称、ID、实体版本与目录版本。切换中心选择不重定向目标；目标或目录变化取消旧确认；提交仍通过既有实体读集 CAS 和统一命令事务。

## 2. 实际代码与边界

| 层 | 本项职责 |
| --- | --- |
| Application/PosterInputRouter | 不可变区域快照、单捕获租约、会话/几何版本门、恢复失效；不持久化状态 |
| Windows/PosterInputIslandManager | 读取视觉 HWND 顺序、路由原生事件、SetCapture/GetCapture 读回、成功应用后发布输入版本；保留 MA_NOACTIVATE/bottom 保护 |
| App/PosterVisualWindow、WinUiPosterSession | 显式传递身份；事件接收检查身份/来源/版本；环境挂起、局部失败和释放使输入失效；菜单回调绑定原实例及实体版本 |
| Application/PosterRemovalConfirmation | 单个不可变 ticket，单次完成、取消、目标与目录版本检查；不会操作文件 |
| App/ControlCenterService、ControlCenterWindow | 中心与海报共用 WinUI ContentDialog；正常中心窗口承载确认，不改变海报 owner/样式/层级；安全关闭与资源释放 |
| 既有 StateProjectionCoordinator / StateCommandPolicy | PrepareRemoval → RemovePosterCommand；事务校验原目标/目录 read set，删除后按集合投影释放会话 |

ContentDialog 显式设置中心的 XamlRoot，默认按钮为 Close/取消，依据微软 [ContentDialog 文档](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.controls.contentdialog)。没有新建 topmost 弹窗或把海报变为可激活普通窗口。

恢复时暂停共享路由、取消原生手势和确认；每个成功恢复实例发布新几何版本。失败实例保持无可用输入快照，正常实例可恢复输入，不因单实例失败全部失去按钮操作；协同空间操作仍遵守 M04-03..05 的完整布局门。

收尾审查补强：失效时同时把原生输入 HWND 的区域置空，避免“逻辑拒绝事件但旧窗口仍拦截桌面点击”。区域操作失败时降为零 alpha，并记录结果；成功恢复重新应用区域和透明度。零 alpha 的输入穿透语义见微软 [Layered Windows](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features)。31580/26284是这一补强前的中间回归，最终证据为19540/24776。

确认提示明确：移除该海报的几何、锁定/收起、主题选择等配置及此海报的关联；不删除、移动或清理真实文件/主题包。当前尚未启用真实文件收纳，没有伪造“受管找回目录”；文件仍在原位置。最后一张删除后保留空目录，下次启动不自动补默认，控制中心/托盘继续运行。界面没有撤销入口，不宣称点删除可立即撤销。

V3-05 接入真实文件任务后，必须补“进行中文件任务阻止/安全等待”和真实受管内容路径摘要；本项没有活动文件任务，也没有用假 busy 标志冒充完整文件安全门。新增/克隆、导入导出与完整中心预览不是本项扩展内容。

## 3. 自动证据

- 全量 **313/313 PASS**，0 失败、0 跳过；在上一项 288 基础上新增 25 个执行用例。
- 新增 PosterInputRouterTests：实际顺序参数/下层命中/圆角与空白、四边四角捕获保持、释放越界、点击几何变化、负坐标与不可变区域、旧实例释放/恢复、同版本变形拒绝、已失效版本不能复活。
- 新增 PosterRemovalConfirmationTests：取消与重复完成、切换中心目标、实体/目录并发 CAS、最后一张与重新初始化零目录、非法来源/旧请求、设置独立变更、关闭/退出后的旧 ticket；使用真实隔离 SQLite。
- 最终 Release：0 warning / 0 error。没有改变现有迁移 v1..v5。
- 实际 WinUI 确认 smoke：最终 PID **19540**，三海报/三个就绪输入 cluster，实际打开确认 3 次；取消、重复请求不换目标、关闭中心取消、重开中心、保持确认框退出均通过；状态 revision 始终为 3，exit 0、资源 0。中间构建 PID45548（两次对话框）与38228（三次）同样通过，最终二进制以19540为准。
- 同一最终 Release 的连锁收展回归 PID **24776**：6次收展、77个原生批次帧，exit 0、资源 0；日志位于 `out/verify/v3-04-m04-06/smoke-chain/logs/desktopup.jsonl`。
- 该 smoke 不自动点击“删除”，不模拟人的鼠标；真实鼠标路由/菜单观感及删除按钮点击仍列入 M04-07。数据库删除与会话删除安全已有自动合同，但不冒充实际点击 PASS。
- `--input-confirmation-smoke` 必须提供显式隔离目录，拒绝普通用户数据目录；脚本拒绝已有运行实例，不强杀日常程序。

产物：`out/verify/v3-04-m04-06/app/`；TRX：`out/verify/v3-04-m04-06/results/m04-06.trx`；确认日志：`out/verify/v3-04-m04-06/smoke-three/logs/desktopup.jsonl`。

| SHA-256 | 值 |
| --- | --- |
| DesktopUp.exe（启动宿主） | 958D77CA81E0035094D2A603DFCD3DBBE595D048F303E574B65EF14AB32BB3A0 |
| DesktopUp.dll（本项应用程序集） | D0E5C0EB01B778CFCB29A61A61EC223C6C8000A01C2B177237FAA1CDA0555EF5 |
| DesktopUp.Windows.dll（原生适配器） | DEE91437F895D12C87E54D5E8B7F53CB30E88F081694B4437ECC3AF1DD06F37E |
| m04-06.trx | 3EEFAA8EDD714AE880263CCAB584D4447F33D580CF1B6CAB2D6D2749A53BDCB3 |

## 4. 启动与重跑

先正常退出旧 DesktopUp。下面使用本轮已准备的隔离三海报配置，A/B/C 中包含锁定和收起初态，不会改日常数据：

```powershell
$env:DESKTOPUP_V3_TEST_DATA_ROOT = 'G:\A_\Tools\DesktopUp_V2\out\verify\v3-04-m04-06\smoke-three'
& 'G:\A_\Tools\DesktopUp_V2\out\verify\v3-04-m04-06\app\DesktopUp.exe' --show-control-center
```

测试结束在同一 PowerShell 中执行 `Remove-Item Env:DESKTOPUP_V3_TEST_DATA_ROOT`，只移除环境变量，不删除配置目录。如人工删除了夹具海报，需要重建时请用新的目标目录，不覆盖旧配置：

```powershell
Set-Location 'G:\A_\Tools\DesktopUp_V2'
dotnet run --project tests/DesktopUp.SessionFixture/DesktopUp.SessionFixture.csproj -c Release -p:Platform=x64 -- 'G:\A_\Tools\DesktopUp_V2\out\manual\m04-06-input' 3
```

然后把上面的环境变量改为该新目录。重复创建时目录必须不存在。

自动真实进程复测需要 PowerShell 7.4+：

```powershell
pwsh -NoProfile -File scripts/smoke-input-confirmation.ps1 -ExecutablePath out/verify/v3-04-m04-06/app/DesktopUp.exe -DataRoot out/verify/v3-04-m04-06/smoke-three -ExitWithDialog
pwsh -NoProfile -File scripts/smoke-spatial.ps1 -ExecutablePath out/verify/v3-04-m04-06/app/DesktopUp.exe -DataRoot out/verify/v3-04-m04-06/smoke-chain
```

## 5. M04-07 集中人工待确认

1. A/B/C 的收起、更多菜单、锁定、滚动只作用目标；空白框选、桌面右键、原生图标仍可操作。
2. 拖缩四边四角、碰撞截停与换列时 capture 不丢失、不串目标；释放按钮外部不误触旧按钮。
3. 海报菜单与中心均能打开原目标删除确认，默认取消；取消/关闭中心不删；确认删除只移除该海报，其余正常，最后一张删除后中心/托盘可用。
4. 确认期间目标/目录变更或 Explorer/显示恢复，旧确认被取消，旧按钮/旧窗口不能复活实例。
5. 菜单/对话框关闭后海报不覆盖普通程序；启动、点击、桌面刷新、文件选择器、Win+D、Explorer 重建、退出保留既有核心门。
6. 在本轮最终 Release 复核 M04-03..05 保尺寸展开、连锁推动/条件归位与无白底/规律闪烁；保留 M04-01 和 M03 尚未逐项确认的门，不再重复全量 DPI/安装认证。

本轮测试以工具运行环境执行，不宣称普通权限人工认证、真实桌面快捷方式收纳或跨显示器认证已完成。下一步执行上述集中门；通过后才进入 M05 参数等后续计划。
