# DesktopUp V2 — Poster Redesign V3

当前工作分支：`impl/poster-redesign-V3-wb`

状态（2026-09-27）：`LR-R5 CORE CLOSED；R6 READY / NOT STARTED`。当前仍为DEMO内容；R5核心收口不等于最终可靠性验收或真实文件接入完成。

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
& .\scripts\build.ps1 -Configuration Release
& .\scripts\test.ps1 -Configuration Release
& .\scripts\run.ps1
```

在项目根目录执行上述命令，需满足`global.json`的.NET SDK及Windows构建依赖。当前机器的隔离工具链/候选运行方式见交接；`out/`验证包、日志和体验数据库仅保留本机，不随Git分发。

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
