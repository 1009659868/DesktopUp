# M08-07-TI-04：隔离故障、进程与生命周期门

2026-09-15最新：用户明确反馈拖动回起点且同次长按失效仍存在，登记[V3-BUG-001](KNOWN_ISSUES.md)，OPEN / DEFERRED BY USER。按要求暂停修复，转[04-04开发准备](../../30_Plans/V3_04_04_CONTROL_CENTER_PLAN.md)。下方自动/组件PASS不能关闭本缺陷；不启动TI-05或改绑正式包，本轮不继续排查。

日期：2026-09-14。状态：**IN PROGRESS / 最新位置、分类取消及安全中止已接原生产通道；最终1152合同与八项R4组件PASS。当前候选、自动回归失败和原生余门以§7为准；正式M08/R1未换，不进入TI-05。**

计划归属：[M08计划§4.1](../../30_Plans/V3_04_M08_AUTO_POSTURE_PLAN.md#41-标题交互修订的最近顺序)；行为归属：[V2-DEC-003 / TI合同](M08_TITLE_INTERACTION_CONTRACT.md)。前步：[TI-03](M08_TI03_PRODUCTION_TITLE_ADAPTER.md)。不改变五类标题意图、空间策略、R4、schema或完整 CAS。

## 1. 本步生产改动：长拖不再受整次 256 样本上限截断

TI-03 的单段语义预算最多256（多人场景还会收紧）；耗尽会取消并恢复 committed 布局。它是失效保护，不是可接受的普通长拖体验。

本步改为**有界分段证明**：

1. 原驱动只在完整 proposal 成功呈现/接受后请求 checkpoint；半提交帧不能封段。
2. 到单段预算的一半，由一次绑定的协调器重新检查初始起点、完整 catalog/entity 读集、实际运行代次和物理读回，再独立重放这一小段。
3. 仅在重放状态与当前状态完整一致后，以不透明、lease 绑定的 checkpoint 保存当前状态并清空样本尾段。保存意图、原 Press/capture、抓取点、归位因果、UserMoved；**不重新按下、不提交数据库、不发 projection**。
4. checkpoint 不保存前驱对象或无限轨迹链；单个当前状态、初始状态与有界尾段留在内存。Digest 是轨迹指纹，不是可由 JSON 构造的授权证明。
5. 松手时重放最后一段并复核同一 checkpoint 身份；仍由原 SQLite 完整 CAS 推导一次写集，只提交一个 revision。删除/隐藏/设置/目录冲突或旧 runtime/lease 一律拒绝旧动作。
6. 未经封段的普通输入证明仍保留原预算和 fail-closed；不开放无界重放。日志新增 SealedSegments。

锚点：Application/TitleSpatialSession.cs、TitleRuntimeAuthority.cs、StateProjectionCoordinator.CheckpointTitle、TitleSpatialCommit.cs；App/PosterSpatialRuntime.Title.cs。没有另建持久权威或每帧写库。

## 2. 自动化与故障证据

| 门项 | 结果 | 范围 |
| --- | --- | --- |
| 新分段证明测试 | 25/25 PASS | 1/3/6 × Expanding/Collapsing/Expanded，每项2048次移动；峰值尾段≤128、≥16段；总revision仅最终+1、单发布、无偏移、保留原抓取与邻居Home；取消/身份/实体失效、外来checkpoint拒绝 |
| 完整合同集 | 1105/1105，0 skipped | 原1080＋新25；包含原Pending长按、阈值/回移/释放换序、部分帧失败、旧token、CAS测试 |
| R2-A独立复验 | 4/4 PASS | 外部按钮/捕获连续变化不重置原1000ms期限（三种组合）及前台覆盖点击无保护；fake-native观察适配测试，不是实机连点 |
| 独立WinUI/fixture构建 | 0警告、0错误 | Release x64；不覆盖R1包 |
| R4原生组件故障 | 五场景PASS，两次进程运行 | BeginTitlePreview/CommitSpatialFrames/RestorePresentedGroup真实窗口组件；非标题原生输入/所有者验收 |
| 既有自动/生命周期原生进程 | 下表PASS | 实际被动观察、局部WTS/电源信号；非实际锁屏/休眠/Explorer重启 |
| PS5.1入口 | PASS | Windows PowerShell5.1实际执行R4脚本成功；候选人工入口CheckOnly校验25个核心产物，未启动人工会话 |

TRX 位于 `out/verify/v3-04-m08-07-ti04/tests`：

- `ti04-rolling-final.trx`：25项。
- `ti04-full-final.trx`：1105项。
- `ti04-r2a-independent-final.trx`：4项。
- `ti04-title-initial.trx`：最初233项；`ti04-rolling.trx`：最初18项，均为阶段记录。
- `ti04-r2a-independent.trx`第一次按文件名过滤，但类是partial PassivePosterPointerTests，命中0项，**不是PASS**；保留记录，改用完整方法名筛选后4项通过。

### R4组件夹具的故障边界

新增 `PosterSpatialRuntime.TitleSurfaceSmoke.cs` 和 `scripts/smoke-title-surface.ps1`。仅显式 `--title-surface-smoke`、独立profile、三张auto-OFF链式DEMO可执行；不注入鼠标，不持有伪造的原生capture，不调用可信Title运行租约，不写数据库。

- prepare-partial：只对一张建立预布局，然后走整组共享恢复。
- submit-before：驱动提交回调返回false，不提交任何窗口。
- submit-partial：实际只提交原多窗口帧的一张，再返回false；不接受proposal，走整组恢复。
- restore-partial：完整组呈现后只对一张结束恢复，随后重试统一恢复。
- complete：完整呈现后恢复原committed基线。
- 每项延后一轮检查：revision未变、所有原生几何等于基线、XAML匹配、TransitionLayout全部释放；取消后旧驱动回调与输入不再绘制。

这些是**故障在组件边界的受控模拟**，不是诱发真实DWM/系统调用失败，也没有验证TitleRun恢复失败时的RecoveryRequired重建分支、native捕获释放、在途title的完整生命周期。成功读回不是GPU像素/无白条证明，不能以这五项关闭视觉门。

### 同一最终候选包的进程记录

全部正常退出 exit0，resource-ledger clean=true / resources=0；profile均在 `out/verify/v3-04-m08-07-ti04` 下。

| Profile / 模式 | PID | 结果 |
| --- | --- | --- |
| surface-faults，组件故障 | 41140 | 五项PASS |
| surface-faults，PS5.1重复运行 | 50312 | 五项PASS |
| automatic-3，auto-posture-smoke | 57132 | 4请求、3提交、1策略Conflict取消；NativePointer=true |
| lifetime-final-3，exercise | 50908 | 局部挂起/恢复、隐藏/重建、删除至零；ObservedOutside=true |
| lifetime-final-3，restart | 40104 | 0张重启，revision/定义完全一致，控制中心可用 |
| lifetime-final-1，enabled-exit / restart | 37688 / 51220 | 1张开关/300秒延迟持久化、同库revision/定义一致 |
| lifetime-final-6，enabled-exit / restart | 52068 / 53532 | 6张同上，无重复计时器或资源泄漏 |

最初 `lifetime-3` PID56080也PASS，但在添加R4夹具前构建，**不计入最终包九次进程证据**。原R1/M08-06历史证据不挪作当前原生标题认证。当前资源有效样本仍0/36。

## 3. 旧候选快照与保留门

旧候选目录：`out/verify/v3-04-m08-07-ti04`。旧25项指纹已另存该目录的`ti04-candidate.json`，旧包/故障配置保留；`scripts/ti04-candidate.json`现在绑定§6的修订候选，不再是本节旧包指纹：

- App SHA256：`0DA8D45E91EA4645DC58BDCDDF764127D582B5483E0F7FE6D43673F04E597762`
- Application：`8DBE9C387BF88235D8083D2B48AA274DAD5D40F1A697CBF77F56C070E38CED10`
- Windows：`8DBA1555B84C42C9D2481971D1136E574866ECA2C0A73008D81D3DDE15184737`
- 冻结动画：12px / 16ms / 240ms / ease-in-out。

`Get-M08Build.ps1` 与 `m08-build.json` **未修改**，仍绑定R1（25项复检PASS）。候选入口不是M08正式人工/资源入口，不提前扩称TI-04全部通过。

只读预检2026-09-14 06:24:31 UTC：输入/线程桌面Default，读取光标正常、error0。Windows交互工具初始化报CreateProcessWithLogonW 1385，重置后仍异常退出。因此**只是交互工具未能运行，不能推断桌面锁定、要求改管理员权限或称原生输入已验**。遵守computer-use技能边界，没有改用自写SendInput/窗口消息模拟鼠标。

仍需补：

1. 原输入岛真实DOWN→同源已接受帧移交→MOVE/UP/CANCEL；Pending久按、快点、越阈值立刻回移/释放、Expanded末帧/按下换序，真正长拖。
2. TitleRun在途的捕获丢失、源/邻居变化、隐藏/删除/停用、实际生命周期/环境恢复；过期Rendering/watchdog不能影响后继所有者，恢复异常必须释放资源并进入原恢复流程。
3. R4组件故障与**真实标题运行所有者**结合的取消/恢复验证，不能仅由本轮组件夹具推断。
4. R2-A桌面/前台持续连点独立实测；原06的实际恢复和视觉余门仍携带。
5. 以上TI-04门通过后才更新同一正式人工/测量指纹，进入TI-05集中体验/1-3-6资源；不跳入真实Item收纳。

## 4. 独立候选人工入口

PowerShell5.1可用；已有DesktopUp请先从托盘正常退出。不需要管理员权限，也不要求升级PowerShell。

```powershell
& 'G:\A_\Tools\DesktopUp_V2\scripts\start-ti04-title-manual.ps1' -Scenario chain
```

只启动普通产品路径，显示控制中心；手动对测试海报开启自动收展。当前修订的三个DEMO海报数据在 `out/manual/v3-04-m08-07-ti04-handoff/chain`；首次创建，后续重用，旧`...ti04/chain`保留且不被覆盖。脚本打印PID、LogPath、候选AppHash；无自动删除或输入注入。支持basic/constrained/blocked，受限夹具的工作区假设沿用原门，须先核实屏幕关系。准确当前产物与状态以§6为准。

先验标题Pending/点击/双类拖动和长拖；收集 `spatial.title_started / spatial.title_finished` 中的 Status、restored、AcceptedFrames、samples、SealedSegments。人工画面和手感结论单独反馈，日志不能替代。按本节补齐原生门后再按TI-05完整矩阵验收。

收尾：83份Markdown、547个本地文件链接、缺失0；git diff --check通过；25项候选绑定复检通过，DesktopUp运行进程0。链接检查不等同于HTML/视觉验证。

未commit/push；本轮及TI-01..03未提交变更保留。六卡仍DEMO，不接触用户快捷方式或文件。

## 5. 首次标题接管失败定位与修订（2026-09-14）

本节优先于上述旧候选的“当前”措辞。用户要求本轮只修复并验证首次接管，不进入TI-05。

### 现象与实际路径

日志：`out/manual/v3-04-m08-07-ti04/chain/logs/desktopup.jsonl`，旧包PID18172。约07:10:17 UTC，同源自动展开已准备全高480px的R4视觉窗口，源逻辑高度从60px开始。第一次标题按下取消旧自动请求回执，移交当前已接受布局；邻居建立新租约，源复用旧租约而不重新准备布局。

紧接着出现`spatial.title_rejected: Title R4 surface preparation failed.`；`title_finished`为Status7、start-or-preparation-failed、AcceptedFrames0、samples0、HasUserMoved=false、restored=true。第二次按下从静态Compact启动，最终Status0、AcceptedFrames50、samples51、HasUserMoved=true。因此首次缩回是失败恢复的结果，不是正常长按策略。

### 根因和修订边界

`DesktopLayerAttachment.VerifyVisualSurface(window, surface, visible)`要求HWND/window/client尺寸等于surface、HRGN等于visible。原SurfacePreparation守卫传入两次`_bounds`，等于错误地要求原生容量与可见范围一致。R4展开途中这两个边界本来就不同，正常首按接管被误拒绝。

修订：

1. CommitSpatialFrames使用CurrentNativeBounds作为旧native容量，_bounds作为旧visible裁切，完整保留readback/region验证。
2. 需要扩大容量时，BeginTransitionLayout先保存PreviousNativeBounds再替换XAML租约；新native准备成功前，CurrentNativeBounds仍报告旧实际容量，而不是新目标或逻辑可见区。
3. 现有足够容量仍直接retag；不提前回滚到收起态、不释放原capture、不换原Press/抓取点，不改空间求解/完整CAS/恢复。
4. 只在基线失败时增加有界日志`spatial.surface_preparation_baseline_rejected`，记录expectedSurface/expectedVisible/requestedSurface。不新增高频鼠标轨迹日志。

### 新回归与当前证据

原五项只从静态表面建立R4，漏掉“活跃R4→标题”的边界。新增：

- handoff-reuse：真正提交一个未完成展开的R4前缀，重新分配标题所有权但复用容量；验证旧物理读回、SurfacePreparation、稳定内容布局与共享结束恢复。
- handoff-grow：同样从已呈现前缀接管，但申请更大源容量；新native提交前仍须读到旧native/visible，提交后验证新容量及原可见区。
- 仍是原生窗口组件夹具，不持有伪造capture/可信TitleRun，不写库，不冒充真实一次按住拖动。

| 产物 / 验证 | 当前结果 |
| --- | --- |
| 未修复代码＋新增夹具 `out/verify/v3-04-m08-07-ti04-handoff-red/app` | 构建0警告0错误，App SHA256 `901F716F0FE7A44EE97C9E97F2C4B2F26A29EC71B96752F194258D5CE3F4351F`；PID58304已取得预期RED：旧五项通过，handoff-reuse在SurfacePreparation失败，grow未执行；退出资源0 |
| 修订代码＋新增夹具 `out/verify/v3-04-m08-07-ti04-handoff/app` | 构建0警告0错误，App SHA256 `8B62D7607B8EA5A06906E25704E160AD203E8FAC8A859E1155ACE0591F9FC194`；用户执行PID50188七项R4全PASS，日志复核一致，exit0/clean0，见§6 |
| 修订合同全量 | 1105/1105，0 skipped，exit0；`out/verify/v3-04-m08-07-ti04-handoff/tests/ti04-handoff-full.trx` |
| 真实首按Pending/拖动/释放/重入 | PENDING；computer-use初始化1385，重置后仍失败，没有旁路输入 |
| 包绑定 | 正式R1不变；TI-04独立候选入口已改绑定本修订（25项），使用新人工profile；旧包/故障日志/指纹快照保留 |

用户随后确认旧版退出；读取PID18172台账，07:45:36 UTC记录clean=true/resources=[]，进程检查无DesktopUp。07:49:19 UTC，未修复对照PID58304完成旧五项后，在handoff-reuse断言报`retained R4 surface rejected during title handoff`（夹具131行）；smoke脚本正确退出1，不记产品PASS。原生App走正常退出，日志application.stopped/activeResources=0。精确日志在`out/verify/v3-04-m08-07-ti04-handoff-red/surface-faults/logs/desktopup.jsonl`；这次证明新增夹具抓住了原生守卫错误，不是启动/环境前置失败。

历史阻挡：上一轮GREEN配置生成命令被审批服务以Selected model is at capacity拒绝，用户随后运行原命令取得§6的PASS；不再视为当前组件阻塞。工具初始化1385仍限制自动鼠标操作，没有使用输入旁路。

## 6. 修订GREEN与当前原生标题入口（2026-09-14）

本节是旧handoff候选的阶段记录，后续反馈、退出及safe-stop修订见§7；下文PID不代表仍在运行。

用户提供PID50188的PASS输出，读取对应日志与台账复核：

- 七项全部PASS：prepare-partial、submit-before、submit-partial、restore-partial、complete、handoff-reuse、handoff-grow。每项Revision=3，布局租约释放、原生/XAML终态回到原committed基线；smoke_completed passedCases=7、error=null、databaseWrites=false、nativeInput=false。
- 用户脚本输出ExitCode=0；08:02:43 UTC台账clean=true/resources=[]，application.stopped activeResources=0。日志位于`out/verify/v3-04-m08-07-ti04-handoff/surface-faults/logs/desktopup.jsonl`。
- 配合PID58304的精确RED，已证明该原生容量/可见裁切守卫缺陷被修正；**尚不能据此认定真实第一次按住拖动通过**。

候选绑定：

- 当前目录`out/verify/v3-04-m08-07-ti04-handoff`；App SHA256仍为`8B62D7607B8EA5A06906E25704E160AD203E8FAC8A859E1155ACE0591F9FC194`，没有重建App后混用用户PASS。
- fixture统一Release x64构建0警告0错误；25项最终指纹相较旧候选仅App程序集不同。首次未指定Platform的中间AnyCPU fixture已被x64产物替换，未拿中间产物运行或绑定正式入口。
- `scripts/start-ti04-title-manual.ps1`＋`ti04-candidate.json`改绑修订目录；新人工数据根`out/manual/v3-04-m08-07-ti04-handoff`。旧包根内另存旧指纹，不覆盖旧人工数据/故障日志；正式`Get-M08Build.ps1/m08-build.json`未改。
- Windows PowerShell5.1 CheckOnly校验25项PASS；实际启动PID56300、同库重启PID54160，均按--exit-after-ms=5000正常退出，无error/critical、资源clean/0。启动脚本exit0；两次App的OS退出码未单独持有进程句柄读取，不另写ExitCode=0。两次退出数据库SHA256均`232C0A7B94BBA2BD0830850218E14360A69457F8F1F91EBD4986A1ED8E0C3C89`。
- 当前已从同一PS5.1入口打开人工候选PID14408，未设自动退出。普通产品启动，三张DEMO、auto初始关闭；用户在中心启用并应用。没有原生鼠标注入或测试专用交互替代。

当前先验证三个紧邻用例：

**后续用户反馈：选择“其他异常”，未描述具体表现，三项不能记PASS。** PID14408已发现一次展开中接管成功提交，另一次Expanded拖动在3个接受帧后因surfaceApplied=false取消并恢复；nativeApplied/matches/inputApplied/regionApplied均true。现有bool校验尚不能说明实际window/client/HRGN哪项不符，具体根因待取证；已请求用户补操作/画面，未新改代码或关闭TI-04。

1. 收起态真实移入标题，展开刚开始时一次按住并越阈值拖动，全程不松手；立即跟手并同步收拢，不能要求第二次按下。
2. 展开中按住标题静止约2秒，保持暂停；随后不松手直接拖动仍有效，不因等待丢失同一次捕获。
3. 松手后保留新位置和收起态；鼠标仍停留不反弹，真正离开再进入才自动展开。

以上真实输入/手感结论PENDING，收集同一capture的title_started/title_finished和完整提交/恢复日志后单独登记。computer-use本轮初始化仍CreateProcessWithLogonW1385；按技能边界不改用自写SendInput或伪造capture，人工是实际输入门，不是降低标准。后续仍补TI-04在途失效/生命周期与R2-A独立门；本轮不进入TI-05、无资源采样、未commit/push。

## 7. 最新位置与安全中止（2026-09-14）

本节为当前 TI-04 状态，优先于上方旧包的“当前/PID/待描述”措辞。用户已确认首次按住可以拖动，但快速拖动、左键未松开时偶发回到起点，并要求先小修合同、连续测试，再接生产。PID14408已由用户正常退出，旧台账08:28:42 UTC clean/0；原包和日志保留。

### 冻结范围与实现

行为仍归 V2-DEC-003 / [标题合同§8](M08_TITLE_INTERACTION_CONTRACT.md#8-ti-04-小修订最新位置与分类结束2026-09-14)。不改五类意图、5px阈值、空间策略、R4、schema、完整CAS或自动开关设置。

- 首次越阈值证据只保留一次；已进入拖动后消费最新合法位置。弯折多帧路径只在成功提交并读回的整组边界替换尾段，不切穿障碍。已呈现前缀由原空间模型重新推导并纳入重放，禁止用任意HWND坐标补写模型。
- 原生取消分类保留来源：CaptureTransferred、CancelMode、RouteInvalidated、CursorUnavailable、CaptureUnavailable、Programmatic。只有捕获转移且原路由/会话、环境、完整读集、可信整组位置仍有效时可申请安全中止；真正失效仍硬取消。
- 独立 Interrupt/Interrupted 终态，不伪造ButtonUp。丢弃尚未呈现的鼠标目标，停在最后可信位置；CompactDrag在该锚点完成必要收拢及合法条件归位，ExpandedDrag保持高度。Pending未成拖动不制造点击。最终仍原租约、事务内复验、完整CAS、单revision，捕获不自动重抢。
- TitleCurrent只对已结束输入免去“仍拥有native capture”的要求，其他校验不变。失败或部分帧不降级为安全中止；恢复仍以最新权威状态为准。日志记录NativeEnd、Interrupted、FailureReason及请求中止的帧边界。
- 连续替换弯折尾段达到封段阈值时，先让原CheckpointTitle验证已接受片段；最新输入保留到下一轮，不让pending提案长期阻塞封段、再次耗尽预算。
- 既有标题收拢逐次重算、使用FrameInterval而未消费DueMilliseconds/完整时间曲线的问题仅登记为实现差异；未做性能测量，本轮不改动画参数含义，不声称所有不流畅都已消除。

### 证据：先RED，再纯验证，再生产接线

独立目录：out/verify/v3-04-m08-07-ti04-safe-stop。TRX均位于该目录tests。

| 验证 | 结果 / 范围 |
| --- | --- |
| latest-red.trx | 2个预期失败：旧驱动均先到120，最新目标分别应为220/40；确认重复追赶旧输入 |
| safe-stop-first.trx | 新22项PASS：四种按下姿态、未呈现目标丢弃、弯折前缀、反向批次、硬失效、归位、单revision/CAS |
| contracts-before-wiring.trx | 1151/1151 PASS后才接原生取消来源与TitleRun；包含补充的因果归位、事务内见证、原生分类 |
| contracts-wired.trx | 接线后1151/1151 PASS |
| prefix-checkpoint-red.trx | 新封段边界1项预期失败：在阈值处继续消费新移动，未先让出封段边界；随后补让出调度 |
| contracts-final.trx | 最终1152/1152 PASS，0 skipped：相对1105基线新增34项本轮合同/连续/权限测试＋13项表面读回诊断纯测试 |
| WinUI及SessionFixture | 最终Release x64均0警告、0错误 |
| 原生R4组件 / PID14772 | 八项PASS，含translate-fast的120次受控位置变化；exit0、clean/0，不写库，不是鼠标/capture或可信TitleRun实测 |
| 自动收展 / PID3932 | 未通过：Unavailable、0请求/0提交，超时退出，clean/0；尚未细分Ready/Visible/Unknown指针原因，不推断锁屏/UAC，不拿旧包PASS覆盖 |
| 生命周期 / PID59288、44952 | exercise与同库restart PASS：3→0→重启0，原生布局/共享恢复/局部通知/退出0资源；ObservedOutside=false，不代替自动触发或标题在途生命周期 |
| 新独立候选CheckOnly | 25项指纹PASS；正式M08/R1的25项仍PASS、绑定未改 |

此前PID14408在08:13:56 UTC的surfaceApplied=false仍只定位到表面读回守卫：其他native/matches/input/region项true。新诊断保留同一次window/client/HRGN观测和具体失败分类，纯比较13项PASS；translate-fast未复现该失败。**没有证据把那次回起点归为捕获丢失，也不能声称本轮消除了该偶发原生失败。** 已修正的是代码中可证明的陈旧输入调度及取消分类缺口。

### 候选与下一步

scripts/start-ti04-title-manual.ps1和ti04-candidate.json现绑定safe-stop目录；App SHA256：B8A75A9ECFC04328514C1040E4CCDCB3CD582A344B28310DE43764AA590A0021。Application：06FE18E9B4AD847B6E3FFB6098D8516834DD7122B49CA2EB26F464F08818B91D；Windows：B0817A487A4519A09F3F13022A43A97E163C67DB92EB7186564223CE2C9EB85C。旧handoff清单另存旧包根，旧人工配置保留；新人工根为out/manual/v3-04-m08-07-ti04-safe-stop/chain，首次启动创建三张DEMO。

~~~powershell
& 'G:\A_\Tools\DesktopUp_V2\scripts\start-ti04-title-manual.ps1' -Scenario chain
~~~

先在控制中心对A开启自动收展并应用。继续 **TI-04原生标题门**：首次按住快拖/快速反向、展开与收起中接管、松手或自然断触后的新位置保持、邻居条件归位；与auto-OFF和完全展开后拖动对照。真实断触不得用假Cancel或组件API调用充数。补本包自动触发与在途恢复余门后，再考虑TI-05人工/资源，当前0/36不变。

审批服务曾多次容量失败，拒绝的命令未执行；新候选普通入口“两次自动退出启动”检查尚未执行，不把历史handoff启动记录挪作当前证据。最终组件/生命周期进程均退出；无commit/push、无日常库/用户文件变更。
