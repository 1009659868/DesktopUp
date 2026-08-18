# C 工作流层占位

C 层按可独立验收的结果建立工作流。当前没有活跃 C 工作流，也没有已批准的首个实现任务。

用户后续若决定进入技术选型，应先在 A/B 层冻结候选评估合同，再建立对应 C 工作流；若用户先要求补充需求或视觉原型，则应按该目标另行建立工作流。不得从旧 V2-P0 名称自动恢复实现。

各工作流目录结构：

```text
C_Workstreams/<Workstream>/
├─ 00_Catalog/TASK_INDEX.md, CODE_ANCHORS.md
├─ 10_Plan/WORKSTREAM_PLAN.md
├─ 20_Log/YYYY-MM/YYYY-MM-DD_topic.md
└─ 30_Sync/COLD_START.md, CURRENT_HANDOFF.md
```
