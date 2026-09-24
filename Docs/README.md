# DesktopUp V2 文档入口

## 当前入口（2026-09-24，先验证整体体验闭环）

DEC-021：先用[整体体验评审/E1](A_Project/20_Architecture/CONTENT_LAYOUT_EXPERIENCE_REVIEW_V1.md)验证基础手势、适配整图承接和模式退出覆盖；现有决定暂不推翻，控件细化后置。E1六任务反馈已收清（06c提示后恢复成功）；DEC-023采用double/退出A，DEC-022批准被动极端不足时临时双轴。[操作/空间方案](A_Project/20_Architecture/CONTENT_LAYOUT_OPERATION_SPACE_REVIEW_V1.md)已成文；旧41项原型证据不验证新双轴，不是生产通过。

DEC-019：纯手动/热度自动两种模式，均在桌面海报按住Item并移动即拖，无整理按钮/长按时间前置；纯手动支持指定网格位置和主动留白，控制中心另提供精细微调。自动拖动仍按DEC-018固定顺序位，不能把手动位置也降为序号。

[UI C](A_Project/20_Architecture/CONTENT_TEMPLATE_SCHEMA_UI_C.md)视觉方向保持采纳；原型里的强制“整理”入口已落后于新决定，需定向调整，不重选整套风格。UI C原稿保留；本轮新增独立E1体验试验，未改生产。

DEC-020确认手动局部让位、每次进入手动从当前画面开始、适配后有效编辑保存新构图；[手动构图专项](A_Project/20_Architecture/CONTENT_LAYOUT_MANUAL_COMPOSITION_REVIEW_V1.md)与[组合规则](A_Project/20_Architecture/CONTENT_LAYOUT_PIN_DRAG_REVIEW_V1.md)已同步。[LR-R1下一分项](A_Project/30_Plans/V3_04_L_REMEDIATION_REVIEW_PLAN.md)为操作/空间组合反例、循环接缝/受限恢复、特殊约束兼容及双入口草稿交接；暂不进入控件细化。几何候选未获生产证明。

目标仍是稳定、好用的桌面内容收纳。当前状态归[工作流](A_Project/30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md)，[已发现缺陷](A_Project/40_Implementation/V3-04/CONTENT_LAYOUT_REVIEW_2026-09-23.md)未修，LR-R2/真实Item未开工，旧候选证据不转授当前dirty源码。

## 历史阶段摘要

最新执行（2026-09-17）：[04-L](A_Project/30_Plans/V3_04_L_CONTENT_LAYOUT_PLAN.md)的L01～L06实现和自动门完成：1630合同、最终R4同包35进程门及1/3/6资源基线通过；25指纹/35门人工入口已在PS5.1预检。当前进入L07人工验收，真实输入/像素尚未确认。能力、限制与操作见[交付](A_Project/40_Implementation/V3-04/CONTENT_LAYOUT_DELIVERY.md)。旧延期和BUG处置不变，不补PASS。 最新L07反馈要求单轴/横向循环并简化整理，当前NEEDS REVISION；按DEC-009先讨论交互修订，尚未进入05-01。

历史阶段（2026-09-16，04-06首批）：[04-06专项计划](A_Project/30_Plans/V3_04_06_PORTABLE_PROFILE_PLAN.md)的A/B/C生产接线与D1自动门已通过；完整1452/1452合同、同包portable6＋既有回归12＋离线恢复窗口3门通过，退出资源均为0。分批人工范围见[交付/人工矩阵](A_Project/40_Implementation/V3-04/PORTABLE_PROFILE_DELIVERY.md)，真实另一主机尚未验证。空间不足按V2-TBD-021保留全部配置为“等待安置”，不改显隐意图或尺寸/收展。旧layout包不能验收04-06。

历史入口（2026-09-16，04-06开始前）：05-05托盘人工G1～G8用户确认通过；[布局统一修订](A_Project/40_Implementation/V3-04/LAYOUT_UNIFICATION_DELIVERY.md)完成schema8兼容修复，1315合同/12同包门及旧配置副本迁移通过，外观三项USER PASS。旧layout包/入口与配置保留作证据；当时04-L仅设计、下一步04-06-A，现由DEC-008的L01入口替代，不再按该旧顺序开工。

历史记录（04-04滑块交付，2026-09-15）：[紧凑滑块与轨道预设点](A_Project/40_Implementation/V3-04/CONTROL_CENTER_SLIDER_REFINEMENT.md)1283合同/新同包六组门PASS，真实点选/微调待复验。旧PID30652已退出；当时要求先排查列表、暂不进入05，后续用户已允许BUG-004非阻塞并进入05。旧候选入口不冒充新托盘验证。

历史记录（04-04 UX交付）：1265合同/六组进程门及旧人工反馈见[UX实施记录](A_Project/40_Implementation/V3-04/CONTROL_CENTER_USABILITY_DELIVERY.md)；旧02～06证据见对应交付，不再将旧PID或“下一步UX-05”作为当前指令。M08余门不补PASS。

历史检查点：2026-09-17的04-L自动交付/L07待人工；当时分支impl/poster-redesign-V3，HEAD记录b118936。2026-09-23审查时两本地分支均为9607d4e，差异来自wb未提交工作。2026-09-24用户授权保存该工作，将impl/poster-redesign-V3-wb合入并切换至impl/poster-redesign-V3，再推送两分支；后续工作入口为impl/poster-redesign-V3。该开发检查点不改变LR-R1状态、不继承旧候选PASS；实际提交/远端结果以Git记录为准。

## 常用导航

- [当前执行路径](00_Catalog/ACTIVE_PATH.md)：A → B → C 与最近步骤。
- [当前交接](A_Project/40_Implementation/V3-04/HANDOFF.md)、[代码锚点](A_Project/40_Implementation/V3-04/CODE_ANCHORS.md)、[验证日志](A_Project/40_Implementation/V3-04/VERIFICATION.md)。
- [04-L任务卡](A_Project/30_Plans/V3_04_L_CONTENT_LAYOUT_PLAN.md)保留原计划；当前先看[整改评审计划](A_Project/30_Plans/V3_04_L_REMEDIATION_REVIEW_PLAN.md)。[旧交付/人工矩阵](A_Project/40_Implementation/V3-04/CONTENT_LAYOUT_DELIVERY.md)仅适用对应包，不直接用于验收当前未提交修订。
- [白条修复与清理证据](A_Project/40_Implementation/V3-04/M07_ANIMATION_R4.md)；[同类问题排查方法](A_Project/20_Architecture/PRESENTATION_DIAGNOSIS_PLAYBOOK_V1.md)。
- 历史稳定动画入口：`scripts/start-m07-animation.ps1`；[启动配置](../config/README.md)。R2/R3/独立诊断入口已退出活动目录，旧动画包不能验收04-06。

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

按步推进的每次交付须明确：当前步骤及验证范围、对应计划/证据文档链接、下一步计划；没有实机验证时不将纯模型通过描述为产品交互已修好。

## 产品与边界

正式方向：通用桌面内容收纳、多海报、一个控制中心、一个托盘、独立风格主题、热度排版、AI 辅助定制。游戏图标海报是特色起点，不限制通用文件/目录/应用/Shell 内容。
技术路线沿用 DeskBox 的 Windows 原生经验与 WinUI；不恢复旧 Qt/WebView/Axiom 试验。当前内容仍是 DEMO ITEMS，04-L支持异尺寸样例与有界压力数据，未实现真实文件收纳。

V3-01/02/03、V3-04-01/02/03及M01～M06已有分项证据；本轮[M07](A_Project/40_Implementation/V3-04/M07_MEASUREMENT_AND_SAFETY.md)集中S1～S5安全和测量均已通过，功能欠项不代填完成：减少动效/手动连点队列按需登记，自动收展转入M08。
最近顺序（V2-DEC-008）：04-L01合同/样例 → L02纯布局 → L03～07持久化、呈现、编辑及验证 → V3-05真实内容安全闭环。04-06剩余验收和04-07/08专项收口延期，不是L开工前置；05-03取消，M08余门保留，不自动重启标题缺陷循环。
[按需优化登记](A_Project/30_Plans/ON_DEMAND_OPTIMIZATIONS.md)没有排期，不阻塞上述主线。当前已有纯模型、设置数据、原生观察与共享自动调度/空间事务；已有最小设置UI，完整体验门仍待06/07，当前R1普通原生自动smoke已补验，真实恢复与视觉余门待续。

需求保留：空配置首次仅一张默认海报；中心/托盘新增，中心/海报菜单删除并确认后果；单文件跨机习惯配置。配置迁移按 04-06-A..D，自定义素材按 07-04，不以内部 SQLite 备份冒充便携配置。
历史 V3_CURRENT 可视化是当时单海报实现快照，不是多海报当前实现证明。
