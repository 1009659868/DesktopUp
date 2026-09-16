# M08-05：控制中心自动开关、离开延迟及应用/取消

日期：2026-09-11。状态：**PASS（设置合同／构建／真实控件）**；M08整体仍IN PROGRESS。
2026-09-15适用性说明：本页“开关/文本不自动保存、必须应用/取消”是旧UI实现证据；V2-DEC-005改为常用设置即时反馈与自动提交，迁移在[04-04 UX计划](../../30_Plans/V3_04_04_CONTROL_CENTER_PLAN.md)。沿用SetAutoPosture、默认值/范围、实体CAS、调度和交互保护，不重做M08行为模型。本页PASS不代表新入口已实现，也不关闭M08已知缺陷/余门。
依据：[M08计划](../../30_Plans/V3_04_M08_AUTO_POSTURE_PLAN.md)、[M08-02数据合同](M08_AUTO_POSTURE_SETTINGS.md)、[M08-04调度合同](M08_SHARED_SCHEDULER.md)。

## 1. 本次交付与边界

- 现有控制中心右侧新增“自动收展 · 仅所选海报”：自动开关、离开延迟、应用、取消。默认关闭／3秒，允许1～300整数秒；关闭时仍可预先配置秒数。
- 开关和文本不自动保存；应用通过现有SetAutoPosture命令、PosterId＋EntityVersion读集CAS、committed revision三端投影；不新增schema、仓储、第二份配置权威或计时器。
- 设置仅改变策略，UI不直接执行收起/展开、不改几何；已提交策略仍由M08-04生产调度消费。
- 不实现预览画布、完整参数编辑器、真实Item、主题编辑、减少动效或手动连点队列；后续主线不变。

## 2. 草稿行为与版本规则

| 操作／输入 | 实际行为 |
| --- | --- |
| 打开／重开中心 | 从选中海报committed投影建立干净草稿；仅打开不保护或改变海报姿态 |
| 输入、粘贴或切换开关 | 只修改窗口内草稿；空值、小数、负数、超界、指数和非数字保留原文并提示，不截断/四舍五入为合法秒数 |
| 应用 | 固定草稿最初目标及实体版本；提交前重读控件校验；pending期间禁用应用/取消/编辑，防重复发送 |
| 同步发布早于回执 | 保留pending草稿，回执按requestId完成；不能被同步投影提前清掉 |
| 无关海报更新／重复会话投影 | 不改原目标的读集，不丢文本或移动光标 |
| 当前实体已变 | 保留输入、显示“草稿已过期”；旧basis应用触发Conflict，不覆盖最新值，不静默重基／自动重试 |
| 取消 | 从当前最新committed值重建草稿，无数据库写入；失败输入不残留 |
| 切换／删除目标 | 丢弃原窗口未应用草稿；命令已固定的A不能改发B，晚到A回执不清空B草稿；无目标禁用控件 |
| 关闭中心 | 不保存草稿，释放保护和事件订阅；重开仍显示已提交值 |
| 文本03且原值3 | 允许规范化；命令NoChange后清理pending，不增加revision |

`ControlCenterAutoPostureEditor`位于Application层，包装现有`PosterAutoPostureDraft`。latest用于取消/重载，basis用于CAS，两者不可混为一份自动覆盖的表单状态。窗口负责显示，ControlCenterService转交现有命令结果，不直接读写数据库。

## 3. 编辑保护与文本事件

草稿dirty或pending期间，以现有共享Editing保护暂停自动空间操作，包括可能推动当前目标的邻居自动动作；仅打开中心不生效。应用完成、取消、切换或关闭解除后，现有调度按当前指针重新判定和重新计完整延迟；不补执行旧deadline。它不是永久更改海报编辑/锁定状态，也不阻挡手动命令；手动或外部改动仍会使草稿CAS过期。

原生夹具证实：设置TextBox.Text后同一回调中，异步TextChanged尚未刷新草稿。仅增加夹具等待不能排除后台投影先到导致丢字，因此生产采用两阶段：

1. TextChanging同步记录原文到纯模型，不改视觉树、不发命令。
2. TextChanged更新校验/按钮/保护通知；Apply再次读取实际控件值。
3. 程序投影渲染有重入保护；文本相同不重设Text，避免频繁投影移动光标。

该分工与[Microsoft TextChanging文档](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.controls.textbox.textchanging?view=windows-app-sdk-1.8)一致：同步事件可发生在布局期间，不在其中增删视觉树或打开弹窗。新增原生断言检查输入后立即插入一次投影仍保留1.5、禁止提交；不是依赖等待来掩盖生产竞态。

## 4. 验证证据与产物

- 新27/27；全量736/736，0失败0跳过。涵盖合法/非法原文、取消无写入、冻结读集、连续投影、过期冲突、目标切换、删除/零目录、晚到回执、重入发布、NoChange及重开。
- 架构测试将新增UI partial纳入“只能走命令/投影，不直连仓储”检查。
- 最终WinUI Release构建0警告0错误。实际PS5.1脚本无PS7.4或管理员前置要求；进程仍继承当前宿主环境，不宣称已完成普通权限认证。
- 最终包`out/verify/v3-04-m08-05/app`；App DLL SHA256：`6CDF89B44760BD24DC8F5834442A81AA421501B195CE0A9F553293ECDB3F8E3E`。
- Application：`60A3F9A82408AC2D7763059372ADAED8BBDC1B7DD697AED4A1013870C873612D`；Windows：`0B5A5879CE5133708D497B1E996C21A6A1CCE30B19BE66ECA277C76DB34A5876`。

| 隔离证据（均在out/verify/v3-04-m08-05） | 结果 |
| --- | --- |
| native-settings-release，PID43568 | PASS：真实ToggleSwitch/TextBox和Button Invoke→Click→生产命令；A/B独立提交、非法输入、取消、冲突、关闭重开、共享保护；revision6，exit0/clean0 |
| native-empty-release，PID49852 | PASS：0张目录仍可开中心，设置禁用／不保护／不写revision；revision2，exit0/clean0 |
| native-auto-final，PID26188（空目录标题修正前） | PASS：原生观察驱动4次请求，3提交＋1动画中策略取消；exit0/clean0；不是最终App哈希的证据 |
| native-auto-release/recheck，PID7424/36092（最终包） | 未通过：均Unavailable、0自动请求，超时后正常退出/资源0；保留原日志，不用上一行覆盖失败 |

最终设置区及零目录的RenderTargetBitmap由真实XAML产生并已检查；这只验证控制中心布局，不是鼠标悬停、桌面层级或动画无白条证明。
截图：[设置区](../../../../out/verify/v3-04-m08-05/native-settings-release/control-center-settings.png)、[零目录](../../../../out/verify/v3-04-m08-05/native-empty-release/control-center-empty.png)。

### 最终自动回归的环境前置门

两轮Unavailable后，只读检查得到OpenInputDesktop=false/GetLastError=5、GetPhysicalCursorPos=false、GetForegroundWindow=0；线程桌面Default，实际输入桌面无法读取。与观察器的fail-closed条件一致；**不能仅凭这些返回值断言具体是锁屏、UAC还是会话变化**。已请用户返回已解锁普通桌面后复测；未切换/附着输入桌面、修改安全权限或合成指针绕过。

这项仍为OPEN，先补最终包在可读输入桌面上的自动通道复测，再继续M08-06/07；不冒称最终包全部运行门通过。[只读记录](../../../../out/verify/v3-04-m08-05/native-input-unavailable.json)。

早期native-settings-01因夹具过早断言失败，native-settings-02验证异步时序后通过；后续生产同步原文补强与最后空目录标题修正均另建隔离记录，未删除失败证据。

## 5. 重跑命令

在仓库根目录的PowerShell执行（fixture目录必须是新的，已有实例先从托盘正常退出）：

```powershell
Set-Location 'G:\A_\Tools\DesktopUp_V2'
dotnet test tests/DesktopUp.Contracts.Tests/DesktopUp.Contracts.Tests.csproj -c Release -p:Platform=x64
dotnet build src/DesktopUp.App/DesktopUp.App.csproj -c Release -p:Platform=x64 -o 'G:\A_\Tools\DesktopUp_V2\out\verify\v3-04-m08-05\app\'
dotnet run --project tests/DesktopUp.SessionFixture/DesktopUp.SessionFixture.csproj -c Release -p:Platform=x64 -- out/verify/v3-04-m08-05/retest-settings 3 --measure-expanded
& scripts/smoke-auto-settings.ps1 -ExecutablePath out/verify/v3-04-m08-05/app/DesktopUp.exe -DataRoot out/verify/v3-04-m08-05/retest-settings
```

自动回归用另一份新的3张展开fixture运行`scripts/smoke-auto-posture.ps1`。两个脚本都限制隔离数据；不同时启动，不覆盖M07稳定包或用户日常库。M07的24项稳定指纹保持，当前六卡仍DEMO。未commit/push；已有M07/M08未提交工作保留。

下一项[M08-06](../../30_Plans/V3_04_M08_AUTO_POSTURE_PLAN.md#4-逐项实施卡)：生命周期/显隐/删除/零目录/0-1-3-6启用退出；再M08-07真实鼠标和增量资源门。安全主线与原04-04之后真实内容闭环不变。
