# V3-04 当前交接

日期：2026-09-09
当前任务：M07 IN PROGRESS；用户确认启动后1～2人工项通过（S1基础/S2生命周期），另报“…”菜单部分命中异常。当前只排查、不修改实现；[诊断](M07_MENU_INPUT_DIAGNOSIS.md)记录菜单打开期间底层Item响应及输入岛隔离缺口，仍需失败点原生命中证据。S3～S5待确认，M07不能关闭；5.1启动与411/411结论保留。

用户进一步确认：失败时菜单项不高亮，偶尔进入编辑后多个菜单项全部失灵。已查明Editing会新增整面EditSurface输入区域；PID48408进入Editing后，菜单打开期间又记录13次底层Item点击。高置信度是菜单/输入岛隔离与优先级问题，不能仅改按钮坐标或移除MenuCurrent。只读UI工具1385未取得实时HWND命中快照；未改实现、未重建、未操控用户进程。

## M07 当前交接（优先于下方全部历史）

完整范围、口径、当前环境命令和人工5组见[M07合同](M07_MEASUREMENT_AND_SAFETY.md)。`scripts/measure-m07.ps1`外部采样18格×2；`MeasurementMath.cs`统计与合同测试共用；`MeasurementFixtureData`仅用于新隔离配置。空间真实SetCollapsed路径仅在隔离smoke开启实际提交/回调计时，正常启动不开启；未重写Host/输入/事务，无新schema。

固定产物`out/verify/v3-04-m07/app/`和`scripts/m07-build.json`未变。首轮`measurements-r1`4个探索样本及第5格中断证据原样保留，不冒充完整矩阵/普通用户认证。原权限拒绝条件已取消，`stop-after-current`仍保留；未改UAC或系统权限。新共享进程启动器采用子进程环境变量，避免7.4专属API，也不污染调用方环境。

立即入口：`scripts/start-m07-manual.ps1 -Scenario interaction`，直接在用户当前5.1执行，无自动到期。无需先运行完整测量。正式资源采样仍用`start-m07-measurement.ps1`（约12分钟），输出`current-env-*`；空间计时用`measure-m07-spatial.ps1`，输出`spatial-current-env-*`。两者记录真实Elevated/PowerShell，不以权限判失败。人工和采样不能同时运行。

人工4套新库在`out/manual/v3-04-m07/`，全部CheckOnly通过。当前直接从`start-m07-manual.ps1 -Scenario interaction`开始S1/S2，chain做S3，removal做S4/S5；return-sync为辅助，不重复要求全部DPI/安装或动画打磨。每次切换正常退出，removal删零后同库重启；只能删隔离海报实体。未收到反馈不能补写人工PASS。

兼容回归已完成：5.1单海报6场景采样通过；1/3/6空间时序已通过（`spatial-current-env-20260909-121733-7529e4`，PID42984/37208/43192，20/60/120实际提交、exit0/资源0），不再标为时序全部未执行。PowerShell7.6.5同启动回归PID41616也通过。完整36次资源矩阵仍未完成；不要将脚本兼容通过当作S1～S5人工通过。回归进程已全部正常退出，用户可直接启动人工场景。

当前50MiB目标未改预算；0.10秒CPU目标与真实呈现FPS/GPU等测量边界详见合同。M07关门前不进原04-04，基线收集不等于性能认证。所有既有dirty/历史包保留，未commit/push、未改日常配置/桌面文件。 5.1启动证据：`launcher-20260909-121424-23a691/result.json`（PID47912）；兼容回归采样：`ps51-launcher-samples-20260909-121448`（单海报6格、一次重复，非36次完整矩阵）。

## M06 历史交接（已完成，当前以上方M07为准）

用户本次明确进入M06；[完整合同/脚本用法](M06_MULTI_INSTANCE_FIXTURES.md)已建立。测试与SessionFixture共用三用途参数，新增10项合同覆盖参数独立、隐藏编辑、局部启动/应用/恢复故障、取消/过期删除、删除后晚到回调、重启与零目录；完整405/405。生产默认、SQLite schema、Host和空间算法未重写；App只新增显式隔离开关的端到端探针及只读XAML/三端读回。

当前产物`out/verify/v3-04-m06/app/`未变；数据`out/verify/v3-04-m06/smoke/multi-instance/`已由初始三张变为两条目录/仅A显示（B隐藏、C删除），不能再次直接Exercise。PID44928的9阶段验证A编辑、B显隐/隐藏编辑、C取消/过期/确认删除、晚到命令、中心关闭重开；短进程PID48160单实例转发exit0。Restart PID47076完整定义与上次final精确一致。两个主进程均exit0、无error/critical、资源clean0，任一快照仅一中心一托盘。

2026-09-09用户确认正常退出，进程检查通过；M05参数PID43944/45260、空间PID45428先行补跑全部PASS，随后M06上述两轮通过。完整合同另存m06-resume.trx，405/405、0skip；实际证据/指纹见[VERIFICATION](VERIFICATION.md)。本次无生产代码修复或Release替换。

立即下一步：M07先冻结1/3/6张、静态/收起/隐藏/中心开关的测量口径，记录CPU/工作集/句柄/帧及交互延迟和普通用户权限；收口M04及参数变化的鼠标、菜单、层级/恢复和视觉安全余项，再原04-04与真实Item主线。六张仅压力探测，不声明容量保证；不立即优化、不进入完整主题/AI，动画继续延期。M05/M06关闭不代表M04全人工PASS。

本轮保留全部既有dirty和旧M04/M05包/数据，无commit/push、无日常数据库或桌面文件修改。

## M05 历史交接（以下当时待办已于2026-09-09补齐，当前以上文为准）

[参数合同](M05_PRESENTATION_PARAMETERS.md)已完成字段/默认/范围/版本/legacy/fallback及目标草稿/CAS；逐Poster度量已进渲染、输入、滚动、展开重算和恢复，basic-box/night-ops最小内置结构、独立标题/颜色及中心只读摘要已接。无新SQLite迁移、无第二状态权威、无完整参数表单、无真实文件/AI/全主题加载器；动画按用户要求延期。

用户本次明确允许进入M05；历史“不进入M05”不再是当前指令。收展策略体验已获认可但未逐项确认的删除/恢复等安全格仍保留，不能把宽泛认可写成全部人工PASS。

当时待办（第1/2项现已完成，第3项安全余项保留M07）：

1. 用户已收到正常退出旧R2的请求；确认不存在DesktopUp进程后运行`scripts/smoke-presentation.ps1`。Release与新三张配置分别为`out/verify/v3-04-m05/app/`、`out/verify/v3-04-m05/smoke/three-posters/`；重复两次验证保存参数重启，命令见[验证记录](VERIFICATION.md)。禁止强杀旧版、修改单实例机制或借旧进程日志冒充新版本。
2. 用同一隔离库执行既有spatial smoke，验证参数化最小高度没有回归收展；核对exit0/台账资源0，失败先定位。更新指纹/结果与状态，尚未执行就不能填PASS。
3. M05受影响视觉/鼠标门与既有M04余项集中，不全量重跑DPI/安装。之后M06复用三种用途参数和现有恢复/隐藏/删除夹具完善端到端，M07做测量/前置门，再原04-04完整管理→V3-05真实Item。

已验证：395/395，其中39新增；初次394/395仅旧背景颜色源码断言，已改为同源参数背景，原resize安全断言保留。Release0warning0error；本轮无启动测试常驻、无日常SQLite/桌面文件修改；既有M04 dirty完整保留，未commit/push。

## R2 历史实现交接

纯候选、共享最小内容高度、会话有效投影、中心/托盘同源摘要和按钮反馈已接入。保存600 DIP、当前450 DIP的受限展开、刷新和重启恢复已进入真实进程回归；最终明细见 [验证记录](VERIFICATION.md)，实现边界见 [R2实际合同](M04_EXPANSION_R2.md)。无新schema，无临时高度持久字段，原owner/bottom/输入捕获未改。

当前Release为out/verify/v3-04-m04-07-r2-down-first/app，新人工配置同名修订目录。旧求解器在正常下扩失败后直接接受纯上全高，跳过下方间隙；现先求下容量再上补足，定向修订前10失败/14通过，修订后完整356通过。原生down-first PID16544验证d132/u408、端点392..992高600；受限/不足/同步归位也通过，详见验证记录。

下一步启动脚本默认expand-down-first（expand-up保留别名），确认此次方向与收起观感，再合并余项人工；不把自动结果当真实鼠标PASS。完整 [策略矩阵](../../20_Architecture/POSTER_SPATIAL_STRATEGY_MATRIX_V1.md)已与三份权威需求同步，并明确自动收展、最后意图队列、完整减少动画和后续空间入口的未完成边界。旧R1/R2数据和证据保留，未commit/push，不提前进入M05。

## M04-07 R2 策略确认阶段历史（当时仅文档）

用户反馈下方B贴底不可推时A无法展开，确认三档回退和最小高度语义。已同步画像RP-SP-05/06/08、视觉§7.5、运行表面§17、需求索引、空间架构与模块、制作计划/工作流及人工门。

新策略详见 [空间设计§5.1～5.4](../../20_Architecture/POSTER_SPATIAL_LAYOUT_AND_EXPANSION_V1.md#51-三档裁决与候选顺序r2)：完整尺寸候选下优先→向上→必要上下组合，完整均失败才取合法剩余高度；最小值按当前布局的完整标题区域、一行完整Item与必要页脚，临时高度不写回保存尺寸；不足则零布局变化、仅按钮轻抖。候选失败不等于请求失败，预算/状态/宿主错误不能伪装为空间不足。

下一步按 [细化工作流§4.2](../../30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md#42-m04-07-r2-展开回退修订执行卡)：01纯策略/度量夹具→02持久意图与有效投影→03运行/轻抖→04自动及集中人工。尤其审计PosterVisualWindow.TryReadSpatial用当前_bounds.Height作为展开高度的路径，防临时投影被后续场景当作新偏好；不只加一个高度clamp。

当前可执行仍R1，不支持R2；本轮未改src/tests/scripts、未build/test/启动、未改SQLite/配置/文件、未commit/push。G1～G4人工通过保留，R1同步观感未确认；R2实施后合并G6-R1/G7-R2及余项，不提前进入M05。

## M04-07 R1 最新交接（优先于下方阶段历史）

用户已明确确认G1～G4人工通过，另确认收起时邻居随释放空间同步归位，不应等源收起完成才回位。SpatialPushSolver旧版先输出source全部帧，再追加邻居回程，原因已由7个新时序测试失败复现。

R1先冻结安全归位子集，再逐个source收起帧做有界单调链式跟随，同一批次提交；不改变原资格、锁定、二次调整、CAS和Host层级。新11项（含200组性质场景），完整324/324、Release0warning0error。新增日志标明simultaneousFrames/returnOnlyFrames，不冒充屏幕逐帧截图。

产物`out/verify/v3-04-m04-07-r1/app/`。双收起PID42412：4次/56帧；B收起14帧中13帧联合移动、尾帧0。链式PID43036：6次/52帧，首组收起14帧联合移动、尾帧0；两者exit0/资源0。新人工目录`out/manual/v3-04-m04-07-r1/{return-sync,interaction,chain,removal}`，启动脚本已更新6个指纹、四场景预检通过，未自动点击菜单/删除或执行Win+D。

请先用`-Scenario return-sync`复测两张均收起、B在A上方时B展开推动A再收起的观感；再按 [集中记录表](M04_MANUAL_VALIDATION.md)继续G5～G10。G1～G4保留旧版通过，不要求全量重做；M04未整体关闭，不提前进入M05。当前无测试常驻，未commit/push，未改日常数据库或桌面文件。

## M04-07 准备阶段交接（历史）

已准备interaction/chain/removal三套全新隔离配置，均revision3/三张；`scripts/start-m04-07-manual.ps1`锁定6个二进制指纹，拒绝已有进程与配置缺失，不自动改库/确认/定时退出。语法和三场景CheckOnly通过，没有启动实际应用。

computer-use技能及guidance/api/confirmations已完整读取，初始化及重置重试均报CreateProcessWithLogonW1385（kernel33072/5816）。无窗口列表/截图/输入证据，按技能停止工具交互，不用自写注入绕过。请用户启动interaction，先反馈G1～G4；其余见 [集中人工记录表](M04_MANUAL_VALIDATION.md)。删除只在removal夹具由用户确认，Win+D也由用户按下。

本轮没有修改生产代码/重建Release、没有重跑自动smoke充当人工门，没有commit/push。M04未关闭；收到反馈后核对对应日志/资源/版本，再决定PASS或定位失败，不进入M05。

## M04-06 最新交接（优先于下方阶段历史）

已接共享PosterInputRouter：实例ID/epoch、已呈现几何版本、唯一capture、真实视觉HWND顺序命中与旧版本失效。保留既有原生捕获/owner/bottom及空间求解；局部失败实例无可用输入，正常实例继续就绪。

中心与海报“...”已接同一删除确认服务。确认固定目标/实体/目录，取消默认；目标/目录变化、中心关闭、环境恢复和退出使ticket失效。删除只经现有RemovePosterCommand，不新建状态权威/迁移，不操作真实文件。真实文件任务阻止和找回路径待V3-05，新增入口及完整中心仍按后续计划。

最终313/313（新增25）、Release0warning0error；确认PID19540：3个对话框、3个就绪输入cluster、revision保持3、带框退出0/资源0。空间PID24776：6次收展/77帧、exit0/资源0。实际点击删除/鼠标命中/层级观感未代填PASS。详细产物指纹、启动和集中人工见 [M04-06合同](M04_INPUT_CONFIRMATION.md)。

保留M04-02..05已有dirty修改；本轮未commit/push，基线a6ac6cf不变。测试仅out内隔离配置和独立临时SQLite，无日常数据库/文件/快捷方式修改。下一项M04-07集中门，不提前扩展M05。

## M04-03..05 最新交接（优先于下方阶段历史）

已接原有16ms拖缩/capture、连续截停/滑动、完整读集松手提交；保尺寸两段展开与逆收起；有界垂直连锁推让、安全子集条件归位。SQLite追加v5，layout_generation独立于实体版本，移动后回到原点也失去旧归位资格；布局/证明/消费/回执同事务。原生视觉窗与输入岛同批移动、失败整组恢复；原owner/bottom/Win+D保护不改。

[空间运行合同](M04_SPATIAL_RUNTIME.md)记录实现、288项、产物与命令。实际链式PID24596：6次/77帧；普通三海报PID46980：4次/56帧；均exit0/资源0。初次原生SWP_DEFERERASE参数87、收起180px时滚动轨道越界、迁移提交前形状验证缺口都已修正并保留失败记录。

本轮只用隔离配置，未改日常数据库/文件/快捷方式；未commit/push。新建/显示/导入/异常恢复放置与隐藏态配置入口仍需后续统一，不声称全产品无重叠。下一项M04-06多cluster输入/确认→M04-07集中人工→M05参数；不回写未执行的人工PASS。

## M04-02最新交接（优先于下方历史记录）

已冻结并实现物理像素占位/保守DPI转换、布局与连续路径检查、8向候选、保尺寸两段展开、归位资格及完整目录读集/环境预检。新67项加入既有176项共243/243，Release完整通过。详细合同、边界、指纹及迁移登记见 [M04空间合同](M04_SPATIAL_CONTRACT.md)。

没有改Host/capture/schema，没有启动实际窗口或操作用户数据。本项纯数学门不新增人工矩阵；M04-01短人工和M03余项仍保留到各自/集中门。旧固定顶边/自动夹取测试加Legacy标签继续运行，不能作为新空间产品验收。

下一项03将求解候选接入现有拖缩，做截停/沿边滑动、不穿透/不丢capture；04再接两段展开/输入同步，05再做连锁推让、条件归位和持久布局代次。不得将本轮纯端点批次预检说成实际推让或跨系统原子成功。手势“移出又移回”需累计调整事实，不能靠坐标相等跳过因果失效。

Git基线a6ac6cf已在上轮同步远程；本轮未commit/push。以下“未commit/push”为当时历史记录，不代表基线尚未同步。

## M04-01最新交接（优先于下方历史记录）

已读DeskBox吸附、组移动和展开计算/测试，新增 [来源吸收记录](DESKBOX_SPATIAL_REFERENCE.md)，明确复用点/不照搬分支/commit和指纹。没有复制参考代码或启动DeskBox。已实现中心列表、进程内SelectedPosterId、冻结目标/版本的锁定/收展/显隐命令和运行状态；现有Host/schema/M03会话不重写。见 [M04-01合同](M04_CONTROL_CENTER_SELECTION.md)。

176/176；最终out/verify/v3-04-m04-01/app/ Release0warning0error。最终smoke三张PID20360/零张39740：中心目录ID完整、唯一窗口/监听、exit0/resources0。首次26340为列表焦点优化前证据。环境elevated=true。computer-use两次初始化因Windows沙箱1385失败，实际GUI点击未执行，短人工仍待确认。

已准备out/manual/v3-04-m04-01三张隔离配置；命令及5项短门见合同§4。当前无测试实例常驻。下一开发项M04-02纯空间合同/夹具，继而碰撞、保尺寸展开、推动与条件归位；不把本版拿来验收尚未接入的空间功能。未commit/push，保留此前dirty改动。

## 上轮需求补充与设计工作卡（历史）

用户反馈多海报可正常交互，但中心只有A；新增不可重叠、收展推让、保尺寸三种展开。两项明确裁决：已锁定者不能被推、作为固定障碍；未被再次调整者条件归位。已修订画像/视觉/运行需求、架构合同/模块、总计划和细化工作流，详见 [空间设计](../../20_Architecture/POSTER_SPATIAL_LAYOUT_AND_EXPANSION_V1.md)。本次只改文档，没有改代码/SQLite/桌面文件，也未commit/push。

中心原因：StateProjectionCoordinator兼容别名优先默认/首张，ControlCenterWindow只绑定projection.Poster；集合和会话并未丢B/C。下一开发项M04-01先建最小目录/稳定SelectedPosterId，原04-04复用选择器；随后M04-02..07完成空间合同、碰撞、保尺寸展开、推让/归位、输入/确认和集中回归。不要为补目录重新写M03 registry。

用户没有逐项报告Explorer/Win+D等，保留该人工证据缺口至M04-07；不声称M03完整PASS。Space实现前须给无解/锁定/满屏/历史重叠/二次修改/目录幻影和多窗口物理失败建立夹具。旧固定顶边/缩小适配是待替代代码，不因本轮文档更新而已修复。

## M03 实现交接（历史证据，当前动作以上方工作卡为准）

用户同意推进 M03。本轮已建立 Application/PosterSessionRegistry、IPosterSession/Factory、按ID/epoch隔离的ledger/log和WinUiPosterSession；App从单 `_poster` 切换为共享环境调度＋0..N会话。独立隐藏顶级监听锚点在零张时仍工作，单张原有防护保留。删除/隐藏取消生命周期，失败仅隔离目标，重建用新epoch；退出等待共享恢复取消完成。初始化只在bootstrap进行，既有空目录不补造默认。

证据：163/163；out/verify/v3-04-m03/app/ 完整Release 0 warning/error。最终PID：40316（三张）、8876（零张）、34984（首次一张）、36364（恢复等待中退出），均exit0/resources0；前三者1中心、2次show复用、1共享监听。测试环境为elevated，不冒充普通用户或完整多屏认证。最初发现的IntPtr日志序列化错误已修复并增加测试，失败日志保留。

立即动作：按 [M03合同 §4](M03_SESSION_RECOVERY.md#4-短人工门与启动命令) 的三张隔离配置执行短人工门；之后 M04。不重复全量安装/DPI；未主动重启Explorer；没有把自动窗口读回当人工视觉PASS。M05还负责去除固定NIGHT OPS文案，M06完善跨实例端到端夹具，原04-04才建立完整管理/预览入口。

此轮未 commit/push；保留前轮dirty文件。实际用户数据库/桌面文件未改；隔离配置与日志留在out/verify/v3-04-m03。新增/删除按钮尚未提供；不要通过日常库注入配置代替UI合同。

## M02 历史交接（当前以顶部 M03 工作卡为准）

生产 StateProjectionCoordinator 已改为 IStateCommandRepository 同事务快照/实体读集 CAS，新增 StateCommandPolicy、ScopedStateContracts、SqliteStateCommandRepository。SQLite 追加 v4：Settings 实体版本与持久命令回执；v1..3 指纹未变。三端 PostersById/有序目录/changed IDs/托盘计数与锁定汇总已实现；托盘原“锁定全部”接入批量事务。实际合同见 [TARGETED_COMMAND_PROJECTION_V2](TARGETED_COMMAND_PROJECTION_V2.md)。

App/海报现有操作传目标 EntityVersion，窗口按自身 ID 应用投影；中心及托盘支持空目录，不强制造默认。删除影响预览绑定 ID/实体/目录版本，取消不提交，删除不动文件。新增/删除 UI、实际多会话和动态显隐恢复尚未实现。

产物：out/verify/v3-04-m02/app/；测试结果：out/verify/v3-04-m02/results/m02.trx。最初旧版 PID 8600 占用单实例，未擅自终止；随后旧版退出，隔离新版 smoke 已通过（PID 40212，1 中心/1 海报、2次显示、退出资源0）。smoke 脚本已改为必须指定隔离 DataRoot，普通启动路径不变。

立即动作：用同一测试产物确认中心/托盘锁定、收起顶部锚点、拖缩后投影更新、中心关闭重开、点击/刷新/普通窗口覆盖/Win+D/正常退出。不要重新要求全量 DPI/安装矩阵。下面是已通过的 smoke 复现命令（PowerShell 7.4+）：

```powershell
& 'G:\A_\Tools\DesktopUp_V2\scripts\smoke-control-center.ps1' `
  -ExecutablePath 'G:\A_\Tools\DesktopUp_V2\out\verify\v3-04-m02\app\DesktopUp.exe' `
  -DataRoot 'G:\A_\Tools\DesktopUp_V2\out\verify\v3-04-m02\smoke-data'
```

M03 工作准备：建立 PosterSessionRegistry，以 PosterId 消费集合投影、创建/停止会话；去除 App._poster 的单实例持有方式，共享 Shell/显示发现及恢复节流。按配置恢复0..N，首启只一张；每实例账本、删除/隐藏/局部失败与恢复取消。沿用 DesktopLayerAttachment 和已通过的拖缩，不借迁移重写算法。M03 不在本轮提前实施。

旧 GetOrCreateDefault、M01 global-CAS 仓储和单 Poster projection 合同保留为兼容/历史证据；生产采用新端口，不再用失败回退拼接混合快照。新 command 回执与 legacy operation ID 相互防冲突。未 commit/push，保留先前 dirty 修改。

## 已验证与未完成（截至 M02 的历史状态）

用户确认 V3-04-03 R1 人工通过；托盘开窗资源名修复、97/97 合同测试与真实开窗 smoke 为既有证据。V3-01/02/03 单海报宿主和交互保留。当前代码尚未支持多个运行海报、真实内容收纳、生产主题或热度/AI。

M01 数据/仓储门已完成：schema v3、目录 list/create/clone/remove、独立身份/显隐/层序、实体版本与 profile 初始化标志；117/117 合同测试、完整 WinUI Release 构建 0 warning / 0 error。没有迁移用户真实数据库、操作桌面文件或新开实际窗口。保留此前未提交的托盘修复，不自动提交或推送。图目录 V3_CURRENT 仍是旧单海报快照。

## 新权威链

1. [需求索引](../../../00_Catalog/REQUIREMENTS_INDEX.md)：七份需求已原位修订。
2. [通用多海报合同](../../20_Architecture/GENERAL_CONTENT_MULTI_POSTER_CONTRACT_V1.md)：数据、版本、实例/共享、文件归属、主题与参考来源。
3. [总计划](../../30_Plans/DESKTOPUP_V2_V3_IMPLEMENTATION_PLAN_V1.md) 与 [细化工作流](../../30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md)：保留原阶段，插入 M 前置，逐项出口与依赖。
4. [代码锚点](CODE_ANCHORS.md)、[验证记录](VERIFICATION.md)：真实实现与证据范围。

## M01 完成记录

输入为 schema v2、原有仓储及通用多海报合同 §3/4。实际合同见 [POSTER_CATALOG_SCHEMA_V3](POSTER_CATALOG_SCHEMA_V3.md)；验证和产物指纹见 [VERIFICATION](VERIFICATION.md)。

产出：
- catalog/list/create/clone-configuration/remove 的数据和 repository 合同；
- 每实例身份、显隐/内部层序、实体版本，以及 profileInitialized（区分首次启动与零海报）；
- 新增迁移，不修改 v1/v2 migration/hash；旧 poster-main 几何/锁定/收起完整保留；
- 真实 SQLite 测试：新增/查询/删除/克隆新身份、删至零重启、旧库迁移与备份、失败回滚/损坏保留、并发冲突/幂等。

本项不做：真实多窗口、控制中心新画布、文件移动、完整主题或热度模型。集合/主题先冻结最小关联与兼容接口，正式内容实体按 V3-05 进入。

## M02 工作卡（本轮实现前输入，完成状态以上方交接为准）

输入：M01 的 IPosterCatalogRepository、PosterStateSnapshot.EntityVersion、CatalogVersion/global revision；现有单 Poster command/query/projection。

- 输出：PostersById、目录/目标投影、changed IDs、Tray 汇总，唯一控制中心/托盘不复制。
- 用户补充的入口合同：中心/托盘共用新增；中心/海报“...”共用删除，必须准备目标名称/影响摘要并确认后重校验版本。确认 UI 在 M04、04-04 接入，托盘新增在04-05接入。
- 实现实体/必要读集 CAS；global revision 继续表示全局提交序列。M01 的目录仓储仍是 global CAS，不能假定 A/B 无关写入已无冲突。
- 批量锁定/显隐等纯状态事务整体校验；NotFound、Conflict、幂等和删除后晚到命令不能复活。
- 现有 GetOrCreateDefault 在已初始化零海报时会明确拒绝；M02/M03 必须用 catalog 初始化查询/会话，不能以异常恢复为由重新播种默认海报。
- 普通首次确无配置只生成一张默认；已有或显式导入的0..N方案按集合恢复。空初始化 API 仅供明确空方案/夹具，不是普通首启选项。
- 目录写结果的 Snapshot 是最新同事务读视图，CommittedRevision 是原提交；接投影时不得混淆。
- 验收：A/B 无关写入、同 A 过期版本、批量失败回滚、操作重放、删除晚到命令、各投影同 revision。

后续 M03 才实现多会话，M04 输入/层序，M05 参数，M06 夹具，M07 测量/人工门，再进 V3-04-04。M01 不要求重做既有 DPI/宿主人工矩阵；新多窗口能力仍未通过人工验证。

## 单文件习惯配置新增计划

需求画像 §32 / RP-PORT-01/02 已记录跨机应用；运行仍以 SQLite 为唯一权威，通过控制中心导出一致快照、导入预览确认后事务应用。文件不与数据库双写；不包含 Key、实际文件或机器句柄。

04-06-A..D 冻结格式/已有布局策略、导出、导入映射和实际跨机验证；基础内置主题参数先进入，自定义主题素材复用07-04，来源失效修复复用05。数量/位置/锁定/收起/显隐/主题/内容与排版偏好都在范围；屏幕或依赖不同需提示并安全映射/fallback。

本次只补需求/设计/计划，未实现便携格式、确认弹窗或新增入口；未运行新的功能测试，117/117 仍是 M01 既有证据。

## 必须保留的安全和产品边界

- 游戏海报是特色场景；通用内容与多海报是当前正式方向，不再退回“未来扩展”。
- 一个控制中心/一个托盘不因海报数增加而复制。
- 主题/配置独立，不等于共享源文件变化不传播，也不等于每张独立进程。
- 引用、映射、受管内容区分；删海报不删文件；journal 先于真实移动。
- 不为增加实例重写已验证的 DesktopLayerAttachment 或恢复旧 Qt/WebView 技术线。

## 前一轮仅文档校验（历史）

2026-09-07：检查 37 份 Markdown，本地链接失效 0、顶层章节编号重复 0；git diff --check（Docs/README 范围）退出码 0。现行入口/需求/架构/计划中旧“只创建一张”“多海报仍延期”“普通文件一律不能移动”及旧人工待复测状态已检查；历史审计和单海报实现证据明确保留原范围。没有运行新的功能测试，不增加多海报 PASS 声明。
