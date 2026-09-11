# M08-04：共享调度与空间接线

日期：2026-09-11。范围：调度/事务接线、合同与隔离真实进程门 PASS；M08 整体仍 IN PROGRESS。
需求及决定沿用 [M08计划](../../30_Plans/V3_04_M08_AUTO_POSTURE_PLAN.md)、[M08-01](M08_AUTO_POSTURE_MODEL.md) 与 V2-TBD-017。下一项 M08-05 最小设置入口；不提前交付全部人工/性能门。

M08-07 [R1补充](M08_ANIMATION_INPUT_R1.md)：普通按键/临时capture只阻挡出队前准入，不单独取消正在动画的请求；实际语义保护及失效仍保留。失效日志在观察时捕获保护/代次，避免稍后pump只看到释放后的状态。下方04证据属于原04产物，当前状态见07。

## 1. 已接入的运行路径

`原生被动观察/输入唤醒 → 共享 AutoPostureScheduler → 既有 PosterSpatialRuntime → R4 呈现 → 受限 System 姿态提交 → 同 revision 三端投影 → 恢复完成回执`

- `Application/AutoPostureScheduler.cs`：一个单调时钟、一份多海报模型、最多一个待执行 dispatcher pump。原生回调及时采集每次边界证据，但在其调用栈上不开始空间动作；出队再次读取当前会话、策略、指针、保护、实体版本和 busy。
- `App/App.AutoPosture.cs`：正常启动即组合运行器。需要观察时创建唯一非重复计时器，按最近未来截止点与 120ms 兜底间隔的较小值唤醒；没有逐海报 timer。120ms 是当前实现参数，不是响应时延/性能认证。输入事件与菜单活动加速观察，不记录指针坐标轨迹。
- 无 enabled＋visible＋ready 海报时，计时器、其账本 lease、PointerActivity/菜单观察订阅均释放；恢复/投影/会话事件重新驱动评估。零专用任务不等于整个应用没有既有桌面健康监测。
- `App/PosterSpatialRuntime.Automatic.cs`：检查当前目标 session/entity/policy，自动收起和展开均调用原有 SetCollapsed；源与推让邻居仍由连续空间求解、完整读集和同一 SQLite 事务处理。沿用先下后上、有效高度、锁定障碍、不能完整归位就不动的规则。
- `Application/AutomaticPostureCommit.cs`：新增独立 `IAutomaticPostureCommitter`，只允许 System＋SetCollapsed＋启用/可见目标＋完整目录读集＋连续空间载荷。普通 `Execute` 仍拒绝 System；不放宽设置、删除、移动等系统命令。
- 请求完成只在 `FinishPosture` 完成既有恢复后回执。按完整 AutoPostureRequest 身份匹配，取消不能提前释放单航班。空间不足不抖按钮、无部分写入；原触发未变化时不重复求解。忙后按新鲜资格及稳定排序继续，不积存历史命令。

## 2. 意图、保护与生命周期

- `AutoPostureEvidence` 区分显式手动意图、独立空间变化和普通 revision。重命名不重试空间；自动批次和其邻居帧不制造新触发。手动布局、显隐/锁定、目录及内容度量变化可更新独立空间代次。
- 手动姿态/拖缩先取消已有自动事务，再使用原手动通道；不新增手动最后意图队列，也不放宽原拖缩起点/read-set 检查。
- 接线发现的边界：向上展开后的手动收起可能移动标题，完成时指针已在新几何外。仅按完成时 presence 会丢掉手动抑制。因此记录意图时的真实 `ExitVersion`，通过可选 `ManualCollapseExitVersion` 传给纯模型；几何变化不解除它，只有真实离开再进入才展开。显式手动展开、会话替换/移除及恢复基线会清理该临时证据，不写入数据库。
- 编辑为本海报保护；菜单、删除确认、按键/capture/手势为既有共享保护。菜单在准备/释放时通知，失败回滚也成对通知；观察者异常不会破坏菜单原有输入恢复。控制中心仅打开不算保护。
- App 的共享环境失效递增 epoch、停止 timer；ready 后重新同步。隐藏/删除取当前注册集，退出先停自动服务、撤销活跃请求，再释放海报会话。动效暂停不被解释成自动功能关闭。
- 默认仍 OFF，延迟 1～300 秒/默认 3 秒；schema7、持久化字段、恢复/空间几何权威均不变。M08-05 才提供实际设置控件。

## 3. 验证范围与实际结果

新增 **38/38**，全量 **709/709**，0失败/0跳过；独立 WinUI Release 构建 0警告/0错误。

- 调度夹具24项：1/3/300秒边界、出队重验、同队列真实往返、忙后排序/重入、五类保护与精确取消确认、1000次无空间观察只尝试一次、关闭/隐藏/未就绪/移除、迟到回执、手动移动标题抑制、销毁、同步回执及日志故障隔离。
- 事务夹具12项：System受限入口、自动嵌套展开/推让/归位、同revision与幂等、完整CAS冲突、SQL故障无半状态、元数据不触发重试、锁定源自身仍能自动收展。
- 菜单通知2项：正常/准备失败都有 active→inactive，坏观察者不阻断其他观察者或输入恢复。

最终真实进程：**PID43140**，`out/verify/v3-04-m08-04/native-final`，Windows 自带 **PowerShell5.1** 启动。使用新建三海报配置，通过正式命令启用开关；没有替换指针样本、移动鼠标、合成点击或直接调用自动动作。

1. 原生观察/共享计时器发起三次自动收起，依次通过真实 WinUI 动画及空间/CAS提交。
2. 三张收起后静止等待没有继续增加 revision；全部禁用后专用 timer 和指针订阅为0。
3. 原手动通道展开一张，再启用后立即关闭，旧截止点不执行。
4. 再次启用，等真实自动动画开始后关闭；实体变更使本次回执为 Conflict，完成布局恢复，无姿态半提交。总计4自动请求＝3Committed＋1Conflict。
5. 最后3张 XAML/原生布局读回通过，revision17；exit0、无error/critical、账本clean/0。

中间 `native-01` PID48532（3次提交）和 `native-02` PID39028（增加动画中取消）均通过且正常退出，保留日志；最终证据以 native-final 为准。首次补丁有一处旧文案匹配失败，核对部分应用后修正，没有删除失败用例或改空间策略来凑PASS。

本机实际进程 `isElevated=true`、manifest仍asInvoker；未修改UAC或要求提权，不把它算普通权限认证。没有改 Explorer、显示配置或用户文件，未迁移日常数据库。M07稳定包原24指纹保持，App DLL仍 `C699CAB63D50E224B697E2BD4F8918ED629BFB4BC7E40CBD7375113BB403BDAF`。

**证据限制**：本次真实硬件指针门验证的是在外计时/自动收起及取消恢复；真实鼠标进入标题自动展开、菜单点击、恢复组合、视觉无白条和1/3/6增量资源仍按 M08-06/07 集中验证。自动展开的请求、空间及持久化已有纯/事务测试，不拿手动展开的实际进程步骤冒充硬件 hover 门。XAML读回也不是GPU呈现证明。保留M07内存预算差距。

## 4. 命令与产物

在仓库根目录，专项/完整测试及构建：

```powershell
dotnet test tests/DesktopUp.Contracts.Tests/DesktopUp.Contracts.Tests.csproj -c Release -p:Platform=x64 --filter 'FullyQualifiedName~AutoPostureSchedulerTests|FullyQualifiedName~AutomaticPosture_|FullyQualifiedName~AutoPosture_menu' --logger 'trx;LogFileName=m08-04-focused.trx' --results-directory out/verify/v3-04-m08-04
dotnet test tests/DesktopUp.Contracts.Tests/DesktopUp.Contracts.Tests.csproj -c Release -p:Platform=x64 --no-build --logger 'trx;LogFileName=m08-04-full.trx' --results-directory out/verify/v3-04-m08-04
dotnet build src/DesktopUp.App/DesktopUp.App.csproj -c Release -p:Platform=x64 -o 'G:\A_\Tools\DesktopUp_V2\out\verify\v3-04-m08-04\app\' -bl:out/verify/v3-04-m08-04/build.binlog
```

重跑使用**新的、从未存在的**隔离目录；先从托盘正常退出旧 DesktopUp，脚本不会强杀进程。示例：

```powershell
dotnet run --project tests/DesktopUp.SessionFixture/DesktopUp.SessionFixture.csproj -c Release -p:Platform=x64 -- out/verify/v3-04-m08-04/native-rerun 3 --measure-expanded
& ./scripts/smoke-auto-posture.ps1 -ExecutablePath out/verify/v3-04-m08-04/app/DesktopUp.exe -DataRoot out/verify/v3-04-m08-04/native-rerun
```

smoke要求鼠标不持续停在待收起海报上、无持续按键/菜单保护，否则正常安全保护可能让测试超时；按日志区分保护与故障。测试会改这些隔离海报的开关和姿态并自行正常退出，不是日常启动器。零日常数据改动；不要求 PowerShell7.4 或管理员权限。

SHA256（均在 `out/verify/v3-04-m08-04/`）：

| 文件 | SHA256 |
| --- | --- |
| app/DesktopUp.dll | 06CB5180E9E6915EB49616BFA951CBA423C1950C1E5A2F249279C43605DFE98C |
| app/DesktopUp.Application.dll | 51910BC7AB9763F9791228E332087D104AF968CDADABE6776C9371E6D5C5C211 |
| app/DesktopUp.Windows.dll | 9B6FDF2226109848760507F3830555A5A2B89A24989C1B0B4D7B7A5E18379C6A |
| m08-04-focused.trx | 061BF7166D4483E09EEB7AEC134247FF9ADDA16AC9CC00B7EBAA5130D5D01A0D |
| m08-04-full.trx | B1E360F3396E28A9048E2A46FAAAE11447B67A138765DC449987F24CB03EB751 |
| native-final/logs/desktopup.jsonl | 99D9026AE35DDF1BA5A4EDF5B9C6EC14DD9A58FE1BE30A8D8563608498122E18 |

当前只完成本子项；不推进设置UI或真实Item，不commit/push。继承M08既有DeskBox参考取舍，本步独立实现，无新增第三方代码移植。
