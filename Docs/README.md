# DesktopUp V2 文档入口

状态：`V3-04-M07 IN PROGRESS — CONTRACT/BUILD PASS；CURRENT-ENVIRONMENT / MANUAL PENDING`

当前[M07测量与安全门](A_Project/40_Implementation/V3-04/M07_MEASUREMENT_AND_SAFETY.md)：按用户决定直接在当前环境推进，入口兼容Windows PowerShell 5.1/PowerShell 7，权限只记录不拦截。5.1真实三海报/单中心/单托盘启动与干净退出通过，合同411/411；先运行人工入口完成S1～S5，不要求先跑资源矩阵。M07尚未关闭，完整基线待补；M05/M06结论保留，动画与性能优化延期。

M04-06历史输入/确认证据见 [本项合同](A_Project/40_Implementation/V3-04/M04_INPUT_CONFIRMATION.md)：313/313及三海报确认/退出与空间回归通过；当前R2产物和人工结果以上方入口为准。

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

## 当前产品与阶段

2026-09-09当前：M05/M06关闭；M07已修正为当前环境可运行，先做S1～S5人工安全门，再收口测量证据/缺口。权限不再阻塞本轮，不等于普通用户环境已获认证。M07通过后再原04-04管理与V3-05真实Item。以下为阶段历史，不代表当前下一项。

M04-01阶段历史：本轮最新：已完成 [DeskBox空间计算吸收记录](A_Project/40_Implementation/V3-04/DESKBOX_SPATIAL_REFERENCE.md) 与 [M04-01中心列表/目标切换](A_Project/40_Implementation/V3-04/M04_CONTROL_CENTER_SELECTION.md)；176/176、Release0警告0错误、最终三张/零张真实进程通过。中心可按稳定ID切目标、锁定/收展/显隐并在关闭重开后保留进程内选择；新增管理/完整预览仍留原04-04。computer-use环境1385导致实际点击未执行，人工门待确认。下一开发项M04-02空间合同；碰撞/展开/推动/归位未接入。

正式关键词：通用桌面内容收纳、多海报、一个控制中心、一个托盘、自定义独立风格化主题、热度排版、AI 辅助定制。游戏图标海报是起点和特色场景，不再限制通用文件/目录/应用/Shell 内容。

V3-01/02/03、V3-04-01/02 保留已完成结论。V3-04-03 R1 托盘修复已获用户人工 PASS；M01 数据门通过。M02 保留147/147历史证据，用户同意进入M03但未补写逐项人工PASS。本轮 M03 163/163，Release 0 warning / 0 error，最终产物0/1/3实际进程及恢复等待中退出 smoke 均通过；短人工门仍待确认。

七份需求、[通用多海报合同](A_Project/20_Architecture/GENERAL_CONTENT_MULTI_POSTER_CONTRACT_V1.md) 与 [逐项细化工作流](A_Project/30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md) 保持权威。M01 实体/目录与 M02 目标命令/实体读集 CAS/三端集合投影均已落地；[M02 实际合同](A_Project/40_Implementation/V3-04/TARGETED_COMMAND_PROJECTION_V2.md) 说明 schema v4、持久回执和零目录。M03 已实现目录驱动的多个真实海报会话；管理 UI 仍待后续，详见 [M03](A_Project/40_Implementation/V3-04/M03_SESSION_RECOVERY.md)。

用户已确认多海报基本交互；未逐项确认的核心人工门保留，不补写全项PASS。上一轮 [空间协同与展开设计](A_Project/20_Architecture/POSTER_SPATIAL_LAYOUT_AND_EXPANSION_V1.md) 明确锁定固定、条件归位和保原尺寸先下后上；本轮中心缺口已在M04-01实现。后续空间/输入、参数及原04-04完整中心复用现有选择器；真实文件主线紧随，基础发行包不等AI。执行入口见 [ACTIVE_PATH](00_Catalog/ACTIVE_PATH.md)。

V3_CURRENT 可视化保留为当时单海报代码快照，不作为多海报新设计或实现证据。

用户补充已写入 [画像 §32](A_Project/10_Requirements/DESKTOPUP_V2_REQUIREMENTS_PROFILE_V1.md#32-首次启动管理入口与跨主机配置)：首次仅一张默认；中心/托盘新增、中心/海报“...”删除确认；单文件跨主机习惯配置。M02 已补创建/删除命令与版本绑定预览，M04-06现已接中心/海报删除按钮与共用确认；新增/导入导出入口仍按后续计划；配置导入导出在04-06-A..D，自定义主题载荷复用07-04。
