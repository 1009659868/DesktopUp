# 卸载中断排查与修订（2026-10-21）

## 后续发布复验补记（海报优化同步）

既有诊断已验并集成；本轮未改卸载代码。20261021.3的main-scale-smoke01普通卸载曾在app/DesktopUp.dll停止，marker记录24、566项声明文件保留；稍后只读检查未见占用、允许删除共享，不能确定失败当时首因。现场保存后显式重试原卸载器0，独立smoke02普通首轮0。最终测试安装和登记已清理、日常状态未变，详细证据见[本次发布记录](PREVIEW_INSTALLER_DELIVERY.md#2026-10-21-海报优化集成与发布同步)。这是新增失败证据，不是根治结论；历史首因及P1/R6锁冲突分别继续保留。以下为此前排查批历史。

## 范围与准入

分支 `impl/09-uninstall-interruption` 从已核对远端一致的 `impl/develop@e1cb600` 派生。先确认失败链，再修订；不提交、推送或合并，不推进真实热度。已验业务和架构保持，P1/R6锁冲突与规模成本独立携带。

旧证据：20261020.1最终包smoke02删除730/806项后中断，首个剩余为 `app/wuceffectsi.dll`；内容保留准备完成、App退出资源为0。首次实际卸载子进程退出码及Win32失败码未记录。同一卸载器重试和smoke03成功仅证明可继续，不能证明首因或根治。原始包/日志/配置保留。

## 连续实施

1. 用同一包、独立安装/配置复现，捕获真实卸载执行进程退出码及首个失败文件；必要时仅在隔离诊断包记录即时Win32错误及占用信息，产品代码先不改。
2. 区分内容准备失败、退出未完成、程序文件删除失败与测试等待误判；确认后只修对应责任层，不通过强删、终止无关进程、重启删除或跳过失败文件绕过保护。
3. 只验证修订行为、失败保留/再次执行与一次真实包安装启动卸载。配置、收纳内容、未知文件和其他版本保持。清理本轮登记和测试安装，不在桌面留样例；证据保存在out。

## 退出标准

- 有事实支持的原因与修订范围；不能重现的历史首因继续明确标未定，不以故障注入替代历史归因。
- 实际执行返回码、清理结果与数据保留一致；失败明确停止并可安全再执行。
- 集中人工仅保留本轮相关体验（预计正常卸载、可解释的失败恢复两组，自动覆盖足够则不重复）；不重开既有业务验收。

## 进度

**诊断补强 CODE / AUTO / USER PASS，本批诊断交付收口；历史首因仍 OPEN，未做推测性根因修复。** 后续已按用户授权完成Git集成：c8a6714 → develop940ff96 → main4e79035，均已推送，最终回develop；20261021.2最终发布包定向验证通过，详见[发布记录](PREVIEW_INSTALLER_DELIVERY.md#2026-10-21-已验卸载诊断集成与发布同步)。下方未提交/未合并是验收当时的历史状态。

### 已核实与仍未知

- 同一旧最终包四次隔离复现全部通过，真实清理执行码均为0、无清单文件剩余。cause01～03使用哈希一致的卸载器副本及NSIS的 `_?=` 参数直接等待执行器；cause04按原注册卸载入口启动，捕获实际自复制子进程 PID57192（父18180）及退出码0。不能把直接执行器与自复制观测混为一类。
- 原测试只等待注册/文件消失，没有捕获实际子进程退出码，属于已确认的观测缺口。原时段Application事件无相关DesktopUp/wuceffectsi记录，Defender相同时段无事件；这些阴性结果不能排除占用/权限等原因。
- 本轮没有证据能恢复2026-10-20首次失败的即时Win32错误或占用者。因此没有添加重试、等待时长、进程终止、重启删除或放宽保护；历史中断不能标“已修复”。P1/R6锁冲突不与此合并。

### 最小改动

- `installer.nsi` 用 `DeleteInstalledFile` 宏记住精确载荷相对路径，仍执行原NSIS `Delete` 和失败即停。失败时弹出该文件路径，并尽力写入原安装标识 `app/preview-install.ini` 的 `LastCleanupFailure`（File/ExitCode）。记录失败也继续返回24，不再执行后续删除。
- 明确24是卸载器退出码，**不是Win32错误码**；不在NSIS Delete返回后调用GetLastError并冒充原错误。诊断写入现有安装标识，不创建恢复任务，不上传路径，不写收纳内容。
- `package-preview.ps1` 继续按原清单、原顺序清理，无通配/递归扩展。`Test-UninstallFailure.ps1` 在out中用准确副本及 `_?=` 捕获实际执行码，不再以启动器返回码判断清理成功；这是新定向验证入口，不声称旧测试库已全部重写。
- NSIS参数依据：[官方命令行说明](https://nsis.sourceforge.io/Docs/Chapter3.html)。参数只用于隔离测试，不修改产品注册的卸载命令。

### 产物与限定验证

- 候选 `out/releases/uninstall-diagnostics01/DesktopUp-Preview-20261021.1-win-x64-Setup.exe`，67,369,553字节，SHA256 `21206794ADBEF5F40BCFA46B409816A6D83D41126DB457A86F4C341BBC88C41D`。源码基点e1cb600＋本轮未提交修改（IncludesUncommittedChanges=true），不是已发布干净提交。
- 正常Preview通道，完整自包含构建/打包0警告0错误；文本门965文件/25个冻结迁移通过。未修改App、Application、存储、Shell、数据库或归还业务。
- Windows PowerShell5.1执行 `Test-UninstallFailure.ps1`，`out/releases/uninstall-diagnostics-fault02/results.json` 三个事实通过：受控禁止删除共享返回24并记首个失败文件/停止后续删除；释放自建句柄后显式第二次执行返回0；同包重装后的普通首轮卸载返回0。配置数据库与未知文件SHA保持，登记和程序已清理。
- fault01首先因PS5.1 ConvertFrom-Json数组包装导致**测试脚本**读取清单失败（当时产品的故障路径/退出码记录已正确）；已修订脚本并经fault02验证，fault01也用同一原卸载器完成收尾、保护摘要保持。旧失败日志与 `harness-cleanup.json` 均保留，不覆盖历史。
- 原始复现证据：`out/releases/uninstall-cause01`～`04`及同名日志；系统事件只读结果见本节。助手端诊断包及安装测试为本机高完整性；下述GUI人工现已通过，但用户执行环境未记录完整性级别，不将其补记为普通权限专项认证，也不补记真实登录/跨机/历史业务结果。
- 最终只读审计 `out/releases/uninstall-diagnostics-final-audit.json`：日常Preview/V3文件摘要、Run登记及DesktopUp桌面快捷方式均与cause04前快照相同；20261020.1/20261021.1测试安装登记均不存在，DesktopUp进程0。新测试的PS5.1解析、文本门和diff-check通过；保留的样例/程序副本均仅在out作证据，不遗留桌面文件。

## 集中人工：一次提示核对

**已于2026-10-21完成，以下命令保留为验收依据，无需再次运行。**

先正常退出其他DesktopUp，再在PowerShell执行：

~~~powershell
powershell.exe -NoProfile -File "G:\A_\Tools\DesktopUp_V2\tests\Packaging\Test-UninstallFailure.ps1" -PackageDirectory "G:\A_\Tools\DesktopUp_V2\out\releases\uninstall-diagnostics01" -Name uninstall-diagnostics-review01 -ReviewFailure
~~~

脚本只创建本批隔离安装/配置，不建桌面快捷方式、不使用真实文件；在自建DLL上持有禁止删除共享的读取句柄。卸载向导请保留“保留在原收纳位置”，继续卸载，核对失败提示列出本批安装目录的 `app\wuceffectsi.dll`。关闭该提示后，脚本释放**自己的**句柄，再显式运行卸载器完成清理，并自动验证普通卸载和数据保留。

预期：提示可定位失败文件；脚本最终三个Passed均True。本轮仅验诊断及原安全行为，不据此宣布历史首因解决。若用户取消向导或脚本异常，不覆盖重装/强删；保留输出和现场再处理。此命令没有在助手端预先执行，不重复使用同一Name覆盖证据。

### 人工结果与收尾（2026-10-21）

用户提供的弹窗准确显示本批隔离安装的 `app\wuceffectsi.dll`，随后贴回三个Passed=True；本机 `out/releases/uninstall-diagnostics-review01/results.json` 与用户输出一致：

- 受控禁止删除共享：退出码24，FirstRemaining=app/wuceffectsi.dll，HistoricalCauseEstablished=False。
- 释放测试句柄后显式重试：退出码0，配置及未知文件保留检查通过。
- 不注入故障的普通卸载：退出码0。

只读收尾核对：20261021.1登记不存在，安装目录仅剩预期保留的 `unknown-user-file.txt`（摘要与样例来源一致），启动器/卸载器均不存在；隔离配置数据库仍在，DesktopUp进程0。测试程序和登记由脚本调用原卸载器清理，本轮登记验收没有额外删除文件；证据和保留样例仍仅在out。

本批**诊断补强验收通过并收口**，不重做已有回归。旧偶发中断的历史首因继续OPEN，不能依据本次故障注入推定原占用者；后续如自然复现，应保留失败路径、实际执行码和当时占用/权限证据，再决定定向修复。当前仍在 `impl/09-uninstall-interruption`，本轮未提交/推送/合并，不推进真实热度。
