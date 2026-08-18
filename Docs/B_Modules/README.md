# B 模块层占位

B 层按能力模块建立计划与交接。当前没有活跃 B 模块：需求与设计迁移仍处于 A 层，技术选型和实现尚未由用户激活。

`Docs/00_Catalog/B_MODULE_INDEX.md` 中的条目只是未来能力地图，不代表执行顺序或技术依赖已经冻结。激活任一模块前，必须依据完整需求画像重新确认模块边界、验收门和与其他模块的契约。

各模块目录结构：

```text
B_Modules/<ModuleName>/
├─ 00_Catalog/C_WORKSTREAM_INDEX.md
├─ 10_Plan/MODULE_PLAN.md
├─ 20_Log/MODULE_LOG.md
└─ 30_Sync/COLD_START.md, CURRENT_HANDOFF.md
```
