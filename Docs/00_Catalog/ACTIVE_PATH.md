# DesktopUp V2 当前执行入口

最后检查：2026-09-09
当前分支：impl/poster-redesign-V3

| 层级 | 权威入口 | 状态 |
| --- | --- | --- |
| 当前实现/下一项 | [M07动画R1](../A_Project/40_Implementation/V3-04/M07_ANIMATION_R1.md) | AUTO PASS；488项、两组参数原生回归及动画中退出；当前屏幕留缝/顺畅度人工待确认，M07整体OPEN |
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
| 当前代码 | [src](../../src/) | 连续空间锚点/关系、原生批次/输入岛、SQLite v6同事务连续元数据；旧宿主及收展策略复用 |
| 当前证据 | [VERIFICATION](../A_Project/40_Implementation/V3-04/VERIFICATION.md) | 488/488、Release0警告0错误；默认240帧/自定义400帧、exit0/资源0；不替代真实呈现人工与完整资源矩阵 |

## 当前动作

2026-09-09 Git检查点：用户已授权将当前菜单修复、连续空间模型及启动可配置动画整体提交并同步至`origin/impl/poster-redesign-V3`。提交前复验488/488、PowerShell5.1入口预检通过；M07/动画视觉人工仍待确认。下方各开发记录中的“无commit/push”保留为当时历史，不再表示本轮没有同步授权。

当前优先：用户认可连续收展/推让/复原，另指出动画中约30px空隙，授权优化并提供启动配置。[动画R1](../A_Project/40_Implementation/V3-04/M07_ANIMATION_R1.md)已接渲染回调、受限步长细分和XAML/几何/裁切协调；不改空间模型或schema。新入口`scripts/start-m07-animation.ps1`，配置`config/poster-animation.json`，仅启动读取。下一步交屏幕留缝/顺畅度人工验收；旧连续性及菜单包均保留，当前无commit/push。以下为历史过程，不以之前80帧读回替代当前像素观感验证。

2026-09-09最新优先：已按用户授权完成[连续行为合同](../A_Project/20_Architecture/POSTER_SPATIAL_CONTINUITY_CONTRACT_V1.md)、[连续场景矩阵](../A_Project/20_Architecture/POSTER_SPATIAL_CONTINUITY_MATRIX_V1.md)、纯模型与生产接入自动门。完整归位或原地不动，不部分回收；自身收展/系统后继推让不再一概清除关系。最终原生PID42012通过，人工PID47704已就绪，等待两种嵌套关闭顺序鼠标/观感反馈；入口`scripts/start-m07-continuity.ps1`。M07剩余门和1/3/6资源基线仍OPEN。以下诊断/修复段按时间保留，其中“本轮仅诊断、未修订实现”等不再是当前状态。

2026-09-09最新用户反馈：“…”交互已正常，菜单 R1 原故障人工 PASS；自动原生6场景未完成的事实保留，不再要求重复确认同一故障。另报[嵌套收展归位缺口](../A_Project/40_Implementation/V3-04/M07_NESTED_POSTURE_RETURN_DIAGNOSIS.md)：C展开→B展开→C收起时B/A被条件归位排除；已由PID44940日志与完整快照相等检查确认根因。本轮仅诊断/登记，拟区分姿态变化与位置意图，尚未修订生产实现。M07仍OPEN；下段人工PENDING为本次反馈前历史。

2026-09-09修复最新：[菜单R1](../A_Project/40_Implementation/V3-04/M07_MENU_REPAIR_R1.md)已按用户授权接入共享菜单生命周期/输入岛让位/Popup桌面内排序，最终433/433、Release0警告0错误、PS5.1预检通过。中间原生样本层级与输入位通过，但点位被外部程序遮挡，不算完整PASS；等待露出桌面后的最终原生6场景及用户鼠标复测。新入口`scripts/start-m07-menu-repair.ps1`；M07仍OPEN。下段为修复前诊断结论，不再代表“修复未实施”。

2026-09-09最新：[菜单真实输入取证](../A_Project/40_Implementation/V3-04/M07_MENU_INPUT_DIAGNOSIS.md)已确认本次 B 样本：PID18716 的实际 down/up 经输入岛派发为 GHOST SIGNAL；x2232/2248 边界对应卡片/滚动条输入区，Popup 低于 B 输入岛及 C。根因是桌面窗口组未纳入临时菜单的输入/层级协调，修复方案已记录、尚未实施。修复前11e7f1a已同步远程；420/420属于前轮诊断包，不是修复PASS。M07仍OPEN，下方普通场景暂不推进；不再要求重复取得同一失败点。

当前先从PowerShell5.1运行`scripts/start-m07-manual.ps1 -Scenario interaction`，完成S1/S2，再按M07合同切换chain/removal。已有DesktopUp先托盘正常退出，人工与测量不能同时运行。管理员权限不再拦截启动；无需更换Shell或修改UAC。完整1/3/6资源矩阵仍保留，另行收口；结果须标明实际环境，不将兼容回归当人工PASS。

用户已正常退出旧版，2026-09-09先补M05参数两轮/空间回归，再执行M06 Exercise PID44928、单实例短进程PID48160、Restart PID47076，全部通过；合同复跑405/405。六张卡仍是DEMO ITEMS，M06隔离数据现为A/B两条且仅A显示，不能再直接Exercise；证据/脚本/产物见M06合同。本轮无生产代码修复、无日常配置修改。

M05/M06已关闭；当前M07连续空间补强及集中安全门/测量收口→原04-04管理入口→V3-05真实内容。只补本次连续关系，不重写已验证宿主、不扩展全主题平台。R2策略见[完整矩阵](../A_Project/20_Architecture/POSTER_SPATIAL_STRATEGY_MATRIX_V1.md)，动画润色延期，未确认人工门不消失。

当前 M03已实现PosterSessionRegistry、独立会话/取消、共享发现恢复及零目录监听；163/163、Release和0/1/3实际进程/等待中退出为历史自动证据。用户本次确认多海报可正常交互，未逐项确认的普通窗口/Win+D/刷新/Explorer重建证据保留到M04-07集中门；启动说明仍见 [M03合同](../A_Project/40_Implementation/V3-04/M03_SESSION_RECOVERY.md)。不补写M03全项PASS。

已同步用户后续需求：首次无配置一张默认；中心/托盘新增，中心/海报“...”删除并确认后果；单文件跨机习惯配置。M02 已补统一命令和版本绑定的删除摘要，M04-06已接中心与海报删除确认；新增入口仍待后续；配置导入导出按04-06-A..D，主题素材复用07-04。详见 [需求画像 §32](../A_Project/10_Requirements/DESKTOPUP_V2_REQUIREMENTS_PROFILE_V1.md)。

M04-03..05空间实现保留；M04-06已接输入身份/唯一capture、原生路由与共用删除确认。313/313，最终确认PID19540和链式PID24776均exit0/资源0。以上为历史基线；本轮M05不重新实现这些功能。日常数据未修改，历史实现详见 [输入与确认合同](../A_Project/40_Implementation/V3-04/M04_INPUT_CONFIRMATION.md)。

## 已完成与本轮边界

- V3-01/02/03 已关闭；V3-04-01/02 SQLite/仓储基础及人工门通过。
- V3-04-03 R1 修复 OutputPath 分隔符导致的 XBF/PRI 资源名错误；真实开窗 smoke、97/97 既有合同测试与用户本次人工确认保留，不再等待旧托盘复测。
- M01 的实体/目录仓储、兼容迁移与故障回归已完成；未迁移用户真实数据库、未操作桌面文件。三张真实内容海报并存不在现有证据中。
- M02 追加 schema v4/持久回执，保留 v1..3；生产调用使用实体读集，三端消费同事务快照。M03 已新增实际多窗口，但没有文件收纳或改写 Host；M02 的历史边界不等于当前实现边界。
- 修复前基线11e7f1a已同步远程；后续菜单修复及本次连续空间模型、schema v6、测试/文档均为本地变更，未再次commit/push。托盘修复已在历史基线，不再作为未提交旧任务。

## 禁止事项

- 不恢复旧 Qt/WebView/Win32/Axiom 试验，也不为多海报整体重写已通过的 Host。
- 不把 DeskBox 参考结果、设计图或结构数组当作 DesktopUp 多实例 PASS。
- 恢复账本、预演与补偿夹具完成前不开放实际文件移动。
- 主屏多海报与多显示器认证分开；不把扩展需求偷偷降回单海报。
