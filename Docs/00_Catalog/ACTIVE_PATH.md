# 当前 A → B → C 执行路径

本页只导航；当前进度、下一步和携带项以 [CURRENT_STATUS](CURRENT_STATUS.md)为准。不要把旧阶段的“下一步”当成当前授权。

## A：需求和职责

1. [需求索引](REQUIREMENTS_INDEX.md) → 本次受影响的需求章节与[决定／未决项](../A_Project/10_Requirements/REQUIREMENTS_RISKS_AND_OPEN_QUESTIONS.md)。
2. [总体架构](../A_Project/20_Architecture/DESKTOPUP_V2_V3_ARCHITECTURE_V1.md) → [模块职责](../A_Project/20_Architecture/DESKTOPUP_V2_V3_MODULE_CATALOG_V1.md) → [源码导航](../../src/README.md)。
3. 热度任务重点读[内容活跃度需求](../A_Project/10_Requirements/CONTENT_ITEM_ACTIVITY_LAYOUT_V1.md)与[通用内容合同](../A_Project/20_Architecture/GENERAL_CONTENT_MULTI_POSTER_CONTRACT_V1.md)，同时应用后续单一归属及连续尺寸修订。

## B：计划和退出标准

- [总计划](../A_Project/30_Plans/DESKTOPUP_V2_V3_IMPLEMENTATION_PLAN_V1.md)：阶段状态与后续顺序。
- [细化工作流](../A_Project/30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md)：05 状态表、06 真实热度已验链路及后续平台范围。
- [基础架构收口／成本携带](../A_Project/40_Implementation/Architecture/FOUNDATION_REMAINDER_PLAN.md#30-p2p3收口后的最终架构与成本复审2026-10-20)：已关闭，不重开 R1。
- [Git 规则](../Rules/GIT_STRATEGY.md)、[实施与验证](../Rules/IMPLEMENTATION_AND_VALIDATION.md)。

## C：实际证据与接手

当前在`impl/07-canvas-ai-contracts`：J0及源码目录整理已收口；旧J1原型和前版原生工作台未通过记录保留；新交互稿已认可，按[原生工作台实施清单](../A_Project/40_Implementation/V3-07/CANVAS_J1_STUDIO_IMPLEMENTATION.md)完成画面与功能同步对齐修订，review02待集中人工；起点develop@ecf28fe，不合并。既有模型／性能与main@43a77e7、preview-20261026.2发布见[回执](../A_Project/40_Implementation/V3-06/MODEL_PROTOCOL_RELEASE.md)。原[模型验收](../A_Project/40_Implementation/V3-06/PLUGGABLE_MODEL_DELIVERY.md)与[成本J1／T1证据](../A_Project/40_Implementation/V3-07/JOINT_COST_AND_COMPONENTS.md)保留；未验T1源码只在功能分支61819dd。

依次读[当前交接](../A_Project/40_Implementation/V3-04/HANDOFF.md)、[代码锚点](../A_Project/40_Implementation/V3-04/CODE_ANCHORS.md)、[最新验证记录](../A_Project/40_Implementation/V3-04/VERIFICATION.md)，再打开当前任务的专项记录；不默认重读全库或重跑累计测试。

2026-10-28按用户授权仅同步本功能分支，源码检查点`d1af0e5`已推送（纯移动另存`2016668`）；不合并、不发布，也不把Git同步视为J1人工通过。远端已建立同名跟踪分支，下文旧“未提交”属于同步前时点。

本次先读[J1未通过记录](../A_Project/40_Implementation/V3-07/CANVAS_J1_KERNEL.md)与[创作设计重审](../A_Project/40_Implementation/V3-07/CANVAS_J1_CREATION_REDESIGN.md)，再读[联合实施计划](../A_Project/30_Plans/POSTER_CANVAS_ENGINE_DESIGN_PLAN.md) → [0.2设计协议](../A_Project/20_Architecture/CanvasContracts/CONTRACT.md)。产品代码位置见[源码导航](../../src/README.md)；当前新增源码按功能归位、不合并，旧发布、成本与未定因继续保留。

旧导航和阶段队列保留在[历史快照](ACTIVE_PATH_HISTORY_20261023.md)，仅用于追溯。
