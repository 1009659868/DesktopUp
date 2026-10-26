# 文档登记与状态来源

本表负责导航，不复制各专项的测试数或待办。全局当前状态见 [CURRENT_STATUS](CURRENT_STATUS.md)；历史阶段 PASS 仅覆盖当时记录范围。

| 文档 | 角色／当前使用方式 |
| --- | --- |
| [需求索引](REQUIREMENTS_INDEX.md) | A 层七份需求及后续用户决定；旧共享／穿透规则不得复活 |
| [总体架构](../A_Project/20_Architecture/DESKTOPUP_V2_V3_ARCHITECTURE_V1.md) | 分开现有实现与未来目标 |
| [模块目录](../A_Project/20_Architecture/DESKTOPUP_V2_V3_MODULE_CATALOG_V1.md) / [src](../../src/README.md) | 设计职责与实际工程对应；不要求按设计名另建类 |
| [通用内容合同](../A_Project/20_Architecture/GENERAL_CONTENT_MULTI_POSTER_CONTRACT_V1.md) | 真实内容、单一归属、文件安全及会话边界 |
| [总计划](../A_Project/30_Plans/DESKTOPUP_V2_V3_IMPLEMENTATION_PLAN_V1.md) / [工作流](../A_Project/30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md) | 当前阶段状态、06 准备和后续范围 |
| [直接拖放／生命周期](../A_Project/40_Implementation/V3-05/DIRECT_TRANSFER_DELIVERY.md) | 原生拖放、跨海报、删海报／卸载归还的已验记录 |
| [真实文件操作](../A_Project/40_Implementation/V3-05/CONTENT_NATIVE_ACTIONS.md) | 新建／命名／真实删除／Win菜单／属性／目录映射 |
| [兼容范围](../A_Project/40_Implementation/V3-05/CONTENT_COMPATIBILITY.md) | 05-13 已验本机范围、05-14差异收口；非全提供方认证 |
| [独立图标](../A_Project/40_Implementation/V3-05/CONTENT_ICON_ASSETS.md) | V1/V2已完成；构造卡片不是已验能力 |
| [增量呈现](../A_Project/40_Implementation/V3-05/POSTER_INCREMENTAL_RENDERING.md) | 布局操作不整组重建图标，悬停让位 |
| [基础架构](../A_Project/40_Implementation/Architecture/FOUNDATION_REMAINDER_PLAN.md) | R1～R8、P1～P3 CLOSED；§30.4剩余成本与锁冲突保留 |
| [当前架构图源](../A_Project/20_Architecture/ArchitectureReview/Foundation-Closed/current.architecture.json) | 2026-10-20收口时业务架构快照；非2026-10-23完整安装拓扑 |
| [尺度与悬停](../A_Project/40_Implementation/V3-04/POSTER_SCALE_AND_HOVER.md) | 连续尺寸、动态下限、扩框／回弹 |
| [拖动呈现优化](../A_Project/40_Implementation/V3-04/POSTER_DRAG_PRESENTATION.md) | 统一调度与纯平移复用；两组人工USER PASS / CLOSED，测量／清理已记录，待授权集成 |
| [回车与方向切换外框修订](../A_Project/40_Implementation/V3-04/CONTROL_CENTER_USABILITY_DELIVERY.md#2026-10-23-浏览方向与外框下限统一) | 两项USER PASS；浏览方向切换可缩回合法尺寸，不改循环 |
| [覆盖升级](../A_Project/40_Implementation/V3-04/INSTALLER_UPGRADE_DELIVERY.md) | 已验前一阶段；其中并行安装规则已被下项替代 |
| [唯一安装／迁移升级](../A_Project/40_Implementation/V3-04/SINGLE_INSTALL_MIGRATION.md) | 最新已验安装规则，保留数据及自启 |
| [即时取证](../A_Project/40_Implementation/V3-04/UNINSTALL_IMMEDIATE_EVIDENCE.md) | 取证能力已验；不证明历史卸载首因 |
| [发布交付](../A_Project/40_Implementation/V3-04/PREVIEW_INSTALLER_DELIVERY.md) | 准确源码／包／tag绑定、自启分项状态及测试清理 |
| [交接](../A_Project/40_Implementation/V3-04/HANDOFF.md) / [锚点](../A_Project/40_Implementation/V3-04/CODE_ANCHORS.md) / [验证](../A_Project/40_Implementation/V3-04/VERIFICATION.md) | C 层按日期保存差量；旧“当前”不覆盖新快照 |
| [Git](../Rules/GIT_STRATEGY.md) / [实施验证](../Rules/IMPLEMENTATION_AND_VALIDATION.md) | 分支授权、发布边界、增量发布说明、精简验证与收尾 |

## 历史资料

旧登记表完整保留为[历史快照](DOCUMENT_REGISTRY_HISTORY_20261023.md)。LR、M08、D0～D5及早期图稿/候选仍可追溯；其历史“待验”不自动代表今天待做，也不能仅凭后续总体通过而把未验环境全部关闭。新任务从当前状态进入相关专项，不沿旧编号重新排队。
