# DesktopUp V2 — Poster Redesign V3

2026-10-21 当前：海报整体 0.4～3 连续尺度、单项小数尺寸、动态交叉轴下限、局部悬停及尺度联动扩框两轮人工均通过，本批收口；无法安全扩框时尺度回弹且可继续手动调整外框。按授权同步功能分支、impl/develop 与精简 impl/main，打包结果见[当前交接](Docs/A_Project/40_Implementation/V3-04/HANDOFF.md)。不推进真实热度，历史未定因与未验环境继续保留。下方为阶段历史。

2026-10-21 当前：已验卸载诊断功能c8a6714已合入develop940ff96并同步精简发布main4e79035；20261021.2实际打包/隔离运行卸载验证通过，最终回到 `impl/develop`。历史卸载中断首因及P1/R6锁冲突仍未定，不推进真实热度。见[发布交接](Docs/A_Project/40_Implementation/V3-04/HANDOFF.md)与[后续系列计划](Docs/A_Project/30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md#2026-10-21-发布同步后的系列计划建议未开工)；下方为此前里程碑。

2026-10-20 当前：**本轮基础架构整理 USER ACCEPTED / CLOSED（R1～R8、P1～P3）**。现有业务职责、操作/会话生命周期、呈现与同代命中、Worker及观察读取边界已收口；详见[最终复审与收口](Docs/A_Project/40_Implementation/Architecture/FOUNDATION_REMAINDER_PLAN.md#30-p2p3收口后的最终架构与成本复审2026-10-20)。架构成果 `3c2417e` 已提交推送并合入 `impl/develop@bbbe417`，精简发布线 `impl/main@fe5741d` 已同步推送，完成限定打包/隔离安装运行验证，最终回到 `impl/develop`；一次卸载中断重试成功但首因未定，另行保留记录；执行结果见[当前交接](Docs/A_Project/40_Implementation/V3-04/HANDOFF.md)。剩余读取/观察成本与 P1/R6 锁冲突未定因继续保留；真实登录自启及未验环境不补 PASS。真实热度不实施，动态模板、AI 和独立更新器未实现。以下为阶段历史。

2026-10-16 当前：[开机自启动](Docs/A_Project/40_Implementation/V3-04/PREVIEW_INSTALLER_DELIVERY.md#2026-10-16-v3-09-03开机自启动前移计划)已完成定向自动验证；“关闭与卸载”人工通过，真实登录自启按用户要求暂缓、保留未验。本轮获授权提交/推送impl/09-03-startup并合入、同步impl/develop，不更新impl/main；结果见HANDOFF。下一主线V3-06真实热度，本轮不实施。下方为历史阶段。

2026-10-16 最新：05-12余项＋09-07/08卸载归还四组集中人工USER PASS / 本批CLOSED；支持保留原位后重装，或先归还内容再卸载（原路径不可用/未知回系统桌面），冲突取消保留程序及剩余内容，无卡片受管文件也处理，映射目录和配置/位置清单保留。验收与样例收尾见[直接拖放§16.3](Docs/A_Project/40_Implementation/V3-05/DIRECT_TRANSFER_DELIVERY.md#163-人工通过与测试收尾2026-10-16)。当前仍`impl/05-12-content-lifecycle`，新批未提交/合入develop/推送，impl/main未更新；下一顺序见[系列计划](Docs/A_Project/30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md#2026-10-16-卸载归还收口后的系列计划)。下方“最终在develop”等为历史阶段，未验环境不补PASS。

2026-10-15 当前集成：控制中心外观UI1～3项人工通过，功能 `0a75494` 已合入 `impl/develop@d66265f` 并推送；同时将已验收的图标更换、增量呈现/悬停让位、原生文件操作/Win菜单/目录映射和统一外观同步至精简发布线 `impl/main@f80765e`。成品20261015.1完成构建、安装启动/配置保真和卸载的限定验证，见[发布记录](Docs/A_Project/40_Implementation/V3-04/PREVIEW_INSTALLER_DELIVERY.md#2026-10-15-develop里程碑与外观ui发布同步)；准确交接见[当前交接](Docs/A_Project/40_Implementation/V3-04/HANDOFF.md)。最终工作区在develop，构造卡片继续延期，本轮不开始新功能。

基础能力闭环/开发集成线：`impl/develop`；后续新功能一律从它派生独立开发分支，实现和集中验收通过后合回。既有历史线 `impl/poster-redesign-V3` 保留，发布源码线 `impl/main` 继续精简。功能分支及集成线保留完整开发资料与测试，发布精简不得反向合入；里程碑同步仍按[Git策略](Docs/Rules/GIT_STRATEGY.md)执行。安装包保留自选路径、可选桌面快捷方式与正常卸载，配置/收纳目录不随卸载删除；开发源默认仍从当前分支构建，安装预览版使用独立Preview配置。

2026-10-14 增量：图标优先提取原始资源中的最大有效帧，保守回退 Shell 填充画布，不裁正常留白；安装目录页之后提供“创建桌面快捷方式”选项，默认勾选、可取消。图标肉眼复验及安装向导人工往返仍独立记录，不以 Git 同步替代验收。

当前（2026-10-13）：全区域原生拖入、同/跨卷收纳、空白框选、外部移出同步、单一归属与原生 Shell 拖出已接线，D4-N 四组人工通过；05-14入口及六项界面减法已实现。普通点击穿透已按用户决定撤销，不再作为功能承诺。当前发布同步及验证见[交接](Docs/A_Project/40_Implementation/V3-04/HANDOFF.md)。`impl/main`仅保留发布源码；完整开发资料/测试留在本分支，不反向合入发布精简删除，规则见[Git策略](Docs/Rules/GIT_STRATEGY.md)。

2026-10-14构建/安装更新：新配置零海报、无测试卡片；每次普通启动都按当前目录判断：零海报打开控制中心，有海报后台启动，显式`--show-control-center`始终打开中心。新增默认“基础收纳”。运行脚本每次先构建当前源码。预览包通过`Package-Preview.cmd`制作，支持包含无关文件的自选安装目录；遇到应用专属名称冲突会拒绝。根目录保留启动器、卸载器、说明与`app/`、`licenses/`，运行依赖集中于`app/`。

配置与真实收纳文件独立于安装目录，默认收纳位于`%LOCALAPPDATA%\DesktopUp\Storage\<通道>\<配置身份>`，可在控制中心“全局 → 收纳位置”修改并迁移；卸载不删除用户数据。详见[打包入口](packaging/preview/README.md)与[本轮交付记录](Docs/A_Project/40_Implementation/V3-04/PREVIEW_INSTALLER_DELIVERY.md#2026-10-14-便携工具链与安装体验同步)。当前仍是未签名预览，不等于已完成跨机、多DPI、满盘和长时验证。

## 以下为历史阶段说明

状态（2026-09-30）：R5 / **R6 CORE CLOSED**；模块B/C/D与物理移除恢复门通过，雨夜窗边已接生产。唯一云标题/按钮、安全提速及核心收展组隔离已修订；[RR-06同包与人工余门待完成](Docs/A_Project/40_Implementation/V3-04/LR_SCENE_MODULE_EXECUTION.md#10-rr整改生产接线与同包验证2026-09-30)，BUG-031白条用户延期、未解决。源码检查点`5f753e1`及相邻文档保存当前状态；构建/2274合同复验通过，仍为DEMO，不代表稳定发布或真实文件接入。

先读[当前执行入口](Docs/00_Catalog/ACTIVE_PATH.md)、[R5收口与代码/计划审计](Docs/A_Project/40_Implementation/V3-04/LR_R5_CLOSEOUT_AND_PLAN_AUDIT.md)和[交接](Docs/A_Project/40_Implementation/V3-04/HANDOFF.md)。已批准顺序为R6组合→最小模块解耦→雨夜窗边→R7可靠性/配置保真→R8接入交接→V3-05真实内容。BUG-026停顿后续滚按用户决定延期。

历史[M07测量与安全门](Docs/A_Project/40_Implementation/V3-04/M07_MEASUREMENT_AND_SAFETY.md)保留对应版本证据；下方M03/M04测试数量和安排均为历史，不覆盖上述当前进度。现版验证范围、候选绑定和未验项以交接及验证记录为准。

历史自动基线：M04-06已接共享输入身份/唯一capture、实际HWND顺序路由及中心/海报共用删除确认；313/313、Release0警告0错误，真实确认框取消/关闭/带框退出与链式77帧回归通过。见 [输入与确认合同](Docs/A_Project/40_Implementation/V3-04/M04_INPUT_CONFIRMATION.md)。旧M04固定验证包不覆盖。

本分支已经移除旧 Qt、Win32 探针、Axiom 试验、生产代码和旧测试集。冻结需求继续保留；新的技术路线以开源 DeskBox 的 C#、WinUI 3、Windows App SDK、原生 Shell 边界和窗口生命周期实践为主要参考，并针对 DesktopUp 的透明海报、内容 Item、活跃度排版、主题热更新与 AI 控制平面进行扩展。V3-01 已建立五层工程、单实例启动意图、当前用户数据目录、结构日志、资源台账、最小托盘与按需控制中心。

## 阅读顺序

新agent可复制[冷启动提示词](Docs/00_Catalog/AGENT_COLD_START.md)中的启动段，按其当前阶段阅读顺序接手；以下为项目全貌资料入口。

1. [完整需求索引](Docs/00_Catalog/REQUIREMENTS_INDEX.md)
2. [V3 技术路线与总体架构](Docs/A_Project/20_Architecture/DESKTOPUP_V2_V3_ARCHITECTURE_V1.md)
3. [V3 模块分类与端口目录](Docs/A_Project/20_Architecture/DESKTOPUP_V2_V3_MODULE_CATALOG_V1.md)
4. [V3 制作计划](Docs/A_Project/30_Plans/DESKTOPUP_V2_V3_IMPLEMENTATION_PLAN_V1.md)
5. [V3-04 实现与验证](Docs/A_Project/40_Implementation/V3-04/VERIFICATION.md)
6. [当前执行入口](Docs/00_Catalog/ACTIVE_PATH.md)

## 参考与许可证边界

- 参考工程：`G:\A_\Tools\reference\DeskBox`；
- 当前参考源码声明为 `GPL-3.0-only`；
- V3 可以直接借鉴或移植其实现，但任何直接衍生代码都必须保留来源和许可证通知；如果分发衍生版本，应履行 GPLv3 的对应源码义务；
- DeskBox 名称、图标、品牌和发行包装不属于 DesktopUp 资产，不得复制为 DesktopUp 品牌；
- V3-01 未逐字复制 DeskBox 源文件，但已采用其依赖组合和托盘/生命周期构造模式并登记来源；
- 本探索分支以 `GPL-3.0-only` 发布，完整文本见 [LICENSE](LICENSE)，依赖通知见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。

## 构建与运行

```powershell
& .\scripts\setup.ps1 -ForPackaging
& .\scripts\build.ps1 -Configuration Release
& .\scripts\test.ps1 -Configuration Release
& .\scripts\run.ps1
```

在Windows 11 x64项目根目录执行，支持Windows PowerShell 5.1 / PowerShell 7。脚本按`global.json`与`scripts/toolchain.json`准备项目本地.NET SDK 10.0.112和NSIS 3.12，校验下载哈希，缓存至`out/toolchains/`；NuGet缓存至`out/nuget/packages/`。不要求替换系统SDK，不永久修改PATH。首次需联网；准备完整后可加`-Offline`。本机Windows原生构建/运行仍是验证基线，不以Docker替代WinUI和Shell验证。脚本设置并恢复原生命令UTF-8输出编码。

2026-10-14按用户要求清除了本机构建历史、工具链/包缓存及旧证据；此后首次构建会重新下载。旧文档中的`out/`路径仅为历史引用，不能认为产物仍可访问。同步阶段未重新生成这些产物。

M03历史：已接目录驱动的0..N真实会话，无配置首次一张、既有目录恢复、已初始化空目录保持零张；一个控制中心、一个托盘。163/163合同和对应Release结果属于该早期版本；新增/删除管理UI、多海报输入和主题参数的后续进度见当前文档。

自定义 Release 输出目录请使用脚本，以保证 WinUI XBF/PRI 的资源名称正确：

```powershell
& .\scripts\build.ps1 -Configuration Release -OutputDirectory "out\local-build\app"
& .\scripts\smoke-control-center.ps1 -ExecutablePath "$PWD\out\local-build\app\DesktopUp.exe" -DataRoot "$PWD\out\local-build\smoke-data"
```

直接使用 `dotnet build -o` 时，路径必须以目录分隔符结尾；不符合时构建报 `DUV3001`，避免生成可以启动但无法打开控制中心的产物。

smoke 现强制 DataRoot，通过子进程测试环境变量隔离数据库/日志；运行前必须正常退出已有 DesktopUp，避免单实例转发到旧版。普通直接启动仍使用真实用户库并执行版本迁移；本轮没有自动迁移用户实际库。

## 正式产品方向与下一步

最新执行顺序与里程碑见[LR计划§8](Docs/A_Project/30_Plans/V3_04_L_REMEDIATION_REVIEW_PLAN.md#8-当前出口和最近下一步)。下方M03/M04记述为历史安排，不是当前任务。

通用桌面内容收纳、多海报、一个控制中心、一个托盘、自定义独立风格化主题、热度排版、AI 辅助定制。游戏图标海报保留为特色用途，不限制应用、文件、文件夹和 Shell 桌面入口。

[通用多海报合同](Docs/A_Project/20_Architecture/GENERAL_CONTENT_MULTI_POSTER_CONTRACT_V1.md)、[细化工作流](Docs/A_Project/30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md) 与 [M02 实际合同](Docs/A_Project/40_Implementation/V3-04/TARGETED_COMMAND_PROJECTION_V2.md) 已同步。M03 实现与启动方法见 [会话恢复合同](Docs/A_Project/40_Implementation/V3-04/M03_SESSION_RECOVERY.md)。短人工门后进入 M04 多 cluster 输入/层序，再推进参数合同和多海报控制中心；不重复创建状态权威。
