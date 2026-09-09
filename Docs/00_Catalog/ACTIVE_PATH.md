# DesktopUp V2 当前执行入口

最后检查：2026-09-09
当前分支：impl/poster-redesign-V3

| 层级 | 权威入口 | 状态 |
| --- | --- | --- |
| 当前实现/下一项 | [M07测量与安全门](../A_Project/40_Implementation/V3-04/M07_MEASUREMENT_AND_SAFETY.md) | IN PROGRESS；411项/构建/入口预检通过；5.1真实启动/退出通过，当前先做S1～S5人工 |
| 保留人工门 | [M04-07](../A_Project/40_Implementation/V3-04/M04_MANUAL_VALIDATION.md) | G1～G4通过、收展策略体验认可；动画延期，未逐项确认的安全门保留 |
| 需求 | [七份需求](REQUIREMENTS_INDEX.md) | 通用内容/多海报产品方向已确认 |
| 架构 | [总体架构](../A_Project/20_Architecture/DESKTOPUP_V2_V3_ARCHITECTURE_V1.md) | 设计基线已同步 |
| 合同 | [M02 目标化命令合同](../A_Project/40_Implementation/V3-04/TARGETED_COMMAND_PROJECTION_V2.md) | 数据、实体读集 CAS、三端投影已实现 |
| 模块 | [模块目录](../A_Project/20_Architecture/DESKTOPUP_V2_V3_MODULE_CATALOG_V1.md) | 多实例端口/共享边界已补充 |
| 总计划 | [制作计划](../A_Project/30_Plans/DESKTOPUP_V2_V3_IMPLEMENTATION_PLAN_V1.md) | M07 IN PROGRESS / CURRENT-ENVIRONMENT & MANUAL PENDING |
| 中心实际合同 | [M04-01](../A_Project/40_Implementation/V3-04/M04_CONTROL_CENTER_SELECTION.md) | 全目录/目标选择/版本绑定命令已实现，含启动方式 |
| 空间设计 | [碰撞/展开/归位](../A_Project/20_Architecture/POSTER_SPATIAL_LAYOUT_AND_EXPANSION_V1.md) | R1同步归位保留，R2完整候选/临时高度/最小高度反馈已接线，人工待确认 |
| 空间实际合同 | [M04-02](../A_Project/40_Implementation/V3-04/M04_SPATIAL_CONTRACT.md) | AUTO PASS；占位/路径/展开/归位资格/完整读集与迁移登记 |
| 空间运行接入 | [M04-03..05](../A_Project/40_Implementation/V3-04/M04_SPATIAL_RUNTIME.md) | 碰撞/保尺寸展开/推让归位/v5及真实批次；人工待确认 |
| 执行细节 | [细化工作流](../A_Project/30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md) | 输入、依赖、任务、失败与出口 |
| 输入确认接入 | [M04-06](../A_Project/40_Implementation/V3-04/M04_INPUT_CONFIRMATION.md) | 多cluster身份/唯一capture、共用删除确认、313/313与真实进程证据；人工待确认 |
| 交接 | [V3-04 HANDOFF](../A_Project/40_Implementation/V3-04/HANDOFF.md) | CURRENT |
| 当前代码 | [src](../../src/) | 空间会话、连续碰撞、原尺寸收展、组求解及原生批次/输入岛、SQLite v5 |
| 当前证据 | [VERIFICATION](../A_Project/40_Implementation/V3-04/VERIFICATION.md) | 411/411、Release0警告0错误；5.1三海报真启动/退出通过；当前环境测量单列，不冒充普通用户认证；M05/M06历史通过保留 |

## 当前动作

当前先从PowerShell5.1运行`scripts/start-m07-manual.ps1 -Scenario interaction`，完成S1/S2，再按M07合同切换chain/removal。已有DesktopUp先托盘正常退出，人工与测量不能同时运行。管理员权限不再拦截启动；无需更换Shell或修改UAC。完整1/3/6资源矩阵仍保留，另行收口；结果须标明实际环境，不将兼容回归当人工PASS。

用户已正常退出旧版，2026-09-09先补M05参数两轮/空间回归，再执行M06 Exercise PID44928、单实例短进程PID48160、Restart PID47076，全部通过；合同复跑405/405。六张卡仍是DEMO ITEMS，M06隔离数据现为A/B两条且仅A显示，不能再直接Exercise；证据/脚本/产物见M06合同。本轮无生产代码修复、无日常配置修改。

M05/M06已关闭；当前M07测量合同/集中安全门→原04-04管理入口→V3-05真实内容。M07未关闭，不重做空间求解或扩展全主题平台。R2策略见[完整矩阵](../A_Project/20_Architecture/POSTER_SPATIAL_STRATEGY_MATRIX_V1.md)，用户认可体验，动画延期，未确认人工门不消失。

当前 M03已实现PosterSessionRegistry、独立会话/取消、共享发现恢复及零目录监听；163/163、Release和0/1/3实际进程/等待中退出为历史自动证据。用户本次确认多海报可正常交互，未逐项确认的普通窗口/Win+D/刷新/Explorer重建证据保留到M04-07集中门；启动说明仍见 [M03合同](../A_Project/40_Implementation/V3-04/M03_SESSION_RECOVERY.md)。不补写M03全项PASS。

已同步用户后续需求：首次无配置一张默认；中心/托盘新增，中心/海报“...”删除并确认后果；单文件跨机习惯配置。M02 已补统一命令和版本绑定的删除摘要，M04-06已接中心与海报删除确认；新增入口仍待后续；配置导入导出按04-06-A..D，主题素材复用07-04。详见 [需求画像 §32](../A_Project/10_Requirements/DESKTOPUP_V2_REQUIREMENTS_PROFILE_V1.md)。

M04-03..05空间实现保留；M04-06已接输入身份/唯一capture、原生路由与共用删除确认。313/313，最终确认PID19540和链式PID24776均exit0/资源0。以上为历史基线；本轮M05不重新实现这些功能。日常数据未修改，历史实现详见 [输入与确认合同](../A_Project/40_Implementation/V3-04/M04_INPUT_CONFIRMATION.md)。

## 已完成与本轮边界

- V3-01/02/03 已关闭；V3-04-01/02 SQLite/仓储基础及人工门通过。
- V3-04-03 R1 修复 OutputPath 分隔符导致的 XBF/PRI 资源名错误；真实开窗 smoke、97/97 既有合同测试与用户本次人工确认保留，不再等待旧托盘复测。
- M01 的实体/目录仓储、兼容迁移与故障回归已完成；未迁移用户真实数据库、未操作桌面文件。三张真实内容海报并存不在现有证据中。
- M02 追加 schema v4/持久回执，保留 v1..3；生产调用使用实体读集，三端消费同事务快照。M03 已新增实际多窗口，但没有文件收纳或改写 Host；M02 的历史边界不等于当前实现边界。
- 基线a6ac6cf已同步远程；M04-02..06修改均未commit/push。托盘修复已在基线，不再作为未提交旧任务。

## 禁止事项

- 不恢复旧 Qt/WebView/Win32/Axiom 试验，也不为多海报整体重写已通过的 Host。
- 不把 DeskBox 参考结果、设计图或结构数组当作 DesktopUp 多实例 PASS。
- 恢复账本、预演与补偿夹具完成前不开放实际文件移动。
- 主屏多海报与多显示器认证分开；不把扩展需求偷偷降回单海报。
