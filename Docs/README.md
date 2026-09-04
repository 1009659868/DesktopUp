# DesktopUp V2 文档入口

状态：`V3-04 AUTHORITATIVE STATE — V3-04-03 THREE-SURFACE PROJECTION IMPLEMENTED`

## 权威顺序

1. 用户最新明确决定；
2. [七份冻结需求](00_Catalog/REQUIREMENTS_INDEX.md)；
3. [V3 技术路线与总体架构](A_Project/20_Architecture/DESKTOPUP_V2_V3_ARCHITECTURE_V1.md)；
4. [V3 模块分类与端口目录](A_Project/20_Architecture/DESKTOPUP_V2_V3_MODULE_CATALOG_V1.md)；
5. [V3 制作计划](A_Project/30_Plans/DESKTOPUP_V2_V3_IMPLEMENTATION_PLAN_V1.md)；
6. [V3-04 实现记录](A_Project/40_Implementation/V3-04/VERIFICATION.md)；
7. 后续每阶段产生的实现合同、验证记录和交接材料。

需求定义“必须得到什么行为”，架构定义“当前探索分支准备如何实现”，计划定义“按什么顺序交付与验证”。发生冲突时，下层文档不能覆盖上层文档。

## V3 重置结果

- 保留 `Docs/A_Project/10_Requirements/` 下的完整需求画像、六份配套权威/支持文档和一致性审计；
- 删除旧 Qt、QPainter、Qt Quick、Win32 Host、Axiom 原型及测试证据；
- 删除旧技术选型和旧生产架构，避免它们继续作为默认实现；
- 新路线以 DeskBox 的成熟 Windows 桌面窗口、文件收纳与 Shell 交互经验为基线；
- DesktopUp 独有的透明空白穿透、海报排序、主题平台和 AI 能力仍独立设计。

## 当前阶段

V3-01、V3-02、V3-03 已关闭。V3-04-01/02 的 SQLite、Poster/Settings repository 与恢复门完成；V3-04-03 已建立 command/query、单事务 aggregate snapshot 和 Poster/Control Center/Tray 同 revision projection。Release 为 0 warning / 0 error，合同测试 97/97，schema v1→v2 真实迁移及启动/退出 smoke 通过。当前执行 V3-04-03 人工三端门，通过后进入 V3-04-04 控制中心桌面预览与编辑。当前执行入口见 [ACTIVE_PATH](00_Catalog/ACTIVE_PATH.md)。
