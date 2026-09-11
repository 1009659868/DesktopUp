# DesktopUp V2 当前执行入口

最后检查：2026-09-11（M08-07人工与资源）。分支：`impl/poster-redesign-V3`。

| 层级 | 权威入口 | 当前状态 |
| --- | --- | --- |
| A 需求 | [七份需求](REQUIREMENTS_INDEX.md) | 通用内容、多海报、唯一中心/托盘、独立主题、热度与 AI |
| A 架构 | [总体架构](../A_Project/20_Architecture/DESKTOPUP_V2_V3_ARCHITECTURE_V1.md)、[多海报合同](../A_Project/20_Architecture/GENERAL_CONTENT_MULTI_POSTER_CONTRACT_V1.md) | 当前设计基线 |
| B 模块 | [模块分类](../A_Project/20_Architecture/DESKTOPUP_V2_V3_MODULE_CATALOG_V1.md) | 共享宿主恢复、独立海报会话 |
| C 总计划/任务索引 | [制作计划](../A_Project/30_Plans/DESKTOPUP_V2_V3_IMPLEMENTATION_PLAN_V1.md)、[细化工作流](../A_Project/30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md) | M07已收口 → M08自动收展 → 原04-04 |
| C 当前门 | [M08计划](../A_Project/30_Plans/V3_04_M08_AUTO_POSTURE_PLAN.md)、[M08-07合同](../A_Project/40_Implementation/V3-04/M08_EXPERIENCE_AND_RESOURCE_GATE.md)；前置[M07收口](../A_Project/40_Implementation/V3-04/M07_CLOSEOUT.md) | 07基础三项通过；R1隔离进程/两项点击PASS，R2-A源码修订、R2-B组合前置进行中，余门及有效采样0/36待验；06原生/视觉余门保留 |
| C 本次修复 | [R4](../A_Project/40_Implementation/V3-04/M07_ANIMATION_R4.md) | 白条人工确认；清理与回归记录在文末 |
| C 交接/代码/证据 | [HANDOFF](../A_Project/40_Implementation/V3-04/HANDOFF.md)、[CODE_ANCHORS](../A_Project/40_Implementation/V3-04/CODE_ANCHORS.md)、[VERIFICATION](../A_Project/40_Implementation/V3-04/VERIFICATION.md) | 当前事实与历史证据分开 |

## 当前决定

ABC资料归属见[文档入口](../README.md#abc归属与历史记录)；当前任务状态归细化工作流，登记表只导航，旧版任务卡/日志按对应日期和产物理解。

用户决定统一登记于[A层索引](../A_Project/10_Requirements/REQUIREMENTS_RISKS_AND_OPEN_QUESTIONS.md)：V2-DEFER-005/006按需无排期，V2-DEC-001由M08补齐必要自动收展；V2-TBD-017已于2026-09-11明确确认。本处仅导航，不另行改变决定；本次设置入口完成不代表M08整体体验门已通过，主线不变。

用户确认 R4 看起来解决白条；原位重绘、高频诊断和过时 R2/R3/R4 分叉启动器已收敛。当前动画入口仅 `scripts/start-m07-animation.ps1`，参数仍只在启动读取。
方法论见 [呈现问题排查](../A_Project/20_Architecture/PRESENTATION_DIAGNOSIS_PLAYBOOK_V1.md)。保留根表面容量验证、内容租约、统一恢复及纯回归，不改变空间策略或 SQLite 合同。

## 最近步骤

1. V3-04-M08：[R2分线](../A_Project/40_Implementation/V3-04/M08_INPUT_ARBITRATION_R2.md)外部点击归属已修订；[组合合同/28项模型](../A_Project/40_Implementation/V3-04/M08_EXPANSION_DRAG_CONTRACT.md)通过，边界冲突V2-TBD-018待确认。随后补组合策略重放/因果/CAS，再接生产并验实机；不是短悬停或先回滚再拖。全880/880、独立构建通过，人工入口仍R1；其余人工/36格资源及06实际恢复门保留。
2. V3-04-04：复用目录/选择器，基础桌面预览及新增/复制/重命名/删除，位置/尺寸/内容参数草稿与CAS；均走同一空间服务，不额外发展复杂预览画布。
3. V3-04-05：托盘新增、数量/状态汇总、单张/全部目标与全局隐藏/暂停，保留唯一实例与原双击/退出。
4. V3-04-06-A..D：基础便携配置/恢复；04-07/08生命周期收口后进入V3-05真实内容纵向闭环，先非破坏性添加/展示/打开/定位/移除引用及重启保持；磁盘执行仍以前置恢复账本为门。

“回归主线”指开发计划，**不是未经授权合并 dev/mainline 或 main**。此前检查点53ebdb6已同步；用户于2026-09-11明确授权将当前状态提交并推送至现有V3分支，本次为进行中检查点，不改变未通过门项或发布基线。

## 不可越过的边界

- 当前六卡为 DEMO ITEMS；预写恢复账本/故障夹具前不开放真实文件移动。
- 不把 Win32 返回成功或布局读回当作 GPU 呈现/视觉 PASS；不把主屏验证扩称多屏/混合 DPI 认证。
- 原始故障日志、运行包、日常配置保留；旧固定版本入口不能作为当前版本认证。
