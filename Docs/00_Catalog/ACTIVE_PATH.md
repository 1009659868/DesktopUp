# DesktopUp V2 当前执行入口

最后检查：2026-09-10。分支：`impl/poster-redesign-V3`。

| 层级 | 权威入口 | 当前状态 |
| --- | --- | --- |
| A 需求 | [七份需求](REQUIREMENTS_INDEX.md) | 通用内容、多海报、唯一中心/托盘、独立主题、热度与 AI |
| A 架构 | [总体架构](../A_Project/20_Architecture/DESKTOPUP_V2_V3_ARCHITECTURE_V1.md)、[多海报合同](../A_Project/20_Architecture/GENERAL_CONTENT_MULTI_POSTER_CONTRACT_V1.md) | 当前设计基线 |
| B 模块 | [模块分类](../A_Project/20_Architecture/DESKTOPUP_V2_V3_MODULE_CATALOG_V1.md) | 共享宿主恢复、独立海报会话 |
| C 总计划/任务索引 | [制作计划](../A_Project/30_Plans/DESKTOPUP_V2_V3_IMPLEMENTATION_PLAN_V1.md)、[细化工作流](../A_Project/30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md) | M07 收口后原04-04 |
| C 当前门 | [M07测量/安全](../A_Project/40_Implementation/V3-04/M07_MEASUREMENT_AND_SAFETY.md) | 剩余安全项/完整36次资源采样未完成 |
| C 本次修复 | [R4](../A_Project/40_Implementation/V3-04/M07_ANIMATION_R4.md) | 白条人工确认；清理与回归记录在文末 |
| C 交接/代码/证据 | [HANDOFF](../A_Project/40_Implementation/V3-04/HANDOFF.md)、[CODE_ANCHORS](../A_Project/40_Implementation/V3-04/CODE_ANCHORS.md)、[VERIFICATION](../A_Project/40_Implementation/V3-04/VERIFICATION.md) | 当前事实与历史证据分开 |

## 当前决定

用户确认 R4 看起来解决白条；原位重绘、高频诊断和过时 R2/R3/R4 分叉启动器已收敛。当前动画入口仅 `scripts/start-m07-animation.ps1`，参数仍只在启动读取。
方法论见 [呈现问题排查](../A_Project/20_Architecture/PRESENTATION_DIAGNOSIS_PLAYBOOK_V1.md)。保留根表面容量验证、内容租约、统一恢复及纯回归，不改变空间策略或 SQLite 合同。

## 最近步骤

1. M07-02/03/04：将剩余人工/测量场景固定到本次稳定产物，保留 S1/S2 与菜单/收展已获确认的证据；补 S3 安全余项、S4 确认失效、S5 删除至零/重启及 18格×2 资源基线，记录预算差距。
2. V3-04-04：复用目录/选择器，完整桌面预览，新增/复制/重命名/删除，位置/尺寸/内容参数草稿与 CAS；数值编辑/新增均经同一空间服务。
3. V3-04-05：托盘新增、数量/状态汇总、单张/全部目标与全局隐藏/暂停，保留唯一实例与原双击/退出。
4. V3-04-06-A..D：基础便携配置/恢复；04-07/08 生命周期收口后进入 V3-05-01 内容实体合同。

“回归主线”指开发计划，**不是未经授权合并 dev/mainline 或 main**。本轮只同步现有探索分支。

## 不可越过的边界

- 当前六卡为 DEMO ITEMS；预写恢复账本/故障夹具前不开放真实文件移动。
- 不把 Win32 返回成功或布局读回当作 GPU 呈现/视觉 PASS；不把主屏验证扩称多屏/混合 DPI 认证。
- 原始故障日志、运行包、日常配置保留；旧固定版本入口不能作为当前版本认证。
