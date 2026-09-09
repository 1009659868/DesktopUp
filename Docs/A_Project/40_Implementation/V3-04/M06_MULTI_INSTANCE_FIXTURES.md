# V3-04-M06：多实例合同夹具

日期：2026-09-09。状态：CLOSED — CONTRACT/BUILD/PROCESS PASS；人工安全余项保留至M07。

## 执行合同

用户明确进入M06。复用M03注册表/共享恢复、M04输入/删除/空间合同、M05目标参数；本轮不改产品方向、不新增状态权威或真实文件业务、不重写Host/动画。M05遗留真实窗口验证并入本轮先执行，未获人工确认项不补填PASS。

1. 共享测试数据：A游戏、B文件、C应用三套不同标题/内容参数/颜色/收起和锁定状态，始终只是DEMO ITEMS；通过现有命令创建独立ID/主题身份。
2. 数据＋注册表夹具：独立参数与读集CAS、隐藏后更新再显示、删除后旧回调、单实例启动/应用/恢复故障及重试、重启后隐藏/删除不复活、零目录保持零；使用隔离SQLite和替身会话，不冒充原生可视验证。
3. 真实Release程序化门：记录三端目录/单控制中心/单托盘/输入会话及XAML/原生bounds读回；修改A、隐藏/显示B、取消/过期删除C、确认删除C，最后保留B隐藏。第二进程启动验证两个持久条目但仅A显示、无额外默认。
4. 复用M05参数烟测和原空间烟测；程序化验证结果与人工鼠标/视觉严格分开。旧R2实例还在运行时不强杀或转发测试意图。

## 阶段卡

| 子项 | 范围 | 出口 |
| --- | --- | --- |
| M06-01 | 共享三用途隔离数据与合同夹具 | AUTO PASS：新增10项，完整405/405 |
| M06-02 | 实际窗口exercise＋restart脚本 | PROCESS PASS：Exercise PID44928、单实例激活PID48160、Restart PID47076；exit0，资源0 |
| M06-03 | M05尾门＋M06端到端回归与交接 | PASS：M05参数两次及空间回归通过；完整合同复跑405/405；文档同步，下一项M07 |

本轮删除测试仅解除隔离库中的海报实体。真实文件、桌面快捷方式、日常数据库不在目标中；不模拟Explorer进程终止。来源继续复用已记录的DeskBox实例配置/生命周期设计，不新复制参考源码。

## 共享配置与自动证据

测试与生成器共用`tests/Shared/MultiPosterFixtureData.cs`，通过编译链接复用，不放进Domain生产默认值；生成器新增`--multi-instance`，要求全新隔离根目录且恰好3张。数据库身份由原CreatePoster命令分配，禁止手工复制同一ID或主题身份。

| 海报 | 模板 | 图标DIP/间距DIP | 背景 | 初始状态/保存高度DIP |
| --- | --- | --- | --- | --- |
| A / Games | night-ops | 24 / 8 | #182B36 | 展开、未锁定 / 480 |
| B / Documents | basic-box | 32 / 12 | #282B30 | 展开、锁定 / 420 |
| C / Applications | basic-box | 40 / 16 | #243329 | 收起、未锁定 / 520 |

三个名称表达用途样例，不代表真实文件来源已接入。不同屏幕启动仍走原位置重投影，本轮不新增跨屏算法。

`PosterSessionRegistryTests.M06.cs`复用原注册表替身/隔离SQLite夹具：

- 三套参数和主题身份独立、仓储重开一致，三端revision一致；
- 隐藏B后改参数不建窗口，重新显示使用新epoch/最新参数，A/C会话不重建；
- B启动、应用投影、恢复三个故障阶段均隔离；A/C继续就绪，重试B只建立新B、保留committed数据；
- B未知参数版本只给B安全fallback，保留其原JSON且A/C不受影响；
- 取消确认无写，过期确认Conflict；确认删C后旧projection/命令不能复活C；
- 重开数据库保留A改动、B隐藏、C删除；零目录不补默认；
- 环境等待中旧快照失效，恢复读取最新显隐/参数，不复活已隐藏/删除项。

完整405/405、0skip，证据`out/verify/v3-04-m06/results/m06-contract.trx`，SHA256 `6248A8805E5C3AC06321BF5F330467C739055451B00A93F0EEA341C3D1DCE12A`。首次新增测试编译缺少Application using，修正后通过；首轮App夹具误把PrepareRemoval调用到命令接口，已改用查询接口，最终Release0warning/0error。没有修改生产仓储/求解器来迎合测试。

## 真实进程执行方法与断言（2026-09-09已通过）

产物：`out/verify/v3-04-m06/app/DesktopUp.exe`。数据：`out/verify/v3-04-m06/smoke/multi-instance/`，初始revision3，IDs：poster-main、poster-9547c10962974c2a9cf65657be625fdd、poster-198e99dc11534233821d67b5c6636a0a。该数据现已完成Exercise/Restart，保留A/B两条、仅A显示、C删除；不再是初始三张配置。不要用本夹具替换日常配置，也不要对同一已完成exercise配置再次exercise。

用户确认旧版正常退出，进程检查无DesktopUp后，已先补[M05遗留门](VERIFICATION.md)：固定M05 Release的presentation两次（PID43944/45260）及空间回归（PID45428）全通过。随后按以下命令执行M06；命令作为证据复现说明，新的Exercise必须另建隔离数据：

```powershell
& 'G:\A_\Tools\DesktopUp_V2\scripts\smoke-multi-instance.ps1' `
  -ExecutablePath 'G:\A_\Tools\DesktopUp_V2\out\verify\v3-04-m06\app\DesktopUp.exe' `
  -DataRoot 'G:\A_\Tools\DesktopUp_V2\out\verify\v3-04-m06\smoke\multi-instance' -Phase Exercise

& 'G:\A_\Tools\DesktopUp_V2\scripts\smoke-multi-instance.ps1' `
  -ExecutablePath 'G:\A_\Tools\DesktopUp_V2\out\verify\v3-04-m06\app\DesktopUp.exe' `
  -DataRoot 'G:\A_\Tools\DesktopUp_V2\out\verify\v3-04-m06\smoke\multi-instance' -Phase Restart
```

一次一个进程。Exercise仅操作隔离实体，9个快照阶段依次为initial、edited-a、hidden-b、edited-hidden-b、reshown-b、stale-removal-ready、deleted-c、final-catalog、final。每次核对完整PosterDefinition，确认除目标允许变化外其他配置不变；除重新显示B外，不重建健康邻居HWND，所有外框保持；XAML度量/参数、中心实际列表与托盘revision、输入会话数均读回。

脚本在初始快照后另启一个短进程走真正单实例激活，要求该进程exit0、主进程没有额外托盘/海报。中心中途普通关闭/重开，所以生命周期合计允许2次创建，但任一快照只有1个活动中心；托盘全程只创建1次。最后目录剩A/B，B隐藏、C已删除，活动海报只剩A。Restart不执行业务变更，用新进程快照与上次final的完整海报定义精确对照，而不是只比数量。

以本轮PID过滤`logs/desktopup.jsonl`；要求smoke_completed passed、exit0、无error/critical、resource-ledger clean/0。Exercise PID44928的9个快照完整，短进程PID48160正常转发并exit0；中心顺序创建2次、任何快照仅1个活动中心，托盘全程1次。Restart PID47076仅创建1中心/1托盘，完整PosterDefinition与上次final精确一致（两条目录/一个原生海报）。两轮均exit0、无error/critical、台账clean/0。

本次固定构建无需修复或重建；完整合同另存`out/verify/v3-04-m06/results/m06-resume.trx`，405/405、0skip。日志和产物指纹见[VERIFICATION](VERIFICATION.md)。只有有明确CLI开关且数据根不在日常profile时才运行；多种smoke开关互斥。测试退出超时报告PID，不强杀。

## 保留出口和下一步

M05/M06自动合同与真实进程读回门已关闭，旧进程等待条件已解除；下一项M07测量合同/前置安全门，本轮未实施M07。局部故障为替身会话注入，不等同Explorer实际重启；删除确认的cancel/stale是状态机路径，不替代鼠标点击确认框。保留的点击、菜单、桌面层级、恢复和视觉验证集中至M07；动画美化继续延期，不重复全部DPI/安装测试。

代码锚点/最新指纹与状态见[CODE_ANCHORS](CODE_ANCHORS.md)、[VERIFICATION](VERIFICATION.md)、[HANDOFF](HANDOFF.md)。本轮无commit/push，无日常数据库、文件或快捷方式修改。
