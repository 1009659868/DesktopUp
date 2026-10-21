# 卸载中断即时取证

2026-10-21：用户确认本批集中人工三段均Passed=True，**USER PASS / CLOSED**。收口的是即时取证交付，历史自然卸载中断首因仍未定；清理及归档见文末。

2026-10-21：从远端一致的 impl/develop@45e2a3a 派生 impl/09-uninstall-immediate-evidence。用户授权连续推进到集中验收；不提交/推送/合并，不推进真实热度，P1/R6锁冲突独立保持OPEN。

## 目标与已有事实

历史中断为程序载荷删除阶段，曾涉及wuceffectsi.dll和DesktopUp.dll；内容准备成功，后续显式重试可完成。现有24是卸载器阶段退出码，不能解释原始Win32错误。事后无占用不能推定失败当时无占用，故不添加猜测性的等待/重试或强删。

已读现有安装器及NSIS删除语义：原指令在删除后仍会恢复属性、枚举/关闭句柄；之后调用GetLastError不能还原删除错误。官方System插件的?e可紧随被调用函数捕获错误。取证要在实际失败边界完成，而不是重试删除来“测原因”。

- NSIS Delete语义：https://nsis.sourceforge.io/Reference/Delete
- System调用错误捕获：https://nsis.sourceforge.io/Docs/System/System.html
- 官方实现参考（并非本地3.12二进制证明）：https://github.com/NSIS-Dev/nsis/blob/master/Source/exehead/util.c

## 连续实施与安全边界

1. 为清单中的单个确定载荷路径建立窄删除/诊断边界，保留不存在文件可继续、只读属性处理、失败停止、原退出码24及再次执行入口；即时保存失败API/错误、时间、执行PID、属性和随后只读的占用快照。不得伪造错误来源，也不把Restart Manager空列表当作无锁证明。
2. 已验安装目录/注册/归属/重解析点检查、维护租约、内容保留/归还流程均保持。不增加路径通配、递归删除、自动重试、杀进程或重启删除；取证失败也不能允许后续清理。诊断只落本机已验证位置，不上传路径或文件。
3. 用隔离实际包及无价值样例验证正常清理、受控拒绝删除和显式重试；补不存在/只读/报告不可写边界，复用原包测试。受控故障仅证明诊断正确，不能替代历史归因；不扩展为全文件系统故障矩阵。
4. 有限普通复现捕获自然失败。有证据才做定向根因修复；无法复现则交付已验证的即时取证能力，历史首因仍OPEN，不无限重试或扩大回归。

## 退出与集中验收

即时错误必须与实际失败调用一致，后续采样/写日志不覆盖首次错误；失败后配置/真实内容/未知文件不变，后续载荷不继续删除，登记/卸载入口保留。直接风险自动检查充分时，人工只核对一次失败提示和显式重试，不重新验海报/拖放。

本批即时取证候选定向自动和集中人工均通过；未宣称历史根因确认或修复。旧包、旧现场及日常配置保持；本批测试安装/登记已由原卸载器正常收尾，后续已确认样例及重复构建输出另按用户授权清理。

## 实现边界

- 原installer内DeleteInstalledFile宏已替换为单独的packaging/preview/uninstall-evidence.nsh，不保留并列旧路径。清单仍决定删除顺序和唯一目标；每个条目只调用一次DeleteFileW，System ?e立刻保留其错误，不通过第二次删除推测首因。
- 保持文件不存在可继续、目录替换不递归、只读属性处理/失败恢复及失败即停。属性查询出现非“不存在”的错误时保守停止并记录GetFileAttributesW，不把无法读取误记成功。这是窄边界替换，不宣称对NSIS所有历史边缘情况逐字节等价，也不是历史根因修复。
- 失败标志与原始错误值分离，极端的原生失败却未返回错误码也不能继续删除。记录失败附近UTC FILETIME、实际执行PID/TID、API/错误、原属性及属性准备/恢复错误。
- 仅失败后一次Restart Manager查询，最多读取16项PID/创建时间，无关闭/重启进程。采样在失败之后，可能错过瞬时占用或内核过滤器；空列表、错误5等都不能单独指认某软件。
- 报告在已通过重解析点检查的隔离profile/uninstall-reports内CREATE_NEW创建；不覆盖先前报告。记录失败仍退出24、保留登记和后续载荷；完整报告无法写入时不宣称已保存，提示/旧marker尽力保留原始错误。成功显式重试不会删除首份报告。
- 稳态没有新增线程、计时器、观察器或轮询；新诊断只在卸载失败路径运行。内容保留/归还、数据库、海报业务、维护租约、路径/归属保护没有修改。

## 已完成的定向检查

Windows PowerShell 5.1执行tests/Packaging/Test-UninstallEvidence.ps1 -Name uninstall-evidence-unit03，6个直接边界通过：正常、已消失、只读、占用、只读且占用、报告不可写；编译实际生产include，真实Win32失败为32、卸载退出为24，已知持有句柄的测试PID可识别，属性/后续文件/未知文件保留，显式重试0且首报告摘要不变。产物仅在out/releases/uninstall-evidence-unit03。

早期unit01因夹具误用SetCompressor off编译失败，修正为SetCompress off；未开始文件清理。unit02通过初版边界，最终以unit03为准。没有把失败轮覆盖为通过，也没有扩大人工回归。

## 实际安装包与收尾

候选out/releases/uninstall-immediate01/DesktopUp-Preview-20261021.4-win-x64-Setup.exe；源码基点45e2a3a加本分支未提交修改（preview-build明确IncludesUncommittedChanges=true，不冒充干净提交）。635项构建/载荷输入绑定一致，807个载荷文件；67,371,020字节，SHA256：64DC6E4A385E198D52D7B4C84F4CBD32E9A2476FC22830B5D6CF804EA0867C82。Release自包含构建与NSIS打包0警告0错误，普通Preview通道，ReviewTools=false，不改impl/main。

- 只复验原InstallerUninstallRemainsManifestBoundAndNeverRecursivelyDeletesStorage安全用例，扩大源码检查到新include：1/1通过。结果out/releases/uninstall-immediate-audit/contracts/*.trx。没有运行全量合同。
- tests/Packaging/Test-UninstallFailure.ps1复用原三段，补原生错误/实际执行PID/已知测试占用者/首报告持久性断言，并在断言前保存executions.json；失败异常写failure.txt，普通卸载失败不会被脚本自动重试。
- 两个独立目录uninstall-immediate-auto01、auto02均为受控失败24（DeleteFileW/32）→显式再次执行0→普通无故障卸载0；已知持有者由测试脚本自己打开的句柄证明。两轮普通卸载都未自然复现历史失败，不继续无界重复。
- 包内程序实际安装后schema26首启、basic-box默认、正常退出资源0；失败后后续载荷、数据库及未知文件保持，重试及普通卸载后旧报告摘要不变。两轮安装/登记清理完毕，日常Preview/V3配置、Run及DesktopUp桌面/开始菜单入口前后相同，无DesktopUp残留进程。
- 审计脚本复用上一发布包检查，放out/releases/uninstall-immediate-audit/Test-Package.ps1，仅允许本次明确的未提交候选并指向维护中的测试；每轮before/after、package-audit、isolation-audit均保留。自动执行为高完整性，不能代替普通权限GUI人工核对、真实登录或所有权限/杀软环境的认证。
- 源码文本门975文件/26冻结迁移通过；未改迁移、业务代码、数据结构、已验文件操作。没有提交、推送或合并，impl/develop和impl/main均未改动。

## 集中人工：已通过（以下为原验收操作）

先正常退出正在运行的DesktopUp，然后在PowerShell执行（未使用过的新隔离目录）：

~~~powershell
powershell.exe -NoProfile -File "G:\A_\Tools\DesktopUp_V2\tests\Packaging\Test-UninstallFailure.ps1" -PackageDirectory "G:\A_\Tools\DesktopUp_V2\out\releases\uninstall-immediate01" -Name uninstall-immediate-review01 -ReviewFailure
~~~

脚本自动安装真实候选到独立目录并正常启动/退出，不要求手工覆盖日常安装。卸载向导保留默认“保留在原收纳位置”，继续；本轮特意阻止一个自建载荷文件删除，核对提示含wuceffectsi.dll、DeleteFileW、Windows错误32、卸载退出码24及cleanup报告路径。该提示是预期受控测试，不代表自然故障重现。

关闭提示后脚本释放自己持有的句柄，再单独调用卸载器并做普通卸载，最终原三段均Passed=True；不手动删安装目录、不终止外部进程。报告保留在review01/profile/uninstall-reports，实际退出码在executions.json。脚本负责测试登记/快捷方式的收尾，未知样例和隔离配置留作证据；不向桌面放样例。如出现其他错误，保留整段输出及报告，不先重装覆盖现场。

## 人工结果与授权清理（2026-10-21）

用户提供完整输出和截图，确认三段符合预期；只读核对本机review01/results.json与executions.json一致：

- 受控删除共享冲突：Passed=True，DeleteFileW / Win32Error=32，实际卸载执行PID39812、退出24，首个剩余app/wuceffectsi.dll；报告cleanup-134370454233033578-39812.ini，HistoricalCauseEstablished=False。
- 释放测试句柄后另一次执行：Passed=True，退出0，配置及未知样例完整；普通无故障卸载Passed=True、退出0。自然首因没有据此销项。
- 20261021.4安装登记已不存在，无DesktopUp进程或该版本桌面/开始菜单快捷方式；没有重新执行人工或累计回归，也没有修改产品逻辑。

按用户“测试产物清理干净”的授权执行本批明确范围清理：unit01/02/03、auto01/02、review01六个隔离测试目录（执行器、样例、隔离配置）及uninstall-immediate01/build重复构建输出，共834文件、358,109,923字节。先验证目标均位于本项目out/releases、无重解析点/在用进程，并核实三个unknown-user-file.txt均为测试输入SHA256SUMS.txt副本，未触碰真实文件。

清理前逐文件记录并验证可恢复来源：95项原始测试证据/样例及六个仅构建目录含有的文件（共101项）归档到out/releases/uninstall-immediate-audit/closed-test-evidence.zip，SHA256=06F6124C2E12B2F5A88AE96AB7F1AF5CC7FF4A412CCDE36DF8D94566B751831F；另外733个重复构建文件在保留的payload/app内已有逐项同哈希副本。每项删除前再次核对摘要，7个明确目标最终均不存在。原文中的测试路径现在从该归档按原相对路径读取，不再作为活跃安装/配置；不是丢弃首次故障证据。

cleanup-manifest.json保存恢复位置，cleanup-result.json保存清理结果；cleanup-before/after.json证明日常Preview/V3配置、DesktopUp Run登记及桌面/开始菜单快捷方式在清理前后相同。用户截图另保存在同目录manual-review-20261021.png。已验20261021.4安装包、最终payload与构建/验证日志保留供集成复核，旧包和其他批次现场未清理。不把必要证据和已验交付物混同临时测试残留。

本批USER PASS / CLOSED，仍在impl/09-uninstall-immediate-evidence，未提交/推送/合并。历史中断首因继续OPEN；后续自然发生时先保存首报告、实际执行码和当时进程身份，再区分共享拒绝/权限/其他错误并定向核查，单凭RM空列表或成功重试不得定因。P1 final02、R6 probe02锁冲突为独立未定因项，剩余成本沿架构30.4节安排，真实热度HOLD。
