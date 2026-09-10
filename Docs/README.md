# DesktopUp V2 文档入口

最后更新：2026-09-10。当前分支：`impl/poster-redesign-V3`。
当前阶段：**V3-04-M07 仍在收口**；本次动画白条 R4 已获用户画面确认。不是整个 M07、跨 DPI 或长期稳定性认证。

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

## 产品与边界

正式方向：通用桌面内容收纳、多海报、一个控制中心、一个托盘、独立风格主题、热度排版、AI 辅助定制。游戏图标海报是特色起点，不限制通用文件/目录/应用/Shell 内容。
技术路线沿用 DeskBox 的 Windows 原生经验与 WinUI；不恢复旧 Qt/WebView/Axiom 试验。当前六卡仍是 DEMO ITEMS，未实现真实文件收纳。

V3-01/02/03、V3-04-01/02/03及 M01～M06 已有分项证据；未逐项确认的人工余项集中在 [M07](A_Project/40_Implementation/V3-04/M07_MEASUREMENT_AND_SAFETY.md)，不补填全 PASS。
最近顺序：M07 剩余安全/测量 → 原 V3-04-04 完整中心管理 → 04-05 托盘 → 04-06 配置/恢复及 04-07/08 生命周期 → V3-05 真实内容安全闭环。

需求保留：空配置首次仅一张默认海报；中心/托盘新增，中心/海报菜单删除并确认后果；单文件跨机习惯配置。配置迁移按 04-06-A..D，自定义素材按 07-04，不以内部 SQLite 备份冒充便携配置。
历史 V3_CURRENT 可视化是当时单海报实现快照，不是多海报当前实现证明。
