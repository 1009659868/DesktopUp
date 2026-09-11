# DesktopUp V2 文档入口

2026-09-11最新：[R2分线](A_Project/40_Implementation/V3-04/M08_INPUT_ARBITRATION_R2.md)：外部点击归属已修订；[组合交互](A_Project/40_Implementation/V3-04/M08_EXPANSION_DRAG_CONTRACT.md)合同/28项纯模型已完成，边界优先级V2-TBD-018待确认，空间证明/CAS/生产未接入。全量880/880、独立构建通过；人工入口仍R1，未替换运行包。有效资源0/36，06/07余门未关闭。

最后更新：2026-09-11（M08-07，主线不变）。当前分支：`impl/poster-redesign-V3`。
当前阶段：**M08进行中：01～05分项PASS，06余门OPEN，07基础三项及R1进程通过/待人工复验与采样**；M07稳定App未换包，不冒称全部原生体验、性能预算或混合DPI已达标。

## 当前入口

- [当前执行路径](00_Catalog/ACTIVE_PATH.md)：A → B → C 与最近步骤。
- [当前交接](A_Project/40_Implementation/V3-04/HANDOFF.md)、[代码锚点](A_Project/40_Implementation/V3-04/CODE_ANCHORS.md)、[验证日志](A_Project/40_Implementation/V3-04/VERIFICATION.md)。
- [白条修复与清理证据](A_Project/40_Implementation/V3-04/M07_ANIMATION_R4.md)；[同类问题排查方法](A_Project/20_Architecture/PRESENTATION_DIAGNOSIS_PLAYBOOK_V1.md)。
- 稳定动画入口：`scripts/start-m07-animation.ps1`；[启动配置](../config/README.md)。R2/R3/独立诊断入口已退出活动目录，不再推荐旧包验新功能。

## 权威顺序

1. 用户最新明确决定。
2. [七份冻结需求](00_Catalog/REQUIREMENTS_INDEX.md)。
3. [总体架构](A_Project/20_Architecture/DESKTOPUP_V2_V3_ARCHITECTURE_V1.md)、[模块目录](A_Project/20_Architecture/DESKTOPUP_V2_V3_MODULE_CATALOG_V1.md)。
4. [V3 制作计划](A_Project/30_Plans/DESKTOPUP_V2_V3_IMPLEMENTATION_PLAN_V1.md)、[细化工作流](A_Project/30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md)。
5. 对应阶段的实现合同、验证和交接；历史失败记录不能覆盖当前状态。

## ABC归属与历史记录

ABC是逻辑分层，不要求搬迁现有目录。A维护产品需求、全局约束/架构；B维护模块职责、端口及代码映射；C维护任务顺序、实现/验证证据和交接。

- 产品决定/未决/延期以[统一索引](A_Project/10_Requirements/REQUIREMENTS_RISKS_AND_OPEN_QUESTIONS.md)为准；C层只引用决定ID及安排实施，不另行裁决产品行为。
- [细化工作流](A_Project/30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md)维护当前子项状态；总计划维护阶段依赖，HANDOFF是最新交接摘要，登记表只导航，不各自维护另一套待办。
- 验证日志和旧版任务卡保留当时状态/产物。HISTORICAL不等于无用，PLANNED不等于已实现；旧“待验”不能覆盖后续证据，后续PASS也不能扩大到未经验证的环境。

## 产品与边界

正式方向：通用桌面内容收纳、多海报、一个控制中心、一个托盘、独立风格主题、热度排版、AI 辅助定制。游戏图标海报是特色起点，不限制通用文件/目录/应用/Shell 内容。
技术路线沿用 DeskBox 的 Windows 原生经验与 WinUI；不恢复旧 Qt/WebView/Axiom 试验。当前六卡仍是 DEMO ITEMS，未实现真实文件收纳。

V3-01/02/03、V3-04-01/02/03及M01～M06已有分项证据；本轮[M07](A_Project/40_Implementation/V3-04/M07_MEASUREMENT_AND_SAFETY.md)集中S1～S5安全和测量均已通过，功能欠项不代填完成：减少动效/手动连点队列按需登记，自动收展转入M08。
最近顺序：M08 R1人工复验及06/07余门 → 原V3-04-04基础预览/完整必要管理 → 04-05托盘 → 04-06配置/恢复及04-07/08生命周期 → V3-05真实内容安全闭环。
[按需优化登记](A_Project/30_Plans/ON_DEMAND_OPTIMIZATIONS.md)没有排期，不阻塞上述主线。当前已有纯模型、设置数据、原生观察与共享自动调度/空间事务；已有最小设置UI，完整体验门仍待06/07，当前R1普通原生自动smoke已补验，真实恢复与视觉余门待续。

需求保留：空配置首次仅一张默认海报；中心/托盘新增，中心/海报菜单删除并确认后果；单文件跨机习惯配置。配置迁移按 04-06-A..D，自定义素材按 07-04，不以内部 SQLite 备份冒充便携配置。
历史 V3_CURRENT 可视化是当时单海报实现快照，不是多海报当前实现证明。
