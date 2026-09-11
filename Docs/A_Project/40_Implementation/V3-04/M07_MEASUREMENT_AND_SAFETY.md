# V3-04-M07：测量合同与前置安全门

日期：2026-09-10（M07收口证据）。状态：M07-01..04 PASS；自动收展转M08，后续实施状态查[当前任务索引](../../30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md)。前置：M05/M06自动与真实进程门已通过。

[M07收口](M07_CLOSEOUT.md)已完成24项稳定绑定、508项合同、36/36资源、1/3/6空间和7项自动安全回归。S1～S5人工安全与相应日志/同库重启证据已齐；菜单、连续收展和白条确认保留。用户已决定F03/F06按需登记、F07由[M08](../../30_Plans/V3_04_M08_AUTO_POSTURE_PLAN.md)先补必要闭环，M07-04交接完成；不重复安全测试。[资源报告](M07_RESOURCE_BASELINE_20260910.md)保留预算差距；§4旧版本证据不混入本轮基线。

入口兼容Windows PowerShell5.1及PowerShell7，不因管理员权限拒绝启动，不改UAC；权限仅记入证据，不冒充普通用户认证。资源和人工矩阵均已完成，收口记录§5已登记用户的缺项处置；未重开动画实现范围，未把三个缺项记为已实现。

## 1. 范围与固定口径

本轮建立基线，不实施大优化、动画打磨、文件Item或完整控制中心；不重写宿主、输入、空间求解或SQLite权威。六张是压力探测，不是容量承诺。所有数据在新的隔离根，六张卡片仍为DEMO ITEMS。

- 同一x64 Release、相同二进制SHA256、相同机器/主屏/DPI及实际运行权限；记录OS、CPU、逻辑处理器数、物理内存、PowerShell版本和运行时依赖。不同权限/版本的证据分别归档；`Elevated`仅用于描述环境，不作为本轮启动或推进门槛。
- 1/3/6张 × 展开/收起/隐藏 × 控制中心关闭/打开，共18格；每格独立进程、独立配置、两次重复。不同用途参数按A/B/C循环，但同序号几何/参数在各格一致，全部不锁定，不自动收起。
- 每次启动后第8秒至第18秒取固定稳态窗口，外部进程每500ms采样，最少19个样本；全部所需窗口必须在第8秒前就绪。不是清空OS文件缓存后的冷启动，不把首次运行与第二次缓存差异抹平。
- 原始样本保存时间、累计CPU时间、WorkingSet64、PrivateMemorySize64、句柄、线程；CPU使用实际单调时钟差值，报告单核等效百分比及除以逻辑处理器数的整机占比，不混用二者。稳态CPU秒数另归一到10秒。
- 工作集为当前驻留物理页，私有提交不是私有工作集；使用MiB=2^20字节。报告中位数/P95/最大值，P95采用nearest-rank。缺数据标不可用，不填0。
- 启动指标为进程开始到最后所需poster_session.ready/控制中心created/环境恢复提交的日志时间；只代表宿主会话就绪，不代表首次屏幕呈现。
- 独立空间探针复用真实SetCollapsed动画路径记录实际成功批次提交时刻/耗时与命令调用到回调耗时，和纯计划帧数分开。它不是DWM/GPU实际呈现FPS或物理鼠标到光子延迟；GPU、实际呈现/输入到光子指标未测必须明示。
- 正常自动退出，检查exit0、当前PID无error/critical、资源台账clean/0；采样不强杀、不压缩工作集、不改进程优先级、不关其他应用。运行中用户操作或环境变化污染样本时留证重跑新目录。

需求画像§22的单海报静态10秒CPU≤0.10s、工作集≤50MiB仍保留。超出标为预算差距，不据本阶段通过自行接受新预算；M07是测量/安全门，不是V3-10性能认证。当前桌面同时运行其他应用，短窗口与少量重复不构成长时稳定性结论。

## 2. 子项

| 子项 | 出口 | 状态 |
| --- | --- | --- |
| M07-01 测量合同、数据、采样与统计夹具 | 隔离1/3/6生成、统计边界、固定产物及原始样本 | AUTO PASS：508/508；11脚本AST/24指纹、5.1实际启动和7只读兼容；生产App未重建 |
| M07-02 18格资源基线及空间提交节奏 | 两次重复，异常不静默过滤；预算差距单列 | AUTO PASS：36/36；1/3/6空间60/180/360帧及恢复通过；内存超50MiB，不等于性能达标 |
| M07-03 集中人工安全门 | 当前版本，菜单/确认/恢复/视觉逐项确认 | PASS：7项自动安全及S1～S5人工；同库零目录重启PID49016，0海报/1中心/1托盘、revision19保持 |
| M07-04 交接 | 证据/缺口清楚，用户处置明确 | PASS：报告/证据齐；F03/F06按需无排期、F07转入M08（交接时PLANNED）；后续状态见任务索引；先完成必要自动收展，再原04-04 |

## 3. 人工合并矩阵

不重复旧G1～G4全套；保留既有确认，仅补当前尚缺项。真实鼠标与视觉结果不能由原生读回代替。

| 编号 | 来源 | 操作及标准 | 当前结果 |
| --- | --- | --- | --- |
| S1 当前视觉/目标 | M05/G1～G4 | 各自参数；缩窄滚动；无白边/底栏遮挡/命中偏移；菜单只作用本实例，中心唯一 | USER PASS基础项；菜单故障、收展体验和R4白条另有专项确认 |
| S2 桌面生命周期 | G5/M03 | Win+D、普通/全屏覆盖；安全重启Explorer后全部恢复，无重复菜单/托盘/中心 | USER PASS：2026-09-09 |
| S3 推动/归位安全 | G6/G6-R1/G7/R2 | 未调整邻居归位、用户调整后不强拉回、锁定不推动；空间不足不半提交、按钮反馈 | USER PASS：PID37064链式三项及PID12396空间不足三项均获明确确认 |
| S4 确认失效 | G8 | 目标/后果核对后取消；确认中关闭中心；另次确认时托盘退出再启动目标保留 | USER/LOG PASS：PID34216确认中退出clean/0；同库PID42800保持A/C两张、revision16，B不复活 |
| S5 删除/零目录/退出 | G9/G10 | 同隔离库删除全部海报；末张确认分支；重启仍零张，中心/托盘可用，退出无残留 | USER/LOG PASS：B先前删除，PID42800删除A及最后C/clean0；PID49016同库0张/revision19、1中心/托盘、无错误 |

S3链式反馈为“验证通过与预期一致”，blocked随后明确“确认,验证通过,符合1～3”，两组分别登记；不扩大到S4/S5安全格。本轮不自动重启Explorer或改动桌面文件；无法取得人工证据时保留MANUAL PENDING并提供最短清单。旧菜单已按[修复记录](M07_MENU_REPAIR_R1.md)获用户确认，不重复要求复现旧失败。

## 4. 历史证据与限制（旧包，不并入本轮36次）

- 合同回归：`out/verify/v3-04-m07/results/m07-contract.trx`，411/411、0skip（新增6项：统计分位数/CPU单位、1/3/6配置同源及非法输入）；SHA256 `82E4E52CB956E825275FC5713C922D86B9E8D380E6D29ECC453BF9AD4CA819C1`。
- Release：`out/verify/v3-04-m07/app/`，0warning/0error；6个产物固定指纹为[scripts/m07-build.json](../../../../scripts/m07-build.json)。App DLL `63A319F6DEB9C8B167818CC41E6D7D59A39BC7E4042992D1038C327DFE351DA2`。本轮生产路径仅增加默认关闭的探针计时开关，不改变帧准备/提交次序、Host、输入、求解和状态事务。
- 历史采样：初次发现`Elevated=true`后停止调度。前4格有application.stopped和clean/0；第5格`n1-collapsed-center0-r1`随调度Ctrl+C中断，没有stopped/measurement.json，旧台账clean=false/7，属于无效中断样本，不能写干净退出PASS。普通权限工具调用曾失败`CreateProcessWithLogonW 1385`；未改写台账、未改系统权限。原管理员硬门现已按用户决定取消，`AllowElevatedReference`只保留参数兼容，不再控制启动。
- 机器：Windows 11 Pro build26100，i7-14700（20核/28线程），物理内存约32426MiB；主屏2560×1440、工作区2560×1392、96DPI，另有副屏。本轮所有海报在主屏，不认证多屏。采集器PowerShell7.6.5/.NET10.0.11；项目锁定WindowsAppSDK2.4.0/WinUIEx2.9.3等，详见Directory.Packages.props。未单独测GPU利用率/显存/屏幕呈现FPS。
- 管理员参考原始数据保留`out/verify/v3-04-m07/measurements-r1/{environment.json,summary.json,各格/measurement.json}`；该目录只有4格样本，旧`Valid=true`仅表示样本/退出完整性，**不是完整矩阵或普通用户基线PASS**。CPU零值是计数器分辨率下无可见增量，不是绝对无CPU活动。
- 5.1启动兼容回归：`out/verify/v3-04-m07/launcher-20260909-121424-23a691/result.json`，PowerShell5.1.26100.8875/.NET Framework4.0.30319.42000、Elevated=true，PID47912：3个poster_session.ready、1中心、1托盘、exit0、无error/critical、台账clean/0。7个脚本AST、4套人工库及固定6产物检查通过；父进程环境变量不变。仅启动/退出自动检查，S1～S5人工未代填PASS。此前PID46428运行正常，但测试器未提前保留进程句柄造成退出码null，该失败证据保留，修正测试器后重跑。
- 共享统计代码改用.NET Framework可用的NaN/Infinity判断，统计口径未变；完整合同复跑`out/verify/v3-04-m07/results/m07-ps51.trx`，411/411、0skip。未重建或更换M07固定App产物。
- PowerShell7.6.5同入口回归亦通过：`out/verify/v3-04-m07/launcher-20260909-122024-2c720c/result.json`，PID41616，3会话、1中心/托盘、exit0、clean/0。人工库日志会追加，回归记录中的LogHash是采集当时的哈希，后续以ProcessId筛选当次事件，不能要求可变日志永远等于旧哈希。
- 5.1资源兼容采样：`out/verify/v3-04-m07/ps51-launcher-samples-20260909-121448/`，单海报展开/收起/隐藏×中心关/开共6格，一次重复，全部Valid=true、正常退出。工作集中位范围138.85～165.27MiB，CPU归一最高0.13535秒/10秒；存在预算差距，且本轮不用于优化或完整矩阵认证。
- 5.1空间时序：`out/verify/v3-04-m07/spatial-current-env-20260909-121733-7529e4/n*/timing.json`；1/3/6张PID42984/37208/43192，成功命令2/6/12、实际提交20/60/120帧，均等于计划帧数，exit0、资源0。这是原生提交计时，不是人工视觉或屏幕呈现认证。所有回归结束后DesktopUp进程为0，可直接开始人工验证。
- 六张expanded/collapsed/hidden的CLI生成分别实际成功，均revision6、6个独立ID，数据位于`out/verify/v3-04-m07/fixture-six-*`；仅证明真实SQLite配置生成，不等同六张窗口/性能通过。

| 管理员探索格 | PID | 工作集中位MiB | 私有提交中位MiB | CPU秒/10秒（归一） |
| --- | --- | --- | --- | --- |
| 1张展开/中心关/r1 | 48012 | 160.13 | 100.35 | 0.0000 |
| 1张展开/中心关/r2 | 47044 | 159.68 | 99.92 | 0.0161 |
| 1张展开/中心开/r1 | 13772 | 167.42 | 103.79 | 0.0160 |
| 1张展开/中心开/r2 | 47996 | 166.62 | 104.12 | 0.0482 |

上述样本已提示工作集显著超过50MiB目标，不能用“CPU低”或“管理员运行”直接认定其他权限会达标。完整差距仍须以同环境矩阵确认；没有擅自调大预算或开始优化。

## 5. 复验入口（本轮S1～S5及资源矩阵已完成）

直接使用Windows PowerShell5.1，无需改权限或切换7。当前入口复核稳定包24项指纹，不静默回退；具体版本和数据合同见[M07收口](M07_CLOSEOUT.md)。切场景前须托盘正常退出，以下命令逐个执行，人工不要并行采样。

```powershell
# S3链式三项用户已通过，仅需复查时运行。
& 'G:\A_\Tools\DesktopUp_V2\scripts\start-m07-manual.ps1' -Scenario chain
# S3空间不足亦已人工通过，仅需复查时运行。
& 'G:\A_\Tools\DesktopUp_V2\scripts\start-m07-manual.ps1' -Scenario blocked
# S4/S5：先取消/关闭/带确认退出，再真实删除至零并重启。
& 'G:\A_\Tools\DesktopUp_V2\scripts\start-m07-manual.ps1' -Scenario removal
```

人工目录`out/manual/v3-04-m07-closeout/<scenario>`首次创建、后续复用；CheckOnly只读不创建。不覆盖旧M07数据；删除至零必须仍用同一removal库。interaction/return-sync保留复查入口，不要求重复已经认可的S1/S2和动画。

自动测量与安全回归已完成；以下仅用于后续复验，不要求用户再跑36次：

```powershell
& 'G:\A_\Tools\DesktopUp_V2\scripts\test-m07-launcher.ps1' -CheckOnly
& 'G:\A_\Tools\DesktopUp_V2\scripts\start-m07-measurement.ps1'
& 'G:\A_\Tools\DesktopUp_V2\scripts\measure-m07-spatial.ps1'
& 'G:\A_\Tools\DesktopUp_V2\scripts\test-m07-safety.ps1'
```

自动入口也须逐个运行且无其他DesktopUp。资源约12分钟、空间约1分钟；结果位于`out/verify/v3-04-m07-closeout/{resource,spatial,safety,launcher}-时间-后缀/`，各次唯一不覆盖。资源逐格新配置，冻结本次动画参数并复核程序/夹具/显示环境，失败保留；根目录`stop-after-current`文件可让当前格正常退出后停止，不强杀。

采样期间不操作测试海报/中心，不切显示设置或重启Explorer；不把提交计时当GPU呈现FPS。上述命令供后续复验，不再要求用户重做本轮已通过项。M07-04已按用户决定收口；新增M08承接明确的必要功能，并非改任务号掩盖缺口，预算及未认证范围均不变。
