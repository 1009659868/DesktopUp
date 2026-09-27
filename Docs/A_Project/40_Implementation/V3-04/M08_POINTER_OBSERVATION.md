# M08-03：被动指针观察与交互保护

日期：2026-09-11。状态：PASS（观察适配器、合同及原生API夹具范围）；不是完整自动收展或新增实际鼠标/WinUI视觉认证。
前置：[M08-01行为模型](M08_AUTO_POSTURE_MODEL.md)、[M08-02设置](M08_AUTO_POSTURE_SETTINGS.md)。主线见[M08计划](../../30_Plans/V3_04_M08_AUTO_POSTURE_PLAN.md)，下一项04调度与空间接线。

## 1. 实际交付与边界

- `Application/PosterInputRouter.cs`增加只读ReadSurface、版本校验的PointerActivity提示，共享原圆角/区域判定到PosterInputGeometry；原命中区域、优先级、capture及透传规则不改。
- `Application/PosterPointerObservation.cs`维护完整当前会话集的标准化presence、EnterVersion/ExitVersion和保护快照。无时钟/线程/文件/命令。保存上一份不可变几何与位置，不保存轨迹或已删除ID墓碑。
- `Windows/PassivePosterPointerObserver.cs`对整个当前会话集一次原生采样，结合逻辑可见几何、真实输入HWND大小、原生接收根、实际层级和遮挡，复用共享菜单与capture保护。
- `Windows/NativeDesktopPointerPlatform.cs`是只读原生端口；没有SetWindowPos、激活、注入、SetCapture或创建输入覆盖窗口。OpenInputDesktop/GetWindowRgn使用的短期句柄均在finally释放；不切换输入桌面。
- `PosterInputIslandManager`仅在有观察订阅者时按需请求TME_LEAVE，已有WM_MOUSEMOVE/LEAVE/按键/滚轮/capture消息提供唤醒提示；不把消息本身当作悬停事实。回调异常隔离且每个岛只报告一次，不中断原点击/拖缩。
- App增加`PosterVisualWindow.Pointer`、`PosterSpatialRuntime.Pointer`与`App.PassivePointer`读口：调用时读取现存会话、编辑/手势/恢复状态；控制中心删除确认从请求开始至异步对话流程finally结束都提供共享保护。仅打开中心不产生保护。

本步没有订阅或启动生产观察循环，没有后台timer/hook，没有调用AutoPostureModel或发System命令。ReadPassivePointer是下一步使用的采集入口；M08-04负责唯一共享源/生命周期/截止时间调度和完整模型帧组装。零目录或所有目标关闭/隐藏/未Ready时，显式采样也不查询原生鼠标。M08-05才增加设置控件。

## 2. 判定规则

| 情况 | 输出/保护 |
| --- | --- |
| 收起标题热区 | ExpansionZone；直接读取现有TitleBar命中区域，动作按钮/边缘等更高优先级区域不算展开热区 |
| 展开可见空白/内容，或动作区域 | Inside；覆盖当前逻辑圆角轮廓，但不扩张输入岛，因此可观察不等于拦截点击 |
| 逻辑范围外、R4额外容量、圆角外、可靠普通窗口遮挡 | Outside；边界采用半开区间，贴边不归属两张海报 |
| 原生查询失败、无接收根、无当前窗口配对、输入读回不匹配、路由失效/挂起、过期Shell身份或其他未验证Progman/WorkerW根 | Unknown；不得把不确定性冒充真实离开，类名相似只可撤销信任，不能授权悬停 |
| 原生接收根 | 只接受自己的Visual/Input，或共享定位器提供且再次校验存活/类/父子/PID的桌面容器；不用ROOTOWNER，不信“同属explorer.exe”或旧路由事件的覆盖权 |
| 上方遮挡 | 额外核对物理层级及可见窗口形状，排除最小化/cloaked；外部窗口透明但无法证明可穿透时保守阻挡。候选20补充：仅当分层窗口属性读取成功、含LWA_ALPHA且alpha=0，才排除已证明全透明的窗口；不缓存，未知/色键独用/非零alpha仍阻挡，见[输入报告§4.2](LR_R5_POINTER_AND_MODULE_REVIEW.md#42-真实复现与修复候选20)。其他海报只使用逻辑可见范围，不用容量 |
| 按键/capture/原生菜单和移动缩放 | 当前5种物理鼠标键只取GetAsyncKeyState高位；GetCapture及GetGUIThreadInfo读取当前/前台交互。R1分类为PointerCapture/PointerButton/Menu；router按下capture也为PointerCapture，真正空间手势才是Gesture。阶段行为见[R1](M08_ANIMATION_INPUT_R1.md) |
| 已有海报菜单/删除确认/编辑/恢复 | 分别复用DesktopMenuInteraction.IsActive、完整异步确认标志、会话编辑态及空间恢复状态；锁定未被改成禁止自身自动收展 |

DragDrop保护位已能由上层传入；目前尚无真实Item/OLE收纳会话，不能将按键/capture保护冒称完整OLE生命周期已实现。V3-05引入实际拖放会话时应传入该位直至会话结束。

## 3. 什么才是“真正离开再进入”

只有连续可靠采样、相同会话/环境、逻辑几何稳定，且物理指针位置确实变化并跨越相应边界，才增加计数。ExitVersion对应离开整个保护轮廓；EnterVersion对应进入标题展开热区。初次同步、解除遮挡、窗口推让/伸缩跨过静止指针、恢复及Unknown间隙不制造边界证据。

几何和鼠标同时改变时保守地重建基线，不猜测哪一个导致跨界；同几何仅layout revision增长不会制造事件。计数跨环境恢复保留单调值，但比较基线重建；新session epoch从0开始。旧sequence/环境被拒绝，当前会话移除后不保留节点。

组合测试已验证用户明确规则：手动收起后保持抑制，遮挡/解除遮挡不释放，真实离开再进入才产生一次展开请求；保护解除后离开延迟重新完整计算，不回放旧资格。本步测试中的模型请求不执行原生动画或事务。

路由提示用于尽快补采样，共享低频兜底用于透明空白或缺少退出消息的路径。仅有消息却没有可靠跨界坐标不加计数；全部发生在未观测间隙中的极快离开/重入不伪造。M08-04应及时处理提示，避免把已观测边界再次合并丢失；实际频率/响应与CPU由04/07测量确定，不能从纯夹具承诺零漏采或120ms时延。

## 4. 验证证据

新增52/52，全量671/671，0失败0跳过；独立WinUI Release构建0警告0错误。三个新增测试文件：

- `PassivePosterPointerTests.cs`：逻辑/物理范围、动作区域、圆角/半开边界、透明空白不拦截、可信桌面/外部根/遮挡、原生故障、路由挂起、Shell/配对过期、1/3/6一次采样、7种保护、capture不抢占、零需求不读取。
- `PassivePosterPointerSequenceTests.cs`：无路由事件仍观测跨界、版本化唤醒/异常隔离、1000次静止指针下几何跨越、同时移动、遮挡与Unknown恢复、动作区进入标题、迟到帧/会话替换、不可变几何、手动抑制及确认解除重计。
- `PassivePosterPointerNativeTests.cs`：真实屏幕外自有HWND，300×800容量/300×360输入/48px标题region，实际ReceiverRoot/区域孔洞/尺寸读回、禁用遮挡、20次查询前后配对层级/区域/几何及前台不变。finally销毁全部测试窗口/类并恢复线程DPI上下文；无移动鼠标、合成点击、Explorer变更或生产App启动。

原生夹具记录：`DisabledOccluderReturned=True; RegionOccludes=True; CursorProbe=True; ProtectionProbe=True`。首次夹具错误套用WindowFromPoint规则，预期physical-point查询跳过禁用窗口，实际返回该窗口；修订为验证两类接收结果均不能越过可见遮挡，保留此差异记录。另一次构建空值警告已修正，最终0警告。没有删除失败场景或更改旧业务测试以凑通过。

命令与产物（独立目录`out/verify/v3-04-m08-03/`）：

```powershell
dotnet test tests/DesktopUp.Contracts.Tests/DesktopUp.Contracts.Tests.csproj -c Release -p:Platform=x64 --filter FullyQualifiedName~PassivePosterPointer --logger 'trx;LogFileName=m08-03-pointer.trx' --results-directory out/verify/v3-04-m08-03
dotnet test tests/DesktopUp.Contracts.Tests/DesktopUp.Contracts.Tests.csproj -c Release -p:Platform=x64 --no-build --logger 'trx;LogFileName=m08-03-full.trx' --results-directory out/verify/v3-04-m08-03
dotnet build src/DesktopUp.App/DesktopUp.App.csproj -c Release -p:Platform=x64 -o 'G:\A_\Tools\DesktopUp_V2\out\verify\v3-04-m08-03\app\' -bl:out/verify/v3-04-m08-03/build.binlog
```

运行环境：Windows/x64、SDK10.0.103。没有启动/停止DesktopUp新旧App，没有迁移日常数据库、替换M07稳定包或改变schema7。M07原24项稳定指纹另行只读验证；实际菜单鼠标、自动动画、Shell恢复及混合DPI不凭屏幕外API夹具补PASS，后续按04/06/07集中验。

最终产物SHA256（此前中间测试结果以本次最终回归为准）：

| 文件 | SHA256 |
| --- | --- |
| m08-03-pointer.trx | 438C331C41EEB83A88C1BA6CD3B6B91B4A354EF69C2B4322AF7AD05782C59ED0 |
| m08-03-full.trx | AD9B00B3D9C8EAB4F98A448F6CAEB8EA76CD4B1830E71E929FAD0FF4EE2A92A9 |
| app/DesktopUp.dll | A631D3498AB940989211B54CDDE2BCBDA51A2D0B96FFFE9590FDFF2D9C7670FA |
| app/DesktopUp.Application.dll | 4CA8B6BE550081FF4C79ED82A450A25DB55C682217E09FCC0E57C80DECD25796 |
| app/DesktopUp.Windows.dll | 27E5A4427AD6B41B9C23ACD64D4222F8C6E66C0A3611DA7CF4E872B7E59EF855 |

## 5. 参考吸收与接线责任

重新阅读本地DeskBox `64f27e1` 的WidgetCompactInteractionPolicy及WidgetWindowBase.Collapse中共享恢复探测/根窗口归属流程；吸收“原生归属＋交互保护＋共享兜底”，不照搬整HWND热区、旧路由证据覆盖原生遮挡、模式/计时器/抬层或它的配置权威。本步独立实现，无新增第三方代码移植。

原生语义查阅官方说明：[WindowFromPoint](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-windowfrompoint)、[GetGUIThreadInfo](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getguithreadinfo)、[GetAsyncKeyState](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getasynckeystate)、[GetPhysicalCursorPos](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getphysicalcursorpos)。仅依赖已验证返回值，API家族的差异不自行推等价。

04必须：UI线程同步采集当前目录/会话并维护单调sequence/环境epoch；订阅既有PointerActivity，按需求启停唯一兜底源和截止时间调度，处理采集失败；以新鲜committed实体/完整空间读集组装模型帧。补受限System姿态命令及精确完成/取消通知，保留R4统一呈现/空间求解/CAS；不能以IsSpatialBusy读口存在就宣称自动通道完成。05设置UI，06生命周期，07人工与增量资源门。

未commit/push；保留之前未提交的M07/文档对齐/M08-01/02成果，不操作旁边旧DesktopUp仓库。
