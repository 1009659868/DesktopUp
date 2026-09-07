# DesktopUp V2 当前执行入口

最后检查：2026-09-07
当前分支：impl/poster-redesign-V3

| 层级 | 权威入口 | 状态 |
| --- | --- | --- |
| 需求 | [七份需求](REQUIREMENTS_INDEX.md) | 通用内容/多海报产品方向已确认 |
| 架构 | [总体架构](../A_Project/20_Architecture/DESKTOPUP_V2_V3_ARCHITECTURE_V1.md) | 设计基线已同步 |
| 合同 | [M02 目标化命令合同](../A_Project/40_Implementation/V3-04/TARGETED_COMMAND_PROJECTION_V2.md) | 数据、实体读集 CAS、三端投影已实现 |
| 模块 | [模块目录](../A_Project/20_Architecture/DESKTOPUP_V2_V3_MODULE_CATALOG_V1.md) | 多实例端口/共享边界已补充 |
| 总计划 | [制作计划](../A_Project/30_Plans/DESKTOPUP_V2_V3_IMPLEMENTATION_PLAN_V1.md) | M04-01 AUTO PASS / MANUAL PENDING；下一开发项M04-02 |
| 中心实际合同 | [M04-01](../A_Project/40_Implementation/V3-04/M04_CONTROL_CENTER_SELECTION.md) | 全目录/目标选择/版本绑定命令已实现，含启动方式 |
| 空间设计 | [碰撞/展开/归位](../A_Project/20_Architecture/POSTER_SPATIAL_LAYOUT_AND_EXPANSION_V1.md) | DESIGN BASELINE / IMPLEMENTATION PLANNED |
| 执行细节 | [细化工作流](../A_Project/30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md) | 输入、依赖、任务、失败与出口 |
| 交接 | [V3-04 HANDOFF](../A_Project/40_Implementation/V3-04/HANDOFF.md) | CURRENT |
| 当前代码 | [src](../../src/) | M02集合/M03会话/M04-01中心选择已实现；空间协同未实现 |
| 当前证据 | [VERIFICATION](../A_Project/40_Implementation/V3-04/VERIFICATION.md) | 176/176；Release0警告0错误；最终3/0真实开窗/退出通过，中心点击人工待确认 |

## 当前动作

当前 M03已实现PosterSessionRegistry、独立会话/取消、共享发现恢复及零目录监听；163/163、Release和0/1/3实际进程/等待中退出为历史自动证据。用户本次确认多海报可正常交互，未逐项确认的普通窗口/Win+D/刷新/Explorer重建证据保留到M04-07集中门；启动说明仍见 [M03合同](../A_Project/40_Implementation/V3-04/M03_SESSION_RECOVERY.md)。不补写M03全项PASS。

已同步用户后续需求：首次无配置一张默认；中心/托盘新增，中心/海报“...”删除并确认后果；单文件跨机习惯配置。M02 已补统一命令和版本绑定的删除摘要，按钮/确认 UI 仍待后续；配置导入导出按04-06-A..D，主题素材复用07-04。详见 [需求画像 §32](../A_Project/10_Requirements/DESKTOPUP_V2_REQUIREMENTS_PROFILE_V1.md)。

M04-01已实现全目录与稳定选择、所选锁定/收展/显隐、运行状态和关闭重开保留；176/176与最终Release三张/零张smoke通过。立即短人工按M04-01合同验证切换不串写、隐藏/显示、中心重开。computer-use初始化与重试均1385，未模拟点击。后续M04-02合同→03碰撞→04保尺寸展开→05推让/归位→06输入/确认→07集中回归；之后M05..07及原04-04复用选择器。本轮无schema迁移或日常数据修改。

## 已完成与本轮边界

- V3-01/02/03 已关闭；V3-04-01/02 SQLite/仓储基础及人工门通过。
- V3-04-03 R1 修复 OutputPath 分隔符导致的 XBF/PRI 资源名错误；真实开窗 smoke、97/97 既有合同测试与用户本次人工确认保留，不再等待旧托盘复测。
- M01 的实体/目录仓储、兼容迁移与故障回归已完成；未迁移用户真实数据库、未操作桌面文件。三张真实内容海报并存不在现有证据中。
- M02 追加 schema v4/持久回执，保留 v1..3；生产调用使用实体读集，三端消费同事务快照。M03 已新增实际多窗口，但没有文件收纳或改写 Host；M02 的历史边界不等于当前实现边界。
- 保留当前未提交的托盘修复代码与已有本地提交；本轮未执行 commit/push。

## 禁止事项

- 不恢复旧 Qt/WebView/Win32/Axiom 试验，也不为多海报整体重写已通过的 Host。
- 不把 DeskBox 参考结果、设计图或结构数组当作 DesktopUp 多实例 PASS。
- 恢复账本、预演与补偿夹具完成前不开放实际文件移动。
- 主屏多海报与多显示器认证分开；不把扩展需求偷偷降回单海报。
