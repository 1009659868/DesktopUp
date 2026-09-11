# M08-06 生命周期与隔离回归

日期：2026-09-11。适用：`impl/poster-redesign-V3`，实际仓库 `G:\A_\Tools\DesktopUp_V2`。
状态：**IN PROGRESS：实现、合同和已执行隔离回归通过；真实恢复计时及受影响视觉/层级门 OPEN**。当前任务状态归[细化工作流](../../30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md)。M08-07 [R1§5](M08_ANIMATION_INPUT_R1.md)已在当前包补验原生自动smoke（3提交＋1策略变更恢复），输入环境现已可读；不扩称真实锁屏/解锁后的新计时已验，不用局部PASS关闭06。

## 1. 本次发现与接入

此前纯模型具备“不可用后重新计时”，App 也处理 Shell/显示恢复，但共享隐藏窗口未接系统休眠、会话锁定通知。因此不能保证跨休眠的首个轮询不会沿用旧期限。这是本项补齐的生命周期缺口，不是新的空间/动画策略。

- Windows 的共享 `DesktopEnvironmentWatcher` 注册当前会话 WTS 通知，处理 `WM_POWERBROADCAST` / `WM_WTSSESSION_CHANGE`；海报窗口不重复注册。注册失败保留错误码，用既有健康 tick 重试，不新建计时器。Dispose/WM_NCDESTROY 配对注销。
- Application 的 `SystemLifecycleGate` 分别记录休眠、锁定、断连原因。重复暂停无副作用；只解除某个原因不能恢复其他暂停。未见暂停的恢复通知也要求重新建立基线，由既有 debounce 合并。
- App 的 `App.SystemLifecycle.cs`：暂停时使 recovery version、auto environment 失效，取消计时、输入观察、删除确认与空间工作，保留 committed 数据；解除所有原因后走原共享 Shell/显示恢复。
- 锁定期间的显示通知、健康检查和旧异步恢复不能越过暂停状态。恢复后重新采集当前会话/策略/指针，在外重新计完整延迟，不补播暂停期间动作，不将恢复当作真实进入。
- 隐藏/删除沿用 PosterSessionRegistry 的取消与释放；重显使用新 epoch；旧请求完成回执不能清空新会话事务。未改 schema7、空间求解、输入岛几何、R4 布局租约、动画配置或事务回滚。

## 2. DeskBox 参考与区别

复读来源：DeskBox `64f27e1aa0f16867abe20074f35ea05cd7b12ce7`：

- `src/DeskBox/Services/AppLifecycleRecoveryWatcher.cs`
- `src/DeskBox/Services/AppLifecycleRecoverySignalClassifier.cs`

吸收共享监听、WTS 注册释放、恢复去抖及重新挂接；本项目仍组合自己的多海报恢复、自动调度和精确取消协议，不迁入另一套宿主。参考源码为 GPL-3.0-only，来源与版本在此登记；本次按接口/机制独立接线，无逐段源码搬入。

参考代码的远程连接/断开数值为 9/10，不能直接照抄：Windows 文档定义远程连接/断开为 **3/4**，9 是 remote-control change，10 保留。本次按官方定义过滤当前 session 并补测试。[WM_WTSSESSION_CHANGE](https://learn.microsoft.com/en-us/windows/win32/termserv/wm-wtssession-change)

注册限当前会话，失败可能是 Terminal Services 尚未准备好；与注销成对，按已有健康循环重试。[WTSRegisterSessionNotification](https://learn.microsoft.com/en-us/windows/win32/api/wtsapi32/nf-wtsapi32-wtsregistersessionnotification)
电源通知经共享顶层窗口接收，已处理消息返回 TRUE。[WM_POWERBROADCAST](https://learn.microsoft.com/en-us/windows/win32/power/wm-powerbroadcast)

## 3. 纯合同与进程结果

新增 **32/32**，全量 **768/768**，Release **0 warning / 0 error**。初次聚焦测试发现“仍有锁定原因时解除休眠误返回 Suspend”，修正 gate 的新增原因判定后全量通过；原失败 TRX 保留，不改弱断言。

新增覆盖：13 项原生消息映射、其他会话/无关消息过滤、嵌套暂停、缺失暂停的恢复、非法信号不变更状态；0/1/3/6 张各 40 轮显隐/新 epoch；出队前隐藏/删除/暂停；完整新延迟；旧 incarnation 回执与退出后迟到完成。

全部进程使用 `out/verify/v3-04-m08-06` 下独立配置，逐个运行，不覆盖旧包或日常配置。

| 配置/入口 | PID | 结果与范围 |
| --- | --- | --- |
| lifetime-3 / exercise | 42316 | 3→暂停→恢复→隐藏单张/全部→重显→删除单张/全部→中心可用；PASS，退出 clean/0 |
| lifetime-3 / restart | 18500 | 同库 0 张，不复活默认；revision/完整定义一致 |
| enabled-0 / exit → restart | 25952 → 30840 | 0 张、无专用自动 timer/观察；PASS |
| enabled-1 / exit → restart | 38236 → 7464 | 1 张启用，定义/revision 不变；PASS |
| enabled-3 / exit → restart | 51140 → 45880 | 3 张启用，定义/revision 不变；PASS |
| enabled-6 / exit → restart | 42104 → 37416 | 6 张启用，定义/revision 不变；PASS |
| settings-3 / settings-0 | 50104 / 50516 | 最终包实际 XAML 开关、秒数、应用/取消/冲突与零目录回归 PASS |
| spatial / Full | 7612 | 4 次姿态操作，168 原生提交帧，完整展开/收起及布局恢复读回 PASS |

上述 13 次通过进程均 exit0、clean、资源0。启用时最多一个专用自动 timer；全部隐藏/零目录释放 timer 和指针订阅。重显 epoch 前进；暂停到恢复 revision 保持6，Shell generation 1→2；后续隐藏/删除的正常命令才增加 revision。

**证据边界：** lifecycle fixture 只向本应用隐藏 HWND 发本地测试通知，经过生产分类、暂停和恢复；没有真的锁屏、休眠、切换会话或重启 Explorer。`ObservedOutside=false`，本轮真实进程不能证明“有效在外旧期限的取消/重启”，这部分当前只有纯时钟测试。XAML/Win32 边界读回不等于 DWM 动画画面认证；未检查的白条/层级门不写 PASS。

## 4. 原生环境阻塞，必须保留

- M05 最终包重检 PID46516（`m05-auto-recheck`）仍失败：Unavailable、0 自动请求。
- M06 最终包 PID45892（`native-auto-final`）也停于步骤1：0请求/0提交，超时后正常清理，resource-ledger clean/0。
- 2026-09-11 07:42:57 UTC 只读探针：`OpenInputDesktop=false/error5`、`GetPhysicalCursorPos=false/error5`、线程桌面 Default、输入桌面名称不可读、foreground0。记录 `native-input-unavailable.json`。这定位到输入采集前置不可用，**不能仅凭此认定具体是锁屏、UAC还是会话隔离**。
- 没有合成鼠标、切换输入桌面、调整 UAC/权限或放宽 Unknown 保护来凑通过。用户恢复普通已解锁桌面后，先复测本项最终包，再补动画过程/终态及启动/点击/刷新/窗口覆盖的受影响层级门。
- M08-07 的完整鼠标体验与 1/3/6 增量 CPU/内存门仍未开始；不以本项退出资源0冒充性能预算达标。

## 5. 可复现入口与产物

最终包：`out/verify/v3-04-m08-06/app`。SHA256：

| 文件 | SHA256 |
| --- | --- |
| DesktopUp.dll | 4116ED1287B75B7945369B5256E55C925146404E6AA88EF8F8387A118D3DB3C6 |
| DesktopUp.Application.dll | D63B912641EBF84BF276B7DD750749E5870A4253AC2570612E9513225EF22E6C |
| DesktopUp.Windows.dll | BE9B497A373A3E84329C9367DF412308C056396568705846E9A109C9275CE077 |

在仓库根使用 Windows PowerShell 5.1 或更新版；无需管理员/PS7.4前置。已有 DesktopUp 必须先托盘正常退出。下例目录必须尚未存在，不复用已启用/删除后的 fixture：

```powershell
dotnet run --project tests/DesktopUp.SessionFixture/DesktopUp.SessionFixture.csproj -c Release -p:Platform=x64 -- out/verify/v3-04-m08-06/lifetime-next 3 --measure-expanded
& scripts/smoke-auto-lifetime.ps1 -ExecutablePath out/verify/v3-04-m08-06/app/DesktopUp.exe -DataRoot out/verify/v3-04-m08-06/lifetime-next
& scripts/smoke-auto-lifetime.ps1 -ExecutablePath out/verify/v3-04-m08-06/app/DesktopUp.exe -DataRoot out/verify/v3-04-m08-06/lifetime-next -Mode restart
```

`enabled-exit` 模式用于新建0/1/3/6配置启用后退出；随后 `restart` 逐字段校验最后成功快照。0张 fixture 不加 `--measure-expanded`。该脚本是生命周期隔离夹具，不是日常启动器；exercise 会删除测试海报。

输入桌面恢复后，使用另一个全新3张展开 fixture，运行 `scripts/smoke-auto-posture.ps1`，预期3自动提交＋1策略变更取消。不能对已启用的 enabled-* 直接运行它，也不能用旧版本PASS替代最终包。

M07固定绑定24/24只读校验通过，旧App SHA256仍 C699CAB63D50E224B697E2BD4F8918ED629BFB4BC7E40CBD7375113BB403BDAF。本次未commit/push，未修改用户文件、日常数据库或M07验证包。
