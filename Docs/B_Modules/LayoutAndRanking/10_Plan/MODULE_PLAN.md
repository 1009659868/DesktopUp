# Layout and Ranking 模块计划

最后检查：2026-08-04

状态：`PLANNED`

上层计划：[`DESKTOPUP_V1_IMPLEMENTATION_PLAN.md`](../../../A_Project/10_Plan/DESKTOPUP_V1_IMPLEMENTATION_PLAN.md)

## 1. 责任

拥有启动统计、最近使用、活跃度衰减、排序得分、稳定排序、对数缩放、行式弹性流、子列填充、三层控制、锚定、拖拽和跳变。

## 2. C 工作流

| C 工作流 | 结果 | 依赖 | 状态 |
| --- | --- | --- | --- |
| Launch Telemetry Model (`C-LTM`) | 统计、最近使用、衰减和迁移 | C-LRC | `PLANNED` |
| Ranking Engine (`C-RNK`) | 可复现得分、稳定排序和跳变衰减 | C-LTM | `PLANNED` |
| Elastic Layout Flow (`C-ELF`) | 弹性流、子列填充、响应式宽度和快照 | C-RNK, C-PSI | `PLANNED` |
| Pin Drag and Scaling (`C-PDS`) | 三层控制、锚定、拖拽、缩放和动画 | C-ELF | `PLANNED` |

## 3. 模块完成信号

- 相同输入产生确定性排序和布局；
- 50%–300% 对数缩放与衰减可测试；
- 自动、锚定+自动、全手动行为无冲突；
- 暂停和收起不破坏数据，动画不引入持续空闲帧。
